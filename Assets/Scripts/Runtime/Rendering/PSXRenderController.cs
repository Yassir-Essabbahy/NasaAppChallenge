using System.Collections.Generic;
using Outpost.Runtime.Room;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Rendering
{
    /// <summary>
    /// Drives the PSX low-resolution world RenderTexture pipeline:
    /// renders the 3D world camera to a point-filtered RenderTexture (e.g. 480x270) displayed
    /// inside an aspect-preserving RawImage on the lowest-sorted overlay canvas ("WorldView"),
    /// while pushing global PSX shader uniforms and active point lights.
    /// </summary>
    [ExecuteAlways]
    public class PSXRenderController : MonoBehaviour
    {
        private static readonly int PointLightPosProp = Shader.PropertyToID("_PSX_PointLightPos");
        private static readonly int PointLightColorProp = Shader.PropertyToID("_PSX_PointLightColor");

        [Header("PSX Configuration & World References")]
        [SerializeField] private PSXSettings settings;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private AccessibilitySettings accessibility;

        [Header("WorldView Overlay UI (Lowest Sorting Order)")]
        [SerializeField] private Canvas worldViewCanvas;
        [SerializeField] private RawImage worldViewImage;
        [SerializeField] private AspectRatioFitter aspectFitter;
        [SerializeField] private Material postMaterial;
        [SerializeField] private Camera displayCamera;

        private RenderTexture _rt;
        private int _rtWidth;
        private int _rtHeight;
        private readonly List<Light> _pointLights = new List<Light>(16);
        private readonly Vector4[] _lightPosArray = new Vector4[4];
        private readonly Vector4[] _lightColArray = new Vector4[4];
        private float _nextLightScanTime = -1f;

        public PSXSettings Settings => settings;
        public Camera WorldCamera => worldCamera;
        public RenderTexture ActiveRenderTexture => _rt;

        public void ConfigureReferences(
            PSXSettings psxSettings,
            Camera cam,
            AccessibilitySettings acc,
            Canvas viewCanvas,
            RawImage viewImage,
            AspectRatioFitter fitter,
            Material psxPostMat)
        {
            settings = psxSettings;
            worldCamera = cam;
            accessibility = acc;
            worldViewCanvas = viewCanvas;
            worldViewImage = viewImage;
            aspectFitter = fitter;
            postMaterial = psxPostMat;
            RefreshLightCache();
            ApplyPipelineState();
        }

        private void Awake()
        {
            if (settings == null)
                settings = Resources.Load<PSXSettings>("PSXSettings");
            if (worldCamera == null)
                worldCamera = Camera.main;
            RefreshLightCache();
        }

        private void Start()
        {
            if (Application.isPlaying && settings != null)
                settings.LoadFromPlayerPrefs();
            ApplyPipelineState();
        }

        private void OnEnable()
        {
            if (settings != null)
                settings.OnSettingsChanged += HandleSettingsChanged;
            if (accessibility != null)
                accessibility.OnSettingsChanged += HandleSettingsChanged;
            RefreshLightCache();
            ApplyPipelineState();
        }

        private void OnDisable()
        {
            if (settings != null)
                settings.OnSettingsChanged -= HandleSettingsChanged;
            if (accessibility != null)
                accessibility.OnSettingsChanged -= HandleSettingsChanged;
            EnsureDisplayCamera(false);
            ReleaseRenderTexture();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void OnValidate()
        {
            ApplyGlobalsAndLights();
        }

        private void LateUpdate()
        {
            ApplyGlobalsAndLights();
            EnsureRenderTargetValid();
        }

        public void HandleSettingsChanged()
        {
            ApplyPipelineState();
        }

        public void ApplyPipelineState()
        {
            ApplyGlobalsAndLights();
            EnsureRenderTargetValid();
        }

        private void ApplyGlobalsAndLights()
        {
            if (settings == null)
                settings = Resources.Load<PSXSettings>("PSXSettings");

            bool reduceFlicker = accessibility != null && accessibility.ReduceFlicker;
            if (settings != null)
                settings.ApplyGlobals(reduceFlicker);

            if (Time.unscaledTime >= _nextLightScanTime || _pointLights.Count == 0)
            {
                RefreshLightCache();
                _nextLightScanTime = Time.unscaledTime + 1.5f;
            }

            Vector3 camPos = worldCamera != null ? worldCamera.transform.position : Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                _lightPosArray[i] = Vector4.zero;
                _lightColArray[i] = Vector4.zero;
            }

            // Pick up to 4 brightest weighted active point lights for per-vertex PSX lighting
            var bestScore = new float[4];
            var bestIdx = new int[] { -1, -1, -1, -1 };

            for (int i = 0; i < _pointLights.Count; i++)
            {
                Light l = _pointLights[i];
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy || l.intensity <= 0.001f)
                    continue;

                float distSq = (l.transform.position - camPos).sqrMagnitude;
                float score = (l.intensity * Mathf.Max(l.range, 0.5f)) / (1f + distSq * 0.12f);

                for (int slot = 0; slot < 4; slot++)
                {
                    if (score > bestScore[slot])
                    {
                        for (int shift = 3; shift > slot; shift--)
                        {
                            bestScore[shift] = bestScore[shift - 1];
                            bestIdx[shift] = bestIdx[shift - 1];
                        }
                        bestScore[slot] = score;
                        bestIdx[slot] = i;
                        break;
                    }
                }
            }

            for (int slot = 0; slot < 4; slot++)
            {
                int idx = bestIdx[slot];
                if (idx < 0) continue;
                Light l = _pointLights[idx];
                Vector3 p = l.transform.position;
                float invRangeSq = 1f / Mathf.Max(l.range * l.range, 0.001f);
                _lightPosArray[slot] = new Vector4(p.x, p.y, p.z, invRangeSq);
                Color c = l.color;
                _lightColArray[slot] = new Vector4(c.r * l.intensity, c.g * l.intensity, c.b * l.intensity, 1f);
            }

            Shader.SetGlobalVectorArray(PointLightPosProp, _lightPosArray);
            Shader.SetGlobalVectorArray(PointLightColorProp, _lightColArray);
        }

        private void RefreshLightCache()
        {
            _pointLights.Clear();
            var allLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < allLights.Length; i++)
            {
                if (allLights[i] != null && allLights[i].type == LightType.Point)
                    _pointLights.Add(allLights[i]);
            }
        }

        private void EnsureRenderTargetValid()
        {
            if (worldCamera == null) return;

            bool hasPostShader = postMaterial != null || Shader.Find("Outpost/PSXPost") != null;
            bool useLowRes = settings != null && settings.enabled && settings.preset != PSXResolutionPreset.Off && hasPostShader;
            if (!useLowRes)
            {
                if (worldViewCanvas != null && worldViewCanvas.gameObject.activeSelf)
                    worldViewCanvas.gameObject.SetActive(false);
                if (worldViewImage != null && worldViewImage.texture != null)
                    worldViewImage.texture = null;
                EnsureDisplayCamera(false);
                ReleaseRenderTexture();
                return;
            }

            int targetW = Mathf.Clamp(settings.renderWidth, 64, 1920);
            int targetH = Mathf.Clamp(settings.renderHeight, 36, 1080);

            if (_rt == null || !_rt.IsCreated() || _rtWidth != targetW || _rtHeight != targetH)
            {
                ReleaseRenderTexture();
                _rtWidth = targetW;
                _rtHeight = targetH;
                _rt = new RenderTexture(targetW, targetH, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"RT_WorldPSX_{targetW}x{targetH}",
                    filterMode = FilterMode.Point,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                _rt.Create();
            }

            worldCamera.allowMSAA = false;
            worldCamera.allowHDR = false;
            if (worldCamera.targetTexture != _rt)
                worldCamera.targetTexture = _rt;

            if (worldViewCanvas != null)
            {
                worldViewCanvas.sortingOrder = -100;
                if (!worldViewCanvas.gameObject.activeSelf)
                    worldViewCanvas.gameObject.SetActive(true);
            }

            if (worldViewImage != null)
            {
                worldViewImage.raycastTarget = false;
                if (worldViewImage.texture != _rt)
                    worldViewImage.texture = _rt;
                if (postMaterial == null)
                {
                    Shader postShader = Shader.Find("Outpost/PSXPost");
                    if (postShader != null)
                        postMaterial = new Material(postShader) { name = "M_PSXPost_Runtime" };
                }
                if (postMaterial != null && worldViewImage.material != postMaterial)
                    worldViewImage.material = postMaterial;
            }

            if (aspectFitter != null)
            {
                aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspectFitter.aspectRatio = targetW / (float)targetH;
            }

            EnsureDisplayCamera(true);
        }

        private void EnsureDisplayCamera(bool active)
        {
            if (displayCamera == null)
            {
                Transform existing = transform.Find("PSXDisplayCamera");
                if (existing != null)
                    displayCamera = existing.GetComponent<Camera>();
            }

            if (displayCamera == null)
            {
                if (!active) return;
                var go = new GameObject("PSXDisplayCamera");
                go.tag = "Untagged";
                go.transform.SetParent(transform, false);
                displayCamera = go.AddComponent<Camera>();
            }

            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = Color.black;
            displayCamera.depth = -100f;
            displayCamera.allowMSAA = false;
            displayCamera.allowHDR = false;
            displayCamera.useOcclusionCulling = false;
            displayCamera.targetTexture = null;
            displayCamera.targetDisplay = 0;
            if (displayCamera.enabled != active)
                displayCamera.enabled = active;
        }

        private void ReleaseRenderTexture()
        {
            if (worldCamera != null && worldCamera.targetTexture != null &&
                (worldCamera.targetTexture == _rt || worldCamera.targetTexture.name.StartsWith("RT_WorldPSX_")))
            {
                worldCamera.targetTexture = null;
            }

            if (_rt != null)
            {
                _rt.Release();
                if (Application.isPlaying)
                    Destroy(_rt);
                else
                    DestroyImmediate(_rt);
                _rt = null;
            }
            _rtWidth = 0;
            _rtHeight = 0;
        }
    }
}
