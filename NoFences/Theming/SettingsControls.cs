using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NoFences.Theming
{
    /// <summary>支持应用设置页面固定浅色/深色调色板的控件契约。</summary>
    internal interface ISettingsPaletteAware
    {
        /// <summary>应用当前设置页面调色板。</summary>
        void ApplyPalette(SettingsPalette palette);
    }

    /// <summary>
    /// 设置页面矢量图标渲染器。采用纯 GDI+ 抗锯齿路径绘制标题栏、导航栏、
    /// 分组卡片标题及目录选择按钮图标，不依赖外部图片资源或特殊字体。
    /// </summary>
    internal static class SettingsIconRenderer
    {
        /// <summary>绘制设置齿轮矢量图标。</summary>
        public static void DrawGearIcon(Graphics g, RectangleF bounds, Color color, float penWidth = 1.6f)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float outerR = Math.Min(bounds.Width, bounds.Height) * 0.45f;
            float midR = outerR * 0.72f;
            float innerR = outerR * 0.36f;

            using (var pen = new Pen(color, penWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    float x1 = cx + (float)(Math.Cos(angle) * (midR - 0.3f));
                    float y1 = cy + (float)(Math.Sin(angle) * (midR - 0.3f));
                    float x2 = cx + (float)(Math.Cos(angle) * outerR);
                    float y2 = cy + (float)(Math.Sin(angle) * outerR);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }

                g.DrawEllipse(pen, cx - midR, cy - midR, midR * 2f, midR * 2f);
                g.DrawEllipse(pen, cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
            }
            g.Restore(state);
        }

        /// <summary>绘制左侧分类导航栏图标。</summary>
        public static void DrawCategoryIcon(Graphics g, SettingsCategoryIcon icon, RectangleF bounds, Color color)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = bounds.Width;
            float h = bounds.Height;
            float x = bounds.X;
            float y = bounds.Y;

            using (var pen = new Pen(color, 1.65f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                switch (icon)
                {
                    case SettingsCategoryIcon.BasicHome:
                        // 屋顶折线 + 墙体与门框（与参考图“基础设置”一致）
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.12f, y + h * 0.48f),
                            new PointF(x + w * 0.50f, y + h * 0.14f),
                            new PointF(x + w * 0.88f, y + h * 0.48f)
                        });
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.22f, y + h * 0.42f),
                            new PointF(x + w * 0.22f, y + h * 0.86f),
                            new PointF(x + w * 0.78f, y + h * 0.86f),
                            new PointF(x + w * 0.78f, y + h * 0.42f)
                        });
                        g.DrawRectangle(
                            pen,
                            x + w * 0.41f,
                            y + h * 0.56f,
                            w * 0.18f,
                            h * 0.30f);
                        break;

                    case SettingsCategoryIcon.ThemePalette:
                        DrawPaletteOutlineIcon(g, bounds, color, 1.65f);
                        break;

                    case SettingsCategoryIcon.Storage:
                        DrawFolderOutlineIcon(g, bounds, color, 1.65f);
                        break;

                    default:
                        DrawGearIcon(g, bounds, color, 1.65f);
                        break;
                }
            }
            g.Restore(state);
        }

        /// <summary>绘制分组卡片标题左侧图标。</summary>
        public static void DrawGroupIcon(Graphics g, SettingsGroupIcon icon, RectangleF bounds, Color color)
        {
            switch (icon)
            {
                case SettingsGroupIcon.Gear:
                    DrawGearIcon(g, bounds, color, 1.55f);
                    break;
                case SettingsGroupIcon.Folder:
                    DrawFolderOutlineIcon(g, bounds, color, 1.55f);
                    break;
                case SettingsGroupIcon.Palette:
                    DrawPaletteOutlineIcon(g, bounds, color, 1.55f);
                    break;
                case SettingsGroupIcon.Sliders:
                    DrawSlidersIcon(g, bounds, color, 1.55f);
                    break;
                case SettingsGroupIcon.Image:
                    DrawImageIcon(g, bounds, color, 1.55f);
                    break;
                case SettingsGroupIcon.Swatches:
                    DrawSwatchesIcon(g, bounds, color, 1.55f);
                    break;
            }
        }

        /// <summary>绘制调色盘线条图标。</summary>
        public static void DrawPaletteOutlineIcon(Graphics g, RectangleF bounds, Color color, float penWidth)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = bounds.X + bounds.Width * 0.10f;
            float y = bounds.Y + bounds.Height * 0.10f;
            float w = bounds.Width * 0.80f;
            float h = bounds.Height * 0.80f;

            using (var pen = new Pen(color, penWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            using (var brush = new SolidBrush(color))
            {
                g.DrawEllipse(pen, x, y, w, h);
                float dotR = Math.Max(1.8f, w * 0.12f);
                g.FillEllipse(brush, x + w * 0.24f, y + h * 0.28f, dotR, dotR);
                g.FillEllipse(brush, x + w * 0.46f, y + h * 0.18f, dotR, dotR);
                g.FillEllipse(brush, x + w * 0.66f, y + h * 0.34f, dotR, dotR);
                g.FillEllipse(brush, x + w * 0.28f, y + h * 0.56f, dotR, dotR);
                g.DrawEllipse(pen, x + w * 0.54f, y + h * 0.56f, dotR * 1.35f, dotR * 1.15f);
            }
            g.Restore(state);
        }

        /// <summary>绘制文件夹线条图标。</summary>
        public static void DrawFolderOutlineIcon(Graphics g, RectangleF bounds, Color color, float penWidth)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = bounds.X + bounds.Width * 0.10f;
            float y = bounds.Y + bounds.Height * 0.18f;
            float w = bounds.Width * 0.80f;
            float h = bounds.Height * 0.66f;

            using (var pen = new Pen(color, penWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                PointF[] folderPath =
                {
                    new PointF(x, y + h * 0.18f),
                    new PointF(x, y),
                    new PointF(x + w * 0.36f, y),
                    new PointF(x + w * 0.48f, y + h * 0.18f),
                    new PointF(x + w, y + h * 0.18f),
                    new PointF(x + w, y + h),
                    new PointF(x, y + h),
                    new PointF(x, y + h * 0.18f)
                };
                g.DrawLines(pen, folderPath);
            }
            g.Restore(state);
        }

        private static void DrawSlidersIcon(Graphics g, RectangleF bounds, Color color, float penWidth)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x1 = bounds.X + bounds.Width * 0.15f;
            float x2 = bounds.Right - bounds.Width * 0.15f;
            float y1 = bounds.Y + bounds.Height * 0.26f;
            float y2 = bounds.Y + bounds.Height * 0.50f;
            float y3 = bounds.Y + bounds.Height * 0.74f;
            float r = Math.Max(2f, bounds.Width * 0.11f);

            using (var pen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(color))
            {
                g.DrawLine(pen, x1, y1, x2, y1);
                g.DrawLine(pen, x1, y2, x2, y2);
                g.DrawLine(pen, x1, y3, x2, y3);
                g.FillEllipse(brush, bounds.X + bounds.Width * 0.32f - r, y1 - r, r * 2, r * 2);
                g.FillEllipse(brush, bounds.X + bounds.Width * 0.66f - r, y2 - r, r * 2, r * 2);
                g.FillEllipse(brush, bounds.X + bounds.Width * 0.44f - r, y3 - r, r * 2, r * 2);
            }
            g.Restore(state);
        }

        private static void DrawImageIcon(Graphics g, RectangleF bounds, Color color, float penWidth)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(
                bounds.X + bounds.Width * 0.12f,
                bounds.Y + bounds.Height * 0.16f,
                bounds.Width * 0.76f,
                bounds.Height * 0.68f);

            using (var pen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, 2.5f))
            {
                g.DrawPath(pen, path);
                g.DrawEllipse(pen, rect.X + rect.Width * 0.18f, rect.Y + rect.Height * 0.20f, rect.Width * 0.18f, rect.Width * 0.18f);
                g.DrawLines(pen, new[]
                {
                    new PointF(rect.X + rect.Width * 0.12f, rect.Bottom - rect.Height * 0.15f),
                    new PointF(rect.X + rect.Width * 0.45f, rect.Y + rect.Height * 0.48f),
                    new PointF(rect.X + rect.Width * 0.68f, rect.Y + rect.Height * 0.68f),
                    new PointF(rect.Right - rect.Width * 0.10f, rect.Y + rect.Height * 0.52f)
                });
            }
            g.Restore(state);
        }

        private static void DrawSwatchesIcon(Graphics g, RectangleF bounds, Color color, float penWidth)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = bounds.X + bounds.Width * 0.14f;
            float y = bounds.Y + bounds.Height * 0.14f;
            float size = bounds.Width * 0.31f;
            float gap = bounds.Width * 0.10f;

            using (var pen = new Pen(color, penWidth))
            {
                g.DrawRectangle(pen, x, y, size, size);
                g.DrawRectangle(pen, x + size + gap, y, size, size);
                g.DrawRectangle(pen, x, y + size + gap, size, size);
                g.DrawRectangle(pen, x + size + gap, y + size + gap, size, size);
            }
            g.Restore(state);
        }
    }

    /// <summary>
    /// 设置窗口顶部自定义标题栏控件。左侧显示蓝色设置图标与标题“设置”，
    /// 右侧提供窗口最小化、最大化/还原和关闭按钮，并支持拖拽移动窗口。
    /// </summary>
    internal sealed class SettingsTitleBarControl : Control, ISettingsPaletteAware
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 0x0002;

        private SettingsPalette palette = SettingsPalette.CreateDark();
        private int sidebarWidth = 210;
        private string titleText = "设置";
        private int hoveredButtonIndex = -1; // 0 = Min, 1 = Max, 2 = Close
        private int pressedButtonIndex = -1;

        public SettingsTitleBarControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Dock = DockStyle.Top;
            Height = 46;
        }

        /// <summary>左侧导航栏宽度（像素），用于让标题栏左侧区块与侧边栏保持对齐。</summary>
        public int SidebarWidth
        {
            get => sidebarWidth;
            set
            {
                if (sidebarWidth != value)
                {
                    sidebarWidth = value;
                    Invalidate();
                }
            }
        }

        /// <summary>标题栏文字。</summary>
        public string TitleText
        {
            get => titleText;
            set
            {
                titleText = value ?? string.Empty;
                Invalidate();
            }
        }

        public event EventHandler MinimizeClicked;
        public event EventHandler MaximizeRestoreClicked;
        public event EventHandler CloseClicked;

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTestWindowButton(e.Location);
            if (hit != hoveredButtonIndex)
            {
                hoveredButtonIndex = hit;
                Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoveredButtonIndex != -1 || pressedButtonIndex != -1)
            {
                hoveredButtonIndex = -1;
                pressedButtonIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            int hit = HitTestWindowButton(e.Location);
            if (hit >= 0)
            {
                pressedButtonIndex = hit;
                Invalidate();
                return;
            }

            if (e.Clicks == 2)
            {
                MaximizeRestoreClicked?.Invoke(this, EventArgs.Empty);
                return;
            }

            Form hostForm = FindForm();
            if (hostForm != null && hostForm.IsHandleCreated)
            {
                ReleaseCapture();
                SendMessage(hostForm.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            int hit = HitTestWindowButton(e.Location);
            int wasPressed = pressedButtonIndex;
            pressedButtonIndex = -1;
            Invalidate();

            if (hit >= 0 && hit == wasPressed)
            {
                if (hit == 0)
                    MinimizeClicked?.Invoke(this, EventArgs.Empty);
                else if (hit == 1)
                    MaximizeRestoreClicked?.Invoke(this, EventArgs.Empty);
                else if (hit == 2)
                    CloseClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int leftWidth = Math.Min(Width, sidebarWidth);

            // 左侧背景与导航栏融为一体，右侧背景与主面板统一
            using (var leftBrush = new SolidBrush(palette.TitleBarLeftBackground))
            using (var rightBrush = new SolidBrush(palette.TitleBarRightBackground))
            {
                g.FillRectangle(leftBrush, 0, 0, leftWidth, Height);
                if (Width > leftWidth)
                    g.FillRectangle(rightBrush, leftWidth, 0, Width - leftWidth, Height);
            }

            // 左侧齿轮图标 + “设置”标题
            int iconSize = Math.Max(18, (int)Math.Round(20 * scale));
            int iconLeft = Math.Max(16, (int)Math.Round(18 * scale));
            int iconTop = (Height - iconSize) / 2;
            SettingsIconRenderer.DrawGearIcon(
                g,
                new RectangleF(iconLeft, iconTop, iconSize, iconSize),
                palette.AccentBlue,
                Math.Max(1.6f, 1.7f * scale));

            using (var titleFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            {
                int textLeft = iconLeft + iconSize + (int)Math.Round(10 * scale);
                var textBounds = new Rectangle(textLeft, 0, Math.Max(40, leftWidth - textLeft - 8), Height);
                TextRenderer.DrawText(
                    g,
                    titleText,
                    titleFont,
                    textBounds,
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            // 右侧窗口控制按钮（最小化、最大化/还原、关闭）
            for (int i = 0; i < 3; i++)
            {
                Rectangle btnRect = GetWindowButtonBounds(i, scale);
                bool hovered = hoveredButtonIndex == i;
                bool pressed = pressedButtonIndex == i;

                if (hovered || pressed)
                {
                    Color hoverColor = i == 2
                        ? (pressed ? Color.FromArgb(196, 20, 36) : Color.FromArgb(232, 17, 35))
                        : (pressed ? palette.ControlBorder : palette.NavHoverBackground);
                    using (var hoverBrush = new SolidBrush(hoverColor))
                        g.FillRectangle(hoverBrush, btnRect);
                }

                Color glyphColor = (i == 2 && (hovered || pressed))
                    ? Color.White
                    : (hovered ? palette.PrimaryText : palette.SecondaryText);
                DrawWindowButtonGlyph(g, i, btnRect, glyphColor, scale);
            }

            // 底部分隔细线与导航栏右边框衔接
            using (var borderPen = new Pen(palette.CardBorder, 1f))
            {
                g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
                if (leftWidth > 0 && leftWidth < Width)
                    g.DrawLine(borderPen, leftWidth - 1, 0, leftWidth - 1, Height - 1);
            }
        }

        private void DrawWindowButtonGlyph(Graphics g, int buttonIndex, Rectangle bounds, Color color, float scale)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int half = Math.Max(4, (int)Math.Round(5 * scale));

            using (var pen = new Pen(color, Math.Max(1.2f, 1.3f * scale)))
            {
                if (buttonIndex == 0)
                {
                    // 最小化横线
                    g.DrawLine(pen, cx - half, cy, cx + half, cy);
                }
                else if (buttonIndex == 1)
                {
                    // 最大化/还原方框
                    Form host = FindForm();
                    bool isMaximized = host != null && host.WindowState == FormWindowState.Maximized;
                    if (isMaximized)
                    {
                        int box = half * 2 - 2;
                        g.DrawRectangle(pen, cx - half + 2, cy - half, box, box);
                        g.DrawRectangle(pen, cx - half, cy - half + 2, box, box);
                    }
                    else
                    {
                        g.DrawRectangle(pen, cx - half, cy - half, half * 2, half * 2);
                    }
                }
                else
                {
                    // 关闭叉号
                    g.DrawLine(pen, cx - half, cy - half, cx + half, cy + half);
                    g.DrawLine(pen, cx + half, cy - half, cx - half, cy + half);
                }
            }
        }

        private Rectangle GetWindowButtonBounds(int index, float scale)
        {
            int btnWidth = Math.Max(42, (int)Math.Round(46 * scale));
            int rightEdge = Width - (2 - index) * btnWidth;
            return new Rectangle(rightEdge - btnWidth, 0, btnWidth, Height - 1);
        }

        private int HitTestWindowButton(Point location)
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            for (int i = 0; i < 3; i++)
            {
                if (GetWindowButtonBounds(i, scale).Contains(location))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// 左侧配置分类纵向导航栏。支持蓝色选中胶囊背景、左侧高亮指示竖条、
    /// 图标+文字渲染，并支持动态扩展新的配置分类。
    /// </summary>
    internal sealed class SettingsSidebarControl : Control, ISettingsPaletteAware
    {
        private readonly List<SettingsCategoryDescriptor> categories = new List<SettingsCategoryDescriptor>();
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private int selectedIndex;
        private int hoveredIndex = -1;

        public SettingsSidebarControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Dock = DockStyle.Left;
            Width = 210;
        }

        public event EventHandler SelectedCategoryChanged;

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                int clamped = categories.Count == 0
                    ? -1
                    : Math.Max(0, Math.Min(categories.Count - 1, value));
                if (selectedIndex == clamped)
                    return;

                selectedIndex = clamped;
                Invalidate();
                SelectedCategoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public SettingsCategoryDescriptor SelectedCategory =>
            selectedIndex >= 0 && selectedIndex < categories.Count
                ? categories[selectedIndex]
                : null;

        public void SetCategories(IEnumerable<SettingsCategoryDescriptor> items)
        {
            categories.Clear();
            if (items != null)
                categories.AddRange(items);
            selectedIndex = categories.Count > 0 ? 0 : -1;
            Invalidate();
        }

        public void SelectCategoryById(string categoryId)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                if (string.Equals(categories[i].Id, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTestTab(e.Location);
            if (hit != hoveredIndex)
            {
                hoveredIndex = hit;
                Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoveredIndex != -1)
            {
                hoveredIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            int hit = HitTestTab(e.Location);
            if (hit >= 0)
                SelectedIndex = hit;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(palette.SidebarBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 8f * scale;

            using (var normalFont = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Regular))
            using (var selectedFont = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Bold))
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    Rectangle tabBounds = GetTabBounds(i, scale);
                    bool selected = i == selectedIndex;
                    bool hovered = i == hoveredIndex;

                    if (selected)
                    {
                        using (var tabPath = ThemeDrawing.CreateRoundedRectangle(tabBounds, radius))
                        using (var fillBrush = new LinearGradientBrush(
                            tabBounds,
                            palette.NavSelectedFillStart,
                            palette.NavSelectedFillEnd,
                            LinearGradientMode.Horizontal))
                        using (var borderPen = new Pen(palette.NavSelectedBorder, 1f))
                        {
                            g.FillPath(fillBrush, tabPath);
                            g.DrawPath(borderPen, tabPath);
                        }

                        // 左侧高亮蓝色竖条
                        float barWidth = Math.Max(3.5f, 3.8f * scale);
                        float barInsetY = Math.Max(7f, 8f * scale);
                        var barRect = new RectangleF(
                            tabBounds.X + 1.5f,
                            tabBounds.Y + barInsetY,
                            barWidth,
                            Math.Max(8f, tabBounds.Height - barInsetY * 2f));
                        using (var barPath = ThemeDrawing.CreateRoundedRectangle(barRect, barWidth / 2f))
                        using (var barBrush = new SolidBrush(palette.NavSelectedAccentBar))
                        {
                            g.FillPath(barBrush, barPath);
                        }
                    }
                    else if (hovered)
                    {
                        using (var tabPath = ThemeDrawing.CreateRoundedRectangle(tabBounds, radius))
                        using (var hoverBrush = new SolidBrush(palette.NavHoverBackground))
                        {
                            g.FillPath(hoverBrush, tabPath);
                        }
                    }

                    Color itemColor = selected
                        ? palette.NavSelectedText
                        : (hovered ? palette.PrimaryText : palette.SecondaryText);

                    int iconSize = Math.Max(16, (int)Math.Round(18 * scale));
                    int iconX = tabBounds.X + (int)Math.Round(16 * scale);
                    int iconY = tabBounds.Y + (tabBounds.Height - iconSize) / 2;
                    SettingsIconRenderer.DrawCategoryIcon(
                        g,
                        categories[i].Icon,
                        new RectangleF(iconX, iconY, iconSize, iconSize),
                        itemColor);

                    int textLeft = iconX + iconSize + (int)Math.Round(12 * scale);
                    var textBounds = new Rectangle(
                        textLeft,
                        tabBounds.Y,
                        Math.Max(20, tabBounds.Right - textLeft - 10),
                        tabBounds.Height);
                    TextRenderer.DrawText(
                        g,
                        categories[i].Title != null ? categories[i].Title() : categories[i].Id,
                        selected ? selectedFont : normalFont,
                        textBounds,
                        itemColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }

            // 右侧分隔细线
            using (var borderPen = new Pen(palette.CardBorder, 1f))
            {
                g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
            }
        }

        private Rectangle GetTabBounds(int index, float scale)
        {
            int marginH = (int)Math.Round(12 * scale);
            int topStart = (int)Math.Round(14 * scale);
            int itemHeight = (int)Math.Round(42 * scale);
            int gap = (int)Math.Round(6 * scale);
            int y = topStart + index * (itemHeight + gap);
            return new Rectangle(marginH, y, Math.Max(40, Width - marginH * 2 - 1), itemHeight);
        }

        private int HitTestTab(Point location)
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            for (int i = 0; i < categories.Count; i++)
            {
                if (GetTabBounds(i, scale).Contains(location))
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// 现代胶囊式切换开关控件（Toggle Switch）。
    /// 开启状态使用明亮蓝色胶囊轨道与右侧白色圆形滑块，关闭状态使用中性轨道与左侧滑块。
    /// </summary>
    internal sealed class SettingsToggleSwitch : Control, ISettingsPaletteAware
    {
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private bool isChecked;
        private bool isHovered;
        private bool isPressed;
        private bool isInteractive = true;

        public SettingsToggleSwitch()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
        }

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value)
                    return;

                isChecked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

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

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            return new Size(
                Math.Max(42, (int)Math.Round(46 * scale)),
                Math.Max(22, (int)Math.Round(24 * scale)));
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && IsInteractive)
            {
                isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && isPressed)
            {
                isPressed = false;
                if (IsInteractive && ClientRectangle.Contains(e.Location))
                    Checked = !Checked;
                else
                    Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (IsInteractive && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? palette.CardBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float trackHeight = Math.Min(Height - 2f, 22f * scale);
            float trackWidth = Math.Min(Width - 2f, 42f * scale);
            float trackX = (Width - trackWidth) / 2f;
            float trackY = (Height - trackHeight) / 2f;
            var trackRect = new RectangleF(trackX, trackY, trackWidth, trackHeight);

            bool interactive = IsInteractive;
            Color trackColor;
            if (isChecked)
            {
                trackColor = !interactive
                    ? ThemeDrawing.WithAlpha(palette.AccentBlue, 120)
                    : (isPressed ? palette.AccentPressedBlue : (isHovered ? palette.AccentHoverBlue : palette.AccentBlue));
            }
            else
            {
                trackColor = !interactive
                    ? ThemeDrawing.WithAlpha(palette.SwitchOffTrack, 110)
                    : (isHovered
                        ? ThemeDrawing.Mix(palette.SwitchOffTrack, palette.PrimaryText, 0.14f)
                        : palette.SwitchOffTrack);
            }

            using (var trackPath = ThemeDrawing.CreateRoundedRectangle(trackRect, trackHeight / 2f))
            using (var trackBrush = new SolidBrush(trackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float thumbPadding = Math.Max(2.5f, 3f * scale);
            float thumbSize = Math.Max(10f, trackHeight - thumbPadding * 2f);
            float thumbX = isChecked
                ? (trackRect.Right - thumbPadding - thumbSize)
                : (trackRect.X + thumbPadding);
            float thumbY = trackRect.Y + (trackRect.Height - thumbSize) / 2f;

            // 轻微投影使白色圆点层次分明
            using (var shadowBrush = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, thumbX, thumbY + 0.8f, thumbSize, thumbSize);
            }

            Color thumbColor = interactive
                ? Color.White
                : Color.FromArgb(200, 240, 244, 248);
            using (var thumbBrush = new SolidBrush(thumbColor))
            {
                g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
            }
        }
    }

    /// <summary>
    /// 现代圆角目录路径选择控件。左侧为路径输入框，右侧为带细分隔线的文件夹选择按钮（与参考图一致）。
    /// </summary>
    internal sealed class SettingsDirectoryPicker : Control, ISettingsPaletteAware
    {
        private readonly TextBox innerTextBox = new TextBox();
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private bool isHovered;
        private bool isButtonHovered;
        private bool isButtonPressed;
        private bool hasValidationError;
        private bool isInteractive = true;

        public SettingsDirectoryPicker()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Size = new Size(290, 34);

            innerTextBox.BorderStyle = BorderStyle.None;
            innerTextBox.TextChanged += (s, e) => OnPathChanged();
            innerTextBox.GotFocus += (s, e) => Invalidate();
            innerTextBox.LostFocus += (s, e) => Invalidate();
            innerTextBox.MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            innerTextBox.MouseLeave += (s, e) => { isHovered = false; Invalidate(); };
            Controls.Add(innerTextBox);
        }

        public event EventHandler SelectedPathChanged;

        public string SelectedPath
        {
            get => innerTextBox.Text;
            set
            {
                if (!string.Equals(innerTextBox.Text, value ?? string.Empty, StringComparison.Ordinal))
                {
                    innerTextBox.Text = value ?? string.Empty;
                }
            }
        }

        public string DialogDescription { get; set; }

        public bool HasValidationError
        {
            get => hasValidationError;
            set
            {
                if (hasValidationError != value)
                {
                    hasValidationError = value;
                    Invalidate();
                }
            }
        }

        public bool IsInteractive
        {
            get => isInteractive && Enabled;
            set
            {
                isInteractive = value;
                Enabled = value;
                innerTextBox.ReadOnly = !value;
                innerTextBox.Enabled = value;
                Invalidate();
            }
        }

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            UpdateInnerColors();
            LayoutInnerTextBox();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            innerTextBox.Font = Font;
            LayoutInnerTextBox();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInnerTextBox();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            isHovered = true;
            bool btnHit = GetButtonBounds().Contains(e.Location);
            if (btnHit != isButtonHovered)
            {
                isButtonHovered = btnHit;
                Cursor = btnHit && IsInteractive ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            isButtonHovered = false;
            isButtonPressed = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !IsInteractive)
                return;

            if (GetButtonBounds().Contains(e.Location))
            {
                isButtonPressed = true;
                Invalidate();
            }
            else
            {
                innerTextBox.Focus();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            bool wasPressed = isButtonPressed;
            isButtonPressed = false;
            Invalidate();

            if (wasPressed && IsInteractive && GetButtonBounds().Contains(e.Location))
            {
                BrowseDirectory();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? palette.CardBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Rectangle btnBounds = GetButtonBounds();

            Color fillColor = palette.ControlBackground;
            Color borderColor = hasValidationError
                ? palette.ErrorColor
                : innerTextBox.Focused
                    ? palette.AccentBlue
                    : isHovered
                        ? ThemeDrawing.Mix(palette.ControlBorder, palette.PrimaryText, 0.25f)
                        : palette.ControlBorder;

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            {
                g.FillPath(fillBrush, path);

                if (isButtonHovered || isButtonPressed)
                {
                    var state = g.Save();
                    g.SetClip(path);
                    Color btnHoverColor = isButtonPressed
                        ? ThemeDrawing.Mix(palette.ControlHoverBackground, palette.AccentBlue, 0.25f)
                        : palette.ControlHoverBackground;
                    using (var btnBrush = new SolidBrush(btnHoverColor))
                        g.FillRectangle(btnBrush, btnBounds);
                    g.Restore(state);
                }

                using (var borderPen = new Pen(borderColor, (innerTextBox.Focused || hasValidationError) ? 1.4f : 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 垂直分隔线
            using (var sepPen = new Pen(palette.ControlBorder, 1f))
            {
                g.DrawLine(sepPen, btnBounds.Left, 1, btnBounds.Left, Height - 2);
            }

            // 右侧文件夹图标
            float iconSize = Math.Max(14f, 15f * scale);
            var iconRect = new RectangleF(
                btnBounds.X + (btnBounds.Width - iconSize) / 2f,
                btnBounds.Y + (btnBounds.Height - iconSize) / 2f,
                iconSize,
                iconSize);
            Color iconColor = isButtonHovered ? palette.PrimaryText : palette.SecondaryText;
            SettingsIconRenderer.DrawFolderOutlineIcon(g, iconRect, iconColor, Math.Max(1.3f, 1.45f * scale));
        }

        private void BrowseDirectory()
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = DialogDescription ?? ThemeText.Key(NoFences.Localization.LKeys.DialogSelectDataDir),
                ShowNewFolderButton = true
            })
            {
                string current = innerTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                    dialog.SelectedPath = current;

                if (dialog.ShowDialog(FindForm()) == DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    SelectedPath = dialog.SelectedPath;
                }
            }
        }

        private void OnPathChanged()
        {
            if (hasValidationError)
            {
                hasValidationError = false;
                Invalidate();
            }
            SelectedPathChanged?.Invoke(this, EventArgs.Empty);
        }

        private Rectangle GetButtonBounds()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int btnWidth = Math.Max(32, (int)Math.Round(36 * scale));
            return new Rectangle(Math.Max(0, Width - btnWidth), 0, btnWidth, Height);
        }

        private void UpdateInnerColors()
        {
            innerTextBox.BackColor = palette.ControlBackground;
            innerTextBox.ForeColor = IsInteractive ? palette.PrimaryText : palette.MutedText;
        }

        private void LayoutInnerTextBox()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padLeft = Math.Max(10, (int)Math.Round(11 * scale));
            Rectangle btnBounds = GetButtonBounds();
            int textHeight = innerTextBox.PreferredHeight;
            int y = Math.Max(2, (Height - textHeight) / 2);
            int width = Math.Max(20, btnBounds.Left - padLeft - 8);
            innerTextBox.Location = new Point(padLeft, y);
            innerTextBox.Width = width;
        }
    }

    /// <summary>
    /// 现代圆角文件选择控件（用于主题背景图片选择）。
    /// 左侧为文件路径输入框，右侧提供浏览图片按钮与清除按钮。
    /// </summary>
    internal sealed class SettingsFilePicker : Control, ISettingsPaletteAware
    {
        private readonly TextBox innerTextBox = new TextBox();
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private bool isHovered;
        private int hoveredButtonIndex = -1; // 0 = Browse, 1 = Clear
        private int pressedButtonIndex = -1;
        private bool hasValidationError;
        private bool isInteractive = true;

        public SettingsFilePicker()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Size = new Size(300, 34);

            innerTextBox.BorderStyle = BorderStyle.None;
            innerTextBox.TextChanged += (s, e) =>
            {
                if (hasValidationError)
                {
                    hasValidationError = false;
                    Invalidate();
                }
                SelectedFileChanged?.Invoke(this, EventArgs.Empty);
            };
            innerTextBox.GotFocus += (s, e) => Invalidate();
            innerTextBox.LostFocus += (s, e) => Invalidate();
            innerTextBox.MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            innerTextBox.MouseLeave += (s, e) => { isHovered = false; Invalidate(); };
            Controls.Add(innerTextBox);
        }

        public event EventHandler SelectedFileChanged;

        public string SelectedFilePath
        {
            get => innerTextBox.Text;
            set
            {
                if (!string.Equals(innerTextBox.Text, value ?? string.Empty, StringComparison.Ordinal))
                {
                    innerTextBox.Text = value ?? string.Empty;
                }
            }
        }

        public bool HasValidationError
        {
            get => hasValidationError;
            set
            {
                if (hasValidationError != value)
                {
                    hasValidationError = value;
                    Invalidate();
                }
            }
        }

        public bool IsInteractive
        {
            get => isInteractive && Enabled;
            set
            {
                isInteractive = value;
                Enabled = value;
                innerTextBox.ReadOnly = !value;
                innerTextBox.Enabled = value;
                UpdateInnerColors();
                Invalidate();
            }
        }

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            UpdateInnerColors();
            LayoutInnerTextBox();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            innerTextBox.Font = Font;
            LayoutInnerTextBox();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInnerTextBox();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            isHovered = true;
            int hit = HitTestButton(e.Location);
            if (hit != hoveredButtonIndex)
            {
                hoveredButtonIndex = hit;
                Cursor = hit >= 0 && IsInteractive ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            hoveredButtonIndex = -1;
            pressedButtonIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !IsInteractive)
                return;

            int hit = HitTestButton(e.Location);
            if (hit >= 0)
            {
                pressedButtonIndex = hit;
                Invalidate();
            }
            else
            {
                innerTextBox.Focus();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            int wasPressed = pressedButtonIndex;
            int hit = HitTestButton(e.Location);
            pressedButtonIndex = -1;
            Invalidate();

            if (IsInteractive && wasPressed >= 0 && wasPressed == hit)
            {
                if (hit == 0)
                    BrowseFile();
                else if (hit == 1)
                    SelectedFilePath = string.Empty;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? palette.CardBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 6f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Rectangle browseBounds = GetButtonBounds(0);
            Rectangle clearBounds = GetButtonBounds(1);

            Color fillColor = IsInteractive
                ? palette.ControlBackground
                : ThemeDrawing.Mix(palette.ControlBackground, palette.CardBackground, 0.45f);
            Color borderColor = hasValidationError
                ? palette.ErrorColor
                : !IsInteractive
                    ? ThemeDrawing.WithAlpha(palette.ControlBorder, 100)
                    : innerTextBox.Focused
                        ? palette.AccentBlue
                        : isHovered
                            ? ThemeDrawing.Mix(palette.ControlBorder, palette.PrimaryText, 0.25f)
                            : palette.ControlBorder;

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            {
                g.FillPath(fillBrush, path);

                if (IsInteractive && (hoveredButtonIndex >= 0 || pressedButtonIndex >= 0))
                {
                    var state = g.Save();
                    g.SetClip(path);
                    int activeIdx = pressedButtonIndex >= 0 ? pressedButtonIndex : hoveredButtonIndex;
                    Rectangle activeRect = GetButtonBounds(activeIdx);
                    Color btnHoverColor = pressedButtonIndex >= 0
                        ? ThemeDrawing.Mix(palette.ControlHoverBackground, palette.AccentBlue, 0.25f)
                        : palette.ControlHoverBackground;
                    using (var btnBrush = new SolidBrush(btnHoverColor))
                        g.FillRectangle(btnBrush, activeRect);
                    g.Restore(state);
                }

                using (var borderPen = new Pen(borderColor, (innerTextBox.Focused || hasValidationError) ? 1.4f : 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            using (var sepPen = new Pen(ThemeDrawing.WithAlpha(palette.ControlBorder, IsInteractive ? 255 : 100), 1f))
            {
                g.DrawLine(sepPen, browseBounds.Left, 1, browseBounds.Left, Height - 2);
                g.DrawLine(sepPen, clearBounds.Left, 1, clearBounds.Left, Height - 2);
            }

            // 绘制浏览文件夹图标
            float iconSize = Math.Max(13f, 14f * scale);
            var folderRect = new RectangleF(
                browseBounds.X + (browseBounds.Width - iconSize) / 2f,
                browseBounds.Y + (browseBounds.Height - iconSize) / 2f,
                iconSize,
                iconSize);
            Color browseColor = !IsInteractive
                ? palette.MutedText
                : (hoveredButtonIndex == 0 ? palette.PrimaryText : palette.SecondaryText);
            SettingsIconRenderer.DrawFolderOutlineIcon(g, folderRect, browseColor, Math.Max(1.3f, 1.4f * scale));

            // 绘制清除（×）图标
            Color clearColor = !IsInteractive
                ? palette.MutedText
                : (hoveredButtonIndex == 1 ? palette.ErrorColor : palette.SecondaryText);
            int cx = clearBounds.X + clearBounds.Width / 2;
            int cy = clearBounds.Y + clearBounds.Height / 2;
            int half = Math.Max(3, (int)Math.Round(4 * scale));
            using (var crossPen = new Pen(clearColor, Math.Max(1.3f, 1.45f * scale))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                g.DrawLine(crossPen, cx - half, cy - half, cx + half, cy + half);
                g.DrawLine(crossPen, cx + half, cy - half, cx - half, cy + half);
            }
        }

        private void BrowseFile()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = ThemeText.Key(NoFences.Localization.LKeys.DialogSelectBgImageTitle),
                Filter = ThemeText.Key(NoFences.Localization.LKeys.DialogSelectBgImageFilter),
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (File.Exists(innerTextBox.Text))
                    dialog.InitialDirectory = Path.GetDirectoryName(innerTextBox.Text);
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                    SelectedFilePath = dialog.FileName;
            }
        }

        private Rectangle GetButtonBounds(int index)
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int btnWidth = Math.Max(30, (int)Math.Round(34 * scale));
            int rightEdge = Width - (1 - index) * btnWidth;
            return new Rectangle(Math.Max(0, rightEdge - btnWidth), 0, btnWidth, Height);
        }

        private int HitTestButton(Point location)
        {
            for (int i = 0; i < 2; i++)
            {
                if (GetButtonBounds(i).Contains(location))
                    return i;
            }
            return -1;
        }

        private void UpdateInnerColors()
        {
            innerTextBox.BackColor = IsInteractive
                ? palette.ControlBackground
                : ThemeDrawing.Mix(palette.ControlBackground, palette.CardBackground, 0.45f);
            innerTextBox.ForeColor = IsInteractive ? palette.PrimaryText : palette.MutedText;
        }

        private void LayoutInnerTextBox()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padLeft = Math.Max(10, (int)Math.Round(11 * scale));
            Rectangle browseBounds = GetButtonBounds(0);
            int textHeight = innerTextBox.PreferredHeight;
            int y = Math.Max(2, (Height - textHeight) / 2);
            int width = Math.Max(20, browseBounds.Left - padLeft - 8);
            innerTextBox.Location = new Point(padLeft, y);
            innerTextBox.Width = width;
        }
    }

    /// <summary>
    /// 设置页面底部操作按钮（支持“保存”主要蓝色强调按钮与“取消”次要按钮，具备默认/悬停/按下/禁用四态）。
    /// </summary>
    internal sealed class SettingsActionButton : Button, ISettingsPaletteAware
    {
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private bool isHovered;
        private bool isPressed;

        public SettingsActionButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Size = new Size(88, 36);
            Cursor = Cursors.Hand;
        }

        /// <summary>是否为主要蓝色强调按钮（如“保存”）。</summary>
        public bool IsPrimary { get; set; }

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
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
            isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left && Enabled)
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

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? palette.ContentBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 7f * scale;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

            Color fillColor;
            Color borderColor;
            Color textColor;

            if (IsPrimary)
            {
                if (!Enabled)
                {
                    fillColor = ThemeDrawing.Mix(palette.AccentBlue, palette.SecondaryButtonBackground, 0.55f);
                    borderColor = Color.Transparent;
                    textColor = ThemeDrawing.WithAlpha(Color.White, 145);
                }
                else if (isPressed)
                {
                    fillColor = palette.AccentPressedBlue;
                    borderColor = palette.AccentPressedBlue;
                    textColor = Color.White;
                }
                else if (isHovered)
                {
                    fillColor = palette.AccentHoverBlue;
                    borderColor = palette.AccentHoverBlue;
                    textColor = Color.White;
                }
                else
                {
                    fillColor = palette.AccentBlue;
                    borderColor = palette.AccentBlue;
                    textColor = Color.White;
                }
            }
            else
            {
                if (!Enabled)
                {
                    fillColor = palette.SecondaryButtonBackground;
                    borderColor = ThemeDrawing.WithAlpha(palette.SecondaryButtonBorder, 100);
                    textColor = palette.MutedText;
                }
                else if (isPressed)
                {
                    fillColor = ThemeDrawing.Mix(palette.SecondaryButtonHover, palette.AccentBlue, 0.18f);
                    borderColor = palette.AccentBlue;
                    textColor = palette.PrimaryText;
                }
                else if (isHovered)
                {
                    fillColor = palette.SecondaryButtonHover;
                    borderColor = ThemeDrawing.Mix(palette.SecondaryButtonBorder, palette.PrimaryText, 0.22f);
                    textColor = palette.PrimaryText;
                }
                else
                {
                    fillColor = palette.SecondaryButtonBackground;
                    borderColor = palette.SecondaryButtonBorder;
                    textColor = palette.PrimaryText;
                }
            }

            using (var path = ThemeDrawing.CreateRoundedRectangle(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            {
                g.FillPath(fillBrush, path);
                if (borderColor.A > 0)
                {
                    using (var borderPen = new Pen(borderColor, 1f))
                        g.DrawPath(borderPen, path);
                }
            }

            using (var font = new Font("Microsoft YaHei UI", 9.5f, IsPrimary ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(
                    g,
                    Text ?? string.Empty,
                    font,
                    ClientRectangle,
                    textColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>
    /// 配置分组卡片内的单行配置项控件。
    /// 左侧显示配置项名称、辅助说明及校验错误提示；右侧显示对应的交互控件。
    /// </summary>
    internal sealed class SettingsItemRow : Panel, ISettingsPaletteAware
    {
        private readonly SettingsItemDescriptor descriptor;
        private SettingsPalette palette = SettingsPalette.CreateDark();
        private string validationError;
        private bool showBottomDivider = true;

        public SettingsItemRow(SettingsItemDescriptor descriptor)
        {
            this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            Height = 66;

            if (descriptor.EditorControl != null)
            {
                Controls.Add(descriptor.EditorControl);
            }
        }

        public SettingsItemDescriptor Descriptor => descriptor;

        public bool ShowBottomDivider
        {
            get => showBottomDivider;
            set
            {
                if (showBottomDivider != value)
                {
                    showBottomDivider = value;
                    Invalidate();
                }
            }
        }

        public string ValidationError
        {
            get => validationError;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? null : value;
                if (!string.Equals(validationError, next, StringComparison.Ordinal))
                {
                    validationError = next;
                    if (descriptor.EditorControl is SettingsDirectoryPicker dirPicker)
                        dirPicker.HasValidationError = next != null;
                    else if (descriptor.EditorControl is SettingsFilePicker filePicker)
                        filePicker.HasValidationError = next != null;
                    UpdateRowLayout();
                    Invalidate();
                }
            }
        }

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            BackColor = palette.CardBackground;
            if (descriptor.EditorControl != null)
            {
                ApplyPaletteToChildControl(descriptor.EditorControl, palette);
            }
            UpdateRowLayout();
            Invalidate();
        }

        public static void ApplyPaletteToChildControl(Control control, SettingsPalette palette)
        {
            if (control is ISettingsPaletteAware paletteAware)
            {
                paletteAware.ApplyPalette(palette);
            }
            else if (control is IThemedControl themedControl)
            {
                themedControl.ApplyTheme(palette.ToControlThemeDefinition());
            }

            foreach (Control child in control.Controls)
            {
                ApplyPaletteToChildControl(child, palette);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRowLayout();
        }

        public void UpdateRowLayout()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padH = Math.Max(16, (int)Math.Round(20 * scale));
            bool hasDesc = descriptor.Description != null && !string.IsNullOrWhiteSpace(descriptor.Description());
            bool hasError = !string.IsNullOrWhiteSpace(validationError);
            int targetHeight = hasDesc
                ? (int)Math.Round((hasError ? 82 : 66) * scale)
                : (int)Math.Round((hasError ? 64 : 50) * scale);

            if (descriptor.EditorControl != null)
            {
                targetHeight = Math.Max(targetHeight, descriptor.EditorControl.Height + (int)Math.Round(18 * scale));
            }

            if (Height != targetHeight)
                Height = targetHeight;

            if (descriptor.EditorControl != null)
            {
                int ctrlX = Math.Max(padH + 140, Width - padH - descriptor.EditorControl.Width);
                int ctrlY = (Height - descriptor.EditorControl.Height) / 2;
                descriptor.EditorControl.Location = new Point(ctrlX, Math.Max(4, ctrlY));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(palette.CardBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int padH = Math.Max(16, (int)Math.Round(20 * scale));
            int rightLimit = descriptor.EditorControl != null
                ? descriptor.EditorControl.Left - (int)Math.Round(16 * scale)
                : Width - padH;
            int textWidth = Math.Max(60, rightLimit - padH);

            string title = descriptor.Title != null ? descriptor.Title() : descriptor.Key;
            string desc = descriptor.Description != null ? descriptor.Description() : null;
            bool hasDesc = !string.IsNullOrWhiteSpace(desc);
            bool hasError = !string.IsNullOrWhiteSpace(validationError);

            using (var titleFont = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Bold))
            using (var descFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular))
            {
                if (!hasDesc && !hasError)
                {
                    var titleRect = new Rectangle(padH, 0, textWidth, Height);
                    TextRenderer.DrawText(
                        g,
                        title,
                        titleFont,
                        titleRect,
                        palette.PrimaryText,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    int topY = (int)Math.Round(12 * scale);
                    var titleRect = new Rectangle(padH, topY, textWidth, (int)Math.Round(22 * scale));
                    TextRenderer.DrawText(
                        g,
                        title,
                        titleFont,
                        titleRect,
                        palette.PrimaryText,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    if (hasDesc)
                    {
                        var descRect = new Rectangle(padH, titleRect.Bottom + (int)Math.Round(2 * scale), textWidth, (int)Math.Round(20 * scale));
                        TextRenderer.DrawText(
                            g,
                            desc,
                            descFont,
                            descRect,
                            palette.SecondaryText,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }

                    if (hasError)
                    {
                        int errY = hasDesc
                            ? titleRect.Bottom + (int)Math.Round(21 * scale)
                            : titleRect.Bottom + (int)Math.Round(2 * scale);
                        var errRect = new Rectangle(padH, errY, textWidth, (int)Math.Round(18 * scale));
                        TextRenderer.DrawText(
                            g,
                            "⚠ " + validationError,
                            descFont,
                            errRect,
                            palette.ErrorColor,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                }
            }

            if (showBottomDivider)
            {
                using (var divPen = new Pen(palette.DividerColor, 1f))
                {
                    g.DrawLine(divPen, padH, Height - 1, Width - padH, Height - 1);
                }
            }
        }
    }

    /// <summary>
    /// 配置分组卡片容器控件。采用圆角边框、图标+分组标题头部及细分隔线结构。
    /// </summary>
    internal sealed class SettingsGroupCard : Panel, ISettingsPaletteAware
    {
        private readonly SettingsGroupDescriptor groupDescriptor;
        private readonly List<SettingsItemRow> itemRows = new List<SettingsItemRow>();
        private SettingsPalette palette = SettingsPalette.CreateDark();

        public SettingsGroupCard(SettingsGroupDescriptor groupDescriptor)
        {
            this.groupDescriptor = groupDescriptor ?? throw new ArgumentNullException(nameof(groupDescriptor));
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            if (groupDescriptor.CustomHeaderContent != null)
            {
                Controls.Add(groupDescriptor.CustomHeaderContent);
            }

            for (int i = 0; i < groupDescriptor.Items.Count; i++)
            {
                var row = new SettingsItemRow(groupDescriptor.Items[i])
                {
                    ShowBottomDivider = i < groupDescriptor.Items.Count - 1 || groupDescriptor.CustomBodyContent != null
                };
                itemRows.Add(row);
                Controls.Add(row);
            }

            if (groupDescriptor.CustomBodyContent != null)
            {
                Controls.Add(groupDescriptor.CustomBodyContent);
            }
        }

        public SettingsGroupDescriptor GroupDescriptor => groupDescriptor;
        public IReadOnlyList<SettingsItemRow> ItemRows => itemRows;

        public void ApplyPalette(SettingsPalette newPalette)
        {
            palette = newPalette ?? SettingsPalette.CreateDark();
            BackColor = palette.ContentBackground;

            if (groupDescriptor.CustomHeaderContent != null)
                SettingsItemRow.ApplyPaletteToChildControl(groupDescriptor.CustomHeaderContent, palette);

            foreach (var row in itemRows)
                row.ApplyPalette(palette);

            if (groupDescriptor.CustomBodyContent != null)
                SettingsItemRow.ApplyPaletteToChildControl(groupDescriptor.CustomBodyContent, palette);

            PerformCardLayout();
            Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PerformCardLayout();
        }

        public void PerformCardLayout()
        {
            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            int headerHeight = Math.Max(42, (int)Math.Round(46 * scale));
            int borderInset = 2;
            int innerWidth = Math.Max(80, Width - borderInset * 2);
            int currentY = headerHeight + 1;

            if (groupDescriptor.CustomHeaderContent != null)
            {
                int padRight = (int)Math.Round(18 * scale);
                groupDescriptor.CustomHeaderContent.Location = new Point(
                    Math.Max(140, Width - padRight - groupDescriptor.CustomHeaderContent.Width),
                    (headerHeight - groupDescriptor.CustomHeaderContent.Height) / 2);
            }

            for (int i = 0; i < itemRows.Count; i++)
            {
                var row = itemRows[i];
                row.ShowBottomDivider = i < itemRows.Count - 1 || groupDescriptor.CustomBodyContent != null;
                row.Width = innerWidth;
                row.UpdateRowLayout();
                row.Location = new Point(borderInset, currentY);
                currentY += row.Height;
            }

            if (groupDescriptor.CustomBodyContent != null)
            {
                int bodyPad = (int)Math.Round(16 * scale);
                groupDescriptor.CustomBodyContent.Location = new Point(borderInset + bodyPad, currentY + bodyPad);
                groupDescriptor.CustomBodyContent.Width = Math.Max(60, innerWidth - bodyPad * 2);
                currentY += groupDescriptor.CustomBodyContent.Height + bodyPad * 2;
            }

            int totalHeight = currentY + (int)Math.Round(6 * scale);
            if (Height != totalHeight)
                Height = totalHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? palette.ContentBackground);

            float scale = DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
            float radius = 10f * scale;
            int headerHeight = Math.Max(42, (int)Math.Round(46 * scale));
            var cardRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

            using (var cardPath = ThemeDrawing.CreateRoundedRectangle(cardRect, radius))
            using (var fillBrush = new SolidBrush(palette.CardBackground))
            using (var borderPen = new Pen(palette.CardBorder, 1f))
            {
                g.FillPath(fillBrush, cardPath);
                g.DrawPath(borderPen, cardPath);
            }

            // 分组标题图标 + 标题文字
            int padH = Math.Max(16, (int)Math.Round(20 * scale));
            int iconSize = Math.Max(15, (int)Math.Round(16 * scale));
            int iconY = (headerHeight - iconSize) / 2;
            SettingsIconRenderer.DrawGroupIcon(
                g,
                groupDescriptor.Icon,
                new RectangleF(padH, iconY, iconSize, iconSize),
                palette.SecondaryText);

            using (var headerFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold))
            {
                int textX = padH + iconSize + (int)Math.Round(9 * scale);
                var textRect = new Rectangle(textX, 0, Math.Max(60, Width - textX - padH), headerHeight);
                TextRenderer.DrawText(
                    g,
                    groupDescriptor.Title != null ? groupDescriptor.Title() : groupDescriptor.Id,
                    headerFont,
                    textRect,
                    palette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            // 分组头部底部分隔细线
            using (var divPen = new Pen(palette.DividerColor, 1f))
            {
                g.DrawLine(divPen, padH, headerHeight, Width - padH, headerHeight);
            }
        }
    }
}
