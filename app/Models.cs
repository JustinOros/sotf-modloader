using System.Collections.Generic;

namespace SotfModLoader
{
    public class PackageFile
    {
        public string path { get; set; }
        public long size { get; set; }
    }

    public class Package
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public string platform { get; set; }
        public string repo { get; set; }
        public string releaseUrl { get; set; }
        public string downloadUrl { get; set; }
        public string @base { get; set; }
        public string dll { get; set; }
        public string manifestPath { get; set; }
        public long? downloads { get; set; }
        public List<string> roots { get; set; }
        public List<PackageFile> files { get; set; }
    }

    public class RedLoaderVersion
    {
        public string version { get; set; }
        public string downloadUrl { get; set; }
        public string releaseUrl { get; set; }
        public long size { get; set; }
    }

    public class AppInfo
    {
        public string version { get; set; }
        public string url { get; set; }
    }

    public class Manifest
    {
        public string generated { get; set; }
        public string owner { get; set; }
        public Package redloader { get; set; }
        public List<RedLoaderVersion> redloaderVersions { get; set; }
        public List<Package> mods { get; set; }
        public AppInfo app { get; set; }
    }

    public class ModStatus
    {
        public bool Installed { get; set; }
        public string Version { get; set; }
        public bool Current { get; set; }
    }
}
