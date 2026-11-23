using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NoFences.Model
{
    /// <summary>一次完整的目录快照；读取失败时不发布不完整的文件清单。</summary>
    internal sealed class FolderPanelSnapshot
    {
        public Dictionary<string, string> Versions { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Directories { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Exception Error { get; private set; }

        public static FolderPanelSnapshot Read(string folderPath)
        {
            var result = new FolderPanelSnapshot();
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Path.IsPathRooted(folderPath))
                    throw new ArgumentException(nameof(folderPath));
                foreach (FileSystemInfo entry in new DirectoryInfo(folderPath).GetFileSystemInfos())
                {
                    if (entry is DirectoryInfo) result.Directories.Add(entry.FullName);
                    long length = entry is FileInfo file ? file.Length : 0;
                    result.Versions[entry.FullName] = entry.LastWriteTimeUtc.Ticks + ":" +
                        entry.CreationTimeUtc.Ticks + ":" + length + ":" + (int)entry.Attributes;
                }
            }
            catch (Exception ex)
            {
                result.Versions.Clear();
                result.Directories.Clear();
                result.Error = ex;
            }
            return result;
        }

        /// <summary>保留现有显示顺序，移除已不存在的项目，并按名称追加新项目。</summary>
        public List<string> MergeOrder(IEnumerable<string> previous)
        {
            var remaining = Versions.Keys.ToDictionary(p => p, p => p, StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (string path in previous ?? Enumerable.Empty<string>())
            {
                string actualPath;
                if (path != null && remaining.TryGetValue(path, out actualPath))
                {
                    // 使用磁盘上的实际大小写，使仅改变大小写的重命名也能更新显示名称。
                    result.Add(actualPath);
                    remaining.Remove(path);
                }
            }
            result.AddRange(remaining.Values.OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase));
            return result;
        }
    }

    /// <summary>
    /// 在后台读取目录并监控直接子项。定期重读补偿事件丢失和目录离线后的恢复，
    /// 同一来源最多运行一次读取；关闭后丢弃未完成结果，不阻塞 UI 等待磁盘。
    /// </summary>
    internal sealed class FolderPanelSource : IDisposable
    {
        private readonly string folderPath;
        private readonly Action<FolderPanelSnapshot> publish;
        private readonly object gate = new object();
        private readonly Timer pollTimer;
        private FileSystemWatcher watcher;
        private bool reading;
        private bool readAgain;
        private bool disposed;

        public FolderPanelSource(string folderPath, Action<FolderPanelSnapshot> publish)
        {
            this.folderPath = folderPath;
            this.publish = publish;
            pollTimer = new Timer(_ => RequestRefresh(), null, 0, 5000);
        }

        public void RequestRefresh()
        {
            lock (gate)
            {
                if (disposed) return;
                if (reading)
                {
                    readAgain = true;
                    return;
                }
                reading = true;
            }
            Task.Run((Action)ReadLoop);
        }

        private void ReadLoop()
        {
            while (true)
            {
                // 先订阅再枚举，避免首次读取与启用监控之间漏掉变更。
                EnsureWatcher();
                FolderPanelSnapshot snapshot = FolderPanelSnapshot.Read(folderPath);
                lock (gate)
                {
                    if (disposed) return;
                    if (snapshot.Error != null)
                    {
                        watcher?.Dispose();
                        watcher = null;
                    }
                    // 发布只负责排入 UI 队列；与 Dispose 互斥，防止关闭后继续发布。
                    try { publish(snapshot); }
                    catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
                    if (disposed) return;
                    if (!readAgain)
                    {
                        reading = false;
                        return;
                    }
                    readAgain = false;
                }
            }
        }

        private void EnsureWatcher()
        {
            lock (gate)
            {
                if (disposed || watcher != null) return;
            }
            FileSystemWatcher candidate = null;
            try
            {
                candidate = new FileSystemWatcher(folderPath)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
                };
                candidate.Created += OnChanged;
                candidate.Deleted += OnChanged;
                candidate.Changed += OnChanged;
                candidate.Renamed += OnChanged;
                candidate.Error += OnError;
                candidate.EnableRaisingEvents = true;
                lock (gate)
                {
                    if (!disposed)
                    {
                        watcher = candidate;
                        candidate = null;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Folder panel watcher: " + ex.Message); }
            finally { candidate?.Dispose(); }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => RequestRefresh();

        private void OnError(object sender, ErrorEventArgs e)
        {
            lock (gate)
            {
                if (ReferenceEquals(sender, watcher))
                {
                    watcher.Dispose();
                    watcher = null;
                }
            }
            RequestRefresh();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                pollTimer.Dispose();
                watcher?.Dispose();
                watcher = null;
            }
        }
    }
}
