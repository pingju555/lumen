using System;
using System.IO;

namespace Lumen.Core
{
    /// <summary>
    /// 可配置的数据根目录：profiles / config / settings / lang 全部位于此目录下。
    /// 解析优先级（GUI 模式，无命令行覆盖）：
    ///   1) 程序目录旁的指针文件 <c>lumen.location</c>（用户经 GUI 设定）
    ///   2) 默认 %LocalAppData%/Lumen
    /// 指针文件置于 exe 旁（而非数据根内），故迁移数据本身不会带走指针。
    /// 日志（%TEMP%/lumen.log）与第三方缓存（网易云/QQ 音乐）不在此范围内。
    /// </summary>
    public static class LumenPaths
    {
        private const string PointerFile = "lumen.location";

        /// <summary>出厂默认数据根：%LocalAppData%/Lumen。</summary>
        public static string DefaultDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");

        /// <summary>当前生效的数据根（已校验存在性的绝对路径，不含末尾斜杠）。</summary>
        public static string DataDir
        {
            get
            {
                var p = ReadPointer();
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                    return p;
                return DefaultDataDir;
            }
        }

        public static string ConfigFilePath => Path.Combine(DataDir, "config.json");
        public static string ProfilesDir => Path.Combine(DataDir, "profiles");
        public static string MetaPath => Path.Combine(DataDir, "meta.json");
        public static string SettingsFilePath => Path.Combine(DataDir, "settings.json");
        public static string LangDir => Path.Combine(DataDir, "lang");

        /// <summary>写入指针文件，将后续数据根固定到 newDir（不立即迁移数据）。失败时记日志、静默忽略。</summary>
        public static void SetDataDir(string newDir)
        {
            if (string.IsNullOrWhiteSpace(newDir)) return;
            try
            {
                var file = Path.Combine(AppContext.BaseDirectory, PointerFile);
                File.WriteAllText(file, newDir.Trim());
            }
            catch (Exception ex) { Logger.Log($"LumenPaths.SetDataDir failed: {ex.Message}"); }
        }

        private static string ReadPointer()
        {
            try
            {
                var file = Path.Combine(AppContext.BaseDirectory, PointerFile);
                if (File.Exists(file))
                {
                    var t = File.ReadAllText(file).Trim();
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                }
            }
            catch { /* 读不到则用默认 */ }
            return null;
        }

        /// <summary>该目录是否含 Lumen 数据（config / meta / settings 任一文件，或 profiles 子目录存在）。</summary>
        public static bool HasData(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;
            return File.Exists(Path.Combine(dir, "config.json"))
                || File.Exists(Path.Combine(dir, "meta.json"))
                || File.Exists(Path.Combine(dir, "settings.json"))
                || Directory.Exists(Path.Combine(dir, "profiles"));
        }

        /// <summary>递归复制 source 下全部内容到 target（含子目录）。成功返回 true；异常记日志并返回 false。</summary>
        public static bool CopyAll(string source, string target)
        {
            try
            {
                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(source))
                {
                    var name = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(target, name), true);
                }
                foreach (var sub in Directory.GetDirectories(source))
                {
                    var name = Path.GetFileName(sub);
                    CopyAll(sub, Path.Combine(target, name));
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"LumenPaths.CopyAll failed ({source} -> {target}): {ex.Message}");
                return false;
            }
        }
    }
}
