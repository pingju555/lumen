using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;

namespace Lumen.Atoms
{
    /// <summary>
    /// 组件原子（自包含容器）：自身带局部变量表（内部默认 + 外部覆盖）。
    /// 复制即独立实例（两表深拷贝）。子树公式经 <c>gv("", name)</c> / <c>gv("self", name)</c> 读取本组件变量，
    /// 作用域靠 <see cref="ContainerAtom.ChildContext"/> 给子树传「链到父 ctx + Resolver=本组件表」的子 ctx。
    /// 跨组件引用：<c>gv("&lt;组件ID&gt;", name)</c> 经静态注册表 <see cref="TryGetByVid"/> 按 Id 查找本组件变量表。
    /// 属性面板经 <see cref="ICustomTabProvider"/> 增加「内部变量 / 外部变量」两个自定义 Tab。
    ///
    /// 持久化：变量表经 <c>vars</c> 键放进 GetProps/SetProps，序列化为 JSON 字符串。
    /// 该 JSON 经 PropertyValue.Serialize 存储；加载时 PropertyValue.Parse 可能因含 $ 误判为公式，
    /// 但 Serialize(公式值) 仍原样返回 JSON 串，故 FromJson 总能取回正确 JSON（往返安全）。
    /// </summary>
    public class ComponentAtom : ContainerAtom, ICustomTabProvider
    {
        public ComponentVarStore Vars = new();

        /// <summary>变量编辑器改动后回调（由属性面板注入，触发预览 / 重组 / 保存）。</summary>
        public Action VarChanged;

        // 组件 ID → 实例 弱引用注册表（供 gv("id", name) 跨组件寻址；实例被页面移除后 GC，弱引用自动失效）。
        private static readonly Dictionary<string, WeakReference<ComponentAtom>> _registry = new();

        /// <summary>按 8 位 ID 查找当前页面上的组件实例（用于 gv 跨组件引用）。</summary>
        public static bool TryGetByVid(string id, out ComponentAtom atom)
        {
            atom = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_registry.TryGetValue(id, out var wr) && wr.TryGetTarget(out var a)) { atom = a; return true; }
            return false;
        }

        private static readonly GvType[] Types = { GvType.Number, GvType.Text, GvType.Color, GvType.Font, GvType.List, GvType.Switch };
        private static readonly string[] TypeLabels =
            { Loc.T("gv.type.number"), Loc.T("gv.type.text"), Loc.T("gv.type.color"), Loc.T("gv.type.font"), Loc.T("gv.type.list"), Loc.T("gv.type.switch") };

        public ComponentAtom() : base("Component")
        {
            _registry[Id] = new WeakReference<ComponentAtom>(this);
        }

        /// <summary>给子树传「链到父 ctx、多一层 Resolver=本组件变量表」的子 ctx；gv("",name) 经 NearestResolver() 命中本组件。</summary>
        protected override EvalContext ChildContext
            => new EvalContext(Ctx.Gv, Ctx.Provider, Ctx) { Resolver = Vars };

        public override List<TabSpec> EditTabs()
        {
            var list = base.EditTabs();
            list.Add(new TabSpec { Key = "comp.internal", LocKey = "comp.tab.internal" });
            list.Add(new TabSpec { Key = "comp.external", LocKey = "comp.tab.external" });
            return list;
        }

        public UIElement BuildCustomTab(string key)
        {
            if (key == "comp.internal") return BuildInternalTab();
            if (key == "comp.external") return BuildExternalTab();
            return null;
        }

        // ---------- 内部变量 Tab（定义 schema：名称/类型/默认值）----------
        private UIElement BuildInternalTab()
        {
            var host = new StackPanel { Orientation = Orientation.Vertical };
            RebuildInternal(host);
            return MakeScroll(host);
        }

        private void RebuildInternal(StackPanel host)
        {
            host.Children.Clear();
            if (Vars.InternalDefaults.Count == 0)
                host.Children.Add(Hint(Loc.T("comp.var.empty")));
            foreach (var kv in Vars.InternalDefaults)
                host.Children.Add(BuildVarRow(host, kv.Key, kv.Value, true));
            host.Children.Add(AddButton(Loc.T("comp.var.add"), () =>
            {
                string name = UniqueName("var");
                Vars.InternalDefaults[name] = new TypedValue { Type = GvType.Text, Raw = "" };
                RebuildInternal(host);
                Notify();
            }));
        }

        // ---------- 外部变量 Tab（覆盖当前实例值，留空=继承内部默认）----------
        private UIElement BuildExternalTab()
        {
            var host = new StackPanel { Orientation = Orientation.Vertical };
            RebuildExternal(host);
            return MakeScroll(host);
        }

        private void RebuildExternal(StackPanel host)
        {
            host.Children.Clear();
            if (Vars.InternalDefaults.Count == 0)
            {
                host.Children.Add(Hint(Loc.T("comp.var.noSchema")));
                return;
            }
            foreach (var kv in Vars.InternalDefaults)
                host.Children.Add(BuildExternalRow(host, kv.Key, kv.Value));
        }

        // ---------- 内部变量行 ----------
        private Grid BuildVarRow(StackPanel host, string name, TypedValue tv, bool isInternal)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

            var nameTb = new TextBox
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46))
            };
            nameTb.TextChanged += (s, e) =>
            {
                string nn = (nameTb.Text ?? "").Trim();
                if (nn == name || string.IsNullOrEmpty(nn) || Vars.InternalDefaults.ContainsKey(nn)) return;
                Vars.InternalDefaults.Remove(name);
                Vars.InternalDefaults[nn] = tv;
                // 外部覆盖随同名迁移
                if (Vars.ExternalOverrides.TryGetValue(name, out var ov)) { Vars.ExternalOverrides.Remove(name); Vars.ExternalOverrides[nn] = ov; }
                name = nn;
                Notify();
            };

            var valueHost = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            RebuildValueHost(valueHost, tv);

            var typeCb = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                ItemsSource = TypeLabels,
                SelectedIndex = Math.Max(0, Array.IndexOf(Types, tv.Type))
            };
            typeCb.SelectionChanged += (s, e) =>
            {
                int idx = typeCb.SelectedIndex;
                if (idx < 0) return;
                var nt = Types[idx];
                object raw = tv.Raw;
                if (nt == GvType.Number && !(raw is double)) raw = 0d;
                else if (nt == GvType.Color && !(raw is uint)) raw = 0xFF000000u;
                else if ((nt == GvType.Text || nt == GvType.Font) && raw is not string) raw = "";
                else if (nt == GvType.List) raw = "";
                else if (nt == GvType.Switch) raw = false;
                tv.Type = nt; tv.Raw = raw;
                RebuildValueHost(valueHost, tv);
                Notify();
            };

            var del = new Button
            {
                Content = "✕",
                Width = 22, Height = 22,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            };
            del.Click += (s, e) =>
            {
                Vars.InternalDefaults.Remove(name);
                Vars.ExternalOverrides.Remove(name);
                RebuildInternal(host);
                Notify();
            };

            Grid.SetColumn(nameTb, 0);
            Grid.SetColumn(typeCb, 1);
            Grid.SetColumn(valueHost, 2);
            Grid.SetColumn(del, 3);
            row.Children.Add(nameTb);
            row.Children.Add(typeCb);
            row.Children.Add(valueHost);
            row.Children.Add(del);
            return row;
        }

        // ---------- 外部变量行 ----------
        private Grid BuildExternalRow(StackPanel host, string name, TypedValue def)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            var nameTb = new TextBox
            {
                Text = name,
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46))
            };

            // 工作副本：有覆盖用覆盖，否则用默认值（编辑即创建/更新覆盖）
            var working = Vars.ExternalOverrides.TryGetValue(name, out var ov) && ov != null
                ? new TypedValue { Type = ov.Type, Raw = ov.Raw, SelectedIndex = ov.SelectedIndex }
                : new TypedValue { Type = def.Type, Raw = def.Raw, SelectedIndex = def.SelectedIndex };

            var valueHost = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            RebuildValueHost(valueHost, working, () => CommitExternal(name, def, working));

            var clear = new Button
            {
                Content = Loc.T("comp.var.clearOverride"),
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 10,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            clear.Click += (s, e) =>
            {
                Vars.ExternalOverrides.Remove(name);
                RebuildExternal(host);
                Notify();
            };

            Grid.SetColumn(nameTb, 0);
            Grid.SetColumn(valueHost, 1);
            Grid.SetColumn(clear, 2);
            row.Children.Add(nameTb);
            row.Children.Add(valueHost);
            row.Children.Add(clear);
            return row;
        }

        private void CommitExternal(string name, TypedValue def, TypedValue working)
        {
            bool same = working.Type == def.Type
                && string.Equals((working.Raw ?? "").ToString(), (def.Raw ?? "").ToString(), StringComparison.Ordinal)
                && working.SelectedIndex == def.SelectedIndex;
            if (same) Vars.ExternalOverrides.Remove(name);
            else Vars.ExternalOverrides[name] = new TypedValue { Type = working.Type, Raw = working.Raw, SelectedIndex = working.SelectedIndex };
            Notify();
        }

        // ---------- 通用：值编辑器（按类型，实时写回 tv 并 Notify）----------
        private void RebuildValueHost(StackPanel host, TypedValue tv, Action onCommit = null)
        {
            host.Children.Clear();
            host.Children.Add(BuildValueEditor(tv, onCommit));
        }

        private UIElement BuildValueEditor(TypedValue tv, Action onCommit)
        {
            var host = new StackPanel { Orientation = Orientation.Horizontal };
            void Commit() => onCommit?.Invoke();
            if (tv.Type == GvType.Color)
            {
                var swatch = new Border
                {
                    Width = 22, Height = 18, Margin = new Thickness(0, 0, 6, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(ToColor((uint)(tv.Raw ?? 0u)))
                };
                var tb = new TextBox { Text = "#" + ((uint)(tv.Raw ?? 0u)).ToString("X8"), MinWidth = 110 };
                tb.TextChanged += (s, e) =>
                {
                    var h = (tb.Text ?? "").Trim().TrimStart('#');
                    if (h.Length == 8 && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                    {
                        swatch.Background = new SolidColorBrush(ToColor(u));
                        tv.Type = GvType.Color; tv.Raw = u;
                        Commit();
                    }
                };
                host.Children.Add(swatch); host.Children.Add(tb);
            }
            else if (tv.Type == GvType.List)
            {
                var optsTb = new TextBox { MinWidth = 120, Text = (tv.Raw as string) ?? "", ToolTip = Loc.T("gv.listHint") };
                optsTb.TextChanged += (s, e) =>
                {
                    tv.Type = GvType.List; tv.Raw = optsTb.Text; tv.SelectedIndex = 0;
                    Commit();
                };
                var selCb = new ComboBox { Margin = new Thickness(6, 0, 0, 0), MinWidth = 80, ToolTip = Loc.T("gv.selHint") };
                FillSel(selCb, tv.Raw as string, tv.SelectedIndex);
                selCb.SelectionChanged += (s, e) =>
                {
                    if (selCb.SelectedIndex < 0) return;
                    tv.Type = GvType.List; tv.Raw = (tv.Raw as string) ?? ""; tv.SelectedIndex = selCb.SelectedIndex;
                    Commit();
                };
                host.Children.Add(optsTb); host.Children.Add(selCb);
            }
            else if (tv.Type == GvType.Switch)
            {
                var cb = new CheckBox
                {
                    Content = Loc.T("gv.type.switch"),
                    IsChecked = tv.Raw is bool b && b,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4))
                };
                cb.Checked += (s, e) => { tv.Type = GvType.Switch; tv.Raw = true; Commit(); };
                cb.Unchecked += (s, e) => { tv.Type = GvType.Switch; tv.Raw = false; Commit(); };
                host.Children.Add(cb);
            }
            else
            {
                var tb = new TextBox { MinWidth = 150, Text = tv.Raw?.ToString() ?? "" };
                tb.TextChanged += (s, e) =>
                {
                    object raw = tb.Text;
                    if (tv.Type == GvType.Number && double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) raw = d;
                    tv.Raw = raw;
                    Commit();
                };
                host.Children.Add(tb);
            }
            return host;
        }

        private static void FillSel(ComboBox cb, string opts, int sel)
        {
            cb.Items.Clear();
            var arr = (opts ?? "").Split('|');
            foreach (var o in arr) cb.Items.Add(o);
            cb.SelectedIndex = arr.Length > 0 ? Math.Min(Math.Max(0, sel), arr.Length - 1) : -1;
        }

        // ---------- 小工具 ----------
        private void Notify() => VarChanged?.Invoke();

        private static string UniqueName(string baseName)
        {
            int i = 1; string n = baseName + i;
            // 仅用于新内部变量；外部行不新建 key
            return n; // 调用方已保证字典无此 key 时再写入；如需去重可在此查 Vars
        }

        private static UIElement MakeScroll(UIElement body)
        {
            var sv = new ScrollViewer { MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            sv.Content = body;
            return sv;
        }

        private static TextBlock Hint(string text) => new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };

        private static Button AddButton(string label, Action onClick)
        {
            var b = new Button
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        // ---------- 持久化：vars 键经 PropertyValue 字符串安全往返 ----------
        public override Dictionary<string, PropertyValue> GetProps()
        {
            var d = base.GetProps();
            d["vars"] = new StaticValue(Vars.ToJson());
            return d;
        }

        public override void SetProps(Dictionary<string, PropertyValue> props)
        {
            base.SetProps(props);
            // 加载后 Id 已更新为持久化值，重新注册到静态表（覆盖构造时生成的临时 Id）
            _registry[Id] = new WeakReference<ComponentAtom>(this);
            if (props.TryGetValue("vars", out var pv))
            {
                // PropertyValue.Serialize 无论 Parse 把 $ 识别为公式与否，都原样返回 JSON 串
                Vars = ComponentVarStore.FromJson(PropertyValue.Serialize(pv));
            }
        }
    }
}
