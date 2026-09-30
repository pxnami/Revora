using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Revora
{
    public sealed class Discovery
    {
        public List<Device> Devices { get; private set; }
        public List<string> Issues { get; private set; }
        public Discovery() { Devices = new List<Device>(); Issues = new List<string>(); }
    }

    public sealed class DeviceService
    {
        private readonly IToolRunner runner;
        public DeviceService(IToolRunner runner) { this.runner = runner; }

        public async Task<Discovery> DiscoverAsync()
        {
            var discovery = new Discovery();
            var normal = await DetectAsync("idevice_id", new[] { "-l" }, discovery);
            if (normal != null && normal.ExitCode == 0) {
                foreach (string udid in normal.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())) {
                    var result = await DetectAsync("ideviceinfo", new[] { "-u", udid }, discovery);
                    if (result == null) continue;
                    if (result.ExitCode != 0) { discovery.Issues.Add("Cannot read device " + udid + ". Unlock it and accept Trust This Computer.\n" + result.Output); continue; }
                    try { discovery.Devices.Add(Device.FromProperties(result.Output, udid)); }
                    catch (InvalidOperationException e) { discovery.Issues.Add(e.Message); }
                }
            }
            else if (normal != null) discovery.Issues.Add("Normal-mode detection failed. Check Apple Devices and its USB drivers.\n" + normal.Output);
            var recovery = await DetectAsync("irecovery", new[] { "-q" }, discovery);
            if (recovery != null && recovery.ExitCode == 0) {
                try { discovery.Devices.Add(Device.FromProperties(recovery.Output, null)); }
                catch (InvalidOperationException e) { discovery.Issues.Add(e.Message); }
            }
            else if (recovery != null && recovery.Output.IndexOf("Unable to connect to device", StringComparison.OrdinalIgnoreCase) < 0)
                discovery.Issues.Add("Recovery-mode detection failed.\n" + recovery.Output);
            return discovery;
        }

        private async Task<ToolResult> DetectAsync(string tool, string[] args, Discovery discovery)
        {
            try { return await runner.RunAsync(tool, args, 20, null); }
            catch (Exception e) {
                if (!ToolRunner.IsExpectedFailure(e)) throw;
                discovery.Issues.Add(e.Message);
                return null;
            }
        }

        public async Task EnterRecoveryAsync(Device device, Action<string> output)
        {
            if (device.Mode != DeviceMode.Normal || string.IsNullOrEmpty(device.Udid)) throw new InvalidOperationException("Select a device in normal mode.");
            var result = await runner.RunAsync("ideviceenterrecovery", new[] { device.Udid }, 30, output);
            result.EnsureSuccess("Entering recovery mode");
        }

        public async Task ExitRecoveryAsync(Device device, Action<string> output)
        {
            if (device.Mode != DeviceMode.Recovery || device.Ecid == 0) throw new InvalidOperationException("Select a recovery-mode device with a known ECID. DFU requires a manual force restart.");
            var result = await runner.RunAsync("irecovery", new[] { "-i", device.EcidArgument, "-n" }, 30, output);
            result.EnsureSuccess("Exiting recovery mode");
        }

        public async Task RestoreAsync(Device device, Firmware firmware, bool erase, string cachePath, Action<string> output)
        {
            firmware.Validate(device, erase);
            if (device.Ecid == 0) throw new InvalidOperationException("A known device ECID is required to target a restore safely.");
            using (var firmwareLock = new FileStream(firmware.Path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var checkedFirmware = await Firmware.ReadAsync(firmware.Path, runner);
                checkedFirmware.Validate(device, erase);
                if (checkedFirmware.Version != firmware.Version || checkedFirmware.Build != firmware.Build)
                    throw new InvalidOperationException("The selected firmware file has changed. Choose it again before installing.");
                Device current = await ReadSelectedAsync(device);
                if (current.Ecid != device.Ecid || current.Product != device.Product || !string.Equals(current.Board, device.Board, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The connected device has changed. Refresh and select it again.");
                firmware.Validate(current, erase);
                var args = new List<string> { "-i", device.EcidArgument, "-y", "-P", "-C", cachePath };
                if (erase) args.Add("-e");
                args.Add(firmware.Path);
                var result = await runner.RunAsync("idevicerestore", args, 0, output);
                result.EnsureSuccess("Firmware restore");
            }
        }

        private async Task<Device> ReadSelectedAsync(Device device)
        {
            bool normal = device.Mode == DeviceMode.Normal;
            var result = await runner.RunAsync(normal ? "ideviceinfo" : "irecovery",
                normal ? new[] { "-u", device.Udid } : new[] { "-i", device.EcidArgument, "-q" }, 20, null);
            result.EnsureSuccess("Checking the selected device");
            return Device.FromProperties(result.Output, normal ? device.Udid : null);
        }
    }
}
