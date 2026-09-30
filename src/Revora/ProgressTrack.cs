using System;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class ProgressTrack : Control
    {
        private readonly Timer animation = new Timer { Interval = 25 };
        private int phase;
        private int? value;
        public int? Value
        {
            get { return value; }
            set {
                this.value = value.HasValue ? (int?)Math.Max(0, Math.Min(100, value.Value)) : null;
                AccessibleDescription = this.value.HasValue ? this.value + "% of the current stage" : "Waiting for device progress";
                animation.Enabled = !this.value.HasValue && Visible;
                Invalidate();
            }
        }
        public ProgressTrack()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.ProgressBar;
            animation.Tick += (s, e) => { phase = (phase + 2) % 100; Invalidate(); };
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); animation.Enabled = !value.HasValue && Visible; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var track = Theme.Rounded(new RectangleF(0, 0, Width, Height), Height / 2F))
            using (var background = new SolidBrush(Theme.Border)) {
                e.Graphics.FillPath(background, track);
                e.Graphics.SetClip(track);
                float length = value.HasValue ? Width * value.Value / 100F : Width / 4F;
                float left = value.HasValue ? 0 : phase / 100F * (Width + length) - length;
                if (length > 0) using (var fill = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(fill, left, 0, length, Height);
                e.Graphics.ResetClip();
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    }
}
