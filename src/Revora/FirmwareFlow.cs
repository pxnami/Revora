using System;

namespace Revora
{
    internal enum FirmwareStep { Device, Firmware, InstallationType, Confirmation, Installation, Result }
    internal sealed class FirmwareFlow
    {
        private string deviceIdentity;
        public FirmwareStep Step { get; private set; }
        public Firmware Firmware { get; private set; }
        public bool Erase { get; private set; }
        public bool Acknowledged { get; set; }
        public string Confirmation { get; set; }
        public string Error { get; private set; }
        public bool Succeeded { get; private set; }
        public bool DeviceConnected { get; private set; }
        public bool CanInstall { get { return Step == FirmwareStep.Confirmation && DeviceConnected && Firmware != null && Acknowledged && (!Erase || Confirmation == "ERASE"); } }
        public void ObserveDevice(Device device, bool connected)
        {
            if (Step == FirmwareStep.Installation || Step == FirmwareStep.Result) return;
            DeviceConnected = connected && device != null;
            string identity = device == null ? null : device.Identity;
            if (deviceIdentity != null && identity != deviceIdentity) Reset();
            deviceIdentity = identity;
            if (!DeviceConnected && Step != FirmwareStep.Device) Step = FirmwareStep.Device;
        }
        public void ContinueFromDevice() { if (!DeviceConnected) throw new InvalidOperationException("Connect a device before selecting firmware."); Step = FirmwareStep.Firmware; }
        public void SelectFirmware(Firmware firmware) { Firmware = firmware; Error = ""; }
        public void ContinueFromFirmware()
        {
            if (Step != FirmwareStep.Firmware || Firmware == null || !DeviceConnected) throw new InvalidOperationException("Select firmware for the connected device.");
            Step = FirmwareStep.InstallationType;
        }
        public void SelectType(bool erase) { Erase = erase; Acknowledged = false; Confirmation = ""; }
        public void Review(Device device)
        {
            if (Step != FirmwareStep.InstallationType || Firmware == null || !DeviceConnected) throw new InvalidOperationException("Select an installation type first.");
            if (device == null || device.Identity != deviceIdentity || device.Ecid == 0)
                throw new InvalidOperationException("The selected device changed or did not report an ECID. Select it again before installing.");
            Firmware.Validate(device, Erase); Step = FirmwareStep.Confirmation;
        }
        public void Start() { if (!CanInstall) throw new InvalidOperationException("Confirm the data-loss acknowledgement before starting."); Step = FirmwareStep.Installation; }
        public void Complete(string error) { Error = error ?? ""; Succeeded = Error.Length == 0; Step = FirmwareStep.Result; }
        public void Back() { if (Step > FirmwareStep.Device && Step < FirmwareStep.Installation) Step = (FirmwareStep)((int)Step - 1); Acknowledged = false; Confirmation = ""; }
        public void Reset() { Step = FirmwareStep.Device; Firmware = null; Acknowledged = false; Confirmation = ""; Error = ""; Succeeded = false; Erase = false; }
    }
}
