using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class RestoreConfirmation : Form
    {
        public RestoreConfirmation(Device device, Firmware firmware, bool erase)
        {
            Text = erase ? "Confirm erase restore" : "Confirm firmware update";
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 430);
            MinimumSize = new Size(600, 470);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI", 15F, FontStyle.Bold), Text = erase ? "This will delete all device data" : "Install firmware on this device" }, 0, 0);
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(510, 0), Margin = new Padding(0, 12, 0, 12), Text = device.Name + " · " + device.Product + "\nECID " + device.EcidArgument + "\niOS / iPadOS " + firmware.Version + " (" + firmware.Build + ")\n\n" + (erase ? "All apps, settings, and personal data will be erased. You will need the Apple Account associated with the device to activate it." : "The update attempts to preserve data, but data loss is possible. Apple must still sign this firmware. Revora cannot fix hardware faults.") }, 0, 1);
            var acknowledged = new CheckBox { Text = "I have a backup or accept the risk of data loss", AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
            layout.Controls.Add(acknowledged, 0, 2);
            var phrase = new TextBox { Dock = DockStyle.Top };
            if (erase) {
                var field = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown };
                field.Controls.Add(new Label { Text = "Type ERASE to confirm:", AutoSize = true });
                phrase.Width = 240;
                field.Controls.Add(phrase);
                layout.Controls.Add(field, 0, 3);
            }
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var confirm = MainForm.MakeButton(erase ? "Erase and install" : "Install update", true);
            confirm.Enabled = false;
            confirm.DialogResult = DialogResult.OK;
            var cancel = MainForm.MakeButton("Cancel", false);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(confirm);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 4);
            Action update = () => confirm.Enabled = acknowledged.Checked && (!erase || phrase.Text == "ERASE");
            acknowledged.CheckedChanged += (s, e) => update();
            phrase.TextChanged += (s, e) => update();
            CancelButton = cancel;
            Controls.Add(layout);
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox path;
        public string ToolsPath { get { return path.Text.Trim(); } }

        public SetupForm(string toolsPath, string dataPath)
        {
            Text = "Revora setup";
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 400);
            MinimumSize = new Size(640, 430);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "Connect the device tools", AutoSize = true, Font = new Font("Segoe UI", 15F, FontStyle.Bold) }, 0, 0);
            layout.Controls.Add(new Label { Text = "1. Install Apple Devices from Microsoft Store for Apple's USB drivers.\n2. Use the tools folder shipped with Revora, or build it using the repository's native build script.\n3. Connect your device, unlock it, and accept Trust This Computer.\n\nConnect one recovery / DFU device at a time. Firmware signing and device support depend on the native tools. Revora does not bypass Activation Lock.", AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 14, 0, 14) }, 0, 1);
            var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            path = new TextBox { Text = toolsPath, Dock = DockStyle.Fill };
            var browse = MainForm.MakeButton("Browse…", false);
            browse.Click += (s, e) => { using (var picker = new FolderBrowserDialog { Description = "Select the folder containing Apple device utilities", SelectedPath = path.Text }) if (picker.ShowDialog(this) == DialogResult.OK) path.Text = picker.SelectedPath; };
            row.Controls.Add(path, 0, 0);
            row.Controls.Add(browse, 1, 0);
            layout.Controls.Add(row, 0, 2);
            var logs = MainForm.MakeButton("Open logs and cache", false);
            logs.Margin = new Padding(0, 14, 0, 14);
            logs.Click += (s, e) => Process.Start(new ProcessStartInfo(dataPath) { UseShellExecute = true });
            layout.Controls.Add(logs, 0, 3);
            var save = MainForm.MakeButton("Save tools folder", true);
            save.DialogResult = DialogResult.OK;
            layout.Controls.Add(save, 0, 4);
            Controls.Add(layout);
        }
    }
}
