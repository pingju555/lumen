using System;
using System.IO;
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
            // 全局异常兜底：UI 线程 / 非 UI 线程 / Task 异常统一写入诊断日志（跟随数据根：<数据根>/lumen.log，无指针时回退 %TEMP%/lumen.log），
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

            // 部署/调试入口：仅注册表自启，不启动 UI
            var args = Environment.GetCommandLineArgs();
            foreach (var a in args)
            {
                if (a == "--install")   { Autostart.SetEnabled(true);  Shutdown(); return; }
                if (a == "--uninstall") { Autostart.SetEnabled(false); Shutdown(); return; }
            }

            // 首次自动迁移：指针指向空自定义位置、默认位置有旧数据时提示迁移（GUI 模式）。
            // 须在 Loc.Init 前执行，使迁移后能从正确位置读取已持久化的 UI 语言。
            MaybeAutoMigrate();

            // 启动早期确定 UI 语言（读 meta 持久化 / 否则跟随系统 culture）并预载语言包
            Loc.Init();

            // 直接运行 UI（单进程）。Ctrl+Alt+Q 干净退出即进程退出，无重拉。
            MainWindow = new LumenWindow();
            MainWindow.Show();
        }

        /// <summary>
        /// 首次启动自动迁移：若「当前数据位置」（指针指定，否则便携默认 = exe 文件夹）为空，
        /// 而旧默认位置 %LocalAppData%/Lumen 仍含旧数据，弹窗询问是否迁移（复制）到当前数据位置。
        /// 选择「否」则保留旧数据不动、以空配置运行（可稍后手动迁移）。
        /// 覆盖从 v1.3.1 及更早（数据在 %LocalAppData%/Lumen）升级到便携默认（exe 文件夹）的场景。
        /// </summary>
        private static void MaybeAutoMigrate()
        {
            try
            {
                var target = LumenPaths.DataDir;                       // 指针指定位置，否则便携默认（exe 文件夹）
                var portableDefault = LumenPaths.DefaultDataDir;         // 便携默认 = exe 文件夹
                var legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");

                if (LumenPaths.HasData(target)) return;            // 当前位置已有数据，无需处理
                if (!LumenPaths.HasData(legacy)) return;         // 旧默认位置也无数据

                var msg = "在旧默认位置发现旧数据：\n" + legacy +
                          "\n\n是否迁移到当前数据位置？\n" + target +
                          "\n\n（选择「否」将使用空配置，旧数据保留不动）";
                var res = MessageBox.Show(msg, "Lumen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    LumenPaths.CopyAll(legacy, target);
                    Logger.Log($"Auto-migrated legacy data {legacy} -> {target}");
                }
            }
            catch (Exception ex) { Logger.Log($"MaybeAutoMigrate failed: {ex.Message}"); }
        }
    }
}
