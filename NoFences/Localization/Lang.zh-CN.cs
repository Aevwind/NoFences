using System;
using System.Collections.Generic;

namespace NoFences.Localization
{
    /// <summary>
    /// 简体中文语言包（zh-CN）。
    /// </summary>
    public sealed class SimplifiedChineseLanguagePack : ILanguagePack
    {
        public string Code => "zh-CN";

        public string DisplayName => "简体中文";

        public IReadOnlyDictionary<string, string> Strings { get; } = BuildStrings();

        private static IReadOnlyDictionary<string, string> BuildStrings()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. 通用按钮与基础操作
            d[LKeys.Ok] = "确定";
            d[LKeys.Confirm] = "确定";
            d[LKeys.Cancel] = "取消";
            d[LKeys.Save] = "保存";
            d[LKeys.Reset] = "重置";

            // 2. 栅栏右键菜单与窗口操作
            d[LKeys.MenuDeleteItem] = "删除项目";
            d[LKeys.MenuLock] = "锁定";
            d[LKeys.MenuMinify] = "收起窗口";
            d[LKeys.MenuRename] = "重命名";
            d[LKeys.MenuTitleSize] = "标题设置";
            d[LKeys.MenuScale] = "缩放比例";
            d[LKeys.MenuIconManagement] = "图标管理";
            d[LKeys.MenuSort] = "排序方式";
            d[LKeys.MenuSortByName] = "名称";
            d[LKeys.MenuSortBySize] = "大小";
            d[LKeys.MenuSortByType] = "项目类型";
            d[LKeys.MenuSortByCreated] = "创建时间";
            d[LKeys.MenuSortByModified] = "修改日期";
            d[LKeys.MoveItemTitle] = "移动图标";
            d[LKeys.MenuSettings] = "设置";
            d[LKeys.MenuTheme] = "主题风格";
            d[LKeys.MenuDarkMode] = "黑暗模式";
            d[LKeys.MenuAutoSnap] = "自动吸附";
            d[LKeys.MenuNewFence] = "新建桌面分区";
            d[LKeys.MenuRemoveFence] = "移除桌面分区";
            d[LKeys.MenuQuitApp] = "退出程序";

            // 3. 栅栏默认名称、重命名与标题设置弹窗
            d[LKeys.DefaultTheme] = "默认";
            d[LKeys.DefaultFenceName] = "第一个分区";
            d[LKeys.NewFenceName] = "新建分区";
            d[LKeys.EditDialogTitle] = "编辑";
            d[LKeys.EditDialogNameLabel] = "名称：";
            d[LKeys.HeightDialogTitle] = "标题设置";
            d[LKeys.HeightDialogRestoreDefault] = "恢复默认值";
            d[LKeys.TitleSettingsPositionTitle] = "标题栏位置";
            d[LKeys.TitleSettingsPositionDesc] = "选择标题栏在桌面分区上的位置";
            d[LKeys.TitleSettingsPosBottom] = "底部";
            d[LKeys.TitleSettingsPosTop] = "顶部";
            d[LKeys.TitleSettingsPosLeft] = "左";
            d[LKeys.TitleSettingsPosRight] = "右侧";
            d[LKeys.TitleSettingsAlignTitle] = "标题文本对齐";
            d[LKeys.TitleSettingsAlignDesc] = "选择标题文本在标题栏上的位置";
            d[LKeys.TitleSettingsAlignLeft] = "靠左";
            d[LKeys.TitleSettingsAlignCenter] = "居中";
            d[LKeys.TitleSettingsAlignRight] = "靠右";
            d[LKeys.TitleSettingsSizeTitle] = "标题文本大小";
            d[LKeys.TitleSettingsSizeDesc] = "调整标题文本的字体大小（10px - 40px）";
            d[LKeys.TitleSettingsPaddingTitle] = "内边距";
            d[LKeys.TitleSettingsPaddingDesc] = "调整标题文本与边缘的内边距距离（0px - 50px）";
            d[LKeys.TitleSettingsPaddingXTitle] = "左右内边距";
            d[LKeys.TitleSettingsPaddingXDesc] = "调整标题文本与左右边缘的距离（0px - 50px）";
            d[LKeys.TitleSettingsPaddingYTitle] = "上下内边距";
            d[LKeys.TitleSettingsPaddingYDesc] = "调整标题文本与上下边缘的距离，标题栏高度自适应扩展（0px - 50px）";
            d[LKeys.TitleSettingsLineHeightTitle] = "文字行高";
            d[LKeys.TitleSettingsLineHeightDesc] = "调整标题文本的行高倍数与垂直字距（1.0 - 2.5）";

            // 4. 图标多选管理与消息提示框
            d[LKeys.IconManagementHint] = "选择要移除的图标";
            d[LKeys.IconManagementSelectedCountFormat] = "已选择 {0} 项";
            d[LKeys.RemoveItemsTitle] = "移除图标";
            d[LKeys.RemoveItemsFailed] = "部分图标无法移除：";
            d[LKeys.NativeMenuTitle] = "文件菜单";
            d[LKeys.NativeMenuFailed] = "无法打开该项目的 Windows 右键菜单。";
            d[LKeys.NativeMenuSyncFailed] = "文件操作已完成，但无法同步该快捷方式的托管记录。";
            d[LKeys.PersistenceTitle] = "保存分区";
            d[LKeys.PersistenceFailed] = "无法保存分区设置。本次运行仍可继续，但部分更改可能在重启后丢失。";
            d[LKeys.RecoveryTitle] = "恢复桌面快捷方式";
            d[LKeys.RecoveryRunning] = "NoFences 正在运行。请先退出程序，再执行恢复。";
            d[LKeys.RecoveryCompleted] = "所有可恢复的桌面快捷方式均已解除托管。";
            d[LKeys.RecoveryFailed] = "部分桌面快捷方式无法恢复。没有文件被覆盖。";
            d[LKeys.StartupTitle] = "NoFences 启动";
            d[LKeys.StartupWarning] = "部分桌面快捷方式操作未能完成。没有文件被覆盖。";
            d[LKeys.RemoveFenceTitle] = "移除分区";
            d[LKeys.RemoveFenceQuestion] = "确定要移除这个分区吗？";
            d[LKeys.RemoveFenceFailed] = "无法恢复全部桌面快捷方式。为保护文件，分区已保留。";
            d[LKeys.ExitTitle] = "退出 NoFences";
            d[LKeys.ExitFailed] = "部分桌面快捷方式无法恢复。没有文件被覆盖，程序将保持运行。";
            d[LKeys.RemoveItemTitle] = "移除项目";
            d[LKeys.RemoveItemFailed] = "无法从栅栏移除该项目。";
            d[LKeys.AddItemTitle] = "添加项目";
            d[LKeys.AddItemsFailed] = "部分拖入的项目未能添加到栅栏：";

            // 5. 设置页面框架与底部状态栏
            d[LKeys.SettingsWindowTitle] = "设置";
            d[LKeys.StatusUnsavedChanges] = "● 存在尚未保存的修改";
            d[LKeys.StatusReverted] = "已恢复到最近一次保存的配置状态";
            d[LKeys.StatusSavedSuccess] = "✓ 配置已成功保存";
            d[LKeys.StatusSaveFailedPrefix] = "保存失败：";

            // 6. 设置页面 - 基础设置 (Basic Settings)
            d[LKeys.CatBasicTitle] = "基础设置";
            d[LKeys.CatBasicDesc] = "配置应用的基本行为与常规选项。";

            d[LKeys.GroupGeneralTitle] = "常规设置";
            d[LKeys.ItemLaunchAtStartupTitle] = "开机自启动";
            d[LKeys.ItemLaunchAtStartupDesc] = "系统启动时自动运行本应用。";
            d[LKeys.ItemLanguageTitle] = "语言";
            d[LKeys.ItemLanguageDesc] = "选择应用界面语言。";
            d[LKeys.ItemCheckUpdatesTitle] = "检查更新（暂未支持）";
            d[LKeys.ItemCheckUpdatesDesc] = "当前版本暂不提供自动更新检查。";

            d[LKeys.GroupStorageTitle] = "存储设置";
            d[LKeys.ItemDataPathTitle] = "数据保存位置";
            d[LKeys.ItemDataPathDesc] = "当前数据保存位置；此版本暂不支持更改。";
            d[LKeys.DialogSelectDataDir] = "选择应用数据保存目录";
            d[LKeys.ItemAutoSaveTitle] = "自动保存";
            d[LKeys.ItemAutoSaveDesc] = "退出时自动保存当前配置。";

            d[LKeys.GroupGlobalConfigTitle] = "全局配置";
            d[LKeys.ItemMinifyAnimationTitle] = "收起窗口过渡动画";
            d[LKeys.ItemMinifyAnimationDesc] = "开启后，桌面分区在收起和展开时使用平滑过渡动画效果。";

            d[LKeys.ErrDataPathEmpty] = "数据保存位置不能为空。";
            d[LKeys.ErrDataPathInvalidChars] = "数据保存位置包含非法路径字符。";
            d[LKeys.ErrDataPathNotAbsolute] = "请输入完整的绝对目录路径。";
            d[LKeys.ErrDataPathDriveMissing] = "数据保存位置所在的磁盘驱动器不存在。";
            d[LKeys.ErrDataPathFormatPrefix] = "数据保存位置路径格式无效：";
            d[LKeys.ErrDataDirNotWritablePrefix] = "无法访问或写入数据保存目录：";
            d[LKeys.ErrStartupRegistryPrefix] = "无法设置开机自启动注册表项：";
            d[LKeys.ErrSaveConfigPrefix] = "保存配置文件失败：";

            // 7. 设置页面 - 主题设置 (Theme Settings)
            d[LKeys.CatThemeTitle] = "主题设置";
            d[LKeys.CatThemeDesc] = "配置应用的界面主题、颜色、外观及显示效果。";

            d[LKeys.GroupThemeModeTitle] = "主题与模式";
            d[LKeys.ItemThemeStyleTitle] = "主题风格";
            d[LKeys.ItemThemeStyleDesc] = "选择预设桌面分区主题或基于当前预设进行自定义。";
            d[LKeys.ThemeCustomOption] = "自定义";
            d[LKeys.BtnCopyToCustom] = "基于此主题自定义";
            d[LKeys.BtnResetCustom] = "重置自定义";
            d[LKeys.ItemDarkModeTitle] = "黑夜模式";
            d[LKeys.ItemDarkModeDesc] = "独立切换全局深色与浅色模式，同时切换设置页面明暗主题。";

            d[LKeys.GroupAppearanceTitle] = "外观设置";
            d[LKeys.ItemFontFamilyTitle] = "字体名称";
            d[LKeys.ItemFontFamilyDesc] = "桌面分区标题与菜单优先使用的字体名称。";
            d[LKeys.ErrFontFamilyEmpty] = "自定义主题的字体名称不能为空。";
            d[LKeys.ItemMenuStyleTitle] = "右键菜单风格";
            d[LKeys.ItemMenuStyleDesc] = "选择分区右键菜单的边框、圆角与间距样式。";
            d[LKeys.MenuStyleStandard] = "标准 / 经典半透明";
            d[LKeys.ItemCornerRadiusTitle] = "圆角半径";
            d[LKeys.ItemCornerRadiusDesc] = "调整桌面分区窗口的圆角像素半径（0 ~ 48 px）。";
            d[LKeys.ItemPanelOpacityTitle] = "主面板不透明度";
            d[LKeys.ItemPanelOpacityDesc] = "调整桌面分区内容区域背景的不透明度（20% ~ 100%）。";
            d[LKeys.ItemTitleOpacityTitle] = "标题栏不透明度";
            d[LKeys.ItemTitleOpacityDesc] = "调整桌面分区顶部标题栏的不透明度（20% ~ 100%）。";
            d[LKeys.ItemEnableBlurTitle] = "启用背景模糊";
            d[LKeys.ItemEnableBlurDesc] = "在半透明分区后启用系统毛玻璃模糊效果。";

            d[LKeys.GroupBackgroundImageTitle] = "背景图片";
            d[LKeys.ItemBgImageFileTitle] = "图片文件";
            d[LKeys.ItemBgImageFileDesc] = "选择分区背景图；读取后不锁定原文件，缺失时自动回退为纯色。";
            d[LKeys.DialogSelectBgImageTitle] = "选择主面板背景图片";
            d[LKeys.DialogSelectBgImageFilter] = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*";
            d[LKeys.ErrBgImageFileMissing] = "所选的背景图片文件不存在。";
            d[LKeys.ItemBgImageLayoutTitle] = "显示方式";
            d[LKeys.ItemBgImageLayoutDesc] = "设置背景图片在分区内的裁剪、缩放或平铺方式。";
            d[LKeys.ImageLayoutFill] = "填充（裁剪）";
            d[LKeys.ImageLayoutFit] = "适应（完整）";
            d[LKeys.ImageLayoutStretch] = "拉伸";
            d[LKeys.ImageLayoutCenter] = "居中";
            d[LKeys.ImageLayoutTile] = "平铺";
            d[LKeys.ItemBgImageOpacityTitle] = "图片不透明度";
            d[LKeys.ItemBgImageOpacityDesc] = "调整背景图片的叠加显示不透明度（0% ~ 100%）。";

            d[LKeys.GroupColorsTitle] = "颜色配置";
            d[LKeys.ColorMainPanelTitle] = "主面板颜色";
            d[LKeys.ColorMainPanelDesc] = "桌面分区主内容区域的背景颜色。";
            d[LKeys.ColorTitleBarTitle] = "标题栏颜色";
            d[LKeys.ColorTitleBarDesc] = "桌面分区顶部标题栏的背景颜色。";
            d[LKeys.ColorTitleTextTitle] = "标题文字颜色";
            d[LKeys.ColorTitleTextDesc] = "桌面分区标题栏的文字颜色。";
            d[LKeys.ColorItemTextTitle] = "项目文字颜色";
            d[LKeys.ColorItemTextDesc] = "分区内图标文件名标签的文字颜色。";
            d[LKeys.ColorItemShadowTitle] = "项目文字阴影";
            d[LKeys.ColorItemShadowDesc] = "图标文字右下方的投影颜色。";
            d[LKeys.ColorItemHoverTitle] = "项目悬停高亮";
            d[LKeys.ColorItemHoverDesc] = "鼠标悬停在图标上时的背景高亮颜色。";
            d[LKeys.ColorItemSelectedTitle] = "项目选中高亮";
            d[LKeys.ColorItemSelectedDesc] = "单击选中图标时的背景高亮颜色。";
            d[LKeys.ColorBorderTitle] = "分区边框颜色";
            d[LKeys.ColorBorderDesc] = "桌面分区外框与分隔线的颜色。";
            d[LKeys.ColorScrollBarTitle] = "滚动条颜色";
            d[LKeys.ColorScrollBarDesc] = "分区右侧滚动条滑块的颜色。";
            d[LKeys.ColorMenuBgTitle] = "菜单背景颜色";
            d[LKeys.ColorMenuBgDesc] = "分区右键菜单的背景颜色。";
            d[LKeys.ColorMenuTextTitle] = "菜单文字颜色";
            d[LKeys.ColorMenuTextDesc] = "分区右键菜单项的默认文字颜色。";
            d[LKeys.ColorMenuHoverTitle] = "菜单悬停背景";
            d[LKeys.ColorMenuHoverDesc] = "分区右键菜单项悬停时的背景颜色。";
            d[LKeys.ColorMenuHoverTextTitle] = "菜单悬停文字";
            d[LKeys.ColorMenuHoverTextDesc] = "分区右键菜单项悬停时的文字颜色。";
            d[LKeys.ColorAccentTitle] = "主题强调色";
            d[LKeys.ColorAccentDesc] = "分区管理栏、选中标记及滑块的强调颜色。";

            d[LKeys.PreviewWorkspace] = "工作区";
            d[LKeys.PreviewDocs] = "文档";
            d[LKeys.PreviewImages] = "图片";
            d[LKeys.PreviewProject] = "项目";

            return d;
        }
    }
}
