using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using 窗格.Model;
using 窗格.Util;

namespace 窗格
{
    /// <summary>
    /// Draws fence items and owns the per-item effect caches (icon glow, text shadow) with the
    /// fonts and brushes they need. Layout, background, scroll bar and the pointer halo remain in
    /// <see cref="FenceWindow"/>.
    /// </summary>
    internal sealed class FenceRenderer : IDisposable
    {
        private const float logicalIconFontSize = 9;
        private const int maximumCachedItemEffects = 128;

        private readonly ThumbnailProvider thumbnailProvider;
        private readonly Func<int, int> contentToDeviceUnits;
        private readonly StringFormat itemTextFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter
        };
        private readonly Dictionary<string, ItemRenderCache> itemRenderCache =
            new Dictionary<string, ItemRenderCache>(StringComparer.OrdinalIgnoreCase);

        private Font iconFont;
        private SolidBrush selectionBrush;
        private Pen selectionBorderPen;
        private SolidBrush itemNameBrush;
        private SolidBrush missingNameBrush;
        private long renderCacheAccessOrder;

        public FenceRenderer(ThumbnailProvider thumbnailProvider, Func<int, int> contentToDeviceUnits)
        {
            this.thumbnailProvider = thumbnailProvider
                ?? throw new ArgumentNullException(nameof(thumbnailProvider));
            this.contentToDeviceUnits = contentToDeviceUnits
                ?? throw new ArgumentNullException(nameof(contentToDeviceUnits));
        }

        public void ReloadFonts()
        {
            var iconFontSize = logicalIconFontSize * GlobalFenceSettings.ContentScalePercent / 100.0f;
            var newIconFont = new Font("Segoe UI", iconFontSize, FontStyle.Regular, GraphicsUnit.Point);
            var oldIconFont = iconFont;
            iconFont = newIconFont;
            oldIconFont?.Dispose();
        }

        public void UpdateThemeBrushes()
        {
            selectionBrush?.Dispose();
            selectionBorderPen?.Dispose();
            itemNameBrush?.Dispose();
            missingNameBrush?.Dispose();

            selectionBrush = new SolidBrush(FluentTheme.AccentOverlay(FluentTheme.IsDark ? 64 : 42));
            selectionBorderPen = new Pen(Color.FromArgb(210, FluentTheme.Accent));
            itemNameBrush = new SolidBrush(FluentTheme.Text);
            missingNameBrush = new SolidBrush(Color.FromArgb(255, 224, 176));
        }

        public void DrawItem(Graphics g, FenceEntry entry, int x, int y, int itemWidth,
            int textHeight, int iconSize, int itemHeight, bool isSelected)
        {
            var icon = entry.ExtractIcon(thumbnailProvider, iconSize);
            var name = entry.Name;

            var textMaxSize = new SizeF(itemWidth, textHeight);
            var textSize = GetItemTextSize(g, entry, name, textMaxSize);

            var inset = contentToDeviceUnits(4);
            var cardRect = new Rectangle(x - inset, y,
                itemWidth + inset * 2, itemHeight);
            var iconTextGap = contentToDeviceUnits(5);
            var contentHeight = iconSize + iconTextGap + (int)textSize.Height;
            var contentOffset = Math.Max(0, (itemHeight - contentHeight) / 2);
            var iconY = y + contentOffset;
            var textPosition = new PointF(x, iconY + iconSize + iconTextGap);

            if (isSelected)
            {
                using (var cardPath = CreateRoundedRectangle(cardRect, contentToDeviceUnits(7)))
                    g.FillPath(selectionBrush, cardPath);

                using (var cardPath = CreateRoundedRectangle(cardRect, contentToDeviceUnits(7)))
                    g.DrawPath(selectionBorderPen, cardPath);
            }

            var iconRect = new Rectangle(x + (itemWidth - iconSize) / 2, iconY, iconSize, iconSize);
            if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.IconGlowIntensity > 0)
            {
                var glowRadius = contentToDeviceUnits(GlobalFenceSettings.IconGlowRadius);
                var glowOpacity = (int)Math.Round(GlobalFenceSettings.IconGlowIntensity * 255.0 / 100);
                var iconGlow = GetIconGlow(entry, icon, iconSize, glowRadius, glowOpacity);
                g.DrawImageUnscaled(iconGlow, iconRect.X - glowRadius, iconRect.Y - glowRadius);
            }
            g.DrawIcon(icon, iconRect);

            var shadowRadius = contentToDeviceUnits(3);
            var shadowOffset = contentToDeviceUnits(1);
            var shadowOpacity = FluentTheme.IsDark ? 82 : 58;
            var blurredText = GetTextShadow(entry, name, textMaxSize, shadowRadius,
                shadowOffset, shadowOpacity, itemTextFormat);
            g.DrawImageUnscaled(blurredText,
                (int)textPosition.X - shadowRadius, (int)textPosition.Y - shadowRadius);
            g.DrawString(name, iconFont,
                entry.Type == EntryType.Missing ? missingNameBrush : itemNameBrush,
                new RectangleF(textPosition, textMaxSize), itemTextFormat);
        }

        public void Clear()
        {
            foreach (var cache in itemRenderCache.Values)
                cache.Dispose();
            itemRenderCache.Clear();
        }

        public void Remove(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            ItemRenderCache cache;
            if (!itemRenderCache.TryGetValue(path, out cache))
                return;

            itemRenderCache.Remove(path);
            cache.Dispose();
        }

        public void RemoveGlow(string path)
        {
            ItemRenderCache cache;
            if (itemRenderCache.TryGetValue(path, out cache))
            {
                cache.IconGlow?.Dispose();
                cache.IconGlow = null;
            }
        }

        public void Dispose()
        {
            iconFont?.Dispose();
            itemTextFormat.Dispose();
            selectionBrush?.Dispose();
            selectionBorderPen?.Dispose();
            itemNameBrush?.Dispose();
            missingNameBrush?.Dispose();
            Clear();
        }

        private SizeF GetItemTextSize(Graphics graphics, FenceEntry entry, string text, SizeF maxSize)
        {
            var cache = GetRenderCache(entry.Path);
            if (string.Equals(cache.MeasuredText, text, StringComparison.Ordinal)
                && cache.MeasuredFontSize == iconFont.Size
                && cache.MeasuredMaxWidth == maxSize.Width
                && cache.MeasuredMaxHeight == maxSize.Height)
                return cache.MeasuredTextSize;

            var textSize = graphics.MeasureString(text, iconFont, maxSize, itemTextFormat);
            cache.MeasuredTextSize = textSize;
            cache.MeasuredText = text;
            cache.MeasuredFontSize = iconFont.Size;
            cache.MeasuredMaxWidth = maxSize.Width;
            cache.MeasuredMaxHeight = maxSize.Height;
            return textSize;
        }

        private ItemRenderCache GetRenderCache(string path)
        {
            ItemRenderCache cache;
            if (!itemRenderCache.TryGetValue(path, out cache))
            {
                if (itemRenderCache.Count >= maximumCachedItemEffects)
                {
                    string leastRecentlyUsedPath = null;
                    ItemRenderCache leastRecentlyUsed = null;
                    foreach (var item in itemRenderCache)
                    {
                        if (leastRecentlyUsed == null || item.Value.LastAccess < leastRecentlyUsed.LastAccess)
                        {
                            leastRecentlyUsedPath = item.Key;
                            leastRecentlyUsed = item.Value;
                        }
                    }

                    itemRenderCache.Remove(leastRecentlyUsedPath);
                    leastRecentlyUsed.Dispose();
                }

                cache = new ItemRenderCache();
                itemRenderCache.Add(path, cache);
            }

            cache.LastAccess = ++renderCacheAccessOrder;
            return cache;
        }

        private Bitmap GetIconGlow(FenceEntry entry, Icon icon, int iconSize, int glowRadius, int glowOpacity)
        {
            var cache = GetRenderCache(entry.Path);
            if (cache.IconGlow != null && cache.IconSize == iconSize
                && cache.IconGlowRadius == glowRadius && cache.IconGlowOpacity == glowOpacity
                && cache.IconType == entry.Type)
                return cache.IconGlow;

            using (var iconLayer = new Bitmap(iconSize + glowRadius * 2, iconSize + glowRadius * 2))
            using (var iconGraphics = Graphics.FromImage(iconLayer))
            {
                iconGraphics.DrawIcon(icon, new Rectangle(glowRadius, glowRadius, iconSize, iconSize));
                var iconGlow = FluentTheme.CreateBlurredColor(iconLayer, glowRadius, glowOpacity);
                cache.IconGlow?.Dispose();
                cache.IconGlow = iconGlow;
                cache.IconSize = iconSize;
                cache.IconGlowRadius = glowRadius;
                cache.IconGlowOpacity = glowOpacity;
                cache.IconType = entry.Type;
            }

            return cache.IconGlow;
        }

        private Bitmap GetTextShadow(FenceEntry entry, string text, SizeF textSize,
            int shadowRadius, int shadowOffset, int shadowOpacity, StringFormat stringFormat)
        {
            var cache = GetRenderCache(entry.Path);
            var width = Math.Max(1, (int)Math.Ceiling(textSize.Width) + shadowRadius * 2);
            var height = Math.Max(1, (int)Math.Ceiling(textSize.Height) + shadowRadius * 2);
            if (cache.TextShadow != null && cache.Text == text
                && cache.TextWidth == width && cache.TextHeight == height
                && cache.FontSize == iconFont.Size && cache.ShadowRadius == shadowRadius
                && cache.ShadowOffset == shadowOffset && cache.ShadowOpacity == shadowOpacity)
                return cache.TextShadow;

            using (var textLayer = new Bitmap(width, height))
            using (var textGraphics = Graphics.FromImage(textLayer))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowOpacity, Color.Black)))
            {
                textGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                textGraphics.DrawString(text, iconFont, shadowBrush,
                    new RectangleF(shadowRadius + shadowOffset, shadowRadius + shadowOffset,
                        textSize.Width, textSize.Height), stringFormat);
                var blurredText = FluentTheme.CreateBlurredTint(textLayer, Color.Black, shadowRadius);
                cache.TextShadow?.Dispose();
                cache.TextShadow = blurredText;
                cache.Text = text;
                cache.TextWidth = width;
                cache.TextHeight = height;
                cache.FontSize = iconFont.Size;
                cache.ShadowRadius = shadowRadius;
                cache.ShadowOffset = shadowOffset;
                cache.ShadowOpacity = shadowOpacity;
            }

            return cache.TextShadow;
        }

        internal static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(1, radius * 2);
            if (bounds.Width <= diameter || bounds.Height <= diameter)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class ItemRenderCache : IDisposable
        {
            public Bitmap IconGlow;
            public int IconSize;
            public int IconGlowRadius;
            public int IconGlowOpacity;
            public EntryType IconType;
            public Bitmap TextShadow;
            public string Text;
            public int TextWidth;
            public int TextHeight;
            public float FontSize;
            public SizeF MeasuredTextSize;
            public string MeasuredText;
            public float MeasuredFontSize;
            public float MeasuredMaxWidth;
            public float MeasuredMaxHeight;
            public int ShadowRadius;
            public int ShadowOffset;
            public int ShadowOpacity;
            public long LastAccess;

            public void Dispose()
            {
                IconGlow?.Dispose();
                TextShadow?.Dispose();
            }
        }
    }
}
