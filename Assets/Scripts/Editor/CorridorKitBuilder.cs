using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Room;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Editor
{
    /// <summary>
    /// Imports hopefulsham's SciFi Corridor PRO LITE FBX modules & props at uniform scale,
    /// converts textures/materials to point-filtered PSX shaders, splits Door_Slide_Closed
    /// into frame + animated DoorPanel leaf, saves the 6 prefabs in Assets/Prefabs/Interior/,
    /// and rebuilds the HubCorridor & doorway chain in the active Outpost scene.
    /// </summary>
    public static class CorridorKitBuilder
    {
        public const string KitRoot = "Assets/Models/scifi-hard-surface-corridor-kit-free-sample-lite/Scifi_Corridor_PRO_LITE_v1.0.0";
        public const string ModelsDir = KitRoot + "/models";
        public const string TexturesDir = KitRoot + "/textures";
        public const string PrefabsOutDir = "Assets/Prefabs/Interior";
        public const string MaterialsOutDir = "Assets/Art/PSX/Corridor";

        private static readonly (string matKey, string baseTex, string emisTex, bool isGlow)[] KitMaterialDefs =
        {
            ("glow_cyan",    "tile_glow_cyan_BaseColor.jpg",    null,                     true),
            ("panel_blue",   "tile_panel_blue_BaseColor.jpg",   null,                     false),
            ("panel_grey",   "tile_panel_grey_BaseColor.jpg",   null,                     false),
            ("panel_orange", "tile_panel_orange_BaseColor.jpg", null,                     false),
            ("panel_white",  "tile_panel_white_BaseColor.jpg",  null,                     false),
            ("steel",        "tile_steel_BaseColor.jpg",        null,                     false),
            ("cargo",        "props_cargo_BaseColor.jpg",       "props_cargo_Emissive.jpg", false),
            ("crew",         "props_crew_BaseColor.jpg",        "props_crew_Emissive.jpg",  false),
            ("tech",         "props_tech_BaseColor.jpg",        "props_tech_Emissive.jpg",  false)
        };

        [MenuItem("Outpost/Rebuild Interior Corridor Kit")]
        public static void RebuildInteriorWithCorridorKit()
        {
            EnsureDirectories();
            ConfigurePackTexturesAndModels();
            var matLookup = BuildKitPsxMaterials();

            // 1. Measure real FBX bounds and standardize grid scale vs FPS capsule (r=0.5m, h=2.0m)
            float straightMaxDim = MeasureFbxMaxDim("Corridor_Straight.fbx", out Bounds straightBounds);
            float uniformScale = (straightMaxDim > 0.01f && Mathf.Abs(straightMaxDim - 4.0f) > 0.25f)
                ? (4.0f / straightMaxDim)
                : 1.0f;

            Debug.Log($"[CorridorKitBuilder] Measured Corridor_Straight.fbx bounds: size={straightBounds.size:F3}, min={straightBounds.min:F3}, max={straightBounds.max:F3}. " +
                      $"UniformScale={uniformScale:F3}. FPS Capsule (r=0.5m, h=2.0m) fits 3.76m x 3.40m clearance cleanly.");

            // 2. Build & save the 6 interior prefabs + corridor junction cutout variants
            var pStraight = BuildCorridorStraightPrefab(matLookup, uniformScale);
            var pDoors = BuildCorridorDoorsPrefab(matLookup, uniformScale);
            var pEnd = BuildCorridorEndPrefab(matLookup, uniformScale, out bool endCapPositiveZ);
            var pCrate = BuildPropPrefab("Crate_Small.fbx", "Prop_Crate", matLookup, uniformScale);
            var pLocker = BuildPropPrefab("Locker_Single.fbx", "Prop_Locker", matLookup, uniformScale);
            var pConsole = BuildPropPrefab("Console_Desk.fbx", "Prop_Console", matLookup, uniformScale);

            // Junction variants derived from Corridor_Straight (punched 2.2m x 2.35m doorway holes where branches meet)
            var mCutWest = CreateOrUpdateCorridorCutoutMesh(cutWest: true, cutEast: false, "Mesh_Corridor_CutWest");
            var mCutBoth = CreateOrUpdateCorridorCutoutMesh(cutWest: true, cutEast: true, "Mesh_Corridor_CutBoth");

            // 3. Rebuild HubCorridor in active scene using a 7-module corridor chain
            RebuildSceneCorridor(pStraight, pDoors, pEnd, pCrate, pLocker, pConsole, mCutWest, mCutBoth, endCapPositiveZ);

            AssetDatabase.SaveAssets();
            Debug.Log("[CorridorKitBuilder] Completed modular corridor kit rebuild & interior prop placement.");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsOutDir)) Directory.CreateDirectory(PrefabsOutDir);
            if (!Directory.Exists(MaterialsOutDir)) Directory.CreateDirectory(MaterialsOutDir);
        }

        private static void ConfigurePackTexturesAndModels()
        {
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesDir });
            foreach (string g in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool dirty = false;
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }

            string[] fbxNames =
            {
                "Corridor_Straight.fbx", "Corridor_End.fbx", "Door_Slide_Closed.fbx",
                "Crate_Small.fbx", "Locker_Single.fbx", "Console_Desk.fbx"
            };
            foreach (string fn in fbxNames)
            {
                string path = $"{ModelsDir}/{fn}";
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                bool dirty = false;
                if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
                if (Mathf.Abs(imp.globalScale - 1.0f) > 0.001f) { imp.globalScale = 1.0f; dirty = true; }
                if (imp.importLights) { imp.importLights = false; dirty = true; }
                if (imp.importCameras) { imp.importCameras = false; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> BuildKitPsxMaterials()
        {
            NewAssetsIntegrationBuilder.EnsureStationAtlasTextures();
            Shader litShader = Shader.Find("Outpost/PSXLit");
            Shader unlitShader = Shader.Find("Outpost/PSXUnlit");
            var dict = new Dictionary<string, Material>();
            var cleanWallTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NewAssetsIntegrationBuilder.PsxTexDir}/T_Station_WallPanel.png");
            var cleanTrimTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NewAssetsIntegrationBuilder.PsxTexDir}/T_Station_TrimHazard.png");

            foreach (var (matKey, baseTex, emisTex, isGlow) in KitMaterialDefs)
            {
                string matPath = $"{MaterialsOutDir}/M_Kit_{matKey}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(isGlow ? unlitShader : litShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.shader = isGlow ? unlitShader : litShader;
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{baseTex}");
                Color tint = Color.white;
                Vector2 tiling = Vector2.one;

                // Replace rusty brown panel_grey & corrugated shutter panel_white with crisp PSX station panels
                if (matKey == "panel_grey" && cleanTrimTex != null)
                {
                    tex = cleanTrimTex;
                    tint = new Color(0.76f, 0.81f, 0.88f, 1f);
                    tiling = new Vector2(2f, 2f);
                }
                else if (matKey == "panel_white" && cleanWallTex != null)
                {
                    tex = cleanWallTex;
                    tint = new Color(0.90f, 0.93f, 0.96f, 1f);
                    tiling = new Vector2(2f, 2f);
                }
                else if (matKey == "steel")
                {
                    tint = new Color(0.80f, 0.84f, 0.90f, 1f);
                }
                else if (matKey == "panel_blue")
                {
                    tint = new Color(0.80f, 0.88f, 0.96f, 1f);
                }

                mat.SetTexture("_MainTex", tex);
                mat.mainTextureScale = tiling;
                mat.SetColor("_Color", tint);

                if (isGlow)
                {
                    mat.SetColor("_EmissionColor", new Color(0.24f, 0.90f, 0.96f, 1f));
                    mat.SetFloat("_EmissionStrength", 1.55f);
                    mat.SetFloat("_AffineAmount", 0.35f);
                    mat.SetFloat("_FogWeight", 0.12f);
                }
                else
                {
                    if (!string.IsNullOrEmpty(emisTex))
                    {
                        var eTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{emisTex}");
                        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", eTex);
                        mat.SetColor("_EmissionColor", Color.white);
                        mat.SetFloat("_EmissionStrength", 1.50f);
                    }
                    else
                    {
                        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", null);
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.SetFloat("_EmissionStrength", 0f);
                    }
                    mat.SetFloat("_AffineAmount", 0.70f);
                    mat.SetFloat("_FogWeight", 0.18f);
                }

                EditorUtility.SetDirty(mat);
                dict[matKey] = mat;
            }
            return dict;
        }

        private static Material ResolveKitMaterial(string srcMatName, string fbxName, Dictionary<string, Material> lookup)
        {
            string s = ((srcMatName ?? "") + " " + fbxName).ToLowerInvariant();
            if (s.Contains("glow")) return lookup["glow_cyan"];
            if (s.Contains("blue")) return lookup["panel_blue"];
            if (s.Contains("orange")) return lookup["panel_orange"];
            if (s.Contains("white")) return lookup["panel_white"];
            if (s.Contains("steel") || s.Contains("iron")) return lookup["steel"];
            if (s.Contains("grey") || s.Contains("gray")) return lookup["panel_grey"];
            if (s.Contains("cargo") || s.Contains("crate")) return lookup["cargo"];
            if (s.Contains("crew") || s.Contains("locker")) return lookup["crew"];
            if (s.Contains("tech") || s.Contains("console")) return lookup["tech"];
            return lookup["panel_grey"];
        }

        private static void AssignKitMaterialsToHierarchy(GameObject root, string fbxName, Dictionary<string, Material> lookup)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = ResolveKitMaterial(mats[i] != null ? mats[i].name : "", fbxName, lookup);
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static float MeasureFbxMaxDim(string fbxName, out Bounds totalBounds)
        {
            totalBounds = new Bounds(Vector3.zero, Vector3.zero);
            var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{fbxName}");
            if (fbxGo == null) return 4.0f;
            var temp = Object.Instantiate(fbxGo);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var rends = temp.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            foreach (var r in rends)
            {
                if (!any) { totalBounds = r.bounds; any = true; }
                else totalBounds.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(temp);
            return Mathf.Max(totalBounds.size.x, totalBounds.size.y, totalBounds.size.z);
        }

        private static GameObject BuildCorridorStraightPrefab(Dictionary<string, Material> lookup, float scale)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Corridor_Straight.fbx");
            var root = new GameObject("Corridor_Straight");
            var visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * scale;
            AssignKitMaterialsToHierarchy(visual, "Corridor_Straight.fbx", lookup);

            AddBoxCol(root, "Col_Floor", new Vector3(0f, 0.10f, 0f), new Vector3(4.00f, 0.20f, 4.00f));
            AddBoxCol(root, "Col_Ceiling", new Vector3(0f, 3.80f, 0f), new Vector3(4.00f, 0.40f, 4.00f));
            AddBoxCol(root, "Col_Wall_W", new Vector3(-1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            AddBoxCol(root, "Col_Wall_E", new Vector3(1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            SetStaticFlagsRecursive(root, true);

            return SaveInteriorPrefab(root, $"{PrefabsOutDir}/Corridor_Straight.prefab");
        }

        private static GameObject BuildCorridorEndPrefab(Dictionary<string, Material> lookup, float scale, out bool endCapPositiveZ)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Corridor_End.fbx");
            var root = new GameObject("Corridor_End");
            var visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";
            visual.transform.localScale = Vector3.one * scale;
            AssignKitMaterialsToHierarchy(visual, "Corridor_End.fbx", lookup);

            // Detect which Z end has the bulkhead cap vertices cleanly
            endCapPositiveZ = DetectCapSidePositiveZ(root, visual);
            float capZ = endCapPositiveZ ? 1.92f : -1.92f;
            Debug.Log($"[CorridorKitBuilder] Measured Corridor_End.fbx cap side: {(endCapPositiveZ ? "+Z" : "-Z")}.");

            AddBoxCol(root, "Col_Floor", new Vector3(0f, 0.10f, 0f), new Vector3(4.00f, 0.20f, 4.00f));
            AddBoxCol(root, "Col_Ceiling", new Vector3(0f, 3.80f, 0f), new Vector3(4.00f, 0.40f, 4.00f));
            AddBoxCol(root, "Col_Wall_W", new Vector3(-1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            AddBoxCol(root, "Col_Wall_E", new Vector3(1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            AddBoxCol(root, "Col_EndCap", new Vector3(0f, 1.90f, capZ), new Vector3(3.88f, 3.60f, 0.20f));
            SetStaticFlagsRecursive(root, true);

            return SaveInteriorPrefab(root, $"{PrefabsOutDir}/Corridor_End.prefab");
        }

        private static GameObject BuildCorridorDoorsPrefab(Dictionary<string, Material> lookup, float scale)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Door_Slide_Closed.fbx");
            var root = new GameObject("Corridor_Doors");
            var temp = Object.Instantiate(src);
            var srcMf = temp.GetComponentInChildren<MeshFilter>(true);
            var srcMr = temp.GetComponentInChildren<MeshRenderer>(true);

            // Partition Door_Slide_Closed mesh into Frame + Bi-Parting DoorLeaves (L & R)
            Mesh srcMesh = srcMf != null ? srcMf.sharedMesh : null;
            SplitDoorMesh(
                srcMesh,
                srcMf != null ? srcMf.transform : temp.transform,
                out Mesh frameMesh,
                out Mesh leafRightMesh,
                out Mesh leafLeftMesh);

            var frameGo = new GameObject("DoorFrame");
            frameGo.transform.SetParent(root.transform, false);
            frameGo.transform.localScale = Vector3.one * scale;
            frameGo.AddComponent<MeshFilter>().sharedMesh = frameMesh;
            var frameMr = frameGo.AddComponent<MeshRenderer>();
            if (srcMr != null) frameMr.sharedMaterials = srcMr.sharedMaterials;
            AssignKitMaterialsToHierarchy(frameGo, "Door_Slide_Closed.fbx", lookup);

            var doorPanel = CreateDoorLeafObject(root.transform, "DoorPanel", leafRightMesh, srcMr, lookup, scale);
            var doorPanelL = CreateDoorLeafObject(root.transform, "DoorPanel_L", leafLeftMesh, srcMr, lookup, scale);

            Object.DestroyImmediate(temp);

            // Frame colliders leaving clean 2.02m wide x 2.34m tall walk-through threshold
            AddBoxCol(root, "Col_Floor", new Vector3(0f, 0.10f, 0f), new Vector3(4.00f, 0.20f, 4.00f));
            AddBoxCol(root, "Col_Ceiling", new Vector3(0f, 3.80f, 0f), new Vector3(4.00f, 0.40f, 4.00f));
            AddBoxCol(root, "Col_Wall_W", new Vector3(-1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            AddBoxCol(root, "Col_Wall_E", new Vector3(1.94f, 1.90f, 0f), new Vector3(0.14f, 3.60f, 4.00f));
            AddBoxCol(root, "Col_Bulkhead_L", new Vector3(-1.46f, 1.90f, 0f), new Vector3(0.90f, 3.60f, 0.30f));
            AddBoxCol(root, "Col_Bulkhead_R", new Vector3(1.46f, 1.90f, 0f), new Vector3(0.90f, 3.60f, 0.30f));
            AddBoxCol(root, "Col_Bulkhead_Top", new Vector3(0f, 3.12f, 0f), new Vector3(2.04f, 1.18f, 0.30f));

            // Bi-parting leaves each slide 0.94m inside the 4m bulkhead pocket (never poking outside [-2, +2])
            var leafColR = doorPanel.AddComponent<BoxCollider>();
            leafColR.center = new Vector3(0.505f, 1.36f, 0f);
            leafColR.size = new Vector3(1.01f, 2.32f, 0.20f);

            var leafColL = doorPanelL.AddComponent<BoxCollider>();
            leafColL.center = new Vector3(-0.505f, 1.36f, 0f);
            leafColL.size = new Vector3(1.01f, 2.32f, 0.20f);

            var doorCtrl = root.AddComponent<DoorInteractable>();
            doorCtrl.Configure(
                "door.name.corridor",
                DoorMotionType.SlideLocalOffset,
                doorPanel.transform,
                leafColR,
                new Vector3(0.94f, 0f, 0f),
                openByDefault: false);
            doorCtrl.ConfigureBiPartingLeaves(
                doorPanelL.transform,
                new Vector3(-0.94f, 0f, 0f),
                leafColL);
            doorPanel.AddComponent<InteractableProp>().ConfigureDoor(doorCtrl);
            doorPanelL.AddComponent<InteractableProp>().ConfigureDoor(doorCtrl);

            // Mount an interactive button + status indicator light on both sides of the corridor bulkhead
            AttachCorridorDoorButtons(root, doorCtrl);

            // Static flags only on non-moving frame & walls (DoorPanel stays movable!)
            SetStaticFlagsRecursive(frameGo, true);
            foreach (Transform child in root.transform)
                if (child.name.StartsWith("Col_")) child.gameObject.isStatic = true;

            return SaveInteriorPrefab(root, $"{PrefabsOutDir}/Corridor_Doors.prefab");
        }

        [MenuItem("Outpost/Ensure All Facility Door Buttons")]
        public static void EnsureAllFacilityDoorButtons()
        {
            string prefabPath = $"{PrefabsOutDir}/Corridor_Doors.prefab";
            if (File.Exists(prefabPath))
            {
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var doorCtrl = contents != null ? contents.GetComponent<DoorInteractable>() : null;
                if (contents != null && doorCtrl != null)
                {
                    AttachCorridorDoorButtons(contents, doorCtrl);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            // Ensure both Airlock doors have unobstructed keypads on both sides and wired button indicators
            UpdateAirlockDoorButtons("Door_AirlockInner", new Vector3(-0.95f, 1.25f, -0.21f), new Vector3(-0.88f, 1.25f, 0.11f));
            UpdateAirlockDoorButtons("Door_AirlockOuter", new Vector3(0.92f, 1.25f, -0.09f), new Vector3(-0.88f, 1.25f, 0.11f));

            AssetDatabase.SaveAssets();
            Debug.Log("[CorridorKitBuilder] Ensured interactive buttons on both sides of all facility doors.");
        }

        private static void AttachCorridorDoorButtons(GameObject doorRoot, DoorInteractable doorCtrl)
        {
            if (doorRoot == null || doorCtrl == null) return;
            var trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/M_Room_Trim.mat");
            var btnMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/M_Beacon_Status.mat");

            // Corridor_Doors root sits at y=-0.20 and DoorFrame bulkhead faces lie at z=+-0.250
            var frontBtn = CreateOrUpdateDoorButton(
                doorRoot.transform, "Keypad_Front",
                new Vector3(-1.28f, 1.45f, -0.27f), Vector3.zero, trimMat, btnMat, doorCtrl);
            var backBtn = CreateOrUpdateDoorButton(
                doorRoot.transform, "Keypad_Back",
                new Vector3(-1.28f, 1.45f, 0.27f), new Vector3(0f, 180f, 0f), trimMat, btnMat, doorCtrl);

            doorCtrl.SetAdditionalIndicators(frontBtn, backBtn);
            EditorUtility.SetDirty(doorCtrl);
        }

        private static void UpdateAirlockDoorButtons(string doorName, Vector3 frontLocalPos, Vector3 backLocalPos)
        {
            var doorGo = GameObject.Find(doorName);
            var doorCtrl = doorGo != null ? doorGo.GetComponent<DoorInteractable>() : null;
            if (doorGo == null || doorCtrl == null) return;

            var trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/M_Room_Trim.mat");
            var btnMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/M_Beacon_Airlock.mat")
                      ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/M_Beacon_Status.mat");

            var frontBtn = CreateOrUpdateDoorButton(doorGo.transform, "Keypad_Front", frontLocalPos, Vector3.zero, trimMat, btnMat, doorCtrl);
            var backBtn = CreateOrUpdateDoorButton(doorGo.transform, "Keypad_Back", backLocalPos, new Vector3(0f, 180f, 0f), trimMat, btnMat, doorCtrl);

            doorCtrl.SetAdditionalIndicators(frontBtn, backBtn);
            EditorUtility.SetDirty(doorCtrl);
        }

        private static Renderer CreateOrUpdateDoorButton(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 localEuler,
            Material trimMat,
            Material btnMat,
            DoorInteractable doorCtrl)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var pad = new GameObject(name);
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = localPos;
            pad.transform.localEulerAngles = localEuler;

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "PadBack";
            back.transform.SetParent(pad.transform, false);
            back.transform.localPosition = Vector3.zero;
            back.transform.localScale = new Vector3(0.14f, 0.22f, 0.04f);
            Object.DestroyImmediate(back.GetComponent<Collider>());
            var backRend = back.GetComponent<Renderer>();
            if (trimMat != null) backRend.sharedMaterial = trimMat;
            backRend.shadowCastingMode = ShadowCastingMode.Off;
            backRend.receiveShadows = false;

            var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = "PadButton";
            btn.transform.SetParent(pad.transform, false);
            btn.transform.localPosition = new Vector3(0f, 0.02f, -0.025f);
            btn.transform.localScale = new Vector3(0.08f, 0.08f, 0.025f);
            Object.DestroyImmediate(btn.GetComponent<Collider>());
            var btnRend = btn.GetComponent<Renderer>();
            if (btnMat != null) btnRend.sharedMaterial = btnMat;
            btnRend.shadowCastingMode = ShadowCastingMode.Off;
            btnRend.receiveShadows = false;

            var col = pad.AddComponent<BoxCollider>();
            col.size = new Vector3(0.24f, 0.34f, 0.14f);
            col.isTrigger = true;
            pad.AddComponent<InteractableProp>().ConfigureDoor(doorCtrl);

            return btnRend;
        }

        private static GameObject BuildPropPrefab(string fbxName, string prefabName, Dictionary<string, Material> lookup, float scale)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{fbxName}");
            var root = new GameObject(prefabName);
            var visual = Object.Instantiate(src, root.transform, false);
            visual.name = "Visual";
            visual.transform.localScale = Vector3.one * scale;
            AssignKitMaterialsToHierarchy(visual, fbxName, lookup);

            // Compute exact bounds in prefab root space & ground bottom pivot cleanly at Y = 0
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in rends)
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any)
            {
                Vector3 shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                visual.transform.localPosition += shift;
                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                col.size = b.size;
                Debug.Log($"[CorridorKitBuilder] Measured {fbxName}: grounded bounds size={b.size:F3}, scale={scale:F3}");
            }

            SetStaticFlagsRecursive(root, true);
            return SaveInteriorPrefab(root, $"{PrefabsOutDir}/{prefabName}.prefab");
        }

        private static GameObject CreateDoorLeafObject(
            Transform parent,
            string name,
            Mesh mesh,
            MeshRenderer srcMr,
            Dictionary<string, Material> lookup,
            float scale)
        {
            var leafGo = new GameObject(name);
            leafGo.transform.SetParent(parent, false);
            leafGo.transform.localScale = Vector3.one * scale;
            leafGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var leafMr = leafGo.AddComponent<MeshRenderer>();
            if (srcMr != null) leafMr.sharedMaterials = srcMr.sharedMaterials;
            AssignKitMaterialsToHierarchy(leafGo, "Door_Slide_Closed.fbx", lookup);
            return leafGo;
        }

        private static void SplitDoorMesh(Mesh src, Transform xf, out Mesh frameMesh, out Mesh leafRightMesh, out Mesh leafLeftMesh)
        {
            frameMesh = new Mesh { name = "Mesh_DoorFrame" };
            leafRightMesh = new Mesh { name = "Mesh_DoorLeaf" };
            leafLeftMesh = new Mesh { name = "Mesh_DoorLeaf_L" };
            if (src == null) return;

            var verts = src.vertices;
            var norms = src.normals;
            var uvs = src.uv;
            Matrix4x4 m = Matrix4x4.TRS(xf.position, xf.rotation, xf.lossyScale);
            var worldVerts = new Vector3[verts.Length];
            var worldNorms = new Vector3[norms.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                worldVerts[i] = m.MultiplyPoint3x4(verts[i]);
                if (i < norms.Length) worldNorms[i] = m.MultiplyVector(norms[i]).normalized;
            }

            int subCount = src.subMeshCount;
            var frameTris = new List<int>[subCount];
            var leafRightTris = new List<int>[subCount];
            var leafLeftTris = new List<int>[subCount];
            for (int s = 0; s < subCount; s++)
            {
                frameTris[s] = new List<int>();
                leafRightTris[s] = new List<int>();
                leafLeftTris[s] = new List<int>();
                int[] tris = src.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    Vector3 center = (worldVerts[a] + worldVerts[b] + worldVerts[c]) / 3f;
                    bool inDoorOpening = Mathf.Abs(center.x) <= 1.03f
                                      && center.y >= 0.245f && center.y <= 2.55f
                                      && Mathf.Abs(center.z) <= 0.11f;
                    if (inDoorOpening)
                    {
                        var dst = center.x < 0f ? leafLeftTris[s] : leafRightTris[s];
                        dst.Add(a); dst.Add(b); dst.Add(c);
                    }
                    else
                    {
                        frameTris[s].Add(a); frameTris[s].Add(b); frameTris[s].Add(c);
                    }
                }
            }

            frameMesh = CopySubmeshesToNewMesh(worldVerts, worldNorms, uvs, frameTris, frameMesh, $"{MaterialsOutDir}/Mesh_DoorFrame.asset");
            leafRightMesh = CopySubmeshesToNewMesh(worldVerts, worldNorms, uvs, leafRightTris, leafRightMesh, $"{MaterialsOutDir}/Mesh_DoorLeaf.asset");
            leafLeftMesh = CopySubmeshesToNewMesh(worldVerts, worldNorms, uvs, leafLeftTris, leafLeftMesh, $"{MaterialsOutDir}/Mesh_DoorLeaf_L.asset");
        }

        private struct ClipVert
        {
            public Vector3 pos;
            public Vector3 norm;
            public Vector2 uv;
        }

        private static Mesh CreateOrUpdateCorridorCutoutMesh(bool cutWest, bool cutEast, string assetName)
        {
            var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/Corridor_Straight.fbx");
            var temp = Object.Instantiate(srcGo);
            var mf = temp.GetComponentInChildren<MeshFilter>(true);
            Mesh src = mf.sharedMesh;

            var verts = src.vertices;
            var norms = src.normals;
            var uvs = src.uv;
            Matrix4x4 m = Matrix4x4.TRS(mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);

            var outVerts = new List<Vector3>(verts.Length + 512);
            var outNorms = new List<Vector3>(verts.Length + 512);
            var outUvs = new List<Vector2>(verts.Length + 512);
            for (int i = 0; i < verts.Length; i++)
            {
                outVerts.Add(m.MultiplyPoint3x4(verts[i]));
                outNorms.Add(i < norms.Length ? m.MultiplyVector(norms[i]).normalized : Vector3.up);
                outUvs.Add(i < uvs.Length ? uvs[i] : Vector2.zero);
            }

            var westBox = new Bounds();
            westBox.SetMinMax(new Vector3(-2.10f, 0.215f, -1.03f), new Vector3(-1.45f, 2.55f, 1.03f));
            var eastBox = new Bounds();
            eastBox.SetMinMax(new Vector3(1.45f, 0.215f, -1.03f), new Vector3(2.10f, 2.55f, 1.03f));

            int subCount = src.subMeshCount;
            var keptTris = new List<int>[subCount];
            var scratchTrisA = new List<int>(64);
            var scratchTrisB = new List<int>(64);

            for (int s = 0; s < subCount; s++)
            {
                keptTris[s] = new List<int>();
                int[] tris = src.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    scratchTrisA.Clear();
                    scratchTrisA.Add(tris[i]);
                    scratchTrisA.Add(tris[i + 1]);
                    scratchTrisA.Add(tris[i + 2]);

                    if (cutWest)
                    {
                        scratchTrisB.Clear();
                        for (int k = 0; k < scratchTrisA.Count; k += 3)
                        {
                            SubtractBoxFromTriangle(
                                scratchTrisA[k], scratchTrisA[k + 1], scratchTrisA[k + 2],
                                westBox, outVerts, outNorms, outUvs, scratchTrisB);
                        }
                        scratchTrisA.Clear();
                        scratchTrisA.AddRange(scratchTrisB);
                    }

                    if (cutEast && scratchTrisA.Count > 0)
                    {
                        scratchTrisB.Clear();
                        for (int k = 0; k < scratchTrisA.Count; k += 3)
                        {
                            SubtractBoxFromTriangle(
                                scratchTrisA[k], scratchTrisA[k + 1], scratchTrisA[k + 2],
                                eastBox, outVerts, outNorms, outUvs, scratchTrisB);
                        }
                        scratchTrisA.Clear();
                        scratchTrisA.AddRange(scratchTrisB);
                    }

                    keptTris[s].AddRange(scratchTrisA);
                }
            }
            Object.DestroyImmediate(temp);

            var outMesh = new Mesh { name = assetName };
            return CopySubmeshesToNewMesh(outVerts.ToArray(), outNorms.ToArray(), outUvs.ToArray(), keptTris, outMesh, $"{MaterialsOutDir}/{assetName}.asset");
        }

        private static void SubtractBoxFromTriangle(
            int i0, int i1, int i2,
            Bounds box,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> dstTris)
        {
            Vector3 p0 = verts[i0], p1 = verts[i1], p2 = verts[i2];
            Vector3 bMin = box.min, bMax = box.max;

            float triMinX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x));
            float triMaxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x));
            float triMinY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y));
            float triMaxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y));
            float triMinZ = Mathf.Min(p0.z, Mathf.Min(p1.z, p2.z));
            float triMaxZ = Mathf.Max(p0.z, Mathf.Max(p1.z, p2.z));

            if (triMaxX <= bMin.x || triMinX >= bMax.x ||
                triMaxY <= bMin.y || triMinY >= bMax.y ||
                triMaxZ <= bMin.z || triMinZ >= bMax.z)
            {
                dstTris.Add(i0); dstTris.Add(i1); dstTris.Add(i2);
                return;
            }

            var insidePoly = new List<ClipVert>(8)
            {
                new ClipVert { pos = p0, norm = norms[i0], uv = uvs[i0] },
                new ClipVert { pos = p1, norm = norms[i1], uv = uvs[i1] },
                new ClipVert { pos = p2, norm = norms[i2], uv = uvs[i2] }
            };
            var nextInside = new List<ClipVert>(8);
            var outsidePoly = new List<ClipVert>(8);

            for (int planeIdx = 0; planeIdx < 6 && insidePoly.Count >= 3; planeIdx++)
            {
                int axis = planeIdx >> 1;
                bool isMin = (planeIdx & 1) == 0;
                float planeCoord = isMin ? bMin[axis] : bMax[axis];

                nextInside.Clear();
                outsidePoly.Clear();

                int count = insidePoly.Count;
                ClipVert prev = insidePoly[count - 1];
                float prevDist = isMin ? (prev.pos[axis] - planeCoord) : (planeCoord - prev.pos[axis]);
                bool prevIn = prevDist >= 0f;

                for (int k = 0; k < count; k++)
                {
                    ClipVert curr = insidePoly[k];
                    float currDist = isMin ? (curr.pos[axis] - planeCoord) : (planeCoord - curr.pos[axis]);
                    bool currIn = currDist >= 0f;

                    if (currIn != prevIn)
                    {
                        float denom = prevDist - currDist;
                        float t = Mathf.Abs(denom) > 1e-7f ? Mathf.Clamp01(prevDist / denom) : 0.5f;
                        ClipVert inter = new ClipVert
                        {
                            pos = Vector3.Lerp(prev.pos, curr.pos, t),
                            norm = Vector3.Lerp(prev.norm, curr.norm, t).normalized,
                            uv = Vector2.Lerp(prev.uv, curr.uv, t)
                        };
                        nextInside.Add(inter);
                        outsidePoly.Add(inter);
                    }

                    if (currIn)
                        nextInside.Add(curr);
                    else
                        outsidePoly.Add(curr);

                    prev = curr;
                    prevDist = currDist;
                    prevIn = currIn;
                }

                EmitFanPolygon(outsidePoly, verts, norms, uvs, dstTris);

                insidePoly.Clear();
                insidePoly.AddRange(nextInside);
            }
        }

        private static void EmitFanPolygon(
            List<ClipVert> poly,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> dstTris)
        {
            if (poly.Count < 3) return;
            int firstIdx = -1;
            int prevIdx = -1;

            for (int i = 0; i < poly.Count; i++)
            {
                if (i >= 2)
                {
                    Vector3 e1 = poly[i - 1].pos - poly[0].pos;
                    Vector3 e2 = poly[i].pos - poly[0].pos;
                    if (Vector3.Cross(e1, e2).sqrMagnitude <= 1e-10f)
                        continue;
                }

                if (firstIdx < 0)
                {
                    firstIdx = AppendClipVert(poly[0], verts, norms, uvs);
                    prevIdx = AppendClipVert(poly[1], verts, norms, uvs);
                }

                if (i >= 2)
                {
                    int currIdx = AppendClipVert(poly[i], verts, norms, uvs);
                    dstTris.Add(firstIdx);
                    dstTris.Add(prevIdx);
                    dstTris.Add(currIdx);
                    prevIdx = currIdx;
                }
            }
        }

        private static int AppendClipVert(ClipVert v, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs)
        {
            int idx = verts.Count;
            verts.Add(v.pos);
            norms.Add(v.norm);
            uvs.Add(v.uv);
            return idx;
        }

        private static Mesh CopySubmeshesToNewMesh(Vector3[] verts, Vector3[] norms, Vector2[] uvs, List<int>[] subTris, Mesh dest, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            Mesh target = existing ?? dest;
            target.Clear();
            target.vertices = verts;
            if (norms != null && norms.Length == verts.Length) target.normals = norms;
            if (uvs != null && uvs.Length == verts.Length) target.uv = uvs;
            target.subMeshCount = subTris.Length;
            for (int s = 0; s < subTris.Length; s++)
                target.SetTriangles(subTris[s], s, true);
            target.RecalculateBounds();
            if (norms == null || norms.Length != verts.Length) target.RecalculateNormals();

            if (existing == null)
                AssetDatabase.CreateAsset(target, assetPath);
            else
                EditorUtility.SetDirty(target);
            return target;
        }

        private static bool DetectCapSidePositiveZ(GameObject root, GameObject visual)
        {
            var mf = visual.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return true;
            var verts = mf.sharedMesh.vertices;
            Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            int posHits = 0, negHits = 0;
            foreach (var v in verts)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                if (Mathf.Abs(p.x) < 1.2f && p.y > 0.5f && p.y < 3.0f)
                {
                    if (p.z > 1.7f) posHits++;
                    else if (p.z < -1.7f) negHits++;
                }
            }
            return posHits >= negHits;
        }

        private static void RebuildSceneCorridor(
            GameObject pStraight,
            GameObject pDoors,
            GameObject pEnd,
            GameObject pCrate,
            GameObject pLocker,
            GameObject pConsole,
            Mesh mCutWest,
            Mesh mCutBoth,
            bool endCapPositiveZ)
        {
            var worldRoot = GameObject.Find("OutpostExpansionWorld");
            if (worldRoot == null)
            {
                Debug.LogWarning("[CorridorKitBuilder] OutpostExpansionWorld not found in active scene.");
                return;
            }

            // Preserve interactive HubCorridor widgets (CorridorStatusPanel, WallTablet_Corridor, CorridorSpawn) before replacing geometry
            var oldCorr = worldRoot.transform.Find("HubCorridor");
            GameObject statusPanelGo = oldCorr != null ? oldCorr.Find("CorridorStatusPanel")?.gameObject : null;
            GameObject tabletGo = oldCorr != null ? oldCorr.Find("WallTablet_Corridor")?.gameObject : null;
            Transform corrSpawnTf = oldCorr != null ? oldCorr.Find("CorridorSpawn") : null;

            if (statusPanelGo != null) statusPanelGo.transform.SetParent(worldRoot.transform, true);
            if (tabletGo != null) tabletGo.transform.SetParent(worldRoot.transform, true);
            if (corrSpawnTf != null) corrSpawnTf.SetParent(worldRoot.transform, true);

            if (oldCorr != null) Object.DestroyImmediate(oldCorr.gameObject);

            // Destroy legacy primitive doors that will be replaced by Corridor_Doors prefabs
            DestroyIfExists(worldRoot.transform, "Door_CommandToCorridor");
            DestroyIfExists(worldRoot.transform, "Door_Greenhouse");
            DestroyIfExists(worldRoot.transform, "Door_Storage");
            DestroyIfExists(worldRoot.transform, "Door_Medbay");
            DestroyIfExists(worldRoot.transform, "CorridorKit_Chain");
            DestroyIfExists(worldRoot.transform, "InteriorKit_Props");

            var chain = new GameObject("HubCorridor");
            chain.transform.SetParent(worldRoot.transform, false);

            // Modular 7-Piece Chain on 4.0m Grid (floor top Y=0.0 -> module root Y=-0.20):
            // 1) Module 1: Corridor_Straight at Z=-4.16 (with West doorway cutout for Alcove)
            var mod1 = InstantiateModule(pStraight, chain.transform, "Corridor_Mod1_North", new Vector3(0f, -0.20f, -4.16f), Vector3.zero);
            ApplySideCutoutToModule(mod1, mCutWest, cutWest: true, cutEast: false);

            // 2) Alcove West Dead-End: Corridor_End at (-4.00, -0.20, -4.16), capped at -X
            float endYaw = endCapPositiveZ ? -90f : 90f;
            InstantiateModule(pEnd, chain.transform, "Corridor_Mod2_WestAlcoveEnd", new Vector3(-4.00f, -0.20f, -4.16f), new Vector3(0f, endYaw, 0f));

            // 3) Module 2: Central Junction Corridor_Straight at Z=-8.16 (East & West doorway cutouts)
            var mod2 = InstantiateModule(pStraight, chain.transform, "Corridor_Mod3_Junction", new Vector3(0f, -0.20f, -8.16f), Vector3.zero);
            ApplySideCutoutToModule(mod2, mCutBoth, cutWest: true, cutEast: true);

            // 4) Greenhouse Branch Door: Corridor_Doors at (-4.00, -0.20, -8.16)
            var ghDoorGo = InstantiateModule(pDoors, chain.transform, "Corridor_Mod4_GreenhouseDoor", new Vector3(-4.00f, -0.20f, -8.16f), new Vector3(0f, 90f, 0f));
            ConfigureDoorInstance(ghDoorGo, "door.name.greenhouse");

            // Shift Room_Greenhouse + HabHull_GreenhouseRoof cleanly to meet West opening at X=-6.00, Z=-8.16
            ShiftBayIfNeeded(worldRoot.transform, "Room_Greenhouse", new Vector3(-4.38f, 0f, -1.66f), expectedTargetX: -8.41f);
            ShiftRoofPlate(worldRoot.transform, "HabHull_GreenhouseRoof", new Vector3(-8.41f, 3.16f, -8.16f));

            // 5) Storage Branch Door: Corridor_Doors at (+4.00, -0.20, -8.16)
            var stDoorGo = InstantiateModule(pDoors, chain.transform, "Corridor_Mod5_StorageDoor", new Vector3(4.00f, -0.20f, -8.16f), new Vector3(0f, -90f, 0f));
            ConfigureDoorInstance(stDoorGo, "door.name.storage");

            // Shift Room_Storage + HabHull_StorageRoof cleanly to meet East opening at X=+6.00, Z=-8.16
            ShiftBayIfNeeded(worldRoot.transform, "Room_Storage", new Vector3(4.38f, 0f, 1.34f), expectedTargetX: 8.41f);
            ShiftRoofPlate(worldRoot.transform, "HabHull_StorageRoof", new Vector3(8.41f, 3.16f, -8.16f));

            // 6) Airlock Vestibule Door: Corridor_Doors at (0, -0.20, -12.16) leading straight into Room_Airlock (-14.22)
            var alEntryGo = InstantiateModule(pDoors, chain.transform, "Corridor_Mod6_AirlockEntryDoor", new Vector3(0f, -0.20f, -12.16f), new Vector3(0f, 180f, 0f));
            ConfigureDoorInstance(alEntryGo, "door.name.corridor");

            // 7) Medbay Branch Door: Corridor_Doors at (-5.06, -0.20, 1.275) aligned directly with Command West opening (Z=1.35) & Room_Medbay (Z=1.275)
            var medDoorGo = InstantiateModule(pDoors, chain.transform, "Corridor_Mod7_MedbayDoor", new Vector3(-5.06f, -0.20f, 1.275f), new Vector3(0f, 90f, 0f));
            ConfigureDoorInstance(medDoorGo, "door.name.medbay");
            ShiftBayIfNeeded(worldRoot.transform, "Room_Medbay", new Vector3(-3.94f, 0f, 0f), expectedTargetX: -8.93f);
            ShiftRoofPlate(worldRoot.transform, "HabHull_MedbayRoof", new Vector3(-8.93f, 3.16f, 1.275f));

            // Create junction collars where Room_Greenhouse / Room_Storage / Command / Airlock transition to 4m Corridor width
            BuildBulkheadCollar(chain.transform, "Collar_CommandToHub", new Vector3(0f, 1.8f, -2.16f), new Vector3(4.0f, 3.6f, 0.25f), openW: 1.50f, openH: 2.30f, alongZ: true);
            BuildBulkheadCollar(chain.transform, "Collar_AirlockBridge", new Vector3(0f, 1.8f, -14.16f), new Vector3(4.0f, 3.6f, 0.25f), openW: 1.50f, openH: 2.30f, alongZ: true);
            BuildBulkheadCollar(chain.transform, "Collar_CommandToMedbayNorthTrim", new Vector3(-3.06f, 1.8f, 3.09f), new Vector3(0.25f, 3.6f, 0.38f), openW: 0f, openH: 0f, alongZ: false);

            // Clean bulkhead portal jambs & threshold plates around the 3 corridor side cutouts
            BuildSidePortalFrame(chain.transform, "PortalFrame_AlcoveWest", new Vector3(-1.84f, 0f, -4.16f));
            BuildSidePortalFrame(chain.transform, "PortalFrame_GreenhouseWest", new Vector3(-1.84f, 0f, -8.16f));
            BuildSidePortalFrame(chain.transform, "PortalFrame_StorageEast", new Vector3(1.84f, 0f, -8.16f));

            // Re-parent and reposition CorridorStatusPanel & WallTablet_Corridor & CorridorSpawn cleanly onto new corridor inner walls
            if (statusPanelGo != null)
            {
                statusPanelGo.transform.SetParent(chain.transform, true);
                statusPanelGo.transform.position = new Vector3(1.80f, 1.65f, -5.00f);
                statusPanelGo.transform.eulerAngles = new Vector3(0f, 90f, 0f);
            }
            if (tabletGo != null)
            {
                tabletGo.transform.SetParent(chain.transform, true);
                tabletGo.transform.position = new Vector3(-1.80f, 1.45f, -10.20f);
                tabletGo.transform.eulerAngles = new Vector3(0f, -90f, 0f);
            }
            if (corrSpawnTf != null)
            {
                corrSpawnTf.SetParent(chain.transform, true);
                corrSpawnTf.position = new Vector3(0f, 1.0f, -3.90f);
            }

            // Warm & Cool Point Lights along corridor chain (Zero Realtime Shadows for WebGL)
            CreateCorridorPointLight(chain.transform, "Light_CorrNorthWarm", new Vector3(0f, 3.15f, -4.16f), new Color(1.00f, 0.84f, 0.66f), 1.35f, 8.5f);
            CreateCorridorPointLight(chain.transform, "Light_CorrAlcoveWarm", new Vector3(-3.80f, 3.15f, -4.16f), new Color(0.98f, 0.78f, 0.56f), 1.25f, 7.0f);
            CreateCorridorPointLight(chain.transform, "Light_CorrHubCool", new Vector3(0f, 3.15f, -8.16f), new Color(0.72f, 0.90f, 1.00f), 1.45f, 9.5f);
            CreateCorridorPointLight(chain.transform, "Light_CorrSouthWarm", new Vector3(0f, 3.15f, -12.16f), new Color(0.95f, 0.86f, 0.74f), 1.25f, 8.0f);

            // Interior Props + 2 Interactable One-Line Facts
            var propsGroup = new GameObject("InteriorKit_Props");
            propsGroup.transform.SetParent(chain.transform, false);

            // Storage Bay props (Shifted Storage floor center X=8.41, Z=-8.16)
            PlaceProp(pLocker, propsGroup.transform, "Prop_Locker_Storage1", new Vector3(9.95f, 0f, -7.25f), new Vector3(0f, -90f, 0f));
            PlaceProp(pCrate, propsGroup.transform, "Prop_Crate_Storage1", new Vector3(9.95f, 0f, -9.25f), new Vector3(0f, -15f, 0f));
            PlaceProp(pCrate, propsGroup.transform, "Prop_Crate_Storage2", new Vector3(9.95f, 0.67f, -9.25f), new Vector3(0f, 12f, 0f));

            // Corridor props (+ One-Line Fact POI #1 on Corridor Locker)
            var corrLocker = PlaceProp(pLocker, propsGroup.transform, "Prop_Locker_Corridor", new Vector3(1.42f, 0f, -6.50f), new Vector3(0f, -90f, 0f));
            corrLocker.AddComponent<InteractableProp>().ConfigureCustomFact(
                "[E] Inspect EVA Suit Locker",
                "Fact: Closed-loop O2 scrubbers capture cabin CO2 via regenerable amine beds [source: src_co2]",
                "src_co2");

            PlaceProp(pCrate, propsGroup.transform, "Prop_Crate_Corridor", new Vector3(-1.40f, 0f, -10.80f), new Vector3(0f, 22f, 0f));

            // Command Module Console + Alcove Console (+ One-Line Fact POI #2 on Alcove Console)
            PlaceProp(pConsole, propsGroup.transform, "Prop_Console_CommandSide", new Vector3(2.05f, 0f, 0.65f), new Vector3(0f, -90f, 0f));
            var alcoveConsole = PlaceProp(pConsole, propsGroup.transform, "Prop_Console_AlcoveTelemetry", new Vector3(-4.85f, 0f, -4.16f), new Vector3(0f, 90f, 0f));
            alcoveConsole.AddComponent<InteractableProp>().ConfigureCustomFact(
                "[E] Inspect Deep Space Telemetry Console",
                "Fact: Earth-Mars one-way radio light delay ranges from 4 to 22 minutes [source: src_comms]",
                "src_comms");

            // Keep lights list updated on RoomStateBinder & dim ambient cleanly
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.18f, 1f);
            UpdateRoomStateBinderCorridorLights(chain);
        }

        private static GameObject InstantiateModule(GameObject prefab, Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            return inst;
        }

        private static void ApplySideCutoutToModule(GameObject mod, Mesh cutMesh, bool cutWest, bool cutEast)
        {
            var mf = mod.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && cutMesh != null)
            {
                mf.sharedMesh = cutMesh;
                mf.transform.localPosition = Vector3.zero;
                mf.transform.localRotation = Quaternion.identity;
                mf.transform.localScale = Vector3.one;
            }

            if (cutWest) ReplaceWallColWithDoorSplit(mod.transform, "Col_Wall_W", -1.94f);
            if (cutEast) ReplaceWallColWithDoorSplit(mod.transform, "Col_Wall_E", 1.94f);
        }

        private static void ReplaceWallColWithDoorSplit(Transform parent, string colName, float wallX)
        {
            var old = parent.Find(colName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            AddBoxCol(parent.gameObject, colName + "_N", new Vector3(wallX, 1.90f, 1.52f), new Vector3(0.14f, 3.60f, 0.96f)).isStatic = true;
            AddBoxCol(parent.gameObject, colName + "_S", new Vector3(wallX, 1.90f, -1.52f), new Vector3(0.14f, 3.60f, 0.96f)).isStatic = true;
            AddBoxCol(parent.gameObject, colName + "_Hdr", new Vector3(wallX, 3.12f, 0f), new Vector3(0.14f, 1.18f, 2.08f)).isStatic = true;
        }

        private static void ConfigureDoorInstance(GameObject doorGo, string nameKey)
        {
            var d = doorGo.GetComponent<DoorInteractable>();
            if (d == null) return;
            var so = new SerializedObject(d);
            so.FindProperty("doorNameKey").stringValue = nameKey;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ShiftBayIfNeeded(Transform worldRoot, string bayName, Vector3 delta, float expectedTargetX)
        {
            var bay = worldRoot.Find(bayName);
            if (bay == null) return;
            bay.position = delta;
        }

        private static void ShiftRoofPlate(Transform worldRoot, string roofName, Vector3 targetPos)
        {
            var marsWorld = worldRoot.Find("MarsSurfaceWorld");
            var roof = marsWorld != null ? marsWorld.Find(roofName) : worldRoot.Find(roofName);
            if (roof != null)
                roof.position = targetPos;
        }

        private static void BuildBulkheadCollar(Transform parent, string name, Vector3 pos, Vector3 outerSize, float openW, float openH, bool alongZ)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsOutDir}/M_Kit_panel_grey.mat");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            float sideW = (outerSize.x - openW) * 0.5f;
            float hdrH = outerSize.y - openH;
            if (alongZ)
            {
                AddTrimSlab(go.transform, "Left", new Vector3(-(openW * 0.5f + sideW * 0.5f), 0f, 0f), new Vector3(sideW, outerSize.y, outerSize.z), mat);
                AddTrimSlab(go.transform, "Right", new Vector3(openW * 0.5f + sideW * 0.5f, 0f, 0f), new Vector3(sideW, outerSize.y, outerSize.z), mat);
                AddTrimSlab(go.transform, "Top", new Vector3(0f, outerSize.y * 0.5f - hdrH * 0.5f, 0f), new Vector3(openW, hdrH, outerSize.z), mat);
            }
            else
            {
                AddTrimSlab(go.transform, "TrimCap", Vector3.zero, outerSize, mat);
            }
            SetStaticFlagsRecursive(go, true);
        }

        private static void BuildSidePortalFrame(Transform parent, string name, Vector3 wallFloorCenter)
        {
            var jambMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsOutDir}/M_Kit_panel_grey.mat");
            var plateMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsOutDir}/M_Kit_steel.mat");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = wallFloorCenter;

            AddTrimSlab(go.transform, "Jamb_N", new Vector3(0f, 1.185f, 1.06f), new Vector3(0.36f, 2.37f, 0.08f), jambMat, keepCollider: false);
            AddTrimSlab(go.transform, "Jamb_S", new Vector3(0f, 1.185f, -1.06f), new Vector3(0.36f, 2.37f, 0.08f), jambMat, keepCollider: false);
            AddTrimSlab(go.transform, "Lintel_Top", new Vector3(0f, 2.375f, 0f), new Vector3(0.36f, 0.08f, 2.20f), jambMat, keepCollider: false);
            AddTrimSlab(go.transform, "Threshold_Floor", new Vector3(0f, 0.002f, 0f), new Vector3(0.36f, 0.025f, 2.06f), plateMat, keepCollider: false);
            SetStaticFlagsRecursive(go, true);
        }

        private static void AddTrimSlab(Transform p, string n, Vector3 localPos, Vector3 size, Material mat, bool keepCollider = true)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = n;
            cube.transform.SetParent(p, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            if (!keepCollider)
            {
                var c = cube.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            var r = cube.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static GameObject PlaceProp(GameObject prefab, Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            return inst;
        }

        private static void CreateCorridorPointLight(Transform parent, string name, Vector3 pos, Color col, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static void UpdateRoomStateBinderCorridorLights(GameObject chain)
        {
            var binder = Object.FindAnyObjectByType<RoomStateBinder>();
            if (binder != null)
            {
                var so = new SerializedObject(binder);
                var lights = new List<Light>();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (l.type == LightType.Point && !l.name.Contains("Emergency") && !l.name.Contains("Blackout") && !l.name.Contains("Alarm"))
                        lights.Add(l);
                }
                var prop = so.FindProperty("mainRoomLights");
                if (prop != null)
                {
                    prop.arraySize = lights.Count;
                    for (int i = 0; i < lights.Count; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(binder);
            }

            var fpCtrl = Object.FindAnyObjectByType<FirstPersonRoomController>();
            if (fpCtrl != null)
            {
                var so = new SerializedObject(fpCtrl);
                var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include);
                var doorsProp = so.FindProperty("sceneDoors");
                if (doorsProp != null)
                {
                    doorsProp.arraySize = doors.Length;
                    for (int i = 0; i < doors.Length; i++)
                        doorsProp.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fpCtrl);
            }
        }

        private static GameObject AddBoxCol(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var col = go.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
            return go;
        }

        private static void SetStaticFlagsRecursive(GameObject root, bool isStatic)
        {
            root.isStatic = isStatic;
            foreach (Transform t in root.transform)
                SetStaticFlagsRecursive(t.gameObject, isStatic);
        }

        private static void DestroyIfExists(Transform parent, string childName)
        {
            var c = parent != null ? parent.Find(childName) : null;
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        private static GameObject SaveInteriorPrefab(GameObject root, string prefabPath)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }
    }
}
