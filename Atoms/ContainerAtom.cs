using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lumen.Formula;

namespace Lumen.Atoms
{
    public enum ContainerLayout { Overlap, Stack, Series }

    /// <summary>布局容器：Overlap(重叠)/Stack(纵向堆叠)/Series(横向序列)，承载子原子树（递归渲染）。</summary>
    public class ContainerAtom : Atom
    {
        public PropertyValue LayoutProp = new StaticValue("Stack");
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue RadiusProp = new StaticValue("0");
        public PropertyValue BorderProp = new StaticValue("#00000000");
        public PropertyValue BorderWProp = new StaticValue("0");
        public PropertyValue PaddingProp = new StaticValue("0");
        public List<Atom> Children = new();

        private Panel _panel;
        private Border _border;

        public ContainerAtom() : base("Container") { Bounds = new Rect(440, 120, 400, 320); }

        public override UIElement Render()
        {
            var layout = LayoutProp.Resolve(Ctx).AsStr().Trim().ToLowerInvariant();
            _panel = BuildPanel(layout);
            foreach (var child in Children)
            {
                child.Ctx = Ctx;
                child.OnChanged = () => OnChanged?.Invoke();
                child.RenderMode = RenderModeKind.Nested;   // 子原子递归渲染为 Nested 模式
                child.ParentContainer = this;                // 标记父容器，点击选中冒泡到容器
                var ui = child.Render();
                AddToPanel(_panel, child, layout, ui);
            }
            _border = new Border
            {
                Child = _panel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ApplyDynamic();
            _root = MakeDraggable(_border);
            return _root;
        }

        public override void Update()
        {
            // 自身视觉（背景/圆角/描边/内边距）+ 递归重算子原子动态属性
            ApplyDynamic();
            foreach (var c in Children) c.Update();
        }

        private static Panel BuildPanel(string layout)
        {
            if (layout == "overlap") return new Canvas();
            if (layout == "series") return new WrapPanel { Orientation = Orientation.Horizontal };
            return new StackPanel { Orientation = Orientation.Vertical };
        }

        private static void AddToPanel(Panel panel, Atom child, string layout, UIElement ui)
        {
            if (panel is Canvas cv)
            {
                Canvas.SetLeft(ui, child.Bounds.X);
                Canvas.SetTop(ui, child.Bounds.Y);
                cv.Children.Add(ui);
            }
            else
            {
                panel.Children.Add(ui);
            }
        }

        private void ApplyDynamic()
        {
            if (_border == null) { ApplyCommon(); return; }
            _border.Background = ResolveBrush(BgProp, Ctx, Brushes.Transparent);
            if (double.TryParse(Txt(RadiusProp, Ctx), out var r) && r > 0) _border.CornerRadius = new CornerRadius(r);
            else _border.CornerRadius = new CornerRadius(0);
            _border.BorderBrush = ResolveBrush(BorderProp, Ctx, Brushes.Transparent);
            if (double.TryParse(Txt(BorderWProp, Ctx), out var bw) && bw > 0) _border.BorderThickness = new Thickness(bw);
            else _border.BorderThickness = new Thickness(0);
            if (double.TryParse(Txt(PaddingProp, Ctx), out var p) && p >= 0) _border.Padding = new Thickness(p);
            ApplyCommon();
        }

        public override Dictionary<string, PropertyValue> GetProps()
        {
            var d = new Dictionary<string, PropertyValue>
            {
                ["layout"] = LayoutProp,
                ["bg"] = BgProp, ["radius"] = RadiusProp, ["border"] = BorderProp,
                ["borderW"] = BorderWProp, ["padding"] = PaddingProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("layout", out var l)) LayoutProp = l;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("radius", out var r)) RadiusProp = r;
            if (props.TryGetValue("border", out var bo)) BorderProp = bo;
            if (props.TryGetValue("borderW", out var bw)) BorderWProp = bw;
            if (props.TryGetValue("padding", out var p)) PaddingProp = p;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "layout", Label = "布局", Kind = EditKind.Choice, Choices = new[] { "Overlap", "Stack", "Series" } });
            l.Add(new EditField { Key = "bg", Label = "背景色", Kind = EditKind.Color });
            l.Add(new EditField { Key = "radius", Label = "圆角(px)", Kind = EditKind.Slider, Min = 0, Max = 200 });
            l.Add(new EditField { Key = "border", Label = "描边色", Kind = EditKind.Color });
            l.Add(new EditField { Key = "borderW", Label = "描边宽(px)", Kind = EditKind.Slider, Min = 0, Max = 40 });
            l.Add(new EditField { Key = "padding", Label = "内边距(px)", Kind = EditKind.Slider, Min = 0, Max = 80 });
            return l;
        }
    }
}
