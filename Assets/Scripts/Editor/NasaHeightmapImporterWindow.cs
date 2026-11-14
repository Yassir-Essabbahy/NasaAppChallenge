using System.IO;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Editor window ("Outpost/Import NASA Heightmap (RAW)") to import 16-bit NASA/USGS RAW/R16 heightmaps
    /// (auto-inferring 513/1025/2049 resolution from exact byte size) and apply them to BackdropTerrain.
    /// </summary>
    public class NasaHeightmapImporterWindow : EditorWindow
    {
        private string _rawFilePath = "";
        private int _resolutionIndex = 1; // 0: 513, 1: 1025, 2: 2049
        private readonly int[] _resOptions = { 513, 1025, 2049 };
        private readonly string[] _resLabels = { "513 x 513", "1025 x 1025", "2049 x 2049" };

        private int _byteOrderIndex = 0; // 0: Windows (Little-Endian), 1: Mac (Big-Endian)
        private readonly string[] _byteOrderLabels = { "Windows (Little-Endian / UInt16)", "Mac (Big-Endian / UInt16)" };

        private float _verticalExaggeration = 2.0f;
        private bool _keepPlayZoneClear = true;
        private string _statusMessage = "Select a 16-bit RAW / R16 heightmap file to update BackdropTerrain.";

        [MenuItem("Outpost/Import NASA Heightmap (RAW)")]
        public static void OpenWindow()
        {
            var win = GetWindow<NasaHeightmapImporterWindow>("NASA Heightmap Importer");
            win.minSize = new Vector2(460f, 340f);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NASA / USGS 16-bit Heightmap -> BackdropTerrain", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            _rawFilePath = EditorGUILayout.TextField("RAW / R16 File", _rawFilePath);
            if (GUILayout.Button("Browse...", GUILayout.Width(84f)))
            {
                string picked = EditorUtility.OpenFilePanel("Select 16-bit NASA/USGS RAW Heightmap", Application.dataPath, "raw,r16");
                if (!string.IsNullOrEmpty(picked))
                {
                    _rawFilePath = picked;
                    InferResolutionFromFileSize(picked);
                }
            }
            EditorGUILayout.EndHorizontal();

            _resolutionIndex = EditorGUILayout.Popup("Resolution", _resolutionIndex, _resLabels);
            _byteOrderIndex = EditorGUILayout.Popup("Byte Order (16-bit)", _byteOrderIndex, _byteOrderLabels);
            _verticalExaggeration = EditorGUILayout.Slider("Vertical Scale (x)", _verticalExaggeration, 0.25f, 8.0f);
            _keepPlayZoneClear = EditorGUILayout.Toggle("Keep 300m Play Area Clear", _keepPlayZoneClear);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Have a Float32 GeoTIFF (.tif) from MOLA/HiRISE/HRSC? Unity cannot import float GeoTIFFs natively. Convert it first with GDAL:\n" +
                "gdal_translate -of ENVI -ot UInt16 -outsize 1025 1025 -scale <min_m> <max_m> 0 65535 input_dem.tif mars_heightmap_1025.raw",
                MessageType.None);

            EditorGUILayout.Space(8f);
            GUI.enabled = !string.IsNullOrEmpty(_rawFilePath) && File.Exists(_rawFilePath);
            if (GUILayout.Button("Import & Update BackdropTerrain", GUILayout.Height(32f)))
            {
                ImportSelectedFile();
            }
            GUI.enabled = true;
        }

        private void InferResolutionFromFileSize(string path)
        {
            if (!File.Exists(path)) return;
            long bytes = new FileInfo(path).Length;
            for (int i = 0; i < _resOptions.Length; i++)
            {
                long expected = (long)_resOptions[i] * _resOptions[i] * 2L;
                if (bytes == expected)
                {
                    _resolutionIndex = i;
                    _statusMessage = $"Inferred {_resOptions[i]}x{_resOptions[i]} 16-bit RAW from file size ({bytes:N0} bytes = {_resOptions[i]}^2 x 2).";
                    return;
                }
            }
            _statusMessage = $"Warning: File size ({bytes:N0} bytes) does not match exact 513^2x2, 1025^2x2, or 2049^2x2 16-bit RAW sizes.";
        }

        private void ImportSelectedFile()
        {
            int res = _resOptions[_resolutionIndex];
            long expectedBytes = (long)res * res * 2L;
            byte[] raw = File.ReadAllBytes(_rawFilePath);
            if (raw.Length < expectedBytes)
            {
                _statusMessage = $"Error: File has {raw.Length:N0} bytes, need at least {expectedBytes:N0} bytes for {res}x{res} 16-bit.";
                Debug.LogError($"[NasaHeightmapImporter] {_statusMessage}");
                return;
            }

            bool bigEndian = _byteOrderIndex == 1;
            float[,] heights = new float[res, res];
            int idx = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    ushort sample = bigEndian
                        ? (ushort)((raw[idx] << 8) | raw[idx + 1])
                        : (ushort)(raw[idx] | (raw[idx + 1] << 8));
                    idx += 2;
                    heights[z, x] = sample / 65535f;
                }
            }

            var settings = MarsTerrainGenerator.EnsureTerrainSettingsObject(out Transform surfaceRoot);
            settings.verticalExaggeration = _verticalExaggeration;
            settings.lastRawPath = _rawFilePath;
            settings.lastRawResolution = res;
            EditorUtility.SetDirty(settings);

            MarsTerrainGenerator.ApplyBackdropHeightmap(heights, res, _verticalExaggeration, _keepPlayZoneClear, settings, surfaceRoot: surfaceRoot);
            AssetDatabase.SaveAssets();
            _statusMessage = $"Applied {Path.GetFileName(_rawFilePath)} ({res}x{res}, {(_byteOrderIndex == 0 ? "Little-Endian" : "Big-Endian")}, {_verticalExaggeration:F1}x) to BackdropTerrain.";
            Debug.Log($"[NasaHeightmapImporter] {_statusMessage}");
        }
    }
}
