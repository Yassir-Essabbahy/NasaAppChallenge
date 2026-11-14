using System.Text;
using Outpost.Sim;
using TMPro;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Binds Simulation SessionSnapshot state to physical room visuals:
    /// wall status lights, pulsing red critical beacon, Martian dust particles,
    /// desk radio unread LED, room power dimming, medbay commander light, and 3D monitor readout.
    /// </summary>
    public class RoomStateBinder : MonoBehaviour
    {
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private static readonly Color ColorOk = new Color(0.18f, 0.88f, 0.36f, 1f);
        private static readonly Color ColorLow = new Color(1.00f, 0.72f, 0.14f, 1f);
        private static readonly Color ColorCrit = new Color(0.96f, 0.18f, 0.18f, 1f);
        private static readonly Color ColorOff = new Color(0.10f, 0.11f, 0.13f, 1f);

        [Header("Wall Status Panel (Power, Oxygen, Water, Food, Shielding)")]
        [SerializeField] private Renderer[] statusLightRenderers = new Renderer[5];
        [SerializeField] private TextMeshPro[] statusLightLabels = new TextMeshPro[5];

        [Header("Critical Overhead Alarm")]
        [SerializeField] private Renderer overheadAlarmRenderer;
        [SerializeField] private Light overheadAlarmLight;

        [Header("Martian Exterior Window")]
        [SerializeField] private ParticleSystem marsDustParticles;
        [SerializeField] private ParticleSystemRenderer marsDustRenderer;

        [Header("Desk Radio")]
        [SerializeField] private Renderer radioLedRenderer;
        [SerializeField] private Light radioPointLight;

        [Header("Room Power Lighting")]
        [SerializeField] private Light[] mainRoomLights;
        [SerializeField] private Light emergencyRedLight;

        [Header("Medbay Door & Injured Commander")]
        [SerializeField] private Light medbayLight;
        [SerializeField] private Renderer medbayWindowRenderer;

        [Header("3D Monitor Screen Readout")]
        [SerializeField] private Renderer monitorScreenRenderer;
        [SerializeField] private TextMeshPro monitorWorldText;

        private GameSession _session;
        private AccessibilitySettings _accessibility;
        private AudioManager _audioManager;
        private MaterialPropertyBlock _mpb;

        private float[] _baseRoomLightIntensities;
        private Color[] _baseRoomLightColors;
        private float _baseMedbayIntensity = 1.6f;
        private float _baseOverheadIntensity = 2.4f;

        private bool _anyCritical;
        private bool _hasUnreadComms;
        private bool _isBlackout;
        private bool _endingOverrideActive;
        private float _commanderHealthRatio = 0.6f;
        private readonly StringBuilder _sb = new StringBuilder(128);

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            CacheBaseIntensities();
        }

        private void OnDestroy()
        {
            if (_session != null)
                _session.OnStateChanged -= HandleStateChanged;
        }

        public void Initialize(GameSession session, AccessibilitySettings accessibility, AudioManager audioManager)
        {
            if (_session != null)
                _session.OnStateChanged -= HandleStateChanged;

            _session = session;
            _accessibility = accessibility;
            _audioManager = audioManager;

            CacheBaseIntensities();

            if (_session != null)
            {
                _session.OnStateChanged += HandleStateChanged;
                if (_session.CurrentSnapshot != null)
                    HandleStateChanged(_session.CurrentSnapshot);
            }
        }

        public void ConfigureReferences(
            Renderer[] statusRenderers,
            TextMeshPro[] statusLabels,
            Renderer alarmRenderer,
            Light alarmLight,
            ParticleSystem dustParticles,
            Renderer radioLed,
            Light radioLight,
            Light[] roomLights,
            Light redEmergencyLight,
            Light medLight,
            Renderer medWindowRenderer,
            Renderer screenRenderer,
            TextMeshPro screenWorldText)
        {
            statusLightRenderers = statusRenderers;
            statusLightLabels = statusLabels;
            overheadAlarmRenderer = alarmRenderer;
            overheadAlarmLight = alarmLight;
            marsDustParticles = dustParticles;
            if (dustParticles != null)
                marsDustRenderer = dustParticles.GetComponent<ParticleSystemRenderer>();
            radioLedRenderer = radioLed;
            radioPointLight = radioLight;
            mainRoomLights = roomLights;
            emergencyRedLight = redEmergencyLight;
            medbayLight = medLight;
            medbayWindowRenderer = medWindowRenderer;
            monitorScreenRenderer = screenRenderer;
            monitorWorldText = screenWorldText;
            CacheBaseIntensities();
        }

        private void Update()
        {
            if (_endingOverrideActive) return;

            bool reduceMotion = _accessibility != null && _accessibility.ReduceMotion;

            // 1. Pulsing red overhead alarm when any resource is CRITICAL
            if (overheadAlarmLight != null || overheadAlarmRenderer != null)
            {
                if (_anyCritical)
                {
                    float pulse = reduceMotion ? 0.85f : (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5.2f)));
                    if (overheadAlarmLight != null)
                    {
                        overheadAlarmLight.enabled = true;
                        overheadAlarmLight.intensity = _baseOverheadIntensity * pulse;
                    }
                    if (overheadAlarmRenderer != null)
                        ApplyEmissive(overheadAlarmRenderer, ColorCrit, pulse * 2.2f);
                }
                else
                {
                    if (overheadAlarmLight != null) overheadAlarmLight.enabled = false;
                    if (overheadAlarmRenderer != null) ApplyEmissive(overheadAlarmRenderer, ColorOff, 0f);
                }
            }

            // 2. Blinking desk radio LED when unread Comms message exists
            if (radioLedRenderer != null || radioPointLight != null)
            {
                if (_hasUnreadComms)
                {
                    bool on = reduceMotion || Mathf.Sin(Time.time * 4.5f) > 0f;
                    Color c = on ? new Color(0.25f, 0.95f, 0.55f, 1f) : ColorOff;
                    if (radioLedRenderer != null) ApplyEmissive(radioLedRenderer, c, on ? 2.0f : 0f);
                    if (radioPointLight != null) radioPointLight.enabled = on;
                }
                else
                {
                    if (radioLedRenderer != null) ApplyEmissive(radioLedRenderer, ColorOff, 0f);
                    if (radioPointLight != null) radioPointLight.enabled = false;
                }
            }

            // 3. Medbay light brightness reflects injured commander's health + soft flicker
            if (medbayLight != null || medbayWindowRenderer != null)
            {
                float hpScale = Mathf.Lerp(0.18f, 1.0f, _commanderHealthRatio);
                float flicker = reduceMotion
                    ? 1.0f
                    : (0.86f + 0.14f * Mathf.PerlinNoise(Time.time * 3.4f, 1.7f));
                float combined = hpScale * flicker;

                if (medbayLight != null)
                    medbayLight.intensity = _baseMedbayIntensity * combined;
                if (medbayWindowRenderer != null)
                {
                    Color medColor = Color.Lerp(new Color(0.85f, 0.25f, 0.20f, 1f), new Color(0.45f, 0.85f, 0.95f, 1f), _commanderHealthRatio);
                    ApplyEmissive(medbayWindowRenderer, medColor, 0.9f * combined);
                }
            }

            // 4. Blackout emergency red glow pulse
            if (emergencyRedLight != null && _isBlackout)
            {
                float pulse = reduceMotion ? 1.0f : (0.65f + 0.35f * Mathf.Sin(Time.time * 2.5f));
                emergencyRedLight.intensity = 1.6f * pulse;
            }
        }

        private void HandleStateChanged(SessionSnapshot snap)
        {
            if (snap == null || _endingOverrideActive) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            UpdateStatusLights(snap);
            UpdateDustParticles(snap);
            UpdateRoomLights(snap);
            UpdateCommanderHealth(snap);
            UpdateMonitorScreenText(snap);
            UpdatePart3WorldBindings(snap);

            _hasUnreadComms = snap.UnreadCommsCount > 0;
            _audioManager?.ApplySnapshot(snap);
        }

        [Header("Part 3 - Corridor, Bays & Mars Surface")]
        [SerializeField] private AirlockController airlockController;
        [SerializeField] private Renderer[] corridorStatusRenderers;
        [SerializeField] private TextMeshPro[] corridorStatusLabels;
        [SerializeField] private Transform[] greenhouseCropTransforms;
        [SerializeField] private Renderer[] greenhouseCropRenderers;
        [SerializeField] private Light greenhouseGrowLight;
        [SerializeField] private Renderer medbayVitalRenderer;
        [SerializeField] private Renderer[] solarPanelRenderers;
        [SerializeField] private ParticleSystem surfaceDustParticles;
        [SerializeField] private Renderer marsSkyDomeRenderer;
        [SerializeField] private Light marsSunLight;

        private float _baseGreenhouseIntensity = 1.6f;
        private float _baseSunIntensity = 1.25f;

        public void ConfigurePart3Bindings(
            AirlockController airlock,
            Renderer[] corrRenderers,
            TextMeshPro[] corrLabels,
            Transform[] cropTransforms,
            Renderer[] cropRenderers,
            Light growLight,
            Renderer medVitalRend,
            Renderer[] solarRenderers,
            ParticleSystem surfaceDust,
            Renderer skyDome,
            Light sunLight)
        {
            airlockController = airlock;
            corridorStatusRenderers = corrRenderers;
            corridorStatusLabels = corrLabels;
            greenhouseCropTransforms = cropTransforms;
            greenhouseCropRenderers = cropRenderers;
            greenhouseGrowLight = growLight;
            medbayVitalRenderer = medVitalRend;
            solarPanelRenderers = solarRenderers;
            surfaceDustParticles = surfaceDust;
            marsSkyDomeRenderer = skyDome;
            marsSunLight = sunLight;
            if (greenhouseGrowLight != null) _baseGreenhouseIntensity = greenhouseGrowLight.intensity;
            if (marsSunLight != null) _baseSunIntensity = marsSunLight.intensity;
        }

        private void UpdatePart3WorldBindings(SessionSnapshot snap)
        {
            if (airlockController != null)
                airlockController.OnSessionSnapshotChanged(snap);

            // Corridor secondary 5-light monitor
            float okMin = snap.StatusLightOkThreshold > 0f ? snap.StatusLightOkThreshold : 40f;
            float lowMin = snap.StatusLightLowThreshold > 0f ? snap.StatusLightLowThreshold : 20f;
            float[] values = { snap.Power, snap.Oxygen, snap.Water, snap.Food, snap.Shielding };
            string[] shortKeys = { "light.power", "light.oxygen", "light.water", "light.food", "light.shielding" };
            for (int i = 0; i < 5; i++)
            {
                float v = values[i];
                Color c = v >= okMin ? ColorOk : (v >= lowMin ? ColorLow : ColorCrit);
                string tag = Localization.Get(v >= okMin ? "status.ok" : (v >= lowMin ? "status.low" : "status.crit"));
                if (corridorStatusRenderers != null && i < corridorStatusRenderers.Length && corridorStatusRenderers[i] != null)
                    ApplyEmissive(corridorStatusRenderers[i], c, 1.8f);
                if (corridorStatusLabels != null && i < corridorStatusLabels.Length && corridorStatusLabels[i] != null)
                {
                    corridorStatusLabels[i].text = $"{Localization.Get(shortKeys[i])}\n{Mathf.RoundToInt(v)}% {tag}";
                    corridorStatusLabels[i].color = c;
                }
            }

            // Greenhouse crops health & LED grow light
            float cropRatio = Mathf.Clamp01(snap.CropHealthPct / 100f);
            Color healthyGreen = new Color(0.24f, 0.72f, 0.34f, 1f);
            Color wiltedYellow = new Color(0.54f, 0.42f, 0.16f, 1f);
            Color cropColor = Color.Lerp(wiltedYellow, healthyGreen, cropRatio);
            if (greenhouseCropTransforms != null)
            {
                for (int i = 0; i < greenhouseCropTransforms.Length; i++)
                {
                    if (greenhouseCropTransforms[i] != null)
                    {
                        float h = Mathf.Lerp(0.35f, 1.0f, cropRatio);
                        greenhouseCropTransforms[i].localScale = new Vector3(1f, h, 1f);
                    }
                }
            }
            if (greenhouseCropRenderers != null)
            {
                for (int i = 0; i < greenhouseCropRenderers.Length; i++)
                    ApplyEmissive(greenhouseCropRenderers[i], cropColor, 0.12f);
            }
            if (greenhouseGrowLight != null)
            {
                float ghPowerFactor = snap.Power <= 0.01f ? 0.05f : Mathf.Lerp(0.25f, 1.0f, snap.GreenhouseAllocPct / 100f);
                greenhouseGrowLight.intensity = _baseGreenhouseIntensity * ghPowerFactor;
            }

            // Medbay Commander Vale vital lamp
            if (medbayVitalRenderer != null)
            {
                float valeHp = 0f;
                for (int i = 0; i < snap.Crew.Count; i++)
                {
                    if (snap.Crew[i].IsFallenCommander) { valeHp = snap.Crew[i].IsAlive ? snap.Crew[i].Health : 0f; break; }
                }
                Color vitalCol = valeHp >= 60f ? ColorOk : (valeHp >= 30f ? ColorLow : ColorCrit);
                ApplyEmissive(medbayVitalRenderer, vitalCol, valeHp > 0f ? 1.5f : 0.2f);
            }

            // Exterior Solar Panels dust buildup tint
            if (solarPanelRenderers != null)
            {
                float dustRatio = Mathf.Clamp01(snap.PanelDustPct / 100f);
                Color cleanBlue = new Color(0.11f, 0.22f, 0.42f, 1f);
                Color dustyMars = new Color(0.56f, 0.34f, 0.22f, 1f);
                Color panelCol = Color.Lerp(cleanBlue, dustyMars, dustRatio * 0.85f);
                for (int i = 0; i < solarPanelRenderers.Length; i++)
                    ApplyEmissive(solarPanelRenderers[i], panelCol, 0.05f);
            }

            // Surface dust & Martian sky/fog/sun lighting
            bool inStorm = snap.DustStormSolsRemaining > 0;
            bool flare = snap.SolarFlareActive;
            if (surfaceDustParticles != null)
            {
                var em = surfaceDustParticles.emission;
                em.rateOverTime = inStorm ? 260f : 32f;
                var main = surfaceDustParticles.main;
                main.startSpeed = inStorm ? 7.5f : 1.6f;
                main.startColor = inStorm
                    ? new Color(0.82f, 0.42f, 0.22f, 0.72f)
                    : new Color(0.84f, 0.54f, 0.36f, 0.30f);
            }

            Color skyCol = inStorm
                ? new Color(0.46f, 0.22f, 0.14f, 1f)
                : (flare ? new Color(0.76f, 0.42f, 0.20f, 1f) : new Color(0.62f, 0.34f, 0.24f, 1f));
            if (marsSkyDomeRenderer != null)
                ApplyEmissive(marsSkyDomeRenderer, skyCol, flare ? 0.55f : 0.35f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = inStorm ? new Color(0.49f, 0.25f, 0.16f, 1f) : new Color(0.68f, 0.39f, 0.27f, 1f);
            RenderSettings.fogDensity = inStorm ? 0.036f : 0.0085f;
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_HorizonColor"))
            {
                RenderSettings.skybox.SetColor("_HorizonColor", RenderSettings.fogColor);
                if (RenderSettings.skybox.HasProperty("_MidColor"))
                    RenderSettings.skybox.SetColor("_MidColor", inStorm ? new Color(0.56f, 0.30f, 0.21f, 1f) : new Color(0.76f, 0.44f, 0.34f, 1f));
                if (RenderSettings.skybox.HasProperty("_ZenithColor"))
                    RenderSettings.skybox.SetColor("_ZenithColor", inStorm ? new Color(0.28f, 0.16f, 0.17f, 1f) : new Color(0.38f, 0.23f, 0.28f, 1f));
            }

            if (marsSunLight != null)
            {
                marsSunLight.color = flare ? new Color(1.0f, 0.76f, 0.42f, 1f) : new Color(0.98f, 0.74f, 0.58f, 1f);
                marsSunLight.intensity = inStorm ? _baseSunIntensity * 0.42f : (flare ? _baseSunIntensity * 1.45f : _baseSunIntensity);
            }
        }

        public void SetEndingLightingOverride(bool active, bool won)
        {
            _endingOverrideActive = active;
            if (!active)
            {
                if (mainRoomLights != null && _baseRoomLightColors != null)
                {
                    for (int i = 0; i < mainRoomLights.Length && i < _baseRoomLightColors.Length; i++)
                    {
                        if (mainRoomLights[i] != null) mainRoomLights[i].color = _baseRoomLightColors[i];
                    }
                }
                if (_session?.CurrentSnapshot != null)
                    HandleStateChanged(_session.CurrentSnapshot);
                return;
            }

            _anyCritical = false;
            _isBlackout = false;
            _hasUnreadComms = false;

            if (overheadAlarmLight != null) overheadAlarmLight.enabled = false;
            if (overheadAlarmRenderer != null) ApplyEmissive(overheadAlarmRenderer, ColorOff, 0f);
            if (emergencyRedLight != null) emergencyRedLight.enabled = false;
            if (radioPointLight != null) radioPointLight.enabled = false;
            if (radioLedRenderer != null) ApplyEmissive(radioLedRenderer, ColorOff, 0f);

            if (won)
            {
                Color warm = new Color(1.0f, 0.80f, 0.52f, 1f);
                if (mainRoomLights != null && _baseRoomLightIntensities != null)
                {
                    for (int i = 0; i < mainRoomLights.Length; i++)
                    {
                        if (mainRoomLights[i] == null) continue;
                        mainRoomLights[i].color = warm;
                        mainRoomLights[i].intensity = _baseRoomLightIntensities[i] * 1.25f;
                    }
                }
                if (medbayLight != null) medbayLight.intensity = _baseMedbayIntensity;
            }
            else
            {
                if (mainRoomLights != null)
                {
                    for (int i = 0; i < mainRoomLights.Length; i++)
                    {
                        if (mainRoomLights[i] != null) mainRoomLights[i].intensity = 0f;
                    }
                }
                if (medbayLight != null) medbayLight.intensity = 0f;
                if (medbayWindowRenderer != null) ApplyEmissive(medbayWindowRenderer, ColorOff, 0f);
                if (monitorScreenRenderer != null) ApplyEmissive(monitorScreenRenderer, Color.black, 0f);
                if (monitorWorldText != null) monitorWorldText.text = string.Empty;
            }
        }

        private void UpdateStatusLights(SessionSnapshot snap)
        {
            float okMin = snap.StatusLightOkThreshold > 0f ? snap.StatusLightOkThreshold : 40f;
            float lowMin = snap.StatusLightLowThreshold > 0f ? snap.StatusLightLowThreshold : 20f;

            float[] values = { snap.Power, snap.Oxygen, snap.Water, snap.Food, snap.Shielding };
            string[] shortKeys = { "light.power", "light.oxygen", "light.water", "light.food", "light.shielding" };

            bool crit = false;
            for (int i = 0; i < 5; i++)
            {
                float v = values[i];
                Color c;
                string tag;
                if (v >= okMin)
                {
                    c = ColorOk;
                    tag = Localization.Get("status.ok");
                }
                else if (v >= lowMin)
                {
                    c = ColorLow;
                    tag = Localization.Get("status.low");
                }
                else
                {
                    c = ColorCrit;
                    tag = Localization.Get("status.crit");
                    crit = true;
                }

                if (statusLightRenderers != null && i < statusLightRenderers.Length && statusLightRenderers[i] != null)
                    ApplyEmissive(statusLightRenderers[i], c, 1.8f);

                if (statusLightLabels != null && i < statusLightLabels.Length && statusLightLabels[i] != null)
                {
                    _sb.Clear();
                    _sb.Append(Localization.Get(shortKeys[i])).Append('\n').Append(Mathf.RoundToInt(v)).Append("% ").Append(tag);
                    statusLightLabels[i].text = _sb.ToString();
                    statusLightLabels[i].color = c;
                }
            }
            _anyCritical = crit;
        }

        private void UpdateDustParticles(SessionSnapshot snap)
        {
            if (marsDustParticles == null) return;
            bool inStorm = snap.DustStormSolsRemaining > 0;
            float dustRatio = Mathf.Clamp01(snap.PanelDustPct / 100f);

            var emission = marsDustParticles.emission;
            emission.rateOverTime = inStorm ? 140f : Mathf.Lerp(10f, 38f, dustRatio);

            var main = marsDustParticles.main;
            main.startSpeed = inStorm ? 6.2f : Mathf.Lerp(1.0f, 2.6f, dustRatio);
            main.startColor = inStorm
                ? new Color(0.86f, 0.45f, 0.25f, 0.65f)
                : new Color(0.84f, 0.52f, 0.34f, 0.32f);
        }

        private void UpdateRoomLights(SessionSnapshot snap)
        {
            _isBlackout = snap.Power <= 0.01f;
            float factor = _isBlackout ? 0.06f : (snap.Power < 25f ? 0.52f : 1.0f);

            if (mainRoomLights != null && _baseRoomLightIntensities != null)
            {
                for (int i = 0; i < mainRoomLights.Length; i++)
                {
                    if (mainRoomLights[i] != null)
                        mainRoomLights[i].intensity = _baseRoomLightIntensities[i] * factor;
                }
            }

            if (emergencyRedLight != null)
                emergencyRedLight.enabled = _isBlackout;
        }

        private void UpdateCommanderHealth(SessionSnapshot snap)
        {
            float hp = 0f;
            for (int i = 0; i < snap.Crew.Count; i++)
            {
                if (snap.Crew[i].IsFallenCommander)
                {
                    hp = snap.Crew[i].IsAlive ? snap.Crew[i].Health : 0f;
                    break;
                }
            }
            _commanderHealthRatio = Mathf.Clamp01(hp / 100f);
        }

        private void UpdateMonitorScreenText(SessionSnapshot snap)
        {
            if (monitorScreenRenderer != null)
            {
                Color scrCol = snap.AlertSeverity == 2
                    ? new Color(0.32f, 0.08f, 0.08f, 1f)
                    : new Color(0.05f, 0.16f, 0.22f, 1f);
                ApplyEmissive(monitorScreenRenderer, scrCol, 1.0f);
            }

            if (monitorWorldText != null)
            {
                bool pendingEvent = snap.ActiveEvent != null && !snap.ActiveEvent.IsResolved;
                _sb.Clear();
                _sb.Append("ARES-4 TERMINAL\n");
                _sb.Append("SOL ").Append(snap.CurrentSol).Append('/').Append(snap.TotalSols).Append('\n');
                if (pendingEvent)
                    _sb.Append("[!] EVENT PENDING");
                else
                    _sb.Append(snap.AlertSeverity == 2 ? "[CRITICAL]" : snap.AlertSeverity == 1 ? "[LOW]" : "[OK]");
                monitorWorldText.text = _sb.ToString();
            }
        }

        private void CacheBaseIntensities()
        {
            if (mainRoomLights != null && mainRoomLights.Length > 0)
            {
                if (_baseRoomLightIntensities == null || _baseRoomLightIntensities.Length != mainRoomLights.Length)
                {
                    _baseRoomLightIntensities = new float[mainRoomLights.Length];
                    _baseRoomLightColors = new Color[mainRoomLights.Length];
                    for (int i = 0; i < mainRoomLights.Length; i++)
                    {
                        _baseRoomLightIntensities[i] = mainRoomLights[i] != null ? mainRoomLights[i].intensity : 1f;
                        _baseRoomLightColors[i] = mainRoomLights[i] != null ? mainRoomLights[i].color : Color.white;
                    }
                }
            }
            if (medbayLight != null && _baseMedbayIntensity <= 0.01f)
                _baseMedbayIntensity = medbayLight.intensity;
            if (overheadAlarmLight != null && _baseOverheadIntensity <= 0.01f)
                _baseOverheadIntensity = overheadAlarmLight.intensity;
        }

        private void ApplyEmissive(Renderer rend, Color baseColor, float emissionIntensity)
        {
            if (rend == null) return;
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorProp, baseColor);
            _mpb.SetColor(ColorProp, baseColor);
            _mpb.SetColor(EmissionColorProp, baseColor * emissionIntensity);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
