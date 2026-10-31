using System.Drawing;
using Xunit;

namespace NoFences.Tests
{
    public class FenceLayoutTests
    {
        private const int Padding = 12;
        private const int ItemWidth = 88;
        private const int ItemHeight = 80;
        private const int TitleHeight = 35;

        private static FenceLayout CreateLayout(int width = 300, int scrollOffset = 0)
        {
            return new FenceLayout(width, TitleHeight, scrollOffset, Padding, ItemWidth, ItemHeight);
        }

        [Fact]
        public void Items_start_below_the_title()
        {
            var layout = CreateLayout();

            Assert.Equal(new Rectangle(12, 47, 88, 80), layout.GetItemBounds(0));
        }

        [Fact]
        public void Items_wrap_after_three_columns()
        {
            var layout = CreateLayout();

            Assert.Equal(new Rectangle(12, 139, 88, 80), layout.GetItemBounds(3));
        }

        [Fact]
        public void Scroll_offset_moves_items_up()
        {
            var layout = CreateLayout(scrollOffset: 20);

            Assert.Equal(new Rectangle(12, 27, 88, 80), layout.GetItemBounds(0));
        }

        [Theory]
        [InlineData(12, 47, 0)]
        [InlineData(150, 47, 1)]
        [InlineData(12, 323, 9)]
        public void Hit_testing_maps_points_to_items(int x, int y, int expected)
        {
            var layout = CreateLayout();

            Assert.Equal(expected, layout.GetItemIndexAt(new Point(x, y), 10));
        }

        [Theory]
        [InlineData(11, 47)]
        [InlineData(110, 47)]
        [InlineData(12, 30)]
        public void Hit_testing_rejects_padding_and_gaps(int x, int y)
        {
            var layout = CreateLayout();

            Assert.Equal(-1, layout.GetItemIndexAt(new Point(x, y), 10));
        }

        [Fact]
        public void Visible_range_covers_the_viewport_only()
        {
            var layout = CreateLayout();

            int firstIndex;
            int lastIndex;
            layout.GetVisibleItemRange(new Rectangle(0, 0, 300, 200), 10, 0, out firstIndex, out lastIndex);

            Assert.Equal(0, firstIndex);
            Assert.Equal(5, lastIndex);
        }

        [Fact]
        public void Overflow_is_zero_when_everything_fits()
        {
            var layout = CreateLayout();

            Assert.Equal(0, layout.GetContentOverflow(4, 200));
            Assert.Equal(84, layout.GetContentOverflow(4, 100));
        }

        [Fact]
        public void Fit_size_matches_the_grid()
        {
            var layout = CreateLayout();

            Assert.Equal(new Size(312, 231), layout.GetFitSize(4));
        }

        [Fact]
        public void Narrow_fences_use_a_single_column()
        {
            var layout = CreateLayout(width: 100);

            Assert.Equal(new Size(112, 415), layout.GetFitSize(4));
        }
    }
}
