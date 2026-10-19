using System;
using System.Collections.Generic;
using NoFences.Model;

namespace NoFences.Util
{
    /// <summary>缓存路径列表的可绘制条目及原始索引，保留无效路径而不让其占据网格单元。</summary>
    internal sealed class FenceEntrySnapshot
    {
        private string[] sourcePaths = Array.Empty<string>();
        private readonly List<FenceEntry> entries = new List<FenceEntry>();
        private readonly List<int> sourceIndices = new List<int>();
        private readonly Dictionary<string, int> displayIndices =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> sourcePathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>可绘制条目数，不包含语法无效的路径。</summary>
        public int Count => entries.Count;

        /// <summary>按显示索引访问缓存条目。</summary>
        public FenceEntry this[int index] => entries[index];

        /// <summary>路径或顺序变化时重建快照；列表未变化时不重新创建条目或集合。</summary>
        public bool Synchronize(IReadOnlyList<string> paths)
        {
            int count = paths?.Count ?? 0;
            bool changed = sourcePaths.Length != count;
            for (int index = 0; !changed && index < count; index++)
                changed = !string.Equals(sourcePaths[index], paths[index], StringComparison.Ordinal);
            if (!changed)
                return false;

            Clear();
            sourcePaths = new string[count];
            for (int index = 0; index < count; index++)
            {
                string path = paths[index];
                sourcePaths[index] = path;
                sourcePathSet.Add(path);
                FenceEntry entry = FenceEntry.FromPath(path);
                if (entry == null)
                    continue;
                displayIndices[path] = entries.Count;
                sourceIndices.Add(index);
                entries.Add(entry);
            }
            return true;
        }

        /// <summary>查找路径对应的显示索引，允许大小写不同的同一路径。</summary>
        public int IndexOfPath(string path)
        {
            int index;
            return path != null && displayIndices.TryGetValue(path, out index) ? index : -1;
        }

        /// <summary>依据完整路径列表维护多选状态，不受可见行或路径语法影响。</summary>
        public bool ContainsSourcePath(string path) => sourcePathSet.Contains(path);

        /// <summary>将显示插入位置转换为元数据列表中的位置，不删除跳过的无效路径。</summary>
        public int ToSourceInsertionIndex(int displayIndex)
        {
            displayIndex = Math.Max(0, displayIndex);
            return displayIndex >= entries.Count ? sourcePaths.Length : sourceIndices[displayIndex];
        }

        /// <summary>将原始插入位置转换为显示位置，使插入标记与实际绘制网格一致。</summary>
        public int ToDisplayInsertionIndex(int sourceIndex)
        {
            int index = sourceIndices.BinarySearch(Math.Max(0, Math.Min(sourcePaths.Length, sourceIndex)));
            return index >= 0 ? index : ~index;
        }

        /// <summary>释放当前快照中的路径与条目引用。</summary>
        public void Clear()
        {
            sourcePaths = Array.Empty<string>();
            entries.Clear();
            sourceIndices.Clear();
            displayIndices.Clear();
            sourcePathSet.Clear();
        }
    }
}
