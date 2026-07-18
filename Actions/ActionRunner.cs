using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Lumen.Atoms;
using Lumen.Core;
using Lumen.Globals;
using Lumen.Native;

namespace Lumen.Actions
{
    /// <summary>
    /// 动作执行器：把 AtomAction 落到具体行为。host = LumenWindow.Main。
    /// 复用现有 Provider：媒体控制走 IDataProvider.MediaControl，应用启动走 AppProvider 名匹配 / Process。
    /// 流程(Delay)需要异步等待，故 Run/RunAll 改为 async；调用方以 fire-and-forget 方式调用即可。
    /// </summary>
    public static class ActionRunner
    {
        /// <summary>依次执行一组动作（流程）。跳过 None。host 用于 RunFlow 反查本原子流程；visiting 用于流程循环引用防护。</summary>
        public static async Task RunAllAsync(IEnumerable<AtomAction> actions, Atom host = null, HashSet<int> visiting = null)
        {
            if (actions == null) return;
            foreach (var a in actions)
                await RunAsync(a, host, visiting);
        }

        public static async Task RunAsync(AtomAction action, Atom host = null, HashSet<int> visiting = null)
        {
            if (action == null || action.Kind == ActionKind.None) return;
            var win = LumenWindow.Main;
            try
            {
                switch (action.Kind)
                {
                    case ActionKind.RunApp:
                        RunApp(action.Arg); break;
                    case ActionKind.MediaControl:
                        win?.Ctx?.Provider?.MediaControl((action.Arg ?? "play").Trim().ToLowerInvariant()); break;
                    case ActionKind.SwitchPage:
                        SwitchPage(win, action.Arg); break;
                    case ActionKind.ToggleEditMode:
                        win?.RequestToggleEditMode(); break;
                    case ActionKind.OpenSettings:
                        win?.ShowSettings(); break;
                    case ActionKind.LockScreen:
                        NativeMethods.LockWorkStation(); break;
                    case ActionKind.OpenURL:
                        StartProcess(action.Arg, null, true); break;
                    case ActionKind.Command:
                        StartProcess("cmd.exe", "/c " + action.Arg, true); break;
                    case ActionKind.SwitchPreset:
                        win?.SwitchPreset(action.Arg); break;
                    case ActionKind.RunFlow:
                        if (host != null && int.TryParse((action.Arg ?? "").Trim(), out var idx) && idx >= 0 && idx < host.Flows.Count)
                        {
                            // 循环引用防护：同一调用链上已执行的流程不再重入（允许不同分支再次触发该流程）
                            visiting ??= new HashSet<int>();
                            if (!visiting.Contains(idx))
                            {
                                visiting.Add(idx);
                                try { await RunAllAsync(host.Flows[idx].Actions, host, visiting); }
                                finally { visiting.Remove(idx); }
                            }
                        }
                        break;
                    case ActionKind.SetVar:
                        SetVar(action.Arg, win); break;
                    case ActionKind.Delay:
                        if (int.TryParse(action.Arg, out var ms) && ms > 0) await Task.Delay(ms);
                        break;
                    case ActionKind.ReadFile:
                        ReadFile(action.Arg, win); break;
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

        /// <summary>写全局变量：Arg = 变量名|值；值支持 $公式$ / $gv(x)$（实现读写变量）。数值自动存为 Number，其余存 Text。</summary>
        private static void SetVar(string arg, LumenWindow win)
        {
            if (string.IsNullOrWhiteSpace(arg) || win == null) return;
            var i = arg.IndexOf('|');
            if (i < 0) return;
            var name = arg.Substring(0, i).Trim();
            var value = arg.Substring(i + 1);
            if (string.IsNullOrEmpty(name)) return;
            string resolved = value;
            if (value.Contains("$") && win.Ctx != null)
            {
                var raw = value.Trim();
                if (raw.StartsWith("$") && raw.EndsWith("$") && raw.Length >= 2) raw = raw.Substring(1, raw.Length - 2);
                try { resolved = win.Ctx.Eval(raw).AsStr(); } catch { resolved = value; }
            }
            var tv = new TypedValue
            {
                Type = double.TryParse(resolved, out _) ? GvType.Number : GvType.Text,
                Raw = resolved
            };
            win.Gv.Set(name, tv);
        }

        /// <summary>读取文件写入全局变量：Arg = 路径|变量名 或 路径|变量名|json字段（支持 a.b.c / a[0].b 提取）。txt/json 皆可，json 缺字段回退整文件。</summary>
        private static void ReadFile(string arg, LumenWindow win)
        {
            if (string.IsNullOrWhiteSpace(arg) || win == null) return;
            var parts = arg.Split('|');
            if (parts.Length < 2) return;
            var path = parts[0].Trim();
            var varName = parts[1].Trim();
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(varName)) return;
            try
            {
                var text = File.ReadAllText(path);
                string result = text;
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        var el = JsonNavigate(doc.RootElement, parts[2].Trim());
                        if (el != null) result = el.Value.GetRawText().Trim('"');
                    }
                    catch { /* 非 JSON 或字段缺失：回退整文件内容 */ }
                }
                win.Gv.Set(varName, new TypedValue { Type = GvType.Text, Raw = result });
            }
            catch (Exception ex)
            {
                Logger.Log("ReadFile failed: " + path + " -> " + ex.Message);
            }
        }

        private static JsonElement? JsonNavigate(JsonElement root, string key)
        {
            JsonElement cur = root;
            foreach (var seg in key.Split('.'))
            {
                if (string.IsNullOrEmpty(seg)) return null;
                var name = seg; int arrIdx = -1;
                var lb = seg.IndexOf('[');
                if (lb >= 0 && seg.EndsWith("]"))
                {
                    var num = seg.Substring(lb + 1, seg.Length - lb - 2);
                    if (int.TryParse(num, out arrIdx)) name = seg.Substring(0, lb);
                }
                if (cur.ValueKind == JsonValueKind.Array)
                {
                    if (arrIdx >= 0 && arrIdx < cur.GetArrayLength()) cur = cur[arrIdx];
                    else return null;
                }
                else if (cur.ValueKind == JsonValueKind.Object)
                {
                    if (!cur.TryGetProperty(name, out var next)) return null;
                    cur = next;
                }
                else return null;
            }
            return cur;
        }
    }
}
