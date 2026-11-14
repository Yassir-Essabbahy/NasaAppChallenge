using System;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Coordinates center-screen prop raycasting (3m), room HUD prompts/fact banners,
    /// smooth 0.6s terminal focus transitions, Esc pause/leave routing, and exact restoration
    /// of the modular FPS controller's player pose and parented camera local transform.
    /// </summary>
    public class FirstPersonRoomController : MonoBehaviour
    {
        public enum ViewState
        {
            FlowLocked,
            RoomView,
            FocusingIn,
            TerminalView,
            FocusingOut
        }

        [Header("Player & Anchors")]
        [SerializeField] private PlayerGate playerGate;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform terminalFocusPoint;

        [Header("Dependencies")]
        [SerializeField] private TerminalController terminalController;
        [SerializeField] private AccessibilitySettings accessibility;
        [SerializeField] private AudioManager audioManager;

        [Header("Room HUD (Crosshair + Prompt + Fact Banner)")]
        [SerializeField] private GameObject roomHudRoot;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private UnityEngine.UI.Image crosshairIcon;
        [SerializeField] private UnityEngine.UI.Image promptLockIcon;
        [SerializeField] private GameObject factBannerRoot;
        [SerializeField] private TextMeshProUGUI factBannerText;

        [Header("Interaction & Focus Settings")]
        [SerializeField] private float raycastDistance = 3.0f;
        [SerializeField] private float leanDuration = 0.6f;

        [Header("Part 3 - World Teleports & Doors")]
        [SerializeField] private Transform corridorSpawnPoint;
        [SerializeField] private Transform airlockSpawnPoint;
        [SerializeField] private Transform surfaceSpawnPoint;
        [SerializeField] private AirlockController airlockController;
        [SerializeField] private DoorInteractable[] sceneDoors;

        private GameSession _session;
        private ViewState _state = ViewState.FlowLocked;
        private TerminalAppId? _pendingAppOnFocus;
        private bool _openedAsReadOnlyTablet;

        // Saved player & parented camera state for exact restoration after TerminalView
        private Vector3 _savedPlayerPos;
        private Quaternion _savedPlayerRot;
        private Transform _savedCamParent;
        private Vector3 _savedCamLocalPos;
        private Quaternion _savedCamLocalRot;
        private Vector3 _savedCamLocalScale;
        private float _savedCamFov;
        private Vector3 _savedJointLocalPos;
        private Quaternion _savedJointLocalRot;
        private bool _hasSavedPose;

        private Vector3 _transStartPos;
        private Quaternion _transStartRot;
        private float _transTimer;
        private float _factBannerTimer;
        private string _lastPromptStr = string.Empty;

        public ViewState CurrentViewState => _state;
        public Func<bool> ExternalLeaveBlockCheck { get; set; }
        public Func<bool> IsPauseMenuOpenCheck { get; set; }

        public event Action OnPauseRequested;
        public event Action OnEnteredTerminal;
        public event Action OnExitedTerminal;
        public event Action OnFlowLockDebugRequested;

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        public void Initialize(GameSession session, bool startInRoomView)
        {
            UnsubscribeEvents();
            _session = session;

            if (terminalController != null)
            {
                terminalController.OnLeaveTerminalRequested += HandleLeaveTerminalRequested;
                terminalController.OnDebugTeleportLeanRequested += TeleportLeanState;
                terminalController.OnUiSoundRequested += HandleUiSound;
                terminalController.OnDebugUnlockAllDoorsRequested += HandleDebugUnlockAllDoors;
                terminalController.OnDebugTeleportSpawnRequested += HandleDebugTeleportSpawn;
                terminalController.OnDebugTeleportCorridorRequested += HandleDebugTeleportCorridor;
                terminalController.OnDebugTeleportAirlockRequested += HandleDebugTeleportAirlock;
                terminalController.OnDebugTeleportSurfaceRequested += HandleDebugTeleportSurface;
            }

            if (airlockController != null)
                airlockController.BindRuntime(audioManager, ShowFactBanner);

            if (startInRoomView)
                EnterRoomViewImmediateForFlow();
            else
                EnterTerminalImmediateForFlow(null);
        }

        public void ConfigureReferences(
            PlayerGate gate,
            Transform spawnPoint,
            Transform focusPoint,
            TerminalController terminal,
            AccessibilitySettings acc,
            AudioManager audio,
            GameObject hudRoot,
            TextMeshProUGUI hoverPrompt,
            GameObject bannerRoot,
            TextMeshProUGUI factBanner)
        {
            playerGate = gate;
            playerSpawnPoint = spawnPoint;
            terminalFocusPoint = focusPoint;
            terminalController = terminal;
            accessibility = acc;
            audioManager = audio;
            roomHudRoot = hudRoot;
            promptText = hoverPrompt;
            factBannerRoot = bannerRoot;
            factBannerText = factBanner;
        }

        public void ConfigurePart3References(
            Transform corridorAnchor,
            Transform airlockAnchor,
            Transform surfaceAnchor,
            AirlockController airlock,
            DoorInteractable[] doors)
        {
            corridorSpawnPoint = corridorAnchor;
            airlockSpawnPoint = airlockAnchor;
            surfaceSpawnPoint = surfaceAnchor;
            airlockController = airlock;
            sceneDoors = doors;
        }

        private void UnsubscribeEvents()
        {
            if (terminalController != null)
            {
                terminalController.OnLeaveTerminalRequested -= HandleLeaveTerminalRequested;
                terminalController.OnDebugTeleportLeanRequested -= TeleportLeanState;
                terminalController.OnUiSoundRequested -= HandleUiSound;
                terminalController.OnDebugUnlockAllDoorsRequested -= HandleDebugUnlockAllDoors;
                terminalController.OnDebugTeleportSpawnRequested -= HandleDebugTeleportSpawn;
                terminalController.OnDebugTeleportCorridorRequested -= HandleDebugTeleportCorridor;
                terminalController.OnDebugTeleportAirlockRequested -= HandleDebugTeleportAirlock;
                terminalController.OnDebugTeleportSurfaceRequested -= HandleDebugTeleportSurface;
            }
        }

        /// <summary>
        /// Locks room/terminal control during Title, Intro, Endings, Debrief, or Pause Menu.
        /// </summary>
        public void SetFlowLocked(bool unlockCursor = true)
        {
            _state = ViewState.FlowLocked;
            SetRoomHudVisible(false);
            ClearFactBanner();
            if (terminalController != null)
                terminalController.SetTerminalVisible(false);
            if (playerGate != null)
                playerGate.EnableControl(false);
            if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void TeleportPlayerToSpawn()
        {
            if (playerGate == null || playerSpawnPoint == null) return;
            playerGate.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            CaptureReturnPose();
        }

        public void EnterTerminalImmediateForFlow(TerminalAppId? openApp)
        {
            if (playerGate != null)
            {
                if (_state == ViewState.RoomView || !_hasSavedPose)
                {
                    if (!_hasSavedPose && playerSpawnPoint != null)
                        playerGate.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
                    CaptureReturnPose();
                }
                playerGate.EnableControl(false);
            }

            Camera cam = GetPlayerCamera();
            if (cam != null && terminalFocusPoint != null)
                cam.transform.SetPositionAndRotation(terminalFocusPoint.position, terminalFocusPoint.rotation);

            _state = ViewState.TerminalView;
            SetRoomHudVisible(false);
            ClearFactBanner();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (terminalController != null)
            {
                terminalController.SetReadOnlyMode(_openedAsReadOnlyTablet);
                terminalController.SetTerminalVisible(true);
                if (openApp.HasValue && !_openedAsReadOnlyTablet)
                    terminalController.OpenApp(openApp.Value);
            }
            OnEnteredTerminal?.Invoke();
        }

        public void EnterRoomViewImmediateForFlow()
        {
            _openedAsReadOnlyTablet = false;
            if (terminalController != null)
            {
                terminalController.SetReadOnlyMode(false);
                terminalController.SetTerminalVisible(false);
            }

            RestoreReturnPose();
            _state = ViewState.RoomView;
            SetRoomHudVisible(true);
            ClearFactBanner();
            if (playerGate != null)
                playerGate.EnableControl(true);
        }

        private void Update()
        {
            // Backquote debug panel shortcut works across all active game states
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
            {
                if (terminalController != null)
                    terminalController.ToggleDebugPanel();
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                OnFlowLockDebugRequested?.Invoke();
            }

            UpdateFactBannerTimer();

            switch (_state)
            {
                case ViewState.RoomView:
                    UpdateRoomView();
                    break;
                case ViewState.FocusingIn:
                    UpdateFocusingIn();
                    break;
                case ViewState.TerminalView:
                    UpdateTerminalView();
                    break;
                case ViewState.FocusingOut:
                    UpdateFocusingOut();
                    break;
            }
        }

        private void UpdateRoomView()
        {
            if (IsPauseMenuOpenCheck != null && IsPauseMenuOpenCheck())
                return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                audioManager?.PlayUiClick();
                OnPauseRequested?.Invoke();
                return;
            }

            Camera cam = GetPlayerCamera();
            if (cam == null) return;

            InteractableProp hovered = null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                hovered = hit.collider.GetComponent<InteractableProp>();
                if (hovered == null)
                    hovered = hit.collider.GetComponentInParent<InteractableProp>();
            }

            string newPrompt = hovered != null ? hovered.GetDynamicPrompt(_session) : string.Empty;
            if (newPrompt != _lastPromptStr)
            {
                _lastPromptStr = newPrompt;
                if (promptText != null) promptText.text = newPrompt;
                if (crosshairIcon != null)
                {
                    var targetSpr = Outpost.Runtime.Terminal.TerminalUiArt.GetIcon("Icon_Target");
                    if (hovered != null && targetSpr != null)
                    {
                        crosshairIcon.sprite = targetSpr;
                        crosshairIcon.color = Outpost.Runtime.Terminal.UITheme.AccentPrimary;
                        crosshairIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);
                    }
                    else
                    {
                        crosshairIcon.sprite = null;
                        crosshairIcon.color = new Color(1f, 1f, 1f, 0.85f);
                        crosshairIcon.rectTransform.sizeDelta = new Vector2(6f, 6f);
                    }
                }
                if (promptLockIcon != null)
                {
                    bool locked = hovered != null && ((hovered.DoorTarget != null && hovered.DoorTarget.IsLocked) || newPrompt.IndexOf("LOCK", System.StringComparison.OrdinalIgnoreCase) >= 0);
                    var lockSpr = Outpost.Runtime.Terminal.TerminalUiArt.GetIcon("Icon_Lock");
                    promptLockIcon.gameObject.SetActive(locked && lockSpr != null);
                    if (lockSpr != null) promptLockIcon.sprite = lockSpr;
                    promptLockIcon.color = Outpost.Runtime.Terminal.UITheme.WarnStatic;
                }
            }

            if (kb != null && kb.eKey.wasPressedThisFrame && hovered != null)
                InteractWithProp(hovered);
        }

        private void InteractWithProp(InteractableProp prop)
        {
            var snap = _session?.CurrentSnapshot;
            switch (prop.PropType)
            {
                case InteractablePropType.Monitor:
                    _openedAsReadOnlyTablet = false;
                    audioManager?.PlayUiClick();
                    BeginTerminalFocus(null);
                    break;
                case InteractablePropType.Radio:
                    _openedAsReadOnlyTablet = false;
                    audioManager?.PlayRadioBlip();
                    BeginTerminalFocus(TerminalAppId.Comms);
                    break;
                case InteractablePropType.ReadOnlyTablet:
                    audioManager?.PlayUiClick();
                    OpenReadOnlyTablet();
                    break;
                case InteractablePropType.Window:
                    audioManager?.PlayUiClick();
                    ShowFactBanner(prop.MessageText, 4.8f);
                    break;
                case InteractablePropType.MedbayDoor:
                    audioManager?.PlayUiClick();
                    ShowFactBanner(prop.MessageText, 3.6f);
                    break;
                case InteractablePropType.Door:
                    if (prop.DoorTarget != null)
                    {
                        if (!prop.DoorTarget.TryToggle(audioManager, out string feedback) && !string.IsNullOrEmpty(feedback))
                            ShowFactBanner(feedback, 3.8f);
                    }
                    break;
                case InteractablePropType.AirlockCycle:
                    if (prop.AirlockTarget != null)
                    {
                        prop.AirlockTarget.TryCycleAirlock(snap, out string statusMsg);
                        if (!string.IsNullOrEmpty(statusMsg))
                            ShowFactBanner(statusMsg, 3.5f);
                    }
                    break;
                case InteractablePropType.GreenhousePlanter:
                    audioManager?.PlayUiClick();
                    if (snap != null)
                        ShowFactBanner(Localization.Format("poi.greenhouse.fact", snap.CropHealthPct, snap.Food), 5.2f);
                    break;
                case InteractablePropType.StorageWaterTank:
                    audioManager?.PlayUiClick();
                    if (snap != null)
                        ShowFactBanner(Localization.Format("poi.water.fact", snap.Water), 5.2f);
                    break;
                case InteractablePropType.StorageOxygenTank:
                    audioManager?.PlayUiClick();
                    if (snap != null)
                        ShowFactBanner(Localization.Format("poi.oxygen.fact", snap.Oxygen, snap.ScrubberHealthPct), 5.2f);
                    break;
                case InteractablePropType.MedbayPatient:
                    audioManager?.PlayUiClick();
                    if (snap != null)
                    {
                        float valeHp = 0f;
                        for (int i = 0; i < snap.Crew.Count; i++)
                        {
                            if (snap.Crew[i].IsFallenCommander) { valeHp = snap.Crew[i].IsAlive ? snap.Crew[i].Health : 0f; break; }
                        }
                        ShowFactBanner(Localization.Format("poi.vale.status", valeHp), 5.0f);
                    }
                    break;
                case InteractablePropType.SurfaceSolarArray:
                    HandleSolarArrayInteraction(snap);
                    break;
                case InteractablePropType.SurfaceRover:
                    audioManager?.PlayUiClick();
                    ShowFactBanner(!string.IsNullOrEmpty(prop.CustomMessageText) ? prop.CustomMessageText : Localization.Get("poi.rover.fact"), 5.2f);
                    break;
                case InteractablePropType.CustomFact:
                    audioManager?.PlayUiClick();
                    ShowFactBanner(prop.MessageText, 5.2f);
                    break;
                case InteractablePropType.SurfaceAntenna:
                    audioManager?.PlayRadioBlip();
                    if (snap != null)
                    {
                        string linkState = snap.CommsOnline ? Localization.Get("status.word.good") : Localization.Get("status.word.low");
                        ShowFactBanner(Localization.Format("poi.antenna.fact", linkState), 5.2f);
                    }
                    break;
                case InteractablePropType.SurfaceRadiationPost:
                    audioManager?.PlayUiClick();
                    if (snap != null)
                        ShowFactBanner(Localization.Format("poi.rad.fact", snap.ProjectedDoseMsv), 5.2f);
                    break;
                case InteractablePropType.SurfaceIceMarker:
                    audioManager?.PlayUiClick();
                    ShowFactBanner(Localization.Get("poi.ice.fact"), 5.2f);
                    break;
            }
        }

        private void HandleSolarArrayInteraction(SessionSnapshot snap)
        {
            if (snap == null) return;
            bool canClean = !snap.SolarFlareActive &&
                            snap.DustStormSolsRemaining == 0 &&
                            snap.ActionPoints > 0 &&
                            snap.PanelDustPct > 5f &&
                            snap.Status == MissionStatus.InProgress;

            if (canClean && _session.DoAction(CrewActionType.CleanPanels))
            {
                audioManager?.PlayUiClick();
                float newDust = _session.CurrentSnapshot != null ? _session.CurrentSnapshot.PanelDustPct : 0f;
                ShowFactBanner(Localization.Format("poi.solar.cleaned_toast", newDust), 4.6f);
            }
            else
            {
                audioManager?.PlayUiClick();
                ShowFactBanner(Localization.Format("poi.solar.fact", snap.PanelDustPct, snap.SolarGeneration), 5.4f);
            }
        }

        public void OpenReadOnlyTablet()
        {
            if (_state != ViewState.RoomView) return;
            _openedAsReadOnlyTablet = true;
            CaptureReturnPose();
            if (playerGate != null)
                playerGate.EnableControl(false);

            _state = ViewState.TerminalView;
            SetRoomHudVisible(false);
            ClearFactBanner();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (terminalController != null)
            {
                terminalController.SetReadOnlyMode(true);
                terminalController.SetTerminalVisible(true);
            }
            OnEnteredTerminal?.Invoke();
        }

        public void BeginTerminalFocus(TerminalAppId? openApp)
        {
            if (_state != ViewState.RoomView) return;

            CaptureReturnPose();
            if (playerGate != null)
                playerGate.EnableControl(false);

            Camera cam = GetPlayerCamera();
            if (cam == null || terminalFocusPoint == null)
            {
                EnterTerminalImmediateForFlow(openApp);
                return;
            }

            _pendingAppOnFocus = openApp;
            _transStartPos = cam.transform.position;
            _transStartRot = cam.transform.rotation;
            _transTimer = 0f;
            _state = ViewState.FocusingIn;

            SetRoomHudVisible(false);
            ClearFactBanner();
        }

        private void UpdateFocusingIn()
        {
            _transTimer += Time.unscaledDeltaTime;
            float t = leanDuration > 0.001f ? Mathf.Clamp01(_transTimer / leanDuration) : 1f;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            Camera cam = GetPlayerCamera();
            if (cam != null && terminalFocusPoint != null)
            {
                cam.transform.position = Vector3.Lerp(_transStartPos, terminalFocusPoint.position, smooth);
                cam.transform.rotation = Quaternion.Slerp(_transStartRot, terminalFocusPoint.rotation, smooth);
            }

            if (t >= 1f)
            {
                EnterTerminalImmediateForFlow(_pendingAppOnFocus);
                _pendingAppOnFocus = null;
            }
        }

        private void UpdateTerminalView()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                RequestLeaveTerminal();
        }

        public void RequestLeaveTerminal()
        {
            if (_state != ViewState.TerminalView) return;
            if (ExternalLeaveBlockCheck != null && ExternalLeaveBlockCheck())
                return;

            if (terminalController != null && terminalController.IsModalBlockingLeanBack)
            {
                audioManager?.PlayRadioBlip();
                terminalController.ShowModalBlockWarning();
                return;
            }

            audioManager?.PlayUiClick();
            BeginLeaveTerminal();
        }

        public void BeginLeaveTerminal()
        {
            bool wasReadOnlyTablet = _openedAsReadOnlyTablet;
            _openedAsReadOnlyTablet = false;

            if (terminalController != null)
            {
                terminalController.SetReadOnlyMode(false);
                terminalController.SetTerminalVisible(false);
            }

            Camera cam = GetPlayerCamera();
            if (wasReadOnlyTablet || cam == null || !_hasSavedPose)
            {
                EnterRoomViewImmediateForFlow();
                if (wasReadOnlyTablet)
                    ShowFactBanner(Localization.Get("tablet.closed_toast"), 4.2f);
                OnExitedTerminal?.Invoke();
                return;
            }

            _transStartPos = cam.transform.position;
            _transStartRot = cam.transform.rotation;
            _transTimer = 0f;
            _state = ViewState.FocusingOut;
        }

        private void UpdateFocusingOut()
        {
            _transTimer += Time.unscaledDeltaTime;
            float t = leanDuration > 0.001f ? Mathf.Clamp01(_transTimer / leanDuration) : 1f;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            Camera cam = GetPlayerCamera();
            if (cam != null)
            {
                Vector3 targetWorldPos = _savedCamParent != null
                    ? _savedCamParent.TransformPoint(_savedCamLocalPos)
                    : _savedPlayerPos;
                Quaternion targetWorldRot = _savedCamParent != null
                    ? _savedCamParent.rotation * _savedCamLocalRot
                    : _savedPlayerRot;

                cam.transform.position = Vector3.Lerp(_transStartPos, targetWorldPos, smooth);
                cam.transform.rotation = Quaternion.Slerp(_transStartRot, targetWorldRot, smooth);
            }

            if (t >= 1f)
            {
                RestoreReturnPose();
                _state = ViewState.RoomView;
                SetRoomHudVisible(true);
                if (playerGate != null)
                    playerGate.EnableControl(true);
                OnExitedTerminal?.Invoke();
            }
        }

        public void TeleportLeanState(bool focusTerminal)
        {
            if (focusTerminal)
                EnterTerminalImmediateForFlow(null);
            else
                EnterRoomViewImmediateForFlow();
        }

        private void HandleDebugUnlockAllDoors()
        {
            if (airlockController != null)
                airlockController.DebugUnlock();
            if (sceneDoors != null)
            {
                for (int i = 0; i < sceneDoors.Length; i++)
                {
                    if (sceneDoors[i] != null)
                        sceneDoors[i].DebugUnlock();
                }
            }
            ShowFactBanner("DEBUG: All doors & airlock unlocked.", 3.0f);
        }

        private void HandleDebugTeleportSpawn() => TeleportToAnchor(playerSpawnPoint, depressurizeAirlock: false);
        private void HandleDebugTeleportCorridor() => TeleportToAnchor(corridorSpawnPoint, depressurizeAirlock: false);
        private void HandleDebugTeleportAirlock() => TeleportToAnchor(airlockSpawnPoint, depressurizeAirlock: false);
        private void HandleDebugTeleportSurface() => TeleportToAnchor(surfaceSpawnPoint, depressurizeAirlock: true);

        private void TeleportToAnchor(Transform anchor, bool depressurizeAirlock)
        {
            if (anchor == null || playerGate == null) return;

            _openedAsReadOnlyTablet = false;
            if (terminalController != null)
            {
                terminalController.SetReadOnlyMode(false);
                terminalController.SetTerminalVisible(false);
            }

            if (airlockController != null)
                airlockController.SetDepressurizedImmediateForTeleport(depressurizeAirlock);

            RestoreReturnPose();
            playerGate.TeleportTo(anchor.position, anchor.rotation);
            CaptureReturnPose();

            _state = ViewState.RoomView;
            SetRoomHudVisible(true);
            bool debugVisible = terminalController != null && terminalController.IsDebugPanelVisible;
            playerGate.EnableControl(!debugVisible);
            if (debugVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void CaptureReturnPose()
        {
            if (playerGate != null && playerGate.PlayerTransform != null)
            {
                _savedPlayerPos = playerGate.PlayerTransform.position;
                _savedPlayerRot = playerGate.PlayerTransform.rotation;
            }
            if (playerGate != null && playerGate.JointTransform != null)
            {
                _savedJointLocalPos = playerGate.JointTransform.localPosition;
                _savedJointLocalRot = playerGate.JointTransform.localRotation;
            }
            Camera cam = GetPlayerCamera();
            if (cam != null)
            {
                _savedCamParent = cam.transform.parent;
                _savedCamLocalPos = cam.transform.localPosition;
                _savedCamLocalRot = cam.transform.localRotation;
                _savedCamLocalScale = cam.transform.localScale;
                _savedCamFov = cam.fieldOfView;
            }
            _hasSavedPose = true;
        }

        private void RestoreReturnPose()
        {
            if (!_hasSavedPose) return;

            if (playerGate != null && playerGate.PlayerTransform != null)
                playerGate.PlayerTransform.SetPositionAndRotation(_savedPlayerPos, _savedPlayerRot);

            if (playerGate != null && playerGate.JointTransform != null)
            {
                playerGate.JointTransform.localPosition = _savedJointLocalPos;
                playerGate.JointTransform.localRotation = _savedJointLocalRot;
            }

            Camera cam = GetPlayerCamera();
            if (cam != null)
            {
                if (_savedCamParent != null && cam.transform.parent != _savedCamParent)
                    cam.transform.SetParent(_savedCamParent, false);
                cam.transform.localPosition = _savedCamLocalPos;
                cam.transform.localRotation = _savedCamLocalRot;
                cam.transform.localScale = _savedCamLocalScale;
                cam.fieldOfView = _savedCamFov;
            }
        }

        private Camera GetPlayerCamera() => playerGate != null ? playerGate.PlayerCamera : null;

        private void HandleLeaveTerminalRequested() => RequestLeaveTerminal();

        private void HandleUiSound() => audioManager?.PlayUiClick();

        private void SetRoomHudVisible(bool visible)
        {
            if (roomHudRoot != null)
                roomHudRoot.SetActive(visible);
            if (!visible)
            {
                _lastPromptStr = string.Empty;
                if (promptText != null) promptText.text = string.Empty;
            }
        }

        private void ShowFactBanner(string message, float duration)
        {
            if (factBannerRoot == null || factBannerText == null) return;
            factBannerText.text = message;
            factBannerRoot.SetActive(true);
            _factBannerTimer = duration;
        }

        private void ClearFactBanner()
        {
            _factBannerTimer = 0f;
            if (factBannerRoot != null)
                factBannerRoot.SetActive(false);
        }

        private void UpdateFactBannerTimer()
        {
            if (_factBannerTimer <= 0f) return;
            _factBannerTimer -= Time.unscaledDeltaTime;
            if (_factBannerTimer <= 0f && factBannerRoot != null)
                factBannerRoot.SetActive(false);
        }
    }
}
