using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    internal sealed class StatusIndicator : Control
    {
        private Color dot = Theme.Muted;
        private readonly Timer spinner = new Timer { Interval = 80 };
        private bool busy;
        private int angle;
        public StatusIndicator()
        {
            Font = Theme.Font(10F); ForeColor = Theme.Ink; DoubleBuffered = true;
            spinner.Tick += (s, e) => { angle = (angle + 30) % 360; Invalidate(); };
        }
        public void Set(ConnectionState connection, Device device, string operation = null)
        {
            busy = operation != null || connection == ConnectionState.Scanning;
            Text = operation ?? (connection == ConnectionState.Connected ? device.Mode == DeviceMode.Normal ? "Connected · Normal Mode" : device.ModeLabel
                : connection == ConnectionState.Error ? "Detection unavailable"
                : connection == ConnectionState.Disconnected ? "Waiting for a device" : "Checking for devices…");
            dot = connection == ConnectionState.Connected ? device.Mode == DeviceMode.Normal ? Theme.Success : Theme.Accent : Theme.Muted;
            AccessibleName = Text;
            spinner.Enabled = busy && Visible;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int size = Theme.Px(this, busy ? 12 : 7);
            int textWidth = TextRenderer.MeasureText(Text, Font).Width;
            int left = System.Math.Max(0, (Width - textWidth - size - Theme.Px(this, 8)) / 2);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (busy) using (var pen = new Pen(Theme.Accent, Theme.Px(this, 2))) e.Graphics.DrawArc(pen, left, (Height - size) / 2, size, size, angle, 260);
            else using (var brush = new SolidBrush(dot)) e.Graphics.FillEllipse(brush, left, (Height - size) / 2, size, size);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(left + size + Theme.Px(this, 8), 0, Width - left - size - Theme.Px(this, 8), Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void OnVisibleChanged(System.EventArgs e) { base.OnVisibleChanged(e); spinner.Enabled = busy && Visible; }
        protected override void Dispose(bool disposing) { if (disposing) spinner.Dispose(); base.Dispose(disposing); }
    }
}
