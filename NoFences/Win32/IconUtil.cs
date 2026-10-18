using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NoFences.Win32
{
    /// <summary>
    /// 系统图标工具类。通过 Shell API 获取文件、文件夹及库存图标，
    /// 并保留快捷方式等 Shell 覆盖层。
    /// </summary>
    public static class IconUtil
    {
        private static readonly Lazy<Icon> FolderLargeIcon = new Lazy<Icon>(
            () => GetStockIcon(SHSIID_FOLDER, SHGSI_LARGEICON),
            true);
        private static readonly Lazy<Icon> LinkOverlayIcon = new Lazy<Icon>(
            () => GetStockIcon(SHSIID_LINK, SHGSI_LARGEICON),
            true);
        /// <summary>快捷方式覆盖层相对于图标尺寸的缩放比例（0.8 倍）。</summary>
        private const float ShortcutOverlayScale = 0.8f;
        /// <summary>标准图标逻辑像素尺寸（48 逻辑像素对应 Windows 默认中等桌面图标）。</summary>
        private const int StandardLogicalIconSize = 48;

        /// <summary>
        /// 获取系统文件夹大图标（带缓存）。
        /// 使用 null 合并运算符实现延迟初始化，只调用一次 SHGetStockIconInfo。
        /// </summary>
        public static Icon FolderLarge => FolderLargeIcon.Value;

        /// <summary>系统快捷方式箭头覆盖层。</summary>
        private static Icon LinkOverlay => LinkOverlayIcon.Value;

        /// <summary>
        /// 通过 Shell 图像工厂或快捷方式解析获取指定路径的目标尺寸图标，
        /// 快捷方式会重新叠加系统箭头覆盖层。
        /// </summary>
        public static Icon GetLargeIcon(string path, int targetSize)
        {
            if (TryGetUrlShortcutIconLocation(path, out string iconFile, out int iconIndex))
            {
                Icon customIcon = ExtractCustomShortcutIcon(path, iconFile, iconIndex, targetSize);
                if (customIcon != null)
                    return customIcon;
            }

            return GetImageFactoryIcon(path, targetSize) ?? GetFallbackIcon(path);
        }

        /// <summary>通过 SHGetFileInfo 或快捷方式解析快速获取用于异步加载前占位的 Shell 图标。</summary>
        public static Icon GetFallbackIcon(string path)
        {
            if (TryGetUrlShortcutIconLocation(path, out string iconFile, out int iconIndex))
            {
                Icon customIcon = ExtractCustomShortcutIcon(path, iconFile, iconIndex, 32);
                if (customIcon != null)
                    return customIcon;
            }

            var info = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(
                path,
                0,
                ref info,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                SHGFI_ICON | SHGFI_LARGEICON | SHGFI_ADDOVERLAYS);

            if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    return (Icon)Icon.FromHandle(info.hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }

            if (Directory.Exists(path))
            {
                Icon folder = FolderLarge;
                return folder != null
                    ? (Icon)folder.Clone()
                    : (Icon)SystemIcons.Application.Clone();
            }

            try
            {
                return Icon.ExtractAssociatedIcon(path) ?? (Icon)SystemIcons.Application.Clone();
            }
            catch
            {
                return (Icon)SystemIcons.Application.Clone();
            }
        }

        /// <summary>
        /// 尝试从 .url 互联网快捷方式（如 Steam 游戏桌面图标）中解析自定义图标文件路径及索引。
        /// </summary>
        private static bool TryGetUrlShortcutIconLocation(string path, out string iconFile, out int iconIndex)
        {
            iconFile = null;
            iconIndex = 0;

            if (string.IsNullOrEmpty(path))
                return false;

            string extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!File.Exists(path))
                return false;

            try
            {
                var sb = new StringBuilder(1024);
                GetPrivateProfileString("InternetShortcut", "IconFile", string.Empty, sb, (uint)sb.Capacity, path);
                string file = sb.ToString();

                var sbIndex = new StringBuilder(64);
                GetPrivateProfileString("InternetShortcut", "IconIndex", "0", sbIndex, (uint)sbIndex.Capacity, path);
                string indexStr = sbIndex.ToString();

                // 若 GetPrivateProfileString 未读取到，采用逐行文本读取作为后备
                if (string.IsNullOrWhiteSpace(file))
                {
                    foreach (string line in File.ReadLines(path))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        {
                            file = trimmed.Substring(9);
                        }
                        else if (trimmed.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                        {
                            indexStr = trimmed.Substring(10);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(file))
                    return false;

                file = file.Trim('"', '\'', ' ', '\t', '\r', '\n');
                file = Environment.ExpandEnvironmentVariables(file);

                if (!Path.IsPathRooted(file))
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        string combined = Path.Combine(directory, file);
                        if (File.Exists(combined))
                            file = combined;
                    }
                }

                if (!File.Exists(file))
                    return false;

                iconFile = file;
                int parsedIndex;
                if (int.TryParse(indexStr, out parsedIndex))
                    iconIndex = parsedIndex;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从快捷方式指定的图标文件（.ico、.exe、.dll 等）按目标尺寸提取图标，并重新叠加快捷方式箭头覆盖层。
        /// </summary>
        private static Icon ExtractCustomShortcutIcon(string shortcutPath, string iconFile, int iconIndex, int targetSize)
        {
            try
            {
                int safeSize = Math.Max(16, targetSize);
                var phicon = new IntPtr[1];
                var piconid = new IntPtr[1];
                uint count = PrivateExtractIcons(iconFile, iconIndex, safeSize, safeSize, phicon, piconid, 1, 0);

                if (count > 0 && phicon[0] != IntPtr.Zero)
                {
                    IntPtr hIcon = phicon[0];
                    try
                    {
                        using (var rawIcon = (Icon)Icon.FromHandle(hIcon).Clone())
                        using (var bitmap = rawIcon.ToBitmap())
                        {
                            ApplyShortcutOverlay(shortcutPath, bitmap);

                            IntPtr iconHandle = bitmap.GetHicon();
                            try
                            {
                                return (Icon)Icon.FromHandle(iconHandle).Clone();
                            }
                            finally
                            {
                                DestroyIcon(iconHandle);
                            }
                        }
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }

                // 若 PrivateExtractIcons 提取失败，对 .ico 文件尝试 System.Drawing.Icon 构造
                if (string.Equals(Path.GetExtension(iconFile), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using (var rawIcon = new Icon(iconFile, safeSize, safeSize))
                    using (var bitmap = rawIcon.ToBitmap())
                    {
                        ApplyShortcutOverlay(shortcutPath, bitmap);

                        IntPtr iconHandle = bitmap.GetHicon();
                        try
                        {
                            return (Icon)Icon.FromHandle(iconHandle).Clone();
                        }
                        finally
                        {
                            DestroyIcon(iconHandle);
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 通过 IShellItemImageFactory 按目标尺寸提取高分辨率图标。
        /// 此方法可能访问磁盘，应从后台线程调用。
        /// </summary>
        private static Icon GetImageFactoryIcon(string path, int targetSize)
        {
            IShellItemImageFactory imageFactory = null;
            IntPtr bitmapHandle = IntPtr.Zero;

            try
            {
                var interfaceId = IID_IShellItemImageFactory;
                if (SHCreateItemFromParsingName(
                    path,
                    IntPtr.Zero,
                    ref interfaceId,
                    out imageFactory) < 0 || imageFactory == null)
                {
                    return null;
                }

                var requestedSize = new SIZE
                {
                    Width = targetSize,
                    Height = targetSize
                };
                if (imageFactory.GetImage(
                    requestedSize,
                    SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY | SIIGBF_SCALEUP,
                    out bitmapHandle) < 0 || bitmapHandle == IntPtr.Zero)
                {
                    if (imageFactory.GetImage(
                        requestedSize,
                        SIIGBF_BIGGERSIZEOK | SIIGBF_SCALEUP,
                        out bitmapHandle) < 0 || bitmapHandle == IntPtr.Zero)
                    {
                        return null;
                    }
                }

                using (var bitmap = CreateBitmapWithAlpha(bitmapHandle))
                {
                    if (bitmap == null)
                        return null;

                    ApplyShortcutOverlay(path, bitmap);

                    IntPtr iconHandle = bitmap.GetHicon();
                    try
                    {
                        return (Icon)Icon.FromHandle(iconHandle).Clone();
                    }
                    finally
                    {
                        DestroyIcon(iconHandle);
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bitmapHandle != IntPtr.Zero)
                    DeleteObject(bitmapHandle);
                if (imageFactory != null && Marshal.IsComObject(imageFactory))
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(imageFactory);
                    }
                    catch (InvalidComObjectException)
                    {
                    }
                }
            }
        }

        /// <summary>为快捷方式高分辨率图标重新叠加系统箭头覆盖层，并限制最大尺寸以防大图标下模糊或过大。</summary>
        private static void ApplyShortcutOverlay(string path, Bitmap bitmap)
        {
            string extension = Path.GetExtension(path);
            if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Icon overlay = LinkOverlay;
            if (overlay == null)
                return;

            using (var overlayBitmap = overlay.ToBitmap())
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // 限制覆盖层最大尺寸为标准中等图标（48 逻辑像素）时的效果，随 DPI 缩放自适应
                uint dpi = GetSystemDpi();
                int maxOverlayWidth = Math.Max(1, (int)Math.Round(
                    StandardLogicalIconSize * ShortcutOverlayScale * (dpi / 96.0f),
                    MidpointRounding.AwayFromZero));
                int maxOverlayHeight = Math.Max(1, (int)Math.Round(
                    StandardLogicalIconSize * ShortcutOverlayScale * (dpi / 96.0f),
                    MidpointRounding.AwayFromZero));

                int targetOverlayWidth = Math.Max(1, (int)Math.Round(
                    bitmap.Width * ShortcutOverlayScale,
                    MidpointRounding.AwayFromZero));
                int targetOverlayHeight = Math.Max(1, (int)Math.Round(
                    bitmap.Height * ShortcutOverlayScale,
                    MidpointRounding.AwayFromZero));

                int overlayWidth = Math.Min(targetOverlayWidth, maxOverlayWidth);
                int overlayHeight = Math.Min(targetOverlayHeight, maxOverlayHeight);

                // 缩小覆盖层画布并保持左下角锚定，避免箭头向图标中央漂移
                var overlayRectangle = new Rectangle(
                    0,
                    bitmap.Height - overlayHeight,
                    overlayWidth,
                    overlayHeight);
                graphics.DrawImage(
                    overlayBitmap,
                    overlayRectangle);
            }
        }

        /// <summary>获取当前系统 DPI，获取失败时回退到 96 DPI。</summary>
        private static uint GetSystemDpi()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi > 0)
                    return dpi;
            }
            catch
            {
            }
            return 96;
        }

        /// <summary>将 Shell 返回的 32 位 HBITMAP 转换为保留 Alpha 通道的位图。</summary>
        private static Bitmap CreateBitmapWithAlpha(IntPtr bitmapHandle)
        {
            BITMAP nativeBitmap;
            if (GetObject(bitmapHandle, Marshal.SizeOf(typeof(BITMAP)), out nativeBitmap) == 0)
                return null;

            int width = Math.Abs(nativeBitmap.Width);
            int height = Math.Abs(nativeBitmap.Height);
            if (width == 0 || height == 0)
                return null;

            var header = new BITMAPINFOHEADER
            {
                Size = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                SizeImage = (uint)(width * height * 4)
            };
            byte[] pixels = new byte[header.SizeImage];
            IntPtr deviceContext = GetDC(IntPtr.Zero);
            try
            {
                if (deviceContext == IntPtr.Zero || GetDIBits(
                    deviceContext,
                    bitmapHandle,
                    0,
                    (uint)height,
                    pixels,
                    ref header,
                    DIB_RGB_COLORS) == 0)
                {
                    return null;
                }
            }
            finally
            {
                if (deviceContext != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, deviceContext);
            }

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                int sourceStride = width * 4;
                for (int row = 0; row < height; row++)
                {
                    Marshal.Copy(
                        pixels,
                        row * sourceStride,
                        IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride),
                        sourceStride);
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        /// <summary>
        /// 通过 SHGetStockIconInfo 获取指定类型的库存图标。
        /// </summary>
        private static Icon GetStockIcon(uint type, uint size)
        {
            var info = new SHSTOCKICONINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);

            int result = SHGetStockIconInfo(type, SHGSI_ICON | size, ref info);
            if (result < 0 || info.hIcon == IntPtr.Zero)
                return null;

            // 克隆一份以便安全释放原始句柄（防止资源泄漏）
            var icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
            DestroyIcon(info.hIcon);

            return icon;
        }

        /// <summary>SHGetStockIconInfo 参数结构体。</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHSTOCKICONINFO
        {
            public uint cbSize;       // 结构体大小
            public IntPtr hIcon;      // 图标句柄
            public int iSysIconIndex; // 系统图标列表索引
            public int iIcon;         // 图标索引
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;     // 图标文件路径
        }

        /// <summary>SHGetFileInfo 返回的文件图标信息。</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>Shell 图像工厂请求尺寸。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int Width;
            public int Height;
        }

        /// <summary>原生 HBITMAP 基本信息。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public IntPtr Bits;
        }

        /// <summary>GetDIBits 使用的 32 位位图头。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [ComImport]
        [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, uint flags, out IntPtr bitmapHandle);
        }

        [DllImport("shell32.dll")]
        public static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory imageFactory);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr handle, int objectSize, out BITMAP bitmap);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr deviceContext,
            IntPtr bitmap,
            uint startScan,
            uint scanLines,
            byte[] bits,
            ref BITMAPINFOHEADER bitmapInfo,
            uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            uint nSize,
            string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint PrivateExtractIcons(
            string szFileName,
            int nIconIndex,
            int cxIcon,
            int cyIcon,
            IntPtr[] phicon,
            IntPtr[] piconid,
            uint nIcons,
            uint flags);

        // 文件夹图标常量
        private const uint SHSIID_FOLDER = 0x3;
        private const uint SHSIID_LINK = 0x1D;
        private const uint SHGSI_ICON = 0x100;
        private const uint SHGSI_LARGEICON = 0x0;
        private const uint SHGSI_SMALLICON = 0x1;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_ADDOVERLAYS = 0x000000020;
        private const uint SIIGBF_BIGGERSIZEOK = 0x00000001;
        private const uint SIIGBF_ICONONLY = 0x00000004;
        private const uint SIIGBF_SCALEUP = 0x00000100;
        private const uint DIB_RGB_COLORS = 0;
        private static readonly Guid IID_IShellItemImageFactory =
            new Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B");
    }
}
