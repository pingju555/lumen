using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumen.Formula;
using Lumen.I18n;

namespace Lumen.Atoms
{
    public enum ProgressKind { Bar, Ring }

    /// <summary>进度原子：进度条(Bar) / 圆环(Ring)，数值绑定公式（如 $bi(level)$）。</summary>
    public class ProgressAtom : Atom
    {
        public PropertyValue ValueProp = new StaticValue("$bi(level)$");
        public PropertyValue KindProp = new StaticValue("Bar");
        public PropertyValue ColorProp = new StaticValue("#FF00FF88");
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue ThicknessProp = new StaticValue("0");
        public PropertyValue ShowTextProp = new StaticValue("0");

        private ProgressBar _bar;
        private Ellipse _ring;
        private Grid _panel;
        private TextBlock _text;
        private FrameworkElement _el;
        private double _frac;

        public ProgressAtom() : base("Progress") { Bounds = new Rect(120, 600, 240, 16); }

        public override UIElement Render()
        {
            var kind = KindProp.Resolve(Ctx).AsStr().Trim().ToLowerInvariant();
            _panel = new Grid();
            if (kind == "ring")
            {
                _ring = new Ellipse();
                _el = _ring;
            }
            else
            {
                _bar = new ProgressBar { Minimum = 0, Maximum = 100 };
                _el = _bar;
            }
            _panel.Children.Add(_el);
            _text = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            _panel.Children.Add(_text);
            _panel.Width = Bounds.Width;
            _panel.Height = Bounds.Height;
            ApplyDynamic();
            _root = MakeDraggable(_panel);
            return _root;
        }

        public override void Update() => ApplyDynamic();

        private void ApplyDynamic()
        {
            if (Ctx == null) return;
            double.TryParse(Txt(ValueProp, Ctx), out var frac);
            frac = Math.Max(0, Math.Min(100, frac)) / 100.0;
            _frac = frac;
            var brush = ResolveBrush(ColorProp, Ctx, Brushes.LimeGreen);

            if (_bar != null)
            {
                _bar.Value = frac * 100;
                _bar.Foreground = brush;
            }
            else if (_ring != null)
            {
                _ring.Stroke = brush;
                _ring.StrokeThickness = 8;
                UpdateRingGeometry();
            }
            // 增强：背景色 / 厚度 / 百分比文本
            var bg = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            if (_bar != null) _bar.Background = bg;
            else if (_ring != null) _ring.Fill = bg;
            if (double.TryParse(Txt(ThicknessProp, Ctx), out var th) && th > 0)
            {
                if (_bar != null) _bar.Height = th;
                else if (_ring != null) _ring.StrokeThickness = th;
            }
            if (Txt(ShowTextProp, Ctx).Trim() == "1")
            {
                _text.Visibility = Visibility.Visible;
                _text.Text = (_frac * 100).ToString("0") + "%";
            }
            else _text.Visibility = Visibility.Collapsed;
            ApplyCommon();
        }

        /// <summary>圆环几何随 Bounds 变化（resize 后重算）。</summary>
        private void UpdateRingGeometry()
        {
            if (_ring == null) return;
            double r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 6;
            double circ = 2 * Math.PI * r;
            _ring.StrokeDashArray = new DoubleCollection { circ, circ };
            _ring.StrokeDashOffset = circ * (1 - _frac);
            _ring.RenderTransform = new RotateTransform(-90, Bounds.Width / 2, Bounds.Height / 2);
        }

        /// <summary>resize 后同步进度条/圆环尺寸。</summary>
        protected override void SyncSize()
        {
            if (_el == null) return;
            _el.Width = Bounds.Width;
            _el.Height = Bounds.Height;
            if (_ring != null) UpdateRingGeometry();
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["value"] = ValueProp, ["kind"] = KindProp, ["color"] = ColorProp,
                ["bg"] = BgProp, ["thickness"] = ThicknessProp, ["showText"] = ShowTextProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(System.Collections.Generic.Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("value", out var v)) ValueProp = v;
            if (props.TryGetValue("kind", out var k)) KindProp = k;
            if (props.TryGetValue("color", out var c)) ColorProp = c;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("thickness", out var th)) ThicknessProp = th;
            if (props.TryGetValue("showText", out var st)) ShowTextProp = st;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "value", Label = Loc.T("atom.label.value"),     Kind = EditKind.Text,   Hint = Loc.T("atom.hint.value") });
            l.Add(new EditField { Key = "kind",  Label = Loc.T("atom.label.progressKind"),     Kind = EditKind.Choice, Choices = new[] { "Bar", "Ring" } });
            l.Add(new EditField { Key = "color", Label = Loc.T("atom.label.color"),     Kind = EditKind.Color });
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgColor"), Kind = EditKind.Color });
            l.Add(new EditField { Key = "thickness", Label = Loc.T("atom.label.thickness"), Kind = EditKind.Slider, Min = 0, Max = 60 });
            l.Add(new EditField { Key = "showText", Label = Loc.T("atom.label.showText"), Kind = EditKind.Bool });
            return l;
        }
    }
}
