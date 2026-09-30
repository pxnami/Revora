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
        public string Identity { get { return Ecid != 0 ? Ecid.ToString(CultureInfo.InvariantCulture) : Udid; } }
        public string EcidArgument { get { return "0x" + Ecid.ToString("x", CultureInfo.InvariantCulture); } }
        public override string ToString() { return Name + " · " + Product + " · " + Mode; }

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
                Version = Get(values, "ProductVersion", "Unavailable in recovery mode"),
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
