using System;
using System.Collections.Generic;
using System.Text;
using Outpost.Sim;
using TMPro;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Automatically handles RTL line wrapping and horizontal alignment for TextMeshProUGUI labels
    /// when Arabic is active. Reacts only to TMPro TEXT_CHANGED_EVENT and pre-render canvas passes
    /// with zero per-frame allocations when text is idle.
    /// </summary>
    public static class ArabicTextReflow
    {
        private struct CacheEntry
        {
            public string RawText;
            public string WrappedText;
            public float QuantizedWidth;
            public TextAlignmentOptions OriginalAlignment;
            public bool HadOriginalAlignment;
        }

        private static readonly Dictionary<EntityId, CacheEntry> Cache = new Dictionary<EntityId, CacheEntry>(256);
        private static readonly Queue<TextMeshProUGUI> PendingQueue = new Queue<TextMeshProUGUI>(64);
        private static readonly HashSet<EntityId> QueuedIds = new HashSet<EntityId>();
        private static bool _initialized;
        private static bool _rewriting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTmpTextChanged);
            Canvas.willRenderCanvases += ProcessPendingReflow;
            Localization.OnLanguageChanged += OnLanguageChanged;
        }

        private static void OnLanguageChanged(Language _)
        {
            // Clear line-wrap cache so switched text reflows cleanly at its new copy
            Cache.Clear();
        }

        private static void OnTmpTextChanged(UnityEngine.Object obj)
        {
            if (_rewriting) return;
            if (!(obj is TextMeshProUGUI tmp) || tmp == null) return;

            EntityId id = tmp.GetEntityId();
            if (Cache.Count > 1024)
                Cache.Clear();

            if (!Cache.TryGetValue(id, out var entry))
            {
                entry = new CacheEntry
                {
                    OriginalAlignment = tmp.alignment,
                    HadOriginalAlignment = true
                };
            }

            string text = tmp.text ?? string.Empty;
            if (!string.Equals(text, entry.WrappedText, StringComparison.Ordinal))
            {
                entry.RawText = text;
                entry.WrappedText = null;
                entry.QuantizedWidth = -1f;
            }
            Cache[id] = entry;

            if (QueuedIds.Add(id))
                PendingQueue.Enqueue(tmp);
        }

        private static void ProcessPendingReflow()
        {
            if (PendingQueue.Count == 0) return;

            bool isArabic = Localization.CurrentLanguage == Language.Arabic;
            int safety = PendingQueue.Count + 8;

            _rewriting = true;
            try
            {
                while (PendingQueue.Count > 0 && safety-- > 0)
                {
                    var tmp = PendingQueue.Dequeue();
                    if (tmp == null) continue;
                    EntityId id = tmp.GetEntityId();
                    QueuedIds.Remove(id);

                    if (!tmp.gameObject.activeInHierarchy) continue;
                    if (!Cache.TryGetValue(id, out var entry)) continue;

                    string sourceText = entry.RawText ?? tmp.text ?? string.Empty;
                    bool hasArabic = isArabic && ArabicShaper.ContainsArabic(sourceText);

                    if (!hasArabic)
                    {
                        if (entry.HadOriginalAlignment && tmp.alignment != entry.OriginalAlignment)
                            tmp.alignment = entry.OriginalAlignment;
                        continue;
                    }

                    // Flip left alignments to right for natural RTL layout, preserving centered/right labels
                    if (entry.HadOriginalAlignment)
                    {
                        TextAlignmentOptions rtlAlign = ToRtlAlignment(entry.OriginalAlignment);
                        if (tmp.alignment != rtlAlign)
                            tmp.alignment = rtlAlign;
                    }

                    if (tmp.textWrappingMode == TextWrappingModes.NoWrap)
                        continue;

                    float width = tmp.rectTransform != null ? tmp.rectTransform.rect.width : 0f;
                    if (width <= 24f)
                        continue;

                    float qWidth = Mathf.Round(width);
                    if (entry.WrappedText != null && Mathf.Abs(qWidth - entry.QuantizedWidth) < 1f)
                        continue;

                    string wrapped = WrapVisualRtlText(tmp, sourceText, Mathf.Max(20f, width - 6f));
                    entry.WrappedText = wrapped;
                    entry.QuantizedWidth = qWidth;
                    Cache[id] = entry;

                    if (!string.Equals(tmp.text, wrapped, StringComparison.Ordinal))
                        tmp.text = wrapped;
                }
            }
            finally
            {
                _rewriting = false;
            }
        }

        private static string WrapVisualRtlText(TextMeshProUGUI tmp, string visualText, float maxWidth)
        {
            if (string.IsNullOrEmpty(visualText)) return visualText;
            if (tmp.GetPreferredValues(visualText).x <= maxWidth)
                return visualText;

            string[] paragraphs = visualText.Split('\n');
            var sb = new StringBuilder(visualText.Length + 16);
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) sb.Append('\n');
                string para = paragraphs[p];
                if (string.IsNullOrEmpty(para) || tmp.GetPreferredValues(para).x <= maxWidth || !ArabicShaper.ContainsArabic(para))
                {
                    sb.Append(para);
                    continue;
                }
                sb.Append(WrapSingleVisualRtlParagraph(tmp, para, maxWidth));
            }
            return sb.ToString();
        }

        private static string WrapSingleVisualRtlParagraph(TextMeshProUGUI tmp, string para, float maxWidth)
        {
            // Tokenize spaces outside <...> rich-text tags.
            // Because ArabicShaper emits visual order [LastWord ... FirstWord], walking tokens from the END
            // iterates logical reading order (FirstWord -> LastWord) while building each line right-to-left.
            var tokens = TokenizeOutsideTags(para);
            if (tokens.Count <= 1) return para;

            var lines = new List<string>(4);
            var currentTokens = new List<string>(8);
            string currentCandidate = string.Empty;

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                string tok = tokens[i];
                string candidate = currentTokens.Count == 0 ? tok : (tok + " " + currentCandidate);
                if (currentTokens.Count > 0 && tmp.GetPreferredValues(candidate).x > maxWidth)
                {
                    lines.Add(currentCandidate);
                    currentTokens.Clear();
                    currentTokens.Add(tok);
                    currentCandidate = tok;
                }
                else
                {
                    currentTokens.Insert(0, tok);
                    currentCandidate = candidate;
                }
            }
            if (currentTokens.Count > 0)
                lines.Add(currentCandidate);

            return string.Join("\n", lines);
        }

        private static List<string> TokenizeOutsideTags(string text)
        {
            var tokens = new List<string>(16);
            int start = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (c == ' ' && !inTag)
                {
                    if (i > start)
                        tokens.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < text.Length)
                tokens.Add(text.Substring(start));
            return tokens;
        }

        private static TextAlignmentOptions ToRtlAlignment(TextAlignmentOptions align)
        {
            switch (align)
            {
                case TextAlignmentOptions.TopLeft: return TextAlignmentOptions.TopRight;
                case TextAlignmentOptions.MidlineLeft: return TextAlignmentOptions.MidlineRight;
                case TextAlignmentOptions.BottomLeft: return TextAlignmentOptions.BottomRight;
                case TextAlignmentOptions.Left: return TextAlignmentOptions.Right;
                default: return align;
            }
        }
    }
}
