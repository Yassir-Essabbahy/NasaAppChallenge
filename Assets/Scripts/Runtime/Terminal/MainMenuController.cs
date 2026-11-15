using System;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Standalone Main Menu controller for MainMenu.unity.
    /// Shows a pre-menu Language selector (with an English / Arabic slider) before revealing
    /// "Mars / Last Shift" main menu actions, plus a live Language button and Settings slider.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene & Assets")]
        [SerializeField] private string outpostSceneName = "Outpost";
        [SerializeField] private UITheme theme;
        [SerializeField] private FontTheme fontTheme;
        [SerializeField] private PSXSettings psxSettings;
        [SerializeField] private SourcesDatabase sourcesDatabase;
        [SerializeField] private AccessibilitySettings accessibility;

        private Canvas _canvas;
        private AccessibilityTextScaler _textScaler;
        private AudioSource _uiAudioSource;
        private AudioClip _uiClickClip;

        private GameObject _menuButtonsRoot;
        private GameObject _languageGatePanel;
        private GameObject _mobileNoticeGo;
        private GameObject _settingsModal;
        private GameObject _howToPlayModal;
        private GameObject _creditsModal;

        private sealed class BinaryLanguageSwitch
        {
            public Slider Slider;
            public RectTransform VisualThumb;
            public Image TrackBg;
            public Image TrackBorder;
            public Image TrackTint;
            public Image ThumbBg;
            public Image EnglishBg;
            public Image ArabicBg;
            public TextMeshProUGUI EnglishLabel;
            public TextMeshProUGUI ArabicLabel;
            public float VisualT;
        }

        // Title & Gate Localized Labels
        private TextMeshProUGUI _marsTitleText;
        private TextMeshProUGUI _lastShiftText;
        private TextMeshProUGUI _mobileNoticeText;
        private TextMeshProUGUI _langGateTitleText;
        private TextMeshProUGUI _langGateHintText;
        private TextMeshProUGUI _langGateKeysText;
        private Slider _gateLangSlider;
        private TextMeshProUGUI _gateContinueLabel;
        private BinaryLanguageSwitch _gateSwitch;
        private BinaryLanguageSwitch _settingsSwitch;

        // Main Menu Button Labels
        private TextMeshProUGUI _playBtnLabel;
        private TextMeshProUGUI _settingsBtnLabel;
        private TextMeshProUGUI _languageBtnLabel;
        private TextMeshProUGUI _creditsBtnLabel;
        private TextMeshProUGUI _exitBtnLabel;

        // Settings & Modals
        private TextMeshProUGUI _settingsTitleText;
        private Slider _masterVolSlider;
        private TextMeshProUGUI _masterVolHeaderText;
        private TextMeshProUGUI _masterVolText;
        private Slider _voxVolSlider;
        private TextMeshProUGUI _voxVolHeaderText;
        private TextMeshProUGUI _voxVolText;
        private TextMeshProUGUI _voxMuteLabel;
        private TextMeshProUGUI _largeTextLabel;
        private TextMeshProUGUI _reduceFlickerLabel;
        private TextMeshProUGUI _gfxHeaderText;
        private TextMeshProUGUI _psxEnabledLabel;
        private TextMeshProUGUI _psxResLabel;
        private Slider _settingsLangSlider;
        private TextMeshProUGUI _settingsLangHeaderText;
        private TextMeshProUGUI _settingsHowBtnLabel;
        private TextMeshProUGUI _settingsCloseBtnLabel;

        private TextMeshProUGUI _howTitleText;
        private TextMeshProUGUI _howBodyText;
        private TextMeshProUGUI _howCloseLabel;
        private TextMeshProUGUI _creditsTitleText;
        private TextMeshProUGUI _creditsBodyText;
        private TextMeshProUGUI _creditsBackLabel;

        private float _masterVolume = 0.75f;
        private float _voxVolume = 0.85f;
        private bool _voxMuted;
        private bool _enteredMenuThisVisit;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LanguageBootLoader.LoadSavedLanguage();

            if (theme == null) theme = Resources.Load<UITheme>("TerminalTheme") ?? ScriptableObject.CreateInstance<UITheme>();
            if (fontTheme == null) fontTheme = Resources.Load<FontTheme>("FontTheme");
            if (theme.FontTheme == null && fontTheme != null) theme.FontTheme = fontTheme;
            TerminalUiArt.SetActiveTheme(theme);

            if (psxSettings == null) psxSettings = Resources.Load<PSXSettings>("PSXSettings");
            if (psxSettings != null) psxSettings.LoadFromPlayerPrefs();
            if (sourcesDatabase == null) sourcesDatabase = Resources.Load<SourcesDatabase>("SourcesDatabase");
            if (accessibility == null) accessibility = GetComponent<AccessibilitySettings>() ?? gameObject.AddComponent<AccessibilitySettings>();

            LoadAudioPrefs();
            EnsureUiAudio();
        }

        private void OnEnable()
        {
            Localization.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            Localization.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void Start()
        {
            BuildUi();
            ApplyPsxGlobals();
            float initialT = Localization.CurrentLanguage == Language.Arabic ? 1f : 0f;
            if (_gateSwitch != null) _gateSwitch.VisualT = initialT;
            if (_settingsSwitch != null) _settingsSwitch.VisualT = initialT;
            RefreshLocalizedTexts();
            RefreshSettingsUi();
            UpdateLanguageSwitchVisual(_gateSwitch, immediate: true);
            UpdateLanguageSwitchVisual(_settingsSwitch, immediate: true);
            // Always show the Language choice & slider screen before the Main Menu until Continue is clicked
            ShowLanguageGate(show: true);
        }

        private void Update()
        {
            if (_languageGatePanel != null && _languageGatePanel.activeSelf)
            {
                UpdateLanguageSwitchVisual(_gateSwitch, immediate: false);
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame && _gateLangSlider != null)
                        _gateLangSlider.value = 0f;
                    else if (kb.rightArrowKey.wasPressedThisFrame && _gateLangSlider != null)
                        _gateLangSlider.value = 1f;
                    else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                        ConfirmLanguageAndEnterMenu(playClick: true);
                }
            }

            if (_settingsModal != null && _settingsModal.activeSelf)
                UpdateLanguageSwitchVisual(_settingsSwitch, immediate: false);
        }

        private void HandleLanguageChanged(Language _)
        {
            RefreshLocalizedTexts();
            RefreshSettingsUi();
        }

        private void ShowLanguageGate(bool show)
        {
            if (_languageGatePanel != null) _languageGatePanel.SetActive(show);
            if (_menuButtonsRoot != null) _menuButtonsRoot.SetActive(!show);
        }

        private void ConfirmLanguageAndEnterMenu(bool playClick)
        {
            LanguageBootLoader.SetAndSaveLanguage(Localization.CurrentLanguage, markSelected: true);
            WebGLPlatformHelper.MarkAudioUnlocked();
            _enteredMenuThisRunSafe();
            ShowLanguageGate(show: false);
            if (playClick) PlayClick();
        }

        private void _enteredMenuThisRunSafe()
        {
            _enteredMenuThisVisit = true;
        }

        private void OnLanguageSliderChanged(float val)
        {
            Language target = Mathf.RoundToInt(val) >= 1 ? Language.Arabic : Language.English;
            if (Localization.CurrentLanguage != target)
            {
                LanguageBootLoader.SetAndSaveLanguage(target, markSelected: _enteredMenuThisVisit);
                WebGLPlatformHelper.MarkAudioUnlocked();
                PlayClick();
            }
            else
            {
                RefreshLocalizedTexts();
                RefreshSettingsUi();
            }
        }

        private void LoadAudioPrefs()
        {
            _masterVolume = PlayerPrefs.HasKey(AudioManager.PrefMasterVolume)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(AudioManager.PrefMasterVolume, 0.75f))
                : 0.75f;
            _voxVolume = PlayerPrefs.HasKey(AnnouncerSystem.PrefVolume)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(AnnouncerSystem.PrefVolume, 0.85f))
                : 0.85f;
            _voxMuted = PlayerPrefs.GetInt(AnnouncerSystem.PrefMuted, 0) == 1;
        }

        private void EnsureUiAudio()
        {
            _uiAudioSource = GetComponent<AudioSource>();
            if (_uiAudioSource == null) _uiAudioSource = gameObject.AddComponent<AudioSource>();
            _uiAudioSource.playOnAwake = false;
            _uiAudioSource.loop = false;
            _uiAudioSource.spatialBlend = 0f;

            const int sr = 22050;
            int len = sr / 25;
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                data[i] = Mathf.Sin(2f * Mathf.PI * 1050f * t) * Mathf.Exp(-t * 95f) * 0.25f;
            }
            _uiClickClip = AudioClip.Create("MainMenu_UiClick", len, 1, sr, false);
            _uiClickClip.SetData(data, 0);
        }

        private void PlayClick()
        {
            if (_uiAudioSource != null && _uiClickClip != null && _masterVolume > 0.001f)
                _uiAudioSource.PlayOneShot(_uiClickClip, _masterVolume);
        }

        private void ApplyPsxGlobals()
        {
            bool rf = accessibility != null && accessibility.ReduceFlicker;
            psxSettings?.ApplyGlobals(rf);
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("MainMenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _textScaler = canvasGo.AddComponent<AccessibilityTextScaler>();
            if (accessibility != null)
                accessibility.BindTextScaler(_textScaler);

            var root = canvasGo.GetComponent<RectTransform>();
            Color accentOrange = new Color(0.86f, 0.45f, 0.27f, 1f);

            // Title "Mars / Last Shift"
            _marsTitleText = TerminalUiBuilder.CreateText("MarsTitle", root, theme, 118f, accentOrange, TextAlignmentOptions.BottomLeft, _textScaler);
            _marsTitleText.font = theme.ResolveTitleFont();
            _marsTitleText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _marsTitleText.raycastTarget = false;
            TerminalUiBuilder.SetAnchors(_marsTitleText.rectTransform, 0.088f, 0.695f, 0.52f, 0.88f);

            var ruleRect = TerminalUiBuilder.CreateRect("TitleAccentRule", root);
            TerminalUiBuilder.SetAnchors(ruleRect, 0.088f, 0.687f, 0.44f, 0.691f);
            var ruleImg = ruleRect.gameObject.AddComponent<Image>();
            ruleImg.color = accentOrange;
            ruleImg.raycastTarget = false;

            _lastShiftText = TerminalUiBuilder.CreateText("LastShiftSubtitle", root, theme, 52f, accentOrange, TextAlignmentOptions.TopLeft, _textScaler);
            _lastShiftText.font = theme.ResolveTitleFont();
            _lastShiftText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _lastShiftText.raycastTarget = false;
            TerminalUiBuilder.SetAnchors(_lastShiftText.rectTransform, 0.090f, 0.59f, 0.52f, 0.68f);

            // Pre-Menu Language Selection Gate (with English <-> Arabic slider + Continue button)
            BuildLanguageGatePanel(root, accentOrange);

            // Main Menu Buttons (shown after Language screen or toggling back via LanguageBtn)
            var menuRect = TerminalUiBuilder.CreateRect("MenuButtonsRoot", root);
            TerminalUiBuilder.SetStretch(menuRect);
            _menuButtonsRoot = menuRect.gameObject;

            var playBtn = MissionFlowController.CreateSimpleTextMenuButton("PlayGameBtn", menuRect, theme, _textScaler, "", 0.088f, 0.455f, 0.38f, 0.520f, 36f, () =>
            {
                WebGLPlatformHelper.MarkAudioUnlocked();
                PlayClick();
                if (!string.IsNullOrEmpty(outpostSceneName))
                    SceneManager.LoadScene(outpostSceneName);
            });
            _playBtnLabel = playBtn.GetComponentInChildren<TextMeshProUGUI>();

            var settingsBtn = MissionFlowController.CreateSimpleTextMenuButton("SettingsBtn", menuRect, theme, _textScaler, "", 0.088f, 0.375f, 0.38f, 0.440f, 36f, () =>
            {
                PlayClick();
                RefreshSettingsUi();
                _settingsModal?.SetActive(true);
            });
            _settingsBtnLabel = settingsBtn.GetComponentInChildren<TextMeshProUGUI>();

            var langMenuBtn = MissionFlowController.CreateSimpleTextMenuButton("LanguageMenuBtn", menuRect, theme, _textScaler, "", 0.088f, 0.295f, 0.38f, 0.360f, 36f, () =>
            {
                PlayClick();
                ShowLanguageGate(show: true);
            });
            _languageBtnLabel = langMenuBtn.GetComponentInChildren<TextMeshProUGUI>();

            var creditsBtn = MissionFlowController.CreateSimpleTextMenuButton("CreditsBtn", menuRect, theme, _textScaler, "", 0.088f, 0.215f, 0.38f, 0.280f, 36f, () =>
            {
                PlayClick();
                RefreshLocalizedTexts();
                _creditsModal?.SetActive(true);
            });
            _creditsBtnLabel = creditsBtn.GetComponentInChildren<TextMeshProUGUI>();

            var exitBtn = MissionFlowController.CreateSimpleTextMenuButton("ExitBtn", menuRect, theme, _textScaler, "", 0.088f, 0.135f, 0.38f, 0.200f, 36f, () =>
            {
                PlayClick();
                MissionFlowController.QuitApplication();
            });
            _exitBtnLabel = exitBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (exitBtn != null && WebGLPlatformHelper.IsWebGLPlatform)
                exitBtn.gameObject.SetActive(false);

            // Touch/mobile advisory notice
            var mobileRect = TerminalUiBuilder.CreateRect("MobileNoticeFooter", root);
            TerminalUiBuilder.SetAnchors(mobileRect, 0.088f, 0.03f, 0.91f, 0.09f);
            _mobileNoticeText = TerminalUiBuilder.CreateText("MobileNoticeText", mobileRect, theme, 20f, new Color(0.95f, 0.76f, 0.45f, 0.95f), TextAlignmentOptions.MidlineLeft, _textScaler);
            _mobileNoticeText.font = theme.ResolveStampFont();
            _mobileNoticeText.raycastTarget = false;
            TerminalUiBuilder.SetStretch(_mobileNoticeText.rectTransform);
            _mobileNoticeGo = mobileRect.gameObject;
            _mobileNoticeGo.SetActive(WebGLPlatformHelper.ShouldShowTouchDeviceNotice());

            BuildSettingsModal(root);
            BuildHowToPlayModal(root);
            BuildCreditsModal(root);
        }

        private void BuildLanguageGatePanel(RectTransform root, Color accentOrange)
        {
            var box = TerminalUiBuilder.CreateFlatPanel("LanguageGatePanel", root, new Color(0.05f, 0.05f, 0.08f, 0.92f), accentOrange, 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.088f, 0.16f, 0.485f, 0.55f);
            _languageGatePanel = box.gameObject;
            var inner = TerminalUiBuilder.GetInner(box);

            _langGateTitleText = TerminalUiBuilder.CreateText("LangGateTitle", inner, theme, 27f, Color.white, TextAlignmentOptions.Center, _textScaler);
            _langGateTitleText.font = theme.ResolveStampFont();
            _langGateTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_langGateTitleText.rectTransform, 0.05f, 0.77f, 0.95f, 0.94f);

            var dividerRect = TerminalUiBuilder.CreateRect("GateDivider", inner);
            TerminalUiBuilder.SetAnchors(dividerRect, 0.14f, 0.745f, 0.86f, 0.753f);
            var dividerImg = dividerRect.gameObject.AddComponent<Image>();
            dividerImg.color = new Color(accentOrange.r, accentOrange.g, accentOrange.b, 0.55f);
            dividerImg.raycastTarget = false;

            _gateSwitch = CreatePolishedLanguageSwitch("Gate", inner, 0.06f, 0.47f, 0.94f, 0.69f, accentOrange, OnLanguageSliderChanged);
            _gateLangSlider = _gateSwitch.Slider;

            _langGateHintText = TerminalUiBuilder.CreateText("LangGateHint", inner, theme, 19f, new Color(0.82f, 0.88f, 0.94f, 0.92f), TextAlignmentOptions.Center, _textScaler);
            TerminalUiBuilder.SetAnchors(_langGateHintText.rectTransform, 0.06f, 0.34f, 0.94f, 0.44f);

            _langGateKeysText = TerminalUiBuilder.CreateText("LangGateKeys", inner, theme, 15f, theme.TextMuted, TextAlignmentOptions.Center, _textScaler);
            TerminalUiBuilder.SetAnchors(_langGateKeysText.rectTransform, 0.06f, 0.25f, 0.94f, 0.33f);

            var continueBtn = TerminalUiBuilder.CreateButton("LanguageContinueBtn", inner, "", theme, theme.ButtonAccentBg, accentOrange, Color.white, 26f, () =>
            {
                ConfirmLanguageAndEnterMenu(playClick: true);
            }, _textScaler, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(continueBtn.GetComponent<RectTransform>(), 0.18f, 0.06f, 0.82f, 0.22f);
            _gateContinueLabel = continueBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private BinaryLanguageSwitch CreatePolishedLanguageSwitch(
            string prefix,
            RectTransform parent,
            float xMin,
            float yMin,
            float xMax,
            float yMax,
            Color accentOrange,
            Action<float> onChanged)
        {
            var row = TerminalUiBuilder.CreateRect(prefix + "LangRow", parent);
            TerminalUiBuilder.SetAnchors(row, xMin, yMin, xMax, yMax);

            Sprite rounded = TerminalUiArt.GetRoundedRect8px();

            // Left pill button ("English")
            var enBtn = CreateLanguagePillButton(prefix + "EnglishBtn", row, "English", rounded, theme.BorderUnfocused, out Image enBg, out TextMeshProUGUI enLbl);
            TerminalUiBuilder.SetAnchors(enBtn.GetComponent<RectTransform>(), 0.00f, 0.08f, 0.33f, 0.92f);

            // Right pill button ("العربية")
            var arBtn = CreateLanguagePillButton(prefix + "ArabicBtn", row, ArabicShaper.Shape("العربية"), rounded, theme.BorderUnfocused, out Image arBg, out TextMeshProUGUI arLbl);
            TerminalUiBuilder.SetAnchors(arBtn.GetComponent<RectTransform>(), 0.67f, 0.08f, 1.00f, 0.92f);

            // Center capsule switch track
            var trackRect = TerminalUiBuilder.CreateRect(prefix + "LangSlider", row);
            TerminalUiBuilder.SetAnchors(trackRect, 0.36f, 0.14f, 0.64f, 0.86f);
            var trackBorder = trackRect.gameObject.AddComponent<Image>();
            trackBorder.sprite = rounded;
            trackBorder.type = Image.Type.Sliced;
            trackBorder.color = new Color(accentOrange.r, accentOrange.g, accentOrange.b, 0.72f);

            var trackInner = TerminalUiBuilder.CreateRect("TrackInner", trackRect);
            TerminalUiBuilder.SetStretch(trackInner, 2f);
            var trackBg = trackInner.gameObject.AddComponent<Image>();
            trackBg.sprite = rounded;
            trackBg.type = Image.Type.Sliced;
            trackBg.color = new Color(0.05f, 0.07f, 0.11f, 0.98f);

            // Soft active track glow (tints subtly toward active side color)
            var glowRect = TerminalUiBuilder.CreateRect("TrackGlow", trackInner);
            TerminalUiBuilder.SetStretch(glowRect, 2f);
            var trackTint = glowRect.gameObject.AddComponent<Image>();
            trackTint.sprite = rounded;
            trackTint.type = Image.Type.Sliced;
            trackTint.color = new Color(accentOrange.r, accentOrange.g, accentOrange.b, 0.14f);
            trackTint.raycastTarget = false;

            // Subtle center detent mark between EN (0) and AR (1)
            var notch = TerminalUiBuilder.CreateRect("CenterNotch", trackInner);
            TerminalUiBuilder.SetAnchors(notch, 0.495f, 0.22f, 0.505f, 0.78f);
            var notchImg = notch.gameObject.AddComponent<Image>();
            notchImg.color = new Color(1f, 1f, 1f, 0.22f);
            notchImg.raycastTarget = false;

            // Smooth animated visual thumb capsule
            var thumbArea = TerminalUiBuilder.CreateRect("VisualThumbArea", trackInner);
            TerminalUiBuilder.SetStretch(thumbArea, 4f);
            var visualThumb = TerminalUiBuilder.CreateRect("VisualThumb", thumbArea);
            TerminalUiBuilder.SetAnchors(visualThumb, 0.02f, 0.04f, 0.48f, 0.96f);
            var thumbBg = visualThumb.gameObject.AddComponent<Image>();
            thumbBg.sprite = rounded;
            thumbBg.type = Image.Type.Sliced;
            thumbBg.color = theme.TextAmber;
            thumbBg.raycastTarget = false;

            var ridge = TerminalUiBuilder.CreateRect("ThumbRidge", visualThumb);
            TerminalUiBuilder.SetAnchors(ridge, 0.45f, 0.24f, 0.55f, 0.76f);
            var ridgeImg = ridge.gameObject.AddComponent<Image>();
            ridgeImg.color = new Color(0.08f, 0.07f, 0.06f, 0.45f);
            ridgeImg.raycastTarget = false;

            // Invisible native handle area so Slider drag/click behavior stays exact while VisualThumb glides cleanly
            var handleArea = TerminalUiBuilder.CreateRect("Handle Slide Area", trackInner);
            TerminalUiBuilder.SetStretch(handleArea, 2f);
            var handleRect = TerminalUiBuilder.CreateRect("Handle", handleArea);
            handleRect.sizeDelta = new Vector2(28f, 0f);
            var handleHitImg = handleRect.gameObject.AddComponent<Image>();
            handleHitImg.color = new Color(1f, 1f, 1f, 0f);

            var slider = trackRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = true;
            slider.handleRect = handleRect;
            slider.targetGraphic = trackBg;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            enBtn.onClick.AddListener(() => slider.value = 0f);
            arBtn.onClick.AddListener(() => slider.value = 1f);

            return new BinaryLanguageSwitch
            {
                Slider = slider,
                VisualThumb = visualThumb,
                TrackBg = trackBg,
                TrackBorder = trackBorder,
                TrackTint = trackTint,
                ThumbBg = thumbBg,
                EnglishBg = enBg,
                ArabicBg = arBg,
                EnglishLabel = enLbl,
                ArabicLabel = arLbl,
                VisualT = Localization.CurrentLanguage == Language.Arabic ? 1f : 0f
            };
        }

        private Button CreateLanguagePillButton(string name, RectTransform parent, string text, Sprite rounded, Color defaultBg, out Image pillBg, out TextMeshProUGUI label)
        {
            var rect = TerminalUiBuilder.CreateRect(name, parent);
            pillBg = rect.gameObject.AddComponent<Image>();
            if (rounded != null)
            {
                pillBg.sprite = rounded;
                pillBg.type = Image.Type.Sliced;
            }
            pillBg.color = defaultBg;

            label = TerminalUiBuilder.CreateText("Label", rect, theme, 23f, Color.white, TextAlignmentOptions.Center, _textScaler);
            label.font = theme.ResolveStampFont();
            label.fontStyle = FontStyles.Bold;
            label.text = text;
            label.raycastTarget = false;
            TerminalUiBuilder.SetStretch(label.rectTransform, 4f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = pillBg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            return btn;
        }

        private void UpdateLanguageSwitchVisual(BinaryLanguageSwitch sw, bool immediate)
        {
            if (sw == null || sw.VisualThumb == null) return;
            float targetT = Localization.CurrentLanguage == Language.Arabic ? 1f : 0f;
            sw.VisualT = immediate
                ? targetT
                : Mathf.MoveTowards(sw.VisualT, targetT, Time.unscaledDeltaTime * 8.5f);

            float eased = Mathf.SmoothStep(0f, 1f, sw.VisualT);
            float minX = Mathf.Lerp(0.02f, 0.52f, eased);
            float maxX = Mathf.Lerp(0.48f, 0.98f, eased);
            sw.VisualThumb.anchorMin = new Vector2(minX, 0.04f);
            sw.VisualThumb.anchorMax = new Vector2(maxX, 0.96f);
            sw.VisualThumb.offsetMin = Vector2.zero;
            sw.VisualThumb.offsetMax = Vector2.zero;

            Color enAccent = new Color(0.95f, 0.68f, 0.25f, 1f);
            Color arAccent = new Color(0.24f, 0.78f, 0.84f, 1f);
            Color activeThumb = Color.Lerp(enAccent, arAccent, eased);
            if (sw.ThumbBg != null) sw.ThumbBg.color = activeThumb;
            if (sw.TrackBorder != null) sw.TrackBorder.color = Color.Lerp(enAccent, arAccent, eased) * 0.85f;
            if (sw.TrackTint != null)
            {
                Color tint = Color.Lerp(enAccent, arAccent, eased);
                tint.a = 0.16f;
                sw.TrackTint.color = tint;
            }

            Color inactivePill = new Color(0.08f, 0.10f, 0.15f, 0.85f);
            Color enActivePill = new Color(0.32f, 0.21f, 0.09f, 0.95f);
            Color arActivePill = new Color(0.08f, 0.27f, 0.32f, 0.95f);
            if (sw.EnglishBg != null) sw.EnglishBg.color = Color.Lerp(enActivePill, inactivePill, eased);
            if (sw.ArabicBg != null) sw.ArabicBg.color = Color.Lerp(inactivePill, arActivePill, eased);
            if (sw.EnglishLabel != null) sw.EnglishLabel.color = Color.Lerp(Color.white, theme.TextMuted, eased);
            if (sw.ArabicLabel != null) sw.ArabicLabel.color = Color.Lerp(theme.TextMuted, Color.white, eased);
        }

        private void BuildSettingsModal(RectTransform root)
        {
            _settingsModal = CreateOverlayBg(root, "SettingsModal", new Color(0.02f, 0.02f, 0.04f, 0.82f));
            var box = TerminalUiBuilder.CreateFlatPanel("SettingsBox", _settingsModal.GetComponent<RectTransform>(), new Color(0.08f, 0.06f, 0.07f, 0.96f), new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.26f, 0.11f, 0.74f, 0.89f);
            var inner = TerminalUiBuilder.GetInner(box);

            _settingsTitleText = TerminalUiBuilder.CreateText("SettingsTitle", inner, theme, 36f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            _settingsTitleText.font = theme.ResolveTitleFont();
            _settingsTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_settingsTitleText.rectTransform, 0f, 0.87f, 1f, 0.97f);

            const float topY = 0.83f;
            BuildVolSliderRow(inner, "MasterVol", Localization.Get("pause.volume"), topY - 0.045f, topY, topY - 0.095f, topY - 0.052f, 0.90f, out _masterVolHeaderText, out _masterVolSlider, out _masterVolText, v =>
            {
                _masterVolume = Mathf.Clamp01(v);
                PlayerPrefs.SetFloat(AudioManager.PrefMasterVolume, _masterVolume);
                PlayerPrefs.Save();
                RefreshSettingsUi();
            });

            BuildVolSliderRow(inner, "VoxVol", Localization.Get("pause.vox_volume"), topY - 0.148f, topY - 0.103f, topY - 0.198f, topY - 0.155f, 0.68f, out _voxVolHeaderText, out _voxVolSlider, out _voxVolText, v =>
            {
                _voxVolume = Mathf.Clamp01(v);
                PlayerPrefs.SetFloat(AnnouncerSystem.PrefVolume, _voxVolume);
                PlayerPrefs.Save();
                RefreshSettingsUi();
            });

            var muteBtn = TerminalUiBuilder.CreateButton("VoxMuteBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 19f, () =>
            {
                PlayClick();
                _voxMuted = !_voxMuted;
                PlayerPrefs.SetInt(AnnouncerSystem.PrefMuted, _voxMuted ? 1 : 0);
                PlayerPrefs.Save();
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(muteBtn.GetComponent<RectTransform>(), 0.70f, topY - 0.202f, 0.90f, topY - 0.151f);
            _voxMuteLabel = muteBtn.GetComponentInChildren<TextMeshProUGUI>();

            var largeBtn = TerminalUiBuilder.CreateButton("LargerTextBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                if (accessibility != null) accessibility.SetLargerText(!accessibility.LargerText);
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(largeBtn.GetComponent<RectTransform>(), 0.10f, topY - 0.29f, 0.48f, topY - 0.215f);
            _largeTextLabel = largeBtn.GetComponentInChildren<TextMeshProUGUI>();

            var flickerBtn = TerminalUiBuilder.CreateButton("ReduceFlickerBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                if (accessibility != null)
                {
                    bool next = !accessibility.ReduceFlicker;
                    accessibility.SetReduceFlicker(next);
                    accessibility.SetReduceMotion(next);
                    ApplyPsxGlobals();
                }
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(flickerBtn.GetComponent<RectTransform>(), 0.52f, topY - 0.29f, 0.90f, topY - 0.215f);
            _reduceFlickerLabel = flickerBtn.GetComponentInChildren<TextMeshProUGUI>();

            _gfxHeaderText = TerminalUiBuilder.CreateText("GraphicsLabel", inner, theme, 22f, theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            _gfxHeaderText.font = theme.ResolveStampFont();
            TerminalUiBuilder.SetAnchors(_gfxHeaderText.rectTransform, 0.10f, topY - 0.345f, 0.90f, topY - 0.305f);

            var psxBtn = TerminalUiBuilder.CreateButton("PsxLookBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                if (psxSettings != null)
                {
                    psxSettings.SetEnabled(!psxSettings.enabled, savePrefs: true);
                    ApplyPsxGlobals();
                }
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(psxBtn.GetComponent<RectTransform>(), 0.10f, topY - 0.435f, 0.48f, topY - 0.355f);
            _psxEnabledLabel = psxBtn.GetComponentInChildren<TextMeshProUGUI>();

            var resBtn = TerminalUiBuilder.CreateButton("PsxResBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                if (psxSettings != null)
                {
                    int next = ((int)psxSettings.preset + 1) % 4;
                    psxSettings.ApplyPreset((PSXResolutionPreset)next, savePrefs: true);
                    ApplyPsxGlobals();
                }
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(resBtn.GetComponent<RectTransform>(), 0.52f, topY - 0.435f, 0.90f, topY - 0.355f);
            _psxResLabel = resBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Inline polished Language Switch row inside Settings (0 = English, 1 = Arabic)
            _settingsLangHeaderText = TerminalUiBuilder.CreateText("SettingsLangLbl", inner, theme, 20f, theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_settingsLangHeaderText.rectTransform, 0.10f, topY - 0.56f, 0.36f, topY - 0.46f);

            _settingsSwitch = CreatePolishedLanguageSwitch("Settings", inner, 0.38f, topY - 0.565f, 0.90f, topY - 0.455f, new Color(0.86f, 0.45f, 0.27f, 1f), OnLanguageSliderChanged);
            _settingsLangSlider = _settingsSwitch.Slider;

            var howBtn = TerminalUiBuilder.CreateButton("HowToPlayBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                _settingsModal.SetActive(false);
                _howToPlayModal?.SetActive(true);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(howBtn.GetComponent<RectTransform>(), 0.10f, 0.05f, 0.48f, 0.16f);
            _settingsHowBtnLabel = howBtn.GetComponentInChildren<TextMeshProUGUI>();

            var closeBtn = TerminalUiBuilder.CreateButton("CloseSettingsBtn", inner, "", theme, theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                _settingsModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.52f, 0.05f, 0.90f, 0.16f);
            _settingsCloseBtnLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();

            _settingsModal.SetActive(false);
        }

        private void BuildVolSliderRow(RectTransform inner, string name, string label, float lblMin, float lblMax, float sldMin, float sldMax, float sldXMax, out TextMeshProUGUI headerText, out Slider slider, out TextMeshProUGUI valueText, Action<float> onChanged)
        {
            headerText = TerminalUiBuilder.CreateText(name + "Label", inner, theme, 20f, theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            headerText.text = label;
            TerminalUiBuilder.SetAnchors(headerText.rectTransform, 0.10f, lblMin, 0.65f, lblMax);

            valueText = TerminalUiBuilder.CreateText(name + "Value", inner, theme, 20f, theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
            TerminalUiBuilder.SetAnchors(valueText.rectTransform, 0.65f, lblMin, 0.90f, lblMax);

            var panel = TerminalUiBuilder.CreateFlatPanel(name + "Slider", inner, theme.BarTrackBg, theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(panel, 0.10f, sldMin, sldXMax, sldMax);
            var sInner = TerminalUiBuilder.GetInner(panel);

            var fillArea = TerminalUiBuilder.CreateRect("Fill Area", sInner);
            TerminalUiBuilder.SetStretch(fillArea, 2f);
            var fillRect = TerminalUiBuilder.CreateRect("Fill", fillArea);
            TerminalUiBuilder.SetStretch(fillRect);
            fillRect.gameObject.AddComponent<Image>().color = theme.StatusOk;

            var handleArea = TerminalUiBuilder.CreateRect("Handle Slide Area", sInner);
            TerminalUiBuilder.SetStretch(handleArea, 4f);
            var handleRect = TerminalUiBuilder.CreateRect("Handle", handleArea);
            handleRect.sizeDelta = new Vector2(18f, 0f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.gameObject.AddComponent<Image>().color = theme.TextAmber;

            slider = panel.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleRect.GetComponent<Image>();
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
        }

        private void BuildHowToPlayModal(RectTransform root)
        {
            _howToPlayModal = CreateOverlayBg(root, "HowToPlayModal", new Color(0.02f, 0.02f, 0.04f, 0.86f));
            var box = TerminalUiBuilder.CreateFlatPanel("HowBox", _howToPlayModal.GetComponent<RectTransform>(), theme.WindowBg, new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.18f, 0.20f, 0.82f, 0.80f);
            var inner = TerminalUiBuilder.GetInner(box);

            _howTitleText = TerminalUiBuilder.CreateText("HowTitle", inner, theme, 34f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            _howTitleText.font = theme.ResolveTitleFont();
            _howTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_howTitleText.rectTransform, 0.04f, 0.80f, 0.96f, 0.95f);

            _howBodyText = TerminalUiBuilder.CreateText("HowBody", inner, theme, 25f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_howBodyText.rectTransform, 0.07f, 0.24f, 0.93f, 0.78f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseHowBtn", inner, "", theme, theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), theme.TextPrimary, 24f, () =>
            {
                PlayClick();
                _howToPlayModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.36f, 0.06f, 0.64f, 0.18f);
            _howCloseLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();

            _howToPlayModal.SetActive(false);
        }

        private void BuildCreditsModal(RectTransform root)
        {
            _creditsModal = CreateOverlayBg(root, "CreditsPanel", theme.DesktopBg);
            var mRoot = _creditsModal.GetComponent<RectTransform>();

            _creditsTitleText = TerminalUiBuilder.CreateText("CreditsTitle", mRoot, theme, 36f, theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            _creditsTitleText.font = theme.ResolveTitleFont();
            _creditsTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_creditsTitleText.rectTransform, 0.05f, 0.89f, 0.95f, 0.97f);

            _creditsBodyText = TerminalUiBuilder.CreateScrollableText("CreditsScroll", mRoot, theme, 22f, theme.TextPrimary, theme.PanelBg, theme.BorderFocused, out var scrollPanel, _textScaler);
            _creditsBodyText.font = theme.ResolveFont();
            TerminalUiBuilder.SetAnchors(scrollPanel, 0.08f, 0.13f, 0.92f, 0.88f);

            var backBtn = TerminalUiBuilder.CreateButton("CreditsBackBtn", mRoot, "", theme, theme.ButtonAccentBg, theme.BorderFocused, theme.TextPrimary, 24f, () =>
            {
                PlayClick();
                _creditsModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(backBtn.GetComponent<RectTransform>(), 0.38f, 0.03f, 0.62f, 0.10f);
            _creditsBackLabel = backBtn.GetComponentInChildren<TextMeshProUGUI>();

            _creditsModal.SetActive(false);
        }

        private void RefreshLocalizedTexts()
        {
            bool isArabic = Localization.CurrentLanguage == Language.Arabic;

            if (_marsTitleText != null)
            {
                _marsTitleText.characterSpacing = isArabic ? 0f : 6f;
                _marsTitleText.text = Localization.Get("title.mars");
            }
            if (_lastShiftText != null)
            {
                _lastShiftText.characterSpacing = isArabic ? 0f : 5f;
                _lastShiftText.text = Localization.Get("title.last_shift");
            }

            if (_langGateTitleText != null) _langGateTitleText.text = Localization.Get("lang.gate_title");
            if (_langGateHintText != null) _langGateHintText.text = Localization.Get("title.subtitle");
            if (_langGateKeysText != null) _langGateKeysText.text = Localization.Get("lang.gate_controls");
            if (_gateContinueLabel != null) _gateContinueLabel.text = Localization.Get("lang.continue");
            if (_gateLangSlider != null) _gateLangSlider.SetValueWithoutNotify(isArabic ? 1f : 0f);
            UpdateLanguageSwitchVisual(_gateSwitch, immediate: false);

            if (_playBtnLabel != null) _playBtnLabel.text = Localization.Get("title.play_game");
            if (_settingsBtnLabel != null) _settingsBtnLabel.text = Localization.Get("title.settings");
            if (_languageBtnLabel != null) _languageBtnLabel.text = Localization.Get("title.language");
            if (_creditsBtnLabel != null) _creditsBtnLabel.text = Localization.Get("title.credits");
            if (_exitBtnLabel != null) _exitBtnLabel.text = Localization.Get("title.exit");
            if (_mobileNoticeText != null) _mobileNoticeText.text = Localization.Get("title.mobile_notice");

            if (_settingsTitleText != null) _settingsTitleText.text = Localization.Get("pause.settings_title");
            if (_masterVolHeaderText != null) _masterVolHeaderText.text = Localization.Get("pause.volume");
            if (_voxVolHeaderText != null) _voxVolHeaderText.text = Localization.Get("pause.vox_volume");
            if (_gfxHeaderText != null) _gfxHeaderText.text = Localization.Get("pause.graphics");
            if (_settingsLangHeaderText != null) _settingsLangHeaderText.text = Localization.Get("lang.slider_label");
            if (_settingsHowBtnLabel != null) _settingsHowBtnLabel.text = Localization.Get("title.how_to_play");
            if (_settingsCloseBtnLabel != null) _settingsCloseBtnLabel.text = Localization.Get("title.close");

            if (_howTitleText != null) _howTitleText.text = Localization.Get("title.how_to_play");
            if (_howBodyText != null) _howBodyText.text = Localization.Get("title.how_to_play.body");
            if (_howCloseLabel != null) _howCloseLabel.text = Localization.Get("title.close");

            if (_creditsTitleText != null) _creditsTitleText.text = Localization.Get("credits.title");
            if (_creditsBodyText != null) _creditsBodyText.text = MissionFlowController.BuildCreditsBodyText(sourcesDatabase);
            if (_creditsBackLabel != null) _creditsBackLabel.text = Localization.Get("title.close");
        }

        private void RefreshSettingsUi()
        {
            bool isArabic = Localization.CurrentLanguage == Language.Arabic;
            string onWord = Localization.GetRaw("pause.on");
            string offWord = Localization.GetRaw("pause.off");

            if (_masterVolSlider != null) _masterVolSlider.SetValueWithoutNotify(_masterVolume);
            if (_masterVolText != null) _masterVolText.text = $"{Mathf.RoundToInt(_masterVolume * 100f)}%";
            if (_voxVolSlider != null) _voxVolSlider.SetValueWithoutNotify(_voxVolume);
            if (_voxVolText != null) _voxVolText.text = _voxMuted ? Localization.Get("pause.vox_muted") : $"{Mathf.RoundToInt(_voxVolume * 100f)}%";
            if (_voxMuteLabel != null) _voxMuteLabel.text = Localization.Get(_voxMuted ? "pause.vox_muted" : "pause.vox_active");
            if (_largeTextLabel != null) _largeTextLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.larger_text")}: {((accessibility != null && accessibility.LargerText) ? onWord : offWord)}");
            if (_reduceFlickerLabel != null) _reduceFlickerLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.reduce_flicker")}: {((accessibility != null && accessibility.ReduceFlicker) ? onWord : offWord)}");
            if (_psxEnabledLabel != null) _psxEnabledLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.psx_look")}: {((psxSettings != null && psxSettings.enabled) ? onWord : offWord)}");
            string presetLbl = psxSettings != null
                ? (psxSettings.preset == PSXResolutionPreset.Off ? Localization.GetRaw("psx.res.native") : psxSettings.GetPresetLabel())
                : offWord;
            if (_psxResLabel != null) _psxResLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.psx_res")}: {presetLbl}");
            if (_settingsLangSlider != null) _settingsLangSlider.SetValueWithoutNotify(isArabic ? 1f : 0f);
            UpdateLanguageSwitchVisual(_settingsSwitch, immediate: false);
        }

        private static GameObject CreateOverlayBg(RectTransform parent, string name, Color color)
        {
            var rect = TerminalUiBuilder.CreateRect(name, parent);
            TerminalUiBuilder.SetStretch(rect);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect.gameObject;
        }
    }
}
