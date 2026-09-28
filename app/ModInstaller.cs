using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SotfModLoader
{
    public static class ModInstaller
    {
        public const string SiteUrl = "https://justinoros.github.io/sotf-modloader/";
        public const string StateFile = "sotf-modloader.json";
        private const int Concurrency = 6;

        private static readonly HttpClient Http = CreateClient();

        public static Version AppVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SotfModLoader/" + AppVersion.ToString(3));
            return client;
        }

        private static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public static async Task<Manifest> LoadManifest()
        {
            var text = await Http.GetStringAsync(SiteUrl + "manifest.json?t=" + DateTime.UtcNow.Ticks);
            return Json().Deserialize<Manifest>(text);
        }

        public static bool IsGameRunning()
        {
            return Process.GetProcessesByName("SonsOfTheForest").Length > 0 ||
                   Process.GetProcessesByName("SonsOfTheForestDS").Length > 0;
        }

        public static string FullPath(string gameDir, string relative)
        {
            var root = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The mod list contains an unsafe path: " + relative);
            return full;
        }

        public static ModStatus Status(string gameDir, Package mod)
        {
            if (string.IsNullOrEmpty(mod.dll) || !File.Exists(FullPath(gameDir, mod.dll)))
                return new ModStatus();
            string version = null;
            if (!string.IsNullOrEmpty(mod.manifestPath))
                version = ReadValue(FullPath(gameDir, mod.manifestPath), "version");
            return new ModStatus { Installed = true, Version = version, Current = version == mod.version };
        }

        public static ModStatus RedLoaderStatus(string gameDir, Package redloader)
        {
            var installed = File.Exists(Path.Combine(gameDir, "version.dll")) &&
                            Directory.Exists(Path.Combine(gameDir, "_Redloader"));
            if (!installed)
                return new ModStatus();
            var version = ReadValue(Path.Combine(gameDir, StateFile), "redloader");
            return new ModStatus { Installed = true, Version = version, Current = version == redloader.version };
        }

        private static string ReadValue(string file, string key)
        {
            try
            {
                if (!File.Exists(file))
                    return null;
                var data = Json().DeserializeObject(File.ReadAllText(file)) as Dictionary<string, object>;
                object value;
                if (data != null && data.TryGetValue(key, out value) && value != null)
                    return value.ToString();
            }
            catch
            {
            }
            return null;
        }

        public static void WriteRedLoaderVersion(string gameDir, string version)
        {
            var file = Path.Combine(gameDir, StateFile);
            Dictionary<string, object> data = null;
            try
            {
                if (File.Exists(file))
                    data = Json().DeserializeObject(File.ReadAllText(file)) as Dictionary<string, object>;
            }
            catch
            {
            }
            if (data == null)
                data = new Dictionary<string, object>();
            data["redloader"] = version;
            File.WriteAllText(file, Json().Serialize(data));
        }

        private static string FileUrl(Package pkg, string path)
        {
            return SiteUrl + pkg.@base + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        }

        public static async Task Install(string gameDir, Package pkg, IProgress<string> progress)
        {
            var label = pkg.name + " " + pkg.version;
            var total = pkg.files.Count;
            var data = new byte[total][];
            var done = 0;

            using (var gate = new SemaphoreSlim(Concurrency))
            {
                var tasks = new List<Task>();
                for (var i = 0; i < total; i++)
                {
                    var index = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        await gate.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            data[index] = await Http.GetByteArrayAsync(FileUrl(pkg, pkg.files[index].path)).ConfigureAwait(false);
                            var count = Interlocked.Increment(ref done);
                            progress.Report(label + ": downloading " + count + " of " + total + " files");
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
                await Task.WhenAll(tasks);
            }

            if (pkg.roots != null)
            {
                foreach (var root in pkg.roots)
                    Remove(gameDir, root);
            }

            for (var i = 0; i < total; i++)
            {
                progress.Report(label + ": writing " + (i + 1) + " of " + total + " files");
                var target = FullPath(gameDir, pkg.files[i].path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, data[i]);
            }
        }

        public static void Uninstall(string gameDir, Package pkg)
        {
            if (pkg.roots == null)
                return;
            foreach (var root in pkg.roots)
                Remove(gameDir, root);
        }

        private static void Remove(string gameDir, string relative)
        {
            var full = FullPath(gameDir, relative);
            if (Directory.Exists(full))
                Directory.Delete(full, true);
            else if (File.Exists(full))
                File.Delete(full);
        }

        public static void CountDownload(Package pkg)
        {
            if (string.IsNullOrEmpty(pkg.downloadUrl))
                return;
            Task.Run(async () =>
            {
                try
                {
                    using (await Http.GetAsync(pkg.downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                    }
                }
                catch
                {
                }
            });
        }
    }
}
