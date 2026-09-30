using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private Label firmwareSteps, firmwareTitle, firmwareDescription, firmwareMetadata, installationStage;
        private Label confirmationSummary, eraseHint, firmwareResult;
        private Panel firmwareHost;
        private readonly Control[] firmwarePanels = new Control[6];
        private RevoraButton firmwareNext, firmwareBack, firmwareChoose, updateChoice, restoreChoice, install;
        private CheckBox acknowledgement;
        private TextBox erasePhrase;
        private ProgressTrack installationProgress;
        private long firmwareBytes;

        private Control CreateFirmware()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            firmwareSteps = Body("1 Device   /   2 Firmware   /   3 Type   /   4 Confirm   /   5 Install   /   6 Result", 24);
            firmwareTitle = Heading("");
            root.Controls.Add(firmwareSteps, 0, 0);
            root.Controls.Add(firmwareTitle, 0, 1);
            firmwareHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var device = Stack();
            firmwareDescription = Body("");
            device.Controls.Add(firmwareDescription);
            var deviceRefresh = new RevoraButton("Refresh");
            deviceRefresh.Click += async (s, e) => await RefreshDevicesAsync();
            device.Controls.Add(Actions(deviceRefresh));
            firmwarePanels[0] = ScrollPage(device);
            var file = Stack();
            file.Controls.Add(Body("Choose an IPSW file stored on this computer. Revora reads its manifest before installation."));
            firmwareChoose = new RevoraButton("Choose IPSW…", ButtonKind.Primary, null);
            firmwareChoose.Click += async (s, e) => await ChooseFirmwareAsync();
            file.Controls.Add(Actions(firmwareChoose));
            firmwareMetadata = Body("No firmware selected.");
            file.Controls.Add(firmwareMetadata);
            file.Controls.Add(Body("Apple’s signing service checks eligibility during installation. Revora does not verify signing status in advance."));
            firmwarePanels[1] = ScrollPage(file);
            var type = Stack();
            type.Controls.Add(Body("Choose how to install the selected firmware."));
            updateChoice = new RevoraButton("Update · attempt to keep data") { Width = 360, Height = 56 };
            restoreChoice = new RevoraButton("Restore · erase all device data") { Width = 360, Height = 56 };
            updateChoice.Click += (s, e) => { firmwareFlow.SelectType(false); RenderFirmware(); };
            restoreChoice.Click += (s, e) => { firmwareFlow.SelectType(true); RenderFirmware(); };
            type.Controls.Add(Actions(updateChoice));
            type.Controls.Add(Body("Update attempts to preserve apps and personal data. A backup is still recommended.", 20));
            type.Controls.Add(Actions(restoreChoice));
            type.Controls.Add(Body("Restore deletes apps, settings and personal data. You may need the device’s Apple Account to activate it.", 20));
            firmwarePanels[2] = ScrollPage(type);
            var confirm = Stack();
            confirmationSummary = Body("");
            confirm.Controls.Add(confirmationSummary);
            confirm.Controls.Add(Body("Keep your device connected throughout installation. Back up anything you want to keep. Installation cannot be cancelled safely once it starts."));
            acknowledgement = new CheckBox { Text = "I have a backup or accept the risk of data loss", AutoSize = true, Margin = new Padding(0, 8, 0, 20), AccessibleName = "Data loss acknowledgement" };
            acknowledgement.CheckedChanged += (s, e) => { firmwareFlow.Acknowledged = acknowledgement.Checked; UpdateInstallButton(); };
            confirm.Controls.Add(acknowledgement);
            eraseHint = Body("Type ERASE below to confirm deletion.", 8);
            confirm.Controls.Add(eraseHint);
            erasePhrase = new TextBox { Width = 250, AccessibleName = "Type ERASE to confirm deletion", Margin = new Padding(0, 0, 0, 20) };
            erasePhrase.TextChanged += (s, e) => { firmwareFlow.Confirmation = erasePhrase.Text; UpdateInstallButton(); };
            confirm.Controls.Add(erasePhrase);
            install = new RevoraButton("Install update", ButtonKind.Primary) { Width = 180 };
            install.Click += async (s, e) => await InstallFirmwareAsync();
            confirm.Controls.Add(Actions(install));
            firmwarePanels[3] = ScrollPage(confirm);
            var progress = Stack();
            progress.Controls.Add(Body("Keep Revora open and your device connected. The device may restart as installation progresses."));
            installationStage = Body("Preparing installation…", 12);
            progress.Controls.Add(installationStage);
            installationProgress = new ProgressTrack { Width = 450, Height = 6, Margin = new Padding(0, 0, 0, 24), AccessibleName = "Firmware installation progress" };
            progress.Controls.Add(installationProgress);
            var details = new RevoraButton("View details", ButtonKind.Quiet);
            details.Click += (s, e) => ShowActivityDetails();
            progress.Controls.Add(Actions(details));
            firmwarePanels[4] = ScrollPage(progress);
            var result = Stack();
            firmwareResult = Body("");
            result.Controls.Add(firmwareResult);
            var done = new RevoraButton("Done", ButtonKind.Primary);
            done.Click += (s, e) => { firmwareFlow.Reset(); firmwareFlow.ObserveDevice(SelectedDevice, DeviceReady); RenderFirmware(); };
            var resultDetails = new RevoraButton("View details");
            resultDetails.Click += (s, e) => ShowActivityDetails();
            result.Controls.Add(Actions(done, resultDetails));
            firmwarePanels[5] = ScrollPage(result);
            foreach (var panel in firmwarePanels) { panel.Dock = DockStyle.Fill; panel.Visible = false; firmwareHost.Controls.Add(panel); }
            root.Controls.Add(firmwareHost, 0, 2);
            firmwareBack = new RevoraButton("Back");
            firmwareBack.Click += (s, e) => { firmwareFlow.Back(); RenderFirmware(); };
            firmwareNext = new RevoraButton("Continue", ButtonKind.Primary);
            firmwareNext.Click += (s, e) => {
                try {
                    if (firmwareFlow.Step == FirmwareStep.Device) firmwareFlow.ContinueFromDevice();
                    else if (firmwareFlow.Step == FirmwareStep.Firmware) firmwareFlow.ContinueFromFirmware();
                    else if (firmwareFlow.Step == FirmwareStep.InstallationType) firmwareFlow.Review(SelectedDevice);
                    RenderFirmware();
                } catch (InvalidOperationException error) {
                    using (var notice = new NoticeDialog("Firmware cannot continue", error.Message, null)) notice.ShowDialog(this);
                }
            };
            root.Controls.Add(Actions(firmwareBack, firmwareNext), 0, 3);
            return root;
        }

        private void RenderFirmware()
        {
            if (firmwareHost == null) return;
            var step = firmwareFlow.Step;
            string[] titles = { "Select your device", "Choose firmware", "Installation type", "Review installation", "Installing firmware", firmwareFlow.Succeeded ? "Installation completed" : "Installation couldn’t complete" };
            firmwareTitle.Text = titles[(int)step];
            firmwareSteps.Text = "Step " + ((int)step + 1) + " of 6  ·  " + step.ToString().Replace("InstallationType", "Installation type");
            for (int i = 0; i < firmwarePanels.Length; i++) firmwarePanels[i].Visible = i == (int)step;
            firmwarePanels[(int)step].BringToFront();
            var device = SelectedDevice;
            firmwareDescription.Text = DeviceReady ? device.Name + "\n" + device.Product + " · " + device.ModeLabel + "\n\n" + (device.Ecid == 0 ? "The device did not report an ECID. A known ECID is required for safe installation." : "Firmware will be checked against this device’s product and hardware board.") : "Connect your iPhone or iPad by USB. Unlock it and accept Trust This Computer when prompted.";
            var firmware = firmwareFlow.Firmware;
            firmwareMetadata.Text = firmware == null ? "No firmware selected." : Path.GetFileName(firmware.Path) + "\n\nVersion  " + firmware.Version + "  ·  Build  " + firmware.Build + "\nFile size  " + (firmwareBytes / 1073741824D).ToString("0.00") + " GB\n\n" + Compatibility(firmware);
            firmwareChoose.Enabled = !operation.Running;
            updateChoice.Kind = !firmwareFlow.Erase ? ButtonKind.Primary : ButtonKind.Secondary;
            restoreChoice.Kind = firmwareFlow.Erase ? ButtonKind.Danger : ButtonKind.Secondary;
            updateChoice.Text = "Update · attempt to keep data" + (!firmwareFlow.Erase ? " · selected" : "");
            restoreChoice.Text = "Restore · erase all device data" + (firmwareFlow.Erase ? " · selected" : "");
            updateChoice.AccessibleRole = AccessibleRole.RadioButton;
            restoreChoice.AccessibleRole = AccessibleRole.RadioButton;
            updateChoice.AccessibleName = updateChoice.Text;
            restoreChoice.AccessibleName = restoreChoice.Text;
            updateChoice.Invalidate(); restoreChoice.Invalidate();
            if (acknowledgement.Checked != firmwareFlow.Acknowledged) acknowledgement.Checked = firmwareFlow.Acknowledged;
            if (erasePhrase.Text != (firmwareFlow.Confirmation ?? "")) erasePhrase.Text = firmwareFlow.Confirmation ?? "";
            erasePhrase.Visible = firmwareFlow.Erase;
            eraseHint.Visible = firmwareFlow.Erase;
            confirmationSummary.Text = device == null || firmware == null ? "" : device.Name + " · " + device.Product + "\n" + device.PlatformName + " " + firmware.Version + " (" + firmware.Build + ")\n\n" + (firmwareFlow.Erase ? "This will erase all data on this device." : "Update attempts to preserve your data, but data loss remains possible.");
            confirmationSummary.ForeColor = firmwareFlow.Erase ? Theme.Danger : Theme.Ink;
            install.Text = firmwareFlow.Erase ? "Erase and install" : "Install update";
            install.AccessibleName = install.Text;
            install.Kind = firmwareFlow.Erase ? ButtonKind.Danger : ButtonKind.Primary;
            UpdateInstallButton();
            firmwareBack.Visible = step > FirmwareStep.Device && step < FirmwareStep.Installation;
            firmwareBack.Enabled = !operation.Running;
            firmwareNext.Visible = step <= FirmwareStep.InstallationType;
            firmwareNext.Enabled = !operation.Running && DeviceReady && device.Ecid != 0 && (step != FirmwareStep.Firmware || firmware != null);
            RenderFirmwareProgress();
            firmwareResult.Text = firmwareFlow.Succeeded
                ? "The device tool reported a successful installation. Let the device finish starting up, then check its setup screen.\n\n" + (firmwareFlow.Erase ? "All device data was erased." : "Check that your apps and data are available.")
                : "Installation stopped.\n\n" + (operation.Explanation ?? "Check the device and USB connection. Open Activity for the message returned by the device tools before trying again.");
            firmwareResult.ForeColor = firmwareFlow.Succeeded ? Theme.Success : Theme.Danger;
        }

        private string Compatibility(Firmware firmware)
        {
            if (!DeviceReady) return "Connect the selected device to check compatibility.";
            try { firmware.Validate(SelectedDevice, firmwareFlow.Erase); return "Compatible manifest for " + SelectedDevice.Product + " · " + SelectedDevice.Board + ". Signing status is not checked."; }
            catch (InvalidOperationException error) { return error.Message; }
        }

        private void UpdateInstallButton() { install.Enabled = firmwareFlow.CanInstall && DeviceReady && !operation.Running; install.Invalidate(); }

        private async Task ChooseFirmwareAsync()
        {
            using (var picker = new OpenFileDialog { Title = "Choose iOS or iPadOS firmware", Filter = "Apple firmware (*.ipsw)|*.ipsw", CheckFileExists = true }) {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                await RunOperationAsync(OperationKind.PreparingFirmware, "Reading firmware", async () => {
                    var firmware = await Firmware.ReadAsync(picker.FileName, runner);
                    firmwareBytes = new FileInfo(firmware.Path).Length;
                    firmwareFlow.SelectFirmware(firmware);
                    Record("Firmware selected", firmware.Version + " (" + firmware.Build + ")");
                });
            }
        }

        private async Task InstallFirmwareAsync()
        {
            if (!firmwareFlow.CanInstall || !DeviceReady || operation.Running) return;
            var device = SelectedDevice;
            var firmware = firmwareFlow.Firmware;
            bool erase = firmwareFlow.Erase;
            firmwareFlow.Start();
            await RunOperationAsync(OperationKind.InstallingFirmware, erase ? "Restoring firmware" : "Updating firmware",
                () => service.RestoreAsync(device, firmware, erase, Path.Combine(dataPath, "Cache"), LogTechnical));
            firmwareFlow.Complete(operation.Error);
            RenderFirmware();
            await monitor.RefreshAsync();
        }

        private void RenderFirmwareProgress()
        {
            if (installationStage == null) return;
            installationStage.Text = operation.Stage + (operation.Progress.HasValue ? " · " + operation.Progress.Value + "% of this stage" : "…");
            installationProgress.Value = operation.Progress;
        }
    }
}
