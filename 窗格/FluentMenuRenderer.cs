using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using 窗格.Win32;

namespace 窗格
{
    /// <summary>
    /// Palette for the app's context menus, in the Solid Glass material: the thickest pane
    /// (near-opaque popover), an edge finish only, and one neutral state for the row under the
    /// pointer.
    /// </summary>
    internal sealed class FluentMenuColorTable : ProfessionalColorTable
    {
        public bool BackdropBlurEnabled { get; set; }

        private Color Surface => BackdropBlurEnabled
            ? FluentTheme.PopoverBlurSurface
            : FluentTheme.PopoverSurface;

        public override Color ToolStripDropDownBackground => Surface;

        public override Color ImageMarginGradientBegin => Surface;

        public override Color ImageMarginGradientMiddle => Surface;

        public override Color ImageMarginGradientEnd => Surface;

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
        private readonly FluentMenuColorTable colorTable;
        private readonly Dictionary<IntPtr, bool> backdropBlurStates = new Dictionary<IntPtr, bool>();

        public static void Configure(ContextMenuStrip toolStrip)
        {
            if (toolStrip == null)
                throw new ArgumentNullException(nameof(toolStrip));

            toolStrip.RenderMode = ToolStripRenderMode.Professional;
            toolStrip.Renderer = new FluentMenuRenderer();
            toolStrip.Font = FluentTheme.BodyFont;
            toolStrip.ForeColor = FluentTheme.Text;
            toolStrip.Padding = new Padding(6);
            toolStrip.ShowImageMargin = false;
            toolStrip.ShowCheckMargin = true;

            foreach (ToolStripItem item in toolStrip.Items)
            {
                var separator = item as ToolStripSeparator;
                item.Padding = separator == null
                    ? new Padding(12, 6, 12, 6)
                    : Padding.Empty;
                item.Margin = separator == null
                    ? new Padding(2, 1, 2, 1)
                    : new Padding(4, 3, 4, 3);
            }
        }

        public FluentMenuRenderer()
            : this(new FluentMenuColorTable())
        {
        }

        private FluentMenuRenderer(FluentMenuColorTable colorTable)
            : base(colorTable)
        {
            this.colorTable = colorTable;
            RoundedEdges = false;
        }

        public void PrepareBackdrop(ToolStrip toolStrip)
        {
            var handle = toolStrip.Handle;
            bool blurEnabled;
            if (!backdropBlurStates.TryGetValue(handle, out blurEnabled))
            {
                blurEnabled = BlurUtil.EnableBlur(handle);
                backdropBlurStates.Add(handle, blurEnabled);
            }

            colorTable.BackdropBlurEnabled = blurEnabled;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            bool blurEnabled;
            if (!backdropBlurStates.TryGetValue(e.ToolStrip.Handle, out blurEnabled))
            {
                PrepareBackdrop(e.ToolStrip);
                blurEnabled = backdropBlurStates[e.ToolStrip.Handle];
            }

            colorTable.BackdropBlurEnabled = blurEnabled;
            var surface = blurEnabled ? FluentTheme.PopoverBlurSurface : FluentTheme.PopoverSurface;
            using (var brush = new SolidBrush(surface))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);

            var radius = Math.Max(4, e.ToolStrip.DeviceDpi * 8 / 96);
            GlassPainter.Paint(e.Graphics, e.AffectedBounds, radius);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // The edge hairline is drawn by the glass finish; a second border would double it.
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (e.Vertical)
            {
                base.OnRenderSeparator(e);
                return;
            }

            var bounds = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
            var inset = Math.Max(12, e.ToolStrip.DeviceDpi * 12 / 96);
            var left = bounds.Left + inset;
            var right = bounds.Right - inset;
            if (right <= left)
                return;

            var y = bounds.Top + bounds.Height / 2;
            using (var pen = new Pen(Color.FromArgb(FluentTheme.IsDark ? 56 : 38, FluentTheme.Text)))
                e.Graphics.DrawLine(pen, left, y, right, y);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            var bounds = new Rectangle(3, 0, Math.Max(1, e.Item.Width - 6), e.Item.Height - 1);
            var radius = Math.Max(4, e.ToolStrip.DeviceDpi * 5 / 96);
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
