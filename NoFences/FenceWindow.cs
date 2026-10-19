using NoFences.Model;
using NoFences.Theming;
using NoFences.Util;
using NoFences.Win32;
using Peter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static NoFences.Win32.WindowUtil;

namespace NoFences
{
    public partial class FenceWindow : Form
    {
        private int logicalTitleHeight;
        private int titleHeight;

        // 图标与文字布局尺寸（根据桌面图标大小动态计算）
        private int iconSize;        // 图标边长（设备像素，直接用于 GDI+ 绘制）
        private int itemWidth;       // 每个栅栏项的宽度（设备像素，含间距）
        private int textHeight;      // 文字区域高度（设备像素）
        private int itemPadding;     // 图标与文字之间的间距（设备像素）
        private int itemHeight;      // 单个栅栏项的总高度 = iconSize + itemPadding + textHeight
        private ICONMETRICS iconMetrics; // 系统桌面图标度量（含字体、间距等）

        private readonly FenceInfo fenceInfo;

        private Font titleFont;
        private Font iconFont;
        private Font uiBoldFont;
        private readonly StringFormat standardIconTextFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.LineLimit
        };
        private readonly StringFormat expandedIconTextFormat = new StringFormat
        {
            Alignment = StringAlignment.Center
        };

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool hasHoverUpdated;
        private bool isMinified;
        private int prevWidth;
        private int prevHeight;
        private uint currentDpi = 96;
        private bool isInitializing = true;
        private bool isUpdatingFenceZOrder;
        private static FenceWindow interactedFence;
        private bool isContextMenuInteraction;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelMouseProc interactionMouseHookProc;
        private IntPtr interactionMouseHook;

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        /// <summary>接收前台窗口切换通知，委托在整个窗口生命周期内保持存活。</summary>
        private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint eventThread, uint eventTime);
        private readonly WinEventProc interactionForegroundHookProc;
        private IntPtr interactionForegroundHook;
        private IntPtr interactionForegroundWindow;

        private const int WM_MOVING = 0x0216;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private bool hasMoveOrigin;
        private Point moveStartCursor;
        private Point moveStartLocation;

        private bool isUpdatingMinifyBounds;
        private bool isAnimatingMinify;
        private Timer minifyAnimationTimer;
        private readonly System.Diagnostics.Stopwatch minifyAnimationStopwatch =
            new System.Diagnostics.Stopwatch();
        private Rectangle minifyAnimStartBounds;
        private Rectangle minifyAnimTargetBounds;
        private const double MinifyAnimationDurationMs = 180.0;

        // 图标管理模式使用独立多选集合，不改变普通模式的单选与文字展开规则。
        private bool isIconManagementMode;
        private readonly HashSet<string> iconManagementSelection =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> entryHitBounds =
            new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
        private readonly FenceEntrySnapshot entrySnapshot = new FenceEntrySnapshot();
        private Rectangle iconManagementConfirmBounds;
        private Rectangle iconManagementCancelBounds;
        private int iconManagementHoverTarget;

        private int scrollHeight;
        private int scrollOffset;

        // 右键“排序方式”子菜单（代码创建，不放入 Designer 以免资源文件冲突）
        private ToolStripMenuItem sortToolStripMenuItem;
        private ToolStripMenuItem sortByNameToolStripMenuItem;
        private ToolStripMenuItem sortBySizeToolStripMenuItem;
        private ToolStripMenuItem sortByTypeToolStripMenuItem;
        private ToolStripMenuItem sortByModifiedToolStripMenuItem;
        private ToolStripMenuItem sortByCreatedToolStripMenuItem;

        // 图标手动拖拽：按下时记录候选条目，超过系统拖拽阈值后进入拖拽状态。
        // 拖拽期间鼠标由源窗口捕获，跨分区命中通过屏幕坐标查找目标分区窗口。
        private string dragCandidatePath;
        private Point dragStartPoint;
        private bool isDraggingItem;
        private bool suppressNextClick;
        private FenceWindow dragHoverTarget;
        // 本窗口作为拖放目标时的插入位置（fenceInfo.Files 索引），-1 表示不显示指示线
        private int dropIndicatorIndex = -1;

        // 定时轮询桌面图标大小变化（Ctrl+滚轮缩放不会触发 WM_SETTINGCHANGE）
        private readonly Timer iconMetricsPollTimer;
        private int lastDesktopIconSpacing; // 上一次检测到的桌面图标设备像素间距
        private int lastDesktopIconSize;    // 上一次检测到的桌面图标逻辑像素边长

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider;
        private FileSystemWatcher managedItemsWatcher;

        // 主题对象始终保存为独立快照，确保一次绘制使用一致的颜色和尺寸。
        private ThemeDefinition currentTheme;
        private Image backgroundImage;
        private string backgroundImagePath;
        private long backgroundImageVersion;
        private readonly List<Image> themedMenuImages = new List<Image>();
        private ScaleMenuControl scaleMenuControl;
        // Windows 11 顶层窗口的系统圆角为 8 个逻辑像素；由 DWM 同时裁剪
        // Acrylic 合成表面和窗口内容，避免 Form.Region 之外残留矩形模糊层。
        private const int NativeWindowCornerRadius = 8;
        private bool usesNativeWindowCorners;
        private bool fenceRemoved;
        private bool persistenceWarningShown;

        private FenceTitlePosition CurrentTitlePosition =>
            fenceInfo != null ? fenceInfo.TitlePosition : FenceTitlePosition.Top;

        private FenceTitleTextAlignment CurrentTitleTextAlignment =>
            fenceInfo != null ? fenceInfo.TitleTextAlignment : FenceTitleTextAlignment.Center;

        private bool IsVerticalTitleBar =>
            CurrentTitlePosition == FenceTitlePosition.Left ||
            CurrentTitlePosition == FenceTitlePosition.Right;

        /// <summary>获取当前方向下标题栏在窗口客户区内的矩形范围。</summary>
        private Rectangle GetTitleBarBounds()
        {
            switch (CurrentTitlePosition)
            {
                case FenceTitlePosition.Bottom:
                    return new Rectangle(0, Math.Max(0, Height - titleHeight), Width, titleHeight);
                case FenceTitlePosition.Left:
                    return new Rectangle(0, 0, titleHeight, Height);
                case FenceTitlePosition.Right:
                    return new Rectangle(Math.Max(0, Width - titleHeight), 0, titleHeight, Height);
                default:
                    return new Rectangle(0, 0, Width, titleHeight);
            }
        }

        /// <summary>获取当前标题栏位置下，图标内容视口在客户区内的矩形范围。</summary>
        private Rectangle GetContentViewport(int managementFooterHeight)
        {
            switch (CurrentTitlePosition)
            {
                case FenceTitlePosition.Bottom:
                    return new Rectangle(
                        0,
                        0,
                        Width,
                        Math.Max(0, Height - titleHeight - managementFooterHeight));
                case FenceTitlePosition.Left:
                    return new Rectangle(
                        titleHeight,
                        0,
                        Math.Max(0, Width - titleHeight),
                        Math.Max(0, Height - managementFooterHeight));
                case FenceTitlePosition.Right:
                    return new Rectangle(
                        0,
                        0,
                        Math.Max(0, Width - titleHeight),
                        Math.Max(0, Height - managementFooterHeight));
                default:
                    return new Rectangle(
                        0,
                        titleHeight,
                        Width,
                        Math.Max(0, Height - titleHeight - managementFooterHeight));
            }
        }

        /// <summary>获取当前栅栏有效的缩放比例百分比（10%~200%，默认 100%）。</summary>
        private int GetEffectiveScalePercent()
        {
            int scale = fenceInfo != null ? fenceInfo.ScalePercent : ScaleMenuControl.DefaultScale;
            if (scale < ScaleMenuControl.MinScale || scale > ScaleMenuControl.MaxScale)
                return ScaleMenuControl.DefaultScale;
            return scale;
        }

        /// <summary>
        /// 获取当前窗口所处显示器的实时设备 DPI（不受 WinForms 缓存滞后影响）。
        /// </summary>
        private uint GetCurrentDpi()
        {
            if (!isInitializing && IsHandleCreated)
            {
                uint dpi = GetDpiForWindow(Handle);
                if (dpi > 0)
                    return dpi;
            }
            return currentDpi > 0 ? currentDpi : 96;
        }

        /// <summary>
        /// 将设备像素（物理像素）转换为逻辑像素（96 DPI 坐标）。
        /// 使用原生 GetDpiForWindow 而非 CreateGraphics().DpiX，
        /// 因为 WinForms 会缓存 DPI 值，DPI 变更后不会自动更新。
        /// </summary>
        private int DevicePixelsToLogical(int devicePixels)
        {
            uint dpi = GetCurrentDpi();
            return (int)Math.Round(devicePixels * 96.0 / dpi);
        }

        /// <summary>
        /// 将逻辑像素（96 DPI 坐标）转换为当前窗口的设备像素。
        /// 使用原生 GetDpiForWindow，避免 WinForms 缓存的 DPI 值滞后。
        /// </summary>
        private int LogicalPixelsToDevice(int logicalPixels)
        {
            uint dpi = GetCurrentDpi();
            return (int)Math.Round(logicalPixels * dpi / 96.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 处理显示器 DPI 变化，按建议的新尺寸或新旧 DPI 比例更新窗口大小及标题栏和图标度量。
        /// 拖拽窗口跨屏时，以当前鼠标坐标为缩放原点（Anchor），避免跨屏临界区尺寸反复跳变和闪烁。
        /// </summary>
        private void HandleDpiChanged(uint newDpi, RECT? suggestedRect)
        {
            if (newDpi == 0) newDpi = 96;
            if (newDpi == currentDpi) return;
            // 必须先按旧 DPI 还原展开矩形，收起态的 Left/Top 可能已经包含底部或右侧的收起偏移。
            Rectangle expandedBounds = GetCurrentExpandedBounds();
            StopMinifyAnimation();
            uint oldDpi = currentDpi;
            currentDpi = newDpi;
            float ratio = oldDpi > 0 ? (float)newDpi / oldDpi : 1f;

            bool isDragging = hasMoveOrigin || (Control.MouseButtons & MouseButtons.Left) != 0;
            Point cursor = Cursor.Position;
            float relX = Width > 0 ? (cursor.X - Left) / (float)Width : 0.5f;
            float relY = Height > 0 ? (cursor.Y - Top) / (float)Height : 0.5f;
            relX = Math.Max(0f, Math.Min(1f, relX));
            relY = Math.Max(0f, Math.Min(1f, relY));

            if (isMinified)
            {
                prevWidth = (int)Math.Round(expandedBounds.Width * ratio);
                prevHeight = (int)Math.Round(expandedBounds.Height * ratio);

                titleHeight = LogicalPixelsToDevice(logicalTitleHeight);
                Rectangle expanded = new Rectangle(expandedBounds.Location, new Size(prevWidth, prevHeight));
                Rectangle collapsed = GetCollapsedBoundsFromExpanded(expanded);
                int newW = collapsed.Width;
                int newH = collapsed.Height;

                int newLeft;
                int newTop;
                if (isDragging)
                {
                    newLeft = cursor.X - (int)Math.Round(relX * newW);
                    newTop = cursor.Y - (int)Math.Round(relY * newH);
                    moveStartLocation = new Point(newLeft, newTop);
                    moveStartCursor = cursor;
                    hasMoveOrigin = true;
                }
                else
                {
                    newLeft = collapsed.X;
                    newTop = collapsed.Y;
                }

                SetBoundsForMinifyState(new Rectangle(newLeft, newTop, newW, newH));
                if (fenceInfo != null)
                {
                    Rectangle updatedExpanded = GetCurrentExpandedBounds();
                    fenceInfo.PosX = updatedExpanded.X;
                    fenceInfo.PosY = updatedExpanded.Y;
                    fenceInfo.Width = updatedExpanded.Width;
                    fenceInfo.Height = updatedExpanded.Height;
                    fenceInfo.LastDpi = currentDpi;
                }
            }
            else
            {
                int newW;
                int newH;
                if (suggestedRect.HasValue)
                {
                    RECT r = suggestedRect.Value;
                    newW = r.Right - r.Left;
                    newH = r.Bottom - r.Top;
                    if (newW <= 0 || newH <= 0)
                    {
                        newW = (int)Math.Round(expandedBounds.Width * ratio);
                        newH = (int)Math.Round(expandedBounds.Height * ratio);
                    }
                }
                else
                {
                    newW = (int)Math.Round(expandedBounds.Width * ratio);
                    newH = (int)Math.Round(expandedBounds.Height * ratio);
                }

                int newLeft;
                int newTop;
                if (isDragging)
                {
                    newLeft = cursor.X - (int)Math.Round(relX * newW);
                    newTop = cursor.Y - (int)Math.Round(relY * newH);
                    moveStartLocation = new Point(newLeft, newTop);
                    moveStartCursor = cursor;
                    hasMoveOrigin = true;
                }
                else if (suggestedRect.HasValue)
                {
                    newLeft = suggestedRect.Value.Left;
                    newTop = suggestedRect.Value.Top;
                }
                else
                {
                    newLeft = expandedBounds.Left;
                    newTop = expandedBounds.Top;
                }

                SetWindowPos(Handle, IntPtr.Zero, newLeft, newTop, newW, newH, SWP_NOZORDER | SWP_NOACTIVATE);
                if (fenceInfo != null)
                {
                    fenceInfo.Width = newW;
                    fenceInfo.Height = newH;
                    fenceInfo.PosX = newLeft;
                    fenceInfo.PosY = newTop;
                    fenceInfo.LastDpi = currentDpi;
                }
                prevWidth = newW;
                prevHeight = newH;
                titleHeight = LogicalPixelsToDevice(logicalTitleHeight);
            }

            ApplyIconMetrics();
            ThemeUi.ApplyToContextMenu(appContextMenu, currentTheme, (int)currentDpi);
        }

        /// <summary>
        /// 主动检测当前窗口 DPI 是否与记录值不一致，用于弥补拖拽跨屏时系统未发送 WM_DPICHANGED 的情况。
        /// </summary>
        private void CheckAndHandleDpiChange()
        {
            if (!IsHandleCreated) return;
            uint winDpi = GetDpiForWindow(Handle);
            if (winDpi > 0 && winDpi != currentDpi)
            {
                HandleDpiChanged(winDpi, null);
            }
        }

        /// <summary>
        /// 加载系统图标度量并计算栅栏项布局尺寸。
        /// 优先从桌面 SysListView32 读取实际图标间距（支持 Ctrl+滚轮缩放），
        /// 回退到系统 SPI_GETICONMETRICS 的默认值，并在此基准上叠加当前栅栏的缩放比例（10%~200%）。
        /// </summary>
        private void LoadAndApplyMetrics()
        {
            float scaleFactor = GetEffectiveScalePercent() / 100f;
            try
            {
                // 1. 通过 LVM_GETITEMSPACING 读取桌面 SysListView32 的实际图标间距（设备像素）
                int deviceSpacing = GetDesktopIconSpacing();
                // 2. 通过 Shell 当前文件夹视图读取实际图标边长（逻辑像素）
                int logicalDesktopIconSize = DesktopUtil.GetDesktopIconSize();

                lastDesktopIconSpacing = deviceSpacing > 0 ? deviceSpacing : -1;
                lastDesktopIconSize = logicalDesktopIconSize > 0 ? logicalDesktopIconSize : -1;

                IntPtr hwndListView = FindDesktopListView();
                uint desktopDpi = hwndListView != IntPtr.Zero ? GetDpiForWindow(hwndListView) : 96;
                if (desktopDpi == 0) desktopDpi = 96;
                uint currentWinDpi = GetCurrentDpi();

                if (deviceSpacing > 0 && desktopDpi != currentWinDpi)
                {
                    deviceSpacing = (int)Math.Round(deviceSpacing * (double)currentWinDpi / desktopDpi);
                }

                // 3. 通过 SPI_GETICONMETRICS 读取系统图标度量（获取字体信息）
                iconMetrics = new ICONMETRICS();
                iconMetrics.cbSize = (uint)Marshal.SizeOf(typeof(ICONMETRICS));
                SystemParametersInfo(SPI_GETICONMETRICS, iconMetrics.cbSize, ref iconMetrics, 0);

                // 4. GDI+ 手工绘制直接使用设备像素；仅对 SPI 的逻辑值做一次 DPI 放大
                int deviceItemWidth;
                if (deviceSpacing > 0)
                    deviceItemWidth = deviceSpacing;
                else if (iconMetrics.iHorzSpacing > 0)
                {
                    uint sysDpi = WindowUtil.GetSystemDpi();
                    int logicalSpacing = (int)Math.Round(iconMetrics.iHorzSpacing * 96.0 / sysDpi);
                    deviceItemWidth = LogicalPixelsToDevice(logicalSpacing);
                }
                else
                    deviceItemWidth = LogicalPixelsToDevice(75);

                // 5. 根据桌面实际间距计算基准单元格宽度与基准图标尺寸
                int baseItemWidth = Math.Max(LogicalPixelsToDevice(60), deviceItemWidth);
                int renderedIconSize;
                if (logicalDesktopIconSize > 0)
                    renderedIconSize = LogicalPixelsToDevice(logicalDesktopIconSize);
                else
                    renderedIconSize = LogicalPixelsToDevice(
                        Math.Max(16, (int)(DevicePixelsToLogical(baseItemWidth) * 0.43))); // 回退到估算值

                // Shell 返回 DIP，GDI+ 绘制矩形使用设备像素，因此在此统一乘以 DPI 比例
                int baseIconSize = Math.Max(LogicalPixelsToDevice(16), renderedIconSize);
                baseItemWidth = Math.Max(baseItemWidth, baseIconSize);

                // 6. 在桌面基准尺寸之上叠加栅栏自定义缩放比例（10%~200%）
                iconSize = Math.Max(
                    LogicalPixelsToDevice(8),
                    (int)Math.Round(baseIconSize * scaleFactor, MidpointRounding.AwayFromZero));
                itemWidth = Math.Max(
                    LogicalPixelsToDevice(24),
                    (int)Math.Round(baseItemWidth * scaleFactor, MidpointRounding.AwayFromZero));
                // 图标放大时单元格至少与图标同宽，避免相邻图标重叠
                itemWidth = Math.Max(itemWidth, iconSize);
                itemPadding = Math.Max(LogicalPixelsToDevice(2), (int)(itemWidth * 0.20));
                // 文字区域高度：按桌面图标字体行高 × 行数（iTitleWrap 启用时 2 行，否则 1 行）
                using (var tmpFont = CreateIconFontFromLogFont())
                using (var graphics = CreateGraphics())
                {
                    int lineHeight = (int)Math.Ceiling(tmpFont.GetHeight(graphics));
                    textHeight = Math.Max(lineHeight, lineHeight * 2 + 4); // 按 Windows 桌面规则始终 2 行
                }
                itemHeight = iconSize + itemPadding / 3 + textHeight;  // 项总高度（与实际渲染中 icon-text 间距一致）
            }
            catch
            {
                // 读取失败时使用合理的硬编码默认值并叠加缩放比例
                itemWidth = Math.Max(
                    LogicalPixelsToDevice(24),
                    (int)Math.Round(LogicalPixelsToDevice(75) * scaleFactor, MidpointRounding.AwayFromZero));
                iconSize = Math.Max(
                    LogicalPixelsToDevice(8),
                    (int)Math.Round(LogicalPixelsToDevice(32) * scaleFactor, MidpointRounding.AwayFromZero));
                textHeight = Math.Max(
                    LogicalPixelsToDevice(10),
                    (int)Math.Round(LogicalPixelsToDevice(35) * scaleFactor, MidpointRounding.AwayFromZero));
                itemPadding = Math.Max(LogicalPixelsToDevice(2), (int)(itemWidth * 0.20));
                itemHeight = iconSize + itemPadding / 3 + textHeight;
            }
        }

        /// <summary>
        /// 查找桌面 SysListView32 窗口句柄。
        /// 桌面窗口层级：Progman → SHELLDLL_DefView → SysListView32，
        /// 部分 Windows 版本由 WorkerW 窗口承载。
        /// </summary>
        /// <returns>SysListView32 窗口句柄，失败时返回 IntPtr.Zero</returns>
        private static IntPtr FindDesktopListView()
        {
            IntPtr hwndProgman = FindWindow("Progman", null);
            if (hwndProgman == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr hwndDefView = FindWindowEx(hwndProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr hwndListView;

            if (hwndDefView != IntPtr.Zero)
            {
                hwndListView = FindWindowEx(hwndDefView, IntPtr.Zero, "SysListView32", null);
            }
            else
            {
                // 部分 Windows 版本的桌面由 WorkerW 窗口承载（备用查找路径）
                IntPtr hwndWorkerW = IntPtr.Zero;
                do
                {
                    hwndWorkerW = FindWindowEx(IntPtr.Zero, hwndWorkerW, "WorkerW", null);
                    if (hwndWorkerW != IntPtr.Zero)
                    {
                        hwndDefView = FindWindowEx(hwndWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (hwndDefView != IntPtr.Zero)
                            break;
                    }
                } while (hwndWorkerW != IntPtr.Zero);

                hwndListView = hwndDefView != IntPtr.Zero
                    ? FindWindowEx(hwndDefView, IntPtr.Zero, "SysListView32", null)
                    : IntPtr.Zero;
            }

            return hwndListView;
        }

        /// <summary>
        /// 直接读取桌面 SysListView32 的实际图标间距（通过 LVM_GETITEMSPACING 消息）。
        /// 这能反映 Ctrl+滚轮 缩放桌面图标后的实时大小，
        /// 而 SPI_GETICONMETRICS 返回的是系统默认值，不会随 Ctrl+滚轮变化。
        /// </summary>
        /// <returns>水平图标间距（设备像素），失败时返回 -1</returns>
        private static int GetDesktopIconSpacing()
        {
            IntPtr hwndListView = FindDesktopListView();
            if (hwndListView == IntPtr.Zero)
                return -1;

            // LVM_GETITEMSPACING (0x1033)：wParam=FALSE 表示获取大图标视图下的间距
            // 返回值：LOWORD = 水平间距，HIWORD = 垂直间距（单位：设备像素）
            IntPtr result = SendMessage(hwndListView, 0x1033, IntPtr.Zero, IntPtr.Zero);
            if (result == IntPtr.Zero)
                return -1;

            int spacingX = unchecked((short)((uint)result & 0xFFFF));
            return spacingX;
        }

        /// <summary>
        /// 重新加载图标度量并应用到 UI。
        /// 仅在图标大小实际发生变化时才重建缩略图和字体并触发重绘，
        /// 避免不必要的性能开销。
        /// </summary>
        private void ApplyIconMetrics()
        {
            titleHeight = LogicalPixelsToDevice(logicalTitleHeight);
            LoadAndApplyMetrics();

            if (thumbnailProvider != null && thumbnailProvider.TargetSize != iconSize)
                thumbnailProvider.TargetSize = iconSize;

            // 系统设置消息也可能只改变字体，因此每次应用度量都重建字体并重绘
            ReloadFonts();
            UpdateRoundedRegion();
            Invalidate();
        }

        /// <summary>
        /// 解析桌面渲染优先使用的字体族名称：当主题未指定自定义字体或仅使用预设默认英文字体时，
        /// 优先采用系统 SPI_GETICONMETRICS 返回的本地化桌面字体（如 Microsoft YaHei UI），
        /// 避免中文字符在 Segoe UI / Tahoma 下触发低质量字体回退导致字形发虚。
        /// </summary>
        private string ResolveDesktopFontFamily(string systemFaceName)
        {
            string themeFont = currentTheme?.FontFamilyName;
            bool hasCustomThemeFont = !string.IsNullOrWhiteSpace(themeFont) &&
                !string.Equals(themeFont, "Segoe UI", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(themeFont, "Tahoma", StringComparison.OrdinalIgnoreCase);

            if (hasCustomThemeFont)
                return themeFont;
            if (!string.IsNullOrWhiteSpace(systemFaceName))
                return systemFaceName;
            if (!string.IsNullOrWhiteSpace(themeFont))
                return themeFont;
            return SystemFonts.IconTitleFont?.FontFamily?.Name ?? "Segoe UI";
        }

        /// <summary>
        /// 从 SPI_GETICONMETRICS 返回的 LOGFONT 结构体创建 .NET Font 对象。
        /// 不能使用 CreateFontIndirect + Font.FromHfont，因为 FromHfont 仅支持 TrueType 字体，
        /// 而桌面图标字体可能是非 TrueType 字体（如 MS Sans Serif），会抛出异常。
        /// 改为直接读取 LOGFONT 各字段，手动构造 Font 对象。
        /// 字体必须按当前窗口所在显示器的设备像素（GraphicsUnit.Pixel）创建，
        /// 遵循与桌面图标相同的缩放逻辑（参考 LogicalPixelsToDevice），
        /// 避免 GDI+ 对 Point 单位字体始终按系统主屏 DPI 计算导致跨屏缩放失真。
        /// </summary>
        private Font CreateIconFontFromLogFont()
        {
            try
            {
                var lf = iconMetrics.lfFont;
                string familyName = ResolveDesktopFontFamily(lf.lfFaceName);
                // SPI_GETICONMETRICS 返回的 lfHeight 是基于系统主显示器 DPI（System DPI）计算的物理像素高度。
                // 先将其归一化为 96 DPI 的标准逻辑像素高度（标准 9pt 字体在 96 DPI 下为 12px）
                uint sysDpi = WindowUtil.GetSystemDpi();
                float logicalPixelHeight = 12f;
                if (lf.lfHeight != 0)
                {
                    logicalPixelHeight = Math.Abs(lf.lfHeight) * 96f / (float)sysDpi;
                }
                else
                {
                    float points = SystemFonts.IconTitleFont != null ? SystemFonts.IconTitleFont.SizeInPoints : 9f;
                    logicalPixelHeight = points * 96f / 72f;
                }

                if (logicalPixelHeight < 6f || logicalPixelHeight > 48f)
                    logicalPixelHeight = 12f;

                // 参考图标缩放逻辑：将逻辑像素转换为当前窗口所在显示器的设备像素，并叠加栅栏自身缩放比例
                float deviceFontSize = (float)(logicalPixelHeight * GetCurrentDpi() / 96.0);
                float fontSize = Math.Max(2f, deviceFontSize * (GetEffectiveScalePercent() / 100f));

                var style = FontStyle.Regular;
                if (lf.lfWeight >= 700) style |= FontStyle.Bold;
                if (lf.lfItalic != 0) style |= FontStyle.Italic;
                if (lf.lfUnderline != 0) style |= FontStyle.Underline;
                if (lf.lfStrikeOut != 0) style |= FontStyle.Strikeout;
                return new Font(familyName, fontSize, style, GraphicsUnit.Pixel);
            }
            catch
            {
                float fallbackLogical = 12f;
                float fallbackDevice = (float)(fallbackLogical * GetCurrentDpi() / 96.0);
                float fallbackSize = Math.Max(2f, fallbackDevice * (GetEffectiveScalePercent() / 100f));
                string fallbackFamily = SystemFonts.IconTitleFont?.FontFamily?.Name ?? "Segoe UI";
                return new Font(fallbackFamily, fallbackSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
        }

        /// <summary>
        /// 重新创建所有字体（先释放旧字体防止 GDI 资源泄漏）。
        /// 标题字体基于标题文本大小（行高固定为 1）按设备像素创建，图标字体从系统图标度量获取。
        /// </summary>
        private void ReloadFonts()
        {
            titleFont?.Dispose();
            iconFont?.Dispose();
            uiBoldFont?.Dispose();

            string familyName = ResolveDesktopFontFamily(iconMetrics.lfFont.lfFaceName);
            int deviceFontSize = Math.Max(8, LogicalPixelsToDevice(fenceInfo?.TitleFontSize ?? 14));
            try
            {
                titleFont = new Font(familyName, deviceFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch
            {
                titleFont = new Font("Segoe UI", deviceFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            int boldPixelSize = Math.Max(8, LogicalPixelsToDevice(13));
            try
            {
                uiBoldFont = new Font(familyName, boldPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            catch
            {
                uiBoldFont = new Font("Segoe UI", boldPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            iconFont = CreateIconFontFromLogFont();
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            this.fenceInfo = fenceInfo ?? throw new ArgumentNullException(nameof(fenceInfo));
            interactionMouseHookProc = InteractionMouseHookCallback;
            interactionForegroundHookProc = InteractionForegroundHookCallback;
            this.fenceInfo.ScalePercent = GetEffectiveScalePercent();
            currentTheme = ThemeManager.Instance.CurrentTheme;

            // 1. 设置为手动布局，由持久化坐标直接决定初始位置，避免系统先按 CW_USEDEFAULT 在主屏创建窗口再搬移产生的跨屏 DPI 抖动
            StartPosition = FormStartPosition.Manual;

            // 2. 初始化组件（包含上下文菜单与窗体基础设置）
            InitializeComponent();

            // 3. 从持久化位置解析目标显示器的实时 DPI，使后续度量和字体初始化直接采用正确 DPI
            uint targetDpi = WindowUtil.GetDpiForPoint(new Point(fenceInfo.PosX, fenceInfo.PosY));
            currentDpi = targetDpi > 0 ? targetDpi : 96;

            // 若显示器 DPI 在离线期间发生改变，在此按比例调整一次窗口尺寸
            if (this.fenceInfo.LastDpi > 0 && this.fenceInfo.LastDpi != currentDpi)
            {
                float ratio = (float)currentDpi / this.fenceInfo.LastDpi;
                this.fenceInfo.Width = (int)Math.Round(this.fenceInfo.Width * ratio);
                this.fenceInfo.Height = (int)Math.Round(this.fenceInfo.Height * ratio);
            }
            this.fenceInfo.LastDpi = currentDpi;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();
            InitializeScaleSubMenu();
            InitializeSortSubMenu();
            shellContextMenu.MenuClosed += ShellContextMenu_MenuClosed;
            MouseDown += FenceWindow_MouseDown;
            MouseUp += FenceWindow_MouseUp;
            ApplyContextMenuLocalization();
            darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
            autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;

            // 4. 应用持久化边界（在创建句柄前先设置 Bounds，使 CreateWindowEx 直接在目标显示器以持久化尺寸创建 HWND）
            ApplyPersistedBounds();

            // 5. 应用 Windows 视觉效果。DWM 投影需等主题圆角确定后再设置，
            // 否则矩形投影会残留在圆角外侧并在拖动时形成不透明角块。
            WindowUtil.HideFromAltTab(Handle);  // 从 Alt+Tab 隐藏
            DesktopUtil.GlueToDesktop(Handle);  // 粘附到桌面 Progman 窗口
            PlaceFenceAboveDesktop();

            Text = fenceInfo.Name;
            if (IsHandleCreated)
            {
                uint winDpi = GetDpiForWindow(Handle);
                if (winDpi > 0)
                {
                    currentDpi = winDpi;
                    this.fenceInfo.LastDpi = winDpi;
                }
            }

            // 6. 必须在定位到目标显示器后计算图标度量（此时 currentDpi 正确反映所在显示器）
            LoadAndApplyMetrics();

            // 创建缩略图生成器（iconSize 已是设备像素尺寸）
            thumbnailProvider = new ThumbnailProvider(iconSize);

            // 每 1.5 秒同时轮询桌面图标间距与边长（捕获 Ctrl+滚轮缩放及 DPI 变化）
            iconMetricsPollTimer = new Timer { Interval = 1500 };
            iconMetricsPollTimer.Tick += (s, e) =>
            {
                if (isInitializing || hasMoveOrigin || (Control.MouseButtons & MouseButtons.Left) != 0) return;
                CheckAndHandleDpiChange();
                int curSpacing = GetDesktopIconSpacing();
                int curIconSize = DesktopUtil.GetDesktopIconSize();
                bool spacingChanged = curSpacing > 0 &&
                    curSpacing != lastDesktopIconSpacing;
                bool iconSizeChanged = curIconSize > 0 &&
                    curIconSize != lastDesktopIconSize;
                if (spacingChanged || iconSizeChanged)
                {
                    ApplyIconMetrics();
                }
            };
            iconMetricsPollTimer.Start();

            // 标题栏高度：逻辑像素，根据标题文本大小与内边距自动撑开（行高固定为 1）
            int adaptedHeight = HeightDialog.CalculateAdaptedTitleHeight(
                fenceInfo.TitlePosition,
                fenceInfo.TitleFontSize,
                fenceInfo.TitlePaddingX,
                fenceInfo.TitlePaddingY);
            logicalTitleHeight = adaptedHeight;
            fenceInfo.TitleHeight = logicalTitleHeight;
            titleHeight = LogicalPixelsToDevice(logicalTitleHeight);

            this.MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;
            InitializeManagedItemsWatcher();

            ReloadFonts();

            AllowDrop = true; // 允许拖放文件到栅栏

            prevWidth = Width;
            prevHeight = Height;
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;

            ThemeManager.Instance.ThemeChanged += ThemeManager_ThemeChanged;
            AppSettingsManager.Instance.SettingsChanged += AppSettingsManager_SettingsChanged;
            ApplyTheme(currentTheme);
            Minify(animate: false); // 初始立即应用收起状态（启动时不播放过渡动画）
        }

        /// <summary>初始化右键菜单“缩放比例”子菜单及其内嵌的自定义滑块卡片。</summary>
        private void InitializeScaleSubMenu()
        {
            scaleMenuControl = new ScaleMenuControl
            {
                ScalePercent = GetEffectiveScalePercent()
            };
            scaleMenuControl.ScaleChanged += ScaleMenuControl_ScaleChanged;

            var host = new ToolStripControlHost(scaleMenuControl)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = scaleMenuControl.Size
            };

            var dropDown = new ToolStripDropDown
            {
                AutoSize = true,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            dropDown.Items.Add(host);
            dropDown.Opening += (s, e) =>
            {
                scaleMenuControl.ScalePercent = GetEffectiveScalePercent();
            };
            scaleToolStripMenuItem.DropDown = dropDown;

            appContextMenu.Closed += (s, e) =>
            {
                bool keepInFront = e.CloseReason != ToolStripDropDownCloseReason.AppFocusChange &&
                    (e.CloseReason != ToolStripDropDownCloseReason.AppClicked || IsPointInsideFenceInteraction(MousePosition));
                EndContextMenuInteraction(keepInFront);
                if (minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition))
                {
                    Minify();
                }
            };
        }

        /// <summary>响应缩放比例卡片的实时预览或最终提交，更新图标与文本度量并持久化。</summary>
        private void ScaleMenuControl_ScaleChanged(object sender, ScaleChangedEventArgs e)
        {
            fenceInfo.ScalePercent = ScaleMenuControl.ClampScale(e.ScalePercent);
            if (e.IsPreview)
            {
                LoadAndApplyMetrics();
                ReloadFonts();
                Invalidate();
            }
            else
            {
                ApplyIconMetrics();
                Refresh();
                Save();
            }
        }

        /// <summary>初始化右键“排序方式”二级子菜单。</summary>
        private void InitializeSortSubMenu()
        {
            sortToolStripMenuItem = new ToolStripMenuItem();
            sortByNameToolStripMenuItem = new ToolStripMenuItem();
            sortBySizeToolStripMenuItem = new ToolStripMenuItem();
            sortByTypeToolStripMenuItem = new ToolStripMenuItem();
            sortByModifiedToolStripMenuItem = new ToolStripMenuItem();
            sortByCreatedToolStripMenuItem = new ToolStripMenuItem();

            sortByNameToolStripMenuItem.Click += (s, e) => ApplySort(CompareByName);
            sortBySizeToolStripMenuItem.Click += (s, e) => ApplySortByKey(GetItemSize, (a, b) => b.CompareTo(a));
            sortByTypeToolStripMenuItem.Click += (s, e) => ApplySortByKey(GetItemTypeKey, StringComparer.CurrentCultureIgnoreCase.Compare);
            sortByModifiedToolStripMenuItem.Click += (s, e) => ApplySortByKey(GetItemModifiedTime, (a, b) => b.CompareTo(a));
            sortByCreatedToolStripMenuItem.Click += (s, e) => ApplySortByKey(GetItemCreationTime, (a, b) => b.CompareTo(a));

            sortToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                sortByNameToolStripMenuItem,
                sortBySizeToolStripMenuItem,
                sortByTypeToolStripMenuItem,
                sortByModifiedToolStripMenuItem,
                sortByCreatedToolStripMenuItem
            });

            int manageIndex = appContextMenu.Items.IndexOf(iconManagementToolStripMenuItem);
            if (manageIndex >= 0)
                appContextMenu.Items.Insert(manageIndex + 1, sortToolStripMenuItem);
            else
                appContextMenu.Items.Add(sortToolStripMenuItem);
        }

        /// <summary>按指定比较规则对栅栏内图标进行重新排序并持久化。</summary>
        private void ApplySort(Comparison<string> comparison)
        {
            if (fenceInfo?.Files == null || fenceInfo.Files.Count < 2)
                return;

            string error;
            if (FenceManager.Instance.TrySortEntries(fenceInfo, comparison, out error))
            {
                Refresh();
            }
            else if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(
                    this,
                    error,
                    ThemeText.Key(NoFences.Localization.LKeys.MenuSort),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>先读取一次各路径的排序键，再进行纯内存比较，避免比较器反复访问磁盘。</summary>
        private void ApplySortByKey<T>(Func<string, T> getKey, Comparison<T> compare)
        {
            if (fenceInfo.Files == null || fenceInfo.Files.Count < 2)
                return;
            var keys = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in fenceInfo.Files)
            {
                if (path != null && !keys.ContainsKey(path))
                    keys.Add(path, getKey(path));
            }
            ApplySort((a, b) =>
            {
                T left;
                T right;
                int result = a != null && b != null && keys.TryGetValue(a, out left) && keys.TryGetValue(b, out right)
                    ? compare(left, right) : 0;
                return result != 0 ? result : CompareByName(a, b);
            });
        }

        /// <summary>按资源管理器的自然名称规则排序，名称相同时以路径保持确定顺序。</summary>
        private static int CompareByName(string a, string b)
        {
            string nameA = Path.GetFileNameWithoutExtension(a) ?? "";
            string nameB = Path.GetFileNameWithoutExtension(b) ?? "";
            int cmp = WindowUtil.StrCmpLogicalW(nameA, nameB);
            if (cmp != 0) return cmp;
            return WindowUtil.StrCmpLogicalW(a ?? "", b ?? "");
        }

        private static long GetItemSize(string path)
        {
            try
            {
                if (File.Exists(path))
                    return new FileInfo(path).Length;
                return -1L;
            }
            catch
            {
                return -1L;
            }
        }

        private static string GetItemTypeKey(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    return "\0"; // 文件夹排在最前
                string ext = Path.GetExtension(path);
                return string.IsNullOrEmpty(ext) ? " " : ext.ToLowerInvariant();
            }
            catch
            {
                return " ";
            }
        }

        private static DateTime GetItemModifiedTime(string path)
        {
            try
            {
                if (File.Exists(path))
                    return File.GetLastWriteTime(path);
                if (Directory.Exists(path))
                    return Directory.GetLastWriteTime(path);
            }
            catch { }
            return DateTime.MinValue;
        }

        private static DateTime GetItemCreationTime(string path)
        {
            try
            {
                if (File.Exists(path))
                    return File.GetCreationTime(path);
                if (Directory.Exists(path))
                    return Directory.GetCreationTime(path);
            }
            catch { }
            return DateTime.MinValue;
        }

        /// <summary>
        /// 监控分区托管目录。原生菜单的剪切操作可能在菜单关闭后才真正搬走文件，
        /// 因此需要通过文件系统事件补充同步，而不能只依赖菜单返回时的即时检查。
        /// </summary>
        private void InitializeManagedItemsWatcher()
        {
            try
            {
                string dataDirectory = Path.Combine(
                    FenceManager.Instance.GetFenceDataDirectory(fenceInfo),
                    "items");
                Directory.CreateDirectory(dataDirectory);
                managedItemsWatcher = new FileSystemWatcher(dataDirectory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size
                };
                managedItemsWatcher.Deleted += ManagedItemsWatcher_Deleted;
                managedItemsWatcher.Renamed += ManagedItemsWatcher_Renamed;
                managedItemsWatcher.Changed += ManagedItemsWatcher_Changed;
                managedItemsWatcher.Error += (sender, args) =>
                    System.Diagnostics.Trace.WriteLine(
                        "Managed shortcut watcher failed: " + args.GetException());
                managedItemsWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    "Unable to watch managed shortcut directory: " + ex);
            }
        }

        private void ManagedItemsWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            QueueManagedEntryReconciliation(e.FullPath);
        }

        private void ManagedItemsWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            QueueManagedEntryReconciliation(e.OldFullPath);
        }

        private void ManagedItemsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            QueueManagedEntryThumbnailRefresh(e.FullPath);
        }

        /// <summary>托管快捷方式属性发生变化后丢弃旧图标，使下一次绘制重新提取。</summary>
        private void QueueManagedEntryThumbnailRefresh(string path)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing ||
                        !FenceManager.Instance.IsManagedDesktopEntry(fenceInfo, path))
                    {
                        return;
                    }

                    InvalidateEntry(path);
                    Invalidate();
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void QueueManagedEntryReconciliation(string oldPath)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing ||
                        !FenceManager.Instance.IsManagedDesktopEntry(fenceInfo, oldPath))
                    {
                        return;
                    }

                    string error;
                    if (!FenceManager.Instance.ReconcileManagedEntryAfterShellCommand(
                        fenceInfo,
                        oldPath,
                        out error))
                    {
                        System.Diagnostics.Trace.WriteLine(
                            "Managed shortcut reconciliation failed: " + error);
                    }
                    else
                    {
                        InvalidateEntry(oldPath);
                    }
                    selectedItem = null;
                    hoveringItem = null;
                    Invalidate();
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>校正损坏尺寸和已断开显示器留下的屏幕外坐标，恢复持久化的窗口大小与位置。</summary>
        private void ApplyPersistedBounds()
        {
            Rectangle workingArea = Screen.FromPoint(
                new Point(fenceInfo.PosX, fenceInfo.PosY)).WorkingArea;
            int width = fenceInfo.Width > 0 ? fenceInfo.Width : LogicalPixelsToDevice(300);
            int height = fenceInfo.Height > 0 ? fenceInfo.Height : LogicalPixelsToDevice(300);
            int minW = LogicalPixelsToDevice(60);
            int minH = LogicalPixelsToDevice(40);
            width = Math.Max(minW, Math.Min(width, workingArea.Width));
            height = Math.Max(minH, Math.Min(height, workingArea.Height));
            int x = Math.Max(
                workingArea.Left,
                Math.Min(fenceInfo.PosX, workingArea.Right - width));
            int y = Math.Max(
                workingArea.Top,
                Math.Min(fenceInfo.PosY, workingArea.Bottom - height));

            Bounds = new Rectangle(x, y, width, height);
            fenceInfo.PosX = x;
            fenceInfo.PosY = y;
            fenceInfo.Width = width;
            fenceInfo.Height = height;
        }

        /// <summary>
        /// 将全局主题应用到当前栅栏。位置、大小、锁定状态和文件列表等业务数据
        /// 不属于主题，因此切换主题时不会修改这些持久化信息。
        /// </summary>
        private void ApplyTheme(ThemeDefinition theme)
        {
            currentTheme = (theme ?? ThemePresets.CreateDefault()).Clone();
            currentTheme.Normalize();

            BackColor = currentTheme.MainPanelColor;
            ApplyContextMenuIcons();
            ThemeUi.ApplyToContextMenu(appContextMenu, currentTheme, (int)GetCurrentDpi());
            darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
            ReloadBackgroundImage();
            ReloadFonts();
            // 先确定由 DWM 原生圆角还是 WinForms Region 负责裁剪，再启用
            // 模糊合成。旧系统无法裁剪 Acrylic 时关闭模糊，避免矩形灰角。
            UpdateRoundedRegion();
            bool canEnableBlur = currentTheme.EnableBlur &&
                (currentTheme.CornerRadius <= 0 || usesNativeWindowCorners);
            BlurUtil.SetBlur(Handle, canEnableBlur);
            Invalidate(true);
        }

        /// <summary>
        /// 根据当前菜单风格重新生成应用菜单图标。Win11/Standard 使用彩色矢量
        /// 风格，XP 使用经典高对比图标；主题切换前释放旧位图，避免长期运行泄漏 GDI。
        /// </summary>
        private void ApplyContextMenuIcons()
        {
            ClearContextMenuIcons();
            SetContextMenuIcon(iconManagementToolStripMenuItem, ThemedMenuIcon.ManageIcons);
            SetContextMenuIcon(deleteItemToolStripMenuItem, ThemedMenuIcon.Delete);
            SetContextMenuIcon(renameToolStripMenuItem, ThemedMenuIcon.Rename);
            SetContextMenuIcon(titleSizeToolStripMenuItem, ThemedMenuIcon.TitleHeight);
            SetContextMenuIcon(scaleToolStripMenuItem, ThemedMenuIcon.Scale);
            SetContextMenuIcon(sortToolStripMenuItem, ThemedMenuIcon.Sort);
            SetContextMenuIcon(themeToolStripMenuItem, ThemedMenuIcon.Settings);
            SetContextMenuIcon(newFenceToolStripMenuItem, ThemedMenuIcon.NewFence);
            SetContextMenuIcon(exitToolStripMenuItem, ThemedMenuIcon.CloseFence);
            SetContextMenuIcon(quitApplicationToolStripMenuItem, ThemedMenuIcon.QuitApplication);
        }

        private void SetContextMenuIcon(
            ToolStripMenuItem item,
            ThemedMenuIcon icon)
        {
            Image image = ThemedMenuIconFactory.Create(icon, currentTheme);
            themedMenuImages.Add(image);
            item.ImageScaling = ToolStripItemImageScaling.None;
            item.Image = image;
        }

        /// <summary>解除菜单项引用并释放本窗口生成的所有主题图标。</summary>
        private void ClearContextMenuIcons()
        {
            iconManagementToolStripMenuItem.Image = null;
            deleteItemToolStripMenuItem.Image = null;
            lockedToolStripMenuItem.Image = null;
            minifyToolStripMenuItem.Image = null;
            renameToolStripMenuItem.Image = null;
            titleSizeToolStripMenuItem.Image = null;
            scaleToolStripMenuItem.Image = null;
            if (sortToolStripMenuItem != null)
                sortToolStripMenuItem.Image = null;
            themeToolStripMenuItem.Image = null;
            darkModeToolStripMenuItem.Image = null;
            autoSnapToolStripMenuItem.Image = null;
            newFenceToolStripMenuItem.Image = null;
            exitToolStripMenuItem.Image = null;
            quitApplicationToolStripMenuItem.Image = null;

            foreach (Image image in themedMenuImages)
                image.Dispose();
            themedMenuImages.Clear();
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyTheme(ThemeManager.Instance.CurrentTheme)));
                return;
            }

            ApplyTheme(ThemeManager.Instance.CurrentTheme);
        }

        private void AppSettingsManager_SettingsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppSettingsManager_SettingsChanged(sender, e)));
                return;
            }

            autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;
            ApplyContextMenuLocalization();
            if (!AppSettingsManager.Instance.AutoSaveEnabled)
            {
                throttledMove.Cancel();
                throttledResize.Cancel();
            }
            scaleMenuControl?.Invalidate();
            Invalidate(true);
        }

        /// <summary>
        /// 创建背景图片的内存副本，避免程序运行期间持续锁定用户选择的文件。
        /// </summary>
        private void ReloadBackgroundImage()
        {
            string path = currentTheme.BackgroundImagePath;
            long version = ThemeDrawing.GetImageFileVersion(path);
            if (string.Equals(path, backgroundImagePath, StringComparison.OrdinalIgnoreCase) &&
                version == backgroundImageVersion)
            {
                return;
            }

            backgroundImage?.Dispose();
            backgroundImage = ThemeDrawing.LoadImageWithoutLock(path);
            backgroundImagePath = path;
            backgroundImageVersion = version;
        }

        /// <summary>
        /// Windows 11 的模糊主题使用 DWM 原生圆角，使 Acrylic 合成表面、窗口
        /// 内容和拖动快照由同一轮廓裁剪。无原生支持时才回退 Form.Region；
        /// Region 圆角按当前 DPI 缩放，从而在不同显示器上保持一致。
        /// </summary>
        private void UpdateRoundedRegion()
        {
            bool hasRoundedCorners = currentTheme != null &&
                currentTheme.CornerRadius > 0 &&
                Width > 1 && Height > 1;

            // DWM 原生圆角会自动跟随窗口尺寸和 DPI，拖动缩放时无需重复
            // 设置窗口属性。直接返回可让 WM_SIZE 尽快进入整窗重绘。
            if (usesNativeWindowCorners && hasRoundedCorners &&
                currentTheme.EnableBlur)
            {
                return;
            }

            // 官方 DWM 圆角策略不适用于设置了 Window Region 的窗口，因此先
            // 清除旧 Region，再请求原生圆角。Region 对象仍由本窗口负责释放。
            Region oldRegion = Region;
            Region = null;
            oldRegion?.Dispose();
            usesNativeWindowCorners = false;

            if (hasRoundedCorners && currentTheme.EnableBlur)
            {
                // 原生圆角需要启用 DWM 非工作区合成；由 DWM 生成的投影也会
                // 随同系统圆角正确裁剪，不再出现矩形角块。
                DropShadow.SetShadow(this, true);
                usesNativeWindowCorners = DropShadow.TrySetNativeCorners(this, true);
            }

            if (!usesNativeWindowCorners)
            {
                DropShadow.TrySetNativeCorners(this, false);
                if (hasRoundedCorners)
                {
                    int radius = LogicalPixelsToDevice(currentTheme.CornerRadius);
                    using (var path = ThemeDrawing.CreateRoundedRectangle(
                        new RectangleF(0, 0, Width, Height),
                        radius))
                    {
                        Region = new Region(path);
                    }
                }

                // 自定义 Region 不能与 DWM 原生投影可靠组合；直角窗口不受
                // 此限制，继续保留经典主题原有的窗口投影。
                DropShadow.SetShadow(this, !hasRoundedCorners);
            }
        }

        /// <summary>返回与当前实际窗口轮廓一致的边框圆角设备像素半径。</summary>
        private int GetWindowBorderRadius(ThemeDefinition theme)
        {
            int logicalRadius = usesNativeWindowCorners
                ? NativeWindowCornerRadius
                : theme.CornerRadius;
            return logicalRadius > 0 ? LogicalPixelsToDevice(logicalRadius) : 0;
        }

        /// <summary>
        /// 窗口消息处理。实现无边框窗口的拖动/调整大小、自动吸附、DPI 响应、
        /// 图标度量动态更新等自定义行为。
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING)
                PreserveFenceZOrder(m.LParam);

            if (m.Msg == WM_NCLBUTTONDOWN || m.Msg == WM_NCRBUTTONDOWN)
                KeepInteractedFenceOnTop();

            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                hasMoveOrigin = true;
                moveStartCursor = Cursor.Position;
                moveStartLocation = Location;
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                hasMoveOrigin = false;
                CheckAndHandleDpiChange();
            }
            else if (m.Msg == WM_MOVING && AppSettingsManager.Instance.AutoSnapEnabled)
            {
                var rect = Marshal.PtrToStructure<RECT>(m.LParam);
                int width = Width > 0 ? Width : rect.Width;
                int height = Height > 0 ? Height : rect.Height;

                if (!hasMoveOrigin)
                {
                    hasMoveOrigin = true;
                    moveStartCursor = Cursor.Position;
                    moveStartLocation = new Point(rect.Left, rect.Top);
                }

                Point cursor = Cursor.Position;
                int targetLeft = moveStartLocation.X + (cursor.X - moveStartCursor.X);
                int targetTop = moveStartLocation.Y + (cursor.Y - moveStartCursor.Y);

                ApplyAutoSnapPosition(ref targetLeft, ref targetTop, width, height, cursor);

                rect.Left = targetLeft;
                rect.Top = targetTop;
                rect.Right = targetLeft + width;
                rect.Bottom = targetTop + height;
                Marshal.StructureToPtr(rect, m.LParam, false);
                m.Result = (IntPtr)1;
                return;
            }

            // WM_SIZING：限制窗口最小尺寸
            if (m.Msg == WM_SIZING)
            {
                int titleTextWidth;
                using (var g = CreateGraphics())
                    titleTextWidth = (int)Math.Ceiling(g.MeasureString(Text, titleFont).Width);

                int minWidth;
                int minHeight;
                if (IsVerticalTitleBar)
                {
                    int padY = LogicalPixelsToDevice(fenceInfo.TitlePaddingY);
                    minWidth = titleHeight + itemWidth + itemPadding * 2;
                    minHeight = Math.Max(titleTextWidth + padY * 2, itemHeight + itemPadding * 2);
                }
                else
                {
                    int padX = LogicalPixelsToDevice(fenceInfo.TitlePaddingX);
                    int titleMinWidth = titleTextWidth + padX * 2;
                    int iconMinWidth = itemPadding * 2 + itemWidth;
                    minWidth = Math.Max(titleMinWidth, iconMinWidth);
                    minHeight = titleHeight + itemHeight + itemPadding * 2;
                }

                var rect = Marshal.PtrToStructure<RECT>(m.LParam);
                int edge = m.WParam.ToInt32();

                if (rect.Width < minWidth)
                {
                    if (edge == WMSZ_LEFT || edge == WMSZ_TOPLEFT || edge == WMSZ_BOTTOMLEFT)
                        rect.Left = rect.Right - minWidth;
                    else
                        rect.Right = rect.Left + minWidth;
                }
                if (rect.Height < minHeight)
                {
                    if (edge == WMSZ_TOP || edge == WMSZ_TOPLEFT || edge == WMSZ_TOPRIGHT)
                        rect.Top = rect.Bottom - minHeight;
                    else
                        rect.Bottom = rect.Top + minHeight;
                }
                Marshal.StructureToPtr(rect, m.LParam, true);
                m.Result = (IntPtr)1; // 表示已处理
                return;
            }

            // 移除系统边框（WM_NCCALCSIZE → 返回 0 表示整个窗口都是工作区）
            if (m.Msg == 0x0083)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // 鼠标离开窗口 → 自动最小化（收缩为标题栏）
            var myrect = new Rectangle(Location, Size);
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
            {
                Minify();
            }

            // 阻止窗口最大化（SC_MAXIMIZE）
            if ((m.Msg == WM_SYSCOMMAND) && m.WParam.ToInt32() == 0xF032)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // 系统 DPI 变化（设置 → 显示 → 缩放），按建议矩形调整窗口尺寸并重新计算图标与标题栏度量
            if (m.Msg == WM_DPICHANGED)
            {
                if (isInitializing)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
                uint newDpiX = (uint)(m.WParam.ToInt32() & 0xFFFF);
                uint newDpi = newDpiX > 0 ? newDpiX : 96;
                RECT suggestedRect = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                HandleDpiChanged(newDpi, suggestedRect);
                m.Result = IntPtr.Zero;
                return;
            }

            // 鼠标交互只提升当前分区，不激活共享桌面所有者，避免连带抬高或闪动其他分区。
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                KeepInteractedFenceOnTop();
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            // 系统设置广播（主题/字体等变化），重新加载图标度量
            if (m.Msg == WM_SETTINGCHANGE)
            {
                ApplyIconMetrics();
            }

            base.WndProc(ref m);

            // 处于图标拖拽状态、锁定状态或鼠标右键按下时，不允许拖动和调整大小
            if (isDraggingItem || MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                return;

            // WM_NCHITTEST：自定义无边框窗口的拖动和调整大小区域
            if (m.Msg == WM_NCHITTEST)
            {
                var pt = PointToClient(new Point(m.LParam.ToInt32()));

                // 标题栏区域（支持顶/底/左/右四向）→ 模拟标题栏拖动
                if ((int)m.Result == HTCLIENT && GetTitleBarBounds().Contains(pt))
                {
                    m.Result = (IntPtr)HTCAPTION;
                    FenceWindow_MouseEnter(null, null);
                }

                // 四角与四边：10px 热区用于调整大小
                if (pt.X < 10 && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (Width - 10) && pt.Y < 10)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < 10 && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (Width - 10) && pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y < 10 && CurrentTitlePosition == FenceTitlePosition.Bottom)
                    m.Result = new IntPtr(HTTOP);
                else if (pt.Y > (Height - 10))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < 10)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (Width - 10))
                    m.Result = new IntPtr(HTRIGHT);
            }
        }

        /// <summary>切换唯一浮起的分区，先将上一个交互分区归还桌面，再提升当前分区。</summary>
        private void KeepInteractedFenceOnTop()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            FenceWindow previous = interactedFence;
            interactedFence = this;
            if (previous != null && previous != this && !previous.IsDisposed && !previous.Disposing)
            {
                if (previous.appContextMenu != null && previous.appContextMenu.Visible)
                    previous.appContextMenu.Close(ToolStripDropDownCloseReason.AppClicked);
                previous.ReturnFenceToDesktop();
            }

            InstallInteractionMouseHook();
            InstallInteractionForegroundHook();
            // 只有当前分区临时置顶；同时监听外部点击与前台切换，覆盖 Alt+Tab 等没有鼠标消息的退出路径。
            // 监听安装失败时保留普通层级；菜单交互仍由菜单关闭事件负责解除置顶。
            bool canTrackInteraction = interactionMouseHook != IntPtr.Zero && interactionForegroundHook != IntPtr.Zero;
            SetFenceZOrder(canTrackInteraction || isContextMenuInteraction ? HWND_TOPMOST : HWND_TOP);
        }

        /// <summary>取消当前分区的浮起状态，并将其放回桌面窗口层级。</summary>
        private void ReturnFenceToDesktop()
        {
            if (interactedFence == this)
                interactedFence = null;
            UninstallInteractionMouseHook();
            UninstallInteractionForegroundHook();
            PlaceFenceAboveDesktop();
        }

        /// <summary>解除临时置顶并放回实际桌面窗口上方，避免 Win+D 后沉到桌面背景后面。</summary>
        private void PlaceFenceAboveDesktop()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            IntPtr desktopView = FindDesktopListView();
            IntPtr desktop = desktopView != IntPtr.Zero
                ? GetAncestor(desktopView, 2) // GA_ROOT：桌面图标实际所在的 Progman 或 WorkerW。
                : FindWindow("Progman", null);
            if (desktop == IntPtr.Zero)
            {
                SetFenceZOrder(HWND_BOTTOM);
                return;
            }

            // SetWindowPos 插在指定窗口之后，因此取桌面前一个窗口，保持面板位于应用与桌面之间。
            IntPtr insertAfter = GetWindow(desktop, 3); // GW_HWNDPREV
            if (insertAfter == Handle)
                insertAfter = GetWindow(Handle, 3);

            bool precedingIsTopmost = false;
            if (insertAfter != IntPtr.Zero)
            {
                try
                {
                    precedingIsTopmost = (GetWindowLong(insertAfter, (int)GetWindowLongFields.GWL_EXSTYLE).ToInt64() &
                        (long)ExtendedWindowStyles.WS_EX_TOPMOST) != 0;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // 邻接窗口可能在查询期间关闭，按桌面前没有普通窗口处理。
                    insertAfter = IntPtr.Zero;
                }
            }

            if (insertAfter == IntPtr.Zero || precedingIsTopmost)
            {
                // 桌面已经位于普通窗口的顶部时，显式解除置顶；插到任务栏等置顶窗口后会继续置顶。
                bool isTopmost = (GetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE).ToInt64() &
                    (long)ExtendedWindowStyles.WS_EX_TOPMOST) != 0;
                SetFenceZOrder(isTopmost ? HWND_NOTOPMOST : HWND_TOP);
                return;
            }

            SetFenceZOrder(insertAfter);
        }

        /// <summary>单独激活菜单宿主并保持当前分区层级，避免未激活的面板使 WinForms 菜单误判失焦。</summary>
        private void BeginContextMenuInteraction()
        {
            isContextMenuInteraction = true;
            KeepInteractedFenceOnTop();

            // 与 NotifyIcon 的菜单处理一致：先激活独立的隐藏宿主，使 WinForms 记录有效的活动窗口。
            // 此宿主没有 Progman 所有者，激活它不会连带抬高共享桌面所有者的其他分区。
            IntPtr menuOwner = shellContextMenu.Handle;
            if (SetForegroundWindow(menuOwner))
                interactionForegroundWindow = menuOwner; // 同步记录，避免延迟通知漏掉紧接着的 Alt+Tab。
            else
                SetActiveWindow(menuOwner);
        }

        /// <summary>结束菜单状态；外部点击或切换分区时归还桌面，在分区内继续操作时保留当前交互。</summary>
        private void EndContextMenuInteraction(bool keepInFront)
        {
            if (!isContextMenuInteraction)
                return;

            isContextMenuInteraction = false;
            if (keepInFront && interactedFence == this)
                KeepInteractedFenceOnTop();
            else
                ReturnFenceToDesktop();
        }

        /// <summary>原生菜单退出跟踪后先解除分区置顶，再由 Shell 执行命令或打开属性等窗口。</summary>
        private void ShellContextMenu_MenuClosed(object sender, EventArgs e)
        {
            EndContextMenuInteraction(keepInFront: false);
        }

        /// <summary>在应用菜单显示前保护面板层级，菜单取消打开或抛出异常时及时解除保护。</summary>
        private void ShowAppContextMenu(Point location)
        {
            BeginContextMenuInteraction();
            try
            {
                appContextMenu.Show(this, location);
            }
            finally
            {
                if (!appContextMenu.Visible)
                    EndContextMenuInteraction(keepInFront: true);
            }
        }

        /// <summary>在窗口层级改变前阻止系统或菜单带来的被动重排，避免其他分区短暂浮到前面。</summary>
        private void PreserveFenceZOrder(IntPtr windowPosition)
        {
            if (isUpdatingFenceZOrder || windowPosition == IntPtr.Zero)
                return;

            WINDOWPOS position = Marshal.PtrToStructure<WINDOWPOS>(windowPosition);
            if ((position.flags & SWP_NOZORDER) != 0)
                return;

            position.flags |= SWP_NOZORDER;
            Marshal.StructureToPtr(position, windowPosition, false);
        }

        /// <summary>仅在启动、鼠标交互或菜单生命周期内调整当前分区层级，不激活共享所有者。</summary>
        private void SetFenceZOrder(IntPtr insertAfter)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            bool wasUpdatingFenceZOrder = isUpdatingFenceZOrder;
            isUpdatingFenceZOrder = true;
            try
            {
                SetWindowPos(
                    Handle,
                    insertAfter,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
            finally
            {
                isUpdatingFenceZOrder = wasUpdatingFenceZOrder;
            }
        }

        /// <summary>交互期间监听外部点击，保证只有当前分区临时置顶且能及时归还桌面。</summary>
        private void InstallInteractionMouseHook()
        {
            if (interactionMouseHook != IntPtr.Zero)
                return;

            interactionMouseHook = SetWindowsHookEx(
                WH_MOUSE_LL,
                interactionMouseHookProc,
                GetModuleHandle(null),
                0);
        }

        /// <summary>解除当前分区的外部点击监听。</summary>
        private void UninstallInteractionMouseHook()
        {
            IntPtr hook = interactionMouseHook;
            interactionMouseHook = IntPtr.Zero;
            if (hook != IntPtr.Zero)
                UnhookWindowsHookEx(hook);
        }

        /// <summary>仅在分区交互期间监听前台切换，补足不激活窗口时缺失的失活通知。</summary>
        private void InstallInteractionForegroundHook()
        {
            if (interactionForegroundHook != IntPtr.Zero)
                return;

            interactionForegroundWindow = GetForegroundWindow();
            interactionForegroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                interactionForegroundHookProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }

        /// <summary>解除前台切换监听，并清除本次交互的前台窗口记录。</summary>
        private void UninstallInteractionForegroundHook()
        {
            IntPtr hook = interactionForegroundHook;
            interactionForegroundHook = IntPtr.Zero;
            interactionForegroundWindow = IntPtr.Zero;
            if (hook != IntPtr.Zero)
                UnhookWinEvent(hook);
        }

        /// <summary>在安装钩子的 UI 线程处理前台切换，忽略已经过期或属于其他交互的异步通知。</summary>
        private void InteractionForegroundHookCallback(IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint eventThread, uint eventTime)
        {
            try
            {
                if (hook == IntPtr.Zero || hook != interactionForegroundHook ||
                    eventType != EVENT_SYSTEM_FOREGROUND || window == IntPtr.Zero ||
                    IsDisposed || Disposing || !IsHandleCreated || interactedFence != this ||
                    window != GetForegroundWindow())
                    return;

                ProcessInteractionForegroundChange(window);
            }
            catch
            {
                // 前台事件回调不能让异常越过非托管边界。
            }
        }

        /// <summary>切换到其他应用时结束临时置顶；当前分区、所属对话框和菜单之间切换时保留交互。</summary>
        private void ProcessInteractionForegroundChange(IntPtr window)
        {
            if (window == interactionForegroundWindow)
                return;

            interactionForegroundWindow = window;
            if (IsFenceInteractionWindow(window) ||
                IsOpenDropDownWindow(appContextMenu, window))
                return;

            ReturnFenceToDesktop();
            if (appContextMenu != null && appContextMenu.Visible)
                appContextMenu.Close(ToolStripDropDownCloseReason.AppFocusChange);
            else if (isContextMenuInteraction)
                EndMenu();
        }

        /// <summary>按窗口句柄识别当前菜单及展开的子菜单，避免鼠标停在菜单时误判前台切换。</summary>
        private static bool IsOpenDropDownWindow(ToolStripDropDown dropDown, IntPtr window)
        {
            if (dropDown == null || !dropDown.Visible)
                return false;
            if (dropDown.IsHandleCreated && GetAncestor(window, 2) == dropDown.Handle)
                return true;

            foreach (ToolStripItem item in dropDown.Items)
            {
                ToolStripDropDownItem dropDownItem = item as ToolStripDropDownItem;
                if (dropDownItem != null && dropDownItem.HasDropDownItems &&
                    IsOpenDropDownWindow(dropDownItem.DropDown, window))
                    return true;
            }
            return false;
        }

        /// <summary>接收交互期间的鼠标按下，继续传递原始消息以保留其他窗口的正常点击行为。</summary>
        private IntPtr InteractionMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            IntPtr hook = interactionMouseHook;
            try
            {
                int message = wParam.ToInt32();
                if (nCode >= 0 && interactedFence == this &&
                    IsMouseButtonDownMessage(message))
                {
                    LowLevelMouseHookStruct mouseData =
                        (LowLevelMouseHookStruct)Marshal.PtrToStructure(
                            lParam,
                            typeof(LowLevelMouseHookStruct));
                    ProcessInteractionMouseDown(mouseData.Point);
                }
            }
            catch
            {
                // 鼠标钩子回调不能让异常越过非托管边界。
            }

            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        /// <summary>点击菜单、面板及其对话框时保留交互；点击其他窗口时解除置顶并关闭应用菜单。</summary>
        private void ProcessInteractionMouseDown(Point screenPoint)
        {
            if (appContextMenu != null && appContextMenu.Visible)
            {
                if (IsPointInsideOpenContextMenu(screenPoint))
                    return;

                if (!IsPointInsideFenceInteraction(screenPoint))
                    ReturnFenceToDesktop();
                appContextMenu.Close(ToolStripDropDownCloseReason.AppClicked);
                return;
            }

            // 原生菜单自行跟踪外部点击，并通过 MenuClosed 在执行菜单命令前结束交互。
            if (!isContextMenuInteraction && !IsPointInsideFenceInteraction(screenPoint))
                ReturnFenceToDesktop();
        }

        /// <summary>按实际命中窗口判断点击是否仍属于当前分区，兼容覆盖面板的其他窗口。</summary>
        private bool IsPointInsideFenceInteraction(Point screenPoint)
        {
            return IsFenceInteractionWindow(WindowFromPoint(screenPoint));
        }

        /// <summary>识别当前分区、独立菜单宿主及它们拥有的窗口，不将共用桌面所有者的其他分区视为当前交互。</summary>
        private bool IsFenceInteractionWindow(IntPtr window)
        {
            IntPtr current = GetAncestor(window, 2); // GA_ROOT：先从子控件找到顶层窗口。
            for (int depth = 0; current != IntPtr.Zero && depth < 32; depth++)
            {
                if (current == Handle || current == shellContextMenu.Handle)
                    return true;
                current = GetWindow(current, 4); // GW_OWNER：仅沿当前窗口的拥有者链查找。
            }
            return false;
        }

        /// <summary>判断鼠标消息是否为任意按键按下。</summary>
        private static bool IsMouseButtonDownMessage(int message)
        {
            return message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN ||
                message == WM_MBUTTONDOWN || message == WM_XBUTTONDOWN;
        }

        /// <summary>判断屏幕坐标是否位于应用右键菜单或其当前展开的任意子菜单内。</summary>
        private bool IsPointInsideOpenContextMenu(Point screenPoint)
        {
            return IsPointInsideOpenDropDown(appContextMenu, screenPoint);
        }

        /// <summary>递归检查可见菜单及已展开子菜单的屏幕范围。</summary>
        private bool IsPointInsideOpenDropDown(ToolStripDropDown dropDown, Point screenPoint)
        {
            if (dropDown == null || !dropDown.Visible)
                return false;

            Rectangle screenBounds = new Rectangle(
                dropDown.PointToScreen(Point.Empty),
                dropDown.Size);
            if (screenBounds.Contains(screenPoint))
                return true;

            foreach (ToolStripItem item in dropDown.Items)
            {
                ToolStripDropDownItem dropDownItem = item as ToolStripDropDownItem;
                if (dropDownItem != null &&
                    IsPointInsideOpenDropDown(dropDownItem.DropDown, screenPoint))
                    return true;
            }

            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LowLevelMouseHookStruct
        {
            public Point Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelMouseProc callback,
            IntPtr moduleHandle,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        /// <summary>注册指定范围的窗口事件通知。</summary>
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr moduleHandle,
            WinEventProc callback, uint processId, uint threadId, uint flags);

        /// <summary>释放窗口事件通知钩子。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        /// <summary>读取当前前台窗口，用于过滤延迟送达的前台切换事件。</summary>
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>将独立菜单宿主设为前台活动窗口，供 WinForms 正确跟踪菜单交互。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        /// <summary>前台激活受系统限制时，仍为本线程的菜单设置稳定的活动窗口。</summary>
        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr window);

        /// <summary>前台切到外部窗口时结束当前线程的原生菜单跟踪。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndMenu();

        /// <summary>
        /// 计算窗口移动时的边缘与邻近桌面分区自动吸附坐标。
        /// 基于鼠标拖拽起始偏移计算原始坐标，确保慢速移出吸附区时依然顺滑不卡死。
        /// </summary>
        private void ApplyAutoSnapPosition(ref int left, ref int top, int width, int height, Point cursor)
        {
            int snapDist = LogicalPixelsToDevice(14);
            int bestDx = snapDist + 1;
            int bestDy = snapDist + 1;
            int snappedLeft = left;
            int snappedTop = top;

            void TrySnapX(int candidateLeft, int distance)
            {
                if (distance <= snapDist && distance < bestDx)
                {
                    bestDx = distance;
                    snappedLeft = candidateLeft;
                }
            }

            void TrySnapY(int candidateTop, int distance)
            {
                if (distance <= snapDist && distance < bestDy)
                {
                    bestDy = distance;
                    snappedTop = candidateTop;
                }
            }

            // 1. 吸附当前屏幕工作区与屏幕物理边缘
            Screen screen = Screen.FromPoint(cursor);
            if (screen != null)
            {
                Rectangle wa = screen.WorkingArea;
                TrySnapX(wa.Left, Math.Abs(left - wa.Left));
                TrySnapX(wa.Right - width, Math.Abs((left + width) - wa.Right));
                TrySnapY(wa.Top, Math.Abs(top - wa.Top));
                TrySnapY(wa.Bottom - height, Math.Abs((top + height) - wa.Bottom));

                Rectangle sb = screen.Bounds;
                TrySnapX(sb.Left, Math.Abs(left - sb.Left));
                TrySnapX(sb.Right - width, Math.Abs((left + width) - sb.Right));
                TrySnapY(sb.Top, Math.Abs(top - sb.Top));
                TrySnapY(sb.Bottom - height, Math.Abs((top + height) - sb.Bottom));
            }

            // 2. 吸附其他可见桌面分区的四边（外贴合与边缘对齐）
            foreach (Form form in Application.OpenForms)
            {
                var other = form as FenceWindow;
                if (other == null || other == this || !other.Visible || other.IsDisposed)
                    continue;

                Rectangle ob = other.Bounds;
                bool verticalNear = (top <= ob.Bottom + snapDist * 2) && (top + height >= ob.Top - snapDist * 2);
                bool verticalAlignNear = (top <= ob.Bottom + snapDist * 10) && (top + height >= ob.Top - snapDist * 10);

                if (verticalNear)
                {
                    TrySnapX(ob.Right, Math.Abs(left - ob.Right));
                    TrySnapX(ob.Left - width, Math.Abs((left + width) - ob.Left));
                }
                if (verticalAlignNear)
                {
                    TrySnapX(ob.Left, Math.Abs(left - ob.Left));
                    TrySnapX(ob.Right - width, Math.Abs((left + width) - ob.Right));
                }

                bool horizontalNear = (left <= ob.Right + snapDist * 2) && (left + width >= ob.Left - snapDist * 2);
                bool horizontalAlignNear = (left <= ob.Right + snapDist * 10) && (left + width >= ob.Left - snapDist * 10);

                if (horizontalNear)
                {
                    TrySnapY(ob.Bottom, Math.Abs(top - ob.Bottom));
                    TrySnapY(ob.Top - height, Math.Abs((top + height) - ob.Top));
                }
                if (horizontalAlignNear)
                {
                    TrySnapY(ob.Top, Math.Abs(top - ob.Top));
                    TrySnapY(ob.Bottom - height, Math.Abs((top + height) - ob.Bottom));
                }
            }

            left = snappedLeft;
            top = snappedTop;
        }

        /// <summary>移除当前栅栏（删除分区及其保存的数据）。</summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                this,
                ThemeText.RemoveFenceQuestion,
                ThemeText.RemoveFenceTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string error;
                if (FenceManager.Instance.TryRemoveFence(fenceInfo, out error))
                {
                    fenceRemoved = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        this,
                        ThemeText.RemoveFenceFailed + Environment.NewLine + Environment.NewLine + error,
                        ThemeText.RemoveFenceTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Refresh();
                }
            }
        }

        /// <summary>退出整个应用程序，关闭所有栅栏窗口。</summary>
        private void quitApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string error;
            if (FenceManager.Instance.TryRestoreDesktopShortcutsForExit(out error))
            {
                Application.Exit();
            }
            else
            {
                MessageBox.Show(
                    this,
                    ThemeText.ExitFailed + Environment.NewLine + Environment.NewLine + error,
                    ThemeText.ExitTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Refresh();
            }
        }

        /// <summary>从栅栏中删除当前悬停的条目。</summary>
        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string item = hoveringItem;
            if (string.IsNullOrWhiteSpace(item))
                return;

            string error;
            if (FenceManager.Instance.TryRemoveEntry(fenceInfo, item, out error))
            {
                InvalidateEntry(item);
                hoveringItem = null;
                if (selectedItem == item)
                    selectedItem = null;
                Refresh();
            }
            else
            {
                MessageBox.Show(
                    this,
                    ThemeText.RemoveItemFailed + Environment.NewLine + Environment.NewLine + error,
                    ThemeText.RemoveItemTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>从应用右键菜单进入图标多选管理模式。</summary>
        private void iconManagementToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExpandFromMinified(animate: false);

            isIconManagementMode = true;
            iconManagementSelection.Clear();
            selectedItem = null;
            hoveringItem = null;
            Refresh();
        }

        /// <summary>退出图标管理模式，并清理所有仅用于本次操作的选择状态。</summary>
        private void CancelIconManagement()
        {
            isIconManagementMode = false;
            iconManagementSelection.Clear();
            iconManagementConfirmBounds = Rectangle.Empty;
            iconManagementCancelBounds = Rectangle.Empty;
            iconManagementHoverTarget = 0;
            hoveringItem = null;
            Refresh();
        }

        /// <summary>
        /// 确认移除当前多选条目。普通条目仅解除栅栏引用；受托管的桌面快捷方式
        /// 会先恢复文件及桌面坐标。失败条目保持选中，便于用户修复后重试。
        /// </summary>
        private void ConfirmIconManagement()
        {
            if (iconManagementSelection.Count == 0)
                return;

            var errors = new List<string>();
            var selectedPaths = new List<string>(iconManagementSelection);
            foreach (string path in selectedPaths)
            {
                string error;
                if (FenceManager.Instance.TryRemoveEntry(fenceInfo, path, out error))
                {
                    InvalidateEntry(path);
                    iconManagementSelection.Remove(path);
                    if (selectedItem == path)
                        selectedItem = null;
                }
                else
                {
                    errors.Add(Path.GetFileName(path) + "：" + error);
                }
            }

            if (errors.Count == 0)
            {
                CancelIconManagement();
                return;
            }

            Refresh();
            MessageBox.Show(
                this,
                ThemeText.RemoveItemsFailed + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, errors),
                ThemeText.RemoveItemsTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        /// <summary>处理图标管理模式中的底部按钮和条目多选点击。</summary>
        private void HandleIconManagementClick(Point location)
        {
            if (iconManagementCancelBounds.Contains(location))
            {
                CancelIconManagement();
                return;
            }

            if (iconManagementConfirmBounds.Contains(location))
            {
                ConfirmIconManagement();
                return;
            }

            Rectangle viewport = GetContentViewport(GetIconManagementFooterHeight());
            if (!viewport.Contains(location))
                return;

            foreach (KeyValuePair<string, Rectangle> item in entryHitBounds)
            {
                if (!item.Value.Contains(location))
                    continue;

                if (!iconManagementSelection.Add(item.Key))
                    iconManagementSelection.Remove(item.Key);
                Refresh();
                return;
            }
        }

        /// <summary>
        /// 本地化右键上下文菜单的所有菜单项。
        /// 解决在缺少 zh-CN 附属资源程序集（例如单文件绿色运行）时，
        /// 菜单项出现中英文混杂的问题。
        /// </summary>
        private void ApplyContextMenuLocalization()
        {
            deleteItemToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuDeleteItem);
            lockedToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuLock);
            minifyToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuMinify);
            renameToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuRename);
            titleSizeToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuTitleSize);
            scaleToolStripMenuItem.Text = ThemeText.ScaleMenu;
            iconManagementToolStripMenuItem.Text = ThemeText.IconManagement;
            if (sortToolStripMenuItem != null)
            {
                sortToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSort);
                sortByNameToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSortByName);
                sortBySizeToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSortBySize);
                sortByTypeToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSortByType);
                sortByModifiedToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSortByModified);
                sortByCreatedToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuSortByCreated);
            }
            themeToolStripMenuItem.Text = ThemeText.SettingsMenu;
            darkModeToolStripMenuItem.Text = ThemeText.DarkMode;
            autoSnapToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuAutoSnap);
            newFenceToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuNewFence);
            exitToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuRemoveFence);
            quitApplicationToolStripMenuItem.Text = ThemeText.Key(NoFences.Localization.LKeys.MenuQuitApp);
        }

        /// <summary>
        /// 在应用自有右键菜单打开前更新动态菜单项，并在最终布局完成后重新应用
        /// 当前主题。该菜单由空白处右键或条目上的 Shift+右键触发。
        /// </summary>
        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ApplyContextMenuLocalization();
            deleteItemToolStripMenuItem.Visible = hoveringItem != null;
            iconManagementToolStripMenuItem.Enabled = !isIconManagementMode;
            if (sortToolStripMenuItem != null)
            {
                sortToolStripMenuItem.Visible = !isIconManagementMode;
                sortToolStripMenuItem.Enabled = !isIconManagementMode && (fenceInfo?.Files?.Count > 1);
            }
            darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
            autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;
            if (scaleMenuControl != null)
                scaleMenuControl.ScalePercent = GetEffectiveScalePercent();
            // 可见性和勾选状态会触发 ToolStrip 重新布局；在这些状态更新后应用
            // 主题，确保空白处右键弹出的整个菜单窗口都按当前主题重新绘制。
            ThemeUi.ApplyToContextMenu(appContextMenu, currentTheme, (int)GetCurrentDpi());
        }

        /// <summary>拖放进入：仅接受文件拖放（锁定状态下拒绝）。</summary>
        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                !lockedToolStripMenuItem.Checked &&
                !isIconManagementMode)
                e.Effect = DragDropEffects.Move;
        }

        /// <summary>
        /// 拖放释放：普通路径加入栅栏；桌面快捷方式则记录坐标并搬入托管目录。
        /// 任一条目失败时保留原文件，并在处理其余条目后统一提示。
        /// </summary>
        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            if (isIconManagementMode)
                return;

            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            var errors = new List<string>();
            foreach (var file in dropped)
            {
                if (!ItemExists(file))
                    continue;

                string error;
                if (!FenceManager.Instance.TryAddEntry(fenceInfo, file, out error))
                    errors.Add(Path.GetFileName(file) + ": " + error);
            }

            Refresh();
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    ThemeText.AddItemsFailed + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, errors),
                    ThemeText.AddItemsTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>窗口大小变化：节流保存新尺寸（4 秒延迟）。</summary>
        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            UpdateRoundedRegion();

            // 先同步重绘，再执行可能落盘的节流保存，避免拖动边框时新客户区
            // 等待保存逻辑而只显示底层 Acrylic 表面。
            Refresh();

            if (isInitializing || isUpdatingMinifyBounds || isAnimatingMinify)
                return;

            if (!isMinified)
            {
                prevWidth = Width;
                prevHeight = Height;
            }

            ScheduleBoundsSave(throttledResize);
        }

        /// <summary>鼠标移动：处理图标拖拽移动判定，以及悬停高亮更新。</summary>
        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !string.IsNullOrEmpty(dragCandidatePath) && !isIconManagementMode)
            {
                if (!isDraggingItem)
                {
                    int dx = Math.Abs(e.Location.X - dragStartPoint.X);
                    int dy = Math.Abs(e.Location.Y - dragStartPoint.Y);
                    Size dragSize = SystemInformation.DragSize;
                    if (dx > dragSize.Width || dy > dragSize.Height)
                    {
                        isDraggingItem = true;
                        Capture = true;
                        suppressNextClick = true;
                    }
                }

                if (isDraggingItem)
                {
                    HandleItemDragMove(e.Location);
                    return;
                }
            }

            string hitPath = HitTestEntry(e.Location);

            int footerTarget = 0;
            if (isIconManagementMode)
            {
                if (iconManagementConfirmBounds.Contains(e.Location))
                    footerTarget = 1;
                else if (iconManagementCancelBounds.Contains(e.Location))
                    footerTarget = 2;
            }

            if (string.Equals(hitPath, hoveringItem, StringComparison.OrdinalIgnoreCase) &&
                footerTarget == iconManagementHoverTarget)
            {
                return;
            }

            iconManagementHoverTarget = footerTarget;
            Invalidate();
        }

        /// <summary>鼠标进入：如果允许最小化且当前是最小化状态，则向标题栏反方向展开窗口。</summary>
        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                ExpandFromMinified();
            }
        }

        /// <summary>鼠标离开：尝试向标题栏方向收起窗口。选中状态不取消，由用户点击其他条目或空白区域来改变。</summary>
        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            if (isDraggingItem || isContextMenuInteraction)
                return;

            iconManagementHoverTarget = 0;
            if (isAnimatingMinify && !isMinified && Bounds.Contains(MousePosition))
            {
                return;
            }

            Minify();
            Refresh();
        }

        /// <summary>根据展开状态下的完整窗口矩形，计算向标题栏方向收起后的窗口矩形。</summary>
        private Rectangle GetCollapsedBoundsFromExpanded(Rectangle expanded)
        {
            switch (CurrentTitlePosition)
            {
                case FenceTitlePosition.Bottom:
                    return new Rectangle(
                        expanded.X,
                        expanded.Bottom - titleHeight,
                        expanded.Width,
                        titleHeight);
                case FenceTitlePosition.Left:
                    return new Rectangle(
                        expanded.X,
                        expanded.Y,
                        titleHeight,
                        expanded.Height);
                case FenceTitlePosition.Right:
                    return new Rectangle(
                        expanded.Right - titleHeight,
                        expanded.Y,
                        titleHeight,
                        expanded.Height);
                default: // Top
                    return new Rectangle(
                        expanded.X,
                        expanded.Y,
                        expanded.Width,
                        titleHeight);
            }
        }

        /// <summary>根据收起状态下的标题栏窗口矩形，计算从标题栏方向反向展开后的完整窗口矩形。</summary>
        private Rectangle GetExpandedBoundsFromCollapsed(Rectangle collapsed)
        {
            int expW = prevWidth > titleHeight ? prevWidth : Math.Max(collapsed.Width, fenceInfo.Width);
            int expH = prevHeight > titleHeight ? prevHeight : Math.Max(collapsed.Height, fenceInfo.Height);
            switch (CurrentTitlePosition)
            {
                case FenceTitlePosition.Bottom:
                    return new Rectangle(
                        collapsed.X,
                        collapsed.Bottom - expH,
                        expW,
                        expH);
                case FenceTitlePosition.Left:
                    return new Rectangle(
                        collapsed.X,
                        collapsed.Y,
                        expW,
                        expH);
                case FenceTitlePosition.Right:
                    return new Rectangle(
                        collapsed.Right - expW,
                        collapsed.Y,
                        expW,
                        expH);
                default: // Top
                    return new Rectangle(
                        collapsed.X,
                        collapsed.Y,
                        expW,
                        expH);
            }
        }

        /// <summary>获取当前分区的完整展开态屏幕矩形（即使处于收起或收起过渡动画中也能返回展开后的真实坐标与宽高）。</summary>
        private Rectangle GetCurrentExpandedBounds()
        {
            if (isAnimatingMinify)
            {
                return isMinified
                    ? GetExpandedBoundsFromCollapsed(minifyAnimTargetBounds)
                    : minifyAnimTargetBounds;
            }

            if (isMinified)
            {
                return GetExpandedBoundsFromCollapsed(Bounds);
            }

            return Bounds;
        }

        private void SetBoundsForMinifyState(Rectangle bounds)
        {
            bool prevFlag = isUpdatingMinifyBounds;
            isUpdatingMinifyBounds = true;
            try
            {
                Bounds = bounds;
            }
            finally
            {
                isUpdatingMinifyBounds = prevFlag;
            }
        }

        private void StopMinifyAnimation()
        {
            isAnimatingMinify = false;
            minifyAnimationStopwatch.Stop();
            if (minifyAnimationTimer != null && minifyAnimationTimer.Enabled)
            {
                minifyAnimationTimer.Stop();
            }
        }

        private void ApplyOrAnimateMinifyBounds(Rectangle targetBounds, bool animate)
        {
            if (!animate || !IsHandleCreated || !Visible)
            {
                StopMinifyAnimation();
                SetBoundsForMinifyState(targetBounds);
                Refresh();
                return;
            }

            if (Bounds == targetBounds)
            {
                StopMinifyAnimation();
                Refresh();
                return;
            }

            minifyAnimStartBounds = Bounds;
            minifyAnimTargetBounds = targetBounds;
            isAnimatingMinify = true;
            minifyAnimationStopwatch.Restart();

            if (minifyAnimationTimer == null)
            {
                minifyAnimationTimer = new Timer { Interval = 15 };
                minifyAnimationTimer.Tick += MinifyAnimationTimer_Tick;
            }
            if (!minifyAnimationTimer.Enabled)
            {
                minifyAnimationTimer.Start();
            }
        }

        private void MinifyAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                StopMinifyAnimation();
                return;
            }

            double elapsed = minifyAnimationStopwatch.Elapsed.TotalMilliseconds;
            double t = Math.Min(1.0, Math.Max(0.0, elapsed / MinifyAnimationDurationMs));
            double inv = 1.0 - t;
            double eased = 1.0 - inv * inv * inv; // Cubic Ease-Out

            if (t >= 1.0)
            {
                Rectangle finalBounds = minifyAnimTargetBounds;
                StopMinifyAnimation();
                SetBoundsForMinifyState(finalBounds);
                Refresh();

                bool menuOpen = isContextMenuInteraction || (appContextMenu != null && appContextMenu.Visible) ||
                    (scaleToolStripMenuItem?.DropDown != null && scaleToolStripMenuItem.DropDown.Visible);
                if (!isIconManagementMode && !menuOpen && minifyToolStripMenuItem.Checked)
                {
                    if (!isMinified && !Bounds.Contains(MousePosition))
                    {
                        Minify();
                    }
                    else if (isMinified && Bounds.Contains(MousePosition))
                    {
                        ExpandFromMinified();
                    }
                }
                return;
            }

            int curLeft = (int)Math.Round(minifyAnimStartBounds.Left + (minifyAnimTargetBounds.Left - minifyAnimStartBounds.Left) * eased);
            int curTop = (int)Math.Round(minifyAnimStartBounds.Top + (minifyAnimTargetBounds.Top - minifyAnimStartBounds.Top) * eased);
            int curRight = (int)Math.Round(minifyAnimStartBounds.Right + (minifyAnimTargetBounds.Right - minifyAnimStartBounds.Right) * eased);
            int curBottom = (int)Math.Round(minifyAnimStartBounds.Bottom + (minifyAnimTargetBounds.Bottom - minifyAnimStartBounds.Bottom) * eased);

            // 锚定标题栏所在静止边缘，消除帧间圆整误差导致的 1px 抖动
            switch (CurrentTitlePosition)
            {
                case FenceTitlePosition.Bottom:
                    curBottom = minifyAnimTargetBounds.Bottom;
                    curLeft = minifyAnimTargetBounds.Left;
                    curRight = minifyAnimTargetBounds.Right;
                    break;
                case FenceTitlePosition.Left:
                    curLeft = minifyAnimTargetBounds.Left;
                    curTop = minifyAnimTargetBounds.Top;
                    curBottom = minifyAnimTargetBounds.Bottom;
                    break;
                case FenceTitlePosition.Right:
                    curRight = minifyAnimTargetBounds.Right;
                    curTop = minifyAnimTargetBounds.Top;
                    curBottom = minifyAnimTargetBounds.Bottom;
                    break;
                default: // Top
                    curTop = minifyAnimTargetBounds.Top;
                    curLeft = minifyAnimTargetBounds.Left;
                    curRight = minifyAnimTargetBounds.Right;
                    break;
            }

            var stepBounds = new Rectangle(
                curLeft,
                curTop,
                Math.Max(titleHeight, curRight - curLeft),
                Math.Max(titleHeight, curBottom - curTop));
            SetBoundsForMinifyState(stepBounds);
            Invalidate();
            Update();
        }

        /// <summary>从收起（仅标题栏）状态沿标题栏反方向恢复到展开前尺寸。</summary>
        private void ExpandFromMinified(bool? animate = null)
        {
            if (!isMinified)
                return;

            Rectangle expandedBounds = GetCurrentExpandedBounds();
            isMinified = false;
            bool useAnim = animate ?? AppSettingsManager.Instance.MinifyAnimationEnabled;
            ApplyOrAnimateMinifyBounds(expandedBounds, useAnim);
        }

        /// <summary>
        /// 将窗口向标题栏所在方向收缩为仅标题栏（最小化状态）。
        /// 仅在 CanMinify 启用且未最小化时生效；右键菜单展开期间保持展开以方便预览缩放。
        /// </summary>
        private void Minify(bool? animate = null)
        {
            bool menuOpen = isContextMenuInteraction || (appContextMenu != null && appContextMenu.Visible) ||
                (scaleToolStripMenuItem?.DropDown != null && scaleToolStripMenuItem.DropDown.Visible);
            if (!isIconManagementMode && !menuOpen && minifyToolStripMenuItem.Checked && !isMinified)
            {
                Rectangle expandedBounds = GetCurrentExpandedBounds();
                prevWidth = expandedBounds.Width;
                prevHeight = expandedBounds.Height;
                Rectangle collapsedBounds = GetCollapsedBoundsFromExpanded(expandedBounds);
                isMinified = true;
                bool useAnim = animate ?? AppSettingsManager.Instance.MinifyAnimationEnabled;
                ApplyOrAnimateMinifyBounds(collapsedBounds, useAnim);
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isMinified && !minifyToolStripMenuItem.Checked)
            {
                ExpandFromMinified();
            }
            else if (!isMinified && minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition))
            {
                Minify();
            }
            fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
            Save();
        }

        /// <summary>单击：设置选中更新标志，由 Paint 周期实际处理。</summary>
        private void FenceWindow_Click(object sender, EventArgs e)
        {
            if (isIconManagementMode)
                return;

            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            shouldUpdateSelection = true;
            Refresh();
        }

        /// <summary>双击优先命中选中条目的展开区域，再异步打开目标，绘制过程不执行启动操作。</summary>
        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            if (isIconManagementMode)
                return;

            string path = HitTestEntry(PointToClient(MousePosition));
            var entry = path != null ? GetEntry(path) : null;
            if (entry == null)
                return;
            ReturnFenceToDesktop();
            entry.Open();
        }

        /// <summary>从当前可见内容区域查找条目，展开的选中条目优先于其下方网格。</summary>
        private string HitTestEntry(Point location)
        {
            return EntryHitTest.FindPath(entryHitBounds, selectedItem,
                GetContentViewport(isIconManagementMode ? GetIconManagementFooterHeight() : 0), location);
        }

        /// <summary>根据当前布局解析点击，优先检查展开文字，不依赖上一帧可能已失效的命中缓存。</summary>
        private string HitTestEntryLayout(Graphics graphics, IconGridLayout grid, Rectangle viewport, Point location)
        {
            if (!viewport.Contains(location))
                return null;

            int selectedIndex = entrySnapshot.IndexOfPath(selectedItem);
            if (selectedIndex >= 0)
            {
                FenceEntry selected = entrySnapshot[selectedIndex];
                if (MeasureEntryHitBounds(graphics, selected, grid.GetItemBounds(selectedIndex), true).Contains(location))
                    return selected.Path;
            }

            grid.GetVisibleRange(entrySnapshot.Count, out int firstIndex, out int endIndex);
            for (int index = firstIndex; index < endIndex; index++)
            {
                FenceEntry entry = entrySnapshot[index];
                if (MeasureEntryHitBounds(graphics, entry, grid.GetItemBounds(index), false).Contains(location))
                    return entry.Path;
            }
            return null;
        }

        /// <summary>测量标准或选中展开区域，供绘制和当前布局点击命中共同使用。</summary>
        private Rectangle MeasureEntryHitBounds(Graphics graphics, FenceEntry entry, Rectangle cell, bool expandSelected)
        {
            int textInset = Math.Min(6, Math.Max(1, itemWidth / 6));
            int textWidth = Math.Max(4, itemWidth - textInset * 2);
            SizeF textSize = graphics.MeasureString(entry.Name, iconFont,
                new SizeF(textWidth, expandSelected ? 9999 : textHeight),
                expandSelected ? expandedIconTextFormat : standardIconTextFormat);
            if (expandSelected && textSize.Height <= textHeight)
                textSize = graphics.MeasureString(entry.Name, iconFont,
                    new SizeF(textWidth, textHeight), standardIconTextFormat);
            return new Rectangle(cell.X - 2, cell.Y - 2,
                itemWidth + 2, iconSize + (int)textSize.Height + itemPadding / 3 + 2);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            uint dpi = GetDpiForWindow(Handle);
            if (dpi > 0)
                currentDpi = dpi;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (IsHandleCreated)
            {
                uint winDpi = GetDpiForWindow(Handle);
                if (winDpi > 0)
                {
                    currentDpi = winDpi;
                    if (fenceInfo != null)
                        fenceInfo.LastDpi = winDpi;
                }
            }
            isInitializing = false;

            // 初次显示时放在桌面上方，不抢占其他应用的前景位置。
            PlaceFenceAboveDesktop();
        }

        /// <summary>初次显示分区时不激活窗口，避免启动时抢占前景焦点。</summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        /// <summary>
        /// 跳过 WinForms 默认的纯色背景擦除，将背景完全清空为透明（ARGB 0），
        /// 确保 BufferedGraphics 缓存的位图不会残留上一次或其他界面的绘制像素，
        /// 同时让带透明度的主题背景正常交给 DWM 进行亚克力模糊合成。
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Transparent);
        }

        /// <summary>
        /// 主渲染方法。所有 GDI+ 绘制在此完成。
        /// 绘制顺序：清空透明底色 → 背景 → 标题栏 → 条目网格 → 滚动条 → 点击/悬停处理。
        /// </summary>
        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Transparent);
            var theme = currentTheme ?? ThemePresets.CreateDefault();
            e.Graphics.SetClip(ClientRectangle);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            // 主面板颜色、透明度与背景图片均来自当前主题。
            using (var backgroundBrush = new SolidBrush(ThemeDrawing.WithOpacity(
                theme.MainPanelColor,
                theme.MainPanelOpacityPercent)))
            {
                e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
            }
            ThemeDrawing.DrawBackgroundImage(
                e.Graphics,
                backgroundImage,
                ClientRectangle,
                theme.BackgroundImageLayout,
                theme.BackgroundImageOpacityPercent);

            // 标题背景与标题文字按配置的标题栏位置（顶/底/左/右）与对齐方式（左/中/右）绘制。
            Rectangle titleBounds = GetTitleBarBounds();
            using (var titleBrush = new SolidBrush(ThemeDrawing.WithOpacity(
                theme.TitleBarColor,
                theme.TitleBarOpacityPercent)))
            {
                e.Graphics.FillRectangle(titleBrush, titleBounds);
            }
            DrawTitleBarText(e.Graphics, theme, titleBounds);

            int managementFooterHeight = isIconManagementMode
                ? GetIconManagementFooterHeight()
                : 0;
            Rectangle contentViewport = GetContentViewport(managementFooterHeight);
            IconGridLayout grid = PrepareIconGridLayout(contentViewport);
            Point mousePosition = PointToClient(MousePosition);

            // 点击按本帧布局处理，即使列表或滚动变化已清空上一帧的命中缓存也保持展开区域优先。
            if (!isIconManagementMode && shouldUpdateSelection)
            {
                selectedItem = HitTestEntryLayout(e.Graphics, grid, contentViewport, mousePosition);
                shouldUpdateSelection = false;
            }

            // 仅遍历可见行；选中条目可能从视口上方展开文字，因此额外保留这一个候选。
            grid.GetVisibleRange(entrySnapshot.Count, out int firstIndex, out int endIndex);
            int selectedIndex = isIconManagementMode ? -1 : entrySnapshot.IndexOfPath(selectedItem);
            bool selectedOutsideRange = selectedIndex >= 0 && (selectedIndex < firstIndex || selectedIndex >= endIndex);
            entryHitBounds.Clear();
            var contentState = e.Graphics.Save();
            e.Graphics.SetClip(
                contentViewport,
                System.Drawing.Drawing2D.CombineMode.Intersect);
            // 记录选中条目（仅当文字超过标准 2 行高度时才需要展开渲染）
            string expandEntryName = null;
            int expandEntryX = 0, expandEntryY = 0;
            FenceEntry expandEntry = null; // 用于在展开区块中重绘图标

            for (int cursor = firstIndex; cursor < endIndex + (selectedOutsideRange ? 1 : 0); cursor++)
            {
                int entryIndex = cursor < endIndex ? cursor : selectedIndex;
                FenceEntry entry = entrySnapshot[entryIndex];
                Rectangle gridBounds = grid.GetItemBounds(entryIndex);
                int drawX = gridBounds.X;
                int drawY = gridBounds.Y;

                bool isSelected = !isIconManagementMode && entry.Path == selectedItem;
                bool needsExpand = false;

                if (isSelected)
                {
                    // 用不限高度测量文字真实高度，超过标准 2 行高度才需展开
                    int textInset = Math.Min(6, Math.Max(1, itemWidth / 6));
                    var testMaxSize = new SizeF(Math.Max(4, itemWidth - textInset * 2), 9999);
                    var testSize = e.Graphics.MeasureString(
                        entry.Name,
                        iconFont,
                        testMaxSize,
                        expandedIconTextFormat);
                    needsExpand = testSize.Height > textHeight;
                }

                var cellBounds = new Rectangle(drawX - 2, drawY - 2, itemWidth + 2, itemHeight + 4);
                if (needsExpand || cellBounds.IntersectsWith(contentViewport))
                {
                    Rectangle entryBounds = RenderEntry(
                        e.Graphics, entry, drawX, drawY, mousePosition, contentViewport, skipText: needsExpand);
                    entryHitBounds[entry.Path] = entryBounds;
                }

                if (needsExpand)
                {
                    expandEntryName = entry.Name;
                    expandEntryX = drawX;
                    expandEntryY = drawY;
                    expandEntry = entry;
                }
            }

            if (isIconManagementMode)
            {
                // 只绘制可见行后，不能用可见命中缓存清除滚出视口的多选条目。
                iconManagementSelection.RemoveWhere(path => !entrySnapshot.ContainsSourcePath(path));
            }

            // 选中条目文字展开渲染：仅当文字超过标准 2 行高度时才进入此分支，高度不限，渲染在最顶层
            if (expandEntryName != null)
            {
                var expandTextPadding = itemPadding / 3;
                int textInset = Math.Min(6, Math.Max(1, itemWidth / 6));
                var expandTextPos = new PointF(expandEntryX + textInset, expandEntryY + iconSize + expandTextPadding);
                var expandMaxSize = new SizeF(Math.Max(4, itemWidth - textInset * 2), 9999); // 不限制行数
                var expandTextSize = e.Graphics.MeasureString(
                    expandEntryName,
                    iconFont,
                    expandMaxSize,
                    expandedIconTextFormat);
                float drawHeight = expandTextSize.Height;
                var drawSize = new SizeF(expandTextSize.Width, drawHeight);

                // 绘制选中高亮框（高度适配展开后的文案）
                var gap = expandTextPadding;
                var expandOutlineRect = new Rectangle(expandEntryX - 2, expandEntryY - 2,
                    itemWidth + 2, iconSize + (int)drawHeight + gap + 2);
                bool expandMouseOver = contentViewport.Contains(mousePosition) && expandOutlineRect.Contains(mousePosition);
                entryHitBounds[expandEntry.Path] = expandOutlineRect;
                if (expandMouseOver)
                {
                    hoveringItem = expandEntry.Path;
                    hasHoverUpdated = true;
                }
                if (theme.MenuStyle != ThemeMenuStyle.WindowsXp)
                {
                    DrawModernDesktopItemState(
                        e.Graphics,
                        expandOutlineRect,
                        true,
                        expandMouseOver,
                        theme);
                }

                // 重绘图标（图层在最上方，防止被高亮框覆盖变模糊）
                var expandIcon = expandEntry?.ExtractIcon(thumbnailProvider);
                if (expandIcon != null)
                {
                    var expandIconRect = new Rectangle(expandEntryX + itemWidth / 2 - iconSize / 2, expandEntryY, iconSize, iconSize);
                    DrawDesktopIconForState(
                        e.Graphics,
                        expandIcon,
                        expandIconRect,
                        true,
                        theme);
                }

                DrawDesktopIconTextForState(
                    e.Graphics,
                    expandEntryName,
                    new RectangleF(expandTextPos, drawSize),
                    expandedIconTextFormat,
                    theme,
                    true,
                    expandMouseOver);
            }

            // 滚动条：仅在内容溢出时绘制
            if (scrollHeight > 0)
            {
                int contentHeight = Math.Max(1, contentViewport.Height);
                int totalContentHeight = contentHeight + scrollHeight;
                int proportionalHeight = (int)Math.Round(
                    contentHeight * (contentHeight / (double)totalContentHeight));
                int scrollbarHeight = Math.Min(
                    contentHeight,
                    Math.Max(LogicalPixelsToDevice(20), proportionalHeight));
                int scrollbarTravel = contentHeight - scrollbarHeight;
                int scrollbarY = contentViewport.Y + (int)Math.Round(
                    scrollOffset / (double)scrollHeight * scrollbarTravel);
                int scrollbarX = Math.Max(contentViewport.X, contentViewport.Right - 5);
                using (var scrollbarBrush = new SolidBrush(
                    ThemeDrawing.WithAlpha(theme.ScrollBarColor, 190)))
                {
                    e.Graphics.FillRectangle(
                        scrollbarBrush,
                        new Rectangle(scrollbarX, scrollbarY, 5, scrollbarHeight));
                }
            }

            // 绘制拖拽插入指示线（仅在作为目标分区且 dropIndicatorIndex 有效时绘制）
            if (dropIndicatorIndex >= 0)
            {
                DrawDropIndicator(e.Graphics, theme, contentViewport, grid);
            }

            // 恢复内容裁剪，双击打开已在鼠标事件中完成目标解析。
            e.Graphics.Restore(contentState);

            // 绘制跟随鼠标移动的拖拽半透明幽灵图标
            if (isDraggingItem)
            {
                DrawDraggingGhost(e.Graphics, theme);
            }

            if (isIconManagementMode)
                DrawIconManagementFooter(e.Graphics, theme, contentViewport, managementFooterHeight);

            // 边框半径必须与实际的 DWM 或 Region 窗口轮廓一致。
            float borderRadius = GetWindowBorderRadius(theme);
            using (var borderPath = ThemeDrawing.CreateRoundedRectangle(
                new RectangleF(
                    0.5f,
                    0.5f,
                    Math.Max(0, Width - 1f),
                    Math.Max(0, Height - 1f)),
                borderRadius))
            using (var borderPen = new Pen(
                ThemeDrawing.WithAlpha(theme.BorderColor, 125)))
            {
                e.Graphics.DrawPath(borderPen, borderPath);
            }

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldUpdateSelection = false;
            hasHoverUpdated = false;
        }

        /// <summary>按当前栅栏的标题栏位置（顶/底/左/右）、文本对齐方式、内边距绘制标题文字（行高固定为 1）。</summary>
        private void DrawTitleBarText(Graphics graphics, ThemeDefinition theme, Rectangle titleBounds)
        {
            if (titleBounds.Width <= 0 || titleBounds.Height <= 0 || string.IsNullOrEmpty(Text))
                return;

            StringAlignment primaryAlignment;
            switch (CurrentTitleTextAlignment)
            {
                case FenceTitleTextAlignment.Left:
                    primaryAlignment = StringAlignment.Near;
                    break;
                case FenceTitleTextAlignment.Right:
                    primaryAlignment = StringAlignment.Far;
                    break;
                default:
                    primaryAlignment = StringAlignment.Center;
                    break;
            }

            int padX = LogicalPixelsToDevice(fenceInfo.TitlePaddingX);
            int padY = LogicalPixelsToDevice(fenceInfo.TitlePaddingY);
            int deviceFontSize = Math.Max(8, LogicalPixelsToDevice(fenceInfo.TitleFontSize));

            using (var titleTextBrush = new SolidBrush(theme.TitleTextColor))
            {
                var previousOffsetMode = graphics.PixelOffsetMode;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;

                if (IsVerticalTitleBar)
                {
                    // 竖排标题栏：padX 为左右内边距，padY 为上下内边距
                    int effectivePadX = Math.Min(padX, Math.Max(0, (titleBounds.Width - 4) / 2));
                    int effectivePadY = Math.Min(padY, Math.Max(0, (titleBounds.Height - 4) / 2));
                    var textRect = new RectangleF(
                        titleBounds.X + effectivePadX,
                        titleBounds.Y + effectivePadY,
                        Math.Max(1, titleBounds.Width - effectivePadX * 2),
                        Math.Max(1, titleBounds.Height - effectivePadY * 2));

                    var elements = new List<string>();
                    var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(Text);
                    while (enumerator.MoveNext())
                    {
                        elements.Add(enumerator.GetTextElement());
                    }

                    if (elements.Count > 0)
                    {
                        // 行高固定为 1：字符高度与步进严格等于字体设备像素大小
                        float singleCharH = deviceFontSize;
                        float stepY = singleCharH * 1.0f;
                        float totalSpan = (elements.Count - 1) * stepY + singleCharH;

                        float startY;
                        switch (primaryAlignment)
                        {
                            case StringAlignment.Near:
                                startY = textRect.Y;
                                break;
                            case StringAlignment.Far:
                                startY = textRect.Bottom - totalSpan;
                                break;
                            default:
                                startY = textRect.Y + (textRect.Height - totalSpan) / 2f;
                                break;
                        }

                        using (var charFormat = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                            FormatFlags = StringFormatFlags.NoWrap
                        })
                        {
                            for (int i = 0; i < elements.Count; i++)
                            {
                                float charY = startY + i * stepY;
                                if (charY + singleCharH <= textRect.Y)
                                    continue;
                                if (charY >= textRect.Bottom)
                                    break;

                                // 接近底部且还有后续字符时绘制省略号
                                if (charY + stepY > textRect.Bottom && i < elements.Count - 1)
                                {
                                    var ellipsisRect = new RectangleF(textRect.X, charY, textRect.Width, singleCharH);
                                    graphics.DrawString("…", titleFont, titleTextBrush, ellipsisRect, charFormat);
                                    break;
                                }

                                var charRect = new RectangleF(textRect.X, charY, textRect.Width, singleCharH);
                                graphics.DrawString(elements[i], titleFont, titleTextBrush, charRect, charFormat);
                            }
                        }
                    }
                }
                else
                {
                    // 横排标题栏：padX 为左右内边距，padY 为上下内边距（标题栏高度由字体大小与 padY 撑开）
                    int effectivePadX = Math.Min(padX, Math.Max(0, (titleBounds.Width - 4) / 2));
                    int effectivePadY = Math.Min(padY, Math.Max(0, (titleBounds.Height - 4) / 2));
                    var textRect = new RectangleF(
                        titleBounds.X + effectivePadX,
                        titleBounds.Y + effectivePadY,
                        Math.Max(1, titleBounds.Width - effectivePadX * 2),
                        Math.Max(1, titleBounds.Height - effectivePadY * 2));

                    string[] lines = Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    if (lines.Length > 1)
                    {
                        // 行高固定为 1：每行高度与垂直步进严格等于字体设备像素大小
                        float singleLineH = deviceFontSize;
                        float stepY = singleLineH * 1.0f;
                        float totalH = (lines.Length - 1) * stepY + singleLineH;
                        float startY = textRect.Y + (textRect.Height - totalH) / 2f;

                        using (var lineFormat = new StringFormat
                        {
                            Alignment = primaryAlignment,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        })
                        {
                            for (int i = 0; i < lines.Length; i++)
                            {
                                float lineY = startY + i * stepY;
                                if (lineY + singleLineH > textRect.Bottom)
                                    break;
                                var lineRect = new RectangleF(textRect.X, lineY, textRect.Width, singleLineH);
                                graphics.DrawString(lines[i], titleFont, titleTextBrush, lineRect, lineFormat);
                            }
                        }
                    }
                    else
                    {
                        using (var titleFormat = new StringFormat
                        {
                            Alignment = primaryAlignment,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        })
                        {
                            graphics.DrawString(Text, titleFont, titleTextBrush, textRect, titleFormat);
                        }
                    }
                }

                graphics.PixelOffsetMode = previousOffsetMode;
            }
        }

        /// <summary>
        /// 渲染单个栅栏条目（图标 + 文字 + 选中/悬停高亮背景）。
        /// 当 skipText 为 true 时跳过文字绘制（选中条目文字将在循环后单独渲染到最顶层，保证 z-order）。
        /// </summary>
        private Rectangle RenderEntry(Graphics g, FenceEntry entry, int x, int y,
            Point mousePosition, Rectangle viewport, bool skipText = false)
        {
            var theme = currentTheme ?? ThemePresets.CreateDefault();
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            var textPadding = itemPadding / 3;
            // 按 Windows 桌面规则：文本左右各留 6px 安全距离（极小缩放比例下按比例收缩）
            int textInset = Math.Min(6, Math.Max(1, itemWidth / 6));
            var textPosition = new PointF(x + textInset, y + iconSize + textPadding);
            var textMaxSize = new SizeF(Math.Max(4, itemWidth - textInset * 2), textHeight);

            var outlineRect = MeasureEntryHitBounds(g, entry, new Rectangle(x, y, itemWidth, itemHeight), false);
            bool mouseOver = viewport.Contains(mousePosition) && outlineRect.Contains(mousePosition);

            if (mouseOver)
            {
                hoveringItem = entry.Path;
                hasHoverUpdated = true;
            }

            // 绘制选中/悬停背景。XP 默认悬浮无反馈，且选中效果由后续的
            // 图标染色和文字实色底组成，因此这里只处理现代整项高亮。
            // 在图标管理模式下，不使用全项高亮，而是通过专用的图标卡片高亮与勾选标记展示状态。
            bool selected = isIconManagementMode
                ? iconManagementSelection.Contains(entry.Path)
                : selectedItem == entry.Path;
            if (!skipText && theme.MenuStyle != ThemeMenuStyle.WindowsXp && !isIconManagementMode)
            {
                DrawModernDesktopItemState(
                    g,
                    outlineRect,
                    selected,
                    mouseOver,
                    theme);
            }

            // 绘制图标（居中缩放至 iconSize × iconSize）
            var iconRect = new Rectangle(x + itemWidth / 2 - iconSize / 2, y, iconSize, iconSize);
            if (isIconManagementMode)
            {
                DrawIconManagementCard(g, iconRect, selected, mouseOver, theme);
            }
            DrawDesktopIconForState(g, icon, iconRect, selected && !isIconManagementMode, theme);
            if (isIconManagementMode)
                DrawIconManagementCheck(g, iconRect, selected, theme);

            // 选中条目跳过文字绘制（将在循环后单独渲染到最顶层以保证 z-order 正确）
            if (skipText)
                return outlineRect;

            DrawDesktopIconTextForState(
                g,
                name,
                new RectangleF(textPosition, textMaxSize),
                standardIconTextFormat,
                theme,
                !isIconManagementMode && selected,
                !isIconManagementMode && mouseOver);

            return outlineRect;
        }

        /// <summary>
        /// 绘制默认和 Win11 风格的整项状态层。颜色会根据面板明暗选择规范中的
        /// 深/浅覆盖方案，高亮范围始终使用动态计算后的完整项目矩形。
        /// </summary>
        private void DrawModernDesktopItemState(
            Graphics graphics,
            Rectangle bounds,
            bool selected,
            bool hovered,
            ThemeDefinition theme)
        {
            if (!selected && !hovered)
                return;

            Color fillColor;
            Color strokeColor;
            int fillAlpha;
            int strokeAlpha;

            if (SystemInformation.HighContrast)
            {
                fillColor = SystemColors.Highlight;
                strokeColor = selected
                    ? SystemColors.HighlightText
                    : SystemColors.WindowText;
                fillAlpha = 255;
                strokeAlpha = 255;
            }
            else
            {
                bool darkSurface = ThemeDrawing.IsDark(theme.MainPanelColor);
                if (selected && hovered)
                {
                    fillColor = Color.FromArgb(95, 145, 195);
                    strokeColor = Color.FromArgb(220, 238, 252);
                    fillAlpha = 61;   // 24%
                    strokeAlpha = 148; // 58%
                }
                else if (selected)
                {
                    fillColor = darkSurface
                        ? Color.FromArgb(130, 175, 215)
                        : Color.FromArgb(45, 95, 145);
                    strokeColor = darkSurface
                        ? Color.FromArgb(220, 238, 252)
                        : Color.FromArgb(45, 95, 145);
                    fillAlpha = 46;   // 18%
                    strokeAlpha = darkSurface ? 122 : 117;
                }
                else
                {
                    fillColor = darkSurface
                        ? Color.FromArgb(110, 155, 200)
                        : Color.FromArgb(45, 80, 115);
                    strokeColor = darkSurface
                        ? Color.FromArgb(210, 230, 248)
                        : Color.FromArgb(45, 80, 115);
                    fillAlpha = 20;   // 8%
                    strokeAlpha = darkSurface ? 61 : 56;
                }
            }

            float dpiScale = IsHandleCreated
                ? Math.Max(1f, GetDpiForWindow(Handle) / 96f)
                : 1f;
            float strokeWidth = dpiScale;
            var visualBounds = new RectangleF(
                bounds.X + strokeWidth / 2f,
                bounds.Y + strokeWidth / 2f,
                Math.Max(0, bounds.Width - strokeWidth),
                Math.Max(0, bounds.Height - strokeWidth));
            using (var path = ThemeDrawing.CreateRoundedRectangle(
                visualBounds,
                dpiScale))
            using (var fill = new SolidBrush(
                ThemeDrawing.WithAlpha(fillColor, fillAlpha)))
            using (var stroke = new Pen(
                ThemeDrawing.WithAlpha(strokeColor, strokeAlpha),
                strokeWidth))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(stroke, path);
            }
        }

        /// <summary>按当前桌面视觉风格绘制普通、选中或高对比度文字。</summary>
        private void DrawDesktopIconTextForState(
            Graphics graphics,
            string text,
            RectangleF bounds,
            StringFormat format,
            ThemeDefinition theme,
            bool selected,
            bool hovered)
        {
            var snappedBounds = new RectangleF(
                (float)Math.Round(bounds.X),
                (float)Math.Round(bounds.Y),
                (float)Math.Ceiling(bounds.Width),
                (float)Math.Ceiling(bounds.Height));

            var previousOffsetMode = graphics.PixelOffsetMode;
            var previousTextHint = graphics.TextRenderingHint;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Default;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            try
            {
                if (theme.MenuStyle == ThemeMenuStyle.WindowsXp && selected)
                {
                    DrawXpSelectedLabel(graphics, text, snappedBounds);
                    return;
                }

                if (theme.MenuStyle != ThemeMenuStyle.WindowsXp &&
                    SystemInformation.HighContrast &&
                    (selected || hovered))
                {
                    using (var textBrush = new SolidBrush(SystemColors.HighlightText))
                        graphics.DrawString(text, iconFont, textBrush, snappedBounds, format);
                    return;
                }

                DrawDesktopStyleIconText(graphics, text, snappedBounds, format, theme);
            }
            finally
            {
                graphics.PixelOffsetMode = previousOffsetMode;
                graphics.TextRenderingHint = previousTextHint;
            }
        }

        /// <summary>
        /// 绘制 XP 经典选中文字：每一行使用紧贴文字宽度的不透明系统高亮色
        /// 矩形，并以系统高亮文字色绘制，不使用阴影、透明度或圆角。
        /// </summary>
        private void DrawXpSelectedLabel(
            Graphics graphics,
            string text,
            RectangleF bounds)
        {
            float lineHeight = Math.Max(1f, iconFont.GetHeight(graphics));
            int maxLines = Math.Max(1, (int)Math.Floor(bounds.Height / lineHeight));
            List<string> lines = MeasureXpLabelLines(
                graphics,
                text,
                bounds.Width,
                lineHeight,
                maxLines);
            float horizontalPadding = Math.Max(1f, LogicalPixelsToDevice(1));

            using (var background = new SolidBrush(SystemColors.Highlight))
            using (var foreground = new SolidBrush(SystemColors.HighlightText))
            using (var lineFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var measureFormat = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                measureFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                for (int index = 0; index < lines.Count; index++)
                {
                    string line = lines[index];
                    if (line.Length == 0)
                        continue;

                    float lineWidth = graphics.MeasureString(
                        line,
                        iconFont,
                        new SizeF(bounds.Width, lineHeight + 2f),
                        measureFormat).Width;
                    float backgroundWidth = Math.Min(
                        bounds.Width,
                        (float)Math.Ceiling(lineWidth) + horizontalPadding * 2f);
                    float lineY = (float)Math.Round(bounds.Y + index * lineHeight);
                    graphics.FillRectangle(
                        background,
                        (float)Math.Round(bounds.X + (bounds.Width - backgroundWidth) / 2f),
                        lineY,
                        backgroundWidth,
                        (float)Math.Ceiling(lineHeight));
                    graphics.DrawString(
                        line,
                        iconFont,
                        foreground,
                        new RectangleF(bounds.X, lineY, bounds.Width, lineHeight + 1f),
                        lineFormat);
                }
            }
        }

        /// <summary>按标签宽度和可用行数拆分 XP 选中文字，并在截断处添加省略号。</summary>
        private List<string> MeasureXpLabelLines(
            Graphics graphics,
            string text,
            float maximumWidth,
            float lineHeight,
            int maximumLines)
        {
            var lines = new List<string>();
            string remaining = text ?? string.Empty;
            using (var fitFormat = new StringFormat
            {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.LineLimit
            })
            {
                while (remaining.Length > 0 && lines.Count < maximumLines)
                {
                    int charactersFitted;
                    int linesFilled;
                    graphics.MeasureString(
                        remaining,
                        iconFont,
                        new SizeF(maximumWidth, lineHeight * 1.5f),
                        fitFormat,
                        out charactersFitted,
                        out linesFilled);
                    if (charactersFitted <= 0)
                        charactersFitted = 1;

                    charactersFitted = Math.Min(charactersFitted, remaining.Length);
                    string line = remaining.Substring(0, charactersFitted)
                        .TrimEnd(' ', '\t', '\r', '\n');
                    remaining = remaining.Substring(charactersFitted)
                        .TrimStart(' ', '\t', '\r', '\n');
                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
                lines.Add(string.Empty);
            if (remaining.Length > 0)
            {
                int lastIndex = lines.Count - 1;
                lines[lastIndex] = FitXpEllipsis(
                    graphics,
                    lines[lastIndex],
                    maximumWidth,
                    lineHeight);
            }

            return lines;
        }

        /// <summary>缩短 XP 标签末行，确保省略号仍处于标签最大宽度内。</summary>
        private string FitXpEllipsis(
            Graphics graphics,
            string line,
            float maximumWidth,
            float lineHeight)
        {
            string content = line.TrimEnd();
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                while (content.Length > 0)
                {
                    string candidate = content + "…";
                    SizeF size = graphics.MeasureString(
                        candidate,
                        iconFont,
                        new SizeF(maximumWidth, lineHeight + 2f),
                        format);
                    if (size.Width <= maximumWidth)
                        return candidate;
                    content = content.Substring(0, content.Length - 1).TrimEnd();
                }
            }

            return "…";
        }

        /// <summary>
        /// 以整像素对齐和一致的网格拟合提示（AntiAliasGridFit）绘制桌面图标文字与右下投影，
        /// 避免半像素偏移、环形弥散阴影或阴影/正文渲染模式不一致导致中文字形发虚重影。
        /// </summary>
        private void DrawDesktopStyleIconText(
            Graphics graphics,
            string text,
            RectangleF bounds,
            StringFormat format,
            ThemeDefinition theme)
        {
            float dpiScale = IsHandleCreated
                ? Math.Max(1f, GetDpiForWindow(Handle) / 96f)
                : 1f;
            float primaryOffset = Math.Max(1f, (float)Math.Round(1f * dpiScale));
            bool isLightText = !ThemeDrawing.IsDark(theme.ItemTextColor);

            if (isLightText)
            {
                using (var softShadowBrush = new SolidBrush(
                    ThemeDrawing.WithAlpha(theme.ItemTextShadowColor, 48)))
                using (var mainShadowBrush = new SolidBrush(
                    ThemeDrawing.WithAlpha(theme.ItemTextShadowColor, 150)))
                {
                    DrawTextShadowSample(graphics, text, softShadowBrush, bounds, format,
                        primaryOffset, 0f);
                    DrawTextShadowSample(graphics, text, softShadowBrush, bounds, format,
                        0f, primaryOffset);
                    DrawTextShadowSample(graphics, text, mainShadowBrush, bounds, format,
                        primaryOffset, primaryOffset);

                    if (dpiScale >= 1.25f)
                    {
                        float secondaryOffset = primaryOffset + 1f;
                        DrawTextShadowSample(graphics, text, softShadowBrush, bounds, format,
                            secondaryOffset, secondaryOffset);
                    }
                }
            }
            else
            {
                using (var subtleHighlightBrush = new SolidBrush(
                    ThemeDrawing.WithAlpha(theme.ItemTextShadowColor, 72)))
                {
                    DrawTextShadowSample(graphics, text, subtleHighlightBrush, bounds, format,
                        primaryOffset, primaryOffset);
                }
            }

            using (var textBrush = new SolidBrush(theme.ItemTextColor))
                graphics.DrawString(text, iconFont, textBrush, bounds, format);
        }

        /// <summary>在指定偏移位置绘制一次低透明度文字阴影样本。</summary>
        private void DrawTextShadowSample(
            Graphics graphics,
            string text,
            Brush brush,
            RectangleF bounds,
            StringFormat format,
            float offsetX,
            float offsetY)
        {
            graphics.DrawString(
                text,
                iconFont,
                brush,
                new RectangleF(
                    bounds.X + offsetX,
                    bounds.Y + offsetY,
                    bounds.Width,
                    bounds.Height),
                format);
        }

        /// <summary>在图标管理模式下为条目图标绘制独立的圆角卡片选择和悬停高亮框。</summary>
        private void DrawIconManagementCard(
            Graphics graphics,
            Rectangle iconBounds,
            bool selected,
            bool hovered,
            ThemeDefinition theme)
        {
            if (!selected && !hovered)
                return;

            int padding = Math.Max(LogicalPixelsToDevice(5), iconBounds.Width / 8);
            var cardBounds = Rectangle.Inflate(iconBounds, padding, padding);
            int radius = Math.Max(LogicalPixelsToDevice(6), LogicalPixelsToDevice(theme.CornerRadius > 0 ? Math.Min(8, theme.CornerRadius) : 6));
            bool isDark = ThemeDrawing.IsDark(theme.MainPanelColor);

            using (var path = ThemeDrawing.CreateRoundedRectangle(cardBounds, radius))
            {
                if (selected)
                {
                    Color fillColor = ThemeDrawing.WithAlpha(theme.AccentColor, isDark ? 45 : 35);
                    using (var fillBrush = new SolidBrush(fillColor))
                    using (var strokePen = new Pen(theme.AccentColor, Math.Max(2f, LogicalPixelsToDevice(2))))
                    {
                        graphics.FillPath(fillBrush, path);
                        graphics.DrawPath(strokePen, path);
                    }
                }
                else if (hovered)
                {
                    Color fillColor = isDark
                        ? Color.FromArgb(25, 255, 255, 255)
                        : Color.FromArgb(15, 0, 0, 0);
                    Color strokeColor = isDark
                        ? Color.FromArgb(80, 255, 255, 255)
                        : Color.FromArgb(50, 0, 0, 0);
                    using (var fillBrush = new SolidBrush(fillColor))
                    using (var strokePen = new Pen(strokeColor, Math.Max(1.5f, (float)LogicalPixelsToDevice(1))))
                    {
                        graphics.FillPath(fillBrush, path);
                        graphics.DrawPath(strokePen, path);
                    }
                }
            }
        }

        /// <summary>在管理模式下为每个图标绘制可多选的圆形勾选标记。</summary>
        private void DrawIconManagementCheck(
            Graphics graphics,
            Rectangle iconBounds,
            bool selected,
            ThemeDefinition theme)
        {
            int padding = Math.Max(LogicalPixelsToDevice(5), iconBounds.Width / 8);
            var cardBounds = Rectangle.Inflate(iconBounds, padding, padding);
            int badgeSize = Math.Max(LogicalPixelsToDevice(18), (int)(iconBounds.Width * 0.36f));
            var badgeRect = new Rectangle(
                cardBounds.Right - badgeSize + LogicalPixelsToDevice(2),
                cardBounds.Top - LogicalPixelsToDevice(2),
                badgeSize,
                badgeSize);

            bool isDark = ThemeDrawing.IsDark(theme.MainPanelColor);

            if (selected)
            {
                using (var fillBrush = new SolidBrush(theme.AccentColor))
                using (var pen = new Pen(Color.White, Math.Max(1.8f, (float)LogicalPixelsToDevice(2))))
                {
                    graphics.FillEllipse(fillBrush, badgeRect);

                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

                    var pts = new[]
                    {
                        new PointF(badgeRect.Left + badgeSize * 0.28f, badgeRect.Top + badgeSize * 0.52f),
                        new PointF(badgeRect.Left + badgeSize * 0.46f, badgeRect.Top + badgeSize * 0.72f),
                        new PointF(badgeRect.Left + badgeSize * 0.76f, badgeRect.Top + badgeSize * 0.32f)
                    };
                    graphics.DrawLines(pen, pts);
                }
            }
            else
            {
                Color fillColor = isDark
                    ? Color.FromArgb(35, 255, 255, 255)
                    : Color.FromArgb(220, 255, 255, 255);
                Color strokeColor = isDark
                    ? Color.FromArgb(140, 255, 255, 255)
                    : Color.FromArgb(120, 160, 180);

                using (var fillBrush = new SolidBrush(fillColor))
                using (var pen = new Pen(strokeColor, Math.Max(1.5f, (float)LogicalPixelsToDevice(1))))
                {
                    graphics.FillEllipse(fillBrush, badgeRect);
                    graphics.DrawEllipse(pen, badgeRect);
                }
            }
        }

        /// <summary>返回当前 DPI 下图标管理底栏的设备像素高度。</summary>
        private int GetIconManagementFooterHeight()
        {
            return LogicalPixelsToDevice(48);
        }

        /// <summary>绘制图标管理模式底栏的确认、取消按钮和选择数量提示。</summary>
        private void DrawIconManagementFooter(
            Graphics graphics,
            ThemeDefinition theme,
            Rectangle contentViewport,
            int footerHeight)
        {
            if (footerHeight <= 0 || contentViewport.Width <= 0)
                return;

            int footerTop = contentViewport.Bottom;
            var footerBounds = new Rectangle(
                contentViewport.X,
                footerTop,
                contentViewport.Width,
                footerHeight);

            bool isDark = ThemeDrawing.IsDark(theme.MainPanelColor);

            Color footerBg = isDark
                ? Color.FromArgb(245, 20, 28, 36)
                : Color.FromArgb(250, 245, 248, 252);
            Color sepColor = isDark
                ? Color.FromArgb(40, 255, 255, 255)
                : Color.FromArgb(25, 0, 0, 0);

            using (var footerBrush = new SolidBrush(footerBg))
            using (var separatorPen = new Pen(sepColor, 1f))
            {
                graphics.FillRectangle(footerBrush, footerBounds);
                graphics.DrawLine(separatorPen, footerBounds.Left, footerTop, footerBounds.Right, footerTop);
            }

            int margin = LogicalPixelsToDevice(14);
            int gap = LogicalPixelsToDevice(8);
            int buttonHeight = Math.Min(
                LogicalPixelsToDevice(32),
                Math.Max(1, footerBounds.Height - LogicalPixelsToDevice(10)));
            int cancelWidth = LogicalPixelsToDevice(76);
            int confirmWidth = LogicalPixelsToDevice(76);

            int availableWidth = Math.Max(2, footerBounds.Width - margin * 2 - gap);
            if (cancelWidth + confirmWidth > availableWidth)
            {
                cancelWidth = Math.Max(1, availableWidth / 2);
                confirmWidth = Math.Max(1, availableWidth - cancelWidth);
            }

            int buttonTop = footerTop + (footerBounds.Height - buttonHeight) / 2;

            iconManagementCancelBounds = new Rectangle(
                Math.Max(footerBounds.Left, footerBounds.Right - margin - cancelWidth),
                buttonTop,
                cancelWidth,
                buttonHeight);
            iconManagementConfirmBounds = new Rectangle(
                Math.Max(footerBounds.Left, iconManagementCancelBounds.Left - gap - confirmWidth),
                buttonTop,
                confirmWidth,
                buttonHeight);

            Point mouse = PointToClient(MousePosition);
            bool canConfirm = iconManagementSelection.Count > 0;
            DrawIconManagementButton(
                graphics,
                iconManagementConfirmBounds,
                ThemeText.Confirm,
                canConfirm,
                iconManagementConfirmBounds.Contains(mouse),
                true,
                theme);
            DrawIconManagementButton(
                graphics,
                iconManagementCancelBounds,
                ThemeText.Cancel,
                true,
                iconManagementCancelBounds.Contains(mouse),
                false,
                theme);

            int hintLeft = footerBounds.Left + margin;
            int hintRight = iconManagementConfirmBounds.Left - LogicalPixelsToDevice(10);
            if (hintRight > hintLeft)
            {
                string hint = iconManagementSelection.Count > 0
                    ? ThemeText.SelectedItemCount(iconManagementSelection.Count)
                    : ThemeText.IconManagementHint;
                var hintBounds = new RectangleF(
                    hintLeft,
                    footerTop,
                    hintRight - hintLeft,
                    footerBounds.Height);

                Color hintColor = isDark
                    ? Color.White
                    : theme.AccentColor;

                Font fontToUse = uiBoldFont ?? iconFont ?? Font;
                using (var hintBrush = new SolidBrush(hintColor))
                using (var sf = new StringFormat(StringFormat.GenericTypographic))
                {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    graphics.DrawString(hint, fontToUse, hintBrush, hintBounds, sf);
                }
            }
        }

        /// <summary>按当前主题绘制图标管理底栏中的单个操作按钮。</summary>
        private void DrawIconManagementButton(
            Graphics graphics,
            Rectangle bounds,
            string text,
            bool enabled,
            bool hovered,
            bool isConfirm,
            ThemeDefinition theme)
        {
            bool isDark = ThemeDrawing.IsDark(theme.MainPanelColor);

            Color fillColor;
            Color borderColor;
            Color textColor;

            if (isConfirm)
            {
                if (!enabled)
                {
                    if (isDark)
                    {
                        fillColor = Color.FromArgb(35, 42, 50);
                        borderColor = Color.FromArgb(55, 64, 72);
                        textColor = Color.FromArgb(120, 130, 140);
                    }
                    else
                    {
                        fillColor = Color.FromArgb(232, 236, 240);
                        borderColor = Color.FromArgb(216, 220, 224);
                        textColor = Color.FromArgb(160, 168, 176);
                    }
                }
                else
                {
                    if (isDark)
                    {
                        fillColor = hovered ? Color.FromArgb(74, 86, 100) : Color.FromArgb(58, 68, 78);
                        borderColor = hovered ? Color.FromArgb(112, 130, 148) : Color.FromArgb(95, 110, 125);
                        textColor = Color.White;
                    }
                    else
                    {
                        fillColor = hovered
                            ? ThemeDrawing.Mix(theme.AccentColor, Color.Black, 0.12f)
                            : theme.AccentColor;
                        borderColor = fillColor;
                        textColor = Color.White;
                    }
                }
            }
            else // Cancel button
            {
                if (isDark)
                {
                    fillColor = hovered ? Color.FromArgb(48, 62, 76) : Color.FromArgb(36, 48, 60);
                    borderColor = hovered ? Color.FromArgb(79, 98, 116) : Color.FromArgb(62, 78, 94);
                    textColor = Color.White;
                }
                else
                {
                    fillColor = hovered ? Color.FromArgb(240, 244, 248) : Color.White;
                    borderColor = hovered ? Color.FromArgb(184, 198, 212) : Color.FromArgb(205, 215, 225);
                    textColor = Color.FromArgb(45, 52, 60);
                }
            }

            int radius = LogicalPixelsToDevice(6);
            using (var path = ThemeDrawing.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(fillColor))
            using (var pen = new Pen(borderColor, 1f))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }

            Font fontToUse = uiBoldFont ?? iconFont ?? Font;
            using (var textBrush = new SolidBrush(textColor))
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(text, fontToUse, textBrush, (RectangleF)bounds, sf);
            }
        }

        /// <summary>
        /// 使用带 Alpha 通道的位图和高质量双三次插值绘制图标，
        /// 避免 DrawIcon 缩放图标时退化为邻近取样产生锯齿或模糊。
        /// </summary>
        private void DrawIconHighQuality(Graphics graphics, Icon icon, Rectangle targetRectangle)
        {
            if (icon == null || targetRectangle.Width <= 0 || targetRectangle.Height <= 0)
                return;

            if (icon.Width == targetRectangle.Width && icon.Height == targetRectangle.Height)
            {
                graphics.DrawIcon(icon, targetRectangle);
                return;
            }

            var bitmap = thumbnailProvider.GetBitmap(icon);
            if (bitmap != null)
                graphics.DrawImage(bitmap, targetRectangle);
        }

        /// <summary>按当前桌面风格绘制原色图标或 XP 经典选中染色图标。</summary>
        private void DrawDesktopIconForState(
            Graphics graphics,
            Icon icon,
            Rectangle targetRectangle,
            bool selected,
            ThemeDefinition theme)
        {
            if (selected && theme.MenuStyle == ThemeMenuStyle.WindowsXp)
            {
                DrawXpSelectedIcon(graphics, icon, targetRectangle);
                return;
            }

            DrawIconHighQuality(graphics, icon, targetRectangle);
        }

        /// <summary>
        /// 将 XP 选中图标与系统 Highlight 色按 50% 混合，同时保持源 Alpha，
        /// 使透明边缘和图标轮廓仍然清晰可辨。
        /// </summary>
        private void DrawXpSelectedIcon(
            Graphics graphics,
            Icon icon,
            Rectangle targetRectangle)
        {
            if (icon == null)
                return;

            Color highlight = SystemColors.Highlight;
            float red = highlight.R / 255f * 0.5f;
            float green = highlight.G / 255f * 0.5f;
            float blue = highlight.B / 255f * 0.5f;
            var colorMatrix = new ColorMatrix(new[]
            {
                new[] { 0.5f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0.5f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0.5f, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { red, green, blue, 0f, 1f }
            });

            var bitmap = thumbnailProvider.GetBitmap(icon);
            if (bitmap == null)
                return;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(
                    colorMatrix,
                    ColorMatrixFlag.Default,
                    ColorAdjustType.Bitmap);
                graphics.DrawImage(
                    bitmap,
                    targetRectangle,
                    0,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        /// <summary>重命名栅栏。</summary>
        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new EditDialog(Text);
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Text = dialog.NewName;
                fenceInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        /// <summary>创建新栅栏。</summary>
        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence(ThemeText.NewFenceName);
        }

        /// <summary>打开全局主题配置面板。</summary>
        private void themeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new ThemeConfigurationDialog())
            {
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ShowDialog(this);
            }
        }

        /// <summary>
        /// 独立切换应用颜色模式。这里只更新 DarkModeEnabled；当前 Win11、XP
        /// 或自定义风格 ID 保持不变，由 ThemeManager 解析该风格对应的颜色变体。
        /// </summary>
        private void darkModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveGlobalSetting(() => ThemeManager.Instance.SetDarkMode(darkModeToolStripMenuItem.Checked));
        }

        /// <summary>独立切换全局桌面分区移动自动吸附开关。</summary>
        private void autoSnapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveGlobalSetting(() => AppSettingsManager.Instance.SetAutoSnap(autoSnapToolStripMenuItem.Checked));
        }

        /// <summary>显示全局设置写盘失败，并将菜单勾选恢复到实际生效的状态。</summary>
        private void SaveGlobalSetting(Action save)
        {
            try { save(); }
            catch (Exception ex)
            {
                darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
                autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;
                MessageBox.Show(this, ThemeText.PersistenceFailed + Environment.NewLine + ex.Message,
                    ThemeText.PersistenceTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>窗口关闭：如果最后一个栅栏关闭则退出应用。</summary>
        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (interactedFence == this)
                interactedFence = null;
            isContextMenuInteraction = false;
            UninstallInteractionMouseHook();
            UninstallInteractionForegroundHook();
            StopMinifyAnimation();
            if (minifyAnimationTimer != null)
            {
                minifyAnimationTimer.Tick -= MinifyAnimationTimer_Tick;
                minifyAnimationTimer.Dispose();
                minifyAnimationTimer = null;
            }

            if (fenceRemoved || !AppSettingsManager.Instance.AutoSaveEnabled)
            {
                throttledMove.Cancel();
                throttledResize.Cancel();
            }
            else
            {
                throttledMove.Flush();
                throttledResize.Flush();
            }
            throttledMove.Dispose();
            throttledResize.Dispose();

            ThemeManager.Instance.ThemeChanged -= ThemeManager_ThemeChanged;
            AppSettingsManager.Instance.SettingsChanged -= AppSettingsManager_SettingsChanged;
            thumbnailProvider.IconThumbnailLoaded -= ThumbnailProvider_IconThumbnailLoaded;
            thumbnailProvider.Dispose();
            entrySnapshot.Clear();
            iconMetricsPollTimer.Stop();
            iconMetricsPollTimer.Dispose();
            shellContextMenu.MenuClosed -= ShellContextMenu_MenuClosed;
            shellContextMenu.Dispose();
            if (managedItemsWatcher != null)
            {
                managedItemsWatcher.EnableRaisingEvents = false;
                managedItemsWatcher.Dispose();
                managedItemsWatcher = null;
            }

            backgroundImage?.Dispose();
            backgroundImage = null;
            backgroundImagePath = null;
            backgroundImageVersion = 0;
            ClearContextMenuIcons();
            if (scaleMenuControl != null)
            {
                scaleMenuControl.ScaleChanged -= ScaleMenuControl_ScaleChanged;
                scaleMenuControl.Dispose();
                scaleMenuControl = null;
            }
            titleFont?.Dispose();
            titleFont = null;
            iconFont?.Dispose();
            iconFont = null;
            uiBoldFont?.Dispose();
            uiBoldFont = null;
            standardIconTextFormat.Dispose();
            expandedIconTextFormat.Dispose();

            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        private readonly object saveLock = new object();
        /// <summary>保存栅栏数据到磁盘（线程安全）。</summary>
        private void Save()
        {
            try
            {
                lock (saveLock)
                {
                    if (fenceInfo != null && currentDpi > 0)
                    {
                        fenceInfo.LastDpi = currentDpi;
                    }
                    FenceManager.Instance.UpdateFence(fenceInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Fence persistence failed: " + ex);
                if (!persistenceWarningShown && !IsDisposed && !Disposing)
                {
                    persistenceWarningShown = true;
                    MessageBox.Show(
                        this,
                        ThemeText.PersistenceFailed + Environment.NewLine + Environment.NewLine + ex.Message,
                        ThemeText.PersistenceTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>窗口位置变化：节流保存新坐标（4 秒延迟）。</summary>
        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            if (isInitializing || isUpdatingMinifyBounds || isAnimatingMinify)
                return;

            if (!hasMoveOrigin)
            {
                CheckAndHandleDpiChange();
            }

            ScheduleBoundsSave(throttledMove);
        }

        /// <summary>仅在自动保存启用时排队和执行布局保存，文件搬移事务仍独立强制写盘。</summary>
        private void ScheduleBoundsSave(ThrottledExecution execution)
        {
            if (!AppSettingsManager.Instance.AutoSaveEnabled)
                return;
            execution.Run(() =>
            {
                if (!AppSettingsManager.Instance.AutoSaveEnabled)
                    return;
                Rectangle expanded = GetCurrentExpandedBounds();
                fenceInfo.PosX = expanded.X;
                fenceInfo.PosY = expanded.Y;
                fenceInfo.Width = expanded.Width;
                fenceInfo.Height = expanded.Height;
                fenceInfo.LastDpi = currentDpi;
                Save();
            });
        }

        /// <summary>切换锁定状态。</summary>
        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        /// <summary>打开标题设置对话框，支持实时预览；确定则保存修改，取消则恢复修改前的设置。</summary>
        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 备份修改前的初始状态，供取消时回滚
            int origFontSize = fenceInfo.TitleFontSize;
            int origHeight = fenceInfo.TitleHeight;
            FenceTitlePosition origPosition = fenceInfo.TitlePosition;
            FenceTitleTextAlignment origAlignment = fenceInfo.TitleTextAlignment;
            int origPaddingX = fenceInfo.TitlePaddingX;
            int origPaddingY = fenceInfo.TitlePaddingY;

            bool wasMinified = isMinified;
            if (wasMinified)
                ExpandFromMinified(animate: false);

            using (var dialog = new HeightDialog(
                fenceInfo.TitleFontSize,
                fenceInfo.TitlePosition,
                fenceInfo.TitleTextAlignment,
                fenceInfo.TitlePaddingX,
                fenceInfo.TitlePaddingY))
            {
                dialog.StartPosition = FormStartPosition.CenterParent;

                // 交互时实时预览变更
                dialog.PreviewChanged += (pos, align, fontSize, h, padX, padY) =>
                {
                    ApplyTitleSettings(pos, align, fontSize, h, padX, padY);
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // 确认保存修改
                    ApplyTitleSettings(
                        dialog.TitlePosition,
                        dialog.TitleTextAlignment,
                        dialog.TitleFontSize,
                        dialog.TitleHeight,
                        dialog.TitlePaddingX,
                        dialog.TitlePaddingY);
                    if (wasMinified || (minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition)))
                        Minify(animate: false);
                    Save();
                }
                else
                {
                    // 取消或直接关闭：完全恢复修改前的初始配置
                    ApplyTitleSettings(origPosition, origAlignment, origFontSize, origHeight, origPaddingX, origPaddingY);
                    if (wasMinified || (minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition)))
                        Minify(animate: false);
                    Refresh();
                }
            }
        }

        /// <summary>应用标题栏配置并根据标题文本大小与内边距自动撑开标题栏尺寸，刷新界面渲染。</summary>
        private void ApplyTitleSettings(
            FenceTitlePosition position,
            FenceTitleTextAlignment alignment,
            int fontSize,
            int height,
            int padX,
            int padY)
        {
            fenceInfo.TitlePosition = position;
            fenceInfo.TitleTextAlignment = alignment;
            fenceInfo.TitleFontSize = fontSize;
            fenceInfo.TitlePaddingX = padX;
            fenceInfo.TitlePaddingY = padY;

            int adaptedHeight = HeightDialog.CalculateAdaptedTitleHeight(position, fontSize, padX, padY);
            logicalTitleHeight = adaptedHeight;
            fenceInfo.TitleHeight = logicalTitleHeight;
            titleHeight = LogicalPixelsToDevice(logicalTitleHeight);

            ReloadFonts();
            Refresh();
        }

        /// <summary>
        /// 鼠标右键点击处理。
        /// - 右键悬停任意条目：显示原生 Shell 上下文菜单
        /// - Shift+右键或右键空白区域：显示应用自身的上下文菜单
        /// 图标管理模式只响应左键多选及底栏按钮，避免误触打开或右键命令。
        /// </summary>
        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (isIconManagementMode)
            {
                if (e.Button == MouseButtons.Left)
                    HandleIconManagementClick(e.Location);
                return;
            }

            if (e.Button != MouseButtons.Right)
                return;

            // 原生 Shell 菜单会进入自己的模态循环；打开前确保当前分区位于前面。
            KeepInteractedFenceOnTop();

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                // 右键条目 → Windows Shell 右键菜单
                string shellItem = hoveringItem;
                bool wasManaged = FenceManager.Instance.IsManagedDesktopEntry(
                    fenceInfo,
                    shellItem);
                try
                {
                    BeginContextMenuInteraction();
                    try
                    {
                        shellContextMenu.ShowContextMenu(
                            new[] { new FileInfo(shellItem) },
                            MousePosition);
                    }
                    finally
                    {
                        EndContextMenuInteraction(keepInFront: false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        ThemeText.NativeMenuFailed + Environment.NewLine + Environment.NewLine + ex.Message,
                        ThemeText.NativeMenuTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (wasManaged)
                {
                    string reconcileError;
                    bool reconciled = FenceManager.Instance.ReconcileManagedEntryAfterShellCommand(
                        fenceInfo,
                        shellItem,
                        out reconcileError);
                    if (!reconciled)
                    {
                        MessageBox.Show(
                            this,
                            ThemeText.NativeMenuSyncFailed + Environment.NewLine + Environment.NewLine +
                            reconcileError,
                            ThemeText.NativeMenuTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    else if (!File.Exists(shellItem))
                    {
                        InvalidateEntry(shellItem);
                    }
                    selectedItem = null;
                    hoveringItem = null;
                    Refresh();
                }
            }
            else
            {
                // Shift+右键或空白处 → 应用菜单
                ShowAppContextMenu(e.Location);
            }
        }

        /// <summary>鼠标滚轮：控制栅栏内容垂直滚动。</summary>
        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * LogicalPixelsToDevice(10);
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            Invalidate();
        }

        /// <summary>缩略图异步加载完成 → 触发重绘。</summary>
        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(Invalidate));
                }
                catch (InvalidOperationException)
                {
                    // 窗口可能在后台提取完成前已关闭
                }
                return;
            }

            Invalidate();
        }

        /// <summary>
        /// 鼠标按下：检测是否在图标上按下左键以准备手动拖拽操作。
        /// </summary>
        private void FenceWindow_MouseDown(object sender, MouseEventArgs e)
        {
            KeepInteractedFenceOnTop();
            if (e.Button != MouseButtons.Left || isIconManagementMode || lockedToolStripMenuItem.Checked)
                return;

            string hitPath = HitTestEntry(e.Location);

            if (!string.IsNullOrEmpty(hitPath))
            {
                dragCandidatePath = hitPath;
                dragStartPoint = e.Location;
                isDraggingItem = false;
                suppressNextClick = false;
            }
            else
            {
                dragCandidatePath = null;
            }
        }

        /// <summary>
        /// 鼠标释放：提交手动拖拽结果（同分区排序、跨分区移动、拖出分区移除）。
        /// </summary>
        private void FenceWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (isDraggingItem)
            {
                Capture = false;
                isDraggingItem = false;
                Cursor = Cursors.Default;
                string draggedPath = dragCandidatePath;
                dragCandidatePath = null;

                int targetDropIndex = dropIndicatorIndex;
                ClearDropIndicator();

                FenceWindow targetFence = dragHoverTarget;
                if (targetFence != null && targetFence != this)
                {
                    targetDropIndex = targetFence.dropIndicatorIndex;
                    targetFence.ClearDropIndicator();
                }
                dragHoverTarget = null;

                if (!string.IsNullOrEmpty(draggedPath))
                {
                    if (targetFence == this)
                    {
                        // Case A: 同一分区内手动拖拽排序
                        string error;
                        if (FenceManager.Instance.TryReorderEntry(fenceInfo, draggedPath, targetDropIndex, out error))
                        {
                            Refresh();
                        }
                        else if (!string.IsNullOrEmpty(error))
                        {
                            MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else if (targetFence != null && !targetFence.IsDisposed)
                    {
                        // Case B: 拖动到其他分区移动图标
                        string error;
                        if (FenceManager.Instance.TryMoveEntry(
                            this.fenceInfo,
                            targetFence.fenceInfo,
                            draggedPath,
                            targetDropIndex,
                            out error))
                        {
                            if (selectedItem == draggedPath) selectedItem = null;
                            if (hoveringItem == draggedPath) hoveringItem = null;
                            InvalidateEntry(draggedPath);
                            this.Refresh();
                            targetFence.Refresh();
                        }
                        else if (!string.IsNullOrEmpty(error))
                        {
                            MessageBox.Show(
                                this,
                                error,
                                ThemeText.Key(NoFences.Localization.LKeys.MoveItemTitle),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        // Case C: 拖出分区（外部/桌面释放）→ 从分区移除，受托管文件自动恢复到原桌面位置
                        string error;
                        if (FenceManager.Instance.TryRemoveEntry(fenceInfo, draggedPath, out error))
                        {
                            InvalidateEntry(draggedPath);
                            if (selectedItem == draggedPath) selectedItem = null;
                            if (hoveringItem == draggedPath) hoveringItem = null;
                            Refresh();
                        }
                        else if (!string.IsNullOrEmpty(error))
                        {
                            MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }

                if (minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition))
                {
                    Minify();
                }

                Invalidate();
                return;
            }

            dragCandidatePath = null;
        }

        /// <summary>拦截键盘消息：按下 Esc 键时取消当前图标拖拽。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && isDraggingItem)
            {
                CancelItemDrag();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>取消当前正在进行的图标拖拽操作并重置所有指示状态。</summary>
        private void CancelItemDrag()
        {
            if (isDraggingItem)
            {
                Capture = false;
                isDraggingItem = false;
                Cursor = Cursors.Default;
                dragCandidatePath = null;
                ClearDropIndicator();
                if (dragHoverTarget != null && dragHoverTarget != this)
                {
                    dragHoverTarget.ClearDropIndicator();
                }
                dragHoverTarget = null;
                Invalidate();
            }
        }

        /// <summary>处理图标拖拽移动过程中的目标判定、跨分区悬停与插入指示线计算。</summary>
        private void HandleItemDragMove(Point localPoint)
        {
            Point screenPoint = Cursor.Position;
            FenceWindow target = FindFenceAtScreenPoint(screenPoint);

            // 若目标分区处于锁定状态，则不接受拖放放置
            if (target != null && target.lockedToolStripMenuItem.Checked)
            {
                target = null;
            }

            if (target == this)
            {
                if (dragHoverTarget != null && dragHoverTarget != this)
                {
                    dragHoverTarget.ClearDropIndicator();
                }
                dragHoverTarget = this;
                Point clientPt = PointToClient(screenPoint);
                int targetIdx = GetDropTargetIndex(clientPt);
                SetDropTargetIndex(targetIdx);
            }
            else if (target != null)
            {
                if (dragHoverTarget != null && dragHoverTarget != target)
                {
                    dragHoverTarget.ClearDropIndicator();
                }
                ClearDropIndicator();
                dragHoverTarget = target;
                if (target.minifyToolStripMenuItem.Checked && target.isMinified)
                {
                    target.ExpandFromMinified();
                }
                Point clientPt = target.PointToClient(screenPoint);
                int targetIdx = target.GetDropTargetIndex(clientPt);
                target.SetDropTargetIndex(targetIdx);
            }
            else
            {
                if (dragHoverTarget != null)
                {
                    dragHoverTarget.ClearDropIndicator();
                    dragHoverTarget = null;
                }
                ClearDropIndicator();
            }

            Invalidate();
        }

        /// <summary>根据屏幕全局坐标查找位于光标下方的栅栏窗口（重叠时优先选取 Z 轴顶层窗口）。</summary>
        private FenceWindow FindFenceAtScreenPoint(Point screenPoint)
        {
            IntPtr hwnd = WindowFromPoint(new POINT(screenPoint.X, screenPoint.Y));
            if (hwnd != IntPtr.Zero)
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form is FenceWindow fence &&
                        !fence.IsDisposed &&
                        !fence.fenceRemoved &&
                        fence.Visible &&
                        fence.Handle == hwnd)
                    {
                        return fence;
                    }
                }
            }

            if (Bounds.Contains(screenPoint))
                return this;

            foreach (Form form in Application.OpenForms)
            {
                if (form is FenceWindow fence &&
                    fence != this &&
                    fence.Visible &&
                    !fence.IsDisposed &&
                    !fence.fenceRemoved &&
                    fence.Bounds.Contains(screenPoint))
                {
                    return fence;
                }
            }
            return null;
        }

        /// <summary>根据客户区坐标计算当前图标网格中的插入位置索引（0..Files.Count）。</summary>
        public int GetDropTargetIndex(Point clientPoint)
        {
            if (fenceInfo?.Files == null || fenceInfo.Files.Count == 0)
                return 0;

            int managementFooterHeight = isIconManagementMode ? GetIconManagementFooterHeight() : 0;
            Rectangle contentViewport = GetContentViewport(managementFooterHeight);
            IconGridLayout grid = PrepareIconGridLayout(contentViewport);
            return entrySnapshot.ToSourceInsertionIndex(grid.FindInsertionIndex(clientPoint, entrySnapshot.Count));
        }

        /// <summary>同步列表并在布局使用之前校正滚动位置，避免条目减少后的首帧空白。</summary>
        private IconGridLayout PrepareIconGridLayout(Rectangle contentViewport)
        {
            if (entrySnapshot.Synchronize(fenceInfo.Files))
                entryHitBounds.Clear();
            IconGridLayout grid = CreateIconGridLayout(contentViewport);
            scrollHeight = grid.GetScrollHeight(entrySnapshot.Count);
            int correctedOffset = grid.ClampScrollOffset(entrySnapshot.Count);
            if (correctedOffset != scrollOffset)
            {
                scrollOffset = correctedOffset;
                entryHitBounds.Clear();
                grid = CreateIconGridLayout(contentViewport);
            }
            return grid;
        }

        /// <summary>为绘制和拖拽复用同一网格，保留纵向标题栏折叠期间的布局宽度。</summary>
        private IconGridLayout CreateIconGridLayout(Rectangle contentViewport)
        {
            int layoutViewportWidth = (isMinified || isAnimatingMinify) && IsVerticalTitleBar
                ? Math.Max(contentViewport.Width, prevWidth - titleHeight)
                : contentViewport.Width;
            return new IconGridLayout(contentViewport, layoutViewportWidth,
                itemWidth, itemHeight, itemPadding, scrollOffset);
        }

        /// <summary>设置本分区在拖拽悬停时的目标插入位置指示线索引。</summary>
        public void SetDropTargetIndex(int index)
        {
            if (dropIndicatorIndex != index)
            {
                dropIndicatorIndex = index;
                Invalidate();
            }
        }

        /// <summary>清除本分区的拖拽插入指示线并触发重绘。</summary>
        public void ClearDropIndicator()
        {
            if (dropIndicatorIndex != -1)
            {
                dropIndicatorIndex = -1;
                Invalidate();
            }
        }

        /// <summary>在图标网格中绘制高亮插入指示线。</summary>
        private void DrawDropIndicator(Graphics graphics, ThemeDefinition theme, Rectangle contentViewport, IconGridLayout grid)
        {
            if (dropIndicatorIndex < 0 || fenceInfo?.Files == null)
                return;

            int count = entrySnapshot.Count;
            int index = entrySnapshot.ToDisplayInsertionIndex(dropIndicatorIndex);
            Rectangle insertionBounds = grid.GetItemBounds(index);
            int targetX = insertionBounds.X - (count > 0 ? itemPadding / 2 : 0);
            int targetY = insertionBounds.Y;

            Color accentColor = theme.AccentColor;
            int barWidth = Math.Max(3, LogicalPixelsToDevice(3));
            int barHeight = Math.Min(itemHeight, contentViewport.Height);
            int barX = Math.Max(contentViewport.Left, Math.Min(contentViewport.Right - barWidth, targetX - barWidth / 2));
            int barY = Math.Max(contentViewport.Top, targetY);

            var barRect = new Rectangle(barX, barY, barWidth, barHeight);
            using (var brush = new SolidBrush(accentColor))
            using (var glowPen = new Pen(ThemeDrawing.WithAlpha(accentColor, 90), barWidth + LogicalPixelsToDevice(4)))
            {
                glowPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                glowPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                graphics.DrawLine(glowPen, barRect.X + barWidth / 2, barRect.Y + 2, barRect.X + barWidth / 2, barRect.Bottom - 2);

                using (var path = ThemeDrawing.CreateRoundedRectangle(barRect, barWidth))
                {
                    graphics.FillPath(brush, path);
                }
            }
        }

        /// <summary>绘制拖拽中跟随鼠标移动的半透明幽灵图标。</summary>
        private void DrawDraggingGhost(Graphics graphics, ThemeDefinition theme)
        {
            if (!isDraggingItem || string.IsNullOrEmpty(dragCandidatePath))
                return;

            int entryIndex = entrySnapshot.IndexOfPath(dragCandidatePath);
            var entry = entryIndex >= 0 ? entrySnapshot[entryIndex] : null;
            if (entry == null)
                return;

            Icon icon = entry.ExtractIcon(thumbnailProvider);
            if (icon == null)
                return;

            Point mousePos = PointToClient(Cursor.Position);
            int ghostSize = iconSize;
            int ghostX = mousePos.X - ghostSize / 2;
            int ghostY = mousePos.Y - ghostSize / 2;
            var ghostRect = new Rectangle(ghostX, ghostY, ghostSize, ghostSize);

            using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                graphics.FillEllipse(shadowBrush, ghostRect.X + 2, ghostRect.Bottom - ghostSize / 4, ghostSize - 4, ghostSize / 4);
            }

            var matrix = new System.Drawing.Imaging.ColorMatrix
            {
                Matrix33 = 0.72f
            };
            using (var imgAttr = new System.Drawing.Imaging.ImageAttributes())
            {
                imgAttr.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

                Bitmap bmp = thumbnailProvider.GetBitmap(icon);
                if (bmp != null)
                {
                    graphics.DrawImage(
                        bmp,
                        ghostRect,
                        0, 0, bmp.Width, bmp.Height,
                        GraphicsUnit.Pixel,
                        imgAttr);
                }
            }
        }

        /// <summary>复用路径引用及显示名称，绘制期间不执行文件系统查询。</summary>
        private FenceEntry GetEntry(string path)
        {
            if (entrySnapshot.Synchronize(fenceInfo.Files))
                entryHitBounds.Clear();
            int index = entrySnapshot.IndexOfPath(path);
            return index >= 0 ? entrySnapshot[index] : null;
        }

        /// <summary>使变化条目的图标失效；只读路径和名称在列表变化时随快照一起更新。</summary>
        private void InvalidateEntry(string path)
        {
            if (path == null)
                return;
            thumbnailProvider.Remove(path);
        }

        /// <summary>检查拖入路径是否存在（文件或文件夹），只用于显式添加操作。</summary>
        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }
    }

}

