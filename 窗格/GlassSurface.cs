using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    /// <summary>Paints a translucent tint over the window backdrop when blur is available.</summary>
    internal static class GlassSurface
    {
        public static void PaintBackdrop(Graphics graphics, Rectangle bounds, bool blurEnabled)
        {
            if (!blurEnabled)
            {
                graphics.Clear(FluentTheme.Canvas);
                return;
            }

            graphics.Clear(Color.Transparent);
            var alpha = GlobalFenceSettings.GetOverlayOpacity(92, 174);
            using (var brush = new SolidBrush(Color.FromArgb(alpha,
                FluentTheme.IsDark ? Color.Black : Color.White)))
            {
                graphics.FillRectangle(brush, bounds);
            }
        }
    }

    internal class GlassPanel : Panel
    {
        public GlassPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }

    internal class GlassTableLayoutPanel : TableLayoutPanel
    {
        public GlassTableLayoutPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }

    internal class GlassFlowLayoutPanel : FlowLayoutPanel
    {
        public GlassFlowLayoutPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }

    /// <summary>A transparent layout that preserves its settings card's glass finish.</summary>
    internal class CardFaceTableLayoutPanel : TableLayoutPanel
    {
        public CardFaceTableLayoutPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }

    internal class CardFaceFlowLayoutPanel : FlowLayoutPanel
    {
        public CardFaceFlowLayoutPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }
}
