using System;
using System.Collections.Generic;
using System.Text;

namespace Outpost.Sim
{
    /// <summary>
    /// Pure-C# WebGL-safe Arabic contextual letter shaper and visual BiDi line formatter for TextMeshPro.
    /// Converts logical UTF-16 Arabic strings into Arabic Presentation Forms-B with connected forms,
    /// preserves TMP rich-text tags (&lt;color=...&gt;, &lt;b&gt;, etc.), and orders RTL/LTR runs visually.
    /// </summary>
    public static class ArabicShaper
    {
        private struct GlyphForms
        {
            public readonly char Isolated;
            public readonly char Final;
            public readonly char Initial;
            public readonly char Medial;

            public GlyphForms(char isolated, char final, char initial, char medial)
            {
                Isolated = isolated;
                Final = final;
                Initial = initial;
                Medial = medial;
            }
        }

        private struct StyledChar
        {
            public char Ch;
            public string[] OpenTags;
            public string[] CloseTags;
            public sbyte Dir; // 1 = RTL, -1 = LTR, 0 = Neutral
        }

        private struct LogicalRun
        {
            public bool IsRtl;
            public int Start;
            public int Count;
        }

        private static readonly Dictionary<char, GlyphForms> Map = new Dictionary<char, GlyphForms>
        {
            // Non-left-joining (Isolated & Final only)
            { '\u0621', new GlyphForms('\uFE80', '\u0000', '\u0000', '\u0000') }, // Hamza
            { '\u0622', new GlyphForms('\uFE81', '\uFE82', '\u0000', '\u0000') }, // Alef Madda
            { '\u0623', new GlyphForms('\uFE83', '\uFE84', '\u0000', '\u0000') }, // Alef Hamza Above
            { '\u0624', new GlyphForms('\uFE85', '\uFE86', '\u0000', '\u0000') }, // Waw Hamza
            { '\u0625', new GlyphForms('\uFE87', '\uFE88', '\u0000', '\u0000') }, // Alef Hamza Below
            { '\u0627', new GlyphForms('\uFE8D', '\uFE8E', '\u0000', '\u0000') }, // Alef
            { '\u0629', new GlyphForms('\uFE93', '\uFE94', '\u0000', '\u0000') }, // Teh Marbuta
            { '\u062F', new GlyphForms('\uFEA9', '\uFEAA', '\u0000', '\u0000') }, // Dal
            { '\u0630', new GlyphForms('\uFEAB', '\uFEAC', '\u0000', '\u0000') }, // Thal
            { '\u0631', new GlyphForms('\uFEAD', '\uFEAE', '\u0000', '\u0000') }, // Reh
            { '\u0632', new GlyphForms('\uFEAF', '\uFEB0', '\u0000', '\u0000') }, // Zain
            { '\u0648', new GlyphForms('\uFEED', '\uFEEE', '\u0000', '\u0000') }, // Waw
            { '\u0649', new GlyphForms('\uFEEF', '\uFEF0', '\u0000', '\u0000') }, // Alef Maksura

            // Dual-joining (Isolated, Final, Initial, Medial)
            { '\u0626', new GlyphForms('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C') }, // Yeh Hamza
            { '\u0628', new GlyphForms('\uFE8F', '\uFE90', '\uFE91', '\uFE92') }, // Beh
            { '\u062A', new GlyphForms('\uFE95', '\uFE96', '\uFE97', '\uFE98') }, // Teh
            { '\u062B', new GlyphForms('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C') }, // Theh
            { '\u062C', new GlyphForms('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0') }, // Jeem
            { '\u062D', new GlyphForms('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4') }, // Hah
            { '\u062E', new GlyphForms('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8') }, // Khah
            { '\u0633', new GlyphForms('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4') }, // Seen
            { '\u0634', new GlyphForms('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8') }, // Sheen
            { '\u0635', new GlyphForms('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC') }, // Sad
            { '\u0636', new GlyphForms('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0') }, // Dad
            { '\u0637', new GlyphForms('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4') }, // Tah
            { '\u0638', new GlyphForms('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8') }, // Zah
            { '\u0639', new GlyphForms('\uFEC9', '\uFECA', '\uFECB', '\uFECC') }, // Ain
            { '\u063A', new GlyphForms('\uFECD', '\uFECE', '\uFECF', '\uFED0') }, // Ghain
            { '\u0641', new GlyphForms('\uFED1', '\uFED2', '\uFED3', '\uFED4') }, // Feh
            { '\u0642', new GlyphForms('\uFED5', '\uFED6', '\uFED7', '\uFED8') }, // Qaf
            { '\u0643', new GlyphForms('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC') }, // Kaf
            { '\u0644', new GlyphForms('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0') }, // Lam
            { '\u0645', new GlyphForms('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4') }, // Meem
            { '\u0646', new GlyphForms('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8') }, // Noon
            { '\u0647', new GlyphForms('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC') }, // Heh
            { '\u064A', new GlyphForms('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4') }, // Yeh
            { '\u06CC', new GlyphForms('\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF') }, // Farsi Yeh
        };

        /// <summary>Returns true if the string contains any standard Arabic or presentation-form characters.</summary>
        public static bool ContainsArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\u0600' && c <= '\u06FF') ||
                    (c >= '\u0750' && c <= '\u077F') ||
                    (c >= '\uFB50' && c <= '\uFDFF') ||
                    (c >= '\uFE70' && c <= '\uFEFF'))
                    return true;
            }
            return false;
        }

        /// <summary>Returns true if the string already contains shaped Arabic Presentation Forms glyphs.</summary>
        public static bool ContainsPresentationForms(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF'))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Shapes logical Arabic text into visual RTL order with Presentation Forms-B glyphs,
        /// leaving rich-text tags and LTR segments (numbers, Latin symbols, units) intact.
        /// Safe to call on strings that already have presentation forms (returns unchanged).
        /// </summary>
        public static string Shape(string logicalText)
        {
            if (string.IsNullOrEmpty(logicalText) || !ContainsArabic(logicalText))
                return logicalText ?? string.Empty;

            if (ContainsPresentationForms(logicalText))
                return logicalText;

            string[] lines = logicalText.Split('\n');
            if (lines.Length == 1)
                return ShapeSingleLine(lines[0]);

            var sb = new StringBuilder(logicalText.Length + 16);
            var openTags = new List<string>(4);
            var closeTags = new List<string>(4);

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(ShapeSingleLineWithInheritedTags(lines[i], openTags, closeTags));
            }
            return sb.ToString();
        }

        private static string ShapeSingleLine(string line)
        {
            if (string.IsNullOrEmpty(line) || !ContainsArabic(line))
                return line ?? string.Empty;

            var openTags = new List<string>(4);
            var closeTags = new List<string>(4);
            return ShapeSingleLineWithInheritedTags(line, openTags, closeTags);
        }

        private static string ShapeSingleLineWithInheritedTags(string line, List<string> openTags, List<string> closeTags)
        {
            var chars = ParseStyledChars(line, openTags, closeTags);
            if (chars.Count == 0)
                return string.Empty;

            // Even if line has no Arabic characters itself, inherited tags remain tracked above.
            bool hasArabic = false;
            for (int i = 0; i < chars.Count; i++)
            {
                if (IsArabicChar(chars[i].Ch)) { hasArabic = true; break; }
            }

            if (!hasArabic)
            {
                var plainSb = new StringBuilder(line.Length);
                AppendTags(plainSb, openTags, forward: true);
                for (int i = 0; i < chars.Count; i++)
                    plainSb.Append(chars[i].Ch);
                AppendTags(plainSb, closeTags, forward: false);
                return line;
            }

            var shaped = ApplyContextualJoining(chars);
            ResolveDirections(shaped);

            var runs = BuildRuns(shaped);
            var sb = new StringBuilder(line.Length + 16);
            string[] activeOpen = Array.Empty<string>();
            string[] activeClose = Array.Empty<string>();

            // Visual RTL base order: emit runs from last logical run to first logical run
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                var run = runs[r];
                if (run.IsRtl)
                {
                    for (int k = run.Start + run.Count - 1; k >= run.Start; k--)
                    {
                        var sc = shaped[k];
                        if (sc.Ch == ' ' && activeClose.Length > 0)
                        {
                            for (int t = activeClose.Length - 1; t >= 0; t--) sb.Append(activeClose[t]);
                            activeOpen = Array.Empty<string>();
                            activeClose = Array.Empty<string>();
                            sb.Append(' ');
                            continue;
                        }
                        SyncTagDiff(sb, ref activeOpen, ref activeClose, sc.OpenTags, sc.CloseTags);
                        sb.Append(MirrorRtlPunctuation(sc.Ch));
                    }
                }
                else
                {
                    for (int k = run.Start; k < run.Start + run.Count; k++)
                    {
                        var sc = shaped[k];
                        if (sc.Ch == ' ' && activeClose.Length > 0)
                        {
                            for (int t = activeClose.Length - 1; t >= 0; t--) sb.Append(activeClose[t]);
                            activeOpen = Array.Empty<string>();
                            activeClose = Array.Empty<string>();
                            sb.Append(' ');
                            continue;
                        }
                        SyncTagDiff(sb, ref activeOpen, ref activeClose, sc.OpenTags, sc.CloseTags);
                        sb.Append(sc.Ch);
                    }
                }
            }

            // Close any remaining tags at line end
            for (int i = activeClose.Length - 1; i >= 0; i--)
                sb.Append(activeClose[i]);

            return sb.ToString();
        }

        private static List<StyledChar> ParseStyledChars(string line, List<string> openTags, List<string> closeTags)
        {
            var list = new List<StyledChar>(line.Length);
            int len = line.Length;
            int i = 0;
            while (i < len)
            {
                char c = line[i];
                if (c == '<')
                {
                    int end = line.IndexOf('>', i + 1);
                    if (end > i)
                    {
                        string tag = line.Substring(i, end - i + 1);
                        if (IsSupportedTmpRichTag(tag, out bool isClosing, out string tagName))
                        {
                            if (isClosing)
                            {
                                PopMatchingTag(openTags, closeTags, tagName);
                            }
                            else
                            {
                                openTags.Add(tag);
                                closeTags.Add($"</{tagName}>");
                            }
                            i = end + 1;
                            continue;
                        }
                    }
                }

                if (c == '\r' || IsArabicDiacritic(c))
                {
                    i++;
                    continue;
                }

                list.Add(new StyledChar
                {
                    Ch = c,
                    OpenTags = openTags.Count > 0 ? openTags.ToArray() : Array.Empty<string>(),
                    CloseTags = closeTags.Count > 0 ? closeTags.ToArray() : Array.Empty<string>(),
                    Dir = 0
                });
                i++;
            }
            return list;
        }

        private static bool IsSupportedTmpRichTag(string tag, out bool isClosing, out string tagName)
        {
            isClosing = false;
            tagName = string.Empty;
            if (tag.Length < 3) return false;

            int start = 1;
            if (tag[1] == '/')
            {
                isClosing = true;
                start = 2;
            }

            int eq = tag.IndexOf('=', start);
            int end = eq > 0 ? eq : tag.Length - 1;
            if (end <= start) return false;

            string name = tag.Substring(start, end - start).Trim();
            if (string.Equals(name, "color", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "i", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "u", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "s", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "size", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "alpha", StringComparison.OrdinalIgnoreCase))
            {
                tagName = name.ToLowerInvariant();
                return true;
            }
            return false;
        }

        private static void PopMatchingTag(List<string> openTags, List<string> closeTags, string tagName)
        {
            string targetClose = $"</{tagName}>";
            for (int i = closeTags.Count - 1; i >= 0; i--)
            {
                if (string.Equals(closeTags[i], targetClose, StringComparison.OrdinalIgnoreCase))
                {
                    openTags.RemoveAt(i);
                    closeTags.RemoveAt(i);
                    return;
                }
            }
        }

        private static List<StyledChar> ApplyContextualJoining(List<StyledChar> input)
        {
            var result = new List<StyledChar>(input.Count);
            int count = input.Count;

            for (int i = 0; i < count; i++)
            {
                char curr = input[i].Ch;

                // Handle Lam + Alef mandatory ligatures
                if (curr == '\u0644' && i + 1 < count && TryGetLamAlefLigature(input[i + 1].Ch, out char isoLig, out char finLig))
                {
                    bool prevConnectsLeft = i > 0 && CanConnectLeft(input[i - 1].Ch);
                    var ligSc = input[i];
                    ligSc.Ch = prevConnectsLeft ? finLig : isoLig;
                    result.Add(ligSc);
                    i++; // Consume the Alef too
                    continue;
                }

                if (Map.TryGetValue(curr, out var forms))
                {
                    bool prevConnectsLeft = i > 0 && CanConnectLeft(input[i - 1].Ch);
                    bool nextConnectsRight = i + 1 < count && CanConnectRight(input[i + 1].Ch);
                    bool canJoinLeft = forms.Initial != '\u0000';

                    char chosen;
                    if (prevConnectsLeft && nextConnectsRight && canJoinLeft && forms.Medial != '\u0000')
                        chosen = forms.Medial;
                    else if (prevConnectsLeft && forms.Final != '\u0000')
                        chosen = forms.Final;
                    else if (nextConnectsRight && canJoinLeft && forms.Initial != '\u0000')
                        chosen = forms.Initial;
                    else
                        chosen = forms.Isolated;

                    var sc = input[i];
                    sc.Ch = chosen;
                    result.Add(sc);
                }
                else
                {
                    result.Add(input[i]);
                }
            }

            return result;
        }

        private static void ResolveDirections(List<StyledChar> chars)
        {
            int n = chars.Count;
            // First pass: strong types + intrinsic numeric signs
            for (int i = 0; i < n; i++)
            {
                char c = chars[i].Ch;
                if (IsArabicChar(c) || c == '\u060C' || c == '\u061B' || c == '\u061F')
                {
                    var s = chars[i]; s.Dir = 1; chars[i] = s;
                }
                else if (IsStrongLtr(c))
                {
                    var s = chars[i]; s.Dir = -1; chars[i] = s;
                }
                else
                {
                    var s = chars[i]; s.Dir = 0; chars[i] = s;
                }
            }

            // Attach signs/percent directly adjacent to digits as LTR (+15, -63, 75%)
            for (int i = 0; i < n; i++)
            {
                if (chars[i].Dir != 0) continue;
                char c = chars[i].Ch;
                bool nextDigit = i + 1 < n && IsDigit(chars[i + 1].Ch);
                bool prevDigit = i > 0 && IsDigit(chars[i - 1].Ch);

                if ((c == '+' || c == '-' || c == '~') && nextDigit)
                {
                    var s = chars[i]; s.Dir = -1; chars[i] = s;
                }
                else if (c == '%' && prevDigit)
                {
                    var s = chars[i]; s.Dir = -1; chars[i] = s;
                }
            }

            // Neutral runs bounded on BOTH sides by LTR stay LTR (e.g. "2 / 3", "NASA / JPL", "0.55 mSv", "06:00")
            for (int i = 0; i < n; i++)
            {
                if (chars[i].Dir != 0) continue;

                int start = i;
                while (i < n && chars[i].Dir == 0) i++;
                int end = i;

                sbyte leftDir = start > 0 ? chars[start - 1].Dir : (sbyte)1;
                sbyte rightDir = end < n ? chars[end].Dir : (sbyte)1;
                bool betweenLtr = leftDir == -1 && rightDir == -1 && !HasBoundaryColonOrPipe(chars, start, end);
                sbyte resolved = betweenLtr ? (sbyte)-1 : (sbyte)1;

                for (int k = start; k < end; k++)
                {
                    var s = chars[k]; s.Dir = resolved; chars[k] = s;
                }
            }
        }

        private static bool HasBoundaryColonOrPipe(List<StyledChar> chars, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                char c = chars[i].Ch;
                if (c == '|') return true;
                if (c == ':')
                {
                    // Time like "06:00" has digits on both sides -> stay single LTR token
                    bool leftDig = i > 0 && IsDigit(chars[i - 1].Ch);
                    bool rightDig = i + 1 < chars.Count && IsDigit(chars[i + 1].Ch);
                    if (!(leftDig && rightDig)) return true;
                }
            }
            return false;
        }

        private static List<LogicalRun> BuildRuns(List<StyledChar> chars)
        {
            var runs = new List<LogicalRun>(8);
            int n = chars.Count;
            int i = 0;
            while (i < n)
            {
                bool isRtl = chars[i].Dir >= 0;
                int start = i;
                while (i < n && (chars[i].Dir >= 0) == isRtl) i++;
                runs.Add(new LogicalRun { IsRtl = isRtl, Start = start, Count = i - start });
            }
            return runs;
        }

        private static void SyncTagDiff(StringBuilder sb, ref string[] activeOpen, ref string[] activeClose, string[] nextOpen, string[] nextClose)
        {
            int common = 0;
            int minLen = Math.Min(activeOpen.Length, nextOpen.Length);
            while (common < minLen && string.Equals(activeOpen[common], nextOpen[common], StringComparison.Ordinal))
                common++;

            for (int i = activeClose.Length - 1; i >= common; i--)
                sb.Append(activeClose[i]);
            for (int i = common; i < nextOpen.Length; i++)
                sb.Append(nextOpen[i]);

            activeOpen = nextOpen;
            activeClose = nextClose;
        }

        private static void AppendTags(StringBuilder sb, List<string> tags, bool forward)
        {
            if (forward)
            {
                for (int i = 0; i < tags.Count; i++) sb.Append(tags[i]);
            }
            else
            {
                for (int i = tags.Count - 1; i >= 0; i--) sb.Append(tags[i]);
            }
        }

        private static bool TryGetLamAlefLigature(char second, out char isolated, out char final)
        {
            switch (second)
            {
                case '\u0622': isolated = '\uFEF5'; final = '\uFEF6'; return true;
                case '\u0623': isolated = '\uFEF7'; final = '\uFEF8'; return true;
                case '\u0625': isolated = '\uFEF9'; final = '\uFEFA'; return true;
                case '\u0627': isolated = '\uFEFB'; final = '\uFEFC'; return true;
                default: isolated = '\u0000'; final = '\u0000'; return false;
            }
        }

        private static bool CanConnectLeft(char ch)
        {
            return Map.TryGetValue(ch, out var f) && f.Initial != '\u0000';
        }

        private static bool CanConnectRight(char ch)
        {
            return Map.TryGetValue(ch, out var f) && (f.Final != '\u0000' || f.Medial != '\u0000');
        }

        private static bool IsArabicChar(char c)
        {
            return (c >= '\u0621' && c <= '\u064A') || c == '\u06CC' ||
                   (c >= '\uFB50' && c <= '\uFDFF') ||
                   (c >= '\uFE70' && c <= '\uFEFF');
        }

        private static bool IsArabicDiacritic(char c)
        {
            return (c >= '\u064B' && c <= '\u065F') || c == '\u0670' || c == '\u0640';
        }

        private static bool IsDigit(char c) => (c >= '0' && c <= '9') || (c >= '\u0660' && c <= '\u0669');

        private static bool IsStrongLtr(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || IsDigit(c);
        }

        private static char MirrorRtlPunctuation(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                default: return c;
            }
        }
    }
}
