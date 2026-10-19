using NoFences.Localization;
using NoFences.Model;
using NoFences.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static NoFences.Win32.WindowUtil;

namespace NoFences.Theming
{
    /// <summary>
    /// 应用程序统一设置中心窗口。
    /// 采用“顶部自定义标题栏 + 左侧分类导航栏 + 右侧卡片式配置内容面板 + 底部固定操作栏”的双栏架构，
    /// 集中管理“基础设置”与“主题设置”，并支持可扩展的配置分类与统一数据绑定机制。
    /// 设置页面自身仅根据浅色/黑夜模式切换白底或深色背景（蓝色强调色），不再随栅栏主题改变。
    /// </summary>
    public sealed class ThemeConfigurationDialog : Form
    {
        private const string CategoryBasicId = "basic";
        private const string CategoryThemeId = "theme";

        // 顶层布局控件
        private readonly SettingsTitleBarControl titleBar = new SettingsTitleBarControl();
        private readonly SettingsSidebarControl sidebar = new SettingsSidebarControl();
        private readonly Panel rightContainer = new Panel();
        private readonly Panel pageHeaderPanel = new Panel();
        private readonly Panel scrollViewport = new Panel();
        private readonly Panel bottomActionBar = new Panel();
        private readonly SettingsActionButton cancelButton = new SettingsActionButton { IsPrimary = false };
        private readonly SettingsActionButton saveButton = new SettingsActionButton { IsPrimary = true };

        // 基础设置控件
        private readonly SettingsToggleSwitch launchAtStartupSwitch = new SettingsToggleSwitch();
        private readonly ThemedComboBox languageComboBox = new ThemedComboBox();
        private readonly SettingsToggleSwitch checkUpdatesSwitch = new SettingsToggleSwitch();
        private readonly SettingsDirectoryPicker dataPathPicker = new SettingsDirectoryPicker();
        private readonly SettingsToggleSwitch autoSaveSwitch = new SettingsToggleSwitch();
        private readonly SettingsToggleSwitch minifyAnimationSwitch = new SettingsToggleSwitch();

        // 主题设置控件
        private readonly ThemedComboBox themeComboBox = new ThemedComboBox();
        private readonly ThemedButton copyToCustomButton = new ThemedButton();
        private readonly ThemedButton resetCustomButton = new ThemedButton();
        private readonly SettingsToggleSwitch darkModeSwitch = new SettingsToggleSwitch();
        /// <summary>
        /// 兼容 CI 反射检查与旧版命名的暗黑模式开关字段引用。
        /// </summary>
        private readonly SettingsToggleSwitch darkModeCheckBox;
        private readonly ThemePreviewControl previewControl = new ThemePreviewControl();
        private readonly ThemedTextBox fontFamilyTextBox = new ThemedTextBox();
        private readonly ThemedComboBox menuStyleComboBox = new ThemedComboBox();
        private readonly ThemedNumericUpDown cornerRadiusInput = new ThemedNumericUpDown();
        private readonly ThemedNumericUpDown panelOpacityInput = new ThemedNumericUpDown();
        private readonly ThemedNumericUpDown titleOpacityInput = new ThemedNumericUpDown();
        private readonly SettingsToggleSwitch blurSwitch = new SettingsToggleSwitch();
        private readonly SettingsFilePicker imageFilePicker = new SettingsFilePicker();
        private readonly ThemedComboBox imageLayoutComboBox = new ThemedComboBox();
        private readonly ThemedNumericUpDown imageOpacityInput = new ThemedNumericUpDown();

        // 可扩展分类、绑定与卡片集合
        private readonly List<SettingsCategoryDescriptor> categories = new List<SettingsCategoryDescriptor>();
        private readonly List<ISettingsBinding> bindings = new List<ISettingsBinding>();
        private readonly Dictionary<string, List<SettingsGroupCard>> categoryCards =
            new Dictionary<string, List<SettingsGroupCard>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SettingsItemRow> itemRowsByKey =
            new Dictionary<string, SettingsItemRow>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Control> customOnlyControls = new List<Control>();
        private readonly List<ColorEditor> colorEditors = new List<ColorEditor>();
        private readonly List<LanguageChoice> languageChoices = new List<LanguageChoice>();
        private readonly List<ImageLayoutChoice> imageLayoutChoices = new List<ImageLayoutChoice>();

        // 草稿状态与已保存快照
        private AppSettings savedAppSettings;
        private AppSettings draftAppSettings;
        private string savedThemeId;
        private string draftThemeId;
        private bool savedDarkMode;
        private bool draftDarkMode;
        private ThemeDefinition savedCustomLightTheme;
        private ThemeDefinition savedCustomDarkTheme;
        private ThemeDefinition draftCustomLightTheme;
        private ThemeDefinition draftCustomDarkTheme;
        private ThemeColorMode loadedEditorColorMode;

        private SettingsPalette currentPalette;
        private bool isLoadingControls;
        private bool isSaving;
        private string statusMessage;
        private StatusKind statusKind = StatusKind.None;

        private enum StatusKind
        {
            None,
            UnsavedChanges,
            SavedSuccess,
            ValidationError,
            RevertedInfo
        }

        public ThemeConfigurationDialog()
        {
            darkModeCheckBox = darkModeSwitch;
            LoadSnapshotsFromManagers();
            currentPalette = SettingsPalette.FromColorMode(draftDarkMode);

            InitializeWindowChrome();
            InitializeCategoriesAndBindings();
            PopulateStaticChoices();
            BuildCategoryCards();

            RefreshAllControlsFromDraft();
            ApplySettingsPagePalette(draftDarkMode);
            sidebar.SelectedIndex = 0; // 默认选中“基础设置”
            SwitchToCategory(sidebar.SelectedCategory);
            UpdateDirtyStateAndActionBar();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (IsHandleCreated)
            {
                DropShadow.SetShadow(this, true);
                DropShadow.TrySetNativeCorners(this, true);
                WindowUtil.TrySetImmersiveDarkMode(Handle, currentPalette.IsDark);
            }
            LayoutActiveCategoryCards();
        }

        private static string K(string key)
        {
            return ThemeText.Key(key);
        }

        private void LoadSnapshotsFromManagers()
        {
            savedAppSettings = AppSettingsManager.Instance.CurrentSettings;
            draftAppSettings = savedAppSettings.Clone();

            savedThemeId = ThemeManager.Instance.SelectedThemeId;
            draftThemeId = savedThemeId;

            savedDarkMode = ThemeManager.Instance.DarkModeEnabled;
            draftDarkMode = savedDarkMode;

            savedCustomLightTheme = ThemeManager.Instance.CustomLightTheme;
            savedCustomDarkTheme = ThemeManager.Instance.CustomDarkTheme;
            draftCustomLightTheme = savedCustomLightTheme.Clone();
            draftCustomDarkTheme = savedCustomDarkTheme.Clone();
            loadedEditorColorMode = draftDarkMode ? ThemeColorMode.Dark : ThemeColorMode.Light;
        }

        private float GetDpiScale()
        {
            if (IsHandleCreated)
            {
                uint dpi = GetDpiForWindow(Handle);
                if (dpi > 0)
                    return dpi / 96f;
            }
            return DeviceDpi > 0 ? DeviceDpi / 96f : 1f;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            float scale = GetDpiScale();
            MinimumSize = new Size((int)Math.Round(860 * scale), (int)Math.Round(620 * scale));
            ClientSize = new Size((int)Math.Round(980 * scale), (int)Math.Round(690 * scale));
            ApplySettingsPagePalette(draftDarkMode);
            LayoutActiveCategoryCards();
            LayoutBottomActionBar();
        }

        private void InitializeWindowChrome()
        {
            Text = K(LKeys.SettingsWindowTitle);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(860, 620);
            ClientSize = new Size(980, 690);
            DoubleBuffered = true;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(1); // 为无边框窗口预留 1px 外边框

            // 顶部标题栏
            titleBar.TitleText = K(LKeys.SettingsWindowTitle);
            titleBar.MinimizeClicked += (s, e) => WindowState = FormWindowState.Minimized;
            titleBar.MaximizeRestoreClicked += (s, e) =>
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            titleBar.CloseClicked += (s, e) => Close();

            // 左侧分类导航栏
            sidebar.SelectedCategoryChanged += (s, e) =>
            {
                SwitchToCategory(sidebar.SelectedCategory);
            };

            // 右侧主容器
            rightContainer.Dock = DockStyle.Fill;

            // 右侧顶部页面标题与说明区
            pageHeaderPanel.Dock = DockStyle.Top;
            pageHeaderPanel.Height = 78;
            pageHeaderPanel.Paint += PageHeaderPanel_Paint;

            // 右侧底部固定操作栏
            bottomActionBar.Dock = DockStyle.Bottom;
            bottomActionBar.Height = 64;
            bottomActionBar.Paint += BottomActionBar_Paint;
            bottomActionBar.Resize += (s, e) => LayoutBottomActionBar();

            cancelButton.Text = ThemeText.Cancel;
            cancelButton.Click += CancelButton_Click;
            saveButton.Text = ThemeText.Save;
            saveButton.Click += SaveButton_Click;
            bottomActionBar.Controls.Add(cancelButton);
            bottomActionBar.Controls.Add(saveButton);

            // 右侧独立滚动内容区
            scrollViewport.Dock = DockStyle.Fill;
            scrollViewport.AutoScroll = true;
            scrollViewport.Resize += (s, e) => LayoutActiveCategoryCards();

            rightContainer.Controls.Add(scrollViewport);
            rightContainer.Controls.Add(pageHeaderPanel);
            rightContainer.Controls.Add(bottomActionBar);

            Controls.Add(rightContainer);
            Controls.Add(sidebar);
            Controls.Add(titleBar);
        }

        /// <summary>
        /// 定义可扩展的配置分类、分组、配置项元数据、默认值、校验规则与统一数据绑定。
        /// </summary>
        private void InitializeCategoriesAndBindings()
        {
            categories.Clear();
            bindings.Clear();
            customOnlyControls.Clear();
            colorEditors.Clear();

            // =====================================================================
            // 分类一：基础设置 (Basic Settings)
            // =====================================================================
            var basicCategory = new SettingsCategoryDescriptor
            {
                Id = CategoryBasicId,
                Icon = SettingsCategoryIcon.BasicHome,
                Title = () => K(LKeys.CatBasicTitle),
                Description = () => K(LKeys.CatBasicDesc)
            };

            // 分组 1：常规设置
            var generalGroup = new SettingsGroupDescriptor
            {
                Id = "basic.general",
                Icon = SettingsGroupIcon.Gear,
                Title = () => K(LKeys.GroupGeneralTitle)
            };

            generalGroup.Items.Add(CreateToggleItem(
                key: "basic.launchAtStartup",
                categoryId: CategoryBasicId,
                title: () => K(LKeys.ItemLaunchAtStartupTitle),
                description: () => K(LKeys.ItemLaunchAtStartupDesc),
                toggleControl: launchAtStartupSwitch,
                initialValue: savedAppSettings.LaunchAtStartup,
                defaultValue: false,
                getter: () => draftAppSettings.LaunchAtStartup,
                setter: v => draftAppSettings.LaunchAtStartup = v));

            languageComboBox.Width = 210;
            var languageBinding = new SettingsBinding<string>(
                key: "basic.language",
                categoryId: CategoryBasicId,
                initialSavedValue: savedAppSettings.Language,
                defaultValue: AppSettings.DetectSystemLanguage(),
                draftGetter: () => draftAppSettings.Language,
                draftSetter: v => draftAppSettings.Language = AppSettings.NormalizeLanguage(v),
                controlUpdater: SelectLanguageChoiceByCode,
                validator: null,
                comparer: StringComparer.OrdinalIgnoreCase);
            languageComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (isLoadingControls)
                    return;
                var choice = languageComboBox.SelectedItem as LanguageChoice;
                if (choice != null)
                    languageBinding.NotifyControlValueChanged(choice.Code);
            };
            RegisterBinding(languageBinding);
            generalGroup.Items.Add(new SettingsItemDescriptor
            {
                Key = languageBinding.Key,
                CategoryId = CategoryBasicId,
                Title = () => K(LKeys.ItemLanguageTitle),
                Description = () => K(LKeys.ItemLanguageDesc),
                ControlType = SettingsControlType.Dropdown,
                Binding = languageBinding,
                EditorControl = languageComboBox
            });

            generalGroup.Items.Add(CreateToggleItem(
                key: "basic.checkForUpdates",
                categoryId: CategoryBasicId,
                title: () => K(LKeys.ItemCheckUpdatesTitle),
                description: () => K(LKeys.ItemCheckUpdatesDesc),
                toggleControl: checkUpdatesSwitch,
                initialValue: savedAppSettings.CheckForUpdates,
                defaultValue: true,
                getter: () => draftAppSettings.CheckForUpdates,
                setter: v => draftAppSettings.CheckForUpdates = v));

            // 分组 2：存储设置
            var storageGroup = new SettingsGroupDescriptor
            {
                Id = "basic.storage",
                Icon = SettingsGroupIcon.Folder,
                Title = () => K(LKeys.GroupStorageTitle)
            };

            dataPathPicker.Width = 290;
            var dataPathBinding = new SettingsBinding<string>(
                key: "basic.dataStoragePath",
                categoryId: CategoryBasicId,
                initialSavedValue: savedAppSettings.DataStoragePath,
                defaultValue: AppSettingsManager.GetDefaultDataDirectory(),
                draftGetter: () => draftAppSettings.DataStoragePath,
                draftSetter: v => draftAppSettings.DataStoragePath = v ?? string.Empty,
                controlUpdater: v => dataPathPicker.SelectedPath = v,
                validator: path =>
                {
                    string err;
                    return AppSettingsManager.ValidateDataStoragePath(path, out err)
                        ? SettingsValidationResult.Valid()
                        : SettingsValidationResult.Invalid(err);
                },
                comparer: StringComparer.OrdinalIgnoreCase);
            dataPathPicker.SelectedPathChanged += (s, e) =>
            {
                if (!isLoadingControls)
                    dataPathBinding.NotifyControlValueChanged(dataPathPicker.SelectedPath);
            };
            RegisterBinding(dataPathBinding);
            storageGroup.Items.Add(new SettingsItemDescriptor
            {
                Key = dataPathBinding.Key,
                CategoryId = CategoryBasicId,
                Title = () => K(LKeys.ItemDataPathTitle),
                Description = () => K(LKeys.ItemDataPathDesc),
                ControlType = SettingsControlType.DirectoryPicker,
                Binding = dataPathBinding,
                EditorControl = dataPathPicker
            });

            storageGroup.Items.Add(CreateToggleItem(
                key: "basic.autoSave",
                categoryId: CategoryBasicId,
                title: () => K(LKeys.ItemAutoSaveTitle),
                description: () => K(LKeys.ItemAutoSaveDesc),
                toggleControl: autoSaveSwitch,
                initialValue: savedAppSettings.AutoSave,
                defaultValue: true,
                getter: () => draftAppSettings.AutoSave,
                setter: v => draftAppSettings.AutoSave = v));

            // 分组 3：全局配置
            var globalConfigGroup = new SettingsGroupDescriptor
            {
                Id = "basic.global",
                Icon = SettingsGroupIcon.Sliders,
                Title = () => K(LKeys.GroupGlobalConfigTitle)
            };

            globalConfigGroup.Items.Add(CreateToggleItem(
                key: "basic.minifyAnimation",
                categoryId: CategoryBasicId,
                title: () => K(LKeys.ItemMinifyAnimationTitle),
                description: () => K(LKeys.ItemMinifyAnimationDesc),
                toggleControl: minifyAnimationSwitch,
                initialValue: savedAppSettings.MinifyAnimation,
                defaultValue: false,
                getter: () => draftAppSettings.MinifyAnimation,
                setter: v => draftAppSettings.MinifyAnimation = v));

            basicCategory.Groups.Add(generalGroup);
            basicCategory.Groups.Add(storageGroup);
            basicCategory.Groups.Add(globalConfigGroup);
            categories.Add(basicCategory);

            // =====================================================================
            // 分类二：主题设置 (Theme Settings)
            // =====================================================================
            var themeCategory = new SettingsCategoryDescriptor
            {
                Id = CategoryThemeId,
                Icon = SettingsCategoryIcon.ThemePalette,
                Title = () => K(LKeys.CatThemeTitle),
                Description = () => K(LKeys.CatThemeDesc)
            };

            // 分组 1：主题与模式 + 实时预览
            var themeSelectGroup = new SettingsGroupDescriptor
            {
                Id = "theme.selection",
                Icon = SettingsGroupIcon.Palette,
                Title = () => K(LKeys.GroupThemeModeTitle)
            };

            var themeSelectorPanel = BuildThemeSelectorRightPanel();
            var themeIdBinding = new SettingsBinding<string>(
                key: "theme.selectedId",
                categoryId: CategoryThemeId,
                initialSavedValue: savedThemeId,
                defaultValue: ThemeIds.Default,
                draftGetter: () => draftThemeId,
                draftSetter: v =>
                {
                    draftThemeId = string.IsNullOrWhiteSpace(v) ? ThemeIds.Default : v;
                    LoadThemeEditorsFromDraft();
                },
                controlUpdater: SelectThemeChoiceById,
                validator: null,
                comparer: StringComparer.OrdinalIgnoreCase);
            themeComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (isLoadingControls)
                    return;
                var choice = themeComboBox.SelectedItem as ThemeChoice;
                if (choice != null)
                    themeIdBinding.NotifyControlValueChanged(choice.Id);
            };
            RegisterBinding(themeIdBinding);
            themeSelectGroup.Items.Add(new SettingsItemDescriptor
            {
                Key = themeIdBinding.Key,
                CategoryId = CategoryThemeId,
                Title = () => K(LKeys.ItemThemeStyleTitle),
                Description = () => K(LKeys.ItemThemeStyleDesc),
                ControlType = SettingsControlType.Custom,
                Binding = themeIdBinding,
                EditorControl = themeSelectorPanel
            });

            themeSelectGroup.Items.Add(CreateToggleItem(
                key: "theme.darkMode",
                categoryId: CategoryThemeId,
                title: () => K(LKeys.ItemDarkModeTitle),
                description: () => K(LKeys.ItemDarkModeDesc),
                toggleControl: darkModeSwitch,
                initialValue: savedDarkMode,
                defaultValue: false,
                getter: () => draftDarkMode,
                setter: v =>
                {
                    draftDarkMode = v;
                    loadedEditorColorMode = v ? ThemeColorMode.Dark : ThemeColorMode.Light;
                    LoadThemeEditorsFromDraft();
                    ApplySettingsPagePalette(v);
                }));

            previewControl.Height = 168;
            themeSelectGroup.CustomBodyContent = previewControl;
            themeCategory.Groups.Add(themeSelectGroup);

            // 分组 2：外观设置
            var appearanceGroup = new SettingsGroupDescriptor
            {
                Id = "theme.appearance",
                Icon = SettingsGroupIcon.Sliders,
                Title = () => K(LKeys.GroupAppearanceTitle)
            };

            fontFamilyTextBox.Width = 210;
            fontFamilyTextBox.TextChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(fontFamilyTextBox);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.fontFamily",
                title: () => K(LKeys.ItemFontFamilyTitle),
                description: () => K(LKeys.ItemFontFamilyDesc),
                controlType: SettingsControlType.TextInput,
                control: fontFamilyTextBox,
                validator: () =>
                {
                    if (IsCustomSelected() && string.IsNullOrWhiteSpace(fontFamilyTextBox.Text))
                    {
                        return SettingsValidationResult.Invalid(K(LKeys.ErrFontFamilyEmpty));
                    }
                    return SettingsValidationResult.Valid();
                }));

            menuStyleComboBox.Width = 210;
            menuStyleComboBox.SelectedIndexChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(menuStyleComboBox);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.menuStyle",
                title: () => K(LKeys.ItemMenuStyleTitle),
                description: () => K(LKeys.ItemMenuStyleDesc),
                controlType: SettingsControlType.Dropdown,
                control: menuStyleComboBox));

            ConfigureNumeric(cornerRadiusInput, 0, 48, " px");
            cornerRadiusInput.ValueChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(cornerRadiusInput);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.cornerRadius",
                title: () => K(LKeys.ItemCornerRadiusTitle),
                description: () => K(LKeys.ItemCornerRadiusDesc),
                controlType: SettingsControlType.NumberInput,
                control: cornerRadiusInput));

            ConfigureNumeric(panelOpacityInput, 20, 100, "%");
            panelOpacityInput.ValueChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(panelOpacityInput);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.panelOpacity",
                title: () => K(LKeys.ItemPanelOpacityTitle),
                description: () => K(LKeys.ItemPanelOpacityDesc),
                controlType: SettingsControlType.NumberInput,
                control: panelOpacityInput));

            ConfigureNumeric(titleOpacityInput, 20, 100, "%");
            titleOpacityInput.ValueChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(titleOpacityInput);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.titleOpacity",
                title: () => K(LKeys.ItemTitleOpacityTitle),
                description: () => K(LKeys.ItemTitleOpacityDesc),
                controlType: SettingsControlType.NumberInput,
                control: titleOpacityInput));

            blurSwitch.CheckedChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(blurSwitch);
            appearanceGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.enableBlur",
                title: () => K(LKeys.ItemEnableBlurTitle),
                description: () => K(LKeys.ItemEnableBlurDesc),
                controlType: SettingsControlType.ToggleSwitch,
                control: blurSwitch));

            themeCategory.Groups.Add(appearanceGroup);

            // 分组 3：背景图片
            var imageGroup = new SettingsGroupDescriptor
            {
                Id = "theme.background",
                Icon = SettingsGroupIcon.Image,
                Title = () => K(LKeys.GroupBackgroundImageTitle)
            };

            imageFilePicker.Width = 290;
            imageFilePicker.SelectedFileChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(imageFilePicker);
            imageGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.backgroundImagePath",
                title: () => K(LKeys.ItemBgImageFileTitle),
                description: () => K(LKeys.ItemBgImageFileDesc),
                controlType: SettingsControlType.FilePicker,
                control: imageFilePicker,
                validator: () =>
                {
                    string path = imageFilePicker.SelectedFilePath?.Trim();
                    if (IsCustomSelected() && !string.IsNullOrEmpty(path) && !File.Exists(path))
                    {
                        return SettingsValidationResult.Invalid(K(LKeys.ErrBgImageFileMissing));
                    }
                    return SettingsValidationResult.Valid();
                }));

            imageLayoutComboBox.Width = 210;
            imageLayoutComboBox.SelectedIndexChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(imageLayoutComboBox);
            imageGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.backgroundImageLayout",
                title: () => K(LKeys.ItemBgImageLayoutTitle),
                description: () => K(LKeys.ItemBgImageLayoutDesc),
                controlType: SettingsControlType.Dropdown,
                control: imageLayoutComboBox));

            ConfigureNumeric(imageOpacityInput, 0, 100, "%");
            imageOpacityInput.ValueChanged += CustomThemeEditorValueChanged;
            customOnlyControls.Add(imageOpacityInput);
            imageGroup.Items.Add(CreateCustomThemeItem(
                key: "theme.backgroundImageOpacity",
                title: () => K(LKeys.ItemBgImageOpacityTitle),
                description: () => K(LKeys.ItemBgImageOpacityDesc),
                controlType: SettingsControlType.NumberInput,
                control: imageOpacityInput));

            themeCategory.Groups.Add(imageGroup);

            // 分组 4：颜色配置
            var colorGroup = new SettingsGroupDescriptor
            {
                Id = "theme.colors",
                Icon = SettingsGroupIcon.Swatches,
                Title = () => K(LKeys.GroupColorsTitle)
            };

            AddColorItem(colorGroup, "theme.color.mainPanel",
                () => K(LKeys.ColorMainPanelTitle),
                () => K(LKeys.ColorMainPanelDesc),
                t => t.MainPanelColorArgb, (t, v) => t.MainPanelColorArgb = v);
            AddColorItem(colorGroup, "theme.color.titleBar",
                () => K(LKeys.ColorTitleBarTitle),
                () => K(LKeys.ColorTitleBarDesc),
                t => t.TitleBarColorArgb, (t, v) => t.TitleBarColorArgb = v);
            AddColorItem(colorGroup, "theme.color.titleText",
                () => K(LKeys.ColorTitleTextTitle),
                () => K(LKeys.ColorTitleTextDesc),
                t => t.TitleTextColorArgb, (t, v) => t.TitleTextColorArgb = v);
            AddColorItem(colorGroup, "theme.color.itemText",
                () => K(LKeys.ColorItemTextTitle),
                () => K(LKeys.ColorItemTextDesc),
                t => t.ItemTextColorArgb, (t, v) => t.ItemTextColorArgb = v);
            AddColorItem(colorGroup, "theme.color.itemShadow",
                () => K(LKeys.ColorItemShadowTitle),
                () => K(LKeys.ColorItemShadowDesc),
                t => t.ItemTextShadowColorArgb, (t, v) => t.ItemTextShadowColorArgb = v);
            AddColorItem(colorGroup, "theme.color.itemHover",
                () => K(LKeys.ColorItemHoverTitle),
                () => K(LKeys.ColorItemHoverDesc),
                t => t.ItemHoverColorArgb, (t, v) => t.ItemHoverColorArgb = v);
            AddColorItem(colorGroup, "theme.color.itemSelected",
                () => K(LKeys.ColorItemSelectedTitle),
                () => K(LKeys.ColorItemSelectedDesc),
                t => t.ItemSelectedColorArgb, (t, v) => t.ItemSelectedColorArgb = v);
            AddColorItem(colorGroup, "theme.color.border",
                () => K(LKeys.ColorBorderTitle),
                () => K(LKeys.ColorBorderDesc),
                t => t.BorderColorArgb, (t, v) => t.BorderColorArgb = v);
            AddColorItem(colorGroup, "theme.color.scrollBar",
                () => K(LKeys.ColorScrollBarTitle),
                () => K(LKeys.ColorScrollBarDesc),
                t => t.ScrollBarColorArgb, (t, v) => t.ScrollBarColorArgb = v);
            AddColorItem(colorGroup, "theme.color.menuBg",
                () => K(LKeys.ColorMenuBgTitle),
                () => K(LKeys.ColorMenuBgDesc),
                t => t.MenuBackgroundColorArgb, (t, v) => t.MenuBackgroundColorArgb = v);
            AddColorItem(colorGroup, "theme.color.menuText",
                () => K(LKeys.ColorMenuTextTitle),
                () => K(LKeys.ColorMenuTextDesc),
                t => t.MenuTextColorArgb, (t, v) => t.MenuTextColorArgb = v);
            AddColorItem(colorGroup, "theme.color.menuHighlight",
                () => K(LKeys.ColorMenuHoverTitle),
                () => K(LKeys.ColorMenuHoverDesc),
                t => t.MenuHighlightColorArgb, (t, v) => t.MenuHighlightColorArgb = v);
            AddColorItem(colorGroup, "theme.color.menuHighlightText",
                () => K(LKeys.ColorMenuHoverTextTitle),
                () => K(LKeys.ColorMenuHoverTextDesc),
                t => t.MenuHighlightTextColorArgb, (t, v) => t.MenuHighlightTextColorArgb = v);
            AddColorItem(colorGroup, "theme.color.accent",
                () => K(LKeys.ColorAccentTitle),
                () => K(LKeys.ColorAccentDesc),
                t => t.AccentColorArgb, (t, v) => t.AccentColorArgb = v);

            themeCategory.Groups.Add(colorGroup);
            categories.Add(themeCategory);

            sidebar.SetCategories(categories);
        }

        private Panel BuildThemeSelectorRightPanel()
        {
            var panel = new Panel
            {
                Size = new Size(390, 34)
            };

            themeComboBox.Width = 156;
            themeComboBox.Location = new Point(0, 1);

            copyToCustomButton.Text = K(LKeys.BtnCopyToCustom);
            copyToCustomButton.Size = new Size(134, 32);
            copyToCustomButton.Location = new Point(162, 1);
            copyToCustomButton.Click += CopyToCustomButton_Click;

            resetCustomButton.Text = K(LKeys.BtnResetCustom);
            resetCustomButton.Size = new Size(88, 32);
            resetCustomButton.Location = new Point(302, 1);
            resetCustomButton.Click += ResetCustomButton_Click;

            panel.Controls.Add(themeComboBox);
            panel.Controls.Add(copyToCustomButton);
            panel.Controls.Add(resetCustomButton);
            return panel;
        }

        private SettingsItemDescriptor CreateToggleItem(
            string key,
            string categoryId,
            Func<string> title,
            Func<string> description,
            SettingsToggleSwitch toggleControl,
            bool initialValue,
            bool defaultValue,
            Func<bool> getter,
            Action<bool> setter)
        {
            var binding = new SettingsBinding<bool>(
                key: key,
                categoryId: categoryId,
                initialSavedValue: initialValue,
                defaultValue: defaultValue,
                draftGetter: getter,
                draftSetter: setter,
                controlUpdater: v => toggleControl.Checked = v);
            toggleControl.CheckedChanged += (s, e) =>
            {
                if (!isLoadingControls)
                    binding.NotifyControlValueChanged(toggleControl.Checked);
            };
            RegisterBinding(binding);
            return new SettingsItemDescriptor
            {
                Key = key,
                CategoryId = categoryId,
                Title = title,
                Description = description,
                ControlType = SettingsControlType.ToggleSwitch,
                Binding = binding,
                EditorControl = toggleControl
            };
        }

        private SettingsItemDescriptor CreateCustomThemeItem(
            string key,
            Func<string> title,
            Func<string> description,
            SettingsControlType controlType,
            Control control,
            Func<SettingsValidationResult> validator = null)
        {
            var binding = new SettingsBinding<int>(
                key: key,
                categoryId: CategoryThemeId,
                initialSavedValue: 0,
                defaultValue: 0,
                draftGetter: () => HasCustomThemeDiff() ? 1 : 0,
                draftSetter: _ => { },
                controlUpdater: null,
                validator: _ => validator != null ? validator() : SettingsValidationResult.Valid());
            RegisterBinding(binding);
            return new SettingsItemDescriptor
            {
                Key = key,
                CategoryId = CategoryThemeId,
                Title = title,
                Description = description,
                ControlType = controlType,
                Binding = binding,
                EditorControl = control,
                IsCustomThemeOnly = true
            };
        }

        private void AddColorItem(
            SettingsGroupDescriptor group,
            string key,
            Func<string> title,
            Func<string> description,
            Func<ThemeDefinition, int> getter,
            Action<ThemeDefinition, int> setter)
        {
            var editor = new ColorEditor(title, getter, setter);
            editor.Button.Width = 140;
            editor.Button.Height = 32;
            editor.Button.Tag = editor;
            editor.Button.Click += ColorButton_Click;
            colorEditors.Add(editor);
            customOnlyControls.Add(editor.Button);

            group.Items.Add(CreateCustomThemeItem(
                key: key,
                title: title,
                description: description,
                controlType: SettingsControlType.ColorSwatch,
                control: editor.Button));
        }

        private void RegisterBinding(ISettingsBinding binding)
        {
            binding.ValueChanged += (s, e) => OnAnySettingModified();
            bindings.Add(binding);
        }

        private void ConfigureNumeric(ThemedNumericUpDown input, int minimum, int maximum, string suffix)
        {
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Width = 140;
            input.Suffix = suffix;
        }

        private void PopulateStaticChoices()
        {
            isLoadingControls = true;
            try
            {
                // 语言选项：直接从 LocalizationManager 已注册语言包读取，新增语言包即自动展示
                languageChoices.Clear();
                languageComboBox.Items.Clear();
                foreach (var pack in LocalizationManager.GetRegisteredLanguages())
                {
                    var choice = new LanguageChoice(pack.Code, pack.DisplayName);
                    languageChoices.Add(choice);
                    languageComboBox.Items.Add(choice);
                }

                // 主题列表
                RebuildThemeChoices();

                // 菜单风格
                RebuildMenuStyleChoices();

                // 背景图显示方式
                RebuildImageLayoutChoices();
            }
            finally
            {
                isLoadingControls = false;
            }
        }

        private void RebuildThemeChoices()
        {
            themeComboBox.Items.Clear();
            foreach (var provider in ThemeManager.Instance.GetThemeProviders())
                themeComboBox.Items.Add(new ThemeChoice(provider.Id, provider.DisplayName));
            themeComboBox.Items.Add(new ThemeChoice(ThemeIds.Custom, K(LKeys.ThemeCustomOption)));
            SelectThemeChoiceById(draftThemeId);
        }

        private void RebuildMenuStyleChoices()
        {
            var currentStyle = (menuStyleComboBox.SelectedItem as MenuStyleChoice)?.Value ?? ThemeMenuStyle.Standard;
            menuStyleComboBox.Items.Clear();
            menuStyleComboBox.Items.Add(new MenuStyleChoice(
                ThemeMenuStyle.Standard,
                K(LKeys.MenuStyleStandard)));
            menuStyleComboBox.Items.Add(new MenuStyleChoice(
                ThemeMenuStyle.Windows11,
                "Windows 11"));
            menuStyleComboBox.Items.Add(new MenuStyleChoice(
                ThemeMenuStyle.WindowsXp,
                "Windows XP"));
            SelectMenuStyle(currentStyle);
        }

        private void RebuildImageLayoutChoices()
        {
            var currentLayout = (imageLayoutComboBox.SelectedItem as ImageLayoutChoice)?.Value ?? ThemeImageLayout.Fill;
            imageLayoutChoices.Clear();
            imageLayoutComboBox.Items.Clear();
            imageLayoutChoices.Add(new ImageLayoutChoice(ThemeImageLayout.Fill, K(LKeys.ImageLayoutFill)));
            imageLayoutChoices.Add(new ImageLayoutChoice(ThemeImageLayout.Fit, K(LKeys.ImageLayoutFit)));
            imageLayoutChoices.Add(new ImageLayoutChoice(ThemeImageLayout.Stretch, K(LKeys.ImageLayoutStretch)));
            imageLayoutChoices.Add(new ImageLayoutChoice(ThemeImageLayout.Center, K(LKeys.ImageLayoutCenter)));
            imageLayoutChoices.Add(new ImageLayoutChoice(ThemeImageLayout.Tile, K(LKeys.ImageLayoutTile)));
            foreach (var choice in imageLayoutChoices)
                imageLayoutComboBox.Items.Add(choice);
            SelectImageLayout(currentLayout);
        }

        private void BuildCategoryCards()
        {
            categoryCards.Clear();
            itemRowsByKey.Clear();

            foreach (var category in categories)
            {
                var cards = new List<SettingsGroupCard>();
                foreach (var group in category.Groups)
                {
                    var card = new SettingsGroupCard(group);
                    foreach (var row in card.ItemRows)
                    {
                        if (!string.IsNullOrEmpty(row.Descriptor.Key))
                            itemRowsByKey[row.Descriptor.Key] = row;
                    }
                    cards.Add(card);
                }
                categoryCards[category.Id] = cards;
            }
        }

        private void SwitchToCategory(SettingsCategoryDescriptor category)
        {
            if (category == null)
                return;

            scrollViewport.SuspendLayout();
            try
            {
                scrollViewport.Controls.Clear();
                scrollViewport.AutoScrollPosition = new Point(0, 0);

                if (categoryCards.TryGetValue(category.Id, out var cards))
                {
                    foreach (var card in cards)
                    {
                        card.ApplyPalette(currentPalette);
                        scrollViewport.Controls.Add(card);
                    }
                }
            }
            finally
            {
                scrollViewport.ResumeLayout(true);
            }

            pageHeaderPanel.Invalidate();
            LayoutActiveCategoryCards();
        }

        private void LayoutActiveCategoryCards()
        {
            var category = sidebar.SelectedCategory;
            if (category == null || !categoryCards.TryGetValue(category.Id, out var cards))
                return;

            float scale = GetDpiScale();
            int padH = Math.Max(20, (int)Math.Round(28 * scale));
            int gapY = Math.Max(14, (int)Math.Round(18 * scale));
            int scrollY = scrollViewport.AutoScrollPosition.Y;
            int currentY = scrollY + (int)Math.Round(6 * scale);
            int availableWidth = Math.Max(320, scrollViewport.ClientSize.Width - padH * 2);

            scrollViewport.SuspendLayout();
            try
            {
                foreach (var card in cards)
                {
                    card.Width = availableWidth;
                    card.PerformCardLayout();
                    card.Location = new Point(padH, currentY);
                    currentY += card.Height + gapY;
                }
            }
            finally
            {
                scrollViewport.ResumeLayout(true);
            }
        }

        private void LayoutBottomActionBar()
        {
            float scale = GetDpiScale();
            int padRight = Math.Max(20, (int)Math.Round(28 * scale));
            int gap = Math.Max(10, (int)Math.Round(12 * scale));
            int btnWidth = Math.Max(82, (int)Math.Round(88 * scale));
            int btnHeight = Math.Max(34, (int)Math.Round(36 * scale));
            int btnY = (bottomActionBar.Height - btnHeight) / 2;

            saveButton.Size = new Size(btnWidth, btnHeight);
            cancelButton.Size = new Size(btnWidth, btnHeight);

            saveButton.Location = new Point(bottomActionBar.Width - padRight - btnWidth, btnY);
            cancelButton.Location = new Point(saveButton.Left - gap - btnWidth, btnY);
        }

        /// <summary>
        /// 应用设置页面固定浅色/深色调色板。
        /// 浅色模式为白色背景，黑夜模式为深色背景，统一蓝色强调色，不受栅栏主题影响。
        /// </summary>
        private void ApplySettingsPagePalette(bool darkMode)
        {
            currentPalette = SettingsPalette.FromColorMode(darkMode);
            BackColor = currentPalette.WindowBorder;
            rightContainer.BackColor = currentPalette.ContentBackground;
            pageHeaderPanel.BackColor = currentPalette.ContentBackground;
            scrollViewport.BackColor = currentPalette.ContentBackground;
            bottomActionBar.BackColor = currentPalette.ContentBackground;

            float scale = GetDpiScale();
            int sideWidth = Math.Max(195, (int)Math.Round(210 * scale));
            sidebar.Width = sideWidth;
            titleBar.SidebarWidth = sideWidth;
            titleBar.Height = Math.Max(42, (int)Math.Round(46 * scale));
            pageHeaderPanel.Height = Math.Max(70, (int)Math.Round(78 * scale));
            bottomActionBar.Height = Math.Max(58, (int)Math.Round(64 * scale));

            titleBar.ApplyPalette(currentPalette);
            sidebar.ApplyPalette(currentPalette);
            cancelButton.ApplyPalette(currentPalette);
            saveButton.ApplyPalette(currentPalette);

            foreach (var kvp in categoryCards)
            {
                foreach (var card in kvp.Value)
                    card.ApplyPalette(currentPalette);
            }

            // 恢复颜色样本按钮自身所代表的真实主题颜色
            var activeTheme = GetCurrentDraftThemeDefinition();
            foreach (var editor in colorEditors)
                UpdateColorSwatchButton(editor, activeTheme);

            if (IsHandleCreated)
            {
                WindowUtil.TrySetImmersiveDarkMode(Handle, currentPalette.IsDark);
            }

            LayoutBottomActionBar();
            LayoutActiveCategoryCards();
            Invalidate(true);
        }

        private void RefreshAllControlsFromDraft()
        {
            isLoadingControls = true;
            try
            {
                foreach (var binding in bindings)
                    binding.RefreshControlFromDraft();
                LoadThemeEditorsFromDraft();
            }
            finally
            {
                isLoadingControls = false;
            }
        }

        private void LoadThemeEditorsFromDraft()
        {
            ThemeColorMode colorMode = draftDarkMode ? ThemeColorMode.Dark : ThemeColorMode.Light;
            var theme = GetDraftThemeDefinition(colorMode);
            bool isCustom = IsCustomSelected();

            bool prevLoading = isLoadingControls;
            isLoadingControls = true;
            try
            {
                loadedEditorColorMode = colorMode;
                fontFamilyTextBox.Text = theme.FontFamilyName;
                SelectMenuStyle(theme.MenuStyle);
                cornerRadiusInput.Value = theme.CornerRadius;
                panelOpacityInput.Value = theme.MainPanelOpacityPercent;
                titleOpacityInput.Value = theme.TitleBarOpacityPercent;
                blurSwitch.Checked = theme.EnableBlur;
                imageFilePicker.SelectedFilePath = theme.BackgroundImagePath;
                imageOpacityInput.Value = theme.BackgroundImageOpacityPercent;
                SelectImageLayout(theme.BackgroundImageLayout);

                foreach (var editor in colorEditors)
                    UpdateColorSwatchButton(editor, theme);
            }
            finally
            {
                isLoadingControls = prevLoading;
            }

            foreach (var control in customOnlyControls)
                SetControlInteractive(control, isCustom);

            resetCustomButton.IsInteractive = isCustom;
            copyToCustomButton.IsInteractive = !isCustom;
            previewControl.Theme = theme;
        }

        private static void SetControlInteractive(Control control, bool interactive)
        {
            if (control is ThemedTextBox textBox)
                textBox.IsInteractive = interactive;
            else if (control is ThemedNumericUpDown numberBox)
                numberBox.IsInteractive = interactive;
            else if (control is ThemedComboBox comboBox)
                comboBox.IsInteractive = interactive;
            else if (control is ThemedButton button)
                button.IsInteractive = interactive;
            else if (control is SettingsToggleSwitch toggle)
                toggle.IsInteractive = interactive;
            else if (control is SettingsFilePicker filePicker)
                filePicker.IsInteractive = interactive;
            else if (control is SettingsDirectoryPicker dirPicker)
                dirPicker.IsInteractive = interactive;
            else
                control.Enabled = interactive;
        }

        private ThemeDefinition GetCurrentDraftThemeDefinition()
        {
            return GetDraftThemeDefinition(draftDarkMode ? ThemeColorMode.Dark : ThemeColorMode.Light);
        }

        private ThemeDefinition GetDraftThemeDefinition(ThemeColorMode colorMode)
        {
            if (IsCustomSelected())
            {
                return (colorMode == ThemeColorMode.Dark ? draftCustomDarkTheme : draftCustomLightTheme).Clone();
            }
            return ThemeManager.Instance.GetTheme(draftThemeId, colorMode);
        }

        private ThemeDefinition GetEditableCustomTheme(ThemeColorMode colorMode)
        {
            return colorMode == ThemeColorMode.Dark ? draftCustomDarkTheme : draftCustomLightTheme;
        }

        private bool IsCustomSelected()
        {
            return string.Equals(draftThemeId, ThemeIds.Custom, StringComparison.OrdinalIgnoreCase);
        }

        private void WriteControlsToCustomThemeDraft()
        {
            if (isLoadingControls || !IsCustomSelected())
                return;

            var customTheme = GetEditableCustomTheme(loadedEditorColorMode);
            customTheme.FontFamilyName = fontFamilyTextBox.Text.Trim();
            if (menuStyleComboBox.SelectedItem is MenuStyleChoice menuChoice)
                customTheme.MenuStyle = menuChoice.Value;
            customTheme.CornerRadius = (int)cornerRadiusInput.Value;
            customTheme.MainPanelOpacityPercent = (int)panelOpacityInput.Value;
            customTheme.TitleBarOpacityPercent = (int)titleOpacityInput.Value;
            customTheme.EnableBlur = blurSwitch.Checked;
            customTheme.BackgroundImagePath = imageFilePicker.SelectedFilePath.Trim();
            customTheme.BackgroundImageOpacityPercent = (int)imageOpacityInput.Value;
            if (imageLayoutComboBox.SelectedItem is ImageLayoutChoice layoutChoice)
                customTheme.BackgroundImageLayout = layoutChoice.Value;
        }

        private void CustomThemeEditorValueChanged(object sender, EventArgs e)
        {
            if (isLoadingControls || !IsCustomSelected())
                return;

            WriteControlsToCustomThemeDraft();
            var customTheme = GetEditableCustomTheme(loadedEditorColorMode);
            previewControl.Theme = customTheme.Clone();
            OnAnySettingModified();
        }

        private void ColorButton_Click(object sender, EventArgs e)
        {
            if (!IsCustomSelected())
                return;

            var editor = (sender as Control)?.Tag as ColorEditor;
            if (editor == null)
                return;

            var customTheme = GetEditableCustomTheme(loadedEditorColorMode);
            using (var dialog = new ColorDialog
            {
                Color = Color.FromArgb(editor.Getter(customTheme)),
                FullOpen = true,
                AnyColor = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                editor.Setter(customTheme, dialog.Color.ToArgb());
            }

            UpdateColorSwatchButton(editor, customTheme);
            previewControl.Theme = customTheme.Clone();
            OnAnySettingModified();
        }

        private void CopyToCustomButton_Click(object sender, EventArgs e)
        {
            if (IsCustomSelected())
                return;

            draftCustomLightTheme = ThemeManager.Instance.GetTheme(draftThemeId, ThemeColorMode.Light);
            draftCustomDarkTheme = ThemeManager.Instance.GetTheme(draftThemeId, ThemeColorMode.Dark);
            draftCustomLightTheme.Name = "Custom";
            draftCustomDarkTheme.Name = "Custom";
            draftThemeId = ThemeIds.Custom;
            SelectThemeChoiceById(ThemeIds.Custom);
            LoadThemeEditorsFromDraft();
            OnAnySettingModified();
        }

        private void ResetCustomButton_Click(object sender, EventArgs e)
        {
            if (!IsCustomSelected())
                return;

            draftCustomLightTheme = ThemePresets.CreateDefaultCustom(ThemeColorMode.Light);
            draftCustomDarkTheme = ThemePresets.CreateDefaultCustom(ThemeColorMode.Dark);
            LoadThemeEditorsFromDraft();
            OnAnySettingModified();
        }

        private void UpdateColorSwatchButton(ColorEditor editor, ThemeDefinition theme)
        {
            Color color = Color.FromArgb(editor.Getter(theme));
            editor.Button.IsColorSwatch = true;
            editor.Button.UseVisualStyleBackColor = false;
            editor.Button.BackColor = color;
            editor.Button.ForeColor = ThemeDrawing.IsDark(color) ? Color.White : Color.Black;
            editor.Button.Text = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            editor.Button.Invalidate();
        }

        private void SelectLanguageChoiceByCode(string languageCode)
        {
            string normalized = AppSettings.NormalizeLanguage(languageCode);
            for (int i = 0; i < languageComboBox.Items.Count; i++)
            {
                var choice = (LanguageChoice)languageComboBox.Items[i];
                if (string.Equals(choice.Code, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    languageComboBox.SelectedIndex = i;
                    return;
                }
            }
            if (languageComboBox.Items.Count > 0)
                languageComboBox.SelectedIndex = 0;
        }

        private void SelectThemeChoiceById(string themeId)
        {
            for (int i = 0; i < themeComboBox.Items.Count; i++)
            {
                var choice = (ThemeChoice)themeComboBox.Items[i];
                if (string.Equals(choice.Id, themeId, StringComparison.OrdinalIgnoreCase))
                {
                    themeComboBox.SelectedIndex = i;
                    return;
                }
            }
            if (themeComboBox.Items.Count > 0)
                themeComboBox.SelectedIndex = 0;
        }

        private void SelectMenuStyle(ThemeMenuStyle style)
        {
            for (int i = 0; i < menuStyleComboBox.Items.Count; i++)
            {
                var choice = (MenuStyleChoice)menuStyleComboBox.Items[i];
                if (choice.Value == style)
                {
                    menuStyleComboBox.SelectedIndex = i;
                    return;
                }
            }
            if (menuStyleComboBox.Items.Count > 0)
                menuStyleComboBox.SelectedIndex = 0;
        }

        private void SelectImageLayout(ThemeImageLayout layout)
        {
            for (int i = 0; i < imageLayoutComboBox.Items.Count; i++)
            {
                var choice = (ImageLayoutChoice)imageLayoutComboBox.Items[i];
                if (choice.Value == layout)
                {
                    imageLayoutComboBox.SelectedIndex = i;
                    return;
                }
            }
            if (imageLayoutComboBox.Items.Count > 0)
                imageLayoutComboBox.SelectedIndex = 0;
        }

        private void OnAnySettingModified()
        {
            ClearValidationErrors();
            UpdateDirtyStateAndActionBar();
        }

        private bool HasUnsavedChanges()
        {
            if (!draftAppSettings.ContentEquals(savedAppSettings))
                return true;
            if (!string.Equals(draftThemeId, savedThemeId, StringComparison.OrdinalIgnoreCase))
                return true;
            if (draftDarkMode != savedDarkMode)
                return true;
            if (HasCustomThemeDiff())
                return true;
            return false;
        }

        private bool HasCustomThemeDiff()
        {
            return !ThemeDefinitionsEqual(draftCustomLightTheme, savedCustomLightTheme) ||
                   !ThemeDefinitionsEqual(draftCustomDarkTheme, savedCustomDarkTheme);
        }

        private static bool ThemeDefinitionsEqual(ThemeDefinition a, ThemeDefinition b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return string.Equals(a.FontFamilyName ?? string.Empty, b.FontFamilyName ?? string.Empty, StringComparison.Ordinal) &&
                   a.MenuStyle == b.MenuStyle &&
                   a.CornerRadius == b.CornerRadius &&
                   a.MainPanelOpacityPercent == b.MainPanelOpacityPercent &&
                   a.TitleBarOpacityPercent == b.TitleBarOpacityPercent &&
                   a.EnableBlur == b.EnableBlur &&
                   string.Equals(a.BackgroundImagePath ?? string.Empty, b.BackgroundImagePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   a.BackgroundImageLayout == b.BackgroundImageLayout &&
                   a.BackgroundImageOpacityPercent == b.BackgroundImageOpacityPercent &&
                   a.MainPanelColorArgb == b.MainPanelColorArgb &&
                   a.TitleBarColorArgb == b.TitleBarColorArgb &&
                   a.TitleTextColorArgb == b.TitleTextColorArgb &&
                   a.ItemTextColorArgb == b.ItemTextColorArgb &&
                   a.ItemTextShadowColorArgb == b.ItemTextShadowColorArgb &&
                   a.ItemHoverColorArgb == b.ItemHoverColorArgb &&
                   a.ItemSelectedColorArgb == b.ItemSelectedColorArgb &&
                   a.BorderColorArgb == b.BorderColorArgb &&
                   a.ScrollBarColorArgb == b.ScrollBarColorArgb &&
                   a.MenuBackgroundColorArgb == b.MenuBackgroundColorArgb &&
                   a.MenuTextColorArgb == b.MenuTextColorArgb &&
                   a.MenuHighlightColorArgb == b.MenuHighlightColorArgb &&
                   a.MenuHighlightTextColorArgb == b.MenuHighlightTextColorArgb &&
                   a.AccentColorArgb == b.AccentColorArgb;
        }

        private void ClearValidationErrors()
        {
            foreach (var row in itemRowsByKey.Values)
                row.ValidationError = null;
        }

        private void UpdateDirtyStateAndActionBar(StatusKind? explicitStatus = null, string explicitMessage = null)
        {
            bool dirty = HasUnsavedChanges();
            saveButton.Enabled = dirty && !isSaving;
            cancelButton.Enabled = !isSaving;

            if (explicitStatus.HasValue)
            {
                statusKind = explicitStatus.Value;
                statusMessage = explicitMessage;
            }
            else if (dirty)
            {
                statusKind = StatusKind.UnsavedChanges;
                statusMessage = K(LKeys.StatusUnsavedChanges);
            }
            else
            {
                statusKind = StatusKind.None;
                statusMessage = null;
            }

            bottomActionBar.Invalidate();
        }

        /// <summary>
        /// 点击“取消”按钮：若存在未保存修改，则放弃修改并恢复到最近一次保存的配置状态；
        /// 若当前没有任何未保存修改，则关闭设置窗口。
        /// </summary>
        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (isSaving)
                return;

            if (!HasUnsavedChanges())
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            // 恢复所有草稿状态到最近一次保存的快照
            draftAppSettings = savedAppSettings.Clone();
            draftThemeId = savedThemeId;
            draftDarkMode = savedDarkMode;
            draftCustomLightTheme = savedCustomLightTheme.Clone();
            draftCustomDarkTheme = savedCustomDarkTheme.Clone();

            foreach (var binding in bindings)
                binding.RevertToSaved();

            ClearValidationErrors();
            RefreshAllControlsFromDraft();
            ApplySettingsPagePalette(draftDarkMode);
            UpdateDirtyStateAndActionBar(
                StatusKind.RevertedInfo,
                K(LKeys.StatusReverted));
        }

        /// <summary>
        /// 点击“保存”按钮：防重复提交 → 校验所有配置项 → 持久化基础设置与主题设置 → 更新快照并反馈结果。
        /// </summary>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (isSaving || !HasUnsavedChanges())
                return;

            isSaving = true;
            saveButton.Enabled = false;
            cancelButton.Enabled = false;
            try
            {
                ClearValidationErrors();
                WriteControlsToCustomThemeDraft();

                // 1. 逐项执行配置校验
                foreach (var binding in bindings)
                {
                    SettingsValidationResult result = binding.Validate();
                    if (!result.IsValid)
                    {
                        sidebar.SelectCategoryById(binding.CategoryId);
                        if (itemRowsByKey.TryGetValue(binding.Key, out var invalidRow))
                        {
                            invalidRow.ValidationError = result.ErrorMessage;
                            LayoutActiveCategoryCards();
                            scrollViewport.ScrollControlIntoView(invalidRow);
                            invalidRow.Descriptor.EditorControl?.Focus();
                        }

                        UpdateDirtyStateAndActionBar(
                            StatusKind.ValidationError,
                            "⚠ " + result.ErrorMessage);
                        return;
                    }
                }

                // 2. 持久化保存基础设置
                string saveError;
                bool languageChanged = !string.Equals(
                    savedAppSettings.Language,
                    draftAppSettings.Language,
                    StringComparison.OrdinalIgnoreCase);

                if (!AppSettingsManager.Instance.TrySaveAndApply(draftAppSettings, out saveError))
                {
                    sidebar.SelectCategoryById(CategoryBasicId);
                    if (itemRowsByKey.TryGetValue("basic.dataStoragePath", out var pathRow))
                    {
                        pathRow.ValidationError = saveError;
                        LayoutActiveCategoryCards();
                        scrollViewport.ScrollControlIntoView(pathRow);
                    }

                    UpdateDirtyStateAndActionBar(StatusKind.ValidationError, "⚠ " + saveError);
                    return;
                }

                // 3. 规范化并持久化保存主题设置
                draftCustomLightTheme.Normalize();
                draftCustomDarkTheme.Normalize();
                ThemeManager.Instance.ApplySelection(
                    draftThemeId,
                    draftCustomLightTheme,
                    draftCustomDarkTheme,
                    draftDarkMode);

                // 4. 更新已保存快照
                savedAppSettings = AppSettingsManager.Instance.CurrentSettings;
                draftAppSettings = savedAppSettings.Clone();
                savedThemeId = draftThemeId;
                savedDarkMode = draftDarkMode;
                savedCustomLightTheme = draftCustomLightTheme.Clone();
                savedCustomDarkTheme = draftCustomDarkTheme.Clone();

                foreach (var binding in bindings)
                    binding.CommitSnapshot();

                if (languageChanged)
                {
                    RefreshDialogLocalization();
                }

                UpdateDirtyStateAndActionBar(
                    StatusKind.SavedSuccess,
                    K(LKeys.StatusSavedSuccess));
            }
            catch (Exception ex)
            {
                UpdateDirtyStateAndActionBar(
                    StatusKind.ValidationError,
                    "⚠ " + K(LKeys.StatusSaveFailedPrefix) + ex.Message);
            }
            finally
            {
                isSaving = false;
                saveButton.Enabled = HasUnsavedChanges();
                cancelButton.Enabled = true;
            }
        }

        /// <summary>当界面语言切换并保存后，立即刷新设置窗口及所有栅栏右键菜单的语言文案。</summary>
        private void RefreshDialogLocalization()
        {
            isLoadingControls = true;
            try
            {
                Text = K(LKeys.SettingsWindowTitle);
                titleBar.TitleText = K(LKeys.SettingsWindowTitle);
                cancelButton.Text = ThemeText.Cancel;
                saveButton.Text = ThemeText.Save;
                copyToCustomButton.Text = K(LKeys.BtnCopyToCustom);
                resetCustomButton.Text = K(LKeys.BtnResetCustom);

                RebuildThemeChoices();
                RebuildMenuStyleChoices();
                RebuildImageLayoutChoices();
                LoadThemeEditorsFromDraft();
            }
            finally
            {
                isLoadingControls = false;
            }

            sidebar.Invalidate();
            pageHeaderPanel.Invalidate();
            foreach (var kvp in categoryCards)
            {
                foreach (var card in kvp.Value)
                    card.Invalidate(true);
            }
        }

        private void PageHeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(currentPalette.ContentBackground);

            var category = sidebar.SelectedCategory;
            if (category == null)
                return;

            float scale = GetDpiScale();
            int padH = Math.Max(20, (int)Math.Round(28 * scale));
            int titleTop = (int)Math.Round(16 * scale);
            int textWidth = Math.Max(100, pageHeaderPanel.Width - padH * 2);

            using (var titleFont = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold))
            using (var descFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular))
            {
                var titleRect = new Rectangle(padH, titleTop, textWidth, (int)Math.Round(30 * scale));
                TextRenderer.DrawText(
                    g,
                    category.Title != null ? category.Title() : category.Id,
                    titleFont,
                    titleRect,
                    currentPalette.PrimaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                var descRect = new Rectangle(
                    padH,
                    titleRect.Bottom + (int)Math.Round(2 * scale),
                    textWidth,
                    (int)Math.Round(22 * scale));
                TextRenderer.DrawText(
                    g,
                    category.Description != null ? category.Description() : string.Empty,
                    descFont,
                    descRect,
                    currentPalette.SecondaryText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private void BottomActionBar_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(currentPalette.ContentBackground);

            float scale = GetDpiScale();
            int padH = Math.Max(20, (int)Math.Round(28 * scale));

            // 顶部分隔线
            using (var linePen = new Pen(currentPalette.CardBorder, 1f))
            {
                g.DrawLine(linePen, padH, 0, bottomActionBar.Width - padH, 0);
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                Color statusColor;
                switch (statusKind)
                {
                    case StatusKind.ValidationError:
                        statusColor = currentPalette.ErrorColor;
                        break;
                    case StatusKind.SavedSuccess:
                        statusColor = currentPalette.SuccessColor;
                        break;
                    case StatusKind.UnsavedChanges:
                        statusColor = currentPalette.WarningColor;
                        break;
                    default:
                        statusColor = currentPalette.SecondaryText;
                        break;
                }

                int maxTextRight = Math.Max(padH + 40, cancelButton.Left - 16);
                var statusBounds = new Rectangle(padH, 1, maxTextRight - padH, bottomActionBar.Height - 1);
                using (var statusFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular))
                {
                    TextRenderer.DrawText(
                        g,
                        statusMessage,
                        statusFont,
                        statusBounds,
                        statusColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                base.WndProc(ref m);
                RECT r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    r.Left,
                    r.Top,
                    r.Right - r.Left,
                    r.Bottom - r.Top,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                float scale = GetDpiScale();
                MinimumSize = new Size((int)Math.Round(860 * scale), (int)Math.Round(620 * scale));
                ApplySettingsPagePalette(draftDarkMode);
                LayoutActiveCategoryCards();
                LayoutBottomActionBar();
                Invalidate(true);
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                Point pt = PointToClient(new Point(m.LParam.ToInt32()));
                const int grip = 8;

                if (pt.X < grip && pt.Y < grip)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > Width - grip && pt.Y < grip)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < grip && pt.Y > Height - grip)
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > Width - grip && pt.Y > Height - grip)
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y < grip)
                    m.Result = new IntPtr(HTTOP);
                else if (pt.Y > Height - grip)
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < grip)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > Width - grip)
                    m.Result = new IntPtr(HTRIGHT);
            }
        }

        private sealed class LanguageChoice
        {
            public LanguageChoice(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public string Code { get; }
            public string DisplayName { get; }
            public override string ToString() => DisplayName;
        }

        private sealed class ThemeChoice
        {
            public ThemeChoice(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public override string ToString() => DisplayName;
        }

        private sealed class ImageLayoutChoice
        {
            public ImageLayoutChoice(ThemeImageLayout value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public ThemeImageLayout Value { get; }
            public string DisplayName { get; }
            public override string ToString() => DisplayName;
        }

        private sealed class MenuStyleChoice
        {
            public MenuStyleChoice(ThemeMenuStyle value, string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public ThemeMenuStyle Value { get; }
            public string DisplayName { get; }
            public override string ToString() => DisplayName;
        }

        private sealed class ColorEditor
        {
            public ColorEditor(
                Func<string> displayName,
                Func<ThemeDefinition, int> getter,
                Action<ThemeDefinition, int> setter)
            {
                DisplayName = displayName;
                Getter = getter;
                Setter = setter;
                Button = new ThemedButton { IsColorSwatch = true };
            }

            public Func<string> DisplayName { get; }
            public Func<ThemeDefinition, int> Getter { get; }
            public Action<ThemeDefinition, int> Setter { get; }
            public ThemedButton Button { get; }
        }
    }

    /// <summary>
    /// 桌面栅栏与右键菜单实时主题预览控件。
    /// 使用与真实 FenceWindow 相同的绘制工具，直观反馈当前所选主题与自定义参数效果。
    /// </summary>
    internal sealed class ThemePreviewControl : Control
    {
        private ThemeDefinition theme = ThemePresets.CreateDefault();
        private Image backgroundImage;
        private string backgroundImagePath;
        private long backgroundImageVersion;

        public ThemePreviewControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            MinimumSize = new Size(420, 150);
        }

        public ThemeDefinition Theme
        {
            get => theme.Clone();
            set
            {
                theme = value != null ? value.Clone() : ThemePresets.CreateDefault();
                string path = theme.BackgroundImagePath;
                long version = ThemeDrawing.GetImageFileVersion(path);
                if (!string.Equals(path, backgroundImagePath, StringComparison.OrdinalIgnoreCase) ||
                    version != backgroundImageVersion)
                {
                    backgroundImage?.Dispose();
                    backgroundImage = ThemeDrawing.LoadImageWithoutLock(path);
                    backgroundImagePath = path;
                    backgroundImageVersion = version;
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(27, 33, 44));

            var canvasRect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (var canvasPath = ThemeDrawing.CreateRoundedRectangle(canvasRect, 8))
            using (var canvasBrush = new LinearGradientBrush(
                canvasRect,
                Color.FromArgb(44, 78, 126),
                Color.FromArgb(24, 42, 74),
                LinearGradientMode.ForwardDiagonal))
            using (var canvasBorder = new Pen(ThemeDrawing.WithAlpha(theme.BorderColor, 90), 1f))
            {
                e.Graphics.FillPath(canvasBrush, canvasPath);
                e.Graphics.DrawPath(canvasBorder, canvasPath);
            }

            int menuWidth = Math.Min(190, Width / 3);
            var fenceBounds = new Rectangle(12, 12, Math.Max(220, Width - menuWidth - 34), Height - 24);
            var menuBounds = new Rectangle(fenceBounds.Right + 10, 20, menuWidth, Math.Min(132, Height - 28));
            DrawFence(e.Graphics, fenceBounds);
            DrawMenu(e.Graphics, menuBounds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                backgroundImage?.Dispose();
                backgroundImage = null;
            }
            base.Dispose(disposing);
        }

        private void DrawFence(Graphics graphics, Rectangle bounds)
        {
            using (var path = ThemeDrawing.CreateRoundedRectangle(bounds, theme.CornerRadius))
            {
                using (var shadow = new SolidBrush(Color.FromArgb(45, Color.Black)))
                using (var shadowPath = ThemeDrawing.CreateRoundedRectangle(
                    new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width, bounds.Height), theme.CornerRadius))
                    graphics.FillPath(shadow, shadowPath);

                var state = graphics.Save();
                graphics.SetClip(path);
                using (var background = new SolidBrush(ThemeDrawing.WithOpacity(
                    theme.MainPanelColor, theme.MainPanelOpacityPercent)))
                    graphics.FillRectangle(background, bounds);
                ThemeDrawing.DrawBackgroundImage(graphics, backgroundImage, bounds,
                    theme.BackgroundImageLayout, theme.BackgroundImageOpacityPercent);

                var titleBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, 34);
                using (var titleBrush = new SolidBrush(ThemeDrawing.WithOpacity(
                    theme.TitleBarColor, theme.TitleBarOpacityPercent)))
                    graphics.FillRectangle(titleBrush, titleBounds);
                using (var titleFont = CreateThemeFont(11f, FontStyle.Bold))
                using (var titleText = new SolidBrush(theme.TitleTextColor))
                    graphics.DrawString(ThemeText.Key(LKeys.PreviewWorkspace), titleFont, titleText,
                        new RectangleF(titleBounds.X + 12, titleBounds.Y + 7, titleBounds.Width - 24, 22));

                DrawPreviewItem(graphics, bounds.X + 24, bounds.Y + 54, false, ThemeText.Key(LKeys.PreviewDocs));
                DrawPreviewItem(graphics, bounds.X + 116, bounds.Y + 54, true, ThemeText.Key(LKeys.PreviewImages));
                DrawPreviewItem(graphics, bounds.X + 208, bounds.Y + 54, false, ThemeText.Key(LKeys.PreviewProject));
                graphics.Restore(state);

                using (var borderPen = new Pen(ThemeDrawing.WithAlpha(theme.BorderColor, 150)))
                    graphics.DrawPath(borderPen, path);
            }
        }

        private void DrawPreviewItem(Graphics graphics, int x, int y, bool selected, string text)
        {
            var itemBounds = new Rectangle(x - 8, y - 5, 78, 70);
            bool xpStyle = theme.MenuStyle == ThemeMenuStyle.WindowsXp;
            if (selected && !xpStyle)
            {
                bool highContrast = SystemInformation.HighContrast;
                bool darkSurface = ThemeDrawing.IsDark(theme.MainPanelColor);
                Color fillColor = highContrast
                    ? SystemColors.Highlight
                    : darkSurface
                        ? Color.FromArgb(130, 175, 215)
                        : Color.FromArgb(45, 95, 145);
                Color strokeColor = highContrast
                    ? SystemColors.HighlightText
                    : darkSurface
                        ? Color.FromArgb(220, 238, 252)
                        : Color.FromArgb(45, 95, 145);
                using (var path = ThemeDrawing.CreateRoundedRectangle(itemBounds, 1f))
                using (var selectedBrush = new SolidBrush(ThemeDrawing.WithAlpha(
                    fillColor,
                    highContrast ? 255 : 46)))
                using (var borderPen = new Pen(ThemeDrawing.WithAlpha(
                    strokeColor,
                    highContrast ? 255 : darkSurface ? 122 : 117)))
                {
                    graphics.FillPath(selectedBrush, path);
                    graphics.DrawPath(borderPen, path);
                }
            }

            Color folderColor = selected && xpStyle
                ? ThemeDrawing.Mix(theme.AccentColor, SystemColors.Highlight, 0.5f)
                : theme.AccentColor;
            using (var folderBrush = new SolidBrush(folderColor))
                graphics.FillRectangle(folderBrush, x + 10, y, 34, 28);
            using (var font = CreatePreviewIconFont(8.5f))
            using (var format = new StringFormat { Alignment = StringAlignment.Center })
            {
                var textBounds = new RectangleF(x - 5, y + 36, 65, 22);
                if (selected && xpStyle)
                {
                    SizeF textSize = graphics.MeasureString(text, font, textBounds.Size, format);
                    float labelWidth = Math.Min(textBounds.Width, (float)Math.Ceiling(textSize.Width) + 2f);
                    using (var labelBackground = new SolidBrush(SystemColors.Highlight))
                    using (var labelText = new SolidBrush(SystemColors.HighlightText))
                    {
                        graphics.FillRectangle(
                            labelBackground,
                            textBounds.X + (textBounds.Width - labelWidth) / 2f,
                            textBounds.Y,
                            labelWidth,
                            (float)Math.Ceiling(font.GetHeight(graphics)));
                        graphics.DrawString(text, font, labelText, textBounds, format);
                    }
                }
                else
                {
                    Color textColor = selected && SystemInformation.HighContrast
                        ? SystemColors.HighlightText
                        : theme.ItemTextColor;
                    if (!selected || !SystemInformation.HighContrast)
                        DrawPreviewTextShadow(graphics, text, font, textBounds, format);
                    using (var foreground = new SolidBrush(textColor))
                        graphics.DrawString(text, font, foreground, textBounds, format);
                }
            }
        }

        private Font CreatePreviewIconFont(float size)
        {
            return CreateThemeFont(size, FontStyle.Regular);
        }

        private void DrawPreviewTextShadow(
            Graphics graphics,
            string text,
            Font font,
            RectangleF bounds,
            StringFormat format)
        {
            bool isLightText = !ThemeDrawing.IsDark(theme.ItemTextColor);
            int alpha = isLightText ? 140 : 68;
            using (var shadowBrush = new SolidBrush(
                ThemeDrawing.WithAlpha(theme.ItemTextShadowColor, alpha)))
            {
                graphics.DrawString(
                    text,
                    font,
                    shadowBrush,
                    new RectangleF(
                        bounds.X + 1f,
                        bounds.Y + 1f,
                        bounds.Width,
                        bounds.Height),
                    format);
            }
        }

        private void DrawMenu(Graphics graphics, Rectangle bounds)
        {
            MenuThemeProfile profile = MenuThemeProfile.Create(theme);
            int preferredHeight = profile.MenuPadding.Vertical +
                profile.ItemHeight * 3 +
                profile.SeparatorHeight;
            bounds.Height = Math.Min(bounds.Height, preferredHeight);

            using (var shadowPath = ThemeDrawing.CreateRoundedRectangle(
                new Rectangle(bounds.X + 2, bounds.Y + 3, bounds.Width, bounds.Height),
                profile.ContainerCornerRadius))
            using (var shadow = new SolidBrush(Color.FromArgb(
                profile.Style == ThemeMenuStyle.Windows11 ? 28 : 18,
                Color.Black)))
                graphics.FillPath(shadow, shadowPath);

            using (var surfacePath = ThemeDrawing.CreateRoundedRectangle(
                bounds,
                profile.ContainerCornerRadius))
            using (var background = new SolidBrush(theme.MenuBackgroundColor))
                graphics.FillPath(background, surfacePath);

            DrawPreviewMenuBorder(graphics, bounds, profile);

            string[] items =
            {
                ThemeText.Key(LKeys.MenuLock),
                ThemeText.SettingsMenu,
                ThemeText.Key(LKeys.MenuNewFence)
            };
            ThemedMenuIcon[] icons =
            {
                ThemedMenuIcon.Lock,
                ThemedMenuIcon.Settings,
                ThemedMenuIcon.NewFence
            };
            using (var font = CreateMenuFont(profile))
            {
                int x = bounds.X + profile.MenuPadding.Left;
                int width = bounds.Width - profile.MenuPadding.Horizontal;
                int y = bounds.Y + profile.MenuPadding.Top;
                for (int i = 0; i < items.Length; i++)
                {
                    if (i == 2)
                    {
                        DrawPreviewSeparator(graphics, bounds, y, profile);
                        y += profile.SeparatorHeight;
                    }

                    var itemBounds = new Rectangle(x, y, width, profile.ItemHeight);
                    bool highlighted = i == 1;
                    if (highlighted)
                    {
                        var highlightBounds = itemBounds;
                        highlightBounds.Inflate(
                            -profile.ItemHorizontalInset,
                            -profile.ItemVerticalInset);
                        using (var highlight = new SolidBrush(theme.MenuHighlightColor))
                        using (var highlightPath = ThemeDrawing.CreateRoundedRectangle(
                            highlightBounds,
                            profile.ItemCornerRadius))
                            graphics.FillPath(highlight, highlightPath);
                    }

                    Color textColor = highlighted ? theme.MenuHighlightTextColor : theme.MenuTextColor;
                    using (Image icon = ThemedMenuIconFactory.Create(icons[i], theme))
                        graphics.DrawImageUnscaled(
                            icon,
                            itemBounds.X + 8,
                            itemBounds.Y + (itemBounds.Height - icon.Height) / 2);
                    var textBounds = new Rectangle(
                        itemBounds.X + 28 + profile.ItemPadding.Left,
                        itemBounds.Y,
                        Math.Max(1, itemBounds.Width - 48 - profile.ItemPadding.Horizontal),
                        itemBounds.Height);
                    textColor = Color.FromArgb(255, textColor);
                    int textHeight = font.Height;
                    int yOffset = Math.Max(0, (textBounds.Height - textHeight) / 2);
                    var snappedBounds = new Rectangle(textBounds.X, textBounds.Y + yOffset, textBounds.Width, textHeight);

                    using (var brush = new SolidBrush(textColor))
                    using (var sf = new StringFormat(StringFormat.GenericTypographic))
                    {
                        sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
                        sf.Trimming = StringTrimming.EllipsisCharacter;
                        sf.LineAlignment = StringAlignment.Center;
                        sf.Alignment = StringAlignment.Near;
                        graphics.DrawString(items[i], font, brush, snappedBounds, sf);
                    }

                    if (i == 1)
                    {
                        DrawPreviewArrow(
                            graphics,
                            new Point(itemBounds.Right - 13, itemBounds.Top + itemBounds.Height / 2),
                            textColor,
                            profile.DrawChevronArrow);
                    }
                    y += profile.ItemHeight;
                }
            }
        }

        private static void DrawPreviewMenuBorder(
            Graphics graphics,
            Rectangle bounds,
            MenuThemeProfile profile)
        {
            if (profile.DrawClassicThreeDimensionalBorder)
            {
                using (var outer = new Pen(profile.BorderOuterColor))
                    graphics.DrawRectangle(outer, bounds);
                using (var highlight = new Pen(profile.BorderHighlightColor))
                using (var shadow = new Pen(profile.BorderShadowColor))
                {
                    graphics.DrawLine(highlight, bounds.Left + 1, bounds.Top + 1,
                        bounds.Right - 2, bounds.Top + 1);
                    graphics.DrawLine(highlight, bounds.Left + 1, bounds.Top + 1,
                        bounds.Left + 1, bounds.Bottom - 2);
                    graphics.DrawLine(shadow, bounds.Left + 1, bounds.Bottom - 2,
                        bounds.Right - 2, bounds.Bottom - 2);
                    graphics.DrawLine(shadow, bounds.Right - 2, bounds.Top + 1,
                        bounds.Right - 2, bounds.Bottom - 2);
                }
                return;
            }

            using (var borderPath = ThemeDrawing.CreateRoundedRectangle(
                new RectangleF(
                    bounds.X + 0.5f,
                    bounds.Y + 0.5f,
                    bounds.Width - 1f,
                    bounds.Height - 1f),
                profile.ContainerCornerRadius))
            using (var border = new Pen(profile.BorderOuterColor))
                graphics.DrawPath(border, borderPath);
        }

        private static void DrawPreviewSeparator(
            Graphics graphics,
            Rectangle bounds,
            int y,
            MenuThemeProfile profile)
        {
            int lineY = y + profile.SeparatorHeight / 2;
            int left = bounds.Left + profile.SeparatorInset;
            int right = bounds.Right - profile.SeparatorInset;
            using (var primary = new Pen(profile.SeparatorPrimaryColor))
                graphics.DrawLine(primary, left, lineY, right, lineY);
            if (profile.SeparatorSecondaryColor.A > 0)
            {
                using (var secondary = new Pen(profile.SeparatorSecondaryColor))
                    graphics.DrawLine(secondary, left, lineY + 1, right, lineY + 1);
            }
        }

        private static void DrawPreviewArrow(
            Graphics graphics,
            Point center,
            Color color,
            bool chevron)
        {
            Point[] points =
            {
                new Point(center.X - 2, center.Y - 4),
                new Point(center.X + 2, center.Y),
                new Point(center.X - 2, center.Y + 4)
            };
            if (!chevron)
            {
                using (var brush = new SolidBrush(color))
                    graphics.FillPolygon(brush, points);
                return;
            }

            using (var pen = new Pen(color, 1.35f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLines(pen, points);
            }
        }

        private Font CreateMenuFont(MenuThemeProfile profile)
        {
            try
            {
                return new Font(profile.FontFamilyName, profile.FontSize, profile.FontStyle);
            }
            catch
            {
                return CreateThemeFont(profile.FontSize, profile.FontStyle);
            }
        }

        private Font CreateThemeFont(float size, FontStyle style)
        {
            try
            {
                return new Font(theme.FontFamilyName, size, style);
            }
            catch
            {
                return new Font("Segoe UI", size, style);
            }
        }
    }
}
