using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Editor
{
    /// <summary>
    /// Creates the core PSX materials + 64x64 procedural point-filtered textures under Assets/Art/PSX/
    /// and exposes "Outpost/Apply PSX Materials" to convert renderers in the scene and prefabs without pink errors.
    /// </summary>
    public static class PSXMaterialApplier
    {
        public const string PsxArtDir = "Assets/Art/PSX";
        public const string PsxTexDir = "Assets/Art/PSX/PSXTextures";

        public class PsxMaterialSet
        {
            public Material Wall, Floor, Metal, DirtyMetal, Screen, Lamp, WindowGlass, Rock, Dust, MarsGround, Sky, Post;
        }

        [MenuItem("Outpost/Apply PSX Materials")]
        public static void ApplyPSXMaterialsToSceneAndSelection()
        {
            var set = EnsurePsxMaterialsAndTextures();
            int converted = 0;

            var sceneRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            foreach (var r in sceneRenderers)
            {
                if (ConvertRendererToPsx(r, set))
                    converted++;
            }

            var selectedObjects = Selection.GetFiltered<GameObject>(SelectionMode.Assets | SelectionMode.Editable);
            var visitedPrefabs = new HashSet<string>();
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selectedObjects[i]);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab") && visitedPrefabs.Add(path))
                {
                    converted += ConvertPrefabAssetToPsx(path, set);
                }
                else
                {
                    var childRends = selectedObjects[i].GetComponentsInChildren<Renderer>(true);
                    foreach (var cr in childRends)
                    {
                        if (ConvertRendererToPsx(cr, set))
                            converted++;
                    }
                }
            }

            // Always convert interior & exterior prefabs as well so Prefabs/ never retain raw URP Lit or pink shaders
            string[] prefabFolders = { "Assets/Prefabs/Interior", "Assets/Prefabs" };
            string[] guids = AssetDatabase.FindAssets("t:Prefab", prefabFolders);
            foreach (string guid in guids)
            {
                string pPath = AssetDatabase.GUIDToAssetPath(guid);
                if (visitedPrefabs.Add(pPath))
                    converted += ConvertPrefabAssetToPsx(pPath, set);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PSXMaterialApplier] Applied PSX materials cleanly ({converted} renderers checked/updated).");
        }

        public static int ConvertPrefabAssetToPsx(string prefabPath, PsxMaterialSet set)
        {
            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
                return 0;

            int count = 0;
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents != null)
            {
                var renderers = contents.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (ConvertRendererToPsx(renderers[i], set))
                        count++;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return count;
        }

        public static bool ConvertRendererToPsx(Renderer rend, PsxMaterialSet set)
        {
            if (rend == null || set == null) return false;
            if (rend.GetComponent<TMP_Text>() != null || rend.GetComponent<TextMeshPro>() != null)
                return false;

            var mats = rend.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material converted = ConvertOrSelectPsxMaterial(mats[i], rend.gameObject.name, set);
                if (converted != null)
                {
                    if (mats[i] != converted)
                        mats[i] = converted;
                    changed = true;
                }
            }
            if (changed)
            {
                rend.sharedMaterials = mats;
                EditorUtility.SetDirty(rend);
            }
            return changed;
        }

        public static Material ConvertOrSelectPsxMaterial(Material currentMat, string goName, PsxMaterialSet set)
        {
            string key = ((currentMat != null ? currentMat.name : "") + " " + goName).ToLowerInvariant();
            Material targetTemplate = PickNearestTemplate(key, set);

            if (currentMat == null || currentMat.name.Contains("Default-Material") || currentMat.name.Contains("Default-Particle"))
                return targetTemplate;

            string matPath = AssetDatabase.GetAssetPath(currentMat);

            // Preserve custom high-contrast station/radio/sign material assets once already on PSX shader with a texture
            if (!string.IsNullOrEmpty(matPath) && currentMat.shader != null &&
                (currentMat.shader.name == "Outpost/PSXLit" || currentMat.shader.name == "Outpost/PSXUnlit"))
            {
                Texture curTex = currentMat.HasProperty("_MainTex") ? currentMat.GetTexture("_MainTex") : null;
                if (curTex != null)
                {
                    EnsurePointTexture(curTex);
                    EditorUtility.SetDirty(currentMat);
                    return currentMat;
                }
            }
            if (!string.IsNullOrEmpty(matPath) && matPath.StartsWith(PsxArtDir + "/"))
            {
                EnsurePointTexture(currentMat.HasProperty("_MainTex") ? currentMat.GetTexture("_MainTex") : null);
                EnsurePointTexture(currentMat.HasProperty("_EmissionMap") ? currentMat.GetTexture("_EmissionMap") : null);
                if (currentMat.shader == null || currentMat.shader.name.Contains("Error") || !currentMat.shader.isSupported)
                {
                    if (targetTemplate != null) currentMat.shader = targetTemplate.shader;
                }
                EditorUtility.SetDirty(currentMat);
                return currentMat;
            }

            // Convert material cleanly to a dedicated editable material asset if embedded in an FBX/GLB
            Material workMat = currentMat;
            if (!string.IsNullOrEmpty(matPath) && !matPath.EndsWith(".mat"))
            {
                string safeDir = PsxArtDir + "/Converted";
                if (!Directory.Exists(safeDir)) Directory.CreateDirectory(safeDir);
                string cleanName = string.Join("_", (currentMat.name + "_" + Mathf.Abs(currentMat.GetHashCode())).Split(Path.GetInvalidFileNameChars()));
                string outPath = $"{safeDir}/{cleanName}.mat";
                var existingAsset = AssetDatabase.LoadAssetAtPath<Material>(outPath);
                if (existingAsset != null)
                {
                    workMat = existingAsset;
                }
                else
                {
                    workMat = new Material(currentMat);
                    AssetDatabase.CreateAsset(workMat, outPath);
                }
            }

            Texture existingTex = null;
            if (currentMat.HasProperty("_BaseMap")) existingTex = currentMat.GetTexture("_BaseMap");
            if (existingTex == null && currentMat.HasProperty("_MainTex")) existingTex = currentMat.GetTexture("_MainTex");
            if (existingTex == null && currentMat.HasProperty("baseColorTexture")) existingTex = currentMat.GetTexture("baseColorTexture");
            EnsurePointTexture(existingTex);

            Texture emissiveTex = null;
            if (currentMat.HasProperty("_EmissionMap")) emissiveTex = currentMat.GetTexture("_EmissionMap");
            if (emissiveTex == null && currentMat.HasProperty("emissiveTexture")) emissiveTex = currentMat.GetTexture("emissiveTexture");
            EnsurePointTexture(emissiveTex);

            Color tint = Color.white;
            if (currentMat.HasProperty("_BaseColor")) tint = currentMat.GetColor("_BaseColor");
            else if (currentMat.HasProperty("_Color")) tint = currentMat.GetColor("_Color");
            else if (currentMat.HasProperty("baseColorFactor")) tint = currentMat.GetColor("baseColorFactor");

            Color emission = Color.black;
            float emissionStrength = 0f;
            if (currentMat.HasProperty("_EmissionColor"))
            {
                emission = currentMat.GetColor("_EmissionColor");
                if (currentMat.IsKeywordEnabled("_EMISSION") || emission.maxColorComponent > 0.001f)
                    emissionStrength = 1.0f;
            }
            if (currentMat.HasProperty("emissiveFactor"))
            {
                Color ef = currentMat.GetColor("emissiveFactor");
                if (ef.maxColorComponent > 0.001f)
                {
                    emission = ef;
                    emissionStrength = 1.0f;
                }
            }
            if (emissiveTex != null && emission.maxColorComponent <= 0.001f)
            {
                emission = Color.white;
                emissionStrength = 1.4f;
            }

            bool isGlowMat = key.Contains("glow") || key.Contains("emissive") || key.Contains("neon") || key.Contains("cyan");
            if (isGlowMat || targetTemplate == set.Lamp || targetTemplate == set.Screen)
            {
                if (emissionStrength <= 0.001f)
                {
                    emission = isGlowMat ? (tint.maxColorComponent > 0.2f ? tint : new Color(0.27f, 0.89f, 0.90f, 1f)) : tint;
                    emissionStrength = isGlowMat ? 1.5f : 1.0f;
                }
            }

            Shader chosenShader = isGlowMat ? Shader.Find("Outpost/PSXUnlit") : (targetTemplate != null ? targetTemplate.shader : Shader.Find("Outpost/PSXLit"));
            if (chosenShader != null)
            {
                workMat.shader = chosenShader;
                if (existingTex != null)
                    workMat.SetTexture("_MainTex", existingTex);
                else if (targetTemplate != null && targetTemplate.HasProperty("_MainTex") && targetTemplate.GetTexture("_MainTex") != null)
                    workMat.SetTexture("_MainTex", targetTemplate.GetTexture("_MainTex"));

                if (workMat.HasProperty("_EmissionMap") && emissiveTex != null)
                    workMat.SetTexture("_EmissionMap", emissiveTex);

                workMat.SetColor("_Color", tint);
                if (workMat.HasProperty("_EmissionColor")) workMat.SetColor("_EmissionColor", emission);
                if (workMat.HasProperty("_EmissionStrength")) workMat.SetFloat("_EmissionStrength", emissionStrength);
                if (workMat.HasProperty("_AffineAmount") && targetTemplate != null && targetTemplate.HasProperty("_AffineAmount"))
                    workMat.SetFloat("_AffineAmount", targetTemplate.GetFloat("_AffineAmount"));
                if (workMat.HasProperty("_FogWeight") && targetTemplate != null && targetTemplate.HasProperty("_FogWeight"))
                    workMat.SetFloat("_FogWeight", targetTemplate.GetFloat("_FogWeight"));

                if (targetTemplate == set.WindowGlass)
                {
                    workMat.SetOverrideTag("RenderType", "Transparent");
                    workMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    workMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    workMat.SetFloat("_ZWrite", 0f);
                    workMat.renderQueue = 3000;
                }
                else
                {
                    workMat.SetOverrideTag("RenderType", "Opaque");
                    if (workMat.HasProperty("_SrcBlend")) workMat.SetInt("_SrcBlend", (int)BlendMode.One);
                    if (workMat.HasProperty("_DstBlend")) workMat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    if (workMat.HasProperty("_ZWrite")) workMat.SetFloat("_ZWrite", 1f);
                    workMat.renderQueue = 2000;
                }

                EditorUtility.SetDirty(workMat);
            }

            return workMat;
        }

        public static void EnsurePointTexture(Texture tex)
        {
            if (tex == null) return;
            string texPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(texPath)) return;
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        private static Material PickNearestTemplate(string key, PsxMaterialSet set)
        {
            if (key.Contains("dust") || key.Contains("particle")) return set.Dust;
            if (key.Contains("sky")) return set.Sky;
            if (key.Contains("ground") || key.Contains("terrain") || key.Contains("sand") || key.Contains("soil")) return set.MarsGround;
            if (key.Contains("rock") || key.Contains("ridge") || key.Contains("boulder")) return set.Rock;
            if (key.Contains("glass")) return set.WindowGlass;
            if (key.Contains("screen") || key.Contains("monitor_screen")) return set.Screen;
            if (key.Contains("lampbase") || key.Contains("lampstem") || key.Contains("lampshade")) return set.DirtyMetal;
            if (key.Contains("lamp") || key.Contains("bulb") || key.Contains("led") || key.Contains("alarmbulb") || key.Contains("medbaywindow") || key.Contains("sun") || key.Contains("glow")) return set.Lamp;
            if (key.Contains("floor")) return set.Floor;
            if (key.Contains("wall") || key.Contains("ceiling")) return set.Wall;
            if (key.Contains("chair") || key.Contains("locker") || key.Contains("trim") || key.Contains("pipe") || key.Contains("beam") || key.Contains("stand") || key.Contains("frame") || key.Contains("rover")) return set.DirtyMetal;
            return set.Metal;
        }

        public static PsxMaterialSet EnsurePsxMaterialsAndTextures()
        {
            if (!Directory.Exists(PsxArtDir)) Directory.CreateDirectory(PsxArtDir);
            if (!Directory.Exists(PsxTexDir)) Directory.CreateDirectory(PsxTexDir);

            Shader litShader = Shader.Find("Outpost/PSXLit");
            Shader unlitShader = Shader.Find("Outpost/PSXUnlit");
            Shader particleShader = Shader.Find("Outpost/PSXParticle");
            Shader postShader = Shader.Find("Outpost/PSXPost");

            var tWall = CreatePanelTex("T_PSX_Wall", new Color(0.84f, 0.86f, 0.90f, 1f), new Color(0.68f, 0.70f, 0.75f, 1f), 16, 0x101u);
            var tFloor = CreateGridTex("T_PSX_Floor", new Color(0.76f, 0.78f, 0.82f, 1f), new Color(0.56f, 0.58f, 0.63f, 1f), 16, 0x202u);
            var tMetal = CreatePanelTex("T_PSX_Metal", new Color(0.85f, 0.87f, 0.90f, 1f), new Color(0.68f, 0.72f, 0.77f, 1f), 32, 0x303u);
            var tDirtyMetal = CreateNoisePlateTex("T_PSX_DirtyMetal", new Color(0.74f, 0.73f, 0.72f, 1f), new Color(0.50f, 0.48f, 0.47f, 1f), 0x404u);
            var tScreen = CreatePanelTex("T_PSX_Screen", new Color(0.60f, 0.95f, 1.0f, 1f), new Color(0.28f, 0.65f, 0.78f, 1f), 8, 0x505u);
            var tLamp = CreateNoisePlateTex("T_PSX_Lamp", Color.white, new Color(0.95f, 0.90f, 0.80f, 1f), 0x606u);
            var tGlass = CreateGlassTex("T_PSX_WindowGlass");
            var tRock = CreateNoisePlateTex("T_PSX_Rock", new Color(0.90f, 0.78f, 0.72f, 1f), new Color(0.58f, 0.42f, 0.34f, 1f), 0x707u);
            var tGround = CreateNoisePlateTex("T_PSX_MarsGround", new Color(0.92f, 0.76f, 0.65f, 1f), new Color(0.66f, 0.44f, 0.33f, 1f), 0x808u);
            var tDust = CreateDustTex("T_PSX_Dust");
            var tSky = CreateSkyTex("T_PSX_Sky");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var set = new PsxMaterialSet
            {
                Wall = CreateOrUpdateMat("Wall", litShader, tWall, new Color(0.46f, 0.48f, 0.52f, 1f), Color.black, 0f, 0.85f, 0.22f),
                Floor = CreateOrUpdateMat("Floor", litShader, tFloor, new Color(0.28f, 0.30f, 0.34f, 1f), Color.black, 0f, 0.90f, 0.22f),
                Metal = CreateOrUpdateMat("Metal", litShader, tMetal, new Color(0.36f, 0.39f, 0.43f, 1f), Color.black, 0f, 0.80f, 0.22f),
                DirtyMetal = CreateOrUpdateMat("DirtyMetal", litShader, tDirtyMetal, new Color(0.24f, 0.25f, 0.28f, 1f), Color.black, 0f, 0.85f, 0.22f),
                Screen = CreateOrUpdateMat("Screen", unlitShader, tScreen, new Color(0.08f, 0.24f, 0.30f, 1f), new Color(0.08f, 0.26f, 0.32f, 1f), 1.0f, 0.30f, 0.05f),
                Lamp = CreateOrUpdateMat("Lamp", unlitShader, tLamp, new Color(1.0f, 0.86f, 0.56f, 1f), new Color(1.0f, 0.84f, 0.52f, 1f), 1.6f, 0.20f, 0.05f),
                WindowGlass = CreateGlassMat("WindowGlass", unlitShader, tGlass, new Color(0.55f, 0.72f, 0.85f, 0.10f)),
                Rock = CreateOrUpdateMat("Rock", litShader, tRock, new Color(0.62f, 0.30f, 0.18f, 1f), new Color(0.28f, 0.12f, 0.07f, 1f), 0.75f, 1.0f, 0.70f),
                MarsGround = CreateOrUpdateMat("MarsGround", litShader, tGround, new Color(0.76f, 0.37f, 0.21f, 1f), new Color(0.34f, 0.15f, 0.08f, 1f), 0.75f, 1.0f, 0.75f),
                Sky = CreateOrUpdateMat("Sky", unlitShader, tSky, Color.white, Color.black, 0f, 0.25f, 0.10f),
                Dust = CreateParticleMat("Dust", particleShader, tDust, new Color(0.92f, 0.55f, 0.36f, 0.55f)),
                Post = CreatePostMat("M_PSXPost", postShader)
            };

            AssetDatabase.SaveAssets();
            return set;
        }

        private static Material CreateOrUpdateMat(string matName, Shader shader, Texture2D tex, Color color, Color emission, float emissionStrength, float affine, float fogWeight)
        {
            string path = $"{PsxArtDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", color);
            mat.SetColor("_EmissionColor", emission);
            mat.SetFloat("_EmissionStrength", emissionStrength);
            mat.SetFloat("_AffineAmount", affine);
            mat.SetFloat("_FogWeight", fogWeight);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = 2000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateGlassMat(string matName, Shader unlitShader, Texture2D tex, Color tint)
        {
            string path = $"{PsxArtDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(unlitShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = unlitShader;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", tint);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EmissionStrength", 0f);
            mat.SetFloat("_AffineAmount", 0f);
            mat.SetFloat("_FogWeight", 0.05f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateParticleMat(string matName, Shader particleShader, Texture2D tex, Color tint)
        {
            string path = $"{PsxArtDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(particleShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = particleShader;
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", tint);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_EmissionStrength", 0f);
            mat.SetFloat("_FogWeight", 0.25f);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreatePostMat(string matName, Shader postShader)
        {
            string path = $"{PsxArtDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(postShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = postShader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D CreatePanelTex(string name, Color baseCol, Color lineCol, int spacing, uint seed)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                float n = HashNoise(x, y, seed) * 0.06f - 0.03f;
                bool seam = (x % spacing == 0) || (y % spacing == 0);
                bool rivet = ((x % spacing == 2) || (x % spacing == spacing - 2))
                          && ((y % spacing == 2) || (y % spacing == spacing - 2));
                Color c = seam ? lineCol : (rivet ? Color.Lerp(baseCol, lineCol, 0.55f) : baseCol);
                return OffsetColor(c, n);
            });
        }

        private static Texture2D CreateGridTex(string name, Color baseCol, Color lineCol, int spacing, uint seed)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                float n = HashNoise(x, y, seed) * 0.07f - 0.035f;
                bool border = (x % spacing == 0) || (y % spacing == 0);
                bool diag = ((x + y) % 8 == 0) && !border && ((x / spacing + y / spacing) % 2 == 0);
                Color c = border ? lineCol : (diag ? Color.Lerp(baseCol, lineCol, 0.28f) : baseCol);
                return OffsetColor(c, n);
            });
        }

        private static Texture2D CreateNoisePlateTex(string name, Color hi, Color lo, uint seed)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                float n = HashNoise(x, y, seed);
                float coarse = HashNoise(x / 4, y / 4, seed + 19u);
                float t = Mathf.Clamp01(n * 0.55f + coarse * 0.45f);
                return Color.Lerp(lo, hi, t);
            });
        }

        private static Texture2D CreateGlassTex(string name)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                bool streak = (x + y == 28 || x + y == 29 || x + y == 74);
                return streak ? new Color(1f, 1f, 1f, 0.75f) : new Color(0.8f, 0.92f, 1f, 0.35f);
            });
        }

        private static Texture2D CreateDustTex(string name)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                float dx = (x - 31.5f) / 31.5f;
                float dy = (y - 31.5f) / 31.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            });
        }

        private static Texture2D CreateSkyTex(string name)
        {
            return WriteAndLoad64(name, (x, y) =>
            {
                float t = y / 63f;
                Color horizon = new Color(0.86f, 0.46f, 0.26f, 1f);
                Color midSky = new Color(0.74f, 0.42f, 0.38f, 1f);
                Color zenith = new Color(0.22f, 0.16f, 0.24f, 1f);
                Color c = t < 0.42f
                    ? Color.Lerp(horizon, midSky, t / 0.42f)
                    : Color.Lerp(midSky, zenith, (t - 0.42f) / 0.58f);
                float dither = (((x & 1) ^ (y & 1)) == 0 ? 0.015f : -0.015f);
                return OffsetColor(c, dither);
            });
        }

        private static Texture2D WriteAndLoad64(string texName, System.Func<int, int, Color> pixelFunc)
        {
            string path = $"{PsxTexDir}/{texName}.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, pixelFunc(x, y));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float HashNoise(int x, int y, uint seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263) ^ seed;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFu) / 65535f;
        }

        private static Color OffsetColor(Color c, float delta)
        {
            return new Color(
                Mathf.Clamp01(c.r + delta),
                Mathf.Clamp01(c.g + delta),
                Mathf.Clamp01(c.b + delta),
                c.a);
        }
    }
}
