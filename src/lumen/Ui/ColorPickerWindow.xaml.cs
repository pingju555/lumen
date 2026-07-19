using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 高级取色器：SV 色盘 + 色相/Alpha 滑块 + ARGB 彩条+数值 + Hex 预览。
    /// 公开静态入口 PickColor(initialHex, owner) → #AARRGGBB 或 null。
    /// </summary>
    public class ColorPickerWindow : Window
    {
        private TextBox _hexBox, _aBox, _rBox, _gBox, _bBox;
        private Slider _aSlider, _rSlider, _gSlider, _bSlider;
        private Canvas _svCanvas, _alphaCanvas;
        private Ellipse _svCursor;
        private Border _svBase;           // SV 色盘底色（色相纯色）
        private Grid _hueHost, _alphaHost;
        private Rectangle _hueCursor, _alphaCursor;
        private Border _alphaBase;        // Alpha 条当前色
        private Border _previewBorder;
        private bool _loading;
        private byte _a = 255, _r = 0, _g = 0, _b = 0;
        private bool _svDrag, _hueDrag, _alphaDrag;

        public ColorPickerWindow()
        {
            Title = Loc.T("color.title");
            Width = 360; Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            BuildUI();
        }

        /// <summary>打开取色器；返回 #AARRGGBB（确定）或 null（取消）。</summary>
        public static string PickColor(string initialHex, Window owner = null)
        {
            var win = new ColorPickerWindow();
            // WPF 限制：AllowsTransparency=True 的窗口不能作为模态子窗 Owner（抛出 InvalidOperationException）
            if (owner != null && !owner.AllowsTransparency) win.Owner = owner;
            win.InitFromHex(initialHex);
            return win.ShowDialog() == true ? win.GetCurrentHex() : null;
        }

        private void BuildUI()
        {
            var root = new StackPanel { Margin = new Thickness(8) };

            // 标题栏
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(4, 4, 0, 0)
            };
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };
            titleBar.Child = new TextBlock { Text = Loc.T("color.title"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
            root.Children.Add(titleBar);

            // SV 色盘（Grid 叠 3 层渐变 + 顶层 Canvas 鼠标拾取 + 光标）
            var svHost = new Grid { Margin = new Thickness(0, 6, 0, 0), Height = 140, ClipToBounds = true };
            // 层 1：色相底色（实时改）
            _svBase = new Border();
            // 层 2：白→透明（横向，S=0→1）
            var svWhite = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                    GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) }
                }
            };
            // 层 3：透明→黑（纵向，V=1→0）
            var svBlack = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
                    GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 1) }
                }
            };
            svHost.Children.Add(_svBase);
            svHost.Children.Add(svWhite);
            svHost.Children.Add(svBlack);
            // 顶层 Canvas 鼠标拾取 + 光标
            _svCanvas = new Canvas { Background = Brushes.Transparent };
            _svCursor = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
            _svCanvas.Children.Add(_svCursor);
            _svCanvas.MouseLeftButtonDown += SvDown;
            _svCanvas.MouseMove += SvMove;
            _svCanvas.MouseLeftButtonUp += SvUp;
            svHost.Children.Add(_svCanvas);
            root.Children.Add(svHost);

            // 色相
            _hueHost = new Grid { Margin = new Thickness(0, 6, 0, 0), Height = 14 };
            _hueHost.Children.Add(MakeHueRect());
            _hueCursor = new Rectangle { Width = 4, Height = 20, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-2, 0, 0, 0) };
            _hueHost.Children.Add(_hueCursor);
            _hueHost.MouseLeftButtonDown += HueDown;
            _hueHost.MouseMove += HueMove;
            _hueHost.MouseLeftButtonUp += HueUp;
            root.Children.Add(_hueHost);

            // Alpha（Grid 叠：棋盘 + 当前色透明渐变 + Canvas 拾取 + cursor）
            _alphaHost = new Grid { Margin = new Thickness(0, 4, 0, 0), Height = 14, ClipToBounds = true };
            _alphaHost.Children.Add(MakeCheckerRect());      // 底层：棋盘
            _alphaBase = new Border();                        // 上层：当前色透明渐变（实时改）
            _alphaBase.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Color.FromArgb(255, 0, 0, 0), 1) }
            };
            _alphaHost.Children.Add(_alphaBase);
            _alphaCursor = new Rectangle { Width = 4, Height = 24, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-2, 0, 0, 0) };
            var alphaPick = new Canvas { Background = Brushes.Transparent };
            alphaPick.MouseLeftButtonDown += AlphaDown;
            alphaPick.MouseMove += AlphaMove;
            alphaPick.MouseLeftButtonUp += AlphaUp;
            _alphaHost.Children.Add(alphaPick);
            _alphaHost.Children.Add(_alphaCursor);
            _alphaCanvas = alphaPick;
            root.Children.Add(_alphaHost);

            // 预览
            _previewBorder = new Border
            {
                Height = 24, Margin = new Thickness(0, 6, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Colors.White)
            };
            root.Children.Add(_previewBorder);

            // Hex
            var hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            hexRow.Children.Add(new TextBlock { Text = "#", Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            _hexBox = new TextBox { MinWidth = 180 };
            _hexBox.TextChanged += (s, e) => { if (!_loading) ApplyHex(_hexBox.Text); };
            hexRow.Children.Add(_hexBox);
            root.Children.Add(hexRow);

            // ARGB
            AddArgbRow(root, "A", Colors.White, out _aSlider, out _aBox);
            AddArgbRow(root, "R", Color.FromRgb(0xFF, 0x60, 0x60), out _rSlider, out _rBox);
            AddArgbRow(root, "G", Color.FromRgb(0x60, 0xD0, 0x60), out _gSlider, out _gBox);
            AddArgbRow(root, "B", Color.FromRgb(0x60, 0x90, 0xFF), out _bSlider, out _bBox);
            _aSlider.ValueChanged += (s, e) => { if (_loading) return; _a = (byte)_aSlider.Value; _aBox.Text = _a.ToString(); UpdateAll(); };
            _rSlider.ValueChanged += (s, e) => { if (_loading) return; _r = (byte)_rSlider.Value; _rBox.Text = _r.ToString(); UpdateAll(); };
            _gSlider.ValueChanged += (s, e) => { if (_loading) return; _g = (byte)_gSlider.Value; _gBox.Text = _g.ToString(); UpdateAll(); };
            _bSlider.ValueChanged += (s, e) => { if (_loading) return; _b = (byte)_bSlider.Value; _bBox.Text = _b.ToString(); UpdateAll(); };
            _aBox.TextChanged += (s, e) => { if (_loading || !byte.TryParse(_aBox.Text, out var v)) return; _a = v; _aSlider.Value = v; UpdateAll(); };
            _rBox.TextChanged += (s, e) => { if (_loading || !byte.TryParse(_rBox.Text, out var v)) return; _r = v; _rSlider.Value = v; UpdateAll(); };
            _gBox.TextChanged += (s, e) => { if (_loading || !byte.TryParse(_gBox.Text, out var v)) return; _g = v; _gSlider.Value = v; UpdateAll(); };
            _bBox.TextChanged += (s, e) => { if (_loading || !byte.TryParse(_bBox.Text, out var v)) return; _b = v; _bSlider.Value = v; UpdateAll(); };

            // OK / Cancel
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            var ok = new Button
            {
                Content = Loc.T("common.ok"), Width = 70, Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x41)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            ok.Click += (s, e) => { DialogResult = true; Close(); };
            var cancel = new Button
            {
                Content = Loc.T("common.cancel"), Width = 70,
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x41)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            btnRow.Children.Add(ok); btnRow.Children.Add(cancel);
            root.Children.Add(btnRow);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = root
            };
        }

        private void AddArgbRow(StackPanel parent, string label, Color tint, out Slider s, out TextBox t)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var lab = new TextBlock { Text = label, Foreground = new SolidColorBrush(tint), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            s = new Slider { Minimum = 0, Maximum = 255, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            t = new TextBox { Width = 38, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
            Grid.SetColumn(lab, 0); Grid.SetColumn(s, 1); Grid.SetColumn(t, 2);
            row.Children.Add(lab); row.Children.Add(s); row.Children.Add(t);
            parent.Children.Add(row);
        }

        private void InitFromHex(string hex)
        {
            _loading = true;
            ParseHex(hex, out _a, out _r, out _g, out _b);
            UpdateAll();
            _loading = false;
        }

        private void UpdateAll()
        {
            var c = Color.FromArgb(_a, _r, _g, _b);
            if (_previewBorder != null) _previewBorder.Background = new SolidColorBrush(c);
            if (_svBase != null) _svBase.Background = new SolidColorBrush(HsvToRgb(GetHue(c), 1, 1));
            if (_svCanvas != null)
            {
                var (_, s, v) = RgbToHsv(c);
                double w = _svCanvas.ActualWidth, h = _svCanvas.ActualHeight;
                if (w > 0) Canvas.SetLeft(_svCursor, s * (w - 12));
                if (h > 0) Canvas.SetTop(_svCursor, (1 - v) * (h - 12));
            }
            if (_hueHost != null && _hueHost.ActualWidth > 0)
                Canvas.SetLeft(_hueCursor, GetHue(c) / 360.0 * (_hueHost.ActualWidth - 4));
            if (_alphaBase != null)
            {
                _alphaBase.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                    GradientStops = {
                        new GradientStop(Color.FromArgb(0, _r, _g, _b), 0),
                        new GradientStop(Color.FromArgb(255, _r, _g, _b), 1)
                    }
                };
            }
            if (_alphaHost != null && _alphaHost.ActualWidth > 0)
                Canvas.SetLeft(_alphaCursor, _a / 255.0 * (_alphaHost.ActualWidth - 4));

            if (_aSlider != null) { _aSlider.Value = _a; _aBox.Text = _a.ToString(); }
            if (_rSlider != null) { _rSlider.Value = _r; _rBox.Text = _r.ToString(); }
            if (_gSlider != null) { _gSlider.Value = _g; _gBox.Text = _g.ToString(); }
            if (_bSlider != null) { _bSlider.Value = _b; _bBox.Text = _b.ToString(); }
            _hexBox.Text = ToHex(_a, _r, _g, _b);
        }

        // ---- SV 交互 ----
        private void SvDown(object s, MouseButtonEventArgs e) { _svDrag = true; _svCanvas.CaptureMouse(); SetSv(e.GetPosition(_svCanvas)); }
        private void SvMove(object s, MouseEventArgs e) { if (_svDrag) SetSv(e.GetPosition(_svCanvas)); }
        private void SvUp(object s, MouseButtonEventArgs e) { _svDrag = false; _svCanvas.ReleaseMouseCapture(); }
        private void SetSv(Point p)
        {
            double w = _svCanvas.ActualWidth, h = _svCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            double x = Math.Clamp(p.X, 0, w - 1), y = Math.Clamp(p.Y, 0, h - 1);
            var hue = GetHue(Color.FromRgb(_r, _g, _b));
            var rgb = HsvToRgb(hue, (float)(x / (w - 1)), (float)(1 - y / (h - 1)));
            _r = rgb.R; _g = rgb.G; _b = rgb.B;
            _loading = true; UpdateAll(); _loading = false;
        }

        // ---- 色相交互 ----
        private void HueDown(object s, MouseButtonEventArgs e) { _hueDrag = true; _hueHost.CaptureMouse(); SetHue(e.GetPosition(_hueHost)); }
        private void HueMove(object s, MouseEventArgs e) { if (_hueDrag) SetHue(e.GetPosition(_hueHost)); }
        private void HueUp(object s, MouseButtonEventArgs e) { _hueDrag = false; _hueHost.ReleaseMouseCapture(); }
        private void SetHue(Point p)
        {
            double w = _hueHost.ActualWidth;
            if (w <= 0) return;
            double x = Math.Clamp(p.X, 0, w - 1);
            float h = (float)(x / (w - 1)) * 360f;
            var (_, sv, v) = RgbToHsv(Color.FromRgb(_r, _g, _b));
            var rgb = HsvToRgb(h, sv, v);
            _r = rgb.R; _g = rgb.G; _b = rgb.B;
            _loading = true; UpdateAll(); _loading = false;
        }

        // ---- Alpha 交互 ----
        private void AlphaDown(object s, MouseButtonEventArgs e) { _alphaDrag = true; _alphaCanvas.CaptureMouse(); SetAlpha(e.GetPosition(_alphaCanvas)); }
        private void AlphaMove(object s, MouseEventArgs e) { if (_alphaDrag) SetAlpha(e.GetPosition(_alphaCanvas)); }
        private void AlphaUp(object s, MouseButtonEventArgs e) { _alphaDrag = false; _alphaCanvas.ReleaseMouseCapture(); }
        private void SetAlpha(Point p)
        {
            double w = _alphaCanvas.ActualWidth;
            if (w <= 0) return;
            double x = Math.Clamp(p.X, 0, w - 1);
            _a = (byte)Math.Clamp(x / (w - 1) * 255, 0, 255);
            _loading = true; UpdateAll(); _loading = false;
        }

        private void ApplyHex(string hex)
        {
            ParseHex(hex, out _a, out _r, out _g, out _b);
            UpdateAll();
        }

        // ---- Hex ----
        public static void ParseHex(string raw, out byte a, out byte r, out byte g, out byte b)
        {
            string h = (raw ?? "").Trim().TrimStart('#');
            if (h.Length == 6) { r = P(h, 0); g = P(h, 2); b = P(h, 4); a = 255; return; }
            if (h.Length == 8) { a = P(h, 0); r = P(h, 2); g = P(h, 4); b = P(h, 6); return; }
            a = 255; r = 0; g = 0; b = 0;
        }
        private static byte P(string s, int o) => byte.TryParse(s.Substring(o, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : (byte)0;
        public static string ToHex(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        public string GetCurrentHex() => ToHex(_a, _r, _g, _b);

        // ---- HSV ----
        private static float GetHue(Color c) => RgbToHsv(c).H;
        private static (float H, float S, float V) RgbToHsv(Color c)
        {
            float r = c.R / 255f, gn = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(gn, b)), min = Math.Min(r, Math.Min(gn, b));
            float h = 0, s = max == 0 ? 0 : (max - min) / max, v = max;
            if (max != min)
            {
                float d = max - min;
                if (max == r) h = (gn - b) / d + (gn < b ? 6 : 0);
                else if (max == gn) h = (b - r) / d + 2;
                else h = (r - gn) / d + 4;
                h *= 60;
            }
            return (h, s, v);
        }
        private static Color HsvToRgb(float h, float s, float v)
        {
            if (s == 0) { byte gn2 = (byte)(v * 255); return Color.FromRgb(gn2, gn2, gn2); }
            h /= 60f; int i = (int)Math.Floor(h);
            float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            float r, gn, b;
            switch (i % 6)
            {
                case 0: r = v; gn = t; b = p; break;
                case 1: r = q; gn = v; b = p; break;
                case 2: r = p; gn = v; b = t; break;
                case 3: r = p; gn = q; b = v; break;
                case 4: r = t; gn = p; b = v; break;
                default: r = v; gn = p; b = q; break;
            }
            return Color.FromRgb((byte)(r * 255), (byte)(gn * 255), (byte)(b * 255));
        }

        // ---- 渐变画刷 ----
        private static Rectangle MakeHueRect()
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0, 0), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xFF, 0), 1 / 6.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0xFF, 0), 2 / 6.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0xFF, 0xFF), 0.5));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 0xFF), 4 / 6.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0, 0xFF), 5 / 6.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0, 0), 1.0));
            return new Rectangle { Fill = brush, Height = 16 };
        }

        private static Rectangle MakeCheckerRect()
        {
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                new Pen(Brushes.Transparent, 0),
                new RectangleGeometry(new Rect(0, 0, 8, 8))));
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                new Pen(Brushes.Transparent, 0),
                new RectangleGeometry(new Rect(8, 0, 8, 8))));
            var fill = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute
            };
            return new Rectangle { Fill = fill, Height = 18 };
        }

        private static Rectangle MakeAlphaRect()
        {
            // 棋盘格 + 透明→不透明
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                new Pen(Brushes.Transparent, 0),
                new RectangleGeometry(new Rect(0, 0, 8, 8))));
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                new Pen(Brushes.Transparent, 0),
                new RectangleGeometry(new Rect(8, 0, 8, 8))));
            var fadeBrush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            fadeBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
            fadeBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 1));
            group.Children.Add(new GeometryDrawing(fadeBrush, null, new RectangleGeometry(new Rect(0, 0, 200, 16))));
            var fill = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute
            };
            return new Rectangle { Fill = fill, Height = 16 };
        }
    }
}
