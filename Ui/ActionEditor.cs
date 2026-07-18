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
using Lumen.Actions;
using Lumen.Atoms;
using Lumen.Core;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;
using Lumen.Presets;

namespace Lumen.Ui
{
    /// <summary>
    /// 动作 &amp; 流程编辑器（从 PropertyEditorPanel 解耦出来的独立模块）。
    /// 承载：动作类型选项、下拉/组合参数控件、点击动作的 BuildActionEditor，
    /// 以及流程子系统（条件→自动执行动作序列）的全部编辑 UI。
    /// 调用方（PropertyEditorPanel 的 flow / interaction 标签页）只负责实例化并委托，
    /// 自身不再包含任何流程业务逻辑。
    /// </summary>
    public class ActionEditor
    {
        private readonly Atom _atom;
        private readonly Action _onPreview;             // 字段变更即时写回原子视觉
        private readonly EvalContext _ctx;              // 流程条件实时求值上下文
        private StackPanel _flowPanel;

        public ActionEditor(Atom atom, Action onPreview, EvalContext ctx)
        {
            _atom = atom;
            _onPreview = onPreview;
            _ctx = ctx;
        }

        // ---------- 动作类型选项（P5 行为系统，供点击动作 / 流程动作复用） ----------
        public static (ActionKind kind, string key)[] ActionKindOptions() => new[]
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
            (ActionKind.RunFlow, "prop.action.runFlow"),
        };

        /// <summary>构建一个值=raw、显示=label 的下拉（用于动作参数的枚举类取值）。选中即回调 onPick(raw)。</summary>
        public static UIElement BuildDropdown(string current, Action<string> onPick, params (string raw, string label)[] items)
        {
            var cb = new ComboBox { MinHeight = 24, IsEditable = false, MinWidth = 180, Margin = new Thickness(0, 0, 0, 2), Foreground = Theme.TextSecondary };
            ComboBoxItem match = null;
            foreach (var (raw, label) in items)
            {
                var it = new ComboBoxItem { Tag = raw, Content = label };
                cb.Items.Add(it);
                if (raw == current) match = it;
            }
            cb.SelectedItem = match ?? (cb.Items.Count > 0 ? cb.Items[0] : null);
            cb.SelectionChanged += (s, e) =>
            {
                if (cb.SelectedItem is ComboBoxItem it) onPick(it.Tag as string ?? "");
            };
            return cb;
        }

        /// <summary>为新动作类型构建组合参数控件（RunFlow=流程下拉，SetVar=名称|值，Delay=毫秒，ReadFile=路径|变量|JSON字段）。
        /// 返回 null 表示该类型用普通文本框即可（由调用方处理）；setArg 即时写回参数（含分隔符拼接）。</summary>
        public UIElement BuildCompositeArg(ActionKind k, string curArg, Action<string> setArg)
        {
            var lbl = new Func<string, UIElement>(txt =>
            {
                return new TextBlock
                {
                    Text = txt,
                    FontSize = 11,
                    Foreground = Theme.TextSecondary,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
            });
            var sep = new TextBlock
            {
                Text = "|",
                FontSize = 12,
                Foreground = Theme.TextTertiary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };

            if (k == ActionKind.RunFlow)
            {
                var items = new List<(string raw, string label)>();
                for (int i = 0; i < _atom.Flows.Count; i++)
                    items.Add((i.ToString(), Loc.T("prop.arg.flowN", (i + 1).ToString())));
                if (items.Count == 0) items.Add(("-1", Loc.T("prop.arg.noFlow")));
                return BuildDropdown(curArg, setArg, items.ToArray());
            }
            if (k == ActionKind.Delay)
            {
                var tb = new TextBox { MinWidth = 120, Text = curArg, ToolTip = Loc.T("prop.hint.delay") };
                tb.TextChanged += (s, e) => setArg(tb.Text);
                return tb;
            }
            if (k == ActionKind.SetVar)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var parts = curArg.Split(new[] { '|' }, 2);
                var nameTb = new TextBox { MinWidth = 110, Text = parts.Length > 0 ? parts[0] : "", ToolTip = Loc.T("prop.arg.varName") };
                var valTb = new TextBox { MinWidth = 160, Text = parts.Length > 1 ? parts[1] : "", ToolTip = Loc.T("prop.arg.varValue") };
                nameTb.TextChanged += (s, e) => setArg(nameTb.Text + "|" + valTb.Text);
                valTb.TextChanged += (s, e) => setArg(nameTb.Text + "|" + valTb.Text);
                sp.Children.Add(lbl(Loc.T("prop.arg.varName")));
                sp.Children.Add(nameTb);
                sp.Children.Add(sep);
                sp.Children.Add(lbl(Loc.T("prop.arg.value")));
                sp.Children.Add(valTb);
                return sp;
            }
            if (k == ActionKind.ReadFile)
            {
                var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 0) };
                var parts = curArg.Split('|');
                var pathTb = new TextBox { MinWidth = 200, Text = parts.Length > 0 ? parts[0] : "" };
                var varTb = new TextBox { MinWidth = 140, Text = parts.Length > 1 ? parts[1] : "", ToolTip = Loc.T("prop.arg.readVar") };
                var jsonTb = new TextBox { MinWidth = 140, Text = parts.Length > 2 ? parts[2] : "", ToolTip = Loc.T("prop.arg.readJson") };
                void Commit() => setArg(pathTb.Text + "|" + varTb.Text + (jsonTb.Text.Trim().Length > 0 ? "|" + jsonTb.Text.Trim() : ""));
                pathTb.TextChanged += (s, e) => Commit();
                varTb.TextChanged += (s, e) => Commit();
                jsonTb.TextChanged += (s, e) => Commit();

                var pathRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                pathRow.Children.Add(pathTb);
                var browseBtn = new Button { Content = Loc.T("prop.browse"), Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
                browseBtn.Click += (s, e) =>
                {
                    var picked = FilePickerWindow.PickFile(LumenWindow.Main, Loc.T("dlg.textJson.filter"), pathTb.Text);
                    if (!string.IsNullOrEmpty(picked)) pathTb.Text = picked;
                };
                pathRow.Children.Add(browseBtn);

                var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
                labelRow.Children.Add(lbl(Loc.T("prop.arg.readVar")));
                labelRow.Children.Add(varTb);
                labelRow.Children.Add(lbl(Loc.T("prop.arg.readJson")));
                labelRow.Children.Add(jsonTb);

                sp.Children.Add(pathRow);
                sp.Children.Add(labelRow);
                return sp;
            }
            return null;
        }

        /// <summary>动作编辑器（复用点击动作的按钮类型集合）；直接写回 target 对象（与 atom.Flows 同源引用）。</summary>
        public UIElement BuildActionEditor(AtomAction target)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            var options = ActionKindOptions();
            var kindCb = new ComboBox { MinHeight = 24, IsEditable = false };
            foreach (var (kind, key) in options) kindCb.Items.Add(Loc.T(key));
            int sel = 0;
            for (int i = 0; i < options.Length; i++) if (options[i].kind == target.Kind) { sel = i; break; }
            kindCb.SelectedIndex = sel;

            var argHost = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 0) };
            var hint = new TextBlock { FontSize = 10, Foreground = Theme.TextTertiary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };

            ActionKind CurKind() => options[kindCb.SelectedIndex >= 0 ? kindCb.SelectedIndex : 0].kind;

            void SetArg(string v) { target.Arg = v; _onPreview?.Invoke(); }

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

            // 依据当前动作类型构建参数控件：枚举类参数用下拉，自由文本类用文本框(+浏览)，新类型用组合控件
            void RebuildArg()
            {
                argHost.Children.Clear();
                var k = CurKind();
                var curArg = (target.Kind == k) ? (target.Arg ?? "") : "";
                if (k == ActionKind.None || k == ActionKind.ToggleEditMode || k == ActionKind.OpenSettings || k == ActionKind.LockScreen)
                    return; // 无参数动作

                if (k == ActionKind.MediaControl)
                {
                    argHost.Children.Add(BuildDropdown(curArg, SetArg,
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
                    argHost.Children.Add(BuildDropdown(curArg, SetArg, items.ToArray()));
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
                    argHost.Children.Add(BuildDropdown(curArg, SetArg, items.ToArray()));
                    return;
                }
                // 新动作类型（RunFlow / SetVar / Delay / ReadFile）：组合参数控件
                var comp = BuildCompositeArg(k, curArg, SetArg);
                if (comp != null) { argHost.Children.Add(comp); return; }
                // RunApp / OpenURL / Command：文本框（RunApp/Command 附浏览按钮）
                var argTb = new TextBox { MinWidth = 220, Text = curArg };
                argTb.TextChanged += (s, e) => SetArg(argTb.Text);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(argTb);
                if (k == ActionKind.RunApp || k == ActionKind.Command)
                {
                    var browseBtn = new Button { Content = Loc.T("prop.browse"), Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
                    browseBtn.Click += (s, e) =>
                    {
                        var picked = FilePickerWindow.PickFile(LumenWindow.Main, Loc.T("prop.dlg.exeFilter"), argTb.Text);
                        if (!string.IsNullOrEmpty(picked)) argTb.Text = picked;
                    };
                    row.Children.Add(browseBtn);
                }
                argHost.Children.Add(row);
            }

            kindCb.SelectionChanged += (s, e) =>
            {
                var k = CurKind();
                target.Kind = k;
                if (target.Kind == ActionKind.None) target.Arg = "";
                RebuildArg();
                UpdateHint();
                _onPreview?.Invoke();
            };
            RebuildArg();
            UpdateHint();

            sp.Children.Add(kindCb);
            sp.Children.Add(argHost);
            sp.Children.Add(hint);
            return sp;
        }

        // ---------- 流程区块（P5 流程系统：条件 → 自动执行动作序列） ----------
        public UIElement BuildFlowBlock()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.flow.title"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = Theme.TextPrimary
            });
            panel.Children.Add(new TextBlock
            {
                FontSize = 10,
                Foreground = Theme.TextTertiary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                Text = Loc.T("prop.flow.desc")
            });

            _flowPanel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(_flowPanel);
            RebuildFlowCards();
            return panel;
        }

        public void RebuildFlowCards()
        {
            if (_flowPanel == null) return;
            _flowPanel.Children.Clear();
            foreach (var trig in _atom.Flows)
                _flowPanel.Children.Add(BuildFlowCard(trig));
            var addBtn = new Button
            {
                Content = Loc.T("prop.flow.add"),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 3, 8, 3)
            };
            addBtn.Click += (s, e) => { _atom.Flows.Add(new Flow()); RebuildFlowCards(); };
            _flowPanel.Children.Add(addBtn);
        }

        public UIElement BuildFlowCard(Flow trig)
        {
            var card = new Border
            {
                BorderBrush = Theme.BorderDefault,
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
            head.Children.Add(new TextBlock { Text = Loc.T("prop.flow.when"), FontSize = 11, Foreground = Theme.TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            var del = new Button { Content = Loc.T("prop.delete"), FontSize = 10, Padding = new Thickness(6, 2, 6, 2) };
            del.Click += (s, e) => { _atom.Flows.Remove(trig); RebuildFlowCards(); };
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
                Background = Theme.BgBase,
                Foreground = Theme.TextPrimary,
                CaretBrush = Theme.TextPrimary,
                BorderBrush = Theme.BorderSoft,
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Top,
                Text = trig.Condition ?? "",
                ToolTip = Loc.T("prop.flow.condTooltip")
            };
            var condStatus = new TextBlock { FontSize = 10, Margin = new Thickness(0, 3, 0, 0), Visibility = Visibility.Collapsed };
            condTb.TextChanged += (s, e) =>
            {
                trig.Condition = condTb.Text;
                UpdateFlowConditionStatus(condTb, condStatus);
                _onPreview?.Invoke();
            };
            sp.Children.Add(condTb);
            sp.Children.Add(condStatus);

            // 预设条件（点选一键填入上方布尔公式框，使用真实可用的函数名）
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("prop.flow.presetCond"),
                FontSize = 11,
                Foreground = Theme.TextSecondary,
                Margin = new Thickness(0, 8, 0, 2)
            });
            var presetCb = new ComboBox { MinHeight = 24, IsEditable = false, Foreground = Theme.TextSecondary };
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
            var modeCb = new ComboBox { MinHeight = 24, IsEditable = false, Margin = new Thickness(0, 6, 0, 0), Foreground = Theme.TextSecondary };
            modeCb.Items.Add(Loc.T("prop.flow.modeOnce"));
            modeCb.Items.Add(Loc.T("prop.flow.modeWhile"));
            modeCb.SelectedIndex = trig.Mode == FlowFireMode.While ? 1 : 0;
            modeCb.SelectionChanged += (s, e) =>
            {
                trig.Mode = modeCb.SelectedIndex == 1 ? FlowFireMode.While : FlowFireMode.Once;
                _onPreview?.Invoke();
            };
            sp.Children.Add(new TextBlock { Text = Loc.T("prop.flow.mode"), FontSize = 11, Foreground = Theme.TextSecondary, Margin = new Thickness(0, 6, 0, 2) });
            sp.Children.Add(modeCb);

            // 流程（一组按顺序执行的动作）
            sp.Children.Add(new TextBlock { Text = Loc.T("prop.flow.exec"), FontSize = 11, Foreground = Theme.TextSecondary, Margin = new Thickness(0, 6, 0, 2) });
            sp.Children.Add(BuildFlowEditor(trig.Actions));

            card.Child = sp;
            UpdateFlowConditionStatus(condTb, condStatus);
            return card;
        }

        /// <summary>流程编辑器：编辑一个有序动作列表（trig.Actions）。每个步骤复用 BuildActionEditor，支持上移/下移/删除，可追加新步骤。
        /// steps 与 trig.Actions 同源引用，改动即时写回并触发 _onPreview。</summary>
        public UIElement BuildFlowEditor(List<AtomAction> steps)
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
                        Foreground = Theme.TextTertiary,
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
        public void UpdateFlowConditionStatus(TextBox tb, TextBlock status)
        {
            var expr = (tb.Text ?? "").Trim();
            if (expr.Length == 0)
            {
                tb.BorderBrush = Theme.BorderSoft;
                tb.BorderThickness = new Thickness(1);
                status.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                Parser.Parse(Lexer.Tokenize(expr));
                tb.BorderBrush = Theme.BorderSoft;
                tb.BorderThickness = new Thickness(1);
                if (_ctx != null)
                {
                    try
                    {
                        var v = _ctx.Eval(expr);
                        status.Foreground = Theme.OkGreen;
                        status.Text = Loc.T("prop.flow.currentTrue", v.AsBool().ToString().ToLower());
                        status.Visibility = Visibility.Visible;
                    }
                    catch { status.Visibility = Visibility.Collapsed; }
                }
                else status.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                tb.BorderBrush = Theme.ErrRed;
                tb.BorderThickness = new Thickness(1.5);
                status.Foreground = Theme.ErrRed;
                status.Text = Loc.T("prop.formula.error", ex.Message.Split('\n')[0]);
                status.Visibility = Visibility.Visible;
            }
        }
    }
}
