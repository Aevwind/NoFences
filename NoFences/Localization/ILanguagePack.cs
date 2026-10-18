using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NoFences.Localization
{
    /// <summary>
    /// 语言包接口。新增一门语言只需在独立文件中实现此接口并注册到 <see cref="LocalizationManager"/>。
    /// </summary>
    public interface ILanguagePack
    {
        /// <summary>标准语言标识（如 zh-CN、zh-TW、en-US）。</summary>
        string Code { get; }

        /// <summary>在设置页面“语言”下拉框中展示的语言原生名称（如“简体中文”、“繁體中文”、“English”）。</summary>
        string DisplayName { get; }

        /// <summary>当前语言的键值词条映射表。</summary>
        IReadOnlyDictionary<string, string> Strings { get; }
    }

    /// <summary>
    /// 全局本地化词条键常量表（LKeys）。
    /// 统一管理所有界面、菜单、弹窗与设置项的词条 Key，避免硬编码魔法字符串。
    /// </summary>
    public static class LKeys
    {
        // =====================================================================
        // 1. 通用按钮与基础操作
        // =====================================================================
        public const string Ok = "common.ok";
        public const string Confirm = "common.confirm";
        public const string Cancel = "common.cancel";
        public const string Save = "common.save";
        public const string Reset = "common.reset";

        // =====================================================================
        // 2. 栅栏右键菜单与窗口操作
        // =====================================================================
        public const string MenuDeleteItem = "menu.deleteItem";
        public const string MenuLock = "menu.lock";
        public const string MenuMinify = "menu.minify";
        public const string MenuRename = "menu.rename";
        public const string MenuTitleSize = "menu.titleSize";
        public const string MenuScale = "menu.scale";
        public const string MenuIconManagement = "menu.iconManagement";
        public const string MenuSettings = "menu.settings";
        public const string MenuTheme = "menu.theme";
        public const string MenuDarkMode = "menu.darkMode";
        public const string MenuAutoSnap = "menu.autoSnap";
        public const string MenuNewFence = "menu.newFence";
        public const string MenuRemoveFence = "menu.removeFence";
        public const string MenuQuitApp = "menu.quitApp";

        // =====================================================================
        // 3. 栅栏默认名称、重命名与标题设置弹窗
        // =====================================================================
        public const string DefaultTheme = "fence.defaultTheme";
        public const string DefaultFenceName = "fence.defaultFenceName";
        public const string NewFenceName = "fence.newFenceName";
        public const string EditDialogTitle = "dialog.edit.title";
        public const string EditDialogNameLabel = "dialog.edit.nameLabel";
        public const string HeightDialogTitle = "dialog.height.title";
        public const string HeightDialogRestoreDefault = "dialog.height.restoreDefault";
        public const string TitleSettingsPositionTitle = "dialog.titleSettings.position.title";
        public const string TitleSettingsPositionDesc = "dialog.titleSettings.position.desc";
        public const string TitleSettingsPosBottom = "dialog.titleSettings.position.bottom";
        public const string TitleSettingsPosTop = "dialog.titleSettings.position.top";
        public const string TitleSettingsPosLeft = "dialog.titleSettings.position.left";
        public const string TitleSettingsPosRight = "dialog.titleSettings.position.right";
        public const string TitleSettingsAlignTitle = "dialog.titleSettings.align.title";
        public const string TitleSettingsAlignDesc = "dialog.titleSettings.align.desc";
        public const string TitleSettingsAlignLeft = "dialog.titleSettings.align.left";
        public const string TitleSettingsAlignCenter = "dialog.titleSettings.align.center";
        public const string TitleSettingsAlignRight = "dialog.titleSettings.align.right";
        public const string TitleSettingsSizeTitle = "dialog.titleSettings.size.title";
        public const string TitleSettingsSizeDesc = "dialog.titleSettings.size.desc";
        public const string TitleSettingsPaddingTitle = "dialog.titleSettings.padding.title";
        public const string TitleSettingsPaddingDesc = "dialog.titleSettings.padding.desc";
        public const string TitleSettingsPaddingXTitle = "dialog.titleSettings.paddingX.title";
        public const string TitleSettingsPaddingXDesc = "dialog.titleSettings.paddingX.desc";
        public const string TitleSettingsPaddingYTitle = "dialog.titleSettings.paddingY.title";
        public const string TitleSettingsPaddingYDesc = "dialog.titleSettings.paddingY.desc";
        public const string TitleSettingsLineHeightTitle = "dialog.titleSettings.lineHeight.title";
        public const string TitleSettingsLineHeightDesc = "dialog.titleSettings.lineHeight.desc";

        // =====================================================================
        // 4. 图标多选管理与消息提示框
        // =====================================================================
        public const string IconManagementHint = "fence.iconManagement.hint";
        public const string IconManagementSelectedCountFormat = "fence.iconManagement.selectedCountFormat";
        public const string RemoveItemsTitle = "msg.removeItems.title";
        public const string RemoveItemsFailed = "msg.removeItems.failed";
        public const string NativeMenuTitle = "msg.nativeMenu.title";
        public const string NativeMenuFailed = "msg.nativeMenu.failed";
        public const string NativeMenuSyncFailed = "msg.nativeMenu.syncFailed";
        public const string PersistenceTitle = "msg.persistence.title";
        public const string PersistenceFailed = "msg.persistence.failed";
        public const string RecoveryTitle = "msg.recovery.title";
        public const string RecoveryRunning = "msg.recovery.running";
        public const string RecoveryCompleted = "msg.recovery.completed";
        public const string RecoveryFailed = "msg.recovery.failed";
        public const string StartupTitle = "msg.startup.title";
        public const string StartupWarning = "msg.startup.warning";
        public const string RemoveFenceTitle = "msg.removeFence.title";
        public const string RemoveFenceQuestion = "msg.removeFence.question";
        public const string RemoveFenceFailed = "msg.removeFence.failed";
        public const string ExitTitle = "msg.exit.title";
        public const string ExitFailed = "msg.exit.failed";
        public const string RemoveItemTitle = "msg.removeItem.title";
        public const string RemoveItemFailed = "msg.removeItem.failed";
        public const string AddItemTitle = "msg.addItem.title";
        public const string AddItemsFailed = "msg.addItems.failed";

        // =====================================================================
        // 5. 设置页面框架与底部状态栏
        // =====================================================================
        public const string SettingsWindowTitle = "settings.window.title";
        public const string StatusUnsavedChanges = "settings.status.unsavedChanges";
        public const string StatusReverted = "settings.status.reverted";
        public const string StatusSavedSuccess = "settings.status.savedSuccess";
        public const string StatusSaveFailedPrefix = "settings.status.saveFailedPrefix";

        // =====================================================================
        // 6. 设置页面 - 基础设置 (Basic Settings)
        // =====================================================================
        public const string CatBasicTitle = "settings.basic.title";
        public const string CatBasicDesc = "settings.basic.desc";

        public const string GroupGeneralTitle = "settings.basic.general.title";
        public const string ItemLaunchAtStartupTitle = "settings.basic.launchAtStartup.title";
        public const string ItemLaunchAtStartupDesc = "settings.basic.launchAtStartup.desc";
        public const string ItemLanguageTitle = "settings.basic.language.title";
        public const string ItemLanguageDesc = "settings.basic.language.desc";
        public const string ItemCheckUpdatesTitle = "settings.basic.checkUpdates.title";
        public const string ItemCheckUpdatesDesc = "settings.basic.checkUpdates.desc";

        public const string GroupStorageTitle = "settings.basic.storage.title";
        public const string ItemDataPathTitle = "settings.basic.dataPath.title";
        public const string ItemDataPathDesc = "settings.basic.dataPath.desc";
        public const string DialogSelectDataDir = "settings.basic.dataPath.dialogTitle";
        public const string ItemAutoSaveTitle = "settings.basic.autoSave.title";
        public const string ItemAutoSaveDesc = "settings.basic.autoSave.desc";

        public const string GroupGlobalConfigTitle = "settings.basic.global.title";
        public const string ItemMinifyAnimationTitle = "settings.basic.minifyAnimation.title";
        public const string ItemMinifyAnimationDesc = "settings.basic.minifyAnimation.desc";

        // 基础设置校验与错误提示
        public const string ErrDataPathEmpty = "settings.basic.error.dataPathEmpty";
        public const string ErrDataPathInvalidChars = "settings.basic.error.dataPathInvalidChars";
        public const string ErrDataPathNotAbsolute = "settings.basic.error.dataPathNotAbsolute";
        public const string ErrDataPathDriveMissing = "settings.basic.error.dataPathDriveMissing";
        public const string ErrDataPathFormatPrefix = "settings.basic.error.dataPathFormatPrefix";
        public const string ErrDataDirNotWritablePrefix = "settings.basic.error.dataDirNotWritablePrefix";
        public const string ErrStartupRegistryPrefix = "settings.basic.error.startupRegistryPrefix";
        public const string ErrSaveConfigPrefix = "settings.basic.error.saveConfigPrefix";

        // =====================================================================
        // 7. 设置页面 - 主题设置 (Theme Settings)
        // =====================================================================
        public const string CatThemeTitle = "settings.theme.title";
        public const string CatThemeDesc = "settings.theme.desc";

        public const string GroupThemeModeTitle = "settings.theme.mode.title";
        public const string ItemThemeStyleTitle = "settings.theme.style.title";
        public const string ItemThemeStyleDesc = "settings.theme.style.desc";
        public const string ThemeCustomOption = "settings.theme.customOption";
        public const string BtnCopyToCustom = "settings.theme.copyToCustom";
        public const string BtnResetCustom = "settings.theme.resetCustom";
        public const string ItemDarkModeTitle = "settings.theme.darkMode.title";
        public const string ItemDarkModeDesc = "settings.theme.darkMode.desc";

        public const string GroupAppearanceTitle = "settings.theme.appearance.title";
        public const string ItemFontFamilyTitle = "settings.theme.fontFamily.title";
        public const string ItemFontFamilyDesc = "settings.theme.fontFamily.desc";
        public const string ErrFontFamilyEmpty = "settings.theme.error.fontFamilyEmpty";
        public const string ItemMenuStyleTitle = "settings.theme.menuStyle.title";
        public const string ItemMenuStyleDesc = "settings.theme.menuStyle.desc";
        public const string MenuStyleStandard = "settings.theme.menuStyle.standard";
        public const string ItemCornerRadiusTitle = "settings.theme.cornerRadius.title";
        public const string ItemCornerRadiusDesc = "settings.theme.cornerRadius.desc";
        public const string ItemPanelOpacityTitle = "settings.theme.panelOpacity.title";
        public const string ItemPanelOpacityDesc = "settings.theme.panelOpacity.desc";
        public const string ItemTitleOpacityTitle = "settings.theme.titleOpacity.title";
        public const string ItemTitleOpacityDesc = "settings.theme.titleOpacity.desc";
        public const string ItemEnableBlurTitle = "settings.theme.enableBlur.title";
        public const string ItemEnableBlurDesc = "settings.theme.enableBlur.desc";

        public const string GroupBackgroundImageTitle = "settings.theme.bgImage.title";
        public const string ItemBgImageFileTitle = "settings.theme.bgImageFile.title";
        public const string ItemBgImageFileDesc = "settings.theme.bgImageFile.desc";
        public const string DialogSelectBgImageTitle = "settings.theme.bgImageFile.dialogTitle";
        public const string DialogSelectBgImageFilter = "settings.theme.bgImageFile.dialogFilter";
        public const string ErrBgImageFileMissing = "settings.theme.error.bgImageFileMissing";
        public const string ItemBgImageLayoutTitle = "settings.theme.bgImageLayout.title";
        public const string ItemBgImageLayoutDesc = "settings.theme.bgImageLayout.desc";
        public const string ImageLayoutFill = "settings.theme.imageLayout.fill";
        public const string ImageLayoutFit = "settings.theme.imageLayout.fit";
        public const string ImageLayoutStretch = "settings.theme.imageLayout.stretch";
        public const string ImageLayoutCenter = "settings.theme.imageLayout.center";
        public const string ImageLayoutTile = "settings.theme.imageLayout.tile";
        public const string ItemBgImageOpacityTitle = "settings.theme.bgImageOpacity.title";
        public const string ItemBgImageOpacityDesc = "settings.theme.bgImageOpacity.desc";

        public const string GroupColorsTitle = "settings.theme.colors.title";
        public const string ColorMainPanelTitle = "settings.theme.color.mainPanel.title";
        public const string ColorMainPanelDesc = "settings.theme.color.mainPanel.desc";
        public const string ColorTitleBarTitle = "settings.theme.color.titleBar.title";
        public const string ColorTitleBarDesc = "settings.theme.color.titleBar.desc";
        public const string ColorTitleTextTitle = "settings.theme.color.titleText.title";
        public const string ColorTitleTextDesc = "settings.theme.color.titleText.desc";
        public const string ColorItemTextTitle = "settings.theme.color.itemText.title";
        public const string ColorItemTextDesc = "settings.theme.color.itemText.desc";
        public const string ColorItemShadowTitle = "settings.theme.color.itemShadow.title";
        public const string ColorItemShadowDesc = "settings.theme.color.itemShadow.desc";
        public const string ColorItemHoverTitle = "settings.theme.color.itemHover.title";
        public const string ColorItemHoverDesc = "settings.theme.color.itemHover.desc";
        public const string ColorItemSelectedTitle = "settings.theme.color.itemSelected.title";
        public const string ColorItemSelectedDesc = "settings.theme.color.itemSelected.desc";
        public const string ColorBorderTitle = "settings.theme.color.border.title";
        public const string ColorBorderDesc = "settings.theme.color.border.desc";
        public const string ColorScrollBarTitle = "settings.theme.color.scrollBar.title";
        public const string ColorScrollBarDesc = "settings.theme.color.scrollBar.desc";
        public const string ColorMenuBgTitle = "settings.theme.color.menuBg.title";
        public const string ColorMenuBgDesc = "settings.theme.color.menuBg.desc";
        public const string ColorMenuTextTitle = "settings.theme.color.menuText.title";
        public const string ColorMenuTextDesc = "settings.theme.color.menuText.desc";
        public const string ColorMenuHoverTitle = "settings.theme.color.menuHover.title";
        public const string ColorMenuHoverDesc = "settings.theme.color.menuHover.desc";
        public const string ColorMenuHoverTextTitle = "settings.theme.color.menuHoverText.title";
        public const string ColorMenuHoverTextDesc = "settings.theme.color.menuHoverText.desc";
        public const string ColorAccentTitle = "settings.theme.color.accent.title";
        public const string ColorAccentDesc = "settings.theme.color.accent.desc";

        // 预览图示例词条
        public const string PreviewWorkspace = "settings.theme.preview.workspace";
        public const string PreviewDocs = "settings.theme.preview.docs";
        public const string PreviewImages = "settings.theme.preview.images";
        public const string PreviewProject = "settings.theme.preview.project";
    }

    /// <summary>
    /// 多语言包注册与查询中心。
    /// 支持按语言代码注册独立语言包、运行时动态切换语言以及缺失词条自动回退。
    /// </summary>
    public static class LocalizationManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, ILanguagePack> PacksByCode =
            new Dictionary<string, ILanguagePack>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ILanguagePack> OrderedPacks = new List<ILanguagePack>();
        private static string currentLanguageCode;

        static LocalizationManager()
        {
            RegisterLanguage(new SimplifiedChineseLanguagePack());
            RegisterLanguage(new TraditionalChineseLanguagePack());
            RegisterLanguage(new EnglishLanguagePack());
        }

        /// <summary>注册或替换一个语言包。</summary>
        public static void RegisterLanguage(ILanguagePack pack)
        {
            if (pack == null)
                throw new ArgumentNullException(nameof(pack));
            if (string.IsNullOrWhiteSpace(pack.Code))
                throw new ArgumentException("Language pack code cannot be empty.", nameof(pack));

            lock (SyncRoot)
            {
                if (!PacksByCode.ContainsKey(pack.Code))
                {
                    OrderedPacks.Add(pack);
                }
                else
                {
                    for (int i = 0; i < OrderedPacks.Count; i++)
                    {
                        if (string.Equals(OrderedPacks[i].Code, pack.Code, StringComparison.OrdinalIgnoreCase))
                        {
                            OrderedPacks[i] = pack;
                            break;
                        }
                    }
                }

                PacksByCode[pack.Code] = pack;
            }
        }

        /// <summary>获取所有已注册的语言包（保持注册顺序）。</summary>
        public static IReadOnlyList<ILanguagePack> GetRegisteredLanguages()
        {
            lock (SyncRoot)
            {
                return OrderedPacks.ToList();
            }
        }

        /// <summary>获取或设置当前生效的语言代码。</summary>
        public static string CurrentLanguageCode
        {
            get
            {
                lock (SyncRoot)
                {
                    if (!string.IsNullOrWhiteSpace(currentLanguageCode))
                        return currentLanguageCode;
                }

                return ResolveCultureToLanguageCode(
                    CultureInfo.CurrentUICulture?.Name ??
                    CultureInfo.CurrentCulture?.Name);
            }
        }

        /// <summary>设置当前应用语言（传入空值时恢复跟随系统语言）。</summary>
        public static void SetLanguage(string languageCode)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    currentLanguageCode = null;
                    return;
                }

                currentLanguageCode = ResolveCultureToLanguageCode(languageCode);
            }
        }

        /// <summary>将任意区域名称规范化为已注册的语言代码。</summary>
        public static string ResolveCultureToLanguageCode(string cultureOrCode)
        {
            if (!string.IsNullOrWhiteSpace(cultureOrCode))
            {
                string trimmed = cultureOrCode.Trim();
                lock (SyncRoot)
                {
                    if (PacksByCode.TryGetValue(trimmed, out var exactPack))
                        return exactPack.Code;
                }

                if (trimmed.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    return "zh-TW";
                }

                if (trimmed.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    return "zh-CN";

                if (trimmed.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    return "en-US";
            }

            return "zh-CN";
        }

        /// <summary>根据词条 Key 获取当前语言的本地化文本；若当前语言未定义则依次回退到 zh-CN 和 en-US。</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            string code = CurrentLanguageCode;
            lock (SyncRoot)
            {
                if (PacksByCode.TryGetValue(code, out var activePack) &&
                    activePack.Strings != null &&
                    activePack.Strings.TryGetValue(key, out var text) &&
                    text != null)
                {
                    return text;
                }

                if (PacksByCode.TryGetValue("zh-CN", out var fallbackZh) &&
                    fallbackZh.Strings != null &&
                    fallbackZh.Strings.TryGetValue(key, out var zhText) &&
                    zhText != null)
                {
                    return zhText;
                }

                if (PacksByCode.TryGetValue("en-US", out var fallbackEn) &&
                    fallbackEn.Strings != null &&
                    fallbackEn.Strings.TryGetValue(key, out var enText) &&
                    enText != null)
                {
                    return enText;
                }
            }

            return key;
        }

        /// <summary>根据词条 Key 获取带格式化占位符的本地化文本。</summary>
        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            if (args == null || args.Length == 0)
                return pattern;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, pattern, args);
            }
            catch
            {
                return pattern;
            }
        }
    }
}
