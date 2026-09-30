using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private Panel pageHost;
        private Label pageTitle;
        private Label footer;
        private RevoraButton switchDevice;

        private Control CreateShell()
        {
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(16, 24, 16, 20), ColumnCount = 1, RowCount = 8, Margin = new Padding(0) };
            sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            for (int i = 0; i < 4; i++) sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var brand = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), WrapContents = false };
            brand.Controls.Add(new BrandArtwork { Size = new Size(30, 30), BackColor = Theme.Sidebar, ForeColor = Theme.Ink, Margin = new Padding(0, 2, 8, 0) });
            brand.Controls.Add(new Label { Text = "revora", Font = Theme.Font(20F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0) });
            sidebar.Controls.Add(brand, 0, 0);
            string[] names = { "Home", "Recovery", "Firmware", "Activity", "Settings" };
            string[] icons = { "home", "restart", "download", "clock", "settings" };
            for (int i = 0; i < names.Length; i++) {
                string name = names[i];
                var button = new RevoraButton(name, ButtonKind.Navigation, icons[i]) { Dock = DockStyle.Fill, Height = 40, Margin = new Padding(0, 2, 0, 2) };
                button.Click += (s, e) => ShowPage(name);
                navigation.Add(name, button);
                sidebar.Controls.Add(button, 0, i == 4 ? 6 : i + 1);
            }
            sidebar.Controls.Add(new Label { Text = Application.ProductVersion, AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Font(9F), Margin = new Padding(12, 8, 0, 0) }, 0, 7);
            shell.Controls.Add(sidebar, 0, 0);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(32, 20, 32, 8), Margin = new Padding(0) };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pageTitle = new Label { Font = Theme.Font(19F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0) };
            switchDevice = new RevoraButton("Switch device", ButtonKind.Quiet) { Height = 32 };
            switchDevice.Click += (s, e) => {
                using (var picker = new DeviceChooser(monitor.Snapshot.Devices)) {
                    if (picker.ShowDialog(this) == DialogResult.OK) monitor.Select(picker.SelectedIdentity);
                }
            };
            header.Controls.Add(pageTitle, 0, 0);
            header.Controls.Add(switchDevice, 1, 0);
            content.Controls.Add(header, 0, 0);
            pageHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32, 8, 32, 8), Margin = new Padding(0) };
            pages.Add("Home", CreateHome());
            pages.Add("Recovery", CreateRecovery());
            pages.Add("Firmware", CreateFirmware());
            pages.Add("Activity", CreateActivity());
            pages.Add("Settings", CreateSettings());
            foreach (var page in pages.Values) { page.Dock = DockStyle.Fill; page.Visible = false; pageHost.Controls.Add(page); }
            content.Controls.Add(pageHost, 0, 1);
            footer = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = Theme.Font(9F), Padding = new Padding(32, 8, 0, 0), Margin = new Padding(0), AccessibleName = "Device and operation status" };
            content.Controls.Add(footer, 0, 2);
            shell.Controls.Add(content, 1, 0);
            return shell;
        }

        private void ShowPage(string name)
        {
            if (!pages.ContainsKey(name)) throw new ArgumentException("Unknown page.", "name");
            foreach (var page in pages) page.Value.Visible = page.Key == name;
            foreach (var item in navigation) item.Value.Selected = item.Key == name;
            pageTitle.Text = name == "Recovery" ? "Recovery Mode" : name;
            pages[name].BringToFront();
            if (name == "Activity") RenderActivity();
            RenderState();
        }

        private void RenderFooter()
        {
            if (footer == null) return;
            if (operation.Running) footer.Text = operation.Stage + (operation.Progress.HasValue ? " · " + operation.Progress + "% of this stage" : "…");
            else if (monitor.Snapshot.Connection == ConnectionState.Connected) footer.Text = SelectedDevice.Name + " · " + SelectedDevice.ModeLabel;
            else if (monitor.Snapshot.Connection == ConnectionState.Error) footer.Text = "Device detection needs attention · see Activity";
            else footer.Text = monitor.Snapshot.Connection == ConnectionState.Initializing || monitor.Snapshot.Connection == ConnectionState.Scanning ? "Checking for devices…" : "Waiting for a device";
        }

        private static TableLayoutPanel Stack()
        {
            var stack = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Margin = new Padding(0) };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stack.ControlAdded += (s, e) => {
                stack.RowCount = stack.Controls.Count;
                while (stack.RowStyles.Count < stack.RowCount) stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                stack.SetCellPosition(e.Control, new TableLayoutPanelCellPosition(0, stack.Controls.Count - 1));
            };
            return stack;
        }

        private static Control ScrollPage(TableLayoutPanel body)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
            body.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            scroll.Controls.Add(body);
            scroll.SizeChanged += (s, e) => {
                int width = Math.Max(200, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                body.MinimumSize = Size.Empty;
                body.MaximumSize = new Size(width, 0);
                body.MinimumSize = new Size(width, 0);
                foreach (var label in body.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
            };
            return scroll;
        }

        private static Label Heading(string text) { return new Label { Text = text, Font = Theme.Font(22F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 12) }; }
        private static Label Body(string text, int bottom = 24) { return new Label { Text = text, Font = Theme.Font(10F), AutoSize = true, ForeColor = Theme.Muted, MaximumSize = new Size(620, 0), Margin = new Padding(0, 0, 0, bottom) }; }
        private static FlowLayoutPanel Actions(params Control[] buttons)
        {
            var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 16), WrapContents = true };
            row.Controls.AddRange(buttons);
            return row;
        }
    }
}

