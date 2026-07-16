using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Lumen.Actions;
using Lumen.Core;
using Lumen.Formula;
using Lumen.Render;

namespace Lumen.Atoms
{
    /// <summary>原子渲染模式：Standalone=独立原子（全交互层），Nested=容器子原子（仅内容+点击）。</summary>
    public enum RenderModeKind { Standalone, Nested }

    /// <summary>
    /// 原子抽象基类（P2 升级）：支持属性三元组 + 公式重算 + 拖拽 + 持久化 props。
    /// 每个原子 Render() 建主元素并缓存，Update() 仅重算动态属性（增量重算）。
    /// 详见 docs/project/phases/P1_渲染基座与画布/P1-03_Atom抽象与注册.md
    /// </summary>
    public abstract class Atom
    {
        public string Type { get; }
        /// <summary>8 字符唯一标识（构造时生成，持久化不覆盖）。</summary>
        public string Id { get; private set; }
        /// <summary>用户可显示/重命名的名称（默认 "类型 序号"）。</summary>
        public string Name { get; set; }

        /// <summary>渲染模式（默认独立）；容器子原子在渲染前被设为 Nested。</summary>
        public RenderModeKind RenderMode { get; set; } = RenderModeKind.Standalone;
        public Rect Bounds { get; set; }
        public Action OnChanged { get; set; }
        public EvalContext Ctx { get; set; }
        protected UIElement _root;
        private Grid _rootGrid;
        private Thumb _moveThumb;
        private Border _selectionBorder;
        private bool _pressed;
        private bool _hover;
        private const double PressScaleFactor = 0.92;   // 按下时缩放（视觉下沉）
        private const double HoverScaleFactor = 1.04;   // 悬停时放大（提示可点击）
        private const double PressOpacityFactor = 0.6;  // 按下时透明度系数

        // ---------- 编辑模式点击选中（P6-03） ----------
        /// <summary>选中时触发；传入选中的原子。（容器内子原子冒泡到容器。）</summary>
        public event Action<Atom> OnSelected;
        /// <summary>是否处于选中态（高亮边框）。</summary>
        public bool IsSelected { get; private set; }
        /// <summary>父容器引用（非 null 表示位于某容器内，编辑模式点击选中最顶层容器）。</summary>
        public ContainerAtom ParentContainer { get; set; }
        private static readonly SolidColorBrush SelectionBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));

        /// <summary>标记为选中：加蓝色高亮边框。</summary>
        public void Select()
        {
            IsSelected = true;
            if (_selectionBorder != null)
            {
                _selectionBorder.BorderBrush = SelectionBrush;
                _selectionBorder.BorderThickness = new Thickness(2);
            }
        }
        /// <summary>取消选中：移除边框。</summary>
        public void Deselect()
        {
            IsSelected = false;
            if (_selectionBorder != null)
            {
                _selectionBorder.BorderBrush = null;
                _selectionBorder.BorderThickness = new Thickness(0);
            }
        }
        /// <summary>触发选中事件（从 Standalone/Nested 的点击检测中调用，冒泡到容器）。</summary>
        protected void FireSelected()
        {
            if (ParentContainer != null)
                ParentContainer.FireSelected();
            else
                OnSelected?.Invoke(this);
        }
        // --------------------------------------------------

        /// <summary>
        /// 编辑模式标志（双模式 P3 补充）：true=编辑模式（可拖拽/缩放/右键编辑菜单），
        /// false=桌面模式（原子静态展示、禁交互）。所有原子读取此静态位决定自身交互态。
        /// </summary>
        public static bool EditMode { get; set; } = false;

        /// <summary>
        /// 部件级右键菜单工厂（P3 补充）：由宿主（LumenWindow）注入。
        /// Render() 时每个原子的 Root 都会挂上工厂产出的 ContextMenu，右键部件即弹出其专属菜单。
        /// </summary>
        public static System.Func<Atom, System.Windows.Controls.ContextMenu> ContextMenuFactory;

        /// <summary>通用视觉属性：透明度(0-1)、旋转(度)，所有原子共享，经 ApplyCommon 应用到 _root。</summary>
        public PropertyValue OpacityProp = new StaticValue("1");
        public PropertyValue RotationProp = new StaticValue("0");

        /// <summary>桌面模式左键点击触发的行为（P5 行为系统）；None 表示无动作。</summary>
        public AtomAction ClickAction { get; set; } = AtomAction.None();

        /// <summary>触发器列表（P5 触发器系统）：满足条件自动执行动作，无需点击。按通用 props 持久化。</summary>
        public List<AtomTrigger> Triggers = new();

        // 动画（P5 动画系统 v1：进场 + 循环）
        public PropertyValue AnimEnterProp = new StaticValue("None");
        public PropertyValue AnimLoopProp = new StaticValue("None");
        public PropertyValue AnimEnterDurProp = new StaticValue("400");
        public PropertyValue AnimLoopDurProp = new StaticValue("2000");
        private UIElement _animHost;
        private System.Windows.Media.Animation.Storyboard _enterSb, _loopSb;

        /// <summary>九宫格锚点定位（画布模式）：默认左上角，与 XY 偏移组合决定实际像素坐标。</summary>
        public PropertyValue AnchorProp = new StaticValue("TopLeft");
        public PropertyValue OffsetXProp = new StaticValue("0");
        public PropertyValue OffsetYProp = new StaticValue("0");

        protected Atom(string type)
        {
            Type = type;
            Id = GenerateId();
            Name = type;
        }

        /// <summary>生成 8 字符短唯一标识（时间戳 + 随机数）。</summary>
        private static string GenerateId()
        {
            var ticks = (ulong)(DateTime.UtcNow.Ticks & 0xFFFFFFFFFFFF);
            var rnd = (ulong)(Random.Shared.Next() & 0xFFFF);
            return ((ticks << 16) | rnd).ToString("x8");
        }

        // render 后挂到 Canvas 的根 Grid（供 Popup 定位用）
        public UIElement RootElement => _root;

        /// <summary>缩放最小尺寸（= 最小网格档），resize 不可更小。</summary>
        protected const double MinSize = 20;

        /// <summary>内容尺寸随 Bounds 变化（resize 后调用）；默认无操作，显式尺寸的内容（图形/进度条）重写。</summary>
        protected virtual void SyncSize() { }

        /// <summary>部件级菜单的可编辑字段（基类含通用 透明度/旋转/锚点/偏移；子类按类型追加）。</summary>
        public virtual List<EditField> EditFields() => new()
        {
            new EditField { Key = "anchor",  Label = "锚点",     Kind = EditKind.Choice, Category = FieldCategory.Layout, Choices = new[] { "TopLeft", "TopCenter", "TopRight", "MiddleLeft", "Center", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight" } },
            new EditField { Key = "offsetX", Label = "X偏移(px)",Kind = EditKind.Number, Category = FieldCategory.Layout, Min = -9999, Max = 9999 },
            new EditField { Key = "offsetY", Label = "Y偏移(px)",Kind = EditKind.Number, Category = FieldCategory.Layout, Min = -9999, Max = 9999 },
            new EditField { Key = "opacity",  Label = "透明度",   Kind = EditKind.Slider, Category = FieldCategory.Layout, Min = 0, Max = 1 },
            new EditField { Key = "rotation", Label = "旋转(°)", Kind = EditKind.Slider, Category = FieldCategory.Layout, Min = -180, Max = 180 },
            new EditField { Key = "animEnter", Label = "进场动画", Kind = EditKind.Choice, Category = FieldCategory.Animation, Choices = new[] { "None", "Fade", "Slide", "Zoom", "Drop" } },
            new EditField { Key = "animLoop",  Label = "循环动画", Kind = EditKind.Choice, Category = FieldCategory.Animation, Choices = new[] { "None", "Pulse", "Rotate", "Blink", "Float", "Bounce" } },
            new EditField { Key = "animEnterDur", Label = "进场时长(ms)", Kind = EditKind.Number, Category = FieldCategory.Animation, Min = 0, Max = 10000 },
            new EditField { Key = "animLoopDur",  Label = "循环时长(ms)", Kind = EditKind.Number, Category = FieldCategory.Animation, Min = 100, Max = 60000 },
        };

        public abstract UIElement Render();
        public virtual void Update() { }

        /// <summary>持久化：导出属性三元组（子类重写）。</summary>
        public virtual Dictionary<string, PropertyValue> GetProps() => new();
        /// <summary>持久化：导入属性三元组（子类重写）。</summary>
        public virtual void SetProps(Dictionary<string, PropertyValue> props) { }

        /// <summary>
        /// 包一个透明移动 Thumb + 8 个缩放手柄，返回可挂 Canvas 的 Grid。
        /// 在 Nested 模式下只创建纯内容包装（无移柄/手柄/动画），保留点击动作。
        /// </summary>
        protected Grid MakeDraggable(UIElement content)
        {
            if (RenderMode == RenderModeKind.Nested)
                return MakeClickable(content);

            var grid = FullDraggable(content);
            PlayAnimations();
            return grid;
        }

        /// <summary>Nested 模式轻量包装：仅内容 + 点击动作，无移柄/手柄/动画。</summary>
        private Grid MakeClickable(UIElement content)
        {
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                IsHitTestVisible = true
            };
            grid.Children.Add(content);
            _rootGrid = grid;
            ApplyEditModeTo(grid);
            WireClickFeedback(grid);

            // 选中 Border 包裹
            _selectionBorder = new Border
            {
                Child = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var wrapper = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            wrapper.Children.Add(_selectionBorder);

            // 点击检测（Nested 子原子）——点击穿透：仅可见内容区域选中
            Point? _downPos = null;
            grid.MouseLeftButtonDown += (s, e) => { _downPos = e.GetPosition(grid); };
            grid.MouseLeftButtonUp += (s, e) =>
            {
                if (!EditMode) return;
                if (_downPos == null) return;
                var upPos = e.GetPosition(grid);
                double dist = (upPos - _downPos.Value).Length;
                _downPos = null;
                if (dist < 5 && HitTestVisibleContent(grid, upPos))
                    FireSelected();
            };

            // 改存 wrapper 为 _root（因为 Render() 返回 _root，而 wrapper 是实际的可视根）
            _root = wrapper;
            return wrapper;
        }

        /// <summary>为桌面模式绑定点击动作的原子添加悬停光标反馈（Hand 光标）。</summary>
        private void WireClickFeedback(Grid grid)
        {
            grid.MouseEnter += (s, e) =>
            {
                if (EditMode) return;
                if (ClickAction != null && ClickAction.Kind != ActionKind.None)
                {
                    _hover = true;
                    grid.Cursor = Cursors.Hand;
                    ApplyCommon();
                }
            };
            grid.MouseLeave += (s, e) =>
            {
                if (_hover)
                {
                    _hover = false;
                    grid.Cursor = Cursors.Arrow;
                    ApplyCommon();
                }
                if (_pressed)
                {
                    _pressed = false;
                    ApplyCommon();
                }
            };
        }

        /// <summary>完整交互层：移动 Thumb + 8 缩放手柄 + 悬停/按下反馈。仅供 Standalone 模式使用。</summary>
        private Grid FullDraggable(UIElement content)
        {
            var grid = new Grid
            {
                Width = Bounds.Width,
                Height = Bounds.Height,
                IsHitTestVisible = true,
                Background = Brushes.Transparent
            };
            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            host.Children.Add(content);
            if (content is FrameworkElement fe)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                fe.VerticalAlignment = VerticalAlignment.Stretch;
            }
            grid.Children.Add(host);
            _animHost = host;

            var move = new Thumb
            {
                Background = Brushes.Transparent,
                Opacity = 0.01,
                Cursor = Cursors.SizeAll,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(move);
            _moveThumb = move;
            // 挂部件级右键菜单（工厂可能尚未注入，Render 时已注入）
            if (ContextMenuFactory != null) grid.ContextMenu = ContextMenuFactory.Invoke(this);
            move.DragDelta += (s, e) =>
            {
                if (!EditMode) return;
                var rawX = Bounds.X + e.HorizontalChange;
                var rawY = Bounds.Y + e.VerticalChange;
                var pt = Coord.Snap(new Point(rawX, rawY));
                Bounds = new Rect(pt.X, pt.Y, Bounds.Width, Bounds.Height);
                if (_root != null) { Canvas.SetLeft(_root, pt.X); Canvas.SetTop(_root, pt.Y); }
            };
            move.DragCompleted += (s, e) =>
            {
                // 迷你拖动（<3px）= 点击 → 选中；否则 = 真实拖拽
                double dist = Math.Sqrt(e.HorizontalChange * e.HorizontalChange + e.VerticalChange * e.VerticalChange);
                if (dist < 3 && EditMode)
                {
                    // 点击穿透：仅当点击到可见内容时才选中；空白/透明区域不选中（穿透到下层）
                    if (HitTestVisibleContent(_rootGrid, Mouse.GetPosition(_rootGrid)))
                        FireSelected();
                }
                else
                {
                    WriteBackOffsetFromBounds();
                    OnChanged?.Invoke();
                }
            };

            // P6-03: 移除缩放手柄，尺寸编辑统一在属性编辑器内完成
            _rootGrid = grid;
            ApplyEditModeTo(grid);

            // 鼠标悬停反馈：编辑态无特殊处理；桌面模式有动作时变 Hand 光标
            grid.MouseEnter += (s, e) =>
            {
                if (EditMode) return;
                if (ClickAction != null && ClickAction.Kind != ActionKind.None)
                {
                    _hover = true;
                    if (_moveThumb != null) _moveThumb.Cursor = Cursors.Hand;
                    grid.Cursor = Cursors.Hand;
                    ApplyCommon();
                }
            };
            grid.MouseLeave += (s, e) =>
            {
                if (_hover)
                {
                    _hover = false;
                    if (_moveThumb != null) _moveThumb.Cursor = Cursors.SizeAll;
                    grid.Cursor = Cursors.Arrow;
                    ApplyCommon();
                }
                if (_pressed)
                {
                    _pressed = false;
                    ApplyCommon();
                }
            };
            // 选中 Border 包裹
            _selectionBorder = new Border { Child = grid, SnapsToDevicePixels = true };
            var wrapper = new Grid { Width = Bounds.Width, Height = Bounds.Height, Background = Brushes.Transparent };
            wrapper.Children.Add(_selectionBorder);
            return wrapper;
        }

        /// <summary>点击穿透检测：判断坐标是否落在可见内容上（排除 Grid/Thumb/Border 等容器）。</summary>
        private static bool HitTestVisibleContent(UIElement root, Point pt)
        {
            if (root == null) return true;
            try
            {
                var hit = VisualTreeHelper.HitTest(root, pt);
                if (hit == null || hit.VisualHit == null) return false;
                var el = hit.VisualHit;
                while (el != null && (el is Thumb || el is Grid || el is Border || el is Decorator || el is ContentPresenter || el is Panel))
                    el = VisualTreeHelper.GetParent(el);
                return el != null && el != root;
            }
            catch { return true; }
        }

        /// <summary>按当前 EditMode 应用交互态：编辑模式=可命中+挂部件菜单；桌面模式=不可命中+无菜单（点击落到全局菜单）。</summary>
        private void ApplyEditModeTo(Grid grid)
        {
            bool hasAction = ClickAction != null && ClickAction.Kind != ActionKind.None;
            // 桌面模式下，绑定了动作的原子可被点击（拦截该区域输入以触发动作）；
            // 无动作的原子仍不可命中，点击穿透到覆盖层（保持桌面静态展示）。
            grid.IsHitTestVisible = EditMode || hasAction;
            // P6-03: 已移除缩放手柄，不再需要调用 SetHandlesVisible
            grid.ContextMenu = ContextMenuFactory?.Invoke(this);

            // 移动手柄光标：编辑态/无动作=SizeAll（可拖拽）；桌面态+有动作=Hand（可点击）
            if (_moveThumb != null)
                _moveThumb.Cursor = (EditMode || !hasAction) ? Cursors.SizeAll : Cursors.Hand;

            // 切换模式时清掉交互态（悬停/按下），避免残留视觉
            _pressed = false;
            _hover = false;

            // 仅桌面模式 + 有动作时挂点击；编辑模式不挂（点击用于拖拽/选中）。
            grid.MouseLeftButtonUp -= OnClickAction;
            grid.MouseLeftButtonDown -= OnPressDown;
            if (!EditMode && hasAction)
            {
                grid.MouseLeftButtonDown += OnPressDown;
                grid.MouseLeftButtonUp += OnClickAction;
            }
            ApplyCommon();
        }

        private void OnPressDown(object sender, MouseButtonEventArgs e)
        {
            if (EditMode) return;
            if (ClickAction == null || ClickAction.Kind == ActionKind.None) return;
            if (!_pressed) { _pressed = true; ApplyCommon(); }
        }

        private void OnClickAction(object sender, MouseButtonEventArgs e)
        {
            if (EditMode) return;
            if (ClickAction == null || ClickAction.Kind == ActionKind.None) return;
            e.Handled = true;
            _pressed = false;
            ApplyCommon();
            ActionRunner.Run(ClickAction);
        }

        /// <summary>切换模式后刷新本原子交互态（由宿主遍历调用）。</summary>
        public void ApplyEditMode() => ApplyEditModeTo(_root as Grid);

        /// <summary>
        /// 触发器系统（P5）：每个评估周期检查本原子所有触发器，条件成立即自动触发对应动作（无需点击）。
        /// 仅在桌面模式运行（编辑模式下不触发，避免误触把用户拽出编辑态）。
        /// 由 DirtyScheduler.Tick 对全部原子（含容器子原子）调用。
        /// </summary>
        public void EvaluateTriggers()
        {
            if (EditMode) return;
            if (Ctx == null || Triggers == null || Triggers.Count == 0) return;
            foreach (var t in Triggers)
            {
                try
                {
                    if (t.ShouldFire(Ctx))
                        ActionRunner.RunAll(t.Actions);
                }
                catch (Exception ex)
                {
                    Logger.Log("EvaluateTriggers failed: " + (t.Condition ?? "") + " -> " + ex.Message);
                }
            }
        }

        /// <summary>解析画笔（#RRGGBB / 命名色 / 公式结果）。</summary>
        protected static Brush ResolveBrush(PropertyValue p, EvalContext ctx, Brush fallback)
        {
            var v = p.Resolve(ctx);
            if (v.Type == Formula.ValueType.Color)
                return new SolidColorBrush(ToColor(v.ColorArgb));
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(v.AsStr())); }
            catch { return fallback; }
        }

        protected static string Txt(PropertyValue p, EvalContext ctx)
            => ctx == null ? p.Materialize() : PropertyValue.ResolveText(p, ctx);

        protected static Color ToColor(uint argb)
            => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        /// <summary>通用视觉属性应用到 _root（透明度 / 旋转 / 交互态缩放）；子类 ApplyDynamic 末尾调用。</summary>
        protected void ApplyCommon()
        {
            if (_root == null) return;
            double op = 1.0;
            if (double.TryParse(Txt(OpacityProp, Ctx), out var o)) op = Math.Max(0, Math.Min(1, o));
            if (_pressed) op *= PressOpacityFactor;
            _root.Opacity = op;
            UpdateTransform();
        }

        /// <summary>
        /// 组合渲染变换：旋转（属性）+ 交互态缩放（桌面模式悬停放大 / 按下缩小）。
        /// 用一个 TransformGroup 同时承载，避免互相覆盖；每次 ApplyCommon 重算，
        /// 因此被 ~1s 的增量刷新（Update）调用也不会丢失/漂移交互态。
        /// </summary>
        private void UpdateTransform()
        {
            if (_root is not FrameworkElement fe) return;
            var tg = new TransformGroup();
            if (double.TryParse(Txt(RotationProp, Ctx), out var r) && r != 0)
                tg.Children.Add(new RotateTransform(r, Bounds.Width / 2, Bounds.Height / 2));
            double s = 1.0;
            if (!EditMode && _hover && !_pressed) s = HoverScaleFactor;
            else if (_pressed) s = PressScaleFactor;
            if (s != 1.0)
                tg.Children.Add(new ScaleTransform(s, s, Bounds.Width / 2, Bounds.Height / 2));
            fe.RenderTransform = tg;
        }

        /// <summary>
        /// 动画系统 v1（P5）：进场 + 循环。作用于内容内层 _animHost（与 _root 的交互/旋转变换分层，互不冲突）。
        /// 在 MakeDraggable 末尾调用——每次 Render（含切页重组）都会重播进场，循环则持续运行。
        /// </summary>
        private void PlayAnimations()
        {
            if (_animHost is not FrameworkElement host) return;
            _enterSb?.Stop(); _loopSb?.Stop();
            host.Opacity = 1;
            var (t, s, r) = EnsureTransforms(host);
            // 复位到中性姿态，供进场动画从初始态起步
            t.X = 0; t.Y = 0; s.ScaleX = 1; s.ScaleY = 1; r.Angle = 0;

            var enter = Txt(AnimEnterProp, Ctx).Trim();
            var loop = Txt(AnimLoopProp, Ctx).Trim();
            if (!double.TryParse(Txt(AnimEnterDurProp, Ctx), out var enterDur) || enterDur < 0) enterDur = 0;
            if (!double.TryParse(Txt(AnimLoopDurProp, Ctx), out var loopDur) || loopDur < 100) loopDur = 100;

            var sb = new System.Windows.Media.Animation.Storyboard();
            switch (enter)
            {
                case "Fade":
                    host.Opacity = 0;
                    AddDouble(sb, host, UIElement.OpacityProperty, 0, 1, enterDur, new CubicEase { EasingMode = EasingMode.EaseOut });
                    break;
                case "Slide":
                    host.Opacity = 0; t.X = -64;
                    AddDouble(sb, host, UIElement.OpacityProperty, 0, 1, enterDur, new CubicEase { EasingMode = EasingMode.EaseOut });
                    AddDouble(sb, t, TranslateTransform.XProperty, -64, 0, enterDur, new CubicEase { EasingMode = EasingMode.EaseOut });
                    break;
                case "Zoom":
                    host.Opacity = 0; s.ScaleX = 0.6; s.ScaleY = 0.6;
                    AddDouble(sb, host, UIElement.OpacityProperty, 0, 1, enterDur, new CubicEase { EasingMode = EasingMode.EaseOut });
                    AddDouble(sb, s, ScaleTransform.ScaleXProperty, 0.6, 1, enterDur, new BackEase { EasingMode = EasingMode.EaseOut });
                    AddDouble(sb, s, ScaleTransform.ScaleYProperty, 0.6, 1, enterDur, new BackEase { EasingMode = EasingMode.EaseOut });
                    break;
                case "Drop":
                    host.Opacity = 0; t.Y = -52;
                    AddDouble(sb, host, UIElement.OpacityProperty, 0, 1, enterDur, new CubicEase { EasingMode = EasingMode.EaseOut });
                    AddDouble(sb, t, TranslateTransform.YProperty, -52, 0, enterDur, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 1.8 });
                    break;
            }

            if (sb.Children.Count > 0)
            {
                _enterSb = sb;
                sb.Completed += (s, e) => StartLoopAnimation(loop, loopDur);
                sb.Begin();
            }
            else
            {
                StartLoopAnimation(loop, loopDur);
            }
        }

        private void StartLoopAnimation(string loop, double loopDur)
        {
            if (_animHost is not FrameworkElement host) return;
            var (t, s, r) = EnsureTransforms(host);
            switch (loop)
            {
                case "Pulse":
                    _loopSb = BuildForeverLoop(loopDur, true,
                        (host, UIElement.OpacityProperty, 1, 0.35, new SineEase { EasingMode = EasingMode.EaseInOut }));
                    break;
                case "Rotate":
                    r.Angle = 0;
                    _loopSb = BuildForeverLoop(loopDur, false,
                        (r, RotateTransform.AngleProperty, 0, 360, null));
                    break;
                case "Blink":
                    _loopSb = BuildForeverLoop(loopDur, true,
                        (host, UIElement.OpacityProperty, 1, 0.15, new SineEase { EasingMode = EasingMode.EaseInOut }));
                    break;
                case "Float":
                    t.Y = 0;
                    _loopSb = BuildForeverLoop(loopDur, true,
                        (t, TranslateTransform.YProperty, 0, -12, new SineEase { EasingMode = EasingMode.EaseInOut }));
                    break;
                case "Bounce":
                    _loopSb = BuildForeverLoop(loopDur, true,
                        (s, ScaleTransform.ScaleXProperty, 1, 1.08, new SineEase { EasingMode = EasingMode.EaseOut }),
                        (s, ScaleTransform.ScaleYProperty, 1, 1.08, new SineEase { EasingMode = EasingMode.EaseOut }));
                    break;
            }
        }

        /// <summary>确保 _animHost 持有一个 [Translate, Scale, Rotate] 的 TransformGroup（幂等），返回三者引用。</summary>
        private (TranslateTransform T, ScaleTransform S, RotateTransform R) EnsureTransforms(FrameworkElement host)
        {
            if (host.RenderTransform is TransformGroup g && g.Children.Count == 3)
                return (g.Children[0] as TranslateTransform, g.Children[1] as ScaleTransform, g.Children[2] as RotateTransform);
            var t = new TranslateTransform(0, 0);
            var s = new ScaleTransform(1, 1);
            var r = new RotateTransform(0);
            var group = new TransformGroup();
            group.Children.Add(t); group.Children.Add(s); group.Children.Add(r);
            host.RenderTransform = group;
            host.RenderTransformOrigin = new Point(0.5, 0.5);
            return (t, s, r);
        }

        private static void AddDouble(System.Windows.Media.Animation.Storyboard sb, DependencyObject target, DependencyProperty prop, double from, double to, double durMs, IEasingFunction ease)
        {
            var da = new System.Windows.Media.Animation.DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durMs))
            {
                EasingFunction = ease
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(da, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(da, new PropertyPath(prop));
            sb.Children.Add(da);
        }

        private static System.Windows.Media.Animation.Storyboard BuildForeverLoop(double durMs, bool autoreverse,
            params (DependencyObject target, DependencyProperty prop, double from, double to, IEasingFunction ease)[] anims)
        {
            var sb = new System.Windows.Media.Animation.Storyboard();
            foreach (var a in anims)
            {
                var da = new System.Windows.Media.Animation.DoubleAnimation(a.from, a.to, TimeSpan.FromMilliseconds(durMs))
                {
                    AutoReverse = autoreverse,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = a.ease
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(da, a.target);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(da, new PropertyPath(a.prop));
                sb.Children.Add(da);
            }
            return sb;
        }

        /// <summary>合并通用属性到 props（子类 GetProps 末尾调用）。</summary>
        protected void AddCommonProps(Dictionary<string, PropertyValue> d)
        {
            d["_id"] = new StaticValue(Id);
            d["_name"] = new StaticValue(Name);
            d["anchor"] = AnchorProp;
            d["offsetX"] = OffsetXProp;
            d["offsetY"] = OffsetYProp;
            d["opacity"] = OpacityProp;
            d["rotation"] = RotationProp;
            d["click"] = new StaticValue(ClickAction.Serialize());
            d["triggers"] = new StaticValue(AtomTrigger.SerializeList(Triggers));
            d["animEnter"] = AnimEnterProp;
            d["animLoop"] = AnimLoopProp;
            d["animEnterDur"] = AnimEnterDurProp;
            d["animLoopDur"] = AnimLoopDurProp;
        }

        /// <summary>从 props 读回通用属性（子类 SetProps 末尾调用）。</summary>
        protected void ReadCommonProps(Dictionary<string, PropertyValue> props)
        {
            // _id: 仅在首次加载时设置（构造时已生成，不覆盖持久化的 ID）
            if (props.TryGetValue("_id", out var id) && !string.IsNullOrEmpty(id.Materialize()))
                Id = id.Materialize();
            if (props.TryGetValue("_name", out var n)) Name = n.Materialize() ?? Name;
            if (props.TryGetValue("anchor", out var a)) AnchorProp = a;
            if (props.TryGetValue("offsetX", out var ox)) OffsetXProp = ox;
            if (props.TryGetValue("offsetY", out var oy)) OffsetYProp = oy;
            if (props.TryGetValue("opacity", out var o)) OpacityProp = o;
            if (props.TryGetValue("rotation", out var r)) RotationProp = r;
            if (props.TryGetValue("click", out var c)) ClickAction = AtomAction.Parse(c.Materialize());
            if (props.TryGetValue("triggers", out var tr)) Triggers = AtomTrigger.ParseList(tr.Materialize()) ?? new List<AtomTrigger>();
            if (props.TryGetValue("animEnter", out var ae)) AnimEnterProp = ae;
            if (props.TryGetValue("animLoop", out var al)) AnimLoopProp = al;
            if (props.TryGetValue("animEnterDur", out var aed)) AnimEnterDurProp = aed;
            if (props.TryGetValue("animLoopDur", out var ald)) AnimLoopDurProp = ald;
        }

        /// <summary>深拷贝：经注册表按 Type 建新实例，复制 Bounds + 属性三元组；Container 递归克隆子原子。</summary>
        public virtual Atom Clone()
        {
            var a = AtomRegistry.Create(Type);
            a.Bounds = Bounds;
            a.SetProps(GetProps());
            if (a is ContainerAtom c && this is ContainerAtom src)
                foreach (var ch in src.Children) c.Children.Add(ch.Clone());
            return a;
        }

        /// <summary>把 _root 同步到当前 Bounds（整体移动 / resize 后调用）。</summary>
        public void SyncPosition()
        {
            if (_root != null)
            {
                Canvas.SetLeft(_root, Bounds.X);
                Canvas.SetTop(_root, Bounds.Y);
            }
        }

        /// <summary>
        /// 按九宫格锚点 + XY 偏移重算实际像素位置，写入 Bounds 并同步 _root。
        /// 由宿主在 ComposeCurrentPage 时调用（传入工作区尺寸），确保窗口大小变化时原子正确重定位。
        /// </summary>
        public void RecalcPosition(double areaW, double areaH)
        {
            var anchorStr = Txt(AnchorProp, Ctx).Trim();
            Enum.TryParse<NineAnchor>(anchorStr, out var anchor);
            double.TryParse(Txt(OffsetXProp, Ctx), out var ox);
            double.TryParse(Txt(OffsetYProp, Ctx), out var oy);

            var pos = Coord.ResolveAnchor(anchor, ox, oy, areaW, areaH);
            // 尺寸不变，仅更新位置
            Bounds = new Rect(pos.X, pos.Y, Bounds.Width, Bounds.Height);
            SyncPosition();
        }

        /// <summary>
        /// RecalcPosition 的逆运算：拖拽 / 缩放松手后，把当前像素 Bounds 反解为九宫格偏移，
        /// 回写 OffsetXProp / OffsetYProp。使「拖拽结果」与「属性编辑器显示值」一致，
        /// 且重组时 RecalcPosition 不会用旧偏移把原子弹回原位。
        /// 仅当偏移为静态值时回写（若用户用公式/变量绑定了位置，尊重绑定不覆盖）。
        /// </summary>
        public void WriteBackOffsetFromBounds()
        {
            if (OffsetXProp.Mode != PropMode.Static || OffsetYProp.Mode != PropMode.Static) return;
            var anchorStr = Txt(AnchorProp, Ctx).Trim();
            Enum.TryParse<NineAnchor>(anchorStr, out var anchor);
            var basePt = Coord.ResolveAnchor(anchor, 0, 0, Coord.AreaW, Coord.AreaH);
            int ox = (int)Math.Round(Bounds.X - basePt.X);
            int oy = (int)Math.Round(Bounds.Y - basePt.Y);
            OffsetXProp = new StaticValue(ox.ToString());
            OffsetYProp = new StaticValue(oy.ToString());
        }

        /// <summary>显隐整个原子（小组件整体显隐用）。</summary>
        public void SetVisible(bool v)
        {
            if (_root != null) _root.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
