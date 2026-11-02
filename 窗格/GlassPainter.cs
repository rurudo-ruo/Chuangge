using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace 窗格
{
    /// <summary>
    /// Paints the glass material in three light layers — a sheen across the top of the face, an
    /// inner shadow along the bottom, and the highlight at the edge — then a faint tiled grain over
    /// all of it. Used above the backdrop blur on fences and menus.
    /// </summary>
    internal static class GlassPainter
    {
        private static Bitmap textureTile;
        private static bool textureTileDark;
        private static int textureTileStrength;

        public static void Paint(Graphics graphics, Rectangle bounds, int cornerRadius)
        {
            Paint(graphics, bounds, cornerRadius, true);
        }

        public static void Paint(Graphics graphics, Rectangle bounds, int cornerRadius, bool includeTexture)
        {
            if (bounds.Width < 4 || bounds.Height < 4)
                return;

            var state = graphics.Save();
            try
            {
                if (cornerRadius > 0)
                {
                    using (var clip = FenceRenderer.CreateRoundedRectangle(bounds, cornerRadius))
                        graphics.SetClip(clip, CombineMode.Intersect);
                }

                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                PaintSheen(graphics, bounds);
                PaintInnerShadow(graphics, bounds);

                // The edge lines follow a rounded outline. A fence is rounded by DWM, not by the
                // path, so a square 1 px line there would cut the corners and read as a seam; it
                // gets the two face washes and the grain only.
                if (cornerRadius > 0)
                    PaintHighlight(graphics, bounds, cornerRadius);

                if (includeTexture)
                    PaintTexture(graphics, bounds);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        public static void PaintTexture(Graphics graphics, Rectangle bounds)
        {
            var strength = GlobalFenceSettings.GlassTexture;
            if (strength <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var tile = GetTextureTile(strength);
            if (tile == null)
                return;

            using (var brush = new TextureBrush(tile, WrapMode.Tile))
                graphics.FillRectangle(brush, bounds);
        }

        private static void PaintSheen(Graphics graphics, Rectangle bounds)
        {
            var top = Color.FromArgb(Scale(FluentTheme.IsDark ? 18 : 28), Color.White);
            var bottom = Color.FromArgb(0, Color.White);
            using (var brush = new LinearGradientBrush(bounds, top, bottom, 90f))
                graphics.FillRectangle(brush, bounds);
        }

        private static void PaintInnerShadow(Graphics graphics, Rectangle bounds)
        {
            var top = Color.FromArgb(0, Color.Black);
            var bottom = Color.FromArgb(Scale(FluentTheme.IsDark ? 22 : 12), Color.Black);
            using (var brush = new LinearGradientBrush(bounds, top, bottom, 90f))
                graphics.FillRectangle(brush, bounds);
        }

        private static void PaintHighlight(Graphics graphics, Rectangle bounds, int cornerRadius)
        {
            var outer = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (var path = FenceRenderer.CreateRoundedRectangle(outer, Math.Max(0, cornerRadius)))
            using (var pen = new Pen(Scale(FluentTheme.GlassEdge)))
                graphics.DrawPath(pen, path);

            var inner = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
            if (inner.Width > 0 && inner.Height > 0)
            {
                using (var path = FenceRenderer.CreateRoundedRectangle(inner, Math.Max(0, cornerRadius - 1)))
                using (var pen = new Pen(Scale(FluentTheme.GlassInner)))
                    graphics.DrawPath(pen, path);
            }

            var inset = Math.Max(2, cornerRadius);
            var left = bounds.Left + inset;
            var right = bounds.Right - inset;
            if (right > left)
            {
                using (var pen = new Pen(Scale(FluentTheme.GlassRim)))
                    graphics.DrawLine(pen, left, bounds.Top + 1, right, bounds.Top + 1);
            }
        }

        private static Bitmap GetTextureTile(int strength)
        {
            var maxAlpha = Math.Max(1, strength * 8 / 30);
            if (textureTile != null && textureTileDark == FluentTheme.IsDark
                && textureTileStrength == maxAlpha)
                return textureTile;

            textureTile?.Dispose();

            const int size = 64;
            var tile = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            var random = new Random(20261101);
            var value = FluentTheme.IsDark ? 255 : 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    tile.SetPixel(x, y, Color.FromArgb(random.Next(0, maxAlpha + 1), value, value, value));
                }
            }

            textureTile = tile;
            textureTileDark = FluentTheme.IsDark;
            textureTileStrength = maxAlpha;
            return tile;
        }

        /// <summary>Scales an alpha by the user's Glass lighting setting (0–200%, default 100).</summary>
        private static int Scale(int alpha)
        {
            var percent = GlobalFenceSettings.GlassIntensity;
            return Math.Max(0, Math.Min(255, (int)Math.Round(alpha * percent / 100.0)));
        }

        private static Color Scale(Color color)
        {
            return Color.FromArgb(Scale(color.A), color);
        }
    }
}
