using System;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private Button details;
        private Button firmwareStart;

        private Control CreateHome()
        {
            var root = Stack(new Padding(28, 20, 28, 20));
            root.Dock = DockStyle.None;
            root.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 20) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new BrandArtwork { Size = new Size(40, 40), LogoOnly = true }, 0, 0);
            header.Controls.Add(new Label { Text = "revora", Font = new Font("Segoe UI", 22F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0) }, 1, 0);
            header.Controls.Add(setup, 2, 0);
            root.Controls.Add(header);

            var hero = new TableLayoutPanel { Dock = DockStyle.Top, Height = 210, BackColor = Color.Black, ForeColor = Color.White, ColumnCount = 2, Padding = new Padding(26), Margin = new Padding(0, 0, 0, 20) };
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            var intro = Stack(new Padding(0));
            intro.Controls.Add(new Label { Text = "A fresh start for your device.", Font = new Font("Segoe UI", 23F, FontStyle.Bold), AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 0, 0, 14) });
            intro.Controls.Add(new Label { Text = "Recovery mode and firmware installs for iPhone and iPad.\nConnect by USB, unlock your device, and let’s begin.", AutoSize = true, MaximumSize = new Size(540, 0), ForeColor = Color.FromArgb(200, 200, 200), Margin = new Padding(0, 0, 0, 20) });
            firmwareStart = MakeButton("Choose firmware  →", false);
            firmwareStart.Click += async (s, e) => await ChooseFirmwareAsync();
            intro.Controls.Add(firmwareStart);
            hero.Controls.Add(intro, 0, 0);
            hero.Controls.Add(new BrandArtwork { Dock = DockStyle.Fill, ForeColor = Color.White, BackColor = Color.Black }, 1, 0);
            root.Controls.Add(hero);

            var connected = Stack(new Padding(0));
            var deviceRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, Margin = new Padding(0) };
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            deviceRow.Controls.Add(new Label { Text = "Connected device", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            deviceRow.Controls.Add(devices, 1, 0);
            deviceRow.Controls.Add(refresh, 2, 0);
            connected.Controls.Add(deviceRow);
            connected.Controls.Add(deviceInfo);
            root.Controls.Add(connected);

            var tools = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 16) };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            var recovery = Tile("Recovery mode", "Enter recovery to prepare an install. Exit recovery to request a normal restart.");
            recovery.Controls.Add(enter);
            exit.Margin = new Padding(0, 10, 0, 0);
            recovery.Controls.Add(exit);
            recovery.Controls.Add(Note("DFU devices need a manual force restart to exit.", 16));
            tools.Controls.Add(recovery, 0, 0);

            var install = Tile("Firmware restore", "Choose a compatible IPSW. Apple checks firmware signing during the install.");
            install.Controls.Add(chooseFirmware);
            install.Controls.Add(firmwareInfo);
            erase.MaximumSize = new Size(280, 0);
            install.Controls.Add(erase);
            install.Controls.Add(restore);
            install.Controls.Add(Note("Back up first. Update attempts to preserve data; erase deletes it.", 12));
            tools.Controls.Add(install, 1, 0);

            var more = Stack(new Padding(0));
            var information = Tile("Device information", "View the model, serial number, iOS version, and identifiers reported over USB.");
            details = MakeButton("View details", false);
            details.Click += (s, e) => { if (SelectedDevice != null) using (var dialog = new DeviceDetails(SelectedDevice)) dialog.ShowDialog(this); };
            information.Controls.Add(details);
            more.Controls.Add(information);
            var management = Tile("Device management", "Remove a removable profile using its password in device Settings.");
            var guide = MakeButton("Profile removal guide", false);
            guide.Click += (s, e) => { using (var dialog = new ManagementGuide()) dialog.ShowDialog(this); };
            management.Controls.Add(guide);
            more.Controls.Add(management);
            tools.Controls.Add(more, 2, 0);
            root.Controls.Add(tools);

            var activity = MakeButton("Show activity", false);
            activity.Click += (s, e) => log.Visible = !log.Visible;
            log.VisibleChanged += (s, e) => activity.Text = log.Visible ? "Hide activity" : "Show activity";
            root.Controls.Add(activity);
            log.Height = 108;
            log.Dock = DockStyle.Top;
            log.Font = new Font("Consolas", 9F);
            log.Visible = false;
            root.Controls.Add(log);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(root);
            scroll.SizeChanged += (s, e) => {
                int contentWidth = Math.Max(200, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                root.MinimumSize = Size.Empty;
                root.MaximumSize = new Size(contentWidth, 0);
                root.MinimumSize = new Size(contentWidth, 0);
                hero.Height = scroll.Width < 1000 ? 260 : 210;
                foreach (Control child in intro.Controls) {
                    if (child is Label) child.MaximumSize = new Size(Math.Max(200, (int)((scroll.ClientSize.Width - 110) * .58) - 20), 0);
                }
            };
            root.SizeChanged += (s, e) => deviceInfo.MaximumSize = new Size(Math.Max(200, root.Width - 70), 0);
            var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            frame.Controls.Add(scroll, 0, 0);
            var footer = Stack(new Padding(28, 0, 28, 14));
            footer.Controls.Add(status);
            progress.Height = 6;
            footer.Controls.Add(progress);
            frame.Controls.Add(footer, 0, 1);
            return frame;
        }

        private static TableLayoutPanel Stack(Padding padding)
        {
            var stack = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1, Padding = padding, Margin = new Padding(0) };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return stack;
        }

        private static TableLayoutPanel Tile(string title, string description)
        {
            var tile = Stack(new Padding(18));
            tile.BackColor = Color.FromArgb(245, 245, 245);
            tile.Margin = new Padding(0, 0, 10, 10);
            tile.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 14F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
            tile.Controls.Add(Note(description, 0));
            tile.SizeChanged += (s, e) => {
                foreach (Control child in tile.Controls) {
                    if (child is Label) child.MaximumSize = new Size(Math.Max(120, tile.Width - 42), 0);
                }
            };
            return tile;
        }

        private static Label Note(string text, int top)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(280, 0), ForeColor = Muted, Margin = new Padding(0, top, 0, 16) };
        }
    }
}

