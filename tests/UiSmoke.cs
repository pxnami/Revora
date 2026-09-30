using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Revora;

internal static class UiSmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string root = Path.GetFullPath(args[0]);
        string data = Path.Combine(root, "ui-data");
        Directory.CreateDirectory(root);
        try {
            using (var form = new MainForm(data)) {
                Render(form, Path.Combine(root, "revora-desktop.png"));
                var actions = FindButtons(form).Where(b => b.Text == "Install firmware" || b.Text == "Enter recovery" || b.Text == "Exit recovery").ToArray();
                if (actions.Length != 3 || actions.Any(b => b.Enabled)) throw new Exception("Device actions must be disabled without a device.");
                form.Size = form.MinimumSize;
                Render(form, Path.Combine(root, "revora-small-window.png"));
            }
            var device = Device.FromProperties("ProductType: iPhone15,2\nHardwareModel: D73AP\nUniqueChipID: 123456\n", "test");
            var firmware = Firmware.Parse("<plist><dict><key>ProductVersion</key><string>18.0</string><key>ProductBuildVersion</key><string>22A3354</string><key>SupportedProductTypes</key><array><string>iPhone15,2</string></array><key>BuildIdentities</key><array><dict/></array></dict></plist>");
            using (var confirm = new RestoreConfirmation(device, firmware, true)) {
                Render(confirm, Path.Combine(root, "revora-erase-confirmation.png"));
                var action = FindButtons(confirm).Single(b => b.Text == "Erase and install");
                var check = FindControls(confirm).OfType<CheckBox>().Single();
                var phrase = FindControls(confirm).OfType<TextBox>().Single();
                if (action.Enabled) throw new Exception("Erase is enabled without confirmation.");
                check.Checked = true;
                phrase.Text = "erase";
                if (action.Enabled) throw new Exception("Erase requires exact confirmation text.");
                phrase.Text = "ERASE";
                if (!action.Enabled) throw new Exception("Erase should enable after acknowledgement and exact text.");
            }
            using (var setup = new SetupForm("C:\\Revora\\tools", data)) Render(setup, Path.Combine(root, "revora-setup.png"));
            Console.WriteLine("UI smoke checks passed: startup guards, erase confirmation, desktop and small-window rendering.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
    }

    private static void Render(Form form, string path)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        if (!form.Visible) form.Show();
        Application.DoEvents();
        form.CreateControl();
        form.PerformLayout();
        if (!form.Controls.Cast<Control>().Any(c => c.Visible)) throw new Exception("UI controls were not rendered.");
        using (var bitmap = new Bitmap(form.Width, form.Height)) {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    private static System.Collections.Generic.IEnumerable<Control> FindControls(Control root)
    {
        foreach (Control child in root.Controls) {
            yield return child;
            foreach (var descendant in FindControls(child)) yield return descendant;
        }
    }
    private static System.Collections.Generic.IEnumerable<Button> FindButtons(Control root) { return FindControls(root).OfType<Button>(); }
}
