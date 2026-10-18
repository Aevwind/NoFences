using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NoFences.Theming
{
    /// <summary>
    /// 缩放比例变更事件参数。区分拖动过程中的实时预览与松开鼠标/点击按钮后的最终提交。
    /// </summary>
    internal sealed class ScaleChangedEventArgs : EventArgs
    {
        /// <summary>初始化缩放比例变更事件参数。</summary>
        public ScaleChangedEventArgs(int scalePercent, bool isPreview)
        {
            ScalePercent = scalePercent;
            IsPreview = isPreview;
        }

        /// <summary>当前缩放比例百分比（10~200）。</summary>
        public int ScalePercent { get; }

        /// <summary>是否为滑块拖动过程中的实时预览（为 false 时表示需要落盘保存并刷新缩略图目标尺寸）。</summary>
        public bool IsPreview { get; }
    }

    /// <summary>
    /// 右键菜单“缩放比例”子菜单内嵌的自定义卡片控件。
    /// 提供 10%~200% 滑块、带上下微调箭头的数值框、快捷预设按钮（50%/100%/150%/200%）以及一键重置（100%）。
    /// </summary>
    internal sealed class ScaleMenuControl : Control
    {
        /// <summary>最小缩放比例百分比。</summary>
        public const int MinScale = 10;

        /// <summary>最大缩放比例百分比。</summary>
        public const int MaxScale = 200;

        /// <summary>默认缩放比例百分比。</summary>
        public const int DefaultScale = 100;

        private const int LogicalWidth = 264;
        private const int LogicalHeight = 174;
        private static readonly int[] Presets = { 50, 100, 150, 200 };

        private enum HitZone
        {
            None = 0,
            Slider = 1,
            SpinnerUp = 2,
            SpinnerDown = 3,
            Preset0 = 4,
            Preset1 = 5,
            Preset2 = 6,
            Preset3 = 7,
            Reset = 8
        }

        private ThemeDefinition theme;
        private MenuThemeProfile profile;
        private int dpi = 96;
        private int scalePercent = DefaultScale;
        private bool isDraggingSlider;
        private HitZone hoveredZone = HitZone.None;
        private HitZone pressedZone = HitZone.None;
        private readonly Timer repeatTimer;
        private int repeatDirection;
        private int repeatTicks;

        private Rectangle headerIconBounds;
        private Rectangle headerTextBounds;
        private Rectangle sliderTrackBounds;
        private Rectangle sliderHitBounds;
        private Rectangle spinnerBoxBounds;
        private Rectangle spinnerTextBounds;
        private Rectangle spinnerUpBounds;
        private Rectangle spinnerDownBounds;
        private Rectangle percentLabelBounds;
        private readonly Rectangle[] presetBounds = new Rectangle[Presets.Length];
        private int separatorY;
        private int separatorLeft;
        private int separatorRight;
        private Rectangle resetRowBounds;
        private Rectangle resetIconBounds;
        private Rectangle resetTextBounds;

        /// <summary>当用户通过滑块、上下微调按钮、滚轮、预设按钮或重置改变缩放比例时触发。</summary>
        public event EventHandler<ScaleChangedEventArgs> ScaleChanged;

        /// <summary>初始化缩放比例弹出菜单卡片控件。</summary>
        public ScaleMenuControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
            DoubleBuffered = true;
            TabStop = false;

            theme = ThemePresets.CreateDefault();
            profile = MenuThemeProfile.Create(theme);
            RecalculateLayout();

            repeatTimer = new Timer { Interval = 320 };
            repeatTimer.Tick += RepeatTimer_Tick;
        }

        /// <summary>获取或设置当前显示的缩放比例（10~200），不触发变更事件。</summary>
        public int ScalePercent
        {
            get => scalePercent;
            set
            {
                int clamped = ClampScale(value);
                if (scalePercent == clamped)
                    return;
                scalePercent = clamped;
                Invalidate();
            }
        }

        /// <summary>同步当前菜单主题与显示器 DPI，并重新计算控件像素尺寸。</summary>
        public void ApplyTheme(ThemeDefinition newTheme, MenuThemeProfile newProfile, int targetDpi)
        {
            theme = (newTheme ?? ThemePresets.CreateDefault()).Clone();
            profile = newProfile ?? MenuThemeProfile.Create(theme);
            dpi = targetDpi > 0 ? targetDpi : (DeviceDpi > 0 ? DeviceDpi : 96);
            BackColor = theme.MenuBackgroundColor;
            ForeColor = theme.MenuTextColor;
            RecalculateLayout();
            Invalidate();
        }

        /// <summary>将缩放比例限制在 10%~200% 的有效范围内。</summary>
        public static int ClampScale(int value)
        {
            if (value < MinScale)
                return MinScale;
            if (value > MaxScale)
                return MaxScale;
            return value;
        }

        /// <summary>释放按住微调箭头时使用的自动连发定时器。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                repeatTimer.Stop();
                repeatTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>按当前 DPI 重新计算卡片内所有控件区域的像素坐标。</summary>
        private void RecalculateLayout()
        {
            int width = ScaleLogical(LogicalWidth);
            int height = ScaleLogical(LogicalHeight);
            Size = new Size(width, height);

            headerIconBounds = new Rectangle(
                ScaleLogical(16),
                ScaleLogical(13),
                ScaleLogical(16),
                ScaleLogical(16));
            headerTextBounds = new Rectangle(
                ScaleLogical(40),
                ScaleLogical(9),
                ScaleLogical(210),
                ScaleLogical(24));

            sliderTrackBounds = new Rectangle(
                ScaleLogical(18),
                ScaleLogical(56),
                ScaleLogical(140),
                Math.Max(2, ScaleLogical(4)));
            sliderHitBounds = new Rectangle(
                ScaleLogical(10),
                ScaleLogical(44),
                ScaleLogical(156),
                ScaleLogical(28));

            spinnerBoxBounds = new Rectangle(
                ScaleLogical(172),
                ScaleLogical(44),
                ScaleLogical(58),
                ScaleLogical(28));
            spinnerTextBounds = new Rectangle(
                spinnerBoxBounds.Left + ScaleLogical(2),
                spinnerBoxBounds.Top,
                ScaleLogical(36),
                spinnerBoxBounds.Height);
            int arrowWidth = ScaleLogical(18);
            int halfHeight = spinnerBoxBounds.Height / 2;
            spinnerUpBounds = new Rectangle(
                spinnerBoxBounds.Right - arrowWidth - ScaleLogical(2),
                spinnerBoxBounds.Top + 1,
                arrowWidth,
                halfHeight - 1);
            spinnerDownBounds = new Rectangle(
                spinnerBoxBounds.Right - arrowWidth - ScaleLogical(2),
                spinnerBoxBounds.Top + halfHeight,
                arrowWidth,
                spinnerBoxBounds.Height - halfHeight - 1);

            percentLabelBounds = new Rectangle(
                ScaleLogical(232),
                ScaleLogical(44),
                ScaleLogical(20),
                ScaleLogical(28));

            int presetLeft = ScaleLogical(14);
            int presetTop = ScaleLogical(84);
            int presetHeight = ScaleLogical(28);
            int presetGap = ScaleLogical(8);
            int totalPresetWidth = width - presetLeft * 2;
            int singlePresetWidth = (totalPresetWidth - presetGap * (Presets.Length - 1)) / Presets.Length;
            for (int i = 0; i < Presets.Length; i++)
            {
                presetBounds[i] = new Rectangle(
                    presetLeft + i * (singlePresetWidth + presetGap),
                    presetTop,
                    singlePresetWidth,
                    presetHeight);
            }

            separatorY = ScaleLogical(125);
            separatorLeft = ScaleLogical(14);
            separatorRight = width - ScaleLogical(14);

            resetRowBounds = new Rectangle(
                ScaleLogical(6),
                ScaleLogical(133),
                width - ScaleLogical(12),
                ScaleLogical(34));
            resetIconBounds = new Rectangle(
                ScaleLogical(16),
                ScaleLogical(142),
                ScaleLogical(16),
                ScaleLogical(16));
            resetTextBounds = new Rectangle(
                ScaleLogical(40),
                resetRowBounds.Top,
                resetRowBounds.Right - ScaleLogical(48),
                resetRowBounds.Height);
        }

        /// <summary>绘制缩放比例控制卡片的全部视觉元素。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            bool darkMenu = ThemeDrawing.IsDark(theme.MenuBackgroundColor);
            Color textColor = Color.FromArgb(255, theme.MenuTextColor);
            Color mutedTextColor = darkMenu
                ? Color.FromArgb(160, 160, 160)
                : Color.FromArgb(120, 120, 120);
            Color accentBlue = GetAccentBlue();

            using (var bgBrush = new SolidBrush(theme.MenuBackgroundColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 1. 顶部标题行（图标 + “缩放比例”）
            DrawHeaderScaleIcon(g, headerIconBounds, textColor);
            DrawLeftString(g, ThemeText.ScaleMenu, Font, headerTextBounds, textColor);

            // 2. 滑块轨道与圆形滑块把手
            DrawSlider(g, darkMenu, accentBlue);

            // 3. 右侧数值微调框与百分比符号
            DrawSpinnerBox(g, darkMenu, textColor, mutedTextColor);

            // 4. 快捷预设按钮行（50% / 100% / 150% / 200%）
            DrawPresetButtons(g, darkMenu, textColor, accentBlue);

            // 5. 分隔线
            using (var sepPen = new Pen(profile.SeparatorPrimaryColor))
            {
                g.DrawLine(sepPen, separatorLeft, separatorY, separatorRight, separatorY);
            }

            // 6. 底部“重置”操作行
            DrawResetRow(g, textColor);

            // 7. 外层圆角容器边框（保证子菜单弹出框四周边缘完整闭合）
            DrawOuterBorder(g);
        }

        /// <summary>绘制水平进度滑块与白色圆形拖拽手柄。</summary>
        private void DrawSlider(Graphics g, bool darkMenu, Color accentBlue)
        {
            float ratio = (scalePercent - MinScale) / (float)(MaxScale - MinScale);
            float thumbCenterX = sliderTrackBounds.Left + ratio * sliderTrackBounds.Width;
            float trackCenterY = sliderTrackBounds.Top + sliderTrackBounds.Height / 2f;
            float trackRadius = sliderTrackBounds.Height / 2f;

            Color inactiveTrackColor = darkMenu
                ? Color.FromArgb(65, 255, 255, 255)
                : Color.FromArgb(48, 0, 0, 0);

            using (var trackPath = ThemeDrawing.CreateRoundedRectangle(
                sliderTrackBounds,
                trackRadius))
            using (var inactiveBrush = new SolidBrush(inactiveTrackColor))
            {
                g.FillPath(inactiveBrush, trackPath);
            }

            float activeWidth = Math.Max(sliderTrackBounds.Height, thumbCenterX - sliderTrackBounds.Left);
            var activeTrackRect = new RectangleF(
                sliderTrackBounds.Left,
                sliderTrackBounds.Top,
                Math.Min(sliderTrackBounds.Width, activeWidth),
                sliderTrackBounds.Height);
            using (var activePath = ThemeDrawing.CreateRoundedRectangle(
                activeTrackRect,
                trackRadius))
            using (var activeBrush = new SolidBrush(accentBlue))
            {
                g.FillPath(activeBrush, activePath);
            }

            bool thumbActive = isDraggingSlider || hoveredZone == HitZone.Slider;
            float thumbDiameter = ScaleLogical(thumbActive ? 16 : 14);
            var thumbRect = new RectangleF(
                thumbCenterX - thumbDiameter / 2f,
                trackCenterY - thumbDiameter / 2f,
                thumbDiameter,
                thumbDiameter);

            using (var shadowBrush = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
            {
                var shadowRect = thumbRect;
                shadowRect.Offset(0f, Math.Max(1f, GetDpiScale()));
                g.FillEllipse(shadowBrush, shadowRect);
            }

            using (var thumbBrush = new SolidBrush(Color.White))
            using (var thumbBorder = new Pen(
                darkMenu ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(75, 0, 0, 0),
                Math.Max(1f, GetDpiScale())))
            {
                g.FillEllipse(thumbBrush, thumbRect);
                g.DrawEllipse(thumbBorder, thumbRect);
            }
        }

        /// <summary>绘制带上下微调箭头的圆角数值框及右侧百分号。</summary>
        private void DrawSpinnerBox(
            Graphics g,
            bool darkMenu,
            Color textColor,
            Color mutedTextColor)
        {
            bool spinnerHovered = hoveredZone == HitZone.SpinnerUp || hoveredZone == HitZone.SpinnerDown;
            Color boxFill = darkMenu
                ? Color.FromArgb(38, 0, 0, 0)
                : Color.FromArgb(14, 0, 0, 0);
            Color boxBorder = spinnerHovered
                ? (darkMenu ? Color.FromArgb(115, 255, 255, 255) : Color.FromArgb(110, 0, 0, 0))
                : (darkMenu ? Color.FromArgb(62, 255, 255, 255) : Color.FromArgb(58, 0, 0, 0));

            int boxRadius = ScaleLogical(5);
            using (var path = ThemeDrawing.CreateRoundedRectangle(spinnerBoxBounds, boxRadius))
            using (var fill = new SolidBrush(boxFill))
            using (var pen = new Pen(boxBorder, Math.Max(1f, GetDpiScale())))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            if (hoveredZone == HitZone.SpinnerUp || hoveredZone == HitZone.SpinnerDown)
            {
                Rectangle arrowHighlight = hoveredZone == HitZone.SpinnerUp
                    ? spinnerUpBounds
                    : spinnerDownBounds;
                Color hoverColor = darkMenu
                    ? Color.FromArgb(36, 255, 255, 255)
                    : Color.FromArgb(24, 0, 0, 0);
                using (var path = ThemeDrawing.CreateRoundedRectangle(arrowHighlight, ScaleLogical(3)))
                using (var brush = new SolidBrush(hoverColor))
                {
                    g.FillPath(brush, path);
                }
            }

            DrawCenteredString(g, scalePercent.ToString(), Font, spinnerTextBounds, textColor);

            Color upColor = hoveredZone == HitZone.SpinnerUp ? textColor : mutedTextColor;
            Color downColor = hoveredZone == HitZone.SpinnerDown ? textColor : mutedTextColor;
            DrawSmallChevron(g, spinnerUpBounds, true, upColor);
            DrawSmallChevron(g, spinnerDownBounds, false, downColor);

            DrawCenteredString(g, "%", Font, percentLabelBounds, mutedTextColor);
        }

        /// <summary>绘制 4 个快捷缩放比例预设按钮（50% / 100% / 150% / 200%）。</summary>
        private void DrawPresetButtons(
            Graphics g,
            bool darkMenu,
            Color textColor,
            Color accentBlue)
        {
            int radius = ScaleLogical(5);
            float borderWidth = Math.Max(1f, GetDpiScale());

            for (int i = 0; i < Presets.Length; i++)
            {
                int preset = Presets[i];
                Rectangle bounds = presetBounds[i];
                bool isSelected = scalePercent == preset;
                HitZone zone = (HitZone)((int)HitZone.Preset0 + i);
                bool isHovered = hoveredZone == zone;

                Color fillColor;
                Color borderColor;
                Color labelColor;

                if (isSelected)
                {
                    fillColor = ThemeDrawing.Mix(
                        theme.MenuBackgroundColor,
                        accentBlue,
                        darkMenu ? 0.48f : 0.82f);
                    borderColor = ThemeDrawing.Mix(accentBlue, Color.White, darkMenu ? 0.25f : 0f);
                    labelColor = Color.White;
                }
                else if (isHovered)
                {
                    fillColor = darkMenu
                        ? Color.FromArgb(38, 255, 255, 255)
                        : Color.FromArgb(24, 0, 0, 0);
                    borderColor = darkMenu
                        ? Color.FromArgb(95, 255, 255, 255)
                        : Color.FromArgb(85, 0, 0, 0);
                    labelColor = textColor;
                }
                else
                {
                    fillColor = darkMenu
                        ? Color.FromArgb(14, 255, 255, 255)
                        : Color.FromArgb(8, 0, 0, 0);
                    borderColor = darkMenu
                        ? Color.FromArgb(58, 255, 255, 255)
                        : Color.FromArgb(52, 0, 0, 0);
                    labelColor = textColor;
                }

                using (var path = ThemeDrawing.CreateRoundedRectangle(bounds, radius))
                using (var fill = new SolidBrush(fillColor))
                using (var border = new Pen(borderColor, borderWidth))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                DrawCenteredString(g, preset + "%", Font, bounds, labelColor);
            }
        }

        /// <summary>绘制底部“重置”行及其悬停/按下背景和旋转箭头图标。</summary>
        private void DrawResetRow(Graphics g, Color textColor)
        {
            bool hovered = hoveredZone == HitZone.Reset;
            bool pressed = pressedZone == HitZone.Reset && hovered;
            Color itemTextColor = textColor;

            if (hovered || pressed)
            {
                Color highlightColor = pressed
                    ? profile.PressedColor
                    : theme.MenuHighlightColor;
                int itemRadius = ScaleLogical(Math.Max(4, profile.ItemCornerRadius));
                using (var path = ThemeDrawing.CreateRoundedRectangle(resetRowBounds, itemRadius))
                using (var brush = new SolidBrush(highlightColor))
                {
                    g.FillPath(brush, path);
                }
                itemTextColor = theme.MenuHighlightTextColor;
            }

            DrawResetIcon(g, resetIconBounds, itemTextColor);
            DrawLeftString(g, ThemeText.Reset, Font, resetTextBounds, itemTextColor);
        }

        /// <summary>绘制弹出卡片外边框，与主右键菜单的边框风格保持一致。</summary>
        private void DrawOuterBorder(Graphics g)
        {
            if (Width <= 1 || Height <= 1)
                return;

            if (profile.DrawClassicThreeDimensionalBorder)
            {
                int right = Width - 1;
                int bottom = Height - 1;
                using (var outer = new Pen(profile.BorderOuterColor))
                    g.DrawRectangle(outer, 0, 0, right, bottom);
                if (right >= 2 && bottom >= 2)
                {
                    using (var highlight = new Pen(profile.BorderHighlightColor))
                    using (var shadow = new Pen(profile.BorderShadowColor))
                    {
                        g.DrawLine(highlight, 1, 1, right - 1, 1);
                        g.DrawLine(highlight, 1, 1, 1, bottom - 1);
                        g.DrawLine(shadow, 1, bottom - 1, right - 1, bottom - 1);
                        g.DrawLine(shadow, right - 1, 1, right - 1, bottom - 1);
                    }
                }
                return;
            }

            int containerRadius = ScaleLogical(profile.ContainerCornerRadius);
            using (var path = ThemeDrawing.CreateRoundedRectangle(
                new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f),
                containerRadius))
            using (var pen = new Pen(profile.BorderOuterColor))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>绘制卡片顶部标题行左侧的单色矢量缩放图标。</summary>
        private void DrawHeaderScaleIcon(Graphics g, Rectangle bounds, Color color)
        {
            float scale = GetDpiScale();
            float strokeWidth = Math.Max(1.2f, 1.4f * scale);
            var rect = new RectangleF(
                bounds.X + 1.5f * scale,
                bounds.Y + 1.5f * scale,
                Math.Max(2f, bounds.Width - 3f * scale),
                Math.Max(2f, bounds.Height - 3f * scale));

            using (var pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                using (var framePath = ThemeDrawing.CreateRoundedRectangle(rect, 2.5f * scale))
                {
                    g.DrawPath(pen, framePath);
                }

                // 对角双向展开箭头（左下 ↙ 与右上 ↗）
                float x1 = rect.Left + rect.Width * 0.26f;
                float y1 = rect.Bottom - rect.Height * 0.26f;
                float x2 = rect.Right - rect.Width * 0.26f;
                float y2 = rect.Top + rect.Height * 0.26f;
                float arm = rect.Width * 0.22f;

                g.DrawLine(pen, x1, y1, x2, y2);
                g.DrawLines(pen, new[]
                {
                    new PointF(x2 - arm, y2),
                    new PointF(x2, y2),
                    new PointF(x2, y2 + arm)
                });
                g.DrawLines(pen, new[]
                {
                    new PointF(x1 + arm, y1),
                    new PointF(x1, y1),
                    new PointF(x1, y1 - arm)
                });
            }
        }

        /// <summary>绘制底部“重置”行左侧的环形逆时针重置箭头图标。</summary>
        private void DrawResetIcon(Graphics g, Rectangle bounds, Color color)
        {
            float scale = GetDpiScale();
            float strokeWidth = Math.Max(1.25f, 1.45f * scale);
            float inset = 2f * scale;
            var arcRect = new RectangleF(
                bounds.X + inset,
                bounds.Y + inset,
                Math.Max(2f, bounds.Width - inset * 2f),
                Math.Max(2f, bounds.Height - inset * 2f));

            using (var pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // 绘制 300 度圆弧与左上角箭头翼
                g.DrawArc(pen, arcRect, -145f, 300f);

                float rad = (float)(-145.0 * Math.PI / 180.0);
                float cx = arcRect.Left + arcRect.Width / 2f;
                float cy = arcRect.Top + arcRect.Height / 2f;
                float r = arcRect.Width / 2f;
                var tip = new PointF(
                    cx + (float)Math.Cos(rad) * r,
                    cy + (float)Math.Sin(rad) * r);
                float wing = 3.2f * scale;
                g.DrawLines(pen, new[]
                {
                    new PointF(tip.X, tip.Y - wing),
                    tip,
                    new PointF(tip.X + wing, tip.Y + 0.3f * scale)
                });
            }
        }

        /// <summary>绘制数值微调框右侧的上下折叠小箭头。</summary>
        private void DrawSmallChevron(Graphics g, Rectangle bounds, bool up, Color color)
        {
            float scale = GetDpiScale();
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f + (up ? 0.5f * scale : -0.5f * scale);
            float dx = 2.8f * scale;
            float dy = 1.5f * scale;

            PointF[] pts = up
                ? new[]
                {
                    new PointF(cx - dx, cy + dy),
                    new PointF(cx, cy - dy),
                    new PointF(cx + dx, cy + dy)
                }
                : new[]
                {
                    new PointF(cx - dx, cy - dy),
                    new PointF(cx, cy + dy),
                    new PointF(cx + dx, cy - dy)
                };

            using (var pen = new Pen(color, Math.Max(1.1f, 1.3f * scale)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        /// <summary>处理鼠标按下：开始滑块拖拽、启动上下箭头连发或记录按钮按下态。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            HitZone zone = HitTest(e.Location);
            pressedZone = zone;

            if (zone == HitZone.Slider)
            {
                isDraggingSlider = true;
                Capture = true;
                UpdateScaleFromSliderX(e.X, isPreview: true);
                Invalidate();
                return;
            }

            if (zone == HitZone.SpinnerUp || zone == HitZone.SpinnerDown)
            {
                int step = ModifierKeys.HasFlag(Keys.Shift) ? 5 : 1;
                repeatDirection = zone == HitZone.SpinnerUp ? step : -step;
                repeatTicks = 0;
                SetScaleInternal(scalePercent + repeatDirection, isPreview: false);
                repeatTimer.Interval = 320;
                repeatTimer.Start();
                Invalidate();
                return;
            }

            Invalidate();
        }

        /// <summary>处理鼠标移动：拖拽滑块实时更新比例，或更新各交互区域的悬停高亮状态。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDraggingSlider)
            {
                UpdateScaleFromSliderX(e.X, isPreview: true);
                return;
            }

            HitZone zone = HitTest(e.Location);
            if (zone != hoveredZone)
            {
                hoveredZone = zone;
                Cursor = zone != HitZone.None ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        /// <summary>处理鼠标松开：完成滑块提交、停止微调连发，或响应预设与重置点击。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            repeatTimer.Stop();

            if (isDraggingSlider)
            {
                isDraggingSlider = false;
                Capture = false;
                UpdateScaleFromSliderX(e.X, isPreview: false);
                hoveredZone = HitTest(e.Location);
                pressedZone = HitZone.None;
                Invalidate();
                return;
            }

            HitZone releaseZone = HitTest(e.Location);
            if (releaseZone == pressedZone)
            {
                if (releaseZone >= HitZone.Preset0 && releaseZone <= HitZone.Preset3)
                {
                    int index = (int)releaseZone - (int)HitZone.Preset0;
                    SetScaleInternal(Presets[index], isPreview: false);
                }
                else if (releaseZone == HitZone.Reset)
                {
                    SetScaleInternal(DefaultScale, isPreview: false);
                }
            }

            pressedZone = HitZone.None;
            hoveredZone = releaseZone;
            Invalidate();
        }

        /// <summary>鼠标离开卡片时清除悬停态并停止连发定时器。</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!isDraggingSlider)
            {
                repeatTimer.Stop();
                hoveredZone = HitZone.None;
                pressedZone = HitZone.None;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        /// <summary>支持在卡片上滚动鼠标滚轮以快速微调缩放比例。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta == 0)
                return;

            bool overSpinner = spinnerBoxBounds.Contains(e.Location);
            int step = (overSpinner || ModifierKeys.HasFlag(Keys.Shift)) ? 1 : 5;
            int delta = Math.Sign(e.Delta) * step;
            SetScaleInternal(scalePercent + delta, isPreview: false);
        }

        private void RepeatTimer_Tick(object sender, EventArgs e)
        {
            if (pressedZone != HitZone.SpinnerUp && pressedZone != HitZone.SpinnerDown)
            {
                repeatTimer.Stop();
                return;
            }

            repeatTicks++;
            if (repeatTicks == 1)
                repeatTimer.Interval = 55;

            SetScaleInternal(scalePercent + repeatDirection, isPreview: false);
        }

        private void UpdateScaleFromSliderX(int mouseX, bool isPreview)
        {
            int trackWidth = Math.Max(1, sliderTrackBounds.Width);
            float ratio = (mouseX - sliderTrackBounds.Left) / (float)trackWidth;
            if (ratio < 0f)
                ratio = 0f;
            else if (ratio > 1f)
                ratio = 1f;

            int newScale = (int)Math.Round(
                MinScale + ratio * (MaxScale - MinScale),
                MidpointRounding.AwayFromZero);
            SetScaleInternal(newScale, isPreview);
        }

        private void SetScaleInternal(int value, bool isPreview)
        {
            int clamped = ClampScale(value);
            bool changed = scalePercent != clamped;
            scalePercent = clamped;
            if (changed)
                Invalidate();

            if (changed || !isPreview)
                ScaleChanged?.Invoke(this, new ScaleChangedEventArgs(scalePercent, isPreview));
        }

        private HitZone HitTest(Point pt)
        {
            if (sliderHitBounds.Contains(pt))
                return HitZone.Slider;
            if (spinnerUpBounds.Contains(pt))
                return HitZone.SpinnerUp;
            if (spinnerDownBounds.Contains(pt))
                return HitZone.SpinnerDown;

            for (int i = 0; i < presetBounds.Length; i++)
            {
                if (presetBounds[i].Contains(pt))
                    return (HitZone)((int)HitZone.Preset0 + i);
            }

            if (resetRowBounds.Contains(pt))
                return HitZone.Reset;

            return HitZone.None;
        }

        private static void DrawLeftString(
            Graphics g,
            string text,
            Font font,
            Rectangle bounds,
            Color color)
        {
            int textHeight = font.Height;
            int yOffset = Math.Max(0, (bounds.Height - textHeight) / 2);
            var snappedBounds = new Rectangle(bounds.X, bounds.Y + yOffset, bounds.Width, textHeight);

            using (var brush = new SolidBrush(color))
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = StringAlignment.Near;
                g.DrawString(text, font, brush, snappedBounds, sf);
            }
        }

        private static void DrawCenteredString(
            Graphics g,
            string text,
            Font font,
            Rectangle bounds,
            Color color)
        {
            int textHeight = font.Height;
            int yOffset = Math.Max(0, (bounds.Height - textHeight) / 2);
            var snappedBounds = new Rectangle(bounds.X, bounds.Y + yOffset, bounds.Width, textHeight);

            using (var brush = new SolidBrush(color))
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = StringAlignment.Center;
                g.DrawString(text, font, brush, snappedBounds, sf);
            }
        }

        private Color GetAccentBlue()
        {
            Color refBlue = Color.FromArgb(66, 133, 244);
            return ThemeDrawing.Mix(refBlue, theme.AccentColor, 0.35f);
        }

        private float GetDpiScale()
        {
            return (dpi > 0 ? dpi : 96) / 96f;
        }

        private int ScaleLogical(int logicalPixels)
        {
            if (logicalPixels <= 0)
                return 0;
            return Math.Max(1, (int)Math.Round(
                logicalPixels * GetDpiScale(),
                MidpointRounding.AwayFromZero));
        }
    }
}
