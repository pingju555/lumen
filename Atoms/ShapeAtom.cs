using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Lumen.Formula;
using Lumen.I18n;

namespace Lumen.Atoms
{
    public enum ShapeKind { Rect, Ellipse, Line, RoundRect }

    /// <summary>形状纹理效果（模拟质感，不依赖 DWM 全窗模糊，逐形状生效）。</summary>
    public enum ShapeTexture
    {
        None, Frosted, Glass, Plastic,
        Metal, Neon, Matte, Wood, Marble, Carbon, Holographic, Paper, Fabric, Liquid
    }

    /// <summary>形状原子：矩形/椭圆/线/圆角矩形，填充支持纯色（渐变 v1.x 预留）。</summary>
    public class ShapeAtom : Atom
    {
        public PropertyValue KindProp = new StaticValue("Rect");
        public PropertyValue FillProp = new StaticValue("#FF4488FF");
        public PropertyValue StrokeProp = new StaticValue("#00000000");
        public PropertyValue StrokeWProp = new StaticValue("0");
        public PropertyValue RadiusProp = new StaticValue("0");
        public PropertyValue DashProp = new StaticValue("Solid");
        public PropertyValue ShadowProp = new StaticValue("0");
        /// <summary>纹理效果：None / Frosted(毛玻璃) / Glass(玻璃感) / Plastic(塑料感) / Metal(金属) / Neon(霓虹) / Matte(哑光) / Wood(木纹) / Marble(大理石) / Carbon(碳纤维) / Holographic(虹彩) / Paper(纸张) / Fabric(布纹) / Liquid(液态)。</summary>
        public PropertyValue TextureProp = new StaticValue("None");

        private Shape _shape;
        private Shape _gloss;

        public ShapeAtom() : base("Shape") { Bounds = new Rect(120, 200, 160, 120); }

        public override UIElement Render()
        {
            var host = new Grid();
            _shape = BuildShape();
            _gloss = BuildShape();
            _gloss.IsHitTestVisible = false;
            _gloss.Stroke = null;
            host.Children.Add(_shape);
            host.Children.Add(_gloss);
            ApplyDynamic();
            _root = MakeDraggable(host);
            return _root;
        }

        public override void Update() { base.Update(); ApplyDynamic(); }

        private Shape BuildShape()
        {
            var kind = KindProp.Resolve(Ctx).AsStr();
            Shape s = kind.Trim().ToLowerInvariant() switch
            {
                "ellipse" => new Ellipse(),
                "line" => new Line { X1 = 0, Y1 = 0, X2 = Bounds.Width, Y2 = Bounds.Height },
                "roundrect" => new Rectangle { RadiusX = 14, RadiusY = 14 },
                _ => new Rectangle()
            };
            s.Width = Bounds.Width;
            s.Height = Bounds.Height;
            s.Stretch = Stretch.Fill;
            return s;
        }

        private static DropShadowEffect MakeShadow() => new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Opacity = 0.5 };

        private void ApplyDynamic()
        {
            if (_shape == null || _gloss == null) return;
            if (Ctx == null) return;

            var fillBrush = ResolveBrush(FillProp, Ctx, Brushes.Transparent);
            var strokeBrush = ResolveBrush(StrokeProp, Ctx, Brushes.Transparent);
            double.TryParse(Txt(StrokeWProp, Ctx), out var w); if (w < 0) w = 0;
            double.TryParse(Txt(RadiusProp, Ctx), out double r); if (r < 0) r = 0;
            switch (Txt(DashProp, Ctx).Trim().ToLowerInvariant())
            {
                case "dash": _shape.StrokeDashArray = new DoubleCollection { 4, 3 }; break;
                case "dot":  _shape.StrokeDashArray = new DoubleCollection { 1, 3 }; break;
                default:     _shape.StrokeDashArray = null; break;
            }
            if (r > 0 && _shape is Rectangle rect)
            {
                rect.RadiusX = r;
                rect.RadiusY = r;
            }
            bool shadow = Txt(ShadowProp, Ctx).Trim() == "1";
            var tex = ParseTexture(Txt(TextureProp, Ctx));
            bool isLine = _gloss is Line;

            if (tex == ShapeTexture.None || isLine)
            {
                // 无纹理 / 线（线无填充面积）：_shape 直接承载外观
                _gloss.Visibility = Visibility.Collapsed;
                _shape.Fill = fillBrush;
                _shape.Stroke = strokeBrush;
                _shape.StrokeThickness = w;
                _shape.Effect = shadow ? MakeShadow() : null;
            }
            else
            {
                // 纹理模式：_shape 退为透明背衬，视觉完全由 _gloss 承载。
                // 这样半透纹理(Glass/Frosted/Liquid/虹彩)能真正透出桌面，不透明纹理(Wood/Marble/...)照常显示。
                _shape.Fill = Brushes.Transparent;
                _shape.Stroke = Brushes.Transparent;
                _shape.StrokeThickness = 0;
                _shape.Effect = null;

                _gloss.Visibility = Visibility.Visible;
                var baseColor = GetDominantColor(fillBrush);
                _gloss.Fill = BuildTextureBrush(tex, baseColor);
                // 描边/发光按质感类型定制，其余沿用用户描边
                switch (tex)
                {
                    case ShapeTexture.Frosted:
                    case ShapeTexture.Glass:
                        _gloss.Stroke = new SolidColorBrush(Color.FromArgb(130, 255, 255, 255));
                        _gloss.StrokeThickness = Math.Max(w, 1);
                        _gloss.Effect = shadow ? MakeShadow() : null;
                        break;
                    case ShapeTexture.Metal:
                        _gloss.Stroke = new SolidColorBrush(Color.FromRgb(222, 228, 234));
                        _gloss.StrokeThickness = Math.Max(w, 1.5);
                        _gloss.Effect = shadow ? MakeShadow() : null;
                        break;
                    case ShapeTexture.Neon:
                    {
                        var neon = Brighten(baseColor, 0.65);
                        _gloss.Stroke = new SolidColorBrush(neon);
                        _gloss.StrokeThickness = Math.Max(w, 2.5);
                        _gloss.Effect = new DropShadowEffect { Color = neon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.95 };
                        break;
                    }
                    default:
                        _gloss.Stroke = strokeBrush;
                        _gloss.StrokeThickness = w;
                        _gloss.Effect = shadow ? MakeShadow() : null;
                        break;
                }
                _gloss.StrokeDashArray = _shape.StrokeDashArray;
                if (r > 0 && _gloss is Rectangle gr)
                {
                    gr.RadiusX = r;
                    gr.RadiusY = r;
                }
            }
            ApplyCommon();
        }

        private static ShapeTexture ParseTexture(string s)
        {
            if (Enum.TryParse<ShapeTexture>(s.Trim(), true, out var v)) return v;
            return ShapeTexture.None;
        }

        private static Color GetDominantColor(Brush b)
        {
            if (b is SolidColorBrush sb) return sb.Color;
            if (b is GradientBrush gb && gb.GradientStops.Count > 0) return gb.GradientStops[0].Color;
            return Colors.Gray;
        }
        private static Color Brighten(Color c, double f)
        {
            double t(double v) => Math.Min(255, v + (255 - v) * f);
            return Color.FromArgb(c.A, (byte)t(c.R), (byte)t(c.G), (byte)t(c.B));
        }
        private static Color Darken(Color c, double f)
        {
            double t(double v) => v * (1 - f);
            return Color.FromArgb(c.A, (byte)t(c.R), (byte)t(c.G), (byte)t(c.B));
        }

        /// <summary>
        /// 构建形状质感笔刷（叠加在 _gloss 上）。baseColor 取自形状自身填充，用于同色系凸起/发光。
        /// Frosted=均匀乳白半透+浅边；Glass=近全透+顶亮条+浅边；Plastic=同色中心亮边缘暗(凸起)；
        /// Metal=强对比渐变+高光条+亮银边；Neon=暗填充+发光边；Matte=哑光噪点；Wood=棕色木纹；
        /// Marble=浅灰白+灰脉络；Carbon=编织格；Holographic=彩虹渐变；Paper=近白+微噪；Fabric=斜纹；Liquid=半透+顶柔光。
        /// </summary>
        private static Brush BuildTextureBrush(ShapeTexture kind, Color baseColor)
        {
            switch (kind)
            {
                case ShapeTexture.Frosted:
                    return new SolidColorBrush(Color.FromArgb(95, 240, 245, 250));
                case ShapeTexture.Glass:
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1) };
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(170, 255, 255, 255), 0.0));
                    b.GradientStops.Add(new GradientStop(Colors.Transparent, 0.1));
                    b.GradientStops.Add(new GradientStop(Colors.Transparent, 0.82));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(28, 0, 0, 0), 1.0));
                    return b;
                }
                case ShapeTexture.Plastic:
                {
                    var b = new RadialGradientBrush
                    {
                        Center = new Point(0.4, 0.32), GradientOrigin = new Point(0.4, 0.32),
                        RadiusX = 0.9, RadiusY = 0.9
                    };
                    b.GradientStops.Add(new GradientStop(Brighten(baseColor, 0.45), 0.0));
                    b.GradientStops.Add(new GradientStop(baseColor, 0.55));
                    b.GradientStops.Add(new GradientStop(Darken(baseColor, 0.3), 1.0));
                    return b;
                }
                case ShapeTexture.Metal:
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1) };
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(232,234,238), 0.0));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(150,154,160), 0.28));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(255,255,255), 0.5));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(120,124,130), 0.72));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(208,212,218), 1.0));
                    return b;
                }
                case ShapeTexture.Neon:
                    return new SolidColorBrush(Color.FromArgb(45, 0, 0, 0));
                case ShapeTexture.Matte:
                    return MakeNoiseBrush(Color.FromArgb(70, 90, 90, 90), 0.06);
                case ShapeTexture.Wood:
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,0) };
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(150, 100, 58), 0.0));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(120, 78, 42), 0.18));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(165, 112, 66), 0.34));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(110, 70, 38), 0.55));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(150, 100, 58), 0.74));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(125, 82, 46), 1.0));
                    return b;
                }
                case ShapeTexture.Marble:
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,1) };
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(244, 244, 240), 0.0));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(225, 226, 222), 0.5));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(205, 207, 205), 0.62));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(238, 238, 234), 0.8));
                    b.GradientStops.Add(new GradientStop(Color.FromRgb(245, 245, 241), 1.0));
                    return b;
                }
                case ShapeTexture.Carbon:
                    return MakeCarbonBrush();
                case ShapeTexture.Holographic:
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,1) };
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 120, 220), 0.0));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 120, 200, 255), 0.33));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 120, 255, 180), 0.66));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 220, 120), 1.0));
                    return b;
                }
                case ShapeTexture.Paper:
                    return MakeNoiseBrush(Color.FromArgb(235, 250, 250, 246), 0.05);
                case ShapeTexture.Fabric:
                    return MakeFabricBrush();
                case ShapeTexture.Liquid:
                {
                    var b = new RadialGradientBrush
                    {
                        Center = new Point(0.5, 0.18), GradientOrigin = new Point(0.5, 0.18),
                        RadiusX = 1.0, RadiusY = 0.7
                    };
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(200, 255, 255, 255), 0.0));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.32));
                    b.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
                    return b;
                }
                default:
                    return null;
            }
        }

        private static Brush MakeNoiseBrush(Color baseColor, double dotAlpha)
        {
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(baseColor), null, new RectangleGeometry(new Rect(0,0,6,6))));
            var dot = new SolidColorBrush(Color.FromArgb((byte)(dotAlpha*255), 255, 255, 255));
            dg.Children.Add(new GeometryDrawing(dot, null, new RectangleGeometry(new Rect(1,1,1,1))));
            dg.Children.Add(new GeometryDrawing(dot, null, new RectangleGeometry(new Rect(4,3,1,1))));
            return new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0,0,6,6), ViewportUnits = BrushMappingMode.Absolute };
        }

        private static Brush MakeCarbonBrush()
        {
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(38,40,46)), null, new RectangleGeometry(new Rect(0,0,14,14))));
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(96,100,110)), 1.2);
            dg.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0,0), new Point(14,14))));
            dg.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(14,0), new Point(0,14))));
            return new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0,0,14,14), ViewportUnits = BrushMappingMode.Absolute };
        }

        private static Brush MakeFabricBrush()
        {
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(225,225,228)), null, new RectangleGeometry(new Rect(0,0,8,8))));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(70, 140,140,150)), 1.0);
            dg.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0,8), new Point(8,0))));
            return new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0,0,8,8), ViewportUnits = BrushMappingMode.Absolute };
        }

        /// <summary>resize 后同步图形尺寸（含线端坐标）。</summary>
        protected override void SyncSize()
        {
            if (_shape == null) return;
            _shape.Width = Bounds.Width;
            _shape.Height = Bounds.Height;
            if (_shape is Line ln) { ln.X2 = Bounds.Width; ln.Y2 = Bounds.Height; }
            if (_gloss != null)
            {
                _gloss.Width = Bounds.Width;
                _gloss.Height = Bounds.Height;
                if (_gloss is Line gln) { gln.X2 = Bounds.Width; gln.Y2 = Bounds.Height; }
            }
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["kind"] = KindProp, ["fill"] = FillProp, ["stroke"] = StrokeProp, ["strokeW"] = StrokeWProp,
                ["radius"] = RadiusProp, ["dash"] = DashProp, ["shadow"] = ShadowProp, ["texture"] = TextureProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(System.Collections.Generic.Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("kind", out var k)) KindProp = k;
            if (props.TryGetValue("fill", out var f)) FillProp = f;
            if (props.TryGetValue("stroke", out var s)) StrokeProp = s;
            if (props.TryGetValue("strokeW", out var w)) StrokeWProp = w;
            if (props.TryGetValue("radius", out var r)) RadiusProp = r;
            if (props.TryGetValue("dash", out var d)) DashProp = d;
            if (props.TryGetValue("shadow", out var sh)) ShadowProp = sh;
            if (props.TryGetValue("texture", out var tx)) TextureProp = tx;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "kind",    Label = Loc.T("atom.label.shape"),     Kind = EditKind.Choice, Choices = new[] { "Rect", "RoundRect", "Ellipse", "Line" }, ChoiceLocPrefix = "atom.shapeKind." });
            l.Add(new EditField { Key = "fill",    Label = Loc.T("atom.label.fill"),     Kind = EditKind.Color });
            l.Add(new EditField { Key = "stroke",  Label = Loc.T("atom.label.stroke"),     Kind = EditKind.Color });
            l.Add(new EditField { Key = "strokeW", Label = Loc.T("atom.label.strokeWidth"), Kind = EditKind.Number, Min = 0, Max = 60 });
            l.Add(new EditField { Key = "radius", Label = Loc.T("atom.label.radius"), Kind = EditKind.Slider, Min = 0, Max = 120 });
            l.Add(new EditField { Key = "dash", Label = Loc.T("atom.label.dash"), Kind = EditKind.Choice, Choices = new[] { "Solid", "Dash", "Dot" }, ChoiceLocPrefix = "atom.dash." });
            l.Add(new EditField { Key = "shadow", Label = Loc.T("atom.label.shadow"), Kind = EditKind.Bool });
            l.Add(new EditField { Key = "texture", Label = Loc.T("atom.label.texture"), Kind = EditKind.Choice, Choices = new[] { "None", "Frosted", "Glass", "Plastic", "Metal", "Neon", "Matte", "Wood", "Marble", "Carbon", "Holographic", "Paper", "Fabric", "Liquid" }, ChoiceLocPrefix = "atom.texture.", Hint = Loc.T("atom.hint.textureColor") });
            return l;
        }
    }
}
