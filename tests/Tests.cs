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
        await MonitorChecksAsync();
        var normal = Device.FromProperties(Normal, "test-udid");
        var recovery = Device.FromProperties(Recovery, null);
        Check(normal.Ecid == recovery.Ecid && normal.Board == recovery.Board, "decimal / hexadecimal ECID and hardware normalization");
        Check(normal.Mode == DeviceMode.Normal && recovery.Mode == DeviceMode.Recovery, "normal / recovery mode parsing");
        var information = Device.FromProperties(Normal + "SerialNumber: SAMPLE123\nBuildVersion: 22A3354\nActivationState: Activated\nWiFiAddress: 00:11:22:33:44:55\n", "test-udid");
        Check(information.SerialNumber == "SAMPLE123" && information.BuildVersion == "22A3354"
            && information.ActivationState == "Activated" && information.WifiAddress == "00:11:22:33:44:55", "reported device information fields");
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
    private static Discovery Found(string properties, string udid)
    {
        var found = new Discovery();
        if (properties != null) found.Devices.Add(Device.FromProperties(properties, udid));
        return found;
    }

    private static async Task MonitorChecksAsync()
    {
        Discovery found = Found(null, null);
        bool fail = false;
        using (var monitor = new DeviceMonitor(() => {
            if (fail) throw new Win32Exception(1260, "Blocked test tool");
            return Task.FromResult(found);
        })) {
            int changes = 0;
            monitor.Changed += (s, e) => changes++;
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Connection == ConnectionState.Disconnected && monitor.Snapshot.CurrentDevice == null, "empty scan clears connected context");
            found = Found(Normal, "test-udid");
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Connection == ConnectionState.Connected && monitor.Snapshot.CurrentDevice.Name == "Test phone", "monitor publishes real connected context");
            int before = changes;
            for (int i = 0; i < 5; i++) { found = Found(Normal, "test-udid"); await monitor.RefreshAsync(); }
            Check(changes == before && monitor.Snapshot.Connection == ConnectionState.Connected, "equivalent polls do not publish scanning or rerender events");
            found = Found(Recovery, null);
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.CurrentDevice.Mode == DeviceMode.Recovery && monitor.Snapshot.CurrentDevice.Name == "Test phone"
                && monitor.Snapshot.CurrentDevice.Udid == "test-udid" && monitor.Snapshot.CurrentDevice.Version == "18.0" && monitor.Snapshot.CurrentDevice.InformationIsCached,
                "normal-to-recovery retains labeled device context by ECID");
            before = changes;
            found = Found(Recovery, null);
            await monitor.RefreshAsync();
            Check(changes == before, "cached recovery polls remain visually stable");
            found = Found(Recovery.Replace("MODE: Recovery", "MODE: DFU"), null);
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.CurrentDevice.Mode == DeviceMode.Dfu && monitor.Snapshot.CurrentDevice.Name == "Test phone", "DFU transition retains identity");
            found = Found(Normal, "test-udid");
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.CurrentDevice.Mode == DeviceMode.Normal && !monitor.Snapshot.CurrentDevice.InformationIsCached, "normal reconnect replaces cached properties");
            fail = true;
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Connection == ConnectionState.Error && monitor.Snapshot.CurrentDevice != null, "launch failure retains context without claiming connection");
            fail = false;
            found = Found(null, null);
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Connection == ConnectionState.Disconnected && monitor.Snapshot.Devices.Count == 0, "disconnect clears selected device");
            found = Found(Recovery, null);
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.CurrentDevice.Name == "Test phone" && monitor.Snapshot.CurrentDevice.InformationIsCached, "recovery after USB re-enumeration retains labeled normal-mode context");
            found = Found(Normal, "test-udid");
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Connection == ConnectionState.Connected, "monitor recovers after errors and reconnects");
            found.Devices.Add(Device.FromProperties(Normal.Replace("123456", "123457").Replace("Test phone", "Second phone"), "second-udid"));
            await monitor.RefreshAsync();
            monitor.Select("123457");
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.CurrentDevice.Name == "Second phone", "selection persists across multiple-device scans");
            found.Devices.Add(Device.FromProperties(Recovery.Replace("123456", "123457").Replace("1e240", "1e241"), null));
            await monitor.RefreshAsync();
            Check(monitor.Snapshot.Devices.Count == 2, "transition scan deduplicates the same ECID");
            var flow = new FirmwareFlow();
            var device = Device.FromProperties(Normal, "test-udid");
            flow.ObserveDevice(device, true);
            flow.ContinueFromDevice();
            flow.SelectFirmware(Firmware.Parse(Manifest));
            flow.ContinueFromFirmware();
            flow.SelectType(true);
            flow.Review(device);
            Reject(flow.Start, "workflow blocks erase without acknowledgement");
            flow.Acknowledged = true; flow.Confirmation = "erase";
            Check(!flow.CanInstall, "workflow requires case-sensitive ERASE");
            flow.Confirmation = "ERASE";
            Check(flow.CanInstall, "workflow allows acknowledged erase");
            flow.Back();
            Check(!flow.Acknowledged && flow.Confirmation == "" && !flow.CanInstall, "back navigation clears destructive confirmation");
            flow.Review(device); flow.Acknowledged = true; flow.Confirmation = "ERASE";
            flow.ObserveDevice(Device.FromProperties(Normal.Replace("123456", "123457"), "other"), true);
            Check(flow.Step == FirmwareStep.Device && flow.Firmware == null && !flow.CanInstall, "device swap discards firmware and confirmation");
            flow.ObserveDevice(device, true);
            flow.ContinueFromDevice(); flow.SelectFirmware(Firmware.Parse(Manifest)); flow.ContinueFromFirmware();
            flow.SelectType(false); flow.Review(device); flow.Acknowledged = true; flow.Start();
            flow.ObserveDevice(null, false);
            Check(flow.Step == FirmwareStep.Installation, "disconnect during install preserves operation view");
            flow.Complete("USB connection lost");
            Check(flow.Step == FirmwareStep.Result && !flow.Succeeded && flow.Error == "USB connection lost", "failed backend result never claims success");
        }
        using (var recoveryOnly = new DeviceMonitor(() => Task.FromResult(Found(Recovery, null)))) {
            await recoveryOnly.RefreshAsync();
            int recoveryChanges = 0;
            recoveryOnly.Changed += (s, e) => recoveryChanges++;
            await recoveryOnly.RefreshAsync();
            Check(recoveryChanges == 0 && !recoveryOnly.Snapshot.CurrentDevice.InformationIsCached, "recovery-only scans do not invent cached normal-mode data");
        }
        int calls = 0;
        var pending = new TaskCompletionSource<Discovery>();
        using (var monitor = new DeviceMonitor(() => { calls++; return pending.Task; })) {
            var scan = monitor.RefreshAsync();
            await monitor.RefreshAsync();
            var pause = monitor.SuspendAsync();
            await monitor.RefreshAsync();
            Check(calls == 1 && !pause.IsCompleted, "manual refresh and operation pause cannot overlap scans");
            pending.SetResult(Found(Normal, "test-udid"));
            await scan; await pause;
            await monitor.RefreshAsync();
            Check(calls == 1, "suspended monitor leaves USB ownership with operation");
            monitor.Resume();
            await monitor.RefreshAsync();
            Check(calls == 2, "monitor resumes after operation");
        }
        pending = new TaskCompletionSource<Discovery>();
        var disposed = new DeviceMonitor(() => pending.Task);
        var lastScan = disposed.RefreshAsync();
        int notifications = 0;
        disposed.Changed += (s, e) => notifications++;
        disposed.Dispose();
        pending.SetResult(Found(Normal, "test-udid"));
        await lastScan;
        Check(notifications == 0, "disposed monitor ignores late scan completion");
    }
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
