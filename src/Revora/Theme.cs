using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Revora
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(249, 250, 252);
        public static readonly Color Sidebar = Color.FromArgb(239, 241, 245);
        public static readonly Color Surface = Color.White;
        public static readonly Color Ink = Color.FromArgb(35, 39, 47);
        public static readonly Color Muted = Color.FromArgb(102, 110, 121);
        public static readonly Color Border = Color.FromArgb(224, 228, 234);
        public static readonly Color Accent = Color.FromArgb(54, 103, 169);
        public static readonly Color Success = Color.FromArgb(50, 126, 89);
        public static readonly Color Danger = Color.FromArgb(183, 62, 67);
        private static readonly string FontName = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";

        public static Font Font(float size, FontStyle style = FontStyle.Regular) { return new Font(FontName, size, style); }
        public static int Px(Control control, int value) { return (int)System.Math.Round(value * control.DeviceDpi / 96F); }
        public static GraphicsPath Rounded(RectangleF rectangle, float radius)
        {
            float diameter = System.Math.Min(radius * 2, System.Math.Min(rectangle.Width, rectangle.Height));
            var path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
