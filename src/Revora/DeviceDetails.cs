using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class DeviceDetails : Form
    {
        public DeviceDetails(Device device)
        {
            Text = "Device information · Revora";
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(600, 540);
            MinimumSize = new Size(500, 420);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { Text = device.Name, AutoSize = true, Font = new Font(Font.FontFamily, 20F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 18) }, 0, 0);
            var rows = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.CellSelect };
            rows.DefaultCellStyle.SelectionBackColor = Color.Black;
            rows.DefaultCellStyle.SelectionForeColor = Color.White;
            rows.Columns.Add("field", "Field");
            rows.Columns.Add("value", "Reported value");
            Add(rows, "Connection mode", device.Mode.ToString());
            Add(rows, "Product model", device.Product);
            Add(rows, "Hardware board", device.Board);
            Add(rows, "iOS / iPadOS", device.Version);
            Add(rows, "Build", device.BuildVersion);
            Add(rows, "Serial number", device.SerialNumber);
            Add(rows, "UDID", device.Udid);
            Add(rows, "ECID", device.Ecid == 0 ? null : device.EcidArgument);
            Add(rows, "Activation state", device.ActivationState);
            Add(rows, "Wi-Fi address", device.WifiAddress);
            root.Controls.Add(rows, 0, 1);
            root.Controls.Add(new Label { Text = "Values come from the selected device. Some fields are unavailable in recovery or DFU mode. Select cells and press Ctrl+C to copy.", AutoSize = true, MaximumSize = new Size(540, 0), Margin = new Padding(0, 16, 0, 0) }, 0, 2);
            Controls.Add(root);
        }

        private static void Add(DataGridView rows, string field, string value)
        {
            rows.Rows.Add(field, string.IsNullOrEmpty(value) ? "Not reported" : value);
        }
    }

    internal sealed class ManagementGuide : Form
    {
        public ManagementGuide()
        {
            Text = "Profile removal · Revora";
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 510);
            MinimumSize = new Size(560, 440);
            StartPosition = FormStartPosition.CenterParent;
            var root = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            root.Controls.Add(new Label { Text = "Remove a management profile", Font = new Font(Font.FontFamily, 19F, FontStyle.Bold), AutoSize = true, MaximumSize = new Size(550, 0), Margin = new Padding(0, 0, 0, 16) });
            root.Controls.Add(new Label { Text = "On your iPhone or iPad:\n\n1. Open Settings → General → VPN & Device Management.\n2. Select the profile and choose Remove Profile or Remove Management, if available.\n3. Enter the device passcode and profile removal password if prompted.\n\nRemoving a profile also removes its associated settings, apps, and data.\n\nIf removal is unavailable or the device shows Remote Management during setup, ask the enrolling organization to unenroll or release it. Restoring firmware may enroll it again.\n\nRevora provides these instructions; it does not remove profiles over USB or verify a removal password. Enter passwords only on the device or your organization’s official management portal.", AutoSize = true, MaximumSize = new Size(550, 0), Margin = new Padding(0, 0, 0, 20) });
            var documentation = MainForm.MakeButton("Open Apple’s removal instructions", false);
            documentation.Click += (s, e) => Process.Start(new ProcessStartInfo("https://support.apple.com/guide/iphone/iph6c493b19/ios") { UseShellExecute = true });
            root.Controls.Add(documentation);
            Controls.Add(root);
        }
    }
}
