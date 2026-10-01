using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm : Form
    {
        private readonly string dataPath;
        private readonly DeviceMonitor monitor;
        private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        private readonly Dictionary<string, RevoraButton> navigation = new Dictionary<string, RevoraButton>();
        private readonly FirmwareFlow firmwareFlow = new FirmwareFlow();
        private readonly OperationStatus operation = new OperationStatus();
        private readonly List<ActivityEntry> activity = new List<ActivityEntry>();
        private readonly TextBox technicalLog = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, BackColor = Theme.Surface, Font = new Font("Consolas", 9F) };
        private ToolRunner runner;
        private DeviceService service;
        private StreamWriter logWriter;
        private DeviceSnapshot previousSnapshot;
        private readonly bool startMonitoring;
        private Device SelectedDevice { get { return monitor.Snapshot.CurrentDevice; } }
        private bool DeviceReady { get { return SelectedDevice != null && monitor.Snapshot.Connection == ConnectionState.Connected; } }

        public MainForm() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Revora"), null, true) { }
        internal MainForm(string dataPath, Func<Task<Discovery>> discovery, bool startMonitoring)
        {
            SuspendLayout();
            this.dataPath = dataPath;
            this.startMonitoring = startMonitoring;
            Text = "Revora";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = Theme.Font(10F);
            ForeColor = Theme.Ink;
            BackColor = Theme.Background;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(900, 680);
            Size = new Size(1080, 780);
            StartPosition = FormStartPosition.CenterScreen;
            Directory.CreateDirectory(Path.Combine(dataPath, "Logs"));
            string session = Path.Combine(dataPath, "Logs", "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".log");
            logWriter = new StreamWriter(session, false) { AutoFlush = true };
            runner = new ToolRunner(ReadToolsPath(), Path.Combine(dataPath, "Cache"));
            service = new DeviceService(runner);
            monitor = new DeviceMonitor(discovery ?? DiscoverAsync);
            monitor.Changed += DeviceChanged;
            previousSnapshot = monitor.Snapshot;
            Controls.Add(CreateShell());
            LogTechnical("Revora " + Application.ProductVersion + " · " + session);
            Shown += async (s, e) => {
                RenderState();
                if (this.startMonitoring) { monitor.Start(); await monitor.RefreshAsync(); }
            };
            FormClosing += (s, e) => {
                if (!operation.Running) return;
                e.Cancel = true;
                using (var dialog = new NoticeDialog("Operation in progress", "Keep Revora open and your device connected until the current operation finishes.", null)) dialog.ShowDialog(this);
            };
            ShowPage("Home");
            ResumeLayout(true);
        }

        private Task<Discovery> DiscoverAsync()
        {
            var missing = runner.MissingTools();
            if (missing.Length > 0) throw new FileNotFoundException("Device tools need setup. Open Settings to select the tools folder. Missing: " + string.Join(", ", missing));
            return service.DiscoverAsync();
        }

        private void DeviceChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            var snapshot = monitor.Snapshot;
            var oldDevice = previousSnapshot.CurrentDevice;
            var device = snapshot.CurrentDevice;
            if (snapshot.Connection == ConnectionState.Connected) {
                if (oldDevice == null || previousSnapshot.Connection != ConnectionState.Connected || oldDevice.Identity != device.Identity)
                    Record("Device connected", device.Name + " · " + device.Product);
                else if (oldDevice.Mode != device.Mode) Record(device.ModeLabel, device.Name);
            }
            else if (snapshot.Connection == ConnectionState.Disconnected && oldDevice != null) Record("Device disconnected", oldDevice.Name);
            if (snapshot.Error.Length > 0 && snapshot.Error != previousSnapshot.Error) {
                Record("Device detection needs attention", "Check the USB connection and device tools in Settings.");
                LogTechnical(snapshot.Error);
            }
            firmwareFlow.ObserveDevice(device, snapshot.Connection == ConnectionState.Connected);
            previousSnapshot = snapshot;
            RenderState();
        }

        private void RenderState()
        {
            if (IsDisposed || Disposing) return;
            RenderHome();
            RenderRecovery();
            RenderFirmware();
            RenderSettings();
            RenderFooter();
            switchDevice.Visible = monitor.Snapshot.Devices.Count > 1;
            switchDevice.Enabled = !operation.Running;
        }

        private async Task RunOperationAsync(OperationKind kind, string title, Func<Task> action)
        {
            if (operation.Running) return;
            operation.Begin(kind, title);
            Record(title, SelectedDevice == null ? "" : SelectedDevice.Name);
            RenderState();
            await monitor.SuspendAsync();
            try {
                await action();
                operation.Finish();
                Record(title + " completed", "");
            }
            catch (Exception e) {
                if (!ToolRunner.IsExpectedFailure(e)) throw;
                operation.Fail(e.Message, FailureExplanation(e));
                Record(title + " failed", FailureExplanation(e));
                LogTechnical(e.Message);
                if (kind != OperationKind.InstallingFirmware)
                    using (var dialog = new NoticeDialog(title + " couldn’t complete", FailureExplanation(e), e.Message)) dialog.ShowDialog(this);
            }
            finally {
                monitor.Resume();
                RenderState();
            }
        }

        private async Task RecoveryAsync()
        {
            var device = SelectedDevice;
            if (!DeviceReady || operation.Running || device.Mode == DeviceMode.Dfu) return;
            bool entering = device.Mode == DeviceMode.Normal;
            if (entering) using (var confirmation = new ActionConfirmation("Enter Recovery Mode?", "Your device will leave normal operation and restart into Apple’s recovery environment.", "Enter Recovery Mode")) {
                if (confirmation.ShowDialog(this) != DialogResult.OK) return;
            }
            await RunOperationAsync(entering ? OperationKind.EnteringRecovery : OperationKind.ExitingRecovery,
                entering ? "Entering Recovery Mode" : "Exiting Recovery Mode", async () => {
                    if (!DeviceReady || SelectedDevice.Identity != device.Identity || SelectedDevice.Mode != device.Mode)
                        throw new InvalidOperationException("The device changed while the last scan was finishing. Check its current mode and try again.");
                    if (entering) await service.EnterRecoveryAsync(device, LogTechnical);
                    else await service.ExitRecoveryAsync(device, LogTechnical);
                });
            await monitor.RefreshAsync();
        }

        private void Record(string title, string detail)
        {
            activity.Add(new ActivityEntry(DateTime.Now, title, detail));
            if (activity.Count > 200) activity.RemoveAt(0);
            LogTechnical(title + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
            RenderActivity();
        }

        private void LogTechnical(string line)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(LogTechnical), line); return; }
            string clean = Regex.Replace(line, "\u001b\\[[0-9;]*[A-Za-z]", "");
            string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + clean + Environment.NewLine;
            if (logWriter != null) try { logWriter.Write(entry); }
            catch (IOException) { logWriter.Dispose(); logWriter = null; entry += "The log file could not be written. Details remain available in this session.\n"; }
            if (technicalLog.TextLength > 100000) technicalLog.Text = technicalLog.Text.Substring(technicalLog.TextLength - 60000);
            technicalLog.AppendText(entry);
            var match = Regex.Match(clean, @"^progress:\s+(\d+)\s+([0-9.]+)");
            double amount;
            if (operation.Kind == OperationKind.InstallingFirmware && operation.Running && match.Success
                && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)) {
                operation.Progress = (int)Math.Max(0, Math.Min(100, amount * 100));
                int stage;
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out stage)) return;
                string[] stages = { "Detecting device", "Preparing device", "Uploading file system", "Verifying file system", "Installing firmware", "Installing baseband", "Updating device components", "Uploading image" };
                operation.Stage = stage < stages.Length ? stages[stage] : "Firmware stage " + stage;
                RenderFirmwareProgress();
                RenderFooter();
                RenderActivityOperation();
            }
        }

        private static string FailureExplanation(Exception error)
        {
            if (error is System.ComponentModel.Win32Exception) return "Windows couldn’t start a device tool. Check its application-control policy and open Details for the affected file.";
            if (error is TimeoutException) return "The device didn’t respond. Check the cable, unlock the device if possible, and try again.";
            if (error is IOException) return "Revora couldn’t read the required file. Check that it is accessible and choose it again.";
            return "Check the device connection and the selected firmware. Details contain the message returned by the device tools.";
        }

        private string ReadToolsPath()
        {
            string settings = Path.Combine(dataPath, "tools-path.txt");
            return File.Exists(settings) ? File.ReadAllText(settings).Trim() : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        }

        internal DeviceMonitor Monitor { get { return monitor; } }
        internal FirmwareFlow Flow { get { return firmwareFlow; } }
        internal OperationStatus Operation { get { return operation; } }
        internal void Navigate(string page) { ShowPage(page); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                monitor.Changed -= DeviceChanged;
                monitor.Dispose();
                if (logWriter != null) { logWriter.Dispose(); logWriter = null; }
            }
            base.Dispose(disposing);
        }
    }
}
