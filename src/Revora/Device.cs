using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Revora
{
    public enum DeviceMode { Normal, Recovery, Dfu }

    public sealed class Device
    {
        public string Udid { get; set; }
        public ulong Ecid { get; set; }
        public string Name { get; set; }
        public string Product { get; set; }
        public string Board { get; set; }
        public string Version { get; set; }
        public DeviceMode Mode { get; set; }
        public string SerialNumber { get; set; }
        public string BuildVersion { get; set; }
        public string ActivationState { get; set; }
        public string WifiAddress { get; set; }
        public bool InformationIsCached { get; private set; }
        public bool IsIpad { get { return Product != null && Product.StartsWith("iPad", StringComparison.Ordinal); } }
        public string PlatformName { get { return IsIpad ? "iPadOS" : "iOS"; } }
        public string ModeLabel { get { return Mode == DeviceMode.Dfu ? "DFU Mode" : Mode == DeviceMode.Recovery ? "Recovery Mode" : "Normal Mode"; } }
        public string Identity { get { return Ecid != 0 ? Ecid.ToString(CultureInfo.InvariantCulture) : Udid; } }
        public string EcidArgument { get { return "0x" + Ecid.ToString("x", CultureInfo.InvariantCulture); } }
        public override string ToString() { return Name + " · " + Product + " · " + Mode; }

        internal bool SameInformation(Device other)
        {
            return Udid == other.Udid && Ecid == other.Ecid && Name == other.Name && Product == other.Product
                && Board == other.Board && Version == other.Version && Mode == other.Mode
                && SerialNumber == other.SerialNumber && BuildVersion == other.BuildVersion
                && ActivationState == other.ActivationState && WifiAddress == other.WifiAddress
                && InformationIsCached == other.InformationIsCached;
        }

        internal void PreserveContext(Device previous)
        {
            InformationIsCached = true;
            Name = previous.Name;
            if (string.IsNullOrEmpty(Udid)) Udid = previous.Udid;
            if (Version == "Unavailable in recovery mode") Version = previous.Version;
            if (string.IsNullOrEmpty(BuildVersion)) BuildVersion = previous.BuildVersion;
            if (string.IsNullOrEmpty(SerialNumber)) SerialNumber = previous.SerialNumber;
            if (string.IsNullOrEmpty(ActivationState)) ActivationState = previous.ActivationState;
            if (string.IsNullOrEmpty(WifiAddress)) WifiAddress = previous.WifiAddress;
        }

        public static Dictionary<string, string> ParseProperties(string output)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int split = line.IndexOf(':');
                if (split > 0) values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
            return values;
        }

        public static Device FromProperties(string output, string udid)
        {
            var values = ParseProperties(output);
            bool normal = !string.IsNullOrEmpty(udid);
            string product = Get(values, normal ? "ProductType" : "PRODUCT");
            if (!product.StartsWith("iPhone", StringComparison.Ordinal) && !product.StartsWith("iPad", StringComparison.Ordinal))
                throw new InvalidOperationException("The connected device is not a recognized iPhone or iPad.");
            string ecidText = Get(values, normal ? "UniqueChipID" : "ECID");
            ulong ecid = 0;
            if (ecidText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                ulong.TryParse(ecidText.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ecid);
            else ulong.TryParse(ecidText, NumberStyles.None, CultureInfo.InvariantCulture, out ecid);
            string mode = Get(values, "MODE");
            if (!normal && mode != "Recovery" && mode != "DFU")
                throw new InvalidOperationException("This recovery device mode is not supported by Revora.");
            return new Device {
                Udid = udid, Ecid = ecid, Product = product,
                Name = Get(values, normal ? "DeviceName" : "NAME", product),
                Board = Get(values, normal ? "HardwareModel" : "MODEL").ToLowerInvariant(),
                Version = Get(values, "ProductVersion", normal ? "Not reported" : "Unavailable in recovery mode"),
                SerialNumber = Get(values, normal ? "SerialNumber" : "SRNM"),
                BuildVersion = Get(values, "BuildVersion"),
                ActivationState = Get(values, "ActivationState"),
                WifiAddress = Get(values, "WiFiAddress"),
                Mode = normal ? DeviceMode.Normal : mode == "Recovery" ? DeviceMode.Recovery : DeviceMode.Dfu
            };
        }

        private static string Get(Dictionary<string, string> values, string key, string fallback = "")
        {
            string value;
            return values.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }
    }
}
