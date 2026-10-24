using System;
using System.Drawing;

namespace 窗格
{
    internal sealed class FenceLayout
    {
        private readonly int width;
        private readonly int titleHeight;
        private readonly int scrollOffset;
        private readonly int itemPadding;
        private readonly int itemWidth;
        private readonly int itemHeight;

        public FenceLayout(int width, int titleHeight, int scrollOffset, int itemPadding, int itemWidth, int itemHeight)
        {
            this.width = width;
            this.titleHeight = titleHeight;
            this.scrollOffset = scrollOffset;
            this.itemPadding = itemPadding;
            this.itemWidth = itemWidth;
            this.itemHeight = itemHeight;
        }

        public Rectangle GetItemBounds(int index)
        {
            var columns = GetColumnCount(width, itemPadding, itemWidth);
            var row = index / columns;
            var column = index % columns;
            var strideX = itemWidth + itemPadding;
            var strideY = itemHeight + itemPadding;
            return new Rectangle(
                itemPadding + column * strideX,
                itemPadding + row * strideY + titleHeight - scrollOffset,
                itemWidth,
                itemHeight);
        }

        public int GetItemIndexAt(Point point, int itemCount)
        {
            if (itemCount <= 0 || point.X < itemPadding)
                return -1;

            var strideX = itemWidth + itemPadding;
            var columnOffset = point.X - itemPadding;
            var column = columnOffset / strideX;
            if (column >= GetColumnCount(width, itemPadding, itemWidth)
                || columnOffset % strideX >= itemWidth)
                return -1;

            var rowOffset = point.Y - titleHeight + scrollOffset - itemPadding;
            if (rowOffset < 0)
                return -1;

            var strideY = itemHeight + itemPadding;
            var row = rowOffset / strideY;
            if (rowOffset % strideY >= itemHeight)
                return -1;

            var index = row * GetColumnCount(width, itemPadding, itemWidth) + column;
            return index < itemCount ? index : -1;
        }

        public void GetVisibleItemRange(Rectangle clip, int itemCount, int effectPadding,
            out int firstIndex, out int lastIndex)
        {
            firstIndex = 0;
            lastIndex = -1;
            if (itemCount <= 0 || clip.IsEmpty)
                return;

            var columns = GetColumnCount(width, itemPadding, itemWidth);
            var strideY = itemHeight + itemPadding;
            var baseTop = itemPadding + titleHeight - scrollOffset;
            var firstRow = Math.Max(0, FloorDivide(
                clip.Top - baseTop - itemHeight - effectPadding, strideY) + 1);
            var lastRow = FloorDivide(
                clip.Bottom - 1 - baseTop + effectPadding, strideY);
            var rowCount = (itemCount + columns - 1) / columns;
            lastRow = Math.Min(rowCount - 1, lastRow);
            if (firstRow > lastRow)
                return;

            firstIndex = firstRow * columns;
            lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * columns - 1);
        }

        public int GetDropInsertionIndex(Point point, int itemCount)
        {
            if (itemCount == 0)
                return 0;

            var rowTop = GetItemBounds(0).Top;
            var closestRowDistance = int.MaxValue;
            for (var i = 0; i < itemCount; i++)
            {
                var bounds = GetItemBounds(i);
                var distance = Math.Abs(point.Y - (bounds.Top + itemHeight / 2));
                if (distance < closestRowDistance)
                {
                    closestRowDistance = distance;
                    rowTop = bounds.Top;
                }
            }

            var insertionIndex = 0;
            for (var i = 0; i < itemCount; i++)
            {
                var bounds = GetItemBounds(i);
                if (bounds.Top != rowTop)
                    continue;

                if (point.X < bounds.Left + bounds.Width / 2)
                    return i;
                insertionIndex = i + 1;
            }

            if (point.Y > rowTop + itemHeight / 2)
            {
                for (var i = 0; i < itemCount; i++)
                    if (GetItemBounds(i).Top > rowTop)
                        return i;
            }

            return insertionIndex;
        }

        public int GetContentOverflow(int itemCount, int viewportHeight)
        {
            if (itemCount == 0)
                return 0;

            var columns = GetColumnCount(width, itemPadding, itemWidth);
            var rows = (itemCount + columns - 1) / columns;
            var contentBottom = itemPadding + (rows - 1) * (itemHeight + itemPadding) + itemHeight;
            return Math.Max(0, contentBottom - viewportHeight);
        }

        public Size GetFitSize(int itemCount)
        {
            var columns = GetColumnCount(width, itemPadding, itemWidth);
            var rows = (itemCount + columns - 1) / columns;
            var targetWidth = itemPadding + columns * (itemWidth + itemPadding);
            var targetHeight = titleHeight + (Math.Max(1, rows) + 1) * itemPadding
                + rows * itemHeight;
            return new Size(targetWidth, targetHeight);
        }

        private static int GetColumnCount(int width, int itemPadding, int itemWidth)
        {
            var stride = itemWidth + itemPadding;
            var availableWidth = width - itemPadding - itemWidth;
            return Math.Max(1, availableWidth / stride + 1);
        }

        private static int FloorDivide(int value, int divisor)
        {
            var quotient = value / divisor;
            return value < 0 && value % divisor != 0 ? quotient - 1 : quotient;
        }
    }
}
