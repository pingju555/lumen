using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Lumen.Atoms;
using Lumen.Globals;

namespace Lumen.Engine
{
    /// <summary>
    /// 增量重算调度器：脏标记 + 批量 Flush + 容错 + 降频。
    /// - df/tf/ts 等时间类：每秒 tick 标记重算（1s 精度）。
    /// - gv.Changed：标记依赖原子重算。
    /// - 失焦：拉长 Interval 至 10s（NFR-07 降频）。
    /// 详见 docs/project/phases/P2_原子全集与公式引擎/P2-05_增量重算与容错.md
    /// </summary>
    public class DirtyScheduler
    {
        private readonly HashSet<Atom> _dirty = new();
        private readonly List<Atom> _atoms;
        private readonly GvStore _gv;
        private readonly DispatcherTimer _timer;

        public DirtyScheduler(IEnumerable<Atom> atoms, GvStore gv)
        {
            _atoms = new List<Atom>(atoms);
            _gv = gv;
            _gv.Changed += _ => MarkAllDirty();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
        }

        public void SetAtoms(IEnumerable<Atom> atoms)
        {
            _atoms.Clear();
            _atoms.AddRange(atoms);
            MarkAllDirty();
        }

        public void MarkDirty(Atom a) { lock (_dirty) _dirty.Add(a); }
        public void MarkAllDirty() { lock (_dirty) foreach (var a in _atoms) _dirty.Add(a); }

        private void Tick()
        {
            foreach (var a in _atoms)
                if (UsesClock(a) || UsesMedia(a) || UsesPerf(a)) MarkDirty(a);
            Flush();
            // 触发器评估（P5）：每个 tick 对全部原子（含容器子原子）检查条件，成立自动触发动作。
            // 独立于脏标记，确保即使原子自身属性未变、仅条件数据变化（如电量/媒体状态）也能响应。
            // 动画条件重估 + 进度动画 tick
            foreach (var a in _atoms)
            {
                a.EvaluateFlows();
                a.TickAnimation();
                a.TickProgressAnimation();
                if (a is ContainerAtom c)
                    foreach (var ch in c.Children) { ch.EvaluateFlows(); ch.TickAnimation(); ch.TickProgressAnimation(); }
            }
        }

        private static bool UsesClock(Atom a)
        {
            foreach (var kv in a.GetProps())
            {
                var m = kv.Value.Materialize();
                if (m.Contains("$") && (m.Contains("df") || m.Contains("tf") || m.Contains("ts") || m.Contains("tu")))
                    return true;
            }
            return false;
        }

        // 媒体依赖：公式含 mi( 的原子需每拍重算，媒体状态变化（换歌/封面）才能即时反映
        private static bool UsesMedia(Atom a)
        {
            foreach (var kv in a.GetProps())
            {
                var m = kv.Value.Materialize();
                if (m.Contains("$") && m.Contains("mi(")) return true;
            }
            return false;
        }

        // 性能依赖：公式含 si( 的原子需每拍重算，PDH 采样值（CPU/内存/磁盘/网络）才能即时反映。
        // 修复项：v1.3.1 前 DirtyScheduler 仅标脏时钟/媒体类，导致所有 PDH 仪表盘首帧后静止。
        private static bool UsesPerf(Atom a)
        {
            foreach (var kv in a.GetProps())
            {
                var m = kv.Value.Materialize();
                if (m.Contains("$") && m.Contains("si(")) return true;
            }
            return false;
        }

        public void Flush()
        {
            List<Atom> batch;
            lock (_dirty)
            {
                batch = new List<Atom>(_dirty);
                _dirty.Clear();
            }
            foreach (var a in batch)
            {
                try { a.Update(); }
                catch { /* 容错：单个原子重算失败不影响整体 */ }
            }
        }

        public void SetFocused(bool focused)
        {
            _timer.Interval = focused ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10);
        }
    }
}
