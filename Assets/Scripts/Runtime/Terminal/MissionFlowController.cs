using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
            EndingSequence,
            Debrief,
            Credits
        }

        private const string IntroSeenPrefsKey = "introSeen";

        [Header("Flow Options")]
        [SerializeField] private bool startAtTitleScreen = true;

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
        private Slider _titleVolumeSlider;
        private TextMeshProUGUI _titleVolumeValText;
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
        private Slider _pauseVolumeSlider;
        private TextMeshProUGUI _pauseVolumeValText;
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
        private TextMeshProUGUI _debriefRightText;

        // Credits UI
        private GameObject _creditsPanel;
        private TextMeshProUGUI _creditsBodyText;

        private readonly StringBuilder _sb = new StringBuilder(1024);

        public FlowState CurrentState => _state;

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
                _roomController.OnEnteredTerminal += HandlePlayerEnteredTerminal;
                _roomController.OnExitedTerminal += HandlePlayerExitedTerminal;
                _roomController.ExternalLeaveBlockCheck = () => _state != FlowState.Gameplay;
                _roomController.IsPauseMenuOpenCheck = () => _state == FlowState.PauseMenu;
            }

            if (_terminalController != null)
            {
                _terminalController.OnDebugSkipIntroRequested = DebugSkipIntro;
                _terminalController.OnDebugReplayIntroRequested = DebugReplayIntro;
                _terminalController.OnDebugResetIntroSeenRequested = DebugResetIntroSeen;
                _terminalController.OnDebugJumpToDebriefRequested = DebugJumpToDebrief;
                _terminalController.OnDebugTeleportSpawnRequested = DebugTeleportToSpawn;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_session != null)
                _session.OnMissionEnded -= HandleMissionEnded;

            if (_roomController != null)
            {
                _roomController.OnPauseRequested -= HandlePauseRequested;
                _roomController.OnEnteredTerminal -= HandlePlayerEnteredTerminal;
                _roomController.OnExitedTerminal -= HandlePlayerExitedTerminal;
                _roomController.ExternalLeaveBlockCheck = null;
                _roomController.IsPauseMenuOpenCheck = null;
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

                if (Time.frameCount > _stateEnteredFrame && ShouldStartFromTitleKeyboard(kb))
                {
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
                if (kb != null && Time.frameCount > _stateEnteredFrame && kb.escapeKey.wasPressedThisFrame)
                {
                    ResumeFromPause();
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
            if (_titlePanel != null) _titlePanel.SetActive(true);
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
                string raw = (card.textLineKeys != null && card.textLineKeys.Length > 0)
                    ? Localization.Get(card.textLineKeys[0]).ToUpperInvariant()
                    : string.Empty;
                if (_introBigText != null)
                {
                    _introBigText.color = Color.white;
                    _introBigText.text = FormatAccentWord(raw, card.accentWordIndex);
                    _introBigText.characterSpacing = 40f;
                }

                yield return AnimateCardEntranceAndHold(_introBigText, card, slamScale: false, applyMeshJitter: !reduceFlicker);
            }
            else if (card.type == IntroCardType.TitleSlam)
            {
                if (_introSlamGroup != null) _introSlamGroup.SetActive(true);
                if (_introSlamMainText != null)
                {
                    _introSlamMainText.text = (card.textLineKeys != null && card.textLineKeys.Length > 0)
                        ? Localization.Get(card.textLineKeys[0]).ToUpperInvariant()
                        : "MARS OUTPOST";
                    _introSlamMainText.characterSpacing = 28f;
                }
                if (_introSlamSubText != null)
                {
                    _introSlamSubText.text = (card.textLineKeys != null && card.textLineKeys.Length > 1)
                        ? Localization.Get(card.textLineKeys[1]).ToUpperInvariant()
                        : "LAST SHIFT";
                    _introSlamSubText.characterSpacing = 36f;
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
                    text.characterSpacing = Mathf.Lerp(40f, 18f, curved);
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
                Localization.Get("boot.line1"),
                Localization.Get("boot.line2"),
                Localization.Get("boot.line3"),
                Localization.Get("boot.line4")
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

        private void HandlePauseRequested()
        {
            if (_state != FlowState.Gameplay) return;
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
            int nextSeed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed + 1 : 101;
            _audioManager?.StopIntroDrone(immediate: true);
            _audioManager?.SetEndingState(false, false);
            _roomStateBinder?.SetEndingLightingOverride(false, false);
            _session?.RestartMission(nextSeed);
            _terminalController?.ResetForNewRun(deferInitialEvent: true);
            EnterTitleState();
        }

        // --- PART E DEV TOOLS ---

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
            _debriefHeaderText.text = $"{Localization.Get("debrief.title")}  |  {(won ? Localization.Get("mission.won.title") : Localization.Get("mission.lost.title"))}  |  GRADE: {s.Grade}";

            _debriefSummaryText.text =
                $"CMDR. {s.CommanderName}  (SEED {s.Seed})    |    SURVIVORS: {alive}/4    |    AVG HP: {avgHp:0}%    |    SCORE: {s.FinalScore:0.0}\n" +
                $"REMAINING STOCKS —  PWR: {s.Power:0}%   O2: {s.Oxygen:0}%   H2O: {s.Water:0}%   FOOD: {s.Food:0}%   SHLD: {s.Shielding:0}%";

            // Left column: Crew outcomes + Key Decisions Timeline
            _sb.Clear();
            _sb.AppendLine($"<b><color=#FFB84D>{Localization.Get("debrief.crew")}</color></b>");
            for (int i = 0; i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                _sb.AppendLine($"• {c.Name} ({c.Role}) — [{c.StatusTag}]  HP {c.Health:0}%  |  Morale {c.Morale:0}%  |  Dose {c.DoseMsv:0.0} mSv");
            }

            _sb.AppendLine();
            _sb.AppendLine($"<b><color=#FFB84D>{Localization.Get("debrief.timeline")}</color></b>");
            if (s.DebriefCards.Count == 0)
            {
                _sb.AppendLine("• No event decisions recorded yet.");
            }
            else
            {
                for (int i = 0; i < s.DebriefCards.Count; i++)
                {
                    var d = s.DebriefCards[i];
                    string choiceLabel = !string.IsNullOrEmpty(d.ChoiceLabelKey) ? Localization.Get(d.ChoiceLabelKey) : Localization.Get(d.OutcomeKey);
                    string outcomeText = Localization.Get(d.OutcomeKey);
                    _sb.AppendLine($"• SOL {d.Sol} [{Localization.Get(d.TitleKey)}]: {choiceLabel} -> {outcomeText}");
                }
            }
            _debriefLeftText.text = _sb.ToString();

            // Right column: What NASA Knows (FactCards per distinct event)
            _sb.Clear();
            _sb.AppendLine($"<b><color=#FFB84D>{Localization.Get("debrief.what_nasa_knows")}</color></b>");
            var seenEvents = new HashSet<string>();
            int cardCount = 0;

            for (int i = 0; i < s.DebriefCards.Count; i++)
            {
                var d = s.DebriefCards[i];
                string evId = d.EventId;
                if (string.IsNullOrEmpty(evId) || !seenEvents.Add(evId)) continue;

                FactCard card = _factDb != null ? _factDb.GetByEventId(evId) : null;
                AppendFactCardEntry(_sb, card, d.TitleKey, d.FactKey, d.SourceId);
                cardCount++;
            }

            if (cardCount == 0 && _factDb != null && _factDb.Cards != null && _factDb.Cards.Count > 0)
            {
                _sb.AppendLine("(Sample card shown when jumping straight to debrief before events resolve:)");
                AppendFactCardEntry(_sb, _factDb.Cards[0], null, null, _factDb.Cards[0].sourceId);
            }
            _debriefRightText.text = _sb.ToString();
        }

        private void AppendFactCardEntry(StringBuilder sb, FactCard card, string fallbackTitleKey, string fallbackFactKey, string sourceId)
        {
            string title = card != null && !string.IsNullOrEmpty(card.title)
                ? card.title
                : Localization.Get(fallbackTitleKey);
            string body = card != null && !string.IsNullOrEmpty(card.text)
                ? card.text
                : Localization.Get(fallbackFactKey);
            string srcId = card != null && !string.IsNullOrEmpty(card.sourceId) ? card.sourceId : sourceId;

            string srcLabel = srcId;
            if (_sourcesDb != null && _sourcesDb.Entries != null)
            {
                for (int i = 0; i < _sourcesDb.Entries.Count; i++)
                {
                    if (_sourcesDb.Entries[i].Id == srcId)
                    {
                        srcLabel = $"{_sourcesDb.Entries[i].Id} ({_sourcesDb.Entries[i].Title})";
                        break;
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"<b>{title}</b>");
            sb.AppendLine(body);
            sb.AppendLine($"<color=#99AABB>Source: {srcLabel}</color>");
        }

        private void PopulateCredits()
        {
            _sb.Clear();
            _sb.AppendLine($"<b>{Localization.Get("game.title")}</b>");
            _sb.AppendLine(Localization.Get("credits.challenge"));
            _sb.AppendLine(Localization.Get("credits.unity"));
            _sb.AppendLine(Localization.Get("credits.fonts"));
            _sb.AppendLine();
            _sb.AppendLine("<b><color=#45E3D3>--- ASSETS, PACKAGES & TOOLS ---</color></b>");

            if (_sourcesDb != null && _sourcesDb.AssetCredits != null)
            {
                string lastSec = string.Empty;
                for (int i = 0; i < _sourcesDb.AssetCredits.Count; i++)
                {
                    var a = _sourcesDb.AssetCredits[i];
                    if (a.Section != lastSec)
                    {
                        lastSec = a.Section;
                        _sb.AppendLine($"<b><color=#7FDFFF>[{lastSec}]</color></b>");
                    }
                    _sb.AppendLine($"• {a.Title} — {a.Organization}");
                    if (!string.IsNullOrEmpty(a.License))
                        _sb.AppendLine($"  License: {a.License} | Used: {a.WhereUsed}");
                }
            }

            _sb.AppendLine();
            _sb.AppendLine("<b><color=#F7C948>--- SCIENTIFIC SOURCES DATABASE ---</color></b>");

            if (_sourcesDb != null && _sourcesDb.Entries != null)
            {
                for (int i = 0; i < _sourcesDb.Entries.Count; i++)
                {
                    var e = _sourcesDb.Entries[i];
                    _sb.AppendLine($"• [{e.Id}] {e.Title} — {e.Organization}");
                    _sb.AppendLine($"  {e.UrlText}");
                }
            }
            _creditsBodyText.text = _sb.ToString();
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
                    target.text = prefix + line;
                    yield break;
                }

                char c = line[i - 1];
                target.text = prefix + line.Substring(0, i);
                if (playClick && !char.IsWhiteSpace(c))
                    _audioManager?.PlayTypeClick();

                float elapsed = 0f;
                while (elapsed < charDelay)
                {
                    if (checkSkip && _skipRequested)
                    {
                        target.text = prefix + line;
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
            _titleStartBtn = CreateSimpleTextMenuButton("PlayGameBtn", root, "Play Game", 0.435f, 0.505f, () =>
            {
                _audioManager?.PlayUiClick();
                StartIntroSequence();
            });

            CreateSimpleTextMenuButton("SettingsBtn", root, "Settings", 0.345f, 0.415f, () =>
            {
                _audioManager?.PlayUiClick();
                RefreshPauseLabels();
                if (_titleSettingsModal != null)
                    _titleSettingsModal.SetActive(true);
            });

            var credBtn = CreateSimpleTextMenuButton("CreditsBtn", root, "Credits", 0.255f, 0.325f, () =>
            {
                _audioManager?.PlayUiClick();
                EnterCreditsState(FlowState.Title);
            });
            _titleCredBtnGo = credBtn.gameObject;

            CreateSimpleTextMenuButton("ExitBtn", root, "Exit", 0.165f, 0.235f, () =>
            {
                _audioManager?.PlayUiClick();
                QuitApplication();
            });

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

        private Button CreateSimpleTextMenuButton(string name, RectTransform parent, string label, float yMin, float yMax, Action onClick)
        {
            var rect = TerminalUiBuilder.CreateRect(name, parent);
            TerminalUiBuilder.SetAnchors(rect, 0.088f, yMin, 0.36f, yMax);

            // Transparent hit area with no visible box or border
            var hitImg = rect.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;
            hitImg.raycastTarget = true;

            var txt = TerminalUiBuilder.CreateText("Label", rect, _theme, 36f, Color.white, TextAlignmentOptions.MidlineLeft, _textScaler);
            txt.font = _theme.ResolveTitleFont();
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

        private void BuildTitleSettingsModal(RectTransform root)
        {
            _titleSettingsModal = CreateFullscreenBg(root, "TitleSettingsModal", new Color(0.02f, 0.02f, 0.04f, 0.82f));
            var box = TerminalUiBuilder.CreateFlatPanel("SettingsBox", _titleSettingsModal.GetComponent<RectTransform>(), new Color(0.08f, 0.06f, 0.07f, 0.96f), new Color(0.86f, 0.45f, 0.27f, 1f), 2f, addDropShadow: true);
            TerminalUiBuilder.SetAnchors(box, 0.26f, 0.14f, 0.74f, 0.86f);
            var inner = TerminalUiBuilder.GetInner(box);

            var title = TerminalUiBuilder.CreateText("SettingsTitle", inner, _theme, 36f, new Color(0.95f, 0.65f, 0.42f, 1f), TextAlignmentOptions.Center, _textScaler);
            title.font = _theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = "SETTINGS";
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0f, 0.86f, 1f, 0.97f);

            BuildSharedSettingsControls(
                inner,
                0.80f,
                out _titleVolumeSlider,
                out _titleVolumeValText,
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

        private static void QuitApplication()
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
            var root = _pausePanel.GetComponent<RectTransform>();

            var box = TerminalUiBuilder.CreateFlatPanel("PauseBox", root, _theme.WindowBg, _theme.BorderFocused, 2f);
            TerminalUiBuilder.SetAnchors(box, 0.26f, 0.09f, 0.74f, 0.91f);
            var inner = TerminalUiBuilder.GetInner(box);

            var title = TerminalUiBuilder.CreateText("PauseTitle", inner, _theme, 36f, _theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            title.font = _theme.ResolveTitleFont();
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("pause.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0f, 0.89f, 1f, 0.98f);

            var resumeBtn = TerminalUiBuilder.CreateButton("ResumeBtn", inner, Localization.Get("pause.resume"), _theme, _theme.ButtonAccentBg, _theme.BorderFocused, _theme.TextPrimary, 24f, ResumeFromPause, _textScaler);
            TerminalUiBuilder.SetAnchors(resumeBtn.GetComponent<RectTransform>(), 0.10f, 0.78f, 0.90f, 0.87f);

            var restartBtn = TerminalUiBuilder.CreateButton("RestartBtn", inner, Localization.Get("pause.restart"), _theme, _theme.ButtonBg, _theme.BorderUnfocused, _theme.TextPrimary, 24f, () =>
            {
                _audioManager?.PlayUiClick();
                int seed = _session?.CurrentSnapshot != null ? _session.CurrentSnapshot.Seed : 101;
                RestartMissionRun(seed, runIntro: false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(restartBtn.GetComponent<RectTransform>(), 0.10f, 0.67f, 0.90f, 0.76f);

            BuildSharedSettingsControls(
                inner,
                0.65f,
                out _pauseVolumeSlider,
                out _pauseVolumeValText,
                out _pauseLargeTextLabel,
                out _pauseReduceFlickerLabel,
                out _pausePsxEnabledLabel,
                out _pausePsxResLabel);

            var quitBtn = TerminalUiBuilder.CreateButton("QuitTitleBtn", inner, Localization.Get("pause.quit_title"), _theme, _theme.ButtonDangerBg, _theme.StatusCrit, Color.white, 24f, () =>
            {
                _audioManager?.PlayUiClick();
                QuitToTitle();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(quitBtn.GetComponent<RectTransform>(), 0.10f, 0.06f, 0.90f, 0.17f);

            _pausePanel.SetActive(false);
        }

        private void BuildSharedSettingsControls(
            RectTransform inner,
            float topY,
            out Slider volumeSlider,
            out TextMeshProUGUI volumeValText,
            out TextMeshProUGUI largeTextLabel,
            out TextMeshProUGUI reduceFlickerLabel,
            out TextMeshProUGUI psxEnabledLabel,
            out TextMeshProUGUI psxResLabel)
        {
            float volLabelMin = topY - 0.06f;
            float volSliderMin = topY - 0.12f;
            float volSliderMax = topY - 0.07f;
            float row1Min = topY - 0.24f;
            float row1Max = topY - 0.15f;
            float gfxMin = topY - 0.31f;
            float gfxMax = topY - 0.26f;
            float row2Min = topY - 0.42f;
            float row2Max = topY - 0.32f;

            var volLabel = TerminalUiBuilder.CreateText("VolLabel", inner, _theme, 22f, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _textScaler);
            volLabel.text = Localization.Get("pause.volume");
            TerminalUiBuilder.SetAnchors(volLabel.rectTransform, 0.10f, volLabelMin, 0.65f, topY);

            volumeValText = TerminalUiBuilder.CreateText("VolValue", inner, _theme, 22f, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
            TerminalUiBuilder.SetAnchors(volumeValText.rectTransform, 0.65f, volLabelMin, 0.90f, topY);

            volumeSlider = CreateFlatSlider("VolSlider", inner);
            TerminalUiBuilder.SetAnchors(volumeSlider.GetComponent<RectTransform>(), 0.10f, volSliderMin, 0.90f, volSliderMax);
            volumeSlider.onValueChanged.AddListener(v =>
            {
                if (_audioManager != null) _audioManager.MasterVolume = v;
                RefreshPauseLabels();
            });

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
            gfxHeader.text = "GRAPHICS";
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
            bool largeOn = _accessibility != null && _accessibility.LargerText;
            bool flickerOn = _accessibility != null && _accessibility.ReduceFlicker;
            bool psxOn = _psxSettings != null && _psxSettings.enabled;
            string resLbl = _psxSettings != null ? _psxSettings.GetPresetLabel() : "MED (480x270)";

            ApplySettingsValues(vol, largeOn, flickerOn, psxOn, resLbl, _pauseVolumeSlider, _pauseVolumeValText, _pauseLargeTextLabel, _pauseReduceFlickerLabel, _pausePsxEnabledLabel, _pausePsxResLabel);
            ApplySettingsValues(vol, largeOn, flickerOn, psxOn, resLbl, _titleVolumeSlider, _titleVolumeValText, _titleLargeTextLabel, _titleReduceFlickerLabel, _titlePsxEnabledLabel, _titlePsxResLabel);
        }

        private static void ApplySettingsValues(
            float vol,
            bool largeOn,
            bool flickerOn,
            bool psxOn,
            string resLbl,
            Slider volumeSlider,
            TextMeshProUGUI volumeValText,
            TextMeshProUGUI largeTextLabel,
            TextMeshProUGUI reduceFlickerLabel,
            TextMeshProUGUI psxEnabledLabel,
            TextMeshProUGUI psxResLabel)
        {
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(vol);
            if (volumeValText != null) volumeValText.text = $"{Mathf.RoundToInt(vol * 100f)}%";
            if (largeTextLabel != null)
                largeTextLabel.text = $"{Localization.Get("pause.larger_text")}: {(largeOn ? "ON" : "OFF")}";
            if (reduceFlickerLabel != null)
                reduceFlickerLabel.text = $"{Localization.Get("pause.reduce_flicker")}: {(flickerOn ? "ON" : "OFF")}";
            if (psxEnabledLabel != null)
                psxEnabledLabel.text = $"{Localization.Get("pause.psx_look")}: {(psxOn ? "ON" : "OFF")}";
            if (psxResLabel != null)
                psxResLabel.text = $"{Localization.Get("pause.psx_res")}: {resLbl}";
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

            _debriefRightText = TerminalUiBuilder.CreateScrollableText("DebriefRightScroll", root, _theme, 22f, _theme.TextPrimary, _theme.PanelBg, _theme.BorderUnfocused, out var rightPanel, _textScaler);
            _debriefRightText.font = _theme.ResolveFont();
            TerminalUiBuilder.SetAnchors(rightPanel, 0.515f, 0.12f, 0.97f, 0.77f);

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
    }
}
