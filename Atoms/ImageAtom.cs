using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumen.Formula;
using Lumen.I18n;

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
        private TextBlock _placeholder;

        public ImageAtom() : base("Image") { Bounds = new Rect(120, 440, 200, 150); }

        public override UIElement Render()
        {
            _img = new Image { IsHitTestVisible = true };
            _placeholder = new TextBlock
            {
                Text = Loc.T("atom.image.empty"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray),
                IsHitTestVisible = false
            };
            var imgGrid = new Grid();
            imgGrid.Children.Add(_img);
            imgGrid.Children.Add(_placeholder);
            _border = new Border
            {
                Child = imgGrid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ApplyDynamic();
            _root = MakeDraggable(_border);
            return _root;
        }

        public override void Update() { base.Update(); ApplyDynamic(); }

        private void ApplyDynamic()
        {
            if (_img == null) return;
            var src = Ctx != null ? Txt(SourceProp, Ctx) : SourceProp.Materialize();
            bool hasImg = !string.IsNullOrWhiteSpace(src) && File.Exists(src);
            if (hasImg)
            {
                try { _img.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(src)); }
                catch { _img.Source = null; }
                if (_placeholder != null) _placeholder.Visibility = Visibility.Collapsed;
                _border.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            }
            else
            {
                _img.Source = null;
                if (_placeholder != null) _placeholder.Visibility = Visibility.Visible;
                _border.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            }
            if (System.Enum.TryParse<Stretch>(Txt(StretchProp, Ctx), true, out var st))
                _img.Stretch = st;
            if (double.TryParse(Txt(RadiusProp, Ctx), out var r) && r > 0) _border.CornerRadius = new CornerRadius(r);
            else _border.CornerRadius = new CornerRadius(0);
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
            l.Add(new EditField { Key = "source",  Label = Loc.T("atom.label.imageSource"), Kind = EditKind.File,   Hint = Loc.T("atom.hint.source") });
            l.Add(new EditField { Key = "stretch", Label = Loc.T("atom.label.stretch"), Kind = EditKind.Choice, Choices = new[] { "None", "Uniform", "Fill", "UniformToFill" } });
            l.Add(new EditField { Key = "radius", Label = Loc.T("atom.label.radius"), Kind = EditKind.Slider, Min = 0, Max = 200, Tab = "style" });
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgPlaceholder"), Kind = EditKind.Color, Tab = "style" });
            return l;
        }
    }
}
