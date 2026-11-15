using System.Collections.Generic;
using Outpost.Runtime.Rendering;
using Outpost.Runtime.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost.Editor
{
    /// <summary>
    /// Configures realistic dark-facility interior lighting in Outpost.unity:
    /// adds industrial bulkhead ceiling fixtures and hydroponic grow-light bars,
    /// tightens point light ranges/colors for distinct pools of light,
    /// softens overly bright corridor neon strips, and updates RoomStateBinder references.
    /// </summary>
    public static class FacilityLightingEditor
    {
        private const string RoomMatDir = "Assets/Materials/Room";

        [MenuItem("Outpost/Setup Dark Facility Lighting")]
        public static void ApplyDarkFacilityLighting()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Outpost")
            {
                Debug.LogWarning("[FacilityLightingEditor] Open Assets/Scenes/Outpost.unity before running Setup Dark Facility Lighting.");
                return;
            }

            Material frameMat = AssetDatabase.LoadAssetAtPath<Material>(RoomMatDir + "/M_Room_Trim.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>(RoomMatDir + "/M_Kit_steel.mat");
            Material rodMat = frameMat;

            Material warmDiffuser = EnsureUnlitEmissiveMat("M_Fixture_WarmWhite", new Color(1.0f, 0.84f, 0.62f, 1f), 1.30f);
            Material coolDiffuser = EnsureUnlitEmissiveMat("M_Fixture_CoolCyan", new Color(0.74f, 0.90f, 1.0f, 1f), 1.30f);
            Material amberDiffuser = EnsureUnlitEmissiveMat("M_Fixture_Amber", new Color(1.0f, 0.70f, 0.38f, 1f), 1.25f);
            Material growDiffuser = EnsureUnlitEmissiveMat("M_Fixture_GrowPink", new Color(1.0f, 0.56f, 0.88f, 1f), 1.40f);

            // Soften corridor kit cyan trim slightly so neon strips accentuate a dark facility rather than glaring
            var kitCyan = AssetDatabase.LoadAssetAtPath<Material>(RoomMatDir + "/M_Kit_glow_cyan.mat");
            if (kitCyan != null)
            {
                kitCyan.SetColor("_EmissionColor", new Color(0.24f, 0.90f, 0.96f, 1f));
                if (kitCyan.HasProperty("_EmissionStrength"))
                    kitCyan.SetFloat("_EmissionStrength", 0.72f);
                EditorUtility.SetDirty(kitCyan);
            }

            // 1. Command Module lighting + bulkhead
            var cmdRoot = GameObject.Find("CommandModuleRoom")?.transform;
            var roomLightRoot = GameObject.Find("RoomLighting")?.transform;
            if (cmdRoot != null)
            {
                RemoveChildrenByPrefix(cmdRoot, "Bulkhead_");
                BuildBulkheadFixture(cmdRoot, "Bulkhead_CommandCenter", new Vector3(0.25f, 2.97f, 0.25f), 0f, 1.05f, frameMat, coolDiffuser);
            }

            Light lCabin = TunePointLight("Light_CabinSoftFill", new Vector3(0.25f, 2.68f, 0.25f), new Color(0.78f, 0.86f, 0.98f, 1f), 0.78f, 4.6f);
            Light lDesk = TunePointLight("Light_DeskLampWarm", new Vector3(-0.66f, 1.18f, 1.38f), new Color(1.0f, 0.74f, 0.42f, 1f), 1.90f, 3.5f);
            Light lWindow = TunePointLight("Light_WindowCoolFill", new Vector3(0f, 1.62f, 2.38f), new Color(0.76f, 0.54f, 0.48f, 1f), 0.48f, 2.9f);
            // Make sure desk lamp bulb shade emission matches warm glow
            var deskBulbMat = AssetDatabase.LoadAssetAtPath<Material>(RoomMatDir + "/M_Desk_LampBulb.mat");
            if (deskBulbMat != null)
            {
                deskBulbMat.SetColor("_EmissionColor", new Color(1.0f, 0.76f, 0.44f, 1f) * 1.45f);
                EditorUtility.SetDirty(deskBulbMat);
            }

            // 2. Hub Corridor bulkheads & pools
            var hubRoot = GameObject.Find("OutpostExpansionWorld/HubCorridor")?.transform;
            if (hubRoot != null)
            {
                RemoveChildrenByPrefix(hubRoot, "Bulkhead_");
                BuildBulkheadFixture(hubRoot, "Bulkhead_CorrNorth", new Vector3(0f, 3.37f, -4.16f), 0f, 1.15f, frameMat, warmDiffuser);
                BuildBulkheadFixture(hubRoot, "Bulkhead_CorrAlcove", new Vector3(-3.80f, 3.37f, -4.16f), 90f, 1.0f, frameMat, amberDiffuser);
                BuildBulkheadFixture(hubRoot, "Bulkhead_CorrHub", new Vector3(0f, 3.37f, -8.16f), 0f, 1.20f, frameMat, coolDiffuser);
                BuildBulkheadFixture(hubRoot, "Bulkhead_CorrSouth", new Vector3(0f, 3.37f, -12.16f), 0f, 1.15f, frameMat, warmDiffuser);
                BuildBulkheadFixture(hubRoot, "Bulkhead_CorrMedbay", new Vector3(-5.06f, 3.37f, 1.28f), 90f, 1.0f, frameMat, coolDiffuser);
            }

            Light lCorrNorth = TunePointLight("Light_CorrNorthWarm", new Vector3(0f, 2.86f, -4.16f), new Color(1.0f, 0.84f, 0.64f, 1f), 1.35f, 4.6f);
            Light lCorrAlcove = TunePointLight("Light_CorrAlcoveWarm", new Vector3(-3.80f, 2.86f, -4.16f), new Color(1.0f, 0.72f, 0.44f, 1f), 1.15f, 4.0f);
            Light lCorrHub = TunePointLight("Light_CorrHubCool", new Vector3(0f, 2.86f, -8.16f), new Color(0.74f, 0.90f, 1.0f, 1f), 1.40f, 4.8f);
            Light lCorrSouth = TunePointLight("Light_CorrSouthWarm", new Vector3(0f, 2.86f, -12.16f), new Color(0.98f, 0.80f, 0.58f, 1f), 1.25f, 4.4f);

            Light lCorrMedbay = null;
            if (hubRoot != null)
            {
                lCorrMedbay = EnsurePointLight(hubRoot, "Light_CorrMedbay", new Vector3(-5.06f, 2.86f, 1.28f), new Color(0.74f, 0.88f, 1.0f, 1f), 1.10f, 4.1f);
            }

            // 3. Medbay clinical ceiling light
            var medRoot = GameObject.Find("OutpostExpansionWorld/Room_Medbay")?.transform;
            if (medRoot != null)
            {
                RemoveChildrenByPrefix(medRoot, "Bulkhead_");
                BuildBulkheadFixture(medRoot, "Bulkhead_Medbay", new Vector3(-8.93f, 2.97f, 1.28f), 0f, 1.15f, frameMat, coolDiffuser);
            }
            Light lMedbay = TunePointLight("Light_Medbay", new Vector3(-8.94f, 2.74f, 1.28f), new Color(0.68f, 0.90f, 1.0f, 1f), 1.18f, 4.1f);

            // 4. Greenhouse suspended grow-light bars
            var ghRoot = GameObject.Find("OutpostExpansionWorld/Room_Greenhouse")?.transform;
            if (ghRoot != null)
            {
                RemoveChildrenByPrefix(ghRoot, "GrowBar_");
                float[] rowZs = { -6.86f, -8.16f, -9.46f };
                for (int i = 0; i < rowZs.Length; i++)
                {
                    BuildSuspendedGrowBar(ghRoot, $"GrowBar_{i}", new Vector3(-8.63f, 2.42f, rowZs[i]), 3.00f, frameMat, rodMat, growDiffuser);
                }
            }
            Light lGreenhouse = TunePointLight("Light_GreenhouseGrow", new Vector3(-8.63f, 2.34f, -8.16f), new Color(1.0f, 0.60f, 0.90f, 1f), 1.38f, 4.8f);

            // 5. Storage caged industrial work light
            var stRoot = GameObject.Find("OutpostExpansionWorld/Room_Storage")?.transform;
            if (stRoot != null)
            {
                RemoveChildrenByPrefix(stRoot, "Bulkhead_");
                BuildBulkheadFixture(stRoot, "Bulkhead_Storage", new Vector3(8.41f, 2.97f, -8.16f), 0f, 1.25f, frameMat, warmDiffuser);
            }
            Light lStorage = TunePointLight("Light_Storage", new Vector3(8.41f, 2.72f, -8.16f), new Color(0.96f, 0.86f, 0.66f, 1f), 1.18f, 4.4f);

            // 6. Airlock chamber tight cool light
            Light lAirlock = TunePointLight("Light_AirlockChamber", new Vector3(0f, 2.70f, -15.82f), new Color(0.78f, 0.88f, 1.0f, 1f), 0.92f, 3.5f);

            // 7. Wire RoomStateBinder mainRoomLights so power shortage & blackout dim all room lights cleanly
            var binder = Object.FindAnyObjectByType<RoomStateBinder>();
            if (binder != null)
            {
                var roomLights = new List<Light>();
                void AddIfNotNull(Light l) { if (l != null && !roomLights.Contains(l)) roomLights.Add(l); }
                AddIfNotNull(lAirlock);
                AddIfNotNull(lGreenhouse);
                AddIfNotNull(lMedbay);
                AddIfNotNull(lWindow);
                AddIfNotNull(GameObject.Find("Radio_PointLight")?.GetComponent<Light>());
                AddIfNotNull(lCabin);
                AddIfNotNull(lCorrHub);
                AddIfNotNull(lCorrAlcove);
                AddIfNotNull(lDesk);
                AddIfNotNull(lCorrNorth);
                AddIfNotNull(lStorage);
                AddIfNotNull(lCorrSouth);
                AddIfNotNull(lCorrMedbay);

                var so = new SerializedObject(binder);
                var prop = so.FindProperty("mainRoomLights");
                if (prop != null)
                {
                    prop.arraySize = roomLights.Count;
                    for (int i = 0; i < roomLights.Count; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = roomLights[i];
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(binder);
                }
            }

            // Ensure PSXRenderController has interior darkness enabled
            var psxCtrl = Object.FindAnyObjectByType<PSXRenderController>();
            if (psxCtrl != null)
            {
                var so = new SerializedObject(psxCtrl);
                var prop = so.FindProperty("enableFacilityInteriorDarkness");
                if (prop != null)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(psxCtrl);
                }
                psxCtrl.ApplyPipelineState();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[FacilityLightingEditor] Applied dark facility interior lighting and ceiling fixtures.");
        }

        private static Light TunePointLight(string objName, Vector3 worldPos, Color col, float intensity, float range)
        {
            var go = GameObject.Find(objName);
            if (go == null) return null;
            var l = go.GetComponent<Light>();
            if (l == null) return null;
            go.transform.position = worldPos;
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(l);
            return l;
        }

        private static Light EnsurePointLight(Transform parent, string objName, Vector3 worldPos, Color col, float intensity, float range)
        {
            var existing = parent.Find(objName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var l = go.GetComponent<Light>();
            if (l == null) l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(l);
            return l;
        }

        private static void RemoveChildrenByPrefix(Transform parent, string prefix)
        {
            var toRemove = new List<GameObject>();
            foreach (Transform c in parent)
            {
                if (c.name.StartsWith(prefix))
                    toRemove.Add(c.gameObject);
            }
            foreach (var go in toRemove)
                Object.DestroyImmediate(go);
        }

        private static void BuildBulkheadFixture(
            Transform parent,
            string name,
            Vector3 worldPos,
            float yawDeg,
            float length,
            Material frameMat,
            Material diffuserMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = worldPos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            // Outer metal housing mounted flush to ceiling
            CreatePart(root.transform, "Housing", new Vector3(0f, 0.0f, 0f), new Vector3(0.32f, 0.06f, length), frameMat);
            // Glowing diffuser strip underneath
            CreatePart(root.transform, "Diffuser", new Vector3(0f, -0.032f, 0f), new Vector3(0.20f, 0.024f, length * 0.86f), diffuserMat);
            // Industrial cage ribs across diffuser
            int ribCount = 3;
            for (int i = 0; i < ribCount; i++)
            {
                float t = (i / (float)(ribCount - 1)) - 0.5f;
                CreatePart(root.transform, $"Rib_{i}", new Vector3(0f, -0.045f, t * (length * 0.65f)), new Vector3(0.26f, 0.014f, 0.035f), frameMat);
            }
        }

        private static void BuildSuspendedGrowBar(
            Transform parent,
            string name,
            Vector3 worldPos,
            float ceilingY,
            Material frameMat,
            Material rodMat,
            Material growDiffuser)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = worldPos;

            float rodHeight = Mathf.Max(0.1f, ceilingY - worldPos.y);
            CreatePart(root.transform, "Rod_West", new Vector3(-0.75f, rodHeight * 0.5f, 0f), new Vector3(0.03f, rodHeight, 0.03f), rodMat);
            CreatePart(root.transform, "Rod_East", new Vector3(0.75f, rodHeight * 0.5f, 0f), new Vector3(0.03f, rodHeight, 0.03f), rodMat);
            CreatePart(root.transform, "Hood", new Vector3(0f, 0.025f, 0f), new Vector3(1.90f, 0.05f, 0.24f), frameMat);
            CreatePart(root.transform, "GrowTube", new Vector3(0f, -0.008f, 0f), new Vector3(1.76f, 0.025f, 0.14f), growDiffuser);
        }

        private static void CreatePart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static Material EnsureUnlitEmissiveMat(string matName, Color color, float emissionStrength)
        {
            string path = $"{RoomMatDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Outpost/PSXUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(sh) { name = matName };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_Color", color);
            mat.SetColor("_EmissionColor", color);
            if (mat.HasProperty("_EmissionStrength"))
                mat.SetFloat("_EmissionStrength", emissionStrength);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
