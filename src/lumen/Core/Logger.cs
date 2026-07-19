using System;
using System.IO;

namespace Lumen.Core
{
    /// <summary>极简诊断日志。路径跟随数据根（与 profiles/settings/lang 同根）：
    /// 指针文件 lumen.location 指向的目录/lumen.log；无指针或目录不存在时回退 %TEMP%/lumen.log。
    /// 因此改「数据存储位置」或迁移时，日志一并跟随，无需单独配置。</summary>
    internal static class Logger
    {
        private static string ResolvePath()
        {
            try
            {
                var pointer = Path.Combine(AppContext.BaseDirectory, "lumen.location");
                if (File.Exists(pointer))
                {
                    var dir = File.ReadAllText(pointer).Trim();
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        return Path.Combine(dir, "lumen.log");
                }
            }
            catch { /* 读不到则回退默认 */ }
            return Path.Combine(Path.GetTempPath(), "lumen.log");
        }

        public static void Log(string msg)
        {
            try
            {
                var path = ResolvePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
            }
            catch { /* 诊断日志失败不应影响主流程 */ }
        }
    }
}
