using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences.Theming
{
    /// <summary>
    /// 设置页面固定视觉调色板。仅根据浅色模式/黑夜模式切换（浅色以白色为背景，
    /// 黑夜模式以深色为背景，统一采用蓝色作为交互强调色），不受桌面栅栏主题影响。
    /// </summary>
    internal sealed class SettingsPalette
    {
        public bool IsDark { get; private set; }
        public Color WindowBackground { get; private set; }
        public Color WindowBorder { get; private set; }
        public Color TitleBarLeftBackground { get; private set; }
        public Color TitleBarRightBackground { get; private set; }
        public Color SidebarBackground { get; private set; }
        public Color ContentBackground { get; private set; }
        public Color CardBackground { get; private set; }
        public Color CardBorder { get; private set; }
        public Color DividerColor { get; private set; }
        public Color PrimaryText { get; private set; }
        public Color SecondaryText { get; private set; }
        public Color MutedText { get; private set; }
        public Color ControlBackground { get; private set; }
        public Color ControlHoverBackground { get; private set; }
        public Color ControlBorder { get; private set; }
        public Color AccentBlue { get; private set; }
        public Color AccentHoverBlue { get; private set; }
        public Color AccentPressedBlue { get; private set; }
        public Color NavSelectedFillStart { get; private set; }
        public Color NavSelectedFillEnd { get; private set; }
        public Color NavSelectedBorder { get; private set; }
        public Color NavSelectedAccentBar { get; private set; }
        public Color NavSelectedText { get; private set; }
        public Color NavHoverBackground { get; private set; }
        public Color SwitchOffTrack { get; private set; }
        public Color SecondaryButtonBackground { get; private set; }
        public Color SecondaryButtonHover { get; private set; }
        public Color SecondaryButtonBorder { get; private set; }
        public Color ErrorColor { get; private set; }
        public Color SuccessColor { get; private set; }
        public Color WarningColor { get; private set; }

        /// <summary>根据黑夜模式开关获取对应的设置页面调色板。</summary>
        public static SettingsPalette FromColorMode(bool darkMode)
        {
            return darkMode ? CreateDark() : CreateLight();
        }

        /// <summary>创建黑夜模式（深色背景 + 蓝色强调色）调色板。</summary>
        public static SettingsPalette CreateDark()
        {
            return new SettingsPalette
            {
                IsDark = true,
                WindowBackground = Color.FromArgb(22, 27, 36),
                WindowBorder = Color.FromArgb(46, 55, 72),
                TitleBarLeftBackground = Color.FromArgb(30, 36, 48),
                TitleBarRightBackground = Color.FromArgb(24, 29, 39),
                SidebarBackground = Color.FromArgb(28, 34, 45),
                ContentBackground = Color.FromArgb(22, 27, 36),
                CardBackground = Color.FromArgb(27, 33, 44),
                CardBorder = Color.FromArgb(40, 48, 63),
                DividerColor = Color.FromArgb(36, 43, 56),
                PrimaryText = Color.FromArgb(241, 245, 249),
                SecondaryText = Color.FromArgb(139, 152, 169),
                MutedText = Color.FromArgb(100, 116, 139),
                ControlBackground = Color.FromArgb(30, 37, 50),
                ControlHoverBackground = Color.FromArgb(37, 46, 62),
                ControlBorder = Color.FromArgb(51, 61, 79),
                AccentBlue = Color.FromArgb(43, 122, 255),
                AccentHoverBlue = Color.FromArgb(66, 138, 255),
                AccentPressedBlue = Color.FromArgb(29, 98, 224),
                NavSelectedFillStart = Color.FromArgb(38, 79, 138),
                NavSelectedFillEnd = Color.FromArgb(31, 64, 112),
                NavSelectedBorder = Color.FromArgb(52, 102, 172),
                NavSelectedAccentBar = Color.FromArgb(68, 148, 255),
                NavSelectedText = Color.FromArgb(255, 255, 255),
                NavHoverBackground = Color.FromArgb(35, 42, 56),
                SwitchOffTrack = Color.FromArgb(56, 67, 86),
                SecondaryButtonBackground = Color.FromArgb(35, 42, 54),
                SecondaryButtonHover = Color.FromArgb(45, 54, 69),
                SecondaryButtonBorder = Color.FromArgb(52, 62, 80),
                ErrorColor = Color.FromArgb(248, 113, 113),
                SuccessColor = Color.FromArgb(52, 211, 153),
                WarningColor = Color.FromArgb(96, 165, 250)
            };
        }

        /// <summary>创建浅色模式（白色背景 + 蓝色强调色）调色板。</summary>
        public static SettingsPalette CreateLight()
        {
            return new SettingsPalette
            {
                IsDark = false,
                WindowBackground = Color.FromArgb(255, 255, 255),
                WindowBorder = Color.FromArgb(203, 213, 225),
                TitleBarLeftBackground = Color.FromArgb(244, 247, 251),
                TitleBarRightBackground = Color.FromArgb(250, 251, 253),
                SidebarBackground = Color.FromArgb(246, 248, 252),
                ContentBackground = Color.FromArgb(255, 255, 255),
                CardBackground = Color.FromArgb(255, 255, 255),
                CardBorder = Color.FromArgb(226, 232, 240),
                DividerColor = Color.FromArgb(238, 242, 247),
                PrimaryText = Color.FromArgb(15, 23, 42),
                SecondaryText = Color.FromArgb(100, 116, 139),
                MutedText = Color.FromArgb(148, 163, 184),
                ControlBackground = Color.FromArgb(248, 250, 252),
                ControlHoverBackground = Color.FromArgb(241, 245, 249),
                ControlBorder = Color.FromArgb(203, 213, 225),
                AccentBlue = Color.FromArgb(37, 99, 235),
                AccentHoverBlue = Color.FromArgb(29, 78, 216),
                AccentPressedBlue = Color.FromArgb(30, 64, 175),
                NavSelectedFillStart = Color.FromArgb(232, 242, 255),
                NavSelectedFillEnd = Color.FromArgb(219, 234, 254),
                NavSelectedBorder = Color.FromArgb(186, 214, 252),
                NavSelectedAccentBar = Color.FromArgb(37, 99, 235),
                NavSelectedText = Color.FromArgb(29, 78, 216),
                NavHoverBackground = Color.FromArgb(237, 242, 249),
                SwitchOffTrack = Color.FromArgb(203, 213, 225),
                SecondaryButtonBackground = Color.FromArgb(241, 245, 249),
                SecondaryButtonHover = Color.FromArgb(226, 232, 240),
                SecondaryButtonBorder = Color.FromArgb(203, 213, 225),
                ErrorColor = Color.FromArgb(220, 38, 38),
                SuccessColor = Color.FromArgb(16, 185, 129),
                WarningColor = Color.FromArgb(37, 99, 235)
            };
        }

        /// <summary>
        /// 将当前设置页面调色板转换为供 IThemedControl 使用的独立 ThemeDefinition，
        /// 确保设置页内的下拉框、数值框、按钮与文本框始终保持设置页明暗配色，不随栅栏主题改变。
        /// </summary>
        public ThemeDefinition ToControlThemeDefinition()
        {
            return new ThemeDefinition
            {
                Name = IsDark ? "SettingsDark" : "SettingsLight",
                FontFamilyName = "Microsoft YaHei UI",
                DialogBackgroundColorArgb = CardBackground.ToArgb(),
                DialogTextColorArgb = PrimaryText.ToArgb(),
                ControlBackgroundColorArgb = ControlBackground.ToArgb(),
                ControlTextColorArgb = PrimaryText.ToArgb(),
                BorderColorArgb = ControlBorder.ToArgb(),
                AccentColorArgb = AccentBlue.ToArgb(),
                MenuBackgroundColorArgb = CardBackground.ToArgb(),
                MenuTextColorArgb = PrimaryText.ToArgb(),
                MenuHighlightColorArgb = IsDark
                    ? Color.FromArgb(38, 52, 74).ToArgb()
                    : Color.FromArgb(232, 242, 255).ToArgb(),
                MenuHighlightTextColorArgb = PrimaryText.ToArgb()
            };
        }
    }

    /// <summary>左侧配置分类导航图标类型。</summary>
    internal enum SettingsCategoryIcon
    {
        BasicHome,
        ThemePalette,
        Gear,
        Storage
    }

    /// <summary>右侧配置分组卡片标题图标类型。</summary>
    internal enum SettingsGroupIcon
    {
        Gear,
        Folder,
        Palette,
        Sliders,
        Image,
        Swatches
    }

    /// <summary>配置项交互控件类型枚举。</summary>
    internal enum SettingsControlType
    {
        ToggleSwitch,
        Dropdown,
        NumberInput,
        TextInput,
        DirectoryPicker,
        FilePicker,
        ColorSwatch,
        Custom
    }

    /// <summary>配置项校验结果。</summary>
    internal sealed class SettingsValidationResult
    {
        private SettingsValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public static SettingsValidationResult Valid() =>
            new SettingsValidationResult(true, null);

        public static SettingsValidationResult Invalid(string errorMessage) =>
            new SettingsValidationResult(false, errorMessage);
    }

    /// <summary>
    /// 统一的配置项数据绑定与状态管理契约。
    /// 支持配置读取、修改检测、有效性校验、取消恢复及默认值重置。
    /// </summary>
    internal interface ISettingsBinding
    {
        /// <summary>配置项唯一键。</summary>
        string Key { get; }

        /// <summary>所属分类标识。</summary>
        string CategoryId { get; }

        /// <summary>当前草稿值相对于最近一次保存快照是否发生了修改。</summary>
        bool IsModified { get; }

        /// <summary>当草稿值发生变更时触发。</summary>
        event EventHandler ValueChanged;

        /// <summary>校验当前草稿值的合法性。</summary>
        SettingsValidationResult Validate();

        /// <summary>将草稿值恢复到最近一次保存的快照状态，并同步刷新 UI 控件。</summary>
        void RevertToSaved();

        /// <summary>将草稿值恢复到出厂默认值，并同步刷新 UI 控件。</summary>
        void ResetToDefault();

        /// <summary>将当前草稿值确认为最新已保存快照。</summary>
        void CommitSnapshot();

        /// <summary>将当前草稿值同步刷新到 UI 控件。</summary>
        void RefreshControlFromDraft();
    }

    /// <summary>
    /// 泛型配置项数据绑定器。封装草稿值读写、已保存快照对比、默认值与自定义校验逻辑。
    /// </summary>
    internal sealed class SettingsBinding<T> : ISettingsBinding
    {
        private readonly Func<T> draftGetter;
        private readonly Action<T> draftSetter;
        private readonly Action<T> controlUpdater;
        private readonly Func<T, SettingsValidationResult> validator;
        private readonly IEqualityComparer<T> comparer;
        private T savedSnapshot;
        private bool isUpdatingControl;

        public SettingsBinding(
            string key,
            string categoryId,
            T initialSavedValue,
            T defaultValue,
            Func<T> draftGetter,
            Action<T> draftSetter,
            Action<T> controlUpdater,
            Func<T, SettingsValidationResult> validator = null,
            IEqualityComparer<T> comparer = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            this.draftGetter = draftGetter ?? throw new ArgumentNullException(nameof(draftGetter));
            this.draftSetter = draftSetter ?? throw new ArgumentNullException(nameof(draftSetter));
            this.controlUpdater = controlUpdater;
            this.validator = validator;
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            savedSnapshot = initialSavedValue;
            DefaultValue = defaultValue;
        }

        public string Key { get; }
        public string CategoryId { get; }
        public T DefaultValue { get; }

        public event EventHandler ValueChanged;

        public bool IsModified => !comparer.Equals(draftGetter(), savedSnapshot);

        public void NotifyControlValueChanged(T newValue)
        {
            if (isUpdatingControl)
                return;

            draftSetter(newValue);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public SettingsValidationResult Validate()
        {
            if (validator == null)
                return SettingsValidationResult.Valid();
            return validator(draftGetter()) ?? SettingsValidationResult.Valid();
        }

        public void RevertToSaved()
        {
            draftSetter(savedSnapshot);
            RefreshControlFromDraft();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefault()
        {
            draftSetter(DefaultValue);
            RefreshControlFromDraft();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void CommitSnapshot()
        {
            savedSnapshot = draftGetter();
        }

        public void RefreshControlFromDraft()
        {
            if (controlUpdater == null)
                return;

            isUpdatingControl = true;
            try
            {
                controlUpdater(draftGetter());
            }
            finally
            {
                isUpdatingControl = false;
            }
        }
    }

    /// <summary>单个配置项元数据描述。</summary>
    internal sealed class SettingsItemDescriptor
    {
        public string Key { get; set; }
        public string CategoryId { get; set; }
        public Func<string> Title { get; set; }
        public Func<string> Description { get; set; }
        public SettingsControlType ControlType { get; set; }
        public ISettingsBinding Binding { get; set; }
        public Control EditorControl { get; set; }
        public bool IsCustomThemeOnly { get; set; }
    }

    /// <summary>配置分组卡片元数据描述。</summary>
    internal sealed class SettingsGroupDescriptor
    {
        public string Id { get; set; }
        public Func<string> Title { get; set; }
        public SettingsGroupIcon Icon { get; set; }
        public List<SettingsItemDescriptor> Items { get; } = new List<SettingsItemDescriptor>();
        public Control CustomHeaderContent { get; set; }
        public Control CustomBodyContent { get; set; }
    }

    /// <summary>左侧导航配置分类元数据描述。</summary>
    internal sealed class SettingsCategoryDescriptor
    {
        public string Id { get; set; }
        public Func<string> Title { get; set; }
        public Func<string> Description { get; set; }
        public SettingsCategoryIcon Icon { get; set; }
        public List<SettingsGroupDescriptor> Groups { get; } = new List<SettingsGroupDescriptor>();
    }
}
