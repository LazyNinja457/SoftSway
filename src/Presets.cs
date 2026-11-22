using System;
using System.Collections.Generic;
using System.IO;

namespace SoftSway
{
    /// <summary>One-click "feel" presets plus user preset files on disk.</summary>
    public static class Presets
    {
        public struct Feel
        {
            public readonly string Name;
            public readonly string Tip;
            public readonly float Bounce, Softness, Response;

            public Feel(string name, string tip, float bounce, float softness, float response)
            {
                Name = name; Tip = tip; Bounce = bounce; Softness = softness; Response = response;
            }
        }

        public static readonly Feel[] BuiltIn =
        {
            new Feel("Firm", "Tight and subtle, barely moves", 0.35f, 0.55f, 0.60f),
            new Feel("Natural", "Recommended baseline", 1.00f, 1.00f, 1.00f),
            new Feel("Soft", "Heavier, slower, more fluid", 1.15f, 1.45f, 1.10f),
            new Feel("Bouncy", "Lively, springy wobble", 1.50f, 1.05f, 1.40f),
            new Feel("Jelly", "Exaggerated, for fun", 1.90f, 1.70f, 1.75f),
        };

        public static string Folder
        {
            get { return Path.Combine(BepInEx.Paths.ConfigPath, "SoftSway"); }
        }

        private static string PresetFolder
        {
            get { return Path.Combine(Folder, "presets"); }
        }

        public static string DefaultFile
        {
            get { return Path.Combine(Folder, "default.txt"); }
        }

        public static List<string> ListUserPresets()
        {
            var list = new List<string>();
            try
            {
                if (!Directory.Exists(PresetFolder)) return list;
                foreach (string f in Directory.GetFiles(PresetFolder, "*.txt"))
                    list.Add(Path.GetFileNameWithoutExtension(f));
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                SoftSwayPlugin.Log.LogWarning("Could not list presets: " + e.Message);
            }
            return list;
        }

        public static string SanitizeName(string name)
        {
            if (name == null) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return name.Trim();
        }

        public static bool SaveUser(string name, CharacterSettings settings)
        {
            name = SanitizeName(name);
            if (name.Length == 0) return false;
            return WriteFile(Path.Combine(PresetFolder, name + ".txt"), settings);
        }

        public static CharacterSettings LoadUser(string name)
        {
            return ReadFile(Path.Combine(PresetFolder, SanitizeName(name) + ".txt"));
        }

        public static void DeleteUser(string name)
        {
            try { File.Delete(Path.Combine(PresetFolder, SanitizeName(name) + ".txt")); }
            catch (Exception e) { SoftSwayPlugin.Log.LogWarning("Could not delete preset: " + e.Message); }
        }

        public static bool WriteFile(string path, CharacterSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, settings.ToText());
                return true;
            }
            catch (Exception e)
            {
                SoftSwayPlugin.Log.LogError("Could not save " + path + ": " + e.Message);
                return false;
            }
        }

        public static CharacterSettings ReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? CharacterSettings.FromText(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                SoftSwayPlugin.Log.LogError("Could not read " + path + ": " + e.Message);
                return null;
            }
        }
    }
}
