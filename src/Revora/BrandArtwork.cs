using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class BrandArtwork : Control
    {
        public bool LogoOnly { get; set; }

        public BrandArtwork()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            ForeColor = Color.Black;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = System.Math.Min(Width / (LogoOnly ? 64F : 360F), Height / (LogoOnly ? 64F : 200F));
            e.Graphics.TranslateTransform((Width - (LogoOnly ? 64 : 360) * scale) / 2F, (Height - (LogoOnly ? 64 : 200) * scale) / 2F);
            e.Graphics.ScaleTransform(scale, scale);
            using (var pen = new Pen(ForeColor, LogoOnly ? 4F : 2F)) {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (LogoOnly) DrawMark(e.Graphics, pen, 0, 0, 1);
                else {
                    e.Graphics.DrawRectangle(pen, 32, 20, 230, 136);
                    e.Graphics.DrawLine(pen, 10, 168, 284, 168);
                    e.Graphics.DrawLine(pen, 10, 168, 32, 156);
                    e.Graphics.DrawLine(pen, 284, 168, 262, 156);
                    using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, 228, 67, 91, 114);
                    e.Graphics.DrawRectangle(pen, 230, 68, 87, 112);
                    using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, 198, 94, 56, 98);
                    e.Graphics.DrawRectangle(pen, 199, 95, 53, 96);
                    e.Graphics.DrawLine(pen, 214, 104, 237, 104);
                    e.Graphics.DrawLine(pen, 218, 181, 233, 181);
                    DrawMark(e.Graphics, pen, 105, 49, 1);
                    e.Graphics.DrawLine(pen, 146, 169, 146, 192);
                    e.Graphics.DrawLine(pen, 140, 188, 152, 188);
                }
            }
        }

        private static void DrawMark(Graphics graphics, Pen pen, float x, float y, float scale)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(x, y);
            graphics.ScaleTransform(scale, scale);
            graphics.DrawArc(pen, 8, 8, 48, 48, 35, 290);
            graphics.DrawBezier(pen, 18, 35, 26, 25, 36, 25, 46, 35);
            graphics.DrawBezier(pen, 18, 43, 26, 33, 36, 33, 46, 43);
            graphics.Restore(state);
        }
    }
}
