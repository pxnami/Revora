using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private TextBox toolsPath;
        private Control toolsActions;

        private Control CreateSettings()
        {
            var body = Stack();
            body.Controls.Add(SectionHeading("Device tools"));
            body.Controls.Add(Body("Revora uses Apple’s USB drivers and the bundled open-source device utilities. Connect one recovery or DFU device at a time."));
            toolsPath = new TextBox { Text = ReadToolsPath(), Width = 480, AccessibleName = "Device tools folder", Margin = new Padding(0, 0, 0, 12) };
            body.Controls.Add(toolsPath);
            var browse = new RevoraButton("Browse…");
            browse.Click += (s, e) => {
                using (var picker = new FolderBrowserDialog { Description = "Choose the device tools folder", SelectedPath = toolsPath.Text })
                    if (picker.ShowDialog(this) == DialogResult.OK) toolsPath.Text = picker.SelectedPath;
            };
            var save = new RevoraButton("Save tools folder", ButtonKind.Primary);
            save.Click += async (s, e) => await SaveToolsAsync();
            toolsActions = Actions(browse, save);
            body.Controls.Add(toolsActions);
            var drivers = new RevoraButton("Apple Devices setup", ButtonKind.Quiet);
            drivers.Click += (s, e) => OpenLink("https://support.apple.com/en-us/118290");
            body.Controls.Add(Actions(drivers));
            body.Controls.Add(SectionHeading("Files"));
            body.Controls.Add(Body("Session logs and firmware cache are stored on this computer.", 12));
            var logs = new RevoraButton("Open logs");
            logs.Click += (s, e) => OpenFolder(Path.Combine(dataPath, "Logs"));
            var cache = new RevoraButton("Open firmware cache");
            cache.Click += (s, e) => OpenFolder(Path.Combine(dataPath, "Cache"));
            body.Controls.Add(Actions(logs, cache));
            body.Controls.Add(SectionHeading("About"));
            body.Controls.Add(Body("Revora " + Application.ProductVersion + "\nWindows x64 · open-source device recovery and firmware installation.", 12));
            var icons = new RevoraButton("Icons by Icons8", ButtonKind.Quiet);
            icons.Click += (s, e) => OpenLink("https://icons8.com/icons/family-sf-symbols");
            var licenses = new RevoraButton("Licenses", ButtonKind.Quiet);
            licenses.Click += (s, e) => OpenFolder(AppDomain.CurrentDomain.BaseDirectory);
            body.Controls.Add(Actions(icons, licenses));
            return ScrollPage(body);
        }

        private async Task SaveToolsAsync()
        {
            if (operation.Running) return;
            await RunOperationAsync(OperationKind.ConfiguringTools, "Saving device tools", () => {
                string path;
                try { path = Path.GetFullPath(toolsPath.Text.Trim()); }
                catch (ArgumentException) { throw new InvalidOperationException("Choose a valid device tools folder."); }
                var replacement = new ToolRunner(path, Path.Combine(dataPath, "Cache"));
                var missing = replacement.MissingTools();
                if (missing.Length > 0) throw new FileNotFoundException("Missing device utilities: " + string.Join(", ", missing));
                File.WriteAllText(Path.Combine(dataPath, "tools-path.txt"), path);
                runner = replacement;
                service = new DeviceService(runner);
                monitor.SetSource(DiscoverAsync);
                return Task.FromResult(0);
            });
            await monitor.RefreshAsync();
        }

        private void RenderSettings()
        {
            if (toolsPath == null) return;
            toolsPath.Enabled = !operation.Running;
            toolsActions.Enabled = !operation.Running;
        }

        private static Label SectionHeading(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = Theme.Font(14F, System.Drawing.FontStyle.Bold), Margin = new Padding(0, 12, 0, 16) };
        }

        private void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            OpenLink(path);
        }

        private void OpenLink(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception error) {
                using (var notice = new NoticeDialog("Couldn’t open this location", "Windows could not open the requested page or folder.", error.Message)) notice.ShowDialog(this);
            }
        }
    }
}
