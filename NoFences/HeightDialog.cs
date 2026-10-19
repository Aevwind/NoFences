using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NoFences.Localization;
using NoFences.Model;
using NoFences.Theming;
using NoFences.Win32;

namespace NoFences
{
    /// <summary>
    /// 桌面分区标题设置对话框（原“标题尺寸”弹窗升级版）。
    /// 支持配置：
    /// 1. 标题栏位置（底部、顶部、左、右侧）—— 采用可视化缩略卡片选择；
    /// 2. 标题文本对齐方式（靠左、居中、靠右）—— 采用现代圆角下拉弹出菜单选择；
    /// 3. 标题文本大小（10~40px，行高固定为 1）—— 采用平滑滑块实时调节，标题栏高度跟随文本大小与上下内边距自动撑开；
    /// 4. 标题栏左右内边距（0~50px）；
    /// 5. 标题栏上下内边距（0~50px）。
    /// </summary>
    public partial class HeightDialog : Form
    {
        public const int DefaultTitleFontSize = 14;
        public const int MinTitleFontSize = 10;
        public const int MaxTitleFontSize = 40;

        public const int MinTitleHeight = 20;
        public const int MaxTitleHeight = 100;

        public const int DefaultTitlePaddingX = 10;
        public const int DefaultTitlePaddingY = 6;
        public const int MinTitlePadding = 0;
        public const int MaxTitlePadding = 50;

        private SettingsPalette palette;
        private FenceTitlePosition selectedPosition;
        private FenceTitleTextAlignment selectedAlignment;
        private int selectedFontSize;
        private int selectedHeight;
        private int selectedPaddingX;
        private int selectedPaddingY;

        private int hoveredCardIndex = -1;
        private bool isAlignDropdownHovered;

        private enum ActiveSlider { None, Size, PaddingX, PaddingY }
        private ActiveSlider hoveredSlider = ActiveSlider.None;
        private ActiveSlider activeSlider = ActiveSlider.None;

        private Rectangle[] cardHitRects = new Rectangle[4];
        private Rectangle alignDropdownRect;
        private Rectangle sizeSliderTrackRect;
        private Rectangle paddingXSliderTrackRect;
        private Rectangle paddingYSliderTrackRect;

        private readonly SettingsActionButton btnRestore;
        private readonly SettingsActionButton btnOk;
        private readonly SettingsActionButton btnCancel;
        private ToolStripDropDown alignmentPopup;

        /// <summary>当标题设置发生任意变更时触发实时预览事件（位置、对齐方式、标题文本大小、自适应撑开的标题栏高度、左右内边距、上下内边距）。</summary>
        public event Action<FenceTitlePosition, FenceTitleTextAlignment, int, int, int, int> PreviewChanged;

        /// <summary>用户选择的标题文本大小（逻辑像素，10~40，最低 10px）。</summary>
        public int TitleFontSize => selectedFontSize;

        /// <summary>根据标题文本大小与内边距自动撑开的标题栏大小（逻辑像素）。</summary>
        public int TitleHeight => selectedHeight;

        /// <summary>用户选择的标题栏位置（顶部/底部/左/右侧）。</summary>
        public FenceTitlePosition TitlePosition => selectedPosition;

        /// <summary>用户选择的标题文本对齐方式（靠左/居中/靠右）。</summary>
        public FenceTitleTextAlignment TitleTextAlignment => selectedAlignment;

        /// <summary>用户选择的标题栏左右内边距（逻辑像素，0~50）。</summary>
        public int TitlePaddingX => selectedPaddingX;

        /// <summary>用户选择的标题栏上下内边距（逻辑像素，0~50）。</summary>
        public int TitlePaddingY => selectedPaddingY;

        /// <summary>向后兼容保留的单值内边距属性（等同于左右内边距 TitlePaddingX）。</summary>
        public int TitlePadding => selectedPaddingX;

        public HeightDialog(
            int initialFontSize,
            FenceTitlePosition initialPosition = FenceTitlePosition.Top,
            FenceTitleTextAlignment initialAlignment = FenceTitleTextAlignment.Center,
            int initialPaddingX = DefaultTitlePaddingX,
            int initialPaddingY = DefaultTitlePaddingY)
        {
            InitializeComponent();
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            selectedPosition = initialPosition;
            selectedAlignment = initialAlignment;
            selectedPaddingX = ClampPadding(initialPaddingX);
            selectedPaddingY = ClampPadding(initialPaddingY);
            selectedFontSize = ClampFontSize(initialFontSize);
            UpdateAdaptedTitleHeight();

            btnRestore = new SettingsActionButton { IsPrimary = false };
            btnOk = new SettingsActionButton { IsPrimary = true };
            btnCancel = new SettingsActionButton { IsPrimary = false };

            btnRestore.Click += BtnRestore_Click;
            btnOk.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(btnRestore);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdateDialogLayout();
            ApplyLocalization();
            ApplyCurrentPalette();
            ThemeManager.Instance.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateDialogLayout();
            ApplyCurrentPalette();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateDialogLayout();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.Instance.ThemeChanged -= ThemeManager_ThemeChanged;
            alignmentPopup?.Dispose();
            alignmentPopup = null;
            base.OnFormClosed(e);
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(ApplyCurrentPalette));
                return;
            }
            ApplyCurrentPalette();
        }

        private void ApplyLocalization()
        {
            Text = ThemeText.Key(LKeys.HeightDialogTitle);
            btnRestore.Text = ThemeText.Key(LKeys.HeightDialogRestoreDefault);
            btnOk.Text = ThemeText.Ok;
            btnCancel.Text = ThemeText.Cancel;
        }

        private void ApplyCurrentPalette()
        {
            bool darkMode = ThemeManager.Instance.DarkModeEnabled;
            palette = SettingsPalette.FromColorMode(darkMode);
            BackColor = palette.CardBackground;
            ForeColor = palette.PrimaryText;

            btnRestore.ApplyPalette(palette);
            btnOk.ApplyPalette(palette);
            btnCancel.ApplyPalette(palette);

            if (IsHandleCreated)
            {
                WindowUtil.TrySetImmersiveDarkMode(Handle, darkMode);
            }

            Invalidate(true);
        }

        private float GetDpiScale()
        {
            if (IsHandleCreated)
            {
                uint dpi = WindowUtil.GetDpiForWindow(Handle);
                if (dpi > 0)
                    return dpi / 96f;
            }
            return DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowUtil.WM_DPICHANGED)
            {
                WindowUtil.RECT r = (WindowUtil.RECT)Marshal.PtrToStructure(m.LParam, typeof(WindowUtil.RECT));
                WindowUtil.SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    r.Left,
                    r.Top,
                    r.Right - r.Left,
                    r.Bottom - r.Top,
                    WindowUtil.SWP_NOZORDER | WindowUtil.SWP_NOACTIVATE);
                UpdateDialogLayout();
                Invalidate(true);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private int S(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * GetDpiScale(), MidpointRounding.AwayFromZero);
        }

        private void UpdateDialogLayout()
        {
            if (btnRestore == null || btnOk == null || btnCancel == null)
                return;

            int padH = S(24);
            int cardTop = S(64);
            int cardW = S(104);
            int cardH = S(68);
            int cardGap = S(16);

            for (int i = 0; i < 4; i++)
            {
                int cx = padH + i * (cardW + cardGap);
                cardHitRects[i] = new Rectangle(cx - S(4), cardTop - S(4), cardW + S(8), cardH + S(32));
            }

            int row2Top = S(178);
            int row2Height = S(64);
            int dropdownW = S(132);
            int dropdownH = S(34);
            alignDropdownRect = new Rectangle(
                ClientSize.Width - padH - dropdownW,
                row2Top + (row2Height - dropdownH) / 2,
                dropdownW,
                dropdownH);

            int sliderW = S(185);
            int sliderH = S(28);
            int valueLabelW = S(48);

            int row3Top = S(242);
            int row3Height = S(64);
            sizeSliderTrackRect = new Rectangle(
                ClientSize.Width - padH - valueLabelW - S(8) - sliderW,
                row3Top + (row3Height - sliderH) / 2,
                sliderW,
                sliderH);

            int row4Top = S(306);
            int row4Height = S(64);
            paddingXSliderTrackRect = new Rectangle(
                ClientSize.Width - padH - valueLabelW - S(8) - sliderW,
                row4Top + (row4Height - sliderH) / 2,
                sliderW,
                sliderH);

            int row5Top = S(370);
            int row5Height = S(64);
            paddingYSliderTrackRect = new Rectangle(
                ClientSize.Width - padH - valueLabelW - S(8) - sliderW,
                row5Top + (row5Height - sliderH) / 2,
                sliderW,
                sliderH);

            int footerTop = S(434);
            int footerH = S(60);
            int targetClientW = S(556);
            int targetClientH = footerTop + footerH;
            if (ClientSize.Width != targetClientW || ClientSize.Height != targetClientH)
            {
                ClientSize = new Size(targetClientW, targetClientH);
            }

            int btnH = S(34);
            int btnY = footerTop + (footerH - btnH) / 2;
            int restoreW = S(104);
            int actionW = S(82);
            int btnGap = S(10);

            btnRestore.Bounds = new Rectangle(padH, btnY, restoreW, btnH);
            btnCancel.Bounds = new Rectangle(ClientSize.Width - padH - actionW, btnY, actionW, btnH);
            btnOk.Bounds = new Rectangle(btnCancel.Left - btnGap - actionW, btnY, actionW, btnH);
        }

        private static FenceTitlePosition GetCardPosition(int index)
        {
            switch (index)
            {
                case 0:
                    return FenceTitlePosition.Bottom;
                case 1:
                    return FenceTitlePosition.Top;
                case 2:
                    return FenceTitlePosition.Left;
                default:
                    return FenceTitlePosition.Right;
            }
        }

        private static string GetCardLabel(FenceTitlePosition position)
        {
            switch (position)
            {
                case FenceTitlePosition.Bottom:
                    return ThemeText.Key(LKeys.TitleSettingsPosBottom);
                case FenceTitlePosition.Top:
                    return ThemeText.Key(LKeys.TitleSettingsPosTop);
                case FenceTitlePosition.Left:
                    return ThemeText.Key(LKeys.TitleSettingsPosLeft);
                default:
                    return ThemeText.Key(LKeys.TitleSettingsPosRight);
            }
        }

        private static string GetAlignmentLabel(FenceTitleTextAlignment alignment)
        {
            switch (alignment)
            {
                case FenceTitleTextAlignment.Left:
                    return ThemeText.Key(LKeys.TitleSettingsAlignLeft);
                case FenceTitleTextAlignment.Right:
                    return ThemeText.Key(LKeys.TitleSettingsAlignRight);
                default:
                    return ThemeText.Key(LKeys.TitleSettingsAlignCenter);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(palette.CardBackground);

            int padH = S(24);
            int contentW = ClientSize.Width - padH * 2;

            using (var titleFont = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Regular))
            using (var descFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular))
            using (var labelFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular))
            using (var valueFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
            using (var divPen = new Pen(palette.DividerColor, 1f))
            {
                // =========================================================
                // 1. 标题栏位置区域（参考图顶部卡片组）
                // =========================================================
                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPositionTitle),
                    titleFont,
                    new Rectangle(padH, S(16), contentW, S(22)),
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPositionDesc),
                    descFont,
                    new Rectangle(padH, S(38), contentW, S(20)),
                    palette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                int cardTop = S(66);
                int cardW = S(104);
                int cardH = S(68);
                int cardGap = S(16);

                for (int i = 0; i < 4; i++)
                {
                    FenceTitlePosition pos = GetCardPosition(i);
                    int cx = padH + i * (cardW + cardGap);
                    var thumbRect = new RectangleF(cx, cardTop, cardW, cardH);
                    bool isSelected = selectedPosition == pos;
                    bool isHovered = hoveredCardIndex == i;

                    DrawPositionPreviewCard(g, thumbRect, pos, isSelected, isHovered);

                    var labelRect = new Rectangle(cx, cardTop + cardH + S(8), cardW + S(8), S(22));
                    TextRenderer.DrawText(
                        g,
                        GetCardLabel(pos),
                        labelFont,
                        labelRect,
                        palette.PrimaryText,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                }

                int div1Y = S(176);
                g.DrawLine(divPen, 0, div1Y, ClientSize.Width, div1Y);

                // =========================================================
                // 2. 标题文本对齐区域（参考图中部下拉选择行）
                // =========================================================
                int row2Top = div1Y + 1;
                int row2TextW = Math.Max(100, alignDropdownRect.Left - padH - S(16));
                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsAlignTitle),
                    titleFont,
                    new Rectangle(padH, row2Top + S(12), row2TextW, S(22)),
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsAlignDesc),
                    descFont,
                    new Rectangle(padH, row2Top + S(34), row2TextW, S(20)),
                    palette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                DrawAlignmentDropdown(g, alignDropdownRect, labelFont);

                int div2Y = S(241);
                g.DrawLine(divPen, 0, div2Y, ClientSize.Width, div2Y);

                // =========================================================
                // 3. 标题栏大小区域（滑块 + px 数值显示）
                // =========================================================
                int row3Top = div2Y + 1;
                int row3TextW = Math.Max(100, sizeSliderTrackRect.Left - padH - S(16));
                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsSizeTitle),
                    titleFont,
                    new Rectangle(padH, row3Top + S(12), row3TextW, S(22)),
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsSizeDesc),
                    descFont,
                    new Rectangle(padH, row3Top + S(34), row3TextW, S(20)),
                    palette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                float sizeRatio = (selectedFontSize - MinTitleFontSize) / (float)(MaxTitleFontSize - MinTitleFontSize);
                DrawCustomSlider(g, sizeSliderTrackRect, sizeRatio, hoveredSlider == ActiveSlider.Size, activeSlider == ActiveSlider.Size);

                var sizeValRect = new Rectangle(
                    sizeSliderTrackRect.Right + S(6),
                    row3Top,
                    ClientSize.Width - padH - (sizeSliderTrackRect.Right + S(6)),
                    S(64));
                TextRenderer.DrawText(
                    g,
                    selectedFontSize + "px",
                    valueFont,
                    sizeValRect,
                    palette.PrimaryText,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                int div3Y = S(305);
                g.DrawLine(divPen, 0, div3Y, ClientSize.Width, div3Y);

                // =========================================================
                // 4. 左右内边距区域（滑块 + px 数值显示）
                // =========================================================
                int row4Top = div3Y + 1;
                int row4TextW = Math.Max(100, paddingXSliderTrackRect.Left - padH - S(16));
                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPaddingXTitle),
                    titleFont,
                    new Rectangle(padH, row4Top + S(12), row4TextW, S(22)),
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPaddingXDesc),
                    descFont,
                    new Rectangle(padH, row4Top + S(34), row4TextW, S(20)),
                    palette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                float padXRatio = (selectedPaddingX - MinTitlePadding) / (float)(MaxTitlePadding - MinTitlePadding);
                DrawCustomSlider(g, paddingXSliderTrackRect, padXRatio, hoveredSlider == ActiveSlider.PaddingX, activeSlider == ActiveSlider.PaddingX);

                var padXValRect = new Rectangle(
                    paddingXSliderTrackRect.Right + S(6),
                    row4Top,
                    ClientSize.Width - padH - (paddingXSliderTrackRect.Right + S(6)),
                    S(64));
                TextRenderer.DrawText(
                    g,
                    selectedPaddingX + "px",
                    valueFont,
                    padXValRect,
                    palette.PrimaryText,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                int div4Y = S(369);
                g.DrawLine(divPen, 0, div4Y, ClientSize.Width, div4Y);

                // =========================================================
                // 5. 上下内边距区域（滑块 + px 数值显示）
                // =========================================================
                int row5Top = div4Y + 1;
                int row5TextW = Math.Max(100, paddingYSliderTrackRect.Left - padH - S(16));
                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPaddingYTitle),
                    titleFont,
                    new Rectangle(padH, row5Top + S(12), row5TextW, S(22)),
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    g,
                    ThemeText.Key(LKeys.TitleSettingsPaddingYDesc),
                    descFont,
                    new Rectangle(padH, row5Top + S(34), row5TextW, S(20)),
                    palette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                float padYRatio = (selectedPaddingY - MinTitlePadding) / (float)(MaxTitlePadding - MinTitlePadding);
                DrawCustomSlider(g, paddingYSliderTrackRect, padYRatio, hoveredSlider == ActiveSlider.PaddingY, activeSlider == ActiveSlider.PaddingY);

                var padYValRect = new Rectangle(
                    paddingYSliderTrackRect.Right + S(6),
                    row5Top,
                    ClientSize.Width - padH - (paddingYSliderTrackRect.Right + S(6)),
                    S(64));
                TextRenderer.DrawText(
                    g,
                    selectedPaddingY + "px",
                    valueFont,
                    padYValRect,
                    palette.PrimaryText,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                // =========================================================
                // 6. 底部操作栏分隔线与背景
                // =========================================================
                int footerTop = S(434);
                using (var footerBrush = new SolidBrush(palette.ContentBackground))
                {
                    g.FillRectangle(footerBrush, 0, footerTop, ClientSize.Width, ClientSize.Height - footerTop);
                }
                g.DrawLine(divPen, 0, footerTop, ClientSize.Width, footerTop);
            }
        }

        /// <summary>
        /// 绘制参考图风格的标题栏位置缩略预览图（含蓝色壁纸花瓣层次、四向标题栏条带与 5 个圆点指示器）。
        /// </summary>
        private void DrawPositionPreviewCard(
            Graphics g,
            RectangleF thumbRect,
            FenceTitlePosition position,
            bool isSelected,
            bool isHovered)
        {
            float scale = GetDpiScale();
            float outerPad = 3.5f * scale;
            float thumbRadius = 5f * scale;

            // 选中态或悬停态外框（参考图中的圆角双层外框效果）
            if (isSelected || isHovered)
            {
                var outerRect = new RectangleF(
                    thumbRect.X - outerPad,
                    thumbRect.Y - outerPad,
                    thumbRect.Width + outerPad * 2f,
                    thumbRect.Height + outerPad * 2f);
                Color ringColor = isSelected
                    ? palette.AccentBlue
                    : ThemeDrawing.WithAlpha(palette.SecondaryText, 110);
                float ringWidth = isSelected ? Math.Max(1.8f, 2f * scale) : 1.2f * scale;
                using (var outerPath = ThemeDrawing.CreateRoundedRectangle(outerRect, thumbRadius + outerPad * 0.8f))
                using (var ringPen = new Pen(ringColor, ringWidth))
                {
                    g.DrawPath(ringPen, outerPath);
                }
            }

            using (var clipPath = ThemeDrawing.CreateRoundedRectangle(thumbRect, thumbRadius))
            {
                var state = g.Save();
                g.SetClip(clipPath, CombineMode.Intersect);

                // 1. 桌面浅蓝渐变底色
                using (var skyBrush = new LinearGradientBrush(
                    thumbRect,
                    Color.FromArgb(148, 189, 232),
                    Color.FromArgb(208, 226, 245),
                    90f))
                {
                    g.FillRectangle(skyBrush, thumbRect);
                }

                // 2. 绘制立体蓝色花瓣层次（模拟参考图中的 Win11 Bloom 壁纸视觉效果）
                DrawBloomWallpaperMotif(g, thumbRect);

                // 3. 绘制对应方向的浅色标题栏条带与 5 个圆点
                float barThickness = Math.Max(11f, 13f * scale);
                RectangleF barRect;
                bool verticalBar = position == FenceTitlePosition.Left || position == FenceTitlePosition.Right;

                switch (position)
                {
                    case FenceTitlePosition.Bottom:
                        barRect = new RectangleF(thumbRect.X, thumbRect.Bottom - barThickness, thumbRect.Width, barThickness);
                        break;
                    case FenceTitlePosition.Top:
                        barRect = new RectangleF(thumbRect.X, thumbRect.Y, thumbRect.Width, barThickness);
                        break;
                    case FenceTitlePosition.Left:
                        barRect = new RectangleF(thumbRect.X, thumbRect.Y, barThickness, thumbRect.Height);
                        break;
                    default:
                        barRect = new RectangleF(thumbRect.Right - barThickness, thumbRect.Y, barThickness, thumbRect.Height);
                        break;
                }

                using (var barBrush = new SolidBrush(Color.FromArgb(232, 224, 233, 246)))
                using (var barLinePen = new Pen(Color.FromArgb(140, 160, 178, 204), 1f))
                {
                    g.FillRectangle(barBrush, barRect);
                    if (position == FenceTitlePosition.Bottom)
                        g.DrawLine(barLinePen, barRect.Left, barRect.Top, barRect.Right, barRect.Top);
                    else if (position == FenceTitlePosition.Top)
                        g.DrawLine(barLinePen, barRect.Left, barRect.Bottom, barRect.Right, barRect.Bottom);
                    else if (position == FenceTitlePosition.Left)
                        g.DrawLine(barLinePen, barRect.Right, barRect.Top, barRect.Right, barRect.Bottom);
                    else
                        g.DrawLine(barLinePen, barRect.Left, barRect.Top, barRect.Left, barRect.Bottom);
                }

                // 5 个圆点指示器
                float dotSize = Math.Max(3.5f, 4.2f * scale);
                float dotGap = Math.Max(2.2f, 2.8f * scale);
                float totalSpan = 5 * dotSize + 4 * dotGap;
                using (var dotBrush = new SolidBrush(Color.FromArgb(118, 124, 136)))
                {
                    if (!verticalBar)
                    {
                        float startX = barRect.X + (barRect.Width - totalSpan) / 2f;
                        float dotY = barRect.Y + (barRect.Height - dotSize) / 2f;
                        for (int d = 0; d < 5; d++)
                        {
                            g.FillEllipse(dotBrush, startX + d * (dotSize + dotGap), dotY, dotSize, dotSize);
                        }
                    }
                    else
                    {
                        float dotX = barRect.X + (barRect.Width - dotSize) / 2f;
                        float startY = barRect.Y + (barRect.Height - totalSpan) / 2f;
                        for (int d = 0; d < 5; d++)
                        {
                            g.FillEllipse(dotBrush, dotX, startY + d * (dotSize + dotGap), dotSize, dotSize);
                        }
                    }
                }

                g.Restore(state);

                // 缩略图内边框
                using (var borderPen = new Pen(Color.FromArgb(135, 145, 158, 176), 1f))
                {
                    g.DrawPath(borderPen, clipPath);
                }
            }
        }

        /// <summary>在位置预览卡片中绘制多层蓝色立体弧面，还原参考图中的壁纸缩略视觉。</summary>
        private static void DrawBloomWallpaperMotif(Graphics g, RectangleF r)
        {
            float cx = r.X + r.Width * 0.52f;
            float cy = r.Y + r.Height * 0.62f;
            float w = r.Width;
            float h = r.Height;

            var outerPetal = new RectangleF(cx - w * 0.36f, cy - h * 0.46f, w * 0.72f, h * 0.88f);
            using (var b1 = new LinearGradientBrush(
                outerPetal,
                Color.FromArgb(16, 98, 214),
                Color.FromArgb(8, 46, 128),
                45f))
            {
                g.FillEllipse(b1, outerPetal);
            }

            var leftPetal = new RectangleF(cx - w * 0.40f, cy - h * 0.26f, w * 0.50f, h * 0.66f);
            using (var b2 = new LinearGradientBrush(
                leftPetal,
                Color.FromArgb(54, 154, 248),
                Color.FromArgb(15, 78, 186),
                30f))
            {
                g.FillEllipse(b2, leftPetal);
            }

            var rightPetal = new RectangleF(cx - w * 0.08f, cy - h * 0.34f, w * 0.46f, h * 0.68f);
            using (var b3 = new LinearGradientBrush(
                rightPetal,
                Color.FromArgb(78, 176, 255),
                Color.FromArgb(20, 88, 198),
                120f))
            {
                g.FillEllipse(b3, rightPetal);
            }

            var centerCore = new RectangleF(cx - w * 0.20f, cy - h * 0.18f, w * 0.40f, h * 0.48f);
            using (var b4 = new LinearGradientBrush(
                centerCore,
                Color.FromArgb(120, 206, 255),
                Color.FromArgb(28, 110, 225),
                75f))
            {
                g.FillEllipse(b4, centerCore);
            }
        }

        private void DrawAlignmentDropdown(Graphics g, Rectangle bounds, Font font)
        {
            float scale = GetDpiScale();
            float radius = 6f * scale;
            var rect = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1f, bounds.Height - 1f);

            Color fillColor = isAlignDropdownHovered
                ? palette.ControlHoverBackground
                : palette.ControlBackground;
            Color borderColor = isAlignDropdownHovered
                ? ThemeDrawing.Mix(palette.ControlBorder, palette.PrimaryText, 0.22f)
                : palette.ControlBorder;

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            int padLeft = S(12);
            int arrowSpace = S(28);
            var textRect = new Rectangle(bounds.X + padLeft, bounds.Y, bounds.Width - padLeft - arrowSpace, bounds.Height);
            TextRenderer.DrawText(
                g,
                GetAlignmentLabel(selectedAlignment),
                font,
                textRect,
                palette.PrimaryText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            // 右侧下拉 V 型箭头
            float arrowCx = bounds.Right - S(15);
            float arrowCy = bounds.Y + bounds.Height / 2f;
            float aw = 4f * scale;
            float ah = 2.4f * scale;
            using (var arrowPen = new Pen(palette.SecondaryText, Math.Max(1.3f, 1.5f * scale))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                g.DrawLines(arrowPen, new[]
                {
                    new PointF(arrowCx - aw, arrowCy - ah * 0.5f),
                    new PointF(arrowCx, arrowCy + ah),
                    new PointF(arrowCx + aw, arrowCy - ah * 0.5f)
                });
            }
        }

        private void DrawCustomSlider(Graphics g, Rectangle bounds, float ratio, bool isHovered, bool isDragging)
        {
            float scale = GetDpiScale();
            float trackH = Math.Max(4f, 4.5f * scale);
            float thumbR = Math.Max(7f, 8f * scale);
            float trackLeft = bounds.X + thumbR;
            float trackRight = bounds.Right - thumbR;
            float trackW = Math.Max(10f, trackRight - trackLeft);
            float cy = bounds.Y + bounds.Height / 2f;

            ratio = Math.Max(0f, Math.Min(1f, ratio));
            float thumbCx = trackLeft + trackW * ratio;

            var fullTrack = new RectangleF(trackLeft, cy - trackH / 2f, trackW, trackH);
            using (var trackPath = ThemeDrawing.CreateRoundedRectangle(fullTrack, trackH / 2f))
            using (var trackBrush = new SolidBrush(palette.SwitchOffTrack))
            {
                g.FillPath(trackBrush, trackPath);
            }

            if (thumbCx > trackLeft + 1f)
            {
                var activeTrack = new RectangleF(trackLeft, cy - trackH / 2f, thumbCx - trackLeft, trackH);
                using (var activePath = ThemeDrawing.CreateRoundedRectangle(activeTrack, trackH / 2f))
                using (var activeBrush = new SolidBrush(palette.AccentBlue))
                {
                    g.FillPath(activeBrush, activePath);
                }
            }

            var thumbRect = new RectangleF(thumbCx - thumbR, cy - thumbR, thumbR * 2f, thumbR * 2f);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            using (var outerBrush = new SolidBrush(Color.White))
            using (var outerPen = new Pen(palette.ControlBorder, 1f))
            using (var innerBrush = new SolidBrush(isDragging ? palette.AccentPressedBlue : palette.AccentBlue))
            {
                g.FillEllipse(shadowBrush, thumbRect.X, thumbRect.Y + 1f, thumbRect.Width, thumbRect.Height);
                g.FillEllipse(outerBrush, thumbRect);
                g.DrawEllipse(outerPen, thumbRect);

                float innerR = thumbR * (isHovered || isDragging ? 0.58f : 0.48f);
                g.FillEllipse(innerBrush, thumbCx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
            }
        }

        private void ShowAlignmentPopup()
        {
            alignmentPopup?.Dispose();

            var menuControl = new AlignmentFlyoutMenuControl(
                palette,
                selectedAlignment,
                GetDpiScale(),
                chosen =>
                {
                    selectedAlignment = chosen;
                    alignmentPopup?.Close();
                    NotifyPreviewChanged();
                    Invalidate();
                });

            var host = new ToolStripControlHost(menuControl)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = menuControl.Size
            };

            alignmentPopup = new ToolStripDropDown
            {
                AutoSize = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                DropShadowEnabled = true,
                BackColor = palette.CardBackground
            };
            alignmentPopup.Items.Add(host);
            alignmentPopup.Show(this, new Point(alignDropdownRect.Left, alignDropdownRect.Top - S(4)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (activeSlider != ActiveSlider.None)
            {
                UpdateActiveSliderFromMouse(e.X);
                return;
            }

            int hitCard = -1;
            for (int i = 0; i < 4; i++)
            {
                if (cardHitRects[i].Contains(e.Location))
                {
                    hitCard = i;
                    break;
                }
            }

            bool hitDropdown = alignDropdownRect.Contains(e.Location);
            ActiveSlider hitSlider = ActiveSlider.None;
            if (sizeSliderTrackRect.Contains(e.Location))
                hitSlider = ActiveSlider.Size;
            else if (paddingXSliderTrackRect.Contains(e.Location))
                hitSlider = ActiveSlider.PaddingX;
            else if (paddingYSliderTrackRect.Contains(e.Location))
                hitSlider = ActiveSlider.PaddingY;

            if (hitCard != hoveredCardIndex ||
                hitDropdown != isAlignDropdownHovered ||
                hitSlider != hoveredSlider)
            {
                hoveredCardIndex = hitCard;
                isAlignDropdownHovered = hitDropdown;
                hoveredSlider = hitSlider;
                Cursor = (hitCard >= 0 || hitDropdown || hitSlider != ActiveSlider.None) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (activeSlider == ActiveSlider.None)
            {
                hoveredCardIndex = -1;
                isAlignDropdownHovered = false;
                hoveredSlider = ActiveSlider.None;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            for (int i = 0; i < 4; i++)
            {
                if (cardHitRects[i].Contains(e.Location))
                {
                    selectedPosition = GetCardPosition(i);
                    UpdateAdaptedTitleHeight();
                    NotifyPreviewChanged();
                    Invalidate();
                    return;
                }
            }

            if (alignDropdownRect.Contains(e.Location))
            {
                ShowAlignmentPopup();
                return;
            }

            if (sizeSliderTrackRect.Contains(e.Location))
            {
                activeSlider = ActiveSlider.Size;
                Capture = true;
                UpdateActiveSliderFromMouse(e.X);
            }
            else if (paddingXSliderTrackRect.Contains(e.Location))
            {
                activeSlider = ActiveSlider.PaddingX;
                Capture = true;
                UpdateActiveSliderFromMouse(e.X);
            }
            else if (paddingYSliderTrackRect.Contains(e.Location))
            {
                activeSlider = ActiveSlider.PaddingY;
                Capture = true;
                UpdateActiveSliderFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (activeSlider != ActiveSlider.None)
            {
                activeSlider = ActiveSlider.None;
                Capture = false;
                Invalidate();
            }
        }

        private void UpdateActiveSliderFromMouse(int mouseX)
        {
            switch (activeSlider)
            {
                case ActiveSlider.Size:
                    UpdateSizeSliderFromMouse(mouseX);
                    break;
                case ActiveSlider.PaddingX:
                    UpdatePaddingXSliderFromMouse(mouseX);
                    break;
                case ActiveSlider.PaddingY:
                    UpdatePaddingYSliderFromMouse(mouseX);
                    break;
            }
        }

        private float GetSliderRatio(Rectangle trackRect, int mouseX)
        {
            float scale = GetDpiScale();
            float thumbR = Math.Max(7f, 8f * scale);
            float trackLeft = trackRect.X + thumbR;
            float trackRight = trackRect.Right - thumbR;
            float trackW = Math.Max(1f, trackRight - trackLeft);

            float ratio = (mouseX - trackLeft) / trackW;
            return Math.Max(0f, Math.Min(1f, ratio));
        }

        private void UpdateSizeSliderFromMouse(int mouseX)
        {
            float ratio = GetSliderRatio(sizeSliderTrackRect, mouseX);
            int rawValue = (int)Math.Round(MinTitleFontSize + ratio * (MaxTitleFontSize - MinTitleFontSize));
            int nextValue = ClampFontSize(rawValue);
            if (nextValue != selectedFontSize)
            {
                selectedFontSize = nextValue;
                UpdateAdaptedTitleHeight();
                NotifyPreviewChanged();
                Invalidate();
            }
        }

        private void UpdatePaddingXSliderFromMouse(int mouseX)
        {
            float ratio = GetSliderRatio(paddingXSliderTrackRect, mouseX);
            int nextValue = ClampPadding((int)Math.Round(MinTitlePadding + ratio * (MaxTitlePadding - MinTitlePadding)));
            if (nextValue != selectedPaddingX)
            {
                selectedPaddingX = nextValue;
                UpdateAdaptedTitleHeight();
                NotifyPreviewChanged();
                Invalidate();
            }
        }

        private void UpdatePaddingYSliderFromMouse(int mouseX)
        {
            float ratio = GetSliderRatio(paddingYSliderTrackRect, mouseX);
            int nextValue = ClampPadding((int)Math.Round(MinTitlePadding + ratio * (MaxTitlePadding - MinTitlePadding)));
            if (nextValue != selectedPaddingY)
            {
                selectedPaddingY = nextValue;
                UpdateAdaptedTitleHeight();
                NotifyPreviewChanged();
                Invalidate();
            }
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            selectedPosition = FenceTitlePosition.Top;
            selectedAlignment = FenceTitleTextAlignment.Center;
            selectedPaddingX = DefaultTitlePaddingX;
            selectedPaddingY = DefaultTitlePaddingY;
            selectedFontSize = DefaultTitleFontSize;
            UpdateAdaptedTitleHeight();
            NotifyPreviewChanged();
            Invalidate();
        }

        /// <summary>
        /// 根据标题栏位置、标题文本大小与内边距自动计算撑开的标题栏尺寸（逻辑像素）。
        /// 行高固定为 1：
        /// - 横向标题栏（顶/底）：标题栏高度跟随标题文本大小和上下内边距自动撑开（TitleHeight = fontSize + padY * 2）；
        /// - 纵向标题栏（左/右）：标题栏厚度跟随单字宽度和左右内边距自动撑开（TitleHeight = fontSize + padX * 2）。
        /// </summary>
        public static int CalculateAdaptedTitleHeight(FenceTitlePosition position, int fontSize, int padX, int padY)
        {
            int safeFontSize = Math.Max(MinTitleFontSize, fontSize);
            if (position == FenceTitlePosition.Left || position == FenceTitlePosition.Right)
            {
                return safeFontSize + padX * 2;
            }
            return safeFontSize + padY * 2;
        }

        private void UpdateAdaptedTitleHeight()
        {
            selectedHeight = CalculateAdaptedTitleHeight(selectedPosition, selectedFontSize, selectedPaddingX, selectedPaddingY);
        }

        private void NotifyPreviewChanged()
        {
            UpdateAdaptedTitleHeight();
            PreviewChanged?.Invoke(selectedPosition, selectedAlignment, selectedFontSize, selectedHeight, selectedPaddingX, selectedPaddingY);
        }

        public static int ClampFontSize(int value)
        {
            if (value < MinTitleFontSize)
                return MinTitleFontSize;
            if (value > MaxTitleFontSize)
                return MaxTitleFontSize;
            return value;
        }

        public static int ClampPadding(int value)
        {
            if (value < MinTitlePadding)
                return MinTitlePadding;
            if (value > MaxTitlePadding)
                return MaxTitlePadding;
            return value;
        }

        /// <summary>
        /// 还原参考图中带有左侧胶囊指示条的现代圆角弹出下拉列表（靠左 / 居中 / 靠右）。
        /// </summary>
        private sealed class AlignmentFlyoutMenuControl : Control
        {
            private readonly SettingsPalette palette;
            private readonly FenceTitleTextAlignment currentSelection;
            private readonly float scale;
            private readonly Action<FenceTitleTextAlignment> onSelect;
            private readonly FenceTitleTextAlignment[] options =
            {
                FenceTitleTextAlignment.Left,
                FenceTitleTextAlignment.Center,
                FenceTitleTextAlignment.Right
            };
            private int hoveredIndex = -1;

            public AlignmentFlyoutMenuControl(
                SettingsPalette palette,
                FenceTitleTextAlignment currentSelection,
                float scale,
                Action<FenceTitleTextAlignment> onSelect)
            {
                this.palette = palette ?? SettingsPalette.CreateDark();
                this.currentSelection = currentSelection;
                this.scale = Math.Max(1f, scale);
                this.onSelect = onSelect;

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint,
                    true);

                int width = (int)Math.Round(136 * this.scale);
                int itemH = (int)Math.Round(36 * this.scale);
                int padV = (int)Math.Round(6 * this.scale);
                Size = new Size(width, options.Length * itemH + padV * 2);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int hit = HitTestItem(e.Location);
                if (hit != hoveredIndex)
                {
                    hoveredIndex = hit;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hoveredIndex = -1;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.Button != MouseButtons.Left)
                    return;

                int hit = HitTestItem(e.Location);
                if (hit >= 0 && hit < options.Length)
                {
                    onSelect?.Invoke(options[hit]);
                }
            }

            private Rectangle GetItemBounds(int index)
            {
                int padH = (int)Math.Round(6 * scale);
                int padV = (int)Math.Round(6 * scale);
                int itemH = (int)Math.Round(36 * scale);
                return new Rectangle(padH, padV + index * itemH, Width - padH * 2, itemH - (int)Math.Round(2 * scale));
            }

            private int HitTestItem(Point pt)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (GetItemBounds(i).Contains(pt))
                        return i;
                }
                return -1;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(palette.CardBackground);

                var outerRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using (var outerPath = ThemeDrawing.CreateRoundedRectangle(outerRect, 8f * scale))
                using (var bgBrush = new SolidBrush(palette.CardBackground))
                using (var borderPen = new Pen(palette.ControlBorder, 1f))
                using (var font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular))
                {
                    g.FillPath(bgBrush, outerPath);

                    for (int i = 0; i < options.Length; i++)
                    {
                        FenceTitleTextAlignment opt = options[i];
                        Rectangle itemRect = GetItemBounds(i);
                        bool isSelected = opt == currentSelection;
                        bool isHovered = i == hoveredIndex;

                        if (isSelected || isHovered)
                        {
                            Color highlight = isHovered
                                ? palette.ControlHoverBackground
                                : ThemeDrawing.Mix(palette.CardBackground, palette.ControlHoverBackground, 0.65f);
                            using (var itemPath = ThemeDrawing.CreateRoundedRectangle(itemRect, 5f * scale))
                            using (var itemBrush = new SolidBrush(highlight))
                            {
                                g.FillPath(itemBrush, itemPath);
                            }
                        }

                        if (isSelected)
                        {
                            // 左侧竖向高亮条（还原参考图中“靠左”左侧的竖条指示器）
                            float pillW = Math.Max(3f, 3.2f * scale);
                            float pillH = Math.Max(12f, 15f * scale);
                            var pillRect = new RectangleF(
                                itemRect.X + 4f * scale,
                                itemRect.Y + (itemRect.Height - pillH) / 2f,
                                pillW,
                                pillH);
                            using (var pillPath = ThemeDrawing.CreateRoundedRectangle(pillRect, pillW / 2f))
                            using (var pillBrush = new SolidBrush(palette.AccentBlue))
                            {
                                g.FillPath(pillBrush, pillPath);
                            }
                        }

                        int textLeft = itemRect.X + (int)Math.Round(14 * scale);
                        var textRect = new Rectangle(textLeft, itemRect.Y, itemRect.Width - (int)Math.Round(18 * scale), itemRect.Height);
                        TextRenderer.DrawText(
                            g,
                            GetAlignmentLabel(opt),
                            font,
                            textRect,
                            palette.PrimaryText,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    }

                    g.DrawPath(borderPen, outerPath);
                }
            }
        }
    }
}
