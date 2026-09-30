using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class DeviceVisual : Control
    {
        private Device device;
        public Device Device { get { return device; } set { if (device == value) return; device = value; AccessibleName = value == null ? "Device and USB cable" : value.IsIpad ? "iPad" : "iPhone"; Invalidate(); } }
        public DeviceVisual() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); BackColor = Theme.Background; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 1 || Height < 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = System.Math.Min(Width / 260F, Height / 200F);
            e.Graphics.TranslateTransform((Width - 260 * scale) / 2F, (Height - 200 * scale) / 2F);
            e.Graphics.ScaleTransform(scale, scale);
            bool ipad = device == null || device.IsIpad;
            var body = new RectangleF(ipad ? 58 : 88, 6, ipad ? 144 : 84, 176);
            using (var path = Theme.Rounded(body, 13))
            using (var fill = new SolidBrush(Color.FromArgb(54, 59, 68))) e.Graphics.FillPath(fill, path);
            var screen = RectangleF.Inflate(body, -7, -9);
            using (var path = Theme.Rounded(screen, 7))
            using (var fill = new SolidBrush(Color.FromArgb(232, 237, 244))) e.Graphics.FillPath(fill, path);
            using (var accent = new Pen(Color.FromArgb(197, 209, 228), 22)) e.Graphics.DrawLine(accent, screen.Left + 14, screen.Top + 88, screen.Right - 14, screen.Top + 54);
            using (var camera = new SolidBrush(Color.FromArgb(125, 133, 146))) e.Graphics.FillEllipse(camera, body.X + body.Width / 2 - 1.5F, body.Y + 2, 3, 3);
            using (var pen = new Pen(Theme.Muted, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round }) {
                if (device == null || device.Mode != DeviceMode.Normal) {
                    e.Graphics.DrawLine(pen, 130, 182, 130, 195);
                    e.Graphics.DrawRectangle(pen, 124, 190, 12, 8);
                }
                else e.Graphics.DrawLine(pen, body.X + body.Width / 2 - 14, body.Bottom - 5, body.X + body.Width / 2 + 14, body.Bottom - 5);
            }
        }
    }
}
