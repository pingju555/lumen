using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Core;
using Lumen.Actions;
using Lumen.Atoms;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;

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
        private StackPanel _triggerPanel;

        /// <summary>隐藏底部确定/取消按钮（供嵌入 PropWindow 时使用，由外部提供应用按钮）。</summary>
        public void HideButtons() => ButtonRow.Visibility = Visibility.Collapsed;

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
            TitleTb.Text = Loc.T("prop.title.editType", atom.Type);
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
        private static readonly HashSet<string> KnownTabKeys = new HashSet<string> { "content", "style", "layout", "animation", "interaction", "trigger" };

        private void BuildTabs()
        {
            Tabs.Items.Clear();

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
                else if (spec.Key == "trigger") body = BuildTriggerBlock();
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
                var tab = new TabItem { Header = Loc.T(spec.LocKey) };
                tab.Content = sv;
                Tabs.Items.Add(tab);
            }
            if (Tabs.Items.Count > 0) Tabs.SelectedIndex = 0;
        }

        /// <summary>把单个字段渲染为一行（标签 + [值|公式|变量] 三态输入），供标签页按需归位。</summary>
        private Grid BuildFieldRow(EditField f, string raw, FieldState st)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = f.Label,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 8, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            st.ValueHost = BuildValueEditor(f, raw, st);
            st.FormulaHost = BuildFormulaHost(st);
            st.GvHost = BuildGvHost(st);
            SyncHostVisibility(st);

            var input = new StackPanel { Orientation = Orientation.Vertical };
            input.Children.Add(MakeModeToggle(st));
            input.Children.Add(st.ValueHost);
            input.Children.Add(st.FormulaHost);
            input.Children.Add(st.GvHost);
            if (!string.IsNullOrEmpty(f.Hint))
                input.Children.Add(new TextBlock
                {
                    Text = f.Hint,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            Grid.SetColumn(input, 1);
            row.Children.Add(input);
            return row;
        }

        // ---------- 动作类型选项（P5 行为系统，供点击动作 / 流程动作复用） ----------
        private static (ActionKind kind, string key)[] ActionKindOptions() => new[]
        {
            (ActionKind.None, "prop.action.none"),
            (ActionKind.RunApp, "prop.action.runApp"),
            (ActionKind.MediaControl, "prop.action.mediaControl"),
            (ActionKind.SwitchPage, "prop.action.switchPage"),
            (ActionKind.ToggleEditMode, "prop.action.toggleEditMode"),
            (ActionKind.OpenSettings, "prop.action.openSettings"),
            (ActionKind.LockScreen, "prop.action.lockScreen"),
            (ActionKind.OpenURL, "prop.action.openUrl"),
            (ActionKind.Command, "prop.action.command"),
            (ActionKind.SwitchPreset, "prop.action.switchPreset"),
        };

        // ---------- 点击动作交互区块（P5 行为系统） ----------
        private StackPanel BuildInteractionBlock()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            var options = ActionKindOptions();

            panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
            panel.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.interaction.title"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new SolidColorBrush(Colors.White)
            });

            var cur = _atom.ClickAction ?? AtomAction.None();

            var kindCb = new ComboBox { MinHeight = 24, IsEditable = false, Margin = new Thickness(0, 0, 0, 4), Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
            foreach (var (kind, key) in options) kindCb.Items.Add(Loc.T(key));
            int sel = 0;
            for (int i = 0; i < options.Length; i++) if (options[i].kind == cur.Kind) { sel = i; break; }
            kindCb.SelectedIndex = sel;

            var argTb = new TextBox
            {
                MinWidth = 220,
                Margin = new Thickness(0, 0, 0, 2),
                Text = cur.Kind == ActionKind.None ? "" : (cur.Arg ?? "")
            };
            var browseBtn = new Button
            {
                Content = Loc.T("prop.browse"),
                Margin = new Thickness(8, 0, 0, 2),
                Padding = new Thickness(8, 0, 8, 0),
                Visibility = Visibility.Collapsed
            };
            browseBtn.Click += (s, e) =>
            {
                var picked = FilePickerWindow.PickFile(Window.GetWindow(this), Loc.T("prop.dlg.exeFilter"), argTb.Text);
                if (!string.IsNullOrEmpty(picked)) argTb.Text = picked;
            };
            var argRow = new StackPanel { Orientation = Orientation.Horizontal };
            argRow.Children.Add(argTb);
            argRow.Children.Add(browseBtn);
            var hint = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };

            void UpdateHint()
            {
                var k = options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;
                hint.Text = k switch
                {
                    ActionKind.RunApp => Loc.T("prop.hint.runApp", Loc.T("prop.browse")),
                    ActionKind.MediaControl => Loc.T("prop.hint.mediaControl"),
                    ActionKind.SwitchPage => Loc.T("prop.hint.switchPage"),
                    ActionKind.OpenURL => Loc.T("prop.hint.openUrl"),
                    ActionKind.Command => Loc.T("prop.hint.command", Loc.T("prop.browse")),
                    ActionKind.SwitchPreset => Loc.T("prop.hint.switchPreset"),
                    _ => Loc.T("prop.hint.default")
                };
                argTb.IsEnabled = k != ActionKind.None
                    && k != ActionKind.ToggleEditMode
                    && k != ActionKind.OpenSettings
                    && k != ActionKind.LockScreen;
                browseBtn.Visibility = (k == ActionKind.RunApp || k == ActionKind.Command) ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateHint();

            kindCb.SelectionChanged += (s, e) =>
            {
                var k = options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;
                _atom.ClickAction = (k == ActionKind.None) ? AtomAction.None() : new AtomAction { Kind = k, Arg = argTb.Text };
                UpdateHint();
                _onPreview?.Invoke();
            };
            argTb.TextChanged += (s, e) =>
            {
                if (_atom.ClickAction != null && _atom.ClickAction.Kind != ActionKind.None)
                    _atom.ClickAction = new AtomAction { Kind = _atom.ClickAction.Kind, Arg = argTb.Text };
                _onPreview?.Invoke();
            };

            panel.Children.Add(new TextBlock { Text = Loc.T("prop.action.type"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), Margin = new Thickness(0, 4, 0, 2) });
            panel.Children.Add(kindCb);
            panel.Children.Add(new TextBlock { Text = Loc.T("prop.arg"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), Margin = new Thickness(0, 6, 0, 2) });
            panel.Children.Add(argRow);
            panel.Children.Add(hint);

            return panel;
        }

        // ---------- 触发器区块（P5 触发器系统：条件 → 自动执行动作） ----------
        private StackPanel BuildTriggerBlock()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.trigger.title"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = new SolidColorBrush(Colors.White)
            });
            panel.Children.Add(new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                Text = Loc.T("prop.trigger.desc")
            });

            _triggerPanel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(_triggerPanel);
            RebuildTriggerCards();
            return panel;
        }

        private void RebuildTriggerCards()
        {
            if (_triggerPanel == null) return;
            _triggerPanel.Children.Clear();
            foreach (var trig in _atom.Triggers)
                _triggerPanel.Children.Add(BuildTriggerCard(trig));
            var addBtn = new Button
            {
                Content = Loc.T("prop.trigger.add"),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 3, 8, 3)
            };
            addBtn.Click += (s, e) => { _atom.Triggers.Add(new AtomTrigger()); RebuildTriggerCards(); };
            _triggerPanel.Children.Add(addBtn);
        }

        private UIElement BuildTriggerCard(AtomTrigger trig)
        {
            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1D))
            };
            var sp = new StackPanel { Orientation = Orientation.Vertical };

            // 标题行：说明 + 删除
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = Loc.T("prop.trigger.when"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), VerticalAlignment = VerticalAlignment.Center });
            var del = new Button { Content = Loc.T("prop.delete"), FontSize = 10, Padding = new Thickness(6, 2, 6, 2) };
            del.Click += (s, e) => { _atom.Triggers.Remove(trig); RebuildTriggerCards(); };
            Grid.SetColumn(del, 1);
            head.Children.Add(del);
            sp.Children.Add(head);

            // 条件输入框（布尔公式）
            var condTb = new TextBox
            {
                MinWidth = 220,
                MinHeight = 44,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 140,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = new SolidColorBrush(Colors.White),
                CaretBrush = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Top,
                Text = trig.Condition ?? "",
                ToolTip = Loc.T("prop.trigger.condTooltip")
            };
            var condStatus = new TextBlock { FontSize = 10, Margin = new Thickness(0, 3, 0, 0), Visibility = Visibility.Collapsed };
            condTb.TextChanged += (s, e) =>
            {
                trig.Condition = condTb.Text;
                UpdateTriggerConditionStatus(condTb, condStatus);
                _onPreview?.Invoke();
            };
            sp.Children.Add(condTb);
            sp.Children.Add(condStatus);

            // 预设条件（点选一键填入上方布尔公式框，使用真实可用的函数名）
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.trigger.presetCond"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                Margin = new Thickness(0, 8, 0, 2)
            });
            var presetCb = new ComboBox { MinHeight = 24, IsEditable = false, Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
            var presets = new (string key, string formula)[]
            {
                ("prop.preset.custom", ""),
                ("prop.preset.mediaPlaying", "mi(playing) = \"Playing\""),
                ("prop.preset.cpuHigh", "si(cpu) > 80"),
                ("prop.preset.memHigh", "si(mem) > 80"),
                ("prop.preset.darkMode", "si(dark) = 1"),
                ("prop.preset.batteryLow", "bi(level) < 20"),
                ("prop.preset.charging", "bi(plugged) = 1"),
                ("prop.preset.appForeground", "mi(avail) > 0"),
                ("prop.preset.gvTrue", "gv(flag) = 1"),
            };
            foreach (var (key, _) in presets) presetCb.Items.Add(Loc.T(key));
            presetCb.SelectedIndex = 0;
            presetCb.SelectionChanged += (s, e) =>
            {
                if (presetCb.SelectedIndex <= 0) return;        // 自定义：保持手填
                var f = presets[presetCb.SelectedIndex].formula;
                if (!string.IsNullOrEmpty(f)) condTb.Text = f;  // 触发 condTb.TextChanged 自动写回
            };
            sp.Children.Add(presetCb);

            // 触发模式
            var modeCb = new ComboBox { MinHeight = 24, IsEditable = false, Margin = new Thickness(0, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
            modeCb.Items.Add(Loc.T("prop.trigger.modeOnce"));
            modeCb.Items.Add(Loc.T("prop.trigger.modeWhile"));
            modeCb.SelectedIndex = trig.Mode == TriggerFireMode.While ? 1 : 0;
            modeCb.SelectionChanged += (s, e) =>
            {
                trig.Mode = modeCb.SelectedIndex == 1 ? TriggerFireMode.While : TriggerFireMode.Once;
                _onPreview?.Invoke();
            };
            sp.Children.Add(new TextBlock { Text = Loc.T("prop.trigger.mode"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), Margin = new Thickness(0, 6, 0, 2) });
            sp.Children.Add(modeCb);

            // 流程（一组按顺序执行的动作）
            sp.Children.Add(new TextBlock { Text = Loc.T("prop.trigger.flow"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), Margin = new Thickness(0, 6, 0, 2) });
            sp.Children.Add(BuildFlowEditor(trig.Actions));

            card.Child = sp;
            UpdateTriggerConditionStatus(condTb, condStatus);
            return card;
        }

        /// <summary>动作编辑器（复用点击动作的按钮类型集合）；直接写回 target 对象（与 _atom.Triggers 同源引用）。</summary>
        private UIElement BuildActionEditor(AtomAction target)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            var options = ActionKindOptions();
            var kindCb = new ComboBox { MinHeight = 24, IsEditable = false };
            foreach (var (kind, key) in options) kindCb.Items.Add(Loc.T(key));
            int sel = 0;
            for (int i = 0; i < options.Length; i++) if (options[i].kind == target.Kind) { sel = i; break; }
            kindCb.SelectedIndex = sel;
            var argTb = new TextBox { MinWidth = 220, Margin = new Thickness(0, 2, 0, 0), Text = target.Kind == ActionKind.None ? "" : (target.Arg ?? "") };
            var browseBtn = new Button
            {
                Content = Loc.T("prop.browse"),
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 0),
                Visibility = Visibility.Collapsed
            };
            browseBtn.Click += (s, e) =>
            {
                var picked = FilePickerWindow.PickFile(Window.GetWindow(this), Loc.T("prop.dlg.exeFilter"), argTb.Text);
                if (!string.IsNullOrEmpty(picked)) argTb.Text = picked;
            };
            var argRow = new StackPanel { Orientation = Orientation.Horizontal };
            argRow.Children.Add(argTb);
            argRow.Children.Add(browseBtn);
            var hint = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            void UpdateHint()
            {
                var k = options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;
                hint.Text = k switch
                {
                    ActionKind.RunApp => Loc.T("prop.hint.runApp", Loc.T("prop.browse")),
                    ActionKind.MediaControl => Loc.T("prop.hint.mediaControl"),
                    ActionKind.SwitchPage => Loc.T("prop.hint.switchPage"),
                    ActionKind.OpenURL => Loc.T("prop.hint.openUrl"),
                    ActionKind.Command => Loc.T("prop.hint.command", Loc.T("prop.browse")),
                    ActionKind.SwitchPreset => Loc.T("prop.hint.switchPreset"),
                    _ => Loc.T("prop.hint.default")
                };
                argTb.IsEnabled = k != ActionKind.None
                    && k != ActionKind.ToggleEditMode
                    && k != ActionKind.OpenSettings
                    && k != ActionKind.LockScreen;
                browseBtn.Visibility = (k == ActionKind.RunApp || k == ActionKind.Command) ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateHint();
            kindCb.SelectionChanged += (s, e) =>
            {
                var k = options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;
                target.Kind = k;
                if (target.Kind == ActionKind.None) target.Arg = "";
                UpdateHint();
                _onPreview?.Invoke();
            };
            argTb.TextChanged += (s, e) => { target.Arg = argTb.Text; _onPreview?.Invoke(); };
            sp.Children.Add(kindCb);
            sp.Children.Add(argRow);
            sp.Children.Add(hint);
            return sp;
        }

        /// <summary>流程编辑器：编辑一个有序动作列表（trig.Actions）。每个步骤复用 BuildActionEditor，支持上移/下移/删除，可追加新步骤。
        /// steps 与 trig.Actions 同源引用，改动即时写回并触发 _onPreview。</summary>
        private UIElement BuildFlowEditor(List<AtomAction> steps)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            var stepsPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 4) };
            sp.Children.Add(stepsPanel);

            void RebuildSteps()
            {
                stepsPanel.Children.Clear();
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6),
                        Margin = new Thickness(0, 0, 0, 6),
                        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16))
                    };
                    var vs = new StackPanel { Orientation = Orientation.Vertical };
                    vs.Children.Add(new TextBlock
                    {
                        Text = Loc.T("prop.step", i + 1),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                        Margin = new Thickness(0, 0, 0, 2)
                    });
                    vs.Children.Add(BuildActionEditor(step));
                    var ctl = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                    var up = new Button { Content = Loc.T("prop.moveUp"), FontSize = 10, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
                    var down = new Button { Content = Loc.T("prop.moveDown"), FontSize = 10, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
                    var del = new Button { Content = Loc.T("prop.delete"), FontSize = 10, Padding = new Thickness(6, 2, 6, 2) };
                    up.Click += (s, e) => { if (i > 0) { var tmp = steps[i - 1]; steps[i - 1] = steps[i]; steps[i] = tmp; RebuildSteps(); _onPreview?.Invoke(); } };
                    down.Click += (s, e) => { if (i < steps.Count - 1) { var tmp = steps[i + 1]; steps[i + 1] = steps[i]; steps[i] = tmp; RebuildSteps(); _onPreview?.Invoke(); } };
                    del.Click += (s, e) => { steps.Remove(step); RebuildSteps(); _onPreview?.Invoke(); };
                    ctl.Children.Add(up); ctl.Children.Add(down); ctl.Children.Add(del);
                    vs.Children.Add(ctl);
                    border.Child = vs;
                    stepsPanel.Children.Add(border);
                }
                var add = new Button
                {
                    Content = Loc.T("prop.action.add"),
                    Margin = new Thickness(0, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 3, 8, 3)
                };
                add.Click += (s, e) => { steps.Add(new AtomAction()); RebuildSteps(); _onPreview?.Invoke(); };
                stepsPanel.Children.Add(add);
            }

            RebuildSteps();
            return sp;
        }

        /// <summary>触发器条件语法实时校验 + 当前真值预览（语法有效时尝试即时求值，显示「当前成立 = true/false」）。</summary>
        private void UpdateTriggerConditionStatus(TextBox tb, TextBlock status)
        {
            var expr = (tb.Text ?? "").Trim();
            if (expr.Length == 0)
            {
                tb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                tb.BorderThickness = new Thickness(1);
                status.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                Parser.Parse(Lexer.Tokenize(expr));
                tb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                tb.BorderThickness = new Thickness(1);
                if (_ctx != null)
                {
                    try
                    {
                        var v = _ctx.Eval(expr);
                        status.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0xD1, 0x7A));
                        status.Text = Loc.T("prop.trigger.currentTrue", v.AsBool().ToString().ToLower());
                        status.Visibility = Visibility.Visible;
                    }
                    catch { status.Visibility = Visibility.Collapsed; }
                }
                else status.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                tb.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));
                tb.BorderThickness = new Thickness(1.5);
                status.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));
                status.Text = Loc.T("prop.formula.error", ex.Message.Split('\n')[0]);
                status.Visibility = Visibility.Visible;
            }
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
                var b = new Button
                {
                    Content = Loc.T(txt),
                    Width = 48,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 4, 0),
                    Padding = new Thickness(0, 2, 0, 2),
                    Tag = m
                };
                b.Click += (s, e) =>
                {
                    st.Mode = m;
                    if (m == PropMode.Global && st.GvCb != null && st.GvCb.Items.Count > 0 && st.GvCb.SelectedItem == null)
                        st.GvCb.SelectedIndex = 0;
                    if (m == PropMode.Formula && st.FormulaTb != null) st.FormulaTb.Focus();
                    SyncHostVisibility(st);
                    HighlightToggle(buttons, m);
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
                b.Background = new SolidColorBrush(on ? Color.FromRgb(0x00, 0x7A, 0xCC) : Color.FromRgb(0x3A, 0x3D, 0x41));
                b.Foreground = new SolidColorBrush(on ? Colors.White : Color.FromRgb(0xF0, 0xF0, 0xF0));
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
                        var cb = new ComboBox { MinHeight = 24, IsEditable = false, Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
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
                        var btn = new Button { Content = Loc.T("prop.browse"), Margin = new Thickness(8, 0, 0, 0) };
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
                        var tb = new TextBox { Text = raw, MinWidth = 120 };
                        tb.TextChanged += (s, e) => Preview(f);
                        st.ReadValue = () => tb.Text;
                        return tb;
                    }
                case EditKind.Bool:
                    {
                        var cb = new CheckBox
                        {
                            IsChecked = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase),
                            Foreground = new SolidColorBrush(Colors.White),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        st.ReadValue = () => cb.IsChecked == true ? "1" : "0";
                        cb.Checked += (s, e) => Preview(f);
                        cb.Unchecked += (s, e) => Preview(f);
                        return cb;
                    }
                case EditKind.Slider:
                    {
                        double.TryParse(raw, out var cur);
                        if (double.IsNaN(cur)) cur = (f.Min + f.Max) / 2;
                        var sl = new Slider
                        {
                            Minimum = f.Min,
                            Maximum = f.Max,
                            Width = 150,
                            Value = cur,
                            VerticalAlignment = VerticalAlignment.Center,
                            TickFrequency = (f.Max - f.Min) / 20
                        };
                        var num = new TextBox
                        {
                            Width = 56,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 0, 0),
                            Text = cur.ToString("0.##")
                        };
                        sl.ValueChanged += (s, e) => { num.Text = sl.Value.ToString("0.##"); Preview(f); };
                        num.TextChanged += (s, e) =>
                        {
                            if (double.TryParse(num.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                            {
                                if (v < f.Min) v = f.Min;
                                if (v > f.Max) v = f.Max;
                                sl.Value = v;
                                Preview(f);
                            }
                        };
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
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(a, r, g, b)),
                Cursor = Cursors.Hand
            };
            var hex = new TextBox { Text = ColorPickerWindow.ToHex(a, r, g, b), MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
            var pickBtn = new Button { Content = Loc.T("prop.palette"), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0) };

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
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = new SolidColorBrush(Colors.White),
                CaretBrush = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
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
            var refBtn = new Button
            {
                Content = Loc.T("prop.formula.functions"),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
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
            var win = new Window
            {
                Title = Loc.T("prop.func.title"), Width = 520, Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 标题
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 主体
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 底部

            // 标题栏
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                Padding = new Thickness(10, 6, 10, 6)
            };
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) win.DragMove(); };
            titleBar.Child = new TextBlock { Text = Loc.T("prop.func.title"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

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
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // 底部关闭
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0, 8, 8) };
            var closeBtn = new Button { Content = Loc.T("settings.close"), Width = 80, Padding = new Thickness(0, 3, 0, 3) };
            closeBtn.Click += (s, e) => win.Close();
            footer.Children.Add(closeBtn);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            // 一级菜单：分类
            var categories = FunctionCatalog.All.Select(f => f.Category).Distinct().ToList();
            void ShowFunctions(string cat)
            {
                fnPanel.Children.Clear();
                foreach (var fn in FunctionCatalog.All.Where(f => f.Category == cat))
                {
                    var row = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 0, 4),
                        Padding = new Thickness(8, 6, 8, 6),
                        Cursor = Cursors.Hand
                    };
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = fn.Sig, Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)), FontSize = 12, FontWeight = FontWeights.Bold });
                    sp.Children.Add(new TextBlock { Text = fn.Desc, Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                    row.Child = sp;
                    row.MouseLeftButtonUp += (s, e) =>
                    {
                        var target = st.FormulaTb;
                        int caret = target.CaretIndex;
                        string cur = target.Text ?? "";
                        target.Text = cur.Insert(caret < 0 ? cur.Length : caret, fn.Insert);
                        target.Focus();
                        target.CaretIndex = caret < 0 ? target.Text.Length : caret + fn.Insert.Length;
                        Preview(st.Field);
                        UpdateFormulaStatus(st);
                        UpdateFormulaPreview(st);
                        win.Close();
                    };
                    fnPanel.Children.Add(row);
                }
            }
            foreach (var cat in categories)
            {
                var b = new Button
                {
                    Content = cat,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                b.Click += (s, e) => ShowFunctions(cat);
                catList.Children.Add(b);
            }
            ShowFunctions(categories.First());

            win.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
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
                st.FormulaPreview.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
                st.FormulaPreview.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                string toEval = inner.Contains("$") ? inner : "$" + inner + "$";
                var result = _ctx.EvalText(toEval);
                st.FormulaPreview.Text = Loc.T("prop.formula.result", result);
                st.FormulaPreview.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0xD1, 0x7A));
                st.FormulaPreview.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                st.FormulaPreview.Text = Loc.T("prop.formula.evalFail", ex.Message.Split('\n')[0]);
                st.FormulaPreview.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));
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
                st.FormulaTb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
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
                st.FormulaTb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                st.FormulaTb.BorderThickness = new Thickness(1);
                st.FormulaStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0xD1, 0x7A));
                st.FormulaStatus.Text = Loc.T("prop.formula.valid");
                st.FormulaStatus.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                st.FormulaTb.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));
                st.FormulaTb.BorderThickness = new Thickness(1.5);
                st.FormulaStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));
                st.FormulaStatus.Text = Loc.T("prop.formula.error", ex.Message.Split('\n')[0]);
                st.FormulaStatus.Visibility = Visibility.Visible;
            }
        }

        // ---------- 变量 模式宿主 ----------
        private UIElement BuildGvHost(FieldState st)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var cb = new ComboBox { MinWidth = 160, MinHeight = 24, IsEditable = false, ToolTip = Loc.T("prop.gv.tooltip") };
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
            var mgr = new Button { Content = Loc.T("prop.gv.manage"), Margin = new Thickness(8, 0, 0, 0), FontSize = 11 };
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
