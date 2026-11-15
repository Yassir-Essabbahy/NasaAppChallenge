using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Outpost.Editor
{
    /// <summary>
    /// Creates and configures the standalone MainMenu.unity scene with the live rotating
    /// MarsSphere planet in orbital space, updates Build Settings (MainMenu = index 0),
    /// and removes leftover title rigs from Outpost.unity.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string OutpostScenePath = "Assets/Scenes/Outpost.unity";
        public const string TerminalScenePath = "Assets/Scenes/Terminal.unity";
        public const string MarsTexturePath = "Assets/Models/Mars.jpg";
        public const string MarsMeshPath = "Assets/Art/PSX/M_MarsSphereMesh.asset";
        public const string MarsMatPath = "Assets/Art/PSX/M_MarsSphere.mat";
        public const string MarsPrefabPath = "Assets/Prefabs/MarsSphere.prefab";
        public const string StarMatPath = "Assets/Art/PSX/M_MenuStars.mat";
        public const string StarMeshPath = "Assets/Art/PSX/M_MenuStarfield.asset";

        [MenuItem("Outpost/Build Main Menu Scene")]
        public static void BuildMainMenuSceneMenu()
        {
            BuildMarsSpherePrefab();
            BuildAndSaveMainMenuScene();
            CleanTitleRigFromOutpostScene();
            UpdateEditorBuildSettings();
            Debug.Log("[MainMenuSceneBuilder] Built MainMenu.unity with rotating MarsSphere, updated Build Settings (index 0 = MainMenu), and cleaned Outpost.unity.");
        }

        public static GameObject BuildMarsSpherePrefab()
        {
            if (!Directory.Exists("Assets/Art/PSX")) Directory.CreateDirectory("Assets/Art/PSX");
            if (!Directory.Exists("Assets/Prefabs")) Directory.CreateDirectory("Assets/Prefabs");

            var marsTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MarsTexturePath);
            var texImp = AssetImporter.GetAtPath(MarsTexturePath) as TextureImporter;
            if (texImp != null)
            {
                bool dirty = false;
                if (texImp.filterMode != FilterMode.Point) { texImp.filterMode = FilterMode.Point; dirty = true; }
                if (texImp.mipmapEnabled) { texImp.mipmapEnabled = false; dirty = true; }
                if (texImp.maxTextureSize != 2048) { texImp.maxTextureSize = 2048; dirty = true; }
                if (texImp.wrapMode != TextureWrapMode.Repeat) { texImp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (dirty) texImp.SaveAndReimport();
                marsTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MarsTexturePath);
            }

            Mesh sphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MarsMeshPath);
            if (sphereMesh == null)
            {
                sphereMesh = CreateEquirectangularSphereMesh(48, 32, 0.5f);
                AssetDatabase.CreateAsset(sphereMesh, MarsMeshPath);
            }

            Shader psxLit = Shader.Find("Outpost/PSXLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MarsMatPath);
            if (mat == null)
            {
                mat = new Material(psxLit) { name = "M_MarsSphere" };
                AssetDatabase.CreateAsset(mat, MarsMatPath);
            }
            else
            {
                mat.shader = psxLit;
            }

            mat.SetTexture("_MainTex", marsTex);
            mat.SetColor("_Color", new Color(1f, 0.94f, 0.90f, 1f));
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(0.055f, 0.018f, 0.006f, 1f));
            if (mat.HasProperty("_EmissionStrength"))
                mat.SetFloat("_EmissionStrength", 1.0f);
            if (mat.HasProperty("_AffineAmount"))
                mat.SetFloat("_AffineAmount", 0.25f);
            if (mat.HasProperty("_FogWeight"))
                mat.SetFloat("_FogWeight", 0.0f);
            EditorUtility.SetDirty(mat);

            var root = new GameObject("MarsSphere");
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = sphereMesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var rotator = root.AddComponent<MarsSphereRotator>();
            rotator.Configure(2.4f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MarsPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void BuildAndSaveMainMenuScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.03f, 0.025f, 1f);
            RenderSettings.skybox = null;

            // Camera framing Mars on right half of the screen cleanly
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.position = Vector3.zero;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.010f, 0.011f, 0.018f, 1f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 120f;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            camGo.AddComponent<AudioListener>();

            // Sun key light from upper-left front-left for crisp day/night Martian terminator shadow
            var sunGo = new GameObject("SunKeyLight");
            sunGo.transform.rotation = Quaternion.Euler(20f, 44f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.89f, 0.78f, 1f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.None;

            // Subtle warm cosmic rim on dark limb
            var rimGo = new GameObject("RimFillLight");
            rimGo.transform.rotation = Quaternion.Euler(-15f, -135f, 0f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.22f, 0.10f, 1f);
            rim.intensity = 0.22f;
            rim.shadows = LightShadows.None;

            // Rotating Mars planet instance on the right side
            var marsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarsPrefabPath);
            var marsInst = (GameObject)PrefabUtility.InstantiatePrefab(marsPrefab);
            marsInst.name = "MarsSphere_Title";
            marsInst.transform.position = new Vector3(1.52f, -0.02f, 6.4f);
            marsInst.transform.rotation = Quaternion.Euler(-7f, -45f, -12f);
            marsInst.transform.localScale = Vector3.one * 3.35f;

            // Procedural background starfield mesh
            CreateStarfieldObject();

            // EventSystem (Input System)
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            // MainMenuController
            var menuGo = new GameObject("MainMenuController");
            menuGo.AddComponent<AccessibilitySettings>();
            menuGo.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateStarfieldObject()
        {
            Mesh starMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StarMeshPath);
            if (starMesh == null)
            {
                starMesh = BuildStarfieldQuadMesh(260, seed: 2026);
                AssetDatabase.CreateAsset(starMesh, StarMeshPath);
            }

            Shader unlitShader = Shader.Find("Outpost/PSXUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material starMat = AssetDatabase.LoadAssetAtPath<Material>(StarMatPath);
            if (starMat == null)
            {
                starMat = new Material(unlitShader) { name = "M_MenuStars" };
                AssetDatabase.CreateAsset(starMat, StarMatPath);
            }
            else
            {
                starMat.shader = unlitShader;
            }
            starMat.SetColor("_Color", new Color(1.0f, 0.88f, 0.74f, 1f));
            if (starMat.HasProperty("_EmissionColor"))
                starMat.SetColor("_EmissionColor", new Color(0.95f, 0.80f, 0.65f, 1f));
            if (starMat.HasProperty("_FogWeight"))
                starMat.SetFloat("_FogWeight", 0.0f);
            EditorUtility.SetDirty(starMat);

            var go = new GameObject("SpaceStarfield");
            go.transform.position = new Vector3(0f, 0f, 22f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = starMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = starMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static Mesh BuildStarfieldQuadMesh(int starCount, int seed)
        {
            var rng = new System.Random(seed);
            var verts = new Vector3[starCount * 4];
            var uvs = new Vector2[starCount * 4];
            var colors = new Color[starCount * 4];
            var tris = new int[starCount * 6];

            for (int i = 0; i < starCount; i++)
            {
                float x = (float)(rng.NextDouble() * 34.0 - 17.0);
                float y = (float)(rng.NextDouble() * 19.0 - 9.5);
                float z = (float)(rng.NextDouble() * 14.0);
                float s = (float)(0.012 + rng.NextDouble() * 0.028);
                float b = (float)(0.25 + rng.NextDouble() * 0.75);
                Color col = rng.NextDouble() < 0.25
                    ? new Color(b, b * 0.68f, b * 0.45f, 1f)
                    : new Color(b, b * 0.94f, b * 0.86f, 1f);

                int v = i * 4;
                verts[v + 0] = new Vector3(x - s, y - s, z);
                verts[v + 1] = new Vector3(x + s, y - s, z);
                verts[v + 2] = new Vector3(x + s, y + s, z);
                verts[v + 3] = new Vector3(x - s, y + s, z);

                uvs[v + 0] = Vector2.zero;
                uvs[v + 1] = Vector2.right;
                uvs[v + 2] = Vector2.one;
                uvs[v + 3] = Vector2.up;

                colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = col;

                int t = i * 6;
                tris[t + 0] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "M_MenuStarfield" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateEquirectangularSphereMesh(int lonSegments, int latSegments, float radius)
        {
            int vertCols = lonSegments + 1;
            int vertRows = latSegments + 1;
            var verts = new Vector3[vertCols * vertRows];
            var norms = new Vector3[vertCols * vertRows];
            var uvs = new Vector2[vertCols * vertRows];
            var tris = new int[lonSegments * latSegments * 6];

            for (int r = 0; r < vertRows; r++)
            {
                float v = r / (float)latSegments;
                float phi = (v - 0.5f) * Mathf.PI;
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                for (int c = 0; c < vertCols; c++)
                {
                    float u = c / (float)lonSegments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 normal = new Vector3(
                        cosPhi * Mathf.Cos(theta),
                        sinPhi,
                        cosPhi * Mathf.Sin(theta));

                    int idx = r * vertCols + c;
                    verts[idx] = normal * radius;
                    norms[idx] = normal;
                    uvs[idx] = new Vector2(1f - u, v);
                }
            }

            int ti = 0;
            for (int r = 0; r < latSegments; r++)
            {
                for (int c = 0; c < lonSegments; c++)
                {
                    int a = r * vertCols + c;
                    int b = a + 1;
                    int d = a + vertCols;
                    int e = d + 1;
                    // Outward-facing CCW winding
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = e;
                }
            }

            var mesh = new Mesh { name = "M_MarsSphereMesh" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CleanTitleRigFromOutpostScene()
        {
            var outpostScene = EditorSceneManager.OpenScene(OutpostScenePath, OpenSceneMode.Single);
            var rig = GameObject.Find("TitleMarsRig");
            if (rig != null)
                Object.DestroyImmediate(rig);
            EditorSceneManager.MarkSceneDirty(outpostScene);
            EditorSceneManager.SaveScene(outpostScene);

            // Re-open MainMenu so the user immediately sees MainMenu ready with the rotating Mars planet
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        }

        private static void UpdateEditorBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(OutpostScenePath, true)
            };
            if (File.Exists(TerminalScenePath))
                scenes.Add(new EditorBuildSettingsScene(TerminalScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
