using NoFences.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NoFences.Win32
{
    /// <summary>把真实文件交给 Windows 复制或移动，保留原生冲突、进度、取消和撤销行为。</summary>
    internal static class ShellFileTransfer
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileOperation
        {
            public IntPtr Owner;
            public uint Operation;
            public string From;
            public string To;
            public ushort Flags;
            [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
            public IntPtr NameMappings;
            public string ProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHFileOperationW(ref FileOperation operation);

        public static bool Transfer(IntPtr owner, IEnumerable<string> paths, string destination, bool move)
        {
            string target = NormalizePath(destination);
            if (!Directory.Exists(target))
                throw new DirectoryNotFoundException(target);
            string[] sources = PrepareSources(paths, target, move);
            if (sources.Length == 0) return true;

            var operation = new FileOperation
            {
                Owner = owner,
                Operation = move ? 1u : 2u,
                // Shell 要求路径列表以两个 NUL 结尾，且所有路径必须为绝对路径。
                From = string.Join("\0", sources) + "\0\0",
                To = target + "\0\0",
                Flags = 0x0040 // FOF_ALLOWUNDO；不关闭覆盖确认或错误界面。
            };
            if (!move && sources.Any(p => SamePath(Path.GetDirectoryName(p), target)))
                operation.Flags |= 0x0008; // 同目录复制时由 Shell 生成副本名称。
            int result = SHFileOperationW(ref operation);
            if (operation.Aborted) return false;
            if (result != 0)
                throw new IOException(LocalizationManager.Format(LKeys.FolderPanelTransferFailed, result.ToString("X")));
            return true;
        }

        /// <summary>校验真实路径，阻止把目录放入自身或子目录；同目录移动视为无操作。</summary>
        internal static string[] PrepareSources(IEnumerable<string> paths, string target, bool move)
        {
            string normalizedTarget = NormalizePath(target);
            string targetPrefix = normalizedTarget.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var sources = new List<string>();
            foreach (string path in paths ?? Enumerable.Empty<string>())
            {
                string source = NormalizePath(path);
                if (!File.Exists(source) && !Directory.Exists(source))
                    throw new FileNotFoundException(source);
                string sourcePrefix = source.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                if (targetPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(LocalizationManager.Get(LKeys.FolderPanelInvalidDrop));
                if (move && SamePath(Path.GetDirectoryName(source), normalizedTarget)) continue;
                if (!sources.Contains(source, StringComparer.OrdinalIgnoreCase)) sources.Add(source);
            }
            return sources.ToArray();
        }

        private static string NormalizePath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
                return full.TrimEnd('\\', '/');
            return full;
        }

        private static bool SamePath(string first, string second) =>
            string.Equals(first?.TrimEnd('\\', '/'), second?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
