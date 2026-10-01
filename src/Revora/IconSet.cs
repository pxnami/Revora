using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace Revora
{
    internal static class IconSet
    {
        private static readonly Dictionary<string, Image> icons = new Dictionary<string, Image>(StringComparer.Ordinal);
        public static Image Get(string name)
        {
            Image icon;
            if (icons.TryGetValue(name, out icon)) return icon;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Revora.Icons." + name + ".png")) {
                if (stream == null) throw new InvalidOperationException("Missing embedded Icons8 asset: " + name);
                using (var source = Image.FromStream(stream)) icon = new Bitmap(source);
            }
            icons.Add(name, icon);
            return icon;
        }
    }
}
