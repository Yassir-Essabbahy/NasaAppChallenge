using System;
using System.Collections;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Singleton controller mounted alongside PlayerGate on OutpostControllers.
    /// Manages wearing/removing the EVA suit (rack [E] + free toggle key T), 1.2s toggle sequence,
    /// aspect-safe procedural visor overlay + cosmetic Suit HUD (never touches Outpost.Sim),
    /// subtle FOV narrowing, muffle volume scaling (0.6x ambient/announcer), outside/airlock removal block,
    /// and optional first-person primitive arms under the camera.
    /// </summary>
    public class SuitController : MonoBehaviour
    {
        public const int VisorCanvasSortingOrder = 7; // Above WorldView (-100) & RoomHud (5); below Terminal (10), Flow (30), Subtitles (45), BlackCard (80), Pause (90)

        public static SuitController Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private PlayerGate playerGate;
        [SerializeField] private FirstPersonRoomController roomController;
        [SerializeField] private AirlockController airlockController;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private AnnouncerSystem announcerSystem;
        [SerializeField] private AccessibilitySettings accessibility;
        [SerializeField] private MissionFlowController missionFlowController;
        [SerializeField] private TerminalController terminalController;

        [Header("Scene Rack Visual & Visor")]
        [SerializeField] private GameObject propSuitVisual;
        [SerializeField] private UITheme theme;
        [SerializeField] private Sprite visorSprite;
        [SerializeField] private bool showFirstPersonArms = false;

        [Header("Suit Audio Slots (empty clips allowed)")]
        [SerializeField] private AudioClip suitSealClip;
        [SerializeField] private AudioClip suitBreathingLoopClip;
        [Range(0f, 1f)] [SerializeField] private float suitBreathingVolume = 0.20f;

        [Header("Sequence Tuning")]
        [SerializeField] private float toggleTotalDuration = 1.2f;
        [SerializeField] private float visorSlideDuration = 0.5f;
        [SerializeField] private float suitFovDelta = -3.0f;
        [SerializeField] private float outerAirlockDoorZ = -17.36f;

        private bool _isWearingSuit;
        private Coroutine _toggleRoutine;
        private bool _overlayBuilt;

        // SuitVisorCanvas & HUD elements
        private Canvas _visorCanvas;
        private RectTransform _visorSlideRoot;
        private RectTransform _visorCenterFrame;
        private Image _visorOverlayImage;
        private Image _leftMatte;
        private Image _rightMatte;
        private Image _topMatte;
        private Image _bottomMatte;
        private AccessibilityTextScaler _suitTextScaler;
        private int _lastScreenW = -1;
        private int _lastScreenH = -1;

        // COSMETIC ONLY HUD widgets (never read/write Outpost.Sim)
        private Image _hudO2Bar;
        private Image _hudBattBar;
        private TextMeshProUGUI _hudStatusLabel;
        private TextMeshProUGUI _hudO2Val;
        private TextMeshProUGUI _hudBattVal;
        private TextMeshProUGUI _hudTempVal;
        private float _cosmeticClock;
        private float _hudRefreshAccum;
        private int _lastShownO2Pct = -1;
        private int _lastShownBattPct = -1;
        private int _lastShownTempC = int.MinValue;

        // Optional simple first-person primitive arms under PlayerCamera
        private Transform _armsRoot;
        private Vector3 _armsRestLocalPos = new Vector3(0f, -0.02f, 0f);

        public bool IsWearingSuit => _isWearingSuit;
        public bool IsTransitioning => _toggleRoutine != null;

        public event Action<bool> OnSuitStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ResolveMissingDependencies();
            SubscribeEvents();
            EnsureOverlayBuilt();
            ApplySuitVisualStateImmediate(visibleInRoom: false, wearing: false);
            SyncAccessibility();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this)
                Instance = null;
        }

        public void ConfigureReferences(
            PlayerGate gate,
            FirstPersonRoomController roomCtrl,
            AirlockController airlockCtrl,
            AudioManager audio,
            AnnouncerSystem announcer,
            AccessibilitySettings acc,
            MissionFlowController flowCtrl,
            TerminalController termCtrl,
            GameObject propSuit,
            UITheme uiTheme,
            Sprite visor)
        {
            if (gate != null) playerGate = gate;
            if (roomCtrl != null) roomController = roomCtrl;
            if (airlockCtrl != null) airlockController = airlockCtrl;
            if (audio != null) audioManager = audio;
            if (announcer != null) announcerSystem = announcer;
            if (acc != null) accessibility = acc;
            if (flowCtrl != null) missionFlowController = flowCtrl;
            if (termCtrl != null) terminalController = termCtrl;
            if (propSuit != null) propSuitVisual = propSuit;
            if (uiTheme != null) theme = uiTheme;
            if (visor != null) visorSprite = visor;
        }

        public void SetPropSuitVisual(GameObject propSuit)
        {
            propSuitVisual = propSuit;
            if (propSuitVisual != null)
                propSuitVisual.SetActive(!_isWearingSuit);
        }

        /// <summary>
        /// Pure test-friendly rule evaluator for suit removal permission.
        /// Disallows removal outside the pressurized habitat (beyond outer airlock door / depressurized airlock) or while airlock is cycling.
        /// </summary>
        public static bool EvaluateSuitRemovalAllowed(bool isAirlockCycling, bool isDepressurized, float playerZ, float outerDoorZ = -17.36f)
        {
            if (isAirlockCycling) return false;
            if (isDepressurized) return false;
            if (playerZ < outerDoorZ) return false;
            return true;
        }

        public bool CanRemoveSuit(out string denyReasonKey)
        {
            bool cycling = airlockController != null && airlockController.IsCycling;
            bool depressurized = airlockController != null && airlockController.IsDepressurized;
            float playerZ = playerGate != null && playerGate.PlayerTransform != null
                ? playerGate.PlayerTransform.position.z
                : 0f;
            bool onSurface = playerGate != null && playerGate.IsStandingOnExteriorSurface();

            if (!EvaluateSuitRemovalAllowed(cycling, depressurized, playerZ, outerAirlockDoorZ) || onSurface)
            {
                denyReasonKey = "suit_cannot_remove";
                return false;
            }

            denyReasonKey = string.Empty;
            return true;
        }

        public string GetRackPromptText()
        {
            return Localization.Get(_isWearingSuit ? "suit_take_off" : "suit_put_on");
        }

        public bool TryToggleSuit(bool viaRack)
        {
            if (IsTransitioning) return false;
            if (!CanInteractInRoomNow()) return false;

            if (_isWearingSuit && !CanRemoveSuit(out string denyKey))
            {
                audioManager?.PlayDoorLocked();
                roomController?.ShowRoomToast(Localization.Get(denyKey), 3.8f);
                return false;
            }

            _toggleRoutine = StartCoroutine(ToggleSuitRoutine(!_isWearingSuit));
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugForceSetSuit(bool wear)
        {
            if (_toggleRoutine != null)
            {
                StopCoroutine(_toggleRoutine);
                _toggleRoutine = null;
            }

            ApplyImmediateSuitState(wear);
        }
#endif

        public void ForceRemoveSuitImmediate()
        {
            if (_toggleRoutine != null)
            {
                StopCoroutine(_toggleRoutine);
                _toggleRoutine = null;
            }

            ApplyImmediateSuitState(false);
        }

        private void ApplyImmediateSuitState(bool wear)
        {
            _isWearingSuit = wear;
            if (propSuitVisual != null)
                propSuitVisual.SetActive(!wear);

            audioManager?.SetSuitMuffled(wear);
            audioManager?.SetSuitBreathingActive(wear, suitBreathingLoopClip, suitBreathingVolume);
            announcerSystem?.SetSuitMuffleScale(wear ? 0.6f : 1.0f);

            float fovOffset = (wear && !IsReduceMotionActive()) ? suitFovDelta : 0f;
            playerGate?.SetSuitFovDelta(fovOffset);

            EnsureOverlayBuilt();
            if (_visorSlideRoot != null)
                _visorSlideRoot.anchoredPosition = Vector2.zero;

            bool roomVisible = wear && ShouldVisorShowInCurrentState();
            ApplySuitVisualStateImmediate(roomVisible, wear);
            OnSuitStateChanged?.Invoke(_isWearingSuit);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame && CanUseHotkeyNow())
            {
                TryToggleSuit(viaRack: false);
            }

            if (!_isWearingSuit) return;

            bool shouldShow = ShouldVisorShowInCurrentState();
            if (_visorCanvas != null && _visorCanvas.enabled != shouldShow && !IsTransitioning)
                _visorCanvas.enabled = shouldShow;
            if (_armsRoot != null && _armsRoot.gameObject.activeSelf != (showFirstPersonArms && shouldShow))
                _armsRoot.gameObject.SetActive(showFirstPersonArms && shouldShow);

            if (shouldShow)
            {
                UpdateMatteFramingIfScreenResized();
                UpdateCosmeticSuitHud();
                UpdateArmsSway();
            }
        }

        private bool CanInteractInRoomNow()
        {
            if (missionFlowController != null && missionFlowController.CurrentState != MissionFlowController.FlowState.Gameplay)
                return false;
            if (roomController != null && roomController.CurrentViewState != FirstPersonRoomController.ViewState.RoomView)
                return false;
            return true;
        }

        private bool CanUseHotkeyNow()
        {
            if (IsTransitioning) return false;
            if (terminalController != null && (terminalController.IsTerminalVisible || terminalController.IsDebugPanelVisible))
                return false;
            return CanInteractInRoomNow();
        }

        private bool ShouldVisorShowInCurrentState()
        {
            if (!_isWearingSuit) return false;
            if (missionFlowController != null && missionFlowController.CurrentState != MissionFlowController.FlowState.Gameplay)
                return false;
            if (roomController != null && roomController.CurrentViewState != FirstPersonRoomController.ViewState.RoomView)
                return false;
            return true;
        }

        private bool IsReduceMotionActive()
        {
            return accessibility != null && (accessibility.ReduceMotion || accessibility.ReduceFlicker);
        }

        private IEnumerator ToggleSuitRoutine(bool targetWear)
        {
            EnsureOverlayBuilt();
            playerGate?.EnableControl(false);
            audioManager?.PlaySuitSeal(suitSealClip);

            float totalDur = Mathf.Max(0.6f, toggleTotalDuration);
            float slideDur = Mathf.Clamp(visorSlideDuration, 0.1f, totalDur);
            float preDelay = targetWear ? 0.30f : 0.25f;
            float postDelay = Mathf.Max(0.05f, totalDur - preDelay - slideDur);

            if (preDelay > 0f)
                yield return WaitRealSeconds(preDelay);

            if (targetWear)
            {
                if (propSuitVisual != null) propSuitVisual.SetActive(false);
                if (_visorCanvas != null) _visorCanvas.enabled = true;
                if (_visorSlideRoot != null)
                {
                    float offscreenY = GetSlideOffsetPixels();
                    _visorSlideRoot.anchoredPosition = new Vector2(0f, offscreenY);
                }
            }

            float elapsed = 0f;
            float startY = targetWear ? GetSlideOffsetPixels() : 0f;
            float endY = targetWear ? 0f : GetSlideOffsetPixels();
            float baseDeltaFov = _isWearingSuit && !IsReduceMotionActive() ? suitFovDelta : 0f;
            float targetDeltaFov = targetWear && !IsReduceMotionActive() ? suitFovDelta : 0f;

            while (elapsed < slideDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / slideDur);
                float smooth = Mathf.SmoothStep(0f, 1f, u);

                if (_visorSlideRoot != null)
                    _visorSlideRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, smooth));

                if (!IsReduceMotionActive())
                    playerGate?.SetSuitFovDelta(Mathf.Lerp(baseDeltaFov, targetDeltaFov, smooth));

                yield return null;
            }

            if (_visorSlideRoot != null)
                _visorSlideRoot.anchoredPosition = new Vector2(0f, endY);

            if (postDelay > 0f)
                yield return WaitRealSeconds(postDelay);

            _isWearingSuit = targetWear;
            if (propSuitVisual != null) propSuitVisual.SetActive(!targetWear);

            audioManager?.SetSuitMuffled(targetWear);
            audioManager?.SetSuitBreathingActive(targetWear, suitBreathingLoopClip, suitBreathingVolume);
            announcerSystem?.SetSuitMuffleScale(targetWear ? 0.6f : 1.0f);
            playerGate?.SetSuitFovDelta(targetWear && !IsReduceMotionActive() ? suitFovDelta : 0f);

            bool showVisor = targetWear && ShouldVisorShowInCurrentState();
            ApplySuitVisualStateImmediate(showVisor, targetWear);

            _toggleRoutine = null;
            OnSuitStateChanged?.Invoke(_isWearingSuit);

            // Re-enable control cleanly only if still in RoomView & gameplay
            if (CanInteractInRoomNow())
                playerGate?.EnableControl(true);
        }

        private float GetSlideOffsetPixels()
        {
            if (_visorCanvas == null) return 1080f;
            var rt = _visorCanvas.transform as RectTransform;
            float h = rt != null && rt.rect.height > 10f ? rt.rect.height : 1080f;
            return h * 1.05f;
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

        private void ApplySuitVisualStateImmediate(bool visibleInRoom, bool wearing)
        {
            if (_visorCanvas != null)
            {
                _visorCanvas.enabled = visibleInRoom;
                if (_visorSlideRoot != null && visibleInRoom)
                    _visorSlideRoot.anchoredPosition = Vector2.zero;
            }

            if (propSuitVisual != null)
                propSuitVisual.SetActive(!wearing);

            if (_armsRoot != null)
                _armsRoot.gameObject.SetActive(showFirstPersonArms && visibleInRoom && wearing);

            if (visibleInRoom && wearing)
            {
                _lastScreenW = -1;
                UpdateMatteFramingIfScreenResized();
                _hudRefreshAccum = 1f;
                UpdateCosmeticSuitHud();
            }
        }

        private void HandleEnteredTerminal()
        {
            if (_visorCanvas != null) _visorCanvas.enabled = false;
            if (_armsRoot != null) _armsRoot.gameObject.SetActive(false);
        }

        private void HandleExitedTerminal()
        {
            if (!_isWearingSuit) return;
            bool show = ShouldVisorShowInCurrentState();
            ApplySuitVisualStateImmediate(show, wearing: true);
            playerGate?.SetSuitFovDelta(!IsReduceMotionActive() ? suitFovDelta : 0f);
        }

        private void HandleAccessibilityChanged()
        {
            SyncAccessibility();
        }

        private void SyncAccessibility()
        {
            bool largeText = accessibility != null && accessibility.LargerText;
            if (_suitTextScaler != null)
                _suitTextScaler.SetLargerText(largeText);

            if (_isWearingSuit)
                playerGate?.SetSuitFovDelta(!IsReduceMotionActive() ? suitFovDelta : 0f);
        }

        private void UpdateCosmeticSuitHud()
        {
            // COSMETIC ONLY: values drift slowly for EVA immersion; never reads from or writes to Outpost.Sim.
            _cosmeticClock += Time.unscaledDeltaTime;
            _hudRefreshAccum += Time.unscaledDeltaTime;
            if (_hudRefreshAccum < 0.4f && _lastShownO2Pct >= 0) return;
            _hudRefreshAccum = 0f;

            float o2Float = 97.4f + Mathf.Sin(_cosmeticClock * 0.13f) * 1.4f;
            float battFloat = 93.8f + Mathf.Cos(_cosmeticClock * 0.09f) * 2.1f;
            float tempFloat = -63.0f + Mathf.Sin(_cosmeticClock * 0.17f) * 2.2f;

            int o2Pct = Mathf.Clamp(Mathf.RoundToInt(o2Float), 90, 100);
            int battPct = Mathf.Clamp(Mathf.RoundToInt(battFloat), 85, 100);
            int tempC = Mathf.RoundToInt(tempFloat);

            if (_hudO2Bar != null)
                _hudO2Bar.rectTransform.anchorMax = new Vector2(o2Pct / 100f, 1f);
            if (_hudBattBar != null)
                _hudBattBar.rectTransform.anchorMax = new Vector2(battPct / 100f, 1f);

            if (o2Pct != _lastShownO2Pct && _hudO2Val != null)
            {
                _lastShownO2Pct = o2Pct;
                _hudO2Val.text = o2Pct.ToString() + "%";
            }

            if (battPct != _lastShownBattPct && _hudBattVal != null)
            {
                _lastShownBattPct = battPct;
                _hudBattVal.text = battPct.ToString() + "%";
            }

            if (tempC != _lastShownTempC && _hudTempVal != null)
            {
                _lastShownTempC = tempC;
                _hudTempVal.text = Localization.ShapeDisplay($"{Localization.GetRaw("suit.hud.ext")} {tempC} C");
            }
        }

        private void UpdateArmsSway()
        {
            if (_armsRoot == null) return;
            if (IsReduceMotionActive())
            {
                _armsRoot.localPosition = _armsRestLocalPos;
                return;
            }

            float t = Time.time;
            float swayX = Mathf.Sin(t * 1.4f) * 0.0045f;
            float swayY = Mathf.Sin(t * 2.8f) * 0.0040f;
            _armsRoot.localPosition = _armsRestLocalPos + new Vector3(swayX, swayY, 0f);
        }

        private void UpdateMatteFramingIfScreenResized()
        {
            if (_visorCenterFrame == null || _visorCanvas == null) return;
            int w = Screen.width;
            int h = Screen.height;
            if (w == _lastScreenW && h == _lastScreenH) return;
            _lastScreenW = w;
            _lastScreenH = h;

            var rootRt = _visorSlideRoot;
            if (rootRt == null || w <= 0 || h <= 0) return;

            const float targetAspect = 16f / 9f;
            float screenAspect = w / (float)h;
            float rootW = rootRt.rect.width > 1f ? rootRt.rect.width : 1920f;
            float rootH = rootRt.rect.height > 1f ? rootRt.rect.height : 1080f;

            // Keep 1:1 square center visor viewport; fill outside edges cleanly with matching dark helmet rim matte
            if (screenAspect > targetAspect + 0.01f)
            {
                float fittedW = rootH * targetAspect;
                float barFrac = Mathf.Max(0f, (rootW - fittedW) * 0.5f / rootW);
                SetMatte(_leftMatte, 0f, 0f, barFrac + 0.003f, 1f, barFrac > 0.001f);
                SetMatte(_rightMatte, 1f - barFrac - 0.003f, 0f, 1f, 1f, barFrac > 0.001f);
                SetMatte(_topMatte, 0f, 1f, 1f, 1f, false);
                SetMatte(_bottomMatte, 0f, 0f, 1f, 0f, false);
            }
            else if (screenAspect < targetAspect - 0.01f)
            {
                float fittedH = rootW / targetAspect;
                float barFrac = Mathf.Max(0f, (rootH - fittedH) * 0.5f / rootH);
                SetMatte(_topMatte, 0f, 1f - barFrac - 0.003f, 1f, 1f, barFrac > 0.001f);
                SetMatte(_bottomMatte, 0f, 0f, 1f, barFrac + 0.003f, barFrac > 0.001f);
                SetMatte(_leftMatte, 0f, 0f, 0f, 1f, false);
                SetMatte(_rightMatte, 1f, 0f, 1f, 1f, false);
            }
            else
            {
                SetMatte(_leftMatte, 0f, 0f, 0f, 1f, false);
                SetMatte(_rightMatte, 1f, 0f, 1f, 1f, false);
                SetMatte(_topMatte, 0f, 1f, 1f, 1f, false);
                SetMatte(_bottomMatte, 0f, 0f, 1f, 0f, false);
            }
        }

        private static void SetMatte(Image img, float minX, float minY, float maxX, float maxY, bool active)
        {
            if (img == null) return;
            img.gameObject.SetActive(active);
            if (!active) return;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void EnsureOverlayBuilt()
        {
            if (_overlayBuilt) return;
            _overlayBuilt = true;

            if (theme == null) theme = Resources.Load<UITheme>("TerminalTheme") ?? ScriptableObject.CreateInstance<UITheme>();
            if (visorSprite == null) visorSprite = Resources.Load<Sprite>("T_SuitVisor_1024x576");

            var canvasGo = new GameObject("SuitVisorCanvas", typeof(Canvas), typeof(CanvasScaler));
            _visorCanvas = canvasGo.GetComponent<Canvas>();
            _visorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _visorCanvas.sortingOrder = VisorCanvasSortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _suitTextScaler = canvasGo.AddComponent<AccessibilityTextScaler>();
            _visorCanvas.enabled = false;

            var slideGo = new GameObject("VisorSlideRoot", typeof(RectTransform));
            slideGo.transform.SetParent(canvasGo.transform, false);
            _visorSlideRoot = slideGo.GetComponent<RectTransform>();
            TerminalUiBuilder.SetStretch(_visorSlideRoot);

            Color helmetDark = new Color(0.036f, 0.056f, 0.078f, 0.985f);
            _leftMatte = CreateEdgeMatte("LeftMatte", _visorSlideRoot, helmetDark);
            _rightMatte = CreateEdgeMatte("RightMatte", _visorSlideRoot, helmetDark);
            _topMatte = CreateEdgeMatte("TopMatte", _visorSlideRoot, helmetDark);
            _bottomMatte = CreateEdgeMatte("BottomMatte", _visorSlideRoot, helmetDark);

            var centerGo = new GameObject("VisorCenterFrame", typeof(RectTransform), typeof(AspectRatioFitter));
            centerGo.transform.SetParent(_visorSlideRoot, false);
            _visorCenterFrame = centerGo.GetComponent<RectTransform>();
            TerminalUiBuilder.SetStretch(_visorCenterFrame);

            var fitter = centerGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1024f / 576f;

            var imgGo = new GameObject("VisorOverlay", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(_visorCenterFrame, false);
            TerminalUiBuilder.SetStretch(imgGo.GetComponent<RectTransform>());
            _visorOverlayImage = imgGo.GetComponent<Image>();
            _visorOverlayImage.sprite = visorSprite;
            _visorOverlayImage.color = visorSprite != null ? Color.white : new Color(0.08f, 0.14f, 0.18f, 0.25f);
            _visorOverlayImage.raycastTarget = false;

            BuildCosmeticSuitHud(_visorCenterFrame);
            BuildFirstPersonArmsIfNeeded();
        }

        private static Image CreateEdgeMatte(string name, RectTransform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            go.SetActive(false);
            return img;
        }

        private void BuildCosmeticSuitHud(RectTransform parent)
        {
            // Sits neatly inside bottom-left visor viewport, clear of ~12% helmet border & chin piece
            var panel = TerminalUiBuilder.CreateFlatPanel(
                "SuitHudPanel",
                parent,
                new Color(0.025f, 0.055f, 0.085f, 0.78f),
                new Color(0.24f, 0.78f, 0.85f, 0.85f),
                1.5f);
            TerminalUiBuilder.SetAnchors(panel, 0.132f, 0.165f, 0.312f, 0.315f);
            var inner = TerminalUiBuilder.GetInner(panel);
            DisableRaycastsRecursively(panel.gameObject);

            // Row 1: Status light + "SUIT OK"
            var dotGo = new GameObject("OkDot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(inner, false);
            var dotRt = dotGo.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.04f, 0.77f);
            dotRt.anchorMax = new Vector2(0.075f, 0.89f);
            dotRt.offsetMin = Vector2.zero;
            dotRt.offsetMax = Vector2.zero;
            dotGo.GetComponent<Image>().color = theme.StatusOk;

            _hudStatusLabel = TerminalUiBuilder.CreateText("SuitOkText", inner, theme, 20f, theme.StatusOk, TextAlignmentOptions.MidlineLeft, _suitTextScaler);
            _hudStatusLabel.fontStyle = FontStyles.Bold;
            _hudStatusLabel.text = Localization.Get("suit.hud.ok");
            TerminalUiBuilder.SetAnchors(_hudStatusLabel.rectTransform, 0.095f, 0.72f, 0.56f, 0.96f);

            _hudTempVal = TerminalUiBuilder.CreateText("ExtTempText", inner, theme, 18f, theme.AccentComms, TextAlignmentOptions.MidlineRight, _suitTextScaler);
            _hudTempVal.text = Localization.ShapeDisplay($"{Localization.GetRaw("suit.hud.ext")} -63 C");
            TerminalUiBuilder.SetAnchors(_hudTempVal.rectTransform, 0.50f, 0.72f, 0.96f, 0.96f);

            // Row 2: O2 bar (cosmetic)
            BuildCosmeticHudBar(inner, "O2Row", Localization.Get("suit.hud.o2"), 0.42f, 0.66f, new Color(0.27f, 0.89f, 0.83f, 1f), out _hudO2Bar, out _hudO2Val);

            // Row 3: BATT bar (cosmetic)
            BuildCosmeticHudBar(inner, "BattRow", Localization.Get("suit.hud.batt"), 0.12f, 0.36f, new Color(0.49f, 0.87f, 1.00f, 1f), out _hudBattBar, out _hudBattVal);
        }

        private void BuildCosmeticHudBar(
            RectTransform parent,
            string rowName,
            string label,
            float yMin,
            float yMax,
            Color barColor,
            out Image fillImage,
            out TextMeshProUGUI valText)
        {
            var lbl = TerminalUiBuilder.CreateText(rowName + "_Lbl", parent, theme, 18f, theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _suitTextScaler);
            lbl.text = label;
            TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.04f, yMin, 0.24f, yMax);

            var trackGo = new GameObject(rowName + "_Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.26f, yMin + 0.04f);
            trackRt.anchorMax = new Vector2(0.75f, yMax - 0.04f);
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.08f, 0.16f, 0.22f, 0.95f);
            trackImg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackRt, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillImage = fillGo.GetComponent<Image>();
            fillImage.color = barColor;
            fillImage.raycastTarget = false;

            valText = TerminalUiBuilder.CreateText(rowName + "_Val", parent, theme, 18f, barColor, TextAlignmentOptions.MidlineRight, _suitTextScaler);
            valText.text = "98%";
            TerminalUiBuilder.SetAnchors(valText.rectTransform, 0.76f, yMin, 0.96f, yMax);
        }

        private void BuildFirstPersonArmsIfNeeded()
        {
            if (!showFirstPersonArms || _armsRoot != null) return;
            Camera cam = playerGate != null ? playerGate.PlayerCamera : Camera.main;
            if (cam == null) return;

            var armsGo = new GameObject("SuitArmsRig");
            armsGo.transform.SetParent(cam.transform, false);
            armsGo.transform.localPosition = _armsRestLocalPos;
            armsGo.transform.localRotation = Quaternion.identity;
            _armsRoot = armsGo.transform;

            Shader psxLit = Shader.Find("Outpost/PSXLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var sleeveMat = new Material(psxLit);
            sleeveMat.color = new Color(0.84f, 0.86f, 0.89f, 1f);
            var gloveMat = new Material(psxLit);
            gloveMat.color = new Color(0.18f, 0.23f, 0.28f, 1f);

            BuildSingleArm(_armsRoot, "Arm_Left", -1f, sleeveMat, gloveMat);
            BuildSingleArm(_armsRoot, "Arm_Right", +1f, sleeveMat, gloveMat);
            _armsRoot.gameObject.SetActive(false);
        }

        private static void BuildSingleArm(Transform parent, string name, float sideSign, Material sleeveMat, Material gloveMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            // Kept within ~0.32m forward so arms stay inside player capsule collider (no wall clipping in 240p PSX)
            root.localPosition = new Vector3(sideSign * 0.22f, -0.25f, 0.24f);
            root.localRotation = Quaternion.Euler(26f, -sideSign * 16f, sideSign * 10f);

            CreateNoColPrimitive(root, PrimitiveType.Capsule, "Forearm", new Vector3(0f, 0f, -0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.085f, 0.14f, 0.085f), sleeveMat);
            CreateNoColPrimitive(root, PrimitiveType.Cube, "WristCuff", new Vector3(0f, 0f, 0.09f), Vector3.zero, new Vector3(0.092f, 0.092f, 0.032f), gloveMat);
            CreateNoColPrimitive(root, PrimitiveType.Cube, "Glove", new Vector3(0f, -0.005f, 0.145f), new Vector3(8f, 0f, 0f), new Vector3(0.080f, 0.068f, 0.095f), gloveMat);
        }

        private static void CreateNoColPrimitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void DisableRaycastsRecursively(GameObject go)
        {
            if (go == null) return;
            var graphics = go.GetComponentsInChildren<Graphic>(includeInactive: true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private void ResolveMissingDependencies()
        {
            if (playerGate == null) playerGate = GetComponent<PlayerGate>() ?? FindAnyObjectByType<PlayerGate>();
            if (roomController == null) roomController = GetComponent<FirstPersonRoomController>() ?? FindAnyObjectByType<FirstPersonRoomController>();
            if (audioManager == null) audioManager = GetComponent<AudioManager>() ?? FindAnyObjectByType<AudioManager>();
            if (announcerSystem == null) announcerSystem = GetComponent<AnnouncerSystem>() ?? FindAnyObjectByType<AnnouncerSystem>();
            if (accessibility == null) accessibility = GetComponent<AccessibilitySettings>() ?? FindAnyObjectByType<AccessibilitySettings>();
            if (missionFlowController == null) missionFlowController = GetComponent<MissionFlowController>() ?? FindAnyObjectByType<MissionFlowController>();
            if (airlockController == null) airlockController = FindAnyObjectByType<AirlockController>();
            if (terminalController == null) terminalController = FindAnyObjectByType<TerminalController>(FindObjectsInactive.Include);

            if (airlockController != null)
            {
                airlockController.IsWearingSuitCheck = () => _isWearingSuit;
                if (announcerSystem != null)
                    airlockController.SetAnnouncer(announcerSystem);
            }

            if (propSuitVisual == null)
            {
                var rack = GameObject.Find("SuitRack");
                if (rack != null)
                {
                    var child = rack.transform.Find("Prop_Suit");
                    if (child != null) propSuitVisual = child.gameObject;
                }
            }
        }

        private void SubscribeEvents()
        {
            if (roomController != null)
            {
                roomController.OnEnteredTerminal += HandleEnteredTerminal;
                roomController.OnExitedTerminal += HandleExitedTerminal;
            }
            if (accessibility != null)
                accessibility.OnSettingsChanged += HandleAccessibilityChanged;
        }

        private void UnsubscribeEvents()
        {
            if (roomController != null)
            {
                roomController.OnEnteredTerminal -= HandleEnteredTerminal;
                roomController.OnExitedTerminal -= HandleExitedTerminal;
            }
            if (accessibility != null)
                accessibility.OnSettingsChanged -= HandleAccessibilityChanged;
        }
    }
}
