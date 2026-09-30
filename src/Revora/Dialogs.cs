using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal class RevoraDialog : Form
    {
        protected readonly TableLayoutPanel BodyPanel;
        protected readonly FlowLayoutPanel Buttons;
        public RevoraDialog(string title, int width = 560, int height = 360)
        {
            Text = title + " · Revora";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = Theme.Font(10F);
            ForeColor = Theme.Ink;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(width, height);
            MinimumSize = new Size(width, height);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            BodyPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            BodyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scroll.Controls.Add(BodyPanel);
            Buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 16, 0, 0) };
            root.Controls.Add(scroll, 0, 0);
            root.Controls.Add(Buttons, 0, 1);
            Controls.Add(root);
            Shown += (s, e) => {
                if (CancelButton is Control) ActiveControl = (Control)CancelButton;
                scroll.AutoScrollPosition = Point.Empty;
            };
            AddText(title, true);
        }
        protected void AddText(string text, bool heading = false)
        {
            BodyPanel.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(ClientSize.Width - 72, 0),
                Font = Theme.Font(heading ? 18F : 10F, heading ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = heading ? Theme.Ink : Theme.Muted, Margin = new Padding(0, 0, 0, 18) });
        }
        protected RevoraButton AddButton(string text, DialogResult result, ButtonKind kind)
        {
            var button = new RevoraButton(text, kind) { DialogResult = result };
            Buttons.Controls.Add(button);
            return button;
        }
    }

    internal sealed class NoticeDialog : RevoraDialog
    {
        public NoticeDialog(string title, string message, string detail) : base(title, 560, 380)
        {
            AddText(message);
            if (!string.IsNullOrEmpty(detail)) {
                var text = new TextBox { Text = detail, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Top, Height = 110, Visible = false, Font = new Font("Consolas", 9F) };
                var toggle = new RevoraButton("Show details", ButtonKind.Quiet);
                toggle.Click += (s, e) => { text.Visible = !text.Visible; toggle.Text = text.Visible ? "Hide details" : "Show details"; };
                BodyPanel.Controls.Add(toggle);
                BodyPanel.Controls.Add(text);
            }
            var close = AddButton("Close", DialogResult.OK, ButtonKind.Primary);
            AcceptButton = close; CancelButton = close;
        }
    }

    internal sealed class ActionConfirmation : RevoraDialog
    {
        public ActionConfirmation(string title, string message, string action) : base(title)
        {
            AddText(message);
            var confirm = AddButton(action, DialogResult.OK, ButtonKind.Primary);
            CancelButton = AddButton("Cancel", DialogResult.Cancel, ButtonKind.Secondary);
            AcceptButton = confirm;
        }
    }

    internal sealed class DeviceChooser : RevoraDialog
    {
        public string SelectedIdentity { get; private set; }
        public DeviceChooser(IEnumerable<Device> devices) : base("Choose a device")
        {
            foreach (var device in devices) {
                var selected = device;
                var button = new RevoraButton(device.Name + " · " + device.ModeLabel) { Width = 460, Height = 44, Margin = new Padding(0, 0, 0, 12) };
                button.Click += (s, e) => { SelectedIdentity = selected.Identity; DialogResult = DialogResult.OK; Close(); };
                BodyPanel.Controls.Add(button);
            }
            CancelButton = AddButton("Cancel", DialogResult.Cancel, ButtonKind.Secondary);
        }
    }
}
