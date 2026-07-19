using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Lumen.Formula;
using Lumen.Globals;

namespace Lumen.Atoms
{
    /// <summary>
    /// 组件变量存储（自包含组件 Model A）：
    /// - <see cref="InternalDefaults"/> 内部变量（名称/类型/默认值）—— 定义组件 schema；
    /// - <see cref="ExternalOverrides"/> 外部覆盖（实例当前值）—— 各实例独立，覆盖内部默认。
    /// 实现 <see cref="IComponentVarResolver"/>：Resolve 优先外部覆盖，否则内部默认，否则 Null。
    /// 复制组件 = 深拷贝两表，各实例独立。
    /// </summary>
    public class ComponentVarStore : IComponentVarResolver
    {
        public Dictionary<string, TypedValue> InternalDefaults = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TypedValue> ExternalOverrides = new(StringComparer.OrdinalIgnoreCase);

        public Value Resolve(string name)
        {
            if (ExternalOverrides.TryGetValue(name, out var ov) && ov != null)
                return ov.ToValue();
            if (InternalDefaults.TryGetValue(name, out var def) && def != null)
                return def.ToValue();
            return Value.Null();
        }

        /// <summary>按名解析组件变量，返回原始 TypedValue（含 List 选项），供 gv(scope,name,N) 应用 List 索引。未定义返回 null。</summary>
        public TypedValue ResolveTyped(string name)
        {
            if (ExternalOverrides.TryGetValue(name, out var ov) && ov != null) return ov;
            if (InternalDefaults.TryGetValue(name, out var def) && def != null) return def;
            return null;
        }

        public ComponentVarStore Clone()
        {
            var c = new ComponentVarStore();
            foreach (var kv in InternalDefaults) c.InternalDefaults[kv.Key] = CloneTv(kv.Value);
            foreach (var kv in ExternalOverrides) c.ExternalOverrides[kv.Key] = CloneTv(kv.Value);
            return c;
        }

        private static TypedValue CloneTv(TypedValue tv)
            => new TypedValue { Type = tv.Type, Raw = tv.Raw, SelectedIndex = tv.SelectedIndex };

        // ---------- JSON 序列化（经 PropertyValue 字符串安全往返，见 ComponentAtom 注释）----------
        public string ToJson()
        {
            var dto = new VarDto
            {
                Internal = ToList(InternalDefaults),
                External = ToList(ExternalOverrides)
            };
            return JsonSerializer.Serialize(dto);
        }

        public static ComponentVarStore FromJson(string s)
        {
            var store = new ComponentVarStore();
            if (string.IsNullOrWhiteSpace(s)) return store;
            try
            {
                var dto = JsonSerializer.Deserialize<VarDto>(s);
                if (dto != null)
                {
                    foreach (var v in dto.Internal) store.InternalDefaults[v.Name] = ToTv(v);
                    foreach (var v in dto.External) store.ExternalOverrides[v.Name] = ToTv(v);
                }
            }
            catch { /* 坏数据 → 空表，不崩 */ }
            return store;
        }

        private static List<VarItem> ToList(Dictionary<string, TypedValue> src)
        {
            var list = new List<VarItem>();
            foreach (var kv in src)
                list.Add(new VarItem
                {
                    Name = kv.Key,
                    Type = kv.Value.Type.ToString(),
                    Raw = kv.Value.Raw?.ToString() ?? "",
                    Sel = kv.Value.SelectedIndex
                });
            return list;
        }

        private static TypedValue ToTv(VarItem v)
        {
            var t = Enum.TryParse<GvType>(v.Type, true, out var gt) ? gt : GvType.Text;
            object raw = v.Raw ?? "";
            if (t == GvType.Number && double.TryParse(v.Raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) raw = d;
            else if (t == GvType.Color && uint.TryParse((v.Raw ?? "").TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u)) raw = u;
            return new TypedValue { Type = t, Raw = raw, SelectedIndex = v.Sel };
        }

        private class VarDto
        {
            public List<VarItem> Internal { get; set; } = new();
            public List<VarItem> External { get; set; } = new();
        }

        private class VarItem
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Raw { get; set; }
            public int Sel { get; set; }
        }
    }
}
