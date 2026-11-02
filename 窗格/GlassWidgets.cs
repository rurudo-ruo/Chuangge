using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace 窗格
{
    /// <summary>
    /// The Solid Glass button: a clear-glass pane with a rim along the top and a hairline around it,
    /// or, for the primary action, the solid accent pane. Hover and press use the one neutral state.
    /// </summary>
    internal sealed class GlassButton : Button
    {
        private bool hovered;
        private bool pressed;

        public bool Primary { get; set; }

        public GlassButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (pressed)
            {
                pressed = false;
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            var radius = Math.Max(3, LogicalToDeviceUnits(9));

            if (Parent is GlassPanel || Parent is GlassTableLayoutPanel || Parent is GlassFlowLayoutPanel
                || Parent is CardFaceTableLayoutPanel || Parent is CardFaceFlowLayoutPanel)
            {
                var parentColor = Parent.BackColor;
                if (parentColor.A == 0)
                    parentColor = Parent is CardFaceTableLayoutPanel || Parent is CardFaceFlowLayoutPanel
                        ? FluentTheme.CardSurface
                        : FluentTheme.Canvas;

                graphics.Clear(parentColor);
            }
            else
            {
                graphics.Clear(Parent == null ? FluentTheme.Surface : Parent.BackColor);
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = FenceRenderer.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(FillColor()))
            using (var edge = new Pen(FluentTheme.GlassEdge))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(edge, path);

                var left = bounds.Left + radius;
                var right = bounds.Right - radius;
                if (right > left)
                {
                    if (Primary && Enabled)
                    {
                        using (var rim = new Pen(FluentTheme.PrimaryRim))
                            graphics.DrawLine(rim, left, 1, right, 1);
                        using (var under = new Pen(FluentTheme.PrimaryUnder))
                            graphics.DrawLine(under, left, bounds.Bottom - 1, right, bounds.Bottom - 1);
                    }
                    else if (Enabled)
                    {
                        using (var rim = new Pen(FluentTheme.GlassRim))
                            graphics.DrawLine(rim, left, 1, right, 1);
                    }
                }
            }

            var textColor = !Enabled
                ? FluentTheme.SecondaryText
                : Primary ? FluentTheme.GetContrastingText(FluentTheme.Accent) : FluentTheme.Text;

            TextRenderer.DrawText(graphics, Text, Font,
                new Rectangle(0, 0, Width, Height), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Focused && ShowFocusCues)
            {
                var focus = Rectangle.Inflate(bounds, -LogicalToDeviceUnits(4), -LogicalToDeviceUnits(4));
                ControlPaint.DrawFocusRectangle(graphics, focus, textColor, Color.Transparent);
            }
        }

        private Color FillColor()
        {
            if (!Enabled)
                return FluentTheme.GlassControlSurface;

            if (Primary)
            {
                if (pressed)
                    return Blend(FluentTheme.Accent, Color.Black, 0.10);
                if (hovered)
                    return Blend(FluentTheme.Accent, Color.White, 0.08);
                return FluentTheme.Accent;
            }

            if (pressed)
                return Blend(FluentTheme.GlassControlSurface, FluentTheme.Text, 0.12);
            if (hovered)
                return Blend(FluentTheme.GlassControlSurface, FluentTheme.Text, 0.06);
            return FluentTheme.GlassControlSurface;
        }

        private static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb(
                from.A,
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }
    }
}
