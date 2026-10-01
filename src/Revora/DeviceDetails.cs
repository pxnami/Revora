using System;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class DeviceDetails : RevoraDialog
    {
        private readonly Timer feedback = new Timer { Interval = 1500 };
        private RevoraButton copiedButton;
        public DeviceDetails(Device device) : base("Device details", 660, 600)
        {
            AddText(device.Name + " · " + device.ModeLabel + "\nSnapshot captured at " + DateTime.Now.ToString("HH:mm:ss") + (device.InformationIsCached ? ". Some values are cached from the last normal-mode connection." : ". Values reported by the device."));
            Group("General");
            Row("Name", device.Name);
            Row("Mode", device.ModeLabel);
            Row(device.PlatformName + " version", device.Version);
            Row("Build", device.BuildVersion);
            Row("Activation", device.ActivationState);
            Group("Hardware");
            Row("Product model", device.Product);
            Row("Hardware board", device.Board);
            Group("Identifiers");
            Row("Serial number", device.SerialNumber, true);
            Row("UDID", device.Udid, true);
            Row("ECID", device.Ecid == 0 ? null : device.EcidArgument, true);
            Group("Network");
            Row("Wi-Fi address", device.WifiAddress, true);
            CancelButton = AddButton("Done", DialogResult.OK, ButtonKind.Primary);
            AcceptButton = CancelButton;
            Shown += (s, e) => ActiveControl = (Control)CancelButton;
            feedback.Tick += (s, e) => { if (copiedButton != null) { copiedButton.Text = "Copy"; copiedButton.AccessibleName = "Copy value"; } copiedButton = null; feedback.Stop(); };
        }
        private void Group(string name)
        {
            BodyPanel.Controls.Add(new Label { Text = name, AutoSize = true, Font = Theme.Font(12F, FontStyle.Bold), Margin = new Padding(0, 16, 0, 12) });
        }
        private void Row(string title, string value, bool identifier = false)
        {
            bool available = !string.IsNullOrEmpty(value) && value != "Unknown" && value != "Not reported";
            var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            row.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 8, 8, 0) }, 0, 0);
            var text = new TextBox { Text = available ? value : "Not reported", ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Background,
                ForeColor = available ? Theme.Ink : Theme.Muted, Font = identifier ? new Font("Consolas", 9F) : Theme.Font(10F),
                Multiline = true, Height = identifier && value != null && value.Length > 30 ? 40 : 24, Dock = DockStyle.Top, Margin = new Padding(0, 8, 8, 0), AccessibleName = title, TabStop = identifier && available };
            row.Controls.Add(text, 1, 0);
            if (identifier) {
                var copy = new RevoraButton("Copy", ButtonKind.Quiet, "copy") { Width = 104, Height = 32, Enabled = available, AccessibleName = "Copy " + title };
                copy.Click += (s, e) => {
                    try {
                        Clipboard.SetText(value);
                        if (copiedButton != null) copiedButton.Text = "Copy";
                        copiedButton = copy; copy.Text = "Copied"; copy.AccessibleName = title + " copied"; feedback.Stop(); feedback.Start();
                    } catch (System.Runtime.InteropServices.ExternalException error) {
                        using (var notice = new NoticeDialog("Couldn’t copy", "The Windows clipboard is busy. Select the value and try copying it again.", error.Message)) notice.ShowDialog(this);
                    }
                };
                row.Controls.Add(copy, 2, 0);
            }
            BodyPanel.Controls.Add(row);
        }
        protected override void Dispose(bool disposing) { if (disposing) feedback.Dispose(); base.Dispose(disposing); }
    }
}
