using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Orchestrates the Milestone 4 full game flow:
    /// Title -> Intro (with introSeen skip) -> Boot & Commander Login -> Commander's Log ->
    /// Room Pause Menu (Esc) -> Win/Lose Endings -> Full-Screen Debrief & Credits.
    /// </summary>
    public class MissionFlowController : MonoBehaviour
    {
        public enum FlowState
        {
            Title,
            Intro,
            BootSequence,
            LoginPrompt,
            Gameplay,
            PauseMenu,
            EndSolBlackCard,
            EndingSequence,
            Debrief,
            Credits
        }

        private const string IntroSeenPrefsKey = "introSeen";

        [Header("Flow Options")]
        [SerializeField] private bool startAtTitleScreen = true;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Title Wallpaper")]
        [SerializeField] private Texture2D titleWallpaperTexture;

        private GameSession _session;
        private UITheme _theme;
        private FontTheme _fontTheme;
        private IntroSequenceData _introData;
        private PSXSettings _psxSettings;
        private SourcesDatabase _sourcesDb;
        private FactDatabase _factDb;
        private TerminalController _terminalController;
        private FirstPersonRoomController _roomController;
        private RoomStateBinder _roomStateBinder;
        private PlayerGate _playerGate;
        private AudioManager _audioManager;
        private AnnouncerSystem _announcerSystem;
        private AccessibilitySettings _accessibility;
        private AccessibilityTextScaler _textScaler;

        private Canvas _flowCanvas;
        private FlowState _state = FlowState.Title;
        private FlowState _creditsReturnState = FlowState.Title;
        private int _stateEnteredFrame = -1;
        private bool _built;
        private bool _skipRequested;
        private float _skipHoldSeconds;
        private Coroutine _activeRoutine;

        // Fade overlay (black)
        private GameObject _fadeBlackGo;
        private CanvasGroup _fadeBlackGroup;

        // Title UI
        private GameObject _titlePanel;
        private GameObject _titleMarsRig;
        private GameObject _titleFallbackWallpaperGo;
        private TextMeshProUGUI _titlePressAnyKeyText;
        private Button _titleStartBtn;
        private GameObject _titleHowBtnGo;
        private GameObject _titleQuickStartBtnGo;
        private GameObject _titleCredBtnGo;
        private GameObject _howToPlayModal;
        private GameObject _titleSettingsModal;
        private GameObject _titleMenuGroup;
        private GameObject _titleAudioUnlockGate;
        private GameObject _titleMobileNoticeGo;
        private Slider _titleVolumeSlider;
        private TextMeshProUGUI _titleVolumeValText;
        private Slider _titleVoxVolumeSlider;
        private TextMeshProUGUI _titleVoxVolumeValText;
        private TextMeshProUGUI _titleVoxMuteLabel;
        private TextMeshProUGUI _titleLargeTextLabel;
        private TextMeshProUGUI _titleReduceFlickerLabel;
        private TextMeshProUGUI _titlePsxEnabledLabel;
        private TextMeshProUGUI _titlePsxResLabel;

        // Cinematic Intro UI
        private GameObject _introPanel;
        private RectTransform _introCardContainer;
        private CanvasGroup _introCardGroup;
        private Vector2 _introBasePos;
        private RawImage _introGrainImage;
        private RawImage _introVignetteImage;
        private Texture2D _introGrainTex;
        private Texture2D _introVignetteTex;
        private GameObject _introBigGroup;
        private TextMeshProUGUI _introBigText;
        private GameObject _introUnderlineGo;
        private GameObject _introStampGroup;
        private TextMeshProUGUI _introStampText;
        private GameObject _introSmallGroup;
        private TextMeshProUGUI _introSmallText;
        private GameObject _introSlamGroup;
        private TextMeshProUGUI _introSlamMainText;
        private TextMeshProUGUI _introSlamSubText;
        private GameObject _introSkipContainer;
        private Image _introSkipFill;
        private TextMeshProUGUI _introSkipLabel;

        // Boot & Login UI
        private GameObject _bootLoginPanel;
        private TextMeshProUGUI _bootConsoleText;
        private GameObject _loginGroup;
        private TMP_InputField _loginInput;

        // Pause Menu UI
        private GameObject _pausePanel;
        private bool _pausedByPointerLoss;
        private bool _lastFullscreenUiState;
        private TextMeshProUGUI _pausePointerHintText;
        private TextMeshProUGUI _pauseResumeLabel;
        private TextMeshProUGUI _pauseFullscreenLabel;
        private Slider _pauseVolumeSlider;
        private TextMeshProUGUI _pauseVolumeValText;
        private Slider _pauseVoxVolumeSlider;
        private TextMeshProUGUI _pauseVoxVolumeValText;
        private TextMeshProUGUI _pauseVoxMuteLabel;
        private Slider _pauseMouseSensSlider;
        private TextMeshProUGUI _pauseMouseSensValText;
        private TextMeshProUGUI _pauseLargeTextLabel;
        private TextMeshProUGUI _pauseReduceFlickerLabel;
        private TextMeshProUGUI _pausePsxEnabledLabel;
        private TextMeshProUGUI _pausePsxResLabel;

        // Ending UI
        private GameObject _endingPanel;
        private GameObject _endingBlackBg;
        private TextMeshProUGUI _endingTerminalText;
        private TextMeshProUGUI _endingBlackText;
        private GameObject _endingUnderlineGo;

        // Debrief UI
        private GameObject _debriefPanel;
        private TextMeshProUGUI _debriefHeaderText;
        private TextMeshProUGUI _debriefSummaryText;
        private TextMeshProUGUI _debriefLeftText;
        private RectTransform _debriefRightContent;

        // Credits UI
        private GameObject _creditsPanel;
        private TextMeshProUGUI _creditsBodyText;

        // End-Sol Black Card UI (canvas sorted at 80: above WorldView/HUD/Terminal/Subtitles, below Pause 90 & Debug 100)
        private Canvas _endSolCanvas;
        private AccessibilityTextScaler _endSolTextScaler;
        private GameObject _endSolPanel;
        private CanvasGroup _endSolBackdropGroup;
        private RectTransform _endSolCardContainer;
        private CanvasGroup _endSolCardGroup;
        private RawImage _endSolGrainImage;
        private RawImage _endSolVignetteImage;
        private TextMeshProUGUI _endSolTopLineText;
        private TextMeshProUGUI _endSolBigLineText;
        private GameObject _endSolRuleGo;
        private TextMeshProUGUI _endSolSubLineText;
        private GameObject _endSolSkipContainer;
        private Image _endSolSkipFill;
        private TextMeshProUGUI _endSolSkipLabel;
        private bool _hasPlayedEndSolBlackCardThisSession;
        private bool _endSolSkipAllowed;
        private bool _endSolSkipRequested;
        private float _endSolSkipHoldSeconds;

        private readonly StringBuilder _sb = new StringBuilder(1024);

        public FlowState CurrentState => _state;

        public void SetAnnouncerSystem(AnnouncerSystem announcer)
        {
            _announcerSystem = announcer;
            RefreshPauseLabels();
        }

        public void Initialize(
            GameSession session,
            UITheme theme,
            SourcesDatabase sourcesDb,
            FactDatabase factDb,
            TerminalController terminalController,
            FirstPersonRoomController roomController,
            RoomStateBinder roomStateBinder,
            PlayerGate playerGate,
            AudioManager audioManager,
            AccessibilitySettings accessibility,
            bool runTitleFlow,
            FontTheme fontTheme = null,
            IntroSequenceData introData = null,
            PSXSettings psxSettings = null)
        {
            UnsubscribeEvents();

            _session = session;
            _theme = theme != null ? theme : ScriptableObject.CreateInstance<UITheme>();
            _fontTheme = fontTheme != null ? fontTheme : (_theme.FontTheme != null ? _theme.FontTheme : Resources.Load<FontTheme>("FontTheme"));
            if (_theme.FontTheme == null && _fontTheme != null)
                _theme.FontTheme = _fontTheme;
            _introData = introData != null ? introData : Resources.Load<IntroSequenceData>("IntroSequenceData");
            if (_introData == null)
            {
                _introData = ScriptableObject.CreateInstance<IntroSequenceData>();
                _introData.PopulateDefaultCards();
            }
            _psxSettings = psxSettings != null ? psxSettings : Resources.Load<PSXSettings>("PSXSettings");
            _sourcesDb = sourcesDb != null ? sourcesDb : Resources.Load<SourcesDatabase>("SourcesDatabase");
            _factDb = factDb != null ? factDb : Resources.Load<FactDatabase>("FactDatabase");
            _terminalController = terminalController;
            _roomController = roomController;
            _roomStateBinder = roomStateBinder;
            _playerGate = playerGate;
            _audioManager = audioManager;
            _accessibility = accessibility;
            startAtTitleScreen = runTitleFlow;

            if (!_built)
            {
                BuildFlowCanvas();
                _built = true;
            }

            if (_accessibility != null && _textScaler != null)
                _accessibility.BindTextScaler(_textScaler);

            SubscribeEvents();

            if (startAtTitleScreen)
            {
                if (_terminalController != null)
                {
                    _terminalController.UseExternalEndingFlow = true;
                    _terminalController.SetDeferInitialEvent(true);
                }

                // When dedicated MainMenu scene is registered in Build Settings, Outpost.unity launches straight into the intro/shift.
                if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
                    StartIntroSequence();
                else
                    EnterTitleState();
            }
            else
            {
                HideAllFlowPanels();
                _state = FlowState.Gameplay;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            Time.timeScale = 1f;
            if (_introGrainTex != null) Destroy(_introGrainTex);
            if (_introVignetteTex != null) Destroy(_introVignetteTex);
        }

        private void SubscribeEvents()
        {
            if (_session != null)
                _session.OnMissionEnded += HandleMissionEnded;

            if (_roomController != null)
            {
                _roomController.OnPauseRequested += HandlePauseRequested;
                _roomController.OnPointerLockLostPauseRequested += HandlePointerLockLostPauseRequested;
                _roomController.OnEnteredTerminal += HandlePlayerEnteredTerminal;
                _roomController.OnExitedTerminal += HandlePlayerExitedTerminal;
                _roomController.ExternalLeaveBlockCheck = () => _state != FlowState.Gameplay;
                _roomController.IsPauseMenuOpenCheck = () => _state == FlowState.PauseMenu;
            }

            if (_terminalController != null)
            {
                _terminalController.OnEndSolBlackCardRequested = HandleEndSolBlackCardRequested;
                _terminalController.IsExternalBlackCardPlaying = () => _state == FlowState.EndSolBlackCard;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _terminalController.OnDebugTriggerEndSolBlackCardRequested = DebugPlayEndSolBlackCard;
                _terminalController.OnDebugSkipIntroRequested = DebugSkipIntro;
                _terminalController.OnDebugReplayIntroRequested = DebugReplayIntro;
                _terminalController.OnDebugResetIntroSeenRequested = DebugResetIntroSeen;
                _terminalController.OnDebugJumpToDebriefRequested = DebugJumpToDebrief;
                _terminalController.OnDebugTeleportSpawnRequested = DebugTeleportToSpawn;
#endif
            }
        }

        private void UnsubscribeEvents()
        {
            if (_session != null)
                _session.OnMissionEnded -= HandleMissionEnded;

            if (_roomController != null)
            {
                _roomController.OnPauseRequested -= HandlePauseRequested;
                _roomController.OnPointerLockLostPauseRequested -= HandlePointerLockLostPauseRequested;
                _roomController.OnEnteredTerminal -= HandlePlayerEnteredTerminal;
                _roomController.OnExitedTerminal -= HandlePlayerExitedTerminal;
                _roomController.ExternalLeaveBlockCheck = null;
                _roomController.IsPauseMenuOpenCheck = null;
            }

            if (_terminalController != null)
            {
                _terminalController.OnEndSolBlackCardRequested = null;
                _terminalController.IsExternalBlackCardPlaying = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _terminalController.OnDebugTriggerEndSolBlackCardRequested = null;
                _terminalController.OnDebugSkipIntroRequested = null;
                _terminalController.OnDebugReplayIntroRequested = null;
                _terminalController.OnDebugResetIntroSeenRequested = null;
                _terminalController.OnDebugJumpToDebriefRequested = null;
                _terminalController.OnDebugTeleportSpawnRequested = null;
#endif
            }
        }

        private void Update()
        {
            if (!_built) return;
            var kb = Keyboard.current;

            if (_state == FlowState.Title)
            {
                if (_titlePressAnyKeyText != null)
                {
                    float a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                    Color c = _theme.TextAmber;
                    c.a = a;
                    _titlePressAnyKeyText.color = c;
                }

                if (_howToPlayModal != null && _howToPlayModal.activeSelf)
                {
                    if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    {
                        _audioManager?.PlayUiClick();
                        _howToPlayModal.SetActive(false);
                    }
                    return;
                }

                if (_titleSettingsModal != null && _titleSettingsModal.activeSelf)
                {
                    if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    {
                        _audioManager?.PlayUiClick();
                        _titleSettingsModal.SetActive(false);
                    }
                    return;
                }

                if (_titleAudioUnlockGate != null && _titleAudioUnlockGate.activeSelf)
                {
                    if (Time.frameCount > _stateEnteredFrame && WebGLPlatformHelper.HasUnlockGestureThisFrame())
                    {
                        UnlockFallbackTitleAudioGate(playClick: true);
                    }
                    return;
                }

                if (Time.frameCount > _stateEnteredFrame && ShouldStartFromTitleKeyboard(kb))
                {
                    WebGLPlatformHelper.MarkAudioUnlocked();
                    _audioManager?.PlayUiClick();
                    StartIntroSequence();
                }
            }
            else if (_state == FlowState.Credits)
            {
                if (kb != null && Time.frameCount > _stateEnteredFrame && kb.escapeKey.wasPressedThisFrame)
                {
                    ReturnFromCredits();
                }
            }
            else if (_state == FlowState.Intro)
            {
                bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
                if (_introGrainImage != null)
                    _introGrainImage.gameObject.SetActive(!reduceFlicker);
                if (_introVignetteImage != null)
                    _introVignetteImage.gameObject.SetActive(!reduceFlicker);

                bool holdingSpace = kb != null && kb.spaceKey.isPressed;
                if (holdingSpace && Time.frameCount > _stateEnteredFrame)
                {
                    if (_introSkipContainer != null && !_introSkipContainer.activeSelf)
                        _introSkipContainer.SetActive(true);
                    _skipHoldSeconds += Time.unscaledDeltaTime;
                    if (_introSkipFill != null)
                        _introSkipFill.fillAmount = Mathf.Clamp01(_skipHoldSeconds / 0.8f);
                    if (_skipHoldSeconds >= 0.8f)
                        _skipRequested = true;
                }
                else
                {
                    _skipHoldSeconds = Mathf.Max(0f, _skipHoldSeconds - Time.unscaledDeltaTime * 2.5f);
                    if (_introSkipFill != null)
                        _introSkipFill.fillAmount = Mathf.Clamp01(_skipHoldSeconds / 0.8f);
                }
            }
            else if (_state == FlowState.LoginPrompt)
            {
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                {
                    ConfirmCommanderLogin();
                }
            }
            else if (_state == FlowState.PauseMenu)
            {
                if (_pauseFullscreenLabel != null && _lastFullscreenUiState != Screen.fullScreen)
                {
                    _lastFullscreenUiState = Screen.fullScreen;
                    _pauseFullscreenLabel.text = $"{Localization.Get("pause.fullscreen")}: {(_lastFullscreenUiState ? "ON" : "OFF")}";
                }
                if (kb != null && Time.frameCount > _stateEnteredFrame && kb.escapeKey.wasPressedThisFrame)
                {
                    ResumeFromPause();
                }
            }
            else if (_state == FlowState.EndSolBlackCard)
            {
                bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
                if (_endSolGrainImage != null)
                    _endSolGrainImage.gameObject.SetActive(!reduceFlicker);
                if (_endSolVignetteImage != null)
                    _endSolVignetteImage.gameObject.SetActive(!reduceFlicker);
                if (_endSolTextScaler != null)
                    _endSolTextScaler.SetLargerText(_accessibility != null && _accessibility.LargerText);

                if (_endSolSkipAllowed)
                {
                    var mouse = Mouse.current;
                    bool holding = (kb != null && kb.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);
                    if (holding && Time.frameCount > _stateEnteredFrame)
                    {
                        if (_endSolSkipContainer != null && !_endSolSkipContainer.activeSelf)
                            _endSolSkipContainer.SetActive(true);
                        _endSolSkipHoldSeconds += Time.unscaledDeltaTime;
                        SetEndSolSkipProgress(_endSolSkipHoldSeconds / 0.8f);
                        if (_endSolSkipHoldSeconds >= 0.8f)
                            _endSolSkipRequested = true;
                    }
                    else
                    {
                        _endSolSkipHoldSeconds = Mathf.Max(0f, _endSolSkipHoldSeconds - Time.unscaledDeltaTime * 2.5f);
                        SetEndSolSkipProgress(_endSolSkipHoldSeconds / 0.8f);
                    }
                }
            }
        }

        private bool ShouldStartFromTitleKeyboard(Keyboard kb)
        {
            if (kb == null) return false;
            if (_terminalController != null && _terminalController.IsDebugPanelVisible) return false;

            bool confirmPressed =
                kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame ||
                kb.spaceKey.wasPressedThisFrame;
            if (!confirmPressed) return false;

            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected != null && (_titleStartBtn == null || selected != _titleStartBtn.gameObject))
                return false;

            return true;
        }

        private void StopActiveRoutine()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
        }

        private void HideAllFlowPanels()
        {
            if (_titleMarsRig == null) _titleMarsRig = GameObject.Find("TitleMarsRig");
            if (_titleMarsRig != null) _titleMarsRig.SetActive(false);
            if (_titlePanel != null) _titlePanel.SetActive(false);
            if (_howToPlayModal != null) _howToPlayModal.SetActive(false);
            if (_titleSettingsModal != null) _titleSettingsModal.SetActive(false);
            if (_introPanel != null) _introPanel.SetActive(false);
            if (_bootLoginPanel != null) _bootLoginPanel.SetActive(false);
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_endingPanel != null) _endingPanel.SetActive(false);
            if (_debriefPanel != null) _debriefPanel.SetActive(false);
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
            if (_fadeBlackGo != null) _fadeBlackGo.SetActive(false);
            if (_endSolPanel != null) _endSolPanel.SetActive(false);
        }

        // --- STATE TRANSITIONS ---

        public void EnterTitleState()
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            if (_titleMarsRig == null) _titleMarsRig = GameObject.Find("TitleMarsRig");
            _state = FlowState.Title;
            _stateEnteredFrame = Time.frameCount;

            _audioManager?.StopIntroDrone(immediate: true);
            _audioManager?.SetEndingState(false, false);
            _roomStateBinder?.SetEndingLightingOverride(false, false);

            HideAllFlowPanels();
            if (_titleMarsRig != null) _titleMarsRig.SetActive(true);
            if (_titleFallbackWallpaperGo != null)
                _titleFallbackWallpaperGo.SetActive(_titleMarsRig == null && titleWallpaperTexture != null);
            _roomController?.TeleportPlayerToSpawn();
            _roomController?.SetFlowLocked(unlockCursor: true);
            EventSystem.current?.SetSelectedGameObject(null);
            RefreshTitleActionRow();
            ApplyFallbackTitleAudioGateState(WebGLPlatformHelper.AudioUnlocked);
            if (_titlePanel != null) _titlePanel.SetActive(true);
        }

        private void UnlockFallbackTitleAudioGate(bool playClick)
        {
            WebGLPlatformHelper.MarkAudioUnlocked();
            ApplyFallbackTitleAudioGateState(unlocked: true);
            if (playClick) _audioManager?.PlayUiClick();
        }

        private void ApplyFallbackTitleAudioGateState(bool unlocked)
        {
            if (_titleAudioUnlockGate != null) _titleAudioUnlockGate.SetActive(!unlocked);
            if (_titleMenuGroup != null) _titleMenuGroup.SetActive(unlocked);
        }

        private void RefreshTitleActionRow()
        {
            bool introSeen = PlayerPrefs.GetInt(IntroSeenPrefsKey, 0) == 1;
            if (_titleQuickStartBtnGo != null)
                _titleQuickStartBtnGo.SetActive(introSeen);

            if (_titleHowBtnGo == null || _titleCredBtnGo == null)
                return;

            var howRect = _titleHowBtnGo.GetComponent<RectTransform>();
            var credRect = _titleCredBtnGo.GetComponent<RectTransform>();
            if (introSeen && _titleQuickStartBtnGo != null)
            {
                var quickRect = _titleQuickStartBtnGo.GetComponent<RectTransform>();
                TerminalUiBuilder.SetAnchors(howRect, 0.08f, 0.10f, 0.34f, 0.20f);
                TerminalUiBuilder.SetAnchors(quickRect, 0.37f, 0.10f, 0.63f, 0.20f);
                TerminalUiBuilder.SetAnchors(credRect, 0.66f, 0.10f, 0.92f, 0.20f);
            }
            else
            {
                TerminalUiBuilder.SetAnchors(howRect, 0.14f, 0.10f, 0.47f, 0.20f);
                TerminalUiBuilder.SetAnchors(credRect, 0.53f, 0.10f, 0.86f, 0.20f);
            }
        }

        public void StartIntroSequence()
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            _state = FlowState.Intro;
            _stateEnteredFrame = Time.frameCount;
            _skipRequested = false;
            _skipHoldSeconds = 0f;

            HideAllFlowPanels();
            _roomController?.SetFlowLocked(unlockCursor: true);
            if (_introPanel != null) _introPanel.SetActive(true);
            HideAllIntroCardGroups();

            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
            if (_introGrainImage != null) _introGrainImage.gameObject.SetActive(!reduceFlicker);
            if (_introVignetteImage != null) _introVignetteImage.gameObject.SetActive(!reduceFlicker);
            if (_introSkipContainer != null) _introSkipContainer.SetActive(false);
            if (_introSkipFill != null) _introSkipFill.fillAmount = 0f;

            _audioManager?.StartIntroDrone();
            _activeRoutine = StartCoroutine(RunCinematicIntroRoutine());
        }

        private void HideAllIntroCardGroups()
        {
            if (_introBigGroup != null) _introBigGroup.SetActive(false);
            if (_introStampGroup != null) _introStampGroup.SetActive(false);
            if (_introSmallGroup != null) _introSmallGroup.SetActive(false);
            if (_introSlamGroup != null) _introSlamGroup.SetActive(false);
            if (_introCardGroup != null) _introCardGroup.alpha = 1f;
            if (_introCardContainer != null)
            {
                _introCardContainer.localScale = Vector3.one;
                _introCardContainer.anchoredPosition = _introBasePos;
            }
        }

        private IEnumerator RunCinematicIntroRoutine()
        {
            if (_introData == null || _introData.Cards == null || _introData.Cards.Count == 0)
            {
                _introData = ScriptableObject.CreateInstance<IntroSequenceData>();
                _introData.PopulateDefaultCards();
            }

            Coroutine grainRoutine = StartCoroutine(AnimateFilmGrainRoutine());

            var cards = _introData.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (_skipRequested) break;
                if (i >= 1 && _introSkipContainer != null)
                    _introSkipContainer.SetActive(true);

                yield return PlayIntroCardRoutine(cards[i]);
            }

            if (grainRoutine != null) StopCoroutine(grainRoutine);
            HideAllIntroCardGroups();
            _audioManager?.StopIntroDrone(immediate: false);

            PlayerPrefs.SetInt(IntroSeenPrefsKey, 1);
            PlayerPrefs.Save();
            _activeRoutine = null;

            StartBootAndLogin(skipTyping: false);
        }

        private IEnumerator AnimateFilmGrainRoutine()
        {
            while (_state == FlowState.Intro && !_skipRequested)
            {
                if (_introGrainImage != null && _introGrainImage.gameObject.activeSelf)
                {
                    _introGrainImage.uvRect = new Rect(
                        UnityEngine.Random.value,
                        UnityEngine.Random.value,
                        6f,
                        3.5f);
                }
                yield return WaitRealSeconds(0.05f);
            }
        }

        private IEnumerator PlayIntroCardRoutine(IntroCard card)
        {
            if (card == null) yield break;
            HideAllIntroCardGroups();

            if (card.type == IntroCardType.BlackHold)
            {
                yield return WaitSkippableSeconds(card.holdTime);
                yield break;
            }

            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;

            // Play card-entry SFX
            switch (card.sfxSlot)
            {
                case IntroSfxSlot.BassHit:
                    _audioManager?.PlayBassHit(card.customSfx);
                    break;
                case IntroSfxSlot.RadioCrackle:
                    _audioManager?.PlayRadioCrackle(card.customSfx);
                    break;
            }

            if (card.type == IntroCardType.BigWord)
            {
                if (_introBigGroup != null) _introBigGroup.SetActive(true);
                if (_introUnderlineGo != null) _introUnderlineGo.SetActive(true);
                bool isAr = Localization.CurrentLanguage == Language.Arabic;
                string raw = (card.textLineKeys != null && card.textLineKeys.Length > 0)
                    ? (isAr ? Localization.Get(card.textLineKeys[0]) : Localization.Get(card.textLineKeys[0]).ToUpperInvariant())
                    : string.Empty;
                if (_introBigText != null)
                {
                    _introBigText.color = Color.white;
                    _introBigText.text = isAr ? raw : FormatAccentWord(raw, card.accentWordIndex);
                    _introBigText.characterSpacing = isAr ? 0f : 40f;
                }

                yield return AnimateCardEntranceAndHold(_introBigText, card, slamScale: false, applyMeshJitter: !reduceFlicker);
            }
            else if (card.type == IntroCardType.TitleSlam)
            {
                bool isAr = Localization.CurrentLanguage == Language.Arabic;
                if (_introSlamGroup != null) _introSlamGroup.SetActive(true);
                if (_introSlamMainText != null)
                {
                    _introSlamMainText.text = (card.textLineKeys != null && card.textLineKeys.Length > 0)
                        ? (isAr ? Localization.Get(card.textLineKeys[0]) : Localization.Get(card.textLineKeys[0]).ToUpperInvariant())
                        : Localization.Get("title.mars");
                    _introSlamMainText.characterSpacing = isAr ? 0f : 28f;
                }
                if (_introSlamSubText != null)
                {
                    _introSlamSubText.text = (card.textLineKeys != null && card.textLineKeys.Length > 1)
                        ? (isAr ? Localization.Get(card.textLineKeys[1]) : Localization.Get(card.textLineKeys[1]).ToUpperInvariant())
                        : Localization.Get("title.last_shift");
                    _introSlamSubText.characterSpacing = isAr ? 0f : 36f;
                }

                yield return AnimateCardEntranceAndHold(_introSlamMainText, card, slamScale: true, applyMeshJitter: false);
            }
            else if (card.type == IntroCardType.Stamp)
            {
                if (_introStampGroup != null) _introStampGroup.SetActive(true);
                if (_introStampText != null) _introStampText.text = string.Empty;
                yield return TypeMultiLineCardRoutine(_introStampText, card.textLineKeys, charsPerSec: 30f, playClicks: true);
                if (!_skipRequested)
                    yield return BlinkCursorHoldRoutine(_introStampText, card.holdTime);
            }
            else if (card.type == IntroCardType.SmallLines)
            {
                if (_introSmallGroup != null) _introSmallGroup.SetActive(true);
                if (_introSmallText != null)
                {
                    _introSmallText.color = _theme.TextPrimary;
                    _introSmallText.text = string.Empty;
                }
                yield return TypeMultiLineCardRoutine(_introSmallText, card.textLineKeys, charsPerSec: 30f, playClicks: true);
                if (!_skipRequested)
                    yield return BlinkCursorHoldRoutine(_introSmallText, card.holdTime);
            }
            else if (card.type == IntroCardType.RadioLine)
            {
                if (_introSmallGroup != null) _introSmallGroup.SetActive(true);
                if (_introSmallText != null)
                {
                    _introSmallText.color = new Color(0.88f, 0.90f, 0.72f, 1f);
                    string line = (card.textLineKeys != null && card.textLineKeys.Length > 0)
                        ? Localization.Get(card.textLineKeys[0])
                        : string.Empty;
                    _introSmallText.text = line;
                }
                yield return AnimateCardEntranceAndHold(_introSmallText, card, slamScale: false, applyMeshJitter: false);
            }

            if (!_skipRequested && !card.hardCutExit && card.fadeOutTime > 0.01f && _introCardGroup != null)
            {
                float t = 0f;
                while (t < card.fadeOutTime && !_skipRequested)
                {
                    t += Time.unscaledDeltaTime;
                    _introCardGroup.alpha = 1f - Mathf.Clamp01(t / card.fadeOutTime);
                    yield return null;
                }
            }
        }

        private IEnumerator AnimateCardEntranceAndHold(TextMeshProUGUI text, IntroCard card, bool slamScale, bool applyMeshJitter)
        {
            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
            AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            float entranceDur = Mathf.Max(0.05f, card.fadeInTime);
            float totalDur = entranceDur + Mathf.Max(0.1f, card.holdTime);
            float elapsed = 0f;
            float flickerWindow = reduceFlicker ? 0f : 0.25f;

            while (elapsed < totalDur && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / totalDur);
                float curved = ease.Evaluate(u);

                if (text != null)
                {
                    bool isAr = Localization.CurrentLanguage == Language.Arabic;
                    text.characterSpacing = isAr ? 0f : Mathf.Lerp(40f, 18f, curved);
                    if (applyMeshJitter && elapsed < flickerWindow)
                        ApplyPerCharacterSettlementJitter(text, 1f - (elapsed / flickerWindow));
                    else if (applyMeshJitter && elapsed >= flickerWindow)
                        text.ForceMeshUpdate();
                }

                if (_introCardGroup != null)
                {
                    float baseAlpha = elapsed < entranceDur ? Mathf.Clamp01(elapsed / entranceDur) : 1f;
                    if (!reduceFlicker && elapsed < flickerWindow)
                    {
                        float jitterAlpha = UnityEngine.Random.Range(0.45f, 1.0f);
                        _introCardGroup.alpha = baseAlpha * jitterAlpha;
                    }
                    else
                    {
                        _introCardGroup.alpha = baseAlpha;
                    }
                }

                if (_introCardContainer != null)
                {
                    float s;
                    if (slamScale)
                    {
                        if (elapsed < entranceDur)
                        {
                            float se = Mathf.Clamp01(elapsed / entranceDur);
                            s = Mathf.Lerp(1.65f, 1.0f, se * se);
                        }
                        else
                        {
                            float hs = Mathf.Clamp01((elapsed - entranceDur) / Mathf.Max(0.1f, card.holdTime));
                            s = Mathf.Lerp(1.0f, 1.05f, hs);
                        }
                    }
                    else
                    {
                        s = Mathf.Lerp(1.0f, 1.06f, curved);
                    }
                    _introCardContainer.localScale = new Vector3(s, s, 1f);

                    if (!reduceFlicker && card.uiShakeIntensity > 0.01f)
                    {
                        float shakeDecay = elapsed < 0.35f ? 1.0f : Mathf.Lerp(0.35f, 0.10f, u);
                        float amp = card.uiShakeIntensity * shakeDecay;
                        _introCardContainer.anchoredPosition = _introBasePos + new Vector2(
                            UnityEngine.Random.Range(-amp, amp),
                            UnityEngine.Random.Range(-amp, amp));
                    }
                    else
                    {
                        _introCardContainer.anchoredPosition = _introBasePos;
                    }
                }

                yield return null;
            }

            if (_introCardContainer != null)
                _introCardContainer.anchoredPosition = _introBasePos;
            if (text != null)
                text.ForceMeshUpdate();
        }

        private static void ApplyPerCharacterSettlementJitter(TextMeshProUGUI tmp, float strength01)
        {
            if (tmp == null) return;
            tmp.ForceMeshUpdate();
            TMP_TextInfo info = tmp.textInfo;
            float amp = 5.5f * Mathf.Clamp01(strength01);
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                Vector3[] verts = info.meshInfo[c.materialReferenceIndex].vertices;
                int vIdx = c.vertexIndex;
                Vector3 off = new Vector3(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp), 0f);
                verts[vIdx] += off;
                verts[vIdx + 1] += off;
                verts[vIdx + 2] += off;
                verts[vIdx + 3] += off;
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                tmp.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        private IEnumerator TypeMultiLineCardRoutine(TextMeshProUGUI target, string[] lineKeys, float charsPerSec, bool playClicks)
        {
            if (target == null || lineKeys == null) yield break;
            float delay = 1f / Mathf.Max(5f, charsPerSec);
            _sb.Clear();
            for (int i = 0; i < lineKeys.Length; i++)
            {
                string line = Localization.Get(lineKeys[i]);
                if (i > 0) _sb.Append('\n');
                string prefix = _sb.ToString();
                for (int c = 1; c <= line.Length; c++)
                {
                    if (_skipRequested)
                    {
                        target.text = BuildAllLinesString(lineKeys);
                        yield break;
                    }
                    char ch = line[c - 1];
                    target.text = prefix + line.Substring(0, c) + "█";
                    if (playClicks && !char.IsWhiteSpace(ch))
                        _audioManager?.PlayTypeClick();
                    yield return WaitSkippableSeconds(delay);
                }
                _sb.Append(line);
                if (i < lineKeys.Length - 1)
                    yield return WaitSkippableSeconds(0.22f);
            }
            target.text = _sb.ToString();
        }

        private static string BuildAllLinesString(string[] lineKeys)
        {
            var tmpSb = new StringBuilder();
            for (int i = 0; i < lineKeys.Length; i++)
            {
                if (i > 0) tmpSb.Append('\n');
                tmpSb.Append(Localization.Get(lineKeys[i]));
            }
            return tmpSb.ToString();
        }

        private IEnumerator BlinkCursorHoldRoutine(TextMeshProUGUI target, float holdSeconds)
        {
            if (target == null)
            {
                yield return WaitSkippableSeconds(holdSeconds);
                yield break;
            }
            string clean = target.text;
            float elapsed = 0f;
            while (elapsed < holdSeconds && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                bool showBlock = ((int)(elapsed * 3.2f) % 2) == 0;
                target.text = showBlock ? (clean + " █") : clean;
                yield return null;
            }
            target.text = clean;
        }

        private IEnumerator WaitSkippableSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static string FormatAccentWord(string raw, int accentWordIndex)
        {
            if (accentWordIndex < 0 || string.IsNullOrEmpty(raw))
                return raw;
            string[] words = raw.Split(' ');
            if (accentWordIndex >= words.Length)
                return raw;
            words[accentWordIndex] = $"<color=#FFB84D>{words[accentWordIndex]}</color>";
            return string.Join(" ", words);
        }

        private void StartBootAndLogin(bool skipTyping)
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            _state = FlowState.BootSequence;
            _stateEnteredFrame = Time.frameCount;

            HideAllFlowPanels();
            _roomController?.TeleportPlayerToSpawn();
            _roomController?.EnterTerminalImmediateForFlow(null);
            _terminalController?.SetTerminalVisible(false);

            if (_bootLoginPanel != null) _bootLoginPanel.SetActive(true);
            if (_loginGroup != null) _loginGroup.SetActive(false);
            if (_bootConsoleText != null) _bootConsoleText.text = string.Empty;

            if (skipTyping)
            {
                _bootConsoleText.text =
                    Localization.Get("boot.line1") + "\n" +
                    Localization.Get("boot.line2") + "\n" +
                    Localization.Get("boot.line3") + "\n" +
                    Localization.Get("boot.line4");
                ShowLoginPrompt();
            }
            else
            {
                _activeRoutine = StartCoroutine(RunBootSequenceRoutine());
            }
        }

        private IEnumerator RunBootSequenceRoutine()
        {
            // Fade from black directly into the room & monitor at terminal focus
            if (_fadeBlackGo != null && _fadeBlackGroup != null)
            {
                _fadeBlackGo.SetActive(true);
                _fadeBlackGroup.alpha = 1f;
                float t = 0f;
                while (t < 0.75f)
                {
                    t += Time.unscaledDeltaTime;
                    _fadeBlackGroup.alpha = 1f - Mathf.Clamp01(t / 0.75f);
                    yield return null;
                }
                _fadeBlackGo.SetActive(false);
            }

            string[] bootLines =
            {
                Localization.GetRaw("boot.line1"),
                Localization.GetRaw("boot.line2"),
                Localization.GetRaw("boot.line3"),
                Localization.GetRaw("boot.line4")
            };

            _sb.Clear();
            for (int i = 0; i < bootLines.Length; i++)
            {
                string prefix = i == 0 ? "" : _sb.ToString() + "\n";
                yield return TypeTextAppendedRoutine(_bootConsoleText, prefix, bootLines[i], 0.022f, playClick: true);
                _sb.Clear();
                _sb.Append(prefix).Append(bootLines[i]);

                float pause = 0f;
                while (pause < 0.35f)
                {
                    pause += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            _activeRoutine = null;
            ShowLoginPrompt();
        }

        private void ShowLoginPrompt()
        {
            _state = FlowState.LoginPrompt;
            _stateEnteredFrame = Time.frameCount;
            if (_loginGroup != null) _loginGroup.SetActive(true);
            if (_loginInput != null)
            {
                _loginInput.text = Localization.Get("login.default");
                _loginInput.Select();
                _loginInput.ActivateInputField();
            }
        }

        public void ConfirmCommanderLogin()
        {
            if (_state != FlowState.LoginPrompt) return;
            _audioManager?.PlayUiClick();
            string rawName = _loginInput != null ? _loginInput.text : Localization.Get("login.default");
            _session?.SetCommanderName(rawName);

            if (_bootLoginPanel != null) _bootLoginPanel.SetActive(false);
            _state = FlowState.Gameplay;
            _stateEnteredFrame = Time.frameCount;

            _roomController?.EnterTerminalImmediateForFlow(null);
            _terminalController?.BeginFirstShiftCommandLog();
        }

        private void HandlePauseRequested() => OpenPauseMenu(pointerLockLost: false);

        private void HandlePointerLockLostPauseRequested() => OpenPauseMenu(pointerLockLost: true);

        private void OpenPauseMenu(bool pointerLockLost)
        {
            if (_state != FlowState.Gameplay) return;
            _pausedByPointerLoss = pointerLockLost;
            _state = FlowState.PauseMenu;
            _stateEnteredFrame = Time.frameCount;
            Time.timeScale = 0f;

            _roomController?.SetFlowLocked(unlockCursor: true);
            RefreshPauseLabels();
            if (_pausePanel != null) _pausePanel.SetActive(true);
        }

        public void ResumeFromPause()
        {
            if (_state != FlowState.PauseMenu) return;
            _pausedByPointerLoss = false;
            _audioManager?.PlayUiClick();
            Time.timeScale = 1f;
            if (_pausePanel != null) _pausePanel.SetActive(false);
            _state = FlowState.Gameplay;
            _stateEnteredFrame = Time.frameCount;
            _roomController?.EnterRoomViewImmediateForFlow();
        }

        private void HandlePlayerExitedTerminal()
        {
            _terminalController?.NotifyPlayerExitedTerminal();
        }

        private void HandlePlayerEnteredTerminal()
        {
            if (_state == FlowState.Gameplay)
                _terminalController?.NotifyPlayerEnteredTerminal();
        }

        private void HandleMissionEnded(SessionSnapshot snap)
        {
            if (snap == null || snap.Status == MissionStatus.InProgress) return;
            if (_state == FlowState.EndingSequence || _state == FlowState.Debrief) return;

            StopActiveRoutine();
            Time.timeScale = 1f;
            HideAllFlowPanels();

            _state = FlowState.EndingSequence;
            _stateEnteredFrame = Time.frameCount;

            bool won = snap.Status == MissionStatus.Won && snap.LivingCrewCount > 0;
            _roomController?.EnterTerminalImmediateForFlow(null);
            _terminalController?.SetTerminalVisible(false);

            _audioManager?.SetEndingState(true, won);
            _announcerSystem?.StopCurrent();
            _roomStateBinder?.SetEndingLightingOverride(true, won);

            if (_endingPanel != null) _endingPanel.SetActive(true);
            if (_endingBlackBg != null) _endingBlackBg.SetActive(false);
            if (_endingTerminalText != null) _endingTerminalText.text = string.Empty;
            if (_endingBlackText != null) _endingBlackText.text = string.Empty;

            _activeRoutine = StartCoroutine(RunEndingRoutine(snap, won));
        }

        private IEnumerator RunEndingRoutine(SessionSnapshot snap, bool won)
        {
            if (won)
            {
                _audioManager?.PlayRadioBlip();
                if (_endingTerminalText != null) _endingTerminalText.color = _theme.StatusOk;
                yield return TypeTextRoutine(_endingTerminalText, Localization.Get("ending.win.terminal"), 0.036f, playClick: true, checkSkip: false);

                yield return WaitRealSeconds(2.0f);

                if (_endingBlackBg != null) _endingBlackBg.SetActive(true);
                if (_endingUnderlineGo != null) _endingUnderlineGo.SetActive(true);

                string line1 = Localization.Get("ending.win.line1").ToUpperInvariant();
                yield return AnimateEndingBookendRoutine(line1, Color.white, 2.2f);

                string followUp = Localization.Get("ending.win.line2").ToUpperInvariant();
                if (snap.LivingCrewCount == 1)
                    followUp += "\n\n" + Localization.Get("ending.win.barely").ToUpperInvariant();

                yield return WaitRealSeconds(0.5f);
                yield return AnimateEndingBookendRoutine(line1 + "\n\n" + followUp, Color.white, 2.2f, showUnderline: false);
            }
            else
            {
                _audioManager?.PlayRadioBlip();
                if (_endingTerminalText != null) _endingTerminalText.color = _theme.StatusCrit;
                yield return TypeTextRoutine(_endingTerminalText, Localization.Get("ending.lose.terminal"), 0.045f, playClick: true, checkSkip: false);

                yield return WaitRealSeconds(2.0f);

                if (_endingBlackBg != null) _endingBlackBg.SetActive(true);
                if (_endingUnderlineGo != null) _endingUnderlineGo.SetActive(true);
                yield return AnimateEndingBookendRoutine(Localization.Get("ending.lose.line1").ToUpperInvariant(), _theme.TextPrimary, 2.4f);
            }

            yield return WaitRealSeconds(1.2f);
            _activeRoutine = null;
            EnterDebriefState();
        }

        private IEnumerator AnimateEndingBookendRoutine(string formattedText, Color color, float holdSeconds, bool showUnderline = true)
        {
            if (_endingUnderlineGo != null) _endingUnderlineGo.SetActive(showUnderline);
            if (_endingBlackText == null)
            {
                yield return WaitRealSeconds(holdSeconds);
                yield break;
            }

            _endingBlackText.color = new Color(color.r, color.g, color.b, 0f);
            _endingBlackText.text = formattedText;
            _endingBlackText.characterSpacing = 40f;
            _endingBlackText.rectTransform.localScale = Vector3.one;

            AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            float elapsed = 0f;
            float fadeIn = 0.7f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, holdSeconds));
                float a = Mathf.Clamp01(elapsed / fadeIn);
                _endingBlackText.color = new Color(color.r, color.g, color.b, a);
                _endingBlackText.characterSpacing = Mathf.Lerp(40f, 18f, ease.Evaluate(u));
                float scale = Mathf.Lerp(1.0f, 1.05f, u);
                _endingBlackText.rectTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
        }

        public void EnterDebriefState()
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            HideAllFlowPanels();

            _state = FlowState.Debrief;
            _stateEnteredFrame = Time.frameCount;
            _roomController?.SetFlowLocked(unlockCursor: true);

            PopulateDebrief(_session?.CurrentSnapshot);
            if (_debriefPanel != null) _debriefPanel.SetActive(true);
        }

        public void EnterCreditsState(FlowState returnTo)
        {
            _creditsReturnState = returnTo;
            HideAllFlowPanels();
            _state = FlowState.Credits;
            _stateEnteredFrame = Time.frameCount;

            PopulateCredits();
            if (_creditsPanel != null) _creditsPanel.SetActive(true);
        }

        public void ReturnFromCredits()
        {
            _audioManager?.PlayUiClick();
            if (_creditsReturnState == FlowState.Debrief)
                EnterDebriefState();
            else
                EnterTitleState();
        }

        public void RestartMissionRun(int seed, bool runIntro)
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            HideAllFlowPanels();

            _audioManager?.StopIntroDrone(immediate: true);
            _audioManager?.SetEndingState(false, false);
            _roomStateBinder?.SetEndingLightingOverride(false, false);

            _session?.RestartMission(seed);
            _terminalController?.ResetForNewRun(deferInitialEvent: true);
            _roomController?.TeleportPlayerToSpawn();

            if (runIntro)
                StartIntroSequence();
            else
                StartBootAndLogin(skipTyping: false);
        }

        public void QuitToTitle()
        {
            StopActiveRoutine();
            Time.timeScale = 1f;
            _audioManager?.StopIntroDrone(immediate: true);
            _audioManager?.SetEndingState(false, false);
            _announcerSystem?.StopCurrent();

            if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene(mainMenuSceneName);
                return;
            }

            int nextSeed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed + 1 : 101;
            _roomStateBinder?.SetEndingLightingOverride(false, false);
            _session?.RestartMission(nextSeed);
            _terminalController?.ResetForNewRun(deferInitialEvent: true);
            EnterTitleState();
        }

        // --- PART E DEV TOOLS ---

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSkipIntro()
        {
            PlayerPrefs.SetInt(IntroSeenPrefsKey, 1);
            PlayerPrefs.Save();
            _skipRequested = true;
            StopActiveRoutine();
            Time.timeScale = 1f;
            HideAllFlowPanels();

            _audioManager?.StopIntroDrone(immediate: true);
            _audioManager?.SetEndingState(false, false);
            _roomStateBinder?.SetEndingLightingOverride(false, false);

            _state = FlowState.Gameplay;
            _roomController?.TeleportPlayerToSpawn();
            _roomController?.EnterTerminalImmediateForFlow(null);
            _terminalController?.BeginFirstShiftCommandLog();
        }

        public void DebugReplayIntro()
        {
            StartIntroSequence();
        }

        public void DebugResetIntroSeen()
        {
            PlayerPrefs.DeleteKey(IntroSeenPrefsKey);
            PlayerPrefs.Save();
            _terminalController?.ShowToastMessage("introSeen PlayerPrefs cleared", 3.0f);
        }

        public void DebugJumpToDebrief()
        {
            EnterDebriefState();
        }

        public void DebugTeleportToSpawn()
        {
            Time.timeScale = 1f;
            HideAllFlowPanels();
            _state = FlowState.Gameplay;
            _roomController?.TeleportPlayerToSpawn();
            _roomController?.EnterRoomViewImmediateForFlow();
        }
#endif

        // --- DEBRIEF & CREDITS POPULATION ---

        private void PopulateDebrief(SessionSnapshot s)
        {
            if (s == null) return;

            bool won = s.Status == MissionStatus.Won && s.LivingCrewCount > 0;
            float avgHp = 0f;
            int alive = 0;
            for (int i = 0; i < s.Crew.Count; i++)
            {
                if (s.Crew[i].IsAlive)
                {
                    alive++;
                    avgHp += s.Crew[i].Health;
                }
            }
            if (alive > 0) avgHp /= alive;

            _debriefHeaderText.color = won ? _theme.StatusOk : _theme.StatusCrit;
            _debriefHeaderText.text = Localization.Format("debrief.header_fmt", Localization.GetRaw("debrief.title"), Localization.GetRaw(won ? "mission.won.title" : "mission.lost.title"), s.Grade);

            _debriefSummaryText.text = Localization.Format(
                "debrief.summary_fmt",
                s.CommanderName, s.Seed, alive, avgHp, s.FinalScore,
                s.Power, s.Oxygen, s.Water, s.Food, s.Shielding);

            // Left column: Crew outcomes + Key Decisions Timeline
            _sb.Clear();
            _sb.AppendLine($"<b><color=#FFB84D>{Localization.Get("debrief.crew")}</color></b>");
            for (int i = 0; i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                _sb.AppendLine(Localization.Format("debrief.crew_row_fmt", c.Name, c.Role, c.StatusTag, c.Health, c.Morale, c.DoseMsv));
            }

            _sb.AppendLine();
            _sb.AppendLine($"<b><color=#FFB84D>{Localization.Get("debrief.timeline")}</color></b>");
            if (s.DebriefCards.Count == 0)
            {
                _sb.AppendLine(Localization.Get("debrief.no_events"));
            }
            else
            {
                for (int i = 0; i < s.DebriefCards.Count; i++)
                {
                    var d = s.DebriefCards[i];
                    string choiceLabel = !string.IsNullOrEmpty(d.ChoiceLabelKey) ? Localization.GetRaw(d.ChoiceLabelKey) : Localization.GetRaw(d.OutcomeKey);
                    string outcomeText = Localization.GetRaw(d.OutcomeKey);
                    _sb.AppendLine(Localization.Format("debrief.timeline_row_fmt", d.Sol, Localization.GetRaw(d.TitleKey), choiceLabel, outcomeText));
                }
            }
            _debriefLeftText.text = _sb.ToString();

            // Right column: What NASA Knows (distinct FactCards with source titles & Open URL buttons)
            RebuildDebriefFactCards(s);
        }

        private void RebuildDebriefFactCards(SessionSnapshot s)
        {
            if (_debriefRightContent == null) return;
            for (int i = _debriefRightContent.childCount - 1; i >= 0; i--)
                Destroy(_debriefRightContent.GetChild(i).gameObject);

            var hdr = TerminalUiBuilder.CreateText("WhatNasaKnowsHdr", _debriefRightContent, _theme, 24f, _theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            hdr.fontStyle = FontStyles.Bold;
            hdr.text = Localization.Get("debrief.what_nasa_knows");

            var seenCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int cardCount = 0;

            if (s != null && s.DebriefCards != null)
            {
                for (int i = 0; i < s.DebriefCards.Count; i++)
                {
                    var d = s.DebriefCards[i];
                    string evId = d.EventId;
                    if (string.IsNullOrEmpty(evId)) continue;

                    FactCard card = _factDb != null ? _factDb.GetByEventId(evId) : null;
                    string cardKey = card != null && !string.IsNullOrEmpty(card.id) ? card.id : evId;
                    if (!seenCards.Add(cardKey)) continue;

                    AddDebriefFactCardItem(card, d.TitleKey, d.FactKey, d.SourceId);
                    cardCount++;
                }
            }

            // Include baseline radiation card once when crew received surface radiation
            bool hadRadExposure = s != null && s.Crew != null && s.Crew.Exists(c => c.DoseMsv > 0.05f);
            FactCard radCard = _factDb != null ? _factDb.GetById("fact_radiation_baseline") : null;
            if (radCard != null && (hadRadExposure || cardCount == 0) && seenCards.Add(radCard.id))
            {
                AddDebriefFactCardItem(radCard, radCard.titleKey, radCard.textKey, radCard.sourceId);
                cardCount++;
            }

            if (cardCount == 0 && _factDb != null && _factDb.Cards != null && _factDb.Cards.Count > 0)
            {
                var sample = _factDb.Cards[0];
                if (sample != null && seenCards.Add(sample.id ?? "sample"))
                    AddDebriefFactCardItem(sample, sample.titleKey, sample.textKey, sample.sourceId);
            }
        }

        private void AddDebriefFactCardItem(FactCard card, string fallbackTitleKey, string fallbackFactKey, string fallbackSourceId)
        {
            string title = card != null ? card.ResolveTitle() : Localization.Get(fallbackTitleKey);
            if (string.IsNullOrEmpty(title)) title = Localization.Get(fallbackTitleKey);
            string body = card != null ? card.ResolveText() : Localization.Get(fallbackFactKey);
            if (string.IsNullOrEmpty(body)) body = Localization.Get(fallbackFactKey);

            var panel = TerminalUiBuilder.CreateFlatPanel("FactCardRow", _debriefRightContent, _theme.DesktopBg, _theme.BorderUnfocused, 2f);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var inner = TerminalUiBuilder.GetInner(panel);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var titleTmp = TerminalUiBuilder.CreateText("CardTitle", inner, _theme, 21f, _theme.TextAmber, TextAlignmentOptions.TopLeft, _textScaler);
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.textWrappingMode = TextWrappingModes.Normal;
            titleTmp.text = title;

            var bodyTmp = TerminalUiBuilder.CreateText("CardBody", inner, _theme, 19f, _theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.text = body;

            IReadOnlyList<string> srcIds = card != null ? card.GetSourceIds() : null;
            if ((srcIds == null || srcIds.Count == 0) && !string.IsNullOrWhiteSpace(fallbackSourceId))
                srcIds = new[] { fallbackSourceId };

            if (srcIds != null)
            {
                for (int i = 0; i < srcIds.Count; i++)
                {
                    string sid = srcIds[i];
                    SourceRecord rec = _sourcesDb != null ? _sourcesDb.FindById(sid) : null;
                    string srcTitle = rec != null ? rec.ResolveTitle() : sid;
                    string srcOrg = rec != null ? rec.ResolveOrganization() : string.Empty;
                    string url = rec != null ? rec.UrlText : string.Empty;

                    var srcRow = TerminalUiBuilder.CreateRect($"SrcLink_{i}", inner);
                    var hlg = srcRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 8f;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;

                    var srcTmp = TerminalUiBuilder.CreateText("SrcLabel", srcRow, _theme, 17f, _theme.AccentHelp, TextAlignmentOptions.MidlineLeft, _textScaler);
                    srcTmp.textWrappingMode = TextWrappingModes.Normal;
                    string prefix = Localization.GetRaw("debrief.source_prefix");
                    srcTmp.text = Localization.ShapeDisplay(string.IsNullOrEmpty(srcOrg) ? $"{prefix} {srcTitle}" : $"{prefix} {srcTitle} ({srcOrg})");
                    var txtLe = srcTmp.gameObject.AddComponent<LayoutElement>();
                    txtLe.flexibleWidth = 1f;

                    var openBtn = TerminalUiBuilder.CreateButton("OpenSourceBtn", srcRow, Localization.Get("ui.open"), _theme, _theme.ButtonBg, _theme.AccentSources, _theme.TextPrimary, 16f, () =>
                    {
                        _audioManager?.PlayUiClick();
                        SourcesDatabase.OpenSourceUrl(url);
                    }, _textScaler);
                    var btnLe = openBtn.gameObject.AddComponent<LayoutElement>();
                    btnLe.preferredWidth = 86f;
                    btnLe.minWidth = 78f;
                    btnLe.preferredHeight = 32f;
                    openBtn.interactable = !string.IsNullOrWhiteSpace(url);
                }
            }
        }

        private void PopulateCredits()
        {
            _creditsBodyText.text = BuildCreditsBodyText(_sourcesDb, _sb);
        }

        public static string BuildCreditsBodyText(SourcesDatabase sourcesDb, StringBuilder sb = null)
        {
            sb ??= new StringBuilder(1024);
            sb.Clear();
            sb.AppendLine($"<b>{Localization.Get("game.title")}</b>");
            sb.AppendLine(Localization.Get("credits.challenge"));
            sb.AppendLine(Localization.Get("credits.unity"));
            sb.AppendLine(Localization.Get("credits.fonts"));
            sb.AppendLine();
            sb.AppendLine($"<b><color=#45E3D3>{Localization.Get("credits.assets_hdr")}</color></b>");

            if (sourcesDb != null && sourcesDb.AssetCredits != null)
            {
                string lastSec = string.Empty;
                for (int i = 0; i < sourcesDb.AssetCredits.Count; i++)
                {
                    var a = sourcesDb.AssetCredits[i];
                    if (a.Section != lastSec)
                    {
                        lastSec = a.Section;
                        sb.AppendLine($"<b><color=#7FDFFF>[{lastSec}]</color></b>");
                    }
                    sb.AppendLine(Localization.ShapeDisplay($"• {a.ResolveTitle()} — {a.ResolveOrganization()}"));
                    if (!string.IsNullOrEmpty(a.License))
                        sb.AppendLine(Localization.Format("credits.license_used", a.License, a.WhereUsed));
                }
            }

            sb.AppendLine();
            sb.AppendLine($"<b><color=#F7C948>{Localization.Get("credits.sources_hdr")}</color></b>");

            if (sourcesDb != null && sourcesDb.Entries != null)
            {
                for (int i = 0; i < sourcesDb.Entries.Count; i++)
                {
                    var e = sourcesDb.Entries[i];
                    string lic = string.IsNullOrEmpty(e.License) ? "UNVERIFIED - check before submission" : e.License;
                    sb.AppendLine($"• [{e.Id}] {e.ResolveTitle()} — {e.ResolveOrganization()}");
                    sb.AppendLine($"  {e.ResolveSupports()}");
                    sb.AppendLine($"  URL: {e.UrlText}  |  License: {lic}");
                }
            }
            return sb.ToString();
        }

        // --- COROUTINE TYPEWRITER HELPERS ---

        private IEnumerator TypeTextRoutine(TextMeshProUGUI target, string fullText, float charDelay, bool playClick, bool checkSkip)
        {
            yield return TypeTextAppendedRoutine(target, string.Empty, fullText, charDelay, playClick, checkSkip);
        }

        private IEnumerator TypeTextAppendedRoutine(TextMeshProUGUI target, string prefix, string line, float charDelay, bool playClick, bool checkSkip = false)
        {
            if (target == null) yield break;
            for (int i = 1; i <= line.Length; i++)
            {
                if (checkSkip && _skipRequested)
                {
                    target.text = Localization.ShapeDisplay(prefix + line);
                    yield break;
                }

                char c = line[i - 1];
                target.text = Localization.ShapeDisplay(prefix + line.Substring(0, i));
                if (playClick && !char.IsWhiteSpace(c))
                    _audioManager?.PlayTypeClick();

                float elapsed = 0f;
                while (elapsed < charDelay)
                {
                    if (checkSkip && _skipRequested)
                    {
                        target.text = Localization.ShapeDisplay(prefix + line);
                        yield break;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static IEnumerator WaitRealSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private char ValidateCommanderChar(string text, int charIndex, char addedChar)
        {
            return (char.IsLetterOrDigit(addedChar) || addedChar == ' ') ? addedChar : '\0';
        }

        // --- UI CONSTRUCTION (RUNS ONCE) ---

        private void BuildFlowCanvas()
        {
            var canvasGo = new GameObject("OutpostFlowCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _flowCanvas = canvasGo.GetComponent<Canvas>();
            _flowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _flowCanvas.sortingOrder = 30;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _textScaler = canvasGo.AddComponent<AccessibilityTextScaler>();

            var root = canvasGo.GetComponent<RectTransform>();
            BuildTitlePanel(root);
            BuildIntroPanel(root);
            BuildBootLoginPanel(root);
            BuildPausePanel(root);
            BuildEndingPanel(root);
            BuildDebriefPanel(root);
            BuildCreditsPanel(root);
            BuildBlackFadeOverlay(root);
            BuildEndSolBlackCardCanvas();
        }

        private GameObject CreateFullscreenBg(RectTransform parent, string name, Color color)
        {
            var rect = TerminalUiBuilder.CreateRect(name, parent);
            TerminalUiBuilder.SetStretch(rect);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect.gameObject;
        }

        private void BuildTitlePanel(RectTransform parent)
        {
            // Keep title background transparent so TitleBackdropCanvas (MainMenu.jpg + 3D MarsSphere_Title) shows through
            _titlePanel = CreateFullscreenBg(parent, "TitlePanel", new Color(0f, 0f, 0f, 0f));
            var root = _titlePanel.GetComponent<RectTransform>();

            // Fallback wallpaper if TitleMarsRig is absent in a standalone test scene
            if (titleWallpaperTexture != null)
            {
                var wpRect = TerminalUiBuilder.CreateRect("TitleFallbackWallpaper", root);
                TerminalUiBuilder.SetStretch(wpRect);
                var wpImg = wpRect.gameObject.AddComponent<RawImage>();
                wpImg.texture = titleWallpaperTexture;
                wpImg.color = Color.white;
                wpImg.raycastTarget = false;
                var arf = wpRect.gameObject.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                arf.aspectRatio = titleWallpaperTexture.width / (float)Mathf.Max(1, titleWallpaperTexture.height);
                _titleFallbackWallpaperGo = wpRect.gameObject;
                _titleFallbackWallpaperGo.SetActive(false);
            }

            // Minimal left-aligned menu under the wallpaper's "Mars / Last Shift" title (no borders, no icons/emojis, plain text)
            var menuGroupRect = TerminalUiBuilder.CreateRect("MenuButtonsGroup", root);
            TerminalUiBuilder.SetStretch(menuGroupRect);
            _titleMenuGroup = menuGroupRect.gameObject;

            _titleStartBtn = CreateSimpleTextMenuButton("PlayGameBtn", menuGroupRect, "Play Game", 0.435f, 0.505f, () =>
            {
                WebGLPlatformHelper.MarkAudioUnlocked();
                _audioManager?.PlayUiClick();
                StartIntroSequence();
            });

            CreateSimpleTextMenuButton("SettingsBtn", menuGroupRect, "Settings", 0.345f, 0.415f, () =>
            {
                _audioManager?.PlayUiClick();
                RefreshPauseLabels();
                if (_titleSettingsModal != null)
                    _titleSettingsModal.SetActive(true);
            });

            var credBtn = CreateSimpleTextMenuButton("CreditsBtn", menuGroupRect, "Credits", 0.255f, 0.325f, () =>
            {
                _audioManager?.PlayUiClick();
                EnterCreditsState(FlowState.Title);
            });
            _titleCredBtnGo = credBtn.gameObject;

            var exitBtn = CreateSimpleTextMenuButton("ExitBtn", menuGroupRect, "Exit", 0.165f, 0.235f, () =>
            {
                _audioManager?.PlayUiClick();
                QuitApplication();
            });
            if (exitBtn != null && WebGLPlatformHelper.IsWebGLPlatform)
                exitBtn.gameObject.SetActive(false);

            // Browser first-gesture audio unlock prompt overlay
            var gateRect = TerminalUiBuilder.CreateRect("FallbackAudioUnlockGate", root);
            TerminalUiBuilder.SetStretch(gateRect);
            var gateImg = gateRect.gameObject.AddComponent<Image>();
            gateImg.color = new Color(0f, 0f, 0f, 0f);
            var gateBtn = gateRect.gameObject.AddComponent<Button>();
            gateBtn.transition = Selectable.Transition.None;
            gateBtn.onClick.AddListener(() => UnlockFallbackTitleAudioGate(playClick: true));

            var gatePrompt = TerminalUiBuilder.CreateText("FallbackClickToStartPrompt", gateRect, _theme, 30f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            gatePrompt.font = _theme.ResolveStampFont();
            gatePrompt.fontStyle = FontStyles.Bold;
            gatePrompt.text = Localization.Get("title.click_start");
            gatePrompt.raycastTarget = false;
            TerminalUiBuilder.SetAnchors(gatePrompt.rectTransform, 0.088f, 0.38f, 0.72f, 0.47f);
            _titleAudioUnlockGate = gateRect.gameObject;
            ApplyFallbackTitleAudioGateState(WebGLPlatformHelper.AudioUnlocked);

            // Touch/mobile advisory notice
            var mobileRect = TerminalUiBuilder.CreateRect("FallbackMobileNoticeFooter", root);
            TerminalUiBuilder.SetAnchors(mobileRect, 0.088f, 0.03f, 0.91f, 0.09f);
            var mobileText = TerminalUiBuilder.CreateText("FallbackMobileNoticeText", mobileRect, _theme, 20f, new Color(0.95f, 0.76f, 0.45f, 0.95f), TextAlignmentOptions.MidlineLeft, _textScaler);
            mobileText.font = _theme.ResolveStampFont();
            mobileText.text = Localization.Get("title.mobile_notice");
            mobileText.raycastTarget = false;
            TerminalUiBuilder.SetStretch(mobileText.rectTransform);
            _titleMobileNoticeGo = mobileRect.gameObject;
            _titleMobileNoticeGo.SetActive(WebGLPlatformHelper.ShouldShowTouchDeviceNotice());

            BuildTitleSettingsModal(root);

            // How To Play Modal (accessible from Title Settings)
            _howToPlayModal = CreateFullscreenBg(root, "HowToPlayModal", new Color(0.02f, 0.02f, 0.04f, 0.86f));
            var box = TerminalUiBuilder.CreateFlatPanel("HowBox", _howToPlayModal.GetComponent<RectTransform>(), _theme.WindowBg, new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.18f, 0.20f, 0.82f, 0.80f);
            var inner = TerminalUiBuilder.GetInner(box);

            var hTitle = TerminalUiBuilder.CreateText("HowTitle", inner, _theme, 34f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            hTitle.font = _theme.ResolveTitleFont();
            hTitle.fontStyle = FontStyles.Bold;
            hTitle.text = Localization.Get("title.how_to_play");
            TerminalUiBuilder.SetAnchors(hTitle.rectTransform, 0.04f, 0.80f, 0.96f, 0.95f);

            var hBody = TerminalUiBuilder.CreateText("HowBody", inner, _theme, 25f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            hBody.text = Localization.Get("title.how_to_play.body");
            TerminalUiBuilder.SetAnchors(hBody.rectTransform, 0.07f, 0.24f, 0.93f, 0.78f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseHowBtn", inner, Localization.Get("title.close"), _theme, _theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), _theme.TextPrimary, 24f, () =>
            {
                _audioManager?.PlayUiClick();
                _howToPlayModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.36f, 0.06f, 0.64f, 0.18f);

            _howToPlayModal.SetActive(false);
            _titlePanel.SetActive(false);
        }

        public static Button CreateSimpleTextMenuButton(string name, RectTransform parent, UITheme theme, AccessibilityTextScaler scaler, string label, float xMin, float yMin, float xMax, float yMax, float fontSize, Action onClick)
        {
            var rect = TerminalUiBuilder.CreateRect(name, parent);
            TerminalUiBuilder.SetAnchors(rect, xMin, yMin, xMax, yMax);

            var hitImg = rect.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;
            hitImg.raycastTarget = true;

            var txt = TerminalUiBuilder.CreateText("Label", rect, theme, fontSize, Color.white, TextAlignmentOptions.MidlineLeft, scaler);
            txt.font = theme.ResolveTitleFont();
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 2.0f;
            txt.text = label;
            txt.raycastTarget = true;
            TerminalUiBuilder.SetStretch(txt.rectTransform);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = txt;
            var colors = btn.colors;
            colors.normalColor = new Color(0.86f, 0.45f, 0.27f, 1f);
            colors.highlightedColor = new Color(1.00f, 0.74f, 0.50f, 1f);
            colors.selectedColor = new Color(1.00f, 0.74f, 0.50f, 1f);
            colors.pressedColor = new Color(0.70f, 0.33f, 0.18f, 1f);
            colors.disabledColor = new Color(0.45f, 0.25f, 0.18f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        private Button CreateSimpleTextMenuButton(string name, RectTransform parent, string label, float yMin, float yMax, Action onClick) =>
            CreateSimpleTextMenuButton(name, parent, _theme, _textScaler, label, 0.088f, yMin, 0.36f, yMax, 36f, onClick);

        private void BuildTitleSettingsModal(RectTransform root)
        {
            _titleSettingsModal = CreateFullscreenBg(root, "TitleSettingsModal", new Color(0.02f, 0.02f, 0.04f, 0.82f));
            var box = TerminalUiBuilder.CreateFlatPanel("SettingsBox", _titleSettingsModal.GetComponent<RectTransform>(), new Color(0.08f, 0.06f, 0.07f, 0.96f), new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.26f, 0.14f, 0.74f, 0.86f);
            var inner = TerminalUiBuilder.GetInner(box);

            var title = TerminalUiBuilder.CreateText("SettingsTitle", inner, _theme, 36f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            title.font = _theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("pause.settings_title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0f, 0.86f, 1f, 0.97f);

            BuildSharedSettingsControls(
                inner,
                0.80f,
                includeSensitivity: false,
                out _titleVolumeSlider,
                out _titleVolumeValText,
                out _titleVoxVolumeSlider,
                out _titleVoxVolumeValText,
                out _titleVoxMuteLabel,
                out _,
                out _,
                out _titleLargeTextLabel,
                out _titleReduceFlickerLabel,
                out _titlePsxEnabledLabel,
                out _titlePsxResLabel);

            var howBtn = TerminalUiBuilder.CreateButton("SettingsHowToPlayBtn", inner, Localization.Get("title.how_to_play"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                _titleSettingsModal.SetActive(false);
                if (_howToPlayModal != null)
                    _howToPlayModal.SetActive(true);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(howBtn.GetComponent<RectTransform>(), 0.10f, 0.06f, 0.48f, 0.18f);

            var closeBtn = TerminalUiBuilder.CreateButton("CloseSettingsBtn", inner, Localization.Get("title.close"), _theme, _theme.ButtonAccentBg, new Color(0.86f, 0.45f, 0.27f, 1f), _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                _titleSettingsModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.52f, 0.06f, 0.90f, 0.18f);

            _titleSettingsModal.SetActive(false);
        }

        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildIntroPanel(RectTransform parent)
        {
            _introPanel = CreateFullscreenBg(parent, "IntroPanel", Color.black);
            var root = _introPanel.GetComponent<RectTransform>();

            // Subtle procedural 128x128 film grain overlay (Point, tileable, ~6% alpha)
            _introGrainTex = BuildFilmGrainTexture(128);
            var grainRect = TerminalUiBuilder.CreateRect("FilmGrainOverlay", root);
            TerminalUiBuilder.SetStretch(grainRect);
            _introGrainImage = grainRect.gameObject.AddComponent<RawImage>();
            _introGrainImage.texture = _introGrainTex;
            _introGrainImage.uvRect = new Rect(0f, 0f, 6f, 3.5f);
            _introGrainImage.color = new Color(1f, 1f, 1f, 0.06f);
            _introGrainImage.raycastTarget = false;

            // Subtle vignette (edge darkening)
            _introVignetteTex = BuildVignetteTexture(64);
            var vigRect = TerminalUiBuilder.CreateRect("IntroVignette", root);
            TerminalUiBuilder.SetStretch(vigRect);
            _introVignetteImage = vigRect.gameObject.AddComponent<RawImage>();
            _introVignetteImage.texture = _introVignetteTex;
            _introVignetteImage.color = Color.white;
            _introVignetteImage.raycastTarget = false;

            // Animated card container (used for fade, shake, and 1.0 -> 1.06 push-in)
            _introCardContainer = TerminalUiBuilder.CreateRect("CardContainer", root);
            TerminalUiBuilder.SetStretch(_introCardContainer);
            _introBasePos = _introCardContainer.anchoredPosition;
            _introCardGroup = _introCardContainer.gameObject.AddComponent<CanvasGroup>();
            _introCardGroup.blocksRaycasts = false;

            // 1) BigWord Group
            var bigRect = TerminalUiBuilder.CreateRect("BigWordGroup", _introCardContainer);
            TerminalUiBuilder.SetStretch(bigRect);
            _introBigGroup = bigRect.gameObject;

            _introBigText = TerminalUiBuilder.CreateText("BigWordText", bigRect, _theme, 72f, Color.white, TextAlignmentOptions.Center, _textScaler);
            _introBigText.font = _theme.ResolveTitleFont();
            _introBigText.fontStyle = FontStyles.Bold;
            _introBigText.textWrappingMode = TextWrappingModes.Normal;
            _introBigText.richText = true;
            TerminalUiBuilder.SetAnchors(_introBigText.rectTransform, 0.08f, 0.43f, 0.92f, 0.63f);

            var underlineRect = TerminalUiBuilder.CreateRect("BigWordAmberRule", bigRect);
            TerminalUiBuilder.SetAnchors(underlineRect, 0.34f, 0.415f, 0.66f, 0.418f);
            var underlineImg = underlineRect.gameObject.AddComponent<Image>();
            underlineImg.color = _theme.TextAmber;
            underlineImg.raycastTarget = false;
            _introUnderlineGo = underlineRect.gameObject;

            // 2) Stamp Group (lower-left corner, vertical amber accent bar + SmallCaps monospace)
            var stampRect = TerminalUiBuilder.CreateRect("StampGroup", _introCardContainer);
            TerminalUiBuilder.SetAnchors(stampRect, 0.07f, 0.14f, 0.60f, 0.34f);
            _introStampGroup = stampRect.gameObject;

            var stampBar = TerminalUiBuilder.CreateRect("StampAccentBar", stampRect);
            TerminalUiBuilder.SetAnchors(stampBar, 0f, 0.06f, 0.004f, 0.94f);
            stampBar.sizeDelta = new Vector2(3f, 0f);
            var stampBarImg = stampBar.gameObject.AddComponent<Image>();
            stampBarImg.color = _theme.TextAmber;
            stampBarImg.raycastTarget = false;

            _introStampText = TerminalUiBuilder.CreateText("StampText", stampRect, _theme, 30f, _theme.TextPrimary, TextAlignmentOptions.BottomLeft, _textScaler);
            _introStampText.font = _theme.ResolveStampFont();
            _introStampText.fontStyle = FontStyles.SmallCaps;
            TerminalUiBuilder.SetAnchors(_introStampText.rectTransform, 0.025f, 0.05f, 1f, 0.95f);

            // 3) SmallLines & RadioLine Group
            var smallRect = TerminalUiBuilder.CreateRect("SmallLinesGroup", _introCardContainer);
            TerminalUiBuilder.SetAnchors(smallRect, 0.12f, 0.30f, 0.88f, 0.70f);
            _introSmallGroup = smallRect.gameObject;

            _introSmallText = TerminalUiBuilder.CreateText("SmallLinesText", smallRect, _theme, 32f, _theme.TextPrimary, TextAlignmentOptions.Center, _textScaler);
            _introSmallText.font = _theme.ResolveStampFont();
            _introSmallText.textWrappingMode = TextWrappingModes.Normal;
            TerminalUiBuilder.SetStretch(_introSmallText.rectTransform);

            // 4) TitleSlam Group
            var slamRect = TerminalUiBuilder.CreateRect("TitleSlamGroup", _introCardContainer);
            TerminalUiBuilder.SetStretch(slamRect);
            _introSlamGroup = slamRect.gameObject;

            _introSlamMainText = TerminalUiBuilder.CreateText("SlamMainText", slamRect, _theme, 84f, Color.white, TextAlignmentOptions.Center, _textScaler);
            _introSlamMainText.font = _theme.ResolveTitleFont();
            _introSlamMainText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_introSlamMainText.rectTransform, 0.06f, 0.50f, 0.94f, 0.68f);

            var slamRule = TerminalUiBuilder.CreateRect("SlamAmberRule", slamRect);
            TerminalUiBuilder.SetAnchors(slamRule, 0.28f, 0.486f, 0.72f, 0.490f);
            slamRule.gameObject.AddComponent<Image>().color = _theme.TextAmber;

            _introSlamSubText = TerminalUiBuilder.CreateText("SlamSubText", slamRect, _theme, 46f, _theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            _introSlamSubText.font = _theme.ResolveTitleFont();
            _introSlamSubText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_introSlamSubText.rectTransform, 0.10f, 0.34f, 0.90f, 0.47f);

            // Letterbox black bars (top 8%, bottom 8%) above cards so zoom push-in stays framed
            var topBar = CreateFullscreenBg(root, "LetterboxTop", Color.black).GetComponent<RectTransform>();
            TerminalUiBuilder.SetAnchors(topBar, 0f, 0.92f, 1f, 1f);
            topBar.GetComponent<Image>().raycastTarget = false;

            var bottomBar = CreateFullscreenBg(root, "LetterboxBottom", Color.black).GetComponent<RectTransform>();
            TerminalUiBuilder.SetAnchors(bottomBar, 0f, 0f, 1f, 0.08f);
            bottomBar.GetComponent<Image>().raycastTarget = false;

            // Bottom-right "HOLD SPACE TO SKIP" progress pill
            var skipRect = TerminalUiBuilder.CreateRect("HoldSpaceSkip", root);
            TerminalUiBuilder.SetAnchors(skipRect, 0.73f, 0.105f, 0.95f, 0.155f);
            _introSkipContainer = skipRect.gameObject;

            var trackPanel = TerminalUiBuilder.CreateFlatPanel("SkipTrack", skipRect, new Color(0.06f, 0.08f, 0.12f, 0.85f), _theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(trackPanel, 0f, 0.05f, 1f, 0.32f);
            var fillRect = TerminalUiBuilder.CreateRect("SkipFill", TerminalUiBuilder.GetInner(trackPanel));
            TerminalUiBuilder.SetStretch(fillRect);
            _introSkipFill = fillRect.gameObject.AddComponent<Image>();
            _introSkipFill.color = _theme.TextAmber;
            _introSkipFill.type = Image.Type.Filled;
            _introSkipFill.fillMethod = Image.FillMethod.Horizontal;
            _introSkipFill.fillAmount = 0f;
            _introSkipFill.raycastTarget = false;

            _introSkipLabel = TerminalUiBuilder.CreateText("SkipLabel", skipRect, _theme, 22f, _theme.TextMuted, TextAlignmentOptions.BottomRight, _textScaler);
            _introSkipLabel.font = _theme.ResolveStampFont();
            _introSkipLabel.text = Localization.Get("intro.hold_space_skip");
            TerminalUiBuilder.SetAnchors(_introSkipLabel.rectTransform, 0f, 0.34f, 1f, 1f);

            _introSkipContainer.SetActive(false);
            _introPanel.SetActive(false);
        }

        private static Texture2D BuildFilmGrainTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];
            var rng = new System.Random(2026);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)rng.Next(20, 240);
                pixels[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D BuildVignetteTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.35f, r)) * 0.72f;
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private void BuildBootLoginPanel(RectTransform parent)
        {
            _bootLoginPanel = CreateFullscreenBg(parent, "BootLoginPanel", new Color(0f, 0f, 0f, 0.45f));
            var root = _bootLoginPanel.GetComponent<RectTransform>();

            var box = TerminalUiBuilder.CreateFlatPanel("BootTerminalBox", root, new Color(0.04f, 0.06f, 0.10f, 0.96f), _theme.StatusOk, 2f);
            TerminalUiBuilder.SetAnchors(box, 0.16f, 0.18f, 0.84f, 0.82f);
            var inner = TerminalUiBuilder.GetInner(box);

            _bootConsoleText = TerminalUiBuilder.CreateText("BootLines", inner, _theme, 28f, _theme.StatusOk, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_bootConsoleText.rectTransform, 0.06f, 0.48f, 0.94f, 0.90f);

            var loginRect = TerminalUiBuilder.CreateRect("LoginGroup", inner);
            TerminalUiBuilder.SetAnchors(loginRect, 0.06f, 0.08f, 0.94f, 0.44f);
            _loginGroup = loginRect.gameObject;

            var promptTmp = TerminalUiBuilder.CreateText("LoginPrompt", loginRect, _theme, 28f, _theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            promptTmp.fontStyle = FontStyles.Bold;
            promptTmp.text = Localization.Get("login.prompt");
            TerminalUiBuilder.SetAnchors(promptTmp.rectTransform, 0f, 0.66f, 1f, 0.96f);

            _loginInput = CreateTmpInputField(loginRect);
            TerminalUiBuilder.SetAnchors(_loginInput.GetComponent<RectTransform>(), 0f, 0.30f, 0.66f, 0.60f);

            var confirmBtn = TerminalUiBuilder.CreateButton("ConfirmLoginBtn", loginRect, Localization.Get("login.confirm"), _theme, _theme.ButtonAccentBg, _theme.BorderFocused, _theme.TextPrimary, 24f, ConfirmCommanderLogin, _textScaler);
            TerminalUiBuilder.SetAnchors(confirmBtn.GetComponent<RectTransform>(), 0.69f, 0.30f, 1f, 0.60f);

            _loginGroup.SetActive(false);
            _bootLoginPanel.SetActive(false);
        }

        private TMP_InputField CreateTmpInputField(RectTransform parent)
        {
            var panel = TerminalUiBuilder.CreateFlatPanel("NameInputField", parent, _theme.DesktopBg, _theme.BorderFocused, 2f);
            var inner = TerminalUiBuilder.GetInner(panel);

            var textArea = TerminalUiBuilder.CreateRect("Text Area", inner);
            TerminalUiBuilder.SetStretch(textArea, 8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var phTmp = TerminalUiBuilder.CreateText("Placeholder", textArea, _theme, 28f, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            phTmp.text = Localization.Get("login.default");
            TerminalUiBuilder.SetStretch(phTmp.rectTransform);

            var txtTmp = TerminalUiBuilder.CreateText("Text", textArea, _theme, 28f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            TerminalUiBuilder.SetStretch(txtTmp.rectTransform);

            var input = panel.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = inner.GetComponent<Image>();
            input.textViewport = textArea;
            input.textComponent = txtTmp;
            input.placeholder = phTmp;
            input.characterLimit = 12;
            input.onValidateInput = ValidateCommanderChar;
            input.onSubmit.AddListener(_ => ConfirmCommanderLogin());
            return input;
        }

        private void BuildPausePanel(RectTransform parent)
        {
            _pausePanel = CreateFullscreenBg(parent, "PausePanel", new Color(0f, 0f, 0f, 0.78f));
            var pauseCanvas = _pausePanel.AddComponent<Canvas>();
            pauseCanvas.overrideSorting = true;
            pauseCanvas.sortingOrder = 90; // Sorted above everything (including EndSolBlackCard at 80) except DevDebugPanel (100)
            _pausePanel.AddComponent<GraphicRaycaster>();
            var root = _pausePanel.GetComponent<RectTransform>();

            var box = TerminalUiBuilder.CreateFlatPanel("PauseBox", root, _theme.WindowBg, _theme.BorderFocused, 2f);
            TerminalUiBuilder.SetAnchors(box, 0.24f, 0.04f, 0.76f, 0.96f);
            var inner = TerminalUiBuilder.GetInner(box);

            var title = TerminalUiBuilder.CreateText("PauseTitle", inner, _theme, 36f, _theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            title.font = _theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("pause.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0f, 0.915f, 1f, 0.98f);

            _pausePointerHintText = TerminalUiBuilder.CreateText("PointerLostHint", inner, _theme, 18f, UITheme.PaletteLightBlue, TextAlignmentOptions.Center, _textScaler);
            _pausePointerHintText.fontStyle = FontStyles.Bold;
            _pausePointerHintText.text = Localization.Get("pause.pointer_lost_hint");
            TerminalUiBuilder.SetAnchors(_pausePointerHintText.rectTransform, 0.05f, 0.875f, 0.95f, 0.915f);
            _pausePointerHintText.gameObject.SetActive(false);

            var resumeBtn = TerminalUiBuilder.CreateButton("ResumeBtn", inner, Localization.Get("pause.resume"), _theme, _theme.ButtonAccentBg, _theme.BorderFocused, _theme.TextPrimary, 24f, ResumeFromPause, _textScaler);
            TerminalUiBuilder.SetAnchors(resumeBtn.GetComponent<RectTransform>(), 0.10f, 0.805f, 0.90f, 0.870f);
            _pauseResumeLabel = resumeBtn.GetComponentInChildren<TextMeshProUGUI>();

            var restartBtn = TerminalUiBuilder.CreateButton("RestartBtn", inner, Localization.Get("pause.restart"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 24f, () =>
            {
                _audioManager?.PlayUiClick();
                int seed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed : 101;
                RestartMissionRun(seed, runIntro: false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(restartBtn.GetComponent<RectTransform>(), 0.10f, 0.730f, 0.90f, 0.792f);

            BuildSharedSettingsControls(
                inner,
                0.715f,
                includeSensitivity: true,
                out _pauseVolumeSlider,
                out _pauseVolumeValText,
                out _pauseVoxVolumeSlider,
                out _pauseVoxVolumeValText,
                out _pauseVoxMuteLabel,
                out _pauseMouseSensSlider,
                out _pauseMouseSensValText,
                out _pauseLargeTextLabel,
                out _pauseReduceFlickerLabel,
                out _pausePsxEnabledLabel,
                out _pausePsxResLabel);

            var fullscreenBtn = TerminalUiBuilder.CreateButton("FullscreenBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                bool nextFullscreen = !Screen.fullScreen;
                Screen.fullScreen = nextFullscreen;
                _lastFullscreenUiState = nextFullscreen;
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(fullscreenBtn.GetComponent<RectTransform>(), 0.10f, 0.152f, 0.90f, 0.220f);
            _pauseFullscreenLabel = fullscreenBtn.GetComponentInChildren<TextMeshProUGUI>();

            var quitBtn = TerminalUiBuilder.CreateButton("QuitTitleBtn", inner, Localization.Get("pause.quit_title"), _theme, _theme.ButtonDangerBg, _theme.StatusCrit, Color.white, 24f, () =>
            {
                _audioManager?.PlayUiClick();
                QuitToTitle();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(quitBtn.GetComponent<RectTransform>(), 0.10f, 0.060f, 0.90f, 0.130f);

            _pausePanel.SetActive(false);
        }

        private void BuildSharedSettingsControls(
            RectTransform inner,
            float topY,
            bool includeSensitivity,
            out Slider volumeSlider,
            out TextMeshProUGUI volumeValText,
            out Slider voxVolumeSlider,
            out TextMeshProUGUI voxVolumeValText,
            out TextMeshProUGUI voxMuteLabel,
            out Slider mouseSensSlider,
            out TextMeshProUGUI mouseSensValText,
            out TextMeshProUGUI largeTextLabel,
            out TextMeshProUGUI reduceFlickerLabel,
            out TextMeshProUGUI psxEnabledLabel,
            out TextMeshProUGUI psxResLabel)
        {
            float volLabelMin = topY - 0.042f;
            float volSliderMin = topY - 0.088f;
            float volSliderMax = topY - 0.046f;
            float annLabelMin = topY - 0.138f;
            float annLabelMax = topY - 0.096f;
            float annSliderMin = topY - 0.184f;
            float annSliderMax = topY - 0.142f;
            float sensLabelMin = topY - 0.233f;
            float sensLabelMax = topY - 0.191f;
            float sensSliderMin = topY - 0.279f;
            float sensSliderMax = topY - 0.237f;
            float row1Max = includeSensitivity ? sensSliderMin - 0.014f : topY - 0.205f;
            float row1Min = row1Max - (includeSensitivity ? 0.062f : 0.075f);
            float gfxMax = row1Min - 0.012f;
            float gfxMin = gfxMax - 0.036f;
            float row2Max = gfxMin - 0.010f;
            float row2Min = row2Max - (includeSensitivity ? 0.062f : 0.078f);

            mouseSensSlider = null;
            mouseSensValText = null;

            var volLabel = TerminalUiBuilder.CreateText("VolLabel", inner, _theme, 20f, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            volLabel.text = Localization.Get("pause.volume");
            TerminalUiBuilder.SetAnchors(volLabel.rectTransform, 0.10f, volLabelMin, 0.65f, topY);

            volumeValText = TerminalUiBuilder.CreateText("VolValue", inner, _theme, 20f, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
            TerminalUiBuilder.SetAnchors(volumeValText.rectTransform, 0.65f, volLabelMin, 0.90f, topY);

            volumeSlider = CreateFlatSlider("VolSlider", inner);
            TerminalUiBuilder.SetAnchors(volumeSlider.GetComponent<RectTransform>(), 0.10f, volSliderMin, 0.90f, volSliderMax);
            volumeSlider.onValueChanged.AddListener(v =>
            {
                if (_audioManager != null) _audioManager.MasterVolume = v;
                RefreshPauseLabels();
            });

            var annLabel = TerminalUiBuilder.CreateText("VoxVolLabel", inner, _theme, 20f, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            annLabel.text = Localization.Get("pause.vox_volume");
            TerminalUiBuilder.SetAnchors(annLabel.rectTransform, 0.10f, annLabelMin, 0.65f, annLabelMax);

            voxVolumeValText = TerminalUiBuilder.CreateText("VoxVolValue", inner, _theme, 20f, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
            TerminalUiBuilder.SetAnchors(voxVolumeValText.rectTransform, 0.65f, annLabelMin, 0.90f, annLabelMax);

            voxVolumeSlider = CreateFlatSlider("VoxVolSlider", inner);
            TerminalUiBuilder.SetAnchors(voxVolumeSlider.GetComponent<RectTransform>(), 0.10f, annSliderMin, 0.68f, annSliderMax);
            voxVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (_announcerSystem != null)
                {
                    _announcerSystem.AnnouncerVolume = v;
                }
                else
                {
                    PlayerPrefs.SetFloat(AnnouncerSystem.PrefVolume, Mathf.Clamp01(v));
                    PlayerPrefs.Save();
                }
                RefreshPauseLabels();
            });

            var voxMuteBtn = TerminalUiBuilder.CreateButton("VoxMuteBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 19f, () =>
            {
                _audioManager?.PlayUiClick();
                if (_announcerSystem != null)
                {
                    _announcerSystem.IsMuted = !_announcerSystem.IsMuted;
                }
                else
                {
                    bool nextMuted = PlayerPrefs.GetInt(AnnouncerSystem.PrefMuted, 0) == 0;
                    PlayerPrefs.SetInt(AnnouncerSystem.PrefMuted, nextMuted ? 1 : 0);
                    PlayerPrefs.Save();
                }
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(voxMuteBtn.GetComponent<RectTransform>(), 0.70f, annSliderMin - 0.004f, 0.90f, annSliderMax + 0.004f);
            voxMuteLabel = voxMuteBtn.GetComponentInChildren<TextMeshProUGUI>();

            if (includeSensitivity)
            {
                var sensLabel = TerminalUiBuilder.CreateText("MouseSensLabel", inner, _theme, 20f, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
                sensLabel.text = Localization.Get("pause.sensitivity");
                TerminalUiBuilder.SetAnchors(sensLabel.rectTransform, 0.10f, sensLabelMin, 0.65f, sensLabelMax);

                mouseSensValText = TerminalUiBuilder.CreateText("MouseSensValue", inner, _theme, 20f, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
                TerminalUiBuilder.SetAnchors(mouseSensValText.rectTransform, 0.65f, sensLabelMin, 0.90f, sensLabelMax);

                mouseSensSlider = CreateFlatSlider("MouseSensSlider", inner);
                mouseSensSlider.minValue = PlayerGate.MinMouseSensitivity;
                mouseSensSlider.maxValue = PlayerGate.MaxMouseSensitivity;
                mouseSensSlider.wholeNumbers = false;
                TerminalUiBuilder.SetAnchors(mouseSensSlider.GetComponent<RectTransform>(), 0.10f, sensSliderMin, 0.90f, sensSliderMax);
                mouseSensSlider.onValueChanged.AddListener(v =>
                {
                    if (_playerGate != null)
                    {
                        _playerGate.SetMouseSensitivity(v);
                    }
                    else
                    {
                        float clamped = Mathf.Clamp(v, PlayerGate.MinMouseSensitivity, PlayerGate.MaxMouseSensitivity);
                        PlayerPrefs.SetFloat(PlayerGate.PrefMouseSensitivity, clamped);
                        PlayerPrefs.Save();
                    }
                    RefreshPauseLabels();
                });
            }

            var largeBtn = TerminalUiBuilder.CreateButton("LargerTextBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                if (_accessibility != null) _accessibility.SetLargerText(!_accessibility.LargerText);
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(largeBtn.GetComponent<RectTransform>(), 0.10f, row1Min, 0.48f, row1Max);
            largeTextLabel = largeBtn.GetComponentInChildren<TextMeshProUGUI>();

            var flickerBtn = TerminalUiBuilder.CreateButton("ReduceFlickerBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                if (_accessibility != null)
                {
                    bool next = !_accessibility.ReduceFlicker;
                    _accessibility.SetReduceFlicker(next);
                    _accessibility.SetReduceMotion(next);
                    _psxSettings?.NotifyChanged();
                }
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(flickerBtn.GetComponent<RectTransform>(), 0.52f, row1Min, 0.90f, row1Max);
            reduceFlickerLabel = flickerBtn.GetComponentInChildren<TextMeshProUGUI>();

            var gfxHeader = TerminalUiBuilder.CreateText("GraphicsLabel", inner, _theme, 22f, _theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            gfxHeader.font = _theme.ResolveStampFont();
            gfxHeader.text = Localization.Get("pause.graphics");
            TerminalUiBuilder.SetAnchors(gfxHeader.rectTransform, 0.10f, gfxMin, 0.90f, gfxMax);

            var psxBtn = TerminalUiBuilder.CreateButton("PsxLookBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                if (_psxSettings != null) _psxSettings.SetEnabled(!_psxSettings.enabled, savePrefs: true);
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(psxBtn.GetComponent<RectTransform>(), 0.10f, row2Min, 0.48f, row2Max);
            psxEnabledLabel = psxBtn.GetComponentInChildren<TextMeshProUGUI>();

            var resBtn = TerminalUiBuilder.CreateButton("PsxResBtn", inner, "", _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                if (_psxSettings != null)
                {
                    int next = ((int)_psxSettings.preset + 1) % 4;
                    _psxSettings.ApplyPreset((PSXResolutionPreset)next, savePrefs: true);
                }
                RefreshPauseLabels();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(resBtn.GetComponent<RectTransform>(), 0.52f, row2Min, 0.90f, row2Max);
            psxResLabel = resBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private Slider CreateFlatSlider(string name, RectTransform parent)
        {
            var panel = TerminalUiBuilder.CreateFlatPanel(name, parent, _theme.BarTrackBg, _theme.BorderUnfocused, 2f);
            var inner = TerminalUiBuilder.GetInner(panel);

            var fillArea = TerminalUiBuilder.CreateRect("Fill Area", inner);
            TerminalUiBuilder.SetStretch(fillArea, 2f);
            var fillRect = TerminalUiBuilder.CreateRect("Fill", fillArea);
            TerminalUiBuilder.SetStretch(fillRect);
            fillRect.gameObject.AddComponent<Image>().color = _theme.StatusOk;

            var handleArea = TerminalUiBuilder.CreateRect("Handle Slide Area", inner);
            TerminalUiBuilder.SetStretch(handleArea, 4f);
            var handleRect = TerminalUiBuilder.CreateRect("Handle", handleArea);
            handleRect.sizeDelta = new Vector2(18f, 0f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.gameObject.AddComponent<Image>().color = _theme.TextAmber;

            var slider = panel.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleRect.GetComponent<Image>();
            return slider;
        }

        private void RefreshPauseLabels()
        {
            float vol = _audioManager != null ? _audioManager.MasterVolume : 0.75f;
            float voxVol = _announcerSystem != null ? _announcerSystem.AnnouncerVolume : PlayerPrefs.GetFloat(AnnouncerSystem.PrefVolume, 0.85f);
            bool voxMuted = _announcerSystem != null ? _announcerSystem.IsMuted : (PlayerPrefs.GetInt(AnnouncerSystem.PrefMuted, 0) == 1);
            bool largeOn = _accessibility != null && _accessibility.LargerText;
            bool flickerOn = _accessibility != null && _accessibility.ReduceFlicker;
            bool psxOn = _psxSettings != null && _psxSettings.enabled;
            string resLbl = _psxSettings != null ? _psxSettings.GetPresetLabel() : "MED (480x270)";
            float mouseSens = _playerGate != null ? _playerGate.GetMouseSensitivity() : PlayerGate.LoadSavedMouseSensitivity();

            ApplySettingsValues(vol, voxVol, voxMuted, largeOn, flickerOn, psxOn, resLbl, _pauseVolumeSlider, _pauseVolumeValText, _pauseVoxVolumeSlider, _pauseVoxVolumeValText, _pauseVoxMuteLabel, _pauseLargeTextLabel, _pauseReduceFlickerLabel, _pausePsxEnabledLabel, _pausePsxResLabel);
            ApplySettingsValues(vol, voxVol, voxMuted, largeOn, flickerOn, psxOn, resLbl, _titleVolumeSlider, _titleVolumeValText, _titleVoxVolumeSlider, _titleVoxVolumeValText, _titleVoxMuteLabel, _titleLargeTextLabel, _titleReduceFlickerLabel, _titlePsxEnabledLabel, _titlePsxResLabel);

            if (_pauseMouseSensSlider != null) _pauseMouseSensSlider.SetValueWithoutNotify(mouseSens);
            if (_pauseMouseSensValText != null) _pauseMouseSensValText.text = $"{mouseSens:0.00}x";

            if (_pausePointerHintText != null)
                _pausePointerHintText.gameObject.SetActive(_pausedByPointerLoss);
            if (_pauseResumeLabel != null)
                _pauseResumeLabel.text = Localization.Get(_pausedByPointerLoss ? "pause.click_to_resume" : "pause.resume");
            _lastFullscreenUiState = Screen.fullScreen;
            string onOff = Localization.GetRaw(_lastFullscreenUiState ? "pause.on" : "pause.off");
            if (_pauseFullscreenLabel != null)
                _pauseFullscreenLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.fullscreen")}: {onOff}");
        }

        private static void ApplySettingsValues(
            float vol,
            float voxVol,
            bool voxMuted,
            bool largeOn,
            bool flickerOn,
            bool psxOn,
            string resLbl,
            Slider volumeSlider,
            TextMeshProUGUI volumeValText,
            Slider voxVolumeSlider,
            TextMeshProUGUI voxVolumeValText,
            TextMeshProUGUI voxMuteLabel,
            TextMeshProUGUI largeTextLabel,
            TextMeshProUGUI reduceFlickerLabel,
            TextMeshProUGUI psxEnabledLabel,
            TextMeshProUGUI psxResLabel)
        {
            string onWord = Localization.GetRaw("pause.on");
            string offWord = Localization.GetRaw("pause.off");
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(vol);
            if (volumeValText != null) volumeValText.text = $"{Mathf.RoundToInt(vol * 100f)}%";
            if (voxVolumeSlider != null) voxVolumeSlider.SetValueWithoutNotify(voxVol);
            if (voxVolumeValText != null) voxVolumeValText.text = voxMuted ? Localization.Get("pause.vox_muted") : $"{Mathf.RoundToInt(voxVol * 100f)}%";
            if (voxMuteLabel != null) voxMuteLabel.text = Localization.Get(voxMuted ? "pause.vox_muted" : "pause.vox_active");
            if (largeTextLabel != null)
                largeTextLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.larger_text")}: {(largeOn ? onWord : offWord)}");
            if (reduceFlickerLabel != null)
                reduceFlickerLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.reduce_flicker")}: {(flickerOn ? onWord : offWord)}");
            if (psxEnabledLabel != null)
                psxEnabledLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.psx_look")}: {(psxOn ? onWord : offWord)}");
            if (psxResLabel != null)
                psxResLabel.text = Localization.ShapeDisplay($"{Localization.GetRaw("pause.psx_res")}: {resLbl}");
        }

        private void BuildEndingPanel(RectTransform parent)
        {
            _endingPanel = TerminalUiBuilder.CreateRect("EndingPanel", parent).gameObject;
            TerminalUiBuilder.SetStretch(_endingPanel.GetComponent<RectTransform>());

            var termBox = TerminalUiBuilder.CreateFlatPanel("EndingTerminalBox", _endingPanel.GetComponent<RectTransform>(), new Color(0.03f, 0.05f, 0.09f, 0.88f), _theme.BorderFocused, 2f);
            TerminalUiBuilder.SetAnchors(termBox, 0.14f, 0.25f, 0.86f, 0.75f);
            _endingTerminalText = TerminalUiBuilder.CreateText("EndingTermText", TerminalUiBuilder.GetInner(termBox), _theme, 34f, _theme.StatusOk, TextAlignmentOptions.Center, _textScaler);
            _endingTerminalText.font = _theme.ResolveFont();
            TerminalUiBuilder.SetStretch(_endingTerminalText.rectTransform, 28f);

            _endingBlackBg = CreateFullscreenBg(_endingPanel.GetComponent<RectTransform>(), "EndingBlackScreen", Color.black);
            var blackRoot = _endingBlackBg.GetComponent<RectTransform>();
            _endingBlackText = TerminalUiBuilder.CreateText("EndingBlackText", blackRoot, _theme, 64f, Color.white, TextAlignmentOptions.Center, _textScaler);
            _endingBlackText.font = _theme.ResolveTitleFont();
            _endingBlackText.fontStyle = FontStyles.Bold;
            _endingBlackText.textWrappingMode = TextWrappingModes.Normal;
            TerminalUiBuilder.SetAnchors(_endingBlackText.rectTransform, 0.08f, 0.30f, 0.92f, 0.72f);

            var ruleRect = TerminalUiBuilder.CreateRect("EndingAmberRule", blackRoot);
            TerminalUiBuilder.SetAnchors(ruleRect, 0.34f, 0.415f, 0.66f, 0.418f);
            var ruleImg = ruleRect.gameObject.AddComponent<Image>();
            ruleImg.color = _theme.TextAmber;
            ruleImg.raycastTarget = false;
            _endingUnderlineGo = ruleRect.gameObject;

            _endingPanel.SetActive(false);
        }

        private void BuildDebriefPanel(RectTransform parent)
        {
            _debriefPanel = CreateFullscreenBg(parent, "DebriefPanel", _theme.DesktopBg);
            var root = _debriefPanel.GetComponent<RectTransform>();

            _debriefHeaderText = TerminalUiBuilder.CreateText("DebriefHeader", root, _theme, 36f, _theme.StatusOk, TextAlignmentOptions.MidlineLeft, _textScaler);
            _debriefHeaderText.font = _theme.ResolveTitleFont();
            _debriefHeaderText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_debriefHeaderText.rectTransform, 0.03f, 0.90f, 0.97f, 0.97f);

            var sumPanel = TerminalUiBuilder.CreateFlatPanel("SummaryPanel", root, _theme.PanelBg, _theme.BorderFocused, 2f);
            TerminalUiBuilder.SetAnchors(sumPanel, 0.03f, 0.79f, 0.97f, 0.89f);
            _debriefSummaryText = TerminalUiBuilder.CreateText("SummaryText", TerminalUiBuilder.GetInner(sumPanel), _theme, 23f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            _debriefSummaryText.font = _theme.ResolveStampFont();
            TerminalUiBuilder.SetStretch(_debriefSummaryText.rectTransform, 12f);

            _debriefLeftText = TerminalUiBuilder.CreateScrollableText("DebriefLeftScroll", root, _theme, 22f, _theme.TextPrimary, _theme.PanelBg, _theme.BorderUnfocused, out var leftPanel, _textScaler);
            _debriefLeftText.font = _theme.ResolveFont();
            TerminalUiBuilder.SetAnchors(leftPanel, 0.03f, 0.12f, 0.50f, 0.77f);

            var rightPanel = TerminalUiBuilder.CreateFlatPanel("DebriefRightScroll", root, _theme.PanelBg, _theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(rightPanel, 0.515f, 0.12f, 0.97f, 0.77f);
            var rightInner = TerminalUiBuilder.GetInner(rightPanel);
            var rightViewport = TerminalUiBuilder.CreateRect("Viewport", rightInner);
            TerminalUiBuilder.SetStretch(rightViewport, 6f);
            rightViewport.gameObject.AddComponent<RectMask2D>();
            _debriefRightContent = TerminalUiBuilder.CreateRect("Content", rightViewport);
            _debriefRightContent.anchorMin = new Vector2(0f, 1f);
            _debriefRightContent.anchorMax = new Vector2(1f, 1f);
            _debriefRightContent.pivot = new Vector2(0.5f, 1f);
            _debriefRightContent.offsetMin = Vector2.zero;
            _debriefRightContent.offsetMax = Vector2.zero;
            var rightFitter = _debriefRightContent.gameObject.AddComponent<ContentSizeFitter>();
            rightFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rightFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var rightLayout = _debriefRightContent.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.padding = new RectOffset(8, 8, 8, 8);
            rightLayout.spacing = 10f;
            rightLayout.childControlWidth = true;
            rightLayout.childControlHeight = true;
            rightLayout.childForceExpandWidth = true;
            rightLayout.childForceExpandHeight = false;
            var rightScroll = rightInner.gameObject.AddComponent<ScrollRect>();
            rightScroll.viewport = rightViewport;
            rightScroll.content = _debriefRightContent;
            rightScroll.horizontal = false;
            rightScroll.vertical = true;
            rightScroll.movementType = ScrollRect.MovementType.Clamped;
            rightScroll.scrollSensitivity = 36f;

            var playAgainBtn = TerminalUiBuilder.CreateButton("PlayAgainBtn", root, Localization.Get("debrief.play_again"), _theme, _theme.ButtonAccentBg, _theme.BorderFocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                int nextSeed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed + 1 : 102;
                RestartMissionRun(nextSeed, runIntro: true);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(playAgainBtn.GetComponent<RectTransform>(), 0.03f, 0.03f, 0.27f, 0.10f);

            var sameSeedBtn = TerminalUiBuilder.CreateButton("ReplaySeedBtn", root, Localization.Get("debrief.replay_seed"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                int seed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed : 101;
                RestartMissionRun(seed, runIntro: false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(sameSeedBtn.GetComponent<RectTransform>(), 0.29f, 0.03f, 0.52f, 0.10f);

            var credBtn = TerminalUiBuilder.CreateButton("DebriefCreditsBtn", root, Localization.Get("debrief.credits"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                EnterCreditsState(FlowState.Debrief);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(credBtn.GetComponent<RectTransform>(), 0.54f, 0.03f, 0.74f, 0.10f);

            var backBtn = TerminalUiBuilder.CreateButton("BackTitleBtn", root, Localization.Get("debrief.back_title"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 22f, () =>
            {
                _audioManager?.PlayUiClick();
                QuitToTitle();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(backBtn.GetComponent<RectTransform>(), 0.76f, 0.03f, 0.97f, 0.10f);

            _debriefPanel.SetActive(false);
        }

        private void BuildCreditsPanel(RectTransform parent)
        {
            _creditsPanel = CreateFullscreenBg(parent, "CreditsPanel", _theme.DesktopBg);
            var root = _creditsPanel.GetComponent<RectTransform>();

            var title = TerminalUiBuilder.CreateText("CreditsTitle", root, _theme, 36f, _theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            title.font = _theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("credits.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0.05f, 0.89f, 0.95f, 0.97f);

            _creditsBodyText = TerminalUiBuilder.CreateScrollableText("CreditsScroll", root, _theme, 22f, _theme.TextPrimary, _theme.PanelBg, _theme.BorderFocused, out var scrollPanel, _textScaler);
            _creditsBodyText.font = _theme.ResolveFont();
            TerminalUiBuilder.SetAnchors(scrollPanel, 0.08f, 0.13f, 0.92f, 0.88f);

            var backBtn = TerminalUiBuilder.CreateButton("CreditsBackBtn", root, Localization.Get("title.close"), _theme, _theme.ButtonAccentBg, _theme.BorderFocused, _theme.TextPrimary, 24f, ReturnFromCredits, _textScaler);
            TerminalUiBuilder.SetAnchors(backBtn.GetComponent<RectTransform>(), 0.38f, 0.03f, 0.62f, 0.10f);

            _creditsPanel.SetActive(false);
        }

        private void BuildBlackFadeOverlay(RectTransform parent)
        {
            _fadeBlackGo = CreateFullscreenBg(parent, "BlackFadeOverlay", Color.black);
            _fadeBlackGroup = _fadeBlackGo.AddComponent<CanvasGroup>();
            _fadeBlackGroup.blocksRaycasts = false;
            _fadeBlackGo.SetActive(false);
        }

        // --- PART 4: END-SOL BLACK CARD SEQUENCE ---

        private void HandleEndSolBlackCardRequested(SessionSnapshot postSolSnap)
        {
            if (postSolSnap == null || postSolSnap.Status != MissionStatus.InProgress) return;
            int completedSol = postSolSnap.LastSolSummary != null
                ? postSolSnap.LastSolSummary.CompletedSol
                : Mathf.Max(1, postSolSnap.CurrentSol - 1);
            StartEndSolBlackCard(completedSol, postSolSnap.TotalSols, postSolSnap, showSummaryAfter: true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugPlayEndSolBlackCard(int chosenSol)
        {
            int clampedSol = Mathf.Clamp(chosenSol, 1, 7);
            int total = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.TotalSols : 7;
            StartEndSolBlackCard(clampedSol, total, postSolSnap: null, showSummaryAfter: false);
        }
#endif

        private void StartEndSolBlackCard(int completedSol, int totalSols, SessionSnapshot postSolSnap, bool showSummaryAfter)
        {
            if (!_built) return;
            StopActiveRoutine();
            Time.timeScale = 1f;

            _state = FlowState.EndSolBlackCard;
            _stateEnteredFrame = Time.frameCount;
            _endSolSkipRequested = false;
            _endSolSkipHoldSeconds = 0f;
            _endSolSkipAllowed = _hasPlayedEndSolBlackCardThisSession;

            SetEndSolSkipProgress(0f);
            if (_endSolSkipContainer != null) _endSolSkipContainer.SetActive(_endSolSkipAllowed);

            _playerGate?.EnableControl(false);
            _terminalController?.CloseAllModalsForBlackCard();

            _activeRoutine = StartCoroutine(RunEndSolBlackCardRoutine(completedSol, totalSols, postSolSnap, showSummaryAfter));
        }

        private IEnumerator RunEndSolBlackCardRoutine(int completedSol, int totalSols, SessionSnapshot postSolSnap, bool showSummaryAfter)
        {
            if (_endSolPanel == null)
            {
                _state = FlowState.Gameplay;
                _activeRoutine = null;
                yield break;
            }

            _endSolPanel.SetActive(true);
            _endSolBackdropGroup.alpha = 0f;
            _endSolCardGroup.alpha = 1f;
            if (_endSolCardContainer != null) _endSolCardContainer.localScale = Vector3.one;

            SetBlackCardTexts(completedSol, totalSols);
            SetTmpAlpha(_endSolTopLineText, 0f);
            SetTmpAlpha(_endSolBigLineText, 0f);
            SetTmpAlpha(_endSolSubLineText, 0f);
            if (_endSolRuleGo != null) _endSolRuleGo.SetActive(false);

            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
            if (_endSolGrainImage != null) _endSolGrainImage.gameObject.SetActive(!reduceFlicker);
            if (_endSolVignetteImage != null) _endSolVignetteImage.gameObject.SetActive(!reduceFlicker);
            if (_endSolTextScaler != null) _endSolTextScaler.SetLargerText(_accessibility != null && _accessibility.LargerText);

            // 1. Fade to black over 0.8s while closing terminal & fading ambient audio down
            _audioManager?.SetAmbientFadeTarget(0f);
            _roomController?.BeginLeaveTerminal();

            float fadeElapsed = 0f;
            const float fadeDur = 0.8f;
            while (fadeElapsed < fadeDur)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(fadeElapsed / fadeDur);
                _endSolBackdropGroup.alpha = u;
                StepBlackCardGrain();
                yield return null;
            }
            _endSolBackdropGroup.alpha = 1f;

            // 2. While screen is black: teleport to PlayerSpawn facing monitor, auto-remove suit, reset doors/airlock & clock, start drone
            int nextSol = Mathf.Clamp(completedSol + 1, 1, Mathf.Max(1, totalSols));
            if (showSummaryAfter && postSolSnap != null)
                nextSol = postSolSnap.CurrentSol;

            SuitController.Instance?.ForceRemoveSuitImmediate();
            _roomController?.PrepareRoomForNewSolBlackCard(unlockCursor: true);
            if (showSummaryAfter && _terminalController != null)
                _terminalController.ResetClockForSolImmediate(nextSol);

            _audioManager?.StartEndSolDrone();

            yield return WaitBlackCardSeconds(0.15f);

            // 3. Timed cinematic text reveal with slow 1.0 -> 1.06 push-in
            float pushClock = 0f;
            const float totalCardSpan = 4.6f;

            // Line 1 (small): SOL {n} COMPLETE
            yield return RevealBlackCardLineRoutine(
                _endSolTopLineText,
                revealDur: 0.40f,
                startSpacing: 38f,
                endSpacing: 16f,
                jitter: true,
                pushClockRef: () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });

            // +1.0s before big headline
            yield return WaitBlackCardWithPushSeconds(1.0f, () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });

            // Line 2 (big): RESCUE IN {k} SOLS / FINAL SOL. RESCUE ARRIVES TODAY. + bass hit
            if (!_endSolSkipRequested)
            {
                _audioManager?.PlayEndSolBassHit();
                if (_endSolRuleGo != null) _endSolRuleGo.SetActive(true);
                yield return RevealBlackCardLineRoutine(
                    _endSolBigLineText,
                    revealDur: 0.45f,
                    startSpacing: 40f,
                    endSpacing: 18f,
                    jitter: true,
                    pushClockRef: () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });
            }

            // +1.2s before smaller Mars sol note
            yield return WaitBlackCardWithPushSeconds(1.2f, () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });

            // Line 3 (smaller): A sol is one day on Mars.
            if (!_endSolSkipRequested)
            {
                yield return RevealBlackCardLineRoutine(
                    _endSolSubLineText,
                    revealDur: 0.40f,
                    startSpacing: 28f,
                    endSpacing: 12f,
                    jitter: false,
                    pushClockRef: () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });
            }

            // Hold ~2.0s cleanly
            yield return WaitBlackCardWithPushSeconds(2.0f, () => { pushClock += Time.unscaledDeltaTime; UpdateBlackCardPushScale(pushClock, totalCardSpan); });

            // Mark session skip availability after first play
            _hasPlayedEndSolBlackCardThisSession = true;

            // 4. Fade back into room over 0.8s, restore ambient audio, trigger next sol_start announcer line if present
            _audioManager?.StopEndSolDrone(immediate: false);
            _audioManager?.SetAmbientFadeTarget(1f);

            if (showSummaryAfter && _announcerSystem != null)
            {
                if (!_announcerSystem.PlayLineIfPresent($"sol_{nextSol}_start", bypassSuppression: true))
                    _announcerSystem.PlayLineIfPresent("sol_start", bypassSuppression: true);
            }

            float outElapsed = 0f;
            while (outElapsed < fadeDur)
            {
                outElapsed += Time.unscaledDeltaTime;
                float u = 1f - Mathf.Clamp01(outElapsed / fadeDur);
                _endSolBackdropGroup.alpha = u;
                _endSolCardGroup.alpha = u;
                StepBlackCardGrain();
                yield return null;
            }

            _endSolBackdropGroup.alpha = 0f;
            _endSolPanel.SetActive(false);
            _state = FlowState.Gameplay;
            _stateEnteredFrame = Time.frameCount;
            _activeRoutine = null;

            // 5. Show existing Sol Summary card right after fade-in (or restore room control for debug preview)
            if (showSummaryAfter && postSolSnap != null)
            {
                _roomController?.EnterTerminalImmediateForFlow(null);
                _terminalController?.ShowSolSummaryImmediateAfterBlackCard(postSolSnap);
            }
            else
            {
                _roomController?.EnterRoomViewImmediateForFlow();
            }
        }

        private void SetBlackCardTexts(int completedSol, int totalSols)
        {
            if (_endSolTopLineText != null)
            {
                _endSolTopLineText.text = Localization.Format("sol_complete", completedSol).ToUpperInvariant();
                _endSolTopLineText.characterSpacing = 38f;
            }
            if (_endSolBigLineText != null)
            {
                _endSolBigLineText.text = Localization.FormatRescueRemaining(completedSol, totalSols).ToUpperInvariant();
                _endSolBigLineText.characterSpacing = 40f;
            }
            if (_endSolSubLineText != null)
            {
                _endSolSubLineText.text = Localization.Get("sol_definition");
                _endSolSubLineText.characterSpacing = 28f;
            }
        }

        private void UpdateBlackCardPushScale(float pushClock, float totalSpan)
        {
            if (_endSolCardContainer == null) return;
            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;
            if (reduceFlicker)
            {
                _endSolCardContainer.localScale = Vector3.one;
                return;
            }

            float p = Mathf.Clamp01(pushClock / Mathf.Max(0.1f, totalSpan));
            float s = Mathf.Lerp(1.00f, 1.06f, p);
            _endSolCardContainer.localScale = new Vector3(s, s, 1f);
        }

        private IEnumerator RevealBlackCardLineRoutine(
            TextMeshProUGUI tmp,
            float revealDur,
            float startSpacing,
            float endSpacing,
            bool jitter,
            Action pushClockRef)
        {
            if (tmp == null) yield break;
            float elapsed = 0f;
            bool reduceFlicker = _accessibility != null && _accessibility.ReduceFlicker;

            while (elapsed < revealDur && !_endSolSkipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                pushClockRef?.Invoke();
                StepBlackCardGrain();

                float u = Mathf.Clamp01(elapsed / revealDur);
                float smooth = Mathf.SmoothStep(0f, 1f, u);
                float flickerMul = (!reduceFlicker && u < 0.72f)
                    ? (Mathf.Sin(elapsed * 54f) > -0.25f ? 1f : 0.35f)
                    : 1f;

                Color c = tmp.color;
                c.a = smooth * flickerMul;
                tmp.color = c;
                tmp.characterSpacing = Mathf.Lerp(startSpacing, endSpacing, smooth);

                if (jitter && !reduceFlicker && u < 0.75f)
                    ApplyPerCharacterSettlementJitter(tmp, (1f - u) * 0.7f);

                yield return null;
            }

            Color finalCol = tmp.color;
            finalCol.a = 1f;
            tmp.color = finalCol;
            tmp.characterSpacing = endSpacing;
            if (jitter) tmp.ForceMeshUpdate();
        }

        private IEnumerator WaitBlackCardSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !_endSolSkipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                StepBlackCardGrain();
                yield return null;
            }
        }

        private IEnumerator WaitBlackCardWithPushSeconds(float seconds, Action tickPush)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !_endSolSkipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                tickPush?.Invoke();
                StepBlackCardGrain();
                yield return null;
            }
        }

        private void StepBlackCardGrain()
        {
            if (_endSolGrainImage != null && _endSolGrainImage.gameObject.activeSelf)
            {
                _endSolGrainImage.uvRect = new Rect(
                    UnityEngine.Random.value,
                    UnityEngine.Random.value,
                    6f,
                    3.5f);
            }
        }

        private static void SetTmpAlpha(TextMeshProUGUI tmp, float alpha)
        {
            if (tmp == null) return;
            Color c = tmp.color;
            c.a = alpha;
            tmp.color = c;
        }

        private void BuildEndSolBlackCardCanvas()
        {
            var canvasGo = new GameObject("EndSolBlackCardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _endSolCanvas = canvasGo.GetComponent<Canvas>();
            _endSolCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _endSolCanvas.overrideSorting = true;
            _endSolCanvas.sortingOrder = 80; // Above WorldView (-100), HUD (5), Visor (7), Terminal (10), Flow (30), Subtitles (45); below Pause (90)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _endSolTextScaler = canvasGo.AddComponent<AccessibilityTextScaler>();

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            _endSolPanel = CreateFullscreenBg(canvasRect, "EndSolBlackCardPanel", Color.black);
            var root = _endSolPanel.GetComponent<RectTransform>();
            _endSolBackdropGroup = _endSolPanel.AddComponent<CanvasGroup>();
            _endSolBackdropGroup.blocksRaycasts = true;

            var grainRect = TerminalUiBuilder.CreateRect("BlackCardGrain", root);
            TerminalUiBuilder.SetStretch(grainRect);
            _endSolGrainImage = grainRect.gameObject.AddComponent<RawImage>();
            _endSolGrainImage.texture = _introGrainTex != null ? _introGrainTex : (_introGrainTex = BuildFilmGrainTexture(128));
            _endSolGrainImage.uvRect = new Rect(0f, 0f, 6f, 3.5f);
            _endSolGrainImage.color = new Color(1f, 1f, 1f, 0.028f);
            _endSolGrainImage.raycastTarget = false;

            var vigRect = TerminalUiBuilder.CreateRect("BlackCardVignette", root);
            TerminalUiBuilder.SetStretch(vigRect);
            _endSolVignetteImage = vigRect.gameObject.AddComponent<RawImage>();
            _endSolVignetteImage.texture = _introVignetteTex != null ? _introVignetteTex : (_introVignetteTex = BuildVignetteTexture(64));
            _endSolVignetteImage.color = Color.white;
            _endSolVignetteImage.raycastTarget = false;

            _endSolCardContainer = TerminalUiBuilder.CreateRect("BlackCardContainer", root);
            TerminalUiBuilder.SetStretch(_endSolCardContainer);
            _endSolCardGroup = _endSolCardContainer.gameObject.AddComponent<CanvasGroup>();
            _endSolCardGroup.blocksRaycasts = false;

            // Line 1: Small line ("SOL {n} COMPLETE")
            _endSolTopLineText = TerminalUiBuilder.CreateText("SolCompleteText", _endSolCardContainer, _theme, 32f, _theme.TextAmber, TextAlignmentOptions.Center, _endSolTextScaler);
            _endSolTopLineText.font = _theme.ResolveTitleFont();
            _endSolTopLineText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_endSolTopLineText.rectTransform, 0.08f, 0.59f, 0.92f, 0.67f);

            // Line 2: Big line ("RESCUE IN {k} SOLS" / "FINAL SOL. RESCUE ARRIVES TODAY.")
            _endSolBigLineText = TerminalUiBuilder.CreateText("RescueBigText", _endSolCardContainer, _theme, 66f, Color.white, TextAlignmentOptions.Center, _endSolTextScaler);
            _endSolBigLineText.font = _theme.ResolveTitleFont();
            _endSolBigLineText.fontStyle = FontStyles.Bold;
            _endSolBigLineText.textWrappingMode = TextWrappingModes.Normal;
            TerminalUiBuilder.SetAnchors(_endSolBigLineText.rectTransform, 0.08f, 0.40f, 0.92f, 0.58f);

            var ruleRect = TerminalUiBuilder.CreateRect("BlackCardAmberRule", _endSolCardContainer);
            TerminalUiBuilder.SetAnchors(ruleRect, 0.34f, 0.388f, 0.66f, 0.391f);
            var ruleImg = ruleRect.gameObject.AddComponent<Image>();
            ruleImg.color = _theme.TextAmber;
            ruleImg.raycastTarget = false;
            _endSolRuleGo = ruleRect.gameObject;

            // Line 3: Smaller line ("A sol is one day on Mars.")
            _endSolSubLineText = TerminalUiBuilder.CreateText("SolDefText", _endSolCardContainer, _theme, 28f, new Color(0.72f, 0.80f, 0.88f, 1f), TextAlignmentOptions.Center, _endSolTextScaler);
            _endSolSubLineText.font = _theme.ResolveStampFont();
            TerminalUiBuilder.SetAnchors(_endSolSubLineText.rectTransform, 0.10f, 0.27f, 0.90f, 0.36f);

            // Letterbox black bars
            var topBar = CreateFullscreenBg(root, "LetterboxTop", Color.black).GetComponent<RectTransform>();
            TerminalUiBuilder.SetAnchors(topBar, 0f, 0.92f, 1f, 1f);
            topBar.GetComponent<Image>().raycastTarget = false;

            var bottomBar = CreateFullscreenBg(root, "LetterboxBottom", Color.black).GetComponent<RectTransform>();
            TerminalUiBuilder.SetAnchors(bottomBar, 0f, 0f, 1f, 0.08f);
            bottomBar.GetComponent<Image>().raycastTarget = false;

            // Skip hold bar (shown on 2nd+ black card in a session)
            var skipRect = TerminalUiBuilder.CreateRect("BlackCardSkip", root);
            TerminalUiBuilder.SetAnchors(skipRect, 0.70f, 0.105f, 0.95f, 0.155f);
            _endSolSkipContainer = skipRect.gameObject;

            var trackPanel = TerminalUiBuilder.CreateFlatPanel("SkipTrack", skipRect, new Color(0.06f, 0.08f, 0.12f, 0.85f), _theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(trackPanel, 0f, 0f, 1f, 0.22f);
            var fillRect = TerminalUiBuilder.CreateRect("SkipFill", TerminalUiBuilder.GetInner(trackPanel));
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _endSolSkipFill = fillRect.gameObject.AddComponent<Image>();
            _endSolSkipFill.color = _theme.TextAmber;
            _endSolSkipFill.raycastTarget = false;

            _endSolSkipLabel = TerminalUiBuilder.CreateText("SkipLabel", skipRect, _theme, 20f, _theme.TextMuted, TextAlignmentOptions.BottomRight, _endSolTextScaler);
            _endSolSkipLabel.font = _theme.ResolveStampFont();
            _endSolSkipLabel.text = Localization.Get("end_sol.hold_skip");
            TerminalUiBuilder.SetAnchors(_endSolSkipLabel.rectTransform, 0f, 0.34f, 1f, 1f);

            _endSolSkipContainer.SetActive(false);
            _endSolPanel.SetActive(false);
        }

        private void SetEndSolSkipProgress(float progress01)
        {
            if (_endSolSkipFill == null) return;
            RectTransform rt = _endSolSkipFill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
