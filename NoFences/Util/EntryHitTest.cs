using System.Collections.Generic;
using System.Drawing;

namespace NoFences.Util
{
    /// <summary>统一解析可见条目命中，选中项的展开文字覆盖其下方网格。</summary>
    internal static class EntryHitTest
    {
        /// <summary>只在内容视口内查找，优先返回选中条目，避免展开文字误命中下一行。</summary>
        public static string FindPath(
            IReadOnlyDictionary<string, Rectangle> bounds,
            string selectedPath,
            Rectangle viewport,
            Point location)
        {
            if (!viewport.Contains(location))
                return null;
            Rectangle selectedBounds;
            if (selectedPath != null && bounds.TryGetValue(selectedPath, out selectedBounds) &&
                selectedBounds.Contains(location))
                return selectedPath;
            foreach (var item in bounds)
            {
                if (item.Value.Contains(location))
                    return item.Key;
            }
            return null;
        }
    }
}
