using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Drag handler attached to window title bars, clamped to the desktop/workspace bounds.
    /// </summary>
    public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _windowRect;
        private RectTransform _boundsRect;
        private WindowFrame _owner;
        private Vector2 _pointerOffset;

        public void Init(WindowFrame owner, RectTransform windowRect, RectTransform boundsRect)
        {
            _owner = owner;
            _windowRect = windowRect;
            _boundsRect = boundsRect;
        }

        public void SetBoundsRect(RectTransform boundsRect)
        {
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
            _windowRect.anchoredPosition = ClampPosition(target);
        }

        public Vector2 ClampPosition(Vector2 target)
        {
            if (_windowRect == null || _boundsRect == null) return target;
            Vector2 boundsSize = _boundsRect.rect.size;
            Vector2 winSize = _windowRect.rect.size;
            float maxX = Mathf.Max(0f, boundsSize.x - winSize.x);
            float maxY = Mathf.Max(0f, boundsSize.y - winSize.y);

            // Anchored top-left (0, 1)
            target.x = Mathf.Clamp(target.x, 0f, maxX);
            target.y = Mathf.Clamp(target.y, -maxY, 0f);
            return target;
        }
    }

    /// <summary>
    /// Window frame component providing title bar, icon, minimize/close buttons,
    /// drag handling clamped to workspace bounds, 150ms open/close/minimize animation,
    /// and click-to-focus z-ordering.
    /// </summary>
    public abstract class WindowFrame : MonoBehaviour, IPointerDownHandler
    {
        private const float AnimDuration = 0.15f;

        public TerminalAppId AppId { get; protected set; }
        public string TitleKey { get; protected set; }
        public bool IsMinimized { get; internal set; }
        public bool IsOpen => (gameObject.activeSelf || IsMinimized) && !_closing;

        protected GameSession Session { get; private set; }
        protected WindowManager Manager { get; private set; }
        protected UITheme Theme { get; private set; }
        protected AccessibilityTextScaler Scaler { get; private set; }
        protected bool IsReadOnly => Manager != null && Manager.IsReadOnlyMode;

        private RectTransform _rootRect;
        private WindowDragHandler _dragHandler;
        private Image _frameBorderImg;
        private Image _titleBarInnerImg;
        private Image _accentStripImg;
        private TextMeshProUGUI _titleText;
        private CanvasGroup _canvasGroup;
        protected RectTransform ContentRoot { get; private set; }

        private bool _opening;
        private bool _closing;
        private bool _minimizing;
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
            _frameBorderImg.color = GetAccentColor(AppId);

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

            _dragHandler = titleBarRect.gameObject.AddComponent<WindowDragHandler>();
            _dragHandler.Init(this, _rootRect, desktopBounds);

            var titleInner = TerminalUiBuilder.GetInner(titleBarRect);

            // Colored accent pill bar on left edge of title bar
            Color accent = GetAccentColor(AppId);
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
            _titleText.textWrappingMode = TextWrappingModes.NoWrap;
            _titleText.overflowMode = TextOverflowModes.Ellipsis;
            _titleText.text = DesktopLocalization.Get(TitleKey);
            TerminalUiBuilder.SetAnchors(_titleText.rectTransform, 0f, 0f, 0.80f, 1f, 64f, 0f, 0f, 0f);

            var minBtn = TerminalUiBuilder.CreateButton("MinimizeBtn", titleInner, DesktopLocalization.Get("desktop.win.minimize"), Theme, Theme.ButtonBg, Theme.BorderUnfocused, Theme.TextPrimary, Theme.BodyFontSize, () => Manager?.MinimizeWindow(AppId), Scaler);
            TerminalUiBuilder.SetAnchors(minBtn.GetComponent<RectTransform>(), 0.815f, 0.12f, 0.895f, 0.88f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseBtn", titleInner, DesktopLocalization.Get("desktop.win.close"), Theme, Theme.ButtonDangerBg, Color.clear, Color.white, Theme.BodyFontSize, () => Manager?.CloseWindow(AppId), Scaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.905f, 0.12f, 0.99f, 0.88f);

            // Content area below title bar
            ContentRoot = TerminalUiBuilder.CreateRect("Content", inner);
            TerminalUiBuilder.SetAnchors(ContentRoot, 0f, 0f, 1f, 1f, 16f, 14f, 16f, 60f);
        }

        private Color GetAccentColor(TerminalAppId id)
        {
            if (id == DesktopLocalization.MissionControlAppId)
                return UITheme.PaletteTeal;
            return Theme.GetAppAccent(id);
        }

        private static TerminalIconId AppToIcon(TerminalAppId id)
        {
            switch (id)
            {
                case DesktopLocalization.MissionControlAppId: return TerminalIconId.Home;
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

        public void SetWorkspace(RectTransform parentAndBounds, Vector2 size)
        {
            if (_rootRect == null || parentAndBounds == null) return;
            if (_rootRect.parent != parentAndBounds)
                _rootRect.SetParent(parentAndBounds, false);

            Vector2 boundsSize = parentAndBounds.rect.size;
            if (boundsSize.x > 100f && boundsSize.y > 100f)
            {
                size.x = Mathf.Min(size.x, boundsSize.x - 16f);
                size.y = Mathf.Min(size.y, boundsSize.y - 16f);
            }
            _rootRect.sizeDelta = size;
            _dragHandler?.SetBoundsRect(parentAndBounds);
            SetAnchoredPosition(_rootRect.anchoredPosition);
        }

        public void SetAnchoredPosition(Vector2 pos)
        {
            if (_rootRect == null) return;
            _rootRect.anchoredPosition = _dragHandler != null ? _dragHandler.ClampPosition(pos) : pos;
        }

        public void BeginOpenAnimation()
        {
            IsMinimized = false;
            _closing = false;
            _minimizing = false;
            _opening = true;
            _animTimer = 0f;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_rootRect != null) _rootRect.localScale = Vector3.one * 0.93f;
        }

        public void BeginCloseAnimation()
        {
            if (!gameObject.activeSelf)
            {
                IsMinimized = false;
                return;
            }
            _opening = false;
            _minimizing = false;
            _closing = true;
            _animTimer = 0f;
        }

        public void BeginMinimizeAnimation()
        {
            IsMinimized = true;
            if (!gameObject.activeSelf) return;
            _opening = false;
            _closing = false;
            _minimizing = true;
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
                    IsMinimized = false;
                    gameObject.SetActive(false);
                }
            }
            else if (_minimizing)
            {
                _animTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_animTimer / AnimDuration);
                float ease = 1f - t;
                if (_canvasGroup != null) _canvasGroup.alpha = ease;
                if (_rootRect != null) _rootRect.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, ease);
                if (t >= 1f)
                {
                    _minimizing = false;
                    gameObject.SetActive(false);
                }
            }
        }

        public void SetFocusedVisual(bool focused)
        {
            Color accent = GetAccentColor(AppId);
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

    /// <summary>
    /// Base class for all Terminal OS app windows, inheriting WindowFrame chrome.
    /// </summary>
    public abstract class WindowView : WindowFrame
    {
    }
}
