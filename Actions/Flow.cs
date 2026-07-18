using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lumen.Formula;

namespace Lumen.Actions
{
    /// <summary>
    /// 流程触发模式（P5 流程系统）。
    /// Once = 上升沿触发：条件 false→true 时触发一次；条件回到 false 后重新武装（可再次触发）。
    /// While = 持续触发：只要条件为 true，每个评估周期都触发（适合「媒体播放期间显示控件」类）。
    /// </summary>
    public enum FlowFireMode { Once = 0, While = 1 }

    /// <summary>
    /// 单个流程：当布尔公式条件成立时自动执行一个「动作序列」（有序的一组动作，依次执行，无需点击）。
    /// 序列化为 "Condition\u0001Mode\u0001Action1\u0003Action2\u0003..."；流程列表以 \u0002 连接
    /// （均使用不可键入的控制字符作分隔，避免与公式内容冲突）。
    /// 旧版单动作格式（无 \u0003）解析时自动归入序列首步，向后兼容。
    /// </summary>
    public class Flow
    {
        public string Condition = "";
        public List<AtomAction> Actions = new List<AtomAction>();
        public FlowFireMode Mode = FlowFireMode.Once;

        // 运行态（不持久化）：用于 Once 模式的上升沿检测
        private bool _wasTrue;

        /// <summary>
        /// 用给定上下文评估条件，返回本周期是否应触发序列。
        /// 副作用：更新 _wasTrue（用于 Once 模式的边沿检测）。
        /// </summary>
        public bool ShouldFire(EvalContext ctx)
        {
            bool cond = EvalCondition(ctx);
            bool fire;
            if (Mode == FlowFireMode.Once)
                fire = cond && !_wasTrue;   // 上升沿：从未成立→成立
            else
                fire = cond;                // While：持续成立每周期触发
            _wasTrue = cond;
            return fire;
        }

        private bool EvalCondition(EvalContext ctx)
        {
            if (ctx == null || string.IsNullOrWhiteSpace(Condition)) return false;
            try { return ctx.Eval(Condition).AsBool(); }
            catch { return false; }
        }

        public string Serialize()
        {
            var cond = (Condition ?? "")
                .Replace("\u0001", " ").Replace("\u0002", " ").Replace("\u0003", " ");
            var sb = new StringBuilder();
            for (int i = 0; i < Actions.Count; i++)
            {
                if (i > 0) sb.Append('\u0003');
                var a = (Actions[i]?.Serialize() ?? "").Replace("\u0003", " ");
                sb.Append(a);
            }
            return cond + "\u0001" + ((int)Mode).ToString() + "\u0001" + sb.ToString();
        }

        public static Flow Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new Flow();
            var parts = s.Split('\u0001');
            var t = new Flow();
            if (parts.Length > 0) t.Condition = parts[0] ?? "";
            if (parts.Length > 1 && int.TryParse(parts[1], out var m)) t.Mode = (FlowFireMode)m;
            if (parts.Length > 2)
            {
                var joined = parts[2];
                if (joined.Contains('\u0003'))
                    t.Actions = joined.Split('\u0003').Select(AtomAction.Parse).ToList();
                else
                    t.Actions = new List<AtomAction> { AtomAction.Parse(joined) }; // 旧版单动作：归入序列首步
            }
            return t;
        }

        public static string SerializeList(List<Flow> list)
        {
            if (list == null || list.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append('\u0002');
                sb.Append(list[i].Serialize());
            }
            return sb.ToString();
        }

        public static List<Flow> ParseList(string s)
        {
            var list = new List<Flow>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var seg in s.Split('\u0002'))
                if (!string.IsNullOrEmpty(seg)) list.Add(Parse(seg));
            return list;
        }
    }
}
