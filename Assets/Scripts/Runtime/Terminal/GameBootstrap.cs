using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Scene bootstrap that creates a seeded GameSession and wires it to TerminalController
    /// and (optionally, in Outpost scene) M3/M4/M5 room, audio, PSX rendering, FPS player gate, and MissionFlowController.
    /// Keeps TerminalRoot decoupled from scene-specific dependencies.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Core Assets & Session")]
        [SerializeField] private OutpostConfigAsset configAsset;
        [SerializeField] private UITheme theme;
        [SerializeField] private FontTheme fontTheme;
        [SerializeField] private IntroSequenceData introSequenceData;
        [SerializeField] private PSXSettings psxSettings;
        [SerializeField] private SourcesDatabase sourcesDatabase;
        [SerializeField] private FactDatabase factDatabase;
        [SerializeField] private TerminalController terminalController;
        [SerializeField] private int missionSeed = 101;

        [Header("Milestone 3/4/5 Room, Rendering & Flow Integration (optional in Terminal scene)")]
        [SerializeField] private bool startInRoomView;
        [SerializeField] private bool enableTitleIntroFlow = true;
        [SerializeField] private AccessibilitySettings accessibilitySettings;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private RoomStateBinder roomStateBinder;
        [SerializeField] private PlayerGate playerGate;
        [SerializeField] private FirstPersonRoomController firstPersonController;
        [SerializeField] private MissionFlowController missionFlowController;
        [SerializeField] private PSXRenderController psxRenderController;

        public GameSession Session { get; private set; }

        private void Start()
        {
            if (configAsset == null)
                configAsset = Resources.Load<OutpostConfigAsset>("OutpostConfig");
            if (theme == null)
                theme = Resources.Load<UITheme>("TerminalTheme");
            if (fontTheme == null)
                fontTheme = Resources.Load<FontTheme>("FontTheme");
            if (theme != null && theme.FontTheme == null && fontTheme != null)
                theme.FontTheme = fontTheme;
            if (introSequenceData == null)
                introSequenceData = Resources.Load<IntroSequenceData>("IntroSequenceData");
            if (psxSettings == null)
                psxSettings = Resources.Load<PSXSettings>("PSXSettings");
            if (psxSettings != null)
                psxSettings.LoadFromPlayerPrefs();
            if (sourcesDatabase == null)
                sourcesDatabase = Resources.Load<SourcesDatabase>("SourcesDatabase");
            if (factDatabase == null)
                factDatabase = Resources.Load<FactDatabase>("FactDatabase");

            SimConfig cfg = configAsset != null ? configAsset.Config : new SimConfig();
            Session = new GameSession(cfg, missionSeed);

            bool deferEventForFlow = enableTitleIntroFlow && missionFlowController != null;

            if (terminalController != null)
            {
                terminalController.UseExternalEndingFlow = deferEventForFlow;
                terminalController.SetDeferInitialEvent(deferEventForFlow);
                terminalController.Initialize(Session, theme, sourcesDatabase);
                terminalController.SetM3Integration(firstPersonController != null, accessibilitySettings);
                terminalController.SetPsxSettings(psxSettings);
            }

            if (audioManager != null)
            {
                audioManager.EnsureInitialized();
                audioManager.ApplySnapshot(Session.CurrentSnapshot);
            }

            if (roomStateBinder != null)
                roomStateBinder.Initialize(Session, accessibilitySettings, audioManager);

            if (firstPersonController != null)
                firstPersonController.Initialize(Session, startInRoomView && !deferEventForFlow);

            if (psxRenderController != null && psxSettings != null)
                psxSettings.NotifyChanged();

            if (missionFlowController != null)
            {
                missionFlowController.Initialize(
                    Session,
                    theme,
                    sourcesDatabase,
                    factDatabase,
                    terminalController,
                    firstPersonController,
                    roomStateBinder,
                    playerGate,
                    audioManager,
                    accessibilitySettings,
                    enableTitleIntroFlow,
                    fontTheme,
                    introSequenceData,
                    psxSettings);
            }
        }
    }
}
