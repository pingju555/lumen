using System;
using System.Diagnostics;
using Lumen.Core;
using Lumen.Native;

namespace Lumen.Actions
{
    /// <summary>
    /// 动作执行器：把 AtomAction 落到具体行为。host = LumenWindow.Main。
    /// 复用现有 Provider：媒体控制走 IDataProvider.MediaControl，应用启动走 AppProvider 名匹配 / Process。
    /// </summary>
    public static class ActionRunner
    {
        /// <summary>依次执行一组动作（流程）。跳过 None。</summary>
        public static void RunAll(System.Collections.Generic.IEnumerable<AtomAction> actions)
        {
            if (actions == null) return;
            foreach (var a in actions)
                Run(a);
        }

        public static void Run(AtomAction action)
        {
            if (action == null || action.Kind == ActionKind.None) return;
            var host = LumenWindow.Main;
            try
            {
                switch (action.Kind)
                {
                    case ActionKind.RunApp:
                        RunApp(action.Arg); break;
                    case ActionKind.MediaControl:
                        host?.Ctx?.Provider?.MediaControl((action.Arg ?? "play").Trim().ToLowerInvariant()); break;
                    case ActionKind.SwitchPage:
                        SwitchPage(host, action.Arg); break;
                    case ActionKind.ToggleEditMode:
                        host?.RequestToggleEditMode(); break;
                    case ActionKind.OpenSettings:
                        host?.ShowSettings(); break;
                    case ActionKind.LockScreen:
                        NativeMethods.LockWorkStation(); break;
                    case ActionKind.OpenURL:
                        StartProcess(action.Arg, null, true); break;
                    case ActionKind.Command:
                        StartProcess("cmd.exe", "/c " + action.Arg, true); break;
                    case ActionKind.SwitchPreset:
                        host?.SwitchPreset(action.Arg); break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ActionRunner failed: " + action.Kind + " -> " + ex.Message);
            }
        }

        private static void RunApp(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return;
            var prov = LumenWindow.Main?.Ctx?.Provider;
            if (prov != null)
            {
                int n = prov.AppCount();
                for (int i = 0; i < n; i++)
                {
                    if (string.Equals(prov.AppName(i), arg.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (prov.AppLaunch(i)) return;
                        break;
                    }
                }
            }
            // 未匹配到开始菜单应用名 -> 当作路径 / .lnk / 可执行 / URL 直接启动
            StartProcess(arg, null, true);
        }

        private static void SwitchPage(LumenWindow host, string arg)
        {
            if (host == null) return;
            arg = (arg ?? "").Trim();
            if (arg == "+1") host.NextPage();
            else if (arg == "-1") host.PrevPage();
            else if (int.TryParse(arg, out var idx)) host.GotoPage(idx);
            else host.NextPage();
        }

        private static void StartProcess(string fileName, string args, bool shell)
        {
            try
            {
                var si = new ProcessStartInfo(fileName) { UseShellExecute = shell };
                if (!string.IsNullOrEmpty(args)) si.Arguments = args;
                Process.Start(si);
            }
            catch (Exception ex)
            {
                Logger.Log("ActionRunner.StartProcess failed: " + fileName + " -> " + ex.Message);
            }
        }
    }
}
