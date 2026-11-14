using System;
using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Outpost.Editor
{
    /// <summary>
    /// Generates the 300m x 300m MarsPlayTerrain (heightmap 513, flat 60m x 60m play area in front of airlock),
    /// 1200m x 1200m BackdropTerrain (no colliders, reduced detail, vertical exaggeration),
    /// 3 procedural 64x64 Point-filtered terrain textures & splat layers, 3 low-poly rock prefabs (~110 scattered),
    /// dusty orange-pink PSX sky gradient + tiny sun disc + matched Exp2 fog.
    /// </summary>
    public static class MarsTerrainGenerator
    {
        public const string PsxDir = "Assets/Art/PSX";
        public const string TexDir = "Assets/Art/PSX/PSXTextures";
        public const string MeshDir = "Assets/Art/PSX/Meshes";
        public const string PrefabsDir = "Assets/Prefabs";

        public static readonly Vector3 AirlockExitCenter = new Vector3(0f, 0f, -18f);
        public static readonly Color ClearMarsHorizon = new Color(0.68f, 0.39f, 0.27f, 1f);
        public static readonly Color ClearMarsMidSky = new Color(0.76f, 0.44f, 0.34f, 1f);
        public static readonly Color ClearMarsZenith = new Color(0.38f, 0.23f, 0.28f, 1f);
        public static readonly Color ClearMarsGround = new Color(0.48f, 0.25f, 0.17f, 1f);

        [MenuItem("Outpost/Generate Mars Terrain")]
        public static void GenerateAllMarsTerrain()
        {
            EnsureDirectories();
            var psxSet = PSXMaterialApplier.EnsurePsxMaterialsAndTextures();

            var settings = EnsureTerrainSettingsObject(out Transform surfaceRoot);
            var layers = EnsureTerrainLayers();
            var terrainMat = EnsurePsxTerrainMaterial(layers);

            BuildPlayTerrain(surfaceRoot, settings, layers, terrainMat);
            ApplyBackdropHeightmap(null, 513, settings.verticalExaggeration, keepCenterClear: true, settings, layers, terrainMat, surfaceRoot);
            BuildAndScatterRocks(surfaceRoot, settings, psxSet);
            ConfigureSkyAndFog(surfaceRoot);
            DisableLegacySurfaceQuads(surfaceRoot);

            AssetDatabase.SaveAssets();
            Debug.Log("[MarsTerrainGenerator] Generated MarsPlayTerrain (300m x 300m, res 513), BackdropTerrain (1200m x 1200m), 3 rock prefabs (~110 instances), and Mars sky/fog.");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PsxDir)) Directory.CreateDirectory(PsxDir);
            if (!Directory.Exists(TexDir)) Directory.CreateDirectory(TexDir);
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
        }

        public static MarsTerrainSettings EnsureTerrainSettingsObject(out Transform surfaceRoot)
        {
            var worldRoot = GameObject.Find("OutpostExpansionWorld");
            surfaceRoot = worldRoot != null ? worldRoot.transform.Find("MarsSurfaceWorld") : null;
            if (surfaceRoot == null)
            {
                var surfaceGo = GameObject.Find("MarsSurfaceWorld");
                if (surfaceGo == null) surfaceGo = new GameObject("MarsSurfaceWorld");
                if (worldRoot != null) surfaceGo.transform.SetParent(worldRoot.transform, false);
                surfaceRoot = surfaceGo.transform;
            }

            var settings = Object.FindAnyObjectByType<MarsTerrainSettings>();
            if (settings == null)
                settings = surfaceRoot.gameObject.AddComponent<MarsTerrainSettings>();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        public static TerrainLayer[] EnsureTerrainLayers()
        {
            var tSand = CreateTerrainNoiseTexture("T_Mars_Sand", new Color(0.78f, 0.38f, 0.21f, 1f), new Color(0.64f, 0.29f, 0.16f, 1f), 0x1A2B3Cu, ridged: false);
            var tRock = CreateTerrainNoiseTexture("T_Mars_DarkRock", new Color(0.42f, 0.22f, 0.17f, 1f), new Color(0.26f, 0.14f, 0.11f, 1f), 0x4D5E6Fu, ridged: true);
            var tDust = CreateTerrainNoiseTexture("T_Mars_LightDust", new Color(0.89f, 0.56f, 0.38f, 1f), new Color(0.75f, 0.44f, 0.28f, 1f), 0x7F8A9Bu, ridged: false);

            var lSand = CreateOrUpdateLayer("Layer_MarsSand", tSand, new Vector2(8f, 8f));
            var lRock = CreateOrUpdateLayer("Layer_MarsDarkRock", tRock, new Vector2(10f, 10f));
            var lDust = CreateOrUpdateLayer("Layer_MarsLightDust", tDust, new Vector2(7f, 7f));
            return new[] { lSand, lRock, lDust };
        }

        private static Texture2D CreateTerrainNoiseTexture(string name, Color cA, Color cB, uint seed, bool ridged)
        {
            string path = $"{TexDir}/{name}.png";
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    uint h = Hash(x, y, seed);
                    float n = (h & 0xFF) / 255f;
                    float wave = Mathf.Sin((x / (float)size) * Mathf.PI * (ridged ? 6f : 4f) + n * 1.4f) * 0.5f + 0.5f;
                    float t = Mathf.Clamp01(n * 0.65f + wave * 0.35f);
                    tex.SetPixel(x, y, Color.Lerp(cA, cB, t));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static uint Hash(int x, int y, uint s)
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + s * 1274126177u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        private static TerrainLayer CreateOrUpdateLayer(string assetName, Texture2D diffuse, Vector2 tileSize)
        {
            string path = $"{PsxDir}/{assetName}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = diffuse;
            layer.tileSize = tileSize;
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = 0.04f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        public static Material EnsurePsxTerrainMaterial(TerrainLayer[] layers)
        {
            string path = $"{PsxDir}/M_PSXTerrain.mat";
            Shader sh = Shader.Find("Outpost/PSXTerrain") ?? Shader.Find("Outpost/PSXLit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_AffineAmount")) mat.SetFloat("_AffineAmount", 0.35f);
            if (mat.HasProperty("_FogWeight")) mat.SetFloat("_FogWeight", 1.0f);
            if (layers != null && layers.Length >= 3)
            {
                if (mat.HasProperty("_Splat0")) mat.SetTexture("_Splat0", layers[0].diffuseTexture);
                if (mat.HasProperty("_Splat1")) mat.SetTexture("_Splat1", layers[1].diffuseTexture);
                if (mat.HasProperty("_Splat2")) mat.SetTexture("_Splat2", layers[2].diffuseTexture);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildPlayTerrain(Transform surfaceRoot, MarsTerrainSettings s, TerrainLayer[] layers, Material terrainMat)
        {
            int res = (s.heightmapResolution == 513 || s.heightmapResolution == 1025) ? s.heightmapResolution : 513;
            const float maxTerrainHeight = 36f;
            float sizeM = Mathf.Max(100f, s.sizeMeters);

            string dataPath = $"{PsxDir}/TerrainData_MarsPlay.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, dataPath);
            }
            td.heightmapResolution = res;
            td.size = new Vector3(sizeM, maxTerrainHeight, sizeM);
            td.terrainLayers = layers;

            float ox = (s.seed % 997) * 0.37f;
            float oz = ((s.seed / 997) % 997) * 0.41f;
            float[,] heights = new float[res, res];

            float half = sizeM * 0.5f;
            float flatHalf = Mathf.Max(26f, s.flatPlayRadius);

            for (int iz = 0; iz < res; iz++)
            {
                float vz = iz / (float)(res - 1);
                float wz = (vz * sizeM) - half + AirlockExitCenter.z;
                float relZ = wz - AirlockExitCenter.z;

                for (int ix = 0; ix < res; ix++)
                {
                    float vx = ix / (float)(res - 1);
                    float wx = (vx * sizeM) - half + AirlockExitCenter.x;
                    float relX = wx - AirlockExitCenter.x;

                    // Square walkable core: strictly flat 60m x 60m pad centered on airlock exit
                    float boxDist = Mathf.Max(Mathf.Abs(relX), Mathf.Abs(relZ));
                    float radDist = Mathf.Sqrt(relX * relX + relZ * relZ);

                    float h = s.flatPlayElevation;
                    if (boxDist > flatHalf)
                    {
                        float rimT = Mathf.Clamp01((boxDist - flatHalf) / 9.0f);
                        float rimRamp = rimT * rimT * (3f - 2f * rimT);

                        // Layered Perlin dunes & rocky ridges
                        float nDune1 = Mathf.PerlinNoise((wx * 0.025f) + ox, (wz * 0.025f) + oz);
                        float nDune2 = Mathf.PerlinNoise((wx * 0.065f) + ox + 17f, (wz * 0.065f) + oz + 31f);
                        float dunes = ((nDune1 - 0.35f) * 0.72f + (nDune2 - 0.4f) * 0.28f) * s.duneAmplitude;

                        float nRidge = Mathf.PerlinNoise((wx * 0.018f) + ox + 83f, (wz * 0.018f) + oz + 47f);
                        float ridged = Mathf.Pow(1f - Mathf.Abs(2f * nRidge - 1f), 2.1f) * s.ridgeAmplitude;

                        // Steep perimeter rise just outside walkable zone so player naturally stays bounded
                        float perimeterWall = rimRamp * 4.2f;

                        // High outer crater/ridge ring 118m-142m from airlock exit
                        float ringCenter = (s.outerRimMinRadius + s.outerRimMaxRadius) * 0.5f;
                        float ringHalfSpan = Mathf.Max(8f, (s.outerRimMaxRadius - s.outerRimMinRadius) * 0.5f);
                        float ringWeight = Mathf.Exp(-Mathf.Pow((radDist - ringCenter) / ringHalfSpan, 2f));
                        float ringDetail = 0.65f + 0.55f * Mathf.PerlinNoise((wx * 0.032f) + ox + 5f, (wz * 0.032f) + oz + 11f);

                        // Scenic northern saddle along Command Window sightline so distant MarsSphere remains visible low in sky
                        float northSaddle = 1.0f;
                        if (relZ > 12f)
                        {
                            float northAngle = Mathf.Abs(Mathf.Atan2(relX, relZ));
                            if (northAngle < 0.55f)
                                northSaddle = Mathf.Lerp(0.18f, 1.0f, Mathf.SmoothStep(0f, 1f, northAngle / 0.55f));
                        }

                        float outerRing = ringWeight * s.outerRimHeight * ringDetail;
                        h += (rimRamp * Mathf.Max(0f, dunes + ridged) + perimeterWall + outerRing) * northSaddle;
                    }

                    heights[iz, ix] = Mathf.Clamp01(h / maxTerrainHeight);
                }
            }

            td.SetHeights(0, 0, heights);
            PopulateSlopeHeightSplatmap(td);
            EditorUtility.SetDirty(td);

            var goTf = surfaceRoot.Find("MarsPlayTerrain");
            var go = goTf != null ? goTf.gameObject : null;
            if (go == null)
            {
                go = new GameObject("MarsPlayTerrain");
                go.transform.SetParent(surfaceRoot, false);
            }
            go.transform.position = new Vector3(AirlockExitCenter.x - half, -0.08f - s.flatPlayElevation, AirlockExitCenter.z - half);

            var t = go.GetComponent<Terrain>();
            if (t == null) t = go.AddComponent<Terrain>();
            var tc = go.GetComponent<TerrainCollider>();
            if (tc == null) tc = go.AddComponent<TerrainCollider>();
            t.terrainData = td;
            tc.terrainData = td;
            ApplyWebGLSafeTerrainSettings(t, terrainMat, highDetail: true);
        }

        public static void ApplyBackdropHeightmap(
            float[,] customHeights01,
            int customRes,
            float verticalExaggeration,
            bool keepCenterClear,
            MarsTerrainSettings settings = null,
            TerrainLayer[] layers = null,
            Material terrainMat = null,
            Transform surfaceRoot = null)
        {
            if (surfaceRoot == null || settings == null)
                settings = EnsureTerrainSettingsObject(out surfaceRoot);
            if (layers == null) layers = EnsureTerrainLayers();
            if (terrainMat == null) terrainMat = EnsurePsxTerrainMaterial(layers);

            int res = (customHeights01 != null && (customRes == 513 || customRes == 1025 || customRes == 2049)) ? customRes : 513;
            float sizeM = Mathf.Max(600f, settings.backdropSizeMeters);
            float maxHeight = 52f * Mathf.Max(0.25f, verticalExaggeration);

            string dataPath = $"{PsxDir}/TerrainData_MarsBackdrop.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, dataPath);
            }
            td.heightmapResolution = res;
            td.size = new Vector3(sizeM, maxHeight, sizeM);
            td.terrainLayers = layers;

            float[,] heights = new float[res, res];
            float half = sizeM * 0.5f;
            float clearRadius = (settings.sizeMeters * 0.5f) - 2f; // 148m -> overlaps safely under outer ridge
            float ox = (settings.seed % 701) * 0.51f + 300f;
            float oz = ((settings.seed / 701) % 701) * 0.43f + 300f;

            for (int iz = 0; iz < res; iz++)
            {
                float wz = ((iz / (float)(res - 1)) * sizeM) - half;
                for (int ix = 0; ix < res; ix++)
                {
                    float wx = ((ix / (float)(res - 1)) * sizeM) - half;
                    float dist = Mathf.Sqrt(wx * wx + wz * wz);

                    float val01;
                    if (customHeights01 != null)
                    {
                        val01 = Mathf.Clamp01(customHeights01[iz, ix]);
                    }
                    else
                    {
                        float macro = Mathf.PerlinNoise((wx * 0.0045f) + ox, (wz * 0.0045f) + oz);
                        float mid = Mathf.PerlinNoise((wx * 0.015f) + ox + 19f, (wz * 0.015f) + oz + 23f);
                        float crater = Mathf.Pow(1f - Mathf.Abs(2f * Mathf.PerlinNoise((wx * 0.008f) + 71f, (wz * 0.008f) + 39f) - 1f), 1.6f);
                        float baseM = 6f + macro * 22f + mid * 8f + crater * 20f;
                        val01 = Mathf.Clamp01((baseM * verticalExaggeration) / maxHeight);
                    }

                    if (keepCenterClear)
                    {
                        float fade = Mathf.Clamp01((dist - clearRadius) / 48f);
                        val01 *= fade * fade * (3f - 2f * fade);
                    }

                    if (wz > 0f)
                    {
                        float northAngle = Mathf.Abs(Mathf.Atan2(wx, wz));
                        if (northAngle < 0.55f)
                            val01 *= Mathf.Lerp(0.18f, 1.0f, Mathf.SmoothStep(0f, 1f, northAngle / 0.55f));
                    }

                    heights[iz, ix] = val01;
                }
            }

            td.SetHeights(0, 0, heights);
            PopulateSlopeHeightSplatmap(td);
            EditorUtility.SetDirty(td);

            var goTf = surfaceRoot.Find("BackdropTerrain");
            var go = goTf != null ? goTf.gameObject : null;
            if (go == null)
            {
                go = new GameObject("BackdropTerrain");
                go.transform.SetParent(surfaceRoot, false);
            }
            // Sit slightly below MarsPlayTerrain's outer base to guarantee zero Z-fighting in overlap band
            go.transform.position = new Vector3(AirlockExitCenter.x - half, -3.2f, AirlockExitCenter.z - half);

            var existingCol = go.GetComponent<TerrainCollider>();
            if (existingCol != null) Object.DestroyImmediate(existingCol);

            var t = go.GetComponent<Terrain>();
            if (t == null) t = go.AddComponent<Terrain>();
            t.terrainData = td;
            ApplyWebGLSafeTerrainSettings(t, terrainMat, highDetail: false);
            EditorUtility.SetDirty(t);
        }

        private static void PopulateSlopeHeightSplatmap(TerrainData td)
        {
            int aw = 256;
            td.alphamapResolution = aw;
            float[,,] maps = new float[aw, aw, 3];
            float maxH = Mathf.Max(1f, td.size.y);

            for (int y = 0; y < aw; y++)
            {
                float ny = y / (float)(aw - 1);
                for (int x = 0; x < aw; x++)
                {
                    float nx = x / (float)(aw - 1);
                    float slope = td.GetSteepness(nx, ny);
                    float hNorm = td.GetInterpolatedHeight(nx, ny) / maxH;

                    float rock = Mathf.Clamp01((slope - 13f) / 18f) + Mathf.Clamp01((hNorm - 0.45f) * 0.7f);
                    float dust = Mathf.Clamp01(1f - slope / 16f) * Mathf.Clamp01(0.45f + 0.55f * Mathf.PerlinNoise(nx * 14f, ny * 14f));
                    float sand = Mathf.Max(0.25f, 1f - rock * 0.85f);

                    rock = Mathf.Clamp01(rock);
                    dust = Mathf.Clamp01(dust) * 0.45f;
                    float sum = sand + rock + dust;
                    maps[y, x, 0] = sand / sum;
                    maps[y, x, 1] = rock / sum;
                    maps[y, x, 2] = dust / sum;
                }
            }
            td.SetAlphamaps(0, 0, maps);
        }

        private static void ApplyWebGLSafeTerrainSettings(Terrain t, Material terrainMat, bool highDetail)
        {
            t.materialTemplate = terrainMat;
            t.drawInstanced = false;
            t.drawTreesAndFoliage = false;
            t.detailObjectDistance = 0f;
            t.treeDistance = 0f;
            t.treeBillboardDistance = 0f;
            t.shadowCastingMode = ShadowCastingMode.Off;
            t.heightmapPixelError = highDetail ? 6f : 36f;
            t.basemapDistance = highDetail ? 140f : 220f;
            t.bakeLightProbesForTrees = false;
            t.gameObject.isStatic = true;
        }

        private static void BuildAndScatterRocks(Transform surfaceRoot, MarsTerrainSettings s, PSXMaterialApplier.PsxMaterialSet psxSet)
        {
            var smallPrefab = CreateRockPrefab("Rock_Small", 0.52f, 0.34f, 111u, withCollider: false, psxSet.Rock);
            var medPrefab = CreateRockPrefab("Rock_Medium", 1.15f, 0.78f, 222u, withCollider: false, psxSet.Rock);
            var largePrefab = CreateRockPrefab("Rock_Large", 2.35f, 1.65f, 333u, withCollider: true, psxSet.Rock);

            var oldRocks = surfaceRoot.Find("Terrain_ScatteredRocks");
            if (oldRocks != null) Object.DestroyImmediate(oldRocks.gameObject);

            var group = new GameObject("Terrain_ScatteredRocks");
            group.transform.SetParent(surfaceRoot, false);

            var playTerrainTf = surfaceRoot.Find("MarsPlayTerrain");
            var playTerrain = playTerrainTf != null ? playTerrainTf.GetComponent<Terrain>() : null;
            if (playTerrain == null) return;

            // 5 hand-placed rocks around perimeter of play zone (outside spawn, ramp, and POIs)
            Vector3[] handPos =
            {
                new Vector3(-16.5f, 0f, -22.5f),
                new Vector3(17.5f, 0f, -29.0f),
                new Vector3(-20.0f, 0f, -38.5f),
                new Vector3(14.5f, 0f, -41.0f),
                new Vector3(-13.5f, 0f, -15.5f)
            };
            GameObject[] handPrefabs = { medPrefab, largePrefab, smallPrefab, medPrefab, smallPrefab };
            for (int i = 0; i < handPos.Length; i++)
            {
                float wy = playTerrain.SampleHeight(handPos[i]) + playTerrain.transform.position.y - 0.04f;
                var r = (GameObject)PrefabUtility.InstantiatePrefab(handPrefabs[i], group.transform);
                r.name = $"PlayRock_{i}";
                r.transform.SetPositionAndRotation(new Vector3(handPos[i].x, wy, handPos[i].z), Quaternion.Euler(0f, i * 67f, 0f));
                r.transform.localScale = Vector3.one * (0.9f + (i % 3) * 0.14f);
                r.isStatic = true;
            }

            // Scatter 108 procedural rocks across terrain outside habitat & play paths by slope/height rules
            var rng = new System.Random(s.seed + 77);
            int placed = 0;
            int attempts = 0;
            while (placed < 108 && attempts < 1400)
            {
                attempts++;
                float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                float radius = 33f + (float)Math.Pow(rng.NextDouble(), 0.75) * 108f;
                float wx = AirlockExitCenter.x + Mathf.Cos(ang) * radius;
                float wz = AirlockExitCenter.z + Mathf.Sin(ang) * radius;

                Vector3 tPos = playTerrain.transform.position;
                Vector3 tSize = playTerrain.terrainData.size;
                float nx = (wx - tPos.x) / tSize.x;
                float nz = (wz - tPos.z) / tSize.z;
                if (nx < 0.03f || nx > 0.97f || nz < 0.03f || nz > 0.97f) continue;

                float slope = playTerrain.terrainData.GetSteepness(nx, nz);
                if (slope > 38f) continue;

                float height = playTerrain.SampleHeight(new Vector3(wx, 0f, wz));
                // Favor higher slopes/ridges for larger rocks, dunes/flats for small-medium pebbles
                GameObject chosen;
                double roll = rng.NextDouble();
                if (height > 5.0f && roll < 0.36) chosen = largePrefab;
                else if (roll < 0.62) chosen = medPrefab;
                else chosen = smallPrefab;

                float wy = height + tPos.y - 0.06f;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(chosen, group.transform);
                inst.name = $"{chosen.name}_{placed:000}";
                inst.transform.SetPositionAndRotation(
                    new Vector3(wx, wy, wz),
                    Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 8f - 4f));
                float sc = 0.72f + (float)rng.NextDouble() * 0.70f;
                inst.transform.localScale = new Vector3(sc, sc * (0.85f + (float)rng.NextDouble() * 0.3f), sc);
                inst.isStatic = true;
                placed++;
            }
            Debug.Log($"[MarsTerrainGenerator] Placed {5 + placed} low-poly rocks across MarsPlayTerrain.");
        }

        private static GameObject CreateRockPrefab(string name, float radiusXZ, float heightY, uint seed, bool withCollider, Material rockMat)
        {
            string meshPath = $"{MeshDir}/Mesh_{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = $"Mesh_{name}" };

            // Perturbed octahedron/icosahedron flat-shaded faceted rock
            var baseVerts = new List<Vector3>();
            var tris = new List<int>();
            AddOctaSphereTriangles(baseVerts, tris);

            var rng = new System.Random((int)seed);
            // Perturb shared positions deterministically by quantizing key
            var dispMap = new Dictionary<Vector3Int, Vector3>();
            float minY = float.MaxValue;
            for (int i = 0; i < baseVerts.Count; i++)
            {
                Vector3 v = baseVerts[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 100f), Mathf.RoundToInt(v.y * 100f), Mathf.RoundToInt(v.z * 100f));
                if (!dispMap.TryGetValue(key, out Vector3 d))
                {
                    float radial = 0.72f + (float)rng.NextDouble() * 0.56f;
                    d = new Vector3(v.x * radiusXZ * radial, v.y * heightY * radial, v.z * radiusXZ * radial);
                    if (v.y < 0f) d.y *= 0.48f; // Flatten base into Martian sand
                    dispMap[key] = d;
                }
                baseVerts[i] = d;
                if (d.y < minY) minY = d.y;
            }
            for (int i = 0; i < baseVerts.Count; i++)
                baseVerts[i] = new Vector3(baseVerts[i].x, baseVerts[i].y - minY, baseVerts[i].z);

            var uvs = new Vector2[baseVerts.Count];
            for (int i = 0; i < baseVerts.Count; i++)
                uvs[i] = new Vector2(baseVerts[i].x * 0.5f + 0.5f, baseVerts[i].z * 0.5f + 0.5f);

            mesh.Clear();
            mesh.SetVertices(baseVerts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, meshPath);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rockMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.isStatic = true;

            if (withCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = mesh.bounds.center;
                col.size = mesh.bounds.size * 0.88f;
            }

            string prefabPath = $"{PrefabsDir}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static void AddOctaSphereTriangles(List<Vector3> verts, List<int> tris)
        {
            Vector3[] corners =
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
            };
            int[][] faces =
            {
                new[]{0,4,3}, new[]{0,3,5}, new[]{0,5,2}, new[]{0,2,4},
                new[]{1,3,4}, new[]{1,5,3}, new[]{1,2,5}, new[]{1,4,2}
            };
            for (int f = 0; f < faces.Length; f++)
            {
                Vector3 a = corners[faces[f][0]];
                Vector3 b = corners[faces[f][1]];
                Vector3 c = corners[faces[f][2]];
                Vector3 ab = ((a + b) * 0.5f).normalized;
                Vector3 bc = ((b + c) * 0.5f).normalized;
                Vector3 ca = ((c + a) * 0.5f).normalized;

                AddFlatTri(verts, tris, a, ab, ca);
                AddFlatTri(verts, tris, ab, b, bc);
                AddFlatTri(verts, tris, ca, bc, c);
                AddFlatTri(verts, tris, ab, bc, ca);
            }
        }

        private static void AddFlatTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        private static void ConfigureSkyAndFog(Transform surfaceRoot)
        {
            string skyPath = $"{PsxDir}/M_MarsSkyGradient.mat";
            Shader sh = Shader.Find("Outpost/PSXSkyGradient");
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (skyMat == null)
            {
                skyMat = new Material(sh);
                AssetDatabase.CreateAsset(skyMat, skyPath);
            }
            skyMat.shader = sh;
            skyMat.SetColor("_HorizonColor", ClearMarsHorizon);
            skyMat.SetColor("_MidColor", ClearMarsMidSky);
            skyMat.SetColor("_ZenithColor", ClearMarsZenith);
            skyMat.SetColor("_GroundColor", ClearMarsGround);
            skyMat.SetColor("_SunColor", new Color(1.0f, 0.88f, 0.74f, 1f));
            skyMat.SetFloat("_SunSize", 0.012f);
            skyMat.SetFloat("_SunGlow", 0.085f);

            var sunLight = Object.FindAnyObjectByType<Light>();
            Vector3 sunDir = new Vector3(-0.36f, 0.30f, -0.88f).normalized;
            var marsSunObj = surfaceRoot.Find("MarsSunLight");
            if (marsSunObj == null)
            {
                var sunGo = GameObject.Find("MarsSunLight");
                if (sunGo != null) marsSunObj = sunGo.transform;
            }
            if (marsSunObj != null)
            {
                sunDir = (-marsSunObj.forward).normalized;
                var sl = marsSunObj.GetComponent<Light>();
                if (sl != null)
                {
                    sl.shadows = LightShadows.None;
                    sl.color = new Color(1.00f, 0.78f, 0.62f, 1f);
                }
            }
            skyMat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            EditorUtility.SetDirty(skyMat);

            RenderSettings.skybox = skyMat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = ClearMarsHorizon;
            RenderSettings.fogDensity = 0.0085f;

            // Push globals immediately so EditMode view renders fog identically to PlayMode
            Shader.SetGlobalVector("_PSX_FogColor", ClearMarsHorizon);
            Shader.SetGlobalFloat("_PSX_FogDensity", 0.0085f);
            DynamicGI.UpdateEnvironment();
        }

        private static void DisableLegacySurfaceQuads(Transform surfaceRoot)
        {
            string[] legacyNames = { "Mars_TerrainPlateau", "Mars_HorizonBackdrop", "Mars_TinySun", "MarsSkyDome", "Mars_SkyDome" };
            foreach (string n in legacyNames)
            {
                var c = surfaceRoot.Find(n);
                if (c == null)
                {
                    var found = GameObject.Find(n);
                    if (found != null) c = found.transform;
                }
                if (c != null)
                    c.gameObject.SetActive(false);
            }

            // Ensure dust storm particle system stays active & wired
            var dust = surfaceRoot.Find("SurfaceDustParticles");
            if (dust != null && !dust.gameObject.activeSelf)
                dust.gameObject.SetActive(true);
        }
    }
}
