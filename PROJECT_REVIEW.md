# 项目审查与处理结果

初次审查：2026-10-19。处理日期：2026-10-20。初次范围为 44 个 C# 文件、10 个 `.resx` 及项目配置和说明文档；清理后保留 43 个 C# 文件和 3 份窗体资源，并增加独立的隔离回归脚本。

## 功能问题处理

| 审查项 | 处理结果 | 相关实现 |
| --- | --- | --- |
| 数据保存目录与真实目录不一致 | 当前版本明确不支持目录迁移，目录控件只读显示真实位置；旧 XML 字段保留，加载时归一为实际默认目录，不再向未生效的目录写配置副本。没有搬移现有数据 | [AppSettingsManager](NoFences/Model/AppSettingsManager.cs)、[设置窗口](NoFences/Theming/ThemeConfigurationDialog.cs) |
| 关闭自动保存后仍延迟写盘 | 布局保存排队和执行时均检查开关，关闭时取消排队任务；条目操作和快捷方式恢复事务继续强制持久化 | [ScheduleBoundsSave](NoFences/FenceWindow.cs) |
| 缩放滑块提交遗漏缩略图目标尺寸 | 直接比较提供器 `TargetSize` 与最终 `iconSize`，预览后提交相同数值也会同步 | [ApplyIconMetrics](NoFences/FenceWindow.cs) |
| 基础配置替换与部分失败保护不足 | 同目录唯一临时文件、刷新后原子替换、保留 `.bak`，失败保留原主文件并清理临时文件。取消无效配置副本写入；本次自启动注册表变更在写盘失败时恢复原值和类型，内存快照保持原状 | [XmlFileStore](NoFences/Util/XmlFileStore.cs)、[AppSettingsManager](NoFences/Model/AppSettingsManager.cs) |
| Shell 菜单原始 COM 指针未释放 | 桌面和父文件夹原始接口指针均在 `finally` 释放；父文件夹 PIDL 和字符串缓冲区也覆盖异常/提前返回路径 | [ShellContextMenu](NoFences/Win32/ShellContextMenu.cs) |
| 主题写盘失败仍提示成功 | 先保存草稿再替换生效快照，失败上抛且不发送成功变更事件；设置页保留未保存主题，菜单切换失败恢复实际勾选状态。主题主配置损坏时回退备份 | [ThemeManager](NoFences/Theming/ThemeManager.cs)、[设置窗口](NoFences/Theming/ThemeConfigurationDialog.cs) |
| 检查更新开关没有实际服务 | 明确标为未支持并禁止交互，旧序列化属性保留并归一为关闭；三种语言同步说明。尚未增加更新服务 | [语言包](NoFences/Localization)、[设置窗口](NoFences/Theming/ThemeConfigurationDialog.cs) |
| 双击展开文字误打开下方条目 | 单击、悬停、拖拽候选和双击统一使用可见区域命中，选中条目的展开区域优先；打开操作移出绘制过程 | [EntryHitTest](NoFences/Util/EntryHitTest.cs)、[FenceWindow](NoFences/FenceWindow.cs) |

数据目录迁移和自动更新属于尚未实现的功能，本次处理的是不可兑现的界面入口；没有将它们宣称为已支持。

## 性能与职责整理

- `FenceEntry.FromPath` 仅校验路径语法，不再在每帧执行文件/目录存在性查询；窗口缓存路径引用和显示名称。缺失或暂时断开的路径保持可见，方便管理，避免网络磁盘断开时丢失引用。
- 保留完整网格布局与滚动高度计算，仅对可见行及需要展开的选中项执行条目绘制和图标提取。管理模式的多选集合依据完整文件列表维护，滚出可见区域不会取消选择。
- 首次返回通用占位图标，真实 Shell 图标和图片解码全部排入后台；图片解码失败时回退关联图标，避免绘制调用链同步访问磁盘或 Shell 扩展。
- 在图标提供器中缓存 `Icon.ToBitmap()` 的结果，供缩放、XP 选中染色和拖拽预览借用；替换图标、移除条目与关闭时成对释放图标/位图。
- 文件大小、类型、修改时间和创建时间排序在开始前一次性采集键值，比较阶段使用内存快照，并保留自然名称比较与稳定排序。
- 提取 `EntryHitTest` 和 `XmlFileStore` 两个独立职责，删除重复命中逻辑与过期辅助代码。更大范围的布局/窗口交互拆分可继续按独立职责推进，需配合真实桌面交互验证。

以上为源码层面的开销消除，尚未进行大规模图标场景的性能基准测试，不提供未经测量的提速比例。

## 已清理的代码与资源

- 未实例化的 `ThemedCheckBox`、`ThemedTabControl` 及专属内部实现。
- 空的 `FenceWindow_Load` 及 Designer 订阅，未使用的 `titleOffset` 和 `Extensions.Shrink`。
- 旧标题高度计算接口与常量，未调用的 DWM 阴影辅助入口、旧 Win32 字体/批量窗口位置接口与仅供旧逻辑使用的声明。
- Shell 默认命令旧方法和未接入的 `LocalWindowsHook` / `HookEventArgs` / `HookType` 辅助组；保留第三方来源、`Peter` 命名空间和 COM 接口布局。
- `FenceEntry` 不再通过文件/目录枚举分派打开，统一交给 Shell 异步启动，相应移除 `EntryType`。
- 6 份未嵌入的 `*.cs.resx` / `*.zh-CN.resx`，空的 `Properties/Resources` / `Properties/Settings` 及生成包装类；项目文件同步删除相关项。
- 6 个未消费的框架程序集引用，以及确认多余的 `using`。

没有删除序列化兼容属性、原生结构体字段、COM 接口方法或明确的主题扩展入口。`EmptyDropDownRenderer` 的空覆写仍用于抑制默认绘制。

## 验证

- 最终执行 `NoFences.sln /t:Rebuild /p:Configuration=Release /nologo /verbosity:minimal` 成功，退出码 0，无错误或警告输出，生成 `NoFences/bin/Release/NoFences.exe`。
- [tools/Verify-Regressions.ps1](tools/Verify-Regressions.ps1) 使用 .NET Framework / Windows PowerShell 5.1，在 `NoFences/obj` 中创建隔离数据，不显示窗口、不移动桌面文件、不修改实际自启动注册表。
- 提交前重建后通过 36 项隔离回归断言：XML 原子替换和备份、锁文件失败不破坏主文件、大小限制、DTD 拒绝、配置失败不污染快照、主配置损坏后的备份加载、展开命中优先级、视口裁剪、旧分区 XML、缺失路径保留、窗体资源、未支持入口禁用、部分保存后取消、位图复用和移除清理。另执行现有 CI 的持久化契约、x64 输出和主题模式检查，通过。
- 不引入测试框架或额外测试项目。重建后可重复运行：

  ```powershell
  msbuild NoFences.sln /t:Rebuild /p:Configuration=Release
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Regressions.ps1
  ```

用户于 2026-10-20 反馈已手工验证，暂未发现问题；未提供逐项验证记录，因此不能据此认定所有桌面场景均已覆盖。本轮自动验证未对实际用户分区运行文件操作。

## 提交前复审

- 复审保存失败保护、展开命中、可见行绘制、多选保持、异步结果校验、图标/位图释放及资源删除后的项目引用。
- 修复发现的部分保存问题：基础设置成功、主题保存失败后，基础设置的控件绑定快照必须同步提交，否则“取消”会把草稿覆盖回旧值。新增三项断言，验证主题草稿仍保留、已提交基础值不被取消覆盖、取消后不残留虚假脏状态。
- 清除 Shell 辅助代码删除后遗留的空区域标记和无用引用。

## 文档

`AGENTS.md` 已同步当前保存语义、缓存所有权、统一命中、可见行绘制、多选保持和验证方式，保留通用构建命令，不记录本机 Visual Studio 路径。`README.md` 已更新标题设置、侧栏分类、保存反馈及未支持功能说明。
