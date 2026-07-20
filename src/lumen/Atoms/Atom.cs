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
using Lumen.I18n;
using Lumen.Render;
using System.Text.RegularExpressions;

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
                _selectionBorder.CornerRadius = new CornerRadius(4); // 圆角选中框
                _selectionBorder.Padding = new Thickness(2); // 选中框内边距，视觉上更明显
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
                _selectionBorder.CornerRadius = new CornerRadius(0);
                _selectionBorder.Padding = new Thickness(0);
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

        /// <summary>尺寸（宽/高，px）：经属性编辑器或公式/变量绑定；null 表示沿用 Bounds 构造值。ApplySize 解析后写回 Bounds 并同步渲染。</summary>
        public PropertyValue WidthProp;
        public PropertyValue HeightProp;

        /// <summary>桌面模式左键点击触发的行为（P5 行为系统）；None 表示无动作。</summary>
        public AtomAction ClickAction { get; set; } = AtomAction.None();

        /// <summary>流程列表（P5 流程系统）：满足条件自动执行动作序列，无需点击。按通用 props 持久化。</summary>
        public List<Flow> Flows = new();

        // 动画（P5 动画系统 v1：进场 + 循环）
        public PropertyValue AnimEnterProp = new StaticValue("None");
        public PropertyValue AnimLoopProp = new StaticValue("None");
        public PropertyValue AnimEnterDurProp = new StaticValue("400");
        public PropertyValue AnimLoopDurProp = new StaticValue("2000");
        /// <summary>动画触发条件（布尔公式，留空=无条件自动播放）。条件成立(上升沿)才播放进场→循环；为假则停止并复位。</summary>
        public PropertyValue AnimWhenProp = new StaticValue("");
        /// <summary>进度驱动动画配置（JSON，见 ProgressAnimDef）。</summary>
        public PropertyValue ProgressAnimProp = new StaticValue("");
        /// <summary>进度动画：触发类型（Timer / Formula / Touch / None）</summary>
        public PropertyValue ProgressTriggerProp = new StaticValue("None");
        public PropertyValue ProgressFormulaProp = new StaticValue("");
        public PropertyValue ProgressDurProp = new StaticValue("1000");
        public PropertyValue ProgressEasingProp = new StaticValue("linear");
        public PropertyValue ProgressFadeFromProp = new StaticValue("-1");
        public PropertyValue ProgressFadeToProp = new StaticValue("-1");
        public PropertyValue ProgressTxProp = new StaticValue("0");
        public PropertyValue ProgressTyProp = new StaticValue("0");
        public PropertyValue ProgressRotProp = new StaticValue("0");
        public PropertyValue ProgressScaleProp = new StaticValue("-1");
        /// <summary>混合模式（Normal / Multiply / Screen / Overlay / Darken / Lighten / Difference）</summary>
        public PropertyValue BlendProp = new StaticValue("Normal");
        /// <summary>模糊半径（0~40，0=无模糊）</summary>
        public PropertyValue BlurProp = new StaticValue("0");
        private double _progressAnimElapsed;
        private bool _progressAnimRunning;
        private bool _progressAnimTouchPending;
        private UIElement _animHost;
        private System.Windows.Media.Animation.Storyboard _enterSb, _loopSb;
        private bool _animActive;   // 条件驱动：当前是否处于"播放中"（用于边沿检测，避免每拍重置循环）

        /// <summary>九宫格锚点定位（画布模式）：默认左上角，与 XY 偏移组合决定实际像素坐标。</summary>
        public PropertyValue AnchorProp = new StaticValue("TopLeft");
        public PropertyValue OffsetXProp = new StaticValue("0");
        public PropertyValue OffsetYProp = new StaticValue("0");

        /// <summary>尺寸是否随内部内容自适应（容器=是，不强制 Width/Height）。</summary>
        public virtual bool AutoSize => false;
        /// <summary>位置语义是否为「中心点」（容器=是：九宫格锚点+偏移解析为中心，渲染按实际尺寸反推左上）。</summary>
        public virtual bool CenterAnchored => false;
        /// <summary>属性面板是否显示 宽/高 字段（容器=否，尺寸自适应）。</summary>
        protected virtual bool ShowSizeFields => true;

        protected Atom(string type)
        {
            Type = type;
            Id = GenerateId();
            Name = type;
            _allById[Id] = new WeakReference<Atom>(this);
        }

        /// <summary>全局 Id → 实例 弱引用注册表（供 Atom.TryGetById 按 Id 定位任意部件；实例被移除后 GC，弱引用自动失效）。</summary>
        private static readonly Dictionary<string, WeakReference<Atom>> _allById = new();
        /// <summary>按 8 位 Id 查找任意部件实例（含非组件原子）。</summary>
        public static bool TryGetById(string id, out Atom atom)
        {
            atom = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_allById.TryGetValue(id, out var wr) && wr.TryGetTarget(out var a)) { atom = a; return true; }
            return false;
        }

        /// <summary>生成 8 字符唯一标识：base62(Unix 毫秒) 零填充。62^8≈2.18e14，可撑约 6900 年；
        /// 同毫秒顺序 +1 保证页内唯一。加载持久化时由 ReadCommonProps 用 _id 覆盖。</summary>
        private static readonly string _b62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static long _lastIdTick;
        private static readonly object _idLock = new();
        private static string GenerateId()
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (_idLock)
            {
                if (ms <= _lastIdTick) ms = _lastIdTick + 1;   // 同毫秒：顺序 +1，避免碰撞
                _lastIdTick = ms;
            }
            var sb = new System.Text.StringBuilder(8);
            long v = ms;
            for (int i = 0; i < 8; i++) { sb.Insert(0, _b62[(int)(v % 62)]); v /= 62; }
            return sb.ToString();
        }

        // render 后挂到 Canvas 的根 Grid（供 Popup 定位用）
        public UIElement RootElement => _root;

        /// <summary>返回渲染元素在指定祖先坐标系中的**实际**包围盒（含 AutoSize 容器经 WPF 测量的真实尺寸）。
        /// 命中检测须用此实际框，而非可能陈旧/为 0 的 Bounds（容器 Bounds.W/H 不可靠）。
        /// 尚未完成首次布局（ActualWidth/Height≤0）时返回 Rect.Empty，调用方回退到 Bounds。</summary>
        public Rect GetRenderBounds(Visual ancestor)
        {
            if (_root is not FrameworkElement fe || fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
                return Rect.Empty;
            try
            {
                var t = fe.TransformToVisual(ancestor);
                return t.TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
            }
            catch { return Rect.Empty; }
        }

        /// <summary>缩放最小尺寸（= 最小网格档），resize 不可更小。</summary>
        protected const double MinSize = 20;

        /// <summary>内容尺寸随 Bounds 变化（resize 后调用）；默认无操作，显式尺寸的内容（图形/进度条）重写。</summary>
        protected virtual void SyncSize() { }

        /// <summary>
        /// 按 WidthProp/HeightProp 解析实际尺寸，写回 Bounds 并同步渲染层（外层 wrapper / 内部 grid / 内容 SyncSize）。
        /// 仅在数值确实变化时才重排，避免每帧无意义刷新；WidthProp/HeightProp 为 null 时沿用当前 Bounds（无操作）。
        /// 解析支持公式/变量（经 Ctx）；Ctx 为空时退化为 Materialize 文本。
        /// </summary>
        protected void ApplySize()
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (WidthProp != null && TryParseSize(WidthProp, out var wv)) w = wv;
            if (HeightProp != null && TryParseSize(HeightProp, out var hv)) h = hv;
            if (Math.Abs(w - Bounds.Width) < 0.5 && Math.Abs(h - Bounds.Height) < 0.5) return;
            Bounds = new Rect(Bounds.X, Bounds.Y, w, h);
            if (RenderMode == RenderModeKind.Standalone && _root != null)
            {
                if (_root is FrameworkElement feR) { feR.Width = w; feR.Height = h; }
                if (_rootGrid != null) { _rootGrid.Width = w; _rootGrid.Height = h; }
            }
            SyncSize();
        }

        private bool TryParseSize(PropertyValue p, out double v)
        {
            v = 0;
            if (!double.TryParse(Txt(p, Ctx), out var d)) return false;
            if (d < 1 || d > 8192) return false;
            v = d;
            return true;
        }

        /// <summary>部件级菜单的可编辑字段（基类含通用 透明度/旋转/锚点/偏移；子类按类型追加）。</summary>
        public virtual List<EditField> EditFields()
        {
            var l = new List<EditField>
            {
                new EditField { Key = "anchor",  Label = Loc.T("atom.label.anchor"),  Kind = EditKind.Anchor, Tab = "layout" },
                new EditField { Key = "offsetX", Label = Loc.T("atom.label.offsetX"), Kind = EditKind.Number, Tab = "layout", Min = -9999, Max = 9999 },
                new EditField { Key = "offsetY", Label = Loc.T("atom.label.offsetY"), Kind = EditKind.Number, Tab = "layout", Min = -9999, Max = 9999 },
                new EditField { Key = "opacity",  Label = Loc.T("atom.label.opacity"),  Kind = EditKind.Slider, Tab = "layout", Min = 0, Max = 1 },
                new EditField { Key = "rotation", Label = Loc.T("atom.label.rotation"), Kind = EditKind.Slider, Tab = "layout", Min = -180, Max = 180 },
                new EditField { Key = "animEnter", Label = Loc.T("atom.label.animEnter"), Kind = EditKind.Choice, Tab = "animation", Choices = new[] { "None", "Fade", "Slide", "Zoom", "Drop" } },
                new EditField { Key = "animLoop",  Label = Loc.T("atom.label.animLoop"),  Kind = EditKind.Choice, Tab = "animation", Choices = new[] { "None", "Pulse", "Rotate", "Blink", "Float", "Bounce" } },
                new EditField { Key = "animEnterDur", Label = Loc.T("atom.label.animEnterDur"), Kind = EditKind.Number, Tab = "animation", Min = 0, Max = 10000 },
                new EditField { Key = "animLoopDur",  Label = Loc.T("atom.label.animLoopDur"),  Kind = EditKind.Number, Tab = "animation", Min = 100, Max = 60000 },
                new EditField { Key = "animWhen", Label = Loc.T("atom.label.animWhen"), Kind = EditKind.Text, Tab = "animation", Hint = Loc.T("atom.hint.animWhen") },
                // 交互式进度动画
                new EditField { Key = "progressTrigger", Label = Loc.T("atom.label.progressTrigger"), Kind = EditKind.Choice, Tab = "animation", Choices = new[] { "None", "Timer", "Formula", "Touch" } },
                new EditField { Key = "progressFormula", Label = Loc.T("atom.label.progressFormula"), Kind = EditKind.Text, Tab = "animation", ShowIfKey = "progressTrigger", ShowIfValues = new[] { "Formula" } },
                new EditField { Key = "progressDur", Label = Loc.T("atom.label.progressDur"), Kind = EditKind.Number, Tab = "animation", Min = 100, Max = 60000, ShowIfKey = "progressTrigger", ShowIfValues = new[] { "Timer" } },
                new EditField { Key = "progressEasing", Label = Loc.T("atom.label.progressEasing"), Kind = EditKind.Choice, Tab = "animation", Choices = new[] { "linear", "easeIn", "easeOut", "easeInOut", "bounce", "overshoot" } },
                new EditField { Key = "progressFadeFrom", Label = Loc.T("atom.label.progressFadeFrom"), Kind = EditKind.Number, Tab = "animation", Min = 0, Max = 1 },
                new EditField { Key = "progressFadeTo", Label = Loc.T("atom.label.progressFadeTo"), Kind = EditKind.Number, Tab = "animation", Min = 0, Max = 1 },
                new EditField { Key = "progressTx", Label = Loc.T("atom.label.progressTx"), Kind = EditKind.Number, Tab = "animation", Min = -2000, Max = 2000 },
                new EditField { Key = "progressTy", Label = Loc.T("atom.label.progressTy"), Kind = EditKind.Number, Tab = "animation", Min = -2000, Max = 2000 },
                new EditField { Key = "progressRot", Label = Loc.T("atom.label.progressRot"), Kind = EditKind.Number, Tab = "animation", Min = -360, Max = 360 },
                new EditField { Key = "progressScale", Label = Loc.T("atom.label.progressScale"), Kind = EditKind.Number, Tab = "animation", Min = 0, Max = 10 },
                // 图层 Tab（v1.2 渲染增强预留，当前占位）
                new EditField { Key = "blend", Label = Loc.T("atom.label.blend"), Kind = EditKind.Choice, Tab = "layer", Choices = new[] { "Normal", "Multiply", "Screen", "Overlay", "Darken", "Lighten", "Difference" } },
                new EditField { Key = "blur",  Label = Loc.T("atom.label.blur"),  Kind = EditKind.Slider, Tab = "layer", Min = 0, Max = 40 },
            };
            if (ShowSizeFields)
            {
                l.Add(new EditField { Key = "width",  Label = Loc.T("atom.label.width"),  Kind = EditKind.Number, Tab = "layout", Min = 1, Max = 4096 });
                l.Add(new EditField { Key = "height", Label = Loc.T("atom.label.height"), Kind = EditKind.Number, Tab = "layout", Min = 1, Max = 4096 });
            }
            return l;
        }

        /// <summary>
        /// 属性编辑器标签页清单（有序）。基类默认返回 内容/样式/布局/动画 + （可选）交互/触发器。
        /// 子类可重写以增删/重排标签页（例如 Container 不显示触发器；Component 追加「变量」页）。
        /// 字段通过 EditField.Tab 归属到对应 Key；交互/触发器为固定特殊页（由编辑器提供构建器）。
        /// </summary>
        public virtual List<TabSpec> EditTabs()
        {
            var list = new List<TabSpec>
            {
                new TabSpec { Key = "content", LocKey = "prop.tab.content" },
                new TabSpec { Key = "style",   LocKey = "prop.tab.paint" },
                new TabSpec { Key = "layer",   LocKey = "prop.tab.layer" },
                new TabSpec { Key = "layout",  LocKey = "prop.tab.position" },
                new TabSpec { Key = "animation", LocKey = "prop.tab.animation" },
            };
            if (SupportsInteraction) list.Add(new TabSpec { Key = "interaction", LocKey = "prop.tab.touch" });
            if (SupportsFlow) list.Add(new TabSpec { Key = "flow", LocKey = "prop.tab.flow" });
            return list;
        }

        /// <summary>是否显示「交互」标签页（点击动作）。默认 true；纯展示原子可重写返回 false 关闭。</summary>
        public virtual bool SupportsInteraction => true;
        /// <summary>是否显示「流程」标签页（条件→动作序列）。默认 true；不需要的原子可重写返回 false 关闭。</summary>
        public virtual bool SupportsFlow => true;

        public abstract UIElement Render();
        public virtual void Update() { ApplySize(); }

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

        /// <summary>完整交互层：移动 Thumb + 8 缩放手柄。仅供 Standalone 模式使用。</summary>
        private Grid FullDraggable(UIElement content)
        {
            var grid = new Grid
            {
                IsHitTestVisible = true,
                Background = Brushes.Transparent
            };
            if (!AutoSize) { grid.Width = Bounds.Width; grid.Height = Bounds.Height; }
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
            if (OffsetXProp.Mode != PropMode.Static || OffsetYProp.Mode != PropMode.Static)
                move.ToolTip = "偏移已绑定公式/变量，清除绑定后方可拖动";
            // 挂部件级右键菜单（工厂可能尚未注入，Render 时已注入）
            if (ContextMenuFactory != null) grid.ContextMenu = ContextMenuFactory.Invoke(this);
            move.DragDelta += (s, e) =>
            {
                if (!EditMode) return;
                // 偏移被公式/变量绑定时禁用拖拽：保留绑定，拖拽不改变位置（清除绑定后方可拖动）。
                if (OffsetXProp.Mode != PropMode.Static || OffsetYProp.Mode != PropMode.Static) return;
                var rawX = Bounds.X + e.HorizontalChange;
                var rawY = Bounds.Y + e.VerticalChange;
                var pt = Coord.Snap(new Point(rawX, rawY));
                Bounds = new Rect(pt.X, pt.Y, Bounds.Width, Bounds.Height);
                SyncPosition();   // 容器(CenterAnchored)按中心反推左上；非容器等价于 SetLeft/Top
            };
            move.DragCompleted += (s, e) =>
            {
                // 迷你拖动（<3px）= 点击 → 选中；否则 = 真实拖拽
                double dist = Math.Sqrt(e.HorizontalChange * e.HorizontalChange + e.VerticalChange * e.VerticalChange);
                if (dist < 3 && EditMode)
                {
                    // 点击穿透：仅当点击到可见内容时才选中；空白/透明区域不选中（穿透到下层）
                    if (HitTestVisibleContent(_rootGrid, Mouse.GetPosition(_rootGrid), this is ContainerAtom))
                        FireSelected();
                }
                else if (!(OffsetXProp.Mode != PropMode.Static || OffsetYProp.Mode != PropMode.Static))
                {
                    WriteBackOffsetFromBounds();
                    OnChanged?.Invoke();
                }
            };

            // P6-03: 移除缩放手柄，尺寸编辑统一在属性编辑器内完成
            _rootGrid = grid;
            ApplyEditModeTo(grid);

            // 选中 Border 包裹
            _selectionBorder = new Border { Child = grid, SnapsToDevicePixels = true };
            var wrapper = new Grid { Background = Brushes.Transparent };
            if (!AutoSize) { wrapper.Width = Bounds.Width; wrapper.Height = Bounds.Height; }
            wrapper.Children.Add(_selectionBorder);
            return wrapper;
        }

        /// <summary>点击穿透检测：判断坐标是否落在可见内容上（排除 Grid/Thumb/Border 等容器）。
        /// 容器原子（ContainerAtom）直接返回 true——整个容器区域可点击选中。</summary>
        private static bool HitTestVisibleContent(UIElement root, Point pt, bool isContainer = false)
        {
            if (root == null) return true;
            if (isContainer) return true; // 容器整片区域都可点击选中
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
            grid.Cursor = !EditMode && hasAction ? Cursors.Hand : null;
            // P6-03: 已移除缩放手柄，不再需要调用 SetHandlesVisible
            grid.ContextMenu = ContextMenuFactory?.Invoke(this);

            // 移动手柄：编辑态用于拖拽；桌面态应交还点击给内容（不然覆盖整层的 Thumb 会吞掉动作）
            if (_moveThumb != null)
            {
                _moveThumb.IsHitTestVisible = EditMode;
                _moveThumb.Cursor = EditMode ? Cursors.SizeAll : null;
            }

            // 仅桌面模式 + 有动作时挂点击；编辑模式不挂（点击用于拖拽/选中）。
            grid.MouseLeftButtonUp -= OnClickAction;
            if (!EditMode && hasAction)
            {
                grid.MouseLeftButtonUp += OnClickAction;
            }
            ApplyCommon();
        }

        private void OnClickAction(object sender, MouseButtonEventArgs e)
        {
            if (EditMode) return;
            if (ClickAction == null || ClickAction.Kind == ActionKind.None) return;
            e.Handled = true;
            _ = ActionRunner.RunAsync(ClickAction, this);
        }

        /// <summary>切换模式后刷新本原子交互态（由宿主遍历调用）。</summary>
        public void ApplyEditMode() => ApplyEditModeTo(_root as Grid);

        /// <summary>
        /// 流程系统（P5）：每个评估周期检查本原子所有流程，条件成立即自动触发对应动作序列（无需点击）。
        /// 仅在桌面模式运行（编辑模式下不触发，避免误触把用户拽出编辑态）。
        /// 由 DirtyScheduler.Tick 对全部原子（含容器子原子）调用。
        /// </summary>
        public void EvaluateFlows()
        {
            if (EditMode) return;
            if (Ctx == null || Flows == null || Flows.Count == 0) return;
            foreach (var t in Flows)
            {
                try
                {
                    if (t.ShouldFire(Ctx))
                        _ = ActionRunner.RunAllAsync(t.Actions, this);
                }
                catch (Exception ex)
                {
                    Logger.Log("EvaluateFlows failed: " + (t.Condition ?? "") + " -> " + ex.Message);
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
            _root.Opacity = op;
            UpdateTransform();
        }

        /// <summary>
        /// 组合渲染变换：仅旋转（属性）。用一个 TransformGroup 承载，避免与动画层冲突。
        /// </summary>
        private void UpdateTransform()
        {
            if (_root is not FrameworkElement fe) return;
            // 容器(auto-size) 用实际测量尺寸作旋转中心；非容器用 Bounds 尺寸
            double w = (AutoSize && fe.ActualWidth > 0) ? fe.ActualWidth : Bounds.Width;
            double h = (AutoSize && fe.ActualHeight > 0) ? fe.ActualHeight : Bounds.Height;
            var tg = new TransformGroup();
            if (double.TryParse(Txt(RotationProp, Ctx), out var r) && r != 0)
                tg.Children.Add(new RotateTransform(r, w / 2, h / 2));
            fe.RenderTransform = tg;
        }

        /// <summary>
        /// 动画系统 v1（P5）：进场 + 循环。作用于内容内层 _animHost（与 _root 的交互/旋转变换分层，互不冲突）。
        /// 在 MakeDraggable 末尾调用；也由 DirtyScheduler 每拍调用（仅当配置了 animWhen 条件，见 TickAnimation）。
        /// 无条件(animWhen 留空)：每次 Render 重播进场 + 循环（原行为）。
        /// 有条件：条件成立(上升沿)才播放进场→循环；条件为假则停止并复位到中性姿态。
        /// </summary>
        private void PlayAnimations()
        {
            if (_animHost is not FrameworkElement host) return;
            var when = (AnimWhenProp?.Materialize() ?? "").Trim();
            if (string.IsNullOrEmpty(when))
            {
                // 无条件：原行为，每次 Render 重播进场 + 循环
                StartAnimationSequence(host);
                _animActive = true;
                return;
            }
            // 有条件：按条件起停（用 _animActive 做边沿检测，避免每拍重置循环）
            bool cond = EvalAnimWhen(when);
            if (cond && !_animActive) { StartAnimationSequence(host); _animActive = true; }
            else if (!cond && _animActive) { StopAnimation(host); _animActive = false; }
            // cond&&active：保持循环；!cond&&!active：保持静止
        }

        /// <summary>每拍重估动画触发条件（由 DirtyScheduler 调用）。仅当配置了条件时才干预起停，
        /// 否则由 Render 的自动播放负责；内部 _animActive 守卫保证循环不被重置。</summary>
        public void TickAnimation()
        {
            if (_animHost == null) return;
            var when = (AnimWhenProp?.Materialize() ?? "").Trim();
            if (string.IsNullOrEmpty(when)) return;
            PlayAnimations();
        }

        /// <summary>每拍执行进度驱动动画。</summary>
        public void TickProgressAnimation()
        {
            if (_animHost is not FrameworkElement host) return;
            var trigger = (ProgressTriggerProp?.Materialize() ?? "None").Trim();
            if (trigger == "None") return;

            double progress = 0;
            switch (trigger)
            {
                case "Timer":
                    if (!double.TryParse(ProgressDurProp?.Materialize() ?? "1000", out var dur)) dur = 1000;
                    _progressAnimElapsed += 16; // approx 60fps tick
                    if (_progressAnimElapsed >= dur)
                    {
                        progress = 1;
                        _progressAnimElapsed = 0;
                    }
                    else progress = _progressAnimElapsed / dur;
                    break;

                case "Formula":
                    var formula = (ProgressFormulaProp?.Materialize() ?? "").Trim();
                    if (!string.IsNullOrEmpty(formula) && Ctx != null)
                    {
                        try
                        {
                            var raw = formula.StartsWith("$") && formula.EndsWith("$") && formula.Length >= 2
                                ? formula.Substring(1, formula.Length - 2) : formula;
                            progress = Math.Clamp(Ctx.Eval(raw).AsNum() / 100.0, 0, 1);
                        }
                        catch { progress = 0; }
                    }
                    break;

                case "Touch":
                    if (_progressAnimTouchPending)
                    {
                        _progressAnimTouchPending = false;
                        _progressAnimElapsed = 0;
                        _progressAnimRunning = true;
                    }
                    if (_progressAnimRunning)
                    {
                        if (!double.TryParse(ProgressDurProp?.Materialize() ?? "1000", out var td)) td = 1000;
                        _progressAnimElapsed += 16;
                        progress = Math.Min(_progressAnimElapsed / td, 1);
                        if (progress >= 1) _progressAnimRunning = false;
                    }
                    break;
            }

            ApplyProgressAnim(host, progress);
        }

        private void ApplyProgressAnim(FrameworkElement host, double progress)
        {
            if (host == null) return;
            var easing = (ProgressEasingProp?.Materialize() ?? "linear").Trim();
            double p = ApplyEasing(progress, easing);

            var (t, s, r) = EnsureTransforms(host);

            // Fade
            if (double.TryParse(ProgressFadeFromProp?.Materialize() ?? "-1", out var ff) && ff >= 0
                && double.TryParse(ProgressFadeToProp?.Materialize() ?? "-1", out var ft) && ft >= 0)
                host.Opacity = Lerp(ff, ft, p);

            // Translate
            if (double.TryParse(ProgressTxProp?.Materialize() ?? "0", out var tx))
                t.X = Lerp(0, tx, p);
            if (double.TryParse(ProgressTyProp?.Materialize() ?? "0", out var ty))
                t.Y = Lerp(0, ty, p);

            // Rotate
            if (double.TryParse(ProgressRotProp?.Materialize() ?? "0", out var rot))
                r.Angle = Lerp(0, rot, p);

            // Scale
            if (double.TryParse(ProgressScaleProp?.Materialize() ?? "-1", out var sc) && sc >= 0)
            {
                s.ScaleX = Lerp(1, sc, p);
                s.ScaleY = Lerp(1, sc, p);
            }
        }

        private static double ApplyEasing(double t, string easing)
        {
            return easing.ToLowerInvariant() switch
            {
                "easein" => t * t,
                "easeout" => t * (2 - t),
                "easeinout" => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t,
                "bounce" => Bounce(t),
                "overshoot" => Overshoot(t),
                _ => t
            };
        }

        private static double Bounce(double t)
        {
            if (t < 1 / 2.75) return 7.5625 * t * t;
            if (t < 2 / 2.75) { t -= 1.5 / 2.75; return 7.5625 * t * t + 0.75; }
            if (t < 2.5 / 2.75) { t -= 2.25 / 2.75; return 7.5625 * t * t + 0.9375; }
            t -= 2.625 / 2.75; return 7.5625 * t * t + 0.984375;
        }

        private static double Overshoot(double t) => t < 0.5 ? 2 * t * t * (2.5 * t - 0.5) : (1 - 2 * (1 - t) * (1 - t) * (2.5 * (1 - t) - 0.5));

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>触发进度动画（Touch 模式从 Click 动作调用）。</summary>
        public void TriggerProgressAnim()
        {
            var trigger = (ProgressTriggerProp?.Materialize() ?? "None").Trim();
            if (trigger == "Touch")
                _progressAnimTouchPending = true;
        }

        /// <summary>将混合模式和模糊效果应用到渲染后的 UIElement 上。</summary>
        public void ApplyLayerEffects(UIElement element, System.Windows.Controls.Canvas parentCanvas = null)
        {
            // 模糊：WPF BlurEffect
            var blurVal = Txt(BlurProp, Ctx);
            if (double.TryParse(blurVal, out var blurRadius) && blurRadius > 0)
                element.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = blurRadius };
            else
                element.Effect = null;

            // 混合模式：ShaderEffect 像素着色器
            var blendVal = (BlendProp?.Materialize() ?? "Normal").Trim();
            if (blendVal != "Normal" && parentCanvas != null)
            {
                var idx = BlendModeIndex(blendVal);
                if (idx > 0)
                {
                    var be = new Render.BlendEffect { Mode = idx };
                    // 捕获当前画布状态作为背景（已渲染的原子）
                    if (parentCanvas.Children.Count > 0)
                    {
                        var vb = new System.Windows.Media.VisualBrush(parentCanvas)
                        {
                            AutoLayoutContent = false,
                            ViewboxUnits = System.Windows.Media.BrushMappingMode.Absolute,
                            Viewbox = new System.Windows.Rect(0, 0, parentCanvas.ActualWidth, parentCanvas.ActualHeight)
                        };
                        be.Background = vb;
                    }
                    element.Effect = be; // 覆盖模糊 Effect（不能重叠）
                }
            }
        }

        private static int BlendModeIndex(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "multiply":   return 1;
                case "screen":     return 2;
                case "overlay":    return 3;
                case "darken":     return 4;
                case "lighten":    return 5;
                case "difference": return 6;
                default:           return 0; // Normal
            }
        }

        /// <summary>求值动画触发条件（布尔公式，支持 $公式$ / gv: / 字面量，与触发器条件同约定）。</summary>
        private bool EvalAnimWhen(string when)
        {
            if (Ctx == null) return false;
            var raw = when.Trim();
            if (raw.StartsWith("$") && raw.EndsWith("$") && raw.Length >= 2)
                raw = raw.Substring(1, raw.Length - 2);
            try { return Ctx.Eval(raw).AsBool(); }
            catch { return false; }
        }

        /// <summary>复位到中性姿态并（重）构建进场+循环动画，立即播放进场。</summary>
        private void StartAnimationSequence(FrameworkElement host)
        {
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

        /// <summary>停止所有动画并将 _animHost 复位到中性姿态（条件为假时调用）。</summary>
        private void StopAnimation(FrameworkElement host)
        {
            _enterSb?.Stop(); _loopSb?.Stop();
            host.Opacity = 1;
            var (t, s, r) = EnsureTransforms(host);
            t.X = 0; t.Y = 0; s.ScaleX = 1; s.ScaleY = 1; r.Angle = 0;
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
            d["triggers"] = new StaticValue(Flow.SerializeList(Flows));
            d["animEnter"] = AnimEnterProp;
            d["animLoop"] = AnimLoopProp;
            d["animEnterDur"] = AnimEnterDurProp;
            d["animLoopDur"] = AnimLoopDurProp;
            d["animWhen"] = AnimWhenProp;
            d["progressTrigger"] = ProgressTriggerProp;
            d["progressFormula"] = ProgressFormulaProp;
            d["progressDur"] = ProgressDurProp;
            d["progressEasing"] = ProgressEasingProp;
            d["progressFadeFrom"] = ProgressFadeFromProp;
            d["progressFadeTo"] = ProgressFadeToProp;
            d["progressTx"] = ProgressTxProp;
            d["progressTy"] = ProgressTyProp;
            d["progressRot"] = ProgressRotProp;
            d["progressScale"] = ProgressScaleProp;
            d["blend"] = BlendProp;
            d["blur"] = BlurProp;
            d["width"] = WidthProp ?? new StaticValue(Bounds.Width.ToString("0"));
            d["height"] = HeightProp ?? new StaticValue(Bounds.Height.ToString("0"));
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
            if (props.TryGetValue("triggers", out var tr)) Flows = Flow.ParseList(tr.Materialize()) ?? new List<Flow>();
            if (props.TryGetValue("animEnter", out var ae)) AnimEnterProp = ae;
            if (props.TryGetValue("animLoop", out var al)) AnimLoopProp = al;
            if (props.TryGetValue("animEnterDur", out var aed)) AnimEnterDurProp = aed;
            if (props.TryGetValue("animLoopDur", out var ald)) AnimLoopDurProp = ald;
            if (props.TryGetValue("animWhen", out var aw)) AnimWhenProp = aw;
            if (props.TryGetValue("progressTrigger", out var pt)) ProgressTriggerProp = pt;
            if (props.TryGetValue("progressFormula", out var pf)) ProgressFormulaProp = pf;
            if (props.TryGetValue("progressDur", out var pd)) ProgressDurProp = pd;
            if (props.TryGetValue("progressEasing", out var pe)) ProgressEasingProp = pe;
            if (props.TryGetValue("progressFadeFrom", out var pff)) ProgressFadeFromProp = pff;
            if (props.TryGetValue("progressFadeTo", out var pft)) ProgressFadeToProp = pft;
            if (props.TryGetValue("progressTx", out var ptx)) ProgressTxProp = ptx;
            if (props.TryGetValue("progressTy", out var pty)) ProgressTyProp = pty;
            if (props.TryGetValue("progressRot", out var pro)) ProgressRotProp = pro;
            if (props.TryGetValue("progressScale", out var psc)) ProgressScaleProp = psc;
            if (props.TryGetValue("blend", out var bl)) BlendProp = bl;
            if (props.TryGetValue("blur", out var br)) BlurProp = br;
            if (props.TryGetValue("width", out var w)) WidthProp = w;
            if (props.TryGetValue("height", out var h)) HeightProp = h;
            ApplySize();
            _allById[Id] = new WeakReference<Atom>(this);   // 加载/克隆后注册到全局 Id 表（覆盖构造时生成的临时 Id）
        }

        /// <summary>深拷贝：经注册表按 Type 建新实例，复制 Bounds + 属性三元组；Container 递归克隆子原子。</summary>
        /// <summary>
        /// 深拷贝：经注册表按 Type 建新实例，复制 Bounds + 属性三元组；Container 递归克隆子原子。
        /// 复制即重生唯一 Id（丢弃原 _id），并把内部对所有旧 Id 的 gv("id",...) 引用一致重映射为新 Id，
        /// 使克隆体（尤其组件）成为完全独立的实例。map 可在多个顶层原子间共享以支持交叉引用重映射。
        /// </summary>
        public virtual Atom Clone(Dictionary<string, string> map = null)
        {
            map ??= new Dictionary<string, string>();
            var oldId = Id;
            var a = AtomRegistry.Create(Type);
            map[oldId] = a.Id;                 // 记录 旧Id → 新Id（构造时已生成新 Id）
            a.Bounds = Bounds;
            // 丢弃原 _id，保留构造器生成的唯一 Id；否则同页两同 Id 会覆盖 ComponentAtom._registry 键
            var p = GetProps();
            p.Remove("_id");
            a.SetProps(p);
            if (a is ContainerAtom c && this is ContainerAtom src)
                foreach (var ch in src.Children) c.Children.Add(ch.Clone(map));
            return a;
        }

        /// <summary>把本原子（及容器子原子）公式里对旧 Id 的 gv("id",...) 引用重映射为新 Id；map 须在全部克隆完成后传入。</summary>
        public virtual void RemapIds(Dictionary<string, string> map)
        {
            if (map == null || map.Count == 0) return;
            var p = GetProps();
            foreach (var kv in p)
                if (kv.Value is FormulaValue fv)
                    fv.Expr = RemapFormula(fv.Expr, map);
        }

        /// <summary>仅替换公式中作为 gv 作用域参数的组件 Id（gv("OLD",...) → gv("NEW",...)），避免误伤其它文本。</summary>
        private static string RemapFormula(string expr, Dictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(expr)) return expr;
            foreach (var kv in map)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Key == kv.Value) continue;
                var pattern = @"(gv\(\s*[""'])(" + Regex.Escape(kv.Key) + @")(\s*[""'])";
                expr = Regex.Replace(expr, pattern, m => m.Groups[1].Value + kv.Value + m.Groups[3].Value);
            }
            return expr;
        }

        /// <summary>
        /// 把 _root 同步到当前 Bounds。非 CenterAnchored：直接左上角 = Bounds.X/Y；
        /// CenterAnchored（容器）：Bounds.X/Y 为「中心」，按实际尺寸反推左上角（首次测量前借 LayoutUpdated 校正一次）。
        /// </summary>
        public void SyncPosition()
        {
            if (_root == null) return;
            if (CenterAnchored) CenterPlace();
            else { Canvas.SetLeft(_root, Bounds.X); Canvas.SetTop(_root, Bounds.Y); }
        }

        private void CenterPlace()
        {
            if (_root is not FrameworkElement fe || fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
            {
                // 尚未测量出实际尺寸：订阅首次布局完成做一次性校正
                _root.LayoutUpdated -= OnLayoutUpdated;
                _root.LayoutUpdated += OnLayoutUpdated;
                return;
            }
            PlaceCenter(fe);
        }

        private void PlaceCenter(FrameworkElement fe)
        {
            Canvas.SetLeft(_root, Bounds.X - fe.ActualWidth / 2);
            Canvas.SetTop(_root, Bounds.Y - fe.ActualHeight / 2);
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            if (_root is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0)
            {
                _root.LayoutUpdated -= OnLayoutUpdated;
                PlaceCenter(fe);
            }
        }

        /// <summary>
        /// 按九宫格锚点 + XY 偏移重算实际像素位置，写入 Bounds 并同步 _root。
        /// 由宿主在 ComposeCurrentPage 时调用（传入工作区尺寸），确保窗口大小变化时原子正确重定位。
        /// </summary>
        public virtual void RecalcPosition(double areaW, double areaH)
        {
            var anchorStr = Txt(AnchorProp, Ctx).Trim();
            Enum.TryParse<NineAnchor>(anchorStr, out var anchor);
            double.TryParse(Txt(OffsetXProp, Ctx), out var ox);
            double.TryParse(Txt(OffsetYProp, Ctx), out var oy);

            // 画布基准点：锚点对应的区域角/边位置
            var basePt = Coord.ResolveAnchor(anchor, 0, 0, areaW, areaH);

            // 实际渲染尺寸（AutoSize 原子 Bounds 不可靠，借已测量值）
            double w = Bounds.Width, h = Bounds.Height;
            if (_root is FrameworkElement fe && fe.ActualWidth > 0) w = fe.ActualWidth;
            if (_root is FrameworkElement fe2 && fe2.ActualHeight > 0) h = fe2.ActualHeight;

            // 锚点决定「原子哪一角」对齐基准点：fx/fy 为该角相对原子左上角的比例
            double fx = AnchorFracX(anchor), fy = AnchorFracY(anchor);
            double left = basePt.X - fx * w + ox;
            double top = basePt.Y - fy * h + oy;

            if (CenterAnchored)
                Bounds = new Rect(left + w / 2, top + h / 2, Bounds.Width, Bounds.Height);
            else
                Bounds = new Rect(left, top, Bounds.Width, Bounds.Height);
            SyncPosition();
        }

        private static double AnchorFracX(NineAnchor a) => a switch
        {
            NineAnchor.TopLeft or NineAnchor.MiddleLeft or NineAnchor.BottomLeft => 0,
            NineAnchor.TopCenter or NineAnchor.Center or NineAnchor.BottomCenter => 0.5,
            NineAnchor.TopRight or NineAnchor.MiddleRight or NineAnchor.BottomRight => 1,
            _ => 0
        };

        private static double AnchorFracY(NineAnchor a) => a switch
        {
            NineAnchor.TopLeft or NineAnchor.TopCenter or NineAnchor.TopRight => 0,
            NineAnchor.MiddleLeft or NineAnchor.Center or NineAnchor.MiddleRight => 0.5,
            NineAnchor.BottomLeft or NineAnchor.BottomCenter or NineAnchor.BottomRight => 1,
            _ => 0
        };

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

            double w = Bounds.Width, h = Bounds.Height;
            if (_root is FrameworkElement fe && fe.ActualWidth > 0) w = fe.ActualWidth;
            if (_root is FrameworkElement fe2 && fe2.ActualHeight > 0) h = fe2.ActualHeight;

            // 原子左上角在画布中的坐标
            double left = CenterAnchored ? Bounds.X - w / 2 : Bounds.X;
            double top = CenterAnchored ? Bounds.Y - h / 2 : Bounds.Y;

            double fx = AnchorFracX(anchor), fy = AnchorFracY(anchor);
            int ox = (int)Math.Round(left - basePt.X + fx * w);
            int oy = (int)Math.Round(top - basePt.Y + fy * h);
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
