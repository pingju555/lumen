using System.Diagnostics;
using Microsoft.Win32;

namespace Lumen.Core
{
    /// <summary>注册表自启（HKCU\...\Run）。仅写入 exe 路径（无参=守护模式，会拉起 UI）。</summary>
    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Lumen";

        public static bool Enabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) != null;
            }
        }

        public static void Install()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key.SetValue(ValueName, Process.GetCurrentProcess().MainModule.FileName);
        }

        public static void Uninstall()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(ValueName, false);
        }

        /// <summary>设置面板开关入口：true=写注册表，false=删注册表。</summary>
        public static void SetEnabled(bool enabled)
        {
            if (enabled) Install();
            else Uninstall();
        }
    }
}
