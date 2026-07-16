using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Lumen.Atoms;
using Lumen.Formula;

namespace Lumen.Render
{
    /// <summary>预设层类型：壁纸层（仅背景，最底）/ 网格化层（网格格点）/ 原子化 Px 画布层。</summary>
    public enum LayerKind { Wallpaper, Grid, Canvas }

    /// <summary>
    /// 层抽象。v1 仅落 ZIndex / Opacity / Enabled 三项可配，其余（混合模式等）留 v1.x。
    /// Root 为层容器 Panel；CanvasLayer 用 Canvas 承载原子绝对坐标。
    /// </summary>
    public abstract class Layer
    {
        public Panel Root { get; protected set; }
        public LayerKind Kind { get; }
        public int ZIndex { get; set; }
        public double Opacity { get; set; } = 1.0;
        public bool Enabled { get; set; } = true;

        protected Layer(Panel root, LayerKind kind)
        {
            Root = root;
            Kind = kind;
        }
    }

    /// <summary>壁纸层：仅背景，最底层。按 BackgroundRef 渲染——纯色（hex）/ 公式色 / 图片（本地文件）。</summary>
    public class WallpaperLayer : Layer
    {
        private BackgroundRef _bg;
        private EvalContext _ctx;
        private readonly DispatcherTimer _bgTimer;

        public WallpaperLayer() : base(new Grid(), LayerKind.Wallpaper)
        {
            Root.Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));
            _bgTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _bgTimer.Tick += (s, e) => ApplyBg();
        }

        /// <summary>应用背景引用：
        /// - image：本地图片拉伸填充（加载失败回退纯色）；
        /// - 公式 / 全局变量（Source 含 $ 或 gv: 前缀，仅 solid 模式）：经 EvalContext 求值为颜色，且启用 1s 实时刷新；
        /// - solid/其他：解析 hex 颜色（失败回退深色）。全透明→透传（叠桌面/主页壁纸）。</summary>
        public void SetBackground(BackgroundRef bg, EvalContext ctx = null)
        {
            _bg = bg; _ctx = ctx;
            ApplyBg();
            bool isFormula = bg != null && !string.IsNullOrWhiteSpace(bg.Source)
                && (bg.Source.Contains("$") || bg.Source.StartsWith("gv:", StringComparison.OrdinalIgnoreCase));
            _bgTimer.IsEnabled = isFormula;
        }

        private void ApplyBg()
        {
            var bg = _bg;
            var src = bg?.Source ?? "#FF1E1E1E";

            // 图片优先
            if (bg != null && bg.Kind == "image" && !string.IsNullOrWhiteSpace(src))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(src, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    Root.Background = new ImageBrush(bmp) { Stretch = Stretch.Fill };
                    return;
                }
                catch { /* 加载失败 -> 回退纯色 */ }
            }

            // 公式 / 全局变量 → 求值为颜色
            if (_ctx != null && bg != null && !string.IsNullOrWhiteSpace(src)
                && (src.Contains("$") || src.StartsWith("gv:", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var v = PropertyValue.Parse(src).Resolve(_ctx);
                    if (v.Type == Formula.ValueType.Color)
                    {
                        Root.Background = new SolidColorBrush(ToColor(v.ColorArgb));
                        return;
                    }
                    var s = v.AsStr();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        var c = (Color)ColorConverter.ConvertFromString(s.Trim());
                        Root.Background = new SolidColorBrush(c);
                        return;
                    }
                }
                catch { /* 求值失败 → 回退字面量解析 */ }
            }

            // 纯色 / 字面量 hex
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(src.Trim());
                Root.Background = new SolidColorBrush(c);
            }
            catch
            {
                Root.Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));
            }
        }

        private static Color ToColor(uint argb)
            => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }

    /// <summary>网格化层：Canvas 承载网格线（P3 填充 Draw）。</summary>
    public class GridLayer : Layer
    {
        public Canvas GridCanvas => (Canvas)Root;

        public GridLayer() : base(new Canvas(), LayerKind.Grid) { }

        /// <summary>按 gridSize 重绘网格线（线宽/颜色为 P6 设置预留，此处淡灰占位）。</summary>
        public void Draw(double gridSize, double width, double height)
        {
            var c = GridCanvas;
            c.Children.Clear();
            if (gridSize <= 0 || width <= 0 || height <= 0) return;
            var pen = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
            for (double x = 0; x <= width + 0.5; x += gridSize)
                c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = pen, StrokeThickness = 1 });
            for (double y = 0; y <= height + 0.5; y += gridSize)
                c.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = pen, StrokeThickness = 1 });
        }
    }

    /// <summary>原子化 Px 画布层：Canvas 绝对坐标自由放原子。</summary>
    public class CanvasLayer : Layer
    {
        public CanvasLayer() : base(new Canvas(), LayerKind.Canvas) { }
    }
}
