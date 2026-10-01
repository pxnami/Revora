using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Revora
{
    public enum ConnectionState { Initializing, Scanning, Connected, Disconnected, Error }

    public sealed class DeviceSnapshot
    {
        public ConnectionState Connection { get; private set; }
        public ReadOnlyCollection<Device> Devices { get; private set; }
        public Device CurrentDevice { get; private set; }
        public string Error { get; private set; }

        internal DeviceSnapshot(ConnectionState connection, IEnumerable<Device> devices, string selected, string error)
        {
            Connection = connection;
            Devices = new List<Device>(devices).AsReadOnly();
            CurrentDevice = Devices.FirstOrDefault(d => d.Identity == selected) ?? Devices.FirstOrDefault();
            Error = error ?? "";
        }

        internal bool EquivalentTo(DeviceSnapshot other)
        {
            return Connection == other.Connection && Error == other.Error
                && (CurrentDevice == null ? null : CurrentDevice.Identity) == (other.CurrentDevice == null ? null : other.CurrentDevice.Identity)
                && Devices.Count == other.Devices.Count
                && Devices.Zip(other.Devices, (a, b) => a.SameInformation(b)).All(equal => equal);
        }
    }

    public sealed class DeviceMonitor : IDisposable
    {
        private Func<Task<Discovery>> discover;
        private readonly SemaphoreSlim scanGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<ulong, Device> normalContext = new Dictionary<ulong, Device>();
        private readonly System.Windows.Forms.Timer timer;
        private bool suspended;
        private bool disposed;
        private bool started;
        public DeviceSnapshot Snapshot { get; private set; }
        public event EventHandler Changed;
        public bool IsScanning { get; private set; }

        public DeviceMonitor(Func<Task<Discovery>> discover)
        {
            this.discover = discover;
            Snapshot = new DeviceSnapshot(ConnectionState.Initializing, new Device[0], null, null);
            timer = new System.Windows.Forms.Timer { Interval = 10000 };
            timer.Tick += async (s, e) => await RefreshAsync();
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException("DeviceMonitor");
            if (started) return;
            started = true;
            timer.Start();
        }

        public async Task RefreshAsync()
        {
            if (disposed || suspended || !await scanGate.WaitAsync(0)) return;
            try {
                if (disposed || suspended) return;
                IsScanning = true;
                if (Snapshot.Connection == ConnectionState.Initializing)
                    Publish(new DeviceSnapshot(ConnectionState.Scanning, Snapshot.Devices, null, null));
                Discovery found;
                try { found = await discover(); }
                catch (Exception failure) {
                    if (!ToolRunner.IsExpectedFailure(failure)) throw;
                    if (!disposed) Publish(new DeviceSnapshot(ConnectionState.Error, Snapshot.Devices,
                        Snapshot.CurrentDevice == null ? null : Snapshot.CurrentDevice.Identity, failure.Message));
                    return;
                }
                if (disposed) return;
                foreach (var device in found.Devices.Where(d => d.Mode == DeviceMode.Normal && d.Ecid != 0)) normalContext[device.Ecid] = device;
                var devices = found.Devices.GroupBy(d => d.Identity).Select(group => group.Last()).OrderBy(d => d.Identity, StringComparer.Ordinal).ToArray();
                foreach (var device in devices) {
                    Device previous;
                    if (device.Mode != DeviceMode.Normal && device.Ecid != 0 && normalContext.TryGetValue(device.Ecid, out previous)) device.PreserveContext(previous);
                }
                string selected = Snapshot.CurrentDevice == null ? null : Snapshot.CurrentDevice.Identity;
                string error = string.Join("\n", found.Issues.Distinct());
                var connection = devices.Length > 0 ? ConnectionState.Connected : error.Length > 0 ? ConnectionState.Error : ConnectionState.Disconnected;
                Publish(new DeviceSnapshot(connection, connection == ConnectionState.Error ? (IEnumerable<Device>)Snapshot.Devices : devices, selected, error));
            }
            finally { IsScanning = false; scanGate.Release(); }
        }

        public async Task SuspendAsync()
        {
            suspended = true;
            await scanGate.WaitAsync();
            scanGate.Release();
        }

        public void Resume() { if (!disposed) suspended = false; }

        public void SetSource(Func<Task<Discovery>> source)
        {
            if (!suspended) throw new InvalidOperationException("Pause monitoring before changing device tools.");
            discover = source;
        }

        public void Select(string identity)
        {
            if (!Snapshot.Devices.Any(d => d.Identity == identity)) throw new ArgumentException("Device is not connected.", "identity");
            Publish(new DeviceSnapshot(Snapshot.Connection, Snapshot.Devices, identity, Snapshot.Error));
        }

        private void Publish(DeviceSnapshot next)
        {
            if (Snapshot.EquivalentTo(next)) return;
            Snapshot = next;
            var changed = Changed;
            if (changed != null) changed(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            disposed = true;
            timer.Stop();
            timer.Dispose();
            normalContext.Clear();
        }
    }
}
