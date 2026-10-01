using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Revora
{
    internal enum ButtonKind { Secondary, Primary, Danger, Navigation, Quiet }

    internal sealed class RevoraButton : Button
    {
        private readonly Timer animation = new Timer { Interval = 16 };
        private bool hover;
        private bool pressed;
        private float highlight;
        private bool selected;
        public ButtonKind Kind { get; set; }
        public string IconName { get; set; }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        public RevoraButton(string text, ButtonKind kind = ButtonKind.Secondary, string iconName = null)
        {
            Text = text;
            AccessibleName = text;
            AccessibleRole = AccessibleRole.PushButton;
            Kind = kind;
            IconName = iconName;
            Font = Theme.Font(10F);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Surface;
            ForeColor = Theme.Ink;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Height = 40;
            Width = Math.Max(100, (int)Math.Ceiling(TextRenderer.MeasureText(text, Font).Width * 96F / DeviceDpi) + (iconName == null ? 32 : 58));
            Margin = new Padding(0, 0, 8, 0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            animation.Tick += (s, e) => {
                highlight = Math.Max(0, Math.Min(1, highlight + (hover && Enabled ? .1F : -.1F)));
                Invalidate();
                if (highlight == 0 || highlight == 1) animation.Stop();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; animation.Start(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; animation.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } }
        protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); pressed = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) { animation.Stop(); highlight = 0; } Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height < 3) return;
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool accent = Kind == ButtonKind.Primary || Kind == ButtonKind.Danger;
            Color fill = accent ? Kind == ButtonKind.Danger ? Theme.Danger : Theme.Accent : Theme.Surface;
            Color foreground = accent ? Color.White : Theme.Ink;
            if (Kind == ButtonKind.Navigation || Kind == ButtonKind.Quiet) fill = Selected ? Color.FromArgb(221, 227, 236) : Parent == null ? Theme.Background : Parent.BackColor;
            if (!Enabled) { fill = Color.FromArgb(236, 239, 243); foreground = Color.FromArgb(133, 140, 150); }
            else if (pressed) fill = ControlPaint.Dark(fill, .06F);
            else if (highlight > 0) fill = Color.FromArgb(Math.Max(0, fill.R - (int)(highlight * 9)), Math.Max(0, fill.G - (int)(highlight * 9)), Math.Max(0, fill.B - (int)(highlight * 9)));
            var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
            using (var path = Theme.Rounded(bounds, Theme.Px(this, 8)))
            using (var brush = new SolidBrush(fill)) {
                e.Graphics.FillPath(brush, path);
                if (Kind == ButtonKind.Secondary) using (var border = new Pen(Theme.Border)) e.Graphics.DrawPath(border, path);
                if (Focused && ShowFocusCues) using (var focus = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dot }) e.Graphics.DrawPath(focus, path);
            }
            int iconSize = Theme.Px(this, 18);
            int left = Kind == ButtonKind.Navigation ? Theme.Px(this, 12) : Theme.Px(this, 16);
            if (IconName != null) {
                e.Graphics.DrawImage(IconSet.Get(IconName), new Rectangle(left, (Height - iconSize) / 2, iconSize, iconSize));
                left += iconSize + Theme.Px(this, 10);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(left, 0, Width - left - Theme.Px(this, 12), Height), foreground,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (Kind == ButtonKind.Navigation || IconName != null ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
        }

        protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    }
}
