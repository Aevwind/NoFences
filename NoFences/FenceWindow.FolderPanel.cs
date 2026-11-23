using NoFences.Localization;
using NoFences.Model;
using NoFences.Theming;
using NoFences.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NoFences
{
    public partial class FenceWindow
    {
        private ToolStripMenuItem folderPanelToolStripMenuItem;
        private ToolStripMenuItem pasteToFolderToolStripMenuItem;
        private FolderPanelSource folderPanelSource;
        private Dictionary<string, string> folderEntryVersions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> folderDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool folderUnavailable;
        private volatile bool folderPanelClosed;
        private bool isFolderFileDrag;

        /// <summary>在新建分区旁增加文件夹面板入口，并提供目录粘贴操作。</summary>
        private void InitializeFolderPanelMenu()
        {
            folderPanelToolStripMenuItem = new ToolStripMenuItem();
            folderPanelToolStripMenuItem.Click += CreateFolderPanel_Click;
            appContextMenu.Items.Insert(appContextMenu.Items.IndexOf(newFenceToolStripMenuItem) + 1,
                folderPanelToolStripMenuItem);
            pasteToFolderToolStripMenuItem = new ToolStripMenuItem();
            pasteToFolderToolStripMenuItem.Click += (s, e) => PasteToFolder();
            appContextMenu.Items.Insert(0, pasteToFolderToolStripMenuItem);
            ApplyFolderPanelMenuVisibility();
        }

        private void ApplyFolderPanelMenuVisibility()
        {
            iconManagementToolStripMenuItem.Visible = !fenceInfo.IsFolderPanel;
            deleteItemToolStripMenuItem.Visible = !fenceInfo.IsFolderPanel && hoveringItem != null;
            pasteToFolderToolStripMenuItem.Visible = fenceInfo.IsFolderPanel;
            pasteToFolderToolStripMenuItem.Enabled = fenceInfo.IsFolderPanel && !fenceInfo.Locked;
        }

        private void ApplyFolderPanelLocalization()
        {
            folderPanelToolStripMenuItem.Text = ThemeText.Key(LKeys.MenuFolderPanel);
            pasteToFolderToolStripMenuItem.Text = ThemeText.Key(LKeys.MenuPasteToFolder);
        }

        private void CreateFolderPanel_Click(object sender, EventArgs e)
        {
            using (var picker = new FolderBrowserDialog
            {
                Description = ThemeText.Key(LKeys.SelectFolderPanelFolder),
                ShowNewFolderButton = true
            })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try { FenceManager.Instance.CreateFolderPanel(picker.SelectedPath); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ThemeText.Key(LKeys.FolderPanelCreateFailed) +
                        Environment.NewLine + ex.Message, ThemeText.Key(LKeys.MenuFolderPanel),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>后台目录读取只发布快照，所有列表和图标缓存变更都回到 UI 线程。</summary>
        private void InitializeFolderPanelSource()
        {
            if (!fenceInfo.IsFolderPanel) return;
            fenceInfo.Files = new List<string>();
            folderPanelSource = new FolderPanelSource(fenceInfo.FolderPath, snapshot =>
            {
                if (folderPanelClosed || IsDisposed || Disposing || !IsHandleCreated) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (folderPanelClosed || IsDisposed || Disposing) return;
                        ApplyFolderSnapshot(snapshot);
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        private void ApplyFolderSnapshot(FolderPanelSnapshot snapshot)
        {
            bool unavailable = snapshot.Error != null;
            bool changed = folderUnavailable != unavailable;
            foreach (var entry in folderEntryVersions)
            {
                string version;
                if (!snapshot.Versions.TryGetValue(entry.Key, out version) || entry.Value != version)
                {
                    InvalidateEntry(entry.Key);
                    changed = true;
                }
            }
            List<string> paths = snapshot.MergeOrder(fenceInfo.Files);
            if (!paths.SequenceEqual(fenceInfo.Files, StringComparer.Ordinal)) changed = true;
            folderEntryVersions = snapshot.Versions;
            folderDirectories = snapshot.Directories;
            folderUnavailable = unavailable;
            if (!changed) return;
            fenceInfo.Files = paths;
            entryHitBounds.Clear();
            if (selectedItem != null)
                selectedItem = paths.FirstOrDefault(p => string.Equals(p, selectedItem, StringComparison.OrdinalIgnoreCase));
            if (hoveringItem != null)
                hoveringItem = paths.FirstOrDefault(p => string.Equals(p, hoveringItem, StringComparison.OrdinalIgnoreCase));
            Invalidate();
        }

        private void DrawFolderPanelStatus(Graphics graphics, ThemeDefinition theme, Rectangle viewport)
        {
            if (!fenceInfo.IsFolderPanel || !folderUnavailable || viewport.Width < 1 || viewport.Height < 1) return;
            Rectangle bounds = Rectangle.Inflate(viewport, -LogicalPixelsToDevice(12), -LogicalPixelsToDevice(12));
            if (bounds.Width < 1 || bounds.Height < 1) return;
            using (var brush = new SolidBrush(theme.ItemTextColor))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter })
                graphics.DrawString(ThemeText.Key(LKeys.FolderPanelUnavailable), iconFont, brush, bounds, format);
        }

        /// <summary>文件夹条目使用系统拖放；移动、复制或取消均由真实目标目录处理。</summary>
        private void BeginFolderFileDrag()
        {
            string path = dragCandidatePath;
            dragCandidatePath = null;
            suppressNextClick = true;
            isFolderFileDrag = true;
            Capture = false;
            ReturnFenceToDesktop();
            try
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { path });
                DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, ThemeText.Key(LKeys.MenuFolderPanel),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isFolderFileDrag = false;
                folderPanelSource?.RequestRefresh();
                if (!folderPanelClosed && !IsDisposed)
                {
                    if (!Bounds.Contains(MousePosition)) Minify();
                    Invalidate();
                }
            }
        }

        /// <summary>同卷默认移动，跨卷默认复制；Ctrl/Shift 可明确选择复制/移动。</summary>
        private DragDropEffects GetFolderDropEffect(DragEventArgs e)
        {
            if (fenceInfo.Locked || !e.Data.GetDataPresent(DataFormats.FileDrop)) return DragDropEffects.None;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return DragDropEffects.None;
            try
            {
                bool copy = (e.KeyState & 8) != 0;
                bool move = (e.KeyState & 4) != 0;
                if (copy && move) return DragDropEffects.None;
                if (!copy && !move)
                    move = paths.All(p => string.Equals(Path.GetPathRoot(p), Path.GetPathRoot(fenceInfo.FolderPath),
                        StringComparison.OrdinalIgnoreCase));
                // 默认操作不可用时采用来源支持的另一种；显式修饰键则保持用户选择。
                if ((e.KeyState & 12) == 0 && move && (e.AllowedEffect & DragDropEffects.Move) == 0)
                    move = false;
                else if ((e.KeyState & 12) == 0 && !move && (e.AllowedEffect & DragDropEffects.Copy) == 0)
                    move = true;
                var effect = move ? DragDropEffects.Move : DragDropEffects.Copy;
                return (e.AllowedEffect & effect) != 0 ? effect : DragDropEffects.None;
            }
            catch (ArgumentException) { return DragDropEffects.None; }
            catch (NotSupportedException) { return DragDropEffects.None; }
        }

        private bool TransferToFolder(string[] paths, bool move)
        {
            if (!fenceInfo.IsFolderPanel || fenceInfo.Locked) return false;
            ReturnFenceToDesktop();
            try { return ShellFileTransfer.Transfer(Handle, paths, fenceInfo.FolderPath, move); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, ThemeText.Key(LKeys.MenuFolderPanel),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally { folderPanelSource?.RequestRefresh(); }
        }

        private void PasteToFolder()
        {
            if (!fenceInfo.IsFolderPanel || fenceInfo.Locked) return;
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                var paths = data?.GetData(DataFormats.FileDrop) as string[];
                if (paths == null || paths.Length == 0) return;
                bool move = false;
                using (var effect = data.GetData("Preferred DropEffect") as MemoryStream)
                {
                    if (effect != null && effect.Length >= 4)
                        move = (BitConverter.ToInt32(effect.ToArray(), 0) & (int)DragDropEffects.Move) != 0;
                }
                if (TransferToFolder(paths, move) && move)
                {
                    try { Clipboard.Clear(); }
                    catch (Exception) { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, ThemeText.Key(LKeys.MenuFolderPanel),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
