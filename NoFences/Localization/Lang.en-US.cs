using System;
using System.Collections.Generic;

namespace NoFences.Localization
{
    /// <summary>
    /// 英文语言包（en-US）。
    /// </summary>
    public sealed class EnglishLanguagePack : ILanguagePack
    {
        public string Code => "en-US";

        public string DisplayName => "English";

        public IReadOnlyDictionary<string, string> Strings { get; } = BuildStrings();

        private static IReadOnlyDictionary<string, string> BuildStrings()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Common buttons and actions
            d[LKeys.Ok] = "OK";
            d[LKeys.Confirm] = "Confirm";
            d[LKeys.Cancel] = "Cancel";
            d[LKeys.Save] = "Save";
            d[LKeys.Reset] = "Reset";

            // 2. Fence context menu and window actions
            d[LKeys.MenuDeleteItem] = "Delete item";
            d[LKeys.MenuLock] = "Lock";
            d[LKeys.MenuMinify] = "Minify";
            d[LKeys.MenuRename] = "Rename";
            d[LKeys.MenuTitleSize] = "Title settings";
            d[LKeys.MenuScale] = "Scale";
            d[LKeys.MenuIconManagement] = "Manage icons";
            d[LKeys.MenuSort] = "Sort by";
            d[LKeys.MenuSortByName] = "Name";
            d[LKeys.MenuSortBySize] = "Size";
            d[LKeys.MenuSortByType] = "Item type";
            d[LKeys.MenuSortByCreated] = "Date created";
            d[LKeys.MenuSortByModified] = "Date modified";
            d[LKeys.MoveItemTitle] = "Move icon";
            d[LKeys.MenuSettings] = "Settings";
            d[LKeys.MenuTheme] = "Theme";
            d[LKeys.MenuDarkMode] = "Dark mode";
            d[LKeys.MenuAutoSnap] = "Auto snap";
            d[LKeys.MenuNewFence] = "New fence";
            d[LKeys.MenuRemoveFence] = "Remove fence";
            d[LKeys.MenuQuitApp] = "Exit application";

            // 3. Default fence names & dialogs
            d[LKeys.DefaultTheme] = "Default";
            d[LKeys.DefaultFenceName] = "First fence";
            d[LKeys.NewFenceName] = "New fence";
            d[LKeys.EditDialogTitle] = "Edit";
            d[LKeys.EditDialogNameLabel] = "Name:";
            d[LKeys.HeightDialogTitle] = "Title settings";
            d[LKeys.HeightDialogRestoreDefault] = "Restore default";
            d[LKeys.TitleSettingsPositionTitle] = "Title bar position";
            d[LKeys.TitleSettingsPositionDesc] = "Choose where the title bar appears on the fence";
            d[LKeys.TitleSettingsPosBottom] = "Bottom";
            d[LKeys.TitleSettingsPosTop] = "Top";
            d[LKeys.TitleSettingsPosLeft] = "Left";
            d[LKeys.TitleSettingsPosRight] = "Right";
            d[LKeys.TitleSettingsAlignTitle] = "Title text alignment";
            d[LKeys.TitleSettingsAlignDesc] = "Choose how title text is aligned on the title bar";
            d[LKeys.TitleSettingsAlignLeft] = "Left";
            d[LKeys.TitleSettingsAlignCenter] = "Center";
            d[LKeys.TitleSettingsAlignRight] = "Right";
            d[LKeys.TitleSettingsSizeTitle] = "Title text size";
            d[LKeys.TitleSettingsSizeDesc] = "Adjust the font size of the title text (10px - 40px)";
            d[LKeys.TitleSettingsPaddingTitle] = "Padding";
            d[LKeys.TitleSettingsPaddingDesc] = "Adjust title text inner padding from edges (0px - 50px)";
            d[LKeys.TitleSettingsPaddingXTitle] = "Horizontal padding";
            d[LKeys.TitleSettingsPaddingXDesc] = "Adjust distance from left and right edges (0px - 50px)";
            d[LKeys.TitleSettingsPaddingYTitle] = "Vertical padding";
            d[LKeys.TitleSettingsPaddingYDesc] = "Adjust distance from top and bottom edges; title height adapts automatically (0px - 50px)";
            d[LKeys.TitleSettingsLineHeightTitle] = "Line height";
            d[LKeys.TitleSettingsLineHeightDesc] = "Adjust title text line height multiplier (1.0 - 2.5)";

            // 4. Icon management and message dialogs
            d[LKeys.IconManagementHint] = "Select icons to remove";
            d[LKeys.IconManagementSelectedCountFormat] = "{0} selected";
            d[LKeys.RemoveItemsTitle] = "Remove icons";
            d[LKeys.RemoveItemsFailed] = "Some icons could not be removed:";
            d[LKeys.NativeMenuTitle] = "File menu";
            d[LKeys.NativeMenuFailed] = "Unable to open the Windows context menu for this item.";
            d[LKeys.NativeMenuSyncFailed] = "The file operation completed, but its managed shortcut record could not be synchronized.";
            d[LKeys.PersistenceTitle] = "Save fence";
            d[LKeys.PersistenceFailed] = "Unable to save the fence settings. You can continue this session, but some changes may be lost after restart.";
            d[LKeys.RecoveryTitle] = "Restore desktop shortcuts";
            d[LKeys.RecoveryRunning] = "NoFences is running. Exit it before starting recovery.";
            d[LKeys.RecoveryCompleted] = "All recoverable desktop shortcuts have been released.";
            d[LKeys.RecoveryFailed] = "Some desktop shortcuts could not be restored. No files were overwritten.";
            d[LKeys.StartupTitle] = "NoFences startup";
            d[LKeys.StartupWarning] = "Some desktop shortcut operations could not be completed. No files were overwritten.";
            d[LKeys.RemoveFenceTitle] = "Remove fence";
            d[LKeys.RemoveFenceQuestion] = "Really remove this fence?";
            d[LKeys.RemoveFenceFailed] = "Unable to restore all desktop shortcuts. The fence was kept to protect its files.";
            d[LKeys.ExitTitle] = "Exit NoFences";
            d[LKeys.ExitFailed] = "Some desktop shortcuts could not be restored. No files were overwritten and the application will remain open.";
            d[LKeys.RemoveItemTitle] = "Remove item";
            d[LKeys.RemoveItemFailed] = "Unable to remove the item from this fence.";
            d[LKeys.AddItemTitle] = "Add item";
            d[LKeys.AddItemsFailed] = "Some dropped items could not be added to this fence:";

            // 5. Settings window & status bar
            d[LKeys.SettingsWindowTitle] = "Settings";
            d[LKeys.StatusUnsavedChanges] = "● You have unsaved changes";
            d[LKeys.StatusReverted] = "Reverted to last saved configuration";
            d[LKeys.StatusSavedSuccess] = "✓ Settings saved successfully";
            d[LKeys.StatusSaveFailedPrefix] = "Save failed: ";

            // 6. Settings - Basic Settings
            d[LKeys.CatBasicTitle] = "Basic Settings";
            d[LKeys.CatBasicDesc] = "Configure general application behavior, language, and storage options.";

            d[LKeys.GroupGeneralTitle] = "General Settings";
            d[LKeys.ItemLaunchAtStartupTitle] = "Launch at startup";
            d[LKeys.ItemLaunchAtStartupDesc] = "Automatically run this application when Windows starts.";
            d[LKeys.ItemLanguageTitle] = "Language";
            d[LKeys.ItemLanguageDesc] = "Select the display language for the application.";
            d[LKeys.ItemCheckUpdatesTitle] = "Check for updates";
            d[LKeys.ItemCheckUpdatesDesc] = "Periodically check for new versions and updates.";

            d[LKeys.GroupStorageTitle] = "Storage Settings";
            d[LKeys.ItemDataPathTitle] = "Data storage location";
            d[LKeys.ItemDataPathDesc] = "Directory where application data and metadata are stored.";
            d[LKeys.DialogSelectDataDir] = "Select application data storage directory";
            d[LKeys.ItemAutoSaveTitle] = "Auto save";
            d[LKeys.ItemAutoSaveDesc] = "Automatically save current configuration and fence changes.";

            d[LKeys.GroupGlobalConfigTitle] = "Global Configuration";
            d[LKeys.ItemMinifyAnimationTitle] = "Collapse transition animation";
            d[LKeys.ItemMinifyAnimationDesc] = "Animate desktop fences smoothly when collapsing and expanding.";

            d[LKeys.ErrDataPathEmpty] = "Data storage location cannot be empty.";
            d[LKeys.ErrDataPathInvalidChars] = "Data storage path contains invalid characters.";
            d[LKeys.ErrDataPathNotAbsolute] = "Please enter a full absolute directory path.";
            d[LKeys.ErrDataPathDriveMissing] = "The drive for the data storage path does not exist.";
            d[LKeys.ErrDataPathFormatPrefix] = "Invalid data storage path: ";
            d[LKeys.ErrDataDirNotWritablePrefix] = "Unable to access or write to the data storage directory: ";
            d[LKeys.ErrStartupRegistryPrefix] = "Unable to update startup registry entry: ";
            d[LKeys.ErrSaveConfigPrefix] = "Failed to save configuration file: ";

            // 7. Settings - Theme Settings
            d[LKeys.CatThemeTitle] = "Theme Settings";
            d[LKeys.CatThemeDesc] = "Customize fence themes, colors, appearance, and visual effects.";

            d[LKeys.GroupThemeModeTitle] = "Theme & Mode";
            d[LKeys.ItemThemeStyleTitle] = "Theme style";
            d[LKeys.ItemThemeStyleDesc] = "Choose a preset fence theme or customize your own style.";
            d[LKeys.ThemeCustomOption] = "Custom";
            d[LKeys.BtnCopyToCustom] = "Customize";
            d[LKeys.BtnResetCustom] = "Reset";
            d[LKeys.ItemDarkModeTitle] = "Dark mode";
            d[LKeys.ItemDarkModeDesc] = "Switch between Dark and Light mode for fences and this settings window.";

            d[LKeys.GroupAppearanceTitle] = "Appearance";
            d[LKeys.ItemFontFamilyTitle] = "Font family";
            d[LKeys.ItemFontFamilyDesc] = "Primary font family used for fence titles and menus.";
            d[LKeys.ErrFontFamilyEmpty] = "Font family name cannot be empty for a custom theme.";
            d[LKeys.ItemMenuStyleTitle] = "Context menu style";
            d[LKeys.ItemMenuStyleDesc] = "Select the visual layout style for fence context menus.";
            d[LKeys.MenuStyleStandard] = "Standard / translucent";
            d[LKeys.ItemCornerRadiusTitle] = "Corner radius";
            d[LKeys.ItemCornerRadiusDesc] = "Adjust the corner radius of fence windows (0 - 48 px).";
            d[LKeys.ItemPanelOpacityTitle] = "Main panel opacity";
            d[LKeys.ItemPanelOpacityDesc] = "Adjust the background opacity of the fence main panel (20% - 100%).";
            d[LKeys.ItemTitleOpacityTitle] = "Title bar opacity";
            d[LKeys.ItemTitleOpacityDesc] = "Adjust the background opacity of the fence title bar (20% - 100%).";
            d[LKeys.ItemEnableBlurTitle] = "Enable background blur";
            d[LKeys.ItemEnableBlurDesc] = "Enable system acrylic/blur behind translucent fences.";

            d[LKeys.GroupBackgroundImageTitle] = "Background Image";
            d[LKeys.ItemBgImageFileTitle] = "Image file";
            d[LKeys.ItemBgImageFileDesc] = "Select a background image for fences; falls back to solid color if missing.";
            d[LKeys.DialogSelectBgImageTitle] = "Select main-panel background image";
            d[LKeys.DialogSelectBgImageFilter] = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*";
            d[LKeys.ErrBgImageFileMissing] = "The selected background image file does not exist.";
            d[LKeys.ItemBgImageLayoutTitle] = "Image layout";
            d[LKeys.ItemBgImageLayoutDesc] = "Choose how the background image is scaled or tiled.";
            d[LKeys.ImageLayoutFill] = "Fill (crop)";
            d[LKeys.ImageLayoutFit] = "Fit (contain)";
            d[LKeys.ImageLayoutStretch] = "Stretch";
            d[LKeys.ImageLayoutCenter] = "Center";
            d[LKeys.ImageLayoutTile] = "Tile";
            d[LKeys.ItemBgImageOpacityTitle] = "Image opacity";
            d[LKeys.ItemBgImageOpacityDesc] = "Adjust the opacity of the background image overlay (0% - 100%).";

            d[LKeys.GroupColorsTitle] = "Color Palette";
            d[LKeys.ColorMainPanelTitle] = "Main panel color";
            d[LKeys.ColorMainPanelDesc] = "Background color of the fence content area.";
            d[LKeys.ColorTitleBarTitle] = "Title bar color";
            d[LKeys.ColorTitleBarDesc] = "Background color of the fence title bar.";
            d[LKeys.ColorTitleTextTitle] = "Title text color";
            d[LKeys.ColorTitleTextDesc] = "Text color of the fence title.";
            d[LKeys.ColorItemTextTitle] = "Item text color";
            d[LKeys.ColorItemTextDesc] = "Text color of icon labels inside fences.";
            d[LKeys.ColorItemShadowTitle] = "Item text shadow";
            d[LKeys.ColorItemShadowDesc] = "Drop-shadow color behind icon labels.";
            d[LKeys.ColorItemHoverTitle] = "Item hover highlight";
            d[LKeys.ColorItemHoverDesc] = "Highlight color when hovering over an icon.";
            d[LKeys.ColorItemSelectedTitle] = "Item selected highlight";
            d[LKeys.ColorItemSelectedDesc] = "Highlight color when an icon is selected.";
            d[LKeys.ColorBorderTitle] = "Border color";
            d[LKeys.ColorBorderDesc] = "Color of fence borders and dividers.";
            d[LKeys.ColorScrollBarTitle] = "Scrollbar color";
            d[LKeys.ColorScrollBarDesc] = "Color of the fence scrollbar thumb.";
            d[LKeys.ColorMenuBgTitle] = "Menu background";
            d[LKeys.ColorMenuBgDesc] = "Background color of fence context menus.";
            d[LKeys.ColorMenuTextTitle] = "Menu text";
            d[LKeys.ColorMenuTextDesc] = "Default text color in fence context menus.";
            d[LKeys.ColorMenuHoverTitle] = "Menu hover background";
            d[LKeys.ColorMenuHoverDesc] = "Hover background color in fence context menus.";
            d[LKeys.ColorMenuHoverTextTitle] = "Menu hover text";
            d[LKeys.ColorMenuHoverTextDesc] = "Hover text color in fence context menus.";
            d[LKeys.ColorAccentTitle] = "Theme accent color";
            d[LKeys.ColorAccentDesc] = "Accent color used in fence management and sliders.";

            d[LKeys.PreviewWorkspace] = "Workspace";
            d[LKeys.PreviewDocs] = "Docs";
            d[LKeys.PreviewImages] = "Images";
            d[LKeys.PreviewProject] = "Project";

            return d;
        }
    }
}
