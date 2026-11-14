using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Optional drag handler attached to window title bars, clamped to the desktop bounds.
    /// </summary>
    public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _windowRect;
        private RectTransform _boundsRect;
        private WindowView _owner;
        private Vector2 _pointerOffset;

        public void Init(WindowView owner, RectTransform windowRect, RectTransform boundsRect)
        {
            _owner = owner;
            _windowRect = windowRect;
            _boundsRect = boundsRect;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner != null) _owner.RequestFocus();
            if (_windowRect == null || _boundsRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _boundsRect, eventData.position, eventData.pressEventCamera, out var localPointer);
            _pointerOffset = _windowRect.anchoredPosition - localPointer;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_windowRect == null || _boundsRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _boundsRect, eventData.position, eventData.pressEventCamera, out var localPointer))
                return;

            Vector2 target = localPointer + _pointerOffset;
            Vector2 boundsSize = _boundsRect.rect.size;
            Vector2 winSize = _windowRect.rect.size;
            float maxX = Mathf.Max(0f, boundsSize.x - winSize.x);
            float maxY = Mathf.Max(0f, boundsSize.y - winSize.y);

            // Anchored top-left (0, 1)
            target.x = Mathf.Clamp(target.x, 0f, maxX);
            target.y = Mathf.Clamp(target.y, -maxY, 0f);
            _windowRect.anchoredPosition = target;
        }
    }

    /// <summary>
    /// Base class for all 8 Terminal OS app windows.
    /// Rounded 8px frame with soft drop shadow, per-app bright accent header,
    /// 150ms open/close animation, and read-only wall-tablet awareness.
    /// </summary>
    public abstract class WindowView : MonoBehaviour, IPointerDownHandler
    {
        private const float AnimDuration = 0.15f;

        public TerminalAppId AppId { get; protected set; }
        public string TitleKey { get; protected set; }
        public bool IsOpen => gameObject.activeSelf && !_closing;

        protected GameSession Session { get; private set; }
        protected WindowManager Manager { get; private set; }
        protected UITheme Theme { get; private set; }
        protected AccessibilityTextScaler Scaler { get; private set; }
        protected bool IsReadOnly => Manager != null && Manager.IsReadOnlyMode;

        private RectTransform _rootRect;
        private Image _frameBorderImg;
        private Image _titleBarInnerImg;
        private Image _accentStripImg;
        private TextMeshProUGUI _titleText;
        private CanvasGroup _canvasGroup;
        protected RectTransform ContentRoot { get; private set; }

        private bool _opening;
        private bool _closing;
        private float _animTimer;

        public void Initialize(TerminalAppId appId, string titleKey, GameSession session, WindowManager manager, UITheme theme, AccessibilityTextScaler scaler, RectTransform desktopBounds)
        {
            AppId = appId;
            TitleKey = titleKey;
            Session = session;
            Manager = manager;
            Theme = theme;
            Scaler = scaler;

            BuildChrome(desktopBounds);
            BuildContent(ContentRoot);
            gameObject.SetActive(false);
        }

        private void BuildChrome(RectTransform desktopBounds)
        {
            _rootRect = GetComponent<RectTransform>();
            _rootRect.anchorMin = new Vector2(0f, 1f);
            _rootRect.anchorMax = new Vector2(0f, 1f);
            _rootRect.pivot = new Vector2(0f, 1f);
            _rootRect.sizeDelta = new Vector2(900f, 640f);

            _canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // Soft drop shadow behind the window
            var shadowRect = TerminalUiBuilder.CreateRect("WindowShadow", _rootRect);
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-16f, -20f);
            shadowRect.offsetMax = new Vector2(16f, 10f);
            var shadowImg = shadowRect.gameObject.AddComponent<Image>();
            shadowImg.sprite = TerminalUiArt.GetDropShadowSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(1f, 1f, 1f, 0.9f);
            shadowImg.raycastTarget = false;

            var rounded = TerminalUiArt.GetRoundedRect8px();
            var frameRect = TerminalUiBuilder.CreateRect("WindowFrame", _rootRect);
            TerminalUiBuilder.SetStretch(frameRect);
            _frameBorderImg = frameRect.gameObject.AddComponent<Image>();
            _frameBorderImg.sprite = rounded;
            _frameBorderImg.type = Image.Type.Sliced;
            _frameBorderImg.color = Theme.GetAppAccent(AppId);

            var inner = TerminalUiBuilder.CreateRect("WindowInner", frameRect);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = rounded;
            innerImg.type = Image.Type.Sliced;
            innerImg.color = Theme.WindowBg;
            TerminalUiBuilder.SetStretch(inner, Theme.BorderThickness);

            // Title bar (52px tall)
            var titleBarRect = TerminalUiBuilder.CreateFlatPanel("TitleBar", inner, Theme.TitleBarUnfocusedBg, Color.clear, 0f);
            _titleBarInnerImg = TerminalUiBuilder.GetInner(titleBarRect).GetComponent<Image>();
            titleBarRect.anchorMin = new Vector2(0f, 1f);
            titleBarRect.anchorMax = new Vector2(1f, 1f);
            titleBarRect.pivot = new Vector2(0.5f, 1f);
            titleBarRect.offsetMin = new Vector2(0f, -52f);
            titleBarRect.offsetMax = new Vector2(0f, 0f);

            var drag = titleBarRect.gameObject.AddComponent<WindowDragHandler>();
            drag.Init(this, _rootRect, desktopBounds);

            var titleInner = TerminalUiBuilder.GetInner(titleBarRect);

            // Colored accent pill bar on left edge of title bar
            Color accent = Theme.GetAppAccent(AppId);
            var accentPill = TerminalUiBuilder.CreateRect("AccentPill", titleInner);
            _accentStripImg = accentPill.gameObject.AddComponent<Image>();
            _accentStripImg.sprite = rounded;
            _accentStripImg.type = Image.Type.Sliced;
            _accentStripImg.color = accent;
            TerminalUiBuilder.SetAnchors(accentPill, 0f, 0.14f, 0f, 0.86f, 8f, 0f, -16f, 0f);

            // App icon
            var appIcon = TerminalUiBuilder.CreateIcon("AppIcon", titleInner, AppToIcon(AppId), accent);
            appIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            appIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            appIcon.rectTransform.anchoredPosition = new Vector2(38f, 0f);
            appIcon.rectTransform.sizeDelta = new Vector2(30f, 30f);

            _titleText = TerminalUiBuilder.CreateText("TitleText", titleInner, Theme, Theme.HeaderFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.text = Localization.Get(TitleKey);
            TerminalUiBuilder.SetAnchors(_titleText.rectTransform, 0f, 0f, 0.86f, 1f, 64f, 0f, 0f, 0f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseBtn", titleInner, "X", Theme, Theme.ButtonDangerBg, Color.clear, Color.white, Theme.BodyFontSize, () => Manager.CloseWindow(AppId), Scaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.91f, 0.12f, 0.99f, 0.88f);

            // Content area below title bar
            ContentRoot = TerminalUiBuilder.CreateRect("Content", inner);
            TerminalUiBuilder.SetAnchors(ContentRoot, 0f, 0f, 1f, 1f, 16f, 14f, 16f, 60f);
        }

        private static TerminalIconId AppToIcon(TerminalAppId id)
        {
            switch (id)
            {
                case TerminalAppId.PowerGrid: return TerminalIconId.Battery;
                case TerminalAppId.LifeSupport: return TerminalIconId.Oxygen;
                case TerminalAppId.Greenhouse: return TerminalIconId.Food;
                case TerminalAppId.Radiation: return TerminalIconId.Shield;
                case TerminalAppId.Crew: return TerminalIconId.Crew;
                case TerminalAppId.Comms: return TerminalIconId.Signal;
                case TerminalAppId.CommandersLog: return TerminalIconId.Log;
                case TerminalAppId.Sources: return TerminalIconId.Sources;
                default: return TerminalIconId.Help;
            }
        }

        public void SetAnchoredPosition(Vector2 pos)
        {
            if (_rootRect != null) _rootRect.anchoredPosition = pos;
        }

        public void BeginOpenAnimation()
        {
            _closing = false;
            _opening = true;
            _animTimer = 0f;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_rootRect != null) _rootRect.localScale = Vector3.one * 0.93f;
        }

        public void BeginCloseAnimation()
        {
            if (!gameObject.activeSelf) return;
            _opening = false;
            _closing = true;
            _animTimer = 0f;
        }

        private void Update()
        {
            if (_opening)
            {
                _animTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_animTimer / AnimDuration);
                float ease = 1f - (1f - t) * (1f - t);
                if (_canvasGroup != null) _canvasGroup.alpha = ease;
                if (_rootRect != null) _rootRect.localScale = Vector3.Lerp(Vector3.one * 0.93f, Vector3.one, ease);
                if (t >= 1f) _opening = false;
            }
            else if (_closing)
            {
                _animTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_animTimer / AnimDuration);
                float ease = 1f - t;
                if (_canvasGroup != null) _canvasGroup.alpha = ease;
                if (_rootRect != null) _rootRect.localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, ease);
                if (t >= 1f)
                {
                    _closing = false;
                    gameObject.SetActive(false);
                }
            }
        }

        public void SetFocusedVisual(bool focused)
        {
            Color accent = Theme.GetAppAccent(AppId);
            if (_frameBorderImg != null)
                _frameBorderImg.color = focused ? accent : Theme.BorderUnfocused;
            if (_titleBarInnerImg != null)
                _titleBarInnerImg.color = focused ? Color.Lerp(Theme.TitleBarFocusedBg, accent, 0.18f) : Theme.TitleBarUnfocusedBg;
            if (_accentStripImg != null)
                _accentStripImg.color = focused ? accent : Color.Lerp(accent, Theme.BorderUnfocused, 0.55f);
            if (_titleText != null)
                _titleText.color = focused ? Theme.TextPrimary : Theme.TextMuted;
        }

        public void RequestFocus()
        {
            Manager?.FocusWindow(AppId);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RequestFocus();
        }

        public virtual void OnOpen() { }
        public virtual void OnClose() { }

        protected abstract void BuildContent(RectTransform content);
        public abstract void Refresh(SessionSnapshot snapshot);
    }
}
