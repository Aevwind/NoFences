using NoFences.Localization;
using System;

namespace NoFences.Theming
{
    /// <summary>
    /// 界面本地化文本访问门面类。
    /// 内部委托给 <see cref="LocalizationManager"/>，从独立的语言包文件（Lang.zh-CN.cs、Lang.zh-TW.cs、Lang.en-US.cs）
    /// 按键名读取对应词条，无需外部 .resources.dll 即可实现多语言切换与扩展。
    /// </summary>
    internal static class ThemeText
    {
        /// <summary>获取当前生效的语言代码（zh-CN、zh-TW、en-US）。</summary>
        public static string CurrentLanguageCode => LocalizationManager.CurrentLanguageCode;

        /// <summary>当前是否为中文环境（含简中与繁中）。</summary>
        public static bool IsChinese =>
            CurrentLanguageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        /// <summary>当前是否为繁体中文环境。</summary>
        public static bool IsTraditionalChinese =>
            string.Equals(CurrentLanguageCode, "zh-TW", StringComparison.OrdinalIgnoreCase);

        /// <summary>显式设置当前应用界面语言代码。</summary>
        public static void SetLanguage(string languageCode)
        {
            LocalizationManager.SetLanguage(languageCode);
        }

        /// <summary>按词条键名从当前激活的语言包获取本地化文本。</summary>
        public static string Key(string key)
        {
            return LocalizationManager.Get(key);
        }

        /// <summary>按词条键名获取带格式化参数的本地化文本。</summary>
        public static string Format(string key, params object[] args)
        {
            return LocalizationManager.Format(key, args);
        }

        public static string SettingsMenu => Key(LKeys.MenuSettings);

        public static string ThemeMenu => Key(LKeys.MenuTheme);

        public static string ScaleMenu => Key(LKeys.MenuScale);

        public static string Reset => Key(LKeys.Reset);

        public static string DarkMode => Key(LKeys.MenuDarkMode);

        public static string DefaultTheme => Key(LKeys.DefaultTheme);

        public static string IconManagement => Key(LKeys.MenuIconManagement);

        public static string IconManagementHint => Key(LKeys.IconManagementHint);

        public static string SelectedItemCount(int count)
        {
            return Format(LKeys.IconManagementSelectedCountFormat, count);
        }

        public static string Ok => Key(LKeys.Ok);

        public static string Confirm => Key(LKeys.Confirm);

        public static string Cancel => Key(LKeys.Cancel);

        public static string Save => Key(LKeys.Save);

        public static string RemoveItemsTitle => Key(LKeys.RemoveItemsTitle);

        public static string RemoveItemsFailed => Key(LKeys.RemoveItemsFailed);

        public static string NativeMenuTitle => Key(LKeys.NativeMenuTitle);

        public static string NativeMenuFailed => Key(LKeys.NativeMenuFailed);

        public static string NativeMenuSyncFailed => Key(LKeys.NativeMenuSyncFailed);

        public static string PersistenceTitle => Key(LKeys.PersistenceTitle);

        public static string PersistenceFailed => Key(LKeys.PersistenceFailed);

        public static string RecoveryTitle => Key(LKeys.RecoveryTitle);

        public static string RecoveryRunning => Key(LKeys.RecoveryRunning);

        public static string RecoveryCompleted => Key(LKeys.RecoveryCompleted);

        public static string RecoveryFailed => Key(LKeys.RecoveryFailed);

        public static string DefaultFenceName => Key(LKeys.DefaultFenceName);

        public static string NewFenceName => Key(LKeys.NewFenceName);

        public static string StartupTitle => Key(LKeys.StartupTitle);

        public static string StartupWarning => Key(LKeys.StartupWarning);

        public static string RemoveFenceTitle => Key(LKeys.RemoveFenceTitle);

        public static string RemoveFenceQuestion => Key(LKeys.RemoveFenceQuestion);

        public static string RemoveFenceFailed => Key(LKeys.RemoveFenceFailed);

        public static string ExitTitle => Key(LKeys.ExitTitle);

        public static string ExitFailed => Key(LKeys.ExitFailed);

        public static string RemoveItemTitle => Key(LKeys.RemoveItemTitle);

        public static string RemoveItemFailed => Key(LKeys.RemoveItemFailed);

        public static string AddItemTitle => Key(LKeys.AddItemTitle);

        public static string AddItemsTitle => Key(LKeys.AddItemTitle);

        public static string AddItemsFailed => Key(LKeys.AddItemsFailed);
    }
}
