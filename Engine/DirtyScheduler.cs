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
                if (UsesClock(a)) MarkDirty(a);
            Flush();
            // 触发器评估（P5）：每个 tick 对全部原子（含容器子原子）检查条件，成立自动触发动作。
            // 独立于脏标记，确保即使原子自身属性未变、仅条件数据变化（如电量/媒体状态）也能响应。
            foreach (var a in _atoms)
            {
                a.EvaluateTriggers();
                if (a is ContainerAtom c)
                    foreach (var ch in c.Children) ch.EvaluateTriggers();
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
