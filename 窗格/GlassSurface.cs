using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    /// <summary>Paints an opaque settings canvas so controls stay legible over any desktop.</summary>
    internal static class GlassSurface
    {
        public static void Paint(Graphics graphics, Color color)
        {
            graphics.Clear(color);
        }
    }

    internal class GlassPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassSurface.Paint(e.Graphics, BackColor);
        }
    }

    internal class GlassTableLayoutPanel : TableLayoutPanel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassSurface.Paint(e.Graphics, BackColor);
        }
    }

    internal class GlassFlowLayoutPanel : FlowLayoutPanel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassSurface.Paint(e.Graphics, BackColor);
        }
    }

    /// <summary>A layout inside a settings card, painted with the card's solid surface color.</summary>
    internal class CardFaceTableLayoutPanel : TableLayoutPanel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            CardFace.Paint(e.Graphics);
        }
    }

    internal class CardFaceFlowLayoutPanel : FlowLayoutPanel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            CardFace.Paint(e.Graphics);
        }
    }

    internal static class CardFace
    {
        public static void Paint(Graphics graphics)
        {
            graphics.Clear(FluentTheme.CardSurface);
        }
    }
}
