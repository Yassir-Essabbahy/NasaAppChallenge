using System.Collections.Generic;
using System.IO;
using System.Text;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Generates Assets/Credits/ASSET_SOURCES.md directly from the shared SourcesDatabase
    /// so in-game Credits, Sources app, and documentation always stay in sync.
    /// </summary>
    public static class CreditsMarkdownExporter
    {
        public const string CreditsDir = "Assets/Credits";
        public const string CreditsFile = "Assets/Credits/ASSET_SOURCES.md";

        private static readonly string[] RequiredSections =
        {
            "NASA/USGS data and models",
            "Corridor kit",
            "UI icons",
            "Fonts",
            "Packages",
            "Generated in-project",
            "Tools and AI assistance"
        };

        [MenuItem("Outpost/Export Credits to Markdown")]
        public static void ExportAssetSourcesMarkdown()
        {
            if (!Directory.Exists(CreditsDir))
                Directory.CreateDirectory(CreditsDir);
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            SourcesDatabase db = AssetDatabase.LoadAssetAtPath<SourcesDatabase>("Assets/Resources/SourcesDatabase.asset");
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<SourcesDatabase>();
                AssetDatabase.CreateAsset(db, "Assets/Resources/SourcesDatabase.asset");
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }

            // Always sync default asset credits and science entries from code defaults
            {
                var fresh = ScriptableObject.CreateInstance<SourcesDatabase>();
                db.AssetCredits = fresh.AssetCredits;
                db.Entries = fresh.Entries;
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }

            var sb = new StringBuilder();
            sb.AppendLine("# ASSET_SOURCES & Project Credits");
            sb.AppendLine();
            sb.AppendLine("Master record of every external asset, texture, 3D model, font, UI icon, package, procedural generator, and AI/development tool used in this project.");
            sb.AppendLine("Generated cleanly from shared `SourcesDatabase` (`Assets/Resources/SourcesDatabase.asset`) — keep updated whenever an asset or package is added.");
            sb.AppendLine();

            foreach (string section in RequiredSections)
            {
                sb.AppendLine($"## {section}");
                sb.AppendLine();
                sb.AppendLine("| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |");
                sb.AppendLine("|---|---|---|---|---|---|---|");

                int count = 0;
                for (int i = 0; i < db.AssetCredits.Count; i++)
                {
                    var rec = db.AssetCredits[i];
                    if (rec.Section != section) continue;
                    sb.AppendLine($"| {Esc(rec.Title)} | {Esc(rec.Organization)} | {Esc(rec.UrlText)} | {Esc(rec.License)} | {Esc(rec.DateAdded)} | {Esc(rec.WhereUsed)} | {Esc(rec.ModifiedNotes)} |");
                    count++;
                }
                if (count == 0)
                    sb.AppendLine("| (none) | - | - | - | - | - | - |");

                sb.AppendLine();
            }

            sb.AppendLine("## Scientific & Fact Sources (In-Game Sources App & POIs)");
            sb.AppendLine();
            sb.AppendLine("| Source ID | Title | Organization | URL | Used By Constants / Events / POIs |");
            sb.AppendLine("|---|---|---|---|---|");
            if (db.Entries != null)
            {
                for (int i = 0; i < db.Entries.Count; i++)
                {
                    var e = db.Entries[i];
                    sb.AppendLine($"| `{Esc(e.Id)}` | {Esc(e.Title)} | {Esc(e.Organization)} | {Esc(e.UrlText)} | {Esc(e.UsedByConstantsAndEvents)} |");
                }
            }
            sb.AppendLine();

            File.WriteAllText(CreditsFile, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CreditsFile, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[CreditsMarkdownExporter] Wrote {CreditsFile} ({db.AssetCredits.Count} asset credits, {db.Entries?.Count ?? 0} science sources).");
        }

        private static string Esc(string s)
        {
            return string.IsNullOrEmpty(s) ? "-" : s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
        }
    }
}
