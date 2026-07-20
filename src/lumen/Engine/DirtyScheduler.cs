using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Lumen.Atoms;
using Lumen.Globals;

namespace Lumen.Engine
{
    /// <summary>
    /// 双轨增量重算调度器（v2）：快轨 ~60 FPS（数据→UI + 动画插值），慢轨 1s（流程条件评估）。
    /// - 数据源（时钟/媒体/性能）：每帧检测标脏 → Flush 批量刷新。
    /// - GV 变更：即时 MarkAllDirty + Flush（跳过 Tick 等待）。
    /// - SMTC 事件：通过 RequestFlush() 触发即时刷新。
    /// - 动画 TickAnimation/TickProgressAnimation：移至快轨（60 FPS 流畅动画）。
    /// - 失焦：快轨降为 200ms（5 FPS），慢轨不变（1s）。
    /// </summary>
    public class DirtyScheduler
    {
        private readonly HashSet<Atom> _dirty = new();
        private readonly List<Atom> _atoms;
        private readonly GvStore _gv;
        private readonly DispatcherTimer _fastTimer;
        private readonly DispatcherTimer _slowTimer;

        /// <summary>~60 FPS 快轨间隔（毫秒）。</summary>
        private const int FAST_MS = 16;
        /// <summary>失焦时快轨间隔（毫秒）。</summary>
        private const int FAST_BG_MS = 200;
        /// <summary>慢轨间隔（秒）。</summary>
        private const int SLOW_S = 1;

        public DirtyScheduler(IEnumerable<Atom> atoms, GvStore gv)
        {
            _atoms = new List<Atom>(atoms);
            _gv = gv;
            // GV 变更 → 即时全量标脏 + 立即 Flush（不等 Tick）
            _gv.Changed += _ => { MarkAllDirty(); Flush(); };

            // 快轨：数据→UI + 动画插值
            _fastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FAST_MS) };
            _fastTimer.Tick += (s, e) => FastTick();
            _fastTimer.Start();

            // 慢轨：流程条件评估（不需要高频）
            _slowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(SLOW_S) };
            _slowTimer.Tick += (s, e) => SlowTick();
            _slowTimer.Start();
        }

        public void SetAtoms(IEnumerable<Atom> atoms)
        {
            _atoms.Clear();
            _atoms.AddRange(atoms);
            MarkAllDirty();
        }

        public void MarkDirty(Atom a) { lock (_dirty) _dirty.Add(a); }
        public void MarkAllDirty() { lock (_dirty) foreach (var a in _atoms) _dirty.Add(a); }

        /// <summary>SMTC 事件驱动即时刷新：全量标脏后立即 Flush（跳过 Tick 等待）。</summary>
        public void RequestFlush() { MarkAllDirty(); Flush(); }

        private void FastTick()
        {
            // 1) 数据源依赖检测 → 标脏
            foreach (var a in _atoms)
                if (UsesClock(a) || UsesMedia(a) || UsesPerf(a))
                    MarkDirty(a);

            // 2) 批量刷新所有脏原子
            Flush();

            // 3) 动画插值（60 FPS 流畅）
            foreach (var a in _atoms)
            {
                a.TickAnimation();
                a.TickProgressAnimation();
                if (a is ContainerAtom c)
                    foreach (var ch in c.Children)
                    {
                        ch.TickAnimation();
                        ch.TickProgressAnimation();
                    }
            }
        }

        private void SlowTick()
        {
            // 流程条件评估（低频，不需高帧率）
            foreach (var a in _atoms)
            {
                a.EvaluateFlows();
                if (a is ContainerAtom c)
                    foreach (var ch in c.Children)
                        ch.EvaluateFlows();
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

        private static bool UsesMedia(Atom a)
        {
            foreach (var kv in a.GetProps())
            {
                var m = kv.Value.Materialize();
                if (m.Contains("$") && m.Contains("mi(")) return true;
            }
            return false;
        }

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
            _fastTimer.Interval = focused
                ? TimeSpan.FromMilliseconds(FAST_MS)    // 60 FPS
                : TimeSpan.FromMilliseconds(FAST_BG_MS); // 5 FPS
            // 慢轨保持 1s 不变
        }
    }
}
