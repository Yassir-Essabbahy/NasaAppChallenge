using System;
using System.Collections.Generic;
using System.Text;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Apps = Outpost.Runtime.Terminal.Apps;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Owns TerminalRoot and orchestrates Mission Control Home (5 supply tiles, goal tracker, Next Step,
    /// Sol 1 onboarding checklist, 4 crew cards, HELIOS assistant), desktop windows, modals, and debug panel.
    /// </summary>
    public class TerminalController : MonoBehaviour
    {
        private const string PrefHintsEnabled = "Outpost_HintsEnabled";
        private const string PrefOnboardingDone = "Outpost_OnboardingDone";

        [Header("Assets & Theme")]
        [SerializeField] private UITheme theme;
        [SerializeField] private SourcesDatabase sourcesDatabase;
        [SerializeField] private GlossaryDatabase glossaryDatabase;
        [SerializeField] private RectTransform terminalRoot;

        public bool IsTerminalVisible => terminalRoot != null && terminalRoot.gameObject.activeSelf;
        public bool IsDebugPanelVisible => _debugPanel != null && _debugPanel.activeSelf;
        public bool IsReadOnlyMode { get; private set; }
        public bool IsModalBlockingLeanBack =>
            (_eventModal != null && _eventModal.activeSelf) ||
            (_confirmEndSolModal != null && _confirmEndSolModal.activeSelf) ||
            (_transitionOverlay != null && _transitionOverlay.activeSelf) ||
            (_helpModal != null && _helpModal.activeSelf);

        public bool UseExternalEndingFlow { get; set; }

        public event Action OnLeaveTerminalRequested;
        public event Action<bool> OnDebugTeleportLeanRequested;
        public event Action OnUiSoundRequested;
        public Action OnDebugSkipIntroRequested;
        public Action OnDebugReplayIntroRequested;
        public Action OnDebugResetIntroSeenRequested;
        public Action OnDebugJumpToDebriefRequested;
        public Action OnDebugTeleportSpawnRequested;
        public Action OnDebugUnlockAllDoorsRequested;
        public Action OnDebugTeleportCorridorRequested;
        public Action OnDebugTeleportAirlockRequested;
        public Action OnDebugTeleportSurfaceRequested;

        private GameSession _session;
        private WindowManager _windowManager;
        private AccessibilityTextScaler _textScaler;
        private AccessibilitySettings _accessibility;
        private PSXSettings _psxSettings;
        private bool _built;
        private bool _showLeaveButton;
        private bool _deferInitialEvent;
        private bool _waitingFirstLogCloseHint;
        private int _lastDebugToggleFrame = -1;

        // Top Bar
        private TextMeshProUGUI _solBadgeText;
        private TextMeshProUGUI _solClockText;
        private TextMeshProUGUI _apHeaderText;

        // Read-Only Banner
        private GameObject _readOnlyBanner;
        private TextMeshProUGUI _readOnlyBannerText;

        // Outpost Resources table rows
        private struct SupplyTileUi
        {
            public RectTransform Panel;
            public Image InnerImg;
            public Image FlashOverlay;
            public TextMeshProUGUI ValueText;
            public TextMeshProUGUI StatusWordText;
            public Image StatusBadgeBg;
            public Image FillImage;
            public SmoothBarFill BarAnimator;
            public Image TrendArrow;
            public TextMeshProUGUI TrendText;
            public TextMeshProUGUI DeltaPopupText;
            public TooltipTrigger Tooltip;
            public string SystemLabel;
            public string GlossaryDef;
            public float PopupTimer;
            public float FlashTimer;
            public float LastValue;
            public bool HasValue;
        }
        private readonly SupplyTileUi[] _supplyTiles = new SupplyTileUi[5];

        // Crew Status table rows
        private struct CrewCardUi
        {
            public RectTransform Panel;
            public Image AvatarBoxBg;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI RoleText;
            public TextMeshProUGUI StatusTagText;
            public TextMeshProUGUI HpText;
            public TooltipTrigger Tooltip;
        }
        private readonly CrewCardUi[] _crewCards = new CrewCardUi[4];

        // Left Sidebar Navigation (Overview + 8 Apps)
        private RectTransform _overviewNavPanel;
        private Image _overviewNavInner;
        private Image _overviewNavIcon;
        private TextMeshProUGUI _overviewNavLabel;
        private readonly RectTransform[] _iconPanels = new RectTransform[9];
        private readonly Image[] _iconInners = new Image[9];
        private readonly Image[] _iconImgs = new Image[9];
        private readonly TextMeshProUGUI[] _iconLabels = new TextMeshProUGUI[9];
        private readonly TextMeshProUGUI[] _iconBadges = new TextMeshProUGUI[8];
        private TerminalAppId? _pulsingApp;

        // Center Column: Mission Goal, Outpost Alert, Checklist / HELIOS Brief
        private TextMeshProUGUI _goalTitleText;
        private readonly Image[] _solTrackPips = new Image[7];
        private readonly TextMeshProUGUI[] _solTrackLabels = new TextMeshProUGUI[7];

        private RectTransform _nextStepCard;
        private Image _nextStepInner;
        private Image _alertCardIcon;
        private TextMeshProUGUI _alertCardTitle;
        private TextMeshProUGUI _nextStepBodyText;
        private Button _nextStepBtn;
        private Image _nextStepBtnBg;
        private TextMeshProUGUI _nextStepBtnLabel;
        private TerminalAppId? _nextStepTargetApp;
        private bool _nextStepOpensEvent;

        private RectTransform _onboardingCard;
        private Image _bottomCardIcon;
        private TextMeshProUGUI _bottomCardTitle;
        private GameObject _checklistGroup;
        private bool _onboardingDone;
        private bool _onboardStep1Done;
        private bool _onboardStep2Done;
        private bool _onboardStep3Done;
        private readonly Image[] _onboardChecks = new Image[3];
        private readonly TextMeshProUGUI[] _onboardLabels = new TextMeshProUGUI[3];

        private GameObject _heliosBubble;
        private TextMeshProUGUI _heliosText;
        private Button _hintsToggleBtn;
        private TextMeshProUGUI _hintsToggleLabel;
        private bool _hintsEnabled = true;

        // Bottom Forecast + Taskbar
        private TextMeshProUGUI _forecastStripText;
        private readonly Button[] _taskWinBtns = new Button[WindowManager.MaxOpenWindows];
        private readonly RectTransform[] _taskWinPanels = new RectTransform[WindowManager.MaxOpenWindows];
        private readonly TextMeshProUGUI[] _taskWinLabels = new TextMeshProUGUI[WindowManager.MaxOpenWindows];
        private Button _leaveTerminalBtn;
        private Button _endSolBtn;
        private TextMeshProUGUI _endSolLabel;

        // Fake Sol Clock
        private float _solMinutesElapsed;
        private int _lastDisplayedMinute = -1;
        private int _cachedSolNumber = -1;

        // Modals, Help, Tooltip & Overlays
        private GameObject _eventModal;
        private Image _eventBannerBg;
        private Image _eventBannerIcon;
        private TextMeshProUGUI _eventBannerSubtitle;
        private TextMeshProUGUI _eventTitleText;
        private TextMeshProUGUI _eventDescText;
        private readonly Button[] _eventOptionBtns = new Button[3];
        private readonly TextMeshProUGUI[] _eventOptionLabels = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _eventOptionChips = new TextMeshProUGUI[3];
        private GameObject _eventResultBox;
        private TextMeshProUGUI _eventResultText;
        private TextMeshProUGUI _eventFactText;
        private Button _eventContinueBtn;

        private GameObject _confirmEndSolModal;
        private TextMeshProUGUI _confirmBodyText;

        private GameObject _transitionOverlay;
        private CanvasGroup _transitionGroup;
        private RectTransform _transitionCard;
        private TextMeshProUGUI _transitionTitleText;
        private readonly TextMeshProUGUI[] _transitionBullets = new TextMeshProUGUI[3];
        private bool _playingFadeToBlack;
        private float _fadeTimer;

        private GameObject _helpModal;
        private GameObject _missionEndOverlay;
        private TextMeshProUGUI _missionEndTitle;
        private TextMeshProUGUI _missionEndBody;

        private GameObject _tooltipPanel;
        private RectTransform _tooltipRect;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;

        private GameObject _toastBanner;
        private TextMeshProUGUI _toastText;
        private float _toastTimer;

        private GameObject _debugPanel;
        private TextMeshProUGUI _debugFpsText;
        private float _smoothedFps = 60f;
        private float _fpsLabelTimer;
        private Slider _dbgSnapSlider, _dbgColorSlider, _dbgDitherSlider, _dbgAffineSlider, _dbgGrainSlider, _dbgResSlider;
        private TextMeshProUGUI _dbgSnapVal, _dbgColorVal, _dbgDitherVal, _dbgAffineVal, _dbgGrainVal, _dbgResVal;
        private bool _syncingDebugSliders;

        private readonly StringBuilder _sb = new StringBuilder(512);

        public void Initialize(GameSession session, UITheme overrideTheme = null, SourcesDatabase overrideSources = null)
        {
            if (overrideTheme != null) theme = overrideTheme;
            if (overrideSources != null) sourcesDatabase = overrideSources;

            UnsubscribeSession();
            _session = session;

            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                theme.ApplyFriendlyThemeDefaults();
            }
            TerminalUiArt.SetActiveTheme(theme);
            if (sourcesDatabase == null)
                sourcesDatabase = Resources.Load<SourcesDatabase>("SourcesDatabase") ?? ScriptableObject.CreateInstance<SourcesDatabase>();
            if (glossaryDatabase == null)
                glossaryDatabase = Resources.Load<GlossaryDatabase>("GlossaryDatabase");
            if (glossaryDatabase == null)
            {
                glossaryDatabase = ScriptableObject.CreateInstance<GlossaryDatabase>();
                glossaryDatabase.PopulateDefaults();
            }

            _hintsEnabled = PlayerPrefs.GetInt(PrefHintsEnabled, 1) == 1;
            _onboardingDone = PlayerPrefs.GetInt(PrefOnboardingDone, 0) == 1;

            if (!_built)
            {
                BuildTerminalUI();
                _built = true;
            }

            _windowManager.Initialize(_session);
            SubscribeSession();

            if (_session.CurrentSnapshot != null)
            {
                ResetClockForSol(_session.CurrentSnapshot.CurrentSol);
                RefreshAll(_session.CurrentSnapshot);
                if (!_deferInitialEvent && _session.CurrentSnapshot.ActiveEvent != null && !_session.CurrentSnapshot.ActiveEvent.IsResolved)
                    ShowIncomingEvent(_session.CurrentSnapshot);
            }
        }

        public void SetDeferInitialEvent(bool defer)
        {
            _deferInitialEvent = defer;
        }

        public void SetReadOnlyMode(bool readOnly)
        {
            IsReadOnlyMode = readOnly;
            if (_windowManager != null)
                _windowManager.IsReadOnlyMode = readOnly;
            if (_readOnlyBanner != null)
                _readOnlyBanner.SetActive(readOnly);
            if (_endSolBtn != null)
                _endSolBtn.gameObject.SetActive(!readOnly);

            if (_session?.CurrentSnapshot != null)
            {
                RefreshAll(_session.CurrentSnapshot);
                if (_windowManager != null)
                    _windowManager.RefreshOpenWindows(_session.CurrentSnapshot);
            }
        }

        public void BeginFirstShiftCommandLog()
        {
            if (!_built || _windowManager == null) return;
            _deferInitialEvent = true;
            _waitingFirstLogCloseHint = true;
            if (_eventModal != null) _eventModal.SetActive(false);
            if (_confirmEndSolModal != null) _confirmEndSolModal.SetActive(false);
            if (_transitionOverlay != null) _transitionOverlay.SetActive(false);
            if (_helpModal != null) _helpModal.SetActive(false);
            _windowManager.CloseAll();
            _windowManager.OpenWindow(TerminalAppId.CommandersLog);
            if (_session?.CurrentSnapshot != null) RefreshAll(_session.CurrentSnapshot);
        }

        public void NotifyPlayerExitedTerminal()
        {
            _deferInitialEvent = false;
            _waitingFirstLogCloseHint = false;
            HideTooltip();
            if (_helpModal != null) _helpModal.SetActive(false);
            if (_session?.CurrentSnapshot != null) RefreshAll(_session.CurrentSnapshot);
        }

        public void NotifyPlayerEnteredTerminal()
        {
            if (_deferInitialEvent)
            {
                _deferInitialEvent = false;
                var s = _session?.CurrentSnapshot;
                if (!IsReadOnlyMode && s != null && s.ActiveEvent != null && !s.ActiveEvent.IsResolved && s.Status == MissionStatus.InProgress)
                    ShowIncomingEvent(s);
            }
        }

        public void ResetForNewRun(bool deferInitialEvent)
        {
            _deferInitialEvent = deferInitialEvent;
            _waitingFirstLogCloseHint = false;
            _playingFadeToBlack = false;
            _onboardingDone = PlayerPrefs.GetInt(PrefOnboardingDone, 0) == 1;
            _onboardStep1Done = false;
            _onboardStep2Done = false;
            _onboardStep3Done = false;
            for (int i = 0; i < _supplyTiles.Length; i++) _supplyTiles[i].HasValue = false;

            if (_eventModal != null) _eventModal.SetActive(false);
            if (_confirmEndSolModal != null) _confirmEndSolModal.SetActive(false);
            if (_transitionOverlay != null) _transitionOverlay.SetActive(false);
            if (_missionEndOverlay != null) _missionEndOverlay.SetActive(false);
            if (_helpModal != null) _helpModal.SetActive(false);
            if (_toastBanner != null) _toastBanner.SetActive(false);
            HideTooltip();
            _windowManager?.CloseAll();
            if (_session?.CurrentSnapshot != null)
            {
                ResetClockForSol(_session.CurrentSnapshot.CurrentSol);
                RefreshAll(_session.CurrentSnapshot);
                if (!IsReadOnlyMode && !_deferInitialEvent && _session.CurrentSnapshot.ActiveEvent != null && !_session.CurrentSnapshot.ActiveEvent.IsResolved)
                    ShowIncomingEvent(_session.CurrentSnapshot);
            }
        }

        public void ShowToastMessage(string message, float durationSeconds = 4.5f)
        {
            if (_toastBanner == null || _toastText == null) return;
            _toastText.text = message;
            _toastBanner.SetActive(true);
            _toastTimer = durationSeconds;
        }

        private void HandleWindowClosed(TerminalAppId appId)
        {
            if (appId == TerminalAppId.CommandersLog && _waitingFirstLogCloseHint)
            {
                _waitingFirstLogCloseHint = false;
                ShowToastMessage(Localization.Get("hint.press_esc_stand"), 8.0f);
            }
        }

        public void SetTerminalVisible(bool visible)
        {
            if (!visible) HideTooltip();
            if (terminalRoot != null)
                terminalRoot.gameObject.SetActive(visible);
        }

        public void SetM3Integration(bool showLeaveButton, AccessibilitySettings accessibility)
        {
            _showLeaveButton = showLeaveButton;
            _accessibility = accessibility;
            if (_accessibility != null && _textScaler != null)
                _accessibility.BindTextScaler(_textScaler);
            if (_leaveTerminalBtn != null)
                _leaveTerminalBtn.gameObject.SetActive(_showLeaveButton);
        }

        public void SetPsxSettings(PSXSettings psxSettings)
        {
            _psxSettings = psxSettings;
            SyncPsxDebugSliders();
        }

        public void OpenApp(TerminalAppId appId)
        {
            if (!_built || _windowManager == null) return;
            if (appId == TerminalAppId.PowerGrid)
                MarkOnboardingStep1();
            _windowManager.OpenWindow(appId);
        }

        public void ToggleDebugPanel()
        {
            if (_debugPanel == null) return;
            _lastDebugToggleFrame = Time.frameCount;
            bool next = !_debugPanel.activeSelf;
            _debugPanel.SetActive(next);
            if (next) SyncPsxDebugSliders();
        }

        public void ShowModalBlockWarning()
        {
            ShowToastMessage(Localization.Get("alert.resolve_event_first"), 2.2f);
        }

        private void OnDestroy()
        {
            UnsubscribeSession();
            TerminalUiBuilder.OnShowTooltipRequested -= ShowTooltip;
            TerminalUiBuilder.OnHideTooltipRequested -= HideTooltip;
            Apps.PowerGridAppWindow.OnPlayerAdjustedPower -= HandlePowerSliderAdjusted;
        }

        private void SubscribeSession()
        {
            if (_session == null) return;
            _session.OnSolStarted += HandleSolStarted;
            _session.OnStateChanged += HandleStateChanged;
            _session.OnEventTriggered += HandleEventTriggered;
            _session.OnMissionEnded += HandleMissionEnded;
        }

        private void UnsubscribeSession()
        {
            if (_session == null) return;
            _session.OnSolStarted -= HandleSolStarted;
            _session.OnStateChanged -= HandleStateChanged;
            _session.OnEventTriggered -= HandleEventTriggered;
            _session.OnMissionEnded -= HandleMissionEnded;
        }

        private void Update()
        {
            if (_session == null || _session.CurrentSnapshot == null) return;

            if (_session.CurrentSnapshot.Status == MissionStatus.InProgress && !_playingFadeToBlack)
            {
                _solMinutesElapsed += Time.deltaTime * 4f;
                if (_solMinutesElapsed > 960f) _solMinutesElapsed = 960f;
                int totalMins = 360 + Mathf.FloorToInt(_solMinutesElapsed);
                if (totalMins != _lastDisplayedMinute || _cachedSolNumber != _session.CurrentSnapshot.CurrentSol)
                {
                    _lastDisplayedMinute = totalMins;
                    _cachedSolNumber = _session.CurrentSnapshot.CurrentSol;
                    int hh = (totalMins / 60) % 24;
                    int mm = totalMins % 60;
                    if (_solBadgeText != null)
                        _solBadgeText.text = Localization.Format("mc.sol_progress", _cachedSolNumber, _session.CurrentSnapshot.TotalSols);
                    if (_solClockText != null)
                        _solClockText.text = $"{hh:00}:{mm:00}";
                }
            }

            if (_pulsingApp.HasValue)
            {
                int idx = (int)_pulsingApp.Value;
                bool isFocused = _windowManager != null && _windowManager.FocusedApp.HasValue && _windowManager.FocusedApp.Value == _pulsingApp.Value;
                if (!isFocused && idx >= 0 && idx < 8 && _iconPanels[idx] != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                    Color c = Color.Lerp(new Color(0.15f, 0.25f, 0.34f, 1f), theme.GuidedPulseColor, pulse);
                    TerminalUiBuilder.SetBorderColor(_iconPanels[idx], c);
                }
            }

            // Update supply tile flashes and floating delta popups
            for (int i = 0; i < _supplyTiles.Length; i++)
            {
                ref var tile = ref _supplyTiles[i];
                if (tile.PopupTimer > 0f && tile.DeltaPopupText != null)
                {
                    tile.PopupTimer -= Time.unscaledDeltaTime;
                    float progress = 1f - Mathf.Clamp01(tile.PopupTimer / 0.85f);
                    tile.DeltaPopupText.rectTransform.anchoredPosition = new Vector2(0f, 18f + progress * 26f);
                    Color c = tile.DeltaPopupText.color;
                    c.a = 1f - progress * progress;
                    tile.DeltaPopupText.color = c;
                    if (tile.PopupTimer <= 0f)
                        tile.DeltaPopupText.gameObject.SetActive(false);
                }

                if (tile.FlashTimer > 0f && tile.FlashOverlay != null)
                {
                    tile.FlashTimer -= Time.unscaledDeltaTime;
                    float a = Mathf.Clamp01(tile.FlashTimer / 0.45f) * 0.25f;
                    Color fc = tile.FlashOverlay.color;
                    fc.a = a;
                    tile.FlashOverlay.color = fc;
                }
            }

            if (_playingFadeToBlack && _transitionOverlay != null)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_fadeTimer / 0.55f);
                _transitionGroup.alpha = t;
                if (t >= 1f)
                {
                    _playingFadeToBlack = false;
                    _transitionCard.gameObject.SetActive(true);
                }
            }

            if (_toastTimer > 0f && _toastBanner != null)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f)
                    _toastBanner.SetActive(false);
            }

            if (Time.unscaledDeltaTime > 0.0001f)
                _smoothedFps = Mathf.Lerp(_smoothedFps, 1f / Time.unscaledDeltaTime, 0.1f);
            if (_debugPanel != null && _debugPanel.activeSelf && _debugFpsText != null)
            {
                _fpsLabelTimer -= Time.unscaledDeltaTime;
                if (_fpsLabelTimer <= 0f)
                {
                    _fpsLabelTimer = 0.25f;
                    _debugFpsText.text = $"FPS: {_smoothedFps:0.0}";
                }
            }

            HandleKeyboardShortcuts();
        }

        private void HandleKeyboardShortcuts()
        {
            if (!IsTerminalVisible) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.backquoteKey.wasPressedThisFrame && _debugPanel != null && _lastDebugToggleFrame != Time.frameCount)
            {
                ToggleDebugPanel();
            }

            if (_helpModal != null && _helpModal.activeSelf)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                    _helpModal.SetActive(false);
                return;
            }

            if (_eventModal.activeSelf || _confirmEndSolModal.activeSelf || _transitionOverlay.activeSelf || _missionEndOverlay.activeSelf)
                return;

            if (kb.digit1Key.wasPressedThisFrame) OpenApp(TerminalAppId.PowerGrid);
            else if (kb.digit2Key.wasPressedThisFrame) OpenApp(TerminalAppId.LifeSupport);
            else if (kb.digit3Key.wasPressedThisFrame) OpenApp(TerminalAppId.Greenhouse);
            else if (kb.digit4Key.wasPressedThisFrame) OpenApp(TerminalAppId.Radiation);
            else if (kb.digit5Key.wasPressedThisFrame) OpenApp(TerminalAppId.Crew);
            else if (kb.digit6Key.wasPressedThisFrame) OpenApp(TerminalAppId.Comms);
            else if (kb.digit7Key.wasPressedThisFrame) OpenApp(TerminalAppId.CommandersLog);
            else if (kb.digit8Key.wasPressedThisFrame) OpenApp(TerminalAppId.Sources);
            else if (kb.eKey.wasPressedThisFrame && !IsReadOnlyMode) OnEndSolClicked();
        }

        private void HandleSolStarted(SessionSnapshot s)
        {
            ResetClockForSol(s.CurrentSol);
            if (s.LastSolSummary != null && s.CurrentSol > 1)
            {
                BeginSolTransition(s);
            }
            RefreshAll(s);
        }

        private void HandleStateChanged(SessionSnapshot s)
        {
            RefreshAll(s);
            if (!IsReadOnlyMode && _eventModal != null && _eventModal.activeSelf && s.ActiveEvent != null && s.Status == MissionStatus.InProgress)
                ShowIncomingEvent(s);
        }

        private void HandleEventTriggered(SessionSnapshot s)
        {
            RefreshAll(s);
            if (_deferInitialEvent || IsReadOnlyMode) return;
            if (!_playingFadeToBlack && !_transitionOverlay.activeSelf)
                ShowIncomingEvent(s);
        }

        private void HandleMissionEnded(SessionSnapshot s)
        {
            _playingFadeToBlack = false;
            _transitionOverlay.SetActive(false);
            _eventModal.SetActive(false);
            _confirmEndSolModal.SetActive(false);
            if (_helpModal != null) _helpModal.SetActive(false);
            if (!UseExternalEndingFlow)
                ShowMissionEnded(s);
            RefreshAll(s);
        }

        private void ResetClockForSol(int sol)
        {
            _solMinutesElapsed = 0f;
            _lastDisplayedMinute = -1;
            _cachedSolNumber = sol;
            if (_solBadgeText != null && _session?.CurrentSnapshot != null)
                _solBadgeText.text = Localization.Format("mc.sol_progress", sol, _session.CurrentSnapshot.TotalSols);
            if (_solClockText != null)
                _solClockText.text = "06:00";
        }

        private void MarkOnboardingStep1()
        {
            if (_onboardingDone) return;
            _onboardStep1Done = true;
            RefreshOnboardingUi();
        }

        private void HandlePowerSliderAdjusted()
        {
            if (_onboardingDone) return;
            _onboardStep2Done = true;
            RefreshOnboardingUi();
        }

        private void MarkOnboardingStep3Complete()
        {
            if (_onboardingDone) return;
            _onboardStep3Done = true;
            _onboardingDone = true;
            PlayerPrefs.SetInt(PrefOnboardingDone, 1);
            PlayerPrefs.Save();
            RefreshOnboardingUi();
        }

        private void RefreshAll(SessionSnapshot s)
        {
            if (!_built || s == null) return;

            // 1. Header Status Pill (Sol + AP)
            if (_solBadgeText != null)
                _solBadgeText.text = Localization.Format("mc.sol_progress", s.CurrentSol, s.TotalSols);
            if (_apHeaderText != null)
            {
                string apColor = s.ActionPoints > 0 ? "#F7C948" : "#7FA1B8";
                _apHeaderText.text = $"Action Points: <color={apColor}><b>{s.ActionPoints} / {s.MaxActionPoints}</b></color>";
            }

            // 2. Right Column Top: 5 Outpost Resources Rows
            UpdateSupplyTile(0, s.Power, s.NetPowerDelta, s.StatusLightOkThreshold, s.StatusLightLowThreshold, showRate: true);
            UpdateSupplyTile(1, s.Oxygen, s.NetOxygenDelta, s.StatusLightOkThreshold, s.StatusLightLowThreshold, showRate: true);
            UpdateSupplyTile(2, s.Water, s.NetWaterDelta, s.StatusLightOkThreshold, s.StatusLightLowThreshold, showRate: true);
            UpdateSupplyTile(3, s.Food, s.NetFoodDelta, s.StatusLightOkThreshold, s.StatusLightLowThreshold, showRate: true);
            UpdateSupplyTile(4, s.Shielding, 0f, 45f, 20f, showRate: false);

            // 3. Center Column: Mission Goal & 7-Sol Progress Strip
            _goalTitleText.text = s.SolsRemainingForRescue > 0
                ? Localization.Get("mc.goal_survive_headline") is string g && g != "mc.goal_survive_headline"
                    ? g
                    : "Survive until rescue."
                : Localization.Get("mc.goal_last_sol");
            for (int i = 0; i < 7; i++)
            {
                int solNum = i + 1;
                if (solNum < s.CurrentSol)
                {
                    _solTrackPips[i].color = theme.StatusOk;
                    _solTrackLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else if (solNum == s.CurrentSol)
                {
                    _solTrackPips[i].color = theme.TextAmber;
                    _solTrackLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else
                {
                    _solTrackPips[i].color = new Color(0.07f, 0.14f, 0.21f, 1f);
                    _solTrackLabels[i].color = theme.TextMuted;
                }
            }

            // 4. Center Middle: Outpost Alert / Next Step Card
            var eval = HintEngine.Evaluate(s);
            bool hasPendingEvent = s.ActiveEvent != null && !s.ActiveEvent.IsResolved;
            bool hazardCrit = s.AlertSeverity == 2 || s.SolarFlareActive;
            bool airlockLocked = s.SolarFlareActive || s.DustStormSolsRemaining > 0;
            bool showAlertStyle = hasPendingEvent || eval.IsUrgent || s.AlertSeverity >= 1 || airlockLocked;

            Color cardBg;
            Color cardBorder;
            Color accentCol;
            TerminalIconId cardIconId;
            if (hazardCrit)
            {
                cardBg = new Color(0.29f, 0.09f, 0.12f, 0.96f);
                cardBorder = theme.StatusCrit;
                accentCol = theme.StatusCrit;
                cardIconId = TerminalIconId.Hazard;
            }
            else if (showAlertStyle)
            {
                cardBg = new Color(0.25f, 0.17f, 0.04f, 0.96f);
                cardBorder = new Color(0.85f, 0.61f, 0.14f, 1f);
                accentCol = theme.StatusWarn;
                cardIconId = (!hasPendingEvent && s.AlertSeverity == 0 && airlockLocked) ? TerminalIconId.Lock : TerminalIconId.Warning;
            }
            else
            {
                cardBg = new Color(0.04f, 0.12f, 0.17f, 0.95f);
                cardBorder = new Color(0.16f, 0.44f, 0.50f, 1f);
                accentCol = theme.StatusOk;
                cardIconId = TerminalIconId.Scan;
            }

            TerminalUiBuilder.SetBorderColor(_nextStepCard, cardBorder);
            if (_nextStepInner != null) _nextStepInner.color = cardBg;
            if (_alertCardIcon != null)
            {
                _alertCardIcon.sprite = TerminalUiArt.GetIcon(cardIconId);
                _alertCardIcon.color = (cardIconId == TerminalIconId.Warning || cardIconId == TerminalIconId.Hazard)
                    ? Color.white
                    : accentCol;
            }
            if (_alertCardTitle != null)
            {
                _alertCardTitle.text = hazardCrit ? "CRITICAL ALERT"
                    : (showAlertStyle ? "OUTPOST ALERT" : "SYSTEMS NOMINAL");
                _alertCardTitle.color = accentCol;
            }

            string lockSuffix = airlockLocked ? " [EVA LOCKED]" : string.Empty;
            string bodyCopy = hasPendingEvent
                ? "An urgent outpost situation needs your decision."
                : $"{eval.NextStepText}";
            _nextStepBodyText.text = $"{bodyCopy}{lockSuffix}";
            _nextStepTargetApp = eval.TargetApp;
            _nextStepOpensEvent = !eval.TargetApp.HasValue && hasPendingEvent;

            if (_nextStepBtnLabel != null)
            {
                _nextStepBtnLabel.text = _nextStepOpensEvent ? "VIEW ALERT" : eval.NextStepButtonText.ToUpperInvariant();
                _nextStepBtnLabel.color = new Color(0.04f, 0.08f, 0.12f, 1f);
            }
            if (_nextStepBtnBg != null)
            {
                _nextStepBtnBg.color = hazardCrit ? theme.StatusCrit : (showAlertStyle ? theme.StatusWarn : new Color(0.14f, 0.88f, 0.78f, 1f));
                TerminalUiBuilder.SetBorderColor(_nextStepBtn.GetComponent<RectTransform>(), Color.Lerp(_nextStepBtnBg.color, Color.white, 0.28f));
            }

            // 5. Center Bottom: First Shift Checklist (Sol 1) / HELIOS Brief (Sol 2+)
            RefreshOnboardingUi(s.CurrentSol, eval.HeliosHintText);

            if (_hintsToggleLabel != null)
                _hintsToggleLabel.text = _hintsEnabled ? Localization.Get("helios.hints_on") : Localization.Get("helios.hints_off");

            // 6. Right Bottom: 4 Crew Status Rows
            for (int i = 0; i < 4 && i < s.Crew.Count; i++)
            {
                UpdateCrewCard(i, s.Crew[i]);
            }

            // 7. Footer Hint / Forecast & End Sol Button
            _forecastStripText.text = $"<color=#7FDFFF><b>Hint:</b></color> {s.ForecastSummaryLine}";
            if (!IsReadOnlyMode)
            {
                bool eventBlocksEndSol = !_deferInitialEvent && hasPendingEvent;
                _endSolBtn.gameObject.SetActive(true);
                _endSolBtn.interactable = s.Status == MissionStatus.InProgress && !eventBlocksEndSol;
                _endSolLabel.text = eventBlocksEndSol ? "RESOLVE EVENT" : "END SOL";
            }
            else
            {
                _endSolBtn.gameObject.SetActive(false);
            }

            // 8. Left Sidebar Unread Badges & Active Selection
            _iconBadges[(int)TerminalAppId.Comms].text = s.UnreadCommsCount > 0 ? $"[{s.UnreadCommsCount}]" : "";
            _iconBadges[(int)TerminalAppId.CommandersLog].text = s.UnreadLogCount > 0 ? $"[{s.UnreadLogCount}]" : "";

            _pulsingApp = s.GuidedHighlightApp;
            _windowManager.RefreshOpenWindows(s);
            RefreshTaskbarWindows();
        }

        private void UpdateSupplyTile(int idx, float value, float deltaPerSol, float okThreshold, float lowThreshold, bool showRate)
        {
            ref var tile = ref _supplyTiles[idx];
            if (tile.Panel == null) return;

            if (tile.HasValue)
            {
                float diff = value - tile.LastValue;
                if (Mathf.Abs(diff) >= 0.4f)
                {
                    tile.FlashTimer = 0.45f;
                    if (tile.FlashOverlay != null)
                        tile.FlashOverlay.color = diff > 0 ? new Color(theme.StatusOk.r, theme.StatusOk.g, theme.StatusOk.b, 0.20f)
                            : new Color(theme.StatusCrit.r, theme.StatusCrit.g, theme.StatusCrit.b, 0.22f);
                    if (tile.DeltaPopupText != null)
                    {
                        tile.PopupTimer = 0.85f;
                        tile.DeltaPopupText.color = diff > 0 ? theme.StatusOk : theme.StatusCrit;
                        tile.DeltaPopupText.text = $"{diff:+0;-0;0}%";
                        tile.DeltaPopupText.rectTransform.anchoredPosition = new Vector2(0f, 4f);
                        tile.DeltaPopupText.gameObject.SetActive(true);
                    }
                }
            }
            tile.LastValue = value;
            tile.HasValue = true;

            Color col;
            string word;
            if (value < lowThreshold)
            {
                col = theme.StatusCrit;
                word = Localization.Get("status.word.danger");
            }
            else if (value < okThreshold)
            {
                col = theme.StatusWarn;
                word = Localization.Get("status.word.low");
            }
            else
            {
                col = theme.StatusOk;
                word = Localization.Get("status.word.good");
            }

            tile.ValueText.text = $"{value:0}%";
            tile.StatusWordText.text = word;
            tile.StatusWordText.color = new Color(0.03f, 0.08f, 0.12f, 1f);
            tile.StatusBadgeBg.color = col;
            if (tile.BarAnimator != null)
                tile.BarAnimator.SetTarget(value / 100f, col);

            if (tile.Tooltip != null)
            {
                string rateNote = !showRate
                    ? Localization.Get("mc.steady")
                    : (Mathf.Abs(deltaPerSol) > 0.2f ? $"{deltaPerSol:+0.0;-0.0;0}{Localization.Get("mc.per_sol")}" : Localization.Get("mc.steady"));
                tile.Tooltip.SetRawText($"{tile.SystemLabel} ({value:0}%, {rateNote})", tile.GlossaryDef);
            }
        }

        private void UpdateCrewCard(int idx, CrewSnapshot c)
        {
            ref var card = ref _crewCards[idx];
            if (card.Panel == null) return;

            card.NameText.text = c.Name;
            card.RoleText.text = c.Role.ToString();

            Color stateCol = !c.IsAlive ? theme.StatusCrit
                : c.Health < 35f ? theme.StatusCrit
                : (c.IsFallenCommander || c.Health < 65f) ? theme.StatusWarn
                : theme.StatusOk;

            card.StatusTagText.text = c.StatusTag;
            card.StatusTagText.color = stateCol;
            card.HpText.text = $"{c.Health:0} HP";
            card.HpText.color = theme.TextPrimary;
            if (card.AvatarBoxBg != null)
                card.AvatarBoxBg.color = !c.IsAlive ? new Color(0.28f, 0.10f, 0.12f, 1f) : new Color(0.07f, 0.16f, 0.24f, 1f);
            if (card.Tooltip != null)
                card.Tooltip.SetRawText($"{c.Name} ({c.Role})", $"HP: {c.Health:0}  |  Morale: {c.Morale:0}  |  Radiation Dose: {c.DoseMsv:0.0} mSv");
        }

        private void RefreshOnboardingUi(int currentSol = 1, string heliosTip = null)
        {
            if (_onboardingCard == null || _nextStepCard == null) return;
            bool showChecklist = !_onboardingDone && currentSol == 1;
            if (_checklistGroup != null) _checklistGroup.SetActive(showChecklist);
            if (_heliosBubble != null) _heliosBubble.SetActive(!showChecklist && _hintsEnabled);

            if (showChecklist)
            {
                if (_bottomCardIcon != null)
                {
                    _bottomCardIcon.sprite = TerminalUiArt.GetIcon(TerminalIconId.Log);
                    _bottomCardIcon.color = new Color(0.498f, 0.875f, 1f, 1f);
                }
                if (_bottomCardTitle != null)
                {
                    _bottomCardTitle.text = Localization.Get("onboard.title").ToUpperInvariant();
                    _bottomCardTitle.color = new Color(0.498f, 0.875f, 1f, 1f);
                }
                bool[] states = { _onboardStep1Done, _onboardStep2Done, _onboardStep3Done };
                for (int i = 0; i < 3; i++)
                {
                    if (_onboardChecks[i] != null)
                    {
                        _onboardChecks[i].sprite = TerminalUiArt.GetIcon(states[i] ? TerminalIconId.Check : TerminalIconId.CheckBox);
                        _onboardChecks[i].color = states[i] ? theme.StatusOk : new Color(0.35f, 0.68f, 0.78f, 1f);
                    }
                    if (_onboardLabels[i] != null)
                        _onboardLabels[i].color = states[i] ? theme.StatusOk : theme.TextPrimary;
                }
            }
            else
            {
                if (_bottomCardIcon != null)
                {
                    _bottomCardIcon.sprite = TerminalUiArt.GetIcon(TerminalIconId.Help);
                    _bottomCardIcon.color = theme.AccentHelp;
                }
                if (_bottomCardTitle != null)
                {
                    _bottomCardTitle.text = _hintsEnabled ? "HELIOS OPERATIONS BRIEF" : "SYSTEMS SUMMARY";
                    _bottomCardTitle.color = theme.AccentHelp;
                }
                if (_heliosText != null)
                    _heliosText.text = !string.IsNullOrEmpty(heliosTip)
                        ? heliosTip
                        : (_session?.CurrentSnapshot != null ? HintEngine.Evaluate(_session.CurrentSnapshot).HeliosHintText : "");
            }
        }

        private void RefreshTaskbarWindows()
        {
            var focused = _windowManager != null ? _windowManager.FocusedApp : null;

            // Update Left Sidebar Active Highlight (Overview vs Focused App)
            bool overviewActive = !focused.HasValue;
            Color activeBg = new Color(0.08f, 0.36f, 0.37f, 0.96f);
            Color activeBorder = new Color(0.27f, 0.89f, 0.83f, 1f);
            Color idleBg = new Color(0.045f, 0.09f, 0.14f, 0.94f);
            Color idleBorder = new Color(0.14f, 0.24f, 0.33f, 1f);

            if (_overviewNavPanel != null)
            {
                TerminalUiBuilder.SetBorderColor(_overviewNavPanel, overviewActive ? activeBorder : idleBorder);
                if (_overviewNavInner != null) _overviewNavInner.color = overviewActive ? activeBg : idleBg;
                if (_overviewNavIcon != null) _overviewNavIcon.color = overviewActive ? Color.white : new Color(0.75f, 0.94f, 0.98f, 1f);
                if (_overviewNavLabel != null) _overviewNavLabel.color = overviewActive ? Color.white : new Color(0.84f, 0.92f, 0.97f, 1f);
            }

            for (int i = 0; i < 8; i++)
            {
                if (_iconPanels[i] == null) continue;
                bool isFocus = focused.HasValue && (int)focused.Value == i;
                bool isOpen = _windowManager != null && ContainsOpenOrder((TerminalAppId)i);
                Color bCol = isFocus ? activeBorder : (isOpen ? new Color(0.24f, 0.56f, 0.62f, 1f) : idleBorder);
                if (!(_pulsingApp.HasValue && (int)_pulsingApp.Value == i && !isFocus))
                    TerminalUiBuilder.SetBorderColor(_iconPanels[i], bCol);
                if (_iconInners[i] != null)
                    _iconInners[i].color = isFocus ? activeBg : (isOpen ? new Color(0.06f, 0.15f, 0.22f, 0.96f) : idleBg);
                if (_iconImgs[i] != null)
                    _iconImgs[i].color = isFocus ? Color.white : new Color(0.75f, 0.94f, 0.98f, 1f);
                if (_iconLabels[i] != null)
                    _iconLabels[i].color = isFocus ? Color.white : new Color(0.84f, 0.92f, 0.97f, 1f);
            }
        }

        private bool ContainsOpenOrder(TerminalAppId appId)
        {
            var order = _windowManager.OpenOrder;
            for (int i = 0; i < order.Count; i++)
                if (order[i] == appId) return true;
            return false;
        }

        private void ShowIncomingEvent(SessionSnapshot s)
        {
            if (s.ActiveEvent == null || IsReadOnlyMode) return;
            _eventModal.SetActive(true);
            var ev = s.ActiveEvent;

            ApplyEventIllustrationTheme(ev.Id);
            _eventTitleText.text = ev.Title;
            _eventDescText.text = ev.Description;

            if (!ev.IsResolved)
            {
                _eventResultBox.SetActive(false);
                _eventContinueBtn.gameObject.SetActive(false);
                for (int i = 0; i < 3; i++)
                {
                    if (i < ev.Choices.Count)
                    {
                        var ch = ev.Choices[i];
                        _eventOptionBtns[i].gameObject.SetActive(true);
                        _eventOptionBtns[i].interactable = ch.CanSelect;
                        SplitChoiceAndChips(ch.Label, out string cleanTitle, out string chipsText);
                        _eventOptionLabels[i].text = cleanTitle;
                        _eventOptionChips[i].text = ch.CanSelect ? chipsText : $"{chipsText}  [{Localization.Format("event.need_ap", ch.ApCost)}]";
                        _eventOptionChips[i].color = ch.CanSelect ? theme.TextAmber : theme.StatusCrit;
                    }
                    else
                    {
                        _eventOptionBtns[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++) _eventOptionBtns[i].gameObject.SetActive(false);
                _eventResultBox.SetActive(true);
                _eventContinueBtn.gameObject.SetActive(true);
                _eventResultText.text = ev.OutcomeText;
                _eventFactText.text = $"{Localization.Get("event.fact_heading")} [{ev.SourceId}]: {ev.FactText}";
            }
        }

        private void ApplyEventIllustrationTheme(string eventId)
        {
            TerminalIconId iconId = TerminalIconId.Warning;
            Color tint = theme.StatusWarn;
            string sub = Localization.Get("modal.incoming_event");

            if (!string.IsNullOrEmpty(eventId))
            {
                if (eventId.Contains("flare") || eventId.Contains("meteor") || eventId.Contains("blight")) { iconId = TerminalIconId.Hazard; tint = theme.StatusCrit; }
                else if (eventId.Contains("cold") || eventId.Contains("storm") || eventId.Contains("dust")) { iconId = TerminalIconId.Warning; tint = theme.StatusWarn; }
                else if (eventId.Contains("scrubber") || eventId.Contains("water")) { iconId = TerminalIconId.Water; tint = theme.AccentLifeSupport; }
                else if (eventId.Contains("greenhouse")) { iconId = TerminalIconId.Food; tint = theme.AccentGreenhouse; }
                else if (eventId.Contains("tension")) { iconId = TerminalIconId.PlusBox; tint = theme.AccentCrew; }
                else if (eventId.Contains("message")) { iconId = TerminalIconId.Scan; tint = theme.AccentComms; }
            }

            if (_eventBannerBg != null) _eventBannerBg.color = Color.Lerp(theme.PanelBg, tint, 0.24f);
            if (_eventBannerIcon != null)
            {
                _eventBannerIcon.sprite = TerminalUiArt.GetIcon(iconId);
                _eventBannerIcon.color = Color.white;
            }
            if (_eventBannerSubtitle != null)
            {
                _eventBannerSubtitle.text = sub;
                _eventBannerSubtitle.color = tint;
            }
        }

        private static void SplitChoiceAndChips(string rawLabel, out string cleanTitle, out string chipsText)
        {
            int openParen = rawLabel.LastIndexOf('(');
            int closeParen = rawLabel.LastIndexOf(')');
            if (openParen > 0 && closeParen > openParen)
            {
                cleanTitle = rawLabel.Substring(0, openParen).Trim();
                string rawChips = rawLabel.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                chipsText = rawChips.Replace(", ", "  |  ");
            }
            else
            {
                cleanTitle = rawLabel;
                chipsText = "";
            }
        }

        private void BeginSolTransition(SessionSnapshot s)
        {
            _playingFadeToBlack = true;
            _fadeTimer = 0f;
            _transitionOverlay.SetActive(true);
            _transitionGroup.alpha = 0f;
            _transitionCard.gameObject.SetActive(false);

            var sum = s.LastSolSummary;
            _transitionTitleText.text = Localization.Format("summary.headline", sum.CompletedSol);
            _transitionBullets[0].text = Localization.Format("summary.bullet_supplies", sum.PowerDelta, sum.OxygenDelta, sum.WaterDelta, sum.FoodDelta);
            _transitionBullets[1].text = s.DustStormSolsRemaining > 0
                ? Localization.Format("summary.bullet_weather_storm", s.DustStormSolsRemaining)
                : Localization.Format("summary.bullet_weather_clear", s.PanelDustPct);
            _transitionBullets[2].text = Localization.Format("summary.bullet_crew", s.LivingCrewCount, s.SolsRemainingForRescue);
        }

        private void OnTransitionContinueClicked()
        {
            OnUiSoundRequested?.Invoke();
            _transitionOverlay.SetActive(false);
            var s = _session?.CurrentSnapshot;
            if (!IsReadOnlyMode && s != null && s.ActiveEvent != null && !s.ActiveEvent.IsResolved && s.Status == MissionStatus.InProgress)
                ShowIncomingEvent(s);
        }

        private void OnEndSolClicked()
        {
            if (IsReadOnlyMode || _session?.CurrentSnapshot == null) return;
            OnUiSoundRequested?.Invoke();
            var s = _session.CurrentSnapshot;
            if (s.Status != MissionStatus.InProgress) return;
            if (!_deferInitialEvent && s.ActiveEvent != null && !s.ActiveEvent.IsResolved)
            {
                ShowIncomingEvent(s);
                return;
            }

            if (s.NeedsEndSolConfirm)
            {
                _confirmEndSolModal.SetActive(true);
                _confirmBodyText.text = Localization.Format("modal.confirm_endsol.body", s.EndSolDangerSummary);
                return;
            }

            MarkOnboardingStep3Complete();
            _session.EndSol();
        }

        private void ShowMissionEnded(SessionSnapshot s)
        {
            _missionEndOverlay.SetActive(true);
            bool won = s.Status == MissionStatus.Won;
            _missionEndTitle.color = won ? theme.StatusOk : theme.StatusCrit;
            _missionEndTitle.text = won ? Localization.Get("mission.won.title") : Localization.Get("mission.lost.title");

            _sb.Clear();
            _sb.AppendLine(Localization.Format("mission.end.score_line", s.FinalScore, s.Grade, s.LivingCrewCount));
            _sb.AppendLine();
            for (int i = 0; i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                _sb.AppendLine($"  {c.Name} ({c.Role}) — HP {c.Health:0}, Morale {c.Morale:0}, Dose {c.DoseMsv:0.0} mSv [{c.StatusTag}]");
            }
            _sb.AppendLine();
            for (int i = 0; i < s.DebriefCards.Count; i++)
            {
                var d = s.DebriefCards[i];
                _sb.AppendLine($"Sol {d.Sol}: {Localization.Get(d.TitleKey)} -> {Localization.Get(d.FactKey)} [{d.SourceId}]");
            }
            _missionEndBody.text = _sb.ToString();
        }

        private void ShowTooltip(string title, string body, Vector2 screenPos)
        {
            if (_tooltipPanel == null || terminalRoot == null) return;
            _tooltipTitle.text = title ?? "";
            _tooltipBody.text = body ?? "";
            _tooltipPanel.SetActive(true);
            _tooltipPanel.transform.SetAsLastSibling();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(terminalRoot, screenPos, null, out var localPt))
            {
                Vector2 half = terminalRoot.rect.size * 0.5f;
                Vector2 size = _tooltipRect.sizeDelta;
                float clampedX = Mathf.Clamp(localPt.x + 18f, -half.x + 16f, half.x - size.x - 16f);
                float clampedY = Mathf.Clamp(localPt.y + 18f, -half.y + size.y + 16f, half.y - 16f);
                _tooltipRect.anchoredPosition = new Vector2(clampedX, clampedY);
            }
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.SetActive(false);
        }

        // --- UI CONSTRUCTION ---

        private void BuildTerminalUI()
        {
            if (terminalRoot == null)
                terminalRoot = GetComponent<RectTransform>();

            _textScaler = terminalRoot.GetComponent<AccessibilityTextScaler>() ?? terminalRoot.gameObject.AddComponent<AccessibilityTextScaler>();
            _windowManager = terminalRoot.GetComponent<WindowManager>() ?? terminalRoot.gameObject.AddComponent<WindowManager>();
            _windowManager.OnWindowLayoutChanged += RefreshTaskbarWindows;
            _windowManager.OnWindowClosed += HandleWindowClosed;

            TerminalUiBuilder.OnShowTooltipRequested += ShowTooltip;
            TerminalUiBuilder.OnHideTooltipRequested += HideTooltip;
            Apps.PowerGridAppWindow.OnPlayerAdjustedPower += HandlePowerSliderAdjusted;

            // 0. Rugged Tablet Hardware Chassis Bezel + Glass Screen Surface
            var bezelPanel = TerminalUiBuilder.CreateFlatPanel("TabletBezel", terminalRoot, new Color(0.085f, 0.105f, 0.135f, 1f), new Color(0.20f, 0.25f, 0.31f, 1f), 4f);
            TerminalUiBuilder.SetStretch(bezelPanel);
            var bezelInner = TerminalUiBuilder.GetInner(bezelPanel);

            // Bezel Hardware Details (top center amber LED, bottom left dual LEDs, bottom stamp)
            var topLed = TerminalUiBuilder.CreateRect("BezelTopLed", bezelInner);
            topLed.anchorMin = new Vector2(0.5f, 1f);
            topLed.anchorMax = new Vector2(0.5f, 1f);
            topLed.offsetMin = new Vector2(-42f, -15f);
            topLed.offsetMax = new Vector2(42f, -9f);
            var topLedImg = topLed.gameObject.AddComponent<Image>();
            topLedImg.sprite = TerminalUiArt.GetRoundedRect8px();
            topLedImg.type = Image.Type.Sliced;
            topLedImg.color = new Color(1.00f, 0.65f, 0.18f, 1f);
            topLedImg.raycastTarget = false;

            for (int l = 0; l < 2; l++)
            {
                var led = TerminalUiBuilder.CreateRect($"BezelBotLed_{l}", bezelInner);
                led.anchorMin = new Vector2(0f, 0f);
                led.anchorMax = new Vector2(0f, 0f);
                led.offsetMin = new Vector2(128f + l * 30f, 8f);
                led.offsetMax = new Vector2(150f + l * 30f, 14f);
                var lImg = led.gameObject.AddComponent<Image>();
                lImg.sprite = TerminalUiArt.GetRoundedRect8px();
                lImg.type = Image.Type.Sliced;
                lImg.color = new Color(1.00f, 0.62f, 0.16f, 0.95f);
                lImg.raycastTarget = false;
            }

            var bezelStamp = TerminalUiBuilder.CreateCompactDebugText("BezelStamp", bezelInner, theme, 14f, new Color(0.45f, 0.54f, 0.62f, 0.9f), TextAlignmentOptions.Center, _textScaler);
            bezelStamp.fontStyle = FontStyles.Bold;
            bezelStamp.text = "///   MARS OUTPOST - OPS TERM v2.1   ///";
            bezelStamp.raycastTarget = false;
            bezelStamp.rectTransform.anchorMin = new Vector2(0.25f, 0f);
            bezelStamp.rectTransform.anchorMax = new Vector2(0.75f, 0f);
            bezelStamp.rectTransform.offsetMin = new Vector2(0f, 2f);
            bezelStamp.rectTransform.offsetMax = new Vector2(0f, 22f);

            var screenPanel = TerminalUiBuilder.CreateFlatPanel("ScreenSurface", bezelInner, new Color(0.028f, 0.062f, 0.095f, 1f), new Color(0.14f, 0.25f, 0.34f, 1f), 2f);
            screenPanel.anchorMin = Vector2.zero;
            screenPanel.anchorMax = Vector2.one;
            screenPanel.offsetMin = new Vector2(22f, 26f);
            screenPanel.offsetMax = new Vector2(-22f, -22f);
            var screen = TerminalUiBuilder.GetInner(screenPanel);

            // Subtle Mars horizon ambient texture inside glass screen
            var wpRect = TerminalUiBuilder.CreateRect("Wallpaper", screen);
            TerminalUiBuilder.SetStretch(wpRect);
            var wpImg = wpRect.gameObject.AddComponent<Image>();
            wpImg.sprite = TerminalUiArt.GetMarsWallpaperSprite(theme);
            wpImg.color = new Color(0.30f, 0.52f, 0.65f, 0.10f);
            wpImg.raycastTarget = false;

            // 1. Top Header Bar (Emblem + OUTPOST TERMINAL + Sol / Clock / Action Points Pill)
            var topHeader = TerminalUiBuilder.CreateRect("TopHeaderBar", screen);
            topHeader.anchorMin = new Vector2(0f, 1f);
            topHeader.anchorMax = new Vector2(1f, 1f);
            topHeader.offsetMin = new Vector2(18f, -84f);
            topHeader.offsetMax = new Vector2(-18f, -8f);

            var emblemRect = TerminalUiBuilder.CreateRect("AgencyBadge", topHeader);
            emblemRect.anchorMin = new Vector2(0f, 0.5f);
            emblemRect.anchorMax = new Vector2(0f, 0.5f);
            emblemRect.offsetMin = new Vector2(4f, -26f);
            emblemRect.offsetMax = new Vector2(56f, 26f);
            var emblemImg = emblemRect.gameObject.AddComponent<Image>();
            emblemImg.sprite = TerminalUiArt.GetNasaBadgeSprite();
            emblemImg.preserveAspect = true;
            emblemImg.raycastTarget = false;

            var emblemDiv = TerminalUiBuilder.CreateRect("EmblemDivider", topHeader);
            emblemDiv.anchorMin = new Vector2(0f, 0.16f);
            emblemDiv.anchorMax = new Vector2(0f, 0.84f);
            emblemDiv.offsetMin = new Vector2(72f, 0f);
            emblemDiv.offsetMax = new Vector2(74f, 0f);
            emblemDiv.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.30f, 0.40f, 1f);

            var termTitle = TerminalUiBuilder.CreateText("TerminalBrandTitle", topHeader, theme, 34f, new Color(0.95f, 0.98f, 1f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            termTitle.fontStyle = FontStyles.Bold;
            termTitle.textWrappingMode = TextWrappingModes.NoWrap;
            termTitle.text = "OUTPOST TERMINAL";
            termTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            termTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            termTitle.rectTransform.offsetMin = new Vector2(90f, 0f);
            termTitle.rectTransform.offsetMax = new Vector2(510f, 0f);

            var termSub = TerminalUiBuilder.CreateCompactDebugText("TerminalBrandSub", topHeader, theme, 15f, new Color(0.56f, 0.72f, 0.83f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            termSub.fontStyle = FontStyles.Bold;
            termSub.text = "MARS OUTPOST\nSURVIVAL MANAGEMENT";
            termSub.rectTransform.anchorMin = new Vector2(0f, 0f);
            termSub.rectTransform.anchorMax = new Vector2(0f, 1f);
            termSub.rectTransform.offsetMin = new Vector2(520f, 2f);
            termSub.rectTransform.offsetMax = new Vector2(735f, -2f);

            var slashTicks = TerminalUiBuilder.CreateCompactDebugText("SlashTicks", topHeader, theme, 24f, new Color(0.19f, 0.31f, 0.40f, 0.9f), TextAlignmentOptions.Center, _textScaler);
            slashTicks.fontStyle = FontStyles.Bold;
            slashTicks.text = "/////";
            slashTicks.rectTransform.anchorMin = new Vector2(0f, 0f);
            slashTicks.rectTransform.anchorMax = new Vector2(0f, 1f);
            slashTicks.rectTransform.offsetMin = new Vector2(738f, 0f);
            slashTicks.rectTransform.offsetMax = new Vector2(810f, 0f);

            // Top Right Status Pill: [Cal] Sol X of 7 | [Clock] 06:00 | Action Points: 3 / 3
            var statusPill = TerminalUiBuilder.CreateFlatPanel("TopStatusPill", topHeader, new Color(0.048f, 0.098f, 0.145f, 0.96f), new Color(0.18f, 0.31f, 0.42f, 1f), 2f);
            statusPill.anchorMin = new Vector2(0.475f, 0.12f);
            statusPill.anchorMax = new Vector2(1f, 0.88f);
            statusPill.offsetMin = new Vector2(20f, 0f);
            statusPill.offsetMax = Vector2.zero;
            var pillInner = TerminalUiBuilder.GetInner(statusPill);

            var calIcon = TerminalUiBuilder.CreateIcon("CalIcon", pillInner, TerminalIconId.Calendar, new Color(0.76f, 0.90f, 0.98f, 1f));
            TerminalUiBuilder.SetAnchors(calIcon.rectTransform, 0.02f, 0.22f, 0.068f, 0.78f);

            _solBadgeText = TerminalUiBuilder.CreateText("SolBadgeText", pillInner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            _solBadgeText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_solBadgeText.rectTransform, 0.075f, 0f, 0.30f, 1f);

            var div1 = TerminalUiBuilder.CreateRect("PillDiv1", pillInner);
            TerminalUiBuilder.SetAnchors(div1, 0.305f, 0.18f, 0.308f, 0.82f);
            div1.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.31f, 0.42f, 1f);

            var clkIcon = TerminalUiBuilder.CreateIcon("ClockIcon", pillInner, TerminalIconId.Clock, new Color(0.76f, 0.90f, 0.98f, 1f));
            TerminalUiBuilder.SetAnchors(clkIcon.rectTransform, 0.325f, 0.22f, 0.372f, 0.78f);

            _solClockText = TerminalUiBuilder.CreateText("SolClock", pillInner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            _solClockText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_solClockText.rectTransform, 0.382f, 0f, 0.56f, 1f);

            var div2 = TerminalUiBuilder.CreateRect("PillDiv2", pillInner);
            TerminalUiBuilder.SetAnchors(div2, 0.565f, 0.18f, 0.568f, 0.82f);
            div2.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.31f, 0.42f, 1f);

            _apHeaderText = TerminalUiBuilder.CreateText("ApHeader", pillInner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
            _apHeaderText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_apHeaderText.rectTransform, 0.575f, 0f, 0.98f, 1f);
            TerminalUiBuilder.AttachTooltip(_apHeaderText, "gloss.ap.term", "gloss.ap.def");

            // Thin horizontal divider line below header
            var hdrLine = TerminalUiBuilder.CreateRect("HeaderBottomLine", screen);
            hdrLine.anchorMin = new Vector2(0f, 1f);
            hdrLine.anchorMax = new Vector2(1f, 1f);
            hdrLine.offsetMin = new Vector2(18f, -90f);
            hdrLine.offsetMax = new Vector2(-18f, -88f);
            hdrLine.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.22f, 0.31f, 1f);

            // Read-Only Banner (shown only on Corridor/Airlock Wall Tablets)
            var roPanel = TerminalUiBuilder.CreateFlatPanel("ReadOnlyBanner", screen, new Color(0.10f, 0.22f, 0.33f, 0.98f), theme.AccentSources, 2f, true);
            roPanel.anchorMin = new Vector2(0.24f, 1f);
            roPanel.anchorMax = new Vector2(0.76f, 1f);
            roPanel.offsetMin = new Vector2(0f, -128f);
            roPanel.offsetMax = new Vector2(0f, -92f);
            _readOnlyBanner = roPanel.gameObject;
            _readOnlyBannerText = TerminalUiBuilder.CreateText("ReadOnlyText", TerminalUiBuilder.GetInner(roPanel), theme, 24f, theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            _readOnlyBannerText.fontStyle = FontStyles.Bold;
            _readOnlyBannerText.text = Localization.Get("mc.readonly_banner");
            TerminalUiBuilder.SetStretch(_readOnlyBannerText.rectTransform, 4f);
            _readOnlyBanner.SetActive(false);

            // 2. Left Navigation Sidebar (Overview + 8 Apps)
            var leftSidebar = TerminalUiBuilder.CreateRect("DesktopAppDock", screen);
            leftSidebar.anchorMin = new Vector2(0f, 0f);
            leftSidebar.anchorMax = new Vector2(0.205f, 1f);
            leftSidebar.offsetMin = new Vector2(18f, 88f);
            leftSidebar.offsetMax = new Vector2(-6f, -98f);
            BuildDesktopAppDock(leftSidebar);

            // 3. Main Content Container (Center Overview + Right Status Cards)
            var contentArea = TerminalUiBuilder.CreateRect("MainContentArea", screen);
            contentArea.anchorMin = new Vector2(0.205f, 0f);
            contentArea.anchorMax = new Vector2(1f, 1f);
            contentArea.offsetMin = new Vector2(10f, 88f);
            contentArea.offsetMax = new Vector2(-18f, -98f);

            // 3A. Center Column: OVERVIEW (Mission Goal + Outpost Alert + First Shift Checklist / HELIOS)
            var centerCol = TerminalUiBuilder.CreateRect("CenterHomeCol", contentArea);
            TerminalUiBuilder.SetAnchors(centerCol, 0f, 0f, 0.535f, 1f);
            BuildCenterHomeColumn(centerCol);

            // 3B. Right Column: OUTPOST RESOURCES + CREW STATUS
            var rightCol = TerminalUiBuilder.CreateRect("RightStatusCol", contentArea);
            TerminalUiBuilder.SetAnchors(rightCol, 0.550f, 0f, 1f, 1f);
            var resourcesBox = TerminalUiBuilder.CreateRect("LeftSuppliesCol", rightCol);
            TerminalUiBuilder.SetAnchors(resourcesBox, 0f, 0.495f, 1f, 1f);
            BuildSupplyTilesColumn(resourcesBox);

            var crewBox = TerminalUiBuilder.CreateRect("RightCrewCol", rightCol);
            TerminalUiBuilder.SetAnchors(crewBox, 0f, 0f, 1f, 0.475f);
            BuildCrewCardsColumn(crewBox);

            // 4. Floating Window Layer (cleanly sits over MainContentArea beside Left Sidebar)
            var windowLayer = TerminalUiBuilder.CreateRect("WindowLayer", screen);
            windowLayer.anchorMin = new Vector2(0.205f, 0f);
            windowLayer.anchorMax = new Vector2(1f, 1f);
            windowLayer.offsetMin = new Vector2(10f, 88f);
            windowLayer.offsetMax = new Vector2(-18f, -98f);
            CreateAllAppWindows(windowLayer);

            // 5. Bottom Footer Strip Divider + Controls (Hint, Hints/Help, Leave Terminal, END SOL)
            var footerLine = TerminalUiBuilder.CreateRect("FooterTopLine", screen);
            footerLine.anchorMin = new Vector2(0f, 0f);
            footerLine.anchorMax = new Vector2(1f, 0f);
            footerLine.offsetMin = new Vector2(18f, 80f);
            footerLine.offsetMax = new Vector2(-18f, 82f);
            footerLine.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.22f, 0.31f, 1f);

            var taskbar = TerminalUiBuilder.CreateRect("Taskbar", screen);
            taskbar.anchorMin = new Vector2(0f, 0f);
            taskbar.anchorMax = new Vector2(1f, 0f);
            taskbar.offsetMin = new Vector2(18f, 8f);
            taskbar.offsetMax = new Vector2(-18f, 76f);
            BuildTaskbar(taskbar);

            // 6. Modals, Help, Tooltip, Debug & Toast
            BuildEventModal();
            BuildConfirmEndSolModal();
            BuildTransitionOverlay();
            BuildHelpModal();
            BuildMissionEndOverlay();
            BuildTooltipPopup();
            BuildDebugPanel();
            BuildToastBanner();
        }

        private void BuildSupplyTilesColumn(RectTransform parent)
        {
            var panel = TerminalUiBuilder.CreateFlatPanel("OutpostResourcesPanel", parent, new Color(0.038f, 0.082f, 0.125f, 0.95f), new Color(0.14f, 0.26f, 0.35f, 1f), 2f, true);
            TerminalUiBuilder.SetStretch(panel);
            var pInner = TerminalUiBuilder.GetInner(panel);

            var hdrIcon = TerminalUiBuilder.CreateIcon("ResourcesHdrIcon", pInner, TerminalIconId.Gauge, new Color(0.35f, 0.85f, 0.88f, 1f));
            TerminalUiBuilder.SetAnchors(hdrIcon.rectTransform, 0.028f, 0.855f, 0.085f, 0.965f);

            var hdr = TerminalUiBuilder.CreateText("SuppliesHdr", pInner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            hdr.fontStyle = FontStyles.Bold;
            hdr.text = "OUTPOST RESOURCES";
            TerminalUiBuilder.SetAnchors(hdr.rectTransform, 0.095f, 0.84f, 0.97f, 0.985f);

            var hdrDiv = TerminalUiBuilder.CreateRect("ResourcesHdrDiv", pInner);
            TerminalUiBuilder.SetAnchors(hdrDiv, 0.025f, 0.83f, 0.975f, 0.836f);
            hdrDiv.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.23f, 0.32f, 1f);

            (string label, TerminalIconId icon, Color accent, TerminalAppId targetApp, string glossTerm, string glossDef)[] defs =
            {
                ("Battery", TerminalIconId.Bolt, new Color(0.32f, 0.90f, 0.84f, 1f), TerminalAppId.PowerGrid, "gloss.power.term", "gloss.power.def"),
                ("Oxygen",  TerminalIconId.Oxygen, new Color(0.45f, 0.88f, 0.96f, 1f), TerminalAppId.LifeSupport, "gloss.o2.term", "gloss.o2.def"),
                ("Water",   TerminalIconId.Water, new Color(0.42f, 0.86f, 1.00f, 1f), TerminalAppId.LifeSupport, "gloss.water.term", "gloss.water.def"),
                ("Food",    TerminalIconId.Food, new Color(0.32f, 0.89f, 0.82f, 1f), TerminalAppId.Greenhouse, "gloss.food.term", "gloss.food.def"),
                ("Shield",  TerminalIconId.Shield, new Color(0.45f, 0.88f, 0.94f, 1f), TerminalAppId.Radiation, "gloss.shield.term", "gloss.shield.def")
            };

            for (int i = 0; i < 5; i++)
            {
                var def = defs[i];
                float top = 0.805f - i * 0.158f;
                float bot = top - 0.146f;

                var row = TerminalUiBuilder.CreateRect($"SupplyTile_{i}", pInner);
                TerminalUiBuilder.SetAnchors(row, 0.02f, bot, 0.98f, top);
                var rowBg = row.gameObject.AddComponent<Image>();
                rowBg.sprite = TerminalUiArt.GetRoundedRect8px();
                rowBg.type = Image.Type.Sliced;
                rowBg.color = new Color(0.055f, 0.11f, 0.16f, 0.45f);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                var cols = btn.colors;
                cols.normalColor = new Color(1f, 1f, 1f, 0.65f);
                cols.highlightedColor = new Color(1.5f, 1.8f, 2.0f, 1f);
                cols.pressedColor = new Color(0.85f, 0.95f, 1f, 1f);
                btn.colors = cols;
                btn.onClick.AddListener(() =>
                {
                    OnUiSoundRequested?.Invoke();
                    OpenApp(def.targetApp);
                });
                row.gameObject.AddComponent<ButtonHoverFeedback>();
                var tt = TerminalUiBuilder.AttachTooltip(row, def.glossTerm, def.glossDef);

                var flashRect = TerminalUiBuilder.CreateRect("Flash", row);
                TerminalUiBuilder.SetStretch(flashRect);
                var flashImg = flashRect.gameObject.AddComponent<Image>();
                flashImg.sprite = TerminalUiArt.GetRoundedRect8px();
                flashImg.type = Image.Type.Sliced;
                flashImg.color = new Color(1f, 1f, 1f, 0f);
                flashImg.raycastTarget = false;

                var icon = TerminalUiBuilder.CreateIcon("Icon", row, def.icon, def.accent);
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.015f, 0.18f, 0.075f, 0.82f);

                var title = TerminalUiBuilder.CreateText("Title", row, theme, 24f, new Color(0.88f, 0.95f, 0.98f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
                title.fontStyle = FontStyles.Bold;
                title.text = def.label;
                TerminalUiBuilder.SetAnchors(title.rectTransform, 0.09f, 0f, 0.30f, 1f);

                // Smooth progress bar track
                var trackRect = TerminalUiBuilder.CreateFlatPanel("Track", row, new Color(0.024f, 0.052f, 0.085f, 1f), new Color(0.11f, 0.22f, 0.30f, 1f), 1f);
                TerminalUiBuilder.SetAnchors(trackRect, 0.31f, 0.32f, 0.69f, 0.68f);
                var tInner = TerminalUiBuilder.GetInner(trackRect);
                var fillRect = TerminalUiBuilder.CreateRect("Fill", tInner);
                var fillImg = fillRect.gameObject.AddComponent<Image>();
                fillImg.sprite = TerminalUiArt.GetRoundedRect8px();
                fillImg.type = Image.Type.Sliced;
                fillImg.color = theme.StatusOk;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                var anim = fillRect.gameObject.AddComponent<SmoothBarFill>();

                var val = TerminalUiBuilder.CreateText("Val", row, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
                val.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(val.rectTransform, 0.695f, 0f, 0.815f, 1f);

                var badgeRect = TerminalUiBuilder.CreateRect("StatusBadge", row);
                TerminalUiBuilder.SetAnchors(badgeRect, 0.83f, 0.16f, 0.985f, 0.84f);
                var badgeBg = badgeRect.gameObject.AddComponent<Image>();
                badgeBg.sprite = TerminalUiArt.GetRoundedRect8px();
                badgeBg.type = Image.Type.Sliced;
                badgeBg.color = theme.StatusOk;

                var statusWord = TerminalUiBuilder.CreateCompactDebugText("StatusWord", badgeRect, theme, 20f, new Color(0.03f, 0.08f, 0.12f, 1f), TextAlignmentOptions.Center, _textScaler);
                statusWord.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetStretch(statusWord.rectTransform);

                var popupTxt = TerminalUiBuilder.CreateCompactDebugText("DeltaPopup", row, theme, 20f, theme.StatusOk, TextAlignmentOptions.Center, _textScaler);
                popupTxt.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(popupTxt.rectTransform, 0.56f, 0f, 0.72f, 1f);
                popupTxt.gameObject.SetActive(false);

                _supplyTiles[i] = new SupplyTileUi
                {
                    Panel = row,
                    InnerImg = rowBg,
                    FlashOverlay = flashImg,
                    ValueText = val,
                    StatusWordText = statusWord,
                    StatusBadgeBg = badgeBg,
                    FillImage = fillImg,
                    BarAnimator = anim,
                    DeltaPopupText = popupTxt,
                    Tooltip = tt,
                    SystemLabel = def.label,
                    GlossaryDef = Localization.Get(def.glossDef)
                };
            }
        }

        private void BuildCrewCardsColumn(RectTransform parent)
        {
            var panel = TerminalUiBuilder.CreateFlatPanel("CrewStatusPanel", parent, new Color(0.038f, 0.082f, 0.125f, 0.95f), new Color(0.14f, 0.26f, 0.35f, 1f), 2f, true);
            TerminalUiBuilder.SetStretch(panel);
            var pInner = TerminalUiBuilder.GetInner(panel);

            var hdrIcon = TerminalUiBuilder.CreateIcon("CrewHdrIcon", pInner, TerminalIconId.Crew, new Color(0.35f, 0.85f, 0.88f, 1f));
            TerminalUiBuilder.SetAnchors(hdrIcon.rectTransform, 0.028f, 0.85f, 0.085f, 0.965f);

            var hdr = TerminalUiBuilder.CreateText("CrewHdr", pInner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            hdr.fontStyle = FontStyles.Bold;
            hdr.text = "CREW STATUS";
            TerminalUiBuilder.SetAnchors(hdr.rectTransform, 0.095f, 0.835f, 0.97f, 0.985f);

            var hdrDiv = TerminalUiBuilder.CreateRect("CrewHdrDiv", pInner);
            TerminalUiBuilder.SetAnchors(hdrDiv, 0.025f, 0.825f, 0.975f, 0.831f);
            hdrDiv.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.23f, 0.32f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float top = 0.805f - i * 0.195f;
                float bot = top - 0.184f;

                var row = TerminalUiBuilder.CreateRect($"CrewCard_{i}", pInner);
                TerminalUiBuilder.SetAnchors(row, 0.02f, bot, 0.98f, top);
                var rowBg = row.gameObject.AddComponent<Image>();
                rowBg.color = new Color(0.055f, 0.11f, 0.16f, 0.35f);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                var cols = btn.colors;
                cols.normalColor = new Color(1f, 1f, 1f, 0.55f);
                cols.highlightedColor = new Color(1.5f, 1.8f, 2.0f, 1f);
                btn.colors = cols;
                btn.onClick.AddListener(() =>
                {
                    OnUiSoundRequested?.Invoke();
                    OpenApp(TerminalAppId.Crew);
                });
                row.gameObject.AddComponent<ButtonHoverFeedback>();
                var tt = row.gameObject.AddComponent<TooltipTrigger>();

                var avatarBox = TerminalUiBuilder.CreateFlatPanel("AvatarBox", row, new Color(0.07f, 0.16f, 0.24f, 1f), new Color(0.28f, 0.65f, 0.72f, 1f), 1.5f);
                TerminalUiBuilder.SetAnchors(avatarBox, 0.015f, 0.14f, 0.105f, 0.86f);
                var avatarInner = TerminalUiBuilder.GetInner(avatarBox);
                var avatarIcon = TerminalUiBuilder.CreateIcon("PlusIcon", avatarInner, TerminalIconId.PlusBox, new Color(0.55f, 0.90f, 1f, 1f));
                TerminalUiBuilder.SetStretch(avatarIcon.rectTransform, 4f);

                var nameTxt = TerminalUiBuilder.CreateText("Name", row, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
                nameTxt.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(nameTxt.rectTransform, 0.13f, 0.42f, 0.58f, 0.96f);

                var roleTxt = TerminalUiBuilder.CreateCompactDebugText("Role", row, theme, 18f, new Color(0.56f, 0.72f, 0.83f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
                TerminalUiBuilder.SetAnchors(roleTxt.rectTransform, 0.13f, 0.04f, 0.58f, 0.48f);

                var statusTxt = TerminalUiBuilder.CreateText("StatusTag", row, theme, 24f, theme.StatusOk, TextAlignmentOptions.Center, _textScaler);
                statusTxt.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(statusTxt.rectTransform, 0.58f, 0f, 0.79f, 1f);

                var hpTxt = TerminalUiBuilder.CreateText("HpText", row, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineRight, _textScaler);
                hpTxt.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(hpTxt.rectTransform, 0.79f, 0f, 0.985f, 1f);

                if (i < 3)
                {
                    var div = TerminalUiBuilder.CreateRect($"CrewDiv_{i}", pInner);
                    TerminalUiBuilder.SetAnchors(div, 0.03f, bot - 0.008f, 0.97f, bot - 0.003f);
                    div.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.21f, 0.30f, 0.85f);
                }

                _crewCards[i] = new CrewCardUi
                {
                    Panel = row,
                    AvatarBoxBg = avatarInner.GetComponent<Image>(),
                    NameText = nameTxt,
                    RoleText = roleTxt,
                    StatusTagText = statusTxt,
                    HpText = hpTxt,
                    Tooltip = tt
                };
            }
        }

        private void BuildCenterHomeColumn(RectTransform parent)
        {
            // Eyebrow Header: OVERVIEW + divider line
            var eyebrow = TerminalUiBuilder.CreateCompactDebugText("OverviewEyebrow", parent, theme, 20f, new Color(0.75f, 0.88f, 0.95f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            eyebrow.fontStyle = FontStyles.Bold;
            eyebrow.text = "OVERVIEW";
            TerminalUiBuilder.SetAnchors(eyebrow.rectTransform, 0.005f, 0.95f, 1f, 1f);

            var eyeDiv = TerminalUiBuilder.CreateRect("OverviewDiv", parent);
            TerminalUiBuilder.SetAnchors(eyeDiv, 0f, 0.942f, 1f, 0.946f);
            eyeDiv.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.24f, 0.33f, 1f);

            // 1. MISSION GOAL Card
            var goalCard = TerminalUiBuilder.CreateFlatPanel("MissionGoalCard", parent, new Color(0.035f, 0.125f, 0.165f, 0.96f), new Color(0.18f, 0.70f, 0.68f, 1f), 2f, true);
            TerminalUiBuilder.SetAnchors(goalCard, 0f, 0.675f, 1f, 0.925f);
            var gInner = TerminalUiBuilder.GetInner(goalCard);

            var goalIcon = TerminalUiBuilder.CreateIcon("GoalIcon", gInner, TerminalIconId.Goal, theme.StatusOk);
            TerminalUiBuilder.SetAnchors(goalIcon.rectTransform, 0.035f, 0.68f, 0.095f, 0.90f);

            var goalHdr = TerminalUiBuilder.CreateText("GoalHdr", gInner, theme, 24f, theme.StatusOk, TextAlignmentOptions.MidlineLeft, _textScaler);
            goalHdr.fontStyle = FontStyles.Bold;
            goalHdr.text = "MISSION GOAL";
            TerminalUiBuilder.SetAnchors(goalHdr.rectTransform, 0.11f, 0.66f, 0.96f, 0.92f);

            _goalTitleText = TerminalUiBuilder.CreateText("GoalTitle", gInner, theme, 34f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            _goalTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_goalTitleText.rectTransform, 0.038f, 0.24f, 0.96f, 0.66f);

            for (int i = 0; i < 7; i++)
            {
                float x0 = 0.038f + i * 0.133f;
                float x1 = x0 + 0.120f;
                var pip = TerminalUiBuilder.CreateRect($"SolPip_{i + 1}", gInner);
                TerminalUiBuilder.SetAnchors(pip, x0, 0.06f, x1, 0.21f);
                var pImg = pip.gameObject.AddComponent<Image>();
                pImg.sprite = TerminalUiArt.GetRoundedRect8px();
                pImg.type = Image.Type.Sliced;
                pImg.color = new Color(0.07f, 0.14f, 0.21f, 1f);
                _solTrackPips[i] = pImg;

                var pTxt = TerminalUiBuilder.CreateCompactDebugText("Lbl", pip, theme, 16f, theme.TextMuted, TextAlignmentOptions.Center, _textScaler);
                pTxt.fontStyle = FontStyles.Bold;
                pTxt.text = i == 6 ? $"S{i + 1}*" : $"S{i + 1}";
                TerminalUiBuilder.SetStretch(pTxt.rectTransform);
                _solTrackLabels[i] = pTxt;
            }

            // 2. OUTPOST ALERT Card (Next Step / Active Event CTA)
            _nextStepCard = TerminalUiBuilder.CreateFlatPanel("NextStepCard", parent, new Color(0.25f, 0.17f, 0.04f, 0.96f), new Color(0.85f, 0.61f, 0.14f, 1f), 2f, true);
            TerminalUiBuilder.SetAnchors(_nextStepCard, 0f, 0.370f, 1f, 0.648f);
            var nsInner = TerminalUiBuilder.GetInner(_nextStepCard);
            _nextStepInner = nsInner.GetComponent<Image>();

            _alertCardIcon = TerminalUiBuilder.CreateIcon("AlertCardIcon", nsInner, TerminalIconId.Warning, theme.StatusWarn);
            TerminalUiBuilder.SetAnchors(_alertCardIcon.rectTransform, 0.035f, 0.28f, 0.14f, 0.72f);

            _alertCardTitle = TerminalUiBuilder.CreateText("NextStepHdr", nsInner, theme, 26f, theme.StatusWarn, TextAlignmentOptions.MidlineLeft, _textScaler);
            _alertCardTitle.fontStyle = FontStyles.Bold;
            _alertCardTitle.text = "OUTPOST ALERT";
            TerminalUiBuilder.SetAnchors(_alertCardTitle.rectTransform, 0.165f, 0.58f, 0.68f, 0.88f);

            _nextStepBodyText = TerminalUiBuilder.CreateText("NextStepBody", nsInner, theme, 24f, new Color(0.95f, 0.92f, 0.86f, 1f), TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_nextStepBodyText.rectTransform, 0.165f, 0.10f, 0.68f, 0.58f);

            _nextStepBtn = TerminalUiBuilder.CreateButton("NextStepBtn", nsInner, "VIEW ALERT", theme, theme.StatusWarn, new Color(1f, 0.88f, 0.52f, 1f), new Color(0.04f, 0.08f, 0.12f, 1f), 24f, OnNextStepActionClicked, _textScaler);
            _nextStepBtnLabel = _nextStepBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (_nextStepBtnLabel != null)
                _nextStepBtnBg = _nextStepBtnLabel.transform.parent.GetComponent<Image>();
            TerminalUiBuilder.SetAnchors(_nextStepBtn.GetComponent<RectTransform>(), 0.695f, 0.24f, 0.965f, 0.76f);

            // 3. FIRST SHIFT CHECKLIST / HELIOS OPERATIONS BRIEF Card
            _onboardingCard = TerminalUiBuilder.CreateFlatPanel("OnboardingCard", parent, new Color(0.038f, 0.095f, 0.145f, 0.95f), new Color(0.16f, 0.34f, 0.44f, 1f), 2f, true);
            TerminalUiBuilder.SetAnchors(_onboardingCard, 0f, 0f, 1f, 0.345f);
            var obInner = TerminalUiBuilder.GetInner(_onboardingCard);

            _bottomCardIcon = TerminalUiBuilder.CreateIcon("ObIcon", obInner, TerminalIconId.Log, new Color(0.498f, 0.875f, 1f, 1f));
            TerminalUiBuilder.SetAnchors(_bottomCardIcon.rectTransform, 0.035f, 0.72f, 0.095f, 0.92f);

            _bottomCardTitle = TerminalUiBuilder.CreateText("ObHdr", obInner, theme, 24f, new Color(0.498f, 0.875f, 1f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            _bottomCardTitle.fontStyle = FontStyles.Bold;
            _bottomCardTitle.text = "FIRST SHIFT CHECKLIST";
            TerminalUiBuilder.SetAnchors(_bottomCardTitle.rectTransform, 0.11f, 0.70f, 0.96f, 0.94f);

            var checklistGroup = TerminalUiBuilder.CreateRect("ChecklistGroup", obInner);
            TerminalUiBuilder.SetAnchors(checklistGroup, 0.035f, 0.06f, 0.96f, 0.68f);
            _checklistGroup = checklistGroup.gameObject;

            string[] obKeys = { "onboard.step1", "onboard.step2", "onboard.step3" };
            for (int i = 0; i < 3; i++)
            {
                float rowTop = 1f - i * 0.33f;
                float rowBot = rowTop - 0.30f;
                var chk = TerminalUiBuilder.CreateIcon($"Check_{i}", checklistGroup, TerminalIconId.CheckBox, new Color(0.35f, 0.68f, 0.78f, 1f));
                TerminalUiBuilder.SetAnchors(chk.rectTransform, 0.01f, rowBot + 0.03f, 0.09f, rowTop - 0.03f);
                _onboardChecks[i] = chk;

                var lbl = TerminalUiBuilder.CreateText($"StepLbl_{i}", checklistGroup, theme, 24f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
                lbl.text = Localization.Get(obKeys[i]);
                TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.11f, rowBot, 0.99f, rowTop);
                _onboardLabels[i] = lbl;
            }

            var heliosWrap = TerminalUiBuilder.CreateRect("HeliosBubble", obInner);
            TerminalUiBuilder.SetAnchors(heliosWrap, 0.04f, 0.08f, 0.96f, 0.66f);
            _heliosBubble = heliosWrap.gameObject;
            _heliosText = TerminalUiBuilder.CreateText("HeliosTip", heliosWrap, theme, 24f, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetStretch(_heliosText.rectTransform);
            _heliosBubble.SetActive(false);
        }

        private void BuildDesktopAppDock(RectTransform parent)
        {
            Color idleBg = new Color(0.045f, 0.09f, 0.14f, 0.94f);
            Color idleBorder = new Color(0.14f, 0.24f, 0.33f, 1f);

            const int totalSlots = 9; // Overview + 8 Apps
            float step = 1f / totalSlots;
            float gap = 0.010f;

            // Slot 0: Overview
            float ovTop = 1f;
            float ovBot = 1f - step + gap;
            _overviewNavPanel = TerminalUiBuilder.CreateFlatPanel("Nav_Overview", parent, new Color(0.08f, 0.36f, 0.37f, 0.96f), new Color(0.27f, 0.89f, 0.83f, 1f), 2f, false);
            TerminalUiBuilder.SetAnchors(_overviewNavPanel, 0f, ovBot, 1f, ovTop);
            var ovInner = TerminalUiBuilder.GetInner(_overviewNavPanel);
            _overviewNavInner = ovInner.GetComponent<Image>();

            var ovBtn = _overviewNavPanel.gameObject.AddComponent<Button>();
            ovBtn.targetGraphic = _overviewNavInner;
            _overviewNavPanel.gameObject.AddComponent<ButtonHoverFeedback>();
            ovBtn.onClick.AddListener(() =>
            {
                OnUiSoundRequested?.Invoke();
                _windowManager?.CloseAll();
                RefreshTaskbarWindows();
            });
            _overviewNavIcon = TerminalUiBuilder.CreateIcon("AppIcon", ovInner, TerminalIconId.Home, Color.white);
            TerminalUiBuilder.SetAnchors(_overviewNavIcon.rectTransform, 0.05f, 0.20f, 0.18f, 0.80f);
            _overviewNavLabel = TerminalUiBuilder.CreateText("Name", ovInner, theme, 24f, Color.white, TextAlignmentOptions.MidlineLeft, _textScaler);
            _overviewNavLabel.fontStyle = FontStyles.Bold;
            _overviewNavLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _overviewNavLabel.text = "Overview";
            TerminalUiBuilder.SetAnchors(_overviewNavLabel.rectTransform, 0.205f, 0f, 0.95f, 1f);

            // Slots 1..8: Apps
            (TerminalAppId appId, string navLabel, TerminalIconId icon, string tipTitle, string tipBody)[] appDefs =
            {
                (TerminalAppId.PowerGrid,     "Power",          TerminalIconId.Bolt,    "app.power",       "gloss.power.def"),
                (TerminalAppId.LifeSupport,   "Air & Water",    TerminalIconId.Water,   "app.lifesupport", "gloss.o2.def"),
                (TerminalAppId.Greenhouse,    "Food",           TerminalIconId.Food,    "app.greenhouse",  "gloss.food.def"),
                (TerminalAppId.Radiation,     "Shield",         TerminalIconId.Shield,  "app.radiation",   "gloss.rad.def"),
                (TerminalAppId.Crew,          "Crew",           TerminalIconId.Crew,    "app.crew",        "gloss.ap.def"),
                (TerminalAppId.Comms,         "Communications", TerminalIconId.Signal,  "app.comms",       "gloss.dsn.def"),
                (TerminalAppId.CommandersLog, "Log",            TerminalIconId.Log,     "app.log",         "log.friendly_intro"),
                (TerminalAppId.Sources,       "Sources",        TerminalIconId.Sources, "app.sources",     "sources.friendly_intro")
            };

            for (int i = 0; i < appDefs.Length; i++)
            {
                int slotIdx = i + 1;
                float top = 1f - slotIdx * step;
                float bot = top - step + gap;

                var def = appDefs[i];
                var panel = TerminalUiBuilder.CreateFlatPanel($"Icon_{def.appId}", parent, idleBg, idleBorder, 2f, false);
                TerminalUiBuilder.SetAnchors(panel, 0f, bot, 1f, top);
                var inner = TerminalUiBuilder.GetInner(panel);
                var innerImg = inner.GetComponent<Image>();

                var btn = panel.gameObject.AddComponent<Button>();
                btn.targetGraphic = innerImg;
                panel.gameObject.AddComponent<ButtonHoverFeedback>();
                TerminalUiBuilder.AttachTooltip(panel, def.tipTitle, def.tipBody);

                var appId = def.appId;
                btn.onClick.AddListener(() =>
                {
                    OnUiSoundRequested?.Invoke();
                    OpenApp(appId);
                });

                var icon = TerminalUiBuilder.CreateIcon("AppIcon", inner, def.icon, new Color(0.75f, 0.94f, 0.98f, 1f));
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.05f, 0.20f, 0.18f, 0.80f);

                var label = TerminalUiBuilder.CreateText("Name", inner, theme, 24f, new Color(0.84f, 0.92f, 0.97f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
                label.fontStyle = FontStyles.Bold;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.text = def.navLabel;
                TerminalUiBuilder.SetAnchors(label.rectTransform, 0.205f, 0f, 0.89f, 1f);

                var badge = TerminalUiBuilder.CreateCompactDebugText("Badge", inner, theme, 18f, theme.TextAmber, TextAlignmentOptions.MidlineRight, _textScaler);
                badge.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(badge.rectTransform, 0.84f, 0f, 0.96f, 1f);

                _iconPanels[(int)appId] = panel;
                _iconInners[(int)appId] = innerImg;
                _iconImgs[(int)appId] = icon;
                _iconLabels[(int)appId] = label;
                _iconBadges[(int)appId] = badge;
            }
        }

        private static TerminalIconId AppIdToIcon(TerminalAppId id)
        {
            switch (id)
            {
                case TerminalAppId.PowerGrid: return TerminalIconId.Bolt;
                case TerminalAppId.LifeSupport: return TerminalIconId.Water;
                case TerminalAppId.Greenhouse: return TerminalIconId.Food;
                case TerminalAppId.Radiation: return TerminalIconId.Shield;
                case TerminalAppId.Crew: return TerminalIconId.Crew;
                case TerminalAppId.Comms: return TerminalIconId.Signal;
                case TerminalAppId.CommandersLog: return TerminalIconId.Log;
                case TerminalAppId.Sources: return TerminalIconId.Sources;
                default: return TerminalIconId.Help;
            }
        }

        private void OnNextStepActionClicked()
        {
            OnUiSoundRequested?.Invoke();
            if (_nextStepOpensEvent && _session?.CurrentSnapshot != null)
            {
                ShowIncomingEvent(_session.CurrentSnapshot);
                return;
            }
            if (_nextStepTargetApp.HasValue)
            {
                OpenApp(_nextStepTargetApp.Value);
            }
        }

        private void CreateAllAppWindows(RectTransform parent)
        {
            RegisterApp<Apps.PowerGridAppWindow>(parent, TerminalAppId.PowerGrid, "app.power");
            RegisterApp<Apps.LifeSupportAppWindow>(parent, TerminalAppId.LifeSupport, "app.lifesupport");
            RegisterApp<Apps.GreenhouseAppWindow>(parent, TerminalAppId.Greenhouse, "app.greenhouse");
            RegisterApp<Apps.RadiationAppWindow>(parent, TerminalAppId.Radiation, "app.radiation");
            RegisterApp<Apps.CrewAppWindow>(parent, TerminalAppId.Crew, "app.crew");
            RegisterApp<Apps.CommsAppWindow>(parent, TerminalAppId.Comms, "app.comms");
            RegisterApp<Apps.CommandersLogAppWindow>(parent, TerminalAppId.CommandersLog, "app.log");

            var srcWin = RegisterApp<Apps.SourcesAppWindow>(parent, TerminalAppId.Sources, "app.sources");
            srcWin.SetDatabase(sourcesDatabase);
        }

        private T RegisterApp<T>(RectTransform parent, TerminalAppId id, string titleKey) where T : WindowView
        {
            var go = new GameObject("Window_" + id, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var win = go.AddComponent<T>();
            win.Initialize(id, titleKey, _session, _windowManager, theme, _textScaler, parent);
            _windowManager.RegisterWindow(win);
            return win;
        }

        private void BuildTaskbar(RectTransform parent)
        {
            // Left Footer: Info Icon + "Hint: ..." forecast line
            var hintIcon = TerminalUiBuilder.CreateIcon("HintIcon", parent, TerminalIconId.Help, new Color(0.50f, 0.86f, 0.96f, 1f));
            TerminalUiBuilder.SetAnchors(hintIcon.rectTransform, 0.002f, 0.22f, 0.025f, 0.78f);

            _forecastStripText = TerminalUiBuilder.CreateText("ForecastText", parent, theme, 24f, new Color(0.84f, 0.91f, 0.96f, 1f), TextAlignmentOptions.MidlineLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_forecastStripText.rectTransform, 0.032f, 0f, 0.485f, 1f);

            // Subtle compact utility toggles (Hints + Help)
            _hintsToggleBtn = TerminalUiBuilder.CreateButton("HintsToggleBtn", parent, Localization.Get("helios.hints_on"), theme, new Color(0.055f, 0.11f, 0.16f, 1f), new Color(0.16f, 0.28f, 0.38f, 1f), new Color(0.75f, 0.88f, 0.96f, 1f), 24f, () =>
            {
                OnUiSoundRequested?.Invoke();
                _hintsEnabled = !_hintsEnabled;
                PlayerPrefs.SetInt(PrefHintsEnabled, _hintsEnabled ? 1 : 0);
                PlayerPrefs.Save();
                if (_session?.CurrentSnapshot != null) RefreshAll(_session.CurrentSnapshot);
            }, _textScaler);
            _hintsToggleLabel = _hintsToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_hintsToggleBtn.GetComponent<RectTransform>(), 0.492f, 0.12f, 0.578f, 0.88f);

            var helpBtn = TerminalUiBuilder.CreateButton("HelpTaskBtn", parent, "?", theme, new Color(0.055f, 0.11f, 0.16f, 1f), new Color(0.16f, 0.28f, 0.38f, 1f), theme.StatusOk, 24f, () =>
            {
                OnUiSoundRequested?.Invoke();
                _helpModal.SetActive(true);
                _helpModal.transform.SetAsLastSibling();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(helpBtn.GetComponent<RectTransform>(), 0.583f, 0.12f, 0.618f, 0.88f);
            TerminalUiBuilder.AttachTooltip(helpBtn, "ui.help", "help.howto_header");

            // Leave Terminal [ESC] sleek dark slate button
            _leaveTerminalBtn = TerminalUiBuilder.CreateButton("LeaveTerminalBtn", parent, "LEAVE TERMINAL [ESC]", theme, new Color(0.06f, 0.115f, 0.165f, 1f), new Color(0.20f, 0.34f, 0.44f, 1f), new Color(0.90f, 0.95f, 0.99f, 1f), 24f, () =>
            {
                OnUiSoundRequested?.Invoke();
                OnLeaveTerminalRequested?.Invoke();
            }, _textScaler);
            var leaveLbl = _leaveTerminalBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (leaveLbl != null) leaveLbl.textWrappingMode = TextWrappingModes.NoWrap;
            TerminalUiBuilder.SetAnchors(_leaveTerminalBtn.GetComponent<RectTransform>(), 0.625f, 0.10f, 0.835f, 0.90f);
            _leaveTerminalBtn.gameObject.SetActive(_showLeaveButton);

            // Prominent Cyan-Teal END SOL CTA Button
            _endSolBtn = TerminalUiBuilder.CreateButton("EndSolBtn", parent, "END SOL", theme, new Color(0.12f, 0.86f, 0.76f, 1f), new Color(0.45f, 1f, 0.94f, 1f), new Color(0.03f, 0.08f, 0.12f, 1f), 28f, OnEndSolClicked, _textScaler, true);
            TerminalUiBuilder.SetAnchors(_endSolBtn.GetComponent<RectTransform>(), 0.842f, 0.06f, 1f, 0.94f);
            _endSolLabel = _endSolBtn.GetComponentInChildren<TextMeshProUGUI>();
            var endInner = _endSolLabel != null ? _endSolLabel.transform.parent as RectTransform : null;
            if (endInner != null)
            {
                var nextIcon = TerminalUiBuilder.CreateIcon("NextHexIcon", endInner, TerminalIconId.PlayArrow, new Color(0.03f, 0.08f, 0.12f, 1f));
                TerminalUiBuilder.SetAnchors(nextIcon.rectTransform, 0.11f, 0.24f, 0.25f, 0.76f);
                if (_endSolLabel != null)
                {
                    _endSolLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    TerminalUiBuilder.SetAnchors(_endSolLabel.rectTransform, 0.25f, 0f, 0.95f, 1f);
                }
            }
        }

        private void BuildEventModal()
        {
            var overlay = TerminalUiBuilder.CreateRect("EventModal", terminalRoot);
            TerminalUiBuilder.SetStretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.10f, 0.84f);
            _eventModal = overlay.gameObject;

            var card = TerminalUiBuilder.CreateFlatPanel("EventCard", overlay, theme.WindowBg, theme.AccentPower, 2f, true);
            TerminalUiBuilder.SetAnchors(card, 0.16f, 0.10f, 0.84f, 0.90f);
            var inner = TerminalUiBuilder.GetInner(card);

            // Illustration Slot Banner
            var bannerPanel = TerminalUiBuilder.CreateFlatPanel("IllustrationSlot", inner, theme.PanelRaisedBg, theme.BorderUnfocused, 1f);
            TerminalUiBuilder.SetAnchors(bannerPanel, 0.03f, 0.80f, 0.97f, 0.97f);
            var bInner = TerminalUiBuilder.GetInner(bannerPanel);
            _eventBannerBg = bInner.GetComponent<Image>();

            _eventBannerIcon = TerminalUiBuilder.CreateIcon("EventIcon", bInner, TerminalIconId.Battery, theme.AccentPower);
            TerminalUiBuilder.SetAnchors(_eventBannerIcon.rectTransform, 0.02f, 0.14f, 0.11f, 0.86f);

            _eventBannerSubtitle = TerminalUiBuilder.CreateText("BannerSubtitle", bInner, theme, theme.BodyFontSize, theme.AccentPower, TextAlignmentOptions.MidlineLeft, _textScaler);
            _eventBannerSubtitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_eventBannerSubtitle.rectTransform, 0.12f, 0.56f, 0.96f, 0.94f);

            _eventTitleText = TerminalUiBuilder.CreateText("EventTitle", bInner, theme, theme.TitleFontSize, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            _eventTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_eventTitleText.rectTransform, 0.12f, 0.06f, 0.98f, 0.60f);

            _eventDescText = TerminalUiBuilder.CreateText("EventDesc", inner, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_eventDescText.rectTransform, 0.04f, 0.58f, 0.96f, 0.78f);

            // 3 big Option Buttons with consequence chips
            for (int i = 0; i < 3; i++)
            {
                int optIdx = i;
                float yMax = 0.56f - i * 0.17f;
                float yMin = yMax - 0.15f;

                var btn = TerminalUiBuilder.CreateButton($"EventOpt_{i}", inner, "", theme, theme.ButtonBg, theme.BorderFocused, theme.TextPrimary, theme.BodyFontSize, () =>
                {
                    OnUiSoundRequested?.Invoke();
                    _session.ChooseEventOption(optIdx);
                    RefreshAll(_session.CurrentSnapshot);
                    ShowIncomingEvent(_session.CurrentSnapshot);
                }, _textScaler);
                TerminalUiBuilder.SetAnchors(btn.GetComponent<RectTransform>(), 0.04f, yMin, 0.96f, yMax);

                var btnInner = TerminalUiBuilder.GetInner(btn.GetComponent<RectTransform>());
                var oldLbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                oldLbl.gameObject.SetActive(false);

                var choiceTitle = TerminalUiBuilder.CreateText("ChoiceTitle", btnInner, theme, 26f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
                choiceTitle.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(choiceTitle.rectTransform, 0.02f, 0.44f, 0.98f, 0.96f);

                var chipLine = TerminalUiBuilder.CreateText("ConsequenceChips", btnInner, theme, 24f, theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
                chipLine.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(chipLine.rectTransform, 0.02f, 0.04f, 0.98f, 0.44f);

                _eventOptionBtns[i] = btn;
                _eventOptionLabels[i] = choiceTitle;
                _eventOptionChips[i] = chipLine;
            }

            // Outcome + NASA Science Fact Box
            var resPanel = TerminalUiBuilder.CreateFlatPanel("ResultBox", inner, theme.PanelBg, theme.StatusOk, 2f);
            TerminalUiBuilder.SetAnchors(resPanel, 0.04f, 0.18f, 0.96f, 0.56f);
            var rInner = TerminalUiBuilder.GetInner(resPanel);
            _eventResultBox = resPanel.gameObject;

            _eventResultText = TerminalUiBuilder.CreateText("EventResult", rInner, theme, 26f, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_eventResultText.rectTransform, 0.03f, 0.54f, 0.97f, 0.94f);

            _eventFactText = TerminalUiBuilder.CreateText("EventFact", rInner, theme, theme.BodyFontSize, theme.TextAmber, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_eventFactText.rectTransform, 0.03f, 0.06f, 0.97f, 0.50f);

            _eventContinueBtn = TerminalUiBuilder.CreateButton("EventContinueBtn", inner, Localization.Get("modal.continue"), theme, theme.ButtonAccentBg, theme.StatusOk, theme.TextPrimary, theme.HeaderFontSize, () =>
            {
                OnUiSoundRequested?.Invoke();
                _eventModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(_eventContinueBtn.GetComponent<RectTransform>(), 0.30f, 0.04f, 0.70f, 0.15f);

            _eventModal.SetActive(false);
        }

        private void BuildConfirmEndSolModal()
        {
            var overlay = TerminalUiBuilder.CreateRect("ConfirmEndSolModal", terminalRoot);
            TerminalUiBuilder.SetStretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.10f, 0.84f);
            _confirmEndSolModal = overlay.gameObject;

            var card = TerminalUiBuilder.CreateFlatPanel("ConfirmCard", overlay, theme.WindowBg, theme.StatusCrit, 2f, true);
            TerminalUiBuilder.SetAnchors(card, 0.25f, 0.30f, 0.75f, 0.70f);
            var inner = TerminalUiBuilder.GetInner(card);

            var title = TerminalUiBuilder.CreateText("ConfirmTitle", inner, theme, theme.TitleFontSize, theme.StatusCrit, TextAlignmentOptions.Center, _textScaler);
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("modal.confirm_endsol.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0.04f, 0.72f, 0.96f, 0.94f);

            _confirmBodyText = TerminalUiBuilder.CreateText("ConfirmBody", inner, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.Center, _textScaler);
            TerminalUiBuilder.SetAnchors(_confirmBodyText.rectTransform, 0.06f, 0.32f, 0.94f, 0.70f);

            var cancelBtn = TerminalUiBuilder.CreateButton("CancelBtn", inner, Localization.Get("modal.cancel"), theme, theme.ButtonAccentBg, theme.StatusOk, theme.TextPrimary, theme.BodyFontSize, () =>
            {
                OnUiSoundRequested?.Invoke();
                _confirmEndSolModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(cancelBtn.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.47f, 0.26f);

            var confirmBtn = TerminalUiBuilder.CreateButton("ConfirmBtn", inner, Localization.Get("modal.confirm"), theme, theme.ButtonDangerBg, theme.StatusCrit, Color.white, theme.BodyFontSize, () =>
            {
                OnUiSoundRequested?.Invoke();
                _confirmEndSolModal.SetActive(false);
                MarkOnboardingStep3Complete();
                _session.EndSol();
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(confirmBtn.GetComponent<RectTransform>(), 0.53f, 0.08f, 0.94f, 0.26f);

            _confirmEndSolModal.SetActive(false);
        }

        private void BuildTransitionOverlay()
        {
            var overlay = TerminalUiBuilder.CreateRect("SolTransitionOverlay", terminalRoot);
            TerminalUiBuilder.SetStretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.10f, 0.92f);
            _transitionGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            _transitionOverlay = overlay.gameObject;

            _transitionCard = TerminalUiBuilder.CreateFlatPanel("SummaryCard", overlay, theme.WindowBg, theme.BorderFocused, 2f, true);
            TerminalUiBuilder.SetAnchors(_transitionCard, 0.20f, 0.16f, 0.80f, 0.84f);
            var inner = TerminalUiBuilder.GetInner(_transitionCard);

            _transitionTitleText = TerminalUiBuilder.CreateText("TransTitle", inner, theme, theme.TitleFontSize, theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            _transitionTitleText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_transitionTitleText.rectTransform, 0.04f, 0.80f, 0.96f, 0.96f);

            TerminalIconId[] bulletIcons = { TerminalIconId.Battery, TerminalIconId.Shield, TerminalIconId.Heart };
            Color[] bulletColors = { theme.AccentPower, theme.AccentLifeSupport, theme.AccentCrew };
            for (int i = 0; i < 3; i++)
            {
                float yTop = 0.76f - i * 0.19f;
                float yBot = yTop - 0.16f;
                var row = TerminalUiBuilder.CreateFlatPanel($"SummaryBullet_{i}", inner, theme.PanelBg, theme.BorderUnfocused, 1f);
                TerminalUiBuilder.SetAnchors(row, 0.05f, yBot, 0.95f, yTop);
                var rInner = TerminalUiBuilder.GetInner(row);

                var ic = TerminalUiBuilder.CreateIcon("Icon", rInner, bulletIcons[i], bulletColors[i]);
                TerminalUiBuilder.SetAnchors(ic.rectTransform, 0.02f, 0.18f, 0.09f, 0.82f);

                var txt = TerminalUiBuilder.CreateText("Text", rInner, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
                TerminalUiBuilder.SetAnchors(txt.rectTransform, 0.11f, 0.04f, 0.98f, 0.96f);
                _transitionBullets[i] = txt;
            }

            var nextBtn = TerminalUiBuilder.CreateButton("NextSolBtn", inner, Localization.Get("modal.continue"), theme, theme.ButtonAccentBg, theme.StatusOk, theme.TextPrimary, theme.HeaderFontSize, OnTransitionContinueClicked, _textScaler);
            TerminalUiBuilder.SetAnchors(nextBtn.GetComponent<RectTransform>(), 0.30f, 0.05f, 0.70f, 0.16f);

            _transitionOverlay.SetActive(false);
        }

        private void BuildHelpModal()
        {
            var overlay = TerminalUiBuilder.CreateRect("HelpGlossaryModal", terminalRoot);
            TerminalUiBuilder.SetStretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.10f, 0.84f);
            _helpModal = overlay.gameObject;

            var card = TerminalUiBuilder.CreateFlatPanel("HelpCard", overlay, theme.WindowBg, theme.AccentHelp, 2f, true);
            TerminalUiBuilder.SetAnchors(card, 0.12f, 0.08f, 0.88f, 0.92f);
            var inner = TerminalUiBuilder.GetInner(card);

            var title = TerminalUiBuilder.CreateText("HelpTitle", inner, theme, theme.TitleFontSize, theme.AccentHelp, TextAlignmentOptions.MidlineLeft, _textScaler);
            title.fontStyle = FontStyles.Bold;
            title.text = Localization.Get("help.title");
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0.03f, 0.90f, 0.80f, 0.98f);

            var closeBtn = TerminalUiBuilder.CreateButton("HelpClose", inner, Localization.Get("ui.close"), theme, theme.ButtonDangerBg, Color.clear, Color.white, theme.BodyFontSize, () =>
            {
                OnUiSoundRequested?.Invoke();
                _helpModal.SetActive(false);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(closeBtn.GetComponent<RectTransform>(), 0.84f, 0.90f, 0.97f, 0.97f);

            var howtoBox = TerminalUiBuilder.CreateFlatPanel("HowToBox", inner, theme.PanelRaisedBg, theme.BorderFocused, 2f);
            TerminalUiBuilder.SetAnchors(howtoBox, 0.03f, 0.62f, 0.97f, 0.88f);
            var hInner = TerminalUiBuilder.GetInner(howtoBox);

            var howtoHdr = TerminalUiBuilder.CreateText("HowtoHdr", hInner, theme, theme.HeaderFontSize, theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            howtoHdr.fontStyle = FontStyles.Bold;
            howtoHdr.text = Localization.Get("help.howto_header");
            TerminalUiBuilder.SetAnchors(howtoHdr.rectTransform, 0.02f, 0.74f, 0.98f, 0.96f);

            var howtoBody = TerminalUiBuilder.CreateText("HowtoBody", hInner, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            howtoBody.text = Localization.Get("help.howto_steps");
            TerminalUiBuilder.SetAnchors(howtoBody.rectTransform, 0.02f, 0.04f, 0.98f, 0.72f);

            var glossHdr = TerminalUiBuilder.CreateText("GlossHdr", inner, theme, theme.HeaderFontSize, theme.AccentSources, TextAlignmentOptions.MidlineLeft, _textScaler);
            glossHdr.fontStyle = FontStyles.Bold;
            glossHdr.text = Localization.Get("help.glossary_header");
            TerminalUiBuilder.SetAnchors(glossHdr.rectTransform, 0.03f, 0.54f, 0.97f, 0.61f);

            var glossScrollText = TerminalUiBuilder.CreateScrollableText("GlossScroll", inner, theme, theme.BodyFontSize, theme.TextPrimary, theme.PanelBg, theme.BorderUnfocused, out var glossPanel, _textScaler);
            TerminalUiBuilder.SetAnchors(glossPanel, 0.03f, 0.03f, 0.97f, 0.53f);

            _sb.Clear();
            if (glossaryDatabase != null && glossaryDatabase.Entries != null)
            {
                for (int i = 0; i < glossaryDatabase.Entries.Count; i++)
                {
                    var e = glossaryDatabase.Entries[i];
                    string src = string.IsNullOrEmpty(e.SourceId) ? "" : $" [{e.SourceId}]";
                    _sb.AppendLine($"<b><color=#FFD43B>{Localization.Get(e.TermKey)}</color></b>{src}");
                    _sb.AppendLine($"  {Localization.Get(e.DefinitionKey)}");
                    _sb.AppendLine();
                }
            }
            glossScrollText.text = _sb.ToString();
            _helpModal.SetActive(false);
        }

        private void BuildMissionEndOverlay()
        {
            var overlay = TerminalUiBuilder.CreateRect("MissionEndOverlay", terminalRoot);
            TerminalUiBuilder.SetStretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.10f, 0.94f);
            _missionEndOverlay = overlay.gameObject;

            var card = TerminalUiBuilder.CreateFlatPanel("EndCard", overlay, theme.WindowBg, theme.BorderFocused, 2f, true);
            TerminalUiBuilder.SetAnchors(card, 0.12f, 0.10f, 0.88f, 0.90f);
            var inner = TerminalUiBuilder.GetInner(card);

            _missionEndTitle = TerminalUiBuilder.CreateText("EndTitle", inner, theme, theme.TitleFontSize, theme.StatusOk, TextAlignmentOptions.Center, _textScaler);
            _missionEndTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_missionEndTitle.rectTransform, 0.03f, 0.84f, 0.97f, 0.97f);

            _missionEndBody = TerminalUiBuilder.CreateText("EndBody", inner, theme, theme.BodyFontSize, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_missionEndBody.rectTransform, 0.04f, 0.15f, 0.96f, 0.82f);

            var replayBtn = TerminalUiBuilder.CreateButton("ReplayBtn", inner, Localization.Get("ui.new_mission"), theme, theme.ButtonAccentBg, theme.StatusOk, theme.TextPrimary, theme.HeaderFontSize, () =>
            {
                _missionEndOverlay.SetActive(false);
                _windowManager.CloseAll();
                _session.RestartMission(_session.CurrentSnapshot.Seed + 1);
            }, _textScaler);
            TerminalUiBuilder.SetAnchors(replayBtn.GetComponent<RectTransform>(), 0.34f, 0.03f, 0.66f, 0.12f);

            _missionEndOverlay.SetActive(false);
        }

        private void BuildTooltipPopup()
        {
            var panel = TerminalUiBuilder.CreateFlatPanel("TooltipPopup", terminalRoot, theme.PanelRaisedBg, theme.AccentHelp, 2f, true);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(460f, 150f);
            _tooltipRect = panel;
            _tooltipPanel = panel.gameObject;

            var inner = TerminalUiBuilder.GetInner(panel);
            foreach (var g in panel.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            _tooltipTitle = TerminalUiBuilder.CreateText("TipTitle", inner, theme, 24f, theme.AccentHelp, TextAlignmentOptions.MidlineLeft, _textScaler);
            _tooltipTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_tooltipTitle.rectTransform, 0.04f, 0.66f, 0.96f, 0.96f);

            _tooltipBody = TerminalUiBuilder.CreateText("TipBody", inner, theme, 24f, theme.TextPrimary, TextAlignmentOptions.TopLeft, _textScaler);
            TerminalUiBuilder.SetAnchors(_tooltipBody.rectTransform, 0.04f, 0.05f, 0.96f, 0.64f);

            _tooltipPanel.SetActive(false);
        }

        private void BuildDebugPanel()
        {
            Transform debugParent = terminalRoot.parent != null ? terminalRoot.parent : terminalRoot;
            var panel = TerminalUiBuilder.CreateFlatPanel("DevDebugPanel", debugParent, new Color(0.06f, 0.08f, 0.13f, 0.97f), theme.TextAmber, 2f);
            TerminalUiBuilder.SetAnchors(panel, 0.54f, 0.02f, 0.99f, 0.98f);
            var dbgCanvas = panel.gameObject.AddComponent<Canvas>();
            dbgCanvas.overrideSorting = true;
            dbgCanvas.sortingOrder = 100;
            panel.gameObject.AddComponent<GraphicRaycaster>();
            _debugPanel = panel.gameObject;
            var inner = TerminalUiBuilder.GetInner(panel);

            var title = TerminalUiBuilder.CreateCompactDebugText("DebugTitle", inner, theme, 20f, theme.TextAmber, TextAlignmentOptions.MidlineLeft, _textScaler);
            title.text = "DEV DEBUG (` TOGGLE)";
            TerminalUiBuilder.SetAnchors(title.rectTransform, 0.03f, 0.95f, 0.65f, 0.99f);

            _debugFpsText = TerminalUiBuilder.CreateCompactDebugText("DebugFps", inner, theme, 20f, theme.StatusOk, TextAlignmentOptions.MidlineRight, _textScaler);
            _debugFpsText.text = "FPS: 60.0";
            TerminalUiBuilder.SetAnchors(_debugFpsText.rectTransform, 0.65f, 0.95f, 0.97f, 0.99f);

            int r = 0;
            AddDebugPair(inner, r++, "SKIP SOL", () => _session.DebugSkipSol(), "UNLOCK DOORS", () => OnDebugUnlockAllDoorsRequested?.Invoke());
            AddDebugPair(inner, r++, "SET SOL 1", () => _session.DebugSetSol(1), "SET SOL 7", () => _session.DebugSetSol(7));
            AddDebugPair(inner, r++, "STOCKS 100%", () => _session.DebugSetStocks(100, 100, 100, 100, 100), "STOCKS 15%", () => _session.DebugSetStocks(15, 15, 15, 15, 15));
            AddDebugPair(inner, r++, "BATTERY 0%", () => _session.DebugSetBattery(0f), "STORM ON/OFF", () => _session.DebugSetStorm(_session.CurrentSnapshot != null && _session.CurrentSnapshot.DustStormSolsRemaining <= 0));
            AddDebugPair(inner, r++, "FORCE FLARE", () => _session.DebugTriggerEvent("evt_solar_flare"), "TRIG METEOR", () => _session.DebugTriggerEvent("evt_micrometeorite"));
            AddDebugPair(inner, r++, "TP COMMAND", () => OnDebugTeleportSpawnRequested?.Invoke(), "TP CORRIDOR", () => OnDebugTeleportCorridorRequested?.Invoke());
            AddDebugPair(inner, r++, "TP AIRLOCK", () => OnDebugTeleportAirlockRequested?.Invoke(), "TP SURFACE", () => OnDebugTeleportSurfaceRequested?.Invoke());
            AddDebugPair(inner, r++, "FOCUS IN", () => OnDebugTeleportLeanRequested?.Invoke(true), "FOCUS OUT", () => OnDebugTeleportLeanRequested?.Invoke(false));
            AddDebugPair(inner, r++, "SKIP INTRO", () => OnDebugSkipIntroRequested?.Invoke(), "RESET INTRO", () => { PlayerPrefs.DeleteKey(PrefOnboardingDone); _onboardingDone = false; _onboardStep1Done = _onboardStep2Done = _onboardStep3Done = false; OnDebugResetIntroSeenRequested?.Invoke(); RefreshOnboardingUi(_session?.CurrentSnapshot?.CurrentSol ?? 1); });
            AddDebugPair(inner, r++, "JUMP DEBRIEF", () => OnDebugJumpToDebriefRequested?.Invoke(), "REPLAY INTRO", () => OnDebugReplayIntroRequested?.Invoke());
            AddDebugPair(inner, r++, "FORCE WIN", () => _session.DebugForceOutcome(true), "FORCE LOSE", () => _session.DebugForceOutcome(false));
            AddDebugPair(inner, r++, "PSX ON/OFF", () =>
            {
                if (_psxSettings != null)
                {
                    _psxSettings.SetEnabled(!_psxSettings.enabled, savePrefs: true);
                    SyncPsxDebugSliders();
                }
            }, "REDUCE FLICK", () =>
            {
                if (_accessibility != null)
                {
                    bool next = !_accessibility.ReduceFlicker;
                    _accessibility.SetReduceFlicker(next);
                    _accessibility.SetReduceMotion(next);
                    _psxSettings?.NotifyChanged();
                }
            });

            // 6 live PSX tweak sliders (no CRT sliders; Grain defaults to 0)
            AddDebugSliderRow(inner, r++, "VTX SNAP", 64f, 320f, wholeNumbers: true, out _dbgSnapSlider, out _dbgSnapVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.vertexSnapResolution = Mathf.RoundToInt(v);
                _psxSettings.NotifyChanged();
                if (_dbgSnapVal != null) _dbgSnapVal.text = $"{_psxSettings.vertexSnapResolution}";
            });
            AddDebugSliderRow(inner, r++, "COLOR LVL", 8f, 32f, wholeNumbers: true, out _dbgColorSlider, out _dbgColorVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.colorLevels = Mathf.RoundToInt(v);
                _psxSettings.NotifyChanged();
                if (_dbgColorVal != null) _dbgColorVal.text = $"{_psxSettings.colorLevels}";
            });
            AddDebugSliderRow(inner, r++, "DITHER", 0f, 1f, wholeNumbers: false, out _dbgDitherSlider, out _dbgDitherVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.ditherStrength = v;
                _psxSettings.NotifyChanged();
                if (_dbgDitherVal != null) _dbgDitherVal.text = $"{_psxSettings.ditherStrength:0.00}";
            });
            AddDebugSliderRow(inner, r++, "AFFINE", 0f, 1f, wholeNumbers: false, out _dbgAffineSlider, out _dbgAffineVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.affineAmount = v;
                _psxSettings.NotifyChanged();
                if (_dbgAffineVal != null) _dbgAffineVal.text = $"{_psxSettings.affineAmount:0.00}";
            });
            AddDebugSliderRow(inner, r++, "GRAIN", 0f, 0.1f, wholeNumbers: false, out _dbgGrainSlider, out _dbgGrainVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.grainAmount = v;
                _psxSettings.NotifyChanged();
                if (_dbgGrainVal != null) _dbgGrainVal.text = $"{_psxSettings.grainAmount:0.000}";
            });
            AddDebugSliderRow(inner, r++, "RES (H)", 160f, 540f, wholeNumbers: true, out _dbgResSlider, out _dbgResVal, v =>
            {
                if (_syncingDebugSliders || _psxSettings == null) return;
                _psxSettings.SetCustomResolutionHeight(Mathf.RoundToInt(v));
                if (_dbgResVal != null) _dbgResVal.text = $"{_psxSettings.renderHeight}p";
            });

            SyncPsxDebugSliders();
            _debugPanel.SetActive(false);
        }

        private void SyncPsxDebugSliders()
        {
            if (_psxSettings == null || _dbgSnapSlider == null) return;
            _syncingDebugSliders = true;
            _dbgSnapSlider.SetValueWithoutNotify(_psxSettings.vertexSnapResolution);
            _dbgSnapVal.text = $"{_psxSettings.vertexSnapResolution}";
            _dbgColorSlider.SetValueWithoutNotify(_psxSettings.colorLevels);
            _dbgColorVal.text = $"{_psxSettings.colorLevels}";
            _dbgDitherSlider.SetValueWithoutNotify(_psxSettings.ditherStrength);
            _dbgDitherVal.text = $"{_psxSettings.ditherStrength:0.00}";
            _dbgAffineSlider.SetValueWithoutNotify(_psxSettings.affineAmount);
            _dbgAffineVal.text = $"{_psxSettings.affineAmount:0.00}";
            _dbgGrainSlider.SetValueWithoutNotify(_psxSettings.grainAmount);
            _dbgGrainVal.text = $"{_psxSettings.grainAmount:0.000}";
            _dbgResSlider.SetValueWithoutNotify(_psxSettings.renderHeight);
            _dbgResVal.text = $"{_psxSettings.renderHeight}p";
            _syncingDebugSliders = false;
        }

        private void BuildToastBanner()
        {
            var panel = TerminalUiBuilder.CreateFlatPanel("ToastBanner", terminalRoot, theme.PanelRaisedBg, theme.TextAmber, 2f, true);
            TerminalUiBuilder.SetAnchors(panel, 0.22f, 0.87f, 0.78f, 0.95f);
            _toastBanner = panel.gameObject;
            var inner = TerminalUiBuilder.GetInner(panel);
            _toastText = TerminalUiBuilder.CreateText("ToastText", inner, theme, theme.HeaderFontSize, theme.TextAmber, TextAlignmentOptions.Center, _textScaler);
            _toastText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetStretch(_toastText.rectTransform, 6f);
            _toastBanner.SetActive(false);
        }

        private void AddDebugPair(RectTransform parent, int row, string leftLabel, Action onLeft, string rightLabel, Action onRight)
        {
            const float top = 0.935f;
            const float step = 0.050f;
            float yMax = top - row * step;
            float yMin = yMax - 0.044f;

            var lPanel = TerminalUiBuilder.CreateFlatPanel("Dbg_" + leftLabel, parent, theme.ButtonBg, theme.BorderUnfocused, 1f);
            TerminalUiBuilder.SetAnchors(lPanel, 0.03f, yMin, 0.49f, yMax);
            var lBtn = lPanel.gameObject.AddComponent<Button>();
            lBtn.targetGraphic = TerminalUiBuilder.GetInner(lPanel).GetComponent<Image>();
            lBtn.onClick.AddListener(() => onLeft?.Invoke());
            var lTxt = TerminalUiBuilder.CreateCompactDebugText("Lbl", TerminalUiBuilder.GetInner(lPanel), theme, 16f, theme.TextPrimary, TextAlignmentOptions.Center, _textScaler);
            lTxt.text = leftLabel;
            TerminalUiBuilder.SetStretch(lTxt.rectTransform, 2f);

            var rPanel = TerminalUiBuilder.CreateFlatPanel("Dbg_" + rightLabel, parent, theme.ButtonBg, theme.BorderUnfocused, 1f);
            TerminalUiBuilder.SetAnchors(rPanel, 0.51f, yMin, 0.97f, yMax);
            var rBtn = rPanel.gameObject.AddComponent<Button>();
            rBtn.targetGraphic = TerminalUiBuilder.GetInner(rPanel).GetComponent<Image>();
            rBtn.onClick.AddListener(() => onRight?.Invoke());
            var rTxt = TerminalUiBuilder.CreateCompactDebugText("Lbl", TerminalUiBuilder.GetInner(rPanel), theme, 16f, theme.TextPrimary, TextAlignmentOptions.Center, _textScaler);
            rTxt.text = rightLabel;
            TerminalUiBuilder.SetStretch(rTxt.rectTransform, 2f);
        }

        private void AddDebugSliderRow(RectTransform parent, int row, string label, float min, float max, bool wholeNumbers, out Slider slider, out TextMeshProUGUI valueText, Action<float> onChanged)
        {
            const float top = 0.935f;
            const float step = 0.050f;
            float yMax = top - row * step;
            float yMin = yMax - 0.044f;

            var lbl = TerminalUiBuilder.CreateCompactDebugText("DbgLbl_" + label, parent, theme, 16f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _textScaler);
            lbl.text = label;
            TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.03f, yMin, 0.30f, yMax);

            var panel = TerminalUiBuilder.CreateFlatPanel("DbgSld_" + label, parent, theme.BarTrackBg, theme.BorderUnfocused, 1f);
            TerminalUiBuilder.SetAnchors(panel, 0.31f, yMin + 0.006f, 0.80f, yMax - 0.006f);
            var sInner = TerminalUiBuilder.GetInner(panel);

            var fillArea = TerminalUiBuilder.CreateRect("Fill Area", sInner);
            TerminalUiBuilder.SetStretch(fillArea, 1f);
            var fillRect = TerminalUiBuilder.CreateRect("Fill", fillArea);
            fillRect.gameObject.AddComponent<Image>().color = theme.StatusOk;

            var handleArea = TerminalUiBuilder.CreateRect("Handle Slide Area", sInner);
            TerminalUiBuilder.SetStretch(handleArea, 2f);
            var handleRect = TerminalUiBuilder.CreateRect("Handle", handleArea);
            handleRect.sizeDelta = new Vector2(12f, 0f);
            handleRect.gameObject.AddComponent<Image>().color = theme.TextAmber;

            slider = panel.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleRect.GetComponent<Image>();
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            valueText = TerminalUiBuilder.CreateCompactDebugText("DbgVal_" + label, parent, theme, 16f, theme.TextAmber, TextAlignmentOptions.MidlineRight, _textScaler);
            TerminalUiBuilder.SetAnchors(valueText.rectTransform, 0.81f, yMin, 0.97f, yMax);
        }

        private static string GetAppShortName(TerminalAppId id)
        {
            switch (id)
            {
                case TerminalAppId.PowerGrid: return Localization.Get("dock.power");
                case TerminalAppId.LifeSupport: return Localization.Get("dock.lifesupport");
                case TerminalAppId.Greenhouse: return Localization.Get("dock.greenhouse");
                case TerminalAppId.Radiation: return Localization.Get("dock.radiation");
                case TerminalAppId.Crew: return Localization.Get("dock.crew");
                case TerminalAppId.Comms: return Localization.Get("dock.comms");
                case TerminalAppId.CommandersLog: return Localization.Get("dock.log");
                case TerminalAppId.Sources: return Localization.Get("dock.sources");
                default: return id.ToString();
            }
        }
    }
}
