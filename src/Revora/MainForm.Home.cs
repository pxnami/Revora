using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private DeviceVisual homeVisual;
        private Label homeName, homeModel, homeSoftware, homeMessage;
        private StatusIndicator homeStatus;
        private RevoraButton homeRecovery, homeDetails, homeFirmware, homeRefresh;
        private FlowLayoutPanel homeActions;
        private Action layoutHome;

        private Control CreateHome()
        {
            var container = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0) };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            var summary = Stack();
            summary.Dock = DockStyle.Fill;
            homeVisual = new DeviceVisual { Size = new Size(260, 200), Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 24) };
            homeName = Heading("Connect an iPhone or iPad");
            homeName.Anchor = AnchorStyles.None;
            homeModel = Body("Connect your device using a USB cable to get started.", 12);
            homeModel.Anchor = AnchorStyles.None;
            homeStatus = new StatusIndicator { Anchor = AnchorStyles.None, Width = 320, Height = 28, Margin = new Padding(0, 0, 0, 8) };
            homeSoftware = Body("", 16);
            homeSoftware.Anchor = AnchorStyles.None;
            homeRecovery = new RevoraButton("Enter Recovery Mode", ButtonKind.Primary);
            homeRecovery.Click += async (s, e) => await RecoveryAsync();
            homeDetails = new RevoraButton("Device Details");
            homeDetails.Click += (s, e) => ShowDeviceDetails();
            homeFirmware = new RevoraButton("Firmware", ButtonKind.Quiet);
            homeFirmware.Click += (s, e) => ShowPage("Firmware");
            homeRefresh = new RevoraButton("Refresh", ButtonKind.Quiet, "restart");
            homeRefresh.Click += async (s, e) => await RefreshDevicesAsync();
            homeActions = Actions(homeRecovery, homeDetails);
            homeActions.Dock = DockStyle.None;
            homeActions.Anchor = AnchorStyles.None;
            var secondary = Actions(homeFirmware, homeRefresh);
            secondary.Dock = DockStyle.None;
            secondary.Anchor = AnchorStyles.None;
            homeMessage = Body("", 0);
            homeMessage.TextAlign = ContentAlignment.MiddleCenter;
            homeMessage.Anchor = AnchorStyles.None;
            foreach (Control control in new Control[] { homeVisual, homeName, homeModel, homeStatus, homeSoftware, homeActions, secondary, homeMessage }) summary.Controls.Add(control);
            container.Controls.Add(summary, 0, 1);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
            scroll.Controls.Add(container);
            bool layingOut = false;
            Action fit = () => {
                if (layingOut) return;
                layingOut = true;
                try {
                    int width = Math.Max(260, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                    foreach (var label in new[] { homeName, homeModel, homeSoftware, homeMessage }) label.MaximumSize = new Size(width - 24, 0);
                    summary.MaximumSize = new Size(width, 0);
                    summary.MinimumSize = Size.Empty;
                    int height = summary.GetPreferredSize(new Size(width, 0)).Height;
                    summary.MinimumSize = new Size(0, height);
                    container.Size = new Size(width, Math.Max(scroll.ClientSize.Height, height + Theme.Px(scroll, 24)));
                } finally { layingOut = false; }
            };
            scroll.SizeChanged += (s, e) => fit();
            layoutHome = fit;
            return scroll;
        }

        private void RenderHome()
        {
            if (homeName == null) return;
            var snapshot = monitor.Snapshot;
            var device = SelectedDevice;
            bool connected = DeviceReady;
            homeVisual.Device = connected ? device : null;
            homeName.Text = connected ? device.Name : "Connect an iPhone or iPad";
            homeModel.Text = connected ? device.Product : "Connect your device using a USB cable to get started.";
            homeStatus.Set(snapshot.Connection, device, operation.Running ? operation.Title : null);
            homeSoftware.Visible = connected;
            homeSoftware.Text = !connected ? "" : device.Version == "Unavailable in recovery mode" || device.Version == "Not reported"
                ? "Software version not reported in this mode" : device.PlatformName + " " + device.Version + (device.InformationIsCached ? " · last reported" : "");
            homeActions.Visible = connected;
            homeRecovery.Visible = connected && device.Mode != DeviceMode.Dfu;
            homeRecovery.Text = connected && device.Mode == DeviceMode.Recovery ? "Exit Recovery Mode" : "Enter Recovery Mode";
            homeRecovery.Enabled = connected && !operation.Running && (device.Mode == DeviceMode.Normal || device.Ecid != 0);
            homeDetails.Enabled = connected && !operation.Running;
            homeFirmware.Visible = connected;
            homeFirmware.Enabled = !operation.Running;
            homeRefresh.Enabled = !operation.Running;
            homeMessage.Text = snapshot.Connection == ConnectionState.Error ? "Detection needs attention. Check Settings and open Activity for details."
                : snapshot.Error.Length > 0 ? "Some device information couldn’t be checked. See Activity for details."
                : connected && device.Mode == DeviceMode.Dfu ? "Open Recovery for the manual restart guide, or Firmware to select an IPSW."
                : connected ? "" : "Unlock your device and accept Trust This Computer if prompted.";
            layoutHome();
        }

        private async Task RefreshDevicesAsync()
        {
            if (operation.Running) return;
            homeRefresh.Enabled = false;
            try { await monitor.RefreshAsync(); }
            finally { if (!IsDisposed) homeRefresh.Enabled = !operation.Running; }
        }

        private void ShowDeviceDetails()
        {
            if (!DeviceReady || operation.Running) return;
            using (var dialog = new DeviceDetails(SelectedDevice)) dialog.ShowDialog(this);
        }
    }
}
