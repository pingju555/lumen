using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Lumen.Formula;

namespace Lumen.Atoms
{
    public enum ShapeKind { Rect, Ellipse, Line, RoundRect }

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

        private Shape _shape;

        public ShapeAtom() : base("Shape") { Bounds = new Rect(120, 200, 160, 120); }

        public override UIElement Render()
        {
            _shape = BuildShape();
            ApplyDynamic();
            _root = MakeDraggable(_shape);
            return _root;
        }

        public override void Update() => ApplyDynamic();

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

        private void ApplyDynamic()
        {
            if (_shape == null) return;
            if (Ctx == null) return;
            _shape.Fill = ResolveBrush(FillProp, Ctx, Brushes.Transparent);
            _shape.Stroke = ResolveBrush(StrokeProp, Ctx, Brushes.Transparent);
            if (double.TryParse(Txt(StrokeWProp, Ctx), out var w) && w >= 0) _shape.StrokeThickness = w;
            if (double.TryParse(Txt(RadiusProp, Ctx), out var r) && r > 0 && _shape is Rectangle rect)
            {
                rect.RadiusX = r;
                rect.RadiusY = r;
            }
            switch (Txt(DashProp, Ctx).Trim().ToLowerInvariant())
            {
                case "dash": _shape.StrokeDashArray = new DoubleCollection { 4, 3 }; break;
                case "dot":  _shape.StrokeDashArray = new DoubleCollection { 1, 3 }; break;
                default:     _shape.StrokeDashArray = null; break;
            }
            if (Txt(ShadowProp, Ctx).Trim() == "1")
                _shape.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Opacity = 0.5 };
            else _shape.Effect = null;
            ApplyCommon();
        }

        /// <summary>resize 后同步图形尺寸（含线端坐标）。</summary>
        protected override void SyncSize()
        {
            if (_shape == null) return;
            _shape.Width = Bounds.Width;
            _shape.Height = Bounds.Height;
            if (_shape is Line ln) { ln.X2 = Bounds.Width; ln.Y2 = Bounds.Height; }
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["kind"] = KindProp, ["fill"] = FillProp, ["stroke"] = StrokeProp, ["strokeW"] = StrokeWProp,
                ["radius"] = RadiusProp, ["dash"] = DashProp, ["shadow"] = ShadowProp
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
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "kind",    Label = "形状",     Kind = EditKind.Choice, Choices = new[] { "Rect", "RoundRect", "Ellipse", "Line" } });
            l.Add(new EditField { Key = "fill",    Label = "填充",     Kind = EditKind.Color });
            l.Add(new EditField { Key = "stroke",  Label = "描边",     Kind = EditKind.Color });
            l.Add(new EditField { Key = "strokeW", Label = "描边宽(px)", Kind = EditKind.Number, Min = 0, Max = 60 });
            l.Add(new EditField { Key = "radius", Label = "圆角(px)", Kind = EditKind.Slider, Min = 0, Max = 120 });
            l.Add(new EditField { Key = "dash", Label = "虚线", Kind = EditKind.Choice, Choices = new[] { "Solid", "Dash", "Dot" } });
            l.Add(new EditField { Key = "shadow", Label = "阴影", Kind = EditKind.Bool });
            return l;
        }
    }
}
