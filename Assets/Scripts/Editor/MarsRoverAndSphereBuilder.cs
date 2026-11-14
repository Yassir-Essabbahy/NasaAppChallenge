using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Outpost.Editor
{
    /// <summary>
    /// Integrates NASA/Mars assets into the Outpost project:
    /// A1) Mars.jpg -> PSXUnlit MarsSphere.prefab + TitleMarsRig + distant sky instance through Command window.
    /// A2) 25042_Perseverance.glb -> Prop_Rover.prefab (~3m, grounded, BoxCollider, Point PSX mats), placed near Solar Array.
    /// Also wires RoomHUD crosshair & lock icon sprites from IconLibrary.
    /// </summary>
    public static class MarsRoverAndSphereBuilder
    {
        public const string MarsTexturePath = "Assets/Models/Mars.jpg";
        public const string RoverGlbPath = "Assets/Models/25042_Perseverance.glb";
        public const string MainMenuWallpaperPath = "Assets/Art/UI/MainMenu.jpg";
        public const string PsxDir = "Assets/Art/PSX";
        public const string RoverArtDir = "Assets/Art/PSX/Rover";
        public const string PrefabsDir = "Assets/Prefabs";

        [MenuItem("Outpost/Integrate NASA Mars & Rover Assets")]
        public static void IntegrateMarsSphereAndRover()
        {
            if (!Directory.Exists(PsxDir)) Directory.CreateDirectory(PsxDir);
            if (!Directory.Exists(RoverArtDir)) Directory.CreateDirectory(RoverArtDir);
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);

            var marsPrefab = BuildMarsSpherePrefab();
            PlaceMarsSphereInstances(marsPrefab);
            BuildAndPlaceRoverPrefab();
            WireRoomHudIcons();

            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildMarsSpherePrefab()
        {
            var imp = AssetImporter.GetAtPath(MarsTexturePath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
                if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }

            var marsTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MarsTexturePath);
            Shader unlit = Shader.Find("Outpost/PSXUnlit");
            string matPath = $"{PsxDir}/M_MarsSphere.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(unlit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = unlit;
            mat.SetTexture("_MainTex", marsTex);
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_EmissionColor", new Color(0.18f, 0.08f, 0.04f, 1f));
            mat.SetFloat("_EmissionStrength", 0.35f);
            mat.SetFloat("_AffineAmount", 0.25f);
            mat.SetFloat("_FogWeight", 0.02f);
            EditorUtility.SetDirty(mat);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MarsSphere";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<MarsSphereRotator>().Configure(2.0f);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/MarsSphere.prefab");
            Object.DestroyImmediate(go);
            Debug.Log("[MarsRoverAndSphereBuilder] Built MarsSphere.prefab with Point-filtered Mars.jpg & PSXUnlit.");
            return saved;
        }

        private static void PlaceMarsSphereInstances(GameObject marsPrefab)
        {
            // 1. Title screen rotating background rig (rendered at 640x360 Point RT to TitleBackdropCanvas @ sort -50)
            var oldRig = GameObject.Find("TitleMarsRig");
            if (oldRig != null) Object.DestroyImmediate(oldRig);

            var rig = new GameObject("TitleMarsRig");
            rig.transform.position = Vector3.zero;

            var titlePlanet = (GameObject)PrefabUtility.InstantiatePrefab(marsPrefab, rig.transform);
            titlePlanet.name = "MarsSphere_Title";
            titlePlanet.transform.position = new Vector3(1.52f, 500f, 0f);
            titlePlanet.transform.rotation = Quaternion.Euler(14f, 25f, -12f);
            titlePlanet.transform.localScale = Vector3.one * 3.15f;

            const string rtPath = PsxDir + "/RT_TitleMars.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(640, 360, 16, RenderTextureFormat.ARGB32);
                rt.filterMode = FilterMode.Point;
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            rt.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(rt);

            var camGo = new GameObject("TitleMarsCamera");
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.position = new Vector3(0f, 500f, -5.6f);
            camGo.transform.LookAt(new Vector3(0f, 500f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 30f;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.targetTexture = rt;

            var bgCanvasGo = new GameObject("TitleBackdropCanvas");
            bgCanvasGo.transform.SetParent(rig.transform, false);
            var bgCanvas = bgCanvasGo.AddComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bgCanvas.sortingOrder = -50; // Above WorldViewCanvas (-100), below FlowCanvas (+30)

            var mainMenuTex = LoadAndConfigureMainMenuWallpaper();
            if (mainMenuTex != null)
            {
                var wpGo = new GameObject("MainMenuWallpaper");
                wpGo.transform.SetParent(bgCanvasGo.transform, false);
                var wpRect = wpGo.AddComponent<RectTransform>();
                wpRect.anchorMin = Vector2.zero;
                wpRect.anchorMax = Vector2.one;
                wpRect.offsetMin = Vector2.zero;
                wpRect.offsetMax = Vector2.zero;
                var wpImg = wpGo.AddComponent<RawImage>();
                wpImg.texture = mainMenuTex;
                wpImg.color = Color.white;
                wpImg.raycastTarget = false;
                var arf = wpGo.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                arf.aspectRatio = mainMenuTex.width / (float)Mathf.Max(1, mainMenuTex.height);
            }

            var flowCtrl = Object.FindAnyObjectByType<MissionFlowController>();
            if (flowCtrl != null && mainMenuTex != null)
            {
                var soFlow = new SerializedObject(flowCtrl);
                var prop = soFlow.FindProperty("titleWallpaperTexture");
                if (prop != null)
                {
                    prop.objectReferenceValue = mainMenuTex;
                    soFlow.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var rawGo = new GameObject("TitleMarsImage");
            rawGo.transform.SetParent(bgCanvasGo.transform, false);
            var rawRect = rawGo.AddComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;
            var rawImg = rawGo.AddComponent<RawImage>();
            rawImg.texture = rt;
            rawImg.color = Color.white;
            rawImg.raycastTarget = false;

            // 2. Distant orbital/sky body visible through Command Module +Z window, low in the sky
            var oldSkyOrb = GameObject.Find("MarsSphere_CommandWindow");
            if (oldSkyOrb != null) Object.DestroyImmediate(oldSkyOrb);

            var worldRoot = GameObject.Find("OutpostExpansionWorld");
            var surfaceRoot = worldRoot != null ? worldRoot.transform.Find("MarsSurfaceWorld") : null;
            var orb = (GameObject)PrefabUtility.InstantiatePrefab(marsPrefab, surfaceRoot);
            orb.name = "MarsSphere_CommandWindow";
            orb.transform.position = new Vector3(-4.5f, 9.8f, 68.0f);
            orb.transform.rotation = Quaternion.Euler(10f, 40f, 0f);
            orb.transform.localScale = Vector3.one * 6.5f;
        }

        private static Texture2D LoadAndConfigureMainMenuWallpaper()
        {
            var imp = AssetImporter.GetAtPath(MainMenuWallpaperPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
                if (!imp.sRGBTexture) { imp.sRGBTexture = true; dirty = true; }
                if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
                if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MainMenuWallpaperPath);
        }

        private static void BuildAndPlaceRoverPrefab()
        {
            var srcGlb = AssetDatabase.LoadAssetAtPath<GameObject>(RoverGlbPath);
            if (srcGlb == null)
            {
                Debug.LogWarning($"[MarsRoverAndSphereBuilder] Could not load {RoverGlbPath} via glTFast. Convert 25042_Perseverance.glb to FBX in Blender and place under Assets/Models/, then re-run.");
                return;
            }

            var root = new GameObject("Prop_Rover");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(srcGlb, root.transform);
            if (visual == null) visual = Object.Instantiate(srcGlb, root.transform, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Measure raw triangle count & report clearly if above 20,000 tris
            int totalTris = 0;
            foreach (var mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                    totalTris += mf.sharedMesh.triangles.Length / 3;
            }
            if (totalTris > 20000)
            {
                Debug.LogWarning($"[MarsRoverAndSphereBuilder] 25042_Perseverance.glb has {totalTris:N0} triangles (> 20,000 budget). Decimate to <= 20,000 triangles in Blender for best WebGL/PSX performance (keeping current mesh active for now).");
            }

            // Measure unscaled bounds -> uniform scale to ~3.0m length & pivot cleanly on Y=0
            Bounds rawBounds = ComputeCombinedBounds(visual);
            float maxLen = Mathf.Max(rawBounds.size.x, rawBounds.size.y, rawBounds.size.z);
            float uniformScale = maxLen > 0.001f ? (3.0f / maxLen) : 1.0f;
            visual.transform.localScale = Vector3.one * uniformScale;

            Bounds scaledBounds = ComputeCombinedBounds(visual);
            visual.transform.localPosition = new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);
            Bounds finalBounds = ComputeCombinedBounds(visual);

            ConvertRoverMaterialsToPsx(visual);

            var col = root.AddComponent<BoxCollider>();
            col.center = finalBounds.center;
            col.size = new Vector3(
                Mathf.Max(0.8f, finalBounds.size.x),
                Mathf.Max(0.8f, finalBounds.size.y),
                Mathf.Max(0.8f, finalBounds.size.z));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/Prop_Rover.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[MarsRoverAndSphereBuilder] Built Prop_Rover.prefab (tris={totalTris:N0}, final size={finalBounds.size:F2}, uniformScale={uniformScale:F3}).");

            // Place near POI_SolarArray on the Martian surface and remove old primitive rover placeholder
            var worldRoot = GameObject.Find("OutpostExpansionWorld");
            var surfaceRoot = worldRoot != null ? worldRoot.transform.Find("MarsSurfaceWorld") : null;
            if (surfaceRoot == null) return;

            var oldPrimRover = surfaceRoot.Find("POI_PressurizedRover");
            if (oldPrimRover != null) oldPrimRover.gameObject.SetActive(false);

            var existing = surfaceRoot.Find("Prop_Rover");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var roverInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, surfaceRoot);
            roverInst.name = "Prop_Rover";
            roverInst.transform.SetPositionAndRotation(new Vector3(-4.6f, -0.08f, -24.8f), Quaternion.Euler(0f, 32f, 0f));

            var interactable = roverInst.GetComponent<InteractableProp>();
            if (interactable == null) interactable = roverInst.AddComponent<InteractableProp>();
            interactable.ConfigureCustomFact(
                "[E] Inspect NASA Perseverance Rover",
                "Fact: NASA's Perseverance rover carries MOXIE to turn Martian CO2 into oxygen [source: nasa-perseverance-model]",
                "nasa-perseverance-model",
                InteractablePropType.SurfaceRover);
        }

        private static Bounds ComputeCombinedBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(Vector3.zero, Vector3.one);
            bool any = false;
            foreach (var r in rends)
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void ConvertRoverMaterialsToPsx(GameObject visual)
        {
            Shader psxLit = Shader.Find("Outpost/PSXLit");
            var convertedCache = new Dictionary<Material, Material>();
            var texCache = new Dictionary<Texture, Texture2D>();
            int matIdx = 0;

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!convertedCache.TryGetValue(src, out Material psxMat))
                    {
                        string safeName = SanitizeName(src.name);
                        string matPath = $"{RoverArtDir}/M_Rover_{matIdx:00}_{safeName}.mat";
                        matIdx++;
                        psxMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (psxMat == null)
                        {
                            psxMat = new Material(psxLit);
                            AssetDatabase.CreateAsset(psxMat, matPath);
                        }
                        psxMat.shader = psxLit;

                        Color tint = ReadColor(src, new[] { "baseColorFactor", "_BaseColor", "_Color" }, new Color(0.86f, 0.84f, 0.80f, 1f));
                        Texture srcTex = ReadTexture(src, new[] { "baseColorTexture", "_BaseMap", "_MainTex" });
                        Texture2D pointTex = BakeOrFetchPointTexture(srcTex, safeName, matIdx, texCache);

                        psxMat.SetTexture("_MainTex", pointTex);
                        psxMat.SetColor("_Color", tint);
                        psxMat.SetColor("_EmissionColor", Color.black);
                        psxMat.SetFloat("_EmissionStrength", 0f);
                        psxMat.SetFloat("_AffineAmount", 0.45f);
                        psxMat.SetFloat("_FogWeight", 0.85f);
                        EditorUtility.SetDirty(psxMat);
                        convertedCache[src] = psxMat;
                    }
                    mats[i] = psxMat;
                }
                r.sharedMaterials = mats;
            }
        }

        private static Texture2D BakeOrFetchPointTexture(Texture src, string hint, int idx, Dictionary<Texture, Texture2D> cache)
        {
            if (src == null) return null;
            if (cache.TryGetValue(src, out var cached)) return cached;

            // Always emit a Point-filtered, no-mipmap PNG (capped at 256px for authentic WebGL-safe PSX look)
            string texPath = $"{RoverArtDir}/T_Rover_{idx:00}_{hint}.png";
            int w = Mathf.Clamp(src.width, 16, 256);
            int h = Mathf.Clamp(src.height, 16, 256);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(texPath, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null)
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            cache[src] = loaded;
            return loaded;
        }

        private static Texture ReadTexture(Material m, string[] props)
        {
            foreach (var p in props)
            {
                if (m.HasProperty(p))
                {
                    var t = m.GetTexture(p);
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static Color ReadColor(Material m, string[] props, Color fallback)
        {
            foreach (var p in props)
            {
                if (m.HasProperty(p))
                {
                    Color c = m.GetColor(p);
                    c.a = 1f;
                    return c;
                }
            }
            return fallback;
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Mat";
            foreach (char c in Path.GetInvalidFileNameChars()) raw = raw.Replace(c, '_');
            return raw.Replace(' ', '_').Replace('.', '_');
        }

        private static void WireRoomHudIcons()
        {
            var iconLib = AssetDatabase.LoadAssetAtPath<IconLibrary>("Assets/Resources/IconLibrary.asset");
            if (iconLib == null) iconLib = AssetDatabase.LoadAssetAtPath<IconLibrary>("Assets/Art/UI/Icons/IconLibrary.asset");

            var hudCanvas = GameObject.Find("RoomHudCanvas");
            if (hudCanvas == null) return;
            var hudRoot = hudCanvas.transform.Find("RoomHudRoot");
            if (hudRoot == null) return;

            var dotTf = hudRoot.Find("CrosshairOutline/CrosshairDot");
            Image crosshairImg = dotTf != null ? dotTf.GetComponent<Image>() : null;
            if (crosshairImg != null && iconLib != null)
            {
                var targetSprite = iconLib.GetSprite("Icon_Target");
                if (targetSprite != null)
                {
                    crosshairImg.sprite = targetSprite;
                    crosshairImg.color = UITheme.AccentPrimary;
                    var dotRect = crosshairImg.rectTransform;
                    dotRect.sizeDelta = new Vector2(18f, 18f);
                }
            }

            var existingLockTf = hudRoot.Find("PromptLockIcon");
            Image lockImg = existingLockTf != null ? existingLockTf.GetComponent<Image>() : null;
            if (lockImg == null)
            {
                var lockGo = new GameObject("PromptLockIcon");
                lockGo.transform.SetParent(hudRoot, false);
                var lr = lockGo.AddComponent<RectTransform>();
                lr.anchorMin = new Vector2(0.5f, 0.5f);
                lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(-135f, -38f);
                lr.sizeDelta = new Vector2(18f, 18f);
                lockImg = lockGo.AddComponent<Image>();
                lockImg.raycastTarget = false;
            }
            if (iconLib != null)
            {
                lockImg.sprite = iconLib.GetSprite("Icon_Lock");
                lockImg.color = UITheme.WarnStatic;
            }
            lockImg.enabled = false;

            var fpCtrl = Object.FindAnyObjectByType<FirstPersonRoomController>();
            if (fpCtrl != null)
            {
                var so = new SerializedObject(fpCtrl);
                so.FindProperty("crosshairIcon").objectReferenceValue = crosshairImg;
                so.FindProperty("promptLockIcon").objectReferenceValue = lockImg;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fpCtrl);
            }
        }
    }
}
