using System;
using System.Drawing;

namespace NoFences.Util
{
    /// <summary>统一图标网格的换行、坐标、滚动高度与拖拽插入位置计算。</summary>
    internal struct IconGridLayout
    {
        private readonly Rectangle viewport;
        private readonly int itemWidth;
        private readonly int itemHeight;
        private readonly int padding;
        private readonly int scrollOffset;
        private readonly int columns;

        /// <summary>以设备像素建立网格，布局宽度可保留纵向标题栏折叠前的换行规则。</summary>
        public IconGridLayout(Rectangle viewport, int layoutWidth, int itemWidth,
            int itemHeight, int padding, int scrollOffset)
        {
            if (itemWidth <= 0 || itemHeight <= 0 || padding < 0)
                throw new ArgumentOutOfRangeException(nameof(itemWidth));
            this.viewport = viewport;
            this.itemWidth = itemWidth;
            this.itemHeight = itemHeight;
            this.padding = padding;
            this.scrollOffset = scrollOffset;
            columns = Math.Max(1, layoutWidth / (itemWidth + padding));
        }

        /// <summary>直接计算任意索引的客户区矩形，也支持末尾插入位置。</summary>
        public Rectangle GetItemBounds(int index)
        {
            return new Rectangle(
                viewport.X + padding + index % columns * (itemWidth + padding),
                viewport.Y + padding + index / columns * (itemHeight + padding) - scrollOffset,
                itemWidth, itemHeight);
        }

        /// <summary>由最后一行计算内容溢出高度，无需逐项累加。</summary>
        public int GetScrollHeight(int count)
        {
            return count <= 0 ? 0 : Math.Max(0,
                padding + (count - 1) / columns * (itemHeight + padding) + itemHeight - viewport.Height);
        }

        /// <summary>在绘制和拖拽命中之前校正滚动偏移，适应条目减少和视口扩大。</summary>
        public int ClampScrollOffset(int count)
        {
            return Math.Max(0, Math.Min(scrollOffset, GetScrollHeight(count)));
        }

        /// <summary>取得可见行及相邻缓冲行的索引范围，保留单元边框越界的少量绘制空间。</summary>
        public void GetVisibleRange(int count, out int firstIndex, out int endIndex)
        {
            if (count <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
            {
                firstIndex = endIndex = 0;
                return;
            }
            int rowHeight = itemHeight + padding;
            int firstRow = Math.Max(0, (scrollOffset - padding) / rowHeight - 1);
            int endRow = Math.Max(0, (scrollOffset + viewport.Height - padding) / rowHeight + 2);
            firstIndex = Math.Min(count, firstRow * columns);
            endIndex = Math.Min(count, endRow * columns);
        }

        /// <summary>仅检查邻近完整行及末行，避免每次鼠标移动遍历全部离屏条目。</summary>
        public int FindInsertionIndex(Point location, int count)
        {
            if (count <= 0)
                return 0;

            int lastRow = (count - 1) / columns;
            int centerY = viewport.Y + padding - scrollOffset + itemHeight / 2;
            int projectedRow = (int)Math.Floor((location.Y - centerY) / (double)(itemHeight + padding));
            int lastFullRow = Math.Max(0, lastRow - 1);
            int lowerRow = Math.Max(0, Math.Min(lastFullRow, projectedRow));
            int upperRow = Math.Max(0, Math.Min(lastFullRow, projectedRow + 1));
            int bestIndex = count;
            long bestDistance = long.MaxValue;

            // 两个相邻完整行覆盖行边界；单独检查末行，兼顾未填满的最后一行。
            if (ScanRow(location, count, lowerRow, ref bestIndex, ref bestDistance) ||
                (upperRow != lowerRow && ScanRow(location, count, upperRow, ref bestIndex, ref bestDistance)) ||
                (lastRow != lowerRow && lastRow != upperRow &&
                 ScanRow(location, count, lastRow, ref bestIndex, ref bestDistance)))
                return bestIndex;

            if (location.Y < viewport.Top)
                return 0;
            if (location.Y > viewport.Bottom)
                return count;
            return Math.Max(0, Math.Min(count, bestIndex));
        }

        /// <summary>按原有的行命中和最近中心规则比较一行，使用长整数避免距离平方溢出。</summary>
        private bool ScanRow(Point location, int count, int row, ref int bestIndex, ref long bestDistance)
        {
            int end = Math.Min(count, (row + 1) * columns);
            for (int index = row * columns; index < end; index++)
            {
                Rectangle bounds = GetItemBounds(index);
                int centerX = bounds.Left + itemWidth / 2;
                if (location.Y >= bounds.Top - padding / 2 && location.Y <= bounds.Bottom + padding / 2)
                {
                    if (location.X < centerX)
                    {
                        bestIndex = index;
                        return true;
                    }
                    bestIndex = index + 1;
                }

                long dx = (long)location.X - centerX;
                long dy = (long)location.Y - (bounds.Top + itemHeight / 2);
                long distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = location.X <= centerX ? index : index + 1;
                }
            }
            return false;
        }
    }
}
