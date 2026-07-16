using System;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using Microsoft.Win32;
using Lumen.Native;

namespace Lumen.Formula
{
    /// <summary>系统数据提供器接口。P4 已接入真实 PDH/WMI/Win32 系统指标；P4-2 接入 SMTC 媒体与启动坞应用。</summary>
    public interface IDataProvider
    {
        DateTime Now { get; }
        int BatteryLevel();      // -1 表示无电池
        bool BatteryPlugged();
        double ScreenWidth();
        double ScreenHeight();
        double Dpi();
        bool IsDark();

        // ---- P4 实时系统指标（真实 PDH / Win32）----
        double CpuPercent();     // 0-100
        double MemPercent();     // 0-100
        double MemUsedGb();      // 已用内存 (GB)
        double MemTotalGb();     // 总内存 (GB)
        double DiskFreeGb();     // C: 可用 (GB)
        double DiskTotalGb();    // C: 总量 (GB)
        double DiskPercent();    // C: 已用占比 0-100
        double NetUp();          // 上行速率 (bytes/s)
        double NetDown();        // 下行速率 (bytes/s)

        // ---- P4-2 媒体 (SMTC, WinRT) ----
        bool MediaAvailable();
        string MediaTitle();
        string MediaArtist();
        string MediaAlbum();
        string MediaApp();
        bool MediaPlaying();
        double MediaPosition();   // 秒
        double MediaDuration();   // 秒
        void MediaControl(string cmd);

        // ---- P4-2 应用 (启动坞, COM/文件扫描) ----
        int AppCount();
        string AppName(int idx);  // idx 0-based
        bool AppLaunch(int idx);  // idx 0-based
    }

    /// <summary>
    /// v1 真实数据提供器：时间/电池/屏幕/暗色 + P4 系统监控（CPU/内存/磁盘/网络）
    /// + P4-2 媒体（SMTC 后台轮询）/ 应用（启动坞枚举）。
    /// 差值类指标（CPU/网络）与媒体由后台 Timer 周期采样缓存，公式同步读取，避免阻塞求值。
    /// </summary>
    public class SystemDataProvider : IDataProvider
    {
        private const double GIG = 1024.0 * 1024 * 1024;
        private const double TICK_SEC = 1.0;

        // --- CPU (PDH) ---
        private IntPtr _cpuQuery;
        private IntPtr _cpuCounter;
        private double _cpuValue;

        // --- 网络 (IPHelper 差值) ---
        private ulong _netInPrev, _netOutPrev;
        private double _netInRate, _netOutRate;

        // --- P4-2 媒体 / 应用 ---
        private readonly MediaProvider _media;
        private readonly AppProvider _apps;

        private readonly Timer _timer;

        public SystemDataProvider()
        {
            InitCpu();
            _media = new MediaProvider();
            _media.Start();
            _apps = new AppProvider();
            _timer = new Timer(1000) { AutoReset = true };
            _timer.Elapsed += Tick;
            _timer.Start();
        }

        // ---------- 基础（原有真实实现）----------

        public DateTime Now => DateTime.Now;

        public int BatteryLevel()
        {
            var s = new NativeMethods.SYSTEM_POWER_STATUS();
            if (NativeMethods.GetSystemPowerStatus(ref s))
            {
                if ((s.BatteryFlag & NativeMethods.BATTERY_FLAG_NO_BATTERY) != 0) return -1;
                return s.BatteryLifePercent;
            }
            return -1;
        }

        public bool BatteryPlugged()
        {
            var s = new NativeMethods.SYSTEM_POWER_STATUS();
            if (NativeMethods.GetSystemPowerStatus(ref s))
                return s.ACLineStatus == NativeMethods.ACLINE_STATUS_ONLINE;
            return false;
        }

        public double ScreenWidth() => SystemParameters.PrimaryScreenWidth;
        public double ScreenHeight() => SystemParameters.PrimaryScreenHeight;
        public double Dpi() => 1.0;

        public bool IsDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                if (key?.GetValue("AppsUseLightTheme") is int v) return v == 0;
            }
            catch { /* 忽略，默认亮色 */ }
            return false;
        }

        // ---------- P4 系统指标 ----------

        public double CpuPercent() => _cpuValue;
        public double MemPercent() => ReadMem().pct;
        public double MemUsedGb() => ReadMem().usedGb;
        public double MemTotalGb() => ReadMem().totalGb;
        public double DiskFreeGb() => ReadDisk().freeGb;
        public double DiskTotalGb() => ReadDisk().totalGb;
        public double DiskPercent()
        {
            var d = ReadDisk();
            return d.totalGb > 0 ? (d.totalGb - d.freeGb) / d.totalGb * 100 : 0;
        }
        public double NetUp() => _netOutRate;
        public double NetDown() => _netInRate;

        // ---------- P4-2 媒体 / 应用 ----------

        public bool MediaAvailable() => _media.Available;
        public string MediaTitle() => _media.Title;
        public string MediaArtist() => _media.Artist;
        public string MediaAlbum() => _media.Album;
        public string MediaApp() => _media.AppName;
        public bool MediaPlaying() => _media.Playing;
        public double MediaPosition() => _media.PositionSec;
        public double MediaDuration() => _media.DurationSec;
        public void MediaControl(string cmd) => _ = _media.ControlAsync(cmd);

        public int AppCount() => _apps.Count;
        public string AppName(int idx) => _apps.NameAt(idx);
        public bool AppLaunch(int idx) => _apps.Launch(idx);

        // ---------- 后台采样 ----------

        private void Tick(object sender, ElapsedEventArgs e)
        {
            // CPU：% Processor Time 自上次 Collect 以来的平均利用率
            if (_cpuCounter != IntPtr.Zero)
            {
                NativeMethods.PdhCollectQueryData(_cpuQuery);
                if (NativeMethods.PdhGetFormattedCounterValue(_cpuCounter, NativeMethods.PDH_FMT_DOUBLE, out _, out double v) == 0)
                    _cpuValue = v;
            }

            // 网络：两次采样累计字节差值 / 间隔
            var (cin, cout) = ReadNetRaw();
            if (_netInPrev != 0 && cin >= _netInPrev)
            {
                _netInRate = (cin - _netInPrev) / TICK_SEC;
                _netOutRate = (cout - _netOutPrev) / TICK_SEC;
            }
            _netInPrev = cin;
            _netOutPrev = cout;
        }

        private void InitCpu()
        {
            if (NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out _cpuQuery) != 0) { _cpuQuery = IntPtr.Zero; return; }
            if (NativeMethods.PdhAddEnglishCounter(_cpuQuery, @"\Processor Information(_Total)\% Processor Time", IntPtr.Zero, out _cpuCounter) != 0)
            {
                // 回退到经典实例名
                if (NativeMethods.PdhAddEnglishCounter(_cpuQuery, @"\Processor(_Total)\% Processor Time", IntPtr.Zero, out _cpuCounter) != 0)
                {
                    _cpuCounter = IntPtr.Zero;
                    return;
                }
            }
            // 预热：两次 Collect 后才能 Format 出有效值
            NativeMethods.PdhCollectQueryData(_cpuQuery);
            System.Threading.Thread.Sleep(50);
            NativeMethods.PdhCollectQueryData(_cpuQuery);
            NativeMethods.PdhGetFormattedCounterValue(_cpuCounter, NativeMethods.PDH_FMT_DOUBLE, out _, out _cpuValue);
        }

        private (ulong inAll, ulong outAll) ReadNetRaw()
        {
            uint size = 0;
            if (NativeMethods.GetIfTable(IntPtr.Zero, ref size, false) != 0 && size == 0) return (0, 0);
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (NativeMethods.GetIfTable(buf, ref size, true) != 0) return (0, 0);
                uint n = (uint)Marshal.ReadInt32(buf);
                int stride = Marshal.SizeOf<NativeMethods.MIB_IFROW>();
                ulong inAll = 0, outAll = 0;
                IntPtr p = IntPtr.Add(buf, 4); // 跳过 dwNumEntries
                for (int i = 0; i < n; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_IFROW>(p);
                    if (row.dwType != NativeMethods.IF_TYPE_SOFTWARE_LOOPBACK)
                    {
                        inAll += row.dwInOctets;
                        outAll += row.dwOutOctets;
                    }
                    p = IntPtr.Add(p, stride);
                }
                return (inAll, outAll);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        private (double usedGb, double totalGb, double pct) ReadMem()
        {
            var m = new NativeMethods.MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>();
            if (NativeMethods.GlobalMemoryStatusEx(ref m))
            {
                double total = m.ullTotalPhys / GIG;
                double avail = m.ullAvailPhys / GIG;
                return (total - avail, total, m.dwMemoryLoad);
            }
            return (0, 0, 0);
        }

        private (double freeGb, double totalGb) ReadDisk()
        {
            if (NativeMethods.GetDiskFreeSpaceEx("C:\\", out ulong free, out ulong total, out _))
                return (free / GIG, total / GIG);
            return (0, 0);
        }
    }
}
