using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Editor
{
    /// <summary>
    /// Integrates newly added PSX asset packs, fixes interior/exterior base textures with crisp
    /// atlas-derived Point-filtered PNGs and world-space Cube UVs, replaces primitive placeholders
    /// (radio, cryo bed, crates, suit locker, airlock door slabs, radiation post), and adds extra
    /// set-dressing across the Outpost interior and Mars surface.
    /// </summary>
    public static class NewAssetsIntegrationBuilder
    {
        public const string SamplePackDir = "Assets/Models/PSX_Space_Station_Interior_FREE_Sample/PSX_Space_Station_Interior_FREE_Sample";
        public const string SampleFbxDir = SamplePackDir + "/Models/FBX";
        public const string SampleAtlasPath = SamplePackDir + "/Textures/psx_space_station_atlas.png";

        public const string RadioDir = "Assets/Models/psx-style-satellite-radio";
        public const string RadioFbxPath = RadioDir + "/Satellite Radio.fbx";
        public const string RadioTexDir = RadioDir + "/textures";

        public const string RadSignGlbPath = "Assets/Models/radiation_sign.glb";

        public const string PsxDir = "Assets/Art/PSX";
        public const string PsxTexDir = "Assets/Art/PSX/PSXTextures";
        public const string RadioArtDir = "Assets/Art/PSX/Radio";
        public const string WorldCubeMeshDir = "Assets/Art/PSX/Meshes/WorldCubes";
        public const string InteriorPrefabsDir = "Assets/Prefabs/Interior";
        public const string PrefabsDir = "Assets/Prefabs";
        public const string RoomMatDir = "Assets/Materials/Room";

        private static readonly Dictionary<string, Mesh> CubeUvMeshCache = new Dictionary<string, Mesh>();

        [MenuItem("Outpost/Integrate New Station, Radio & Sign Assets")]
        public static void IntegrateAllNewAssets()
        {
            EnsureDirectories();
            EnsureStationAtlasTextures();
            ConfigureNewImporters();

            Material atlasMat = EnsureStationAtlasMaterial();

            var pCryoPod = BuildStationPrefab("cryo_pod_open.fbx", "Prop_CryoPod", InteriorPrefabsDir, atlasMat, 1.0f, addBoxCollider: true);
            var pConsoleWide = BuildStationPrefab("control_console_wide.fbx", "Prop_ControlConsoleWide", InteriorPrefabsDir, atlasMat, 0.84f, addBoxCollider: true);
            var pSuitRack = BuildStationPrefab("space_suit_rack.fbx", "Prop_SpaceSuitRack", InteriorPrefabsDir, atlasMat, 1.0f, addBoxCollider: true);
            var pMaintBot = BuildStationPrefab("maintenance_robot.fbx", "Prop_MaintenanceRobot", InteriorPrefabsDir, atlasMat, 0.92f, addBoxCollider: true);
            var pHatchRound = BuildStationPrefab("airlock_hatch_round.fbx", "Prop_AirlockHatchRound", InteriorPrefabsDir, atlasMat, 1.0f, addBoxCollider: false);
            var pCorridorCorner = BuildStationPrefab("corridor_corner_4m.fbx", "Corridor_Corner_PSX", InteriorPrefabsDir, atlasMat, 1.0f, addBoxCollider: false);

            var pRadio = BuildSatelliteRadioPrefab();
            var pRadSign = BuildRadiationSignPrefab();

            FixBaseRoomAndHullMaterials();
            ApplyWorldSpaceCubeUVsToArchitecture();

            ReplacePlaceholdersAndPopulateScene(
                pCryoPod, pConsoleWide, pSuitRack, pMaintBot, pHatchRound, pCorridorCorner, pRadio, pRadSign);

            UpgradeExteriorHabitatHull(pHatchRound);
            CorridorKitBuilder.EnsureAllFacilityDoorButtons();

            AssetDatabase.SaveAssets();
            Debug.Log("[NewAssetsIntegrationBuilder] Integrated station pack, satellite radio, radiation sign, fixed base textures, and replaced placeholders.");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { PsxDir, PsxTexDir, RadioArtDir, WorldCubeMeshDir, InteriorPrefabsDir, PrefabsDir, RoomMatDir };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        /// <summary>
        /// Ensures psx_space_station_atlas.png is imported readable + Point-filtered and slices crisp 64x64
        /// seamless wall, floor, ceiling, trim, and exterior hull textures into Assets/Art/PSX/PSXTextures/.
        /// </summary>
        public static void EnsureStationAtlasTextures()
        {
            if (!Directory.Exists(PsxTexDir))
                Directory.CreateDirectory(PsxTexDir);

            var imp = AssetImporter.GetAtPath(SampleAtlasPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
                if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(SampleAtlasPath);
            if (atlas == null || atlas.width < 128 || atlas.height < 128)
            {
                Debug.LogWarning("[NewAssetsIntegrationBuilder] psx_space_station_atlas.png missing or unreadable.");
                return;
            }

            // Atlas 4x4 grid (32x32 cells), Unity y=0 is bottom row:
            // Top row (y=96): col0=riveted wall panel, col1=dark ribbed steel, col2=diamond plate floor, col3=floor grate
            // Second row (y=64): col0=hazard stripe, col1=light bulkhead+teal stripe, col2=teal metal, col3=orange panel
            ExtractAtlasCell64(atlas, 0, 96, "T_Station_WallPanel", brightness: 1.00f);
            ExtractAtlasCell64(atlas, 64, 96, "T_Station_FloorPlate", brightness: 0.96f);
            ExtractAtlasCell64(atlas, 0, 96, "T_Station_CeilingPanel", brightness: 0.80f);
            ExtractAtlasCell64(atlas, 32, 96, "T_Station_TrimHazard", brightness: 1.00f);
            ExtractAtlasCell64(atlas, 32, 64, "T_Station_HullPanel", brightness: 0.95f);
        }

        private static void ExtractAtlasCell64(Texture2D atlas, int cellX, int cellY, string outName, float brightness)
        {
            string outPath = $"{PsxTexDir}/{outName}.png";
            Color[] src = atlas.GetPixels(cellX, cellY, 32, 32);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                int sy = y / 2;
                for (int x = 0; x < 64; x++)
                {
                    int sx = x / 2;
                    Color c = src[sy * 32 + sx];
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(c.r * brightness),
                        Mathf.Clamp01(c.g * brightness),
                        Mathf.Clamp01(c.b * brightness),
                        1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (ti != null)
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        private static void ConfigureNewImporters()
        {
            string[] stationFbxs =
            {
                "cryo_pod_open.fbx", "control_console_wide.fbx", "space_suit_rack.fbx",
                "maintenance_robot.fbx", "airlock_hatch_round.fbx", "corridor_corner_4m.fbx"
            };
            foreach (string fn in stationFbxs)
                ConfigureFbxImporter($"{SampleFbxDir}/{fn}");

            if (File.Exists(RadioFbxPath))
                ConfigureFbxImporter(RadioFbxPath);
            else
                Debug.LogWarning($"[NewAssetsIntegrationBuilder] Missing {RadioFbxPath}; extract it from Satellite Radio.zip.");

            if (Directory.Exists(RadioTexDir))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RadioTexDir }))
                {
                    string tp = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                    if (ti == null) continue;
                    bool dirty = false;
                    if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
                    if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                    if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                    if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                    if (dirty) ti.SaveAndReimport();
                }
            }
        }

        private static void ConfigureFbxImporter(string assetPath)
        {
            var mi = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (mi == null) return;
            bool dirty = false;
            if (!mi.isReadable) { mi.isReadable = true; dirty = true; }
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }

        private static Material EnsureStationAtlasMaterial()
        {
            Shader psxLit = Shader.Find("Outpost/PSXLit");
            string matPath = $"{PsxDir}/M_PSX_StationAtlas.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(psxLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SampleAtlasPath);
            mat.shader = psxLit;
            mat.SetTexture("_MainTex", atlasTex);
            mat.SetColor("_Color", new Color(0.96f, 0.97f, 0.99f, 1f));
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EmissionStrength", 0f);
            mat.SetFloat("_AffineAmount", 0.48f);
            mat.SetFloat("_FogWeight", 0.16f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject BuildStationPrefab(
            string fbxName,
            string prefabName,
            string outDir,
            Material atlasMat,
            float uniformScale,
            bool addBoxCollider)
        {
            string fbxPath = $"{SampleFbxDir}/{fbxName}";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null)
            {
                Debug.LogWarning($"[NewAssetsIntegrationBuilder] Missing {fbxPath}");
                return null;
            }

            var root = new GameObject(prefabName);
            var visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";
            // Lowbit Lab Blender FBXs are exported Z-up; -90 around X rotates +Z to Unity +Y and -Y (front) to Unity +Z
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.transform.localScale = Vector3.one * uniformScale;

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = atlasMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            Bounds b = ComputeCombinedBounds(visual);
            visual.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            Bounds grounded = ComputeCombinedBounds(visual);

            if (addBoxCollider)
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = grounded.center;
                col.size = grounded.size;
            }

            string outPath = $"{outDir}/{prefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[NewAssetsIntegrationBuilder] Built {prefabName}.prefab size={grounded.size:F2}");
            return saved;
        }

        private static GameObject BuildSatelliteRadioPrefab()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(RadioFbxPath);
            if (src == null) return null;

            Shader psxLit = Shader.Find("Outpost/PSXLit");
            Material mTech = CreateOrUpdateTexMat($"{RadioArtDir}/M_Radio_Tech.mat", psxLit,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{RadioTexDir}/Tech Material.jpeg"), Color.white, 0.45f, 0.14f);
            Material mMetal = CreateOrUpdateTexMat($"{RadioArtDir}/M_Radio_Metal.mat", psxLit,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{RadioTexDir}/Metal.jpeg"), new Color(0.88f, 0.90f, 0.93f, 1f), 0.45f, 0.14f);
            Material mCable = CreateOrUpdateTexMat($"{RadioArtDir}/M_Radio_Cable.mat", psxLit,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{RadioTexDir}/Cable Texture.jpeg"), new Color(0.85f, 0.86f, 0.88f, 1f), 0.45f, 0.14f);
            Material mMic = CreateOrUpdateTexMat($"{RadioArtDir}/M_Radio_Mic.mat", psxLit,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{RadioTexDir}/Microphone Texture Rust.png"), Color.white, 0.45f, 0.14f);

            var root = new GameObject("Prop_SatelliteRadio");
            var visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (mn.Contains("cable")) mats[i] = mCable;
                    else if (mn.Contains("rust")) mats[i] = mMic;
                    else if (mn.Contains("microphone") || mn.Contains("metal")) mats[i] = mMetal;
                    else mats[i] = mTech;
                }
                r.sharedMaterials = mats;
            }

            Bounds raw = ComputeCombinedBounds(visual);
            float maxHoriz = Mathf.Max(raw.size.x, raw.size.z);
            float scale = maxHoriz > 0.001f ? (0.48f / maxHoriz) : 0.35f;
            visual.transform.localScale = Vector3.one * scale;
            Bounds scaled = ComputeCombinedBounds(visual);
            visual.transform.localPosition = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
            Bounds finalB = ComputeCombinedBounds(visual);

            var col = root.AddComponent<BoxCollider>();
            col.center = finalB.center;
            col.size = new Vector3(
                Mathf.Max(0.28f, finalB.size.x),
                Mathf.Max(0.26f, finalB.size.y),
                Mathf.Max(0.24f, finalB.size.z));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{InteriorPrefabsDir}/Prop_SatelliteRadio.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[NewAssetsIntegrationBuilder] Built Prop_SatelliteRadio.prefab size={finalB.size:F2}");
            return saved;
        }

        private static GameObject BuildRadiationSignPrefab()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(RadSignGlbPath);
            if (src == null) return null;

            Texture2D srcTex = null;
            foreach (var r in src.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("baseColorTexture")) srcTex = m.GetTexture("baseColorTexture") as Texture2D;
                    if (srcTex == null && m.HasProperty("_BaseMap")) srcTex = m.GetTexture("_BaseMap") as Texture2D;
                    if (srcTex == null && m.HasProperty("_MainTex")) srcTex = m.GetTexture("_MainTex") as Texture2D;
                    if (srcTex != null) break;
                }
                if (srcTex != null) break;
            }

            Texture2D pointSignTex = BakePointPng(srcTex, $"{PsxDir}/T_RadiationSign.png", 256);
            Shader psxLit = Shader.Find("Outpost/PSXLit");
            Material signMat = CreateOrUpdateTexMat($"{PsxDir}/M_RadiationSign.mat", psxLit, pointSignTex, Color.white, 0.40f, 0.65f);

            var root = new GameObject("Prop_RadiationSign");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            if (visual == null) visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";
            // Preserve the GLB root's imported rotation & scale while yaw-rotating +90 deg so broad face aligns with Z
            Quaternion baseRot = visual.transform.localRotation;
            Vector3 baseScale = visual.transform.localScale;
            visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * baseRot;

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = signMat;
                r.sharedMaterials = mats;
            }

            Bounds raw = ComputeCombinedBounds(visual);
            float scale = raw.size.y > 0.001f ? (2.05f / raw.size.y) : 1.0f;
            visual.transform.localScale = baseScale * scale;
            Bounds scaled = ComputeCombinedBounds(visual);
            visual.transform.localPosition = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
            Bounds finalB = ComputeCombinedBounds(visual);

            var col = root.AddComponent<BoxCollider>();
            col.center = finalB.center;
            col.size = new Vector3(
                Mathf.Max(0.85f, finalB.size.x),
                Mathf.Max(2.00f, finalB.size.y),
                Mathf.Max(0.45f, finalB.size.z));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/Prop_RadiationSign.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[NewAssetsIntegrationBuilder] Built Prop_RadiationSign.prefab size={finalB.size:F2}");
            return saved;
        }

        private static Texture2D BakePointPng(Texture src, string outPath, int maxSize)
        {
            if (src == null) return null;
            int w = Mathf.Clamp(src.width, 32, maxSize);
            int h = Mathf.Clamp(src.height, 32, maxSize);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (ti != null)
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Material CreateOrUpdateTexMat(string matPath, Shader shader, Texture2D tex, Color col, float affine, float fog)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", col);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EmissionStrength", 0f);
            mat.SetFloat("_AffineAmount", affine);
            mat.SetFloat("_FogWeight", fog);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = 2000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void FixBaseRoomAndHullMaterials()
        {
            Shader psxLit = Shader.Find("Outpost/PSXLit");
            var tWall = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_Station_WallPanel.png");
            var tFloor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_Station_FloorPlate.png");
            var tCeil = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_Station_CeilingPanel.png");
            var tTrim = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_Station_TrimHazard.png");
            var tHull = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_Station_HullPanel.png");

            UpdateMatAsset($"{RoomMatDir}/M_Room_Wall.mat", psxLit, tWall, new Color(0.84f, 0.87f, 0.91f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Room_Floor.mat", psxLit, tFloor, new Color(0.76f, 0.80f, 0.85f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Room_Ceiling.mat", psxLit, tCeil, new Color(0.72f, 0.76f, 0.82f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Room_Trim.mat", psxLit, tTrim, new Color(0.74f, 0.77f, 0.82f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Ext_HabHull.mat", psxLit, tHull, new Color(0.82f, 0.85f, 0.89f, 1f), 0.45f);
            UpdateMatAsset($"{RoomMatDir}/M_Desk.mat", psxLit, tTrim, new Color(0.78f, 0.81f, 0.86f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Chair.mat", psxLit, tTrim, new Color(0.55f, 0.62f, 0.72f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Door_Bulkhead.mat", psxLit, tWall, new Color(0.76f, 0.82f, 0.88f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Medbay_Door.mat", psxLit, tWall, new Color(0.82f, 0.88f, 0.92f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Door_Greenhouse.mat", psxLit, tWall, new Color(0.64f, 0.82f, 0.72f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Door_Storage.mat", psxLit, tWall, new Color(0.68f, 0.78f, 0.88f, 1f), 0.16f);
            UpdateMatAsset($"{RoomMatDir}/M_Door_AirlockOuter.mat", psxLit, tTrim, new Color(0.86f, 0.68f, 0.42f, 1f), 0.35f);

            UpdateMatAsset($"{PsxDir}/Wall.mat", psxLit, tWall, new Color(0.84f, 0.87f, 0.91f, 1f), 0.16f);
            UpdateMatAsset($"{PsxDir}/Floor.mat", psxLit, tFloor, new Color(0.76f, 0.80f, 0.85f, 1f), 0.16f);
            UpdateMatAsset($"{PsxDir}/Metal.mat", psxLit, tWall, new Color(0.80f, 0.84f, 0.88f, 1f), 0.16f);
            UpdateMatAsset($"{PsxDir}/DirtyMetal.mat", psxLit, tTrim, new Color(0.72f, 0.75f, 0.80f, 1f), 0.16f);
        }

        private static void UpdateMatAsset(string path, Shader sh, Texture2D tex, Color tint, float fogWeight)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || tex == null || sh == null) return;
            mat.shader = sh;
            mat.SetTexture("_MainTex", tex);
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
            mat.SetColor("_Color", tint);
            if (mat.HasProperty("_FogWeight")) mat.SetFloat("_FogWeight", fogWeight);
            if (mat.HasProperty("_AffineAmount")) mat.SetFloat("_AffineAmount", 0.55f);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Replaces primitive Unity Cube UVs on room walls, floors, ceilings, trims, and hull blocks
        /// with world-scale UVs (1 tile per 1.6m) so base textures never stretch across long walls or floors.
        /// </summary>
        private static void ApplyWorldSpaceCubeUVsToArchitecture()
        {
            CubeUvMeshCache.Clear();
            string[] rootNames = { "CommandModuleRoom", "OutpostExpansionWorld" };
            const float tileMeters = 1.55f;

            foreach (string rn in rootNames)
            {
                var rootGo = GameObject.Find(rn);
                if (rootGo == null) continue;
                foreach (var mf in rootGo.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    string mn = mf.sharedMesh.name;
                    if (mn != "Cube" && !mn.StartsWith("CubeUV_")) continue;

                    Vector3 s = mf.transform.lossyScale;
                    if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < 0.25f) continue;

                    mf.sharedMesh = GetOrCreateWorldCubeUvMesh(s, tileMeters);
                    EditorUtility.SetDirty(mf);
                }
            }
        }

        private static Mesh GetOrCreateWorldCubeUvMesh(Vector3 scale, float tileMeters)
        {
            int qx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) * 100f));
            int qy = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.y) * 100f));
            int qz = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.z) * 100f));
            string key = $"CubeUV_{qx}_{qy}_{qz}";
            if (CubeUvMeshCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            string assetPath = $"{WorldCubeMeshDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                CubeUvMeshCache[key] = existing;
                return existing;
            }

            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            Mesh mesh = Object.Instantiate(src);
            mesh.name = key;
            Object.DestroyImmediate(temp);

            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector2[] uvs = new Vector2[verts.Length];
            float sx = Mathf.Abs(scale.x) / tileMeters;
            float sy = Mathf.Abs(scale.y) / tileMeters;
            float sz = Mathf.Abs(scale.z) / tileMeters;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i] + new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 n = norms[i];
                if (Mathf.Abs(n.x) > 0.5f)
                    uvs[i] = new Vector2(v.z * sz, v.y * sy);
                else if (Mathf.Abs(n.y) > 0.5f)
                    uvs[i] = new Vector2(v.x * sx, v.z * sz);
                else
                    uvs[i] = new Vector2(v.x * sx, v.y * sy);
            }
            mesh.uv = uvs;
            AssetDatabase.CreateAsset(mesh, assetPath);
            CubeUvMeshCache[key] = mesh;
            return mesh;
        }

        private static void ReplacePlaceholdersAndPopulateScene(
            GameObject pCryoPod,
            GameObject pConsoleWide,
            GameObject pSuitRack,
            GameObject pMaintBot,
            GameObject pHatchRound,
            GameObject pCorridorCorner,
            GameObject pRadio,
            GameObject pRadSign)
        {
            // --- 1. COMMAND MODULE ---
            var cmdRoot = GameObject.Find("CommandModuleRoom");
            if (cmdRoot != null)
            {
                // 1a. Replace primitive PF_Radio visual with Prop_SatelliteRadio while keeping Radio_LED + Radio_PointLight
                var radioGo = cmdRoot.transform.Find("PF_Radio")?.gameObject;
                if (radioGo != null && pRadio != null)
                {
                    DestroyChildIfExists(radioGo.transform, "RadioBody");
                    DestroyChildIfExists(radioGo.transform, "RadioAntenna");
                    DestroyChildIfExists(radioGo.transform, "RadioDial");
                    DestroyChildIfExists(radioGo.transform, "Radio_Model");

                    var radioModel = (GameObject)PrefabUtility.InstantiatePrefab(pRadio, radioGo.transform);
                    radioModel.name = "Radio_Model";
                    radioModel.transform.localPosition = Vector3.zero;
                    radioModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var modelCol = radioModel.GetComponent<Collider>();
                    if (modelCol != null) Object.DestroyImmediate(modelCol);

                    var led = radioGo.transform.Find("Radio_LED");
                    if (led != null) led.localPosition = new Vector3(-0.08f, 0.16f, -0.14f);
                    var ledLight = radioGo.transform.Find("Radio_PointLight");
                    if (ledLight != null) ledLight.localPosition = new Vector3(-0.08f, 0.17f, -0.19f);
                }

                // 1b. Replace primitive PF_Locker visual with Prop_SpaceSuitRack flush against Wall_Right (X=3.00)
                var lockerGo = cmdRoot.transform.Find("PF_Locker")?.gameObject;
                if (lockerGo != null && pSuitRack != null)
                {
                    lockerGo.transform.localPosition = new Vector3(2.64f, 0f, -0.35f);
                    DestroyChildIfExists(lockerGo.transform, "LockerBody");
                    DestroyChildIfExists(lockerGo.transform, "LockerSeam");
                    DestroyChildIfExists(lockerGo.transform, "LockerHandleL");
                    DestroyChildIfExists(lockerGo.transform, "LockerHandleR");
                    DestroyChildIfExists(lockerGo.transform, "SuitRack_Model");

                    var rack = (GameObject)PrefabUtility.InstantiatePrefab(pSuitRack, lockerGo.transform);
                    rack.name = "SuitRack_Model";
                    rack.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                    rack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var col = rack.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                }

                // 1c. Replace primitive PF_CrewDoor panel with Prop_AirlockHatchRound
                var crewDoorGo = cmdRoot.transform.Find("PF_CrewDoor")?.gameObject;
                if (crewDoorGo != null && pHatchRound != null)
                {
                    DestroyChildIfExists(crewDoorGo.transform, "DoorFrame");
                    DestroyChildIfExists(crewDoorGo.transform, "DoorPanel");
                    DestroyChildIfExists(crewDoorGo.transform, "CrewHatch_Model");

                    var hatch = (GameObject)PrefabUtility.InstantiatePrefab(pHatchRound, crewDoorGo.transform);
                    hatch.name = "CrewHatch_Model";
                    hatch.transform.localPosition = new Vector3(0f, 0f, 0.04f);
                    hatch.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    hatch.transform.localScale = new Vector3(0.68f, 0.96f, 0.55f);

                    var signText = crewDoorGo.transform.Find("CrewSignText");
                    if (signText != null) signText.localPosition = new Vector3(0f, 2.32f, -0.06f);
                }

                // 1d. Add Prop_ControlConsoleWide against West wall & Prop_MaintenanceRobot in SE corner
                DestroyChildIfExists(cmdRoot.transform, "Command_WestConsole");
                if (pConsoleWide != null)
                {
                    var wCon = InstantiateSimple(pConsoleWide, cmdRoot.transform, "Command_WestConsole",
                        new Vector3(-2.55f, 0f, -0.78f), new Vector3(0f, 90f, 0f));
                    wCon.isStatic = true;
                }

                DestroyChildIfExists(cmdRoot.transform, "Command_MaintBot");
                if (pMaintBot != null)
                {
                    var bot = InstantiateSimple(pMaintBot, cmdRoot.transform, "Command_MaintBot",
                        new Vector3(2.10f, 0f, -1.45f), new Vector3(0f, -35f, 0f));
                    bot.AddComponent<InteractableProp>().ConfigureCustomFact(
                        "[E] Inspect Station Maintenance Bot",
                        "Fact: Autonomous maintenance bots inspect seals and airlocks without exposing crew to dust [source: src_shield]",
                        "src_shield");
                }
            }

            var expWorld = GameObject.Find("OutpostExpansionWorld");
            if (expWorld == null) return;

            // --- 2. MEDBAY: Replace Bed + Cmdr Vale & Monitor Stand with CryoPods + Control Console ---
            var medbay = expWorld.transform.Find("Room_Medbay");
            if (medbay != null)
            {
                var medBed = medbay.Find("Medbay_Bed");
                if (medBed != null && pCryoPod != null)
                {
                    DestroyChildIfExists(medBed, "BedBase");
                    DestroyChildIfExists(medBed, "Mattress");
                    DestroyChildIfExists(medBed, "Pillow");
                    DestroyChildIfExists(medBed, "CmdrVale_Torso");
                    DestroyChildIfExists(medBed, "CmdrVale_Head");
                    DestroyChildIfExists(medBed, "CryoPod_Vale");

                    // Medbay walls span X in [-10.74, -7.00], Z in [-0.30, +2.85]; 0.85x keeps clean ceiling & wall clearance
                    medBed.position = new Vector3(-9.85f, 0f, 1.98f);
                    medBed.rotation = Quaternion.Euler(0f, 90f, 0f);
                    var pod = (GameObject)PrefabUtility.InstantiatePrefab(pCryoPod, medBed);
                    pod.name = "CryoPod_Vale";
                    pod.transform.localPosition = Vector3.zero;
                    pod.transform.localRotation = Quaternion.identity;
                    pod.transform.localScale = Vector3.one * 0.85f;
                    var col = pod.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    var bedCol = medBed.GetComponent<BoxCollider>();
                    if (bedCol != null)
                    {
                        bedCol.center = new Vector3(0f, 1.15f, 0f);
                        bedCol.size = new Vector3(1.35f, 2.25f, 1.45f);
                    }
                }

                DestroyChildIfExists(medbay, "Medbay_CryoPod_Spare");
                if (pCryoPod != null)
                {
                    InstantiateSimple(pCryoPod, medbay, "Medbay_CryoPod_Spare",
                        new Vector3(-9.85f, 0f, 0.48f), new Vector3(0f, 90f, 0f), Vector3.one * 0.85f);
                }

                var vitalMon = medbay.Find("Medbay_VitalMonitor");
                if (vitalMon != null && pConsoleWide != null)
                {
                    DestroyChildIfExists(vitalMon, "Stand");
                    DestroyChildIfExists(vitalMon, "Medbay_Console");
                    vitalMon.position = new Vector3(-8.15f, 0f, 2.42f);
                    vitalMon.rotation = Quaternion.Euler(0f, 180f, 0f);

                    var con = (GameObject)PrefabUtility.InstantiatePrefab(pConsoleWide, vitalMon);
                    con.name = "Medbay_Console";
                    con.transform.localPosition = Vector3.zero;
                    con.transform.localRotation = Quaternion.identity;
                    con.transform.localScale = new Vector3(0.70f, 0.85f, 0.85f);

                    var vitalScr = vitalMon.Find("VitalScreen");
                    if (vitalScr != null)
                    {
                        vitalScr.localPosition = new Vector3(0f, 1.46f, 0.04f);
                        vitalScr.localEulerAngles = Vector3.zero;
                        vitalScr.localScale = new Vector3(0.55f, 0.06f, 0.04f);
                    }
                }

                var signMed = medbay.Find("Sign_Medbay");
                if (signMed != null)
                    signMed.position = new Vector3(-10.64f, 2.45f, 1.23f);
            }

            // --- 3. GREENHOUSE BAY: Add Env Console + Maint Bot (keep 3 HydroPlanters intact) ---
            var gh = expWorld.transform.Find("Room_Greenhouse");
            if (gh != null)
            {
                DestroyChildIfExists(gh, "Greenhouse_EnvConsole");
                if (pConsoleWide != null)
                {
                    InstantiateSimple(pConsoleWide, gh, "Greenhouse_EnvConsole",
                        new Vector3(-8.41f, 0f, -6.32f), new Vector3(0f, 180f, 0f), new Vector3(0.85f, 0.90f, 0.85f));
                }

                DestroyChildIfExists(gh, "Greenhouse_Bot");
                if (pMaintBot != null)
                {
                    InstantiateSimple(pMaintBot, gh, "Greenhouse_Bot",
                        new Vector3(-6.60f, 0f, -9.80f), new Vector3(0f, -35f, 0f));
                }
            }

            // --- 4. STORAGE BAY: Remove Primitive SupplyCrate_A/B & Add SuitRack + Bot + Console cleanly without tank/locker overlap ---
            var storage = expWorld.transform.Find("Room_Storage");
            if (storage != null)
            {
                DestroyChildIfExists(storage, "SupplyCrate_A");
                DestroyChildIfExists(storage, "SupplyCrate_B");
                DestroyChildIfExists(storage, "Storage_SuitRack");
                DestroyChildIfExists(storage, "Storage_Bot");
                DestroyChildIfExists(storage, "Storage_Console");

                if (pSuitRack != null)
                    InstantiateSimple(pSuitRack, storage, "Storage_SuitRack", new Vector3(6.55f, 0f, -6.40f), new Vector3(0f, 145f, 0f));
                if (pConsoleWide != null)
                    InstantiateSimple(pConsoleWide, storage, "Storage_Console", new Vector3(10.40f, 0f, -7.85f), new Vector3(0f, 270f, 0f), new Vector3(0.70f, 0.90f, 0.85f));
                if (pMaintBot != null)
                    InstantiateSimple(pMaintBot, storage, "Storage_Bot", new Vector3(6.55f, 0f, -9.85f), new Vector3(0f, 35f, 0f));
            }

            // --- 5. AIRLOCK: Replace Primitive DoorSlab cubes with Round Industrial Airlock Hatches + Add SuitRack ---
            var airlock = expWorld.transform.Find("Room_Airlock");
            if (airlock != null)
            {
                ReplaceAirlockDoorWithRoundHatch(airlock.Find("Door_AirlockInner"), pHatchRound);
                ReplaceAirlockDoorWithRoundHatch(airlock.Find("Door_AirlockOuter"), pHatchRound);

                DestroyChildIfExists(airlock, "Airlock_SuitRack");
                if (pSuitRack != null)
                {
                    var airRack = InstantiateSimple(pSuitRack, airlock, "Airlock_SuitRack",
                        new Vector3(1.06f, 0f, -16.75f), new Vector3(0f, -35f, 0f));
                    airRack.AddComponent<InteractableProp>().ConfigureCustomFact(
                        "[E] Inspect EVA Pressure Suit Rack",
                        "Fact: Mars surface suits operate at 0.3 atm pure oxygen to allow flexible joints during EVAs [source: src_co2]",
                        "src_co2");
                }
            }

            // --- 6. HUB CORRIDOR: Clear East Command overlap, tidy Storage kit props, add SuitRack & Bot in West Alcove ---
            var corr = expWorld.transform.Find("HubCorridor");
            if (corr != null)
            {
                var kitProps = corr.Find("InteriorKit_Props");
                if (kitProps != null)
                {
                    DestroyChildIfExists(kitProps, "Prop_Console_CommandSide");
                    var sLocker = kitProps.Find("Prop_Locker_Storage1");
                    if (sLocker != null)
                    {
                        sLocker.position = new Vector3(10.44f, 0f, -6.45f);
                        sLocker.rotation = Quaternion.Euler(0f, 270f, 0f);
                    }
                    var sCrate1 = kitProps.Find("Prop_Crate_Storage1");
                    if (sCrate1 != null)
                        sCrate1.position = new Vector3(10.20f, 0f, -9.75f);
                    var sCrate2 = kitProps.Find("Prop_Crate_Storage2");
                    if (sCrate2 != null)
                        sCrate2.position = new Vector3(10.20f, 0.67f, -9.75f);
                }

                DestroyChildIfExists(corr, "Corridor_AlcoveSuitRack");
                DestroyChildIfExists(corr, "Corridor_AlcoveBot");
                DestroyChildIfExists(corr, "Corridor_CornerBay");

                if (pSuitRack != null)
                    InstantiateSimple(pSuitRack, corr, "Corridor_AlcoveSuitRack", new Vector3(-3.80f, 0f, -2.55f), new Vector3(0f, 180f, 0f));
                if (pMaintBot != null)
                    InstantiateSimple(pMaintBot, corr, "Corridor_AlcoveBot", new Vector3(-3.20f, 0f, -5.30f), new Vector3(0f, 30f, 0f));
            }

            // --- 7. MARS SURFACE: Replace RadiationPost with radiation_sign.glb + Add Surface Bots, Radios, Extra Signs ---
            var marsSurf = expWorld.transform.Find("MarsSurfaceWorld");
            if (marsSurf != null)
            {
                var radPoi = marsSurf.Find("POI_RadiationPost");
                if (radPoi != null && pRadSign != null)
                {
                    DestroyChildIfExists(radPoi, "RadPole");
                    DestroyChildIfExists(radPoi, "SensorBox");
                    DestroyChildIfExists(radPoi, "RadSign");
                    DestroyChildIfExists(radPoi, "RadSign_Model");

                    float sy = SampleTerrainY(marsSurf, radPoi.position.x, radPoi.position.z);
                    radPoi.position = new Vector3(radPoi.position.x, sy, radPoi.position.z);
                    var signInst = (GameObject)PrefabUtility.InstantiatePrefab(pRadSign, radPoi);
                    signInst.name = "RadSign_Model";
                    signInst.transform.localPosition = Vector3.zero;
                    signInst.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                    var mcol = signInst.GetComponent<Collider>();
                    if (mcol != null) Object.DestroyImmediate(mcol);

                    var rcol = radPoi.GetComponent<BoxCollider>();
                    if (rcol != null)
                    {
                        rcol.center = new Vector3(0f, 1.05f, 0f);
                        rcol.size = new Vector3(1.10f, 2.15f, 0.70f);
                    }
                }

                // Perimeter Hazard Signs
                DestroyChildIfExists(marsSurf, "Surface_RadSign_West");
                DestroyChildIfExists(marsSurf, "Surface_RadSign_South");
                if (pRadSign != null)
                {
                    PlaceOnTerrain(pRadSign, marsSurf, "Surface_RadSign_West", -18.5f, -29.0f, -35f);
                    PlaceOnTerrain(pRadSign, marsSurf, "Surface_RadSign_South", 8.5f, -41.0f, 15f);
                }

                // Field Radio Transceiver near DSN Antenna
                var antPoi = marsSurf.Find("POI_DSN_Antenna");
                var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InteriorPrefabsDir}/Prop_Console.prefab");
                if (antPoi != null && pRadio != null)
                {
                    DestroyChildIfExists(marsSurf, "DSN_FieldConsole");
                    DestroyChildIfExists(marsSurf, "DSN_FieldRadio");
                    float baseY = SampleTerrainY(marsSurf, -12.0f, -33.2f);
                    if (consolePrefab != null)
                        InstantiateSimple(consolePrefab, marsSurf, "DSN_FieldConsole", new Vector3(-12.0f, baseY, -33.2f), new Vector3(0f, 35f, 0f));
                    InstantiateSimple(pRadio, marsSurf, "DSN_FieldRadio", new Vector3(-12.0f, baseY + 0.88f, -33.2f), new Vector3(0f, 35f, 0f));
                }

                // Autonomous Solar-Cleaning Bot next to POI_SolarArray
                DestroyChildIfExists(marsSurf, "SolarArray_SweeperBot");
                if (pMaintBot != null)
                {
                    var bot = PlaceOnTerrain(pMaintBot, marsSurf, "SolarArray_SweeperBot", -6.1f, -23.6f, 225f);
                    bot.AddComponent<InteractableProp>().ConfigureCustomFact(
                        "[E] Inspect Autonomous Solar-Sweeper Bot",
                        "Fact: Electrostatic Martian dust steadily coats solar panels; robotic sweepers restore peak kW output [source: src_dust]",
                        "src_dust");
                }

                // Fix POI_WaterIceMarker/IceSign so it sits atop MarkerPost instead of floating at world Z=0 in CommandModuleRoom
                var icePoi = marsSurf.Find("POI_WaterIceMarker");
                var iceSign = icePoi != null ? icePoi.Find("IceSign") : null;
                if (iceSign != null)
                    iceSign.localPosition = new Vector3(0f, 1.83f, 0f);

                // Disable legacy primitive CraterRidge cubes so only procedural Mars terrain ridges appear
                for (int rIdx = 0; rIdx < 6; rIdx++)
                {
                    var ridge = marsSurf.Find($"CraterRidge_{rIdx}");
                    if (ridge != null)
                        ridge.gameObject.SetActive(false);
                }
            }

            // Ensure PlayerCamera renders RenderSettings.skybox (M_MarsSkyGradient) instead of solid color backdrop
            var playerCamGo = GameObject.Find("PlayerCamera");
            var playerCam = playerCamGo != null ? playerCamGo.GetComponent<Camera>() : null;
            if (playerCam != null)
            {
                playerCam.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(playerCam);
            }
        }

        private static void ReplaceAirlockDoorWithRoundHatch(Transform doorRoot, GameObject pHatchRound)
        {
            if (doorRoot == null || pHatchRound == null) return;
            var leaf = doorRoot.Find("DoorLeaf");
            if (leaf == null) return;

            DestroyChildIfExists(leaf, "DoorSlab");
            DestroyChildIfExists(leaf, "DoorStripe");
            DestroyChildIfExists(leaf, "RoundHatch_Model");
            DestroyChildIfExists(leaf, "HatchBackingPlate");

            var hatch = (GameObject)PrefabUtility.InstantiatePrefab(pHatchRound, leaf);
            hatch.name = "RoundHatch_Model";
            hatch.transform.localPosition = Vector3.zero;
            hatch.transform.localRotation = Quaternion.identity;
            // Fit tightly inside the 1.50m x 2.20m airlock frame opening
            hatch.transform.localScale = new Vector3(0.72f, 0.99f, 0.42f);

            // Thin sealed backing collar behind the embossed octagon rings so rectangular doorway corners never leak outside sky
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "HatchBackingPlate";
            plate.transform.SetParent(leaf, false);
            plate.transform.localPosition = new Vector3(0f, 1.10f, 0f);
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(1.52f, 2.20f, 0.05f);
            var col = plate.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var rend = plate.GetComponent<Renderer>();
            rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{RoomMatDir}/M_Room_Trim.mat");
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            plate.GetComponent<MeshFilter>().sharedMesh = GetOrCreateWorldCubeUvMesh(plate.transform.localScale, 1.55f);
        }

        private static void UpgradeExteriorHabitatHull(GameObject pHatchRound)
        {
            var expWorld = GameObject.Find("OutpostExpansionWorld");
            var marsSurf = expWorld != null ? expWorld.transform.Find("MarsSurfaceWorld") : null;
            if (marsSurf == null) return;

            DestroyChildIfExists(marsSurf, "HabHull_ExteriorDetails");
            var hullMat = AssetDatabase.LoadAssetAtPath<Material>($"{RoomMatDir}/M_Ext_HabHull.mat");
            var trimMat = AssetDatabase.LoadAssetAtPath<Material>($"{RoomMatDir}/M_Room_Trim.mat");

            var details = new GameObject("HabHull_ExteriorDetails");
            details.transform.SetParent(marsSurf, false);

            // Corner armored buttress pillars grounded down to Martian soil (-0.15m)
            Vector3[] pillars =
            {
                new Vector3(-3.16f, 1.55f, 2.90f),
                new Vector3( 3.16f, 1.55f, 2.90f),
                new Vector3(-3.16f, 1.55f, -2.20f),
                new Vector3( 3.16f, 1.55f, -2.20f),
                new Vector3(-10.86f, 1.55f, 2.88f),
                new Vector3(-10.86f, 1.55f, -0.32f),
                new Vector3(-10.86f, 1.55f, -5.88f),
                new Vector3(-10.86f, 1.55f, -10.42f),
                new Vector3(10.86f, 1.55f, -5.88f),
                new Vector3(10.86f, 1.55f, -10.42f),
                new Vector3(-1.66f, 1.55f, -17.42f),
                new Vector3( 1.66f, 1.55f, -17.42f)
            };

            for (int i = 0; i < pillars.Length; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = $"HullButtress_{i}";
                p.transform.SetParent(details.transform, false);
                p.transform.position = pillars[i];
                p.transform.localScale = new Vector3(0.34f, 3.42f, 0.34f);
                var c = p.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = trimMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                p.GetComponent<MeshFilter>().sharedMesh = GetOrCreateWorldCubeUvMesh(p.transform.localScale, 1.55f);
                p.isStatic = true;
            }

            // Airlock Roof Fascia
            var airRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            airRoof.name = "HabHull_AirlockRoof";
            airRoof.transform.SetParent(details.transform, false);
            airRoof.transform.position = new Vector3(0f, 3.16f, -15.82f);
            airRoof.transform.localScale = new Vector3(3.40f, 0.18f, 3.55f);
            Object.DestroyImmediate(airRoof.GetComponent<Collider>());
            var airRend = airRoof.GetComponent<Renderer>();
            airRend.sharedMaterial = hullMat;
            airRend.shadowCastingMode = ShadowCastingMode.Off;
            airRend.receiveShadows = false;
            airRoof.GetComponent<MeshFilter>().sharedMesh = GetOrCreateWorldCubeUvMesh(airRoof.transform.localScale, 1.55f);
            airRoof.isStatic = true;
        }

        private static GameObject InstantiateSimple(GameObject prefab, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3? localScale = null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            if (localScale.HasValue)
                inst.transform.localScale = localScale.Value;
            return inst;
        }

        private static GameObject PlaceOnTerrain(GameObject prefab, Transform surfRoot, string name, float x, float z, float yaw)
        {
            float y = SampleTerrainY(surfRoot, x, z);
            return InstantiateSimple(prefab, surfRoot, name, new Vector3(x, y, z), new Vector3(0f, yaw, 0f));
        }

        private static float SampleTerrainY(Transform surfRoot, float x, float z)
        {
            var tf = surfRoot.Find("MarsPlayTerrain");
            var terr = tf != null ? tf.GetComponent<Terrain>() : null;
            if (terr != null)
                return terr.SampleHeight(new Vector3(x, 0f, z)) + terr.transform.position.y;
            return -0.08f;
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

        private static void DestroyChildIfExists(Transform parent, string name)
        {
            if (parent == null) return;
            var child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
