using System;
using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Editor
{
    /// <summary>
    /// Modular editor builder helper for Part 2 UI icon PNGs / Glossary asset and Part 3
    /// explorable habitat (rear corridor, medbay, greenhouse, storage, airlock, 60x60 Mars surface + POIs).
    /// Keeps every mesh a named primitive child with swappable materials/prefabs.
    /// </summary>
    public static class OutpostWorldExpansionBuilder
    {
        private const string IconsDir = "Assets/Art/UI/Icons";
        private const string MatsDir = "Assets/Materials/Room";

        public struct WorldBuildResult
        {
            public Transform CorridorSpawn;
            public Transform AirlockSpawn;
            public Transform SurfaceSpawn;
            public AirlockController Airlock;
            public DoorInteractable[] AllDoors;
            public Light[] HabitatLights;
            public Renderer[] CorridorStatusRenderers;
            public TextMeshPro[] CorridorStatusLabels;
            public Transform[] GreenhouseCropTransforms;
            public Renderer[] GreenhouseCropRenderers;
            public Light GreenhouseGrowLight;
            public Renderer MedbayVitalRenderer;
            public Renderer[] SolarPanelRenderers;
            public ParticleSystem SurfaceDustParticles;
            public Renderer SkyDomeRenderer;
            public Light MarsSunLight;
            public Light MedbayOverrideLight;
            public Renderer MedbayOverrideWindow;
        }

        // =========================================================================
        // PART 2: EXPORT UI PNG ICONS / WALLPAPER + GLOSSARY ASSET + PREFAB WIRING
        // =========================================================================

        public static void EnsureFriendlyUiAssetsAndTerminalPrefab(UITheme theme, string terminalPrefabPath)
        {
            if (theme == null) return;

            if (!Directory.Exists(IconsDir))
                Directory.CreateDirectory(IconsDir);

            // Export crisp Mars horizon wallpaper PNG and import as Sprite
            string wallPath = $"{IconsDir}/MarsWallpaper.png";
            var wallTex = TerminalUiArt.BuildMarsWallpaperTexture();
            File.WriteAllBytes(wallPath, wallTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(wallTex);
            AssetDatabase.ImportAsset(wallPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(wallPath, FilterMode.Bilinear);
            theme.MarsWallpaperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(wallPath);

            // Export all 19 48x48 icons into Assets/Art/UI/Icons/ and assign to swappable array on UITheme
            Array iconValues = Enum.GetValues(typeof(TerminalIconId));
            theme.IconSprites = new Sprite[iconValues.Length];
            for (int i = 0; i < iconValues.Length; i++)
            {
                var id = (TerminalIconId)iconValues.GetValue(i);
                string iconPath = $"{IconsDir}/Icon_{id}.png";
                var iconTex = TerminalUiArt.BuildIconTexture(id, 48);
                File.WriteAllBytes(iconPath, iconTex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(iconTex);
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                ConfigureSpriteImporter(iconPath, FilterMode.Bilinear);
                theme.IconSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            }

            theme.ApplyFriendlyThemeDefaults();
            EditorUtility.SetDirty(theme);

            // Create / update GlossaryDatabase.asset in Resources
            const string glossaryPath = "Assets/Resources/GlossaryDatabase.asset";
            var glossary = AssetDatabase.LoadAssetAtPath<GlossaryDatabase>(glossaryPath);
            if (glossary == null)
            {
                glossary = ScriptableObject.CreateInstance<GlossaryDatabase>();
                AssetDatabase.CreateAsset(glossary, glossaryPath);
            }
            glossary.PopulateDefaults();
            EditorUtility.SetDirty(glossary);

            // Ensure PF_TerminalRoot.prefab references updated theme, SourcesDatabase & GlossaryDatabase
            if (!string.IsNullOrEmpty(terminalPrefabPath) && File.Exists(terminalPrefabPath))
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(terminalPrefabPath);
                var tc = prefabRoot.GetComponent<TerminalController>();
                if (tc != null)
                {
                    var so = new SerializedObject(tc);
                    so.FindProperty("theme").objectReferenceValue = theme;
                    so.FindProperty("sourcesDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SourcesDatabase>("Assets/Resources/SourcesDatabase.asset");
                    so.FindProperty("glossaryDatabase").objectReferenceValue = glossary;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(tc);
                }
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, terminalPrefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSpriteImporter(string assetPath, FilterMode filter)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = filter;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // =========================================================================
        // PART 3: EXPLORABLE HABITAT (DOOR, CORRIDOR, BAYS, AIRLOCK, 60x60 MARS)
        // =========================================================================

        public static WorldBuildResult BuildExpandedWorld(
            GameObject roomRoot,
            Transform lightsRoot,
            GameObject wallPrefab,
            GameObject statusLightsPanelPrefab,
            GameObject deskLampHolder,
            TMP_FontAsset hudFont)
        {
            var result = new WorldBuildResult();
            var mats = LoadOrCreateExpansionMaterials();

            // 1. Open Command Module's Rear Wall (Corridor doorway) & Left Wall (Medbay doorway)
            ReplaceRearAndLeftCommandWalls(roomRoot.transform, wallPrefab, mats);

            // Clean up legacy close-range floating Mars_Ground inside PF_Mars_Exterior so the unified Y=-0.08 surface fits smoothly
            var legacyExt = roomRoot.transform.Find("PF_Mars_Exterior");
            if (legacyExt != null)
            {
                DestroyChildIfFound(legacyExt, "Mars_Ground");
                DestroyChildIfFound(legacyExt, "MarsExteriorLight");
                DestroyChildIfFound(legacyExt, "Mars_SkyBackdrop");
                DestroyChildIfFound(legacyExt, "Mars_TinySun");
                DestroyChildIfFound(legacyExt, "FarRidge_Left");
                DestroyChildIfFound(legacyExt, "FarRidge_Right");
            }

            // Parent containers for clean scene hierarchy
            var worldRoot = new GameObject("OutpostExpansionWorld");
            var doorsList = new List<DoorInteractable>();
            var lightsList = new List<Light>();

            // Collect existing Command Module lights
            if (lightsRoot != null)
            {
                foreach (var l in lightsRoot.GetComponentsInChildren<Light>(true))
                {
                    if (l != null && !l.name.Contains("Emergency") && !l.name.Contains("Blackout"))
                        lightsList.Add(l);
                }
            }

            // 2. Replace Command Module static Medbay door with openable Door_Medbay
            var oldMedDoor = roomRoot.transform.Find("PF_MedbayDoor");
            if (oldMedDoor != null) UnityEngine.Object.DestroyImmediate(oldMedDoor.gameObject);

            var medDoor = BuildDoorAssembly(
                worldRoot.transform,
                "Door_Medbay",
                new Vector3(-3.06f, 0f, 1.35f),
                new Vector3(0f, -90f, 0f),
                "door.name.medbay",
                new Vector3(-1.40f, 0f, 0f),
                mats.MedbayDoor,
                mats.Trim,
                mats.BeaconGreen,
                includeMedbayWindow: true);
            doorsList.Add(medDoor);
            result.MedbayOverrideWindow = medDoor.transform.Find("DoorLeaf/MedbayWindow")?.GetComponent<Renderer>();

            // 3. Medbay Room (West of Command Module: X in [-6.80, -3.12], Z in [-0.30, +2.85])
            var medLight = BuildMedbayRoom(worldRoot.transform, wallPrefab, mats, hudFont, out var medVitalRend);
            result.MedbayOverrideLight = medLight;
            result.MedbayVitalRenderer = medVitalRend;
            if (medLight != null) lightsList.Add(medLight);

            // 4. Door from Command Module to Hub Corridor (Z = -2.16)
            var cmdToCorrDoor = BuildDoorAssembly(
                worldRoot.transform,
                "Door_CommandToCorridor",
                new Vector3(0f, 0f, -2.16f),
                new Vector3(0f, 180f, 0f),
                "door.name.corridor",
                new Vector3(-1.40f, 0f, 0f),
                mats.BulkheadDoor,
                mats.Trim,
                mats.BeaconGreen,
                includeMedbayWindow: false);
            doorsList.Add(cmdToCorrDoor);

            // 5. 12m Hub Corridor (X in [-1.50, +1.50], Z in [-14.22, -2.22])
            BuildHubCorridor(
                worldRoot.transform,
                wallPrefab,
                statusLightsPanelPrefab,
                mats,
                hudFont,
                lightsList,
                out result.CorridorSpawn,
                out result.CorridorStatusRenderers,
                out result.CorridorStatusLabels);

            // 6. Greenhouse Bay + Door (West of Corridor, Z = -6.50)
            var ghDoor = BuildDoorAssembly(
                worldRoot.transform,
                "Door_Greenhouse",
                new Vector3(-1.56f, 0f, -6.50f),
                new Vector3(0f, -90f, 0f),
                "door.name.greenhouse",
                new Vector3(-1.40f, 0f, 0f),
                mats.GreenhouseDoor,
                mats.Trim,
                mats.BeaconGreen,
                includeMedbayWindow: false);
            doorsList.Add(ghDoor);

            BuildGreenhouseBay(
                worldRoot.transform,
                wallPrefab,
                mats,
                hudFont,
                out result.GreenhouseCropTransforms,
                out result.GreenhouseCropRenderers,
                out result.GreenhouseGrowLight);
            if (result.GreenhouseGrowLight != null) lightsList.Add(result.GreenhouseGrowLight);

            // 7. Life Support Storage Bay + Door (East of Corridor, Z = -9.50)
            var stDoor = BuildDoorAssembly(
                worldRoot.transform,
                "Door_Storage",
                new Vector3(1.56f, 0f, -9.50f),
                new Vector3(0f, 90f, 0f),
                "door.name.storage",
                new Vector3(-1.40f, 0f, 0f),
                mats.StorageDoor,
                mats.Trim,
                mats.BeaconGreen,
                includeMedbayWindow: false);
            doorsList.Add(stDoor);

            var storageLight = BuildStorageBay(worldRoot.transform, wallPrefab, mats, hudFont);
            if (storageLight != null) lightsList.Add(storageLight);

            // 8. EVA Airlock Chamber (Z in [-17.42, -14.22])
            BuildAirlockSection(
                worldRoot.transform,
                wallPrefab,
                mats,
                hudFont,
                doorsList,
                out result.Airlock,
                out result.AirlockSpawn);

            // 9. Unified 60m x 60m Walkable Mars Surface + Exterior Shell + 5 POIs
            BuildMarsSurfaceAndPois(
                worldRoot.transform,
                mats,
                hudFont,
                out result.SurfaceSpawn,
                out result.SolarPanelRenderers,
                out result.SurfaceDustParticles,
                out result.SkyDomeRenderer,
                out result.MarsSunLight);

            result.AllDoors = doorsList.ToArray();
            result.HabitatLights = lightsList.ToArray();
            return result;
        }

        // =========================================================================
        // MATERIALS FOR PART 3
        // =========================================================================

        private class ExpansionMaterials
        {
            public Material Floor, Wall, Ceiling, Trim, ExtHull;
            public Material BulkheadDoor, MedbayDoor, GreenhouseDoor, StorageDoor, AirlockOuterDoor;
            public Material MedbayBed, ValeSuit, VitalScreen, TabletScreen, TabletBody;
            public Material CropLeaf, SoilBed, WaterTank, OxygenTank, Crate;
            public Material BeaconGreen, BeaconAmber, SolarCell, RoverBody, RoverWheel, AntennaDish, MarsSoil, MarsRock, IcePatch;
            public Material SkyRing, SunDisc, DustMat;
        }

        private static ExpansionMaterials LoadOrCreateExpansionMaterials()
        {
            var m = new ExpansionMaterials
            {
                Floor = LoadMat("M_Room_Floor") ?? CreateLit("M_Room_Floor", new Color(0.22f, 0.23f, 0.26f), 0.1f),
                Wall = LoadMat("M_Room_Wall") ?? CreateLit("M_Room_Wall", new Color(0.38f, 0.40f, 0.44f), 0.08f),
                Ceiling = LoadMat("M_Room_Ceiling") ?? CreateLit("M_Room_Ceiling", new Color(0.26f, 0.28f, 0.31f), 0.06f),
                Trim = LoadMat("M_Room_Trim") ?? CreateLit("M_Room_Trim", new Color(0.18f, 0.19f, 0.22f), 0.15f),
                ExtHull = CreateLit("M_Ext_HabHull", new Color(0.56f, 0.57f, 0.59f), 0.14f),
                BulkheadDoor = CreateLit("M_Door_Bulkhead", new Color(0.36f, 0.40f, 0.46f), 0.15f),
                MedbayDoor = LoadMat("M_Medbay_Door") ?? CreateLit("M_Medbay_Door", new Color(0.46f, 0.48f, 0.50f), 0.1f),
                GreenhouseDoor = CreateLit("M_Door_Greenhouse", new Color(0.25f, 0.44f, 0.32f), 0.14f),
                StorageDoor = CreateLit("M_Door_Storage", new Color(0.27f, 0.39f, 0.49f), 0.14f),
                AirlockOuterDoor = CreateLit("M_Door_AirlockOuter", new Color(0.62f, 0.42f, 0.16f), 0.16f),
                MedbayBed = CreateLit("M_Medbay_Bed", new Color(0.84f, 0.86f, 0.90f), 0.06f),
                ValeSuit = CreateLit("M_Vale_Suit", new Color(0.22f, 0.46f, 0.72f), 0.12f),
                VitalScreen = CreateEmissive("M_Vital_Screen", new Color(0.18f, 0.88f, 0.52f), 1.4f),
                TabletBody = CreateLit("M_Tablet_Body", new Color(0.14f, 0.16f, 0.20f), 0.2f),
                TabletScreen = CreateEmissive("M_Tablet_Screen", new Color(0.18f, 0.55f, 0.85f), 1.15f),
                CropLeaf = CreateLit("M_Crop_Leaf", new Color(0.22f, 0.68f, 0.32f), 0.08f),
                SoilBed = CreateLit("M_Hydro_Tray", new Color(0.20f, 0.16f, 0.14f), 0.05f),
                WaterTank = CreateLit("M_Tank_Water", new Color(0.18f, 0.46f, 0.74f), 0.22f),
                OxygenTank = CreateLit("M_Tank_Oxygen", new Color(0.16f, 0.68f, 0.62f), 0.22f),
                Crate = CreateLit("M_Storage_Crate", new Color(0.44f, 0.38f, 0.28f), 0.08f),
                BeaconGreen = CreateEmissive("M_Beacon_Status", new Color(0.24f, 0.90f, 0.55f), 1.5f),
                BeaconAmber = CreateEmissive("M_Beacon_Airlock", new Color(1.0f, 0.62f, 0.16f), 1.6f),
                SolarCell = CreateLit("M_Solar_Cell", new Color(0.12f, 0.24f, 0.46f), 0.28f),
                RoverBody = CreateLit("M_Rover_Body", new Color(0.78f, 0.76f, 0.70f), 0.16f),
                RoverWheel = CreateLit("M_Rover_Wheel", new Color(0.14f, 0.14f, 0.16f), 0.06f),
                AntennaDish = CreateLit("M_Antenna_Dish", new Color(0.74f, 0.77f, 0.82f), 0.22f),
                MarsSoil = LoadMat("M_Mars_Ground") ?? CreateEmissive("M_Mars_Ground", new Color(0.68f, 0.32f, 0.18f), 0.42f),
                MarsRock = LoadMat("M_Mars_Rock") ?? CreateEmissive("M_Mars_Rock", new Color(0.52f, 0.24f, 0.14f), 0.35f),
                IcePatch = CreateEmissive("M_Ice_Patch", new Color(0.42f, 0.66f, 0.78f), 0.22f),
                SkyRing = CreateEmissive("M_Mars_SkyRing", new Color(0.62f, 0.34f, 0.24f), 0.38f),
                SunDisc = LoadMat("M_Mars_Sun") ?? CreateEmissive("M_Mars_Sun", new Color(1.0f, 0.92f, 0.78f), 1.8f),
                DustMat = LoadMat("M_Mars_DustParticle")
            };
            AssetDatabase.SaveAssets();
            return m;
        }

        private static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatsDir}/{name}.mat");

        private static Material CreateLit(string name, Color col, float smoothness)
        {
            string path = $"{MatsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = sh;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateEmissive(string name, Color col, float emissionMult)
        {
            string path = $"{MatsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = sh;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * emissionMult);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =========================================================================
        // COMMAND MODULE OPENINGS & MODULAR SLIDING DOOR
        // =========================================================================

        private static void ReplaceRearAndLeftCommandWalls(Transform roomRoot, GameObject wallPrefab, ExpansionMaterials mats)
        {
            DestroyChildIfFound(roomRoot, "Wall_Rear");
            DestroyChildIfFound(roomRoot, "Wall_Left");

            // Rear wall Z = -2.16 with 1.50m opening at X = 0 for Hub Corridor
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_RearLeft", new Vector3(-1.925f, 1.50f, -2.16f), new Vector3(2.35f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_RearRight", new Vector3(1.925f, 1.50f, -2.16f), new Vector3(2.35f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_RearHeader", new Vector3(0f, 2.60f, -2.16f), new Vector3(1.50f, 0.80f, 0.12f));

            // Left wall X = -3.06 with 1.40m opening at Z = 1.35 for Medbay
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_LeftNorth", new Vector3(-3.06f, 1.50f, 2.48f), new Vector3(0.12f, 3.0f, 0.86f));
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_LeftSouth", new Vector3(-3.06f, 1.50f, -0.785f), new Vector3(0.12f, 3.0f, 2.87f));
            PlaceStaticWall(wallPrefab, roomRoot, "Wall_LeftHeader", new Vector3(-3.06f, 2.60f, 1.35f), new Vector3(0.12f, 0.80f, 1.40f));
        }

        private static DoorInteractable BuildDoorAssembly(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 eulerAngles,
            string doorNameKey,
            Vector3 slideOffset,
            Material leafMat,
            Material trimMat,
            Material indicatorMat,
            bool includeMedbayWindow,
            bool lockedByDefault = false,
            string lockReasonKey = "")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            root.transform.eulerAngles = eulerAngles;

            // Static bulkhead jambs & header (outside the clear 1.5m opening)
            AddPrim(root, PrimitiveType.Cube, "JambLeft", new Vector3(-0.79f, 1.10f, 0f), Vector3.zero, new Vector3(0.08f, 2.20f, 0.18f), trimMat, removeCol: true);
            AddPrim(root, PrimitiveType.Cube, "JambRight", new Vector3(0.79f, 1.10f, 0f), Vector3.zero, new Vector3(0.08f, 2.20f, 0.18f), trimMat, removeCol: true);
            AddPrim(root, PrimitiveType.Cube, "HeaderBar", new Vector3(0f, 2.22f, 0f), Vector3.zero, new Vector3(1.66f, 0.08f, 0.18f), trimMat, removeCol: true);

            // Indicator light strip above frame
            var ind = AddPrim(root, PrimitiveType.Cube, "DoorIndicator", new Vector3(0f, 2.22f, -0.10f), Vector3.zero, new Vector3(0.42f, 0.04f, 0.02f), indicatorMat, removeCol: true);

            // Sliding Door Leaf (with solid BoxCollider switched to isTrigger when open so passage never snags)
            var leaf = new GameObject("DoorLeaf");
            leaf.transform.SetParent(root.transform, false);
            var leafCol = leaf.AddComponent<BoxCollider>();
            leafCol.center = new Vector3(0f, 1.08f, 0f);
            leafCol.size = new Vector3(1.48f, 2.16f, 0.10f);

            AddPrim(leaf, PrimitiveType.Cube, "DoorSlab", new Vector3(0f, 1.08f, 0f), Vector3.zero, new Vector3(1.48f, 2.16f, 0.08f), leafMat, removeCol: true);
            AddPrim(leaf, PrimitiveType.Cube, "DoorStripe", new Vector3(0f, 1.08f, 0f), Vector3.zero, new Vector3(1.42f, 0.12f, 0.095f), trimMat, removeCol: true);

            if (includeMedbayWindow)
            {
                AddPrim(leaf, PrimitiveType.Quad, "MedbayWindow", new Vector3(0f, 1.50f, -0.048f), Vector3.zero, new Vector3(0.50f, 0.34f, 1f), indicatorMat, removeCol: true);
                AddPrim(leaf, PrimitiveType.Quad, "MedbayWindowBack", new Vector3(0f, 1.50f, 0.048f), new Vector3(0f, 180f, 0f), new Vector3(0.50f, 0.34f, 1f), indicatorMat, removeCol: true);
            }

            var doorCtrl = root.AddComponent<DoorInteractable>();
            doorCtrl.Configure(
                doorNameKey,
                DoorMotionType.SlideLocalOffset,
                leaf.transform,
                leafCol,
                slideOffset,
                openByDefault: false,
                lockedByDefault: lockedByDefault,
                lockKey: lockReasonKey,
                indicator: ind.GetComponent<Renderer>());

            leaf.AddComponent<InteractableProp>().ConfigureDoor(doorCtrl);

            // Side Wall Keypad Switches (on BOTH sides of the doorway!) so player can always close/open comfortably
            CreateDoorKeypad(root.transform, "Keypad_Front", new Vector3(-0.88f, 1.25f, -0.11f), Vector3.zero, trimMat, indicatorMat, doorCtrl);
            CreateDoorKeypad(root.transform, "Keypad_Back", new Vector3(-0.88f, 1.25f, 0.11f), new Vector3(0f, 180f, 0f), trimMat, indicatorMat, doorCtrl);

            return doorCtrl;
        }

        private static void CreateDoorKeypad(Transform parent, string name, Vector3 localPos, Vector3 localEuler, Material trimMat, Material indMat, DoorInteractable door)
        {
            var pad = new GameObject(name);
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = localPos;
            pad.transform.localEulerAngles = localEuler;
            AddPrim(pad, PrimitiveType.Cube, "PadBack", Vector3.zero, Vector3.zero, new Vector3(0.14f, 0.22f, 0.04f), trimMat, removeCol: true);
            AddPrim(pad, PrimitiveType.Cube, "PadButton", new Vector3(0f, 0.02f, -0.025f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.02f), indMat, removeCol: true);
            var col = pad.AddComponent<BoxCollider>();
            col.size = new Vector3(0.22f, 0.32f, 0.12f);
            col.isTrigger = true;
            pad.AddComponent<InteractableProp>().ConfigureDoor(door);
        }

        // =========================================================================
        // ROOMS: MEDBAY, CORRIDOR, GREENHOUSE, STORAGE, AIRLOCK
        // =========================================================================

        private static Light BuildMedbayRoom(
            Transform parent,
            GameObject wallPrefab,
            ExpansionMaterials mats,
            TMP_FontAsset font,
            out Renderer medVitalRend)
        {
            var med = new GameObject("Room_Medbay");
            med.transform.SetParent(parent, false);

            // Floor & Ceiling: X in [-6.86, -3.12], Z in [-0.36, 2.91]
            AddFloorAndCeiling(med.transform, "Medbay", -4.99f, 1.275f, 3.74f, 3.27f, mats.Floor, mats.Ceiling);
            PlaceStaticWall(wallPrefab, med.transform, "MedWall_West", new Vector3(-6.80f, 1.50f, 1.275f), new Vector3(0.12f, 3.0f, 3.27f));
            PlaceStaticWall(wallPrefab, med.transform, "MedWall_North", new Vector3(-4.99f, 1.50f, 2.85f), new Vector3(3.74f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, med.transform, "MedWall_South", new Vector3(-4.99f, 1.50f, -0.30f), new Vector3(3.74f, 3.0f, 0.12f));

            // Medbay Bed + Resting Commander Alex Vale
            var bed = new GameObject("Medbay_Bed");
            bed.transform.SetParent(med.transform, false);
            bed.transform.position = new Vector3(-5.65f, 0f, 1.25f);
            AddPrim(bed, PrimitiveType.Cube, "BedBase", new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(1.85f, 0.50f, 0.92f), mats.Trim, removeCol: true);
            AddPrim(bed, PrimitiveType.Cube, "Mattress", new Vector3(0f, 0.56f, 0f), Vector3.zero, new Vector3(1.80f, 0.14f, 0.86f), mats.MedbayBed, removeCol: true);
            AddPrim(bed, PrimitiveType.Cube, "Pillow", new Vector3(-0.72f, 0.67f, 0f), Vector3.zero, new Vector3(0.32f, 0.10f, 0.62f), mats.MedbayBed, removeCol: true);

            // Resting low-poly Commander Vale
            AddPrim(bed, PrimitiveType.Capsule, "CmdrVale_Torso", new Vector3(0.05f, 0.75f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.36f, 0.60f, 0.32f), mats.ValeSuit, removeCol: true);
            AddPrim(bed, PrimitiveType.Sphere, "CmdrVale_Head", new Vector3(-0.62f, 0.78f, 0f), Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), mats.Trim, removeCol: true);
            var bedCol = bed.AddComponent<BoxCollider>();
            bedCol.center = new Vector3(0f, 0.45f, 0f);
            bedCol.size = new Vector3(1.90f, 0.90f, 0.95f);
            bed.AddComponent<InteractableProp>().Configure(InteractablePropType.MedbayPatient, "poi.vale.prompt");

            // Vital monitor stand
            var mon = new GameObject("Medbay_VitalMonitor");
            mon.transform.SetParent(med.transform, false);
            mon.transform.position = new Vector3(-6.15f, 0f, 2.25f);
            AddPrim(mon, PrimitiveType.Cylinder, "Stand", new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(0.10f, 0.65f, 0.10f), mats.Trim, removeCol: true);
            var vitalScr = AddPrim(mon, PrimitiveType.Cube, "VitalScreen", new Vector3(0.05f, 1.35f, 0f), new Vector3(0f, 25f, 0f), new Vector3(0.38f, 0.28f, 0.08f), mats.VitalScreen, removeCol: true);
            medVitalRend = vitalScr.GetComponent<Renderer>();

            CreateWallSign(med.transform, "Sign_Medbay", new Vector3(-6.70f, 2.20f, 1.25f), new Vector3(0f, -90f, 0f), Localization.Get("room.medbay.sign"), mats.Trim, Color.cyan, font);

            return CreatePointLightObj(med.transform, "Light_Medbay", new Vector3(-5.0f, 2.55f, 1.28f), new Color(0.75f, 0.92f, 1.0f), 1.45f, 6.2f);
        }

        private static void BuildHubCorridor(
            Transform parent,
            GameObject wallPrefab,
            GameObject statusLightsPanelPrefab,
            ExpansionMaterials mats,
            TMP_FontAsset font,
            List<Light> lightsList,
            out Transform corridorSpawn,
            out Renderer[] corrRenderers,
            out TextMeshPro[] corrLabels)
        {
            var corr = new GameObject("HubCorridor");
            corr.transform.SetParent(parent, false);

            // 12m Corridor: X in [-1.50, +1.50], Z in [-14.22, -2.22]
            AddFloorAndCeiling(corr.transform, "Corridor", 0f, -8.22f, 3.12f, 12.12f, mats.Floor, mats.Ceiling);
            // Center guidance stripe
            AddPrim(corr, PrimitiveType.Cube, "GuideStripe_NorthSouth", new Vector3(0f, 0.005f, -8.22f), Vector3.zero, new Vector3(0.22f, 0.008f, 11.5f), mats.BeaconAmber, removeCol: true);

            // West Wall (X = -1.56) with Greenhouse opening at Z = -6.50 (width 1.50 -> Z in [-7.25, -5.75])
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_W1", new Vector3(-1.56f, 1.50f, -3.985f), new Vector3(0.12f, 3.0f, 3.53f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_W_Hdr", new Vector3(-1.56f, 2.60f, -6.50f), new Vector3(0.12f, 0.80f, 1.50f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_W2", new Vector3(-1.56f, 1.50f, -10.735f), new Vector3(0.12f, 3.0f, 6.97f));

            // East Wall (X = +1.56) with Storage opening at Z = -9.50 (width 1.50 -> Z in [-10.25, -8.75])
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_E1", new Vector3(1.56f, 1.50f, -5.485f), new Vector3(0.12f, 3.0f, 6.53f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_E_Hdr", new Vector3(1.56f, 2.60f, -9.50f), new Vector3(0.12f, 0.80f, 1.50f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_E2", new Vector3(1.56f, 1.50f, -12.235f), new Vector3(0.12f, 3.0f, 3.97f));

            // South Bulkhead at Z = -14.22 into Airlock (1.50m opening at X = 0)
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_SLeft", new Vector3(-1.155f, 1.50f, -14.22f), new Vector3(0.81f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_SRight", new Vector3(1.155f, 1.50f, -14.22f), new Vector3(0.81f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, corr.transform, "CorrWall_SHeader", new Vector3(0f, 2.60f, -14.22f), new Vector3(1.50f, 0.80f, 0.12f));

            // Directional Bulkhead Signs
            CreateWallSign(corr.transform, "Sign_CorridorTop", new Vector3(0f, 2.48f, -2.26f), Vector3.zero, Localization.Get("sign.command"), mats.Trim, Color.white, font);
            CreateWallSign(corr.transform, "Sign_Greenhouse", new Vector3(-1.46f, 2.38f, -6.50f), new Vector3(0f, -90f, 0f), Localization.Get("sign.greenhouse"), mats.Trim, new Color(0.45f, 0.95f, 0.55f), font);
            CreateWallSign(corr.transform, "Sign_Storage", new Vector3(1.46f, 2.38f, -9.50f), new Vector3(0f, 90f, 0f), Localization.Get("sign.storage"), mats.Trim, new Color(0.45f, 0.85f, 1.0f), font);
            CreateWallSign(corr.transform, "Sign_Airlock", new Vector3(0f, 2.48f, -14.12f), new Vector3(0f, 180f, 0f), Localization.Get("sign.airlock"), mats.Trim, new Color(1.0f, 0.78f, 0.28f), font);

            // Corridor 5-Light Status Panel on East Wall (facing -X)
            corrRenderers = new Renderer[5];
            corrLabels = new TextMeshPro[5];
            if (statusLightsPanelPrefab != null)
            {
                var panelInst = (GameObject)PrefabUtility.InstantiatePrefab(statusLightsPanelPrefab, corr.transform);
                panelInst.name = "CorridorStatusPanel";
                panelInst.transform.position = new Vector3(1.46f, 1.65f, -5.00f);
                panelInst.transform.eulerAngles = new Vector3(0f, 90f, 0f);
                string[] stockNames = { "Power", "Oxygen", "Water", "Food", "Shielding" };
                for (int i = 0; i < 5; i++)
                {
                    corrRenderers[i] = panelInst.transform.Find($"StatusLight_{stockNames[i]}")?.GetComponent<Renderer>();
                    corrLabels[i] = panelInst.transform.Find($"StatusLabel_{stockNames[i]}")?.GetComponent<TextMeshPro>();
                }
            }

            // Read-Only Wall Tablet on West Wall (X = -1.46, Z = -10.80, facing +X)
            CreateReadOnlyTabletProp(corr.transform, "WallTablet_Corridor", new Vector3(-1.47f, 1.45f, -10.80f), new Vector3(0f, -90f, 0f), mats.TabletBody, mats.TabletScreen, font);

            // Corridor overhead lights
            lightsList.Add(CreatePointLightObj(corr.transform, "Light_CorridorNorth", new Vector3(0f, 2.68f, -5.2f), new Color(0.86f, 0.91f, 0.98f), 1.30f, 8.2f));
            lightsList.Add(CreatePointLightObj(corr.transform, "Light_CorridorSouth", new Vector3(0f, 2.68f, -11.4f), new Color(0.86f, 0.91f, 0.98f), 1.30f, 8.2f));

            var spawnGo = new GameObject("CorridorSpawn");
            spawnGo.transform.SetParent(corr.transform, false);
            spawnGo.transform.position = new Vector3(0f, 1.0f, -3.90f);
            spawnGo.transform.eulerAngles = new Vector3(0f, 180f, 0f);
            corridorSpawn = spawnGo.transform;
        }

        private static void BuildGreenhouseBay(
            Transform parent,
            GameObject wallPrefab,
            ExpansionMaterials mats,
            TMP_FontAsset font,
            out Transform[] cropTransforms,
            out Renderer[] cropRenderers,
            out Light growLight)
        {
            var gh = new GameObject("Room_Greenhouse");
            gh.transform.SetParent(parent, false);

            // Interior X in [-6.44, -1.62], Z in [-8.75, -4.25]
            AddFloorAndCeiling(gh.transform, "Greenhouse", -4.03f, -6.50f, 4.94f, 4.62f, mats.Floor, mats.Ceiling);
            PlaceStaticWall(wallPrefab, gh.transform, "GHWall_West", new Vector3(-6.44f, 1.50f, -6.50f), new Vector3(0.12f, 3.0f, 4.62f));
            PlaceStaticWall(wallPrefab, gh.transform, "GHWall_North", new Vector3(-4.03f, 1.50f, -4.25f), new Vector3(4.94f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, gh.transform, "GHWall_South", new Vector3(-4.03f, 1.50f, -8.75f), new Vector3(4.94f, 3.0f, 0.12f));

            var cropT = new List<Transform>();
            var cropR = new List<Renderer>();
            float[] planterZ = { -5.20f, -6.50f, -7.80f };
            for (int r = 0; r < planterZ.Length; r++)
            {
                var planter = new GameObject($"HydroPlanter_{r}");
                planter.transform.SetParent(gh.transform, false);
                planter.transform.position = new Vector3(-4.25f, 0f, planterZ[r]);
                AddPrim(planter, PrimitiveType.Cube, "PlanterBox", new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(2.80f, 0.84f, 0.72f), mats.Trim, removeCol: true);
                AddPrim(planter, PrimitiveType.Cube, "SoilTray", new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(2.64f, 0.08f, 0.62f), mats.SoilBed, removeCol: true);

                for (int c = -2; c <= 2; c++)
                {
                    var leafObj = AddPrim(planter, PrimitiveType.Sphere, $"Crop_{c + 2}", new Vector3(c * 0.48f, 1.05f, 0f), Vector3.zero, new Vector3(0.32f, 0.34f, 0.32f), mats.CropLeaf, removeCol: true);
                    cropT.Add(leafObj.transform);
                    cropR.Add(leafObj.GetComponent<Renderer>());
                }

                var col = planter.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.60f, 0f);
                col.size = new Vector3(2.85f, 1.20f, 0.76f);
                planter.AddComponent<InteractableProp>().Configure(InteractablePropType.GreenhousePlanter, "poi.greenhouse.prompt");
            }

            CreateWallSign(gh.transform, "Sign_GreenhouseBay", new Vector3(-6.34f, 2.25f, -6.50f), new Vector3(0f, -90f, 0f), Localization.Get("sign.greenhouse"), mats.Trim, new Color(0.42f, 0.94f, 0.52f), font);

            growLight = CreatePointLightObj(gh.transform, "Light_GreenhouseGrow", new Vector3(-4.25f, 2.55f, -6.50f), new Color(1.0f, 0.84f, 0.96f), 1.65f, 7.5f);
            cropTransforms = cropT.ToArray();
            cropRenderers = cropR.ToArray();
        }

        private static Light BuildStorageBay(
            Transform parent,
            GameObject wallPrefab,
            ExpansionMaterials mats,
            TMP_FontAsset font)
        {
            var st = new GameObject("Room_Storage");
            st.transform.SetParent(parent, false);

            // Interior X in [+1.62, +6.44], Z in [-11.75, -7.25]
            AddFloorAndCeiling(st.transform, "Storage", 4.03f, -9.50f, 4.94f, 4.62f, mats.Floor, mats.Ceiling);
            PlaceStaticWall(wallPrefab, st.transform, "STWall_East", new Vector3(6.44f, 1.50f, -9.50f), new Vector3(0.12f, 3.0f, 4.62f));
            PlaceStaticWall(wallPrefab, st.transform, "STWall_North", new Vector3(4.03f, 1.50f, -7.25f), new Vector3(4.94f, 3.0f, 0.12f));
            PlaceStaticWall(wallPrefab, st.transform, "STWall_South", new Vector3(4.03f, 1.50f, -11.75f), new Vector3(4.94f, 3.0f, 0.12f));

            // 2 Blue Water Distiller Tanks
            for (int i = 0; i < 2; i++)
            {
                var wt = new GameObject($"WaterDistillerTank_{i}");
                wt.transform.SetParent(st.transform, false);
                wt.transform.position = new Vector3(3.30f + i * 1.15f, 0f, -8.15f);
                AddPrim(wt, PrimitiveType.Cylinder, "TankDrum", new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.75f, 0.95f, 0.75f), mats.WaterTank, removeCol: true);
                AddPrim(wt, PrimitiveType.Cube, "PipeTop", new Vector3(0f, 2.0f, 0f), Vector3.zero, new Vector3(0.24f, 0.16f, 0.24f), mats.Trim, removeCol: true);
                var col = wt.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.0f, 0f);
                col.size = new Vector3(0.85f, 2.05f, 0.85f);
                wt.AddComponent<InteractableProp>().Configure(InteractablePropType.StorageWaterTank, "poi.water.prompt");
            }

            // 2 Teal O2 / CO2 Scrubber Cylinders
            for (int i = 0; i < 2; i++)
            {
                var o2 = new GameObject($"OxygenScrubberTank_{i}");
                o2.transform.SetParent(st.transform, false);
                o2.transform.position = new Vector3(3.30f + i * 1.15f, 0f, -10.85f);
                AddPrim(o2, PrimitiveType.Cylinder, "O2Cylinder", new Vector3(0f, 1.05f, 0f), Vector3.zero, new Vector3(0.58f, 1.02f, 0.58f), mats.OxygenTank, removeCol: true);
                AddPrim(o2, PrimitiveType.Sphere, "ValveCap", new Vector3(0f, 2.12f, 0f), Vector3.zero, new Vector3(0.26f, 0.18f, 0.26f), mats.Trim, removeCol: true);
                var col = o2.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.05f, 0f);
                col.size = new Vector3(0.68f, 2.15f, 0.68f);
                o2.AddComponent<InteractableProp>().Configure(InteractablePropType.StorageOxygenTank, "poi.oxygen.prompt");
            }

            // Supply Crates against East wall
            var crate1 = AddPrim(st, PrimitiveType.Cube, "SupplyCrate_A", new Vector3(5.65f, 0.45f, -9.00f), Vector3.zero, new Vector3(0.90f, 0.90f, 0.90f), mats.Crate, removeCol: false);
            crate1.isStatic = true;
            var crate2 = AddPrim(st, PrimitiveType.Cube, "SupplyCrate_B", new Vector3(5.65f, 0.45f, -10.10f), Vector3.zero, new Vector3(0.85f, 0.85f, 0.85f), mats.Crate, removeCol: false);
            crate2.isStatic = true;

            CreateWallSign(st.transform, "Sign_StorageBay", new Vector3(6.34f, 2.25f, -9.50f), new Vector3(0f, 90f, 0f), Localization.Get("sign.storage"), mats.Trim, new Color(0.45f, 0.86f, 1.0f), font);

            return CreatePointLightObj(st.transform, "Light_Storage", new Vector3(4.10f, 2.60f, -9.50f), new Color(0.84f, 0.92f, 1.0f), 1.40f, 7.2f);
        }

        private static void BuildAirlockSection(
            Transform parent,
            GameObject wallPrefab,
            ExpansionMaterials mats,
            TMP_FontAsset font,
            List<DoorInteractable> doorsList,
            out AirlockController airlockCtrl,
            out Transform airlockSpawn)
        {
            var al = new GameObject("Room_Airlock");
            al.transform.SetParent(parent, false);

            // Airlock Chamber: X in [-1.50, +1.50], Z in [-17.42, -14.22]
            AddFloorAndCeiling(al.transform, "Airlock", 0f, -15.82f, 3.12f, 3.20f, mats.Floor, mats.Ceiling);
            PlaceStaticWall(wallPrefab, al.transform, "ALWall_West", new Vector3(-1.56f, 1.50f, -15.82f), new Vector3(0.12f, 3.0f, 3.20f));
            PlaceStaticWall(wallPrefab, al.transform, "ALWall_East", new Vector3(1.56f, 1.50f, -15.82f), new Vector3(0.12f, 3.0f, 3.20f));

            // South Exterior Hatch wall at Z = -17.36 (1.50m opening at X = 0)
            PlaceStaticWall(wallPrefab, al.transform, "ALWall_SLeft", new Vector3(-1.155f, 1.50f, -17.36f), new Vector3(0.81f, 3.0f, 0.14f));
            PlaceStaticWall(wallPrefab, al.transform, "ALWall_SRight", new Vector3(1.155f, 1.50f, -17.36f), new Vector3(0.81f, 3.0f, 0.14f));
            PlaceStaticWall(wallPrefab, al.transform, "ALWall_SHeader", new Vector3(0f, 2.60f, -17.36f), new Vector3(1.50f, 0.80f, 0.14f));

            // Inner & Outer Airlock Doors (slide vertically into overhead hatch motor housings so zero hull clipping!)
            var innerDoor = BuildDoorAssembly(
                al.transform,
                "Door_AirlockInner",
                new Vector3(0f, 0f, -14.22f),
                new Vector3(0f, 180f, 0f),
                "door.name.airlock_in",
                new Vector3(0f, 2.02f, 0f),
                mats.BulkheadDoor,
                mats.Trim,
                mats.BeaconGreen,
                includeMedbayWindow: false);
            AddPrim(innerDoor.gameObject, PrimitiveType.Cube, "MotorHousing", new Vector3(0f, 3.65f, 0f), Vector3.zero, new Vector3(1.68f, 1.15f, 0.28f), mats.ExtHull, removeCol: true);
            doorsList.Add(innerDoor);

            var outerDoor = BuildDoorAssembly(
                al.transform,
                "Door_AirlockOuter",
                new Vector3(0f, 0f, -17.36f),
                new Vector3(0f, 180f, 0f),
                "door.name.airlock_out",
                new Vector3(0f, 2.02f, 0f),
                mats.AirlockOuterDoor,
                mats.Trim,
                mats.BeaconAmber,
                includeMedbayWindow: false,
                lockedByDefault: true,
                lockReasonKey: "airlock.hatch_locked");
            AddPrim(outerDoor.gameObject, PrimitiveType.Cube, "MotorHousing", new Vector3(0f, 3.65f, 0f), Vector3.zero, new Vector3(1.68f, 1.15f, 0.28f), mats.ExtHull, removeCol: true);
            doorsList.Add(outerDoor);

            // Ceiling beacon + chamber light
            var beaconBulb = AddPrim(al, PrimitiveType.Sphere, "Airlock_BeaconBulb", new Vector3(0f, 2.92f, -15.82f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.18f), mats.BeaconAmber, removeCol: true);
            var chamberLight = CreatePointLightObj(al.transform, "Light_AirlockChamber", new Vector3(0f, 2.65f, -15.82f), new Color(0.82f, 0.90f, 1.0f), 1.35f, 5.5f);

            airlockCtrl = al.AddComponent<AirlockController>();
            airlockCtrl.Configure(innerDoor, outerDoor, chamberLight, beaconBulb.GetComponent<Renderer>(), 3.0f);

            // Airlock Cycle Switch (Inside Chamber on West Wall)
            CreateAirlockSwitchPanel(al.transform, "AirlockCycleSwitch_Inside", new Vector3(-1.46f, 1.32f, -15.82f), new Vector3(0f, -90f, 0f), mats.Trim, mats.BeaconAmber, airlockCtrl, font);
            // Airlock Cycle Switch (Outside on Exterior South Wall next to hatch)
            CreateAirlockSwitchPanel(al.transform, "AirlockCycleSwitch_Outside", new Vector3(-1.18f, 1.32f, -17.46f), Vector3.zero, mats.Trim, mats.BeaconAmber, airlockCtrl, font);

            // Read-Only Status Tablet inside Airlock (East wall, facing -X)
            CreateReadOnlyTabletProp(al.transform, "WallTablet_Airlock", new Vector3(1.47f, 1.42f, -15.82f), new Vector3(0f, 90f, 0f), mats.TabletBody, mats.TabletScreen, font);

            // Gentle access ramp from floor Y=0.00 down to Mars ground Y=-0.08
            var ramp = AddPrim(al, PrimitiveType.Cube, "Airlock_ExitRamp", new Vector3(0f, -0.055f, -17.85f), new Vector3(5.5f, 0f, 0f), new Vector3(2.20f, 0.08f, 1.25f), mats.Trim, removeCol: false);
            ramp.isStatic = true;

            var spawnGo = new GameObject("AirlockSpawn");
            spawnGo.transform.SetParent(al.transform, false);
            spawnGo.transform.position = new Vector3(0f, 1.0f, -15.60f);
            spawnGo.transform.eulerAngles = new Vector3(0f, 180f, 0f);
            airlockSpawn = spawnGo.transform;
        }

        private static void CreateAirlockSwitchPanel(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 euler,
            Material trimMat,
            Material btnMat,
            AirlockController airlock,
            TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.eulerAngles = euler;

            AddPrim(go, PrimitiveType.Cube, "SwitchBack", Vector3.zero, Vector3.zero, new Vector3(0.32f, 0.44f, 0.06f), trimMat, removeCol: true);
            AddPrim(go, PrimitiveType.Cube, "CycleButton", new Vector3(0f, 0.04f, -0.04f), Vector3.zero, new Vector3(0.18f, 0.18f, 0.03f), btnMat, removeCol: true);

            var lbl = new GameObject("SwitchLabel");
            lbl.transform.SetParent(go.transform, false);
            lbl.transform.localPosition = new Vector3(0f, -0.14f, -0.04f);
            lbl.transform.localScale = new Vector3(0.03f, 0.03f, 1f);
            var tmp = lbl.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = "EVA AIRLOCK\nCYCLE";
            tmp.fontSize = 4.8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1.0f, 0.85f, 0.42f);
            tmp.rectTransform.sizeDelta = new Vector2(10f, 4f);

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.38f, 0.50f, 0.18f);
            col.isTrigger = true;
            go.AddComponent<InteractableProp>().ConfigureAirlock(airlock);
        }

        private static void CreateReadOnlyTabletProp(
            Transform parent,
            string name,
            Vector3 worldPos,
            Vector3 euler,
            Material bodyMat,
            Material screenMat,
            TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.eulerAngles = euler;

            AddPrim(go, PrimitiveType.Cube, "TabletBezel", Vector3.zero, Vector3.zero, new Vector3(0.56f, 0.40f, 0.05f), bodyMat, removeCol: true);
            AddPrim(go, PrimitiveType.Quad, "TabletScreen", new Vector3(0f, 0.01f, -0.028f), Vector3.zero, new Vector3(0.48f, 0.30f, 1f), screenMat, removeCol: true);

            var lbl = new GameObject("TabletLabel");
            lbl.transform.SetParent(go.transform, false);
            lbl.transform.localPosition = new Vector3(0f, 0.01f, -0.032f);
            lbl.transform.localScale = new Vector3(0.032f, 0.032f, 1f);
            var tmp = lbl.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = "ARES-4 STATUS\n[READ-ONLY]";
            tmp.fontSize = 5.4f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.98f, 1.0f);
            tmp.rectTransform.sizeDelta = new Vector2(14f, 6f);

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.62f, 0.46f, 0.16f);
            col.isTrigger = true;
            go.AddComponent<InteractableProp>().Configure(InteractablePropType.ReadOnlyTablet, "tablet.prompt");
        }

        // =========================================================================
        // 60m x 60m MARS SURFACE + EXTERIOR HABITAT HULL + 5 POIs
        // =========================================================================

        private static void BuildMarsSurfaceAndPois(
            Transform parent,
            ExpansionMaterials mats,
            TMP_FontAsset font,
            out Transform surfaceSpawn,
            out Renderer[] solarRenderers,
            out ParticleSystem surfaceDust,
            out Renderer skyDomeRenderer,
            out Light marsSunLight)
        {
            var mars = new GameObject("MarsSurfaceWorld");
            mars.transform.SetParent(parent, false);

            // Walkable Mars Terrain Plateau: 70m x 72m centered at (0, -0.08, -16) covering X in [-35,35], Z in [-52, +20]
            // Top surface sits cleanly at Y = -0.08 (below all habitat interiors Y = 0.00 so zero Z-fighting)
            var terrain = AddPrim(mars, PrimitiveType.Cube, "Mars_TerrainPlateau", new Vector3(0f, -0.58f, -16f), Vector3.zero, new Vector3(72f, 1.0f, 72f), mats.MarsSoil, removeCol: false);
            terrain.isStatic = true;

            // 4 Invisible Perimeter Boundary Colliders defining a safe 60m x 60m walkable play zone (X in [-30, +30], Z in [-46, +14])
            const int ignoreRaycastLayer = 2;
            CreateBoundaryWall(mars.transform, "Boundary_North", new Vector3(0f, 2f, 14f), new Vector3(62f, 5f, 1f), ignoreRaycastLayer);
            CreateBoundaryWall(mars.transform, "Boundary_South", new Vector3(0f, 2f, -46f), new Vector3(62f, 5f, 1f), ignoreRaycastLayer);
            CreateBoundaryWall(mars.transform, "Boundary_West", new Vector3(-30f, 2f, -16f), new Vector3(1f, 5f, 62f), ignoreRaycastLayer);
            CreateBoundaryWall(mars.transform, "Boundary_East", new Vector3(30f, 2f, -16f), new Vector3(1f, 5f, 62f), ignoreRaycastLayer);

            // Full 360 Horizon Sky Ring + Tiny Far-Away Sun + Distant Crater Ridges
            var skyCol = AddPrim(mars, PrimitiveType.Cylinder, "Mars_SkyDomeRing", new Vector3(0f, 12f, -15f), Vector3.zero, new Vector3(145f, 26f, 145f), mats.SkyRing, removeCol: true);
            // Invert normals or add inward quads on 4 cardinal horizons (simple inward-facing sky backdrop quads work cleanly without shader culling hacks!)
            UnityEngine.Object.DestroyImmediate(skyCol);
            var skyGroup = new GameObject("Mars_HorizonBackdrop");
            skyGroup.transform.SetParent(mars.transform, false);
            var skyN = AddPrim(skyGroup, PrimitiveType.Quad, "Sky_North", new Vector3(0f, 14f, 45f), Vector3.zero, new Vector3(130f, 36f, 1f), mats.SkyRing, removeCol: true);
            AddPrim(skyGroup, PrimitiveType.Quad, "Sky_South", new Vector3(0f, 14f, -68f), new Vector3(0f, 180f, 0f), new Vector3(130f, 36f, 1f), mats.SkyRing, removeCol: true);
            AddPrim(skyGroup, PrimitiveType.Quad, "Sky_West", new Vector3(-55f, 14f, -12f), new Vector3(0f, -90f, 0f), new Vector3(130f, 36f, 1f), mats.SkyRing, removeCol: true);
            AddPrim(skyGroup, PrimitiveType.Quad, "Sky_East", new Vector3(55f, 14f, -12f), new Vector3(0f, 90f, 0f), new Vector3(130f, 36f, 1f), mats.SkyRing, removeCol: true);
            skyDomeRenderer = skyN.GetComponent<Renderer>();

            // Tiny distant sun ("the sun is far away") visible through Command Window & outside
            AddPrim(mars, PrimitiveType.Sphere, "Mars_TinySun", new Vector3(6.5f, 11.2f, 40f), Vector3.zero, new Vector3(0.60f, 0.60f, 0.60f), mats.SunDisc, removeCol: true);

            // Low-poly Crater Rim Slabs outside perimeter
            Vector3[] ridgePos =
            {
                new Vector3(-16f, 1.8f, 30f), new Vector3(17f, 2.2f, 32f),
                new Vector3(-34f, 2.0f, -20f), new Vector3(35f, 2.4f, -25f),
                new Vector3(-20f, 2.5f, -52f), new Vector3(22f, 2.1f, -50f)
            };
            Vector3[] ridgeScale =
            {
                new Vector3(24f, 4.0f, 4f), new Vector3(24f, 4.5f, 4f),
                new Vector3(5f, 4.5f, 38f), new Vector3(5f, 4.8f, 42f),
                new Vector3(30f, 5.2f, 5f), new Vector3(28f, 4.4f, 5f)
            };
            for (int i = 0; i < ridgePos.Length; i++)
                AddPrim(mars, PrimitiveType.Cube, $"CraterRidge_{i}", ridgePos[i], new Vector3(0f, (i % 2 == 0 ? 12f : -14f), 0f), ridgeScale[i], mats.MarsRock, removeCol: true);

            // Exterior Habitat Hull Trim Plates (make Ares-4 look like a proper Martian outpost module from outside)
            AddPrim(mars, PrimitiveType.Cube, "HabHull_CommandRoof", new Vector3(0f, 3.16f, 0.35f), Vector3.zero, new Vector3(6.40f, 0.16f, 5.40f), mats.ExtHull, removeCol: true);
            AddPrim(mars, PrimitiveType.Cube, "HabHull_CorridorRoof", new Vector3(0f, 3.16f, -9.82f), Vector3.zero, new Vector3(3.32f, 0.16f, 15.4f), mats.ExtHull, removeCol: true);
            AddPrim(mars, PrimitiveType.Cube, "HabHull_GreenhouseRoof", new Vector3(-4.03f, 3.16f, -6.50f), Vector3.zero, new Vector3(5.12f, 0.16f, 4.80f), mats.ExtHull, removeCol: true);
            AddPrim(mars, PrimitiveType.Cube, "HabHull_StorageRoof", new Vector3(4.03f, 3.16f, -9.50f), Vector3.zero, new Vector3(5.12f, 0.16f, 4.80f), mats.ExtHull, removeCol: true);
            AddPrim(mars, PrimitiveType.Cube, "HabHull_MedbayRoof", new Vector3(-4.99f, 3.16f, 1.28f), Vector3.zero, new Vector3(3.90f, 0.16f, 3.45f), mats.ExtHull, removeCol: true);

            // Scattered Mars Rocks (both outside Command Window Z>4 and around EVA terrain Z<-18)
            Vector3[] rocks =
            {
                new Vector3(-2.1f, 0.12f, 5.5f), new Vector3(2.3f, 0.14f, 6.4f), new Vector3(-4.8f, 0.25f, 10.2f), new Vector3(5.2f, 0.28f, 11.4f),
                new Vector3(-6.2f, 0.20f, -20.5f), new Vector3(6.5f, 0.18f, -21.0f), new Vector3(-16.5f, 0.45f, -27.5f),
                new Vector3(17.0f, 0.40f, -28.0f), new Vector3(-4.5f, 0.22f, -31.5f), new Vector3(5.2f, 0.20f, -32.0f),
                new Vector3(-19.5f, 0.50f, -39.0f), new Vector3(19.0f, 0.48f, -40.5f), new Vector3(-10.0f, 0.30f, -42.5f), new Vector3(9.0f, 0.28f, -43.0f)
            };
            for (int i = 0; i < rocks.Length; i++)
            {
                float s = 0.65f + (i % 4) * 0.35f;
                AddPrim(mars, PrimitiveType.Cube, $"MarsSurfaceRock_{i}", rocks[i], new Vector3(11f * i, 27f * i, -7f), new Vector3(1.2f * s, 0.65f * s, 0.95f * s), mats.MarsRock, removeCol: false);
            }

            // Directional Sunlight for Mars Surface (Shadows Off for WebGL / PSX look)
            var sunGo = new GameObject("MarsSunLight");
            sunGo.transform.SetParent(mars.transform, false);
            sunGo.transform.eulerAngles = new Vector3(34f, -150f, 0f);
            marsSunLight = sunGo.AddComponent<Light>();
            marsSunLight.type = LightType.Directional;
            marsSunLight.color = new Color(0.98f, 0.74f, 0.58f);
            marsSunLight.intensity = 1.05f;
            marsSunLight.shadows = LightShadows.None;

            // Wide Surface Dust ParticleSystem
            var dustGo = new GameObject("SurfaceDustParticles");
            dustGo.transform.SetParent(mars.transform, false);
            dustGo.transform.position = new Vector3(0f, 1.5f, -30f);
            surfaceDust = dustGo.AddComponent<ParticleSystem>();
            var main = surfaceDust.main;
            main.duration = 6f;
            main.loop = true;
            main.startLifetime = 5.0f;
            main.startSpeed = 1.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
            main.startColor = new Color(0.84f, 0.54f, 0.36f, 0.32f);
            main.maxParticles = 350;
            var em = surfaceDust.emission;
            em.rateOverTime = 32f;
            var shape = surfaceDust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 2.8f, 26f);
            var psRend = dustGo.GetComponent<ParticleSystemRenderer>();
            if (mats.DustMat != null) psRend.material = mats.DustMat;

            // --- 5 EXTERIOR POIs ---

            // POI 1: Solar Array Field (X=-9.5, Z=-24.5) - interactive Clean Panels (1 AP) when safe
            var solarList = new List<Renderer>();
            var solarPoi = new GameObject("POI_SolarArray");
            solarPoi.transform.SetParent(mars.transform, false);
            solarPoi.transform.position = new Vector3(-9.5f, -0.08f, -24.5f);
            for (int i = -1; i <= 1; i++)
            {
                AddPrim(solarPoi, PrimitiveType.Cylinder, $"SolarPost_{i + 1}", new Vector3(i * 2.2f, 0.65f, 0f), Vector3.zero, new Vector3(0.16f, 0.65f, 0.16f), mats.Trim, removeCol: true);
                var wing = AddPrim(solarPoi, PrimitiveType.Cube, $"SolarWing_{i + 1}", new Vector3(i * 2.2f, 1.25f, 0f), new Vector3(28f, 0f, 0f), new Vector3(1.85f, 0.08f, 2.40f), mats.SolarCell, removeCol: true);
                solarList.Add(wing.GetComponent<Renderer>());
            }
            var solCol = solarPoi.AddComponent<BoxCollider>();
            solCol.center = new Vector3(0f, 1.0f, 0f);
            solCol.size = new Vector3(6.4f, 2.0f, 2.6f);
            solarPoi.AddComponent<InteractableProp>().Configure(InteractablePropType.SurfaceSolarArray, "poi.solar.inspect_prompt");
            solarRenderers = solarList.ToArray();

            // POI 2: Pressurized Mars Rover (X=+9.5, Z=-25.0)
            var roverPoi = new GameObject("POI_PressurizedRover");
            roverPoi.transform.SetParent(mars.transform, false);
            roverPoi.transform.position = new Vector3(9.5f, -0.08f, -25.0f);
            roverPoi.transform.eulerAngles = new Vector3(0f, -28f, 0f);
            AddPrim(roverPoi, PrimitiveType.Cube, "Chassis", new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(2.20f, 0.35f, 3.80f), mats.Trim, removeCol: true);
            AddPrim(roverPoi, PrimitiveType.Cube, "CabinHull", new Vector3(0f, 1.25f, -0.25f), Vector3.zero, new Vector3(2.05f, 1.15f, 2.85f), mats.RoverBody, removeCol: true);
            AddPrim(roverPoi, PrimitiveType.Cube, "VisorWindow", new Vector3(0f, 1.45f, -1.68f), new Vector3(-18f, 0f, 0f), new Vector3(1.65f, 0.52f, 0.12f), mats.TabletScreen, removeCol: true);
            for (int wx = -1; wx <= 1; wx += 2)
                for (int wz = -1; wz <= 1; wz++)
                    AddPrim(roverPoi, PrimitiveType.Cylinder, $"Wheel_{wx}_{wz}", new Vector3(wx * 1.18f, 0.42f, wz * 1.32f), new Vector3(0f, 0f, 90f), new Vector3(0.84f, 0.20f, 0.84f), mats.RoverWheel, removeCol: true);
            var rovCol = roverPoi.AddComponent<BoxCollider>();
            rovCol.center = new Vector3(0f, 1.0f, 0f);
            rovCol.size = new Vector3(2.6f, 2.0f, 4.0f);
            roverPoi.AddComponent<InteractableProp>().Configure(InteractablePropType.SurfaceRover, "poi.rover.prompt");

            // POI 3: High-Gain DSN Antenna (X=-14.0, Z=-34.0)
            var antPoi = new GameObject("POI_DSN_Antenna");
            antPoi.transform.SetParent(mars.transform, false);
            antPoi.transform.position = new Vector3(-14.0f, -0.08f, -34.0f);
            AddPrim(antPoi, PrimitiveType.Cylinder, "AntennaPedestal", new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(1.4f, 0.4f, 1.4f), mats.Trim, removeCol: true);
            AddPrim(antPoi, PrimitiveType.Cylinder, "AntennaMast", new Vector3(0f, 2.2f, 0f), Vector3.zero, new Vector3(0.32f, 1.8f, 0.32f), mats.Trim, removeCol: true);
            AddPrim(antPoi, PrimitiveType.Cylinder, "AntennaDish", new Vector3(0f, 4.1f, 0f), new Vector3(48f, 20f, 0f), new Vector3(3.4f, 0.18f, 3.4f), mats.AntennaDish, removeCol: true);
            AddPrim(antPoi, PrimitiveType.Sphere, "BeaconTip", new Vector3(0f, 5.1f, 0.6f), Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), mats.BeaconGreen, removeCol: true);
            var antCol = antPoi.AddComponent<BoxCollider>();
            antCol.center = new Vector3(0f, 1.8f, 0f);
            antCol.size = new Vector3(2.4f, 3.6f, 2.4f);
            antPoi.AddComponent<InteractableProp>().Configure(InteractablePropType.SurfaceAntenna, "poi.antenna.prompt");

            // POI 4: Radiation Sensor Post (X=+12.5, Z=-35.5)
            var radPoi = new GameObject("POI_RadiationPost");
            radPoi.transform.SetParent(mars.transform, false);
            radPoi.transform.position = new Vector3(12.5f, -0.08f, -35.5f);
            AddPrim(radPoi, PrimitiveType.Cylinder, "RadPole", new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.14f, 1.0f, 0.14f), mats.Trim, removeCol: true);
            AddPrim(radPoi, PrimitiveType.Cube, "SensorBox", new Vector3(0f, 1.75f, 0f), Vector3.zero, new Vector3(0.46f, 0.42f, 0.46f), mats.BeaconAmber, removeCol: true);
            CreateWallSign(radPoi.transform, "RadSign", new Vector3(0f, 2.25f, 0f), new Vector3(0f, 180f, 0f), "RAD SENSOR", mats.Trim, new Color(1.0f, 0.72f, 0.28f), font);
            var radCol = radPoi.AddComponent<BoxCollider>();
            radCol.center = new Vector3(0f, 1.1f, 0f);
            radCol.size = new Vector3(0.90f, 2.2f, 0.90f);
            radPoi.AddComponent<InteractableProp>().Configure(InteractablePropType.SurfaceRadiationPost, "poi.rad.prompt");

            // POI 5: Water-Ice Subsurface Marker Sign (X=0, Z=-39.5)
            var icePoi = new GameObject("POI_WaterIceMarker");
            icePoi.transform.SetParent(mars.transform, false);
            icePoi.transform.position = new Vector3(0f, -0.08f, -39.5f);
            AddPrim(icePoi, PrimitiveType.Cylinder, "IceVeinPatch", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(4.2f, 0.04f, 3.6f), mats.IcePatch, removeCol: true);
            AddPrim(icePoi, PrimitiveType.Cylinder, "MarkerPost", new Vector3(0f, 0.95f, 0f), Vector3.zero, new Vector3(0.12f, 0.95f, 0.12f), mats.Trim, removeCol: true);
            CreateWallSign(icePoi.transform, "IceSign", new Vector3(0f, 1.75f, 0f), new Vector3(0f, 180f, 0f), "WATER ICE VEIN", mats.Trim, new Color(0.42f, 0.92f, 1.0f), font);
            var iceCol = icePoi.AddComponent<BoxCollider>();
            iceCol.center = new Vector3(0f, 1.0f, 0f);
            iceCol.size = new Vector3(1.4f, 2.0f, 1.4f);
            icePoi.AddComponent<InteractableProp>().Configure(InteractablePropType.SurfaceIceMarker, "poi.ice.prompt");

            var surfSpawn = new GameObject("SurfaceSpawn");
            surfSpawn.transform.SetParent(mars.transform, false);
            surfSpawn.transform.position = new Vector3(0f, 0.92f, -19.80f);
            surfSpawn.transform.eulerAngles = new Vector3(0f, 180f, 0f);
            surfaceSpawn = surfSpawn.transform;
        }

        // =========================================================================
        // SMALL PRIMITIVE & WALL HELPERS
        // =========================================================================

        private static void AddFloorAndCeiling(Transform parent, string tag, float cx, float cz, float width, float depth, Material floorMat, Material ceilMat)
        {
            var fl = AddPrim(parent.gameObject, PrimitiveType.Cube, $"{tag}_Floor", new Vector3(cx, -0.05f, cz), Vector3.zero, new Vector3(width, 0.10f, depth), floorMat, removeCol: false);
            fl.isStatic = true;
            var cl = AddPrim(parent.gameObject, PrimitiveType.Cube, $"{tag}_Ceiling", new Vector3(cx, 3.05f, cz), Vector3.zero, new Vector3(width, 0.10f, depth), ceilMat, removeCol: false);
            cl.isStatic = true;
        }

        private static void PlaceStaticWall(GameObject wallPrefab, Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.isStatic = true;
        }

        private static void CreateBoundaryWall(Transform parent, string name, Vector3 pos, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = layer;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
        }

        private static void CreateWallSign(Transform parent, string name, Vector3 worldPos, Vector3 euler, string text, Material backMat, Color textColor, TMP_FontAsset font)
        {
            var sign = new GameObject(name);
            sign.transform.SetParent(parent, false);
            sign.transform.position = worldPos;
            sign.transform.eulerAngles = euler;
            AddPrim(sign, PrimitiveType.Cube, "SignPlate", Vector3.zero, Vector3.zero, new Vector3(1.15f, 0.24f, 0.04f), backMat, removeCol: true);

            var txtGo = new GameObject("SignText");
            txtGo.transform.SetParent(sign.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, -0.025f);
            txtGo.transform.localScale = new Vector3(0.048f, 0.048f, 1f);
            var tmp = txtGo.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 5.4f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.rectTransform.sizeDelta = new Vector2(22f, 4f);
        }

        private static Light CreatePointLightObj(Transform parent, string name, Vector3 pos, Color col, float intensity, float range)
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

        private static GameObject AddPrim(GameObject parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat, bool removeCol)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            if (removeCol)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
            }
            return go;
        }

        private static void DestroyChildIfFound(Transform parent, string childName)
        {
            var c = parent.Find(childName);
            if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }
    }
}
