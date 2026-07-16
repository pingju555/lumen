using System;
using System.IO;

namespace Lumen.Core
{
    /// <summary>极简诊断日志。路径见 MEMORY.md：%TEMP%/lumen.log</summary>
    internal static class Logger
    {
        private static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lumen.log");

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
            }
            catch { /* 诊断日志失败不应影响主流程 */ }
        }
    }
}
