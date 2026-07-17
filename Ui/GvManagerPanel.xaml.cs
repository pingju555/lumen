using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Globals;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>全局变量 (gv) 管理器：列出/增删改类型化变量（数字/文本/颜色/字体）。</summary>
    public partial class GvManagerPanel : UserControl
    {
        private readonly GvStore _gv;
        private readonly Action _onChanged;   // 任意改动后：标记重算 + 保存
        private readonly Action _onClosed;     // 关闭弹窗

        public GvManagerPanel(GvStore gv, Action onChanged, Action onClosed)
        {
            InitializeComponent();
            _gv = gv;
            _onChanged = onChanged;
            _onClosed = onClosed;
            RebuildList();
        }

        private static readonly GvType[] Types = { GvType.Number, GvType.Text, GvType.Color, GvType.Font, GvType.List };
        private static readonly string[] TypeLabels = { Loc.T("gv.type.number"), Loc.T("gv.type.text"), Loc.T("gv.type.color"), Loc.T("gv.type.font"), Loc.T("gv.type.list") };

        private void RebuildList()
        {
            List.Children.Clear();
            if (_gv.All.Count == 0)
            {
                List.Children.Add(new TextBlock
                {
                    Text = Loc.T("gv.empty"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 11, Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }
            foreach (var kv in _gv.All)
                List.Children.Add(BuildRow(kv.Key, kv.Value));
        }

        private UIElement BuildRow(string name, TypedValue tv)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            var nameTb = new TextBox
            {
                Text = name,
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46))
            };

            var typeCb = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                ItemsSource = TypeLabels,
                SelectedIndex = Array.IndexOf(Types, tv.Type)
            };

            var valHost = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            BuildValueEditor(valHost, name, tv);

            var del = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 22,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            };
            del.Click += (s, e) => { _gv.Remove(name); List.Children.Remove(row); _onChanged?.Invoke(); };

            typeCb.SelectionChanged += (s, e) =>
            {
                int idx = typeCb.SelectedIndex;
                if (idx < 0) return;
                var nt = Types[idx];
                // 切换类型：按当前值尽量保留
                object raw = tv.Raw;
                if (nt == GvType.Number && !(raw is double)) raw = 0d;
                else if (nt == GvType.Color && !(raw is uint)) raw = 0xFF000000u;
                else if ((nt == GvType.Text || nt == GvType.Font) && raw is not string) raw = "";
                else if (nt == GvType.List) raw = "";
                _gv.Set(name, new TypedValue { Type = nt, Raw = raw });
                BuildValueEditor(valHost, name, _gv.Get(name));
                _onChanged?.Invoke();
            };

            Grid.SetColumn(nameTb, 0);
            Grid.SetColumn(typeCb, 1);
            Grid.SetColumn(valHost, 2);
            Grid.SetColumn(del, 3);
            row.Children.Add(nameTb);
            row.Children.Add(typeCb);
            row.Children.Add(valHost);
            row.Children.Add(del);
            return row;
        }

        private void BuildValueEditor(StackPanel host, string name, TypedValue tv)
        {
            host.Children.Clear();
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
                    var h = tb.Text.Trim().TrimStart('#');
                    if (h.Length == 8 && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                    {
                        swatch.Background = new SolidColorBrush(ToColor(u));
                        _gv.Set(name, new TypedValue { Type = GvType.Color, Raw = u });
                        _onChanged?.Invoke();
                    }
                };
                host.Children.Add(swatch);
                host.Children.Add(tb);
            }
            else if (tv.Type == GvType.List)
            {
                ComboBox selCb = null;
                var optsTb = new TextBox
                {
                    MinWidth = 120,
                    Text = (tv.Raw as string) ?? "",
                    ToolTip = Loc.T("gv.listHint")
                };
                optsTb.TextChanged += (s, e) =>
                {
                    _gv.Set(name, new TypedValue { Type = GvType.List, Raw = optsTb.Text, SelectedIndex = 0 });
                    _onChanged?.Invoke();
                    if (selCb != null) FillSel(selCb, optsTb.Text, 0);
                };
                selCb = new ComboBox { Margin = new Thickness(6, 0, 0, 0), MinWidth = 80, ToolTip = Loc.T("gv.selHint") };
                FillSel(selCb, tv.Raw as string, tv.SelectedIndex);
                selCb.SelectionChanged += (s, e) =>
                {
                    if (selCb.SelectedIndex < 0) return;
                    var cur = _gv.Get(name);
                    _gv.Set(name, new TypedValue { Type = GvType.List, Raw = (cur.Raw as string) ?? "", SelectedIndex = selCb.SelectedIndex });
                    _onChanged?.Invoke();
                };
                host.Children.Add(optsTb);
                host.Children.Add(selCb);
            }
            else
            {
                var tb = new TextBox { MinWidth = 150, Text = tv.Raw?.ToString() ?? "" };
                tb.TextChanged += (s, e) =>
                {
                    object raw = tb.Text;
                    if (tv.Type == GvType.Number && double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) raw = d;
                    _gv.Set(name, new TypedValue { Type = tv.Type, Raw = raw });
                    _onChanged?.Invoke();
                };
                host.Children.Add(tb);
            }
        }

        private static void FillSel(ComboBox cb, string opts, int sel)
        {
            cb.Items.Clear();
            var arr = (opts ?? "").Split('|');
            foreach (var o in arr) cb.Items.Add(o);
            cb.SelectedIndex = arr.Length > 0 ? Math.Min(Math.Max(0, sel), arr.Length - 1) : -1;
        }

        private static Color ToColor(uint argb)
            => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        /// <summary>标题栏拖拽窗口（依附于 Window 容器）。</summary>
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var win = Window.GetWindow(this);
                if (win != null) win.DragMove();
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            string baseName = "var";
            int i = 1;
            while (_gv.Get(baseName + i) != null) i++;
            string name = baseName + i;
            _gv.Set(name, new TypedValue { Type = GvType.Text, Raw = "" });
            RebuildList();
            _onChanged?.Invoke();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => _onClosed?.Invoke();
    }
}
