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
        private const int titleOffset = 3;

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
        private bool shouldRunDoubleClick;
        private bool hasSelectionUpdated;
        private bool hasHoverUpdated;
        private bool isMinified;
        private int prevWidth;
        private int prevHeight;

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
        private Rectangle iconManagementConfirmBounds;
        private Rectangle iconManagementCancelBounds;
        private int iconManagementHoverTarget;

        private int scrollHeight;
        private int scrollOffset;

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
        /// 将设备像素（物理像素）转换为逻辑像素（96 DPI 坐标）。
        /// 使用原生 GetDpiForWindow 而非 CreateGraphics().DpiX，
        /// 因为 WinForms 会缓存 DPI 值，DPI 变更后不会自动更新。
        /// </summary>
        private int DevicePixelsToLogical(int devicePixels)
        {
            uint dpi = 96;
            if (IsHandleCreated)
                dpi = GetDpiForWindow(Handle);
            return (int)Math.Round(devicePixels * 96.0 / dpi);
        }

        /// <summary>
        /// 将逻辑像素（96 DPI 坐标）转换为当前窗口的设备像素。
        /// 使用原生 GetDpiForWindow，避免 WinForms 缓存的 DPI 值滞后。
        /// </summary>
        private int LogicalPixelsToDevice(int logicalPixels)
        {
            uint dpi = 96;
            if (IsHandleCreated)
                dpi = GetDpiForWindow(Handle);
            return (int)Math.Round(logicalPixels * dpi / 96.0, MidpointRounding.AwayFromZero);
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

                // 3. 通过 SPI_GETICONMETRICS 读取系统图标度量（获取字体信息）
                iconMetrics = new ICONMETRICS();
                iconMetrics.cbSize = (uint)Marshal.SizeOf(typeof(ICONMETRICS));
                SystemParametersInfo(SPI_GETICONMETRICS, iconMetrics.cbSize, ref iconMetrics, 0);

                // 4. GDI+ 手工绘制直接使用设备像素；仅对 SPI 的逻辑值做一次 DPI 放大
                int deviceItemWidth;
                if (deviceSpacing > 0)
                    deviceItemWidth = deviceSpacing;
                else if (iconMetrics.iHorzSpacing > 0)
                    deviceItemWidth = LogicalPixelsToDevice(iconMetrics.iHorzSpacing);
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
            var prevIconSize = iconSize;
            LoadAndApplyMetrics();

            if (thumbnailProvider != null && iconSize != prevIconSize)
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
        /// </summary>
        private Font CreateIconFontFromLogFont()
        {
            try
            {
                var lf = iconMetrics.lfFont;
                string familyName = ResolveDesktopFontFamily(lf.lfFaceName);
                // lfHeight 是逻辑单位，需要转换为磅值（point）
                float fontSize = Math.Abs(lf.lfHeight);
                using (var g = CreateGraphics())
                {
                    fontSize = fontSize * 72f / g.DpiY;
                }
                if (fontSize < 6f) fontSize = 9f; // 最小字号保护
                fontSize = Math.Max(2f, fontSize * (GetEffectiveScalePercent() / 100f));
                var style = FontStyle.Regular;
                if (lf.lfWeight >= 700) style |= FontStyle.Bold;
                if (lf.lfItalic != 0) style |= FontStyle.Italic;
                if (lf.lfUnderline != 0) style |= FontStyle.Underline;
                if (lf.lfStrikeOut != 0) style |= FontStyle.Strikeout;
                return new Font(familyName, fontSize, style);
            }
            catch
            {
                float fallbackSize = Math.Max(2f, 9f * (GetEffectiveScalePercent() / 100f));
                string fallbackFamily = SystemFonts.IconTitleFont?.FontFamily?.Name ?? "Segoe UI";
                return new Font(fallbackFamily, fallbackSize, FontStyle.Regular);
            }
        }

        /// <summary>
        /// 重新创建所有字体（先释放旧字体防止 GDI 资源泄漏）。
        /// 标题字体基于标题栏高度计算，图标字体从系统图标度量获取。
        /// </summary>
        private void ReloadFonts()
        {
            titleFont?.Dispose();
            iconFont?.Dispose();

            string familyName = ResolveDesktopFontFamily(iconMetrics.lfFont.lfFaceName);
            float availableSpace = IsVerticalTitleBar
                ? Math.Max(12f, logicalTitleHeight - (fenceInfo?.TitlePaddingX ?? 10) * 2)
                : Math.Max(12f, logicalTitleHeight - (fenceInfo?.TitlePaddingY ?? 6) * 2);
            float titleSize = Math.Max(8f, Math.Min(32f, (float)Math.Floor(availableSpace * 0.72f)));
            try
            {
                titleFont = new Font(familyName, titleSize, FontStyle.Regular);
            }
            catch
            {
                titleFont = new Font("Segoe UI", titleSize, FontStyle.Regular);
            }
            iconFont = CreateIconFontFromLogFont();
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            this.fenceInfo = fenceInfo ?? throw new ArgumentNullException(nameof(fenceInfo));
            this.fenceInfo.ScalePercent = GetEffectiveScalePercent();
            currentTheme = ThemeManager.Instance.CurrentTheme;

            // 先初始化组件以创建窗口句柄（后续 DPI 相关操作需要 Handle）
            InitializeComponent();
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();
            InitializeScaleSubMenu();
            ApplyContextMenuLocalization();
            darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
            autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;

            // 应用 Windows 视觉效果。DWM 投影需等主题圆角确定后再设置，
            // 否则矩形投影会残留在圆角外侧并在拖动时形成不透明角块。
            WindowUtil.HideFromAltTab(Handle);  // 从 Alt+Tab 隐藏
            DesktopUtil.GlueToDesktop(Handle);  // 粘附到桌面 Progman 窗口

            // 必须在 Handle 创建后计算图标度量（需要正确的 DPI 信息）
            LoadAndApplyMetrics();

            // 创建缩略图生成器（iconSize 已是设备像素尺寸）
            thumbnailProvider = new ThumbnailProvider(iconSize);

            // 每 1.5 秒同时轮询桌面图标间距与边长（捕获 Ctrl+滚轮缩放）
            iconMetricsPollTimer = new Timer { Interval = 1500 };
            iconMetricsPollTimer.Tick += (s, e) =>
            {
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

            // 标题栏高度：逻辑像素，根据内边距自适应扩展
            int minNeeded = HeightDialog.CalculateMinTitleHeight(
                fenceInfo.TitlePosition,
                fenceInfo.TitlePaddingX,
                fenceInfo.TitlePaddingY);
            logicalTitleHeight = Math.Max(minNeeded, (fenceInfo.TitleHeight < 16 || fenceInfo.TitleHeight > 100) ? 35 : fenceInfo.TitleHeight);
            fenceInfo.TitleHeight = logicalTitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
            
            this.MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;
            InitializeManagedItemsWatcher();

            ReloadFonts();

            AllowDrop = true; // 允许拖放文件到栅栏

            Text = fenceInfo.Name;
            ApplyPersistedBounds();

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

                    thumbnailProvider.Remove(path);
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
                        thumbnailProvider.Remove(oldPath);
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

        /// <summary>校正损坏尺寸和已断开显示器留下的屏幕外坐标。</summary>
        private void ApplyPersistedBounds()
        {
            int minimumWidth = Math.Max(itemWidth + itemPadding * 2, LogicalPixelsToDevice(120));
            int minimumHeight = Math.Max(
                titleHeight + itemHeight + itemPadding * 2,
                LogicalPixelsToDevice(100));
            int requestedWidth = Math.Max(minimumWidth, fenceInfo.Width);
            int requestedHeight = Math.Max(minimumHeight, fenceInfo.Height);
            Rectangle workingArea = Screen.FromPoint(
                new Point(fenceInfo.PosX, fenceInfo.PosY)).WorkingArea;
            int width = Math.Max(1, Math.Min(requestedWidth, workingArea.Width));
            int height = Math.Max(1, Math.Min(requestedHeight, workingArea.Height));
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
            ThemeUi.ApplyToContextMenu(appContextMenu, currentTheme);
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
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                hasMoveOrigin = true;
                moveStartCursor = Cursor.Position;
                moveStartLocation = Location;
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                hasMoveOrigin = false;
            }
            else if (m.Msg == WM_MOVING && AppSettingsManager.Instance.AutoSnapEnabled)
            {
                var rect = Marshal.PtrToStructure<RECT>(m.LParam);
                int width = rect.Width;
                int height = rect.Height;

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
                Marshal.StructureToPtr(rect, m.LParam, true);
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

            // 系统 DPI 变化（设置 → 显示 → 缩放），重新计算图标尺寸
            if (m.Msg == WM_DPICHANGED)
            {
                ApplyIconMetrics();
            }

            // 阻止获取焦点（保持在桌面窗口下方，不抢 Progman 焦点）
            if (m.Msg == WM_SETFOCUS)
            {
                SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                return;
            }

            // 系统设置广播（主题/字体等变化），重新加载图标度量
            if (m.Msg == WM_SETTINGCHANGE)
            {
                ApplyIconMetrics();
            }

            base.WndProc(ref m);

            // 锁定状态或鼠标右键按下时，不允许拖动和调整大小
            if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
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
                thumbnailProvider.Remove(item);
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
                    thumbnailProvider.Remove(path);
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
            darkModeToolStripMenuItem.Checked = ThemeManager.Instance.DarkModeEnabled;
            autoSnapToolStripMenuItem.Checked = AppSettingsManager.Instance.AutoSnapEnabled;
            if (scaleMenuControl != null)
                scaleMenuControl.ScalePercent = GetEffectiveScalePercent();
            // 可见性和勾选状态会触发 ToolStrip 重新布局；在这些状态更新后应用
            // 主题，确保空白处右键弹出的整个菜单窗口都按当前主题重新绘制。
            ThemeUi.ApplyToContextMenu(appContextMenu, currentTheme);
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

            if (isUpdatingMinifyBounds || isAnimatingMinify)
                return;

            if (!isMinified)
            {
                prevWidth = Width;
                prevHeight = Height;
            }

            throttledResize.Run(() =>
            {
                Rectangle expanded = GetCurrentExpandedBounds();
                fenceInfo.PosX = expanded.X;
                fenceInfo.PosY = expanded.Y;
                fenceInfo.Width = expanded.Width;
                fenceInfo.Height = expanded.Height;
                Save();
            });
        }

        /// <summary>鼠标移动：触发重绘以更新悬停高亮。</summary>
        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            string hitPath = null;
            if (!isIconManagementMode &&
                !string.IsNullOrEmpty(selectedItem) &&
                entryHitBounds.TryGetValue(selectedItem, out Rectangle selectedBounds) &&
                selectedBounds.Contains(e.Location))
            {
                hitPath = selectedItem;
            }
            else
            {
                foreach (KeyValuePair<string, Rectangle> item in entryHitBounds)
                {
                    if (item.Value.Contains(e.Location))
                    {
                        hitPath = item.Key;
                        break;
                    }
                }
            }

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

                bool menuOpen = (appContextMenu != null && appContextMenu.Visible) ||
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
            bool menuOpen = (appContextMenu != null && appContextMenu.Visible) ||
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

            shouldUpdateSelection = true;
            Refresh();
        }

        /// <summary>双击：设置双击执行标志，由 Paint 周期实际处理。</summary>
        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            if (isIconManagementMode)
                return;

            shouldRunDoubleClick = true;
            Refresh();
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

            // 在进入条目遍历前预先根据当前命中矩形更新点击选中项，
            // 确保点击溢出文本条目时在同一渲染帧立即触发 needsExpand 展开，无需等待鼠标移出。
            if (!isIconManagementMode && shouldUpdateSelection && entryHitBounds.Count > 0)
            {
                Point clickPos = PointToClient(MousePosition);
                string clickedPath = null;
                if (contentViewport.Contains(clickPos))
                {
                    if (!string.IsNullOrEmpty(selectedItem) &&
                        entryHitBounds.TryGetValue(selectedItem, out Rectangle selectedBounds) &&
                        selectedBounds.Contains(clickPos))
                    {
                        clickedPath = selectedItem;
                    }
                    else
                    {
                        foreach (KeyValuePair<string, Rectangle> kvp in entryHitBounds)
                        {
                            if (kvp.Value.Contains(clickPos))
                            {
                                clickedPath = kvp.Key;
                                break;
                            }
                        }
                    }
                }

                selectedItem = clickedPath;
                shouldUpdateSelection = false;
                hasSelectionUpdated = true;
            }

            // 条目网格布局：从左到右、从上到下排列在 contentViewport 内
            var x = itemPadding;
            var y = itemPadding;
            scrollHeight = 0;
            entryHitBounds.Clear();
            var contentState = e.Graphics.Save();
            e.Graphics.SetClip(
                contentViewport,
                System.Drawing.Drawing2D.CombineMode.Intersect);
            // 记录选中条目（仅当文字超过标准 2 行高度时才需要展开渲染）
            string expandEntryName = null;
            int expandEntryX = 0, expandEntryY = 0;
            FenceEntry expandEntry = null; // 用于在展开区块中重绘图标

            foreach (var file in fenceInfo.Files)
            {
                var entry = FenceEntry.FromPath(file);
                if (entry == null)
                    continue;

                int drawX = contentViewport.X + x;
                int drawY = contentViewport.Y + y - scrollOffset;

                // 若首次渲染尚无 entryHitBounds 缓存时发生点击，在此提前命中判定
                if (!isIconManagementMode && shouldUpdateSelection)
                {
                    int textInsetPre = Math.Min(6, Math.Max(1, itemWidth / 6));
                    var textMaxSizePre = new SizeF(Math.Max(4, itemWidth - textInsetPre * 2), textHeight);
                    var textSizePre = e.Graphics.MeasureString(entry.Name, iconFont, textMaxSizePre, standardIconTextFormat);
                    var preRect = new Rectangle(drawX - 2, drawY - 2, itemWidth + 2, iconSize + (int)textSizePre.Height + (itemPadding / 3) + 2);
                    var mousePos = PointToClient(MousePosition);
                    if (contentViewport.Contains(mousePos) && preRect.Contains(mousePos))
                    {
                        selectedItem = entry.Path;
                        shouldUpdateSelection = false;
                        hasSelectionUpdated = true;
                    }
                }

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

                Rectangle entryBounds = RenderEntry(
                    e.Graphics,
                    entry,
                    drawX,
                    drawY,
                    skipText: needsExpand);
                entryHitBounds[entry.Path] = entryBounds;

                if (needsExpand)
                {
                    expandEntryName = entry.Name;
                    expandEntryX = drawX;
                    expandEntryY = drawY;
                    expandEntry = entry;
                }

                var itemBottom = y + itemHeight;
                if (itemBottom > scrollHeight)
                    scrollHeight = itemBottom;

                int layoutViewportWidth = (isMinified || isAnimatingMinify) && IsVerticalTitleBar
                    ? Math.Max(contentViewport.Width, prevWidth - titleHeight)
                    : contentViewport.Width;
                x += itemWidth + itemPadding;
                if (x + itemWidth > layoutViewportWidth) // 换行
                {
                    x = itemPadding;
                    y += itemHeight + itemPadding;
                }
            }

            if (isIconManagementMode)
            {
                iconManagementSelection.RemoveWhere(path =>
                    !entryHitBounds.ContainsKey(path));
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
                var expandMousePos = PointToClient(MousePosition);
                bool expandMouseOver = expandOutlineRect.Contains(expandMousePos);
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

            // 计算内容溢出高度（用于滚动条），确保不会出现负值
            scrollHeight = Math.Max(0, scrollHeight - contentViewport.Height);
            scrollOffset = Math.Min(scrollOffset, scrollHeight);

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

            // 单击/双击处理标志重置（这些标志在 Paint 周期中执行实际操作）
            e.Graphics.Restore(contentState);

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

            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldRunDoubleClick = false;
            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        /// <summary>按当前栅栏的标题栏位置（顶/底/左/右）、文本对齐方式、内边距与行高倍数绘制标题文字。</summary>
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
                        float fontHeight = titleFont.GetHeight(graphics);
                        float stepY = fontHeight * 1.05f;
                        float totalSpan = (elements.Count - 1) * stepY + fontHeight;

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
                                if (charY + fontHeight <= textRect.Y)
                                    continue;
                                if (charY >= textRect.Bottom)
                                    break;

                                // 接近底部且还有后续字符时绘制省略号
                                if (charY + stepY > textRect.Bottom && i < elements.Count - 1)
                                {
                                    var ellipsisRect = new RectangleF(textRect.X, charY, textRect.Width, fontHeight);
                                    graphics.DrawString("…", titleFont, titleTextBrush, ellipsisRect, charFormat);
                                    break;
                                }

                                var charRect = new RectangleF(textRect.X, charY, textRect.Width, fontHeight);
                                graphics.DrawString(elements[i], titleFont, titleTextBrush, charRect, charFormat);
                            }
                        }
                    }
                }
                else
                {
                    // 横排标题栏：padX 为左右内边距，padY 为上下内边距（自适应扩展标题高度）
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
                        float fontHeight = titleFont.GetHeight(graphics);
                        float stepY = fontHeight * 1.15f;
                        float totalH = (lines.Length - 1) * stepY + fontHeight;
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
                                if (lineY + fontHeight > textRect.Bottom)
                                    break;
                                var lineRect = new RectangleF(textRect.X, lineY, textRect.Width, fontHeight);
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
        private Rectangle RenderEntry(Graphics g, FenceEntry entry, int x, int y, bool skipText = false)
        {
            var theme = currentTheme ?? ThemePresets.CreateDefault();
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            var textPadding = itemPadding / 3;
            // 按 Windows 桌面规则：文本左右各留 6px 安全距离（极小缩放比例下按比例收缩）
            int textInset = Math.Min(6, Math.Max(1, itemWidth / 6));
            var textPosition = new PointF(x + textInset, y + iconSize + textPadding);
            var textMaxSize = new SizeF(Math.Max(4, itemWidth - textInset * 2), textHeight);

            // 按 Windows 桌面规则始终允许自动换行，居中、末尾省略号截断
            var textSize = g.MeasureString(
                name,
                iconFont,
                textMaxSize,
                standardIconTextFormat);
            var gap = textPadding;
            var outlineRect = new Rectangle(x - 2, y - 2, itemWidth + 2, iconSize + (int)textSize.Height + gap + 2);

            var mousePos = PointToClient(MousePosition);
            var mouseOver = mousePos.X >= x && mousePos.Y >= y && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;

            if (mouseOver)
            {
                hoveringItem = entry.Path;
                hasHoverUpdated = true;
            }

            // 单击选中（由 Paint 周期统一处理以保证命中测试一致性）
            if (!isIconManagementMode && mouseOver && shouldUpdateSelection)
            {
                selectedItem = entry.Path;
                shouldUpdateSelection = false;
                hasSelectionUpdated = true;
            }

            // 双击打开（由 Paint 周期统一处理以保证命中测试一致性）
            if (!isIconManagementMode && mouseOver && shouldRunDoubleClick)
            {
                shouldRunDoubleClick = false;
                entry.Open();
            }

            // 绘制选中/悬停背景。XP 默认悬浮无反馈，且选中效果由后续的
            // 图标染色和文字实色底组成，因此这里只处理现代整项高亮。
            bool selected = isIconManagementMode
                ? iconManagementSelection.Contains(entry.Path)
                : selectedItem == entry.Path;
            if (!skipText && theme.MenuStyle != ThemeMenuStyle.WindowsXp)
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
            DrawDesktopIconForState(g, icon, iconRect, selected, theme);
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
                selected,
                mouseOver);

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
            using (var footerBrush = new SolidBrush(
                ThemeDrawing.WithAlpha(theme.TitleBarColor, 220)))
            using (var separatorPen = new Pen(
                ThemeDrawing.WithAlpha(theme.BorderColor, 145)))
            {
                graphics.FillRectangle(footerBrush, footerBounds);
                graphics.DrawLine(separatorPen, footerBounds.Left, footerTop, footerBounds.Right, footerTop);
            }

            int margin = LogicalPixelsToDevice(7);
            int gap = LogicalPixelsToDevice(5);
            int buttonHeight = Math.Min(
                LogicalPixelsToDevice(30),
                Math.Max(1, footerBounds.Height - margin * 2));
            int availableWidth = Math.Max(2, footerBounds.Width - margin * 2 - gap);
            int cancelWidth = Math.Max(
                1,
                Math.Min(LogicalPixelsToDevice(68), availableWidth / 2));
            int confirmWidth = Math.Max(
                1,
                Math.Min(
                    LogicalPixelsToDevice(88),
                    availableWidth - cancelWidth));
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
            int hintRight = iconManagementConfirmBounds.Left - margin;
            if (hintRight > hintLeft)
            {
                string hint = iconManagementSelection.Count > 0
                    ? ThemeText.SelectedItemCount(iconManagementSelection.Count)
                    : ThemeText.IconManagementHint;
                var hintBounds = new Rectangle(
                    hintLeft,
                    footerTop,
                    hintRight - hintLeft,
                    footerBounds.Height);
                TextRenderer.DrawText(
                    graphics,
                    hint,
                    iconFont ?? Font,
                    hintBounds,
                    theme.ItemTextColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding);
            }
        }

        /// <summary>返回当前 DPI 下图标管理底栏的设备像素高度。</summary>
        private int GetIconManagementFooterHeight()
        {
            return LogicalPixelsToDevice(46);
        }

        /// <summary>按当前主题绘制图标管理底栏中的单个操作按钮。</summary>
        private void DrawIconManagementButton(
            Graphics graphics,
            Rectangle bounds,
            string text,
            bool enabled,
            bool hovered,
            bool accent,
            ThemeDefinition theme)
        {
            Color fillColor;
            if (!enabled)
            {
                fillColor = ThemeDrawing.Mix(
                    theme.MainPanelColor,
                    theme.ControlBackgroundColor,
                    0.22f);
            }
            else if (accent)
            {
                fillColor = theme.AccentColor;
            }
            else
            {
                fillColor = ThemeDrawing.Mix(
                    theme.MainPanelColor,
                    theme.ControlBackgroundColor,
                    0.45f);
            }

            if (hovered && enabled)
            {
                fillColor = ThemeDrawing.Mix(
                    fillColor,
                    ThemeDrawing.IsDark(fillColor) ? Color.White : Color.Black,
                    0.12f);
            }

            int radius = theme.CornerRadius > 0
                ? LogicalPixelsToDevice(Math.Min(8, theme.CornerRadius))
                : 0;
            using (var path = ThemeDrawing.CreateRoundedRectangle(bounds, radius))
            using (var brush = new SolidBrush(
                ThemeDrawing.WithAlpha(fillColor, enabled ? 230 : 120)))
            using (var pen = new Pen(
                ThemeDrawing.WithAlpha(theme.BorderColor, enabled ? 190 : 90)))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }

            Color textColor = !enabled
                ? ThemeDrawing.WithAlpha(theme.ItemTextColor, 105)
                : accent
                    ? (ThemeDrawing.IsDark(fillColor) ? Color.White : Color.Black)
                    : theme.ItemTextColor;
            TextRenderer.DrawText(
                graphics,
                text,
                iconFont ?? Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);
        }

        /// <summary>在管理模式下为每个图标绘制可多选的圆形勾选标记。</summary>
        private void DrawIconManagementCheck(
            Graphics graphics,
            Rectangle iconBounds,
            bool selected,
            ThemeDefinition theme)
        {
            int size = Math.Max(LogicalPixelsToDevice(14), iconSize / 3);
            var checkBounds = new Rectangle(
                iconBounds.Right - size + LogicalPixelsToDevice(2),
                iconBounds.Top - LogicalPixelsToDevice(2),
                size,
                size);
            Color fillColor = selected
                ? theme.AccentColor
                : ThemeDrawing.WithAlpha(theme.MainPanelColor, 205);
            using (var fill = new SolidBrush(fillColor))
            using (var outline = new Pen(
                selected ? theme.AccentColor : theme.BorderColor,
                Math.Max(1f, LogicalPixelsToDevice(1))))
            {
                graphics.FillEllipse(fill, checkBounds);
                graphics.DrawEllipse(outline, checkBounds);
            }

            if (!selected)
                return;

            Color checkColor = ThemeDrawing.IsDark(theme.AccentColor)
                ? Color.White
                : Color.Black;
            using (var pen = new Pen(
                checkColor,
                Math.Max(1.5f, LogicalPixelsToDevice(1))))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                graphics.DrawLines(pen, new[]
                {
                    new Point(
                        checkBounds.Left + checkBounds.Width / 4,
                        checkBounds.Top + checkBounds.Height / 2),
                    new Point(
                        checkBounds.Left + checkBounds.Width / 2 - 1,
                        checkBounds.Bottom - checkBounds.Height / 4),
                    new Point(
                        checkBounds.Right - checkBounds.Width / 5,
                        checkBounds.Top + checkBounds.Height / 4)
                });
            }
        }

        /// <summary>
        /// 使用带 Alpha 通道的位图和高质量双三次插值绘制图标，
        /// 避免 DrawIcon 缩放图标时退化为邻近取样产生锯齿或模糊。
        /// </summary>
        private static void DrawIconHighQuality(Graphics graphics, Icon icon, Rectangle targetRectangle)
        {
            if (icon == null || targetRectangle.Width <= 0 || targetRectangle.Height <= 0)
                return;

            if (icon.Width == targetRectangle.Width && icon.Height == targetRectangle.Height)
            {
                graphics.DrawIcon(icon, targetRectangle);
                return;
            }

            using (var bitmap = icon.ToBitmap())
            {
                graphics.DrawImage(bitmap, targetRectangle);
            }
        }

        /// <summary>按当前桌面风格绘制原色图标或 XP 经典选中染色图标。</summary>
        private static void DrawDesktopIconForState(
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
        private static void DrawXpSelectedIcon(
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

            using (var bitmap = icon.ToBitmap())
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
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.ShowDialog();
            }
        }

        /// <summary>
        /// 独立切换应用颜色模式。这里只更新 DarkModeEnabled；当前 Win11、XP
        /// 或自定义风格 ID 保持不变，由 ThemeManager 解析该风格对应的颜色变体。
        /// </summary>
        private void darkModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ThemeManager.Instance.SetDarkMode(darkModeToolStripMenuItem.Checked);
        }

        /// <summary>独立切换全局桌面分区移动自动吸附开关。</summary>
        private void autoSnapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppSettingsManager.Instance.SetAutoSnap(autoSnapToolStripMenuItem.Checked);
        }

        /// <summary>窗口关闭：如果最后一个栅栏关闭则退出应用。</summary>
        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
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
            iconMetricsPollTimer.Stop();
            iconMetricsPollTimer.Dispose();
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
            if (isUpdatingMinifyBounds || isAnimatingMinify)
                return;

            throttledMove.Run(() =>
            {
                Rectangle expanded = GetCurrentExpandedBounds();
                fenceInfo.PosX = expanded.X;
                fenceInfo.PosY = expanded.Y;
                Save();
            });
        }

        /// <summary>切换锁定状态。</summary>
        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        private void FenceWindow_Load(object sender, EventArgs e)
        {

        }

        /// <summary>打开标题设置对话框，支持实时预览；确定则保存修改，取消则恢复修改前的设置。</summary>
        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 备份修改前的初始状态，供取消时回滚
            int origHeight = fenceInfo.TitleHeight;
            FenceTitlePosition origPosition = fenceInfo.TitlePosition;
            FenceTitleTextAlignment origAlignment = fenceInfo.TitleTextAlignment;
            int origPaddingX = fenceInfo.TitlePaddingX;
            int origPaddingY = fenceInfo.TitlePaddingY;

            bool wasMinified = isMinified;
            if (wasMinified)
                ExpandFromMinified(animate: false);

            using (var dialog = new HeightDialog(
                fenceInfo.TitleHeight,
                fenceInfo.TitlePosition,
                fenceInfo.TitleTextAlignment,
                fenceInfo.TitlePaddingX,
                fenceInfo.TitlePaddingY))
            {
                dialog.StartPosition = FormStartPosition.CenterScreen;

                // 交互时实时预览变更
                dialog.PreviewChanged += (pos, align, h, padX, padY) =>
                {
                    ApplyTitleSettings(pos, align, h, padX, padY);
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // 确认保存修改
                    ApplyTitleSettings(
                        dialog.TitlePosition,
                        dialog.TitleTextAlignment,
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
                    ApplyTitleSettings(origPosition, origAlignment, origHeight, origPaddingX, origPaddingY);
                    if (wasMinified || (minifyToolStripMenuItem.Checked && !Bounds.Contains(MousePosition)))
                        Minify(animate: false);
                    Refresh();
                }
            }
        }

        /// <summary>应用标题栏配置并使标题栏尺寸自适应内边距，刷新界面渲染。</summary>
        private void ApplyTitleSettings(
            FenceTitlePosition position,
            FenceTitleTextAlignment alignment,
            int height,
            int padX,
            int padY)
        {
            fenceInfo.TitlePosition = position;
            fenceInfo.TitleTextAlignment = alignment;
            fenceInfo.TitlePaddingX = padX;
            fenceInfo.TitlePaddingY = padY;

            int minNeeded = HeightDialog.CalculateMinTitleHeight(position, padX, padY);
            logicalTitleHeight = Math.Max(height, minNeeded);
            fenceInfo.TitleHeight = logicalTitleHeight;
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);

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

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                // 右键条目 → Windows Shell 右键菜单
                string shellItem = hoveringItem;
                bool wasManaged = FenceManager.Instance.IsManagedDesktopEntry(
                    fenceInfo,
                    shellItem);
                try
                {
                    shellContextMenu.ShowContextMenu(
                        new[] { new FileInfo(shellItem) },
                        MousePosition);
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
                        thumbnailProvider.Remove(shellItem);
                    }
                    selectedItem = null;
                    hoveringItem = null;
                    Refresh();
                }
            }
            else
            {
                // Shift+右键或空白处 → 应用菜单
                appContextMenu.Show(this, e.Location);
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

        /// <summary>检查路径是否存在（文件或文件夹）。</summary>
        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }
    }

}

