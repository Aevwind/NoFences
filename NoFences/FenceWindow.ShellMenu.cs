using NoFences.Theming;
using NoFences.Win32;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences
{
    public partial class FenceWindow
    {
        private ShellMenuWorker folderShellMenuWorker;
        private Timer shellMenuWarmupTimer;
        private string shellMenuWarmupPath;
        private int folderShellMenuRequest;
        private bool folderShellMenuLoading;
        private bool folderShellMenuShowing;

        private ShellMenuWorker GetFolderShellMenuWorker()
        {
            if (folderShellMenuWorker == null)
            {
                folderShellMenuWorker = new ShellMenuWorker();
                folderShellMenuWorker.MenuClosed += FolderShellMenu_MenuClosed;
            }
            return folderShellMenuWorker;
        }

        /// <summary>在图标上短暂停留后准备菜单，移动经过的图标不会立即加载扩展。</summary>
        private void QueueShellMenuWarmup(string path)
        {
            if (!fenceInfo.IsFolderPanel || folderShellMenuLoading || folderShellMenuShowing ||
                string.Equals(path, shellMenuWarmupPath, StringComparison.OrdinalIgnoreCase)) return;
            shellMenuWarmupPath = path;
            shellMenuWarmupTimer?.Stop();
            if (path == null) return;
            if (shellMenuWarmupTimer == null)
            {
                shellMenuWarmupTimer = new Timer { Interval = 250 };
                shellMenuWarmupTimer.Tick += ShellMenuWarmupTimer_Tick;
            }
            shellMenuWarmupTimer.Start();
        }

        private async void ShellMenuWarmupTimer_Tick(object sender, EventArgs e)
        {
            shellMenuWarmupTimer.Stop();
            string path = shellMenuWarmupPath;
            if (folderPanelClosed || path == null || folderShellMenuLoading || folderShellMenuShowing ||
                isFolderFileDrag || isContextMenuInteraction) return;
            string version;
            if (!folderEntryVersions.TryGetValue(path, out version)) return;
            try { await GetFolderShellMenuWorker().PrepareAsync(path, folderDirectories.Contains(path), version); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Shell menu warmup: " + ex.Message); }
        }

        /// <summary>菜单加载期间保持 UI 响应；外部点击、切换窗口或关闭分区会取消迟到的菜单。</summary>
        private async void ShowFolderShellMenu(string path, Point position)
        {
            CancelFolderShellMenu();
            shellMenuWarmupTimer?.Stop();
            int request = ++folderShellMenuRequest;
            folderShellMenuLoading = true;
            BeginContextMenuInteraction();
            try
            {
                string version;
                folderEntryVersions.TryGetValue(path, out version);
                ShellMenuWorker worker = GetFolderShellMenuWorker();
                IntPtr owner = await worker.PrepareAsync(path, folderDirectories.Contains(path), version);
                if (request != folderShellMenuRequest || folderPanelClosed || IsDisposed ||
                    !isContextMenuInteraction || interactedFence != this) return;
                folderShellMenuLoading = false;
                folderShellMenuShowing = true;
                BeginContextMenuInteraction(owner);
                await worker.ShowAsync(position, request);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (request == folderShellMenuRequest && !folderPanelClosed && !IsDisposed)
                {
                    folderShellMenuLoading = false;
                    folderShellMenuShowing = false;
                    EndContextMenuInteraction(keepInFront: false);
                    MessageBox.Show(this, ThemeText.NativeMenuFailed + Environment.NewLine + ex.Message,
                        ThemeText.NativeMenuTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (request == folderShellMenuRequest && !folderPanelClosed && !IsDisposed)
                {
                    folderShellMenuLoading = false;
                    folderShellMenuShowing = false;
                    shellMenuWarmupPath = null;
                    EndContextMenuInteraction(keepInFront: false);
                    folderPanelSource?.RequestRefresh();
                }
            }
        }

        /// <summary>在 Shell 执行命令之前回到 UI 线程，先归还面板的桌面层级。</summary>
        private void FolderShellMenu_MenuClosed(int request)
        {
            if (folderPanelClosed || IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                Invoke(new Action(() =>
                {
                    if (request != folderShellMenuRequest || folderPanelClosed || IsDisposed) return;
                    folderShellMenuShowing = false;
                    EndContextMenuInteraction(keepInFront: false);
                }));
            }
            catch (InvalidOperationException) { }
        }

        private void CancelFolderShellMenu(bool keepInFront = false)
        {
            if (!folderShellMenuLoading && !folderShellMenuShowing) return;
            ++folderShellMenuRequest;
            folderShellMenuLoading = false;
            folderShellMenuShowing = false;
            folderShellMenuWorker?.Cancel();
            EndContextMenuInteraction(keepInFront);
        }

        private void DisposeFolderShellMenu()
        {
            CancelFolderShellMenu();
            shellMenuWarmupTimer?.Dispose();
            shellMenuWarmupTimer = null;
            if (folderShellMenuWorker == null) return;
            folderShellMenuWorker.MenuClosed -= FolderShellMenu_MenuClosed;
            folderShellMenuWorker.Dispose();
            folderShellMenuWorker = null;
        }
    }
}
