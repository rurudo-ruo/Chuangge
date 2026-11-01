using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace 窗格
{
    /// <summary>
    /// Palette for the app's context menus, in the Solid Glass material: the thickest pane
    /// (near-opaque popover), an edge finish only, and one neutral state for the row under the
    /// pointer.
    /// </summary>
    internal sealed class FluentMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => FluentTheme.PopoverSurface;

        public override Color ImageMarginGradientBegin => FluentTheme.PopoverSurface;

        public override Color ImageMarginGradientMiddle => FluentTheme.PopoverSurface;

        public override Color ImageMarginGradientEnd => FluentTheme.PopoverSurface;

        public override Color MenuBorder => FluentTheme.GlassEdge;

        public override Color MenuItemBorder => Color.Transparent;

        public override Color MenuItemSelected => FluentTheme.GlassState;

        public override Color MenuItemSelectedGradientBegin => FluentTheme.GlassState;

        public override Color MenuItemSelectedGradientEnd => FluentTheme.GlassState;

        public override Color MenuItemPressedGradientBegin => FluentTheme.GlassState;

        public override Color MenuItemPressedGradientEnd => FluentTheme.GlassState;

        public override Color SeparatorDark => FluentTheme.Border;

        public override Color SeparatorLight => FluentTheme.Border;

        public override Color CheckBackground => FluentTheme.GlassState;

        public override Color CheckSelectedBackground => FluentTheme.GlassState;
    }

    /// <summary>
    /// Draws menus with the app palette so a custom accent is honoured; the native renderer only
    /// follows the system accent. The pane is the thickest in the material: nearly opaque, with
    /// the rim, hairlines and shadow that make it glass.
    /// </summary>
    internal sealed class FluentMenuRenderer : ToolStripProfessionalRenderer
    {
        public FluentMenuRenderer()
            : base(new FluentMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(FluentTheme.PopoverSurface))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);

            var radius = Math.Max(4, e.ToolStrip.DeviceDpi * 8 / 96);
            GlassPainter.Paint(e.Graphics, e.AffectedBounds, radius);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // The edge hairline is drawn by the glass finish; a second border would double it.
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            var bounds = new Rectangle(2, 0, Math.Max(1, e.Item.Width - 4), e.Item.Height - 1);
            var radius = Math.Max(3, e.ToolStrip.DeviceDpi * 4 / 96);
            using (var path = FenceRenderer.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(FluentTheme.GlassState))
            using (var edgePen = new Pen(FluentTheme.GlassEdge))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(edgePen, path);

                var left = bounds.Left + radius;
                var right = bounds.Right - radius;
                if (right > left)
                {
                    using (var rimPen = new Pen(FluentTheme.GlassRim))
                        e.Graphics.DrawLine(rimPen, left, bounds.Top + 1, right, bounds.Top + 1);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? FluentTheme.Text : FluentTheme.SecondaryText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                bounds = new Rectangle(4, Math.Max(0, (e.Item.Height - 16) / 2), 16, 16);

            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(FluentTheme.Text, 2f))
            {
                graphics.DrawLines(pen, new[]
                {
                    new PointF(bounds.Left + bounds.Width * 0.20f, bounds.Top + bounds.Height * 0.50f),
                    new PointF(bounds.Left + bounds.Width * 0.42f, bounds.Top + bounds.Height * 0.72f),
                    new PointF(bounds.Left + bounds.Width * 0.80f, bounds.Top + bounds.Height * 0.28f)
                });
            }
        }
    }
}
