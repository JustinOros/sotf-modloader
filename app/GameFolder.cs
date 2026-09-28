using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SotfModLoader
{
    public static class GameFolder
    {
        public const string ClientExe = "SonsOfTheForest.exe";
        public const string ServerExe = "SonsOfTheForestDS.exe";

        private static string SettingsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SotfModLoader"); }
        }

        private static string SettingsFile
        {
            get { return Path.Combine(SettingsDir, "gamedir.txt"); }
        }

        public static string Kind(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return null;
            if (File.Exists(Path.Combine(dir, ClientExe)))
                return "client";
            if (File.Exists(Path.Combine(dir, ServerExe)))
                return "server";
            return null;
        }

        public static string Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var saved = File.ReadAllText(SettingsFile).Trim();
                    if (Kind(saved) != null)
                        return saved;
                }
            }
            catch
            {
            }
            return Find();
        }

        public static void Save(string dir)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(SettingsFile, dir);
            }
            catch
            {
            }
        }

        public static string Find()
        {
            foreach (var library in SteamLibraries())
            {
                var candidate = Path.Combine(library, "steamapps", "common", "Sons Of The Forest");
                if (Kind(candidate) != null)
                    return candidate;
            }
            return null;
        }

        private static List<string> SteamLibraries()
        {
            var roots = new List<string>();
            AddRegistry(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            AddRegistry(roots, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            AddRegistry(roots, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

            var libraries = new List<string>();
            foreach (var root in roots)
            {
                AddUnique(libraries, root);
                var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf))
                    continue;
                string text;
                try
                {
                    text = File.ReadAllText(vdf);
                }
                catch
                {
                    continue;
                }
                foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"(.+?)\""))
                    AddUnique(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
            }
            return libraries;
        }

        private static void AddRegistry(List<string> list, RegistryKey hive, string path, string name)
        {
            try
            {
                using (var key = hive.OpenSubKey(path))
                {
                    var value = key == null ? null : key.GetValue(name) as string;
                    if (!string.IsNullOrEmpty(value))
                        list.Add(value.Replace('/', '\\'));
                }
            }
            catch
            {
            }
        }

        private static void AddUnique(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            path = path.TrimEnd('\\');
            foreach (var existing in list)
            {
                if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(path);
        }
    }
}
