using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace NoFences.Theming
{
    /// <summary>
    /// 支持主题动态着色的自定义控件契约。用于替代 WinForms 原生控件在深色/默认主题及禁用态下
    /// 强制使用系统灰白边框、经典旋转按钮及不可读灰色文字的问题。
    /// </summary>
    internal interface IThemedControl
    {
        /// <summary>应用当前主题配色与字体。</summary>
        void ApplyTheme(ThemeDefinition theme);
    }

    /// <summary>
    /// 现代圆角按钮控件。自绘圆角背景、边框与文字，解决原生 Button 在禁用态（Enabled=false）下
    /// 强制使用系统暗色文字导致在默认/黑暗模式背景上无法辨认的问题，同时支持颜色样本按钮模式。
    /// </summary>
    internal sealed class ThemedButton : Button, IThemedControl
    {
        private ThemeDefinition currentTheme = ThemePresets.CreateDefault();
        private bool isHovered;
        private bool isPressed;
        private bool isInteractive = true;

        /// <summary>初始化现代圆角按钮控件。</summary>
        public ThemedButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            MinimumSize = new Size(76, 30);
            Padding = new Padding(12, 4, 12, 4);
            Cursor = Cursors.Hand;
        }

        /// <summary>是否为颜色选择样本按钮（保留自定义 BackColor 作为色块展示）。</summary>
        public bool IsColorSwatch { get; set; }

        /// <summary>
        /// 获取或设置控件是否可交互。与原生 Enabled 同步，但自绘时保持高对比度可读文字。
        /// </summary>
        public bool IsInteractive
        {
            get => isInteractive && Enabled;
            set
            {
                isInteractive = value;
                Enabled = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        /// <summary>应用当前主题配色。</summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            currentTheme = theme != null ? theme.Clone() : ThemePresets.CreateDefault();
            Invalidate();
        }

        /// <summary>根据文本内容计算包含圆角内边距的推荐尺寸。</summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            float scale = GetDpiScale();
            Size textSize = TextRenderer.MeasureText(
                Text ?? string.Empty,
                Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int width = Math.Max(
                ScalePx(76, scale),
                textSize.Width + ScalePx(28, scale));
            int height = Math.Max(
                ScalePx(30, scale),
                textSize.Height + ScalePx(12, scale));
            return new Size(width, height);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left && IsInteractive)
            {
                isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            if (isPressed)
            {
                isPressed = false;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBack = Parent?.BackColor ?? currentTheme.DialogBackgroundColor;
            g.Clear(parentBack);

            float scale = GetDpiScale();
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (rect.Width <= 1f || rect.Height <= 1f)
                return;

            bool interactive = Enabled && isInteractive;
            Color fillColor;
            Color borderColor;
            Color textColor;

            if (IsColorSwatch)
            {
                fillColor = BackColor;
                bool darkSwatch = ThemeDrawing.IsDark(fillColor);
                textColor = interactive
                    ? (darkSwatch ? Color.White : Color.FromArgb(24, 24, 24))
                    : ThemeDrawing.Mix(
                        darkSwatch ? Color.White : Color.FromArgb(24, 24, 24),
                        fillColor,
                        0.28f);
                borderColor = interactive && isHovered
                    ? currentTheme.AccentColor
                    : ThemeDrawing.WithAlpha(currentTheme.BorderColor, interactive ? 170 : 110);
            }
            else
            {
                Color baseFill = ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogBackgroundColor,
                    0.15f);
                if (!interactive)
                {
                    fillColor = ThemeDrawing.Mix(
                        currentTheme.ControlBackgroundColor,
                        currentTheme.DialogBackgroundColor,
                        0.45f);
                    borderColor = ThemeDrawing.WithAlpha(currentTheme.BorderColor, 75);
                    textColor = ThemeDrawing.Mix(
                        currentTheme.ControlTextColor,
                        currentTheme.DialogBackgroundColor,
                        0.32f);
                }
                else if (isPressed)
                {
                    fillColor = ThemeDrawing.Mix(baseFill, currentTheme.AccentColor, 0.25f);
                    borderColor = currentTheme.AccentColor;
                    textColor = currentTheme.ControlTextColor;
                }
                else if (isHovered)
                {
                    fillColor = ThemeDrawing.Mix(baseFill, currentTheme.DialogTextColor, 0.08f);
                    borderColor = ThemeDrawing.Mix(currentTheme.BorderColor, currentTheme.DialogTextColor, 0.25f);
                    textColor = currentTheme.ControlTextColor;
                }
                else
                {
                    fillColor = baseFill;
                    borderColor = ThemeDrawing.WithAlpha(currentTheme.BorderColor, 145);
                    textColor = currentTheme.ControlTextColor;
                }
            }

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            TextRenderer.DrawText(
                g,
                Text ?? string.Empty,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        private float GetDpiScale()
        {
            return DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
        }

        private static int ScalePx(int logical, float scale)
        {
            return Math.Max(1, (int)Math.Round(logical * scale));
        }
    }

    /// <summary>
    /// 现代圆角文本输入框。内部封装无边框 TextBox，在只读/预置主题模式下采用高对比度自绘文本，
    /// 避免原生 TextBox 在 Enabled=false 时使用系统灰色文字导致深色背景下字迹模糊或不可见。
    /// </summary>
    internal sealed class ThemedTextBox : Control, IThemedControl
    {
        private readonly TextBox innerTextBox = new TextBox();
        private ThemeDefinition currentTheme = ThemePresets.CreateDefault();
        private bool isHovered;
        private bool isInteractive = true;

        /// <summary>初始化现代圆角文本输入框。</summary>
        public ThemedTextBox()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Size = new Size(200, 32);
            MinimumSize = new Size(80, 30);

            innerTextBox.BorderStyle = BorderStyle.None;
            innerTextBox.TextChanged += (s, e) =>
            {
                base.Text = innerTextBox.Text;
                Invalidate();
                OnTextChanged(e);
            };
            innerTextBox.GotFocus += (s, e) => Invalidate();
            innerTextBox.LostFocus += (s, e) => Invalidate();
            innerTextBox.MouseEnter += (s, e) =>
            {
                isHovered = true;
                Invalidate();
            };
            innerTextBox.MouseLeave += (s, e) =>
            {
                isHovered = false;
                Invalidate();
            };
            Controls.Add(innerTextBox);
            UpdateInnerLayout();
        }

        /// <summary>获取或设置文本框内容。</summary>
        public override string Text
        {
            get => innerTextBox.Text;
            set
            {
                if (innerTextBox.Text != (value ?? string.Empty))
                    innerTextBox.Text = value ?? string.Empty;
                base.Text = value ?? string.Empty;
                Invalidate();
            }
        }

        /// <summary>获取或设置是否允许编辑。不可编辑时隐藏原生控件并自绘清晰文字。</summary>
        public bool IsInteractive
        {
            get => isInteractive;
            set
            {
                if (isInteractive == value)
                    return;
                isInteractive = value;
                innerTextBox.Visible = value;
                innerTextBox.ReadOnly = !value;
                Cursor = value ? Cursors.IBeam : Cursors.Default;
                UpdateInnerLayout();
                Invalidate();
            }
        }

        /// <summary>清空输入内容。</summary>
        public void Clear()
        {
            Text = string.Empty;
        }

        /// <summary>应用当前主题配色与字体。</summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            currentTheme = theme != null ? theme.Clone() : ThemePresets.CreateDefault();
            innerTextBox.Font = Font;
            innerTextBox.BackColor = currentTheme.ControlBackgroundColor;
            innerTextBox.ForeColor = currentTheme.ControlTextColor;
            UpdateInnerLayout();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            innerTextBox.Font = Font;
            UpdateInnerLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerLayout();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (isInteractive && innerTextBox.Visible)
                innerTextBox.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBack = Parent?.BackColor ?? currentTheme.DialogBackgroundColor;
            g.Clear(parentBack);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (rect.Width <= 1f || rect.Height <= 1f)
                return;

            bool focused = isInteractive && innerTextBox.Focused;
            Color fillColor = isInteractive
                ? currentTheme.ControlBackgroundColor
                : ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogBackgroundColor,
                    0.22f);
            Color borderColor = focused
                ? currentTheme.AccentColor
                : isInteractive && isHovered
                    ? ThemeDrawing.Mix(currentTheme.BorderColor, currentTheme.DialogTextColor, 0.25f)
                    : ThemeDrawing.WithAlpha(currentTheme.BorderColor, isInteractive ? 145 : 85);

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, focused ? 1.5f : 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            if (!isInteractive || !innerTextBox.Visible)
            {
                int padX = Math.Max(8, (int)Math.Round(10 * scale));
                var textRect = new Rectangle(
                    padX,
                    0,
                    Math.Max(1, Width - padX * 2),
                    Height);
                Color textColor = ThemeDrawing.Mix(
                    currentTheme.ControlTextColor,
                    currentTheme.DialogBackgroundColor,
                    0.18f);
                TextRenderer.DrawText(
                    g,
                    innerTextBox.Text ?? string.Empty,
                    Font,
                    textRect,
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);
            }
        }

        private void UpdateInnerLayout()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padX = Math.Max(8, (int)Math.Round(10 * scale));
            int textHeight = innerTextBox.PreferredHeight;
            int y = Math.Max(2, (Height - textHeight) / 2);
            innerTextBox.Location = new Point(padX, y);
            innerTextBox.Width = Math.Max(10, Width - padX * 2);
        }
    }

    /// <summary>
    /// 现代圆角数值步进输入框。替代原生 NumericUpDown 粗糙的白色 3D 上下箭头按钮，
    /// 提供平滑圆角边框、上下箭头步进按钮及在默认/黑暗模式下始终清晰可读的数值文本。
    /// </summary>
    internal sealed class ThemedNumericUpDown : Control, IThemedControl
    {
        private readonly TextBox innerTextBox = new TextBox();
        private ThemeDefinition currentTheme = ThemePresets.CreateDefault();
        private decimal minimum;
        private decimal maximum = 100;
        private decimal currentValue;
        private bool isInteractive = true;
        private bool isHovered;
        private int hoveredButton; // 0: none, 1: up, 2: down
        private int pressedButton;
        private bool suppressTextCallback;

        /// <summary>当数值改变时触发。</summary>
        public event EventHandler ValueChanged;

        /// <summary>初始化现代圆角数值步进输入框。</summary>
        public ThemedNumericUpDown()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Size = new Size(150, 32);
            MinimumSize = new Size(100, 30);

            innerTextBox.BorderStyle = BorderStyle.None;
            innerTextBox.TextAlign = HorizontalAlignment.Left;
            innerTextBox.Text = "0";
            innerTextBox.TextChanged += InnerTextBox_TextChanged;
            innerTextBox.KeyDown += InnerTextBox_KeyDown;
            innerTextBox.GotFocus += (s, e) => Invalidate();
            innerTextBox.LostFocus += (s, e) =>
            {
                CommitTextValue();
                Invalidate();
            };
            innerTextBox.MouseEnter += (s, e) =>
            {
                isHovered = true;
                Invalidate();
            };
            innerTextBox.MouseLeave += (s, e) =>
            {
                isHovered = false;
                Invalidate();
            };
            Controls.Add(innerTextBox);
            UpdateInnerLayout();
        }

        /// <summary>获取或设置最小值。</summary>
        public decimal Minimum
        {
            get => minimum;
            set
            {
                minimum = value;
                if (currentValue < minimum)
                    Value = minimum;
            }
        }

        /// <summary>获取或设置最大值。</summary>
        public decimal Maximum
        {
            get => maximum;
            set
            {
                maximum = value;
                if (currentValue > maximum)
                    Value = maximum;
            }
        }

        /// <summary>获取或设置当前数值。</summary>
        public decimal Value
        {
            get => currentValue;
            set
            {
                decimal clamped = Math.Max(minimum, Math.Min(maximum, value));
                bool changed = currentValue != clamped;
                currentValue = clamped;
                suppressTextCallback = true;
                try
                {
                    string formatted = ((int)Math.Round(currentValue)).ToString(CultureInfo.InvariantCulture);
                    if (innerTextBox.Text != formatted)
                        innerTextBox.Text = formatted;
                }
                finally
                {
                    suppressTextCallback = false;
                }
                Invalidate();
                if (changed)
                    ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>获取或设置单位后缀说明。</summary>
        public string Suffix { get; set; }

        /// <summary>获取或设置控件是否允许交互。</summary>
        public bool IsInteractive
        {
            get => isInteractive;
            set
            {
                if (isInteractive == value)
                    return;
                isInteractive = value;
                innerTextBox.Visible = value;
                innerTextBox.ReadOnly = !value;
                hoveredButton = 0;
                pressedButton = 0;
                UpdateInnerLayout();
                Invalidate();
            }
        }

        /// <summary>应用当前主题配色与字体。</summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            currentTheme = theme != null ? theme.Clone() : ThemePresets.CreateDefault();
            innerTextBox.Font = Font;
            innerTextBox.BackColor = currentTheme.ControlBackgroundColor;
            innerTextBox.ForeColor = currentTheme.ControlTextColor;
            UpdateInnerLayout();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            innerTextBox.Font = Font;
            UpdateInnerLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerLayout();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!isInteractive)
                return;
            if (e.Delta > 0)
                StepValue(1);
            else if (e.Delta < 0)
                StepValue(-1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            isHovered = true;
            if (!isInteractive)
            {
                hoveredButton = 0;
                Cursor = Cursors.Default;
                return;
            }

            Rectangle upRect = GetUpButtonBounds();
            Rectangle downRect = GetDownButtonBounds();
            int newHover = upRect.Contains(e.Location)
                ? 1
                : downRect.Contains(e.Location)
                    ? 2
                    : 0;
            Cursor = newHover != 0 ? Cursors.Hand : Cursors.IBeam;
            if (newHover != hoveredButton)
            {
                hoveredButton = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            hoveredButton = 0;
            pressedButton = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!isInteractive || e.Button != MouseButtons.Left)
                return;

            if (GetUpButtonBounds().Contains(e.Location))
            {
                pressedButton = 1;
                StepValue(1);
                Invalidate();
            }
            else if (GetDownButtonBounds().Contains(e.Location))
            {
                pressedButton = 2;
                StepValue(-1);
                Invalidate();
            }
            else if (innerTextBox.Visible)
            {
                innerTextBox.Focus();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressedButton != 0)
            {
                pressedButton = 0;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBack = Parent?.BackColor ?? currentTheme.DialogBackgroundColor;
            g.Clear(parentBack);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (rect.Width <= 1f || rect.Height <= 1f)
                return;

            bool focused = isInteractive && innerTextBox.Focused;
            Color fillColor = isInteractive
                ? currentTheme.ControlBackgroundColor
                : ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogBackgroundColor,
                    0.22f);
            Color borderColor = focused
                ? currentTheme.AccentColor
                : isInteractive && (isHovered || hoveredButton != 0)
                    ? ThemeDrawing.Mix(currentTheme.BorderColor, currentTheme.DialogTextColor, 0.25f)
                    : ThemeDrawing.WithAlpha(currentTheme.BorderColor, isInteractive ? 145 : 85);

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, focused ? 1.5f : 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            Rectangle upBounds = GetUpButtonBounds();
            Rectangle downBounds = GetDownButtonBounds();

            if (isInteractive && (hoveredButton == 1 || pressedButton == 1))
            {
                Color btnHighlight = pressedButton == 1
                    ? ThemeDrawing.Mix(fillColor, currentTheme.AccentColor, 0.25f)
                    : ThemeDrawing.Mix(fillColor, currentTheme.DialogTextColor, 0.10f);
                var highlightRect = Rectangle.Inflate(upBounds, -2, -1);
                using (var path = ThemeDrawing.CreateRoundedRectangle(highlightRect, 3f * scale))
                using (var brush = new SolidBrush(btnHighlight))
                {
                    g.FillPath(brush, path);
                }
            }
            else if (isInteractive && (hoveredButton == 2 || pressedButton == 2))
            {
                Color btnHighlight = pressedButton == 2
                    ? ThemeDrawing.Mix(fillColor, currentTheme.AccentColor, 0.25f)
                    : ThemeDrawing.Mix(fillColor, currentTheme.DialogTextColor, 0.10f);
                var highlightRect = Rectangle.Inflate(downBounds, -2, -1);
                using (var path = ThemeDrawing.CreateRoundedRectangle(highlightRect, 3f * scale))
                using (var brush = new SolidBrush(btnHighlight))
                {
                    g.FillPath(brush, path);
                }
            }

            // 绘制右侧步进区与数值区之间的柔和分隔线
            using (var dividerPen = new Pen(ThemeDrawing.WithAlpha(currentTheme.BorderColor, isInteractive ? 65 : 40), 1f))
            {
                g.DrawLine(dividerPen, upBounds.Left, 5, upBounds.Left, Height - 6);
            }

            Color chevronColor = isInteractive
                ? ThemeDrawing.Mix(currentTheme.ControlTextColor, fillColor, 0.20f)
                : ThemeDrawing.Mix(currentTheme.ControlTextColor, currentTheme.DialogBackgroundColor, 0.45f);
            DrawChevron(g, upBounds, up: true, chevronColor, scale);
            DrawChevron(g, downBounds, up: false, chevronColor, scale);

            if (!isInteractive || !innerTextBox.Visible)
            {
                int padX = Math.Max(8, (int)Math.Round(10 * scale));
                var textRect = new Rectangle(
                    padX,
                    0,
                    Math.Max(1, upBounds.Left - padX - 4),
                    Height);
                Color textColor = ThemeDrawing.Mix(
                    currentTheme.ControlTextColor,
                    currentTheme.DialogBackgroundColor,
                    0.18f);
                string display = ((int)Math.Round(currentValue)).ToString(CultureInfo.InvariantCulture);
                TextRenderer.DrawText(
                    g,
                    display,
                    Font,
                    textRect,
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
        }

        private static void DrawChevron(Graphics g, Rectangle bounds, bool up, Color color, float scale)
        {
            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float dx = 3.6f * scale;
            float dy = 2.0f * scale;

            PointF[] points = up
                ? new[]
                {
                    new PointF(cx - dx, cy + dy * 0.5f),
                    new PointF(cx, cy - dy * 0.6f),
                    new PointF(cx + dx, cy + dy * 0.5f)
                }
                : new[]
                {
                    new PointF(cx - dx, cy - dy * 0.5f),
                    new PointF(cx, cy + dy * 0.6f),
                    new PointF(cx + dx, cy - dy * 0.5f)
                };

            using (var pen = new Pen(color, Math.Max(1.4f, 1.5f * scale)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, points);
            }
        }

        private Rectangle GetUpButtonBounds()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int btnWidth = Math.Max(24, (int)Math.Round(28 * scale));
            int halfHeight = Height / 2;
            return new Rectangle(Width - btnWidth - 1, 1, btnWidth, halfHeight - 1);
        }

        private Rectangle GetDownButtonBounds()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int btnWidth = Math.Max(24, (int)Math.Round(28 * scale));
            int halfHeight = Height / 2;
            return new Rectangle(Width - btnWidth - 1, halfHeight, btnWidth, Height - halfHeight - 1);
        }

        private void UpdateInnerLayout()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padX = Math.Max(8, (int)Math.Round(10 * scale));
            int btnWidth = Math.Max(24, (int)Math.Round(28 * scale));
            int textHeight = innerTextBox.PreferredHeight;
            int y = Math.Max(2, (Height - textHeight) / 2);
            innerTextBox.Location = new Point(padX, y);
            innerTextBox.Width = Math.Max(16, Width - btnWidth - padX - 6);
        }

        private void StepValue(int delta)
        {
            Value = currentValue + delta;
        }

        private void InnerTextBox_TextChanged(object sender, EventArgs e)
        {
            if (suppressTextCallback)
                return;
            if (int.TryParse(innerTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                decimal clamped = Math.Max(minimum, Math.Min(maximum, parsed));
                if (clamped == parsed && currentValue != clamped)
                {
                    currentValue = clamped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void InnerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isInteractive)
                return;
            if (e.KeyCode == Keys.Up)
            {
                StepValue(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                StepValue(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                CommitTextValue();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void CommitTextValue()
        {
            if (int.TryParse(innerTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                Value = parsed;
            }
            else
            {
                Value = currentValue;
            }
        }
    }

    /// <summary>
    /// 现代 Fluent 风格下拉选择组件。闭合态采用圆角矩形与平滑下箭头，展开态弹出圆角浮层卡片，
    /// 选中项带有圆角胶囊背景及左侧竖向强调色指示条（与 Windows 11 设置下拉选项风格一致）。
    /// </summary>
    internal sealed class ThemedComboBox : Control, IThemedControl
    {
        private readonly List<object> items = new List<object>();
        private ThemeDefinition currentTheme = ThemePresets.CreateDefault();
        private int selectedIndex = -1;
        private bool isHovered;
        private bool isPopupOpen;
        private bool isInteractive = true;
        private ToolStripDropDown activeDropDown;

        /// <summary>当选中项索引改变时触发。</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>初始化现代 Fluent 风格下拉选择组件。</summary>
        public ThemedComboBox()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
            Size = new Size(220, 32);
            MinimumSize = new Size(100, 30);
            Cursor = Cursors.Hand;
        }

        /// <summary>获取下拉选项集合。</summary>
        public IList<object> Items => items;

        /// <summary>获取或设置当前选中项索引。</summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                int normalized = (value >= 0 && value < items.Count) ? value : -1;
                if (selectedIndex == normalized)
                    return;
                selectedIndex = normalized;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>获取或设置当前选中对象。</summary>
        public object SelectedItem
        {
            get => (selectedIndex >= 0 && selectedIndex < items.Count) ? items[selectedIndex] : null;
            set
            {
                int index = items.IndexOf(value);
                SelectedIndex = index;
            }
        }

        /// <summary>获取或设置下拉框是否可交互。只读/预置主题态下仍保持高对比度清晰文本。</summary>
        public bool IsInteractive
        {
            get => isInteractive;
            set
            {
                if (isInteractive == value)
                    return;
                isInteractive = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
                if (!value)
                    ClosePopup();
                Invalidate();
            }
        }

        /// <summary>应用当前主题配色与字体。</summary>
        public void ApplyTheme(ThemeDefinition theme)
        {
            currentTheme = theme != null ? theme.Clone() : ThemePresets.CreateDefault();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!isInteractive || e.Button != MouseButtons.Left)
                return;

            Focus();
            if (isPopupOpen)
                ClosePopup();
            else
                ShowPopup();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!isInteractive || items.Count == 0)
                return;
            if (e.Delta > 0 && selectedIndex > 0)
                SelectedIndex = selectedIndex - 1;
            else if (e.Delta < 0 && selectedIndex < items.Count - 1)
                SelectedIndex = selectedIndex + 1;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!isInteractive || items.Count == 0)
                return;

            if (e.KeyCode == Keys.Up && selectedIndex > 0)
            {
                SelectedIndex = selectedIndex - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && selectedIndex < items.Count - 1)
            {
                SelectedIndex = selectedIndex + 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                ShowPopup();
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBack = Parent?.BackColor ?? currentTheme.DialogBackgroundColor;
            g.Clear(parentBack);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (rect.Width <= 1f || rect.Height <= 1f)
                return;

            Color fillColor;
            Color borderColor;
            Color textColor;

            if (!isInteractive)
            {
                fillColor = ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogBackgroundColor,
                    0.22f);
                borderColor = ThemeDrawing.WithAlpha(currentTheme.BorderColor, 85);
                textColor = ThemeDrawing.Mix(
                    currentTheme.ControlTextColor,
                    currentTheme.DialogBackgroundColor,
                    0.18f);
            }
            else if (isPopupOpen)
            {
                fillColor = ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogTextColor,
                    0.08f);
                borderColor = currentTheme.AccentColor;
                textColor = currentTheme.ControlTextColor;
            }
            else if (isHovered)
            {
                fillColor = ThemeDrawing.Mix(
                    currentTheme.ControlBackgroundColor,
                    currentTheme.DialogTextColor,
                    0.06f);
                borderColor = ThemeDrawing.Mix(currentTheme.BorderColor, currentTheme.DialogTextColor, 0.25f);
                textColor = currentTheme.ControlTextColor;
            }
            else
            {
                fillColor = currentTheme.ControlBackgroundColor;
                borderColor = ThemeDrawing.WithAlpha(currentTheme.BorderColor, 145);
                textColor = currentTheme.ControlTextColor;
            }

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, isPopupOpen ? 1.4f : 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            int padLeft = Math.Max(10, (int)Math.Round(12 * scale));
            int arrowZoneWidth = Math.Max(26, (int)Math.Round(30 * scale));
            var textRect = new Rectangle(
                padLeft,
                0,
                Math.Max(1, Width - padLeft - arrowZoneWidth),
                Height);
            string displayText = SelectedItem?.ToString() ?? string.Empty;
            TextRenderer.DrawText(
                g,
                displayText,
                Font,
                textRect,
                textColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            // 绘制右侧平滑 V 形展开箭头
            float cx = Width - arrowZoneWidth / 2f - 2f * scale;
            float cy = Height / 2f;
            float dx = 4.2f * scale;
            float dy = 2.4f * scale;
            Color arrowColor = isInteractive
                ? ThemeDrawing.Mix(currentTheme.ControlTextColor, fillColor, 0.22f)
                : ThemeDrawing.Mix(currentTheme.ControlTextColor, currentTheme.DialogBackgroundColor, 0.42f);

            using (var arrowPen = new Pen(arrowColor, Math.Max(1.5f, 1.6f * scale)))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                arrowPen.LineJoin = LineJoin.Round;
                g.DrawLines(arrowPen, new[]
                {
                    new PointF(cx - dx, cy - dy * 0.5f),
                    new PointF(cx, cy + dy * 0.6f),
                    new PointF(cx + dx, cy - dy * 0.5f)
                });
            }
        }

        private void ShowPopup()
        {
            if (items.Count == 0)
                return;

            ClosePopup();
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            var popupControl = new PopupListControl(this, currentTheme, Font, scale);
            var host = new ToolStripControlHost(popupControl)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = popupControl.Size
            };

            var dropDown = new RoundedPopupDropDown(popupControl.CornerRadiusPx)
            {
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                DropShadowEnabled = true,
                Size = popupControl.Size
            };
            dropDown.Items.Add(host);
            dropDown.Closed += (s, e) =>
            {
                isPopupOpen = false;
                activeDropDown = null;
                Invalidate();
            };

            activeDropDown = dropDown;
            isPopupOpen = true;
            Invalidate();
            dropDown.Show(this, new Point(0, Height + Math.Max(2, (int)Math.Round(4 * scale))));
            popupControl.Focus();
        }

        private void ClosePopup()
        {
            if (activeDropDown != null)
            {
                activeDropDown.Close();
                activeDropDown = null;
            }
            isPopupOpen = false;
        }

        /// <summary>无系统默认边框的圆角弹出容器。</summary>
        private sealed class RoundedPopupDropDown : ToolStripDropDown
        {
            private readonly int cornerRadius;

            public RoundedPopupDropDown(int cornerRadius)
            {
                this.cornerRadius = Math.Max(4, cornerRadius);
                Renderer = new EmptyDropDownRenderer();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                if (Width > 0 && Height > 0)
                {
                    using (var path = ThemeDrawing.CreateRoundedRectangle(
                        new Rectangle(0, 0, Width, Height),
                        cornerRadius))
                    {
                        Region = new Region(path);
                    }
                }
            }
        }

        private sealed class EmptyDropDownRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
            }
        }

        /// <summary>
        /// 现代 Fluent 风格下拉选项面板：圆角浅/深色卡片背景、宽松行高、圆角胶囊高亮
        /// 以及选中项左侧圆角竖条指示器（复刻参考图二视觉样式）。
        /// </summary>
        private sealed class PopupListControl : Control
        {
            private readonly ThemedComboBox owner;
            private readonly ThemeDefinition theme;
            private readonly float scale;
            private readonly int itemHeight;
            private readonly int itemGap;
            private readonly int outerPadding;
            private int hoveredIndex = -1;

            public PopupListControl(
                ThemedComboBox owner,
                ThemeDefinition theme,
                Font font,
                float scale)
            {
                this.owner = owner;
                this.theme = theme;
                this.scale = scale;
                Font = font;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint |
                    ControlStyles.Selectable,
                    true);

                itemHeight = Math.Max(30, (int)Math.Round(36 * scale));
                itemGap = Math.Max(2, (int)Math.Round(3 * scale));
                outerPadding = Math.Max(5, (int)Math.Round(6 * scale));
                CornerRadiusPx = Math.Max(6, (int)Math.Round(8 * scale));
                Cursor = Cursors.Hand;

                int maxTextWidth = 0;
                foreach (object item in owner.Items)
                {
                    Size sz = TextRenderer.MeasureText(
                        item?.ToString() ?? string.Empty,
                        Font,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    if (sz.Width > maxTextWidth)
                        maxTextWidth = sz.Width;
                }

                int width = Math.Max(
                    owner.Width,
                    maxTextWidth + outerPadding * 2 + (int)Math.Round(36 * scale));
                int count = owner.Items.Count;
                int height = outerPadding * 2 +
                    count * itemHeight +
                    Math.Max(0, count - 1) * itemGap;
                Size = new Size(width, height);
            }

            public int CornerRadiusPx { get; }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = HitTestIndex(e.Location);
                if (idx != hoveredIndex)
                {
                    hoveredIndex = idx;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (hoveredIndex != -1)
                {
                    hoveredIndex = -1;
                    Invalidate();
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.Button != MouseButtons.Left)
                    return;
                int idx = HitTestIndex(e.Location);
                if (idx >= 0 && idx < owner.Items.Count)
                {
                    owner.SelectedIndex = idx;
                    owner.ClosePopup();
                }
            }

            protected override bool IsInputKey(Keys keyData)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.Up:
                    case Keys.Down:
                    case Keys.Enter:
                    case Keys.Escape:
                        return true;
                    default:
                        return base.IsInputKey(keyData);
                }
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == Keys.Escape)
                {
                    owner.ClosePopup();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    if (hoveredIndex >= 0 && hoveredIndex < owner.Items.Count)
                        owner.SelectedIndex = hoveredIndex;
                    owner.ClosePopup();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    int baseIdx = hoveredIndex >= 0 ? hoveredIndex : owner.SelectedIndex;
                    if (baseIdx > 0)
                    {
                        hoveredIndex = baseIdx - 1;
                        Invalidate();
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    int baseIdx = hoveredIndex >= 0 ? hoveredIndex : owner.SelectedIndex;
                    if (baseIdx < owner.Items.Count - 1)
                    {
                        hoveredIndex = baseIdx + 1;
                        Invalidate();
                    }
                    e.Handled = true;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool darkSurface = ThemeDrawing.IsDark(theme.DialogBackgroundColor);
                Color surfaceColor = darkSurface
                    ? Color.FromArgb(44, 44, 48)
                    : Color.FromArgb(249, 249, 249);
                Color borderColor = darkSurface
                    ? Color.FromArgb(72, 72, 78)
                    : Color.FromArgb(220, 220, 220);
                Color itemTextColor = darkSurface
                    ? Color.FromArgb(245, 245, 245)
                    : Color.FromArgb(26, 26, 26);
                Color selectedPillColor = darkSurface
                    ? Color.FromArgb(62, 62, 68)
                    : Color.FromArgb(234, 234, 234);
                Color hoverPillColor = darkSurface
                    ? Color.FromArgb(55, 55, 60)
                    : Color.FromArgb(240, 240, 240);

                g.Clear(surfaceColor);

                float pillRadius = 6f * scale;
                int y = outerPadding;
                for (int i = 0; i < owner.Items.Count; i++)
                {
                    var itemRect = new Rectangle(
                        outerPadding,
                        y,
                        Width - outerPadding * 2,
                        itemHeight);
                    bool isSelected = i == owner.SelectedIndex;
                    bool isItemHovered = i == hoveredIndex;

                    if (isSelected || isItemHovered)
                    {
                        Color pillFill = isSelected && isItemHovered
                            ? ThemeDrawing.Mix(selectedPillColor, itemTextColor, 0.05f)
                            : isSelected
                                ? selectedPillColor
                                : hoverPillColor;
                        using (var pillPath = ThemeDrawing.CreateRoundedRectangle(itemRect, pillRadius))
                        using (var pillBrush = new SolidBrush(pillFill))
                        {
                            g.FillPath(pillBrush, pillPath);
                        }
                    }

                    // 选中项左侧绘制圆角竖条强调色指示器（如同参考图二）
                    if (isSelected)
                    {
                        float barWidth = Math.Max(3f, 3.4f * scale);
                        float barHeight = Math.Max(14f, 16f * scale);
                        float barX = itemRect.X + Math.Max(2f, 3f * scale);
                        float barY = itemRect.Y + (itemRect.Height - barHeight) / 2f;
                        var barRect = new RectangleF(barX, barY, barWidth, barHeight);
                        using (var barPath = ThemeDrawing.CreateRoundedRectangle(barRect, barWidth / 2f))
                        using (var barBrush = new SolidBrush(theme.AccentColor))
                        {
                            g.FillPath(barBrush, barPath);
                        }
                    }

                    int textOffsetLeft = Math.Max(12, (int)Math.Round(15 * scale));
                    var textRect = new Rectangle(
                        itemRect.X + textOffsetLeft,
                        itemRect.Y,
                        Math.Max(1, itemRect.Width - textOffsetLeft - 8),
                        itemRect.Height);
                    TextRenderer.DrawText(
                        g,
                        owner.Items[i]?.ToString() ?? string.Empty,
                        Font,
                        textRect,
                        itemTextColor,
                        TextFormatFlags.Left |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix);

                    y += itemHeight + itemGap;
                }

                var outerRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using (var borderPath = ThemeDrawing.CreateRoundedRectangle(outerRect, CornerRadiusPx))
                using (var borderPen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(borderPen, borderPath);
                }
            }

            private int HitTestIndex(Point point)
            {
                int y = outerPadding;
                for (int i = 0; i < owner.Items.Count; i++)
                {
                    var rect = new Rectangle(0, y, Width, itemHeight);
                    if (rect.Contains(point))
                        return i;
                    y += itemHeight + itemGap;
                }
                return -1;
            }
        }
    }

}
