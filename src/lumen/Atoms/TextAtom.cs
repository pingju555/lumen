using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Lumen.Formula;
using Lumen.I18n;

namespace Lumen.Atoms
{
    /// <summary>
    /// 文本原子：支持 4 种尺寸模式。
    ///   FixedHeight — 字号决定高度，宽度自动贴合内容
    ///   AutoWidth   — 给定宽度约束，字号等比例缩小至文本刚好填满
    ///   FixedWidth  — 给定宽度+最大行数(0=无限)，自动换行，高度自适应
    ///   FitBounds   — 给定宽高，字号自动贴合填满
    /// </summary>
    public class TextAtom : Atom
    {
        /// <summary>尺寸模式：FixedHeight / AutoWidth / FixedWidth / FitBounds</summary>
        public PropertyValue SizingProp = new StaticValue("FixedHeight");
        /// <summary>固定/自适应/适应模式下的宽度约束（px）</summary>
        public PropertyValue ConstrainedWProp = new StaticValue("300");
        /// <summary>FitBounds 模式下的高度约束（px）</summary>
        public PropertyValue ConstrainedHProp = new StaticValue("100");
        /// <summary>FixedWidth 最大行数（0=无限）</summary>
        public PropertyValue MaxLinesProp = new StaticValue("0");

        public PropertyValue TextProp = new StaticValue("文本");
        public PropertyValue ColorProp = new StaticValue("#FFFFFFFF");
        public PropertyValue SizeProp = new StaticValue("24");
        public PropertyValue FontProp = new StaticValue("Segoe UI");
        public PropertyValue WeightProp = new StaticValue("Normal");
        public PropertyValue AlignProp = new StaticValue("Left");
        public PropertyValue LineHeightProp = new StaticValue("0");
        public PropertyValue ShadowProp = new StaticValue("0");
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue PaddingProp = new StaticValue("6");

        private TextBlock _tb;
        private Border _bgBorder;

        public TextAtom() : base("Text") { Bounds = new Rect(120, 120, 300, 48); }

        public override UIElement Render()
        {
            _tb = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 24,
                Padding = new Thickness(4),
                IsHitTestVisible = true
            };
            _bgBorder = new Border { Child = _tb, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            ApplyDynamic();
            // 用 Border 包裹替代直接 MakeDraggable TextBlock
            _root = MakeDraggable(_bgBorder);
            return _root;
        }

        public override void Update() { base.Update(); ApplyDynamic(); }

        private void ApplyDynamic()
        {
            if (_tb == null) return;
            _tb.Text = Txt(TextProp, Ctx);
            if (double.TryParse(Txt(SizeProp, Ctx), out var fs) && fs > 0) _tb.FontSize = fs;
            _tb.Foreground = ResolveBrush(ColorProp, Ctx, Brushes.White);
            try { _tb.FontFamily = new FontFamily(Txt(FontProp, Ctx)); } catch { }
            try { _tb.FontWeight = (FontWeight)new FontWeightConverter().ConvertFromString(Txt(WeightProp, Ctx)); } catch { }
            if (System.Enum.TryParse<TextAlignment>(Txt(AlignProp, Ctx), true, out var ta)) _tb.TextAlignment = ta;
            if (double.TryParse(Txt(LineHeightProp, Ctx), out var lh) && lh > 0) _tb.LineHeight = lh; else _tb.LineHeight = double.NaN;
            _tb.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            if (double.TryParse(Txt(PaddingProp, Ctx), out var pad) && pad >= 0) _tb.Padding = new Thickness(pad);
            if (Txt(ShadowProp, Ctx).Trim() == "1")
                _tb.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.6 };
            else _tb.Effect = null;

            ApplySizingMode();
            ApplyCommon();
        }

        /// <summary>按 TextSizingMode 驱动 TextBlock 尺寸行为。</summary>
        private void ApplySizingMode()
        {
            var mode = Txt(SizingProp, Ctx).Trim();
            double.TryParse(Txt(ConstrainedWProp, Ctx), out var cw); if (cw <= 0) cw = 300;
            double.TryParse(Txt(ConstrainedHProp, Ctx), out var ch); if (ch <= 0) ch = 100;
            int.TryParse(Txt(MaxLinesProp, Ctx), out var maxLines); if (maxLines < 0) maxLines = 0;
            double.TryParse(Txt(SizeProp, Ctx), out var fontSize); if (fontSize <= 0) fontSize = 24;

            switch (mode.ToLowerInvariant())
            {
                case "fixedheight":
                    // 字号决定高度，宽度自动贴合
                    _tb.TextWrapping = TextWrapping.NoWrap;
                    _tb.MaxWidth = double.PositiveInfinity;
                    _tb.Width = double.NaN;
                    _tb.Height = double.NaN;
                    _bgBorder.Width = double.NaN;
                    _bgBorder.Height = double.NaN;
                    _bgBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    _bgBorder.VerticalAlignment = VerticalAlignment.Top;
                    break;

                case "autowidth":
                    // 给定宽度约束，字号等比缩小适配
                    _tb.TextWrapping = TextWrapping.NoWrap;
                    _bgBorder.Width = cw;
                    _bgBorder.Height = double.NaN;
                    _bgBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    _bgBorder.VerticalAlignment = VerticalAlignment.Top;
                    FitFontToWidth(cw);
                    break;

                case "fixedwidth":
                    // 固定宽度 + 自动换行 + 最大行数
                    _tb.TextWrapping = TextWrapping.Wrap;
                    _bgBorder.Width = cw;
                    _bgBorder.Height = double.NaN;
                    _bgBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    _bgBorder.VerticalAlignment = VerticalAlignment.Top;
                    _tb.MaxHeight = double.PositiveInfinity;
                    if (maxLines > 0)
                        _tb.MaxHeight = fontSize * maxLines * 1.4;
                    break;

                case "fitbounds":
                    // 给定宽高，字号自动贴合填满
                    _tb.TextWrapping = TextWrapping.Wrap;
                    _bgBorder.Width = cw;
                    _bgBorder.Height = ch;
                    _bgBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _bgBorder.VerticalAlignment = VerticalAlignment.Stretch;
                    FitFontToBounds(cw, ch);
                    break;
            }
        }

        /// <summary>逐步缩小字号直至文本宽度 <= 约束宽度（AutoWidth 模式）。</summary>
        private void FitFontToWidth(double maxWidth)
        {
            double fs = _tb.FontSize;
            while (fs > 4)
            {
                _tb.FontSize = fs;
                _tb.Measure(new Size(maxWidth, double.PositiveInfinity));
                if (_tb.DesiredSize.Width <= maxWidth) return;
                fs -= 0.5;
            }
        }

        /// <summary>逐步缩小字号直至文本同时适配宽高约束（FitBounds 模式）。</summary>
        private void FitFontToBounds(double maxW, double maxH)
        {
            double fs = _tb.FontSize;
            while (fs > 4)
            {
                _tb.FontSize = fs;
                _tb.Measure(new Size(maxW, maxH));
                if (_tb.DesiredSize.Width <= maxW && _tb.DesiredSize.Height <= maxH) return;
                fs -= 0.5;
            }
        }

        public override Dictionary<string, PropertyValue> GetProps()
        {
            var d = new Dictionary<string, PropertyValue>
            {
                ["text"] = TextProp, ["color"] = ColorProp, ["size"] = SizeProp,
                ["font"] = FontProp, ["weight"] = WeightProp, ["align"] = AlignProp,
                ["lineHeight"] = LineHeightProp, ["shadow"] = ShadowProp,
                ["bg"] = BgProp, ["padding"] = PaddingProp,
                ["sizing"] = SizingProp, ["constW"] = ConstrainedWProp,
                ["constH"] = ConstrainedHProp, ["maxLines"] = MaxLinesProp
            };
            AddCommonProps(d);
            return d;
        }

        public override void SetProps(Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("text", out var t)) TextProp = t;
            if (props.TryGetValue("color", out var c)) ColorProp = c;
            if (props.TryGetValue("size", out var s)) SizeProp = s;
            if (props.TryGetValue("font", out var f)) FontProp = f;
            if (props.TryGetValue("weight", out var w)) WeightProp = w;
            if (props.TryGetValue("align", out var a)) AlignProp = a;
            if (props.TryGetValue("lineHeight", out var lh)) LineHeightProp = lh;
            if (props.TryGetValue("shadow", out var sh)) ShadowProp = sh;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("padding", out var pd)) PaddingProp = pd;
            if (props.TryGetValue("sizing", out var sz)) SizingProp = sz;
            if (props.TryGetValue("constW", out var cw)) ConstrainedWProp = cw;
            if (props.TryGetValue("constH", out var ch)) ConstrainedHProp = ch;
            if (props.TryGetValue("maxLines", out var ml)) MaxLinesProp = ml;
            ReadCommonProps(props);
        }

        public override string ToString() => $"Text[{Bounds.X:0},{Bounds.Y:0}] \"{TextProp.Materialize()}\"";

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "sizing", Label = "Sizing", Kind = EditKind.Choice,
                Choices = new[] { "FixedHeight", "AutoWidth", "FixedWidth", "FitBounds" } });
            l.Add(new EditField { Key = "text",  Label = Loc.T("atom.label.text"),   Kind = EditKind.Text,  Hint = Loc.T("atom.hint.text") });
            l.Add(new EditField { Key = "color", Label = Loc.T("atom.label.color"),   Kind = EditKind.Color });
            l.Add(new EditField { Key = "size",  Label = "Font Size", Kind = EditKind.Number, Min = 1, Max = 400 });
            l.Add(new EditField { Key = "font", Label = Loc.T("atom.label.font"), Kind = EditKind.Choice,
                Choices = new[] { "Segoe UI", "Microsoft YaHei", "Arial", "Consolas", "Times New Roman" } });
            l.Add(new EditField { Key = "weight", Label = Loc.T("atom.label.weight"), Kind = EditKind.Choice,
                Choices = new[] { "Thin", "Light", "Normal", "Bold", "ExtraBold" } });
            l.Add(new EditField { Key = "align", Label = Loc.T("atom.label.align"), Kind = EditKind.Choice,
                Choices = new[] { "Left", "Center", "Right", "Justify" } });
            l.Add(new EditField { Key = "lineHeight", Label = "Line Height", Kind = EditKind.Slider, Min = 0, Max = 48 });
            l.Add(new EditField { Key = "constW", Label = "Width", Kind = EditKind.Number, Min = 20, Max = 2000 });
            l.Add(new EditField { Key = "constH", Label = "Height", Kind = EditKind.Number, Min = 20, Max = 2000 });
            l.Add(new EditField { Key = "maxLines", Label = "Max Lines", Kind = EditKind.Number, Min = 0, Max = 100 });
            l.Add(new EditField { Key = "shadow", Label = Loc.T("atom.label.shadow"), Kind = EditKind.Bool, Tab = "style" });
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgColor"), Kind = EditKind.Color, Tab = "style" });
            l.Add(new EditField { Key = "padding", Label = Loc.T("atom.label.padding"), Kind = EditKind.Slider, Min = 0, Max = 40, Tab = "style" });
            return l;
        }
    }
}
