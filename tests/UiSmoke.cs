using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Revora;

internal static class UiSmoke
{
    private const string Properties = "DeviceName: UI test iPad\nProductType: iPad12,1\nHardwareModel: J181AP\nProductVersion: 18.0\nUniqueChipID: 123456\nSerialNumber: TEST123\nBuildVersion: 22A3354\n";
    private const string Manifest = "<plist><dict><key>ProductVersion</key><string>18.0</string><key>ProductBuildVersion</key><string>22A3354</string><key>SupportedProductTypes</key><array><string>iPad12,1</string></array><key>BuildIdentities</key><array><dict><key>Info</key><dict><key>DeviceClass</key><string>j181ap</string><key>RestoreBehavior</key><string>Update</string></dict></dict><dict><key>Info</key><dict><key>DeviceClass</key><string>j181ap</string><key>RestoreBehavior</key><string>Erase</string></dict></dict></array></dict></plist>";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string root = Path.GetFullPath(args[0]);
        if (args.Length == 2) return InspectLive(root, args[1]);
        string data = Path.Combine(root, "ui-data");
        Directory.CreateDirectory(root);
        int scans = 0;
        Discovery discovery = new Discovery();
        try {
            using (var form = new MainForm(data, () => { scans++; return Task.FromResult(discovery); }, false)) {
                Render(form, Path.Combine(root, "home-startup.png"));
                Pump(form.Monitor.RefreshAsync());
                Render(form, Path.Combine(root, "home-disconnected.png"));
                var device = Device.FromProperties(Properties, "test-udid");
                discovery.Devices.Add(device);
                Pump(form.Monitor.RefreshAsync());
                Render(form, Path.Combine(root, "home-connected.png"));
                var primary = FindButtons(form).First(b => b.Visible && b.Text == "Enter Recovery Mode");
                if (!primary.Enabled) throw new Exception("Connected recovery action must be enabled.");
                int changes = 0;
                form.Monitor.Changed += (s, e) => changes++;
                for (int i = 0; i < 5; i++) Pump(form.Monitor.RefreshAsync());
                if (changes != 0 || !primary.Enabled) throw new Exception("Unchanged scans must keep connected controls stable.");
                int before = scans;
                for (int i = 0; i < 3; i++) {
                    foreach (string page in new[] { "Home", "Recovery", "Firmware", "Activity", "Settings" }) form.Navigate(page);
                    using (var details = new DeviceDetails(device)) {
                        Render(details, Path.Combine(root, "device-details.png"));
                        var content = FindControls(details).OfType<Panel>().Single(p => p.AutoScroll);
                        if (content.AutoScrollPosition.Y != 0) throw new Exception("Device details must open at the heading, not scroll to the first identifier.");
                    }
                }
                if (scans != before) throw new Exception("Navigation and details must not create USB scans.");
                foreach (string page in new[] { "Home", "Recovery", "Firmware", "Activity", "Settings" }) {
                    form.Navigate(page);
                    Render(form, Path.Combine(root, page.ToLowerInvariant() + "-desktop.png"));
                    form.Size = form.MinimumSize;
                    Render(form, Path.Combine(root, page.ToLowerInvariant() + "-minimum.png"));
                    if (FindControls(form).OfType<Panel>().Any(p => p.Visible && p.AutoScroll && p.HorizontalScroll.Visible))
                        throw new Exception("Unexpected horizontal scrolling on " + page + ": " + string.Join("; ", FindControls(form).OfType<Panel>().Where(p => p.Visible && p.AutoScroll).Select(p => p.Bounds + " child " + p.Controls[0].Bounds + " min " + p.AutoScrollMinSize)));
                    form.Size = new Size(1080, 780);
                }
                string ipsw = Path.Combine(data, "test.ipsw");
                using (var archive = ZipFile.Open(ipsw, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("BuildManifest.plist").Open())) writer.Write(Manifest);
                var read = Firmware.ReadAsync(ipsw, null);
                Pump(read);
                var firmware = read.Result;
                form.Navigate("Firmware");
                form.Flow.ContinueFromDevice();
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-file.png"));
                form.Flow.SelectFirmware(firmware);
                form.Flow.ContinueFromFirmware();
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-type.png"));
                form.Flow.SelectType(true);
                form.Flow.Review(device);
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-confirmation.png"));
                var install = FindButtons(form).Single(b => b.Text == "Erase and install");
                if (install.Enabled) throw new Exception("Erase must require acknowledgement.");
                var checkbox = FindControls(form).OfType<CheckBox>().Single();
                var phrase = FindControls(form).OfType<TextBox>().Single(t => t.AccessibleName == "Type ERASE to confirm deletion");
                checkbox.Checked = true; phrase.Text = "erase";
                if (install.Enabled) throw new Exception("Erase phrase must match exactly.");
                phrase.Text = "ERASE";
                if (!install.Enabled) throw new Exception("Confirmed erase must enable installation.");
                form.Flow.Start();
                form.Operation.Begin(OperationKind.InstallingFirmware, "Restoring firmware");
                form.Operation.Stage = "Uploading file system";
                form.Operation.Progress = 42;
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-installation.png"));
                FindButtons(form).Single(b => b.Visible && b.Text == "View details").PerformClick();
                Render(form, Path.Combine(root, "activity-operation-details.png"));
                var firmwareNavigation = FindControls(form).OfType<RevoraButton>().Single(b => b.Kind == ButtonKind.Navigation && b.Text == "Firmware");
                if (!firmwareNavigation.Enabled) throw new Exception("Activity must allow returning to installation progress.");
                firmwareNavigation.PerformClick();
                form.Flow.Complete("UI test: USB connection was lost.");
                form.Operation.Fail("UI test: USB connection was lost.", "Check the USB cable and device before trying again.");
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-error.png"));
                form.Flow.Complete("");
                form.Navigate("Firmware");
                Render(form, Path.Combine(root, "firmware-success.png"));
                form.Flow.Reset();
                discovery = new Discovery();
                discovery.Devices.Add(Device.FromProperties("NAME: Test iPad\nPRODUCT: iPad12,1\nMODEL: j181ap\nMODE: Recovery\nECID: 0x1e240\n", null));
                Pump(form.Monitor.RefreshAsync());
                form.Navigate("Home");
                if (!FindButtons(form).Any(b => b.Visible && b.Text == "Exit Recovery Mode")) throw new Exception("Recovery must show exit action.");
                Render(form, Path.Combine(root, "home-recovery.png"));
                discovery = new Discovery();
                discovery.Devices.Add(Device.FromProperties("PRODUCT: iPad12,1\nMODEL: j181ap\nMODE: DFU\nECID: 0x1e240\n", null));
                Pump(form.Monitor.RefreshAsync());
                form.Navigate("Recovery");
                Render(form, Path.Combine(root, "recovery-dfu.png"));
                discovery = new Discovery();
                Pump(form.Monitor.RefreshAsync());
                form.Navigate("Home");
                Render(form, Path.Combine(root, "home-unplugged.png"));
                if (FindButtons(form).Any(b => b.Visible && b.Text == "Exit Recovery Mode")) throw new Exception("Disconnect must clear device actions.");
                discovery.Issues.Add("UI test: Windows policy blocked a device tool.");
                Pump(form.Monitor.RefreshAsync());
                Render(form, Path.Combine(root, "home-detection-error.png"));
                discovery = new Discovery();
                discovery.Devices.Add(Device.FromProperties(Properties, "test-udid"));
                Pump(form.Monitor.RefreshAsync());
                for (int scale = 100; scale <= 200; scale += 25) {
                    using (var scaled = new MainForm(Path.Combine(data, "scale-" + scale), () => Task.FromResult(discovery), false)) {
                        scaled.ShowInTaskbar = false; scaled.Location = new Point(-10000, -10000); scaled.Show();
                        Pump(scaled.Monitor.RefreshAsync());
                        float factor = scale / 100F;
                        var fonts = FindControls(scaled).Where(c => c is Label || c is Button || c is TextBox || c is CheckBox || c is StatusIndicator)
                            .ToDictionary(c => c, c => new Font(c.Font.FontFamily, c.Font.Size * factor, c.Font.Style));
                        scaled.Scale(new SizeF(factor, factor));
                        foreach (var font in fonts) font.Key.Font = font.Value;
                        scaled.MinimumSize = new Size((int)(900 * factor), (int)(680 * factor));
                        scaled.Size = scaled.MinimumSize;
                        var navigation = FindControls(scaled).OfType<RevoraButton>().Where(b => b.Kind == ButtonKind.Navigation).ToArray();
                        var sidebar = (Panel)navigation[0].Parent.Parent;
                        foreach (var item in navigation) {
                            sidebar.ScrollControlIntoView(item);
                            Application.DoEvents();
                            var location = sidebar.PointToClient(item.PointToScreen(Point.Empty));
                            if (location.Y < 0 || location.Y + item.Height > sidebar.ClientSize.Height)
                                throw new Exception("Navigation item is unreachable at scale " + scale + ": " + item.Text);
                        }
                        sidebar.AutoScrollPosition = Point.Empty;
                        foreach (string page in new[] { "Home", "Recovery", "Firmware", "Activity", "Settings" }) {
                            scaled.Navigate(page);
                            if (page == "Home") {
                                var visual = FindControls(scaled).OfType<DeviceVisual>().Single();
                                if (visual.Parent.Height <= 0) throw new Exception("Device summary collapsed at scale " + scale);
                            }
                            Render(scaled, Path.Combine(root, "layout-" + scale + "-" + page.ToLowerInvariant() + ".png"));
                            if (FindControls(scaled).OfType<Panel>().Any(p => p.Visible && p.AutoScroll && p.HorizontalScroll.Visible))
                                throw new Exception("Horizontal scrolling at layout scale " + scale + " on " + page);
                            if (page == "Home") {
                                var visual = FindControls(scaled).OfType<DeviceVisual>().Single();
                                var viewport = (Panel)visual.Parent.Parent.Parent;
                                viewport.ScrollControlIntoView(FindButtons(scaled).Single(b => b.Visible && b.Text == "Refresh"));
                                Application.DoEvents();
                                var refresh = FindButtons(scaled).Single(b => b.Visible && b.Text == "Refresh");
                                var position = viewport.PointToClient(refresh.PointToScreen(Point.Empty));
                                if (position.Y < 0 || position.Y + refresh.Height > viewport.ClientSize.Height)
                                    throw new Exception("Home actions are unreachable at scale " + scale);
                                Render(scaled, Path.Combine(root, "layout-" + scale + "-home-actions.png"));
                            }
                        }
                    }
                }
                Console.WriteLine("Rendered Windows DPI: " + form.DeviceDpi + ". Additional 100-200% layout scales are simulated; real monitor-DPI transitions require manual validation.");
            }
            using (var notice = new NoticeDialog("Device tool couldn’t start", "Check Windows application-control policy and the selected tools folder.", "UI test: launch blocked (1260).")) {
                Render(notice, Path.Combine(root, "notice.png"));
                FindButtons(notice).Single(b => b.Text == "Show details").PerformClick();
                Render(notice, Path.Combine(root, "notice-details.png"));
            }
            using (var confirm = new ActionConfirmation("Enter Recovery Mode?", "Your device will restart into Apple’s recovery environment.", "Enter Recovery Mode")) Render(confirm, Path.Combine(root, "recovery-confirmation.png"));
            using (var chooser = new DeviceChooser(new[] { Device.FromProperties(Properties, "test-udid") })) Render(chooser, Path.Combine(root, "device-chooser.png"));
            Console.WriteLine("UI checks passed: all pages, firmware steps, modals, stable USB polling, transitions, confirmation guards and minimum/scaled layouts.");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
    }

    private static void Pump(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted) { Application.DoEvents(); if (timeout.Elapsed.TotalSeconds > 10) throw new TimeoutException("UI test task timed out."); System.Threading.Thread.Sleep(1); }
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static int InspectLive(string root, string tools)
    {
        string data = Path.Combine(root, "live-ui-data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "tools-path.txt"), Path.GetFullPath(tools));
        try {
            using (var form = new MainForm(data, null, true)) {
                Render(form, Path.Combine(root, "live-startup.png"));
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (form.Monitor.IsScanning) {
                    Application.DoEvents();
                    if (timeout.Elapsed.TotalSeconds > 60) throw new TimeoutException("Live device scan did not finish.");
                    System.Threading.Thread.Sleep(10);
                }
                foreach (string page in new[] { "Home", "Recovery", "Firmware", "Activity", "Settings" }) {
                    form.Navigate(page);
                    Render(form, Path.Combine(root, "live-" + page.ToLowerInvariant() + ".png"));
                }
                Console.WriteLine("Read-only USB check: " + form.Monitor.Snapshot.Connection + ", " + form.Monitor.Snapshot.Devices.Count + " devices.");
                if (form.Monitor.Snapshot.CurrentDevice != null) Console.WriteLine("Mode: " + form.Monitor.Snapshot.CurrentDevice.ModeLabel);
                if (form.Monitor.Snapshot.Error.Length > 0) Console.WriteLine(form.Monitor.Snapshot.Error);
            }
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(data, true); }
    }

    private static void Render(Form form, string path)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        if (!form.Visible) form.Show();
        Application.DoEvents(); form.CreateControl(); form.PerformLayout();
        if (!form.Controls.Cast<Control>().Any(c => c.Visible)) throw new Exception("UI controls were not rendered.");
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        foreach (var button in FindControls(form).OfType<RevoraButton>().Where(b => b.Visible)) {
            using (var graphics = button.CreateGraphics()) {
                int width = TextRenderer.MeasureText(graphics, button.Text, button.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                int available = button.Width - Theme.Px(button, button.Kind == ButtonKind.Navigation ? 12 : 16) - Theme.Px(button, 12)
                    - (button.IconName == null ? 0 : Theme.Px(button, 28));
                if (width > available) throw new Exception("Button label clipped: " + button.Text + " (" + width + " > " + available + ", DPI " + button.DeviceDpi + ", bounds " + button.Bounds + ", parent " + button.Parent.Bounds + ").");
            }
        }
    }
    private static IEnumerable<Control> FindControls(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var descendant in FindControls(child)) yield return descendant; }
    }
    private static IEnumerable<Button> FindButtons(Control root) { return FindControls(root).OfType<Button>(); }
}
