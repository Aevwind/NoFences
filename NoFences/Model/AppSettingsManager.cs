using Microsoft.Win32;
using NoFences.Localization;
using NoFences.Theming;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NoFences.Model
{
    /// <summary>
    /// 应用程序基础设置实体。独立于单个栅栏的元数据与主题配置，
    /// 持久化保存在 %LocalAppData%\NoFences\__app_settings.xml。
    /// </summary>
    [Serializable]
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>配置结构版本号。</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>是否随系统开机自动运行本应用。</summary>
        public bool LaunchAtStartup { get; set; }

        /// <summary>应用界面语言标识（zh-CN、zh-TW、en-US）。</summary>
        public string Language { get; set; } = "zh-CN";

        /// <summary>是否定期检查新版本。</summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>应用数据存储目录路径。</summary>
        public string DataStoragePath { get; set; } = AppSettingsManager.GetDefaultDataDirectory();

        /// <summary>是否自动保存配置与栅栏变更。</summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>是否在移动桌面分区时自动吸附屏幕边缘或其他桌面分区。</summary>
        public bool AutoSnap { get; set; } = true;

        /// <summary>是否在收起或展开桌面分区时播放平滑过渡动画（默认关闭）。</summary>
        public bool MinifyAnimation { get; set; }

        /// <summary>创建当前基础设置对象的深拷贝。</summary>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                SchemaVersion = SchemaVersion,
                LaunchAtStartup = LaunchAtStartup,
                Language = NormalizeLanguage(Language),
                CheckForUpdates = CheckForUpdates,
                DataStoragePath = string.IsNullOrWhiteSpace(DataStoragePath)
                    ? AppSettingsManager.GetDefaultDataDirectory()
                    : DataStoragePath.Trim(),
                AutoSave = AutoSave,
                AutoSnap = AutoSnap,
                MinifyAnimation = MinifyAnimation
            };
        }

        /// <summary>校正反序列化后的字段取值范围与默认回退值。</summary>
        public void Normalize()
        {
            SchemaVersion = CurrentSchemaVersion;
            Language = NormalizeLanguage(Language);
            if (string.IsNullOrWhiteSpace(DataStoragePath))
                DataStoragePath = AppSettingsManager.GetDefaultDataDirectory();
            else
                DataStoragePath = DataStoragePath.Trim();
        }

        /// <summary>判断两份基础配置在业务语义上是否完全一致。</summary>
        public bool ContentEquals(AppSettings other)
        {
            if (other == null)
                return false;

            return LaunchAtStartup == other.LaunchAtStartup &&
                   string.Equals(NormalizeLanguage(Language), NormalizeLanguage(other.Language), StringComparison.OrdinalIgnoreCase) &&
                   CheckForUpdates == other.CheckForUpdates &&
                   PathsEqual(DataStoragePath, other.DataStoragePath) &&
                   AutoSave == other.AutoSave &&
                   AutoSnap == other.AutoSnap &&
                   MinifyAnimation == other.MinifyAnimation;
        }

        /// <summary>规范化语言代码，未知值回退到系统语言或简体中文。</summary>
        public static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return DetectSystemLanguage();

            return LocalizationManager.ResolveCultureToLanguageCode(language);
        }

        /// <summary>根据当前操作系统区域设置推断默认语言代码。</summary>
        public static string DetectSystemLanguage()
        {
            string name = CultureInfo.InstalledUICulture?.Name ??
                          CultureInfo.CurrentUICulture?.Name ??
                          "zh-CN";
            return LocalizationManager.ResolveCultureToLanguageCode(name);
        }

        private static bool PathsEqual(string left, string right)
        {
            string a = NormalizePathForCompare(left);
            string b = NormalizePathForCompare(right);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePathForCompare(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return Path.GetFullPath(path.Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }

    /// <summary>
    /// 应用程序全局基础设置管理器（单例）。
    /// 负责开机自启动注册表同步、多语言切换、存储目录校验及配置持久化。
    /// </summary>
    public sealed class AppSettingsManager
    {
        private const string SettingsFileName = "__app_settings.xml";
        private const string StartupRunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryValueName = "NoFences";
        private const long MaximumSettingsBytes = 1024 * 1024;

        private readonly object syncRoot = new object();
        private readonly string bootstrapSettingsPath;
        private AppSettings settings;

        private AppSettingsManager()
        {
            string defaultDir = GetDefaultDataDirectory();
            bootstrapSettingsPath = Path.Combine(defaultDir, SettingsFileName);
            settings = LoadSettings();
            ApplyCultureAndLanguage(settings.Language);
        }

        /// <summary>全局唯一实例。</summary>
        public static AppSettingsManager Instance { get; } = new AppSettingsManager();

        /// <summary>基础设置保存并生效后触发。</summary>
        public event EventHandler SettingsChanged;

        /// <summary>启动时显式初始化基础设置与界面语言。</summary>
        public static void Initialize()
        {
            var current = Instance.CurrentSettings;
            ApplyCultureAndLanguage(current.Language);
        }

        /// <summary>获取默认应用数据根目录（%LocalAppData%\NoFences）。</summary>
        public static string GetDefaultDataDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NoFences");
        }

        /// <summary>获取当前已保存生效的基础设置副本。</summary>
        public AppSettings CurrentSettings
        {
            get
            {
                lock (syncRoot)
                    return settings.Clone();
            }
        }

        /// <summary>获取当前生效的界面语言标识。</summary>
        public string Language
        {
            get
            {
                lock (syncRoot)
                    return settings.Language;
            }
        }

        /// <summary>获取当前是否启用自动保存。</summary>
        public bool AutoSaveEnabled
        {
            get
            {
                lock (syncRoot)
                    return settings.AutoSave;
            }
        }

        /// <summary>获取当前是否启用移动分区自动吸附。</summary>
        public bool AutoSnapEnabled
        {
            get
            {
                lock (syncRoot)
                    return settings.AutoSnap;
            }
        }

        /// <summary>获取当前是否启用收起/展开窗口过渡动画。</summary>
        public bool MinifyAnimationEnabled
        {
            get
            {
                lock (syncRoot)
                    return settings.MinifyAnimation;
            }
        }

        /// <summary>直接切换全局自动吸附开关并持久化。</summary>
        public void SetAutoSnap(bool enabled)
        {
            AppSettings snapshot = CurrentSettings;
            if (snapshot.AutoSnap == enabled)
                return;

            snapshot.AutoSnap = enabled;
            try
            {
                lock (syncRoot)
                {
                    SaveSettingsFile(bootstrapSettingsPath, snapshot);
                    string targetSettingsPath = Path.Combine(snapshot.DataStoragePath, SettingsFileName);
                    if (!string.Equals(bootstrapSettingsPath, targetSettingsPath, StringComparison.OrdinalIgnoreCase))
                    {
                        SaveSettingsFile(targetSettingsPath, snapshot);
                    }

                    settings = snapshot.Clone();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("SetAutoSnap persistence failed: " + ex);
                lock (syncRoot)
                {
                    settings.AutoSnap = enabled;
                }
            }

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>获取当前生效的数据保存目录。</summary>
        public string DataStoragePath
        {
            get
            {
                lock (syncRoot)
                    return settings.DataStoragePath;
            }
        }

        /// <summary>校验数据存储目录路径是否合法且具备可创建/可写条件。</summary>
        public static bool ValidateDataStoragePath(string path, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = ThemeText.Key(LKeys.ErrDataPathEmpty);
                return false;
            }

            string trimmed = path.Trim();
            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                errorMessage = ThemeText.Key(LKeys.ErrDataPathInvalidChars);
                return false;
            }

            try
            {
                if (!Path.IsPathRooted(trimmed))
                {
                    errorMessage = ThemeText.Key(LKeys.ErrDataPathNotAbsolute);
                    return false;
                }

                string fullPath = Path.GetFullPath(trimmed);
                string root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    errorMessage = ThemeText.Key(LKeys.ErrDataPathDriveMissing);
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ThemeText.Key(LKeys.ErrDataPathFormatPrefix) + ex.Message;
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 校验并原子化保存应用基础配置。若写盘、目录创建或注册表更新失败，
        /// 将返回 false 并通过 errorMessage 提供具体原因，不破坏内存中的已保存快照。
        /// </summary>
        public bool TrySaveAndApply(AppSettings candidate, out string errorMessage)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            AppSettings prepared = candidate.Clone();
            prepared.Normalize();

            if (!ValidateDataStoragePath(prepared.DataStoragePath, out errorMessage))
                return false;

            try
            {
                string fullTargetDir = Path.GetFullPath(prepared.DataStoragePath);
                Directory.CreateDirectory(fullTargetDir);
                VerifyDirectoryWritable(fullTargetDir);
                prepared.DataStoragePath = fullTargetDir;
            }
            catch (Exception ex)
            {
                errorMessage = ThemeText.Key(LKeys.ErrDataDirNotWritablePrefix) + ex.Message;
                return false;
            }

            try
            {
                SetStartupRegistry(prepared.LaunchAtStartup);
            }
            catch (Exception ex)
            {
                errorMessage = ThemeText.Key(LKeys.ErrStartupRegistryPrefix) + ex.Message;
                return false;
            }

            try
            {
                lock (syncRoot)
                {
                    SaveSettingsFile(bootstrapSettingsPath, prepared);
                    string targetSettingsPath = Path.Combine(prepared.DataStoragePath, SettingsFileName);
                    if (!string.Equals(bootstrapSettingsPath, targetSettingsPath, StringComparison.OrdinalIgnoreCase))
                    {
                        SaveSettingsFile(targetSettingsPath, prepared);
                    }

                    settings = prepared.Clone();
                }
            }
            catch (Exception ex)
            {
                errorMessage = ThemeText.Key(LKeys.ErrSaveConfigPrefix) + ex.Message;
                return false;
            }

            ApplyCultureAndLanguage(prepared.Language);
            errorMessage = null;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(bootstrapSettingsPath))
                {
                    var initial = new AppSettings
                    {
                        LaunchAtStartup = IsStartupRegistryEnabled(),
                        Language = AppSettings.DetectSystemLanguage(),
                        CheckForUpdates = true,
                        DataStoragePath = GetDefaultDataDirectory(),
                        AutoSave = true
                    };
                    initial.Normalize();
                    return initial;
                }

                var fileInfo = new FileInfo(bootstrapSettingsPath);
                if (fileInfo.Length <= 0 || fileInfo.Length > MaximumSettingsBytes)
                    return CreateFallbackSettings();

                var serializer = new XmlSerializer(typeof(AppSettings));
                var readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using (var stream = File.OpenRead(bootstrapSettingsPath))
                using (var reader = XmlReader.Create(stream, readerSettings))
                {
                    var loaded = serializer.Deserialize(reader) as AppSettings;
                    if (loaded == null)
                        return CreateFallbackSettings();

                    loaded.Normalize();
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("AppSettings load failed: " + ex);
                return CreateFallbackSettings();
            }
        }

        private static AppSettings CreateFallbackSettings()
        {
            var fallback = new AppSettings
            {
                LaunchAtStartup = IsStartupRegistryEnabled(),
                Language = AppSettings.DetectSystemLanguage(),
                CheckForUpdates = true,
                DataStoragePath = GetDefaultDataDirectory(),
                AutoSave = true
            };
            fallback.Normalize();
            return fallback;
        }

        private static void SaveSettingsFile(string filePath, AppSettings value)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = filePath + ".tmp";
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.Serialize(stream, value);
                stream.Flush(true);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempPath, filePath);
        }

        private static void VerifyDirectoryWritable(string directoryPath)
        {
            string probeFile = Path.Combine(directoryPath, ".nofences_write_test_" + Guid.NewGuid().ToString("N"));
            using (File.Create(probeFile, 1, FileOptions.DeleteOnClose))
            {
            }
        }

        private static bool IsStartupRegistryEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupRunRegistryPath, false))
                {
                    if (key == null)
                        return false;
                    string value = key.GetValue(StartupRegistryValueName) as string;
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void SetStartupRegistry(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupRunRegistryPath, true) ??
                                     Registry.CurrentUser.CreateSubKey(StartupRunRegistryPath))
            {
                if (key == null)
                    throw new InvalidOperationException("Cannot open HKCU Run registry key.");

                if (enabled)
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(StartupRegistryValueName, "\"" + exePath + "\"", RegistryValueKind.String);
                }
                else
                {
                    if (key.GetValue(StartupRegistryValueName) != null)
                        key.DeleteValue(StartupRegistryValueName, false);
                }
            }
        }

        private static void ApplyCultureAndLanguage(string languageCode)
        {
            string normalized = AppSettings.NormalizeLanguage(languageCode);
            ThemeText.SetLanguage(normalized);
            try
            {
                var culture = CultureInfo.GetCultureInfo(normalized);
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch
            {
            }
        }
    }
}
