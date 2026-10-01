using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class BrandArtwork : Control
    {
        public BrandArtwork()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            ForeColor = Theme.Ink;
            BackColor = Theme.Sidebar;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width == 0 || Height == 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = System.Math.Min(Width, Height) / 64F;
            e.Graphics.TranslateTransform((Width - 64 * scale) / 2, (Height - 64 * scale) / 2);
            e.Graphics.ScaleTransform(scale, scale);
            using (var pen = new Pen(ForeColor, 4F) { StartCap = LineCap.Round, EndCap = LineCap.Round }) {
                e.Graphics.DrawArc(pen, 8, 8, 48, 48, 35, 290);
                e.Graphics.DrawBezier(pen, 18, 35, 26, 25, 36, 25, 46, 35);
                e.Graphics.DrawBezier(pen, 18, 43, 26, 33, 36, 33, 46, 43);
            }
        }
    }
}
