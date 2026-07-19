using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumen.Core;
using Lumen.Actions;
using Lumen.Atoms;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;
using Lumen.Presets;

namespace Lumen.Ui
{
    /// <summary>
    /// 部件属性编辑器面板（P3 部件级菜单用，Popup 浮层版）：
    /// 按原子的 EditFields 渲染对应控件；支持「实时预览」——字段变更即时写回原子并反映到桌面，
    /// 确定才保存并关闭，取消恢复快照。作为 Popup 的 Child 出现。
    /// 每个字段支持 [值|公式|变量] 三态：值=原控件；公式=公式框+插入函数；变量=全局变量下拉。
    /// </summary>
    public partial class PropertyEditorPanel : UserControl
    {
        private readonly Atom _atom;
        private readonly Action _onPreview;             // 非结构性字段变更：仅更新当前原子视觉
        private readonly Action _onStructuralChange;    // 结构性字段(Choice)变更：重组页面并重锚 Popup
        private readonly Action _onCommit;              // 确定：保存并关闭
        private readonly Action _onCancel;              // 取消：恢复快照并关闭
        private readonly Action _onOpenGvManager;       // 打开全局变量管理器
        private readonly GvStore _gv;
        private readonly EvalContext _ctx;
        private readonly List<EditField> _fields;
        private readonly Dictionary<string, FieldState> _states = new();
        private readonly Dictionary<string, Grid> _fieldRows = new();   // 字段 Key → 其行容器(Grid)，供条件显隐刷新

        /// <summary>隐藏底部确定/取消按钮（供嵌入 PropWindow 时使用，由外部提供应用按钮）。</summary>
        public void HideButtons() { }

        /// <summary>应用当前修改（触发 onStructuralChange + onCommit），供 PropWindow 调用。</summary>
        public void Apply() { _onStructuralChange?.Invoke(); _onCommit?.Invoke(); }

        public PropertyEditorPanel(Atom atom, Action onPreview, Action onStructuralChange, Action onCommit, Action onCancel, GvStore gv, Action onOpenGvManager, EvalContext ctx)
        {
            InitializeComponent();
            _atom = atom;
            _onPreview = onPreview;
            _onStructuralChange = onStructuralChange;
            _onCommit = onCommit;
            _onCancel = onCancel;
            _gv = gv;
            _onOpenGvManager = onOpenGvManager;
            _ctx = ctx;
            _fields = atom.EditFields();
            BuildTabs();
        }

        // 每个字段的运行时状态：当前模式 + 各模式控件 + 读取器
        private class FieldState
        {
            public EditField Field;
            public PropMode Mode;
            public Func<string> ReadValue;     // 值 模式读取器
            public TextBox FormulaTb;
            public TextBlock FormulaStatus;    // 公式语法 ✓/✗ 状态行
            public TextBlock FormulaPreview;   // 公式运算结果预览
            public ComboBox GvCb;
            public UIElement ValueHost;
            public UIElement FormulaHost;
            public UIElement GvHost;
            public FrameworkElement ModeDotHost;    // 右侧绑定状态指示器（f=公式 G=变量 空=静态）

            public string Reader()
            {
                switch (Mode)
                {
                    case PropMode.Formula:
                        {
                            var t = (FormulaTb?.Text ?? "").Trim();
                            if (t.StartsWith("$") && t.EndsWith("$")) return t;
                            return "$" + t + "$";
                        }
                    case PropMode.Global:
                        {
                            var n = (GvCb?.SelectedItem as string) ?? "";
                            if (string.IsNullOrEmpty(n) || n == Loc.T("prop.gv.none")) return "";
                            return "gv:" + n;
                        }
                    default:
                        return ReadValue != null ? ReadValue() : "";
                }
            }
        }

        // ---------- 分页：由原子 EditTabs() 决定有序标签页，字段按 Tab 键归位；交互/触发器/自定义页特殊处理 ----------
        private static readonly HashSet<string> KnownTabKeys = new HashSet<string> { "content", "style", "texture", "layer", "layout", "animation", "interaction", "flow" };

        /// <summary>构建的 Tab 内容，key = TabSpec.Key，value = (UIElement, LocKey) 对。</summary>
        private readonly Dictionary<string, (UIElement Content, string LocKey)> _tabContents = new Dictionary<string, (UIElement, string)>();

        public IReadOnlyDictionary<string, (UIElement Content, string LocKey)> TabContents => _tabContents;

        private void BuildTabs()
        {
            _tabContents.Clear();

            // 一次性构建所有字段行，按 Tab 键归类
            var rowsByKey = new Dictionary<string, List<Grid>>();
            var props = _atom.GetProps();
            foreach (var f in _fields)
            {
                var raw = props.TryGetValue(f.Key, out var pv) ? PropertyValue.Serialize(pv) : "";
                var mode = raw.StartsWith("gv:", StringComparison.OrdinalIgnoreCase) ? PropMode.Global
                         : raw.Contains("$") ? PropMode.Formula : PropMode.Static;
                var st = new FieldState { Field = f, Mode = mode };
                _states[f.Key] = st;
                var row = BuildFieldRow(f, raw, st);
                _fieldRows[f.Key] = row;
                var key = f.Tab ?? "content";
                if (!rowsByKey.TryGetValue(key, out var lst)) { lst = new List<Grid>(); rowsByKey[key] = lst; }
                lst.Add(row);
            }
            RefreshConditionVisibility();

            // 按原子声明的顺序生成标签页
            foreach (var spec in _atom.EditTabs())
            {
                UIElement body;
                if (spec.Key == "interaction") body = BuildInteractionBlock();
                else if (spec.Key == "flow") body = BuildFlowBlock();
                else if (_atom is ICustomTabProvider c && !KnownTabKeys.Contains(spec.Key))
                {
                    body = c.BuildCustomTab(spec.Key);
                    // 组件变量编辑器改动后：刷新预览 + 重组页面（使子树 cg 重新求值）并保存
                    if (_atom is ComponentAtom comp)
                        comp.VarChanged = () => { _onPreview?.Invoke(); _onStructuralChange?.Invoke(); };
                }
                else
                {
                    if (!rowsByKey.TryGetValue(spec.Key, out var lst) || lst.Count == 0) continue; // 空字段页跳过
                    var sp = new StackPanel { Orientation = Orientation.Vertical };
                    foreach (var r in lst) sp.Children.Add(r);
                    body = sp;
                }
                if (body == null) continue;
                var sv = new ScrollViewer { MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                sv.Content = body;
                _tabContents[spec.Key] = (sv, spec.LocKey);
            }
        }

        /// <summary>把单个字段渲染为一行（左侧标签 + 中间输入 + 右侧模式点）。</summary>
        private Grid BuildFieldRow(EditField f, string raw, FieldState st)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = f.Label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = Theme.TextSecondary
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            st.ValueHost = BuildValueEditor(f, raw, st);
            st.FormulaHost = BuildFormulaHost(st);
            st.GvHost = BuildGvHost(st);
            SyncHostVisibility(st);

            var host = new Grid();
            host.Children.Add(st.ValueHost);
            host.Children.Add(st.FormulaHost);
            host.Children.Add(st.GvHost);

            var inputRow = new StackPanel { Orientation = Orientation.Horizontal };
            var modeDot = MakeInteractiveModeDot(st);
            inputRow.Children.Add(modeDot);
            inputRow.Children.Add(host);
            Grid.SetColumn(inputRow, 1);
            row.Children.Add(inputRow);

            // 右侧绑定状态指示器
            st.ModeDotHost = MakeModeDot(st);
            Grid.SetColumn(st.ModeDotHost, 2);
            row.Children.Add(st.ModeDotHost);

            // 提示文字
            if (!string.IsNullOrEmpty(f.Hint))
            {
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(label, 0);
                Grid.SetRow(inputRow, 0);
                Grid.SetRow(st.ModeDotHost, 0);
                var hint = new TextBlock { Text = f.Hint, FontSize = 10, Foreground = Theme.TextTertiary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
                Grid.SetColumnSpan(hint, 3);
                Grid.SetRow(hint, 1);
                row.Children.Add(hint);
            }

            return row;
        }

        /// <summary>统一数值输入框样式：深色背景 + 边框 + 圆角 + 内边距（数值字段与 Slider 旁输入框一致）。</summary>
        private static TextBox MakeNumberBox(string text, double minWidth, Action<string> onChanged)
        {
            var tb = new TextBox
            {
                Text = text,
                MinWidth = minWidth,
                Background = Theme.BgBase,
                Foreground = Theme.TextPrimary,
                CaretBrush = Theme.TextPrimary,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2)
            };
            if (onChanged != null) tb.TextChanged += (s, e) => onChanged(tb.Text);
            return tb;
        }

        /// <summary>统一下拉框样式：深色背景 + 边框 + 主题色文字。</summary>
        private static ComboBox MakeStyledComboBox(int minHeight = 24, bool editable = false)
        {
            return new ComboBox
            {
                MinHeight = minHeight,
                IsEditable = editable,
                Background = Theme.BgBase,
                Foreground = Theme.TextSecondary,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1)
            };
        }

        /// <summary>统一复选框样式：主题色文字。</summary>
        private static CheckBox MakeStyledCheckBox()
        {
            return new CheckBox
            {
                Foreground = Theme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>统一滑块样式。</summary>
        private static Slider MakeStyledSlider(double min, double max, double val, double width = 150)
        {
            return new Slider
            {
                Minimum = min,
                Maximum = max,
                Width = width,
                Value = val,
                VerticalAlignment = VerticalAlignment.Center,
                TickFrequency = (max - min) / 20
            };
        }

        /// <summary>统一样式按钮：深色背景 + 白色文字 + 无边框。</summary>
        private static Button MakeStyledButton(string content, double width = 70, Thickness? margin = null)
        {
            return new Button
            {
                Content = content,
                Width = width,
                Background = Theme.BgHover,
                Foreground = Theme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 1, 8, 1),
                Margin = margin ?? new Thickness(0)
            };
        }

        /// <summary>左侧交互模式点：点击循环 Static→Formula→Global，样式即时切换。
        /// IsHitTestVisible=false 确保点击落在 Border 整圆区域，而非内部字符上。</summary>
        private UIElement MakeInteractiveModeDot(FieldState st)
        {
            var dot = new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                IsHitTestVisible = !FieldIsStaticOnly(st.Field)
            };
            var text = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontSize = 8, FontWeight = FontWeights.Bold, IsHitTestVisible = false,
                ToolTip = Loc.T("prop.mode.cycle")
            };
            dot.Child = text;
            void Apply()
            {
                if (FieldIsStaticOnly(st.Field))
                {
                    dot.Visibility = Visibility.Collapsed;
                    return;
                }
                dot.Visibility = Visibility.Visible;
                dot.Background = null;
                switch (st.Mode)
                {
                    case PropMode.Formula:
                        dot.BorderBrush = new SolidColorBrush(Theme.FgFormula.Color);
                        text.Text = "\u0192"; text.Foreground = new SolidColorBrush(Theme.FgFormula.Color); break;
                    case PropMode.Global:
                        dot.Background = new SolidColorBrush(Theme.FgGlobal.Color);
                        dot.BorderBrush = new SolidColorBrush(Theme.FgGlobal.Color);
                        text.Text = "G"; text.Foreground = Brushes.White; break;
                    default:
                        dot.BorderBrush = new SolidColorBrush(Theme.TextTertiary.Color);
                        text.Text = ""; text.Foreground = new SolidColorBrush(Theme.TextTertiary.Color); break;
                }
            }
            dot.MouseLeftButtonUp += (s, e) =>
            {
                st.Mode = st.Mode switch { PropMode.Static => PropMode.Formula, PropMode.Formula => PropMode.Global, _ => PropMode.Static };
                SyncHostVisibility(st); Apply(); UpdateModeDot(st);
                if (st.Mode == PropMode.Formula && st.FormulaTb != null) st.FormulaTb.Focus();
                if (st.Mode == PropMode.Global && st.GvCb != null && st.GvCb.Items.Count > 0 && st.GvCb.SelectedItem == null)
                    st.GvCb.SelectedIndex = 0;
                Preview(st.Field);
            };
            Apply();
            return dot;
        }

        /// <summary>判断字段是否仅支持静态模式（Choice/Bool 等）。此类字段不显示模式点，也不支持公式/变量。</summary>
        private static bool FieldIsStaticOnly(EditField f) => f.Kind switch
        {
            EditKind.Choice => true,
            EditKind.Bool => true,
            _ => false
        };

        /// <summary>右侧绑定状态点：公式模式红点 / 否则空心（白底灰描边）。</summary>
        private static FrameworkElement MakeModeDot(FieldState st)
        {
            var tb = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Width = 18,
                Height = 18,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                ToolTip = Loc.T("prop.formula.unbound")
            };
            st.ModeDotHost = tb;
            UpdateModeDot(st);
            return tb;
        }

        private static void UpdateModeDot(FieldState st)
        {
            if (st.ModeDotHost is not TextBlock tb) return;
            switch (st.Mode)
            {
                case PropMode.Formula:
                    tb.Text = "ƒ";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    tb.ToolTip = Loc.T("prop.formula.bound");
                    break;
                case PropMode.Global:
                    tb.Text = "G";
                    tb.Foreground = Theme.OkGreen;
                    tb.ToolTip = Loc.T("prop.mode.global");
                    break;
                default:
                    tb.Text = "";
                    tb.ToolTip = Loc.T("prop.formula.unbound");
                    break;
            }
        }

        // ---------- 点击动作交互区块（P5 行为系统） ----------
        private StackPanel BuildInteractionBlock()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            var ae = new ActionEditor(_atom, _onPreview, _ctx);
            var options = ActionEditor.ActionKindOptions();

            panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
            panel.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.interaction.title"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = Theme.TextPrimary
            });

            var cur = _atom.ClickAction ?? AtomAction.None();

            var kindCb = MakeStyledComboBox();
            kindCb.Margin = new Thickness(0, 0, 0, 4);
            foreach (var (kind, key) in options) kindCb.Items.Add(Loc.T(key));
            int sel = 0;
            for (int i = 0; i < options.Length; i++) if (options[i].kind == cur.Kind) { sel = i; break; }
            kindCb.SelectedIndex = sel;

            var argHost = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 0) };
            var hint = new TextBlock
            {
                FontSize = 10,
                Foreground = Theme.TextTertiary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };

            ActionKind CurKind() => options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;

            void SetArg(string v)
            {
                var k = CurKind();
                _atom.ClickAction = (k == ActionKind.None) ? AtomAction.None() : new AtomAction { Kind = k, Arg = v };
                _onPreview?.Invoke();
            }

            void UpdateHint()
            {
                var k = CurKind();
                hint.Text = k switch
                {
                    ActionKind.RunApp => Loc.T("prop.hint.runApp", Loc.T("prop.browse")),
                    ActionKind.MediaControl => Loc.T("prop.hint.mediaControl"),
                    ActionKind.SwitchPage => Loc.T("prop.hint.switchPage"),
                    ActionKind.OpenURL => Loc.T("prop.hint.openUrl"),
                    ActionKind.Command => Loc.T("prop.hint.command", Loc.T("prop.browse")),
                    ActionKind.SwitchPreset => Loc.T("prop.hint.switchPreset"),
                    ActionKind.RunFlow => Loc.T("prop.hint.runFlow"),
                    ActionKind.SetVar => Loc.T("prop.hint.setVar"),
                    ActionKind.Delay => Loc.T("prop.hint.delay"),
                    ActionKind.ReadFile => Loc.T("prop.hint.readFile"),
                    _ => Loc.T("prop.hint.default")
                };
            }

            // 依据当前动作类型构建参数控件：枚举类参数用下拉，自由文本类用文本框(+浏览)
            void RebuildArg()
            {
                argHost.Children.Clear();
                var k = CurKind();
                var curArg = (_atom.ClickAction != null && _atom.ClickAction.Kind == k) ? (_atom.ClickAction.Arg ?? "") : "";
                if (k == ActionKind.None || k == ActionKind.ToggleEditMode || k == ActionKind.OpenSettings || k == ActionKind.LockScreen)
                    return; // 无参数动作

                if (k == ActionKind.MediaControl)
                {
                    argHost.Children.Add(ActionEditor.BuildDropdown(curArg, SetArg,
                        ("play", Loc.T("prop.arg.play")), ("pause", Loc.T("prop.arg.pause")),
                        ("next", Loc.T("prop.arg.next")), ("prev", Loc.T("prop.arg.prev")), ("stop", Loc.T("prop.arg.stop"))));
                    return;
                }
                if (k == ActionKind.SwitchPreset)
                {
                    var items = new List<(string raw, string label)>
                    {
                        ("+1", Loc.T("prop.arg.presetNext")), ("-1", Loc.T("prop.arg.presetPrev"))
                    };
                    foreach (var p in PresetLibrary.Builtins.Concat(PresetLibrary.User))
                        items.Add((p.Name, p.Name));
                    argHost.Children.Add(ActionEditor.BuildDropdown(curArg, SetArg, items.ToArray()));
                    return;
                }
                if (k == ActionKind.SwitchPage)
                {
                    var pc = LumenWindow.Main?.PageCount ?? 0;
                    var items = new List<(string raw, string label)>
                    {
                        ("+1", Loc.T("prop.arg.pageNext")), ("-1", Loc.T("prop.arg.pagePrev"))
                    };
                    for (int i = 0; i < pc; i++) items.Add((i.ToString(), Loc.T("prop.arg.pageN", (i + 1).ToString())));
                    argHost.Children.Add(ActionEditor.BuildDropdown(curArg, SetArg, items.ToArray()));
                    return;
                }
                // 新动作类型（RunFlow / SetVar / Delay / ReadFile）：组合参数控件
                var comp = ae.BuildCompositeArg(k, curArg, SetArg);
                if (comp != null) { argHost.Children.Add(comp); return; }
                // RunApp / OpenURL / Command：文本框（RunApp/Command 附浏览按钮）
                var argTb = new TextBox { MinWidth = 220, Text = curArg };
                argTb.TextChanged += (s, e) => SetArg(argTb.Text);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(argTb);
                if (k == ActionKind.RunApp || k == ActionKind.Command)
                {
                    var browseBtn = MakeStyledButton(Loc.T("prop.browse"), margin: new Thickness(8, 0, 0, 0));
                    browseBtn.Padding = new Thickness(8, 0, 8, 0);
                    browseBtn.Click += (s, e) =>
                    {
                        var picked = FilePickerWindow.PickFile(Window.GetWindow(this), Loc.T("prop.dlg.exeFilter"), argTb.Text);
                        if (!string.IsNullOrEmpty(picked)) argTb.Text = picked;
                    };
                    row.Children.Add(browseBtn);
                }
                argHost.Children.Add(row);
            }

            kindCb.SelectionChanged += (s, e) => { RebuildArg(); UpdateHint(); };
            RebuildArg();
            UpdateHint();

            panel.Children.Add(new TextBlock { Text = Loc.T("prop.action.type"), FontSize = 11, Foreground = Theme.TextSecondary, Margin = new Thickness(0, 4, 0, 2) });
            panel.Children.Add(kindCb);
            panel.Children.Add(new TextBlock { Text = Loc.T("prop.arg"), FontSize = 11, Foreground = Theme.TextSecondary, Margin = new Thickness(0, 6, 0, 2) });
            panel.Children.Add(argHost);
            panel.Children.Add(hint);

            return panel;
        }

        // ---------- 流程区块（P5 流程系统：条件 → 自动执行动作序列） ----------
        // 流程 / 动作编辑逻辑已解耦至 ActionEditor，本段仅保留 flow 标签页的委托入口。
        // flow 标签页：委托给解耦的 ActionEditor 模块
        private UIElement BuildFlowBlock()
        {
            return new ActionEditor(_atom, _onPreview, _ctx).BuildFlowBlock();
        }

        // ---------- 模式切换条 ----------
        private UIElement MakeModeToggle(FieldState st)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var modes = new[] {
                (PropMode.Static, "prop.mode.value"),
                (PropMode.Formula, "prop.mode.formula"),
                (PropMode.Global, "prop.mode.global"),
            };
            var buttons = new List<Button>();
            foreach (var (m, txt) in modes)
            {
                var b = MakeStyledButton(Loc.T(txt), 48);
                b.FontSize = 11;
                b.Margin = new Thickness(0, 0, 4, 0);
                b.Padding = new Thickness(0, 2, 0, 2);
                b.Tag = m;
                b.Click += (s, e) =>
                {
                    st.Mode = m;
                    if (m == PropMode.Global && st.GvCb != null && st.GvCb.Items.Count > 0 && st.GvCb.SelectedItem == null)
                        st.GvCb.SelectedIndex = 0;
                    if (m == PropMode.Formula && st.FormulaTb != null) st.FormulaTb.Focus();
                    SyncHostVisibility(st);
                    HighlightToggle(buttons, m);
                    UpdateModeDot(st);
                    Preview(st.Field);
                };
                buttons.Add(b);
                panel.Children.Add(b);
            }
            HighlightToggle(buttons, st.Mode);
            return panel;
        }

        private static void HighlightToggle(List<Button> buttons, PropMode active)
        {
            foreach (var b in buttons)
            {
                var on = (PropMode)b.Tag == active;
                b.Background = new SolidColorBrush(on ? Theme.BgActive.Color : Theme.BgHover.Color);
                b.Foreground = new SolidColorBrush(on ? Theme.TextPrimary.Color : Theme.TextSecondary.Color);
            }
        }

        private static void SyncHostVisibility(FieldState st)
        {
            st.ValueHost.Visibility = st.Mode == PropMode.Static ? Visibility.Visible : Visibility.Collapsed;
            st.FormulaHost.Visibility = st.Mode == PropMode.Formula ? Visibility.Visible : Visibility.Collapsed;
            st.GvHost.Visibility = st.Mode == PropMode.Global ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------- 值 模式：按 Kind 建控件 ----------
        private UIElement BuildValueEditor(EditField f, string raw, FieldState st)
        {
            switch (f.Kind)
            {
                case EditKind.Choice:
                    {
                        var cb = MakeStyledComboBox();
                        // 每项：Tag=规范值（持久化用），Content=本地化名（无前缀则直接显示规范值）
                        ComboBoxItem MatchRaw(string rawVal)
                        {
                            foreach (ComboBoxItem it in cb.Items)
                                if ((it.Tag as string) == rawVal) return it;
                            return null;
                        }
                        if (f.Choices != null && f.Choices.Length > 0)
                        {
                            var prefix = f.ChoiceLocPrefix;
                            foreach (var c in f.Choices)
                            {
                                var item = new ComboBoxItem { Tag = c };
                                item.Content = string.IsNullOrEmpty(prefix) ? c : Loc.T(prefix + c);
                                cb.Items.Add(item);
                            }
                            // 默认选中：raw 命中则用 raw，否则选首项（避免空选→下拉栏显扁）
                            cb.SelectedItem = MatchRaw(raw) ?? cb.Items[0];
                        }
                        st.ReadValue = () => (cb.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                        cb.SelectionChanged += (s, e) => Preview(f);
                        return cb;
                    }
                case EditKind.Color:
                    return BuildColorEditor(raw, st);
                case EditKind.File:
                    {
                        var sp = new StackPanel { Orientation = Orientation.Horizontal };
                        var tb = new TextBox { Text = raw, MinWidth = 180 };
                        var btn = MakeStyledButton(Loc.T("prop.browse"), margin: new Thickness(8, 0, 0, 0));
                        btn.Click += (s, e) =>
                        {
                            var picked = FilePickerWindow.PickFile(Window.GetWindow(this), Loc.T("dlg.bgImage.filter"), tb.Text);
                            if (picked != null) tb.Text = picked;
                        };
                        tb.TextChanged += (s, e) => Preview(f);
                        sp.Children.Add(tb);
                        sp.Children.Add(btn);
                        st.ReadValue = () => tb.Text;
                        return sp;
                    }
                case EditKind.Number:
                    {
                        var tb = MakeNumberBox(raw, 120, _ => Preview(f));
                        st.ReadValue = () => tb.Text;
                        return tb;
                    }
                case EditKind.Bool:
                    {
                        var cb = MakeStyledCheckBox();
                        cb.IsChecked = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
                        st.ReadValue = () => cb.IsChecked == true ? "1" : "0";
                        cb.Checked += (s, e) => Preview(f);
                        cb.Unchecked += (s, e) => Preview(f);
                        return cb;
                    }
                case EditKind.Slider:
                    {
                        double.TryParse(raw, out var cur);
                        if (double.IsNaN(cur)) cur = (f.Min + f.Max) / 2;
                        var sl = MakeStyledSlider(f.Min, f.Max, cur);
                        var num = MakeNumberBox(cur.ToString("0.##"), 56, tbText =>
                        {
                            if (double.TryParse(tbText, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                            {
                                if (v < f.Min) v = f.Min;
                                if (v > f.Max) v = f.Max;
                                sl.Value = v;
                                Preview(f);
                            }
                        });
                        num.Width = 56;
                        num.Margin = new Thickness(8, 0, 0, 0);
                        sl.ValueChanged += (s, e) => { num.Text = sl.Value.ToString("0.##"); Preview(f); };
                        var sp = new StackPanel { Orientation = Orientation.Horizontal };
                        sp.Children.Add(sl);
                        sp.Children.Add(num);
                        st.ReadValue = () => sl.Value.ToString("0.##");
                        return sp;
                    }
                default:
                    {
                        var tb = new TextBox { Text = raw, MinWidth = 220, ToolTip = f.Hint };
                        tb.TextChanged += (s, e) => Preview(f);
                        st.ReadValue = () => tb.Text;
                        return tb;
                    }
            }
        }

        // ---------- 色卡编辑器（ARGB 内嵌展开） ----------
        private UIElement BuildColorEditor(string raw, FieldState st)
        {
            ColorPickerWindow.ParseHex(raw, out byte a, out byte r, out byte g, out byte b);

            var swatch = new Border
            {
                Width = 28, Height = 22,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(a, r, g, b)),
                Cursor = Cursors.Hand
            };
            var hex = new TextBox { Text = ColorPickerWindow.ToHex(a, r, g, b), MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
            var pickBtn = MakeStyledButton(Loc.T("prop.palette"), margin: new Thickness(6, 0, 0, 0));
            pickBtn.Padding = new Thickness(6, 1, 6, 1);

            var topRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            topRow.Children.Add(swatch);
            topRow.Children.Add(hex);
            topRow.Children.Add(pickBtn);

            hex.TextChanged += (s, e) =>
            {
                ColorPickerWindow.ParseHex(hex.Text, out byte na, out byte nr, out byte ng, out byte nb);
                swatch.Background = new SolidColorBrush(Color.FromArgb(na, nr, ng, nb));
                Preview(st.Field);
            };
            swatch.MouseLeftButtonDown += (s, e) => OpenColorPicker(hex, swatch, st.Field);
            pickBtn.Click += (s, e) => OpenColorPicker(hex, swatch, st.Field);

            st.ReadValue = () => hex.Text.Trim();
            var wrap = new StackPanel { Orientation = Orientation.Vertical };
            wrap.Children.Add(topRow);
            return wrap;
        }

        /// <summary>独立取色弹窗——ARGB 滑块 + Hex + 实时色块 + 确定/取消。</summary>
        private void OpenColorPicker(TextBox hex, Border swatch, EditField field)
        {
            var picked = ColorPickerWindow.PickColor(hex.Text, Window.GetWindow(this));
            if (picked == null) return;
            hex.Text = picked;
            // swatch 颜色由 hex.TextChanged 触发更新
        }

        // ---------- 公式 模式宿主（纯输入框 + 实时语法校验 + 结果预览 + 焦点参考弹窗） ----------
        private UIElement BuildFormulaHost(FieldState st)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            // 纯 TextBox（不再用双层透明 FormulaTextBox，避免 StaysOpen 弹窗下焦点/光标异常 → 按键冲突）。
            var ftb = new TextBox
            {
                MinWidth = 220,
                MinHeight = 48,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 160,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = Theme.BgBase,
                Foreground = Theme.TextPrimary,
                CaretBrush = Theme.TextPrimary,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Top,
                ToolTip = Loc.T("prop.formula.tooltip")
            };
            st.FormulaTb = ftb;
            // 初始填充：剥去外层 $...$（原始序列化带 $）
            var rawExpr = _atom.GetProps().TryGetValue(st.Field.Key, out var pv) ? PropertyValue.Serialize(pv) : "";
            if (rawExpr.StartsWith("$") && rawExpr.EndsWith("$")) rawExpr = rawExpr.Substring(1, rawExpr.Length - 2);
            ftb.Text = rawExpr;
            ftb.TextChanged += (s, e) => { Preview(st.Field); UpdateFormulaStatus(st); UpdateFormulaPreview(st); };

            var status = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed
            };
            st.FormulaStatus = status;

            var preview = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed
            };
            st.FormulaPreview = preview;

            // 函数选择弹窗：右上「📖 函数…」按钮触发
            var refBtn = MakeStyledButton(Loc.T("prop.formula.functions"));
            refBtn.Padding = new Thickness(6, 2, 6, 2);
            refBtn.Margin = new Thickness(0, 4, 0, 0);
            refBtn.HorizontalAlignment = HorizontalAlignment.Right;
            refBtn.Click += (s, e) => OpenFunctionPicker(st);

            sp.Children.Add(ftb);
            sp.Children.Add(status);
            sp.Children.Add(preview);
            sp.Children.Add(refBtn);
            UpdateFormulaStatus(st);
            UpdateFormulaPreview(st);
            return sp;
        }

        /// <summary>打开二级菜单函数选择器（左侧分类 / 右侧函数）。</summary>
        private void OpenFunctionPicker(FieldState st)
        {
            var owner = Window.GetWindow(this);
            var win = new Window
            {
                Title = Loc.T("prop.func.title"), Width = 520, Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = (owner != null && !owner.AllowsTransparency) ? owner : null,
                Background = Theme.BgBase,
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 标题
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 公式栏（输入+预览）
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 主体
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 底部

            // 标题栏
            var titleBar = new Border
            {
                Background = Theme.BgSurface,
                Padding = new Thickness(10, 6, 10, 6)
            };
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) win.DragMove(); };
            titleBar.Child = new TextBlock { Text = Loc.T("prop.func.title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Theme.TextPrimary };
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // 公式栏：输入框（联动主公式框）+ 实时预览
            var formulaBar = new Grid { Margin = new Thickness(8, 8, 8, 4) };
            formulaBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            formulaBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formulaBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            var fLabel = new TextBlock
            {
                Text = Loc.T("prop.func.formula"),
                Foreground = Theme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var fInput = new TextBox
            {
                Text = st.FormulaTb.Text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = Theme.BgBase,
                Foreground = Theme.TextPrimary,
                CaretBrush = Theme.TextPrimary,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 2, 4, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var fPreview = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Theme.OkGreen
            };
            fInput.TextChanged += (s, e) =>
            {
                st.FormulaTb.Text = fInput.Text;       // 写回主公式框 → 触发 Preview/Status/Preview
                UpdateFormulaPreview(st);
                if (st.FormulaPreview != null)
                {
                    fPreview.Text = st.FormulaPreview.Text;
                    fPreview.Foreground = st.FormulaPreview.Foreground;
                    fPreview.Visibility = st.FormulaPreview.Visibility;
                }
            };
            Grid.SetColumn(fLabel, 0);
            Grid.SetColumn(fInput, 1);
            Grid.SetColumn(fPreview, 2);
            formulaBar.Children.Add(fLabel);
            formulaBar.Children.Add(fInput);
            formulaBar.Children.Add(fPreview);
            Grid.SetRow(formulaBar, 1);
            root.Children.Add(formulaBar);

            // 主体：左右分栏
            var body = new Grid { Margin = new Thickness(8) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var catList = new StackPanel();
            var fnScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fnPanel = new StackPanel();
            fnScroll.Content = fnPanel;
            Grid.SetColumn(catList, 0); Grid.SetColumn(fnScroll, 2);
            body.Children.Add(catList);
            body.Children.Add(fnScroll);
            Grid.SetRow(body, 2);
            root.Children.Add(body);

            // 底部关闭
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0, 8, 8) };
            var closeBtn = MakeStyledButton(Loc.T("settings.close"), 80);
            closeBtn.Padding = new Thickness(0, 3, 0, 3);
            closeBtn.Click += (s, e) => win.Close();
            footer.Children.Add(closeBtn);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            // 一级菜单：分类（基于已有公式重组——已用分类置顶并加 ● 标记）
            var categories = FunctionCatalog.All.Select(f => f.Category).Distinct().ToList();
            // 解析已有公式引用过的函数名（形如 xxx(），不区分大小写）
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            {
                var s = st.FormulaTb.Text ?? "";
                for (int i = 0; i < s.Length; i++)
                {
                    if (char.IsLetter(s[i]) || s[i] == '_')
                    {
                        int j = i;
                        while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
                        if (j < s.Length && s[j] == '(') usedNames.Add(s.Substring(i, j - i));
                        i = j;
                    }
                }
            }
            var usedCats = new HashSet<string>(FunctionCatalog.All.Where(f => usedNames.Contains(f.Name)).Select(f => f.Category));
            var orderedCats = categories.Where(c => usedCats.Contains(c)).Concat(categories.Where(c => !usedCats.Contains(c))).ToList();
            var catButtons = new List<Button>();
            void HighlightCat(string cat)
            {
                foreach (var b in catButtons)
                {
                    var on = (b.Tag as string) == cat;
                    b.Background = new SolidColorBrush(on ? Theme.BgActive.Color : Theme.BgSurface.Color);
                    b.Foreground = new SolidColorBrush(on ? Theme.TextPrimary.Color : Theme.TextSecondary.Color);
                }
            }
            void ShowFunctions(string cat)
            {
                fnPanel.Children.Clear();
                var allFn = FunctionCatalog.All.Where(f => f.Category == cat).OrderBy(f => usedNames.Contains(f.Name) ? 0 : 1).ToList();
                fnPanel.Children.Add(new TextBlock { Text = cat, FontSize = 10, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 0, 0, 6) });
                foreach (var fn in allFn)
                {
                    bool isUsed = usedNames.Contains(fn.Name);
                    var card = new Border
                    {
                        Background = Theme.BgSurface,
                        BorderBrush = new SolidColorBrush(isUsed ? Theme.UsedGreen.Color : Theme.BorderDefault.Color),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 0, 6),
                        Padding = new Thickness(10, 8, 10, 8),
                        CornerRadius = new CornerRadius(6)
                    };
                    var sp = new StackPanel();
                    if (isUsed)
                        sp.Children.Add(new TextBlock { Text = "● used", FontSize = 10, Foreground = Theme.UsedGreen, Margin = new Thickness(0, 0, 0, 4) });

                    // 函数签名 + Insert 按钮
                    var topRow = new StackPanel { Orientation = Orientation.Horizontal };
                    topRow.Children.Add(new TextBlock { Text = fn.Sig, Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
                    var insBtn = new Button
                    {
                        Content = "Insert", FontSize = 10,
                        Background = Theme.BgHover, Foreground = Theme.TextPrimary, BorderThickness = new Thickness(0),
                        Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand
                    };
                    insBtn.Click += (s, e) => InsertFunction(fn.Insert);
                    topRow.Children.Add(insBtn);
                    sp.Children.Add(topRow);

                    // 描述
                    sp.Children.Add(new TextBlock { Text = fn.Desc, Foreground = Theme.TextTertiary, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });

                    // 内参标签（点击填入函数+参数）
                    if (fn.Params != null && fn.Params.Length > 0)
                    {
                        var tagRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                        foreach (var param in fn.Params)
                        {
                            var tag = new Border
                            {
                                Background = Theme.BgBase,
                                BorderBrush = Theme.BorderSoft,
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                Margin = new Thickness(0, 0, 4, 0),
                                Cursor = Cursors.Hand,
                                Tag = param
                            };
                            tag.Child = new TextBlock { Text = param, FontSize = 10, Foreground = Theme.TextSecondary };
                            tag.MouseLeftButtonUp += (s, e) =>
                            {
                                var p = (s as Border)?.Tag as string ?? "";
                                // 智能填入：把函数插入文本的括号内填上参数
                                var insert = fn.Insert;
                                int paren = insert.IndexOf('(');
                                int parenEnd = insert.LastIndexOf(')');
                                if (paren >= 0 && parenEnd > paren)
                                {
                                    var name = insert.Substring(0, paren + 1);
                                    insert = name + p + ")";
                                }
                                InsertFunction(insert);
                            };
                            tagRow.Children.Add(tag);
                        }
                        sp.Children.Add(tagRow);
                    }

                    card.Child = sp;

                    // 整卡点击也插入
                    card.MouseLeftButtonUp += (s, e) => InsertFunction(fn.Insert);

                    fnPanel.Children.Add(card);
                }
            }
            foreach (var cat in orderedCats)
            {
                bool isUsed = usedCats.Contains(cat);
                var b = new Button
                {
                    Content = isUsed ? "● " + cat : cat,
                    Tag = cat,
                    FontWeight = isUsed ? FontWeights.Bold : FontWeights.Normal,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Theme.BgSurface,
                    BorderBrush = Theme.BorderDefault,
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                b.Click += (s, e) => { ShowFunctions(cat); HighlightCat(cat); };
                catButtons.Add(b);
                catList.Children.Add(b);
            }
            var firstCat = orderedCats.FirstOrDefault(c => usedCats.Contains(c)) ?? orderedCats.First();
            ShowFunctions(firstCat);
            HighlightCat(firstCat);

            void InsertFunction(string insert)
            {
                var target = st.FormulaTb;
                int caret = target.CaretIndex;
                string cur = target.Text ?? "";
                target.Text = cur.Insert(caret < 0 ? cur.Length : caret, insert);
                target.Focus();
                target.CaretIndex = caret < 0 ? target.Text.Length : caret + insert.Length;
                Preview(st.Field);
                UpdateFormulaStatus(st);
                UpdateFormulaPreview(st);
                win.Close();
            }

            win.Content = new Border
            {
                Background = Theme.BgBase,
                BorderBrush = Theme.BorderDefault,
                BorderThickness = new Thickness(1), Child = root
            };
            win.ShowDialog();
        }

        /// <summary>公式运算结果实时预览（对应 Kustom 求值回显）：绿色=结果，红色=求值失败；动作函数 mu/an 预览不执行以免副作用。</summary>
        private void UpdateFormulaPreview(FieldState st)
        {
            if (st.FormulaPreview == null || st.FormulaTb == null) return;
            var inner = (st.FormulaTb.Text ?? "").Trim();
            if (inner.Length == 0)
            {
                st.FormulaPreview.Visibility = Visibility.Collapsed;
                return;
            }
            // 语法校验（与状态行同口径），语法不过不预览
            var toParse = inner;
            if (toParse.StartsWith("$") && toParse.EndsWith("$") && toParse.Length >= 2)
                toParse = toParse.Substring(1, toParse.Length - 2);
            try { Parser.Parse(Lexer.Tokenize(toParse)); }
            catch { st.FormulaPreview.Visibility = Visibility.Collapsed; return; }

            if (_ctx == null) { st.FormulaPreview.Visibility = Visibility.Collapsed; return; }

            if (inner.Contains("mu("))
            {
                st.FormulaPreview.Text = Loc.T("prop.formula.actionNoPreview");
                st.FormulaPreview.Foreground = Theme.TextDisabled;
                st.FormulaPreview.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                string toEval = inner.Contains("$") ? inner : "$" + inner + "$";
                var result = _ctx.EvalText(toEval);
                st.FormulaPreview.Text = Loc.T("prop.formula.result", result);
                st.FormulaPreview.Foreground = Theme.OkGreen;
                st.FormulaPreview.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                st.FormulaPreview.Text = Loc.T("prop.formula.evalFail", ex.Message.Split('\n')[0]);
                st.FormulaPreview.Foreground = Theme.ErrRed;
                st.FormulaPreview.Visibility = Visibility.Visible;
            }
        }

        /// <summary>公式语法实时校验（Lexer+Parser），对应 Kustom InputTextFieldError：红框 + ✓/✗ 状态行。</summary>
        private void UpdateFormulaStatus(FieldState st)
        {
            if (st.FormulaStatus == null || st.FormulaTb == null) return;
            var expr = (st.FormulaTb.Text ?? "").Trim();
            if (expr.Length == 0)
            {
                st.FormulaTb.BorderBrush = Theme.BorderSoft;
                st.FormulaTb.BorderThickness = new Thickness(1);
                st.FormulaStatus.Visibility = Visibility.Collapsed;
                return;
            }
            var inner = expr;
            if (inner.StartsWith("$") && inner.EndsWith("$") && inner.Length >= 2)
                inner = inner.Substring(1, inner.Length - 2);
            try
            {
                var toks = Lexer.Tokenize(inner);
                Parser.Parse(toks);
                st.FormulaTb.BorderBrush = Theme.BorderSoft;
                st.FormulaTb.BorderThickness = new Thickness(1);
                st.FormulaStatus.Foreground = Theme.OkGreen;
                st.FormulaStatus.Text = Loc.T("prop.formula.valid");
                st.FormulaStatus.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                st.FormulaTb.BorderBrush = Theme.ErrRed;
                st.FormulaTb.BorderThickness = new Thickness(1.5);
                st.FormulaStatus.Foreground = Theme.ErrRed;
                st.FormulaStatus.Text = Loc.T("prop.formula.error", ex.Message.Split('\n')[0]);
                st.FormulaStatus.Visibility = Visibility.Visible;
            }
        }

        // ---------- 变量 模式宿主 ----------
        private UIElement BuildGvHost(FieldState st)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var cb = MakeStyledComboBox();
            cb.MinWidth = 160;
            cb.ToolTip = Loc.T("prop.gv.tooltip");
            // 占位首项：避免「无默认项 → 下拉栏显扁」
            cb.Items.Add(Loc.T("prop.gv.none"));
            foreach (var k in _gv.All.Keys) cb.Items.Add(k);
            // 初始填充：选中当前变量（原始序列化形如 gv:name），否则选占位项
            var rawGv = _atom.GetProps().TryGetValue(st.Field.Key, out var pv2) ? PropertyValue.Serialize(pv2) : "";
            cb.SelectedItem = rawGv.StartsWith("gv:", StringComparison.OrdinalIgnoreCase)
                ? (object)rawGv.Substring(3).Trim()
                : "（未选择）";
            st.GvCb = cb;
            cb.SelectionChanged += (s, e) => Preview(st.Field);
            var mgr = MakeStyledButton(Loc.T("prop.gv.manage"), margin: new Thickness(8, 0, 0, 0));
            mgr.FontSize = 11;
            mgr.Click += (s, e) => _onOpenGvManager?.Invoke();
            sp.Children.Add(cb);
            sp.Children.Add(mgr);
            return sp;
        }

        /// <summary>字段变更：全量写回原子，按变更类型决定预览方式。</summary>
        private void Preview(EditField changedField)
        {
            var dict = new Dictionary<string, PropertyValue>();
            foreach (var kv in _states)
            {
                var val = kv.Value.Reader();
                dict[kv.Key] = PropertyValue.Parse(val);
            }
            try { _atom.SetProps(dict); }
            catch { return; }
            if (changedField.Kind == EditKind.Choice) _onStructuralChange?.Invoke();
            else _onPreview?.Invoke();
            // 若本次变更的是某字段的显隐依赖键(kind 等)，刷新条件可见性
            if (_fields.Any(f => f.ShowIfKey == changedField.Key))
                RefreshConditionVisibility();
        }

        /// <summary>依据各字段的 ShowIf 元数据，按依赖字段(kind 等)当前值决定字段行显隐：仅当依赖值 ∈ ShowIfValues 才显示。</summary>
        private void RefreshConditionVisibility()
        {
            if (_fieldRows.Count == 0) return;
            var props = _atom.GetProps();
            foreach (var f in _fields)
            {
                if (string.IsNullOrEmpty(f.ShowIfKey) || f.ShowIfValues == null || f.ShowIfValues.Length == 0)
                    continue;
                if (!_fieldRows.TryGetValue(f.Key, out var row)) continue;
                bool show = props.TryGetValue(f.ShowIfKey, out var dep)
                            && f.ShowIfValues.Contains(PropertyValue.Serialize(dep));
                row.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dict = new Dictionary<string, PropertyValue>();
                foreach (var kv in _states)
                    dict[kv.Key] = PropertyValue.Parse(kv.Value.Reader());
                _atom.SetProps(dict);
                _onCommit?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, Loc.T("prop.msg.applyFail", ex.Message), Loc.T("prop.msg.caption"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _onCancel?.Invoke();
    }
}
