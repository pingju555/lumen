using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lumen.Formula;
using Lumen.I18n;
using Lumen.Render;

namespace Lumen.Atoms
{
    /// <summary>容器基类：承载子原子树（递归渲染）。堆叠组 / 重叠组 / 序列组 均继承此类，
    /// 仅布局方式不同（由 LayoutKey 固定），所有 <c>is ContainerAtom</c> 判定对子类同样成立，
    /// 故 AtomTree / AtomHost / DirtyScheduler / ConfigStore / TreeWindow 等无需改动即可复用。</summary>
    public abstract class ContainerAtom : Atom
    {
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue RadiusProp = new StaticValue("0");
        public PropertyValue BorderProp = new StaticValue("#00000000");
        public PropertyValue BorderWProp = new StaticValue("0");
        public PropertyValue PaddingProp = new StaticValue("0");
        public List<Atom> Children = new();

        /// <summary>布局键：stack=纵向堆叠 / overlap=Canvas 绝对定位 / series=横向流式。由子类固定，不再作为可编辑字段。</summary>
        protected virtual string LayoutKey => "Stack";

        /// <summary>子原子继承的 EvalContext。基类默认=自身 ctx；ComponentAtom 重写以链到父 ctx 并挂组件变量 Resolver，
        /// 使子树公式经 cg() 命中最近组件作用域。</summary>
        protected virtual EvalContext ChildContext => Ctx;

        private Panel _panel;
        private Border _border;
        private Rectangle _dash;

        protected ContainerAtom(string type) : base(type) { }

        public override UIElement Render()
        {
            var layout = LayoutKey.ToLowerInvariant();
            _panel = BuildPanel(layout);
            var cc = ChildContext;   // 子树共享同一子 ctx（含组件变量 Resolver）
            foreach (var child in Children)
            {
                child.Ctx = cc;
                child.OnChanged = () => OnChanged?.Invoke();
                child.RenderMode = RenderModeKind.Nested;   // 子原子递归渲染为 Nested 模式
                child.ParentContainer = this;                // 标记父容器，点击选中冒泡到容器
                var ui = child.Render();
                AddToPanel(_panel, child, layout, ui);
            }
            // 重叠组(Canvas) 不随子部件自动撑开尺寸。先按声明 Bounds 估算，
            // 布局完成后再用子部件「实际渲染尺寸」收紧，使虚线框/命中区贴合内容外轮廓（无多余留白）。
            if (layout == "overlap" && _panel is Canvas cv && Children.Count > 0)
            {
                double maxX = 0, maxY = 0;
                foreach (var ch in Children)
                {
                    maxX = Math.Max(maxX, ch.Bounds.X + ch.Bounds.Width);
                    maxY = Math.Max(maxY, ch.Bounds.Y + ch.Bounds.Height);
                }
                if (maxX > 0 && maxY > 0) { cv.Width = maxX; cv.Height = maxY; }
                cv.LayoutUpdated += (s, e) => ShrinkCanvasToContent(cv);
            }
            // 空容器无子部件时尺寸为 0，画布上不可见且无法点选。给一个最小占位尺寸，
            // 使虚线框可见、可点击选中（有子部件后由内容撑开，不受影响）。
            if (Children.Count == 0)
            {
                _panel.MinWidth = 96;
                _panel.MinHeight = 56;
            }
            _border = new Border
            {
                Child = _panel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _dash = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var outer = new Grid();
            outer.Children.Add(_border);
            outer.Children.Add(_dash);
            ApplyDynamic();
            _root = MakeDraggable(outer);
            return _root;
        }

        public override void Update()
        {
            // 自身视觉（背景/圆角/描边/内边距）+ 递归重算子原子动态属性
            base.Update();
            ApplyDynamic();
            foreach (var c in Children) c.Update();
        }

        private static Panel BuildPanel(string layout)
        {
            if (layout == "overlap") return new Canvas();
            if (layout == "series") return new WrapPanel { Orientation = Orientation.Horizontal };
            return new StackPanel { Orientation = Orientation.Vertical };
        }

        /// <summary>重叠组：按子部件实际渲染尺寸收紧 Canvas，并裁剪左上角空白，使虚线框贴合内容外轮廓。
        /// 带 0.5px 变化阈值防止 Set→LayoutUpdated 无限重排。</summary>
        private static void ShrinkCanvasToContent(Canvas cv)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = 0, maxY = 0;
            foreach (UIElement ui in cv.Children)
            {
                if (ui is not FrameworkElement fe) continue;
                double l = Canvas.GetLeft(ui); if (double.IsNaN(l)) l = 0;
                double t = Canvas.GetTop(ui); if (double.IsNaN(t)) t = 0;
                if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0) continue;
                minX = Math.Min(minX, l);
                minY = Math.Min(minY, t);
                maxX = Math.Max(maxX, l + fe.ActualWidth);
                maxY = Math.Max(maxY, t + fe.ActualHeight);
            }
            if (maxX <= minX || maxY <= minY) return;
            double w = maxX - minX, h = maxY - minY;
            if (Math.Abs(cv.Width - w) > 0.5 || Math.Abs(cv.Height - h) > 0.5)
            {
                cv.Width = w;
                cv.Height = h;
            }
            // 裁剪左上角空白：将最小左上角平移到 (0,0)，避免虚线框比内容多出一圈偏移
            if (minX > 0 || minY > 0)
            {
                foreach (UIElement ui in cv.Children)
                {
                    if (ui is not FrameworkElement fe) continue;
                    double l = Canvas.GetLeft(fe); if (double.IsNaN(l)) l = 0;
                    double t = Canvas.GetTop(fe); if (double.IsNaN(t)) t = 0;
                    Canvas.SetLeft(fe, l - minX);
                    Canvas.SetTop(fe, t - minY);
                }
            }
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
            double.TryParse(Txt(BorderWProp, Ctx), out var bw);
            _border.BorderThickness = bw > 0 ? new Thickness(bw) : new Thickness(0);
            if (double.TryParse(Txt(PaddingProp, Ctx), out var p) && p >= 0) _border.Padding = new Thickness(p);
            if (_dash != null)
            {
                _dash.Visibility = bw > 0 ? Visibility.Collapsed : Visibility.Visible;
                if (r > 0) { _dash.RadiusX = r; _dash.RadiusY = r; }
                else { _dash.RadiusX = 0; _dash.RadiusY = 0; }
            }
            ApplyCommon();
        }

        // ---------- 容器定位模型：尺寸随内部部件自适应，仅由中心点 + 九宫格锚点 + XY 偏移定位 ----------
        /// <summary>容器不框定固定 W/H，由内部子部件决定实际尺寸。</summary>
        public override bool AutoSize => true;
        /// <summary>容器位置语义为「中心点」：九宫格锚点 + 偏移解析为容器中心，渲染时按实际尺寸反推左上角。</summary>
        public override bool CenterAnchored => true;
        /// <summary>容器无需宽/高输入框（尺寸自适应）。</summary>
        protected override bool ShowSizeFields => false;

        public override Dictionary<string, PropertyValue> GetProps()
        {
            var d = new Dictionary<string, PropertyValue>
            {
                ["bg"] = BgProp, ["radius"] = RadiusProp, ["border"] = BorderProp,
                ["borderW"] = BorderWProp, ["padding"] = PaddingProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(Dictionary<string, PropertyValue> props)
        {
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
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgColor"), Kind = EditKind.Color, Tab = "style" });
            l.Add(new EditField { Key = "radius", Label = Loc.T("atom.label.radius"), Kind = EditKind.Slider, Min = 0, Max = 200, Tab = "style" });
            l.Add(new EditField { Key = "border", Label = Loc.T("atom.label.borderColor"), Kind = EditKind.Color, Tab = "style" });
            l.Add(new EditField { Key = "borderW", Label = Loc.T("atom.label.borderWidth"), Kind = EditKind.Slider, Min = 0, Max = 40, Tab = "style" });
            l.Add(new EditField { Key = "padding", Label = Loc.T("atom.label.padding"), Kind = EditKind.Slider, Min = 0, Max = 80, Tab = "style" });
            return l;
        }

        /// <summary>容器：先重映射自身公式引用，再递归子原子（共享同一 old→new 映射表）。</summary>
        public override void RemapIds(Dictionary<string, string> map)
        {
            base.RemapIds(map);
            foreach (var ch in Children) ch.RemapIds(map);
        }
    }
}
