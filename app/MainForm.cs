using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SotfModLoader
{
    public class MainForm : Form
    {
        private static readonly Color Pine = ColorTranslator.FromHtml("#131c17");
        private static readonly Color Bark = ColorTranslator.FromHtml("#1d2821");
        private static readonly Color BarkLine = ColorTranslator.FromHtml("#34463b");
        private static readonly Color Lichen = ColorTranslator.FromHtml("#9aae97");
        private static readonly Color Fog = ColorTranslator.FromHtml("#e2e7df");
        private static readonly Color Flare = ColorTranslator.FromHtml("#e0a63a");
        private static readonly Color FlareInk = ColorTranslator.FromHtml("#1b1305");
        private static readonly Color Ember = ColorTranslator.FromHtml("#e38b7f");

        private enum Style
        {
            Primary,
            Secondary,
            Danger
        }

        private readonly float _scale;
        private readonly Font _titleFont;
        private readonly Font _sectionFont;
        private readonly Font _rowTitleFont;
        private readonly Font _bodyFont;
        private readonly Font _metaFont;
        private readonly Font _buttonFont;

        private readonly Panel _folderPanel;
        private readonly Label _folderName;
        private readonly Label _folderHelp;
        private readonly Button _folderButton;
        private readonly LinkLabel _updateLink;
        private readonly FlowLayoutPanel _list;
        private readonly Label _status;

        private Manifest _manifest;
        private string _gameDir;
        private string _kind;
        private bool _busy;
        private ModStatus _redloaderStatus;
        private string _selectedRedLoader;
        private readonly Dictionary<string, ModStatus> _statuses = new Dictionary<string, ModStatus>();

        public MainForm()
        {
            using (var g = CreateGraphics())
                _scale = g.DpiX / 96f;

            Text = "Sons of the Forest Mod Loader";
            BackColor = Pine;
            ForeColor = Fog;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(860), S(760));
            MinimumSize = new Size(S(620), S(480));
            StartPosition = FormStartPosition.CenterScreen;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _titleFont = new Font("Segoe UI Semibold", 22f);
            _sectionFont = new Font("Segoe UI Semibold", 15f);
            _rowTitleFont = new Font("Segoe UI Semibold", 12.5f);
            _bodyFont = new Font("Segoe UI", 10f);
            _metaFont = new Font("Segoe UI", 9f);
            _buttonFont = new Font("Segoe UI Semibold", 9.5f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(S(28), S(20), S(28), S(16)),
                BackColor = Pine
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "Sons of the Forest mod loader",
                Font = _titleFont,
                AutoSize = true,
                ForeColor = Fog,
                Margin = new Padding(0, 0, 0, S(6))
            };

            _updateLink = new LinkLabel
            {
                AutoSize = true,
                Visible = false,
                LinkColor = Flare,
                ActiveLinkColor = Fog,
                Font = _bodyFont,
                Margin = new Padding(0, 0, 0, S(8))
            };

            _folderPanel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Bark,
                Height = S(96),
                Margin = new Padding(0, S(6), 0, S(4)),
                Padding = new Padding(S(18), S(12), S(14), S(12))
            };
            _folderPanel.Paint += PaintFolderPanel;
            _folderName = new Label { AutoSize = false, Font = _rowTitleFont, ForeColor = Fog, BackColor = Bark };
            _folderHelp = new Label { AutoSize = false, Font = _metaFont, ForeColor = Lichen, BackColor = Bark };
            _folderButton = MakeButton("Choose folder", Style.Secondary, (s, e) => ChooseFolder());
            _folderPanel.Controls.Add(_folderName);
            _folderPanel.Controls.Add(_folderHelp);
            _folderPanel.Controls.Add(_folderButton);
            _folderPanel.Resize += (s, e) => ArrangeFolderPanel();

            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Pine,
                Margin = new Padding(0, S(4), 0, 0)
            };
            _list.Resize += (s, e) => ArrangeList();

            _status = new Label
            {
                AutoSize = true,
                Font = _bodyFont,
                ForeColor = Lichen,
                Margin = new Padding(0, S(10), 0, 0),
                Text = "Loading the mod list..."
            };

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(_updateLink, 0, 1);
            root.Controls.Add(_folderPanel, 0, 2);
            root.Controls.Add(_list, 0, 3);
            root.Controls.Add(_status, 0, 4);
            Controls.Add(root);
        }

        private int S(float v)
        {
            return (int)Math.Round(v * _scale);
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                _manifest = await ModInstaller.LoadManifest();
            }
            catch
            {
                SetStatus("The mod list couldn't be loaded. Check your internet connection and reopen the mod loader.", true);
                return;
            }

            ShowUpdateNotice();
            var dir = GameFolder.Load();
            if (dir != null)
                SetGameDir(dir);
            SetStatus(_gameDir == null ? string.Empty : "Ready.", false);
            Render();
        }

        private void ShowUpdateNotice()
        {
            var app = _manifest.app;
            Version latest;
            if (app == null || string.IsNullOrEmpty(app.url) || !Version.TryParse(app.version, out latest))
                return;
            var current = ModInstaller.AppVersion;
            if (latest <= new Version(current.Major, current.Minor, Math.Max(current.Build, 0)))
                return;
            _updateLink.Text = "Version " + app.version + " of the mod loader is available. Download it";
            _updateLink.LinkClicked += (s, e) => OpenUrl(app.url);
            _updateLink.Visible = true;
        }

        private void SetGameDir(string dir)
        {
            _gameDir = dir;
            _kind = GameFolder.Kind(dir);
            GameFolder.Save(dir);
            RefreshStatuses();
        }

        private void RefreshStatuses()
        {
            _statuses.Clear();
            _redloaderStatus = null;
            if (_manifest == null || _gameDir == null)
                return;
            if (_manifest.redloader != null)
                _redloaderStatus = ModInstaller.RedLoaderStatus(_gameDir, _manifest.redloader);
            foreach (var mod in _manifest.mods ?? new List<Package>())
                _statuses[mod.id] = ModInstaller.Status(_gameDir, mod);
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.ForeColor = error ? Ember : Lichen;
        }

        private Button MakeButton(string text, Style style, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                Font = _buttonFont,
                Cursor = Cursors.Hand,
                Padding = new Padding(S(10), S(3), S(10), S(3)),
                Margin = new Padding(S(6), 0, 0, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 1;
            switch (style)
            {
                case Style.Primary:
                    button.BackColor = Flare;
                    button.ForeColor = FlareInk;
                    button.FlatAppearance.BorderColor = Flare;
                    break;
                case Style.Secondary:
                    button.BackColor = Pine;
                    button.ForeColor = Fog;
                    button.FlatAppearance.BorderColor = BarkLine;
                    break;
                default:
                    button.BackColor = Pine;
                    button.ForeColor = Ember;
                    button.FlatAppearance.BorderColor = Pine;
                    break;
            }
            button.Click += click;
            return button;
        }

        private void PaintFolderPanel(object sender, PaintEventArgs e)
        {
            var color = _gameDir != null ? Flare : BarkLine;
            using (var brush = new SolidBrush(color))
                e.Graphics.FillRectangle(brush, 0, 0, S(5), _folderPanel.Height);
        }

        private void RenderFolder()
        {
            if (_gameDir != null)
            {
                _folderName.Text = _gameDir;
                _folderHelp.Text = _kind == "server" ? "Dedicated server folder" : "Game folder, found automatically or chosen by you";
                _folderButton.Text = "Change folder";
            }
            else
            {
                _folderName.Text = "Game folder not found";
                _folderHelp.Text = "Click Choose folder and pick the folder that contains SonsOfTheForest.exe.";
                _folderButton.Text = "Choose folder";
            }
            _folderButton.Enabled = !_busy;
            ArrangeFolderPanel();
            _folderPanel.Invalidate();
        }

        private void ArrangeFolderPanel()
        {
            var width = _folderPanel.ClientSize.Width;
            _folderButton.Location = new Point(width - _folderButton.Width - S(14), S(14));
            var textWidth = Math.Max(S(160), _folderButton.Left - S(40));
            RowPanel.Fit(_folderName, textWidth);
            RowPanel.Fit(_folderHelp, textWidth);
            _folderName.Location = new Point(S(20), S(12));
            _folderHelp.Location = new Point(S(20), _folderName.Bottom + S(4));
            _folderPanel.Height = Math.Max(_folderHelp.Bottom, _folderButton.Bottom) + S(14);
        }

        private void Render()
        {
            RenderFolder();
            _list.SuspendLayout();
            foreach (Control control in _list.Controls.Cast<Control>().ToList())
            {
                _list.Controls.Remove(control);
                control.Dispose();
            }

            if (_manifest != null)
            {
                if (_manifest.redloader != null)
                {
                    _list.Controls.Add(new HeaderPanel("Mod framework", null, _sectionFont, Fog, _scale));
                    _list.Controls.Add(RedLoaderRow());
                }

                var mods = VisibleMods();
                var outdated = mods.Where(m =>
                {
                    ModStatus status;
                    return _statuses.TryGetValue(m.id, out status) && status.Installed && !status.Current;
                }).ToList();
                Control updateAll = null;
                if (_gameDir != null && outdated.Count > 1)
                    updateAll = MakeButton("Update all (" + outdated.Count + ")", Style.Primary, (s, e) => UpdateAll(outdated));
                _list.Controls.Add(new HeaderPanel("Mods", updateAll, _sectionFont, Fog, _scale));
                foreach (var mod in mods)
                    _list.Controls.Add(ModRow(mod));
            }

            _list.ResumeLayout();
            ArrangeList();
        }

        private void ArrangeList()
        {
            var width = _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - S(4);
            if (width <= 0)
                return;
            _list.SuspendLayout();
            foreach (Control control in _list.Controls)
            {
                var arrangeable = control as IArrangeable;
                if (arrangeable != null)
                    arrangeable.Arrange(width);
            }
            _list.ResumeLayout();
        }

        private List<Package> VisibleMods()
        {
            var mods = (_manifest.mods ?? new List<Package>()).OrderBy(m => m.name, StringComparer.OrdinalIgnoreCase).ToList();
            if (_kind == null)
                return mods;
            return mods.Where(m =>
            {
                var platform = (m.platform ?? "Client").ToLowerInvariant();
                return platform == "universal" || platform == _kind;
            }).ToList();
        }

        private string VersionText(Package pkg, ModStatus status)
        {
            var parts = new List<string> { "Latest " + pkg.version };
            if (status != null)
            {
                if (!status.Installed)
                    parts.Add("Not installed");
                else if (status.Current)
                    parts.Add("Installed, up to date");
                else if (status.Version != null)
                    parts.Add("Installed " + status.Version + ", update available");
                else
                    parts.Add("Installed, version unknown");
            }
            if (pkg.downloads.HasValue)
                parts.Add(pkg.downloads.Value.ToString("N0") + " downloads");
            return string.Join("     ", parts);
        }

        private List<RedLoaderVersion> RedLoaderVersions()
        {
            var versions = _manifest.redloaderVersions;
            if (versions != null && versions.Count > 0)
                return versions;
            var rl = _manifest.redloader;
            return new List<RedLoaderVersion> { new RedLoaderVersion { version = rl.version, releaseUrl = rl.releaseUrl } };
        }

        private int SelectedRedLoaderIndex(List<RedLoaderVersion> versions)
        {
            if (_selectedRedLoader == null)
                return 0;
            var index = versions.FindIndex(v => v.version == _selectedRedLoader);
            return index < 0 ? 0 : index;
        }

        private Control RedLoaderRow()
        {
            var rl = _manifest.redloader;
            var versions = RedLoaderVersions();
            var selectedIndex = SelectedRedLoaderIndex(versions);
            var selected = versions[selectedIndex];
            var latest = versions[0];
            var status = _gameDir != null ? _redloaderStatus : null;

            var controls = new List<Control>();
            if (status != null)
            {
                if (versions.Count > 1)
                {
                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Bark,
                        ForeColor = Fog,
                        Font = _buttonFont,
                        Width = S(150),
                        Margin = new Padding(S(6), S(2), 0, 0),
                        Enabled = !_busy
                    };
                    for (var i = 0; i < versions.Count; i++)
                        combo.Items.Add(i == 0 ? "Latest (" + versions[i].version + ")" : versions[i].version);
                    combo.SelectedIndex = selectedIndex;
                    combo.SelectedIndexChanged += (s, e) =>
                    {
                        _selectedRedLoader = combo.SelectedIndex <= 0 ? null : versions[combo.SelectedIndex].version;
                        BeginInvoke((Action)Render);
                    };
                    controls.Add(combo);
                }

                Button button = null;
                if (!status.Installed)
                    button = MakeButton("Install RedLoader", Style.Primary, (s, e) => InstallRedLoader(selected));
                else if (status.Version == selected.version)
                    button = MakeButton("Reinstall", Style.Secondary, (s, e) => InstallRedLoader(selected));
                else if (status.Version == null)
                    button = MakeButton("Install " + selected.version, Style.Secondary, (s, e) => InstallRedLoader(selected));
                else if (selectedIndex == 0)
                    button = MakeButton("Update", Style.Primary, (s, e) => InstallRedLoader(selected));
                else
                    button = MakeButton("Install " + selected.version, Style.Primary, (s, e) => InstallRedLoader(selected));
                button.Enabled = !_busy;
                controls.Add(button);
            }

            var parts = new List<string> { "Latest " + latest.version };
            if (status != null)
            {
                if (!status.Installed)
                    parts.Add("Not installed");
                else if (status.Version == null)
                    parts.Add("Installed, version unknown");
                else if (status.Version == latest.version)
                    parts.Add("Installed, up to date");
                else
                    parts.Add("Installed " + status.Version);
            }
            var metaColor = status != null && status.Installed && status.Version != latest.version ? Flare : Lichen;
            return new RowPanel("RedLoader", rl.repo,
                "Every mod here runs on RedLoader. Install it before adding mods. Pick an older version only if a mod needs one.",
                string.Join("     ", parts), metaColor, controls,
                _rowTitleFont, _bodyFont, _metaFont, Fog, Lichen, BarkLine, _scale);
        }

        private Control ModRow(Package mod)
        {
            ModStatus status = null;
            if (_gameDir != null)
                _statuses.TryGetValue(mod.id, out status);
            var buttons = new List<Control>();
            if (status != null)
            {
                if (!status.Installed)
                {
                    buttons.Add(MakeButton("Install", Style.Primary, (s, e) => InstallMod(mod)));
                }
                else
                {
                    if (!status.Current)
                        buttons.Add(MakeButton(status.Version != null ? "Update" : "Reinstall", Style.Primary, (s, e) => InstallMod(mod)));
                    buttons.Add(MakeButton("Remove", Style.Danger, (s, e) => RemoveMod(mod)));
                }
            }
            foreach (var b in buttons)
                b.Enabled = !_busy;
            var metaColor = status != null && status.Installed && !status.Current ? Flare : Lichen;
            return new RowPanel(mod.name, mod.repo, mod.description, VersionText(mod, status), metaColor, buttons,
                _rowTitleFont, _bodyFont, _metaFont, Fog, Lichen, BarkLine, _scale);
        }

        private void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select your Sons Of The Forest folder, the one that contains SonsOfTheForest.exe";
                dialog.ShowNewFolderButton = false;
                if (_gameDir != null)
                    dialog.SelectedPath = _gameDir;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                if (GameFolder.Kind(dialog.SelectedPath) == null)
                {
                    SetStatus(dialog.SelectedPath + " doesn't contain SonsOfTheForest.exe. Choose the folder the game is installed in.", true);
                    return;
                }
                SetGameDir(dialog.SelectedPath);
                SetStatus("Using " + _gameDir + ".", false);
                Render();
            }
        }

        private async Task Run(Func<IProgress<string>, Task<string>> task)
        {
            await Task.Yield();
            if (_busy || _gameDir == null)
                return;
            if (ModInstaller.IsGameRunning())
            {
                SetStatus("Close Sons of the Forest first, then try again.", true);
                return;
            }

            _busy = true;
            Render();
            var progress = new Progress<string>(message => SetStatus(message, false));
            var needsAdmin = false;
            try
            {
                var message = await task(progress);
                SetStatus(message, false);
            }
            catch (UnauthorizedAccessException)
            {
                needsAdmin = true;
            }
            catch (Exception e)
            {
                SetStatus(Explain(e), true);
            }

            _busy = false;
            RefreshStatuses();
            Render();
            if (needsAdmin)
                OfferAdmin();
        }

        private static string Explain(Exception e)
        {
            if (e is HttpRequestException || e is TaskCanceledException)
                return "A download failed. Check your internet connection and try again.";
            if (e is IOException)
                return "A file is in use. Close Sons of the Forest and try again.";
            return e.Message;
        }

        private static bool IsElevated()
        {
            try
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void OfferAdmin()
        {
            if (IsElevated())
            {
                SetStatus("Windows blocked writing to the game folder, even as administrator.", true);
                return;
            }
            SetStatus("Windows blocked writing to the game folder.", true);
            var answer = MessageBox.Show(this,
                "Windows blocked writing to the game folder.\n\nRestart the mod loader as administrator and try again?",
                "Permission needed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch (Win32Exception)
            {
            }
        }

        private async void InstallRedLoader(RedLoaderVersion version)
        {
            var rl = _manifest.redloader;
            await Run(async progress =>
            {
                if (!string.IsNullOrEmpty(version.downloadUrl))
                    await ModInstaller.InstallZip(_gameDir, version.downloadUrl, "RedLoader " + version.version, progress);
                else
                    await ModInstaller.Install(_gameDir, rl, progress);
                ModInstaller.WriteRedLoaderVersion(_gameDir, version.version);
                return "RedLoader " + version.version + " installed. Launch the game once and wait for the main menu so it can finish setting up.";
            });
        }

        private async void InstallMod(Package mod)
        {
            ModStatus before;
            _statuses.TryGetValue(mod.id, out before);
            await Run(async progress =>
            {
                await ModInstaller.Install(_gameDir, mod, progress);
                ModInstaller.CountDownload(mod);
                return before != null && before.Installed
                    ? mod.name + " updated to " + mod.version + "."
                    : mod.name + " " + mod.version + " installed.";
            });
        }

        private async void RemoveMod(Package mod)
        {
            var answer = MessageBox.Show(this, "Remove " + mod.name + " from your game?", "Remove mod",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;
            await Run(progress =>
            {
                ModInstaller.Uninstall(_gameDir, mod);
                return Task.FromResult(mod.name + " removed.");
            });
        }

        private async void UpdateAll(List<Package> mods)
        {
            await Run(async progress =>
            {
                foreach (var mod in mods)
                {
                    await ModInstaller.Install(_gameDir, mod, progress);
                    ModInstaller.CountDownload(mod);
                }
                return "Updated " + mods.Count + " mod" + (mods.Count == 1 ? "" : "s") + ".";
            });
        }
    }
}
