using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Audio
{
    /// <summary>
    /// Runtime facility announcer + ambient PA scheduler.
    /// Zero AudioMixer or runtime filter dependency; plays pre-baked mono PCM clips directly,
    /// drives the persistent ANNOUNCEMENT caption bar, and respects PlayerPrefs volume/mute.
    /// </summary>
    public class AnnouncerSystem : MonoBehaviour
    {
        public const string PrefVolume = "announcerVolume";
        public const string PrefMuted = "announcerMuted";
        private const float InitialDelaySeconds = 25f;
        private const float MinIntervalSeconds = 50f;
        private const float MaxIntervalSeconds = 110f;
        private const int RecentHistoryLimit = 4;

        [Header("Configuration")]
        [SerializeField] private AnnouncerDatabase database;
        [SerializeField] private AnnouncerSubtitleHud subtitleHud;
        [SerializeField] [Range(0f, 1f)] private float announcerVolume = 0.85f;
        [SerializeField] private bool announcerMuted;
        [SerializeField] private bool enableOptionalExteriorMuffle = true;

        private AudioSource _voiceSource;
        private AudioSource _stingSource;
        private AudioLowPassFilter _optionalLowPass;
        private AudioClip _fallbackChimeClip;
        private bool _lowPassChecked;

        private Func<bool> _hasPlayerControl;
        private Func<bool> _isSuppressed;
        private Func<bool> _isExteriorOrAirlock;

        private bool _controlTimerArmed;
        private float _nextScheduledUnscaledTime = -1f;
        private Coroutine _activePlayback;
        private float _suitMuffleScale = 1f;
        private readonly Queue<string> _recentIds = new Queue<string>();

        public AnnouncerDatabase Database => database;
        public string LastPlayedId { get; private set; } = string.Empty;
        public string LastStatus { get; private set; } = "IDLE";
        public bool IsPlaying => _activePlayback != null;

        public float AnnouncerVolume
        {
            get => announcerVolume;
            set
            {
                announcerVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PrefVolume, announcerVolume);
                PlayerPrefs.Save();
                ApplyAudioVolumes();
            }
        }

        public bool IsMuted
        {
            get => announcerMuted;
            set
            {
                announcerMuted = value;
                PlayerPrefs.SetInt(PrefMuted, announcerMuted ? 1 : 0);
                PlayerPrefs.Save();
                ApplyAudioVolumes();
            }
        }

        private void Awake()
        {
            LoadPrefs();
            EnsureAudioSources();
        }

        public void Initialize(
            AnnouncerDatabase db,
            AnnouncerSubtitleHud hud,
            Func<bool> hasPlayerControl,
            Func<bool> isSuppressed,
            Func<bool> isExteriorOrAirlock)
        {
            if (db != null) database = db;
            if (database == null) database = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");
            if (hud != null) subtitleHud = hud;

            _hasPlayerControl = hasPlayerControl;
            _isSuppressed = isSuppressed;
            _isExteriorOrAirlock = isExteriorOrAirlock;

            LoadPrefs();
            EnsureAudioSources();
            ApplyAudioVolumes();
            ResetScheduler();
        }

        public void SetDatabase(AnnouncerDatabase db) => database = db;
        public void SetSubtitleHud(AnnouncerSubtitleHud hud) => subtitleHud = hud;

        public void ResetScheduler()
        {
            _controlTimerArmed = false;
            _nextScheduledUnscaledTime = -1f;
        }

        private void Update()
        {
            UpdateOptionalMuffle();

            if (database == null || database.lines == null || database.lines.Count == 0)
                return;

            bool controlActive = _hasPlayerControl == null || _hasPlayerControl();
            bool suppressed = _isSuppressed != null && _isSuppressed();

            if (!controlActive)
            {
                // Keep waiting until player actually gains gameplay control after title/intro/boot
                _controlTimerArmed = false;
                _nextScheduledUnscaledTime = -1f;
                return;
            }

            if (!_controlTimerArmed)
            {
                _controlTimerArmed = true;
                _nextScheduledUnscaledTime = Time.unscaledTime + InitialDelaySeconds;
                return;
            }

            if (suppressed || IsPlaying)
                return;

            if (_nextScheduledUnscaledTime > 0f && Time.unscaledTime >= _nextScheduledUnscaledTime)
            {
                PlayRandomNow(bypassSuppression: false);
            }
        }

        public bool PlayLineById(string id, bool bypassSuppression = true)
        {
            if (database == null)
                database = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");
            if (database == null)
            {
                LastStatus = "NO DATABASE";
                return false;
            }

            var line = database.FindLine(id);
            if (line == null && string.Equals(id, "vox_welcome", StringComparison.OrdinalIgnoreCase) && database.welcomeVoiceProcessed != null)
            {
                line = new AnnouncerLine
                {
                    id = "vox_welcome",
                    alertSting = false,
                    subtitleKey = "Commander. Outpost is under your control.",
                    processedClip = database.welcomeVoiceProcessed
                };
            }
            if (line == null)
            {
                LastStatus = $"MISSING ID: {id}";
                return false;
            }

            if (!bypassSuppression && _isSuppressed != null && _isSuppressed())
                return false;

            StartLineRoutine(line);
            return true;
        }

        public bool PlayRandomNow(bool bypassSuppression = true)
        {
            if (database == null)
                database = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");
            if (database == null || database.lines == null || database.lines.Count == 0)
            {
                LastStatus = "NO LINES";
                return false;
            }

            if (!bypassSuppression && _isSuppressed != null && _isSuppressed())
                return false;

            var candidates = new List<AnnouncerLine>();
            for (int i = 0; i < database.lines.Count; i++)
            {
                var line = database.lines[i];
                if (line == null) continue;
                if (!WasRecentlyPlayed(line.id))
                    candidates.Add(line);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < database.lines.Count; i++)
                {
                    if (database.lines[i] != null && !string.Equals(database.lines[i].id, LastPlayedId, StringComparison.OrdinalIgnoreCase))
                        candidates.Add(database.lines[i]);
                }
            }

            if (candidates.Count == 0)
                candidates.AddRange(database.lines);

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            StartLineRoutine(chosen);
            return true;
        }

        public void StopCurrent()
        {
            if (_activePlayback != null)
            {
                StopCoroutine(_activePlayback);
                _activePlayback = null;
            }
            if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
            if (_stingSource != null && _stingSource.isPlaying) _stingSource.Stop();
            subtitleHud?.Hide();
            LastStatus = "STOPPED";
        }

        private void StartLineRoutine(AnnouncerLine line)
        {
            StopCurrent();
            EnsureAudioSources();
            ApplyAudioVolumes();

            RecordPlayed(line.id);
            LastPlayedId = line.id;
            _nextScheduledUnscaledTime = Time.unscaledTime + UnityEngine.Random.Range(MinIntervalSeconds, MaxIntervalSeconds);
            _activePlayback = StartCoroutine(PlayLineCoroutine(line));
        }

        private IEnumerator PlayLineCoroutine(AnnouncerLine line)
        {
            string subtitle = AnnouncerLocalization.GetSubtitle(line.id, line.subtitleKey);
            subtitleHud?.ShowAnnouncement(subtitle, line.alertSting);

            bool audible = !announcerMuted && announcerVolume > 0.001f;
            AudioClip stingClip = line.alertSting ? database.GetStingPlaybackClip() : null;
            AudioClip voiceClip = line.GetPlaybackClip();

            if (line.alertSting)
            {
                if (stingClip != null && audible)
                {
                    LastStatus = $"STING -> {line.id}";
                    _stingSource.clip = stingClip;
                    _stingSource.Play();
                    yield return WaitUnscaled(stingClip.length + 0.35f);
                }
                else
                {
                    LastStatus = $"STING WAIT -> {line.id}";
                    yield return WaitUnscaled(0.35f);
                }
            }
            else if (line.id != null && line.id.StartsWith("n_", StringComparison.OrdinalIgnoreCase))
            {
                // Brief 0.2s notice lead-in
                yield return WaitUnscaled(0.2f);
            }

            if (voiceClip != null)
            {
                LastStatus = $"{line.id} ({(line.processedClip != null ? "PRO" : "ORIG")})";
                if (audible)
                {
                    _voiceSource.clip = voiceClip;
                    _voiceSource.Play();
                }
                yield return WaitUnscaled(Mathf.Max(2.2f, voiceClip.length + 0.35f));
            }
            else
            {
                // Clean caption + soft PA chime fallback if user hasn't dropped the recorded voice line into Assets yet
                if (audible && !line.alertSting)
                {
                    if (_fallbackChimeClip == null)
                        _fallbackChimeClip = CreateFallbackChimeClip();
                    _stingSource.clip = _fallbackChimeClip;
                    _stingSource.Play();
                }
                LastStatus = $"{line.id} (CAPTION ONLY - CLIP MISSING)";
                float readSec = Mathf.Clamp(1.8f + subtitle.Length * 0.045f, 3.2f, 6.2f);
                yield return WaitUnscaled(readSec);
            }

            subtitleHud?.Hide();
            _activePlayback = null;
            _nextScheduledUnscaledTime = Time.unscaledTime + UnityEngine.Random.Range(MinIntervalSeconds, MaxIntervalSeconds);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void EnsureAudioSources()
        {
            if (_voiceSource == null)
            {
                var voiceGo = transform.Find("AnnouncerVoiceSource");
                if (voiceGo == null)
                {
                    voiceGo = new GameObject("AnnouncerVoiceSource").transform;
                    voiceGo.SetParent(transform, false);
                }
                _voiceSource = voiceGo.GetComponent<AudioSource>();
                if (_voiceSource == null) _voiceSource = voiceGo.gameObject.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.loop = false;
                _voiceSource.spatialBlend = 0f;
            }

            if (_stingSource == null)
            {
                var stingGo = transform.Find("AnnouncerStingSource");
                if (stingGo == null)
                {
                    stingGo = new GameObject("AnnouncerStingSource").transform;
                    stingGo.SetParent(transform, false);
                }
                _stingSource = stingGo.GetComponent<AudioSource>();
                if (_stingSource == null) _stingSource = stingGo.gameObject.AddComponent<AudioSource>();
                _stingSource.playOnAwake = false;
                _stingSource.loop = false;
                _stingSource.spatialBlend = 0f;
            }

            if (!_lowPassChecked && enableOptionalExteriorMuffle && _voiceSource != null)
            {
                _lowPassChecked = true;
                try
                {
                    _optionalLowPass = _voiceSource.GetComponent<AudioLowPassFilter>();
                    if (_optionalLowPass == null)
                        _optionalLowPass = _voiceSource.gameObject.AddComponent<AudioLowPassFilter>();
                    if (_optionalLowPass != null)
                    {
                        _optionalLowPass.lowpassResonanceQ = 1f;
                        _optionalLowPass.cutoffFrequency = 22000f;
                    }
                }
                catch
                {
                    // Optional effect; ignore safely on stripped/WebGL runtimes.
                    _optionalLowPass = null;
                }
            }
        }

        private void UpdateOptionalMuffle()
        {
            if (_optionalLowPass == null) return;
            bool muffle = enableOptionalExteriorMuffle && _isExteriorOrAirlock != null && _isExteriorOrAirlock();
            float target = muffle ? 1150f : 22000f;
            if (Mathf.Abs(_optionalLowPass.cutoffFrequency - target) > 10f)
                _optionalLowPass.cutoffFrequency = target;
        }

        private void LoadPrefs()
        {
            announcerVolume = PlayerPrefs.HasKey(PrefVolume) ? Mathf.Clamp01(PlayerPrefs.GetFloat(PrefVolume, 0.85f)) : 0.85f;
            announcerMuted = PlayerPrefs.GetInt(PrefMuted, 0) == 1;
        }

        public bool HasLine(string id)
        {
            if (database == null)
                database = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");
            if (database == null || string.IsNullOrWhiteSpace(id)) return false;
            return database.FindLine(id) != null;
        }

        public bool PlayLineIfPresent(string id, bool bypassSuppression = true)
        {
            if (!HasLine(id)) return false;
            return PlayLineById(id, bypassSuppression);
        }

        public void SetSuitMuffleScale(float scale)
        {
            _suitMuffleScale = Mathf.Clamp01(scale);
            ApplyAudioVolumes();
        }

        private void ApplyAudioVolumes()
        {
            float eff = (announcerMuted ? 0f : announcerVolume) * _suitMuffleScale;
            if (_voiceSource != null) _voiceSource.volume = eff;
            if (_stingSource != null) _stingSource.volume = eff * 0.85f;
        }

        private bool WasRecentlyPlayed(string id)
        {
            foreach (string recent in _recentIds)
            {
                if (string.Equals(recent, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void RecordPlayed(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _recentIds.Enqueue(id);
            while (_recentIds.Count > RecentHistoryLimit)
                _recentIds.Dequeue();
        }

        private static AudioClip CreateFallbackChimeClip()
        {
            const int sr = 22050;
            int total = sr / 3; // 0.33s gentle PA two-tone chime
            float[] data = new float[total];
            int split = sr / 8;
            for (int i = 0; i < total; i++)
            {
                float freq = i < split ? 660f : 880f;
                float localT = (i < split ? i : i - split) / (float)sr;
                float env = Mathf.Exp(-localT * 11f) * Mathf.Clamp01(localT * 80f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * localT) * env * 0.35f;
            }
            return VoxFxDsp.CreateRuntimeClip("Proc_AnnouncerFallbackChime", data, sr);
        }
    }
}
