using System;
using System.Runtime.InteropServices;
using Lumen.Native;

namespace Lumen.Native
{
    /// <summary>覆盖窗口原生操作封装（样式 / z 序 / 定位）。</summary>
    internal static class NativeWindow
    {
        /// <summary>加扩展样式（按位或）。</summary>
        public static void AddExStyle(IntPtr hWnd, int flags)
        {
            var s = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, s | flags);
        }

        /// <summary>剥扩展样式（按位清）。注意：绝不剥 WS_EX_LAYERED（AllowsTransparency 依赖）。</summary>
        public static void RemoveExStyle(IntPtr hWnd, int flags)
        {
            var s = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, s & ~flags);
        }

        /// <summary>
        /// 把窗口插入到桌面 Progman 之上、普通窗口之下。
        /// 不调用 HWND_TOPMOST（会盖住任务栏触发区）。每次重找 Progman/WorkerW，
        /// 规避 Explorer 重启导致旧句柄失效。仅改 z 序，不动位置/大小。
        /// </summary>
        public static void InsertAboveDesktop(IntPtr hWnd)
        {
            var after = NativeMethods.FindWindow("Progman", null);
            if (after == IntPtr.Zero) after = NativeMethods.FindWindow("WorkerW", null);
            if (after == IntPtr.Zero) return; // 兜底：保持默认 z（新窗本就高于桌面）

            NativeMethods.SetWindowPos(
                hWnd, after, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }
}
