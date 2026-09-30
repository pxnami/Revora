using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Revora;

internal static class Tests
{
    private const string Normal = "DeviceName: Test phone\nProductType: iPhone15,2\nHardwareModel: D73AP\nProductVersion: 18.0\nUniqueChipID: 123456\n";
    private const string Recovery = "NAME: iPhone 14 Pro\nPRODUCT: iPhone15,2\nMODEL: d73ap\nMODE: Recovery\nECID: 0x000000000001e240\n";
    private const string Manifest = "<?xml version=\"1.0\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist><dict><key>ProductVersion</key><string>18.0</string><key>ProductBuildVersion</key><string>22A3354</string><key>SupportedProductTypes</key><array><string>iPhone15,2</string></array><key>BuildIdentities</key><array><dict><key>Info</key><dict><key>DeviceClass</key><string>d73ap</string><key>RestoreBehavior</key><string>Update</string></dict></dict><dict><key>Info</key><dict><key>DeviceClass</key><string>d73ap</string><key>RestoreBehavior</key><string>Erase</string></dict></dict></array></dict></plist>";
    private static int passed;

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "echo") { foreach (string arg in args.Skip(1)) Console.WriteLine(arg); Console.Error.WriteLine("stderr"); return 0; }
        if (args.Length > 0 && args[0] == "timeout") { System.Threading.Thread.Sleep(10000); return 0; }
        try { RunAsync().GetAwaiter().GetResult(); Console.WriteLine(passed + " checks passed."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static async Task RunAsync()
    {
        var normal = Device.FromProperties(Normal, "test-udid");
        var recovery = Device.FromProperties(Recovery, null);
        Check(normal.Ecid == recovery.Ecid && normal.Board == recovery.Board, "decimal / hexadecimal ECID and hardware normalization");
        Check(normal.Mode == DeviceMode.Normal && recovery.Mode == DeviceMode.Recovery, "normal / recovery mode parsing");
        Check(Device.FromProperties(Recovery.Replace("MODE: Recovery", "MODE: DFU"), null).Mode == DeviceMode.Dfu, "DFU recognition");
        Reject(() => Device.FromProperties(Recovery.Replace("iPhone15,2", "AppleTV6,2"), null), "unsupported product");
        Reject(() => Device.FromProperties(Recovery.Replace("MODE: Recovery", "MODE: Unknown"), null), "unknown recovery mode");
        var firmware = Firmware.Parse(Manifest);
        firmware.Validate(normal, false);
        firmware.Validate(recovery, true);
        Check(firmware.Version == "18.0" && firmware.Build == "22A3354", "firmware metadata and install variants");
        Reject(() => firmware.Validate(new Device { Product = "iPad14,1", Board = "j310ap" }, true), "wrong product firmware");
        Reject(() => firmware.Validate(new Device { Product = normal.Product, Board = "wrongboard" }, true), "wrong board firmware");
        Reject(() => Firmware.Parse(Manifest.Replace("<string>Update</string>", "<string>Erase</string>")).Validate(normal, false), "erase-only firmware cannot update");
        Reject(() => Firmware.Parse("<plist><dict/></plist>"), "missing manifest fields");
        Reject(() => firmware.Validate(new Device { Product = normal.Product }, false), "unknown hardware blocks restore");

        var fake = new FakeRunner();
        fake.Responses.Enqueue(Success("test-udid\n"));
        fake.Responses.Enqueue(Success(Normal));
        fake.Responses.Enqueue(new ToolResult { ExitCode = 255, Output = "ERROR: Unable to connect to device" });
        var service = new DeviceService(fake);
        var found = await service.DiscoverAsync();
        Check(found.Devices.Count == 1 && found.Issues.Count == 0, "normal detection with no recovery device");
        fake.Responses.Enqueue(Success(""));
        await service.EnterRecoveryAsync(normal, null);
        Check(fake.Calls.Last() == "ideviceenterrecovery test-udid", "enter recovery targets UDID");
        fake.Responses.Enqueue(Success(""));
        await service.ExitRecoveryAsync(recovery, null);
        Check(fake.Calls.Last() == "irecovery -i 0x1e240 -n", "exit recovery targets ECID");
        await RejectAsync(() => service.ExitRecoveryAsync(new Device { Ecid = 123456, Mode = DeviceMode.Dfu }, null), "DFU cannot exit using recovery command");

        string temp = Path.Combine(Path.GetTempPath(), "revora-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            string ipsw = Path.Combine(temp, "firmware with spaces.ipsw");
            using (var archive = ZipFile.Open(ipsw, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("BuildManifest.plist").Open())) writer.Write(Manifest);
            var loaded = await Firmware.ReadAsync(ipsw, fake);
            Check(loaded.Path == ipsw, "read manifest directly from IPSW archive");
            fake.Responses.Enqueue(Success(Normal));
            fake.Responses.Enqueue(Success("DONE"));
            await service.RestoreAsync(normal, loaded, false, temp, null);
            Check(fake.Calls.Last() == "idevicerestore -i 0x1e240 -y -P -C " + temp + " " + ipsw, "update restore uses explicit ECID and no erase flag");
            Check(fake.Timeouts.Last() == 0, "restore is not forcibly timed out");
            fake.Responses.Enqueue(Success(Recovery));
            fake.Responses.Enqueue(Success("DONE"));
            await service.RestoreAsync(recovery, loaded, true, temp, null);
            Check(fake.Calls.Last().Contains(" -e "), "erase restore has explicit erase flag");
            int before = fake.Calls.Count;
            fake.Responses.Enqueue(Success(Normal.Replace("123456", "123457")));
            await RejectAsync(() => service.RestoreAsync(normal, loaded, true, temp, null), "device identity change aborts restore");
            Check(fake.Calls.Count == before + 1, "identity mismatch never invokes restore tool");
            await RejectAsync(() => service.RestoreAsync(new Device { Product = normal.Product, Board = normal.Board }, loaded, true, temp, null), "missing ECID blocks restore");
            string bad = Path.Combine(temp, "bad.ipsw");
            using (var archive = ZipFile.Open(bad, ZipArchiveMode.Create)) archive.CreateEntry("not-a-manifest");
            await RejectAsync(() => Firmware.ReadAsync(bad, fake), "invalid IPSW rejected");

            string tools = Path.Combine(temp, "tools");
            Directory.CreateDirectory(tools);
            File.Copy(Process.GetCurrentProcess().MainModule.FileName, Path.Combine(tools, "ideviceinfo.exe"));
            var runner = new ToolRunner(tools, temp);
            string[] arguments = { "", "space in argument", "C:\\path with space\\", "embedded\"quote", "x&echo unsafe", "$(unsafe)" };
            var output = await runner.RunAsync("ideviceinfo", new[] { "echo" }.Concat(arguments), 5, null);
            Check(output.ExitCode == 0 && arguments.All(arg => output.Output.Contains(arg + Environment.NewLine)) && output.Output.Contains("stderr"), "actual process argument quoting and stream draining");
            await RejectAsync(() => runner.RunAsync("ideviceinfo", new[] { "timeout" }, 1, null), "read-only process timeout terminates stalled tool");
            await RejectAsync(() => runner.RunAsync("cmd", new[] { "/c", "echo unsafe" }, 5, null), "only known native tools allowed");
            string invalidTool = Path.Combine(tools, "irecovery.exe");
            File.WriteAllText(invalidTool, "Invalid executable used to test launch errors.");
            bool launchError = false;
            try { await runner.RunAsync("irecovery", new[] { "--help" }, 5, null); }
            catch (Win32Exception e) {
                launchError = e.NativeErrorCode != 0 && e.Message.Contains(invalidTool)
                    && e.Message.Contains("error " + e.NativeErrorCode);
            }
            Check(launchError, "failed tool launch identifies executable and preserves Windows error code");
        }
        finally { Directory.Delete(temp, true); }
    }

    private static ToolResult Success(string output) { return new ToolResult { ExitCode = 0, Output = output }; }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (Exception e) { if (e is InvalidOperationException || e is InvalidDataException) rejected = true; else throw; }
        Check(rejected, name);
    }
    private static async Task RejectAsync(Func<Task> action, string name)
    {
        bool rejected = false;
        try { await action(); } catch (Exception e) { if (e is InvalidOperationException || e is InvalidDataException || e is TimeoutException || e is ArgumentException) rejected = true; else throw; }
        Check(rejected, name);
    }

    private sealed class FakeRunner : IToolRunner
    {
        public readonly Queue<ToolResult> Responses = new Queue<ToolResult>();
        public readonly List<string> Calls = new List<string>();
        public readonly List<int> Timeouts = new List<int>();
        public Task<ToolResult> RunAsync(string tool, IEnumerable<string> args, int timeout, Action<string> output)
        {
            Calls.Add(tool + " " + string.Join(" ", args));
            Timeouts.Add(timeout);
            if (Responses.Count == 0) throw new Exception("Unexpected tool invocation: " + Calls.Last());
            return Task.FromResult(Responses.Dequeue());
        }
    }
}
