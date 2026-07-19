using System;
using System.Collections.Generic;
using System.ComponentModel;
using Lumen.Formula;

namespace Lumen.Globals
{
    public enum GvType { Number, Text, Color, Font, List, Switch }

    /// <summary>类型化全局变量值。</summary>
    public class TypedValue
    {
        public GvType Type;
        public object Raw;
        /// <summary>仅 List 类型使用：当前选中的选项下标。</summary>
        public int SelectedIndex;

        public Value ToValue()
        {
            switch (Type)
            {
                case GvType.Number: return Value.Of(Convert.ToDouble(Raw ?? 0));
                case GvType.Text: return Value.Of((Raw as string) ?? "");
                case GvType.Color: return Value.OfColor((uint)Convert.ToInt64(Raw ?? 0));
                case GvType.Font: return Value.Of((Raw as string) ?? "");
                case GvType.List:
                    {
                        var opts = (Raw as string) ?? "";
                        if (string.IsNullOrEmpty(opts)) return Value.Of("");
                        var arr = opts.Split('|');
                        int idx = SelectedIndex;
                        if (idx < 0 || idx >= arr.Length) idx = 0;
                        return Value.Of(arr[idx]);
                    }
                case GvType.Switch:
                    return Value.Of(Convert.ToBoolean(Raw ?? false));
                default: return Value.Null();
            }
        }
    }

    /// <summary>
    /// 全局变量存储：跨层共享，改值即触发 Changed 驱动增量重算。
    /// 详见 docs/project/phases/P2_原子全集与公式引擎/P2-04_全局变量gv.md
    /// </summary>
    public class GvStore : INotifyPropertyChanged
    {
        private readonly Dictionary<string, TypedValue> _store =
            new(StringComparer.OrdinalIgnoreCase);

        public TypedValue Get(string name)
            => _store.TryGetValue(name, out var v) ? v : null;

        public void Set(string name, TypedValue v)
        {
            _store[name] = v;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            Changed?.Invoke(name);
        }

        public void Remove(string name)
        {
            if (_store.Remove(name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                Changed?.Invoke(name);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<string> Changed;

        public IReadOnlyDictionary<string, TypedValue> All => _store;
    }
}
