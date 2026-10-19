using System;
using System.Collections.Generic;

namespace NoFences.Localization
{
    /// <summary>
    /// 繁體中文語言包（zh-TW）。
    /// </summary>
    public sealed class TraditionalChineseLanguagePack : ILanguagePack
    {
        public string Code => "zh-TW";

        public string DisplayName => "繁體中文";

        public IReadOnlyDictionary<string, string> Strings { get; } = BuildStrings();

        private static IReadOnlyDictionary<string, string> BuildStrings()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. 通用按鈕與基礎操作
            d[LKeys.Ok] = "確定";
            d[LKeys.Confirm] = "確定";
            d[LKeys.Cancel] = "取消";
            d[LKeys.Save] = "儲存";
            d[LKeys.Reset] = "重置";

            // 2. 柵欄右鍵選單與視窗操作
            d[LKeys.MenuDeleteItem] = "刪除項目";
            d[LKeys.MenuLock] = "鎖定";
            d[LKeys.MenuMinify] = "收起視窗";
            d[LKeys.MenuRename] = "重新命名";
            d[LKeys.MenuTitleSize] = "標題設定";
            d[LKeys.MenuScale] = "縮放比例";
            d[LKeys.MenuIconManagement] = "圖示管理";
            d[LKeys.MenuSort] = "排序方式";
            d[LKeys.MenuSortByName] = "名稱";
            d[LKeys.MenuSortBySize] = "大小";
            d[LKeys.MenuSortByType] = "項目類型";
            d[LKeys.MenuSortByCreated] = "建立時間";
            d[LKeys.MenuSortByModified] = "修改日期";
            d[LKeys.MoveItemTitle] = "移動圖示";
            d[LKeys.MenuSettings] = "設定";
            d[LKeys.MenuTheme] = "主題風格";
            d[LKeys.MenuDarkMode] = "黑暗模式";
            d[LKeys.MenuAutoSnap] = "自動吸附";
            d[LKeys.MenuNewFence] = "新建桌面分區";
            d[LKeys.MenuRemoveFence] = "移除桌面分區";
            d[LKeys.MenuQuitApp] = "結束程式";

            // 3. 柵欄預設名稱、重新命名與標題設定視窗
            d[LKeys.DefaultTheme] = "預設";
            d[LKeys.DefaultFenceName] = "第一個分區";
            d[LKeys.NewFenceName] = "新建分區";
            d[LKeys.EditDialogTitle] = "編輯";
            d[LKeys.EditDialogNameLabel] = "名稱：";
            d[LKeys.HeightDialogTitle] = "標題設定";
            d[LKeys.HeightDialogRestoreDefault] = "恢復預設值";
            d[LKeys.TitleSettingsPositionTitle] = "標題列位置";
            d[LKeys.TitleSettingsPositionDesc] = "選擇標題列在桌面分區上的位置";
            d[LKeys.TitleSettingsPosBottom] = "底部";
            d[LKeys.TitleSettingsPosTop] = "頂部";
            d[LKeys.TitleSettingsPosLeft] = "左";
            d[LKeys.TitleSettingsPosRight] = "右側";
            d[LKeys.TitleSettingsAlignTitle] = "標題文字對齊";
            d[LKeys.TitleSettingsAlignDesc] = "選擇標題文字在標題列上的位置";
            d[LKeys.TitleSettingsAlignLeft] = "靠左";
            d[LKeys.TitleSettingsAlignCenter] = "置中";
            d[LKeys.TitleSettingsAlignRight] = "靠右";
            d[LKeys.TitleSettingsSizeTitle] = "標題文字大小";
            d[LKeys.TitleSettingsSizeDesc] = "調整標題文字的字型大小（10px - 40px）";
            d[LKeys.TitleSettingsPaddingTitle] = "內邊距";
            d[LKeys.TitleSettingsPaddingDesc] = "調整標題文字與邊緣的內邊距距離（0px - 50px）";
            d[LKeys.TitleSettingsPaddingXTitle] = "左右內邊距";
            d[LKeys.TitleSettingsPaddingXDesc] = "調整標題文字與左右邊緣的距離（0px - 50px）";
            d[LKeys.TitleSettingsPaddingYTitle] = "上下內邊距";
            d[LKeys.TitleSettingsPaddingYDesc] = "調整標題文字與上下邊緣的距離，標題列高度自適應擴展（0px - 50px）";
            d[LKeys.TitleSettingsLineHeightTitle] = "文字行高";
            d[LKeys.TitleSettingsLineHeightDesc] = "調整標題文字的行高倍數與垂直字距（1.0 - 2.5）";

            // 4. 圖示多選管理與訊息提示框
            d[LKeys.IconManagementHint] = "選擇要移除的圖示";
            d[LKeys.IconManagementSelectedCountFormat] = "已選擇 {0} 項";
            d[LKeys.RemoveItemsTitle] = "移除圖示";
            d[LKeys.RemoveItemsFailed] = "部分圖示無法移除：";
            d[LKeys.NativeMenuTitle] = "檔案選單";
            d[LKeys.NativeMenuFailed] = "無法開啟該項目的 Windows 右鍵選單。";
            d[LKeys.NativeMenuSyncFailed] = "檔案操作已完成，但無法同步該捷徑的託管記錄。";
            d[LKeys.PersistenceTitle] = "儲存分區";
            d[LKeys.PersistenceFailed] = "無法儲存分區設定。本次執行仍可繼續，但部分變更可能在重啟後遺失。";
            d[LKeys.RecoveryTitle] = "恢復桌面捷徑";
            d[LKeys.RecoveryRunning] = "NoFences 正在執行。請先結束程式，再執行恢復。";
            d[LKeys.RecoveryCompleted] = "所有可恢復的桌面捷徑均已解除託管。";
            d[LKeys.RecoveryFailed] = "部分桌面捷徑無法恢復。沒有檔案被覆蓋。";
            d[LKeys.StartupTitle] = "NoFences 啟動";
            d[LKeys.StartupWarning] = "部分桌面捷徑操作未能完成。沒有檔案被覆蓋。";
            d[LKeys.RemoveFenceTitle] = "移除分區";
            d[LKeys.RemoveFenceQuestion] = "確定要移除這個分區嗎？";
            d[LKeys.RemoveFenceFailed] = "無法恢復全部桌面捷徑。為保護檔案，分區已保留。";
            d[LKeys.ExitTitle] = "結束 NoFences";
            d[LKeys.ExitFailed] = "部分桌面捷徑無法恢復。沒有檔案被覆蓋，程式將保持執行。";
            d[LKeys.RemoveItemTitle] = "移除項目";
            d[LKeys.RemoveItemFailed] = "無法從柵欄移除該項目。";
            d[LKeys.AddItemTitle] = "新增項目";
            d[LKeys.AddItemsFailed] = "部分拖入的項目未能新增到柵欄：";

            // 5. 設定頁面框架與底部狀態列
            d[LKeys.SettingsWindowTitle] = "設定";
            d[LKeys.StatusUnsavedChanges] = "● 存在尚未儲存的修改";
            d[LKeys.StatusReverted] = "已恢復到最近一次儲存的設定狀態";
            d[LKeys.StatusSavedSuccess] = "✓ 設定已成功儲存";
            d[LKeys.StatusSaveFailedPrefix] = "儲存失敗：";

            // 6. 設定頁面 - 基礎設定 (Basic Settings)
            d[LKeys.CatBasicTitle] = "基礎設定";
            d[LKeys.CatBasicDesc] = "設定應用的基本行為與常規選項。";

            d[LKeys.GroupGeneralTitle] = "常規設定";
            d[LKeys.ItemLaunchAtStartupTitle] = "開機自啟動";
            d[LKeys.ItemLaunchAtStartupDesc] = "系統啟動時自動執行本應用。";
            d[LKeys.ItemLanguageTitle] = "語言";
            d[LKeys.ItemLanguageDesc] = "選擇應用介面語言。";
            d[LKeys.ItemCheckUpdatesTitle] = "檢查更新（尚未支援）";
            d[LKeys.ItemCheckUpdatesDesc] = "目前版本尚未提供自動更新檢查。";

            d[LKeys.GroupStorageTitle] = "儲存設定";
            d[LKeys.ItemDataPathTitle] = "資料儲存位置";
            d[LKeys.ItemDataPathDesc] = "目前的資料儲存位置；此版本尚不支援變更。";
            d[LKeys.DialogSelectDataDir] = "選擇應用資料儲存目錄";
            d[LKeys.ItemAutoSaveTitle] = "自動儲存";
            d[LKeys.ItemAutoSaveDesc] = "結束時自動儲存目前設定。";

            d[LKeys.GroupGlobalConfigTitle] = "全域配置";
            d[LKeys.ItemMinifyAnimationTitle] = "收起視窗過渡動畫";
            d[LKeys.ItemMinifyAnimationDesc] = "開啟後，桌面分區在收起與展開時使用平滑過渡動畫效果。";

            d[LKeys.ErrDataPathEmpty] = "資料儲存位置不能為空。";
            d[LKeys.ErrDataPathInvalidChars] = "資料儲存位置包含非法路徑字元。";
            d[LKeys.ErrDataPathNotAbsolute] = "請輸入完整的絕對目錄路徑。";
            d[LKeys.ErrDataPathDriveMissing] = "資料儲存位置所在的磁碟機不存在。";
            d[LKeys.ErrDataPathFormatPrefix] = "資料儲存位置路徑格式無效：";
            d[LKeys.ErrDataDirNotWritablePrefix] = "無法存取或寫入資料儲存目錄：";
            d[LKeys.ErrStartupRegistryPrefix] = "無法設定開機自啟動登錄檔項目：";
            d[LKeys.ErrSaveConfigPrefix] = "儲存設定檔失敗：";

            // 7. 設定頁面 - 主題設定 (Theme Settings)
            d[LKeys.CatThemeTitle] = "主題設定";
            d[LKeys.CatThemeDesc] = "設定應用的介面主題、顏色、外觀及顯示效果。";

            d[LKeys.GroupThemeModeTitle] = "主題與模式";
            d[LKeys.ItemThemeStyleTitle] = "主題風格";
            d[LKeys.ItemThemeStyleDesc] = "選擇預設桌面分區主題或基於目前預設進行自定義。";
            d[LKeys.ThemeCustomOption] = "自定義";
            d[LKeys.BtnCopyToCustom] = "基於此主題自定義";
            d[LKeys.BtnResetCustom] = "重置自定義";
            d[LKeys.ItemDarkModeTitle] = "黑夜模式";
            d[LKeys.ItemDarkModeDesc] = "獨立切換全域深色與淺色模式，同時切換設定頁面明暗主題。";

            d[LKeys.GroupAppearanceTitle] = "外觀設定";
            d[LKeys.ItemFontFamilyTitle] = "字體名稱";
            d[LKeys.ItemFontFamilyDesc] = "桌面分區標題與選單優先使用的字體名稱。";
            d[LKeys.ErrFontFamilyEmpty] = "自定義主題的字體名稱不能為空。";
            d[LKeys.ItemMenuStyleTitle] = "右鍵選單風格";
            d[LKeys.ItemMenuStyleDesc] = "選擇分區右鍵選單的邊框、圓角與間距樣式。";
            d[LKeys.MenuStyleStandard] = "標準 / 經典半透明";
            d[LKeys.ItemCornerRadiusTitle] = "圓角半徑";
            d[LKeys.ItemCornerRadiusDesc] = "調整桌面分區視窗的圓角像素半徑（0 ~ 48 px）。";
            d[LKeys.ItemPanelOpacityTitle] = "主面板不透明度";
            d[LKeys.ItemPanelOpacityDesc] = "調整桌面分區內容區域背景的不透明度（20% ~ 100%）。";
            d[LKeys.ItemTitleOpacityTitle] = "標題欄不透明度";
            d[LKeys.ItemTitleOpacityDesc] = "調整桌面分區頂部標題欄的不透明度（20% ~ 100%）。";
            d[LKeys.ItemEnableBlurTitle] = "啟用背景模糊";
            d[LKeys.ItemEnableBlurDesc] = "在半透明分區後啟用系統毛玻璃模糊效果。";

            d[LKeys.GroupBackgroundImageTitle] = "背景圖片";
            d[LKeys.ItemBgImageFileTitle] = "圖片檔案";
            d[LKeys.ItemBgImageFileDesc] = "選擇分區背景圖；讀取後不鎖定原檔案，缺失時自動回退為純色。";
            d[LKeys.DialogSelectBgImageTitle] = "選擇主面板背景圖片";
            d[LKeys.DialogSelectBgImageFilter] = "圖片檔案|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有檔案|*.*";
            d[LKeys.ErrBgImageFileMissing] = "所選的背景圖片檔案不存在。";
            d[LKeys.ItemBgImageLayoutTitle] = "顯示方式";
            d[LKeys.ItemBgImageLayoutDesc] = "設定背景圖片在分區內的裁剪、縮放或平鋪方式。";
            d[LKeys.ImageLayoutFill] = "填充（裁剪）";
            d[LKeys.ImageLayoutFit] = "適應（完整）";
            d[LKeys.ImageLayoutStretch] = "拉伸";
            d[LKeys.ImageLayoutCenter] = "置中";
            d[LKeys.ImageLayoutTile] = "平鋪";
            d[LKeys.ItemBgImageOpacityTitle] = "圖片不透明度";
            d[LKeys.ItemBgImageOpacityDesc] = "調整背景圖片的疊加顯示不透明度（0% ~ 100%）。";

            d[LKeys.GroupColorsTitle] = "顏色設定";
            d[LKeys.ColorMainPanelTitle] = "主面板顏色";
            d[LKeys.ColorMainPanelDesc] = "桌面分區主內容區域的背景顏色。";
            d[LKeys.ColorTitleBarTitle] = "標題欄顏色";
            d[LKeys.ColorTitleBarDesc] = "桌面分區頂部標題欄的背景顏色。";
            d[LKeys.ColorTitleTextTitle] = "標題文字顏色";
            d[LKeys.ColorTitleTextDesc] = "桌面分區標題欄的文字顏色。";
            d[LKeys.ColorItemTextTitle] = "項目文字顏色";
            d[LKeys.ColorItemTextDesc] = "分區內圖示檔案名標籤的文字顏色。";
            d[LKeys.ColorItemShadowTitle] = "項目文字陰影";
            d[LKeys.ColorItemShadowDesc] = "圖示文字右下方的投影顏色。";
            d[LKeys.ColorItemHoverTitle] = "項目懸停高亮";
            d[LKeys.ColorItemHoverDesc] = "滑鼠懸停在圖示上時的背景高亮顏色。";
            d[LKeys.ColorItemSelectedTitle] = "項目選中高亮";
            d[LKeys.ColorItemSelectedDesc] = "單擊選中圖示時的背景高亮顏色。";
            d[LKeys.ColorBorderTitle] = "分區邊框顏色";
            d[LKeys.ColorBorderDesc] = "桌面分區外框與分隔線的顏色。";
            d[LKeys.ColorScrollBarTitle] = "滾動條顏色";
            d[LKeys.ColorScrollBarDesc] = "分區右側滾動條滑塊的顏色。";
            d[LKeys.ColorMenuBgTitle] = "選單背景顏色";
            d[LKeys.ColorMenuBgDesc] = "分區右鍵選單的背景顏色。";
            d[LKeys.ColorMenuTextTitle] = "選單文字顏色";
            d[LKeys.ColorMenuTextDesc] = "分區右鍵選單項的預設文字顏色。";
            d[LKeys.ColorMenuHoverTitle] = "選單懸停背景";
            d[LKeys.ColorMenuHoverDesc] = "分區右鍵選單項懸停時的背景顏色。";
            d[LKeys.ColorMenuHoverTextTitle] = "選單懸停文字";
            d[LKeys.ColorMenuHoverTextDesc] = "分區右鍵選單項懸停時的文字顏色。";
            d[LKeys.ColorAccentTitle] = "主題強調色";
            d[LKeys.ColorAccentDesc] = "分區管理欄、選中標記及滑塊的強調顏色。";

            d[LKeys.PreviewWorkspace] = "工作區";
            d[LKeys.PreviewDocs] = "文件";
            d[LKeys.PreviewImages] = "圖片";
            d[LKeys.PreviewProject] = "專案";

            return d;
        }
    }
}
