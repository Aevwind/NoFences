# 原生菜单只准备、不显示；验证 STA 归属、缓存失效及取消/关闭时不会执行迟到请求。
Add-Type -ReferencedAssemblies @([Windows.Forms.Control].Assembly.Location) -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

/// <summary>在隔离目录中准备真实 Shell 菜单，验证异步调度和资源释放。</summary>
public static class ShellMenuRegression
{
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
    private static Task<IntPtr> Prepare(Type type, object worker, string path, bool directory, string version)
    {
        return (Task<IntPtr>)type.GetMethod("PrepareAsync").Invoke(worker, new object[] { path, directory, version });
    }
    private static void Complete(Task task)
    {
        try { task.Wait(15000); } catch (AggregateException) { }
        Check(task.IsCompleted, "Shell menu request did not finish.");
    }
    public static int Verify(Assembly assembly, string fixture)
    {
        assertions = 0;
        string file = Path.Combine(fixture, "shell-menu.txt");
        File.WriteAllText(file, "isolated shell menu fixture");
        Type type = assembly.GetType("NoFences.Win32.ShellMenuWorker", true);
        object worker = Activator.CreateInstance(type, true);
        var blocked = new ManualResetEvent(false);
        var release = new ManualResetEvent(false);
        try
        {
            Task<IntPtr> initial = Prepare(type, worker, file, false, "v1");
            Complete(initial);
            Check(initial.Status == TaskStatus.RanToCompletion && initial.Result != IntPtr.Zero,
                "Shell worker did not prepare the file menu.");
            object native = type.GetField("menu", Fields).GetValue(worker);
            FieldInfo popupField = native.GetType().GetField("preparedMenu", Fields);
            IntPtr firstPopup = (IntPtr)popupField.GetValue(native);
            Check(firstPopup != IntPtr.Zero, "Preparation did not retain a native menu.");
            Task<IntPtr> warm = Prepare(type, worker, file, false, "v1");
            Complete(warm);
            Check(warm.Status == TaskStatus.RanToCompletion && (IntPtr)popupField.GetValue(native) == firstPopup,
                "Immediate hover preparation was not reusable.");

            // 使用后台调度器检测运行线程，不把原始 COM 接口移到测试线程。
            var ready = (TaskCompletionSource<Control>)type.GetField("ready", Fields).GetValue(worker);
            Control dispatcher = ready.Task.Result;
            ApartmentState apartment = ApartmentState.Unknown;
            dispatcher.Invoke(new Action(() => apartment = Thread.CurrentThread.GetApartmentState()));
            Check(apartment == ApartmentState.STA && dispatcher.InvokeRequired,
                "Shell objects were not owned by a separate STA thread.");

            // 内容变化后原路径消失：必须重新解析，不能继续复用已失效的菜单。
            File.Delete(file);
            Task<IntPtr> missing = Prepare(type, worker, file, false, "deleted");
            Complete(missing);
            Check(missing.IsFaulted && (IntPtr)popupField.GetValue(native) == IntPtr.Zero,
                "Deleted file retained a stale native menu.");
            File.WriteAllText(file, "restored");
            Task<IntPtr> restored = Prepare(type, worker, file, false, "restored");
            Complete(restored);
            Check(restored.Status == TaskStatus.RanToCompletion, "Preparation failure poisoned later requests.");

            Task<IntPtr> folder = Prepare(type, worker, fixture, true, "folder");
            Complete(folder);
            Check(folder.Status == TaskStatus.RanToCompletion, "Shell worker did not prepare a directory menu.");

            string driveRoot = Path.GetPathRoot(fixture);
            if (!string.IsNullOrEmpty(driveRoot))
            {
                Task<IntPtr> rootMenu = Prepare(type, worker, driveRoot, true, "root");
                Complete(rootMenu);
                Check(rootMenu.IsFaulted && !(rootMenu.Exception.InnerException is NullReferenceException),
                    "Drive root directory menu preparation threw NullReferenceException.");
            }

            // 暂时阻塞工作队列，确定性地验证新请求淘汰旧请求和关闭取消。
            dispatcher.BeginInvoke(new Action(() => { blocked.Set(); release.WaitOne(15000); }));
            Check(blocked.WaitOne(5000), "Shell queue did not enter the cancellation fixture.");
            Task<IntPtr> obsolete = Prepare(type, worker, file, false, "obsolete");
            Task<IntPtr> newest = Prepare(type, worker, file, false, "newest");
            release.Set();
            Complete(obsolete);
            Complete(newest);
            Check(obsolete.IsCanceled && newest.Status == TaskStatus.RanToCompletion,
                "Obsolete hover request was executed instead of the latest target.");

            blocked.Reset();
            release.Reset();
            dispatcher.BeginInvoke(new Action(() => { blocked.Set(); release.WaitOne(15000); }));
            Check(blocked.WaitOne(5000), "Shell queue did not enter the close fixture.");
            Task<IntPtr> pending = Prepare(type, worker, file, false, "pending-close");
            ((IDisposable)worker).Dispose();
            Complete(pending);
            Check(pending.IsCanceled, "Closing the panel left a pending menu request alive.");
            release.Set();
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while ((IntPtr)type.GetProperty("Handle").GetValue(worker) != IntPtr.Zero && DateTime.UtcNow < deadline)
                Thread.Sleep(10);
            Check((IntPtr)type.GetProperty("Handle").GetValue(worker) == IntPtr.Zero,
                "Closed Shell worker retained its owner window.");
        }
        finally
        {
            release.Set();
            ((IDisposable)worker).Dispose();
            blocked.Dispose();
            release.Dispose();
        }
        return assertions;
    }
}
'@
$script:validationPassed += [ShellMenuRegression]::Verify($validationAssembly, [IO.Path]::GetFullPath($validationRoot))
