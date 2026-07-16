using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Lumen.Formula;

namespace Lumen.Atoms
{
    /// <summary>文本原子：支持三元组（静态 / gv / $公式$ 内联）。v1 暂不含 CurvedText。</summary>
    public class TextAtom : Atom
    {
        public PropertyValue TextProp = new StaticValue("Text");
        public PropertyValue ColorProp = new StaticValue("#FFFFFFFF");
        public PropertyValue SizeProp = new StaticValue("24");
        public PropertyValue FontProp = new StaticValue("Segoe UI");
        public PropertyValue WeightProp = new StaticValue("Normal");
        public PropertyValue AlignProp = new StaticValue("Left");
        public PropertyValue LineHeightProp = new StaticValue("0");
        public PropertyValue WrapProp = new StaticValue("0");
        public PropertyValue ShadowProp = new StaticValue("0");
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue PaddingProp = new StaticValue("4");

        private TextBlock _tb;

        public TextAtom() : base("Text") { Bounds = new Rect(120, 120, 300, 48); }

        public override UIElement Render()
        {
            _tb = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 24,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(4),
                IsHitTestVisible = true
            };
            ApplyDynamic();
            _root = MakeDraggable(_tb);
            return _root;
        }

        public override void Update() => ApplyDynamic();

        private void ApplyDynamic()
        {
            if (_tb == null) return;
            _tb.Text = Txt(TextProp, Ctx);
            if (double.TryParse(Txt(SizeProp, Ctx), out var fs) && fs > 0) _tb.FontSize = fs;
            _tb.Foreground = ResolveBrush(ColorProp, Ctx, Brushes.White);
            // 样式增强
            try { _tb.FontFamily = new FontFamily(Txt(FontProp, Ctx)); } catch { }
            try { _tb.FontWeight = (FontWeight)new FontWeightConverter().ConvertFromString(Txt(WeightProp, Ctx)); } catch { }
            if (System.Enum.TryParse<TextAlignment>(Txt(AlignProp, Ctx), true, out var ta)) _tb.TextAlignment = ta;
            if (double.TryParse(Txt(LineHeightProp, Ctx), out var lh) && lh > 0) _tb.LineHeight = lh; else _tb.LineHeight = double.NaN;
            _tb.TextWrapping = Txt(WrapProp, Ctx).Trim() == "1" ? TextWrapping.Wrap : TextWrapping.NoWrap;
            _tb.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            if (double.TryParse(Txt(PaddingProp, Ctx), out var pad) && pad >= 0) _tb.Padding = new Thickness(pad);
            if (Txt(ShadowProp, Ctx).Trim() == "1")
                _tb.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.6 };
            else _tb.Effect = null;
            ApplyCommon();
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["text"] = TextProp, ["color"] = ColorProp, ["size"] = SizeProp,
                ["font"] = FontProp, ["weight"] = WeightProp, ["align"] = AlignProp,
                ["lineHeight"] = LineHeightProp, ["wrap"] = WrapProp, ["shadow"] = ShadowProp,
                ["bg"] = BgProp, ["padding"] = PaddingProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(System.Collections.Generic.Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("text", out var t)) TextProp = t;
            if (props.TryGetValue("color", out var c)) ColorProp = c;
            if (props.TryGetValue("size", out var s)) SizeProp = s;
            if (props.TryGetValue("font", out var f)) FontProp = f;
            if (props.TryGetValue("weight", out var w)) WeightProp = w;
            if (props.TryGetValue("align", out var a)) AlignProp = a;
            if (props.TryGetValue("lineHeight", out var lh)) LineHeightProp = lh;
            if (props.TryGetValue("wrap", out var wp)) WrapProp = wp;
            if (props.TryGetValue("shadow", out var sh)) ShadowProp = sh;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("padding", out var pd)) PaddingProp = pd;
            ReadCommonProps(props);
        }

        public override string ToString() => $"Text[{Bounds.X:0},{Bounds.Y:0}] \"{TextProp.Materialize()}\"";

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "text",  Label = "文本",   Kind = EditKind.Text,  Hint = "支持 $公式$ 与 gv:名称" });
            l.Add(new EditField { Key = "color", Label = "颜色",   Kind = EditKind.Color });
            l.Add(new EditField { Key = "size",  Label = "字号(px)", Kind = EditKind.Number, Min = 1, Max = 400 });
            l.Add(new EditField { Key = "font", Label = "字体", Kind = EditKind.Choice, Choices = new[] { "Segoe UI", "Microsoft YaHei", "Microsoft YaHei UI", "Arial", "Consolas", "Times New Roman" } });
            l.Add(new EditField { Key = "weight", Label = "字重", Kind = EditKind.Choice, Choices = new[] { "Thin", "Light", "Normal", "Bold", "ExtraBold" } });
            l.Add(new EditField { Key = "align", Label = "水平对齐", Kind = EditKind.Choice, Choices = new[] { "Left", "Center", "Right", "Justify" } });
            l.Add(new EditField { Key = "lineHeight", Label = "行距(px)", Kind = EditKind.Slider, Min = 0, Max = 48 });
            l.Add(new EditField { Key = "wrap", Label = "自动换行", Kind = EditKind.Bool });
            l.Add(new EditField { Key = "shadow", Label = "阴影", Kind = EditKind.Bool });
            l.Add(new EditField { Key = "bg", Label = "背景色", Kind = EditKind.Color });
            l.Add(new EditField { Key = "padding", Label = "内边距(px)", Kind = EditKind.Slider, Min = 0, Max = 40 });
            return l;
        }
    }
}
