using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace 窗格
{
    internal class SettingsSectionControl : UserControl
    {
        private const int CornerRadius = 14;

        public SettingsSectionControl()
        {
            AutoSize = true;
            BackColor = Color.Transparent;
            Dock = DockStyle.Top;
            Margin = new Padding(0, 0, 0, 14);
            Padding = new Padding(20);
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var radius = LogicalToDeviceUnits(CornerRadius);
            var bounds = new Rectangle(0, 0, Width - 1, Height - LogicalToDeviceUnits(2));
            if (bounds.Width < 4 || bounds.Height < 4)
                return;

            var shadowBounds = bounds;
            shadowBounds.Offset(0, LogicalToDeviceUnits(2));
            using (var shadowPath = FenceRenderer.CreateRoundedRectangle(shadowBounds, radius))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(22, Color.Black)))
                graphics.FillPath(shadowBrush, shadowPath);

            using (var path = FenceRenderer.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(FluentTheme.GlassCardSurface))
            {
                graphics.FillPath(brush, path);
            }

            GlassPainter.Paint(graphics, bounds, radius);
        }
    }
}
