using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Lumen.Formula;
using Lumen.I18n;

namespace Lumen.Atoms
{
    /// <summary>图标原子：图标字体字形（Segoe MDL2 / FontAwesome），Glyph 以十六进制码点存。</summary>
    public class IconAtom : Atom
    {
        public PropertyValue GlyphProp = new StaticValue("E974"); // Segoe MDL2 码点（默认同步图标）
        public PropertyValue FontProp = new StaticValue("Segoe MDL2 Assets");
        public PropertyValue SizeProp = new StaticValue("48");
        public PropertyValue ColorProp = new StaticValue("#FFFFFFFF");
        public PropertyValue BgProp = new StaticValue("#22000000");
        public PropertyValue ShadowProp = new StaticValue("0");

        private TextBlock _tb;
        private Border _bgBorder;

        public IconAtom() : base("Icon") { Bounds = new Rect(120, 340, 80, 80); }

        public override UIElement Render()
        {
            _tb = new TextBlock
            {
                IsHitTestVisible = true,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _bgBorder = new Border
            {
                Child = _tb,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ApplyDynamic();
            _root = MakeDraggable(_bgBorder);
            return _root;
        }

        public override void Update() { base.Update(); ApplyDynamic(); }

        private void ApplyDynamic()
        {
            if (_tb == null) return;
            if (Ctx == null) { _tb.Text = GlyphToChar(GlyphProp.Materialize()); return; }
            _tb.Text = GlyphToChar(Txt(GlyphProp, Ctx));
            if (double.TryParse(Txt(SizeProp, Ctx), out var fs) && fs > 0) _tb.FontSize = fs;
            _tb.Foreground = ResolveBrush(ColorProp, Ctx, Brushes.White);
            _tb.FontFamily = new FontFamily(Txt(FontProp, Ctx));
            if (_bgBorder != null)
            {
                _bgBorder.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
                bool bgOn = (_bgBorder.Background as SolidColorBrush)?.Color.A > 0;
                _bgBorder.CornerRadius = bgOn ? new CornerRadius(9999) : new CornerRadius(0);
                if (Txt(ShadowProp, Ctx).Trim() == "1")
                    _bgBorder.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.6 };
                else _bgBorder.Effect = null;
            }
            ApplyCommon();
        }

        private static string GlyphToChar(string hex)
        {
            try
            {
                int cp = int.Parse(hex.Trim().Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return char.ConvertFromUtf32(cp);
            }
            catch { return "?"; }
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["glyph"] = GlyphProp, ["font"] = FontProp, ["size"] = SizeProp, ["color"] = ColorProp,
                ["bg"] = BgProp, ["shadow"] = ShadowProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(System.Collections.Generic.Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("glyph", out var g)) GlyphProp = g;
            if (props.TryGetValue("font", out var f)) FontProp = f;
            if (props.TryGetValue("size", out var s)) SizeProp = s;
            if (props.TryGetValue("color", out var c)) ColorProp = c;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("shadow", out var sh)) ShadowProp = sh;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "glyph", Label = Loc.T("atom.label.glyph"), Kind = EditKind.Text,  Hint = Loc.T("atom.hint.glyph") });
            l.Add(new EditField { Key = "font",  Label = Loc.T("atom.label.font"),     Kind = EditKind.Choice, Choices = new[] { "Segoe MDL2 Assets", "Segoe UI Symbol", "Segoe UI Emoji", "Arial" } });
            l.Add(new EditField { Key = "size",  Label = Loc.T("atom.label.fontSize"), Kind = EditKind.Number, Min = 1, Max = 400 });
            l.Add(new EditField { Key = "color", Label = Loc.T("atom.label.color"),     Kind = EditKind.Color });
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgCircle"), Kind = EditKind.Color, Tab = "style" });
            l.Add(new EditField { Key = "shadow", Label = Loc.T("atom.label.shadow"), Kind = EditKind.Bool, Tab = "style" });
            return l;
        }
    }
}
