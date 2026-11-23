using Peter;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoFences.Win32
{
    /// <summary>
    /// 在独立 STA 消息线程上构建和显示原生文件菜单，防止慢速 Shell 扩展阻塞面板。
    /// 只缓存最后一次悬停的菜单；COM 接口、PIDL、菜单句柄始终由该线程拥有。
    /// </summary>
    internal sealed class ShellMenuWorker : IDisposable
    {
        private readonly TaskCompletionSource<Control> ready =
            new TaskCompletionSource<Control>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private readonly CancellationToken closingToken;
        private readonly object lifetimeGate = new object();
        private volatile bool disposed;
        private IntPtr owner;
        private int generation;
        private ShellContextMenu menu;
        private string cachedPath;
        private string cachedVersion;
        private bool cachedDirectory;
        private uint cachedClipboard;
        private long preparedAt;
        private int activeRequest;

        public event Action<int> MenuClosed;
        public IntPtr Handle => Interlocked.CompareExchange(ref owner, IntPtr.Zero, IntPtr.Zero);

        public ShellMenuWorker()
        {
            closingToken = closing.Token;
            var thread = new Thread(Run) { IsBackground = true, Name = "NoFences Shell menu" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void Run()
        {
            Control dispatcher = null;
            try
            {
                Application.OleRequired();
                dispatcher = new Control();
                IntPtr dispatchHandle = dispatcher.Handle;
                menu = new ShellContextMenu();
                menu.MenuClosed += (sender, args) => MenuClosed?.Invoke(activeRequest);
                Interlocked.Exchange(ref owner, menu.Handle);
                ready.TrySetResult(dispatcher);
                if (!disposed) Application.Run();
            }
            catch (Exception ex) { ready.TrySetException(ex); }
            finally
            {
                lock (lifetimeGate)
                {
                    disposed = true;
                    closing.Cancel();
                    closing.Dispose();
                }
                Interlocked.Exchange(ref owner, IntPtr.Zero);
                menu?.Dispose();
                dispatcher?.Dispose();
            }
        }

        /// <summary>合并过期的悬停请求；短期复用同一路径、内容版本及剪贴板状态的菜单。</summary>
        public Task<IntPtr> PrepareAsync(string path, bool directory, string version)
        {
            int request = Interlocked.Increment(ref generation);
            uint clipboard = GetClipboardSequenceNumber();
            return DispatchAsync(() =>
            {
                if (request != Volatile.Read(ref generation)) throw new OperationCanceledException();
                if (!string.Equals(cachedPath, path, StringComparison.OrdinalIgnoreCase) ||
                    cachedVersion != version || cachedDirectory != directory || cachedClipboard != clipboard ||
                    Stopwatch.GetTimestamp() - preparedAt > Stopwatch.Frequency * 3L)
                {
                    cachedPath = null;
                    menu.PrepareContextMenu(path, directory);
                    cachedPath = path;
                    cachedVersion = version;
                    cachedDirectory = directory;
                    cachedClipboard = clipboard;
                    preparedAt = Stopwatch.GetTimestamp();
                }
                if (request != Volatile.Read(ref generation)) throw new OperationCanceledException();
                return menu.Handle;
            });
        }

        public Task<bool> ShowAsync(Point position, int requestId)
        {
            int request = Volatile.Read(ref generation);
            return DispatchAsync(() =>
            {
                if (request != Volatile.Read(ref generation)) throw new OperationCanceledException();
                cachedPath = null;
                activeRequest = requestId;
                menu.ShowPreparedContextMenu(position);
                return true;
            });
        }

        private async Task<T> DispatchAsync<T>(Func<T> operation)
        {
            if (disposed) throw new OperationCanceledException();
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration cancellation;
            lock (lifetimeGate)
            {
                if (disposed) throw new OperationCanceledException();
                cancellation = closingToken.Register(() => completion.TrySetCanceled());
            }
            using (cancellation)
            {
                Control dispatcher = await ready.Task.ConfigureAwait(false);
                if (disposed) throw new OperationCanceledException();
                try
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (disposed) { completion.TrySetCanceled(); return; }
                        try { completion.TrySetResult(operation()); }
                        catch (OperationCanceledException) { completion.TrySetCanceled(); }
                        catch (Exception ex) { completion.TrySetException(ex); }
                    }));
                }
                catch (InvalidOperationException) { completion.TrySetCanceled(); }
                return await completion.Task.ConfigureAwait(false);
            }
        }

        /// <summary>立即废弃尚未显示的请求；已打开的菜单在自己的线程中结束跟踪。</summary>
        public void Cancel()
        {
            Interlocked.Increment(ref generation);
            IntPtr window = Handle;
            if (window != IntPtr.Zero) PostMessage(window, 0x001F, IntPtr.Zero, IntPtr.Zero);
        }

        public void Dispose()
        {
            lock (lifetimeGate)
            {
                if (disposed) return;
                disposed = true;
                closing.Cancel();
            }
            Cancel();
            // 不等待可能仍在执行的扩展；退出动作与 COM 清理仍由所属 STA 线程完成。
            ready.Task.ContinueWith(task =>
            {
                if (task.Status != TaskStatus.RanToCompletion) return;
                try { task.Result.BeginInvoke(new Action(Application.ExitThread)); }
                catch (InvalidOperationException) { }
            }, TaskScheduler.Default);
            ready.TrySetCanceled();
        }

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}
