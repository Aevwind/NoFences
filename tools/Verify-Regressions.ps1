#requires -Version 5.1
# 在 Windows PowerShell 5.1 下使用隔离目录验证构建产物，不显示窗口或修改桌面文件。
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script with Windows PowerShell 5.1 (powershell.exe).'
}
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([System.Management.Automation.PSObject].Assembly.Location, [Drawing.Rectangle].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
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

/// <summary>以原有逐项布局算法为参照，验证直接坐标计算与拖拽候选行裁剪。</summary>
public static class GridLayoutRegression
{
    public static int Verify(Assembly assembly)
    {
        Type type = assembly.GetType("NoFences.Util.IconGridLayout", true);
        MethodInfo getBounds = type.GetMethod("GetItemBounds");
        MethodInfo getScroll = type.GetMethod("GetScrollHeight");
        MethodInfo clampScroll = type.GetMethod("ClampScrollOffset");
        MethodInfo getRange = type.GetMethod("GetVisibleRange");
        MethodInfo findIndex = type.GetMethod("FindInsertionIndex");
        var random = new Random(20261020);
        int samples = 0;
        for (int scenario = 0; scenario < 400; scenario++)
        {
            int width = random.Next(8, 257), height = random.Next(12, 241), padding = random.Next(2, 61);
            int layoutWidth = scenario % 4 == 0 ? (width + padding) * random.Next(1, 9) : random.Next(0, 1801);
            var viewport = new Rectangle(random.Next(0, 80), random.Next(0, 80), random.Next(0, 1401), random.Next(0, 901));
            int count = random.Next(0, 301), scroll = random.Next(0, 20001);
            object grid = Activator.CreateInstance(type, new object[] { viewport, layoutWidth, width, height, padding, scroll });
            int x = padding, y = padding, bottom = 0;
            var legacyBounds = new Rectangle[count];
            for (int index = 0; index <= count; index++)
            {
                var expected = new Rectangle(viewport.X + x, viewport.Y + y - scroll, width, height);
                var actual = (Rectangle)getBounds.Invoke(grid, new object[] { index });
                if (actual != expected)
                    throw new InvalidOperationException("Grid bounds mismatch at scenario " + scenario + ", index " + index);
                if (index == count) break;
                legacyBounds[index] = expected;
                bottom = Math.Max(bottom, y + height);
                x += width + padding;
                if (x + width > layoutWidth) { x = padding; y += height + padding; }
            }
            int expectedScroll = Math.Max(0, bottom - viewport.Height);
            if ((int)getScroll.Invoke(grid, new object[] { count }) != expectedScroll)
                throw new InvalidOperationException("Grid scroll height mismatch at scenario " + scenario);
            int expectedOffset = Math.Max(0, Math.Min(scroll, expectedScroll));
            if ((int)clampScroll.Invoke(grid, new object[] { count }) != expectedOffset)
                throw new InvalidOperationException("Grid scroll clamp mismatch at scenario " + scenario);

            object[] range = { count, 0, 0 };
            getRange.Invoke(grid, range);
            int first = (int)range[1], end = (int)range[2];
            if (first < 0 || first > end || end > count)
                throw new InvalidOperationException("Grid visible range exceeded the entry list.");
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                if (first != 0 || end != 0) throw new InvalidOperationException("Empty viewport has visible entries.");
            }
            else
            {
                for (int index = 0; index < count; index++)
                {
                    Rectangle cell = legacyBounds[index];
                    cell.Inflate(2, 2);
                    if (cell.IntersectsWith(viewport) && (index < first || index >= end))
                        throw new InvalidOperationException("Visible grid entry was skipped at scenario " + scenario);
                }
                int columns = Math.Max(1, layoutWidth / (width + padding));
                int maximumRows = (int)Math.Ceiling(viewport.Height / (double)(height + padding)) + 4;
                if (end - first > maximumRows * columns)
                    throw new InvalidOperationException("Grid visible range traversed excessive offscreen rows.");
            }

            for (int sample = 0; sample < 30; sample++)
            {
                var point = new Point(random.Next(-200, 2001), random.Next(-200, 2001));
                if (count > 0 && sample < 12)
                {
                    Rectangle cell = legacyBounds[random.Next(count)];
                    point = new Point(cell.X + (sample % 3 - 1) * width / 2,
                        sample % 2 == 0 ? cell.Top - padding / 2 : cell.Bottom + padding / 2);
                }
                int expected = LegacyInsertion(legacyBounds, viewport, padding, point);
                int actual = (int)findIndex.Invoke(grid, new object[] { point, count });
                if (expected != actual)
                    throw new InvalidOperationException("Grid insertion mismatch at scenario " + scenario +
                        ", point " + point + ": expected " + expected + ", actual " + actual);
                samples++;
            }
        }
        return samples;
    }

    private static int LegacyInsertion(Rectangle[] items, Rectangle viewport, int padding, Point location)
    {
        if (items.Length == 0) return 0;
        int best = items.Length;
        long bestDistance = long.MaxValue;
        for (int index = 0; index < items.Length; index++)
        {
            Rectangle item = items[index];
            int centerX = item.Left + item.Width / 2;
            if (location.Y >= item.Top - padding / 2 && location.Y <= item.Bottom + padding / 2)
            {
                if (location.X < centerX) return index;
                best = index + 1;
            }
            long dx = (long)location.X - centerX, dy = (long)location.Y - (item.Top + item.Height / 2);
            long distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = location.X <= centerX ? index : index + 1;
            }
        }
        if (location.Y < viewport.Top) return 0;
        if (location.Y > viewport.Bottom) return items.Length;
        return Math.Max(0, Math.Min(items.Length, best));
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

# 含精确换行宽度、窄视口、滚动和未填满末行的差分验证。
$gridSamples = [GridLayoutRegression]::Verify($validationAssembly)
Assert-Review ($gridSamples -eq 12000) 'Grid layout differential verification did not complete.'

# 路径快照保持原数据及索引映射，列表未变时复用条目，变化后更新名称和顺序。
$snapshotType = $validationAssembly.GetType('NoFences.Util.FenceEntrySnapshot', $true)
$snapshot = [Activator]::CreateInstance($snapshotType, $true)
function Invoke-Snapshot([string]$name, [object[]]$arguments) {
    return [ReviewInvoke]::Call($snapshotType.GetMethod($name), $snapshot, $arguments)
}
$paths = New-Object 'System.Collections.Generic.List[string]'
$paths.Add('Z:\snapshot-a.txt')
$paths.Add($null)
$invalidPath = 'bad' + [char]0 + 'path'
$paths.Add($invalidPath)
$paths.Add('Z:\snapshot-b.txt')
$paths.Add('Z:\snapshot-c.txt')
Assert-Review (Invoke-Snapshot Synchronize (,$paths)) 'Initial entry snapshot was not created.'
$snapshotCount = $snapshotType.GetProperty('Count')
$snapshotItem = $snapshotType.GetProperty('Item')
Assert-Review ($snapshotCount.GetValue($snapshot) -eq 3 -and $paths.Count -eq 5) 'Snapshot dropped metadata paths or included invalid cells.'
Assert-Review ((Invoke-Snapshot ToSourceInsertionIndex @(1)) -eq 3 -and (Invoke-Snapshot ToSourceInsertionIndex @(3)) -eq 5) 'Display insertion index did not map to original metadata.'
Assert-Review ((Invoke-Snapshot ToDisplayInsertionIndex @(2)) -eq 1 -and (Invoke-Snapshot ToDisplayInsertionIndex @(5)) -eq 3) 'Insertion marker did not map to the visible grid.'
Assert-Review (Invoke-Snapshot ContainsSourcePath @($invalidPath)) 'Complete path membership omitted an invalid reference.'
$firstEntry = $snapshotItem.GetValue($snapshot, @(0))
Assert-Review (-not (Invoke-Snapshot Synchronize (,$paths)) -and [object]::ReferenceEquals($firstEntry, $snapshotItem.GetValue($snapshot, @(0)))) 'Unchanged list recreated cached entries.'
$paths[0] = 'Z:\snapshot-c.txt'
$paths[4] = 'Z:\snapshot-a.txt'
Assert-Review ((Invoke-Snapshot Synchronize (,$paths)) -and (Invoke-Snapshot IndexOfPath @('z:\SNAPSHOT-A.txt')) -eq 2) 'Reordering did not update display indices.'
$paths.RemoveAt(0)
[void](Invoke-Snapshot Synchronize (,$paths))
Assert-Review ($snapshotCount.GetValue($snapshot) -eq 2 -and -not (Invoke-Snapshot ContainsSourcePath @('Z:\snapshot-c.txt'))) 'Removed entry retained stale snapshot membership.'
$paths[3] = 'Z:\snapshot-renamed.txt'
[void](Invoke-Snapshot Synchronize (,$paths))
Assert-Review ($snapshotItem.GetValue($snapshot, @(1)).Name -eq 'snapshot-renamed') 'Renaming did not refresh the cached display name.'
$paths.Clear()
[void](Invoke-Snapshot Synchronize (,$paths))
Assert-Review ($snapshotCount.GetValue($snapshot) -eq 0 -and (Invoke-Snapshot ToSourceInsertionIndex @(0)) -eq 0) 'Empty snapshot retained old entries.'

# 仅调用窗口中不依赖原生句柄的布局方法，跳过窗体构造、桌面挂接、监控与保存。
$layoutFixture = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NoFences.FenceWindow])
[GC]::SuppressFinalize($layoutFixture)
$fixtureInfo = New-Object NoFences.Model.FenceInfo
$longPath = 'Z:\' + ('W' * 200) + '.txt'
$fixtureInfo.Files.Add($longPath)
for ($index = 1; $index -lt 100; $index++) { $fixtureInfo.Files.Add(('Z:\layout-' + $index + '.txt')) }
$fixtureHits = New-Object 'System.Collections.Generic.Dictionary[string,System.Drawing.Rectangle]'
$fixtureSnapshot = [Activator]::CreateInstance($snapshotType, $true)
$fixtureFont = [Drawing.Font]::new('Segoe UI', 12, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
$standardFormat = New-Object Drawing.StringFormat
$standardFormat.Alignment = [Drawing.StringAlignment]::Center
$standardFormat.Trimming = [Drawing.StringTrimming]::EllipsisCharacter
$standardFormat.FormatFlags = [Drawing.StringFormatFlags]::LineLimit
$expandedFormat = New-Object Drawing.StringFormat
$expandedFormat.Alignment = [Drawing.StringAlignment]::Center
$fixtureBitmap = [Drawing.Bitmap]::new(280, 300)
$fixtureGraphics = [Drawing.Graphics]::FromImage($fixtureBitmap)
$fixtureViewport = [Drawing.Rectangle]::new(20, 35, 220, 240)
$fixtureFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$prepareLayout = $layoutFixture.GetType().GetMethod('PrepareIconGridLayout', $fixtureFlags)
$resolveLayoutHit = $layoutFixture.GetType().GetMethod('HitTestEntryLayout', $fixtureFlags)
$gridBounds = $validationAssembly.GetType('NoFences.Util.IconGridLayout').GetMethod('GetItemBounds')
function Invoke-FixtureLayout {
    return [ReviewInvoke]::Call($prepareLayout, $layoutFixture, @($fixtureViewport))
}
function Invoke-FixtureHit($grid, [Drawing.Point]$location) {
    return [ReviewInvoke]::Call($resolveLayoutHit, $layoutFixture, @($fixtureGraphics, $grid, $fixtureViewport, $location))
}
try {
    Set-ReviewField $layoutFixture fenceInfo $fixtureInfo
    Set-ReviewField $layoutFixture entrySnapshot $fixtureSnapshot
    Set-ReviewField $layoutFixture entryHitBounds $fixtureHits
    Set-ReviewField $layoutFixture iconFont $fixtureFont
    Set-ReviewField $layoutFixture standardIconTextFormat $standardFormat
    Set-ReviewField $layoutFixture expandedIconTextFormat $expandedFormat
    Set-ReviewField $layoutFixture itemWidth 90
    Set-ReviewField $layoutFixture itemHeight 66
    Set-ReviewField $layoutFixture iconSize 32
    Set-ReviewField $layoutFixture textHeight 28
    Set-ReviewField $layoutFixture itemPadding 18
    Set-ReviewField $layoutFixture scrollOffset 999999
    Set-ReviewField $layoutFixture selectedItem $longPath
    $preparedGrid = Invoke-FixtureLayout
    $savedOffset = $layoutFixture.GetType().GetField('scrollOffset', $fixtureFlags).GetValue($layoutFixture)
    $overflowHeight = $layoutFixture.GetType().GetField('scrollHeight', $fixtureFlags).GetValue($layoutFixture)
    $lastCell = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(99))
    Assert-Review ($savedOffset -eq $overflowHeight -and $savedOffset -gt 0 -and $lastCell.Bottom -eq $fixtureViewport.Bottom) 'Window layout used an unclamped scroll offset.'

    Set-ReviewField $layoutFixture scrollOffset 0
    $fixtureHits.Add('stale', $fixtureViewport)
    $fixtureInfo.Files.Add('Z:\layout-added.txt')
    $preparedGrid = Invoke-FixtureLayout
    $nextRow = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(2))
    $overlapPoint = [Drawing.Point]::new($nextRow.X + 10, $nextRow.Y + 10)
    Assert-Review ($fixtureHits.Count -eq 0 -and (Invoke-FixtureHit $preparedGrid $overlapPoint) -eq $longPath) 'Rebuilt window layout hit the row below expanded text.'
    Set-ReviewField $layoutFixture selectedItem ''
    Assert-Review ((Invoke-FixtureHit $preparedGrid $overlapPoint) -eq $fixtureInfo.Files[2]) 'Fresh layout hit without a selected entry failed.'

    Set-ReviewField $layoutFixture selectedItem $longPath
    Set-ReviewField $layoutFixture scrollOffset 240
    $preparedGrid = Invoke-FixtureLayout
    $scrolledRow = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(6))
    $overlapPoint = [Drawing.Point]::new($scrolledRow.X + 10, $scrolledRow.Y + 10)
    Assert-Review ((Invoke-FixtureHit $preparedGrid $overlapPoint) -eq $longPath) 'Offscreen selected label lost hit priority over visible rows.'
    Assert-Review ($null -eq (Invoke-FixtureHit $preparedGrid ([Drawing.Point]::new($overlapPoint.X, $fixtureViewport.Top - 1)))) 'Expanded label hit the title outside the content viewport.'
    Assert-Review ($null -eq (Invoke-FixtureHit $preparedGrid ([Drawing.Point]::new($fixtureViewport.Right - 1, $overlapPoint.Y)))) 'Blank grid space selected an entry.'

    $fixtureInfo.Files.RemoveRange(1, $fixtureInfo.Files.Count - 1)
    $preparedGrid = Invoke-FixtureLayout
    $firstCell = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(0))
    $savedOffset = $layoutFixture.GetType().GetField('scrollOffset', $fixtureFlags).GetValue($layoutFixture)
    Assert-Review ($savedOffset -eq 0 -and $firstCell.Top -eq $fixtureViewport.Top + 18) 'Entry removal left a blank first frame at the old scroll offset.'

    $fixtureInfo.Files[0] = 'Z:\short.txt'
    $fixtureInfo.Files.Add('Z:\short-second.txt')
    $fixtureInfo.Files.Add('Z:\short-next-row.txt')
    Set-ReviewField $layoutFixture selectedItem $fixtureInfo.Files[0]
    $preparedGrid = Invoke-FixtureLayout
    $nextRow = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(2))
    Assert-Review ((Invoke-FixtureHit $preparedGrid ([Drawing.Point]::new($nextRow.X + 10, $nextRow.Y + 10))) -eq $fixtureInfo.Files[2]) 'Single-line selection expanded into the next row.'
    $firstCell = [ReviewInvoke]::Call($gridBounds, $preparedGrid, @(0))
    Assert-Review ((Invoke-FixtureHit $preparedGrid ([Drawing.Point]::new($firstCell.X - 2, $firstCell.Y + 5))) -eq $fixtureInfo.Files[0]) 'Painted selection border did not share the entry hit bounds.'
} finally {
    $fixtureGraphics.Dispose()
    $fixtureBitmap.Dispose()
    $fixtureFont.Dispose()
    $standardFormat.Dispose()
    $expandedFormat.Dispose()
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
Write-Output ('PASS: ' + $validationPassed + ' isolated regression assertions; ' + $gridSamples + ' grid differential samples; sandbox: ' + $validationRoot)
