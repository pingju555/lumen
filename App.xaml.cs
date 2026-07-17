using System;
using System.Threading.Tasks;
using System.Windows;
using Lumen.Core;
using Lumen.I18n;

namespace Lumen
{
    /// <summary>
    /// Application entry. WPF auto-generates Main() -> App.Run() -> OnStartup.
    /// 单进程直接运行覆盖窗口（无守护/看门狗，v1 移除，见 docs/project/phases/P0_脚手架与守护/P0-02_守护进程.md）。
    ///   --install/--uninstall -> 仅切换注册表自启，不启动 UI（部署/调试入口）。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局异常兜底：UI 线程 / 非 UI 线程 / Task 异常统一写入诊断日志（%TEMP%/lumen.log），
            // 避免此前「界面出不来却零日志静默杀进程」无从排查。仅记录，不改默认行为。
            DispatcherUnhandledException += (s, ev) =>
                Logger.Log($"Unhandled(UI): {ev.Exception.GetType().Name}: {ev.Exception.Message}\n{ev.Exception.StackTrace}");
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var ex = ev.ExceptionObject as Exception;
                Logger.Log($"Unhandled(AppDomain{(ev.IsTerminating ? ", terminating" : "")}): {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}");
            };
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                Logger.Log($"UnobservedTask: {ev.Exception.GetType().Name}: {ev.Exception.Message}\n{ev.Exception.StackTrace}");
                ev.SetObserved();
            };

            base.OnStartup(e);

            // 启动早期确定 UI 语言（读 meta 持久化 / 否则跟随系统 culture）并预载语言包
            Loc.Init();

            var args = Environment.GetCommandLineArgs();

            // 部署/调试入口：仅注册表自启，不启动 UI
            foreach (var a in args)
            {
                if (a == "--install")   { Autostart.SetEnabled(true);  Shutdown(); return; }
                if (a == "--uninstall") { Autostart.SetEnabled(false); Shutdown(); return; }
            }

            // 直接运行 UI（单进程）。Ctrl+Alt+Q 干净退出即进程退出，无重拉。
            MainWindow = new LumenWindow();
            MainWindow.Show();
        }
    }
}
