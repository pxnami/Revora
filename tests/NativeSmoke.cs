using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Revora;

internal static class NativeSmoke
{
    private static int Main(string[] args)
    {
        try { RunAsync(args[0]).GetAwaiter().GetResult(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static async Task RunAsync(string toolsPath)
    {
        string temp = Path.Combine(Path.GetTempPath(), "revora-native-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            var runner = new ToolRunner(toolsPath, temp);
            if (runner.MissingTools().Length != 0) throw new Exception("Native package is incomplete.");
            foreach (string tool in ToolRunner.RequiredTools) {
                var result = await runner.RunAsync(tool, new[] { "--help" }, 20, null);
                result.EnsureSuccess("Native executable check: " + tool);
                Console.WriteLine("PASS: " + tool + " launches from Windows with bundled DLLs");
            }
            string xml = Path.Combine(temp, "manifest with spaces.xml");
            string binary = Path.Combine(temp, "manifest binary.plist");
            File.WriteAllText(xml, "<plist><dict><key>ProductVersion</key><string>18.0</string><key>ProductBuildVersion</key><string>22A3354</string><key>SupportedProductTypes</key><array><string>iPhone15,2</string></array><key>BuildIdentities</key><array><dict><key>Info</key><dict><key>DeviceClass</key><string>d73ap</string><key>RestoreBehavior</key><string>Update</string></dict></dict></array></dict></plist>");
            var converted = await runner.RunAsync("plistutil", new[] { "-i", xml, "-o", binary, "-f", "bin" }, 20, null);
            converted.EnsureSuccess("Creating binary plist fixture");
            string ipsw = Path.Combine(temp, "binary manifest firmware.ipsw");
            using (var archive = ZipFile.Open(ipsw, ZipArchiveMode.Create)) archive.CreateEntryFromFile(binary, "BuildManifest.plist");
            var firmware = await Firmware.ReadAsync(ipsw, runner);
            firmware.Validate(new Device { Product = "iPhone15,2", Board = "d73ap" }, false);
            if (firmware.Version != "18.0") throw new Exception("Binary plist conversion changed firmware metadata.");
            Console.WriteLine("PASS: binary IPSW manifest conversion and compatibility validation");
        }
        finally { Directory.Delete(temp, true); }
    }
}
