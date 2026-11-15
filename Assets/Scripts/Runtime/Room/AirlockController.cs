using System;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Controls the Ares-4 EVA Airlock: interlocks inner habitat door and outer Mars hatch,
    /// plays a 3-second depressurize/repressurize cycle with hiss SFX & pulsing beacon,
    /// and blocks outward EVAs during solar flares or dust storms.
    /// </summary>
    public class AirlockController : MonoBehaviour
    {
        [Header("Airlock Doors & Chamber")]
        [SerializeField] private DoorInteractable innerDoor;
        [SerializeField] private DoorInteractable outerDoor;
        [SerializeField] private Light chamberLight;
        [SerializeField] private Renderer beaconRenderer;
        [SerializeField] private float cycleDuration = 3.0f;

        private bool _isDepressurized;
        private bool _isCycling;
        private bool _targetDepressurize;
        private float _cycleTimer;
        private bool _debugUnlocked;
        private MaterialPropertyBlock _mpb;

        private AudioManager _audio;
        private Audio.AnnouncerSystem _announcer;
        private Action<string, float> _bannerCallback;

        public Func<bool> IsWearingSuitCheck { get; set; }

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        public bool IsDepressurized => _isDepressurized;
        public bool IsCycling => _isCycling;

        /// <summary>
        /// Pure airlock suit gate helper used by both runtime cycle attempts and EditMode unit tests:
        /// cycling inward is always allowed; cycling outward requires wearing the suit.
        /// </summary>
        public static bool CanCycleWithSuit(bool isCurrentlyDepressurized, bool isWearingSuit)
        {
            return isCurrentlyDepressurized || isWearingSuit;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            ApplyDoorLocks(null);
            UpdateChamberVisuals(null);
        }

        public void Configure(
            DoorInteractable inner,
            DoorInteractable outer,
            Light lightRef,
            Renderer beaconRef,
            float cycleSeconds = 3.0f)
        {
            innerDoor = inner;
            outerDoor = outer;
            chamberLight = lightRef;
            beaconRenderer = beaconRef;
            cycleDuration = Mathf.Max(1.0f, cycleSeconds);
            ApplyDoorLocks(null);
            UpdateChamberVisuals(null);
        }

        public void BindRuntime(AudioManager audioManager, Action<string, float> showBanner, Audio.AnnouncerSystem announcer = null)
        {
            _audio = audioManager;
            _bannerCallback = showBanner;
            if (announcer != null) _announcer = announcer;
        }

        public void SetAnnouncer(Audio.AnnouncerSystem announcer)
        {
            _announcer = announcer;
        }

        public void OnSessionSnapshotChanged(SessionSnapshot snap)
        {
            if (!_isCycling && !_debugUnlocked)
                ApplyDoorLocks(snap);
            UpdateChamberVisuals(snap);
        }

        public string GetCyclePrompt(SessionSnapshot snap)
        {
            if (_isCycling)
                return Localization.Get("airlock.cycling_busy");

            if (!_isDepressurized)
            {
                if (TryGetUnsafeEvaMessage(snap, out string unsafeMsg))
                    return unsafeMsg;
                return Localization.Get("airlock.cycle_out_prompt");
            }

            return Localization.Get("airlock.cycle_in_prompt");
        }

        public bool TryCycleAirlock(SessionSnapshot snap, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (_isCycling)
            {
                statusMessage = Localization.Get("airlock.cycling_busy");
                return false;
            }

            // Outward EVA safety gate: refuse during active solar flare or dust storm
            if (!_isDepressurized && TryGetUnsafeEvaMessage(snap, out string unsafeMsg))
            {
                _audio?.PlayDoorLocked();
                statusMessage = unsafeMsg;
                return false;
            }

            // Outward EVA suit gate: cycling inward is always allowed
            bool wearingSuit = IsWearingSuitCheck == null || IsWearingSuitCheck();
            if (!CanCycleWithSuit(_isDepressurized, wearingSuit))
            {
                _audio?.PlayDoorLocked();
                _announcer?.PlayLineIfPresent("airlock_denied", bypassSuppression: true);
                statusMessage = Localization.Get("suit_required");
                return false;
            }

            _debugUnlocked = false;
            _targetDepressurize = !_isDepressurized;
            _isCycling = true;
            _cycleTimer = cycleDuration;

            if (innerDoor != null)
            {
                innerDoor.SetOpen(false, _audio);
                innerDoor.SetLocked(true, "airlock.cycling_busy");
            }
            if (outerDoor != null)
            {
                outerDoor.SetOpen(false, _audio);
                outerDoor.SetLocked(true, "airlock.cycling_busy");
            }

            _audio?.PlayAirlockHiss();
            statusMessage = Localization.Get(_targetDepressurize ? "airlock.depressurizing" : "airlock.repressurizing");
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugUnlock()
        {
            _debugUnlocked = true;
            _isCycling = false;
            innerDoor?.DebugUnlock();
            outerDoor?.DebugUnlock();
            UpdateChamberVisuals(null);
        }
#endif

        public void SetDepressurizedImmediateForTeleport(bool depressurized)
        {
            _isCycling = false;
            _debugUnlocked = false;
            _isDepressurized = depressurized;
            if (depressurized)
            {
                innerDoor?.SetOpenImmediate(false);
                innerDoor?.SetLocked(true, "airlock.hatch_locked");
                outerDoor?.SetLocked(false);
                outerDoor?.SetOpenImmediate(true);
            }
            else
            {
                outerDoor?.SetOpenImmediate(false);
                outerDoor?.SetLocked(true, "airlock.hatch_locked");
                innerDoor?.SetLocked(false);
                innerDoor?.SetOpenImmediate(true);
            }
            UpdateChamberVisuals(null);
        }

        private void Update()
        {
            if (_isCycling)
            {
                _cycleTimer -= Time.unscaledDeltaTime;
                UpdateCyclingBeacon();

                if (_cycleTimer <= 0f)
                {
                    _isCycling = false;
                    _isDepressurized = _targetDepressurize;
                    if (_isDepressurized)
                    {
                        innerDoor?.SetLocked(true, "airlock.hatch_locked");
                        outerDoor?.SetLocked(false);
                        outerDoor?.SetOpen(true, _audio);
                        _bannerCallback?.Invoke(Localization.Get("airlock.ready_out"), 4.2f);
                    }
                    else
                    {
                        outerDoor?.SetLocked(true, "airlock.hatch_locked");
                        innerDoor?.SetLocked(false);
                        innerDoor?.SetOpen(true, _audio);
                        _bannerCallback?.Invoke(Localization.Get("airlock.ready_in"), 4.0f);
                    }
                    UpdateChamberVisuals(null);
                }
            }
        }

        private void ApplyDoorLocks(SessionSnapshot snap)
        {
            if (_isDepressurized)
            {
                innerDoor?.SetLocked(true, "airlock.hatch_locked");
                outerDoor?.SetLocked(false);
            }
            else
            {
                innerDoor?.SetLocked(false);
                string outerLockKey = "airlock.hatch_locked";
                if (snap != null)
                {
                    if (snap.SolarFlareActive) outerLockKey = "airlock.unsafe_flare";
                    else if (snap.DustStormSolsRemaining > 0) outerLockKey = "airlock.unsafe_storm";
                }
                outerDoor?.SetLocked(true, outerLockKey);
            }
        }

        private static bool TryGetUnsafeEvaMessage(SessionSnapshot snap, out string message)
        {
            message = string.Empty;
            if (snap == null) return false;
            if (snap.SolarFlareActive)
            {
                message = Localization.Get("airlock.unsafe_flare");
                return true;
            }
            if (snap.DustStormSolsRemaining > 0)
            {
                message = Localization.Get("airlock.unsafe_storm");
                return true;
            }
            return false;
        }

        private void UpdateCyclingBeacon()
        {
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
            Color amber = new Color(1.0f, 0.56f, 0.14f, 1f);
            if (chamberLight != null)
            {
                chamberLight.enabled = true;
                chamberLight.color = amber;
                chamberLight.intensity = Mathf.Lerp(0.8f, 2.4f, pulse);
            }
            SetBeaconEmissive(amber, 1.8f * pulse);
        }

        private void UpdateChamberVisuals(SessionSnapshot snap)
        {
            Color col = _isDepressurized
                ? new Color(0.95f, 0.42f, 0.22f, 1f)
                : (TryGetUnsafeEvaMessage(snap, out _) ? new Color(0.95f, 0.22f, 0.22f, 1f) : new Color(0.28f, 0.85f, 0.58f, 1f));

            if (chamberLight != null)
            {
                chamberLight.enabled = true;
                chamberLight.color = _isDepressurized ? new Color(0.95f, 0.65f, 0.45f, 1f) : new Color(0.82f, 0.90f, 1.0f, 1f);
                chamberLight.intensity = _isDepressurized ? 0.9f : 1.35f;
            }
            SetBeaconEmissive(col, 1.3f);
        }

        private void SetBeaconEmissive(Color col, float intensity)
        {
            if (beaconRenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            beaconRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorProp, col);
            _mpb.SetColor(ColorProp, col);
            _mpb.SetColor(EmissionProp, col * intensity);
            beaconRenderer.SetPropertyBlock(_mpb);
        }
    }
}
