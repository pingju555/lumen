using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumen.Formula;

namespace Lumen.Atoms
{
    /// <summary>图像原子：位图源 + 适应/拉伸/平铺/居中/填充模式（背景复用）。</summary>
    public class ImageAtom : Atom
    {
        public PropertyValue SourceProp = new StaticValue("");
        public PropertyValue StretchProp = new StaticValue("Uniform");
        public PropertyValue RadiusProp = new StaticValue("0");
        public PropertyValue BgProp = new StaticValue("#00000000");

        private Image _img;
        private Border _border;

        public ImageAtom() : base("Image") { Bounds = new Rect(120, 440, 200, 150); }

        public override UIElement Render()
        {
            _img = new Image { IsHitTestVisible = true };
            _border = new Border
            {
                Child = _img,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ApplyDynamic();
            _root = MakeDraggable(_border);
            return _root;
        }

        public override void Update() => ApplyDynamic();

        private void ApplyDynamic()
        {
            if (_img == null || Ctx == null) return;
            var src = Txt(SourceProp, Ctx);
            if (!string.IsNullOrWhiteSpace(src) && File.Exists(src))
            {
                try { _img.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(src)); }
                catch { /* 坏图忽略 */ }
            }
            if (System.Enum.TryParse<Stretch>(Txt(StretchProp, Ctx), true, out var st))
                _img.Stretch = st;
            if (double.TryParse(Txt(RadiusProp, Ctx), out var r) && r > 0) _border.CornerRadius = new CornerRadius(r);
            else _border.CornerRadius = new CornerRadius(0);
            _border.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            ApplyCommon();
        }

        public override System.Collections.Generic.Dictionary<string, PropertyValue> GetProps()
        {
            var d = new System.Collections.Generic.Dictionary<string, PropertyValue>
            {
                ["source"] = SourceProp, ["stretch"] = StretchProp,
                ["radius"] = RadiusProp, ["bg"] = BgProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(System.Collections.Generic.Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("source", out var s)) SourceProp = s;
            if (props.TryGetValue("stretch", out var st)) StretchProp = st;
            if (props.TryGetValue("radius", out var r)) RadiusProp = r;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "source",  Label = "图片路径", Kind = EditKind.File,   Hint = "本地图片文件" });
            l.Add(new EditField { Key = "stretch", Label = "适应模式", Kind = EditKind.Choice, Choices = new[] { "None", "Uniform", "Fill", "UniformToFill" } });
            l.Add(new EditField { Key = "radius", Label = "圆角(px)", Kind = EditKind.Slider, Min = 0, Max = 200 });
            l.Add(new EditField { Key = "bg", Label = "占位背景", Kind = EditKind.Color });
            return l;
        }
    }
}
