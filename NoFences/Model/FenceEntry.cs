using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using NoFences.Util;

namespace NoFences.Model
{
    /// <summary>
    /// 表示栅栏中的一个条目（文件或文件夹）。
    /// 保存路径引用、提取图标和打开目标；构造时不查询磁盘，允许保留暂时不可访问的引用。
    /// </summary>
    public class FenceEntry
    {
        /// <summary>文件或文件夹的完整路径</summary>
        public string Path { get; }

        /// <summary>显示名称（不含扩展名）</summary>
        public string Name { get; }

        private FenceEntry(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));
        }

        /// <summary>
        /// 根据有效路径创建引用，不在 UI 绘制期间执行文件或目录存在性查询。
        /// </summary>
        public static FenceEntry FromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                System.IO.Path.GetFullPath(path);
                return new FenceEntry(path);
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (System.IO.PathTooLongException) { return null; }
        }

        /// <summary>
        /// 提取条目的显示图标。
        /// 图片文件优先使用缩略图，其他条目通过 Shell 获取带覆盖层的大图标。
        /// </summary>
        public Icon ExtractIcon(ThumbnailProvider thumbnailProvider)
        {
            return thumbnailProvider.GenerateIcon(Path);
        }

        /// <summary>
        /// 打开条目。使用 fire-and-forget 的 Task.Run 异步启动，
        /// 不阻塞 UI 线程（不要 await 此方法）。
        /// </summary>
        public void Open()
        {
            Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Failed to start: {e}");
                }
            });
        }
    }
}
