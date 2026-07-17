using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Lumen.Atoms;
using Lumen.Core;
using Lumen.Engine;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.Native;
using Lumen.Pages;
using Lumen.Persistence;
using Lumen.Presets;
using Lumen.Render;
using Lumen.Ui;
using Lumen.I18n;

namespace Lumen
{
    /// <summary>
    /// 覆盖窗口：P0 空壳 → P1 多层渲染基座 → P2 原子全集+公式 → P3 网格+页面+预设。
    /// 由 PageManager 驱动多页；共享 LayerStack 按当前页重组（壁纸/网格/画布 + 原子）。
    /// 网格四档 / 拖拽 snap / 原子整体操作 / 页面切换过渡 / 预设套用 均在此接线。
    /// 详见 docs/project/phases/P3_网格小组件页面/*.md
    /// </summary>
    public partial class LumenWindow : Window
    {
        private IntPtr _hwnd;
        private HwndSource _hwndSource;

        // 热键 id
        private const int HK_EXIT = 1, HK_NEXT = 2, HK_PREV = 3, HK_GEAR = 4,
                          HK_PRESET = 6, HK_NEWPAGE = 7, HK_TOGGLE_VIS = 8;
        private const int VK_Q = 0x51, VK_RIGHT = 0x27, VK_LEFT = 0x25,
                          VK_G = 0x47, VK_P = 0x50, VK_N = 0x4E, VK_H = 0x48;

        private readonly LayerStack _stack = new();
        private WallpaperLayer _wallpaperLayer;
        private GridLayer _gridLayer;
        private CanvasLayer _canvasLayer;
        private AtomHost _atomHost;
        private GvStore _gv;
        private EvalContext _ctx;

        /// <summary>当前唯一主窗口实例（供 ActionRunner 等静态回调拿到 host）。</summary>
        public static LumenWindow Main { get; private set; }

        /// <summary>内部暴露求值上下文给 ActionRunner（媒体控制 / 应用启动）。</summary>
        internal EvalContext Ctx => _ctx;
        private DirtyScheduler _scheduler;
        private PageManager _pages = new();
        private string _activeProfile = "默认";
        private int _presetIdx;
        /// <summary>整体预设切换游标：记录上一次套用到全部页面的预设名，供 +1/-1 循环。</summary>
        private string _lastOverallPreset;
        private System.Windows.Media.Animation.Storyboard _fadeSb;
        // 模态浮层（属性/背景/变量）以独立 Window 承载（非 Popup），避免 WPF Popup 自带
        // WS_EX_NOACTIVATE 导致内部 TextBox 永远拿不到键盘焦点（输入框无法输入）。
        // 打开期间临时移除覆盖层 WS_EX_NOACTIVATE，关闭恢复（引用计数，支持嵌套）。
        // 原因：覆盖层为 WS_EX_NOACTIVATE，Popup 打开要接收键盘焦点时激活被拒，可能导致 WPF 内部重入/原生崩溃（卡顿→闪退）。
        private int _modalRef = 0;

        // 双模式：默认桌面模式（静态展示）；编辑模式开启全部交互（拖拽/缩放/右键编辑/添加原子等）
        private bool _editMode = false;

        // P6-03: 层管理窗口 + 属性编辑窗口（编辑模式弹出，退出模式自动关闭）
        private TreeWindow _treeWindow;
        private PropWindow _propWindow;
        private Window _gvWindow;
        private Atom _selectedAtom;
        private bool _suppressTreeSync;

        public LumenWindow()
        {
            InitializeComponent();
            Main = this;
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            Loc.LangChanged += OnLangChanged;
            Activated += (s, e) => _scheduler?.SetFocused(true);
            Deactivated += (s, e) => _scheduler?.SetFocused(false);
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            NativeWindow.AddExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource.AddHook(WndProc);
            RegisterHotKeys();
            CreateTrayIcon();
        }

        private void RegisterHotKeys()
        {
            (int mods, int vk, int id, string name)[] keys =
            {
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_Q,      HK_EXIT,    "Ctrl+Alt+Q(退出)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_RIGHT,  HK_NEXT,    "Ctrl+Alt+→(下一页)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_LEFT,   HK_PREV,    "Ctrl+Alt+←(上一页)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_G,      HK_GEAR,    "Ctrl+Alt+G(换网格档)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_P,      HK_PRESET,  "Ctrl+Alt+P(套预设)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_N,      HK_NEWPAGE, "Ctrl+Alt+N(新页)"),
                (NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, VK_H,      HK_TOGGLE_VIS, "Ctrl+Alt+H(显隐覆盖层)"),
            };
            foreach (var k in keys)
            {
                if (NativeMethods.RegisterHotKey(_hwnd, k.id, k.mods, k.vk))
                    Logger.Log($"Hotkey registered: {k.name}");
                else
                    Logger.Log($"Hotkey failed: {k.name}, err={Marshal.GetLastWin32Error()}");
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Title = Loc.T("main.title");
            ApplyFullscreenWorkArea();
            NativeWindow.InsertAboveDesktop(_hwnd);

            // 注册六类原子
            AtomRegistry.Register("Text", () => new TextAtom());
            AtomRegistry.Register("Shape", () => new ShapeAtom());
            AtomRegistry.Register("Icon", () => new IconAtom());
            AtomRegistry.Register("Image", () => new ImageAtom());
            AtomRegistry.Register("Progress", () => new ProgressAtom());
            AtomRegistry.Register("Container", () => new ContainerAtom());

            // 部件级右键菜单工厂（P3 补充）：右键任意原子弹出其专属菜单
            // 双模式：仅编辑模式返回部件菜单，桌面模式返回 null（点击落到全局菜单）。
            Atom.ContextMenuFactory = a => Atom.EditMode ? BuildAtomMenu(a) : null;

            // 层栈（壁纸/网格/画布）挂到 PageHost
            _wallpaperLayer = (WallpaperLayer)_stack.AddPreset(LayerKind.Wallpaper);
            _gridLayer = (GridLayer)_stack.AddPreset(LayerKind.Grid);
            _canvasLayer = (CanvasLayer)_stack.AddPreset(LayerKind.Canvas);
            _stack.Attach(PageHost);

            // 载入激活配置档（pages / userPresets / gv）；首次运行自动从旧 config.json 迁移
            var (loaded, active) = ConfigStore.LoadActive();
            _activeProfile = active;
            _gv = loaded.Gv;

            // 用户预设回填
            foreach (var p in loaded.UserPresets) PresetLibrary.AddUser(p);

            // 页面：有则载入，无则播种默认多页工程
            _pages.CurrentChanged += OnPageChanged;
            if (loaded.Pages.Count > 0)
            {
                foreach (var pg in loaded.Pages) _pages.AddExisting(pg);
                _pages.Select(0);
            }
            else
            {
                SeedDefaultProject();
            }

            if (_gv.All.Count == 0)
                _gv.Set("accent", new TypedValue { Type = GvType.Color, Raw = (object)0xFF00FF88u });

            _ctx = new EvalContext(_gv, new SystemDataProvider());

            _atomHost = new AtomHost((Canvas)_canvasLayer.Root);
            _atomHost.OnChanged += SaveAll;

            _scheduler = new DirtyScheduler(_atomHost.Flatten(), _gv);

            ComposeCurrentPage();
            DrawRuler();
            BuildContextMenu();
            SetEditMode(false); // 默认进入桌面模式（静态展示）
            // 点击空白区域取消选中
            PageHost.MouseDown += (s, e) => DeselectCurrentAtom();
            // 窗口大小变化时重锚所有原子（九宫格锚点体系的核心优势：自适应窗口尺寸）
            SizeChanged += (s, e) =>
            {
                if (_pages.CurrentPage != null)
                    ForEachAtomDeep(_pages.CurrentPage.AllAtoms(), a => a.RecalcPosition(Width, Height));
            };
        }

        /// <summary>首次运行：播种默认工程——「主页」(GridWorkspace+时钟小组件+散落原子) 与「画布」(CanvasFree)。</summary>
        private void SeedDefaultProject()
        {
            _pages.Add(Loc.T("main.homePage"));
            PresetLibrary.Apply(PresetLibrary.GetBuiltin("GridWorkspace"), _pages.CurrentPage);
            var home = _pages.CurrentPage;
            home.Atoms.Add(new TextAtom { TextProp = new FormulaValue("电量 $bi(level)$%"), Bounds = new Rect(120, 120, 320, 40) });
            home.Atoms.Add(new ProgressAtom { ValueProp = new FormulaValue("$bi(level)$"), Bounds = new Rect(120, 170, 240, 16) });
            home.Atoms.Add(new ShapeAtom { KindProp = new StaticValue("Rect"), RadiusProp = new StaticValue("14"), FillProp = new StaticValue("#FF4488FF"), Bounds = new Rect(120, 220, 160, 120) });
            home.Atoms.Add(new IconAtom { Bounds = new Rect(120, 360, 80, 80) });

            _pages.Add(Loc.T("main.canvasPage"));
            PresetLibrary.Apply(PresetLibrary.GetBuiltin("CanvasFree"), _pages.CurrentPage);
            _pages.Select(0);
        }

        /// <summary>按当前页重组内容：网格档位/显隐、网格线重绘、原子+小组件实例重渲染、调度器更新、指示器/HUD。</summary>
        private void ComposeCurrentPage()
        {
            var page = _pages.CurrentPage;
            if (page == null) return;

            Coord.GridSize = page.GridSize;
            Coord.SnapEnabled = page.ShowGrid; // 网格关闭=画布模式，拖拽自由移动
            Coord.AreaW = Width; Coord.AreaH = Height; // 注入工作区尺寸：拖拽松手反解偏移用
            _gridLayer.Enabled = page.ShowGrid;
            _stack.Recompose();
            // 当前页背景全透明 → 找第一页（主页）背景作底，避免 画布 页全空
            var bg = page.Background;
            if (IsTransparentBg(bg))
            {
                var home = _pages.Pages.FirstOrDefault(p => p != page && !IsTransparentBg(p.Background));
                if (home != null) bg = home.Background;
            }
            _wallpaperLayer.SetBackground(bg, _ctx);
            _gridLayer.Draw(page.GridSize, Width, Height);

            _atomHost.Compose(page.AllAtoms().ToList(), _ctx);
            // 按锚点+偏移重算所有原子位置（窗口尺寸变化时自动重定位）
            ForEachAtomDeep(page.AllAtoms(), a => a.RecalcPosition(Width, Height));

            _scheduler.SetAtoms(_atomHost.Flatten());
            BuildIndicator();
            UpdateHud();
            WireAtomSelection(page);
        }

        /// <summary>为页面所有原子订阅选中事件（每次重组刷新）。</summary>
        private void WireAtomSelection(Lumen.Pages.Page page)
        {
            foreach (var a in page.AllAtoms())
            {
                a.OnSelected -= OnAtomSelected;
                a.OnSelected += OnAtomSelected;
            }
        }

        private void OnAtomSelected(Atom atom)
        {
            // 取消旧选中
            if (_selectedAtom != null && _selectedAtom != atom)
                _selectedAtom.Deselect();
            // 选中新原子
            atom.Select();
            _selectedAtom = atom;
            // 同步到 TreeWindow 和 PropWindow（禁止树回环）
            _suppressTreeSync = true;
            if (_treeWindow != null && _treeWindow.IsVisible)
                _treeWindow.SelectAtom(atom);
            _suppressTreeSync = false;
            if (_propWindow != null && _propWindow.IsVisible)
                _propWindow.LoadAtom(atom);
        }

        private void DeselectCurrentAtom()
        {
            if (_selectedAtom != null)
            {
                _selectedAtom.Deselect();
                _selectedAtom = null;
            }
            if (_treeWindow != null && _treeWindow.IsVisible)
                _treeWindow.SelectAtom(null);
            if (_propWindow != null && _propWindow.IsVisible)
                _propWindow.LoadAtom(null);
        }

        /// <summary>
        /// 切页：立即重组当前页（状态永远正确，绝不依赖动画完成），随后做一段纯装饰淡入。
        /// 旧实现用 _switching 状态位 + 异步双段动画，一旦动画 Completed 未触发即永久卡死、
        /// 再也无法切回——本次改为同步重组 + 装饰淡入，移除一切会卡死的状态位。
        /// </summary>
        private void OnPageChanged(int _)
        {
            // 1) 立即重组：即便后续动画异常，页面也已正确切换。
            try { ComposeCurrentPage(); }
            catch (Exception ex) { Logger.Log("ComposeCurrentPage failed: " + ex); }
            // 部件树同步当前页
            try { SyncTreeWindow(); }
            catch (Exception ex) { Logger.Log("SyncTreeWindow failed: " + ex); }
            // 2) 装饰性淡入；任何异常都保证 PageHost 回到可见。
            //    保留强引用 _fadeSb，防止局部 Storyboard 被 GC 导致淡入中断、页面卡在透明。
            try
            {
                PageHost.Opacity = 0;
                _fadeSb = new Storyboard();
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                Storyboard.SetTarget(fade, PageHost);
                Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
                _fadeSb.Completed += (s, e) => PageHost.Opacity = 1;
                _fadeSb.Begin();
            }
            catch { PageHost.Opacity = 1; }
        }

        // ---------- 页面指示器 ----------
        private void BuildIndicator()
        {
            PageIndicator.Children.Clear();
            int i = 0;
            foreach (var pg in _pages.Pages)
            {
                int idx = i++;
                var dot = new Ellipse
                {
                    Width = 12, Height = 12, Margin = new Thickness(5, 0, 5, 0),
                    Fill = idx == _pages.Current
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
                        : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    Cursor = Cursors.Hand, IsHitTestVisible = true
                };
                dot.MouseLeftButtonDown += (s, e) => { e.Handled = true; _pages.SwitchTo(idx); };
                PageIndicator.Children.Add(dot);
            }
        }

        private void UpdateHud()
        {
            var page = _pages.CurrentPage;
            if (page == null) return;
            StatusHud.Text = Loc.T("main.status", page.Name, page.GridSize, page.AllAtoms().Count());
        }

        // ---------- 操作：添加原子 ----------
        private static readonly (string Type, string Label)[] AtomTypes =
        {
            ("Text", "文本"), ("Shape", "形状"), ("Icon", "图标"),
            ("Image", "图片"), ("Progress", "进度条"), ("Container", "容器")
        };

        /// <summary>新增原子到当前页：创建 → 默认尺寸居中吸附 → 加入页 → 重组 → 保存 → 立即打开属性编辑器便于配置。</summary>
        private void AddAtom(string type)
        {
            var page = _pages.CurrentPage;
            if (page == null) return;
            var atom = AtomRegistry.Create(type);
            var d = DefaultBounds(type);
            atom.Bounds = d; // 尺寸先写入（RecalcPosition 只改位置不改尺寸）
            // 用锚点+偏移定位到居中：默认锚点 TopLeft，偏移=居中位置
            double ox = (Width - d.Width) / 2;
            double oy = (Height - d.Height) / 2;
            atom.OffsetXProp = new StaticValue(ox.ToString("0"));
            atom.OffsetYProp = new StaticValue(oy.ToString("0"));
            page.Atoms.Add(atom);
            ComposeCurrentPage();
            SaveAll();
            EditAtom(atom);
        }

        private static Rect DefaultBounds(string type) => type switch
        {
            "Text" => new Rect(0, 0, 200, 40),
            "Shape" => new Rect(0, 0, 160, 120),
            "Icon" => new Rect(0, 0, 80, 80),
            "Image" => new Rect(0, 0, 200, 150),
            "Progress" => new Rect(0, 0, 240, 16),
            "Container" => new Rect(0, 0, 200, 200),
            _ => new Rect(0, 0, 160, 120)
        };

        private void CycleGridGear()
        {
            var page = _pages.CurrentPage;
            if (page == null) return;
            int idx = GridModel.NearestGear(page.GridSize);
            idx = (idx + 1) % GridModel.PRESETS.Length;
            page.GridSize = GridModel.PRESETS[idx];
            ComposeCurrentPage();
            SaveAll();
        }

        private void CyclePreset()
        {
            var page = _pages.CurrentPage;
            if (page == null) return;
            var names = PresetLibrary.Builtins.Select(p => p.Name).ToArray();
            if (names.Length == 0) return;
            _presetIdx = (_presetIdx + 1) % names.Length;
            PresetLibrary.Apply(PresetLibrary.GetBuiltin(names[_presetIdx]), page);
            ComposeCurrentPage();
            SaveAll();
        }

        // ---------- 全局右键菜单（P3 补充） ----------
        // 覆盖层默认吸收输入；右键任意处弹出统一操作菜单，鼠标即可完成全部常用操作（对应现有热键）。
        private ContextMenu _menu;
        private MenuItem _mnuToggleMode, _mnuPageGridBg, _mnuPresets, _mnuGv, _mnuTree, _mnuProfile;
        private Separator _mnuSep;

        private void BuildContextMenu()
        {
            _menu = new ContextMenu();
            _menu.Opened += (s, e) => RefreshMenu();
            foreach (var it in MakeGlobalItems(out _mnuToggleMode, out _mnuPageGridBg, out _mnuProfile, out _mnuPresets, out _mnuGv, out _mnuTree, out _, out _mnuSep))
                _menu.Items.Add(it);
            RootGrid.ContextMenu = _menu;
        }

        /// <summary>生成一份全新的全局菜单项（空白区菜单与部件菜单共用同一套功能）。
        /// 每次调用都新建实例，因此可分别挂到 _menu 与部件右键菜单（WPF 的 MenuItem 不能同时属于两个父级）。</summary>
        private UIElement[] MakeGlobalItems(
            out MenuItem toggleMode, out MenuItem pageGridBg, out MenuItem profile,
            out MenuItem presets, out MenuItem gv, out MenuItem tree, out MenuItem exit, out Separator sep)
        {
            toggleMode = new MenuItem { Header = Loc.T("menu.editMode") };
            toggleMode.Click += (s, e) => SetEditMode(!_editMode);
            pageGridBg = new MenuItem { Header = Loc.T("menu.pageSettings") };
            pageGridBg.Click += (s, e) => OpenPageGridBgWindow();
            var programSettings = new MenuItem { Header = Loc.T("menu.programSettings") };
            programSettings.Click += (s, e) => ShowSettings();
            profile = new MenuItem { Header = Loc.T("menu.profile") };
            profile.Click += (s, e) => OpenProfileWindow();
            presets = new MenuItem { Header = Loc.T("menu.applyPreset") };
            gv = new MenuItem { Header = Loc.T("menu.variables") };
            gv.Click += (s, e) => ManageVariables();
            tree = new MenuItem { Header = Loc.T("menu.atomTree") };
            tree.Click += (s, e) => ShowEditorWindows();
            exit = new MenuItem { Header = Loc.T("menu.exit"), InputGestureText = "Ctrl+Alt+Q" };
            exit.Click += (s, e) => Application.Current.Shutdown();
            sep = new Separator();
            return new UIElement[] { toggleMode, pageGridBg, programSettings, profile, presets, gv, tree, sep, exit };
        }

        /// <summary>菜单每次打开时刷新：模式切换项文案 + 编辑态专属项显隐 + 动态列表。</summary>
        private void RefreshMenu()
        {
            _mnuToggleMode.Header = _editMode ? Loc.T("menu.desktopMode") : Loc.T("menu.editMode");
            var editOnly = _editMode ? Visibility.Visible : Visibility.Collapsed;
            _mnuPageGridBg.Visibility = editOnly;
            _mnuPresets.Visibility = editOnly;
            _mnuGv.Visibility = editOnly;
            _mnuTree.Visibility = editOnly;
            _mnuSep.Visibility = editOnly;

            if (!_editMode) return; // 桌面模式无需构建动态项

            // 预设：内置 + 用户自定义（标注种类）
            PopulatePresets(_mnuPresets);
        }

        /// <summary>填充「套用预设」子菜单（内置 + 用户，标注种类 + 另存场景）。空白区菜单与部件菜单共用。</summary>
        private void PopulatePresets(MenuItem presets)
        {
            presets.Items.Clear();
            foreach (var p in PresetLibrary.Builtins)
            {
                // 显示本地化预设名，但套用仍按规范 Name（SwitchPreset 动作里写死的 Day/Night 等不受影响）
                var key = "preset." + p.Name.ToLower();
                var disp = Loc.T(key);
                if (disp == key) disp = p.Name; // 无对应翻译则回退规范名
                var tag = p.Kind == PresetKind.Scene ? Loc.T("menu.presetScene") : Loc.T("menu.presetAppearance");
                var mi = new MenuItem { Header = disp + tag };
                mi.Click += (s, e) => ApplyPresetByName(p.Name);
                presets.Items.Add(mi);
            }
            foreach (var p in PresetLibrary.User)
            {
                var name = p.Name;
                var tag = p.Kind == PresetKind.Scene ? Loc.T("menu.presetScene") : Loc.T("menu.presetAppearance");
                var mi = new MenuItem { Header = name + Loc.T("menu.presetCustom") + tag };
                mi.Click += (s, e) => ApplyPresetByName(name);
                presets.Items.Add(mi);
            }
            presets.Items.Add(new Separator());
            var saveScene = new MenuItem { Header = Loc.T("menu.saveScenePreset") };
            saveScene.Click += (s, e) => SaveCurrentAsScenePreset();
            presets.Items.Add(saveScene);
        }

        internal void ToggleGridShow()
        {
            var page = _pages.CurrentPage; if (page == null) return;
            page.ShowGrid = !page.ShowGrid; ComposeCurrentPage(); SaveAll();
        }

        private void SetGridGear(double g)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            page.GridSize = g; ComposeCurrentPage(); SaveAll();
        }

        /// <summary>
        /// 套用命名预设：
        /// Appearance（外观，如 Day/Night/GridWorkspace）→ 作用于全部页面，仅换网格+背景；
        /// Scene（场景）→ 作用于当前页，整页替换（原子+网格+背景）。
        /// </summary>
        internal void ApplyPresetByName(string name)
        {
            var p = PresetLibrary.GetAny(name);
            if (p == null) return;
            if (p.Kind == PresetKind.Appearance)
            {
                foreach (var pg in _pages.Pages) PresetLibrary.Apply(p, pg);
                _lastOverallPreset = p.Name;
            }
            else
            {
                var page = _pages.CurrentPage; if (page == null) return;
                PresetLibrary.Apply(p, page);
            }
            ComposeCurrentPage(); SaveAll();
        }

        /// <summary>整体预设切换（动作 SwitchPreset 入口）：作用域随 Kind（外观=全部页，场景=当前页）；
        /// 参数为 +1 / -1 时在所有内置+用户预设间循环切换。</summary>
        public void SwitchPreset(string arg)
        {
            arg = (arg ?? "").Trim();
            var names = PresetLibrary.Builtins.Concat(PresetLibrary.User).Select(x => x.Name).ToArray();
            if (names.Length == 0) return;
            if (arg == "+1" || arg == "-1")
            {
                int idx = 0;
                if (_lastOverallPreset != null)
                    for (int i = 0; i < names.Length; i++) if (names[i] == _lastOverallPreset) { idx = i; break; }
                int dir = arg == "+1" ? 1 : -1;
                idx = (idx + dir + names.Length) % names.Length;
                arg = names[idx];
            }
            var p = PresetLibrary.GetAny(arg);
            if (p == null) return;
            if (p.Kind == PresetKind.Appearance)
            {
                foreach (var pg in _pages.Pages) PresetLibrary.Apply(p, pg);
                _lastOverallPreset = p.Name;
            }
            else
            {
                var page = _pages.CurrentPage; if (page == null) return;
                PresetLibrary.Apply(p, page);
            }
            ComposeCurrentPage(); SaveAll();
        }

        /// <summary>把当前页整页快照为场景预设并存入用户库。name 为空时弹窗取名（菜单入口）；否则直接用给定名称（页面设置窗口入口）。</summary>
        internal void SaveCurrentAsScenePreset(string name = null)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            if (string.IsNullOrWhiteSpace(name))
                name = InputBox.Show(this, Loc.T("dlg.saveScene.title"), Loc.T("dlg.saveScene.prompt"), Loc.T("dlg.saveScene.default"));
            if (string.IsNullOrWhiteSpace(name)) return;
            var p = PresetLibrary.CaptureFromPage(page, name.Trim());
            if (p == null) return;
            PresetLibrary.AddUser(p);
            SaveAll();
        }

        internal void SaveAll() => ConfigStore.Save(_gv, _pages.Pages, _activeProfile);

        /// <summary>双模式切换：desktop=静态展示（默认），edit=全部交互。同步原子交互态 + HUD/标尺显隐 + 菜单。</summary>
        private void SetEditMode(bool edit)
        {
            _editMode = edit;
            Atom.EditMode = edit;
            ForEachAtomDeep(_pages.CurrentPage?.Atoms, a => a.ApplyEditMode());
            StatusHud.Visibility = edit ? Visibility.Visible : Visibility.Collapsed;
            RulerLayer.Visibility = edit ? Visibility.Visible : Visibility.Collapsed;
            RefreshMenu();
            UpdateHud();

            if (edit)
            {
                ComposeCurrentPage();    // 确保原子最新渲染 + OnSelected 事件鲜活
                ShowEditorWindows();
            }
            else
                CloseEditorWindows();
            UpdateTrayIcon();
        }

        /// <summary>打开（或创建）TreeWindow + PropWindow，并建立联动。</summary>
        private void ShowEditorWindows()
        {
            if (_treeWindow == null || !_treeWindow.IsVisible)
            {
                _treeWindow = new TreeWindow();
                _treeWindow.LoadPage(_pages.CurrentPage, _pages);
                _treeWindow.Closed += (s, e) => ClosePropWindow();
                _treeWindow.Show();
                _treeWindow.Owner = null;
                // 定位到合适位置（覆盖层屏幕区域的右侧偏上；覆盖层无有效位置则用屏幕中间偏右）
                _treeWindow.Left = Left > 0 ? Left + Width + 10 : SystemParameters.WorkArea.Width * 0.6;
                _treeWindow.Top = Top > 0 ? Top + 40 : SystemParameters.WorkArea.Height * 0.2;
            }
            else
            {
                _treeWindow.LoadPage(_pages.CurrentPage, _pages);
                _treeWindow.Show();
            }

            if (_propWindow == null || !_propWindow.IsVisible)
            {
                _propWindow = new PropWindow();
                _propWindow.InitContext(_gv, _ctx);
                _propWindow.SetCallbacks(
                    onPreview: () => { _selectedAtom?.Update(); },  // 预览：实时刷新
                    onStructural: () => ComposeCurrentPage()  // 结构性变更：重组
                );
                _propWindow.SetOnApply(() => SaveAll());
                _propWindow.Closed += (s, e) => { _propWindow = null; };
                // 定位到树窗口右侧
                _propWindow.Show();
                _propWindow.Owner = null;
            }

            // 树选中 → 加载属性；结构变更 → 重组画面 + 保存
            _treeWindow.SelectedAtomChanged -= OnTreeSelectionChanged;
            _treeWindow.SelectedAtomChanged += OnTreeSelectionChanged;
            _treeWindow.StructureChanged -= OnTreeStructureChanged;
            _treeWindow.StructureChanged += OnTreeStructureChanged;
        }

        private void OnTreeStructureChanged()
        {
            ComposeCurrentPage();
            SaveAll();
        }

        private void SyncTreeWindow()
        {
            if (_treeWindow != null && _treeWindow.IsVisible)
                _treeWindow.LoadPage(_pages.CurrentPage, _pages);
        }

        private void OnTreeSelectionChanged(Atom atom)
        {
            if (_suppressTreeSync) return;
            if (atom != null)
            {
                // 树选中的原子也触发视觉选中
                if (_selectedAtom != null && _selectedAtom != atom)
                    _selectedAtom.Deselect();
                atom.Select();
                _selectedAtom = atom;
            }
            else
            {
                DeselectCurrentAtom();
                return;
            }
            if (_propWindow == null || !_propWindow.IsVisible) return;
            _propWindow.LoadAtom(atom);
        }

        private void CloseEditorWindows()
        {
            ClosePropWindow();
            if (_treeWindow != null) { _treeWindow.Close(); _treeWindow = null; }
        }

        private void ClosePropWindow()
        {
            if (_propWindow != null) { _propWindow.Close(); _propWindow = null; }
        }

        // ---------- 供 ActionRunner 调用的内部动作入口（P5 行为系统） ----------
        internal void RequestToggleEditMode() => SetEditMode(!_editMode);
        internal void NextPage() => _pages.Next();
        internal void PrevPage() => _pages.Prev();

        /// <summary>打开页面/网格/背景 统一设置窗口。</summary>
        private void OpenPageGridBgWindow()
        {
            var win = new PageGridBgWindow();
            win.Init(this, _pages);
            CenterWindow(win);
            win.Show();
        }
        internal void GotoPage(int idx) => _pages.SwitchTo(idx);
        private void OpenProfileWindow()
        {
            var win = new ProfileWindow();
            win.Init(this);
            CenterWindow(win);
            win.Show();
        }
        internal void AddNewPage()
        {
            if (_pages.Add(Loc.T("main.pageName", _pages.Pages.Count + 1))) { ComposeCurrentPage(); SaveAll(); }
        }
        internal void RemoveCurrentPage()
        {
            if (_pages.Pages.Count <= 1) return;
            _pages.Remove(_pages.Current);
            ComposeCurrentPage();
            SaveAll();
        }
        internal void MovePage(int delta)
        {
            int from = _pages.Current;
            int to = from + delta;
            if (to < 0 || to >= _pages.Pages.Count) return;
            _pages.Move(from, to);
            ComposeCurrentPage();
            SaveAll();
        }
        internal void RenameCurrentPage()
        {
            var page = _pages.CurrentPage;
            if (page == null) return;
            var dlg = new Window
            {
                Title = Loc.T("dlg.renamePage.title"), Width = 300, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                ShowInTaskbar = false, Topmost = true, ResizeMode = ResizeMode.NoResize
            };
            var sp = new StackPanel { Margin = new Thickness(10) };
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("dlg.renamePage.prompt"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)), Margin = new Thickness(0,0,0,6)
            });
            var tb = new TextBox
            {
                Text = page.Name, Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                MinHeight = 24
            };
            sp.Children.Add(tb);
            var btn = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
            var ok = new Button { Content = Loc.T("dlg.ok"), Width = 70, Margin = new Thickness(0,0,6,0) };
            ok.Click += (s, ev) =>
            {
                var n = tb.Text.Trim();
                if (!string.IsNullOrEmpty(n)) { page.Name = n; ComposeCurrentPage(); SaveAll(); }
                dlg.Close();
            };
            var cancel = new Button { Content = Loc.T("dlg.cancel"), Width = 70 };
            cancel.Click += (s, ev) => dlg.Close();
            btn.Children.Add(ok); btn.Children.Add(cancel);
            sp.Children.Add(btn);
            dlg.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1), Child = sp
            };
            dlg.ShowDialog();
        }

        /// <summary>递归遍历页面全部原子（含容器内子原子），对每个调用动作。</summary>
        private static void ForEachAtomDeep(IEnumerable<Atom> atoms, Action<Atom> act)
        {
            if (atoms == null) return;
            foreach (var a in atoms)
            {
                act(a);
                if (a is ContainerAtom c) ForEachAtomDeep(c.Children, act);
            }
        }

        // ---------- 部件级右键菜单（P3 补充） ----------
        // 右键任意原子 → 其专属菜单：编辑属性 / 置顶 / 置底 / 复制 / 删除。
        // 工厂在 OnLoaded 注入（Atom.ContextMenuFactory = BuildAtomMenu）。
        private ContextMenu BuildAtomMenu(Atom atom)
        {
            var m = new ContextMenu();
            // P6-03: 编辑模式下点击即选中，不再需要右键「编辑属性」
            var front = new MenuItem { Header = Loc.T("ctx.bringFront") };
            front.Click += (s, e) => BringToFront(atom);
            var back = new MenuItem { Header = Loc.T("ctx.sendBack") };
            back.Click += (s, e) => SendToBack(atom);
            var dup = new MenuItem { Header = Loc.T("ctx.duplicate") };
            dup.Click += (s, e) => DuplicateAtom(atom);
            var del = new MenuItem { Header = Loc.T("ctx.delete") };
            del.Click += (s, e) => DeleteAtom(atom);
            m.Items.Add(front);
            m.Items.Add(back);
            m.Items.Add(dup);
            m.Items.Add(del);
            m.Items.Add(new Separator());

            // 复用全局菜单功能：被全屏部件挡住、右键点不到空白区时，右键部件也能退出编辑模式 / 打开各窗口
            var global = MakeGlobalItems(out var tgl, out var pg, out var pr, out var pre, out var gv2, out var tr2, out var ex2, out var sep2);
            var editOnly = _editMode ? Visibility.Visible : Visibility.Collapsed;
            pg.Visibility = editOnly; pre.Visibility = editOnly; gv2.Visibility = editOnly; tr2.Visibility = editOnly; sep2.Visibility = editOnly;
            tgl.Header = _editMode ? Loc.T("menu.desktopMode") : Loc.T("menu.editMode");
            PopulatePresets(pre);
            foreach (var it in global) m.Items.Add(it);
            return m;
        }

        private void EnterModal()
        {
            _modalRef++;
            if (_modalRef == 1)
            {
                try { NativeWindow.RemoveExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE); }
                catch (Exception ex) { Logger.Log("EnterModal remove NOACTIVATE failed: " + ex.Message); }
            }
        }
        private void ExitModal()
        {
            _modalRef--;
            if (_modalRef <= 0)
            {
                _modalRef = 0;
                try { NativeWindow.AddExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE); }
                catch (Exception ex) { Logger.Log("ExitModal add NOACTIVATE failed: " + ex.Message); }
            }
        }

        /// <summary>右键「编辑属性」→ 在 TreeWindow 中选中该原子（若窗口已打开），或转发到 PropWindow。</summary>
        private void EditAtom(Atom atom)
        {
            // 确保编辑窗口已打开
            if (!_editMode)
            {
                SetEditMode(true);
            }
            else
            {
                // 保证窗口可见
                if (_treeWindow == null || !_treeWindow.IsVisible)
                    ShowEditorWindows();
            }

            // 在树中选中目标原子（PropWindow 自动切换）
            if (_treeWindow != null && _treeWindow.IsVisible)
            {
                _treeWindow.SelectAtom(atom);
            }
            // 后备：直接加载到 PropWindow
            else if (_propWindow != null && _propWindow.IsVisible)
            {
                _propWindow.LoadAtom(atom);
            }
        }

        /// <summary>打开全局变量 (gv) 管理器（编辑模式右键菜单「变量…」）。改动即时重算并保存。</summary>
        private void ManageVariables()
        {
            var win = CreateEditorWindow(Loc.T("dlg.variables"));
            _gvWindow = win;
            var panel = new GvManagerPanel(_gv,
                onChanged: () => { _scheduler?.MarkAllDirty(); _scheduler?.Flush(); SaveAll(); },
                onClosed: () => win.Close());
            win.Content = panel;
            CenterWindow(win);
            win.Closed += (s, e) => { _gvWindow = null; ExitModal(); };
            EnterModal();
            win.Show();
        }

        /// <summary>创建一个无边框、可透明、置顶、正常可激活的编辑器窗口（关键：不是 Popup，TextBox 可正常输入）。</summary>
        private Window CreateEditorWindow(string title)
        {
            var win = new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 280
            };
            // 打开即把焦点移到首个可聚焦控件，确保输入框立即可用
            // （这是 Popup 方案做不到的——Popup 自带 WS_EX_NOACTIVATE，内部 TextBox 永远拿不到键盘焦点）。
            win.Loaded += (s, e) =>
            {
                var scope = FocusManager.GetFocusScope(win);
                FocusManager.SetFocusedElement(scope, win);
                win.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            };
            return win;
        }

        /// <summary>将窗口居中到主工作区。</summary>
        private void CenterWindow(Window win)
        {
            win.Loaded += (s, e) =>
            {
                var wa = SystemParameters.WorkArea;
                win.Left = wa.Left + Math.Max(0, (wa.Width - win.ActualWidth) / 2);
                win.Top = wa.Top + Math.Max(0, (wa.Height - win.ActualHeight) / 2);
            };
        }

        private void BringToFront(Atom atom)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            var list = AtomTree.FindParentList(page, atom);
            if (list == null) return;
            if (list.Remove(atom)) list.Add(atom);
            ComposeCurrentPage(); SaveAll();
        }

        private void SendToBack(Atom atom)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            var list = AtomTree.FindParentList(page, atom);
            if (list == null) return;
            if (list.Remove(atom)) list.Insert(0, atom);
            ComposeCurrentPage(); SaveAll();
        }

        private void DuplicateAtom(Atom atom)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            var list = AtomTree.FindParentList(page, atom);
            if (list == null) return;
            var clone = atom.Clone();
            clone.Bounds = new Rect(atom.Bounds.X + 24, atom.Bounds.Y + 24, atom.Bounds.Width, atom.Bounds.Height);
            int idx = list.IndexOf(atom);
            list.Insert(idx + 1, clone);
            ComposeCurrentPage(); SaveAll();
        }

        private void DeleteAtom(Atom atom)
        {
            var page = _pages.CurrentPage; if (page == null) return;
            var list = AtomTree.FindParentList(page, atom);
            if (list == null) return;
            list.Remove(atom);
            ComposeCurrentPage(); SaveAll();
        }

        public void ApplyFullscreenWorkArea()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left; Top = wa.Top;
            Width = wa.Width; Height = wa.Height;
        }

        private void DrawRuler()
        {
            RulerLayer.Children.Clear();
            double w = Width, h = Height;
            for (double x = 0; x <= w; x += 50)
            {
                bool major = Math.Abs(x % 200) < 0.5;
                var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = major ? 14 : 7, Stroke = Brushes.Gray, StrokeThickness = 1 };
                RulerLayer.Children.Add(line);
                if (major)
                {
                    var tb = new TextBlock { Text = ((int)x).ToString(), Foreground = Brushes.Gray, FontSize = 10 };
                    Canvas.SetLeft(tb, x + 2); Canvas.SetTop(tb, 0);
                    RulerLayer.Children.Add(tb);
                }
            }
            for (double y = 0; y <= h; y += 50)
            {
                bool major = Math.Abs(y % 200) < 0.5;
                var line = new Line { X1 = 0, Y1 = y, X2 = major ? 14 : 7, Y2 = y, Stroke = Brushes.Gray, StrokeThickness = 1 };
                RulerLayer.Children.Add(line);
                if (major)
                {
                    var tb = new TextBlock { Text = ((int)y).ToString(), Foreground = Brushes.Gray, FontSize = 10 };
                    Canvas.SetLeft(tb, 2); Canvas.SetTop(tb, y + 2);
                    RulerLayer.Children.Add(tb);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_SETTINGCHANGE || msg == NativeMethods.WM_DISPLAYCHANGE)
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    ApplyFullscreenWorkArea();
                    NativeWindow.InsertAboveDesktop(_hwnd);
                    DrawRuler();
                    _gridLayer?.Draw(Coord.GridSize, Width, Height);
                    UpdateTrayIcon(); // 系统主题切换时同步托盘图标
                }));
                handled = false;
            }
            else if (msg == NativeMethods.WM_TRAYICON)
            {
                uint lp = (uint)lParam.ToInt32();
                if (lp == NativeMethods.WM_LBUTTONUP || lp == NativeMethods.WM_LBUTTONDBLCLK)
                    ShowSettings();
                else if (lp == NativeMethods.WM_RBUTTONUP)
                    ShowTrayMenu();
                handled = true;
            }
            else if (msg == NativeMethods.WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case HK_EXIT: Application.Current.Shutdown(); break;
                    case HK_NEXT: _pages.Next(); break;
                    case HK_PREV: _pages.Prev(); break;
                    case HK_GEAR: if (_editMode) CycleGridGear(); break;
                    case HK_PRESET: if (_editMode) CyclePreset(); break;
                    case HK_NEWPAGE:
                        if (_editMode && _pages.Add(Loc.T("main.pageName", _pages.Pages.Count + 1))) ComposeCurrentPage();
                        break;
                    case HK_TOGGLE_VIS: ToggleVisibility(); break;
                    default: return IntPtr.Zero;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        // ---- 托盘 / 显隐 / 设置（P6-02 设置面板）----
        private IntPtr _trayIcon = IntPtr.Zero;

        private void CreateTrayIcon()
        {
            try
            {
                var nid = new NativeMethods.NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = NativeMethods.TRAY_ID,
                    uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                    uCallbackMessage = NativeMethods.WM_TRAYICON,
                    hIcon = GetTrayHIcon(),
                    szTip = Loc.T("tray.tooltip")
                };
                _trayIcon = nid.hIcon;
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid);
            }
            catch (Exception ex) { Logger.Log("Tray icon create failed: " + ex.Message); }
        }

        /// <summary>语言切换时刷新长驻元素：主窗标题 + 托盘 ToolTip。</summary>
        private void OnLangChanged(object sender, EventArgs e)
        {
            Title = Loc.T("main.title");
            UpdateTrayTooltip();
            // 修掉 v1 限制：若当前激活档是内置使用手册，则按新语言重装并整体重载页面
            try { RefreshManualIfActive(); }
            catch (Exception ex) { Logger.Log("RefreshManualIfActive failed: " + ex); }
        }

        /// <summary>
        /// 语言切换时若当前激活档为内置使用手册，按新语言重装手册并整体重载页面，
        /// 使手册内容随 UI 语言即时切换（v1 仅首装定语言，现已补齐）。
        /// </summary>
        private void RefreshManualIfActive()
        {
            if (!ConfigStore.IsActiveProfileManual()) return;

            var oldName = _activeProfile;
            var newName = ConfigStore.InstallBuiltinManual(); // 按新语言覆盖写入 + 设激活
            if (string.IsNullOrEmpty(newName)) return;

            // 文档名随语言变化（使用手册.json → User Manual.json）时清理旧文件，避免残留
            if (!string.Equals(oldName, newName, StringComparison.Ordinal)
                && ConfigStore.ProfileExists(oldName))
                ConfigStore.DeleteProfile(oldName);

            CloseEditorWindows();
            if (_gvWindow != null) { _gvWindow.Close(); _gvWindow = null; }

            var loaded = ConfigStore.Load(newName);
            if (loaded != null)
            {
                _gv = loaded.Gv;
                _ctx = new EvalContext(_gv, new SystemDataProvider());
                PresetLibrary.ClearUser();
                foreach (var p in loaded.UserPresets) PresetLibrary.AddUser(p);

                _pages.Pages.Clear();
                foreach (var pg in loaded.Pages) _pages.AddExisting(pg);
                _pages.Select(0);
            }

            _activeProfile = newName;
            _lastOverallPreset = null;
            _scheduler = new DirtyScheduler(new List<Atom>(), _gv);
            ComposeCurrentPage();
            DrawRuler();
            SaveAll();
        }

        /// <summary>用当前语言的 ToolTip 更新托盘图标（NIM_MODIFY + NIF_TIP）。</summary>
        private void UpdateTrayTooltip()
        {
            if (_trayIcon == IntPtr.Zero || _hwnd == IntPtr.Zero) return;
            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = NativeMethods.TRAY_ID,
                uFlags = NativeMethods.NIF_TIP,
                szTip = Loc.T("tray.tooltip")
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
        }

        private void DestroyTrayIcon()
        {
            if (_trayIcon == IntPtr.Zero) return;
            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = NativeMethods.TRAY_ID
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);
            if (_trayIcon != IntPtr.Zero) { NativeMethods.DestroyIcon(_trayIcon); _trayIcon = IntPtr.Zero; }
        }

        // ---- 多态托盘图标：状态(显示/隐藏/编辑) × 主题(明/暗) ----
        private static bool IsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var v = key?.GetValue("SystemUsesLightTheme");
                if (v != null) return Convert.ToInt32(v) == 1;
            }
            catch { }
            return false;
        }

        /// <summary>按当前 主题×状态 选对应 .ico，载入为自持有 HICON；失败回退 exe 内建图标。</summary>
        private IntPtr GetTrayHIcon()
        {
            var theme = IsLightTheme() ? "light" : "dark";
            var state = Visibility != Visibility.Visible ? "hidden" : (_editMode ? "edit" : "visible");
            var file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", $"{theme}_{state}.ico");
            if (!System.IO.File.Exists(file)) file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "dark_visible.ico");
            var h = NativeMethods.LoadImage(IntPtr.Zero, file, NativeMethods.IMAGE_ICON, 0, 0, NativeMethods.LR_LOADFROMFILE);
            return h != IntPtr.Zero
                ? h
                : NativeMethods.ExtractAssociatedIcon(IntPtr.Zero, System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, out _);
        }

        /// <summary>实时切换托盘图标（在显隐/编辑/系统主题变化时调用）。</summary>
        private void UpdateTrayIcon()
        {
            if (_hwnd == IntPtr.Zero) return;
            var hNew = GetTrayHIcon();
            if (hNew == IntPtr.Zero) return;
            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = NativeMethods.TRAY_ID,
                uFlags = NativeMethods.NIF_ICON,
                hIcon = hNew
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
            if (_trayIcon != IntPtr.Zero) NativeMethods.DestroyIcon(_trayIcon);
            _trayIcon = hNew;
        }

        private void ShowTrayMenu()
        {
            var menu = new ContextMenu();
            var mSettings = new MenuItem { Header = Loc.T("menu.programSettings") };
            mSettings.Click += (s, e) => ShowSettings();
            var mToggle = new MenuItem { Header = Visibility == Visibility.Visible ? Loc.T("tray.hideOverlay") : Loc.T("tray.showOverlay") };
            mToggle.Click += (s, e) => ToggleVisibility();
            var mExit = new MenuItem { Header = Loc.T("menu.exit") };
            mExit.Click += (s, e) => Application.Current.Shutdown();
            menu.Items.Add(mSettings);
            menu.Items.Add(mToggle);
            menu.Items.Add(new Separator());
            menu.Items.Add(mExit);
            if (NativeMethods.GetCursorPos(out NativeMethods.POINT pt))
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                menu.HorizontalOffset = pt.X;
                menu.VerticalOffset = pt.Y;
            }
            menu.IsOpen = true;
        }

        internal void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible) Hide();
            else { Show(); NativeWindow.InsertAboveDesktop(_hwnd); Activate(); }
            UpdateTrayIcon();
        }

        internal void ShowSettings() => new SettingsWindow(this).Show();

        internal void ApplyGridGear(double g) => SetGridGear(g);

        /// <summary>判断背景是否全透明（Alpha=0 或空）。</summary>
        private static bool IsTransparentBg(BackgroundRef bg)
        {
            if (bg == null || string.IsNullOrWhiteSpace(bg.Source)) return true;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(bg.Source.Trim());
                return c.A == 0;
            }
            catch { return false; }
        }

        internal Lumen.Pages.Page CurrentPage => _pages.CurrentPage;

        internal void ApplyBackground(string kind, string source)
        {
            var bg = _pages.CurrentPage?.Background;
            if (bg == null) return;
            bg.Kind = kind; bg.Source = source;
            _wallpaperLayer.SetBackground(bg, _ctx);
            SaveAll();
        }

        internal void ExportActiveProfile(string path) => ConfigStore.ExportProfile(_activeProfile, path);

        internal void ImportProfileFile(string path)
        {
            try
            {
                var name = ConfigStore.ImportProfileFromFile(path, null);
                SwitchProfile(name);
                MessageBox.Show(this, Loc.T("msg.importDone", name), Loc.T("msg.importDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("msg.importFail") + ex.Message, Loc.T("msg.importFailTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- 配置档（Profile）管理 ----------
        internal string ActiveProfile => _activeProfile;

        internal void NewProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (ConfigStore.ProfileExists(name))
            {
                MessageBox.Show(this, Loc.T("msg.profileExists", name), Loc.T("msg.newProfileTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveAll();                 // 先存当前档
            ConfigStore.CreateProfile(name);
            SwitchProfile(name);
        }

        /// <summary>切换到指定配置档：保存当前档后整体替换 gv/页面/用户预设/上下文并重组画面。</summary>
        internal void SwitchProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == _activeProfile) return;
            SaveAll();               // 离开前先存当前档
            var loaded = ConfigStore.Load(name);
            if (loaded == null) return;

            CloseEditorWindows();
            if (_gvWindow != null) { _gvWindow.Close(); _gvWindow = null; }

            _gv = loaded.Gv;
            _ctx = new EvalContext(_gv, new SystemDataProvider());
            PresetLibrary.ClearUser();
            foreach (var p in loaded.UserPresets) PresetLibrary.AddUser(p);

            _pages.Pages.Clear();
            if (loaded.Pages.Count > 0)
            {
                foreach (var pg in loaded.Pages) _pages.AddExisting(pg);
                _pages.Select(0);
            }
            else
            {
                SeedDefaultProject();
            }

            _activeProfile = name;
            ConfigStore.SetActive(name);
            _lastOverallPreset = null;

            _scheduler = new DirtyScheduler(new List<Atom>(), _gv);
            ComposeCurrentPage();
            DrawRuler();
            SaveAll();
        }

        internal void DeleteProfile(string name)
        {
            if (name == _activeProfile)
            {
                MessageBox.Show(this, Loc.T("msg.cannotDeleteActiveProfile"), Loc.T("msg.deleteProfileTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ConfigStore.ProfileExists(name)) ConfigStore.DeleteProfile(name);
        }

        internal void RenameProfile(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return;
            ConfigStore.RenameProfile(oldName, newName);
            if (oldName == _activeProfile) _activeProfile = newName;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            Loc.LangChanged -= OnLangChanged;
            SaveAll();
            if (_hwnd != IntPtr.Zero)
            {
                foreach (var id in new[] { HK_EXIT, HK_NEXT, HK_PREV, HK_GEAR, HK_PRESET, HK_NEWPAGE, HK_TOGGLE_VIS })
                    NativeMethods.UnregisterHotKey(_hwnd, id);
                DestroyTrayIcon();
            }
            if (_hwndSource != null) _hwndSource.Dispose();
        }
    }
}
