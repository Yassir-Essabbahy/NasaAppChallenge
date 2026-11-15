using System;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Standalone Main Menu controller for MainMenu.unity.
    /// Renders the left-aligned "Mars / Last Shift" title and borderless menu over the live
    /// rotating 3D Mars sphere backdrop, and handles Settings, Credits, Exit, and scene loading into Outpost.
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

        private GameObject _settingsModal;
        private GameObject _howToPlayModal;
        private GameObject _creditsModal;

        private Slider _masterVolSlider;
        private TextMeshProUGUI _masterVolText;
        private Slider _voxVolSlider;
        private TextMeshProUGUI _voxVolText;
        private TextMeshProUGUI _voxMuteLabel;
        private TextMeshProUGUI _largeTextLabel;
        private TextMeshProUGUI _reduceFlickerLabel;
        private TextMeshProUGUI _psxEnabledLabel;
        private TextMeshProUGUI _psxResLabel;

        private float _masterVolume = 0.75f;
        private float _voxVolume = 0.85f;
        private bool _voxMuted;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

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

        private void Start()
        {
            BuildUi();
            ApplyPsxGlobals();
            RefreshSettingsUi();
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

            // Left-aligned Title matching "Mars / Last Shift" aesthetic
            var marsTitle = TerminalUiBuilder.CreateText("MarsTitle", root, theme, 118f, accentOrange, TextAlignmentOptions.BottomLeft, _textScaler);
            marsTitle.font = theme.ResolveTitleFont();
            marsTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;
            marsTitle.characterSpacing = 6f;
            marsTitle.text = "Mars";
            marsTitle.raycastTarget = false;
            TerminalUiBuilder.SetAnchors(marsTitle.rectTransform, 0.088f, 0.695f, 0.50f, 0.88f);

            var ruleRect = TerminalUiBuilder.CreateRect("TitleAccentRule", root);
            TerminalUiBuilder.SetAnchors(ruleRect, 0.088f, 0.687f, 0.44f, 0.691f);
            var ruleImg = ruleRect.gameObject.AddComponent<Image>();
            ruleImg.color = accentOrange;
            ruleImg.raycastTarget = false;

            var subTitle = TerminalUiBuilder.CreateText("LastShiftSubtitle", root, theme, 52f, accentOrange, TextAlignmentOptions.TopLeft, _textScaler);
            subTitle.font = theme.ResolveTitleFont();
            subTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;
            subTitle.characterSpacing = 5f;
            subTitle.text = "Last Shift";
            subTitle.raycastTarget = false;
            TerminalUiBuilder.SetAnchors(subTitle.rectTransform, 0.090f, 0.59f, 0.50f, 0.68f);

            // Borderless plain-text menu buttons
            MissionFlowController.CreateSimpleTextMenuButton("PlayGameBtn", root, theme, _textScaler, "Play Game", 0.088f, 0.435f, 0.36f, 0.505f, 36f, () =>
            {
                PlayClick();
                if (!string.IsNullOrEmpty(outpostSceneName))
                    SceneManager.LoadScene(outpostSceneName);
            });

            MissionFlowController.CreateSimpleTextMenuButton("SettingsBtn", root, theme, _textScaler, "Settings", 0.088f, 0.345f, 0.36f, 0.415f, 36f, () =>
            {
                PlayClick();
                RefreshSettingsUi();
                _settingsModal?.SetActive(true);
            });

            MissionFlowController.CreateSimpleTextMenuButton("CreditsBtn", root, theme, _textScaler, "Credits", 0.088f, 0.255f, 0.36f, 0.325f, 36f, () =>
            {
                PlayClick();
                _creditsModal?.SetActive(true);
            });

            MissionFlowController.CreateSimpleTextMenuButton("ExitBtn", root, theme, _textScaler, "Exit", 0.088f, 0.165f, 0.36f, 0.235f, 36f, () =>
            {
                PlayClick();
                MissionFlowController.QuitApplication();
            });

            BuildSettingsModal(root);
            BuildHowToPlayModal(root);
            BuildCreditsModal(root);
        }

        private void BuildSettingsModal(RectTransform root)
        {
            _settingsModal = CreateOverlayBg(root, "SettingsModal", new Color(0.02f, 0.02f, 0.04f, 0.82f));
            var box = TerminalUiBuilder.CreateFlatPanel("SettingsBox", _settingsModal.GetComponent<RectTransform>(), new Color(0.08f, 0.06f, 0.07f, 0.96f), new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.26f, 0.14f, 0.74f, 0.86f);
            var inner = TerminalUiBuilder.GetInner(box);

            var title = TerminalUiBuilder.CreateText("SettingsTitle", inner, theme, 36f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            title.font = theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = "SETTINGS";
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0f, 0.86f, 1f, 0.97f);

            const float topY = 0.80f;
            BuildVolSliderRow(inner, "MasterVol", Localization.Get("pause.volume"), topY - 0.05f, topY, topY - 0.105f, topY - 0.06f, 0.90f, out _masterVolSlider, out _masterVolText, v =>
            {
                _masterVolume = Mathf.Clamp01(v);
                PlayerPrefs.SetFloat(AudioManager.PrefMasterVolume, _masterVolume);
                PlayerPrefs.Save();
                RefreshSettingsUi();
            });

            BuildVolSliderRow(inner, "VoxVol", "ANNOUNCER (VOX)", topY - 0.165f, topY - 0.115f, topY - 0.22f, topY - 0.175f, 0.68f, out _voxVolSlider, out _voxVolText, v =>
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
            TerminalUiBuilder.SetAnchors(muteBtn.GetComponent<RectTransform>(), 0.70f, topY - 0.224f, 0.90f, topY - 0.171f);
            _voxMuteLabel = muteBtn.GetComponentInChildren<TextMeshProUGUI>();

            var largeBtn = TerminalUiBuilder.CreateButton("LargerTextBtn", inner, "", theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                if (accessibility != null) accessibility.SetLargerText(!accessibility.LargerText);
                RefreshSettingsUi();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(largeBtn.GetComponent<RectTransform>(), 0.10f, topY - 0.32f, 0.48f, topY - 0.24f);
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
            TerminalUiBuilder.SetAnchors(flickerBtn.GetComponent<RectTransform>(), 0.52f, topY - 0.32f, 0.90f, topY - 0.24f);
            _reduceFlickerLabel = flickerBtn.GetComponentInChildren<TextMeshProUGUI>();

            var gfxHeader = TerminalUiBuilder.CreateText("GraphicsLabel", inner, theme, 22f, theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            gfxHeader.font = theme.ResolveStampFont();
            gfxHeader.text = "GRAPHICS";
            TerminalUiBuilder.SetAnchors(gfxHeader.rectTransform, 0.10f, topY - 0.38f, 0.90f, topY - 0.335f);

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
            TerminalUiBuilder.SetAnchors(psxBtn.GetComponent<RectTransform>(), 0.10f, topY - 0.48f, 0.48f, topY - 0.39f);
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
            TerminalUiBuilder.SetAnchors(resBtn.GetComponent<RectTransform>(), 0.52f, topY - 0.48f, 0.90f, topY - 0.39f);
            _psxResLabel = resBtn.GetComponentInChildren<TextMeshProUGUI>();

            var howBtn = TerminalUiBuilder.CreateButton("HowToPlayBtn", inner, Localization.Get("title.how_to_play"), theme, theme.ButtonBg, theme.BorderUnfocused, theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                _settingsModal.SetActive(false);
                _howToPlayModal?.SetActive(true);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(howBtn.GetComponent<RectTransform>(), 0.10f, 0.06f, 0.48f, 0.18f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseSettingsBtn", inner, Localization.Get("title.close"), theme, theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), theme.TextPrimary, 22f, () =>
            {
                PlayClick();
                _settingsModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.52f, 0.06f, 0.90f, 0.18f);

            _settingsModal.SetActive(false);
        }

        private void BuildVolSliderRow(RectTransform inner, string name, string label, float lblMin, float lblMax, float sldMin, float sldMax, float sldXMax, out Slider slider, out TextMeshProUGUI valueText, Action<float> onChanged)
        {
            var lbl = TerminalUiBuilder.CreateText(name + "Label", inner, theme, 20f, theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            lbl.text = label;
            TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.10f, lblMin, 0.65f, lblMax);

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

            var hTitle = TerminalUiBuilder.CreateText("HowTitle", inner, theme, 34f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            hTitle.font = theme.ResolveTitleFont();
            hTitle.fontStyle = FontStyles.Bold;
            hTitle.text = Localization.Get("title.how_to_play");
            TerminalUiBuilder.SetAnchors(hTitle.rectTransform, 0.04f, 0.80f, 0.96f, 0.95f);

            var hBody = TerminalUiBuilder.CreateText("HowBody", inner, theme, 25f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            hBody.text = Localization.Get("title.how_to_play.body");
            TerminalUiBuilder.SetAnchors(hBody.rectTransform, 0.07f, 0.24f, 0.93f, 0.78f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseHowBtn", inner, Localization.Get("title.close"), theme, theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), theme.TextPrimary, 24f, () =>
            {
                PlayClick();
                _howToPlayModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.36f, 0.06f, 0.64f, 0.18f);

            _howToPlayModal.SetActive(false);
        }

        private void BuildCreditsModal(RectTransform root)
        {
            _creditsModal = CreateOverlayBg(root, "CreditsPanel", theme.DesktopBg);
            var mRoot = _creditsModal.GetComponent<RectTransform>();

            var title = TerminalUiBuilder.CreateText("CreditsTitle", mRoot, theme, 36f, theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            title.font = theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("credits.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0.05f, 0.89f, 0.95f, 0.97f);

            var bodyText = TerminalUiBuilder.CreateScrollableText("CreditsScroll", mRoot, theme, 22f, theme.TextPrimary, theme.PanelBg, theme.BorderFocused, out var scrollPanel, _textScaler);
            bodyText.font = theme.ResolveFont();
            bodyText.text = MissionFlowController.BuildCreditsBodyText(sourcesDatabase);
            TerminalUiBuilder.SetAnchors(scrollPanel, 0.08f, 0.13f, 0.92f, 0.88f);

            var backBtn = TerminalUiBuilder.CreateButton("CreditsBackBtn", mRoot, Localization.Get("title.close"), theme, theme.ButtonAccentBg, theme.BorderFocused, theme.TextPrimary, 24f, () =>
            {
                PlayClick();
                _creditsModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(backBtn.GetComponent<RectTransform>(), 0.38f, 0.03f, 0.62f, 0.10f);

            _creditsModal.SetActive(false);
        }

        private void RefreshSettingsUi()
        {
            if (_masterVolSlider != null) _masterVolSlider.SetValueWithoutNotify(_masterVolume);
            if (_masterVolText != null) _masterVolText.text = $"{Mathf.RoundToInt(_masterVolume * 100f)}%";
            if (_voxVolSlider != null) _voxVolSlider.SetValueWithoutNotify(_voxVolume);
            if (_voxVolText != null) _voxVolText.text = _voxMuted ? "MUTED" : $"{Mathf.RoundToInt(_voxVolume * 100f)}%";
            if (_voxMuteLabel != null) _voxMuteLabel.text = _voxMuted ? "UNMUTE" : "MUTE";
            if (_largeTextLabel != null) _largeTextLabel.text = $"{Localization.Get("pause.larger_text")}: {((accessibility != null && accessibility.LargerText) ? "ON" : "OFF")}";
            if (_reduceFlickerLabel != null) _reduceFlickerLabel.text = $"{Localization.Get("pause.reduce_flicker")}: {((accessibility != null && accessibility.ReduceFlicker) ? "ON" : "OFF")}";
            if (_psxEnabledLabel != null) _psxEnabledLabel.text = $"{Localization.Get("pause.psx_look")}: {((psxSettings != null && psxSettings.enabled) ? "ON" : "OFF")}";
            if (_psxResLabel != null) _psxResLabel.text = $"{Localization.Get("pause.psx_res")}: {(psxSettings != null ? psxSettings.GetPresetLabel() : "OFF")}";
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
