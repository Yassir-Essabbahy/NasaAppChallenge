using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Audio controller with swappable AudioClip inspector slots and WebGL-safe procedural
    /// PCM fallback generation when slots are left unassigned.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Swappable Audio Clips (leave empty for procedural fallback)")]
        [SerializeField] private AudioClip roomHumLoopClip;
        [SerializeField] private AudioClip windLoopClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip radioBlipClip;
        [SerializeField] private AudioClip criticalAlarmLoopClip;
        [SerializeField] private AudioClip typeClickClip;
        [SerializeField] private AudioClip introDroneClip;
        [SerializeField] private AudioClip bassHitClip;
        [SerializeField] private AudioClip radioCrackleClip;
        [SerializeField] private AudioClip doorOpenClip;
        [SerializeField] private AudioClip doorCloseClip;
        [SerializeField] private AudioClip doorLockedClip;
        [SerializeField] private AudioClip airlockHissClip;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private AudioClip[] exteriorFootstepClips;

        [Header("EVA Suit & End-Sol Slots (empty clips allowed)")]
        [SerializeField] private AudioClip suitSealClip;
        [SerializeField] private AudioClip suitBreathingLoopClip;
        [Range(0f, 1f)] [SerializeField] private float suitBreathingVolume = 0.20f;
        [SerializeField] private AudioClip endSolBassHitClip;
        [SerializeField] private AudioClip endSolDroneClip;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.75f;
        [SerializeField] private bool muted;

        public AudioClip IntroDrone => introDroneClip;
        public AudioClip BassHit => bassHitClip;
        public AudioClip TypeClick => typeClickClip;
        public AudioClip RadioCrackle => radioCrackleClip;

        private AudioSource _humSource;
        private AudioSource _windSource;
        private AudioSource _alarmSource;
        private AudioSource _introDroneSource;
        private AudioSource _breathingSource;
        private AudioSource _sfxSource;
        private AudioSource _footstepSource;
        private AudioClip _procFootstepClip;
        private bool _endingOverride;
        private int _footstepIndex;
        private int _lastExteriorFootstepIndex = -1;

        private float _targetHumVol = 0.14f;
        private float _targetHumPitch = 1.0f;
        private float _targetWindVol = 0.04f;
        private float _targetAlarmVol = 0f;
        private float _targetIntroDroneVol = 0f;
        private float _targetBreathingVol = 0f;
        private float _suitMuffleMultiplier = 1f;
        private float _ambientFadeMultiplier = 1f;
        private float _ambientFadeTarget = 1f;
        private bool _initialized;

        public const string PrefMasterVolume = "Outpost_MasterVolume";

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PrefMasterVolume, masterVolume);
                PlayerPrefs.Save();
                ApplyVolumesImmediate();
            }
        }

        public bool IsMuted
        {
            get => muted;
            set
            {
                muted = value;
                ApplyVolumesImmediate();
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureLoopsPlaying();
        }

        private System.Func<bool> _exteriorPredicate;
        private bool _isExterior;
        private bool _hasSnapshot;
        private float _baseHumVol = 0.22f;
        private float _baseWindVol = 0.28f;

        public bool IsExterior => _exteriorPredicate != null ? _exteriorPredicate() : _isExterior;

        public void SetExteriorPredicate(System.Func<bool> predicate) => _exteriorPredicate = predicate;

        public void SetExteriorState(bool exterior) => _isExterior = exterior;

        private void Update()
        {
            if (!_initialized) return;

            if (!_endingOverride)
            {
                bool exterior = IsExterior;
                _targetHumVol = exterior ? 0f : (_hasSnapshot ? _baseHumVol : 0.22f);
                _targetWindVol = exterior ? (_hasSnapshot ? _baseWindVol : 0.28f) : 0f;

                if (_humSource != null && !_humSource.isPlaying && _humSource.clip != null)
                    _humSource.Play();
                if (_windSource != null && !_windSource.isPlaying && _windSource.clip != null)
                    _windSource.Play();
            }

            float baseMult = muted ? 0f : masterVolume;
            float ambientFadeRate = Time.unscaledDeltaTime * 1.6f; // ~0.6-0.8s smooth black card fade
            _ambientFadeMultiplier = Mathf.MoveTowards(_ambientFadeMultiplier, _ambientFadeTarget, ambientFadeRate);
            float mult = baseMult * _suitMuffleMultiplier * _ambientFadeMultiplier;

            float dt = Time.unscaledDeltaTime * 3.0f;
            _humSource.volume = Mathf.Lerp(_humSource.volume, _targetHumVol * mult, dt);
            _humSource.pitch = Mathf.Lerp(_humSource.pitch, _targetHumPitch, dt);
            _windSource.volume = Mathf.Lerp(_windSource.volume, _targetWindVol * mult, dt);
            _alarmSource.volume = Mathf.Lerp(_alarmSource.volume, _targetAlarmVol * mult, dt);
            if (_breathingSource != null)
            {
                _breathingSource.volume = Mathf.Lerp(_breathingSource.volume, _targetBreathingVol * baseMult, dt);
                if (_targetBreathingVol <= 0.001f && _breathingSource.volume <= 0.002f && _breathingSource.isPlaying)
                    _breathingSource.Stop();
            }
            if (_introDroneSource != null)
            {
                _introDroneSource.volume = Mathf.Lerp(_introDroneSource.volume, _targetIntroDroneVol * baseMult, Time.unscaledDeltaTime * 1.4f);
                if (_targetIntroDroneVol <= 0.001f && _introDroneSource.volume <= 0.002f && _introDroneSource.isPlaying)
                    _introDroneSource.Stop();
            }
        }

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            if (PlayerPrefs.HasKey(PrefMasterVolume))
                masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMasterVolume, masterVolume));

            if (roomHumLoopClip == null) roomHumLoopClip = CreateHumClip();
            if (windLoopClip == null) windLoopClip = CreateWindClip();
            if (uiClickClip == null) uiClickClip = CreateUiClickClip();
            if (radioBlipClip == null) radioBlipClip = CreateRadioBlipClip();
            if (criticalAlarmLoopClip == null) criticalAlarmLoopClip = CreateAlarmLoopClip();
            if (typeClickClip == null) typeClickClip = CreateTypeClickClip();
            if (introDroneClip == null) introDroneClip = CreateIntroDroneClip();
            if (bassHitClip == null) bassHitClip = CreateBassHitClip();
            if (radioCrackleClip == null) radioCrackleClip = CreateRadioCrackleClip();
            if (doorOpenClip == null) doorOpenClip = CreateDoorSlideClip(true);
            if (doorCloseClip == null) doorCloseClip = CreateDoorSlideClip(false);
            if (doorLockedClip == null) doorLockedClip = CreateDoorLockedClip();
            if (airlockHissClip == null) airlockHissClip = CreateAirlockHissClip();
            _procFootstepClip = CreateFootstepClip();

            _humSource = CreateAudioSource("Audio_RoomHum", roomHumLoopClip, loop: true);
            _windSource = CreateAudioSource("Audio_MartianWind", windLoopClip, loop: true);
            _alarmSource = CreateAudioSource("Audio_CriticalAlarm", criticalAlarmLoopClip, loop: true);
            _introDroneSource = CreateAudioSource("Audio_IntroDrone", introDroneClip, loop: true);
            _breathingSource = CreateAudioSource("Audio_SuitBreathing", suitBreathingLoopClip, loop: true);
            _sfxSource = CreateAudioSource("Audio_SFX", null, loop: false);
            _sfxSource.volume = 1f;
            _footstepSource = CreateAudioSource("Audio_Footsteps", null, loop: false);
            _footstepSource.volume = 1f;

            ApplyVolumesImmediate();
            EnsureLoopsPlaying();
        }

        public void ApplySnapshot(SessionSnapshot snap)
        {
            EnsureInitialized();
            if (snap == null || _endingOverride) return;

            float powerRatio = Mathf.Clamp01(snap.Power / 100f);
            if (snap.Power <= 0.01f)
            {
                _baseHumVol = 0.025f;
                _targetHumPitch = 0.72f;
            }
            else if (snap.Power < 25f)
            {
                _baseHumVol = 0.08f;
                _targetHumPitch = 0.85f;
            }
            else
            {
                _baseHumVol = Mathf.Lerp(0.10f, 0.20f, powerRatio);
                _targetHumPitch = Mathf.Lerp(0.92f, 1.06f, powerRatio);
            }

            bool inStorm = snap.DustStormSolsRemaining > 0;
            _baseWindVol = inStorm ? 0.38f : Mathf.Lerp(0.22f, 0.30f, snap.PanelDustPct / 100f);
            _hasSnapshot = true;

            bool exterior = IsExterior;
            _targetHumVol = exterior ? 0f : _baseHumVol;
            _targetWindVol = exterior ? _baseWindVol : 0f;

            bool anyCrit = snap.Status == MissionStatus.InProgress && (
                snap.Power < snap.StatusLightLowThreshold ||
                snap.Oxygen < snap.StatusLightLowThreshold ||
                snap.Water < snap.StatusLightLowThreshold ||
                snap.Food < snap.StatusLightLowThreshold ||
                snap.Shielding < snap.StatusLightLowThreshold);
            _targetAlarmVol = anyCrit ? 0.14f : 0f;
        }

        public void PlayUiClick()
        {
            EnsureInitialized();
            EnsureLoopsPlaying();
            if (muted || uiClickClip == null) return;
            _sfxSource.PlayOneShot(uiClickClip, 0.55f * masterVolume);
        }

        public void PlayRadioBlip()
        {
            EnsureInitialized();
            EnsureLoopsPlaying();
            if (muted || radioBlipClip == null) return;
            _sfxSource.PlayOneShot(radioBlipClip, 0.65f * masterVolume);
        }

        public void PlayTypeClick()
        {
            EnsureInitialized();
            if (muted || typeClickClip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(typeClickClip, 0.35f * masterVolume);
        }

        public void PlayBassHit(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : bassHitClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, 0.70f * masterVolume);
        }

        public void PlayRadioCrackle(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : radioCrackleClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, 0.55f * masterVolume);
        }

        public void PlayDoorOpen(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : doorOpenClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.volume = 1f;
            _sfxSource.PlayOneShot(clip, 1.0f * masterVolume);
        }

        public void PlayDoorClose(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : doorCloseClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.volume = 1f;
            _sfxSource.PlayOneShot(clip, 1.0f * masterVolume);
        }

        public void PlayDoorLocked(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : doorLockedClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, 0.55f * masterVolume);
        }

        public void PlayAirlockHiss(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : airlockHissClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, 0.60f * masterVolume);
        }

        public void StartIntroDrone()
        {
            EnsureInitialized();
            _targetIntroDroneVol = 0.24f;
            if (_introDroneSource == null) return;
            _introDroneSource.clip = introDroneClip;
            if (_introDroneSource.clip != null)
            {
                _introDroneSource.volume = 0f;
                if (!_introDroneSource.isPlaying)
                    _introDroneSource.Play();
            }
        }

        public void StopIntroDrone(bool immediate = false)
        {
            _targetIntroDroneVol = 0f;
            if (_introDroneSource != null && immediate)
            {
                _introDroneSource.volume = 0f;
                _introDroneSource.Stop();
            }
        }

        public void SetSuitMuffled(bool muffled)
        {
            _suitMuffleMultiplier = muffled ? 0.6f : 1.0f;
        }

        public void SetAmbientFadeTarget(float target01, bool immediate = false)
        {
            _ambientFadeTarget = Mathf.Clamp01(target01);
            if (immediate)
            {
                _ambientFadeMultiplier = _ambientFadeTarget;
                ApplyVolumesImmediate();
            }
        }

        public void PlaySuitSeal(AudioClip customClip = null)
        {
            EnsureInitialized();
            AudioClip clip = customClip != null ? customClip : suitSealClip;
            if (muted || clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, 0.70f * masterVolume);
        }

        public void SetSuitBreathingActive(bool active, AudioClip overrideClip = null, float overrideVolume = -1f)
        {
            EnsureInitialized();
            AudioClip clip = overrideClip != null ? overrideClip : suitBreathingLoopClip;
            float vol = overrideVolume >= 0f ? Mathf.Clamp01(overrideVolume) : suitBreathingVolume;
            _targetBreathingVol = (active && clip != null) ? vol : 0f;
            if (_breathingSource == null) return;

            if (active && clip != null)
            {
                if (_breathingSource.clip != clip)
                    _breathingSource.clip = clip;
                if (!_breathingSource.isPlaying)
                    _breathingSource.Play();
            }
        }

        public void PlayEndSolBassHit(AudioClip customClip = null)
        {
            AudioClip clip = customClip != null ? customClip : (endSolBassHitClip != null ? endSolBassHitClip : bassHitClip);
            PlayBassHit(clip);
        }

        public void StartEndSolDrone(AudioClip customClip = null)
        {
            EnsureInitialized();
            _targetIntroDroneVol = 0.24f;
            if (_introDroneSource == null) return;
            AudioClip clip = customClip != null ? customClip : (endSolDroneClip != null ? endSolDroneClip : introDroneClip);
            if (clip != null)
            {
                _introDroneSource.clip = clip;
                _introDroneSource.volume = 0f;
                if (!_introDroneSource.isPlaying)
                    _introDroneSource.Play();
            }
        }

        public void StopEndSolDrone(bool immediate = false) => StopIntroDrone(immediate);

        public void PlayFootstep(AudioClip[] overrideClips = null, bool isExterior = false)
        {
            EnsureInitialized();
            if (muted) return;

            AudioClip chosen = null;
            AudioClip[] pool = (overrideClips != null && overrideClips.Length > 0)
                ? overrideClips
                : (isExterior && exteriorFootstepClips != null && exteriorFootstepClips.Length > 0
                    ? exteriorFootstepClips
                    : footstepClips);

            if (pool != null && pool.Length > 0)
            {
                if (isExterior && pool.Length > 1)
                {
                    int idx = Random.Range(0, pool.Length);
                    if (idx == _lastExteriorFootstepIndex)
                        idx = (idx + 1) % pool.Length;
                    _lastExteriorFootstepIndex = idx;
                    chosen = pool[idx];
                }
                else
                {
                    chosen = pool[_footstepIndex % pool.Length];
                    _footstepIndex++;
                }
            }

            if (chosen == null) chosen = _procFootstepClip;
            if (chosen == null) return;

            AudioSource targetSource = _footstepSource != null ? _footstepSource : _sfxSource;
            if (targetSource == null) return;

            if (ReferenceEquals(targetSource, _footstepSource))
                targetSource.pitch = isExterior ? Random.Range(0.94f, 1.06f) : 1.0f;

            float baseGain = isExterior ? 0.42f : 0.28f;
            targetSource.PlayOneShot(chosen, baseGain * masterVolume);
        }

        public void SetEndingState(bool active, bool won)
        {
            EnsureInitialized();
            _endingOverride = active;
            if (active)
            {
                _targetAlarmVol = 0f;
                _alarmSource.volume = 0f;
                _targetHumVol = won ? 0.10f : 0f;
                _targetWindVol = won ? 0.04f : 0.02f;
            }
        }

        public void EnsureLoopsPlaying()
        {
            if (!_initialized) return;
            if (!_humSource.isPlaying) _humSource.Play();
            if (!_windSource.isPlaying) _windSource.Play();
            if (!_alarmSource.isPlaying) _alarmSource.Play();
        }

        private void ApplyVolumesImmediate()
        {
            if (!_initialized) return;
            float baseMult = muted ? 0f : masterVolume;
            float mult = baseMult * _suitMuffleMultiplier * _ambientFadeMultiplier;
            _humSource.volume = _targetHumVol * mult;
            _humSource.pitch = _targetHumPitch;
            _windSource.volume = _targetWindVol * mult;
            _alarmSource.volume = _targetAlarmVol * mult;
            if (_breathingSource != null) _breathingSource.volume = _targetBreathingVol * baseMult;
            if (_sfxSource != null) _sfxSource.volume = 1f;
        }

        private AudioSource CreateAudioSource(string childName, AudioClip clip, bool loop)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private static AudioClip CreateHumClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 2; // Integer cycle count for 64 Hz & 128 Hz over 2s -> seamless loop
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * 64f * t) * 0.45f;
                float s2 = Mathf.Sin(2f * Mathf.PI * 128f * t) * 0.22f;
                float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                data[i] = (s1 + s2) * lfo * 0.35f;
            }
            return CreatePcmClip("Proc_RoomHum", data, sampleRate);
        }

        private static AudioClip CreateWindClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 3;
            float[] data = new float[samples];
            uint seed = 0x1234ABCDu;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                lp = lp * 0.94f + white * 0.06f;
                float t = i / (float)sampleRate;
                float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (t / 3f));
                data[i] = Mathf.Clamp(lp * 2.4f * swell, -0.5f, 0.5f);
            }
            // Crossfade endpoints over 512 samples for click-free loop
            int fade = 512;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                int tail = samples - fade + i;
                float blended = Mathf.Lerp(data[tail], data[i], w);
                data[i] = blended;
            }
            return CreatePcmClip("Proc_MarsWind", data, sampleRate);
        }

        private static AudioClip CreateUiClickClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.045f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                float freq = Mathf.Lerp(960f, 520f, p);
                float env = (1f - p) * (1f - p);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate)) * env * 0.45f;
            }
            return CreatePcmClip("Proc_UiClick", data, sampleRate);
        }

        private static AudioClip CreateRadioBlipClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.16f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float freq = t < 0.08f ? 1046f : 1318f;
                float localP = (t % 0.08f) / 0.08f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(localP));
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            return CreatePcmClip("Proc_RadioBlip", data, sampleRate);
        }

        private static AudioClip CreateAlarmLoopClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 1.5f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                bool activePulse = t < 0.18f || (t >= 0.28f && t < 0.46f);
                if (!activePulse) continue;
                float pulseT = t < 0.18f ? t / 0.18f : (t - 0.28f) / 0.18f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseT));
                data[i] = Mathf.Sin(2f * Mathf.PI * 520f * t) * env * 0.32f;
            }
            return CreatePcmClip("Proc_CriticalAlarm", data, sampleRate);
        }

        private static AudioClip CreatePcmClip(string clipName, float[] samples, int sampleRate)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTypeClickClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.018f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                float env = 1f - p;
                data[i] = Mathf.Sin(2f * Mathf.PI * 1450f * (i / (float)sampleRate)) * env * 0.25f;
            }
            return CreatePcmClip("Proc_TypeClick", data, sampleRate);
        }

        private static AudioClip CreateFootstepClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.075f);
            float[] data = new float[samples];
            uint seed = 0x51A7u;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                lp = lp * 0.85f + white * 0.15f;
                float thud = Mathf.Sin(2f * Mathf.PI * 110f * (i / (float)sampleRate));
                float env = (1f - p) * (1f - p);
                data[i] = (thud * 0.35f + lp * 0.45f) * env;
            }
            return CreatePcmClip("Proc_Footstep", data, sampleRate);
        }

        private static AudioClip CreateIntroDroneClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 4; // 4s seamless loop
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float sub = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f;
                float fifth = Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f;
                float octave = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.14f;
                float swell = 0.70f + 0.30f * Mathf.Sin(2f * Mathf.PI * (t / 4f));
                data[i] = (sub + fifth + octave) * swell * 0.45f;
            }
            return CreatePcmClip("Proc_IntroDrone", data, sampleRate);
        }

        private static AudioClip CreateBassHitClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.55f);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                float freq = Mathf.Lerp(115f, 36f, Mathf.Sqrt(p));
                phase += (2f * Mathf.PI * freq) / sampleRate;
                float env = Mathf.Pow(1f - p, 1.8f);
                data[i] = Mathf.Sin(phase) * env * 0.85f;
            }
            return CreatePcmClip("Proc_BassHit", data, sampleRate);
        }

        private static AudioClip CreateRadioCrackleClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.45f);
            float[] data = new float[samples];
            uint seed = 0x9E3779B9u;
            float bp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                bp = bp * 0.6f + white * 0.4f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p)) * 0.65f + (1f - p) * 0.35f;
                float squelch = (i < sampleRate * 0.05f) ? Mathf.Sin(2f * Mathf.PI * 1950f * (i / (float)sampleRate)) * 0.35f : 0f;
                data[i] = Mathf.Clamp((bp * 0.45f + squelch) * env, -0.7f, 0.7f);
            }
            return CreatePcmClip("Proc_RadioCrackle", data, sampleRate);
        }

        private static AudioClip CreateDoorSlideClip(bool opening)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.45f);
            float[] data = new float[samples];
            float phase = 0f;
            uint seed = opening ? 0x1357u : 0x2468u;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                float freq = opening ? Mathf.Lerp(140f, 340f, p) : Mathf.Lerp(320f, 120f, p);
                phase += (2f * Mathf.PI * freq) / sampleRate;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                lp = lp * 0.78f + white * 0.22f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p)) * 0.85f + 0.15f * (1f - p);
                float thud = (p < 0.12f || p > 0.88f) ? Mathf.Sin(2f * Mathf.PI * 82f * (i / (float)sampleRate)) * 0.45f : 0f;
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.28f + lp * 0.32f) * env + thud, -0.75f, 0.75f);
            }
            return CreatePcmClip(opening ? "Proc_DoorOpen" : "Proc_DoorClose", data, sampleRate);
        }

        private static AudioClip CreateDoorLockedClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.28f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                bool pulse = (p < 0.38f) || (p > 0.55f && p < 0.92f);
                float tone = Mathf.Sin(2f * Mathf.PI * 240f * (i / (float)sampleRate)) * 0.55f +
                             Mathf.Sin(2f * Mathf.PI * 360f * (i / (float)sampleRate)) * 0.25f;
                data[i] = pulse ? tone * 0.55f : 0f;
            }
            return CreatePcmClip("Proc_DoorLocked", data, sampleRate);
        }

        private static AudioClip CreateAirlockHissClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 2.2f);
            float[] data = new float[samples];
            uint seed = 0xCAFEBABEu;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float p = i / (float)samples;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                lp = lp * 0.72f + white * 0.28f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p)) * (1.15f - p * 0.45f);
                float valve = Mathf.Sin(2f * Mathf.PI * 95f * (i / (float)sampleRate)) * 0.14f * (1f - p);
                data[i] = Mathf.Clamp((lp * 0.55f + valve) * Mathf.Clamp01(env), -0.65f, 0.65f);
            }
            return CreatePcmClip("Proc_AirlockHiss", data, sampleRate);
        }
    }
}
