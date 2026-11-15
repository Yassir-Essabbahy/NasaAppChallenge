using System.Collections.Generic;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Outpost.Editor
{
    /// <summary>
    /// Idempotent Editor builder for Part 1 & Part 2:
    /// - Ensures procedural T_SuitVisor_1024x576 sprite exists.
    /// - Builds/updates Assets/Prefabs/SuitRack.prefab with humanoid primitive "Prop_Suit",
    ///   PSX Lit/Unlit materials, and single interaction BoxCollider.
    /// - Places SuitRack in Outpost.unity inside CommandModuleRoom (<= 3m from PF_Monitor)
    ///   and wires SuitController + GameBootstrap references on OutpostControllers.
    /// Safe to run repeatedly from Outpost/Suit/Build Suit Rack & Setup.
    /// </summary>
    public static class SuitSetupBuilder
    {
        public const string OutpostScenePath = "Assets/Scenes/Outpost.unity";
        public const string SuitRackPrefabPath = "Assets/Prefabs/SuitRack.prefab";
        private const string SuitPsxMatDir = "Assets/Art/PSX/Suit";
        private const string PsxTexDir = "Assets/Art/PSX/Textures";

        private static readonly Vector3 RackWorldPos = new Vector3(2.08f, 0.0f, 2.55f);
        private static readonly Vector3 RackWorldEuler = new Vector3(0.0f, 185.0f, 0.0f);

        private struct SuitMaterialPalette
        {
            public Material RackMetal;
            public Material RackTrim;
            public Material RackGlow;
            public Material EvaWhite;
            public Material EvaJoint;
            public Material EvaOrange;
            public Material VisorDark;
        }

        [MenuItem("Outpost/Suit/Build Suit Rack & Setup")]
        public static void BuildSuitRackAndSetupMenu()
        {
            BuildAll(regenerateVisor: false);
        }

        public static void BuildAll(bool regenerateVisor = false)
        {
            Sprite visorSprite = SuitVisorSpriteGenerator.GenerateOrLoadVisorSprite(regenerateVisor);
            SuitMaterialPalette palette = EnsureSuitPsxMaterials();
            GameObject prefab = BuildOrUpdateSuitRackPrefab(palette);

            if (prefab == null)
            {
                Debug.LogError("[SuitSetupBuilder] Failed to build SuitRack prefab.");
                return;
            }

            if (!EnsureOutpostSceneOpen())
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            Transform commandRoom = FindSceneTransform(activeScene, "CommandModuleRoom");
            RemoveExistingSuitRacks(activeScene);

            GameObject rackInstance = PrefabUtility.InstantiatePrefab(prefab, commandRoom != null ? commandRoom : null) as GameObject;
            if (rackInstance == null)
            {
                Debug.LogError("[SuitSetupBuilder] Failed to instantiate SuitRack prefab into Outpost scene.");
                return;
            }

            rackInstance.name = "SuitRack";
            rackInstance.transform.SetPositionAndRotation(RackWorldPos, Quaternion.Euler(RackWorldEuler));
            rackInstance.transform.localScale = Vector3.one;

            Transform monitor = FindSceneTransform(activeScene, "PF_Monitor");
            float monitorDist = monitor != null
                ? Vector3.Distance(rackInstance.transform.position, monitor.position)
                : -1f;

            WireControllersInScene(activeScene, rackInstance, visorSprite);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = rackInstance;

            Debug.Log(
                $"[SuitSetupBuilder] Built SuitRack prefab, placed in CommandModuleRoom (dist to PF_Monitor: {monitorDist:0.00}m), " +
                $"and wired SuitController/GameBootstrap cleanly.");
        }

        private static bool EnsureOutpostSceneOpen()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == OutpostScenePath)
                return true;

            if (active.isDirty && !Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[SuitSetupBuilder] Aborted: save or discard active scene changes before running on Outpost.unity.");
                    return false;
                }
            }

            EditorSceneManager.OpenScene(OutpostScenePath, OpenSceneMode.Single);
            return SceneManager.GetActiveScene().path == OutpostScenePath;
        }

        private static SuitMaterialPalette EnsureSuitPsxMaterials()
        {
            PSXMaterialApplier.EnsurePsxMaterialsAndTextures();
            EnsureFolder(SuitPsxMatDir);

            Shader litShader = Shader.Find("Outpost/PSXLit");
            Shader unlitShader = Shader.Find("Outpost/PSXUnlit");
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

            Texture2D texWall = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_PSX_Wall.png");
            Texture2D texMetal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_PSX_Metal.png");
            Texture2D texDirtyMetal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_PSX_DirtyMetal.png");
            Texture2D texScreen = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PsxTexDir}/T_PSX_Screen.png");

            var palette = new SuitMaterialPalette
            {
                RackMetal = CreateOrUpdatePsxMaterial("M_Suit_RackMetal", litShader, texMetal, new Color(0.28f, 0.31f, 0.36f, 1f), Color.black, 0f, 0.80f, 0.22f),
                RackTrim = CreateOrUpdatePsxMaterial("M_Suit_RackTrim", litShader, texDirtyMetal, new Color(0.76f, 0.42f, 0.18f, 1f), Color.black, 0f, 0.80f, 0.22f),
                RackGlow = CreateOrUpdatePsxMaterial("M_Suit_RackGlow", unlitShader, texScreen, new Color(0.28f, 0.88f, 0.94f, 1f), new Color(0.25f, 0.88f, 0.94f, 1f), 1.25f, 0.25f, 0.05f),
                EvaWhite = CreateOrUpdatePsxMaterial("M_Suit_EVA_White", litShader, texWall, new Color(0.78f, 0.79f, 0.83f, 1f), Color.black, 0f, 0.78f, 0.22f),
                EvaJoint = CreateOrUpdatePsxMaterial("M_Suit_EVA_Joint", litShader, texDirtyMetal, new Color(0.22f, 0.24f, 0.28f, 1f), Color.black, 0f, 0.82f, 0.22f),
                EvaOrange = CreateOrUpdatePsxMaterial("M_Suit_EVA_Orange", litShader, texWall, new Color(0.84f, 0.37f, 0.16f, 1f), Color.black, 0f, 0.78f, 0.22f),
                VisorDark = CreateOrUpdatePsxMaterial("M_Suit_Visor_Dark", litShader, texScreen, new Color(0.08f, 0.11f, 0.16f, 1f), new Color(0.08f, 0.16f, 0.22f, 1f), 0.40f, 0.45f, 0.18f),
            };

            AssetDatabase.SaveAssets();
            return palette;
        }

        private static Material CreateOrUpdatePsxMaterial(
            string matName,
            Shader shader,
            Texture2D tex,
            Color tint,
            Color emission,
            float emissionStrength,
            float affine,
            float fogWeight)
        {
            string path = $"{SuitPsxMatDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            if (tex != null && mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emission);
            if (mat.HasProperty("_EmissionStrength"))
                mat.SetFloat("_EmissionStrength", emissionStrength);
            if (mat.HasProperty("_AffineAmount"))
                mat.SetFloat("_AffineAmount", affine);
            if (mat.HasProperty("_FogWeight"))
                mat.SetFloat("_FogWeight", fogWeight);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = 2000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject BuildOrUpdateSuitRackPrefab(SuitMaterialPalette palette)
        {
            EnsureFolder("Assets/Prefabs");

            var root = new GameObject("SuitRack");
            try
            {
                // Single interaction collider covering locker & suit silhouette
                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.02f, 0f);
                col.size = new Vector3(0.95f, 2.04f, 0.56f);
                col.isTrigger = false;

                var interactable = root.AddComponent<InteractableProp>();
                interactable.Configure(InteractablePropType.SuitRack, "suit_put_on", string.Empty);

                // Rack structural frame
                CreatePrimitivePart("Rack_Base", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(0.94f, 0.08f, 0.54f), palette.RackMetal);
                CreatePrimitivePart("Rack_Backplate", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 1.04f, -0.23f), Vector3.zero, new Vector3(0.88f, 1.96f, 0.06f), palette.RackMetal);
                CreatePrimitivePart("Rack_PostLeft", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.43f, 1.04f, -0.02f), Vector3.zero, new Vector3(0.08f, 1.96f, 0.44f), palette.RackMetal);
                CreatePrimitivePart("Rack_PostRight", PrimitiveType.Cube, root.transform,
                    new Vector3(0.43f, 1.04f, -0.02f), Vector3.zero, new Vector3(0.08f, 1.96f, 0.44f), palette.RackMetal);
                CreatePrimitivePart("Rack_TopBar", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 2.00f, 0.01f), Vector3.zero, new Vector3(0.94f, 0.08f, 0.52f), palette.RackTrim);
                CreatePrimitivePart("Rack_HeaderPlate", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 1.91f, 0.24f), Vector3.zero, new Vector3(0.66f, 0.12f, 0.04f), palette.RackMetal);
                CreatePrimitivePart("Rack_BeaconStrip", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 1.99f, 0.275f), Vector3.zero, new Vector3(0.56f, 0.035f, 0.02f), palette.RackGlow);

                // Placeholder humanoid EVA suit child ("Prop_Suit") — swap this child directly with a custom model later
                var propSuit = new GameObject("Prop_Suit");
                propSuit.transform.SetParent(root.transform, false);
                propSuit.transform.localPosition = new Vector3(0f, 0.08f, 0.03f);

                CreatePrimitivePart("Suit_LegLeft", PrimitiveType.Capsule, propSuit.transform,
                    new Vector3(-0.12f, 0.42f, 0f), Vector3.zero, new Vector3(0.15f, 0.34f, 0.15f), palette.EvaWhite);
                CreatePrimitivePart("Suit_LegRight", PrimitiveType.Capsule, propSuit.transform,
                    new Vector3(0.12f, 0.42f, 0f), Vector3.zero, new Vector3(0.15f, 0.34f, 0.15f), palette.EvaWhite);
                CreatePrimitivePart("Suit_BootLeft", PrimitiveType.Cube, propSuit.transform,
                    new Vector3(-0.12f, 0.06f, 0.03f), Vector3.zero, new Vector3(0.14f, 0.12f, 0.22f), palette.EvaJoint);
                CreatePrimitivePart("Suit_BootRight", PrimitiveType.Cube, propSuit.transform,
                    new Vector3(0.12f, 0.06f, 0.03f), Vector3.zero, new Vector3(0.14f, 0.12f, 0.22f), palette.EvaJoint);
                CreatePrimitivePart("Suit_Belt", PrimitiveType.Cube, propSuit.transform,
                    new Vector3(0f, 0.76f, 0f), Vector3.zero, new Vector3(0.38f, 0.09f, 0.23f), palette.EvaJoint);
                CreatePrimitivePart("Suit_Torso", PrimitiveType.Capsule, propSuit.transform,
                    new Vector3(0f, 1.07f, 0f), Vector3.zero, new Vector3(0.40f, 0.30f, 0.24f), palette.EvaWhite);
                CreatePrimitivePart("Suit_ChestCollar", PrimitiveType.Cube, propSuit.transform,
                    new Vector3(0f, 1.30f, 0.02f), Vector3.zero, new Vector3(0.24f, 0.07f, 0.21f), palette.EvaOrange);
                CreatePrimitivePart("Suit_Backpack", PrimitiveType.Cube, propSuit.transform,
                    new Vector3(0f, 1.12f, -0.17f), Vector3.zero, new Vector3(0.32f, 0.44f, 0.15f), palette.EvaJoint);
                CreatePrimitivePart("Suit_ArmLeft", PrimitiveType.Capsule, propSuit.transform,
                    new Vector3(-0.27f, 1.05f, 0.02f), new Vector3(4f, 0f, -10f), new Vector3(0.12f, 0.28f, 0.12f), palette.EvaWhite);
                CreatePrimitivePart("Suit_ArmRight", PrimitiveType.Capsule, propSuit.transform,
                    new Vector3(0.27f, 1.05f, 0.02f), new Vector3(4f, 0f, 10f), new Vector3(0.12f, 0.28f, 0.12f), palette.EvaWhite);
                CreatePrimitivePart("Suit_GloveLeft", PrimitiveType.Sphere, propSuit.transform,
                    new Vector3(-0.32f, 0.78f, 0.04f), Vector3.zero, new Vector3(0.11f, 0.12f, 0.11f), palette.EvaJoint);
                CreatePrimitivePart("Suit_GloveRight", PrimitiveType.Sphere, propSuit.transform,
                    new Vector3(0.32f, 0.78f, 0.04f), Vector3.zero, new Vector3(0.11f, 0.12f, 0.11f), palette.EvaJoint);
                CreatePrimitivePart("Suit_Helmet", PrimitiveType.Sphere, propSuit.transform,
                    new Vector3(0f, 1.54f, 0.01f), Vector3.zero, new Vector3(0.28f, 0.30f, 0.29f), palette.EvaWhite);
                CreatePrimitivePart("Suit_Visor", PrimitiveType.Sphere, propSuit.transform,
                    new Vector3(0f, 1.55f, 0.095f), Vector3.zero, new Vector3(0.23f, 0.17f, 0.16f), palette.VisorDark);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SuitRackPrefabPath, out bool success);
                return success ? saved : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePrimitivePart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localEuler,
            Vector3 localScale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            // Remove primitive colliders so only SuitRack's root BoxCollider participates in raycasts/collision.
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
                mr.sharedMaterial = material;

            return go;
        }

        private static void WireControllersInScene(Scene scene, GameObject rackInstance, Sprite visorSprite)
        {
            GameObject controllersObj = FindSceneTransform(scene, "OutpostControllers")?.gameObject;
            if (controllersObj == null)
            {
                Debug.LogWarning("[SuitSetupBuilder] Could not find OutpostControllers in Outpost.unity.");
                return;
            }

            SuitController suitCtrl = controllersObj.GetComponent<SuitController>();
            if (suitCtrl == null)
                suitCtrl = controllersObj.AddComponent<SuitController>();

            PlayerGate gate = FindFirstInScene<PlayerGate>(scene);
            FirstPersonRoomController roomCtrl = FindFirstInScene<FirstPersonRoomController>(scene);
            AirlockController airlockCtrl = FindFirstInScene<AirlockController>(scene);
            AudioManager audioMgr = FindFirstInScene<AudioManager>(scene);
            AnnouncerSystem announcer = FindFirstInScene<AnnouncerSystem>(scene);
            AccessibilitySettings acc = FindFirstInScene<AccessibilitySettings>(scene);
            MissionFlowController flowCtrl = FindFirstInScene<MissionFlowController>(scene);
            TerminalController termCtrl = FindFirstInScene<TerminalController>(scene);
            GameBootstrap bootstrap = FindFirstInScene<GameBootstrap>(scene);

            Transform propSuitTr = rackInstance.transform.Find("Prop_Suit");
            GameObject propSuitGo = propSuitTr != null ? propSuitTr.gameObject : null;
            UITheme uiTheme = LoadTerminalTheme();

            suitCtrl.ConfigureReferences(
                gate,
                roomCtrl,
                airlockCtrl,
                audioMgr,
                announcer,
                acc,
                flowCtrl,
                termCtrl,
                propSuitGo,
                uiTheme,
                visorSprite);

            var so = new SerializedObject(suitCtrl);
            AssignObjProp(so, "playerGate", gate);
            AssignObjProp(so, "roomController", roomCtrl);
            AssignObjProp(so, "airlockController", airlockCtrl);
            AssignObjProp(so, "audioManager", audioMgr);
            AssignObjProp(so, "announcerSystem", announcer);
            AssignObjProp(so, "accessibility", acc);
            AssignObjProp(so, "missionFlowController", flowCtrl);
            AssignObjProp(so, "terminalController", termCtrl);
            AssignObjProp(so, "propSuitVisual", propSuitGo);
            AssignObjProp(so, "theme", uiTheme);
            AssignObjProp(so, "visorSprite", visorSprite);
            SerializedProperty armsProp = so.FindProperty("showFirstPersonArms");
            if (armsProp != null) armsProp.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(suitCtrl);

            if (bootstrap != null)
            {
                var bso = new SerializedObject(bootstrap);
                AssignObjProp(bso, "suitController", suitCtrl);
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
            }
        }

        private static void AssignObjProp(SerializedObject so, string propName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && value != null)
                prop.objectReferenceValue = value;
        }

        private static UITheme LoadTerminalTheme()
        {
            UITheme loaded = Resources.Load<UITheme>("TerminalTheme");
            if (loaded != null) return loaded;

            string[] guids = AssetDatabase.FindAssets("t:UITheme");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UITheme candidate = AssetDatabase.LoadAssetAtPath<UITheme>(path);
                if (candidate != null) return candidate;
            }
            return null;
        }

        private static void RemoveExistingSuitRacks(Scene scene)
        {
            var toDestroy = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(includeInactive: true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] == null) continue;
                    // Remove only the SuitRack interactable / prefab placed in command module (preserve unrelated Airlock_SuitRack decor).
                    if (transforms[t].name == "SuitRack")
                        toDestroy.Add(transforms[t].gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (toDestroy[i] != null)
                    Object.DestroyImmediate(toDestroy[i]);
            }
        }

        private static Transform FindSceneTransform(Scene scene, string targetName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(includeInactive: true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t].name == targetName)
                        return transforms[t];
                }
            }
            return null;
        }

        private static T FindFirstInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(includeInactive: true);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
