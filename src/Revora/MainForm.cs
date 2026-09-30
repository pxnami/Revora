using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(22, 22, 22);
        private static readonly Color Muted = Color.FromArgb(105, 105, 105);
        private static readonly Color Accent = Color.Black;
        private readonly string dataPath;
        private readonly ComboBox devices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Label deviceInfo = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 12, 0, 16) };
        private readonly Label firmwareInfo = new Label { AutoSize = true, Text = "No firmware selected", ForeColor = Muted, Margin = new Padding(0, 10, 0, 10) };
        private readonly Label status = new Label { AutoSize = true, ForeColor = Accent, Text = "Ready", Margin = new Padding(0, 8, 0, 6) };
        private readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 246, 246), BorderStyle = BorderStyle.FixedSingle };
        private readonly CheckBox erase = new CheckBox { AutoSize = true, Text = "Erase restore — delete all device data", Margin = new Padding(0, 12, 0, 12) };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 8, Style = ProgressBarStyle.Continuous };
        private readonly Timer scanTimer = new Timer { Interval = 10000 };
        private readonly Button refresh, enter, exit, chooseFirmware, restore, setup;
        private ToolRunner runner;
        private DeviceService service;
        private Firmware firmware;
        private bool busy;
        private bool restoring;
        private string sessionLog;
        private StreamWriter logWriter;
        private Device SelectedDevice { get { return devices.SelectedItem as Device; } }

        public MainForm() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Revora")) { }

        internal MainForm(string dataPath)
        {
            this.dataPath = dataPath;
            Text = "Revora";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10F);
            ForeColor = Ink;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(900, 760);
            Size = new Size(1160, 900);
            StartPosition = FormStartPosition.CenterScreen;
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(Path.Combine(dataPath, "Logs"));
            sessionLog = Path.Combine(dataPath, "Logs", "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".log");
            logWriter = new StreamWriter(sessionLog, false) { AutoFlush = true };
            runner = new ToolRunner(ReadToolsPath(), Path.Combine(dataPath, "Cache"));
            service = new DeviceService(runner);

            refresh = MakeButton("Refresh devices", false);
            enter = MakeButton("Enter recovery", false);
            exit = MakeButton("Exit recovery", false);
            chooseFirmware = MakeButton("Choose IPSW…", false);
            restore = MakeButton("Install firmware", true);
            setup = MakeButton("Setup", false);

            Controls.Add(CreateHome());

            refresh.Click += async (s, e) => await ScanAsync(false);
            enter.Click += async (s, e) => await RecoveryAsync(true);
            exit.Click += async (s, e) => await RecoveryAsync(false);
            chooseFirmware.Click += async (s, e) => await ChooseFirmwareAsync();
            restore.Click += async (s, e) => await RestoreAsync();
            setup.Click += (s, e) => ShowSetup();
            devices.SelectedIndexChanged += (s, e) => UpdateControls();
            erase.CheckedChanged += (s, e) => UpdateControls();
            scanTimer.Tick += async (s, e) => { if (!busy) await ScanAsync(true); };
            Shown += async (s, e) => {
                Log("Revora 0.1.0 · session log: " + sessionLog);
                UpdateControls();
                if (runner.MissingTools().Length > 0) {
                    status.Text = "Device tools need setup";
                    Log("Missing device tools: " + string.Join(", ", runner.MissingTools()) + ". Open Setup to select a tools folder.");
                    deviceInfo.Text = "Connect your device by USB, then complete Setup.";
                }
                else { await ScanAsync(false); scanTimer.Start(); }
            };
            FormClosing += (s, e) => {
                if (busy) {
                    e.Cancel = true;
                    MessageBox.Show(this, restoring ? "A firmware install is running. Keep Revora open and your device connected until it finishes." : "Wait for the current operation to finish before closing Revora.", "Operation in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            UpdateControls();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                scanTimer.Dispose();
                if (logWriter != null) { logWriter.Dispose(); logWriter = null; }
            }
            base.Dispose(disposing);
        }

        private async Task ScanAsync(bool automatic)
        {
            if (busy || runner.MissingTools().Length > 0) return;
            string selected = SelectedDevice == null ? null : SelectedDevice.Identity;
            string previous = string.Join("|", devices.Items.Cast<Device>().Select(d => d.Identity + ":" + d.Mode));
            await OperateAsync("Checking USB devices…", async () => {
                var found = await service.DiscoverAsync();
                devices.Items.Clear();
                foreach (var device in found.Devices) devices.Items.Add(device);
                devices.SelectedIndex = found.Devices.FindIndex(d => d.Identity == selected);
                if (devices.SelectedIndex < 0 && devices.Items.Count > 0) devices.SelectedIndex = 0;
                string next = string.Join("|", found.Devices.Select(d => d.Identity + ":" + d.Mode));
                if (!automatic || previous != next) {
                    Log(found.Devices.Count == 0 ? "No device found. Unlock it, accept Trust This Computer, and check Apple device drivers." : "Found " + found.Devices.Count + " device(s).");
                }
                foreach (string issue in found.Issues) if (!automatic || previous != next) Log(issue);
                status.Text = found.Issues.Count > 0 ? "Detection needs attention — see Activity" : found.Devices.Count == 0 ? "Waiting for a USB device" : "Device ready";
            }, !automatic);
        }

        private async Task RecoveryAsync(bool entering)
        {
            var device = SelectedDevice;
            if (device == null) return;
            if (entering && MessageBox.Show(this, "Put " + device.Name + " into recovery mode? The device will stop normal operation until you exit recovery or reinstall firmware.", "Enter recovery", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            await OperateAsync(entering ? "Entering recovery mode…" : "Exiting recovery mode…", async () => {
                if (entering) await service.EnterRecoveryAsync(device, Log); else await service.ExitRecoveryAsync(device, Log);
                status.Text = "Recovery command completed — refresh to check device mode";
                Log(status.Text);
            }, true);
            await ScanAsync(false);
        }

        private async Task ChooseFirmwareAsync()
        {
            using (var picker = new OpenFileDialog { Filter = "Apple firmware (*.ipsw)|*.ipsw", Title = "Choose firmware for your iPhone or iPad" }) {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                firmware = null;
                firmwareInfo.Text = "Reading firmware…";
                await OperateAsync("Reading firmware manifest…", async () => {
                    firmware = await Firmware.ReadAsync(picker.FileName, runner);
                    firmwareInfo.Text = "iOS / iPadOS " + firmware.Version + " · " + firmware.Build + "\n" + Path.GetFileName(firmware.Path);
                    status.Text = "Firmware selected";
                    Log("Selected firmware " + firmware.Version + " (" + firmware.Build + ").");
                }, true);
                if (firmware == null) firmwareInfo.Text = "No valid firmware selected";
            }
        }

        private async Task RestoreAsync()
        {
            var device = SelectedDevice;
            if (device == null || firmware == null) return;
            bool eraseData = erase.Checked;
            try { firmware.Validate(device, eraseData); }
            catch (InvalidOperationException e) { MessageBox.Show(this, e.Message, "Firmware incompatible", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            using (var confirmation = new RestoreConfirmation(device, firmware, eraseData)) {
                if (confirmation.ShowDialog(this) != DialogResult.OK) return;
            }
            restoring = true;
            try { await OperateAsync("Installing firmware — keep the device connected…", async () => {
                Log("Starting " + (eraseData ? "ERASE" : "update") + " install on " + device.Product + ", ECID " + device.EcidArgument + ".");
                await service.RestoreAsync(device, firmware, eraseData, Path.Combine(dataPath, "Cache"), Log);
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = 100;
                status.Text = "Firmware tool reported success — verify the device finishes booting";
                Log(status.Text);
                MessageBox.Show(this, "The firmware tool reported a successful install. Keep the device connected while it finishes booting, then verify it on the device.", "Install completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, true); }
            finally { restoring = false; }
            await ScanAsync(false);
        }

        private async Task OperateAsync(string description, Func<Task> operation, bool showError)
        {
            if (busy) return;
            busy = true;
            status.Text = description;
            progress.Style = ProgressBarStyle.Marquee;
            UpdateControls();
            try { await operation(); }
            catch (Exception e) {
                if (!(e is InvalidOperationException || e is IOException || e is TimeoutException || e is Win32Exception || e is System.Xml.XmlException)) throw;
                status.Text = "Operation failed — see Activity";
                log.Visible = true;
                Log(e.Message);
                if (showError) MessageBox.Show(this, e.Message.Length > 1800 ? e.Message.Substring(0, 1800) + "\nSee Activity for more." : e.Message, "Revora", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { busy = false; if (progress.Style == ProgressBarStyle.Marquee) { progress.Style = ProgressBarStyle.Continuous; progress.Value = 0; } UpdateControls(); }
        }

        private void UpdateControls()
        {
            bool ready = !busy && runner.MissingTools().Length == 0;
            var device = SelectedDevice;
            devices.Enabled = ready;
            refresh.Enabled = ready;
            setup.Enabled = !busy;
            chooseFirmware.Enabled = !busy;
            firmwareStart.Enabled = !busy;
            details.Enabled = !busy && device != null;
            erase.Enabled = !busy;
            enter.Enabled = ready && device != null && device.Mode == DeviceMode.Normal;
            exit.Enabled = ready && device != null && device.Mode == DeviceMode.Recovery && device.Ecid != 0;
            restore.Enabled = ready && device != null && device.Ecid != 0 && firmware != null;
            deviceInfo.Text = device == null ? "Connect an iPhone or iPad by USB. Unlock it and accept Trust This Computer.\nConnect one recovery / DFU device at a time." :
                "Mode: " + device.Mode + "    iOS / iPadOS: " + device.Version + "\nModel: " + device.Product + "    Hardware: " + device.Board + "    ECID: " + device.EcidArgument;
        }

        private void Log(string line)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), line); return; }
            string clean = Regex.Replace(line, "\u001b\\[[0-9;]*[A-Za-z]", "");
            string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + clean + Environment.NewLine;
            if (logWriter != null) {
                try { logWriter.Write(entry); }
                catch (IOException) {
                    logWriter.Dispose();
                    logWriter = null;
                    log.AppendText("Could not write the session log. Activity remains visible here.\r\n");
                }
            }
            if (log.TextLength > 100000) log.Text = log.Text.Substring(log.TextLength - 60000);
            log.AppendText(entry);
            var match = Regex.Match(clean, @"^progress:\s+(\d+)\s+([0-9.]+)");
            double amount;
            if (restoring && match.Success && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)) {
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = (int)Math.Max(0, Math.Min(100, amount * 100));
                status.Text = "Firmware stage " + match.Groups[1].Value + ": " + progress.Value + "% — keep connected";
            }
        }

        private void ShowSetup()
        {
            using (var dialog = new SetupForm(runner.DirectoryPath, dataPath)) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ToolRunner candidate;
                try { candidate = new ToolRunner(dialog.ToolsPath, Path.Combine(dataPath, "Cache")); }
                catch (ArgumentException) { MessageBox.Show(this, "Choose a valid tools folder path.", "Invalid path", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                var missing = candidate.MissingTools();
                if (missing.Length > 0) { MessageBox.Show(this, "This folder is missing: " + string.Join(", ", missing) + ".", "Incomplete tools folder", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                File.WriteAllText(Path.Combine(dataPath, "tools-path.txt"), candidate.DirectoryPath);
                runner = candidate;
                service = new DeviceService(runner);
                devices.Items.Clear();
                UpdateControls();
                status.Text = "Tools configured — refresh to detect devices";
                Log("Device tools configured: " + candidate.DirectoryPath);
                scanTimer.Start();
            }
        }

        private string ReadToolsPath()
        {
            string settings = Path.Combine(dataPath, "tools-path.txt");
            return File.Exists(settings) ? File.ReadAllText(settings).Trim() : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        }

        internal static Button MakeButton(string title, bool primary)
        {
            var button = new Button { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 7, 14, 7), FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(205, 205, 205);
            if (primary) button.EnabledChanged += (s, e) => button.BackColor = button.Enabled ? Accent : Color.FromArgb(235, 235, 235);
            return button;
        }
    }
}
