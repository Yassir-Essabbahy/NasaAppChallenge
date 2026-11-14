using System;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    public struct StatusBarWidget
    {
        public Image FillImage;
        public SmoothBarFill Animator;
        public TextMeshProUGUI ValueText;
        public TextMeshProUGUI StatusTagText;
    }

    public struct FoldoutWidget
    {
        public Button ToggleButton;
        public TextMeshProUGUI ToggleLabel;
        public RectTransform BodyRect;
        public bool IsExpanded;
    }

    /// <summary>
    /// Smoothly animates a horizontal bar's fill and color over 150ms using unscaled time.
    /// </summary>
    public class SmoothBarFill : MonoBehaviour
    {
        private Image _img;
        private float _startFill = 1f;
        private float _targetFill = 1f;
        private float _currentFill = 1f;
        private Color _startColor = Color.white;
        private Color _targetColor = Color.white;
        private float _timer = 1f;
        private bool _initialized;

        private const float AnimDuration = 0.15f;

        private void Awake()
        {
            _img = GetComponent<Image>();
        }

        public void SetTarget(float fill01, Color color, bool immediate = false)
        {
            if (_img == null) _img = GetComponent<Image>();
            float clamped = Mathf.Clamp01(fill01);

            if (immediate || !_initialized || !gameObject.activeInHierarchy)
            {
                _initialized = true;
                _currentFill = _startFill = _targetFill = clamped;
                _startColor = _targetColor = color;
                _timer = AnimDuration;
                Apply(clamped, color);
                return;
            }

            if (Mathf.Abs(_targetFill - clamped) > 0.001f || _targetColor != color)
            {
                _startFill = _currentFill;
                _targetFill = clamped;
                _startColor = _img != null ? _img.color : color;
                _targetColor = color;
                _timer = 0f;
            }
        }

        private void Update()
        {
            if (!_initialized || _timer >= AnimDuration) return;
            _timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_timer / AnimDuration);
            float ease = 1f - (1f - t) * (1f - t);
            _currentFill = Mathf.Lerp(_startFill, _targetFill, ease);
            Color col = Color.Lerp(_startColor, _targetColor, ease);
            Apply(_currentFill, col);
        }

        private void Apply(float fill, Color col)
        {
            if (_img == null) return;
            _img.rectTransform.anchorMax = new Vector2(fill, 1f);
            _img.color = col;
        }
    }

    /// <summary>
    /// Clear hover & pressed scale feedback for friendly UI buttons (150ms eased feel).
    /// </summary>
    public class ButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;
        private bool _hovered;
        private bool _pressed;
        private Selectable _selectable;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            _hovered = false;
            _pressed = false;
            transform.localScale = _baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            UpdateTargetScale();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            UpdateTargetScale();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            UpdateTargetScale();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            UpdateTargetScale();
        }

        private void UpdateTargetScale()
        {
            if (_selectable != null && !_selectable.IsInteractable())
            {
                _targetScale = _baseScale;
                return;
            }
            if (_pressed) _targetScale = _baseScale * 0.965f;
            else if (_hovered) _targetScale = _baseScale * 1.025f;
            else _targetScale = _baseScale;
        }

        private void Update()
        {
            if (Vector3.SqrMagnitude(transform.localScale - _targetScale) > 0.00001f)
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Mathf.Clamp01(Time.unscaledDeltaTime * 22f));
        }
    }

    /// <summary>
    /// Hover tooltip trigger using Localization keys or dynamic text, routed to TerminalController's tooltip bubble.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string TitleKey;
        public string BodyKey;
        public string RawTitle;
        public string RawBody;

        public void SetKeys(string titleKey, string bodyKey)
        {
            TitleKey = titleKey;
            BodyKey = bodyKey;
            RawTitle = null;
            RawBody = null;
        }

        public void SetRawText(string title, string body)
        {
            RawTitle = title;
            RawBody = body;
            TitleKey = null;
            BodyKey = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            string title = !string.IsNullOrEmpty(TitleKey) ? Localization.Get(TitleKey) : RawTitle;
            string body = !string.IsNullOrEmpty(BodyKey) ? Localization.Get(BodyKey) : RawBody;
            if (string.IsNullOrEmpty(body) && string.IsNullOrEmpty(title)) return;
            TerminalUiBuilder.RaiseShowTooltip(title, body, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TerminalUiBuilder.RaiseHideTooltip();
        }

        private void OnDisable()
        {
            TerminalUiBuilder.RaiseHideTooltip();
        }
    }

    /// <summary>
    /// Helper factory for rounded (8px) friendly slate-blue uGUI + TextMeshPro controls,
    /// icons paired with status words, drop shadows, hover states, and smooth 150ms bars.
    /// </summary>
    public static class TerminalUiBuilder
    {
        public static event Action<string, string, Vector2> OnShowTooltipRequested;
        public static event Action OnHideTooltipRequested;

        public static void RaiseShowTooltip(string title, string body, Vector2 screenPos)
            => OnShowTooltipRequested?.Invoke(title, body, screenPos);

        public static void RaiseHideTooltip()
            => OnHideTooltipRequested?.Invoke();

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform CreateFlatPanel(string name, Transform parent, Color bgColor, Color borderColor, float borderWidth = 2f, bool addDropShadow = false)
        {
            var rounded = TerminalUiArt.GetRoundedRect8px();
            var outer = CreateRect(name, parent);

            RectTransform frameRect = outer;
            if (addDropShadow)
            {
                var shadowRect = CreateRect("DropShadow", outer);
                shadowRect.anchorMin = Vector2.zero;
                shadowRect.anchorMax = Vector2.one;
                shadowRect.offsetMin = new Vector2(-10f, -14f);
                shadowRect.offsetMax = new Vector2(10f, 6f);
                var shadowImg = shadowRect.gameObject.AddComponent<Image>();
                shadowImg.sprite = TerminalUiArt.GetDropShadowSprite();
                shadowImg.type = Image.Type.Sliced;
                shadowImg.color = Color.white;
                shadowImg.raycastTarget = false;

                frameRect = CreateRect("Frame", outer);
                SetStretch(frameRect);
            }

            var borderImg = frameRect.gameObject.AddComponent<Image>();
            borderImg.sprite = rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = borderColor;

            var inner = CreateRect("Inner", frameRect);
            var bgImg = inner.gameObject.AddComponent<Image>();
            bgImg.sprite = rounded;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = bgColor;
            SetStretch(inner, borderWidth);
            return outer;
        }

        public static RectTransform GetInner(RectTransform borderedPanel)
        {
            if (borderedPanel == null) return null;
            var frameInner = borderedPanel.Find("Frame/Inner");
            if (frameInner != null) return frameInner.GetComponent<RectTransform>();
            var inner = borderedPanel.Find("Inner");
            return inner != null ? inner.GetComponent<RectTransform>() : borderedPanel;
        }

        public static void SetBorderColor(RectTransform borderedPanel, Color borderColor)
        {
            if (borderedPanel == null) return;
            var frame = borderedPanel.Find("Frame");
            var img = frame != null ? frame.GetComponent<Image>() : borderedPanel.GetComponent<Image>();
            if (img != null) img.color = borderColor;
        }

        public static Image CreateIcon(string name, Transform parent, TerminalIconId iconId, Color tint)
        {
            var rect = CreateRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = TerminalUiArt.GetIcon(iconId);
            img.color = tint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static TooltipTrigger AttachTooltip(Component target, string titleKey, string bodyKey)
        {
            if (target == null) return null;
            var tt = target.gameObject.GetComponent<TooltipTrigger>() ?? target.gameObject.AddComponent<TooltipTrigger>();
            tt.SetKeys(titleKey, bodyKey);
            return tt;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, UITheme theme, float baseSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, AccessibilityTextScaler scaler = null)
        {
            var rect = CreateRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = theme != null ? theme.ResolveFont() : null;
            if (font != null) tmp.font = font;
            float clampedSize = Mathf.Max(24f, baseSize);
            tmp.fontSize = clampedSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (scaler != null) scaler.RegisterText(tmp, clampedSize);
            return tmp;
        }

        public static TextMeshProUGUI CreateCompactDebugText(string name, Transform parent, UITheme theme, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, AccessibilityTextScaler scaler = null)
        {
            var rect = CreateRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = theme != null ? theme.ResolveFont() : null;
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (scaler != null) scaler.RegisterText(tmp, size);
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent, string label, UITheme theme, Color bgColor, Color borderColor, Color textColor, float fontSize, Action onClick, AccessibilityTextScaler scaler = null, bool addDropShadow = false)
        {
            var panel = CreateFlatPanel(name, parent, bgColor, borderColor, theme != null ? theme.BorderThickness : 2f, addDropShadow);
            var inner = GetInner(panel);
            var bgImg = inner.GetComponent<Image>();

            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.24f, 1.24f, 1.28f, 1f);
            colors.pressedColor = new Color(0.76f, 0.78f, 0.84f, 1f);
            colors.selectedColor = new Color(1.12f, 1.12f, 1.16f, 1f);
            colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.65f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            panel.gameObject.AddComponent<ButtonHoverFeedback>();

            var tmp = CreateText("Label", inner, theme, Mathf.Max(24f, fontSize), textColor, TextAlignmentOptions.Center, scaler);
            tmp.text = label;
            tmp.fontStyle = FontStyles.Bold;
            SetStretch(tmp.rectTransform, 6f);
            return btn;
        }

        public static StatusBarWidget CreateStatusBar(string name, Transform parent, string title, UITheme theme, AccessibilityTextScaler scaler, float xMin, float yMin, float xMax, float yMax, TerminalIconId? icon = null, Color? accentTint = null)
        {
            var row = CreateRect(name, parent);
            SetAnchors(row, xMin, yMin, xMax, yMax);

            float textLeftX = 0f;
            if (icon.HasValue)
            {
                var ic = CreateIcon("Icon", row, icon.Value, accentTint ?? theme.TextPrimary);
                SetAnchors(ic.rectTransform, 0f, 0.50f, 0.06f, 0.98f);
                textLeftX = 0.065f;
            }

            var titleTmp = CreateText("Title", row, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, scaler);
            titleTmp.text = title;
            titleTmp.fontStyle = FontStyles.Bold;
            SetAnchors(titleTmp.rectTransform, textLeftX, 0.48f, 0.46f, 1f);

            var valTmp = CreateText("Value", row, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.MidlineRight, scaler);
            SetAnchors(valTmp.rectTransform, 0.46f, 0.48f, 0.80f, 1f);

            var tagTmp = CreateText("Tag", row, theme, theme.BodyFontSize, theme.StatusOk, TextAlignmentOptions.MidlineRight, scaler);
            tagTmp.fontStyle = FontStyles.Bold;
            SetAnchors(tagTmp.rectTransform, 0.81f, 0.48f, 1f, 1f);

            var track = CreateFlatPanel("Track", row, theme.BarTrackBg, theme.BorderUnfocused, 2f);
            SetAnchors(track, 0f, 0.04f, 1f, 0.44f);
            var trackInner = GetInner(track);

            var fillRect = CreateRect("Fill", trackInner);
            var fillImg = fillRect.gameObject.AddComponent<Image>();
            fillImg.sprite = TerminalUiArt.GetRoundedRect8px();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = theme.StatusOk;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var anim = fillRect.gameObject.AddComponent<SmoothBarFill>();

            return new StatusBarWidget
            {
                FillImage = fillImg,
                Animator = anim,
                ValueText = valTmp,
                StatusTagText = tagTmp
            };
        }

        public static void UpdateStatusBar(StatusBarWidget bar, float value0To100, string valueLabel, UITheme theme, float lowThreshold = 25f, float critThreshold = 10f)
        {
            float t = Mathf.Clamp01(value0To100 / 100f);
            Color col;
            string tag;
            if (value0To100 <= critThreshold)
            {
                col = theme.StatusCrit;
                tag = Localization.Get("status.word.danger");
            }
            else if (value0To100 < lowThreshold)
            {
                col = theme.StatusWarn;
                tag = Localization.Get("status.word.low");
            }
            else
            {
                col = theme.StatusOk;
                tag = Localization.Get("status.word.good");
            }

            if (bar.Animator != null)
                bar.Animator.SetTarget(t, col);
            else if (bar.FillImage != null)
            {
                bar.FillImage.rectTransform.anchorMax = new Vector2(t, 1f);
                bar.FillImage.color = col;
            }

            bar.ValueText.text = valueLabel;
            bar.StatusTagText.color = col;
            bar.StatusTagText.text = tag;
        }

        public static FoldoutWidget CreateMoreDetailsFoldout(string name, Transform parent, UITheme theme, AccessibilityTextScaler scaler, float btnXMin, float btnYMin, float btnXMax, float btnYMax, float bodyXMin, float bodyYMin, float bodyXMax, float bodyYMax, Action<bool> onToggled = null)
        {
            var widget = new FoldoutWidget { IsExpanded = false };

            var bodyPanel = CreateFlatPanel(name + "_Body", parent, theme.PanelBg, theme.BorderUnfocused, 2f);
            SetAnchors(bodyPanel, bodyXMin, bodyYMin, bodyXMax, bodyYMax);
            bodyPanel.gameObject.SetActive(false);
            widget.BodyRect = GetInner(bodyPanel);

            var toggleBtn = CreateButton(name + "_Btn", parent, "+ " + Localization.Get("ui.more_details"), theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextMuted, theme.BodyFontSize, null, scaler);
            SetAnchors(toggleBtn.GetComponent<RectTransform>(), btnXMin, btnYMin, btnXMax, btnYMax);
            var toggleLabel = toggleBtn.GetComponentInChildren<TextMeshProUGUI>();

            widget.ToggleButton = toggleBtn;
            widget.ToggleLabel = toggleLabel;

            toggleBtn.onClick.AddListener(() =>
            {
                bool next = !bodyPanel.gameObject.activeSelf;
                bodyPanel.gameObject.SetActive(next);
                if (toggleLabel != null)
                    toggleLabel.text = next ? "- " + Localization.Get("ui.less_details") : "+ " + Localization.Get("ui.more_details");
                onToggled?.Invoke(next);
            });

            return widget;
        }

        public static void SetStretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax, float padL = 0f, float padB = 0f, float padR = 0f, float padT = 0f)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = new Vector2(padL, padB);
            rect.offsetMax = new Vector2(-padR, -padT);
        }

        public static TextMeshProUGUI CreateScrollableText(string name, Transform parent, UITheme theme, float fontSize, Color textColor, Color bgColor, Color borderColor, out RectTransform panel, AccessibilityTextScaler scaler = null)
        {
            panel = CreateFlatPanel(name, parent, bgColor, borderColor, theme != null ? theme.BorderThickness : 2f);
            var inner = GetInner(panel);

            var viewport = CreateRect("Viewport", inner);
            SetStretch(viewport, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var scroll = inner.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            var tmp = CreateText("BodyText", content, theme, fontSize, textColor, TextAlignmentOptions.TopLeft, scaler);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
