using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost.Editor
{
    /// <summary>
    /// Orchestrates the entire Mars Terrain + NASA Assets + Corridor Kit + UI Icons + Credits pipeline
    /// on the active Outpost scene and verifies zero pink/missing materials.
    /// </summary>
    public static class FullIntegrationRunner
    {
        [MenuItem("Outpost/Run Full Asset Integration Pass")]
        public static void RunFullPass()
        {
            Debug.Log("=== STARTING FULL ASSET INTEGRATION PASS ===");

            // 1. Part D: Build 20-icon 128px IconLibrary + updated UITheme
            TerminalIconAssetBuilder.BuildIconLibraryAndUpdateTheme();

            // 2. Part C: Interior Corridor Kit FBX Prefabs + HubCorridor Chain + Props + Lights
            CorridorKitBuilder.RebuildInteriorWithCorridorKit();

            // 3. Part B: Procedural Play Terrain (300m, res 513) + BackdropTerrain (1200m) + Rocks + Sky/Fog
            MarsTerrainGenerator.GenerateAllMarsTerrain();

            // 4. Part A: Remove illogical MarsSphere + NASA Perseverance Rover + RoomHUD crosshairs
            MarsRoverAndSphereBuilder.IntegrateMarsSphereAndRover();

            // 5. Integrate newly added station kit, satellite radio, radiation sign, fix base textures & replace placeholders
            NewAssetsIntegrationBuilder.IntegrateAllNewAssets();

            // 6. Final PSX material sweep & zero-pink verification across scene + prefabs
            PSXMaterialApplier.ApplyPSXMaterialsToSceneAndSelection();
            int pinkCount = VerifyZeroPinkMaterials();

            // 7. Part E: Export Assets/Credits/ASSET_SOURCES.md
            CreditsMarkdownExporter.ExportAssetSourcesMarkdown();

            // 8. Mark active scene dirty & save cleanly
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"=== FULL ASSET INTEGRATION PASS COMPLETE (Pink/broken materials: {pinkCount}) ===");
        }

        public static int VerifyZeroPinkMaterials()
        {
            var broken = new List<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                        broken.Add($"{r.name}[{i}]");
                }
            }

            if (broken.Count > 0)
                Debug.LogError($"[FullIntegrationRunner] Found {broken.Count} pink/missing materials: {string.Join(", ", broken)}");
            else
                Debug.Log("[FullIntegrationRunner] Verified 0 pink/broken materials in active scene.");

            return broken.Count;
        }
    }
}
