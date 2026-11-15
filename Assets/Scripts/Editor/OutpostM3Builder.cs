using System.Collections.Generic;
using System.IO;
using Outpost.Runtime;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Outpost.Editor
{
    /// <summary>
    /// Procedural Editor builder that generates Milestone 3/4/5 materials, modular primitive prefabs
    /// (converted cleanly to PSX shaders), ScriptableObject themes/sequences, and the main Outpost.unity scene.
    /// </summary>
    public static class OutpostM3Builder
    {
        private const string MatDir = "Assets/Materials/Room";
        private const string PrefabDir = "Assets/Prefabs/Room";
        private const string FactsDir = "Assets/Resources/Facts";
        private const string ScenePath = "Assets/Scenes/Outpost.unity";
        private const string TerminalScenePath = "Assets/Scenes/Terminal.unity";
        private const string TerminalPrefabPath = "Assets/Prefabs/UI/PF_TerminalRoot.prefab";
        private const string ModularFpsPrefabPath = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";

        [MenuItem("Outpost/Build M3 Outpost Scene & Prefabs")]
        public static void BuildAllMenuAlias()
        {
            BuildAll();
        }

        [MenuItem("Tools/Mars Outpost/Build M4 Scene, Prefabs & Fact Cards")]
        public static void BuildAll()
        {
            EnsureDirectories();
            var factDb = CreateFactDatabaseAssets();
            CreateM5ResourceAssets(out var fontTheme, out var introData, out var psxSettings);

            var mats = CreateMaterials();
            var prefabs = CreateAllRoomPrefabs(mats);

            AssembleOutpostScene(prefabs, factDb, fontTheme, introData, psxSettings);
            ConfigureBuildSettings();
            ConfigureWebGlQualitySettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OutpostM3Builder] Successfully built plain URP primitive Outpost scene, prefabs, and Assets/Scenes/Outpost.unity.");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
            if (!Directory.Exists(FactsDir)) Directory.CreateDirectory(FactsDir);
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        }

        private static void CreateM5ResourceAssets(
            out FontTheme fontTheme,
            out IntroSequenceData introData,
            out PSXSettings psxSettings)
        {
            TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont == null)
            {
                string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                if (guids.Length > 0)
                    defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            const string fontThemePath = "Assets/Resources/FontTheme.asset";
            fontTheme = AssetDatabase.LoadAssetAtPath<FontTheme>(fontThemePath);
            if (fontTheme == null)
            {
                fontTheme = ScriptableObject.CreateInstance<FontTheme>();
                AssetDatabase.CreateAsset(fontTheme, fontThemePath);
            }
            if (fontTheme.TitleFont == null) fontTheme.TitleFont = defaultFont;
            if (fontTheme.StampFont == null) fontTheme.StampFont = defaultFont;
            if (fontTheme.TerminalFont == null) fontTheme.TerminalFont = defaultFont;
            EditorUtility.SetDirty(fontTheme);

            var terminalTheme = Resources.Load<UITheme>("TerminalTheme");
            if (terminalTheme != null)
            {
                if (terminalTheme.Font == null && defaultFont != null)
                    terminalTheme.Font = defaultFont;
                terminalTheme.FontTheme = fontTheme;
                EditorUtility.SetDirty(terminalTheme);
            }

            const string introDataPath = "Assets/Resources/IntroSequenceData.asset";
            introData = AssetDatabase.LoadAssetAtPath<IntroSequenceData>(introDataPath);
            if (introData == null)
            {
                introData = ScriptableObject.CreateInstance<IntroSequenceData>();
                introData.PopulateDefaultCards();
                AssetDatabase.CreateAsset(introData, introDataPath);
            }
            else if (introData.Cards == null || introData.Cards.Count != 8)
            {
                introData.PopulateDefaultCards();
            }
            EditorUtility.SetDirty(introData);

            const string psxPath = "Assets/Resources/PSXSettings.asset";
            psxSettings = AssetDatabase.LoadAssetAtPath<PSXSettings>(psxPath);
            if (psxSettings == null)
            {
                psxSettings = ScriptableObject.CreateInstance<PSXSettings>();
                psxSettings.ApplyPreset(PSXResolutionPreset.Off, savePrefs: false);
                psxSettings.vertexSnapResolution = 160;
                psxSettings.affineAmount = 0.35f;
                psxSettings.colorLevels = 24;
                psxSettings.ditherStrength = 0.25f;
                psxSettings.grainAmount = 0f;
                AssetDatabase.CreateAsset(psxSettings, psxPath);
            }
            else
            {
                // Keep PSX disabled by default so primitives use standard URP shaders
                psxSettings.ApplyPreset(PSXResolutionPreset.Off, savePrefs: false);
                psxSettings.ditherStrength = 0.25f;
                psxSettings.grainAmount = 0f;
            }
            EditorUtility.SetDirty(psxSettings);
        }

        private static void ConfigureWebGlQualitySettings()
        {
            var prevShadows = QualitySettings.shadows;
            var prevAniso = QualitySettings.anisotropicFiltering;
            int prevAa = QualitySettings.antiAliasing;

            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.antiAliasing = 0;

            string urpReport = "URP asset not modified";
            RenderPipelineAsset rp = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                var rpSo = new SerializedObject(rp);
                var mainSh = rpSo.FindProperty("m_MainLightShadowsSupported");
                var addSh = rpSo.FindProperty("m_AdditionalLightShadowsSupported");
                var msaa = rpSo.FindProperty("m_MSAA");

                bool oldMain = mainSh != null && mainSh.boolValue;
                bool oldAdd = addSh != null && addSh.boolValue;
                int oldMsaa = msaa != null ? msaa.intValue : -1;

                if (mainSh != null) mainSh.boolValue = false;
                if (addSh != null) addSh.boolValue = false;
                if (msaa != null) msaa.intValue = 1;
                rpSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);

                urpReport = $"{rp.name}: MainLightShadows {oldMain}->false, AdditionalLightShadows {oldAdd}->false, MSAA {oldMsaa}->1";
            }

            Debug.Log($"[OutpostM3Builder] WebGL QualitySettings: shadows {prevShadows}->Disable, anisotropicFiltering {prevAniso}->Disable, antiAliasing {prevAa}->0 | {urpReport}");
        }

        private static FactDatabase CreateFactDatabaseAssets()
        {
            return ContentSyncUtility.SyncFactDatabaseAssets();
        }

        private class MaterialSet
        {
            public Material Floor, Wall, Ceiling, Trim;
            public Material Desk, Chair, LampBulb, MonitorBody, MonitorScreen;
            public Material RadioBody, LedIndicator;
            public Material MedbayDoor, CrewDoor, Locker;
            public Material WindowGlass, MarsGround, MarsRock, MarsSky, MarsSun, MarsDust;
        }

        private static MaterialSet CreateMaterials()
        {
            var skyTex = CreateOrLoadMarsSkyTexture();
            var dustTex = CreateOrLoadSoftDustTexture();

            var m = new MaterialSet
            {
                Floor = CreateUrpLitMat("M_Room_Floor", new Color(0.22f, 0.23f, 0.26f, 1f), 0.1f),
                Wall = CreateUrpLitMat("M_Room_Wall", new Color(0.38f, 0.40f, 0.44f, 1f), 0.08f),
                Ceiling = CreateUrpLitMat("M_Room_Ceiling", new Color(0.26f, 0.28f, 0.31f, 1f), 0.06f),
                Trim = CreateUrpLitMat("M_Room_Trim", new Color(0.18f, 0.19f, 0.22f, 1f), 0.15f),
                Desk = CreateUrpLitMat("M_Desk", new Color(0.29f, 0.31f, 0.34f, 1f), 0.12f),
                Chair = CreateUrpLitMat("M_Chair", new Color(0.16f, 0.18f, 0.22f, 1f), 0.08f),
                LampBulb = CreateUrpEmissiveMat("M_Desk_LampBulb", new Color(1.00f, 0.90f, 0.68f, 1f), 1.6f),
                MonitorBody = CreateUrpLitMat("M_Monitor_Body", new Color(0.24f, 0.25f, 0.28f, 1f), 0.15f),
                MonitorScreen = CreateUrpEmissiveMat("M_Monitor_Screen", new Color(0.05f, 0.18f, 0.24f, 1f), 1.0f),
                RadioBody = CreateUrpLitMat("M_Radio_Body", new Color(0.20f, 0.23f, 0.20f, 1f), 0.12f),
                LedIndicator = CreateUrpEmissiveMat("M_LED_Indicator", new Color(0.18f, 0.88f, 0.36f, 1f), 1.8f),
                MedbayDoor = CreateUrpLitMat("M_Medbay_Door", new Color(0.46f, 0.48f, 0.50f, 1f), 0.08f),
                CrewDoor = CreateUrpLitMat("M_Crew_Door", new Color(0.34f, 0.36f, 0.39f, 1f), 0.08f),
                Locker = CreateUrpLitMat("M_Locker", new Color(0.28f, 0.33f, 0.36f, 1f), 0.12f),
                WindowGlass = CreateUrpGlassMat("M_Window_Glass", new Color(0.55f, 0.72f, 0.85f, 0.12f)),
                MarsGround = CreateUrpEmissiveMat("M_Mars_Ground", new Color(0.68f, 0.32f, 0.18f, 1f), 0.42f),
                MarsRock = CreateUrpEmissiveMat("M_Mars_Rock", new Color(0.52f, 0.24f, 0.14f, 1f), 0.35f),
                MarsSky = CreateUrpUnlitTexturedMat("M_Mars_Sky", skyTex, Color.white),
                MarsSun = CreateUrpUnlitMat("M_Mars_Sun", new Color(1.00f, 0.92f, 0.78f, 1f)),
                MarsDust = CreateUrpParticleMat("M_Mars_DustParticle", dustTex)
            };
            AssetDatabase.SaveAssets();
            return m;
        }

        private static Texture2D CreateOrLoadMarsSkyTexture()
        {
            string path = $"{MatDir}/T_MarsSkyGradient.png";
            var tex = new Texture2D(16, 128, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color horizon = new Color(0.84f, 0.44f, 0.26f, 1f);
            Color midSky = new Color(0.72f, 0.41f, 0.39f, 1f);
            Color zenith = new Color(0.20f, 0.16f, 0.24f, 1f);
            for (int y = 0; y < tex.height; y++)
            {
                float t = y / (float)(tex.height - 1);
                Color c = t < 0.42f
                    ? Color.Lerp(horizon, midSky, t / 0.42f)
                    : Color.Lerp(midSky, zenith, (t - 0.42f) / 0.58f);
                for (int x = 0; x < tex.width; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D CreateOrLoadSoftDustTexture()
        {
            string path = $"{MatDir}/T_MarsDustDot.png";
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - 15.5f) / 15.5f;
                    float dy = (y - 15.5f) / 15.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateUrpLitMat(string name, Color color, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUrpEmissiveMat(string name, Color color, float emissionMultiplier)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emissionMultiplier);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUrpUnlitMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUrpUnlitTexturedMat(string name, Texture2D tex, Color tint)
        {
            var mat = CreateUrpUnlitMat(name, tint);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUrpGlassMat(string name, Color tint)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetFloat("_Surface", 1f); // Transparent in URP
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", tint);
            mat.SetColor("_Color", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUrpParticleMat(string name, Texture2D dotTex)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.SetTexture("_BaseMap", dotTex);
            mat.SetTexture("_MainTex", dotTex);
            mat.SetColor("_BaseColor", new Color(0.92f, 0.55f, 0.36f, 0.55f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private class PrefabSet
        {
            public GameObject Floor, Ceiling, Wall;
            public GameObject Desk, Chair, Monitor, Radio;
            public GameObject WindowFrame, MarsExterior;
            public GameObject StatusLightsPanel, OverheadAlarmLight;
            public GameObject MedbayDoor, CrewDoor, Locker;
        }

        private static PrefabSet CreateAllRoomPrefabs(MaterialSet mats)
        {
            return new PrefabSet
            {
                Floor = BuildFloorPrefab(mats),
                Ceiling = BuildCeilingPrefab(mats),
                Wall = BuildWallPrefab(mats),
                Desk = BuildDeskPrefab(mats),
                Chair = BuildChairPrefab(mats),
                Monitor = BuildMonitorPrefab(mats),
                Radio = BuildRadioPrefab(mats),
                WindowFrame = BuildWindowFramePrefab(mats),
                MarsExterior = BuildMarsExteriorPrefab(mats),
                StatusLightsPanel = BuildStatusLightsPanelPrefab(mats),
                OverheadAlarmLight = BuildOverheadAlarmPrefab(mats),
                MedbayDoor = BuildMedbayDoorPrefab(mats),
                CrewDoor = BuildCrewDoorPrefab(mats),
                Locker = BuildLockerPrefab(mats)
            };
        }

        private static GameObject BuildFloorPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Room_Floor");
            MarkStatic(root);
            AddPrimitive(root, PrimitiveType.Cube, "FloorPlate", new Vector3(0f, -0.05f, 0.35f), Vector3.zero, new Vector3(6.2f, 0.1f, 5.2f), m.Floor, removeCollider: true, markStatic: true);
            for (int i = -1; i <= 1; i++)
                AddPrimitive(root, PrimitiveType.Cube, $"FloorRib_{i + 1}", new Vector3(i * 1.6f, 0.004f, 0.35f), Vector3.zero, new Vector3(0.08f, 0.008f, 5.0f), m.Trim, removeCollider: true, markStatic: true);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.05f, 0.35f);
            col.size = new Vector3(6.2f, 0.1f, 5.2f);
            return SavePrefab(root, "PF_Room_Floor");
        }

        private static GameObject BuildCeilingPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Room_Ceiling");
            MarkStatic(root);
            AddPrimitive(root, PrimitiveType.Cube, "CeilingPlate", new Vector3(0f, 3.05f, 0.35f), Vector3.zero, new Vector3(6.2f, 0.1f, 5.2f), m.Ceiling, removeCollider: false, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "Beam_Front", new Vector3(0f, 2.94f, 1.85f), Vector3.zero, new Vector3(6.1f, 0.12f, 0.14f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "Beam_Mid", new Vector3(0f, 2.94f, 0.15f), Vector3.zero, new Vector3(6.1f, 0.12f, 0.14f), m.Trim, removeCollider: true, markStatic: true);
            return SavePrefab(root, "PF_Room_Ceiling");
        }

        private static GameObject BuildWallPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Room_Wall");
            MarkStatic(root);
            AddPrimitive(root, PrimitiveType.Cube, "WallSegment", Vector3.zero, Vector3.zero, Vector3.one, m.Wall, removeCollider: false, markStatic: true);
            return SavePrefab(root, "PF_Room_Wall");
        }

        private static GameObject BuildDeskPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Desk");
            AddPrimitive(root, PrimitiveType.Cube, "DeskTop", new Vector3(0f, 0.76f, 0f), Vector3.zero, new Vector3(1.95f, 0.06f, 0.84f), m.Desk, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "LegLeft", new Vector3(-0.88f, 0.37f, 0f), Vector3.zero, new Vector3(0.08f, 0.74f, 0.76f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "LegRight", new Vector3(0.88f, 0.37f, 0f), Vector3.zero, new Vector3(0.08f, 0.74f, 0.76f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "FrontModesty", new Vector3(0f, 0.44f, 0.34f), Vector3.zero, new Vector3(1.70f, 0.56f, 0.05f), m.Trim, removeCollider: true);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.39f, 0f);
            col.size = new Vector3(1.95f, 0.78f, 0.84f);

            // Warm desk lamp on left side
            var lamp = new GameObject("DeskLamp");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(-0.78f, 0.79f, 0.14f);
            AddPrimitive(lamp, PrimitiveType.Cylinder, "LampBase", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.16f, 0.02f, 0.16f), m.Trim, removeCollider: true);
            AddPrimitive(lamp, PrimitiveType.Cylinder, "LampStem", new Vector3(0f, 0.22f, 0f), new Vector3(12f, 0f, -8f), new Vector3(0.03f, 0.22f, 0.03f), m.Trim, removeCollider: true);
            AddPrimitive(lamp, PrimitiveType.Cube, "LampShade", new Vector3(0.05f, 0.42f, -0.05f), new Vector3(24f, 20f, 0f), new Vector3(0.16f, 0.08f, 0.20f), m.MonitorBody, removeCollider: true);
            AddPrimitive(lamp, PrimitiveType.Sphere, "LampBulb", new Vector3(0.05f, 0.37f, -0.05f), Vector3.zero, new Vector3(0.065f, 0.065f, 0.065f), m.LampBulb, removeCollider: true);
            return SavePrefab(root, "PF_Desk");
        }

        private static GameObject BuildChairPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Chair");
            AddPrimitive(root, PrimitiveType.Cylinder, "Pedestal", new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.08f, 0.22f, 0.08f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cylinder, "BaseFoot", new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.55f, 0.03f, 0.55f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.46f, 0f), Vector3.zero, new Vector3(0.54f, 0.08f, 0.52f), m.Chair, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "Backrest", new Vector3(0f, 0.80f, -0.23f), new Vector3(-6f, 0f, 0f), new Vector3(0.50f, 0.62f, 0.08f), m.Chair, removeCollider: true);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.48f, -0.02f);
            col.size = new Vector3(0.56f, 0.96f, 0.54f);
            return SavePrefab(root, "PF_Chair");
        }

        private static GameObject BuildMonitorPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Monitor");
            AddPrimitive(root, PrimitiveType.Cube, "MonitorStand", new Vector3(0f, 0.09f, 0.05f), Vector3.zero, new Vector3(0.22f, 0.18f, 0.16f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "MonitorHousing", new Vector3(0f, 0.34f, 0.04f), new Vector3(-4f, 0f, 0f), new Vector3(0.68f, 0.46f, 0.24f), m.MonitorBody, removeCollider: true);

            // Screen slot specifically named Monitor_Screen
            var screen = AddPrimitive(root, PrimitiveType.Quad, "Monitor_Screen", new Vector3(0f, 0.34f, -0.085f), new Vector3(-4f, 0f, 0f), new Vector3(0.58f, 0.38f, 1f), m.MonitorScreen, removeCollider: true);

            // World-space crisp terminal preview text (sibling of screen so scale stays uniform)
            var txtObj = new GameObject("Monitor_WorldText");
            txtObj.transform.SetParent(root.transform, false);
            txtObj.transform.localPosition = new Vector3(0f, 0.38f, -0.095f);
            txtObj.transform.localEulerAngles = new Vector3(-4f, 0f, 0f);
            txtObj.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            var tmp = txtObj.AddComponent<TextMeshPro>();
            tmp.text = "ARES-4 TERMINAL\nSOL 1/7\n[!] EVENT PENDING";
            tmp.fontSize = 6.5f;
            tmp.color = new Color(0.25f, 0.92f, 0.48f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(10.5f, 6.5f);

            // Raycast collider + interactable
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.34f, 0f);
            col.size = new Vector3(0.74f, 0.50f, 0.32f);
            root.AddComponent<InteractableProp>().Configure(InteractablePropType.Monitor, "prompt.monitor");

            // Focus anchor in front of screen (0.48m away from screen)
            var focus = new GameObject("TerminalFocusPoint");
            focus.transform.SetParent(root.transform, false);
            focus.transform.localPosition = new Vector3(0f, 0.34f, -0.58f);
            focus.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);

            return SavePrefab(root, "PF_Monitor");
        }

        private static GameObject BuildRadioPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Radio");
            AddPrimitive(root, PrimitiveType.Cube, "RadioBody", new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.26f, 0.16f, 0.18f), m.RadioBody, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cylinder, "RadioAntenna", new Vector3(0.09f, 0.28f, 0.04f), new Vector3(0f, 0f, -6f), new Vector3(0.015f, 0.20f, 0.015f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Cube, "RadioDial", new Vector3(-0.02f, 0.08f, -0.092f), Vector3.zero, new Vector3(0.14f, 0.06f, 0.01f), m.Trim, removeCollider: true);
            AddPrimitive(root, PrimitiveType.Sphere, "Radio_LED", new Vector3(0.08f, 0.10f, -0.095f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), m.LedIndicator, removeCollider: true);

            var ptGo = new GameObject("Radio_PointLight");
            ptGo.transform.SetParent(root.transform, false);
            ptGo.transform.localPosition = new Vector3(0.08f, 0.11f, -0.14f);
            var pt = ptGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(0.25f, 0.95f, 0.55f, 1f);
            pt.range = 0.45f;
            pt.intensity = 0.45f;
            pt.shadows = LightShadows.None;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.12f, 0f);
            col.size = new Vector3(0.32f, 0.30f, 0.24f);
            root.AddComponent<InteractableProp>().Configure(InteractablePropType.Radio, "prompt.radio");

            return SavePrefab(root, "PF_Radio");
        }

        private static GameObject BuildWindowFramePrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Window_Frame");
            AddPrimitive(root, PrimitiveType.Cube, "FrameTop", new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(2.40f, 0.10f, 0.18f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "FrameBottom", new Vector3(0f, -0.65f, 0f), Vector3.zero, new Vector3(2.40f, 0.10f, 0.18f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "FrameLeft", new Vector3(-1.15f, 0f, 0f), Vector3.zero, new Vector3(0.10f, 1.22f, 0.18f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "FrameRight", new Vector3(1.15f, 0f, 0f), Vector3.zero, new Vector3(0.10f, 1.22f, 0.18f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "Mullion", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 1.20f, 0.12f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Quad, "WindowGlass", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(2.22f, 1.20f, 1f), m.WindowGlass, removeCollider: true);

            var col = root.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(2.35f, 1.30f, 0.22f);
            root.AddComponent<InteractableProp>().Configure(InteractablePropType.Window, "prompt.window", "room.window.fact");

            return SavePrefab(root, "PF_Window_Frame");
        }

        private static GameObject BuildMarsExteriorPrefab(MaterialSet m)
        {
            // Rock prefab
            var rockRoot = new GameObject("PF_Mars_Rock");
            AddPrimitive(rockRoot, PrimitiveType.Cube, "RockCore", new Vector3(0f, 0.25f, 0f), new Vector3(12f, 28f, -8f), new Vector3(1.1f, 0.55f, 0.85f), m.MarsRock, removeCollider: true, markStatic: true);
            AddPrimitive(rockRoot, PrimitiveType.Cube, "RockChip", new Vector3(0.55f, 0.14f, -0.25f), new Vector3(-16f, 45f, 10f), new Vector3(0.55f, 0.32f, 0.48f), m.MarsRock, removeCollider: true, markStatic: true);
            var rockPrefab = SavePrefab(rockRoot, "PF_Mars_Rock");

            var root = new GameObject("PF_Mars_Exterior");
            // Ground plane gently sloped up toward horizon so red soil is clearly visible through window
            AddPrimitive(root, PrimitiveType.Plane, "Mars_Ground", new Vector3(0f, 0.55f, 22f), new Vector3(-2.5f, 0f, 0f), new Vector3(8f, 1f, 6f), m.MarsGround, removeCollider: true, markStatic: true);
            // Sky gradient quad at horizon
            AddPrimitive(root, PrimitiveType.Quad, "Mars_SkyBackdrop", new Vector3(0f, 14.5f, 48f), Vector3.zero, new Vector3(95f, 34f, 1f), m.MarsSky, removeCollider: true);
            // Tiny sun disc in upper-right pane: "the sun is far away"
            AddPrimitive(root, PrimitiveType.Sphere, "Mars_TinySun", new Vector3(4.2f, 10.2f, 40f), Vector3.zero, new Vector3(0.55f, 0.55f, 0.55f), m.MarsSun, removeCollider: true);

            // Distant crater ridge slabs
            AddPrimitive(root, PrimitiveType.Cube, "FarRidge_Left", new Vector3(-11f, 2.2f, 32f), new Vector3(0f, 14f, 4f), new Vector3(18f, 3.6f, 3f), m.MarsRock, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "FarRidge_Right", new Vector3(12f, 2.4f, 34f), new Vector3(0f, -18f, -5f), new Vector3(19f, 4.0f, 3f), m.MarsRock, removeCollider: true, markStatic: true);

            // Place rock primitives outside window visible past monitor sides
            Vector3[] rockPositions =
            {
                new Vector3(-1.9f, 0.15f, 5.2f),
                new Vector3(1.8f, 0.15f, 5.8f),
                new Vector3(-4.2f, 0.45f, 10.5f),
                new Vector3(4.5f, 0.50f, 11.8f),
                new Vector3(2.9f, 0.70f, 15.0f)
            };
            float[] rockScales = { 0.85f, 1.15f, 1.45f, 1.30f, 1.75f };
            for (int i = 0; i < rockPositions.Length; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, root.transform);
                inst.name = $"MarsRock_{i}";
                inst.transform.localPosition = rockPositions[i];
                inst.transform.localRotation = Quaternion.Euler(0f, i * 57f, 0f);
                inst.transform.localScale = Vector3.one * rockScales[i];
            }

            // Drifting dust ParticleSystem outside the window
            var dustGo = new GameObject("MarsDustParticles");
            dustGo.transform.SetParent(root.transform, false);
            dustGo.transform.localPosition = new Vector3(-5.5f, 1.4f, 4.4f);
            dustGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // Emit left-to-right (+X) across window

            var ps = dustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 4.2f;
            main.startSpeed = 2.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            main.startColor = new Color(0.84f, 0.52f, 0.34f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.8f, 2.2f, 0.2f);

            var psRend = dustGo.GetComponent<ParticleSystemRenderer>();
            psRend.material = m.MarsDust;

            // External Martian sunlight so rocks & ground stay lit even in interior blackout
            var extLightGo = new GameObject("MarsExteriorLight");
            extLightGo.transform.SetParent(root.transform, false);
            extLightGo.transform.localPosition = new Vector3(-2f, 6f, 10f);
            var extLight = extLightGo.AddComponent<Light>();
            extLight.type = LightType.Point;
            extLight.color = new Color(1.0f, 0.68f, 0.48f, 1f);
            extLight.range = 32f;
            extLight.intensity = 2.4f;
            extLight.shadows = LightShadows.None;

            return SavePrefab(root, "PF_Mars_Exterior");
        }

        private static GameObject BuildStatusLightsPanelPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_StatusLightsPanel");
            AddPrimitive(root, PrimitiveType.Cube, "PanelBacking", Vector3.zero, Vector3.zero, new Vector3(0.95f, 0.42f, 0.06f), m.Trim, removeCollider: true, markStatic: true);

            string[] names = { "Power", "Oxygen", "Water", "Food", "Shielding" };
            string[] defaultTags = { "PWR", "O2", "H2O", "FOOD", "SHLD" };

            for (int i = 0; i < 5; i++)
            {
                float x = -0.36f + i * 0.18f;
                AddPrimitive(root, PrimitiveType.Sphere, $"StatusLight_{names[i]}", new Vector3(x, 0.08f, -0.04f), Vector3.zero, new Vector3(0.075f, 0.075f, 0.045f), m.LedIndicator, removeCollider: true);

                var lblGo = new GameObject($"StatusLabel_{names[i]}");
                lblGo.transform.SetParent(root.transform, false);
                lblGo.transform.localPosition = new Vector3(x, -0.08f, -0.036f);
                lblGo.transform.localScale = new Vector3(0.042f, 0.042f, 1f);
                var tmp = lblGo.AddComponent<TextMeshPro>();
                tmp.text = $"{defaultTags[i]}\n--";
                tmp.fontSize = 6.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.2f, 0.9f, 0.4f, 1f);
                tmp.rectTransform.sizeDelta = new Vector2(4.0f, 3.0f);
            }
            return SavePrefab(root, "PF_StatusLightsPanel");
        }

        private static GameObject BuildOverheadAlarmPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_OverheadAlarmLight");
            AddPrimitive(root, PrimitiveType.Cylinder, "AlarmBase", new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Sphere, "AlarmBulb", new Vector3(0f, -0.02f, 0f), Vector3.zero, new Vector3(0.14f, 0.10f, 0.14f), m.LedIndicator, removeCollider: true);

            var ptGo = new GameObject("Alarm_PointLight");
            ptGo.transform.SetParent(root.transform, false);
            ptGo.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            var pt = ptGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(1.0f, 0.16f, 0.16f, 1f);
            pt.range = 6.5f;
            pt.intensity = 2.4f;
            pt.shadows = LightShadows.None;
            pt.enabled = false;

            return SavePrefab(root, "PF_OverheadAlarmLight");
        }

        private static GameObject BuildMedbayDoorPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_MedbayDoor");
            // Bulkhead frame & slab (faces local -Z)
            AddPrimitive(root, PrimitiveType.Cube, "DoorFrame", new Vector3(0f, 1.10f, 0.04f), Vector3.zero, new Vector3(1.18f, 2.20f, 0.16f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "DoorPanel", new Vector3(0f, 1.08f, -0.01f), Vector3.zero, new Vector3(0.96f, 2.04f, 0.08f), m.MedbayDoor, removeCollider: true, markStatic: true);
            // Medbay observation window pane (emissive backlit)
            AddPrimitive(root, PrimitiveType.Quad, "MedbayWindow", new Vector3(0f, 1.48f, -0.055f), Vector3.zero, new Vector3(0.44f, 0.30f, 1f), m.LedIndicator, removeCollider: true);

            // Sign: MEDBAY - CMDR VALE (uniform text scale as child of root)
            AddPrimitive(root, PrimitiveType.Cube, "MedbaySignPlate", new Vector3(0f, 1.82f, -0.06f), Vector3.zero, new Vector3(0.84f, 0.16f, 0.02f), m.Trim, removeCollider: true);
            var signGo = new GameObject("MedbaySignText");
            signGo.transform.SetParent(root.transform, false);
            signGo.transform.localPosition = new Vector3(0f, 1.82f, -0.075f);
            signGo.transform.localScale = new Vector3(0.055f, 0.055f, 1f);
            var tmp = signGo.AddComponent<TextMeshPro>();
            tmp.text = Localization.Get("room.medbay.sign");
            tmp.fontSize = 7.2f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.95f, 0.78f, 0.36f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(14f, 2.5f);

            // Soft flickering point light
            var medLightGo = new GameObject("Medbay_PointLight");
            medLightGo.transform.SetParent(root.transform, false);
            medLightGo.transform.localPosition = new Vector3(0f, 1.48f, -0.35f);
            var pt = medLightGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(0.55f, 0.85f, 0.92f, 1f);
            pt.range = 2.6f;
            pt.intensity = 1.6f;
            pt.shadows = LightShadows.None;

            // Raycast collider + interactable
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.12f, 0f);
            col.size = new Vector3(1.10f, 2.15f, 0.22f);
            root.AddComponent<InteractableProp>().Configure(InteractablePropType.MedbayDoor, "prompt.medbay", "prompt.medbay");

            return SavePrefab(root, "PF_MedbayDoor");
        }

        private static GameObject BuildCrewDoorPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_CrewDoor");
            AddPrimitive(root, PrimitiveType.Cube, "DoorFrame", new Vector3(0f, 1.10f, 0.04f), Vector3.zero, new Vector3(1.14f, 2.20f, 0.14f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "DoorPanel", new Vector3(0f, 1.08f, -0.01f), Vector3.zero, new Vector3(0.92f, 2.04f, 0.08f), m.CrewDoor, removeCollider: true, markStatic: true);
            var signGo = new GameObject("CrewSignText");
            signGo.transform.SetParent(root.transform, false);
            signGo.transform.localPosition = new Vector3(0f, 1.72f, -0.06f);
            signGo.transform.localScale = new Vector3(0.06f, 0.06f, 1f);
            var tmp = signGo.AddComponent<TextMeshPro>();
            tmp.text = Localization.Get("room.crewdoor.label");
            tmp.fontSize = 6f;
            tmp.color = new Color(0.75f, 0.80f, 0.85f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(12f, 2f);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.10f, 0f);
            col.size = new Vector3(1.14f, 2.20f, 0.18f);
            return SavePrefab(root, "PF_CrewDoor");
        }

        private static GameObject BuildLockerPrefab(MaterialSet m)
        {
            var root = new GameObject("PF_Locker");
            AddPrimitive(root, PrimitiveType.Cube, "LockerBody", new Vector3(0f, 0.95f, 0f), Vector3.zero, new Vector3(0.82f, 1.90f, 0.50f), m.Locker, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "LockerSeam", new Vector3(0f, 0.95f, -0.255f), Vector3.zero, new Vector3(0.02f, 1.78f, 0.01f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "LockerHandleL", new Vector3(-0.08f, 0.98f, -0.265f), Vector3.zero, new Vector3(0.03f, 0.14f, 0.02f), m.Trim, removeCollider: true, markStatic: true);
            AddPrimitive(root, PrimitiveType.Cube, "LockerHandleR", new Vector3(0.08f, 0.98f, -0.265f), Vector3.zero, new Vector3(0.03f, 0.14f, 0.02f), m.Trim, removeCollider: true, markStatic: true);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.size = new Vector3(0.82f, 1.90f, 0.50f);
            return SavePrefab(root, "PF_Locker");
        }

        private static void AssembleOutpostScene(
            PrefabSet p,
            FactDatabase factDb,
            FontTheme fontTheme,
            IntroSequenceData introData,
            PSXSettings psxSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Render settings: dim interior ambient, sharp Mars horizon outside without washing out distant sun
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.15f, 1f);
            RenderSettings.fog = false;

            // --- COMMAND MODULE ROOM HIERARCHY (~6m x 4.9m walkable interior, >=1.0m clear walk around desk) ---
            var roomRoot = new GameObject("CommandModuleRoom");
            PrefabUtility.InstantiatePrefab(p.Floor, roomRoot.transform);
            PrefabUtility.InstantiatePrefab(p.Ceiling, roomRoot.transform);

            // Front/Window wall (Z = +2.85, inner face Z = +2.79)
            PlaceWall(p.Wall, roomRoot.transform, "Wall_FrontBottom", new Vector3(0f, 0.45f, 2.85f), new Vector3(6.2f, 0.90f, 0.12f));
            PlaceWall(p.Wall, roomRoot.transform, "Wall_FrontTop", new Vector3(0f, 2.60f, 2.85f), new Vector3(6.2f, 0.80f, 0.12f));
            PlaceWall(p.Wall, roomRoot.transform, "Wall_FrontLeft", new Vector3(-2.15f, 1.55f, 2.85f), new Vector3(1.90f, 1.30f, 0.12f));
            PlaceWall(p.Wall, roomRoot.transform, "Wall_FrontRight", new Vector3(2.15f, 1.55f, 2.85f), new Vector3(1.90f, 1.30f, 0.12f));

            // Left, Right & Rear walls (X = +/-3.00 inner, Z = -2.10 inner)
            PlaceWall(p.Wall, roomRoot.transform, "Wall_Left", new Vector3(-3.06f, 1.50f, 0.35f), new Vector3(0.12f, 3.0f, 5.2f));
            PlaceWall(p.Wall, roomRoot.transform, "Wall_Right", new Vector3(3.06f, 1.50f, 0.35f), new Vector3(0.12f, 3.0f, 5.2f));
            PlaceWall(p.Wall, roomRoot.transform, "Wall_Rear", new Vector3(0f, 1.50f, -2.16f), new Vector3(6.2f, 3.0f, 0.12f));

            // Window + Mars Exterior
            var win = (GameObject)PrefabUtility.InstantiatePrefab(p.WindowFrame, roomRoot.transform);
            win.transform.position = new Vector3(0f, 1.55f, 2.81f);
            var ext = (GameObject)PrefabUtility.InstantiatePrefab(p.MarsExterior, roomRoot.transform);
            ext.transform.position = new Vector3(0f, 0f, 0.25f);

            // Chair + Desk + Monitor + Radio (1.07m walk space in front of desk, >=1.55m on both sides)
            var chair = (GameObject)PrefabUtility.InstantiatePrefab(p.Chair, roomRoot.transform);
            chair.transform.position = new Vector3(0f, 0f, 0.55f);

            var desk = (GameObject)PrefabUtility.InstantiatePrefab(p.Desk, roomRoot.transform);
            desk.transform.position = new Vector3(0f, 0f, 1.30f);

            var monitor = (GameObject)PrefabUtility.InstantiatePrefab(p.Monitor, roomRoot.transform);
            monitor.transform.position = new Vector3(0f, 0.79f, 1.38f);

            var radio = (GameObject)PrefabUtility.InstantiatePrefab(p.Radio, roomRoot.transform);
            radio.transform.position = new Vector3(0.68f, 0.79f, 1.18f);
            radio.transform.rotation = Quaternion.Euler(0f, 26f, 0f);

            // Wall Status Lights Panel + Overhead Alarm
            var statusPanel = (GameObject)PrefabUtility.InstantiatePrefab(p.StatusLightsPanel, roomRoot.transform);
            statusPanel.transform.position = new Vector3(-1.82f, 1.65f, 2.75f);
            statusPanel.transform.rotation = Quaternion.identity;

            var overheadAlarm = (GameObject)PrefabUtility.InstantiatePrefab(p.OverheadAlarmLight, roomRoot.transform);
            overheadAlarm.transform.position = new Vector3(0f, 2.94f, 1.30f);

            // Medbay Door flush against Left Wall (local -Z faces +X into the room)
            var medDoor = (GameObject)PrefabUtility.InstantiatePrefab(p.MedbayDoor, roomRoot.transform);
            medDoor.transform.position = new Vector3(-2.92f, 0f, 1.35f);
            medDoor.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            // Crew Door + Locker flush against Right Wall (local -Z faces -X into the room)
            var crewDoor = (GameObject)PrefabUtility.InstantiatePrefab(p.CrewDoor, roomRoot.transform);
            crewDoor.transform.position = new Vector3(2.92f, 0f, 1.45f);
            crewDoor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var locker = (GameObject)PrefabUtility.InstantiatePrefab(p.Locker, roomRoot.transform);
            locker.transform.position = new Vector3(2.72f, 0f, -0.35f);
            locker.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // --- INTERIOR LIGHTING (BAKED LOOK, NO REAL-TIME SHADOWS) ---
            var lightsRoot = new GameObject("RoomLighting");
            var deskLampLight = CreatePointLight(lightsRoot.transform, "Light_DeskLampWarm", new Vector3(-0.72f, 1.24f, 1.43f), new Color(1.0f, 0.78f, 0.48f), 1.55f, 5.5f);
            var windowCoolLight = CreatePointLight(lightsRoot.transform, "Light_WindowCoolFill", new Vector3(0f, 1.58f, 2.40f), new Color(0.60f, 0.75f, 0.96f), 1.45f, 6.0f);
            var cabinFillLight = CreatePointLight(lightsRoot.transform, "Light_CabinSoftFill", new Vector3(0f, 2.65f, 0.35f), new Color(0.75f, 0.80f, 0.88f), 1.05f, 7.2f);
            var emergencyRed = CreatePointLight(lightsRoot.transform, "Light_BlackoutEmergencyRed", new Vector3(0f, 2.65f, 0.85f), new Color(0.95f, 0.16f, 0.12f), 1.6f, 8.0f);
            emergencyRed.enabled = false;

            // --- PART 2 UI ART / GLOSSARY + PART 3 EXPLORABLE HABITAT & 60x60 MARS SURFACE ---
            var theme = Resources.Load<UITheme>("TerminalTheme");
            OutpostWorldExpansionBuilder.EnsureFriendlyUiAssetsAndTerminalPrefab(theme, TerminalPrefabPath);
            TMP_FontAsset hudFont = theme != null ? theme.ResolveFont() : (TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null);
            var expanded = OutpostWorldExpansionBuilder.BuildExpandedWorld(
                roomRoot,
                lightsRoot.transform,
                p.Wall,
                p.StatusLightsPanel,
                desk,
                hudFont);

            // --- PLAYER SPAWN & MODULAR FIRST-PERSON CONTROLLER (1.5m BEHIND CHAIR) ---
            var spawnGo = new GameObject("PlayerSpawn");
            spawnGo.transform.position = new Vector3(0f, 1.0f, -0.95f);
            spawnGo.transform.rotation = Quaternion.identity;

            var fpsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModularFpsPrefabPath);
            var fpsInstance = (GameObject)PrefabUtility.InstantiatePrefab(fpsPrefab);
            fpsInstance.name = "FirstPersonController";
            fpsInstance.transform.SetPositionAndRotation(spawnGo.transform.position, spawnGo.transform.rotation);

            var fpsMono = fpsInstance.GetComponent("FirstPersonController") as MonoBehaviour;
            var fpsRb = fpsInstance.GetComponent<Rigidbody>();
            var jointTrans = fpsInstance.transform.Find("Joint");
            var playerCam = fpsInstance.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
                playerCam.nearClipPlane = 0.05f;
                playerCam.allowMSAA = false;
                playerCam.clearFlags = CameraClearFlags.SolidColor;
                playerCam.backgroundColor = new Color(0.16f, 0.13f, 0.18f, 1f);
            }
            var builtInHud = fpsInstance.GetComponentInChildren<Canvas>(true);

            // Guarantee strictly 1 Camera and 1 AudioListener in Outpost scene
            var allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            var allListeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            if (allCams.Length != 1 || allListeners.Length != 1)
                Debug.LogWarning($"[OutpostM3Builder] Expected 1 Camera / 1 AudioListener, found {allCams.Length} Cameras / {allListeners.Length} AudioListeners.");

            // --- OPTIONAL WORLD VIEW OVERLAY CANVAS (Inactive by default; standard native URP rendering) ---
            var worldViewGo = new GameObject("WorldView", typeof(Canvas), typeof(CanvasScaler));
            var worldViewCanvas = worldViewGo.GetComponent<Canvas>();
            worldViewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            worldViewCanvas.sortingOrder = -100;
            worldViewGo.SetActive(false);

            var worldBgGo = new GameObject("WorldViewBackdrop", typeof(RectTransform), typeof(Image));
            worldBgGo.transform.SetParent(worldViewGo.transform, false);
            StretchRect(worldBgGo.GetComponent<RectTransform>());
            var bgImg = worldBgGo.GetComponent<Image>();
            bgImg.color = Color.black;
            bgImg.raycastTarget = false;

            var worldImgGo = new GameObject("WorldViewRawImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            worldImgGo.transform.SetParent(worldViewGo.transform, false);
            StretchRect(worldImgGo.GetComponent<RectTransform>());
            var worldViewRawImage = worldImgGo.GetComponent<RawImage>();
            worldViewRawImage.color = Color.white;
            worldViewRawImage.material = null;
            worldViewRawImage.raycastTarget = false;
            var fitter = worldImgGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            // --- TERMINAL UI CANVAS (PF_TerminalRoot) ---
            var termCanvasGo = new GameObject("TerminalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var termCanvas = termCanvasGo.GetComponent<Canvas>();
            termCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            termCanvas.sortingOrder = 10;
            var termScaler = termCanvasGo.GetComponent<CanvasScaler>();
            termScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            termScaler.referenceResolution = new Vector2(1920f, 1080f);
            termScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            termScaler.matchWidthOrHeight = 0.5f;

            var termPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPrefabPath);
            var termInst = (GameObject)PrefabUtility.InstantiatePrefab(termPrefab, termCanvasGo.transform);
            termInst.name = "TerminalRoot";
            var terminalController = termInst.GetComponent<TerminalController>();

            // --- ROOM HUD CANVAS (Crosshair, Prompt, Fact Banner) ---
            var hudCanvasGo = new GameObject("RoomHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            var hudCanvas = hudCanvasGo.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 5;
            var hudScaler = hudCanvasGo.GetComponent<CanvasScaler>();
            hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hudScaler.referenceResolution = new Vector2(1920f, 1080f);
            hudScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            hudScaler.matchWidthOrHeight = 0.5f;

            var hudRoot = new GameObject("RoomHudRoot", typeof(RectTransform));
            hudRoot.transform.SetParent(hudCanvasGo.transform, false);
            StretchRect(hudRoot.GetComponent<RectTransform>());

            // Crosshair outline + center dot
            var dotBorder = new GameObject("CrosshairOutline", typeof(RectTransform), typeof(Image));
            dotBorder.transform.SetParent(hudRoot.transform, false);
            dotBorder.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            CenterSize(dotBorder.GetComponent<RectTransform>(), 10f, 10f);

            var dotInner = new GameObject("CrosshairDot", typeof(RectTransform), typeof(Image));
            dotInner.transform.SetParent(dotBorder.transform, false);
            dotInner.GetComponent<Image>().color = new Color(0.95f, 0.98f, 1f, 0.95f);
            CenterSize(dotInner.GetComponent<RectTransform>(), 6f, 6f);

            // Resolve font from updated friendly TerminalTheme above
            if (hudFont == null && TMP_Settings.instance != null)
                hudFont = TMP_Settings.defaultFontAsset;

            // Hover Prompt (sits cleanly below center crosshair and Monitor text)
            var promptGo = new GameObject("HoverPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            promptGo.transform.SetParent(hudRoot.transform, false);
            var promptRect = promptGo.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.25f, 0.25f);
            promptRect.anchorMax = new Vector2(0.75f, 0.33f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            var promptTmp = promptGo.GetComponent<TextMeshProUGUI>();
            if (hudFont != null) promptTmp.font = hudFont;
            promptTmp.fontSize = 28f;
            promptTmp.fontStyle = FontStyles.Bold;
            promptTmp.alignment = TextAlignmentOptions.Center;
            promptTmp.color = new Color(1f, 0.85f, 0.42f, 1f);
            promptTmp.text = string.Empty;

            // Fact / Info Banner at bottom
            var bannerBg = new GameObject("FactBannerBox", typeof(RectTransform), typeof(Image));
            bannerBg.transform.SetParent(hudRoot.transform, false);
            var bannerRect = bannerBg.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.18f, 0.08f);
            bannerRect.anchorMax = new Vector2(0.82f, 0.21f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            bannerBg.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.92f);

            var bannerTxtGo = new GameObject("FactBannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bannerTxtGo.transform.SetParent(bannerBg.transform, false);
            var btRect = bannerTxtGo.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = new Vector2(20f, 10f);
            btRect.offsetMax = new Vector2(-20f, -10f);
            var factTmp = bannerTxtGo.GetComponent<TextMeshProUGUI>();
            if (hudFont != null) factTmp.font = hudFont;
            factTmp.fontSize = 24f;
            factTmp.alignment = TextAlignmentOptions.Center;
            factTmp.color = new Color(0.92f, 0.96f, 0.98f, 1f);
            bannerBg.SetActive(false);

            // --- EVENT SYSTEM (New Input System) ---
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // --- CONTROLLERS & GAME BOOTSTRAP ---
            var ctrlGo = new GameObject("OutpostControllers");
            var acc = ctrlGo.AddComponent<AccessibilitySettings>();
            var audioMgr = ctrlGo.AddComponent<AudioManager>();
            var binder = ctrlGo.AddComponent<RoomStateBinder>();
            var playerGate = ctrlGo.AddComponent<PlayerGate>();
            var fpCtrl = ctrlGo.AddComponent<FirstPersonRoomController>();
            var flowCtrl = ctrlGo.AddComponent<MissionFlowController>();
            var psxCtrl = ctrlGo.AddComponent<PSXRenderController>();
            var bootstrap = ctrlGo.AddComponent<GameBootstrap>();

            psxCtrl.ConfigureReferences(
                psxSettings,
                playerCam,
                acc,
                worldViewCanvas,
                worldViewRawImage,
                fitter,
                null);

            playerGate.ConfigureReferences(
                fpsMono,
                fpsRb,
                fpsInstance.transform,
                jointTrans,
                playerCam,
                builtInHud != null ? builtInHud.gameObject : null,
                audioMgr,
                acc);

            // Collect RoomStateBinder references
            var lightRends = new Renderer[5];
            var lightLabels = new TextMeshPro[5];
            string[] stockNames = { "Power", "Oxygen", "Water", "Food", "Shielding" };
            for (int i = 0; i < 5; i++)
            {
                var lTrans = statusPanel.transform.Find($"StatusLight_{stockNames[i]}");
                var tTrans = statusPanel.transform.Find($"StatusLabel_{stockNames[i]}");
                if (lTrans != null) lightRends[i] = lTrans.GetComponent<Renderer>();
                if (tTrans != null) lightLabels[i] = tTrans.GetComponent<TextMeshPro>();
            }

            var marsExtObj = roomRoot.transform.Find("PF_Mars_Exterior");
            var dustPs = marsExtObj != null ? marsExtObj.GetComponentInChildren<ParticleSystem>() : null;

            binder.ConfigureReferences(
                lightRends,
                lightLabels,
                overheadAlarm.transform.Find("AlarmBulb").GetComponent<Renderer>(),
                overheadAlarm.transform.Find("Alarm_PointLight").GetComponent<Light>(),
                dustPs,
                radio.transform.Find("Radio_LED").GetComponent<Renderer>(),
                radio.transform.Find("Radio_PointLight").GetComponent<Light>(),
                expanded.HabitatLights,
                emergencyRed,
                expanded.MedbayOverrideLight,
                expanded.MedbayOverrideWindow,
                monitor.transform.Find("Monitor_Screen").GetComponent<Renderer>(),
                monitor.GetComponentInChildren<TextMeshPro>());

            binder.ConfigurePart3Bindings(
                expanded.Airlock,
                expanded.CorridorStatusRenderers,
                expanded.CorridorStatusLabels,
                expanded.GreenhouseCropTransforms,
                expanded.GreenhouseCropRenderers,
                expanded.GreenhouseGrowLight,
                expanded.MedbayVitalRenderer,
                expanded.SolarPanelRenderers,
                expanded.SurfaceDustParticles,
                expanded.SkyDomeRenderer,
                expanded.MarsSunLight);

            var focusAnchor = monitor.transform.Find("TerminalFocusPoint");
            fpCtrl.ConfigureReferences(
                playerGate,
                spawnGo.transform,
                focusAnchor,
                terminalController,
                acc,
                audioMgr,
                hudRoot,
                promptTmp,
                bannerBg,
                factTmp);

            fpCtrl.ConfigurePart3References(
                expanded.CorridorSpawn,
                expanded.AirlockSpawn,
                expanded.SurfaceSpawn,
                expanded.Airlock,
                expanded.AllDoors);

            // Wire GameBootstrap via SerializedObject
            var so = new SerializedObject(bootstrap);
            so.FindProperty("configAsset").objectReferenceValue = Resources.Load<OutpostConfigAsset>("OutpostConfig");
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("fontTheme").objectReferenceValue = fontTheme;
            so.FindProperty("introSequenceData").objectReferenceValue = introData;
            so.FindProperty("psxSettings").objectReferenceValue = psxSettings;
            so.FindProperty("sourcesDatabase").objectReferenceValue = Resources.Load<SourcesDatabase>("SourcesDatabase");
            so.FindProperty("factDatabase").objectReferenceValue = factDb;
            so.FindProperty("terminalController").objectReferenceValue = terminalController;
            so.FindProperty("missionSeed").intValue = 101;
            so.FindProperty("startInRoomView").boolValue = true;
            so.FindProperty("enableTitleIntroFlow").boolValue = true;
            so.FindProperty("accessibilitySettings").objectReferenceValue = acc;
            so.FindProperty("audioManager").objectReferenceValue = audioMgr;
            so.FindProperty("roomStateBinder").objectReferenceValue = binder;
            so.FindProperty("playerGate").objectReferenceValue = playerGate;
            so.FindProperty("firstPersonController").objectReferenceValue = fpCtrl;
            so.FindProperty("missionFlowController").objectReferenceValue = flowCtrl;
            so.FindProperty("psxRenderController").objectReferenceValue = psxCtrl;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            if (File.Exists(TerminalScenePath))
                scenes.Add(new EditorBuildSettingsScene(TerminalScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // --- HELPERS ---
        private static void PlaceWall(GameObject wallPrefab, Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            MarkStatic(go);
        }

        private static Light CreatePointLight(Transform parent, string name, Vector3 pos, Color col, float intensity, float range)
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
            return l;
        }

        private static GameObject AddPrimitive(
            GameObject parent,
            PrimitiveType type,
            string name,
            Vector3 localPos,
            Vector3 localEuler,
            Vector3 localScale,
            Material mat,
            bool removeCollider,
            bool markStatic = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            if (removeCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            if (markStatic) MarkStatic(go);
            return go;
        }

        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static GameObject SavePrefab(GameObject root, string prefabName)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CenterSize(RectTransform rect, float w, float h)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
