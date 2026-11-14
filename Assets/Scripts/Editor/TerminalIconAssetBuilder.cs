using System;
using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Imports the 12 Neon Command UI Kit 128px PNGs into Assets/Art/UI/Icons/Icon_*.png,
    /// procedurally generates 8 matching 128px line icons (6px round-cap stroke in #45E3D3/#7FDFFF/#F7C948),
    /// creates IconLibrary ScriptableObject, and updates UITheme assets.
    /// </summary>
    public static class TerminalIconAssetBuilder
    {
        public const string SamplerDir = "Assets/Models/neon-command-ui-kit-free-sampler-default/png/128x128";
        public const string IconsOutDir = "Assets/Art/UI/Icons";
        public const string IconLibPath = "Assets/Art/UI/Icons/IconLibrary.asset";
        public const string ResourcesIconLibPath = "Assets/Resources/IconLibrary.asset";

        private static readonly Color ColTeal = new Color(0.271f, 0.890f, 0.827f, 1f);      // #45E3D3
        private static readonly Color ColLightBlue = new Color(0.498f, 0.875f, 1.000f, 1f); // #7FDFFF
        private static readonly Color ColYellow = new Color(0.969f, 0.788f, 0.282f, 1f);    // #F7C948
        private static readonly Color ColCoral = new Color(1.000f, 0.420f, 0.420f, 1f);     // #FF6B6B

        private static readonly (string srcFile, string outId)[] SamplerMap =
        {
            ("health.png", "Icon_PlusBox"),
            ("shield.png", "Icon_Shield"),
            ("energy.png", "Icon_Bolt"),
            ("ammo.png", "Icon_Sliders"),
            ("scan.png", "Icon_Scan"),
            ("target.png", "Icon_Target"),
            ("lock.png", "Icon_Lock"),
            ("alert.png", "Icon_Warning"),
            ("progress-bar.png", "Icon_Battery"),
            ("circular-gauge.png", "Icon_Gauge"),
            ("button-primary.png", "Icon_NextHex"),
            ("crosshair.png", "Icon_Hazard")
        };

        private static readonly string[] GeneratedIds =
        {
            "Icon_Oxygen",
            "Icon_Water",
            "Icon_Food",
            "Icon_Signal",
            "Icon_Log",
            "Icon_Sources",
            "Icon_Help",
            "Icon_Goal"
        };

        [MenuItem("Outpost/Build UI Icon Library")]
        public static IconLibrary BuildIconLibraryAndUpdateTheme()
        {
            if (!Directory.Exists(IconsOutDir)) Directory.CreateDirectory(IconsOutDir);
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");

            // 1. Copy the 12 Neon Command PNGs
            foreach (var (srcFile, outId) in SamplerMap)
            {
                string srcPath = $"{SamplerDir}/{srcFile}";
                string dstPath = $"{IconsOutDir}/{outId}.png";
                if (File.Exists(srcPath))
                {
                    File.Copy(srcPath, dstPath, overwrite: true);
                }
                else
                {
                    Debug.LogWarning($"[TerminalIconAssetBuilder] Missing sampler icon at {srcPath}");
                }
            }

            // 2. Generate the 8 matching 128px SDF vector-style PNGs
            GenerateAndWriteIcon("Icon_Oxygen", DrawOxygen);
            GenerateAndWriteIcon("Icon_Water", DrawWater);
            GenerateAndWriteIcon("Icon_Food", DrawFood);
            GenerateAndWriteIcon("Icon_Signal", DrawSignal);
            GenerateAndWriteIcon("Icon_Log", DrawLog);
            GenerateAndWriteIcon("Icon_Sources", DrawSources);
            GenerateAndWriteIcon("Icon_Help", DrawHelp);
            GenerateAndWriteIcon("Icon_Goal", DrawGoal);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 3. Enforce Sprite import settings on all 20 icons
            var allIds = new List<string>();
            foreach (var (_, outId) in SamplerMap) allIds.Add(outId);
            allIds.AddRange(GeneratedIds);

            var entries = new List<IconLibrary.Entry>();
            foreach (string id in allIds)
            {
                string p = $"{IconsOutDir}/{id}.png";
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.filterMode = FilterMode.Bilinear;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.maxTextureSize = 256;
                    imp.SaveAndReimport();
                }

                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (spr != null)
                    entries.Add(new IconLibrary.Entry { id = id, sprite = spr });
            }

            // 4. Write IconLibrary in Assets/Art/UI/Icons/ and Resources/IconLibrary.asset
            var lib = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconLibPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(lib, IconLibPath);
            }
            lib.SetEntries(entries);
            EditorUtility.SetDirty(lib);

            var resLib = AssetDatabase.LoadAssetAtPath<IconLibrary>(ResourcesIconLibPath);
            if (resLib == null)
            {
                resLib = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(resLib, ResourcesIconLibPath);
            }
            resLib.SetEntries(entries);
            EditorUtility.SetDirty(resLib);

            // 5. Update UITheme assets with IconLibrary + Icon palette
            UpdateThemeAsset("Assets/Art/UI/TerminalTheme.asset", lib);
            UpdateThemeAsset("Assets/Resources/TerminalTheme.asset", resLib);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TerminalIconAssetBuilder] Built IconLibrary with {entries.Count}/20 crisp 128px icons and updated UITheme.");
            return resLib;
        }

        private static void UpdateThemeAsset(string path, IconLibrary lib)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
            if (theme == null)
                return;

            theme.ApplyFriendlyThemeDefaults();
            theme.IconLibrary = lib;
            var values = Enum.GetValues(typeof(TerminalIconId));
            theme.IconSprites = new Sprite[values.Length];
            foreach (TerminalIconId tid in values)
            {
                string key = TerminalUiArt.GetIconKey(tid);
                if (!string.IsNullOrEmpty(key))
                    theme.IconSprites[(int)tid] = lib.GetSprite(key);
            }
            EditorUtility.SetDirty(theme);
        }

        private static void GenerateAndWriteIcon(string iconId, Action<Color[]> painter)
        {
            const int S = 128;
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);
            painter(px);

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply(false, false);
            File.WriteAllBytes($"{IconsOutDir}/{iconId}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // --- SDF Rasterizers (128x128 grid, ~6px line width, round caps) ---

        private static void Stamp(Color[] px, float dist, float radius, Color col)
        {
            float a = Mathf.Clamp01((radius + 0.85f - dist) / 1.7f) * col.a;
            if (a <= 0.002f) return;
            // Handled per-pixel in helpers below; kept for reference.
        }

        private static void BlendPixel(Color[] px, int x, int y, Color col, float alpha)
        {
            if (x < 0 || x >= 128 || y < 0 || y >= 128 || alpha <= 0.002f) return;
            int idx = y * 128 + x;
            Color dst = px[idx];
            float outA = alpha + dst.a * (1f - alpha);
            if (outA <= 0.0001f) return;
            Color rgb = (col * alpha + dst * dst.a * (1f - alpha)) / outA;
            px[idx] = new Color(rgb.r, rgb.g, rgb.b, outA);
        }

        private static void DrawSegment(Color[] px, Vector2 a, Vector2 b, float halfWidth, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfWidth - 3f));
            int x1 = Mathf.Min(127, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfWidth + 3f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfWidth - 3f));
            int y1 = Mathf.Min(127, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfWidth + 3f));
            Vector2 ab = b - a;
            float abLenSq = Mathf.Max(ab.sqrMagnitude, 0.0001f);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
                    float d = Vector2.Distance(p, a + ab * t);
                    float alpha = Mathf.Clamp01((halfWidth + 0.85f - d) / 1.7f) * col.a;
                    if (alpha > 0.002f) BlendPixel(px, x, y, col, alpha);
                }
            }
        }

        private static void DrawRing(Color[] px, Vector2 center, float r, float halfWidth, Color col, float degStart = 0f, float degEnd = 360f)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - r - halfWidth - 3f));
            int x1 = Mathf.Min(127, Mathf.CeilToInt(center.x + r + halfWidth + 3f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - r - halfWidth - 3f));
            int y1 = Mathf.Min(127, Mathf.CeilToInt(center.y + r + halfWidth + 3f));
            bool full = Mathf.Abs(degEnd - degStart) >= 359.5f;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float distR = Mathf.Abs(p.magnitude - r);
                    float d = distR;
                    if (!full)
                    {
                        float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                        if (ang < 0f) ang += 360f;
                        if (!AngleInRange(ang, degStart, degEnd))
                        {
                            Vector2 e1 = new Vector2(Mathf.Cos(degStart * Mathf.Deg2Rad), Mathf.Sin(degStart * Mathf.Deg2Rad)) * r;
                            Vector2 e2 = new Vector2(Mathf.Cos(degEnd * Mathf.Deg2Rad), Mathf.Sin(degEnd * Mathf.Deg2Rad)) * r;
                            d = Mathf.Min(Vector2.Distance(p, e1), Vector2.Distance(p, e2));
                        }
                    }
                    float alpha = Mathf.Clamp01((halfWidth + 0.85f - d) / 1.7f) * col.a;
                    if (alpha > 0.002f) BlendPixel(px, x, y, col, alpha);
                }
            }
        }

        private static void DrawDisc(Color[] px, Vector2 center, float r, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - r - 2f));
            int x1 = Mathf.Min(127, Mathf.CeilToInt(center.x + r + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - r - 2f));
            int y1 = Mathf.Min(127, Mathf.CeilToInt(center.y + r + 2f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((r + 0.85f - d) / 1.7f) * col.a;
                    if (alpha > 0.002f) BlendPixel(px, x, y, col, alpha);
                }
            }
        }

        private static void DrawRoundedRectOutline(Color[] px, Vector2 center, Vector2 halfSize, float cornerR, float halfStroke, Color col)
        {
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    Vector2 q = new Vector2(Mathf.Abs(x + 0.5f - center.x), Mathf.Abs(y + 0.5f - center.y)) - (halfSize - new Vector2(cornerR, cornerR));
                    float sdf = (new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f))).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - cornerR;
                    float d = Mathf.Abs(sdf);
                    float alpha = Mathf.Clamp01((halfStroke + 0.85f - d) / 1.7f) * col.a;
                    if (alpha > 0.002f) BlendPixel(px, x, y, col, alpha);
                }
            }
        }

        private static bool AngleInRange(float a, float start, float end)
        {
            start = (start % 360f + 360f) % 360f;
            end = (end % 360f + 360f) % 360f;
            if (start <= end) return a >= start && a <= end;
            return a >= start || a <= end;
        }

        // --- 8 Specific Icon Painters ---

        private static void DrawOxygen(Color[] px)
        {
            // Ascending O2 bubbles: large primary bubble + two lighter rising bubbles + highlight arc
            DrawRing(px, new Vector2(46f, 48f), 25f, 3.0f, ColTeal);
            DrawRing(px, new Vector2(46f, 48f), 15f, 2.2f, ColLightBlue, 110f, 175f);
            DrawRing(px, new Vector2(88f, 84f), 16f, 3.0f, ColLightBlue);
            DrawRing(px, new Vector2(72f, 28f), 11f, 2.8f, ColTeal);
            DrawDisc(px, new Vector2(88f, 84f), 3.8f, ColTeal);
        }

        private static void DrawWater(Color[] px)
        {
            // Stylized water drop outline in #45E3D3 + specular inner drop curve in #7FDFFF
            DrawRing(px, new Vector2(64f, 46f), 30f, 3.1f, ColTeal, 155f, 385f);
            Vector2 top = new Vector2(64f, 108f);
            DrawSegment(px, new Vector2(36.8f, 58.6f), top, 3.1f, ColTeal);
            DrawSegment(px, new Vector2(91.2f, 58.6f), top, 3.1f, ColTeal);
            DrawRing(px, new Vector2(64f, 45f), 18f, 2.7f, ColLightBlue, 200f, 290f);
        }

        private static void DrawFood(Color[] px)
        {
            // Plant leaf: curved blade outline + central stem & side veins
            DrawRing(px, new Vector2(54f, 70f), 36f, 3.0f, ColTeal, 290f, 55f);
            DrawRing(px, new Vector2(74f, 54f), 36f, 3.0f, ColTeal, 110f, 235f);
            DrawSegment(px, new Vector2(34f, 24f), new Vector2(90f, 94f), 2.8f, ColLightBlue);
            DrawSegment(px, new Vector2(58f, 54f), new Vector2(76f, 52f), 2.5f, ColLightBlue);
            DrawSegment(px, new Vector2(70f, 69f), new Vector2(64f, 84f), 2.5f, ColLightBlue);
        }

        private static void DrawSignal(Color[] px)
        {
            // Antenna mast + glowing tip + symmetric radar signal arcs
            DrawSegment(px, new Vector2(64f, 20f), new Vector2(64f, 74f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(44f, 20f), new Vector2(64f, 46f), 2.8f, ColTeal);
            DrawSegment(px, new Vector2(84f, 20f), new Vector2(64f, 46f), 2.8f, ColTeal);
            DrawDisc(px, new Vector2(64f, 78f), 5.5f, ColYellow);
            DrawRing(px, new Vector2(64f, 78f), 20f, 2.9f, ColLightBlue, 325f, 35f);
            DrawRing(px, new Vector2(64f, 78f), 20f, 2.9f, ColLightBlue, 145f, 215f);
            DrawRing(px, new Vector2(64f, 78f), 34f, 2.9f, ColTeal, 325f, 35f);
            DrawRing(px, new Vector2(64f, 78f), 34f, 2.9f, ColTeal, 145f, 215f);
        }

        private static void DrawLog(Color[] px)
        {
            // Rounded notebook page + left binder rings + horizontal log lines
            DrawRoundedRectOutline(px, new Vector2(68f, 64f), new Vector2(32f, 42f), 8f, 3.0f, ColTeal);
            DrawSegment(px, new Vector2(28f, 86f), new Vector2(40f, 86f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(28f, 64f), new Vector2(40f, 64f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(28f, 42f), new Vector2(40f, 42f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(52f, 82f), new Vector2(84f, 82f), 2.8f, ColLightBlue);
            DrawSegment(px, new Vector2(52f, 64f), new Vector2(84f, 64f), 2.8f, ColLightBlue);
            DrawSegment(px, new Vector2(52f, 46f), new Vector2(74f, 46f), 2.8f, ColYellow);
        }

        private static void DrawSources(Color[] px)
        {
            // Open dual-page reference book + center spine & bookmark accent
            DrawSegment(px, new Vector2(64f, 28f), new Vector2(64f, 92f), 2.9f, ColLightBlue);
            DrawSegment(px, new Vector2(64f, 92f), new Vector2(24f, 98f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(24f, 98f), new Vector2(24f, 36f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(24f, 36f), new Vector2(64f, 28f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(64f, 92f), new Vector2(104f, 98f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(104f, 98f), new Vector2(104f, 36f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(104f, 36f), new Vector2(64f, 28f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(36f, 74f), new Vector2(52f, 72f), 2.5f, ColLightBlue);
            DrawSegment(px, new Vector2(76f, 72f), new Vector2(92f, 74f), 2.5f, ColLightBlue);
            DrawSegment(px, new Vector2(36f, 56f), new Vector2(52f, 54f), 2.5f, ColLightBlue);
            DrawSegment(px, new Vector2(76f, 54f), new Vector2(92f, 56f), 2.5f, ColLightBlue);
        }

        private static void DrawHelp(Color[] px)
        {
            // Rounded badge circle + crisp question mark
            DrawRing(px, new Vector2(64f, 64f), 42f, 3.0f, ColTeal);
            DrawRing(px, new Vector2(64f, 76f), 14f, 3.0f, ColLightBlue, 330f, 180f);
            DrawSegment(px, new Vector2(76f, 69f), new Vector2(64f, 56f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(64f, 56f), new Vector2(64f, 48f), 3.0f, ColLightBlue);
            DrawDisc(px, new Vector2(64f, 35f), 4.2f, ColYellow);
        }

        private static void DrawGoal(Color[] px)
        {
            // Outpost flag: upright pole + waving pennant outline + gold emblem
            DrawSegment(px, new Vector2(34f, 20f), new Vector2(34f, 106f), 3.1f, ColTeal);
            DrawSegment(px, new Vector2(24f, 20f), new Vector2(46f, 20f), 3.1f, ColTeal);
            DrawSegment(px, new Vector2(34f, 104f), new Vector2(98f, 86f), 3.0f, ColTeal);
            DrawSegment(px, new Vector2(98f, 86f), new Vector2(74f, 74f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(74f, 74f), new Vector2(96f, 60f), 3.0f, ColLightBlue);
            DrawSegment(px, new Vector2(96f, 60f), new Vector2(34f, 60f), 3.0f, ColTeal);
            DrawDisc(px, new Vector2(54f, 80f), 5.0f, ColYellow);
        }
    }
}
