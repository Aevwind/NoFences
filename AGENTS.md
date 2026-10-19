# AGENTS.md

本文件为在此仓库中工作的 AI 助手提供指导。项目审查结果与清理候选见 [PROJECT_REVIEW.md](PROJECT_REVIEW.md)。修改代码前应核实相关实现，避免把历史文档描述当成当前行为。

## 构建与验证

- Windows 桌面程序，使用 WinForms、GDI+ 和 .NET Framework 4.8；没有 NuGet 包依赖。
- 仅支持 Visual Studio / MSBuild 解决方案构建，不使用 `dotnet` CLI：

  ```powershell
  msbuild NoFences.sln /p:Configuration=Release
  ```

- 解决方案平台名称是 `Any CPU`，项目的 Debug/Release `PlatformTarget` 均硬编码为 `x64`。不要直接传入未配置的 `/p:Platform=x64`。
- 输出为 `NoFences/bin/Release/NoFences.exe`。`msbuild` 不在 PATH 时可查找 Visual Studio 安装目录中的 MSBuild；缺少工具时明确报告未完成构建，不把语法检查当成构建成功。
- [NoFences.csproj](NoFences/NoFences.csproj) 是传统项目，新增/删除 `.cs` 或资源文件需要同步显式的 `Compile` / `EmbeddedResource` 项。
- 没有测试项目或测试框架。先构建 Release，再在 Windows PowerShell 5.1 下执行隔离回归脚本：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Regressions.ps1`。CI 在打包前也执行该脚本。脚本在 `NoFences/obj` 中创建临时数据，不显示窗口或修改桌面文件；桌面交互仍需按受影响功能手工验证。

## 模块职责

| 位置 | 职责 |
| --- | --- |
| [Program.cs](NoFences/Program.cs) | 单实例、初始化、恢复命令、应用退出恢复 |
| [FenceWindow.cs](NoFences/FenceWindow.cs) | 栅栏绘制、DPI、窗口层级、折叠、选择、拖拽、排序和菜单 |
| [Model/FenceManager.cs](NoFences/Model/FenceManager.cs) | 分区加载、元数据持久化、条目操作和跨分区移动 |
| [Model/DesktopShortcutManager.cs](NoFences/Model/DesktopShortcutManager.cs) | 桌面快捷方式托管、恢复与 Shell 操作后的状态校正 |
| [Model/AppSettingsManager.cs](NoFences/Model/AppSettingsManager.cs) | 基础设置、界面语言、自启动注册表和配置写盘 |
| [Theming](NoFences/Theming) | 主题、设置草稿与绑定、自绘控件、菜单布局和图标 |
| [Localization](NoFences/Localization) | 语言包、稳定词条键和语言回退 |
| [Util](NoFences/Util) / [Win32](NoFences/Win32) | 防抖、异步图标缓存、Shell/COM、桌面窗口及 DWM 接口 |
| [tools/Verify-Regressions.ps1](tools/Verify-Regressions.ps1) | 配置失败保护、备份恢复、命中、旧 XML 与图标缓存的隔离回归 |

## 持久化与兼容性

- 当前分区和主题的实际根目录固定为 `%LocalAppData%/NoFences/`：
  - `<guid>/__fence_metadata.xml`：分区元数据；`.bak` 是最近备份。
  - `<guid>/items/<记录 ID 的 N 格式>/<文件名>`：托管快捷方式。
  - `__app_settings.xml`：启动时读取的基础配置。
  - `__theme_settings.xml`：全局主题配置。
- 数据目录控件只读，显示真实使用的默认目录；`AppSettings.Normalize()` 将旧版未生效的自定义目录归一为实际目录。当前未实现数据迁移，也不再向无效目标目录写配置副本。实现迁移时需统一正常启动、恢复命令、主题、文件监控及已有托管路径，不能只替换一个 `basePath`。
- [FenceInfo](NoFences/Model/FenceInfo.cs) 的属性名直接对应 XML 节点，**不可删除或重命名**。新增属性应提供兼容旧配置的默认值，并处理可能为空的集合。删除/重命名可能造成数据丢失或恢复失败，不能依赖反序列化必然报错。
- 同样保护 `DesktopShortcutInfo`、`AppSettings`、`ThemeSettings`、`ThemeDefinition` 的序列化字段、枚举名称及已有主题 ID。修改配置模型时同步检查克隆、归一化、差异比较和旧配置迁移逻辑。
- `FenceInfo.TitlePadding` 是 `TitlePaddingX` 的旧格式兼容入口；`TitleLineHeight`、`ThemeDefinition.PreferDarkNativeMenus` 也是保留字段，不能因为当前绘制不读取而删除。`TitleLineHeight` 仍有历史初始化值，当前标题布局按固定行高 1 计算。
- `FenceManager.UpdateFence` 在同目录写唯一临时文件、刷新到磁盘，再通过 `File.Replace` 保留备份；加载校验目录 GUID 与元数据 ID、限制 4 MiB，并尝试备份回退。保留这些保护和异常后的临时文件清理。
- 基础设置和主题设置通过 [XmlFileStore](NoFences/Util/XmlFileStore.cs) 写同目录唯一临时文件、`Flush(true)` 后原子替换并保留 `.bak`；写盘失败上抛，加载主文件失败时尝试备份。两类配置限制 1 MiB，读取禁用 DTD/外部实体。不要改成先删除主文件再移动，或吞掉保存异常。
- 基础设置保存失败时保持原内存快照，并回滚本次修改的自启动注册表原值与类型；主题先成功写盘再替换快照和触发事件。设置窗口分别记录已经提交的基础设置与仍未保存的主题草稿；部分保存成功时同步基础设置的绑定快照，防止“取消”覆盖已提交值，不能显示虚假的保存成功。

## 桌面快捷方式事务

- 只托管用户桌面或公共桌面**根目录**中的 `.lnk`、`.url`、`.website`。普通文件、文件夹与子目录内文件仅保存路径引用。
- 状态为 `Restored` → `MovingToStorage` → `Managed` → `MovingToDesktop` → `Restored`。搬移前先持久化事务；移除条目先写 `KeepTracked=false`，删除分区先写 `PendingRemoval=true`。这些写盘操作不应受可选自动保存开关影响。
- 正常退出会恢复文件并保留跟踪，供下次启动重新托管；永久移除/恢复命令在恢复成功后解除跟踪。退出菜单和 `ApplicationExit` 都会触发恢复，必须保持幂等。
- 不覆盖不同内容的同名文件；重复副本仅在长度与 SHA-256 一致时清理。跨卷回退为复制、校验、删除，失败需保留可恢复状态。公共桌面恢复失败时可回退当前用户桌面，先保存新目标再搬移。
- `ValidateRecordLocation` 校验原路径位于桌面、托管路径位于当前记录专属 GUID 目录。删除分区前确认无遗留托管文件、无未知根目录内容；不要直接递归删除分区目录。
- 故障恢复命令：`NoFences.exe --restore-shortcuts [--silent]`。它不创建栅栏窗口，退出码为成功 `0`、恢复失败 `1`、已有实例运行 `2`。此入口在普通初始化分支之前执行。

## 窗口、DPI 与折叠

- [Program.cs](NoFences/Program.cs) 使用命名 `Mutex("No_fences")` 强制单实例；`Application.Run()` 无主窗体，各栅栏独立显示，最后一个窗口关闭时退出应用。
- [DesktopUtil.GlueToDesktop](NoFences/Win32/DesktopUtil.cs) 设置 `GWL_HWNDPARENT` 为 `Progman`，找不到时回退承载桌面的 `WorkerW`；不是把窗口改为普通子控件。`WS_EX_TOOLWINDOW` 隐藏 Alt+Tab 项。
- 当前交互栅栏可临时使用 `HWND_TOPMOST`，通过低级鼠标钩子和前台窗口事件在外部点击、Alt+Tab、菜单结束时归还桌面层级。保持 `KeepInteractedFenceOnTop`、`ReturnFenceToDesktop`、`PreserveFenceZOrder` 配套；关闭窗口时卸载钩子，委托生命周期覆盖原生回调。
- 不要在 `WM_SETFOCUS` 时把窗口推至 `HWND_BOTTOM`，会使重叠分区交替沉底。也不要让临时置顶状态遗留到普通应用窗口之上。
- `App.config` 与 `app.manifest` 均声明 `PerMonitorV2`。`FenceInfo.Width/Height`、位置使用设备像素，`LastDpi` 用于离线 DPI 变化后的缩放；标题字号、标题栏尺寸与内边距使用逻辑像素。
- `DevicePixelsToLogical` / `LogicalPixelsToDevice` 使用原生 `GetDpiForWindow(Handle)`。不能用会缓存旧值的 `CreateGraphics().DpiX`；.NET Framework 4.8 没有 `DeviceToLogicalUnits()`。
- 桌面实际间距来自 `SysListView32` 的 `LVM_GETITEMSPACING`；实际图标边长来自桌面 `IFolderView2.GetViewModeAndIconSize`（逻辑像素）。不要跨进程使用 Explorer 的 `HIMAGELIST` 句柄。
- `SPI_GETICONMETRICS` 用于字体与回退度量，不能反映 Ctrl+滚轮缩放。每个栅栏的 1.5 秒 UI 定时器补充轮询，并响应 `WM_DPICHANGED`、`WM_SETTINGCHANGE`。
- 从 `LOGFONT` 手动构造 `Font(..., GraphicsUnit.Pixel)`，保留字体粗细等样式。非 TrueType 桌面字体可能使 `Font.FromHfont()` 抛异常，不能改回该方式。
- `WM_NCHITTEST` 实现标题栏拖动与边缘/角落 10px 调整大小热区；锁定或右键按下时不处理。`WM_MOVING` 实现可选吸附。
- 标题栏支持顶、底、左、右四个方向。标题尺寸由 `HeightDialog.CalculateAdaptedTitleHeight` 根据字号与对应方向的内边距计算；标题设置实时预览，取消时恢复原配置。
- 折叠和动画期间，持久化使用 `GetCurrentExpandedBounds()`，不能保存收起后的宽高或偏移坐标。跨屏 DPI 变更也先还原展开矩形。180ms 过渡动画由全局设置控制，默认关闭。

## 渲染与交互规则

- 主窗口在 `FenceWindow_Paint` 中手动 GDI+ 绘制，**已经启用** `ControlStyles.OptimizedDoubleBuffer` 等样式；不要移除防闪烁设置。其他自绘控件也有各自的双缓冲设置。
- 图标单元格内边距保持 `itemWidth * 0.20`，并保留逻辑像素最小值。缩放范围为 10%~200%；设备像素、逻辑像素和比例缩放不要重复叠加。
- 标准图标文字最多两行；只有真实文字高度**超过两行区域**才展开。一行或刚好两行都不展开。测量完整高度使用 `SizeF(width, 9999)`，有限高度会让 `MeasureString` 截断测量结果。
- 展开文字使用两遍绘制：第一遍 `RenderEntry(skipText: true)`，循环后绘制展开高亮框 → 重绘图标 → 展开文字。展开条目的悬停和点击命中使用 `expandOutlineRect`，不能沿用标准两行矩形。
- 鼠标移动、拖拽候选和双击复用 [EntryHitTest](NoFences/Util/EntryHitTest.cs)，先检查内容视口，再优先检查选中项展开矩形。双击在鼠标事件中打开目标，绘制过程中不启动文件。
- 选择由点击设置 `shouldUpdateSelection` 后，在绘制前通过 `HitTestEntryLayout` 按本帧布局统一解析；不能依赖可能因列表变化或滚动校正失效的上一帧命中缓存。`RenderEntry` 仅绘制，不修改选择；`MouseLeave` 不取消选择。点击其他条目或空白区域改变选择，图标管理模式使用独立多选集合。
- 每帧读取一次客户区鼠标坐标并传给各条目；标准与展开区域的悬停均按内容视口裁剪，绘制边框与点击复用 `MeasureEntryHitBounds` 的尺寸。
- 拖动图标超过系统拖拽阈值后进入手动拖拽，支持同分区排序、跨分区移动、拖出移除；`Escape` 取消，拖拽后的点击需抑制。涉及托管条目时必须通过 `FenceManager` 的恢复/重新托管流程。
- [FenceEntry.Open](NoFences/Model/FenceEntry.cs) 使用不等待的 `Task.Run` 启动目标，避免 UI 线程阻塞。
- `FenceEntry.FromPath` 只校验路径语法，不在绘制时查询磁盘；缺失或暂时断开的路径继续保留显示。`FenceEntrySnapshot` 缓存可绘制条目及其原始列表索引，列表不变时复用对象；`FenceWindow` 只绘制可见行并在绘制前校正滚动偏移。多选集合按完整文件列表维护，不能因为条目滚出命中缓存就取消选择。排序先读取一次键值，再进行内存比较。
- 绘制、拖拽落点和插入标记共用 [IconGridLayout](NoFences/Util/IconGridLayout.cs)。列数保持 `max(1, layoutWidth / (itemWidth + itemPadding))`，纵向标题栏折叠时保留展开宽度；不要各自复制换行算法。拖拽仅扫描相邻完整行和末行，距离平方使用 `long`，未填满末行和行边界需通过差分回归。
- [ThrottledExecution](NoFences/Util/ThrottledExecution.cs) 是 **UI 线程的可取消防抖器**：WinForms `Timer` 每次 `Run` 重新计时，4 秒静默后保存最后一次操作；关闭时 `Flush` 或 `Cancel`，随后释放。它没有旧版 `volatile isAwaiting`，也不是跨线程调度工具。
- `ScheduleBoundsSave` 在排队和执行时均检查自动保存开关，关闭开关时取消待处理操作；仅限制布局延迟保存，文件添加、排序、移除与快捷方式事务仍必须持久化。
- [ThumbnailProvider](NoFences/Util/ThumbnailProvider.cs) 立即返回独占的通用占位图标，图片缩略图和真实 Shell 图标均在后台生成，`SemaphoreSlim(4)` 限制并发。解码失败回退关联图标。`TargetSize` 为设备像素，提交缩放时直接比较并同步提供器目标尺寸，不能只比较预览前后的布局尺寸。
- `GetBitmap` 缓存当前图标的位图副本；图标、位图均由提供器所有，绘制方只能借用，不能 `Dispose`。替换图标、移除条目和关闭时同步释放位图，避免每帧 `ToBitmap()` 或遗留旧版本缓存。
- 缩略图完成通过创建时捕获的 UI `SynchronizationContext` 发布；保留取消、旧任务结果校验和关闭时资源释放。删除/重命名/内容变更后用 `Remove(path)` 使缓存失效。构造提供器应处于有 WinForms 同步上下文的 UI 线程。

## 主题、设置、本地化与 Shell

- 主题 ID（`default`、`windows11`、`windowsxp`、`custom`）与全局明暗模式独立；自定义主题保留浅色和深色两份。保留旧主题版本迁移，不能把用户主动选择的 Win11 反复迁移为默认主题。
- `ThemeDefinition` 的颜色以 ARGB 整数持久化，便捷 `Color` 属性标记 `[XmlIgnore]`。消费者使用克隆快照，编辑草稿不应直接修改正在生效的主题。
- 设置中心使用 `SettingsCategoryDescriptor` / `SettingsGroupDescriptor` / `SettingsItemDescriptor` 和 `SettingsBinding<T>` 管理草稿、校验、回退与保存快照。新增设置需接通实际消费逻辑，不能只增加可保存的开关。
- 自动更新检查入口明确标为未支持且不可交互，旧 `CheckForUpdates` 字段保留并归一为 `false`。完成真实版本检查后才能启用；数据目录迁移同样需要完成后端后再开放编辑。
- 设置页调色板由 `SettingsPalette` 按明暗模式决定；应用菜单的几何结构由 `MenuThemeProfile` 决定，颜色来自主题。`ThemeUi` 递归应用所有子菜单并处理 DPI。
- 模糊圆角优先使用可用的 DWM 原生圆角；回退自定义 `Form.Region` 时避免同时保留不匹配的 DWM 投影。图标、背景图片、字体、Region 和菜单图片的所有权及释放要成对；共享字体缓存不能由单个控件释放。
- 用户可见文本通过 `ThemeText` / `LocalizationManager` / `LKeys` 获取；新增词条同步维护 `Lang.zh-CN.cs`、`Lang.zh-TW.cs`、`Lang.en-US.cs`。当前每种语言覆盖相同的 182 个键。
- [ShellContextMenu.cs](NoFences/Win32/ShellContextMenu.cs) 是第三方 CodeProject 来源代码，位于 **`Peter` 命名空间**，通过 `using Peter;` 导入；保留来源信息。COM 接口方法顺序、原生结构体布局和字段不可按“未引用”盲删。
- 条目普通右键显示 Windows Shell 菜单；Shift+右键或空白区域显示应用菜单。Shell 菜单只跟随原生明暗偏好，不使用应用自绘菜单的任意配色。
- `ShellContextMenu.MenuClosed` 早于所选命令执行，用于及时解除临时置顶；托管路径需在菜单返回后校正，并由 `FileSystemWatcher` 补充异步剪切、重命名、删除和内容变更。监控回调经 `BeginInvoke` 回到 UI，并检查窗口是否关闭。
- COM 原始接口指针、RCW 和 PIDL 分别管理；`GetTypedObjectForIUnknown` 不代替释放取得的原始指针。保留 `finally` 清理与异常路径处理。
- 手写源代码的 `<summary>` XML 文档注释使用中文；第三方来源/许可注释保留原文。WM 常量继续使用 `using static NoFences.Win32.WindowUtil;` 导入。`Properties/*.Designer.cs` 等生成文件应随对应设计源维护。

## 手工验证重点

- 文件操作：添加普通文件/目录与三类桌面快捷方式；重启、退出、移除、跨分区移动及恢复命令；同名冲突与只读/跨卷回退。
- 窗口交互：重叠分区点击、两类菜单、外部点击、Alt+Tab、Win+D、锁定、自动吸附和拖拽取消。
- DPI 与布局：不同 DPI 显示器间拖动、桌面 Ctrl+滚轮、10%~200% 缩放、四向标题栏、折叠动画及重启后的展开尺寸。
- 渲染：一行、两行和超两行文件名；展开后的选择/悬停命中；滚动、图标多选与拖拽指示。
- 设置：三种语言、各预设和自定义明暗主题、保存失败提示、取消预览、背景图片更换后的文件锁与资源释放。
- 当前数据目录迁移和自动更新服务仍未实现，界面应保持只读/禁用提示；不要在文档或界面中宣称已经支持。
