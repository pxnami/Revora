using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Revora
{
    public sealed class Firmware
    {
        public string Path { get; private set; }
        public string Version { get; private set; }
        public string Build { get; private set; }
        public string[] Products { get; private set; }
        private XElement identities;

        public static async Task<Firmware> ReadAsync(string path, IToolRunner runner)
        {
            if (!string.Equals(System.IO.Path.GetExtension(path), ".ipsw", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose an IPSW firmware file.");
            byte[] manifest = await Task.Run(() => {
                using (var archive = ZipFile.OpenRead(path)) {
                    var entry = archive.GetEntry("BuildManifest.plist");
                    if (entry == null || entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("The IPSW has no valid BuildManifest.plist.");
                    using (var input = entry.Open())
                    using (var memory = new MemoryStream()) { input.CopyTo(memory); return memory.ToArray(); }
                }
            });
            string xml;
            if (manifest.Length >= 8 && Encoding.ASCII.GetString(manifest, 0, 8) == "bplist00") {
                string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revora-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try {
                    string input = System.IO.Path.Combine(temp, "manifest.plist");
                    string output = System.IO.Path.Combine(temp, "manifest.xml");
                    File.WriteAllBytes(input, manifest);
                    var converted = await runner.RunAsync("plistutil", new[] { "-i", input, "-o", output, "-f", "xml" }, 30, null);
                    converted.EnsureSuccess("Reading firmware manifest");
                    xml = File.ReadAllText(output);
                }
                finally { Directory.Delete(temp, true); }
            }
            else xml = Encoding.UTF8.GetString(manifest).TrimStart('\uFEFF');
            var firmware = Parse(xml);
            firmware.Path = System.IO.Path.GetFullPath(path);
            return firmware;
        }

        public static Firmware Parse(string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024
            })) {
                var root = XDocument.Load(reader).Root;
                var dict = root == null ? null : root.Element("dict");
                var products = Value(dict, "SupportedProductTypes");
                var identities = Value(dict, "BuildIdentities");
                if (products == null || identities == null || !identities.Elements("dict").Any())
                    throw new InvalidDataException("The firmware manifest is missing product or restore information.");
                return new Firmware {
                    Version = Text(dict, "ProductVersion"), Build = Text(dict, "ProductBuildVersion"),
                    Products = products.Elements("string").Select(e => e.Value).ToArray(), identities = identities
                };
            }
        }

        public void Validate(Device device, bool erase)
        {
            if (device == null || string.IsNullOrEmpty(device.Product) || string.IsNullOrEmpty(device.Board))
                throw new InvalidOperationException("Refresh device information before restoring. The product and hardware model are required.");
            if (!Products.Contains(device.Product)) throw new InvalidOperationException("This IPSW does not support " + device.Product + ". Choose firmware for your device.");
            string behavior = erase ? "Erase" : "Update";
            bool compatible = identities.Elements("dict").Any(identity => {
                var info = Value(identity, "Info");
                return string.Equals(Text(info, "DeviceClass"), device.Board, StringComparison.OrdinalIgnoreCase)
                    && Text(info, "RestoreBehavior") == behavior;
            });
            if (!compatible) throw new InvalidOperationException("This IPSW does not contain a compatible " + behavior.ToLowerInvariant() + " install for " + device.Board + "." + (erase ? "" : " An erase restore may be available, but deletes all device data."));
        }

        private static XElement Value(XElement dict, string key)
        {
            if (dict == null) return null;
            var entry = dict.Elements("key").FirstOrDefault(e => e.Value == key);
            return entry == null ? null : entry.ElementsAfterSelf().FirstOrDefault();
        }
        private static string Text(XElement dict, string key) { var value = Value(dict, key); return value == null ? "" : value.Value; }
    }
}
