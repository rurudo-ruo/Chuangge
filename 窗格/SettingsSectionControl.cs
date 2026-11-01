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
            Dock = DockStyle.Top;
            Margin = new Padding(0, 0, 0, 14);
            Padding = new Padding(20);
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var radius = LogicalToDeviceUnits(CornerRadius);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = FenceRenderer.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(FluentTheme.CardSurface))
            using (var border = new Pen(FluentTheme.CardBorder))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
            }
        }
    }
}
