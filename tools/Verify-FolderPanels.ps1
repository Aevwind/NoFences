# 由 Verify-Regressions.ps1 在隔离目录中调用；不创建栅栏窗口或接触桌面文件。
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

/// <summary>在后台接收目录快照，避免从工作线程执行 PowerShell 脚本块。</summary>
public sealed class FolderPanelProbe : IDisposable
{
    private readonly object source;
    private readonly MethodInfo refresh;
    private object latest;
    private int calls;
    public int Calls { get { return Volatile.Read(ref calls); } }
    public object Latest { get { return Volatile.Read(ref latest); } }

    public FolderPanelProbe(Assembly assembly, string path)
    {
        Type snapshot = assembly.GetType("NoFences.Model.FolderPanelSnapshot", true);
        Type callbackType = typeof(Action<>).MakeGenericType(snapshot);
        Delegate callback = Delegate.CreateDelegate(callbackType, this, GetType().GetMethod("Receive"));
        Type type = assembly.GetType("NoFences.Model.FolderPanelSource", true);
        source = Activator.CreateInstance(type, new object[] { path, callback });
        refresh = type.GetMethod("RequestRefresh");
    }
    public void Receive(object snapshot)
    {
        Volatile.Write(ref latest, snapshot);
        Interlocked.Increment(ref calls);
    }
    public void Refresh() { refresh.Invoke(source, null); }
    public bool WaitFor(string path, bool present, bool unavailable)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(12);
        do
        {
            object snapshot = Latest;
            if (snapshot != null)
            {
                bool failed = snapshot.GetType().GetProperty("Error").GetValue(snapshot) != null;
                var versions = (IDictionary<string, string>)snapshot.GetType().GetProperty("Versions").GetValue(snapshot);
                if (failed == unavailable && (path == null || versions.ContainsKey(path) == present)) return true;
            }
            Thread.Sleep(20);
        } while (DateTime.UtcNow < deadline);
        return false;
    }
    public void Dispose() { ((IDisposable)source).Dispose(); }
}
'@

$folderRoot = [IO.Path]::GetFullPath((Join-Path $validationRoot 'folder-panel'))
[void][IO.Directory]::CreateDirectory($folderRoot)
$childFolder = Join-Path $folderRoot 'subfolder'
[void][IO.Directory]::CreateDirectory($childFolder)
$nestedFile = Join-Path $childFolder 'nested.txt'
[IO.File]::WriteAllText($nestedFile, 'Keep this file when removing a panel.')
$folderFile = Join-Path $folderRoot 'file.txt'
[IO.File]::WriteAllText($folderFile, 'one')
$folderShortcut = Join-Path $folderRoot 'example.url'
[IO.File]::WriteAllText($folderShortcut, '[InternetShortcut]')
$folderSnapshotType = $validationAssembly.GetType('NoFences.Model.FolderPanelSnapshot', $true)
$folderRead = $folderSnapshotType.GetMethod('Read')
function Read-PanelSnapshot([string]$path) { return $folderRead.Invoke($null, @($path)) }
$folderSnapshot = Read-PanelSnapshot $folderRoot
Assert-Review ($null -eq $folderSnapshot.Error -and $folderSnapshot.Versions.Count -eq 3) 'Folder panel did not enumerate direct files and directories.'
Assert-Review (-not $folderSnapshot.Versions.ContainsKey($nestedFile)) 'Folder panel recursively flattened subdirectories.'
Assert-Review ($folderSnapshot.Directories.Contains($childFolder) -and -not $folderSnapshot.Directories.Contains($folderFile)) 'Folder snapshot did not retain entry types for Shell menus.'
Assert-Review ($folderSnapshot.Versions.ContainsKey($folderShortcut) -and [IO.File]::Exists($folderShortcut)) 'Folder panel moved a shortcut during enumeration.'
$previousOrder = New-Object 'System.Collections.Generic.List[string]'
$previousOrder.Add($folderShortcut)
$previousOrder.Add((Join-Path $folderRoot 'stale.txt'))
$mergedOrder = $folderSnapshot.MergeOrder($previousOrder)
Assert-Review ($mergedOrder.Count -eq 3 -and $mergedOrder[0] -eq $folderShortcut) 'Folder refresh lost custom order or retained stale files.'
$firstVersion = $folderSnapshot.Versions[$folderFile]
[IO.File]::WriteAllText($folderFile, 'updated content')
$folderSnapshot = Read-PanelSnapshot $folderRoot
Assert-Review ($firstVersion -ne $folderSnapshot.Versions[$folderFile]) 'File edits did not invalidate the thumbnail version.'

$folderInfo = New-Object NoFences.Model.FenceInfo
$folderInfo.Id = [Guid]::NewGuid()
$folderInfo.Type = [NoFences.Model.FenceType]::Folder
$folderInfo.FolderPath = $folderRoot
$folderInfo.Name = 'Folder panel regression'
$folderInfo.Files = $mergedOrder
$serializedPanel = Join-Path $validationRoot 'folder-panel.xml'
Invoke-Store Save ([NoFences.Model.FenceInfo]) @($serializedPanel, $folderInfo, [long]1048576)
$panelXml = [IO.File]::ReadAllText($serializedPanel)
$reloadedPanel = Invoke-Store Deserialize ([NoFences.Model.FenceInfo]) @($panelXml, [long]1048576)
Assert-Review ($reloadedPanel.IsFolderPanel -and $reloadedPanel.FolderPath -eq $folderRoot) 'Folder binding did not survive serialization.'
Assert-Review ($reloadedPanel.Files.Count -eq 0 -and $panelXml -notmatch '<Files>') 'Live folder contents leaked into persistent metadata.'
Assert-Review (-not $fence.IsFolderPanel -and $fence.Type -eq [NoFences.Model.FenceType]::Icons) 'Legacy metadata changed into a folder panel.'

# 目录监控涵盖新增、重命名、删除、离线和自动重连；所有操作均限制在回归目录中。
$probe = New-Object FolderPanelProbe -ArgumentList $validationAssembly, $folderRoot
try {
    Assert-Review ($probe.WaitFor($folderFile, $true, $false)) 'Initial folder snapshot was not published.'
    $addedFile = Join-Path $folderRoot 'added.txt'
    [IO.File]::WriteAllText($addedFile, 'new')
    Assert-Review ($probe.WaitFor($addedFile, $true, $false)) 'Watcher missed a new file.'
    $renamedFile = Join-Path $folderRoot 'renamed.txt'
    [IO.File]::Move($addedFile, $renamedFile)
    Assert-Review ($probe.WaitFor($renamedFile, $true, $false) -and $probe.WaitFor($addedFile, $false, $false)) 'Watcher missed a renamed file.'
    [IO.File]::Delete($renamedFile)
    Assert-Review ($probe.WaitFor($renamedFile, $false, $false)) 'Watcher retained a deleted file.'
    $offlineFolder = [IO.Path]::GetFullPath((Join-Path $validationRoot 'folder-panel-offline'))
    $sandboxPrefix = [IO.Path]::GetFullPath($validationRoot).TrimEnd('\') + '\'
    if (-not $folderRoot.StartsWith($sandboxPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not $offlineFolder.StartsWith($sandboxPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Directory move escaped the regression sandbox.'
    }
    [IO.Directory]::Move($folderRoot, $offlineFolder)
    $probe.Refresh()
    Assert-Review ($probe.WaitFor($null, $false, $true)) 'Unavailable folder did not produce a recoverable error.'
    [IO.Directory]::Move($offlineFolder, $folderRoot)
    Assert-Review ($probe.WaitFor($folderFile, $true, $false)) 'Folder polling did not recover after reconnection.'
} finally { $probe.Dispose() }
$closedCalls = $probe.Calls
$probe.Refresh()
[Threading.Thread]::Sleep(100)
Assert-Review ($probe.Calls -eq $closedCalls) 'Closed folder source published another snapshot.'

# 验证真实文件操作的路径边界，不运行会显示原生进度/冲突窗口的 Shell 操作。
$transferType = $validationAssembly.GetType('NoFences.Win32.ShellFileTransfer', $true)
$prepareTransfer = $transferType.GetMethod('PrepareSources', [Reflection.BindingFlags]'Static,NonPublic')
Assert-Failure { [ReviewInvoke]::Call($prepareTransfer, $null, @([string[]]@($folderRoot), $childFolder, $true)) } 'Moving a folder into its own child was accepted.'
$noOpMove = [ReviewInvoke]::Call($prepareTransfer, $null, @([string[]]@($folderFile), $folderRoot, $true))
Assert-Review ($noOpMove.Length -eq 0) 'Same-directory move was not treated as a no-op.'
$noOpFolderMove = [ReviewInvoke]::Call($prepareTransfer, $null, @([string[]]@(($childFolder + '\')), ($folderRoot + '\'), $true))
Assert-Review ($noOpFolderMove.Length -eq 0) 'Same-directory folder move with trailing backslash was not treated as a no-op.'

# 菜单测试跳过窗体构造和桌面挂接；检查隐藏而不是仅禁用图标管理。
$panelMenuFixture = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NoFences.FenceWindow])
[GC]::SuppressFinalize($panelMenuFixture)
$panelMenu = New-Object Windows.Forms.ContextMenuStrip
$manageMenu = New-Object Windows.Forms.ToolStripMenuItem
$removeEntryMenu = New-Object Windows.Forms.ToolStripMenuItem
$newFenceMenu = New-Object Windows.Forms.ToolStripMenuItem
try {
    [void]$panelMenu.Items.Add($manageMenu)
    [void]$panelMenu.Items.Add($removeEntryMenu)
    [void]$panelMenu.Items.Add($newFenceMenu)
    Set-ReviewField $panelMenuFixture fenceInfo $folderInfo
    Set-ReviewField $panelMenuFixture appContextMenu $panelMenu
    Set-ReviewField $panelMenuFixture iconManagementToolStripMenuItem $manageMenu
    Set-ReviewField $panelMenuFixture deleteItemToolStripMenuItem $removeEntryMenu
    Set-ReviewField $panelMenuFixture newFenceToolStripMenuItem $newFenceMenu
    Invoke-ReviewPrivate $panelMenuFixture InitializeFolderPanelMenu
    Assert-Review (-not $manageMenu.Available -and -not $removeEntryMenu.Available) 'Folder panel still exposes reference-only icon operations.'
    $folderInfo.Type = [NoFences.Model.FenceType]::Icons
    Invoke-ReviewPrivate $panelMenuFixture ApplyFolderPanelMenuVisibility
    Assert-Review $manageMenu.Available 'Ordinary fence lost icon management.'
    $folderInfo.Type = [NoFences.Model.FenceType]::Folder

    $dropEffectMethod = $panelMenuFixture.GetType().GetMethod('GetFolderDropEffect', $fixtureFlags)
    $dropData = [Windows.Forms.DataObject]::new([Windows.Forms.DataFormats]::FileDrop, [string[]]@($folderFile))
    $bothEffects = [Windows.Forms.DragDropEffects]'Copy,Move'
    $defaultDrop = [Windows.Forms.DragEventArgs]::new($dropData, 0, 0, 0, $bothEffects, [Windows.Forms.DragDropEffects]::None)
    $copyDrop = [Windows.Forms.DragEventArgs]::new($dropData, 8, 0, 0, $bothEffects, [Windows.Forms.DragDropEffects]::None)
    $copyOnlyDrop = [Windows.Forms.DragEventArgs]::new($dropData, 0, 0, 0, [Windows.Forms.DragDropEffects]::Copy, [Windows.Forms.DragDropEffects]::None)
    Assert-Review ([ReviewInvoke]::Call($dropEffectMethod, $panelMenuFixture, @($defaultDrop)) -eq [Windows.Forms.DragDropEffects]::Move) 'Same-volume folder drop did not choose a real move.'
    Assert-Review ([ReviewInvoke]::Call($dropEffectMethod, $panelMenuFixture, @($copyDrop)) -eq [Windows.Forms.DragDropEffects]::Copy) 'Ctrl did not select copy for a folder drop.'
    Assert-Review ([ReviewInvoke]::Call($dropEffectMethod, $panelMenuFixture, @($copyOnlyDrop)) -eq [Windows.Forms.DragDropEffects]::Copy) 'Copy-only drag source was rejected.'
    $folderInfo.Locked = $true
    Assert-Review ([ReviewInvoke]::Call($dropEffectMethod, $panelMenuFixture, @($defaultDrop)) -eq [Windows.Forms.DragDropEffects]::None) 'Locked folder panel accepted a drop.'
    $folderInfo.Locked = $false
} finally { $panelMenu.Dispose() }

# 隔离管理器的数据根目录，确认删除面板只清理自己的元数据。
$panelManager = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NoFences.Model.FenceManager])
$panelDataRoot = Join-Path $validationRoot 'folder-panel-data'
Set-ReviewField $panelManager basePath $panelDataRoot
Set-ReviewField $panelManager metadataLock (New-Object object)
Set-ReviewField $panelManager loadedFences (New-Object 'System.Collections.Generic.List[NoFences.Model.FenceInfo]')
Set-ReviewField $panelManager fenceDirectories (New-Object 'System.Collections.Generic.Dictionary[NoFences.Model.FenceInfo,string]')
Set-ReviewField $panelManager desktopShortcutManager ([Activator]::CreateInstance($validationAssembly.GetType('NoFences.Model.DesktopShortcutManager'), $true))
$panelError = ''
Assert-Review (-not $panelManager.TryAddEntry($folderInfo, $nestedFile, [ref]$panelError)) 'Folder panel accepted a reference instead of a real file operation.'
Assert-Review (-not $panelManager.TryRemoveEntry($folderInfo, $folderFile, [ref]$panelError)) 'Folder panel silently removed a live entry from its list.'
$ordinaryInfo = New-Object NoFences.Model.FenceInfo
$ordinaryInfo.Files.Add($folderFile)
Assert-Review (-not $panelManager.TryMoveEntry($ordinaryInfo, $folderInfo, $folderFile, 0, [ref]$panelError) -and $ordinaryInfo.Files.Count -eq 1) 'Reference transfer to a folder panel mutated the source.'
$panelManager.UpdateFence($folderInfo)
Assert-Review ($panelManager.TryRemoveFence($folderInfo, [ref]$panelError)) ('Isolated panel removal failed: ' + $panelError)
Assert-Review ([IO.File]::Exists($folderFile) -and [IO.File]::Exists($nestedFile) -and [IO.File]::Exists($folderShortcut)) 'Removing a folder panel deleted real folder contents.'
Assert-Review (-not [IO.Directory]::Exists((Join-Path $panelDataRoot $folderInfo.Id.ToString()))) 'Panel metadata was not removed.'

$languageKeys = @([NoFences.Localization.LKeys].GetFields() | ForEach-Object { $_.GetRawConstantValue() })
foreach ($language in [NoFences.Localization.LocalizationManager]::GetRegisteredLanguages()) {
    Assert-Review ($language.Strings.Count -eq $languageKeys.Count) ('Language key count mismatch: ' + $language.Code)
    foreach ($key in $languageKeys) {
        Assert-Review ($language.Strings.ContainsKey($key)) ('Missing language key: ' + $language.Code + ' / ' + $key)
    }
}
