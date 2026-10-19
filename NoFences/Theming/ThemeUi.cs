using NoFences.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences.Theming
{
    /// <summary>
    /// 递归应用设置窗口与菜单主题。窗口只依赖本服务，不需要分别判断
    /// Default、Windows 11 或 Windows XP，从而避免各页面出现不同步的主题逻辑。
    /// </summary>
    public static class ThemeUi
    {
        // 字体在这个轻量桌面程序的整个生命周期内共享。WinForms 控件对外部
        // Font 的所有权不一致，缓存可以避免实时预览每次刷新都泄漏一个 HFONT。
        private static readonly object fontCacheLock = new object();
        private static readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();

        public static void ApplyToForm(Form form, ThemeDefinition theme)
        {
            ApplyToForm(form, theme, ThemeManager.Instance.DarkModeEnabled);
        }

        /// <summary>
        /// Applies a palette and an explicit color mode. The overload is needed by
        /// the configuration preview: its switch may differ from the committed
        /// application setting until the user presses Apply or OK.
        /// </summary>
        public static void ApplyToForm(Form form, ThemeDefinition theme, bool darkModeEnabled)
        {
            if (form == null || theme == null)
                return;

            form.BackColor = theme.DialogBackgroundColor;
            form.ForeColor = theme.DialogTextColor;
            form.Font = GetThemeFont(theme.FontFamilyName, form.Font.Size, form.Font.Style);
            ApplyToChildren(form.Controls, theme);

            if (form.IsHandleCreated)
            {
                bool useDarkTitleBar = darkModeEnabled || ThemeDrawing.IsDark(theme.DialogBackgroundColor);
                WindowUtil.TrySetImmersiveDarkMode(form.Handle, useDarkTitleBar);
            }
            form.Invalidate(true);
        }

        public static void ApplyToContextMenu(ContextMenuStrip menu, ThemeDefinition theme, int targetDpi = 0)
        {
            if (menu == null || theme == null)
                return;

            int effectiveDpi = targetDpi > 0
                ? targetDpi
                : ResolveToolStripDpi(menu);

            ApplyToToolStrip(menu, theme, new ThemedToolStripRenderer(theme, effectiveDpi), effectiveDpi);
        }

        internal static int ResolveToolStripDpi(ToolStrip strip)
        {
            if (strip == null)
                return 96;

            var menu = strip as ContextMenuStrip;
            if (menu != null && menu.SourceControl != null && menu.SourceControl.IsHandleCreated)
            {
                uint dpi = WindowUtil.GetDpiForWindow(menu.SourceControl.Handle);
                if (dpi > 0)
                    return (int)dpi;
            }

            if (strip.IsHandleCreated)
            {
                uint dpi = WindowUtil.GetDpiForWindow(strip.Handle);
                if (dpi > 0)
                    return (int)dpi;
            }

            return strip.DeviceDpi > 0 ? strip.DeviceDpi : 96;
        }

        private static void ApplyToChildren(Control.ControlCollection controls, ThemeDefinition theme)
        {
            foreach (Control control in controls)
            {
                ApplyToControl(control, theme);
                if (control.HasChildren && !(control is ThemedTextBox) && !(control is ThemedNumericUpDown))
                    ApplyToChildren(control.Controls, theme);
            }
        }

        private static void ApplyToControl(Control control, ThemeDefinition theme)
        {
            control.ForeColor = theme.DialogTextColor;
            control.Font = GetThemeFont(theme.FontFamilyName, control.Font.Size, control.Font.Style);

            if (control is IThemedControl themedControl)
            {
                themedControl.ApplyTheme(theme);
                return;
            }

            if (control is TextBoxBase)
            {
                control.BackColor = theme.ControlBackgroundColor;
                control.ForeColor = theme.ControlTextColor;
                ((TextBoxBase)control).BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox)
            {
                control.BackColor = theme.ControlBackgroundColor;
                control.ForeColor = theme.ControlTextColor;
                ((ComboBox)control).FlatStyle = FlatStyle.Flat;
            }
            else if (control is NumericUpDown)
            {
                control.BackColor = theme.ControlBackgroundColor;
                control.ForeColor = theme.ControlTextColor;
                ((NumericUpDown)control).BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ListBox || control is CheckedListBox)
            {
                control.BackColor = theme.ControlBackgroundColor;
                control.ForeColor = theme.ControlTextColor;
            }
            else if (control is Button)
            {
                var button = (Button)control;
                button.UseVisualStyleBackColor = false;
                button.BackColor = ThemeDrawing.Mix(theme.ControlBackgroundColor, theme.DialogBackgroundColor, 0.2f);
                button.ForeColor = theme.ControlTextColor;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = theme.BorderColor;
                button.FlatAppearance.MouseOverBackColor = theme.MenuHighlightColor;
                button.FlatAppearance.MouseDownBackColor = ThemeDrawing.Mix(theme.MenuHighlightColor, theme.AccentColor, 0.35f);
            }
            else if (control is TabControl || control is TabPage || control is Panel ||
                     control is TableLayoutPanel || control is FlowLayoutPanel || control is GroupBox)
            {
                control.BackColor = theme.DialogBackgroundColor;
                control.ForeColor = theme.DialogTextColor;
            }
            else if (control is TrackBar || control is CheckBox || control is RadioButton || control is Label)
            {
                // Some native WinForms controls (notably TrackBar) reject
                // Color.Transparent. Matching the parent color is visually
                // equivalent and works consistently on .NET Framework 4.8.
                control.BackColor = theme.DialogBackgroundColor;
                control.ForeColor = theme.DialogTextColor;
            }
            else if (control is ToolStrip)
            {
                ApplyToToolStrip(
                    (ToolStrip)control,
                    theme,
                    new ThemedToolStripRenderer(theme));
            }
            else
            {
                control.BackColor = theme.DialogBackgroundColor;
            }
        }

        /// <summary>
        /// 将同一个主题渲染器应用到工具栏及其所有下拉层级。
        /// ToolStripMenuItem.DropDown 本身是一个独立 ToolStrip；只修改顶层菜单
        /// 不会可靠地覆盖子菜单的背景、边框和悬停状态，因此必须递归处理。
        /// </summary>
        private static void ApplyToToolStrip(
            ToolStrip strip,
            ThemeDefinition theme,
            ThemedToolStripRenderer renderer,
            int targetDpi = 0)
        {
            MenuThemeProfile profile = renderer.Profile;
            int effectiveDpi = targetDpi > 0
                ? targetDpi
                : (renderer.TargetDpi > 0 ? renderer.TargetDpi : ResolveToolStripDpi(strip));

            bool isDropDown = strip is ToolStripDropDown;
            bool isHostedCardDropDown = isDropDown &&
                strip.Items.Count == 1 &&
                strip.Items[0] is ToolStripControlHost;
            Padding menuPadding = ScalePadding(strip, profile.MenuPadding, effectiveDpi);
            Padding itemPadding = ScalePadding(strip, profile.ItemPadding, effectiveDpi);
            int minimumWidth = ScaleLogical(strip, profile.MinimumWidth, effectiveDpi);
            int itemHeight = ScaleLogical(strip, profile.ItemHeight, effectiveDpi);
            int separatorHeight = ScaleLogical(strip, profile.SeparatorHeight, effectiveDpi);

            strip.SuspendLayout();
            try
            {
                strip.Renderer = renderer;
                strip.BackColor = theme.MenuBackgroundColor;
                strip.ForeColor = theme.MenuTextColor;
                strip.Font = GetThemeFont(
                    profile.FontFamilyName,
                    profile.FontSize,
                    profile.FontStyle,
                    theme.FontFamilyName);
                strip.ImageScalingSize = new Size(
                    ScaleLogical(strip, profile.ImageScalingSize.Width, effectiveDpi),
                    ScaleLogical(strip, profile.ImageScalingSize.Height, effectiveDpi));
                strip.GripStyle = ToolStripGripStyle.Hidden;

                if (isDropDown)
                {
                    strip.AutoSize = true;
                    strip.MinimumSize = isHostedCardDropDown
                        ? Size.Empty
                        : new Size(minimumWidth, 0);
                    strip.Padding = isHostedCardDropDown
                        ? Padding.Empty
                        : menuPadding;

                    var dropDown = (ToolStripDropDown)strip;
                    dropDown.DropShadowEnabled = profile.DropShadowEnabled;
                    dropDown.Opacity = profile.Opacity;
                    dropDown.Tag = new DropDownThemeState { Profile = profile, Theme = theme };
                    dropDown.HandleCreated -= DropDownHandleCreatedHandler;
                    dropDown.HandleCreated += DropDownHandleCreatedHandler;
                    if (dropDown.IsHandleCreated)
                    {
                        ConfigureDropDownHandle(dropDown.Handle, profile, theme);
                    }

                    var dropDownMenu = strip as ToolStripDropDownMenu;
                    if (dropDownMenu != null)
                    {
                        dropDownMenu.ShowImageMargin = !isHostedCardDropDown;
                        dropDownMenu.ShowCheckMargin = false;
                    }
                }

                ApplyToToolStripItems(
                    strip,
                    strip.Items,
                    theme,
                    renderer,
                    isDropDown,
                    itemPadding,
                    minimumWidth,
                    itemHeight,
                    separatorHeight,
                    effectiveDpi);
            }
            finally
            {
                strip.ResumeLayout(true);
            }

            strip.PerformLayout();
            if (isDropDown && !isHostedCardDropDown)
                EqualizeDropDownHorizontalMargins(strip);
            strip.Invalidate();
        }

        /// <summary>
        /// 让下拉菜单中每个项目的右边距与其布局后的左边距一致。WinForms 的
        /// ToolStripDropDownMenu 会把水平 Padding 计入容器宽度，却不会始终把
        /// 同等空间放到项目左侧；固定项目宽度后，多余空间会因此全部留在右边。
        /// </summary>
        private static void EqualizeDropDownHorizontalMargins(ToolStrip strip)
        {
            int clientWidth = strip.ClientSize.Width;
            if (clientWidth <= 0)
                return;

            foreach (ToolStripItem item in strip.Items)
            {
                int left = Math.Max(0, item.Bounds.Left);
                int width = Math.Max(1, clientWidth - left * 2);
                item.AutoSize = false;
                item.Size = new Size(width, item.Height);
            }
        }

        /// <summary>
        /// 同步菜单项本身的字体和前景色，并把主题继续传递给已创建的子菜单。
        /// </summary>
        private static void ApplyToToolStripItems(
            ToolStrip owner,
            ToolStripItemCollection items,
            ThemeDefinition theme,
            ThemedToolStripRenderer renderer,
            bool applyDropDownMetrics,
            Padding itemPadding,
            int minimumWidth,
            int itemHeight,
            int separatorHeight,
            int targetDpi = 0)
        {
            MenuThemeProfile profile = renderer.Profile;
            int effectiveDpi = targetDpi > 0
                ? targetDpi
                : (renderer.TargetDpi > 0 ? renderer.TargetDpi : ResolveToolStripDpi(owner));
            foreach (ToolStripItem item in items)
            {
                item.BackColor = theme.MenuBackgroundColor;
                item.ForeColor = theme.MenuTextColor;
                item.Font = GetThemeFont(
                    profile.FontFamilyName,
                    profile.FontSize,
                    profile.FontStyle,
                    theme.FontFamilyName);

                var controlHost = item as ToolStripControlHost;
                if (controlHost != null)
                {
                    controlHost.Margin = Padding.Empty;
                    controlHost.Padding = Padding.Empty;
                    if (controlHost.Control != null)
                    {
                        controlHost.Control.Font = item.Font;
                        var scaleControl = controlHost.Control as ScaleMenuControl;
                        if (scaleControl != null)
                        {
                            scaleControl.ApplyTheme(theme, profile, effectiveDpi);
                            controlHost.AutoSize = false;
                            controlHost.Size = scaleControl.Size;
                        }
                    }
                    continue;
                }

                if (applyDropDownMetrics)
                {
                    item.Margin = Padding.Empty;
                    if (item is ToolStripSeparator)
                    {
                        item.AutoSize = false;
                        item.Padding = Padding.Empty;
                        item.Size = new Size(
                            Math.Max(1, minimumWidth - owner.Padding.Horizontal),
                            separatorHeight);
                    }
                    else
                    {
                        // 先恢复 AutoSize 取得当前文字、快捷键和箭头的自然宽度，
                        // 再固定规格要求的行高；切换 Win11/XP 时不会继承旧尺寸。
                        item.AutoSize = true;
                        item.Padding = itemPadding;
                        Size preferred = item.GetPreferredSize(Size.Empty);
                        item.AutoSize = false;
                        item.Size = new Size(
                            Math.Max(
                                preferred.Width,
                                minimumWidth - owner.Padding.Horizontal),
                            itemHeight);
                    }
                }

                var menuItem = item as ToolStripMenuItem;
                if (menuItem != null && menuItem.HasDropDownItems)
                    ApplyToToolStrip(menuItem.DropDown, theme, renderer, effectiveDpi);
            }
        }

        /// <summary>
        /// 把 Profile 中的 96-DPI 逻辑像素换算为当前菜单的设备像素。
        /// 在 Opening 阶段再次应用主题时，ContextMenuStrip 已取得所属显示器 DPI，
        /// 因而拖到不同缩放比例的显示器后仍可保持 22px/34px 的逻辑行高。
        /// </summary>
        private static int ScaleLogical(ToolStrip strip, int logicalPixels, int targetDpi = 0)
        {
            int dpi = targetDpi > 0 ? targetDpi : (strip?.DeviceDpi > 0 ? strip.DeviceDpi : 96);
            return Math.Max(1, (int)Math.Round(
                logicalPixels * dpi / 96d,
                MidpointRounding.AwayFromZero));
        }

        private static Padding ScalePadding(ToolStrip strip, Padding logicalPadding, int targetDpi = 0)
        {
            return new Padding(
                ScaleLogicalAllowZero(strip, logicalPadding.Left, targetDpi),
                ScaleLogicalAllowZero(strip, logicalPadding.Top, targetDpi),
                ScaleLogicalAllowZero(strip, logicalPadding.Right, targetDpi),
                ScaleLogicalAllowZero(strip, logicalPadding.Bottom, targetDpi));
        }

        private static int ScaleLogicalAllowZero(ToolStrip strip, int logicalPixels, int targetDpi = 0)
        {
            if (logicalPixels == 0)
                return 0;
            int dpi = targetDpi > 0 ? targetDpi : (strip?.DeviceDpi > 0 ? strip.DeviceDpi : 96);
            return Math.Max(1, (int)Math.Round(
                logicalPixels * dpi / 96d,
                MidpointRounding.AwayFromZero));
        }

        private static Font GetThemeFont(
            string familyName,
            float size,
            FontStyle style,
            string fallbackFamilyName = null)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                familyName = "Segoe UI";
            if (string.IsNullOrWhiteSpace(fallbackFamilyName))
                fallbackFamilyName = "Segoe UI";
            string key = familyName + "|" + fallbackFamilyName + "|" + size + "|" + (int)style;
            lock (fontCacheLock)
            {
                if (fontCache.TryGetValue(key, out var cached))
                    return cached;
                try
                {
                    cached = new Font(familyName, size, style);

                    // GDI+ 对不存在的字体通常静默回退而不是抛异常。显式检查
                    // 实际 FontFamily，保证没有 Segoe UI Variable 的系统回退到
                    // 当前主题字体，而不是不可预测的 Microsoft Sans Serif。
                    if (!string.Equals(
                        cached.FontFamily.Name,
                        familyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        cached.Dispose();
                        cached = new Font(fallbackFamilyName, size, style);
                    }
                }
                catch
                {
                    try
                    {
                        cached = new Font(fallbackFamilyName, size, style);
                    }
                    catch
                    {
                        cached = new Font("Segoe UI", size, style);
                    }
                }
                fontCache[key] = cached;
                return cached;
            }
        }

        private static readonly EventHandler DropDownHandleCreatedHandler = (sender, _) =>
        {
            if (sender is ToolStripDropDown dropDown && dropDown.Tag is DropDownThemeState state)
            {
                ConfigureDropDownHandle(dropDown.Handle, state.Profile, state.Theme);
            }
        };

        private sealed class DropDownThemeState
        {
            public MenuThemeProfile Profile { get; set; }
            public ThemeDefinition Theme { get; set; }
        }

        private static void ConfigureDropDownHandle(IntPtr hwnd, MenuThemeProfile profile, ThemeDefinition theme)
        {
            if (hwnd == IntPtr.Zero)
                return;

            bool hasCorners = profile.ContainerCornerRadius > 0;
            bool usesNativeCorners = DropShadow.TrySetNativeCorners(hwnd, hasCorners);
            if (usesNativeCorners)
            {
                DropShadow.SetShadow(hwnd, true);
            }
            BlurUtil.SetBlur(
                hwnd,
                profile.Style == ThemeMenuStyle.Standard &&
                theme.EnableBlur &&
                profile.Opacity < 1d);
        }
    }

    /// <summary>
    /// Base class for normal settings dialogs. It keeps an already-open dialog in
    /// sync when another fence changes the global theme.
    /// </summary>
    public class ThemeAwareForm : Form
    {
        public ThemeAwareForm()
        {
            ThemeManager.Instance.ThemeChanged += ThemeManager_ThemeChanged;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyApplicationTheme(ThemeManager.Instance.CurrentTheme);
        }

        protected virtual void ApplyApplicationTheme(ThemeDefinition theme)
        {
            bool darkMode = ThemeManager.Instance.DarkModeEnabled;
            ThemeDefinition dialogTheme = SettingsPalette
                .FromColorMode(darkMode)
                .ToControlThemeDefinition();
            ThemeUi.ApplyToForm(this, dialogTheme, darkMode);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Instance.ThemeChanged -= ThemeManager_ThemeChanged;
            base.Dispose(disposing);
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyApplicationTheme(ThemeManager.Instance.CurrentTheme)));
                return;
            }
            ApplyApplicationTheme(ThemeManager.Instance.CurrentTheme);
        }
    }
}
