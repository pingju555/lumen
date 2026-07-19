using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Lumen.Formula
{
    /// <summary>
    /// P4-2 应用启动坞：枚举「开始菜单\Programs」与桌面目录下的 .lnk 快捷方式，
    /// 文件名为应用名、.lnk 路径可直接用 shell 启动。纯文件扫描 + Process 启动，
    /// 不依赖 WinRT（与 mi/mu 的 WinRT 路径解耦，攻击面最小）。
    /// </summary>
    public sealed class AppProvider
    {
        private List<string> _names = new();
        private List<string> _paths = new();
        private readonly object _lock = new();

        public AppProvider() => Refresh();

        public void Refresh()
        {
            var names = new List<string>();
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in AppDirs())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in SafeEnumerate(dir, "*.lnk"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (seen.Add(name)) { names.Add(name); paths.Add(file); }
                }
            }
            lock (_lock) { _names = names; _paths = paths; }
        }

        public int Count { get { lock (_lock) return _names.Count; } }

        public string NameAt(int idxZeroBased)
        {
            lock (_lock) return (idxZeroBased >= 0 && idxZeroBased < _names.Count) ? _names[idxZeroBased] : "";
        }

        public bool Launch(int idxZeroBased)
        {
            string p;
            lock (_lock) p = (idxZeroBased >= 0 && idxZeroBased < _paths.Count) ? _paths[idxZeroBased] : null;
            if (p == null) return false;
            try
            {
                Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                return true;
            }
            catch { return false; }
        }

        private static IEnumerable<string> AppDirs()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        }

        private static IEnumerable<string> SafeEnumerate(string dir, string pat)
        {
            try { return Directory.EnumerateFiles(dir, pat, SearchOption.AllDirectories); }
            catch { return Enumerable.Empty<string>(); }
        }
    }
}
