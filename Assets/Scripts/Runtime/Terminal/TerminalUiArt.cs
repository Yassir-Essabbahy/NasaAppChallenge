using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    public enum TerminalIconId
    {
        Battery,
        Oxygen,
        Water,
        Food,
        Shield,
        Heart,
        Signal,
        Crew,
        Log,
        Sources,
        Help,
        Helios,
        MoodGood,
        MoodWarn,
        MoodCrit,
        ArrowUp,
        ArrowDown,
        ArrowRight,
        Check,
        PlusBox,
        Bolt,
        Sliders,
        Scan,
        Target,
        Lock,
        Warning,
        Gauge,
        NextHex,
        Hazard,
        Goal,
        Home,
        CheckBox,
        Calendar,
        Clock,
        PlayArrow
    }

    /// <summary>
    /// Procedural UI sprite and icon generator plus IconLibrary lookup for crisp 128px terminal icons.
    /// </summary>
    public static class TerminalUiArt
    {
        private static Sprite _roundedRect8px;
        private static Sprite _dropShadow;
        private static Sprite _marsWallpaper;
        private static Sprite _nasaBadge;
        private static UITheme _activeTheme;
        private static IconLibrary _fallbackLibrary;
        private static readonly Sprite[] Icons = new Sprite[40];

        public static string GetIconKey(TerminalIconId id)
        {
            switch (id)
            {
                case TerminalIconId.PlusBox:
                case TerminalIconId.Crew:
                case TerminalIconId.Heart: return "Icon_PlusBox";
                case TerminalIconId.Shield: return "Icon_Shield";
                case TerminalIconId.Bolt: return "Icon_Bolt";
                case TerminalIconId.Sliders: return "Icon_Sliders";
                case TerminalIconId.Scan: return "Icon_Scan";
                case TerminalIconId.Target: return "Icon_Target";
                case TerminalIconId.Lock: return "Icon_Lock";
                case TerminalIconId.Warning:
                case TerminalIconId.MoodWarn: return "Icon_Warning";
                case TerminalIconId.Battery: return "Icon_Battery";
                case TerminalIconId.Gauge: return "Icon_Gauge";
                case TerminalIconId.NextHex:
                case TerminalIconId.ArrowRight: return "Icon_NextHex";
                case TerminalIconId.Hazard:
                case TerminalIconId.MoodCrit: return "Icon_Hazard";
                case TerminalIconId.Oxygen: return "Icon_Oxygen";
                case TerminalIconId.Water: return "Icon_Water";
                case TerminalIconId.Food: return "Icon_Food";
                case TerminalIconId.Signal: return "Icon_Signal";
                case TerminalIconId.Log: return "Icon_Log";
                case TerminalIconId.Sources: return "Icon_Sources";
                case TerminalIconId.Help: return "Icon_Help";
                case TerminalIconId.Goal: return "Icon_Goal";
                default: return null;
            }
        }

        public static Sprite GetIcon(string iconId)
        {
            if (string.IsNullOrEmpty(iconId)) return null;
            if (_activeTheme != null && _activeTheme.IconLibrary != null)
            {
                var fromActive = _activeTheme.IconLibrary.GetSprite(iconId);
                if (fromActive != null) return fromActive;
            }
            if (_fallbackLibrary == null)
                _fallbackLibrary = Resources.Load<IconLibrary>("IconLibrary");
            if (_fallbackLibrary != null)
            {
                var fromRes = _fallbackLibrary.GetSprite(iconId);
                if (fromRes != null) return fromRes;
            }
            return Resources.Load<Sprite>($"UI/Icons/{iconId}");
        }

        public static void SetActiveTheme(UITheme theme)
        {
            _activeTheme = theme;
        }

        public static Sprite GetRoundedRect8px()
        {
            if (_roundedRect8px != null) return _roundedRect8px;
            const int size = 32;
            const int r = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_UI_Rounded8px",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x < r ? (r - x - 0.5f) : (x >= size - r ? (x - (size - r) + 0.5f) : 0f);
                    float dy = y < r ? (r - y - 0.5f) : (y >= size - r ? (y - (size - r) + 0.5f) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = dx > 0f && dy > 0f ? Mathf.Clamp01(r - dist + 0.6f) : 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            _roundedRect8px = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedRect8px;
        }

        public static Sprite GetDropShadowSprite()
        {
            if (_dropShadow != null) return _dropShadow;
            const int size = 48;
            const int pad = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_UI_DropShadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x < pad ? (pad - x) / (float)pad : (x >= size - pad ? (x - (size - pad) + 1) / (float)pad : 0f);
                    float dy = y < pad ? (pad - y) / (float)pad : (y >= size - pad ? (y - (size - pad) + 1) / (float)pad : 0f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2.1f) * 0.55f;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply(false, false);
            _dropShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(pad, pad, pad, pad));
            return _dropShadow;
        }

        public static Sprite GetMarsWallpaperSprite(UITheme theme = null)
        {
            if (theme != null && theme.MarsWallpaperSprite != null)
                return theme.MarsWallpaperSprite;

            if (_marsWallpaper != null) return _marsWallpaper;

            var custom = Resources.Load<Sprite>("UI/MarsWallpaper");
            if (custom != null)
            {
                _marsWallpaper = custom;
                return _marsWallpaper;
            }

            var tex = BuildMarsWallpaperTexture();
            _marsWallpaper = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _marsWallpaper;
        }

        public static Texture2D BuildMarsWallpaperTexture()
        {
            const int w = 384;
            const int h = 216;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "T_MarsWallpaper",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color skyTop = new Color(0.06f, 0.09f, 0.17f, 1f);
            Color skyMid = new Color(0.11f, 0.16f, 0.27f, 1f);
            Color skyHorizon = new Color(0.38f, 0.21f, 0.22f, 1f);
            Color duneBack = new Color(0.24f, 0.12f, 0.11f, 1f);
            Color duneFront = new Color(0.14f, 0.08f, 0.09f, 1f);
            Vector2 sunPos = new Vector2(w * 0.72f, h * 0.44f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)(h - 1);
                float horizonY = 0.28f;

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);
                    float ridge1 = horizonY + 0.035f * Mathf.Sin(nx * 5.2f + 0.4f) + 0.015f * Mathf.Cos(nx * 11f);
                    float ridge2 = horizonY - 0.055f + 0.025f * Mathf.Sin(nx * 4.1f + 2.1f);

                    Color col;
                    if (ny < ridge2)
                    {
                        col = Color.Lerp(duneFront * 0.85f, duneFront, ny / Mathf.Max(0.01f, ridge2));
                    }
                    else if (ny < ridge1)
                    {
                        col = Color.Lerp(duneBack * 0.85f, duneBack, (ny - ridge2) / Mathf.Max(0.01f, ridge1 - ridge2));
                    }
                    else
                    {
                        float skyT = Mathf.Clamp01((ny - ridge1) / (1f - ridge1));
                        col = skyT < 0.35f
                            ? Color.Lerp(skyHorizon, skyMid, skyT / 0.35f)
                            : Color.Lerp(skyMid, skyTop, (skyT - 0.35f) / 0.65f);

                        // Subtle stars high in the Martian dusk sky
                        if (skyT > 0.45f)
                        {
                            float starHash = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
                            starHash -= Mathf.Floor(starHash);
                            if (starHash > 0.9965f)
                                col += new Color(0.45f, 0.50f, 0.62f, 0f) * skyT;
                        }

                        // Tiny sun disc on purpose ("the sun is far away")
                        float distSun = Vector2.Distance(new Vector2(x, y), sunPos);
                        if (distSun < 4.8f)
                        {
                            float disc = Mathf.Clamp01((4.8f - distSun) * 0.6f);
                            col = Color.Lerp(col, new Color(1.00f, 0.92f, 0.78f, 1f), disc);
                        }
                        else if (distSun < 22f)
                        {
                            float halo = Mathf.Pow(1f - (distSun - 4.8f) / 17.2f, 2f) * 0.25f;
                            col += new Color(0.95f, 0.55f, 0.32f, 0f) * halo;
                        }
                    }

                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite GetAppIcon(TerminalAppId appId)
        {
            switch (appId)
            {
                case TerminalAppId.PowerGrid: return GetIcon(TerminalIconId.Bolt);
                case TerminalAppId.LifeSupport: return GetIcon(TerminalIconId.Gauge);
                case TerminalAppId.Greenhouse: return GetIcon(TerminalIconId.Food);
                case TerminalAppId.Radiation: return GetIcon(TerminalIconId.Shield);
                case TerminalAppId.Crew: return GetIcon(TerminalIconId.PlusBox);
                case TerminalAppId.Comms: return GetIcon(TerminalIconId.Signal);
                case TerminalAppId.CommandersLog: return GetIcon(TerminalIconId.Log);
                case TerminalAppId.Sources: return GetIcon(TerminalIconId.Sources);
                default: return GetIcon(TerminalIconId.Help);
            }
        }

        public static Sprite GetIcon(TerminalIconId id)
        {
            if (_activeTheme != null)
            {
                var themed = _activeTheme.GetIconSprite(id);
                if (themed != null) return themed;
            }

            string libKey = GetIconKey(id);
            if (!string.IsNullOrEmpty(libKey))
            {
                var libSprite = GetIcon(libKey);
                if (libSprite != null) return libSprite;
            }

            int idx = (int)id;
            if (idx >= 0 && idx < Icons.Length && Icons[idx] != null)
                return Icons[idx];

            // Allow artist override in Assets/Resources/UI/Icons/<Name>.png
            var custom = Resources.Load<Sprite>($"UI/Icons/{id}");
            if (custom != null)
            {
                Icons[idx] = custom;
                return custom;
            }

            var tex = BuildIconTexture(id, 48);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 100f);
            Icons[idx] = sprite;
            return sprite;
        }

        public static Texture2D BuildIconTexture(TerminalIconId id, int s = 48)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = $"Icon_{id}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            void Plot(int x, int y, float alpha = 1f)
            {
                if (x < 0 || x >= s || y < 0 || y >= s) return;
                int idx = y * s + x;
                px[idx] = new Color(1f, 1f, 1f, Mathf.Max(px[idx].a, Mathf.Clamp01(alpha)));
            }

            void FillRect(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Plot(x, y, 1f);
            }

            void FillCircle(float cx, float cy, float rad)
            {
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d <= rad + 0.5f)
                            Plot(x, y, Mathf.Clamp01(rad - d + 0.7f));
                    }
                }
            }

            void Ring(float cx, float cy, float rad, float thick)
            {
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - rad);
                        if (d <= thick)
                            Plot(x, y, Mathf.Clamp01(thick - d + 0.6f));
                    }
                }
            }

            void Line(float x0, float y0, float x1, float y1, float thick)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    FillCircle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thick);
                }
            }

            switch (id)
            {
                case TerminalIconId.Battery:
                    FillRect(14, 8, 34, 36);
                    FillRect(19, 36, 29, 41);
                    // Carve inner bolt cutout
                    for (int y = 12; y <= 32; y++)
                        for (int x = 18; x <= 30; x++)
                            px[y * s + x] = new Color(1f, 1f, 1f, 0.25f);
                    Line(26, 31, 20, 22, 2.0f);
                    Line(20, 22, 27, 22, 2.0f);
                    Line(27, 22, 22, 13, 2.0f);
                    break;

                case TerminalIconId.Oxygen:
                    FillCircle(18, 28, 8.5f);
                    FillCircle(31, 20, 6.5f);
                    FillCircle(25, 11, 4.0f);
                    FillCircle(33, 35, 4.5f);
                    break;

                case TerminalIconId.Water:
                    for (int y = 8; y <= 39; y++)
                    {
                        float t = (y - 8f) / 31f;
                        float halfW = t < 0.35f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.35f) / 0.35f, 2f))) * 12f : (1f - (t - 0.35f) / 0.65f) * 12f;
                        for (int x = 0; x < s; x++)
                            if (Mathf.Abs(x - 24f) <= halfW)
                                Plot(x, y, 1f);
                    }
                    break;

                case TerminalIconId.Food:
                    // Crisp leaf + stem
                    for (int y = 10; y <= 38; y++)
                    {
                        for (int x = 10; x <= 38; x++)
                        {
                            float dx = (x - 24f) / 13f;
                            float dy = (y - 25f) / 14f;
                            if (dx * dx + dy * dy <= 1f && x + y >= 32)
                                Plot(x, y, 1f);
                        }
                    }
                    Line(13, 12, 30, 30, 1.5f);
                    break;

                case TerminalIconId.Shield:
                    for (int y = 8; y <= 39; y++)
                    {
                        float t = (y - 8f) / 31f;
                        float halfW = t < 0.55f ? Mathf.Lerp(2f, 14f, t / 0.55f) : 14f;
                        for (int x = 0; x < s; x++)
                            if (Mathf.Abs(x - 24f) <= halfW)
                                Plot(x, y, 1f);
                    }
                    break;

                case TerminalIconId.Heart:
                    FillCircle(17, 29, 8.5f);
                    FillCircle(31, 29, 8.5f);
                    for (int y = 9; y <= 28; y++)
                    {
                        float halfW = (y - 9f) / 19f * 15f;
                        for (int x = 0; x < s; x++)
                            if (Mathf.Abs(x - 24f) <= halfW)
                                Plot(x, y, 1f);
                    }
                    break;

                case TerminalIconId.Signal:
                    FillCircle(13, 13, 4f);
                    Ring(13, 13, 12f, 1.9f);
                    Ring(13, 13, 21f, 1.9f);
                    Ring(13, 13, 30f, 1.9f);
                    // Keep upper-right quadrant only
                    for (int y = 0; y < s; y++)
                        for (int x = 0; x < s; x++)
                            if (x < 10 || y < 10) px[y * s + x] = clear;
                    FillCircle(14, 14, 4.2f);
                    break;

                case TerminalIconId.Crew:
                    FillCircle(24, 31, 8.5f);
                    for (int y = 8; y <= 21; y++)
                    {
                        float t = (y - 8f) / 13f;
                        float hw = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * 15f;
                        for (int x = 0; x < s; x++)
                            if (Mathf.Abs(x - 24f) <= hw) Plot(x, y, 1f);
                    }
                    break;

                case TerminalIconId.Log:
                    FillRect(11, 8, 37, 39);
                    for (int y = 13; y <= 34; y += 6)
                        for (int x = 16; x <= 32; x++)
                            px[y * s + x] = new Color(1f, 1f, 1f, 0.3f);
                    break;

                case TerminalIconId.Sources:
                    Ring(24, 24, 15f, 2.2f);
                    Line(9, 24, 39, 24, 1.4f);
                    Line(24, 9, 24, 39, 1.4f);
                    break;

                case TerminalIconId.Help:
                    Ring(24, 24, 17f, 2.5f);
                    Ring(24, 29, 5f, 2.0f);
                    FillRect(17, 23, 25, 29);
                    // Clean question mark curve
                    for (int i = 0; i < px.Length; i++) px[i] = clear;
                    Ring(24, 24, 18f, 2.4f);
                    Line(20, 31, 27, 31, 2.0f);
                    Line(27, 31, 24, 22, 2.0f);
                    FillCircle(24, 15, 2.6f);
                    break;

                case TerminalIconId.Helios:
                    FillRect(10, 12, 38, 35);
                    FillRect(22, 35, 26, 42);
                    FillCircle(24, 42, 3.2f);
                    // Eyes
                    for (int y = 24; y <= 29; y++)
                        for (int x = 16; x <= 20; x++) px[y * s + x] = new Color(0.1f, 0.2f, 0.3f, 1f);
                    for (int y = 24; y <= 29; y++)
                        for (int x = 28; x <= 32; x++) px[y * s + x] = new Color(0.1f, 0.2f, 0.3f, 1f);
                    // Smile
                    for (int x = 18; x <= 30; x++) px[18 * s + x] = new Color(0.1f, 0.2f, 0.3f, 1f);
                    break;

                case TerminalIconId.MoodGood:
                    Ring(24, 24, 17f, 2.2f);
                    FillCircle(18, 28, 2.4f);
                    FillCircle(30, 28, 2.4f);
                    Line(17, 19, 24, 15, 1.8f);
                    Line(24, 15, 31, 19, 1.8f);
                    break;

                case TerminalIconId.MoodWarn:
                    Ring(24, 24, 17f, 2.2f);
                    FillCircle(18, 28, 2.4f);
                    FillCircle(30, 28, 2.4f);
                    Line(17, 17, 31, 17, 1.8f);
                    break;

                case TerminalIconId.MoodCrit:
                    Ring(24, 24, 17f, 2.2f);
                    FillCircle(18, 28, 2.4f);
                    FillCircle(30, 28, 2.4f);
                    Line(17, 15, 24, 19, 1.8f);
                    Line(24, 19, 31, 15, 1.8f);
                    break;

                case TerminalIconId.ArrowUp:
                    Line(24, 11, 24, 35, 2.6f);
                    Line(14, 25, 24, 36, 2.6f);
                    Line(34, 25, 24, 36, 2.6f);
                    break;

                case TerminalIconId.ArrowDown:
                    Line(24, 37, 24, 13, 2.6f);
                    Line(14, 23, 24, 12, 2.6f);
                    Line(34, 23, 24, 12, 2.6f);
                    break;

                case TerminalIconId.ArrowRight:
                    Line(11, 24, 36, 24, 2.6f);
                    Line(26, 33, 36, 24, 2.6f);
                    Line(26, 15, 36, 24, 2.6f);
                    break;

                case TerminalIconId.Check:
                    Line(12, 23, 20, 14, 2.8f);
                    Line(20, 14, 36, 33, 2.8f);
                    break;

                case TerminalIconId.Home:
                    Line(8, 25, 24, 39, 2.6f);
                    Line(24, 39, 40, 25, 2.6f);
                    Line(13, 27, 13, 10, 2.6f);
                    Line(35, 27, 35, 10, 2.6f);
                    Line(13, 10, 20, 10, 2.6f);
                    Line(28, 10, 35, 10, 2.6f);
                    Line(20, 10, 20, 20, 2.4f);
                    Line(28, 10, 28, 20, 2.4f);
                    Line(20, 20, 28, 20, 2.4f);
                    break;

                case TerminalIconId.CheckBox:
                    Line(11, 11, 37, 11, 2.2f);
                    Line(11, 37, 37, 37, 2.2f);
                    Line(11, 11, 11, 37, 2.2f);
                    Line(37, 11, 37, 37, 2.2f);
                    break;

                case TerminalIconId.Calendar:
                    Line(10, 10, 38, 10, 2.2f);
                    Line(10, 34, 38, 34, 2.2f);
                    Line(10, 10, 10, 34, 2.2f);
                    Line(38, 10, 38, 34, 2.2f);
                    Line(10, 27, 38, 27, 2.0f);
                    Line(17, 34, 17, 39, 2.4f);
                    Line(31, 34, 31, 39, 2.4f);
                    break;

                case TerminalIconId.Clock:
                    Ring(24, 24, 15.5f, 2.2f);
                    Line(24, 24, 24, 33, 2.2f);
                    Line(24, 24, 31, 24, 2.2f);
                    break;

                case TerminalIconId.PlayArrow:
                    for (int x = 14; x <= 36; x++)
                    {
                        float t = (x - 14f) / 22f;
                        float halfH = (1f - t) * 14f;
                        for (int y = 0; y < s; y++)
                        {
                            float dy = Mathf.Abs(y - 24f);
                            if (dy <= halfH + 0.6f)
                                Plot(x, y, Mathf.Clamp01(halfH - dy + 0.8f));
                        }
                    }
                    break;
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite GetNasaBadgeSprite()
        {
            if (_nasaBadge != null) return _nasaBadge;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "T_UI_NasaBadge",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color bg = new Color(0.055f, 0.245f, 0.56f, 1f);
            Color swoosh = new Color(0.92f, 0.24f, 0.20f, 1f);
            Color orbit = new Color(0.92f, 0.96f, 1.0f, 0.92f);
            Color clear = new Color(0f, 0f, 0f, 0f);
            float c = (s - 1) * 0.5f;
            float r = 29f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r + 1f)
                    {
                        tex.SetPixel(x, y, clear);
                        continue;
                    }
                    float edgeA = Mathf.Clamp01(r + 0.7f - d);
                    Color col = bg;
                    // Diagonal red vector chevron
                    float lineY1 = 0.55f * dx - 2f;
                    float lineY2 = -0.35f * dx + 3f;
                    if ((Mathf.Abs(dy - lineY1) < 2.3f && dx > -16f && dx < 18f) ||
                        (Mathf.Abs(dy - lineY2) < 2.1f && dx > -10f && dx < 18f))
                    {
                        col = swoosh;
                    }
                    // Subtle elliptical white orbital ring
                    float ox = dx * 0.85f + dy * 0.45f;
                    float oy = -dx * 0.45f + dy * 0.85f;
                    float ringD = Mathf.Abs(Mathf.Sqrt((ox * ox) / (23f * 23f) + (oy * oy) / (9.5f * 9.5f)) - 1f);
                    if (ringD < 0.085f)
                        col = Color.Lerp(col, orbit, 0.85f);

                    col.a = edgeA;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(false, false);
            _nasaBadge = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _nasaBadge;
        }

        public static Sprite GetIcon(TerminalIconId id, UITheme theme)
        {
            if (theme != null)
            {
                var s = theme.GetIconSprite(id);
                if (s != null) return s;
            }
            return GetIcon(id);
        }
    }
}
