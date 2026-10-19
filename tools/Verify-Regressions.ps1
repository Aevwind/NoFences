#requires -Version 5.1
# 在 Windows PowerShell 5.1 下使用隔离目录验证构建产物，不显示窗口或修改桌面文件。
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script with Windows PowerShell 5.1 (powershell.exe).'
}
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies ([System.Management.Automation.PSObject].Assembly.Location) -TypeDefinition @'
using System.Reflection;
using System.Management.Automation;
/// <summary>解包测试脚本传给反射调用的 PowerShell 包装对象。</summary>
public static class ReviewInvoke
{
    public static object Call(MethodInfo method, object target, object[] arguments)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            while (arguments[i] is PSObject)
                arguments[i] = ((PSObject)arguments[i]).BaseObject;
        }
        return method.Invoke(target, arguments);
    }
}
'@
$validationAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\NoFences\bin\Release\NoFences.exe'))
$validationRoot = Join-Path (Join-Path $PSScriptRoot '..\NoFences\obj') ('review-sandbox-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($validationRoot)
$validationStore = $validationAssembly.GetType('NoFences.Util.XmlFileStore', $true)
$validationPassed = 0

function Assert-Review($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:validationPassed++
}
function Invoke-Store([string]$method, [Type]$type, [object[]]$arguments) {
    $methodInfo = $validationStore.GetMethod($method)
    if ($methodInfo.IsGenericMethodDefinition) { $methodInfo = $methodInfo.MakeGenericMethod([Type[]]@($type)) }
    return [ReviewInvoke]::Call($methodInfo, $null, $arguments)
}
function Assert-Failure([scriptblock]$action, [string]$message) {
    $failed = $false
    try { & $action | Out-Null } catch { $failed = $true }
    Assert-Review $failed $message
}
function Set-ReviewField($instance, [string]$name, $value) {
    $instance.GetType().GetField($name, [Reflection.BindingFlags]'Instance,NonPublic').SetValue($instance, $value.PSObject.BaseObject)
}
function Invoke-ReviewPrivate($instance, [string]$name) {
    return $instance.GetType().GetMethod($name, [Reflection.BindingFlags]'Instance,NonPublic').Invoke($instance, @())
}

# 原子替换、备份、锁文件失败和临时文件清理。
$configPath = Join-Path $validationRoot 'settings.xml'
$firstSettings = New-Object NoFences.Model.AppSettings
$firstSettings.Language = 'en-US'
Invoke-Store Save ([NoFences.Model.AppSettings]) @($configPath, $firstSettings, [long]1048576)
$secondSettings = $firstSettings.Clone()
$secondSettings.Language = 'zh-TW'
Invoke-Store Save ([NoFences.Model.AppSettings]) @($configPath, $secondSettings, [long]1048576)
$roundTrip = Invoke-Store Deserialize ([NoFences.Model.AppSettings]) @([IO.File]::ReadAllText($configPath), [long]1048576)
$backup = Invoke-Store Deserialize ([NoFences.Model.AppSettings]) @([IO.File]::ReadAllText($configPath + '.bak'), [long]1048576)
Assert-Review ($roundTrip.Language -eq 'zh-TW') 'Primary configuration did not round-trip.'
Assert-Review ($backup.Language -eq 'en-US') 'Previous configuration was not retained as backup.'
$savedBytes = [Convert]::ToBase64String([IO.File]::ReadAllBytes($configPath))
$locked = [IO.File]::Open($configPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try { Assert-Failure { Invoke-Store Save ([NoFences.Model.AppSettings]) @($configPath, $firstSettings, [long]1048576) } 'Locked file was reported as saved.' }
finally { $locked.Dispose() }
Assert-Review ($savedBytes -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($configPath))) 'Failed replacement changed the primary file.'
Assert-Review (@([IO.Directory]::GetFiles($validationRoot, '*.tmp')).Count -eq 0) 'Failed replacement left a temporary file.'
Assert-Failure { Invoke-Store Save ([NoFences.Model.AppSettings]) @($configPath, $firstSettings, [long]8) } 'Oversized serialized configuration was accepted.'
Assert-Review ($savedBytes -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($configPath))) 'Size rejection changed the primary file.'
Assert-Failure { Invoke-Store ReadText $null @($configPath, [long]8) } 'Oversized XML input was accepted.'
Assert-Failure { Invoke-Store Deserialize ([NoFences.Model.AppSettings]) @('<!DOCTYPE AppSettings [<!ENTITY value "x">]><AppSettings><Language>&value;</Language></AppSettings>', [long]1048576) } 'DTD input was accepted.'

# 通过隔离的管理器实例验证提交失败不会污染内存，且主文件损坏时使用备份。
$appManager = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NoFences.Model.AppSettingsManager])
Set-ReviewField $appManager syncRoot (New-Object object)
Set-ReviewField $appManager bootstrapSettingsPath $configPath
$initial = New-Object NoFences.Model.AppSettings
$initial.Normalize()
Set-ReviewField $appManager settings $initial
$candidate = $initial.Clone()
$candidate.AutoSave = $false
$errorMessage = $null
Assert-Review ($appManager.TrySaveAndApply($candidate, [ref]$errorMessage)) 'Isolated application settings save failed.'
Assert-Review (-not $appManager.CurrentSettings.AutoSave) 'Successful save did not commit the application snapshot.'
$locked = [IO.File]::Open($configPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $candidate.AutoSave = $true
    Assert-Review (-not $appManager.TrySaveAndApply($candidate, [ref]$errorMessage)) 'Failed application save reported success.'
    Assert-Review (-not $appManager.CurrentSettings.AutoSave) 'Failed application save changed the active snapshot.'
    Assert-Review (-not [string]::IsNullOrWhiteSpace($errorMessage)) 'Failed application save omitted the error.'
} finally { $locked.Dispose() }
[IO.File]::WriteAllText($configPath, '<broken')
$loadedSettings = Invoke-ReviewPrivate $appManager LoadSettings
Assert-Review ($loadedSettings.Language -eq 'zh-TW') 'Application backup recovery did not load the previous valid file.'

# 主题保存失败不改变内存或发送成功通知，旧主题与备份仍能加载。
$themePath = Join-Path $validationRoot 'theme.xml'
$theme = New-Object NoFences.Theming.ThemeSettings
Invoke-Store Save ([NoFences.Theming.ThemeSettings]) @($themePath, $theme, [long]1048576)
$theme.DarkModeEnabled = $true
Invoke-Store Save ([NoFences.Theming.ThemeSettings]) @($themePath, $theme, [long]1048576)
$themeManager = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NoFences.Theming.ThemeManager])
Set-ReviewField $themeManager syncRoot (New-Object object)
Set-ReviewField $themeManager settingsPath $themePath
Set-ReviewField $themeManager settings (New-Object NoFences.Theming.ThemeSettings)
Set-ReviewField $themeManager providers (New-Object 'System.Collections.Generic.Dictionary[string,NoFences.Theming.IThemeProvider]')
$script:themeEvents = 0
$themeHandler = [EventHandler]{ $script:themeEvents++ }
$themeManager.add_ThemeChanged($themeHandler)
$locked = [IO.File]::Open($themePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try { Assert-Failure { $themeManager.SetDarkMode($true) } 'Failed theme save did not report an error.' }
finally { $locked.Dispose() }
Assert-Review (-not $themeManager.DarkModeEnabled) 'Failed theme save changed active dark mode.'
Assert-Review ($script:themeEvents -eq 0) 'Failed theme save emitted a successful change event.'
[IO.File]::WriteAllText($themePath, '<broken')
$loadedTheme = Invoke-ReviewPrivate $themeManager LoadSettings
Assert-Review (-not $loadedTheme.DarkModeEnabled) 'Theme backup recovery failed.'

# 展开区域优先于下方条目，内容视口之外不命中。
$bounds = New-Object 'System.Collections.Generic.Dictionary[string,System.Drawing.Rectangle]'
$bounds.Add('next', [Drawing.Rectangle]::new(0, 100, 90, 90))
$bounds.Add('selected', [Drawing.Rectangle]::new(0, 0, 90, 170))
$hitMethod = $validationAssembly.GetType('NoFences.Util.EntryHitTest').GetMethod('FindPath')
$viewport = [Drawing.Rectangle]::new(0, 0, 200, 190)
$hit = [ReviewInvoke]::Call($hitMethod, $null, [object[]]@($bounds, 'selected', $viewport, [Drawing.Point]::new(20, 125)))
Assert-Review ($hit -eq 'selected') 'Expanded label hit the next row.'
$hit = [ReviewInvoke]::Call($hitMethod, $null, [object[]]@($bounds, 'selected', [Drawing.Rectangle]::new(0, 0, 200, 110), [Drawing.Point]::new(20, 125)))
Assert-Review ($null -eq $hit) 'Footer or clipped content was hit.'
$hit = [ReviewInvoke]::Call($hitMethod, $null, [object[]]@($bounds, $null, $viewport, [Drawing.Point]::new(20, 175)))
Assert-Review ($hit -eq 'next') 'Normal row hit failed.'

# 旧版 XML 契约和暂时不可访问的路径引用。
$metadata = '<FenceInfo><Id>e7ff2bc1-765e-4cd0-b4a4-b86aa66121c6</Id><Name>Legacy</Name><Width>640</Width><Height>480</Height><TitlePadding>7</TitlePadding><Files><string>Z:\temporarily-unavailable.txt</string></Files></FenceInfo>'
$fence = Invoke-Store Deserialize ([NoFences.Model.FenceInfo]) @($metadata, [long]1048576)
Assert-Review ($fence.Width -eq 640 -and $fence.TitlePaddingX -eq 7 -and $fence.Files.Count -eq 1) 'Legacy fence metadata contract changed.'
$entry = [NoFences.Model.FenceEntry]::FromPath('Z:\temporarily-unavailable.txt')
Assert-Review ($null -ne $entry -and $entry.Name -eq 'temporarily-unavailable') 'Unavailable reference was dropped.'
Assert-Review ($null -eq [NoFences.Model.FenceEntry]::FromPath(('bad' + [char]0 + 'path'))) 'Invalid path was accepted.'
$resourceNames = $validationAssembly.GetManifestResourceNames()
foreach ($formName in @('EditDialog','FenceWindow','HeightDialog')) {
    Assert-Review ($resourceNames -contains ('NoFences.' + $formName + '.resources')) ('Missing form resource: ' + $formName)
}

# 不显示窗口，检查未实现入口确实不可交互，未保存状态保留。
$dialog = New-Object NoFences.Theming.ThemeConfigurationDialog
try {
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    $updateSwitch = $dialog.GetType().GetField('checkUpdatesSwitch', $flags).GetValue($dialog)
    $pathPicker = $dialog.GetType().GetField('dataPathPicker', $flags).GetValue($dialog)
    Assert-Review (-not $updateSwitch.IsInteractive) 'Unsupported update switch is interactive.'
    Assert-Review (-not $pathPicker.IsInteractive) 'Unsupported data migration picker is interactive.'

    # 模拟基础设置提交成功、主题失败，再取消草稿；不调用实际设置单例的保存方法。
    $savedApp = $dialog.GetType().GetField('savedAppSettings', $flags).GetValue($dialog).Clone()
    $savedApp.AutoSave = -not $savedApp.AutoSave
    $savedMode = $dialog.GetType().GetField('savedDarkMode', $flags).GetValue($dialog)
    $darkSwitch = $dialog.GetType().GetField('darkModeSwitch', $flags).GetValue($dialog)
    $darkSwitch.Checked = -not $savedMode
    [void][ReviewInvoke]::Call($dialog.GetType().GetMethod('AcceptSavedAppSettings', $flags), $dialog, @($savedApp))
    Assert-Review (Invoke-ReviewPrivate $dialog HasUnsavedChanges) 'Partial save discarded the unsaved theme draft.'
    [void][ReviewInvoke]::Call($dialog.GetType().GetMethod('CancelButton_Click', $flags), $dialog, @($null, [EventArgs]::Empty))
    $revertedApp = $dialog.GetType().GetField('draftAppSettings', $flags).GetValue($dialog)
    Assert-Review ($revertedApp.AutoSave -eq $savedApp.AutoSave) 'Cancel reverted already committed application settings.'
    Assert-Review ($darkSwitch.Checked -eq $savedMode -and -not (Invoke-ReviewPrivate $dialog HasUnsavedChanges)) 'Cancel did not restore the unsaved theme draft.'
} finally { $dialog.Dispose() }

# 缩略图完成在 UI 上下文发布；位图可复用，移除条目时清理位图缓存。
$oldContext = [Threading.SynchronizationContext]::Current
$uiContext = New-Object Windows.Forms.WindowsFormsSynchronizationContext
[Threading.SynchronizationContext]::SetSynchronizationContext($uiContext)
$provider = New-Object NoFences.Util.ThumbnailProvider -ArgumentList 32
try {
    $imagePath = Join-Path $validationRoot 'broken.png'
    [IO.File]::WriteAllText($imagePath, 'not an image')
    $icon = $provider.GenerateIcon($imagePath)
    $bitmap = $provider.GetBitmap($icon)
    Assert-Review ([object]::ReferenceEquals($bitmap, $provider.GetBitmap($icon))) 'Repeated drawing recreated the same bitmap.'
    $cache = $provider.GetType().GetField('iconCache', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($provider)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ($cache[$imagePath].TargetSize -ne 32 -and [DateTime]::UtcNow -lt $deadline) {
        [Windows.Forms.Application]::DoEvents()
        [Threading.Thread]::Sleep(10)
    }
    Assert-Review ($cache[$imagePath].TargetSize -eq 32) 'Broken image did not complete its associated-icon fallback.'
    $provider.Remove($imagePath)
    $bitmapCache = $provider.GetType().GetField('bitmapCache', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($provider)
    Assert-Review ($bitmapCache.Count -eq 0 -and $cache.Count -eq 0) 'Removed entry retained bitmap or icon cache.'
} finally {
    $provider.Dispose()
    [Threading.SynchronizationContext]::SetSynchronizationContext($oldContext)
    $uiContext.Dispose()
}
Write-Output ('PASS: ' + $validationPassed + ' isolated regression assertions; sandbox: ' + $validationRoot)
