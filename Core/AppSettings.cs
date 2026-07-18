using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lumen.Core
{
    /// <summary>
    /// 程序级设置（与配置档 Profile 解耦），持久化于 %LocalAppData%/Lumen/settings.json。
    /// 当前承载：媒体封面缓存回退目录列表（SMTC 无缩略图时扫描）。
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "settings.json");

        private static AppSettings _instance;

        /// <summary>懒加载单例：首次访问读盘，之后内存中共享（设置面板与 MediaProvider 读同一实例）。</summary>
        public static AppSettings Instance => _instance ??= Load();

        /// <summary>用户手动配置的封面缓存目录（SMTC 无缩略图时附加扫描，不依赖播放器识别）。</summary>
        public List<string> CoverCacheDirs { get; set; } = new List<string>();

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // 设置持久化失败不影响运行
            }
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var txt = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<AppSettings>(txt);
                    if (s != null) return s;
                }
            }
            catch
            {
                // 损坏或读失败：回退默认
            }
            return new AppSettings();
        }
    }
}
