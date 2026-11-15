using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outpost.Editor
{
    /// <summary>
    /// Configures release WebGL PlayerSettings, builds Mars Outpost - Last Shift to Builds/WebGL/,
    /// packages itch.io-ready Builds/MarsOutpost_WebGL.zip (index.html at root), and writes Builds/BUILD_NOTES.txt.
    /// </summary>
    public static class WebGLBuildPipeline
    {
        public const string OutputDirectory = "Builds/WebGL";
        public const string ZipOutputPath = "Builds/MarsOutpost_WebGL.zip";
        public const string BuildNotesPath = "Builds/BUILD_NOTES.txt";
        public const string TargetProductName = "Mars Outpost - Last Shift";

        [MenuItem("Outpost/Build/Release WebGL", false, 100)]
        public static void BuildReleaseWebGL()
        {
            RunReleaseBuild(autoRun: false);
        }

        [MenuItem("Outpost/Build/Release WebGL and Run", false, 101)]
        public static void BuildReleaseWebGLAndRun()
        {
            RunReleaseBuild(autoRun: true);
        }

        [MenuItem("Outpost/Build/Configure WebGL Release PlayerSettings", false, 120)]
        public static void ConfigureWebGLReleaseSettingsMenu()
        {
            ConfigureWebGLReleaseSettings();
            Debug.Log("[WebGLBuildPipeline] Applied WebGL release PlayerSettings.");
        }

        /// <summary>
        /// Applies the final release WebGL configuration specified for Mars Outpost - Last Shift.
        /// </summary>
        public static void ConfigureWebGLReleaseSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            PlayerSettings.productName = TargetProductName;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);

            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Executes the clean Release WebGL build, generates Builds/MarsOutpost_WebGL.zip and Builds/BUILD_NOTES.txt,
        /// and logs exact output sizes and top packed contributors.
        /// </summary>
        public static BuildReport RunReleaseBuild(bool autoRun)
        {
            WebGLQaAuditor.ClearDevPlayerPrefs();
            ConfigureWebGLReleaseSettings();

            string[] scenes = CollectEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuildPipeline] No enabled scenes found in EditorBuildSettings.");
                return null;
            }

            Directory.CreateDirectory("Builds");
            CleanDirectorySafely(OutputDirectory);
            Directory.CreateDirectory(OutputDirectory);

            BuildOptions options = autoRun ? BuildOptions.AutoRunPlayer : BuildOptions.None;
            var playerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDirectory,
                target = BuildTarget.WebGL,
                options = options
            };

            Debug.Log($"[WebGLBuildPipeline] Starting Release WebGL build ({scenes.Length} scenes) -> '{OutputDirectory}'...");
            BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                string resultName = report != null ? report.summary.result.ToString() : "NullReport";
                int errs = report != null ? report.summary.totalErrors : -1;
                Debug.LogError($"[WebGLBuildPipeline] WebGL build failed: result={resultName}, errors={errs}");
                return report;
            }

            List<(string fullPath, string relPath, long bytes)> shippableFiles = CollectShippableOutputFiles(OutputDirectory);
            CreateWebGLZip(shippableFiles, ZipOutputPath);
            WriteBuildNotes(BuildNotesPath, report, OutputDirectory, ZipOutputPath, scenes, shippableFiles);

            long folderBytes = shippableFiles.Sum(f => f.bytes);
            long zipBytes = File.Exists(ZipOutputPath) ? new FileInfo(ZipOutputPath).Length : 0;
            Debug.Log($"[WebGLBuildPipeline] SUCCEEDED in {report.summary.totalTime.TotalSeconds:0.0}s | Errors={report.summary.totalErrors} | Warnings={report.summary.totalWarnings} | Folder={FormatSize(folderBytes)} | Zip={FormatSize(zipBytes)}");

            LogOutputBreakdown(shippableFiles, report);
            return report;
        }

        [MenuItem("Outpost/Build/Package Existing WebGL Output", false, 105)]
        public static void PackageExistingWebGLOutput()
        {
            if (!Directory.Exists(OutputDirectory) || !File.Exists(Path.Combine(OutputDirectory, "index.html")))
            {
                Debug.LogError($"[WebGLBuildPipeline] Cannot package: '{OutputDirectory}/index.html' not found.");
                return;
            }

            List<(string fullPath, string relPath, long bytes)> shippableFiles = CollectShippableOutputFiles(OutputDirectory);
            CreateWebGLZip(shippableFiles, ZipOutputPath);
            WriteBuildNotes(BuildNotesPath, null, OutputDirectory, ZipOutputPath, CollectEnabledScenes(), shippableFiles);

            long folderBytes = shippableFiles.Sum(f => f.bytes);
            long zipBytes = File.Exists(ZipOutputPath) ? new FileInfo(ZipOutputPath).Length : 0;
            Debug.Log($"[WebGLBuildPipeline] Packaged existing WebGL output: Files={shippableFiles.Count} | Folder={FormatSize(folderBytes)} | Zip={FormatSize(zipBytes)}");
        }

        private static void CleanDirectorySafely(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
                File.Delete(file);
            foreach (string subDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                Directory.Delete(subDir, recursive: true);
            try
            {
                Directory.Delete(dir, recursive: false);
            }
            catch (IOException)
            {
                // Root folder handle held open (e.g., Explorer); contents are cleared so BuildPlayer can overwrite cleanly.
            }
        }

        private static string[] CollectEnabledScenes()
        {
            var list = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                    list.Add(scenes[i].path);
            }
            return list.ToArray();
        }

        private static List<(string fullPath, string relPath, long bytes)> CollectShippableOutputFiles(string outputDir)
        {
            var result = new List<(string fullPath, string relPath, long bytes)>();
            if (!Directory.Exists(outputDir)) return result;

            string fullRoot = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] files = Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string full = Path.GetFullPath(files[i]);
                if (full.Length <= fullRoot.Length + 1) continue;
                string rel = full.Substring(fullRoot.Length + 1).Replace('\\', '/');
                if (rel.IndexOf("_DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rel.IndexOf("_BackUpThisFolder", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                result.Add((full, rel, new FileInfo(full).Length));
            }

            return result.OrderByDescending(f => f.bytes).ToList();
        }

        private static void CreateWebGLZip(List<(string fullPath, string relPath, long bytes)> shippableFiles, string zipPath)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var sorted = shippableFiles.OrderBy(f => f.relPath, StringComparer.OrdinalIgnoreCase).ToList();
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(sorted[i].relPath, System.IO.Compression.CompressionLevel.Optimal);
                    using (var inStream = File.OpenRead(sorted[i].fullPath))
                    using (var outStream = entry.Open())
                    {
                        inStream.CopyTo(outStream);
                    }
                }
            }
        }

        private static void WriteBuildNotes(
            string notesPath,
            BuildReport report,
            string outputDir,
            string zipPath,
            string[] scenes,
            List<(string fullPath, string relPath, long bytes)> shippableFiles)
        {
            long folderBytes = shippableFiles.Sum(f => f.bytes);
            long zipBytes = File.Exists(zipPath) ? new FileInfo(zipPath).Length : 0;
            var topPacked = CollectTopPackedAssets(report, 15);

            string resultText = report != null ? report.summary.result.ToString() : "Succeeded (Re-packaged)";
            string durationText = report != null ? $"{report.summary.totalTime.TotalSeconds:0.0} s" : "(existing output packaged)";
            int errCount = report != null ? report.summary.totalErrors : 0;
            int warnCount = report != null ? report.summary.totalWarnings : 0;

            var sb = new StringBuilder(4096);
            sb.AppendLine("=========================================================");
            sb.AppendLine($"{TargetProductName.ToUpperInvariant()} — RELEASE WEBGL BUILD NOTES");
            sb.AppendLine("=========================================================");
            sb.AppendLine($"Built (UTC):           {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine($"Unity Version:         {Application.unityVersion}");
            sb.AppendLine($"Build Result:          {resultText}");
            sb.AppendLine($"Build Duration:        {durationText}");
            sb.AppendLine($"Compiler/Build Errors: {errCount}");
            sb.AppendLine($"Build Warnings:        {warnCount}");
            sb.AppendLine($"Output Directory:      {outputDir.Replace('\\', '/')}/ ({FormatSize(folderBytes)} shippable)");
            sb.AppendLine($"Itch.io Package Zip:   {zipPath.Replace('\\', '/')} ({FormatSize(zipBytes)})");
            sb.AppendLine();
            sb.AppendLine("PLAYER & WEBGL SETTINGS");
            sb.AppendLine($"- Product Name:        {PlayerSettings.productName}");
            sb.AppendLine($"- Graphics APIs:       {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL))} (Auto={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL)})");
            sb.AppendLine($"- Compression Format:  {PlayerSettings.WebGL.compressionFormat} (Decompression Fallback={PlayerSettings.WebGL.decompressionFallback})");
            sb.AppendLine($"- Exception Support:   {PlayerSettings.WebGL.exceptionSupport}");
            sb.AppendLine($"- Managed Stripping:   {PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)}");
            sb.AppendLine($"- Memory (MB):         Initial={PlayerSettings.WebGL.initialMemorySize}, Growth={PlayerSettings.WebGL.memoryGrowthMode}, Max={PlayerSettings.WebGL.maximumMemorySize}");
            sb.AppendLine($"- Multithreading:      {PlayerSettings.WebGL.threadsSupport}");
            sb.AppendLine($"- Template:            {PlayerSettings.WebGL.template}");
            sb.AppendLine($"- Viewport (default):  {PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight}");
            sb.AppendLine($"- Run In Background:   {PlayerSettings.runInBackground}");
            sb.AppendLine($"- Release Options:     BuildOptions.None (Development={EditorUserBuildSettings.development})");
            sb.AppendLine();
            sb.AppendLine($"INCLUDED SCENES ({scenes.Length})");
            for (int i = 0; i < scenes.Length; i++)
                sb.AppendLine($"  [{i}] {scenes[i]}");
            sb.AppendLine();
            sb.AppendLine($"SHIPPABLE OUTPUT FILES ({shippableFiles.Count}, total {FormatSize(folderBytes)})");
            for (int i = 0; i < shippableFiles.Count; i++)
                sb.AppendLine($"  {FormatSize(shippableFiles[i].bytes),12} | {shippableFiles[i].relPath}");
            sb.AppendLine();
            if (topPacked.Count > 0)
            {
                sb.AppendLine("TOP PACKED ASSET CONTRIBUTORS (BUILD REPORT)");
                for (int i = 0; i < topPacked.Count; i++)
                    sb.AppendLine($"  #{i + 1:00} {FormatSize((long)topPacked[i].bytes),12} | {topPacked[i].path}");
                sb.AppendLine();
            }
            sb.AppendLine("LOCAL CHROME TEST COMMAND (HTTP SERVER REQUIRED FOR WEBGL)");
            sb.AppendLine("  cd Builds/WebGL");
            sb.AppendLine("  python -m http.server 8000");
            sb.AppendLine("  Open: http://localhost:8000 in Google Chrome");
            sb.AppendLine("  Note: Do not open index.html directly via file://; Chrome blocks gzipped .wasm/.data fetches on file://.");
            sb.AppendLine();
            sb.AppendLine("ITCH.IO UPLOAD INSTRUCTIONS");
            sb.AppendLine($"1. Upload {zipPath.Replace('\\', '/')} (index.html, Build/, TemplateData/ are at archive root).");
            sb.AppendLine("2. Kind of project: HTML (played in browser).");
            sb.AppendLine("3. Embed options: Viewport 1280x720 (or responsive), Enable Fullscreen button.");
            sb.AppendLine("4. SharedArrayBuffer support: NOT required (WebGL multithreading is disabled).");
            sb.AppendLine("5. Gzip Decompression Fallback is ON, so it boots cleanly even if host headers omit Content-Encoding: gzip.");

            File.WriteAllText(notesPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static List<(string path, ulong bytes)> CollectTopPackedAssets(BuildReport report, int limit)
        {
            var totals = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            PackedAssets[] packed = report != null ? report.packedAssets : null;
            if (packed != null)
            {
                for (int p = 0; p < packed.Length; p++)
                {
                    PackedAssetInfo[] contents = packed[p].contents;
                    if (contents == null) continue;
                    for (int c = 0; c < contents.Length; c++)
                    {
                        string assetPath = string.IsNullOrEmpty(contents[c].sourceAssetPath) ? "<built-in/other>" : contents[c].sourceAssetPath;
                        if (totals.TryGetValue(assetPath, out ulong cur))
                            totals[assetPath] = cur + contents[c].packedSize;
                        else
                            totals[assetPath] = contents[c].packedSize;
                    }
                }
            }

            return totals.OrderByDescending(kv => kv.Value)
                .Take(limit)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }

        private static void LogOutputBreakdown(List<(string fullPath, string relPath, long bytes)> shippableFiles, BuildReport report)
        {
            int limit = Math.Min(10, shippableFiles.Count);
            for (int i = 0; i < limit; i++)
                Debug.Log($"[WebGLBuildPipeline] Out {i + 1}: {FormatSize(shippableFiles[i].bytes)} | {shippableFiles[i].relPath}");

            var topPacked = CollectTopPackedAssets(report, 10);
            for (int i = 0; i < topPacked.Count; i++)
                Debug.Log($"[WebGLBuildPipeline] Packed {i + 1}: {FormatSize((long)topPacked[i].bytes)} | {topPacked[i].path}");
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024f * 1024f):0.00} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024f:0.0} KB";
            return $"{bytes} B";
        }
    }
}
