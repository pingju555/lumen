using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Atoms;
using Lumen.Core;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;
using Lumen.Persistence;
using Page = Lumen.Pages.Page;

namespace Lumen.Ui
{
    /// <summary>编辑主控窗口：扁平 TabControl（项目 + 属性 + 网格 + 背景）。</summary>
    public partial class PropWindow : Window
    {
        // ---- 外部注入 ----
        private GvStore _gv;
        private EvalContext _ctx;
        private Action _onApply;
        private Action _externalPreview;
        private Action _externalStructural;
        public Func<Atom, bool> BeforeAtomSwitch { get; set; }

        // ---- 当前状态 ----
        private Page _page;
        private LumenWindow _lumenOwner;
        private PropertyEditorPanel _propPanel;

        // ---- 树 ----
        private readonly Stack<ContainerAtom> _navStack = new Stack<ContainerAtom>();
        private Page _loadedPage;
        private int _selectedIndex = -1;
        private Point _dragStart;
        private TreeView AtomsTree;
        private StackPanel BreadcrumbPanel;
        private Button BackBtn;

        // ---- 页面设置 ----
        private bool _loading;
        private CheckBox ShowGridCb;
        private ComboBox GridSizeCb;
        private RadioButton RbSolid, RbImage;
        private TextBox ColorTb, ImageTb;
        private Border Swatch;
        private Button PickColorBtn, BrowseBtn, ApplyBgBtn;

        public PropWindow()
        {
            InitializeComponent();
            Left = 60; Top = 80;
            CloseBtn.Click += (s, e) => Close();
            DeselectBtn.Click += (s, e) => _lumenOwner?.DeselectCurrentAtom();
            CancelBtn.Click += (s, e) => _lumenOwner?.DeselectCurrentAtom();
            ApplyBtn.Click += (s, e) => ApplyCurrent();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        public void InitContext(GvStore gv, EvalContext ctx) { _gv = gv; _ctx = ctx; }
        public void SetCallbacks(Action onPreview, Action onStructural) { _externalPreview = onPreview; _externalStructural = onStructural; }
        public void SetOnApply(Action onApply) => _onApply = onApply;
        public void SetLumenOwner(LumenWindow owner) => _lumenOwner = owner;
        public void ApplyCurrent() => _propPanel?.Apply();
        /// <summary>按标题文字切换到指定 Tab（如 "网格"、"背景"）。</summary>
        public void SelectTabByHeader(string header)
        {
            foreach (TabItem item in MainTabs.Items)
            {
                if ((item.Header as string)?.Contains(header, StringComparison.OrdinalIgnoreCase) == true)
                { MainTabs.SelectedItem = item; break; }
            }
        }

        // ========== 主入口 ==========

        public void LoadPage(Page page)
        {
            _page = page;
            bool changed = !ReferenceEquals(page, _loadedPage);
            _loadedPage = page;
            if (changed) { _navStack.Clear(); _selectedIndex = -1; }
            RebuildTabs();
        }

        /// <summary>外部选中原子（从桌面右键触发时调用）。</summary>
        public void SelectAtom(Atom atom)
        {
            if (atom == null) return;
            _selectedIndex = -1;
            var list = CurrentList();
            if (list != null && !list.Contains(atom)) { _navStack.Clear(); list = CurrentList(); }
            _selectedIndex = list?.IndexOf(atom) ?? -1;
            LoadAtom(atom);
            RebuildTabs();
        }

        // ========== Tab 构建 ==========

        private void RebuildTabs()
        {
            MainTabs.Items.Clear();

            // 1) 项目 Tab（部件树 + 数量统计）
            int total = CountItems();
            var treeHeader = total > 0 ? $"{Loc.T("propwin.tree")} ({total})" : Loc.T("propwin.tree");
            var treeTab = BuildTreeTab();
            MainTabs.Items.Add(new TabItem { Header = treeHeader, Content = treeTab });

            // 2) 属性 Tab（PropertyEditorPanel 构建）
            if (_propPanel != null)
            {
                foreach (var kv in _propPanel.TabContents)
                {
                    MainTabs.Items.Add(new TabItem
                    {
                        Header = Loc.T(kv.Value.LocKey),
                        Content = kv.Value.Content
                    });
                }
            }

            // 3) 页面管理 Tab
            MainTabs.Items.Add(new TabItem { Header = Loc.T("pagebg.tab.page"), Content = BuildPageTab() });

            // 4) 网格 Tab
            MainTabs.Items.Add(new TabItem { Header = Loc.T("pagebg.tab.grid"), Content = BuildGridTab() });

            // 5) 背景 Tab
            MainTabs.Items.Add(new TabItem { Header = Loc.T("pagebg.tab.bg"), Content = BuildBgTab() });

            // 6) 配置档 Tab
            MainTabs.Items.Add(new TabItem { Header = Loc.T("profile.title"), Content = BuildProfileTab() });

            // 7) 设置 Tab
            MainTabs.Items.Add(new TabItem { Header = Loc.T("settings.title"), Content = BuildSettingsTab() });

            if (MainTabs.Items.Count > 0) MainTabs.SelectedIndex = 0;
            RebuildTree();
        }

        private int CountItems()
        {
            if (_page == null) return 0;
            int count = 0;
            void Walk(IEnumerable<Atom> atoms) { if (atoms == null) return; foreach (var a in atoms) { count++; if (a is ContainerAtom c) Walk(c.Children); } }
            Walk(_page.Atoms);
            return count;
        }

        // ========== 树 ==========

        private UIElement BuildTreeTab()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var breadcrumbBar = new Border { Background = Theme.BgSunken, Padding = new Thickness(8, 4, 8, 4), BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(0, 0, 0, 1) };
            BreadcrumbPanel = new StackPanel { Orientation = Orientation.Horizontal };
            breadcrumbBar.Child = BreadcrumbPanel;
            Grid.SetRow(breadcrumbBar, 0);
            grid.Children.Add(breadcrumbBar);

            AtomsTree = new TreeView
            {
                Background = Theme.BgSunken, Foreground = Theme.TextSecondary, BorderThickness = new Thickness(0),
                AllowDrop = true
            };
            AtomsTree.SelectedItemChanged += Tree_SelectedChanged;
            AtomsTree.MouseDoubleClick += Tree_MouseDoubleClick;
            AtomsTree.PreviewMouseMove += Tree_PreviewMouseMove;
            AtomsTree.Drop += Tree_Drop;
            AtomsTree.DragOver += Tree_DragOver;
            Grid.SetRow(AtomsTree, 1);
            grid.Children.Add(AtomsTree);

            var toolbar = new Border { Background = Theme.BgSunken, Padding = new Thickness(6, 4, 6, 4), BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(0, 1, 0, 0) };
            var tbPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            BackBtn = MakeToolBtn("←", "tree.back", Back_Click);
            var addBtn = MakeToolBtn("＋", "tree.add", Add_Click);
            var upBtn = MakeToolBtn("↑", "tree.up", Up_Click);
            var downBtn = MakeToolBtn("↓", "tree.down", Down_Click);
            var delBtn = MakeToolBtn("✕", "tree.delete", Delete_Click);
            tbPanel.Children.Add(BackBtn); tbPanel.Children.Add(addBtn);
            tbPanel.Children.Add(upBtn); tbPanel.Children.Add(downBtn); tbPanel.Children.Add(delBtn);
            toolbar.Child = tbPanel;
            Grid.SetRow(toolbar, 2);
            grid.Children.Add(toolbar);
            return grid;
        }

        private static Button MakeToolBtn(string text, string locKey, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Width = 24, Height = 22, FontSize = 11, Padding = new Thickness(2), Margin = new Thickness(0, 0, 4, 0) };
            b.Click += click;
            b.ToolTip = Loc.T(locKey);
            return b;
        }

        private void RebuildTree()
        {
            if (AtomsTree == null) return;
            AtomsTree.Items.Clear();
            BuildBreadcrumb();
            UpdateBackBtn();
            if (_page == null) return;
            var list = CurrentList();
            for (int i = 0; i < list.Count; i++)
                AtomsTree.Items.Add(BuildNode(list[i], i == list.Count - 1));
        }

        private IList<Atom> CurrentList() => _navStack.Count == 0 ? _page?.Atoms : _navStack.Peek().Children;

        private TreeViewItem BuildNode(Atom atom, bool isLast)
        {
            var item = new TreeViewItem { Header = BuildItemHeader(atom), Tag = atom, IsExpanded = false };
            var m = new ContextMenu();
            var rename = new MenuItem { Header = Loc.T("tree.rename") };
            rename.Click += (s, e) => RenameAtom(atom);
            m.Items.Add(rename);
            var copyId = new MenuItem { Header = Loc.T("tree.copyId") };
            copyId.Click += (s, e) => Clipboard.SetText(atom.Id);
            m.Items.Add(copyId);
            item.ContextMenu = m;
            return item;
        }

        /// <summary>KLWP 风格条目：色块+名称+子描述+□+容器箭头。</summary>
        private FrameworkElement BuildItemHeader(Atom atom)
        {
            var grid = new Grid { Margin = new Thickness(4, 6, 4, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // 色块
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });  // 文本
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // □
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // 箭头

            var icon = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                Background = GetTypeIconBrush(atom), Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(icon, 0); grid.Children.Add(icon);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = atom.Name, FontSize = 12, Foreground = Theme.TextPrimary });
            sp.Children.Add(new TextBlock { Text = FormatSubLabel(atom), FontSize = 10, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(sp, 1); grid.Children.Add(sp);

            var fbox = new Border { Width = 14, Height = 14, BorderBrush = Theme.BorderSoft, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2), Background = Brushes.Transparent, Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = Loc.T("prop.formula.unbound") };
            Grid.SetColumn(fbox, 2); grid.Children.Add(fbox);

            if (atom is ContainerAtom)
            {
                var arrow = new TextBlock { Text = "▸", FontSize = 10, Foreground = Theme.TextTertiary,
                    Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(arrow, 3); grid.Children.Add(arrow);
            }

            return grid;
        }

        /// <summary>KLWP 风格子描述（参数概要）。</summary>
        private string FormatSubLabel(Atom atom)
        {
            var p = atom.GetProps();
            if (atom is ContainerAtom c)
                return $"{(atom is StackGroupAtom ? "Stack" : atom is OverlapGroupAtom ? "Overlap" : atom is SeriesGroupAtom ? "Series" : "Group")} · {c.Children.Count} items";
            // 简单取关键属性
            var parts = new List<string>();
            if (p.TryGetValue("width", out var w) && double.TryParse(PropertyValue.Serialize(w), out var wv) && wv > 0) parts.Add($"W {wv:0}");
            if (p.TryGetValue("height", out var h) && double.TryParse(PropertyValue.Serialize(h), out var hv) && hv > 0) parts.Add($"H {hv:0}");
            if (p.TryGetValue("fill", out var f)) parts.Add(PropertyValue.Serialize(f));
            if (p.TryGetValue("text", out var t)) parts.Add(PropertyValue.Serialize(t).Substring(0, Math.Min(20, PropertyValue.Serialize(t).Length)));
            return string.Join(" · ", parts);
        }

        private static Brush GetTypeIconBrush(Atom atom)
        {
            var type = atom.Type?.ToLowerInvariant() ?? "";
            if (type == "text") return new SolidColorBrush(Color.FromRgb(0x6A, 0xD1, 0x7A));   // 绿
            if (type == "shape") return new SolidColorBrush(Color.FromRgb(0x4A, 0x8F, 0xE7));  // 蓝
            if (type == "icon") return new SolidColorBrush(Color.FromRgb(0xE0, 0x78, 0x66));   // 橙
            if (type == "image") return new SolidColorBrush(Color.FromRgb(0xE0, 0xB9, 0x66));  // 黄
            if (type == "progress") return new SolidColorBrush(Color.FromRgb(0x9C, 0x6A, 0xD1));// 紫
            if (type == "stack") return new SolidColorBrush(Color.FromRgb(0x6A, 0xB7, 0xD1));  // 青
            if (type == "overlap") return new SolidColorBrush(Color.FromRgb(0xE0, 0x66, 0x9C));// 粉
            if (type == "series") return new SolidColorBrush(Color.FromRgb(0xD1, 0x9C, 0x6A)); // 棕
            return new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        }

        private static string FormatLabel(Atom atom)
        {
            if (atom is ContainerAtom c)
            {
                string kind = Loc.T("atom.type." + c.Type.ToLowerInvariant());
                return $"{c.Name} [{kind}] ({c.Children.Count})";
            }
            return atom.Name;
        }

        private void BuildBreadcrumb()
        {
            if (BreadcrumbPanel == null) return;
            BreadcrumbPanel.Children.Clear();
            
            // 页面名称（根）
            var pageName = _page?.Name ?? Loc.T("tree.none");
            var home = new TextBlock { Text = pageName, Foreground = _navStack.Count == 0 ? Brushes.White : SystemColors.GrayTextBrush, Cursor = Cursors.Hand, FontSize = 11 };
            if (_navStack.Count > 0) home.MouseLeftButtonDown += (s, e) => GoToDepth(0);
            BreadcrumbPanel.Children.Add(home);

            // 容器导航路径
            int depth = 1;
            foreach (var c in _navStack.Reverse())
            {
                BreadcrumbPanel.Children.Add(new TextBlock { Text = " › ", FontSize = 11, Foreground = SystemColors.GrayTextBrush });
                var crumb = new TextBlock { Text = c.Name, FontSize = 11, Foreground = depth == _navStack.Count ? Brushes.White : SystemColors.GrayTextBrush, Cursor = Cursors.Hand };
                int d = depth;
                if (depth < _navStack.Count) crumb.MouseLeftButtonDown += (s, e) => GoToDepth(d);
                BreadcrumbPanel.Children.Add(crumb);
                depth++;
            }

            // 当前层级的部件数
            var list = CurrentList();
            if (list != null)
            {
                BreadcrumbPanel.Children.Add(new TextBlock { Text = $"  ({list.Count})", FontSize = 10, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(8, 0, 0, 0) });
            }
        }

        private void UpdateBackBtn() { if (BackBtn != null) BackBtn.IsEnabled = _navStack.Count > 0; }
        private void GoToDepth(int depth) { while (_navStack.Count > depth) _navStack.Pop(); RebuildTree(); }

        // ---- 树事件 ----

        private void Tree_SelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is Atom atom)
            {
                var list = CurrentList();
                _selectedIndex = list?.IndexOf(atom) ?? -1;
                LoadAtom(atom);
            }
        }

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (tvi?.Tag is ContainerAtom c) { _navStack.Push(c); RebuildTree(); }
        }

        private static TreeViewItem FindTreeViewItem(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem)) source = VisualTreeHelper.GetParent(source);
            return source as TreeViewItem;
        }

        private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(AtomsTree);
            if (Math.Abs(pos.X - _dragStart.X) < 5 && Math.Abs(pos.Y - _dragStart.Y) < 5) return;
            if (AtomsTree.SelectedItem is TreeViewItem tvi && tvi.Tag is Atom atom)
                DragDrop.DoDragDrop(AtomsTree, atom, DragDropEffects.Move);
        }

        private void Tree_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(typeof(Atom)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }

        private void Tree_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Atom)) || _page == null) return;
            var dragged = (Atom)e.Data.GetData(typeof(Atom));
            var targetTvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
            var target = targetTvi?.Tag as Atom;
            var srcList = _page != null ? AtomTree.FindParentList(_page, dragged) : null;
            if (srcList == null) return;
            int srcIdx = srcList.IndexOf(dragged);
            IList<Atom> dstList = CurrentList();
            if (target is ContainerAtom tc) { tc.Children.Add(dragged); }
            else if (target != null) { int dstIdx = dstList.IndexOf(target); dstList.Insert(dstIdx >= srcIdx ? dstIdx + 1 : dstIdx, dragged); }
            else { dstList.Add(dragged); }
            if (srcList != dstList || srcIdx >= 0) { if (srcIdx >= 0) srcList.RemoveAt(srcIdx); }
            _externalStructural?.Invoke();
            RebuildTree();
        }

        // ---- 工具栏按钮 ----

        private void Back_Click(object sender, RoutedEventArgs e) => GoToDepth(_navStack.Count - 1);

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var type = AddComponentPanel.ShowPick(this);
            if (type == null) return;
            var atom = AtomRegistry.Create(type);
            if (atom == null) return;
            CurrentList()?.Add(atom);
            _externalStructural?.Invoke();
            RebuildTree();
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            var list = CurrentList(); if (list == null || _selectedIndex <= 0) return;
            var atom = list[_selectedIndex]; list.RemoveAt(_selectedIndex);
            list.Insert(_selectedIndex - 1, atom); _selectedIndex--;
            _externalStructural?.Invoke(); RebuildTree(); RestoreSelection(atom);
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            var list = CurrentList(); if (list == null || _selectedIndex < 0 || _selectedIndex >= list.Count - 1) return;
            var atom = list[_selectedIndex]; list.RemoveAt(_selectedIndex);
            list.Insert(_selectedIndex + 1, atom); _selectedIndex++;
            _externalStructural?.Invoke(); RebuildTree(); RestoreSelection(atom);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_page == null || AtomsTree == null) return;
            var sel = AtomsTree.SelectedItem;
            if (sel is TreeViewItem tvi && tvi.Tag is Atom target)
            {
                var list = AtomTree.FindParentList(_page, target);
                if (list != null) { list.Remove(target); if (_currentAtom == target) _currentAtom = null; _externalStructural?.Invoke(); RebuildTree(); }
            }
        }

        /// <summary>重建树后恢复选中指定原子。</summary>
        private void RestoreSelection(Atom atom)
        {
            if (atom == null || AtomsTree == null) return;
            foreach (TreeViewItem item in AtomsTree.Items)
            {
                if (item.Tag == atom) { item.IsSelected = true; break; }
            }
        }

        private void RenameAtom(Atom atom)
        {
            var result = InputBox.Show(this, Loc.T("tree.renameTitle"), atom.Name, atom.Name);
            if (result != null) { atom.Name = result; _externalStructural?.Invoke(); RebuildTree(); }
        }

        // ========== 属性编辑 ==========

        private Atom _currentAtom;

        public void LoadAtom(Atom atom)
        {
            _currentAtom = atom;
            if (atom == null) return;
            Title = Loc.T("propwin.editing", atom.Type);

            // 清理旧 panel 事件绑定——解除旧 panel 的闭包引用避免内存泄漏
            if (_propPanel != null)
            {
                var old = _propPanel;
                _propPanel = null;
            }

            _propPanel = new PropertyEditorPanel(atom: atom,
                onPreview: () => _externalPreview?.Invoke(),
                onStructuralChange: () => _externalStructural?.Invoke(),
                onCommit: () => { }, onCancel: () => { }, gv: _gv,
                onOpenGvManager: () => { }, ctx: _ctx);
            // 重建属性 Tab
            RebuildTabs();
        }

        // ========== 页面设置 ==========

        private UIElement BuildPageTab()
        {
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var sp = new StackPanel { Margin = new Thickness(8) };

            // 页面列表
            sp.Children.Add(new TextBlock { Text = Loc.T("pagebg.tab.page"), Margin = new Thickness(0, 0, 0, 6), Foreground = Theme.TextPrimary, FontSize = 12, FontWeight = FontWeights.SemiBold });
            var pageList = new ListBox
            {
                Height = 120, Background = Theme.BgSunken, Foreground = Theme.TextSecondary,
                BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1)
            };
            if (_page != null)
            {
                for (int i = 0; i < _lumenOwner.PageCount; i++)
                {
                    var isCurrent = i == _lumenOwner.CurrentPageIndex;
                    var item = new ListBoxItem
                    {
                        Content = _lumenOwner.GetPageName(i),
                        Tag = i,
                        IsSelected = isCurrent,
                        FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
                    };
                    pageList.Items.Add(item);
                }
            }
            pageList.SelectionChanged += (s, e) =>
            {
                if (pageList.SelectedItem is ListBoxItem lbi && lbi.Tag is int idx)
                {
                    _lumenOwner.GotoPage(idx);
                    RebuildTabs();
                }
            };
            sp.Children.Add(pageList);

            // 页面操作按钮
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            btnRow.Children.Add(MakeToolBtn("＋", "pagebg.newPage", (s, e) => { _lumenOwner.AddNewPage(); RebuildTabs(); }));
            btnRow.Children.Add(MakeToolBtn("✕", "pagebg.delete", (s, e) => { if (_lumenOwner.PageCount > 1) { _lumenOwner.RemoveCurrentPage(); RebuildTabs(); } }));
            btnRow.Children.Add(MakeToolBtn("✎", "pagebg.rename", (s, e) => { _lumenOwner.RenameCurrentPage(); RebuildTabs(); }));
            btnRow.Children.Add(MakeToolBtn("↑", "pagebg.up", (s, e) => { _lumenOwner.MovePage(-1); RebuildTabs(); }));
            btnRow.Children.Add(MakeToolBtn("↓", "pagebg.down", (s, e) => { _lumenOwner.MovePage(1); RebuildTabs(); }));
            sp.Children.Add(btnRow);

            // 场景预设
            sp.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 6) });
            sp.Children.Add(new TextBlock { Text = Loc.T("pagebg.tab.scene"), Margin = new Thickness(0, 0, 0, 4), Foreground = Theme.TextPrimary, FontSize = 12, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = Loc.T("pagebg.sceneDesc"), FontSize = 10, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });

            var sceneList = new ListBox
            {
                Height = 80, Background = Theme.BgSunken, Foreground = Theme.TextSecondary,
                BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1)
            };
            // 用 Presets.PresetLibrary.User
            var sceneType = typeof(Lumen.Presets.PresetLibrary);
            var scenesProp = sceneType.GetProperty("User", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (scenesProp?.GetValue(null) is System.Collections.Generic.IEnumerable<Lumen.Presets.Preset> presets)
            {
                foreach (var p in presets)
                {
                    var tag = p.Kind == Lumen.Presets.PresetKind.Scene ? "场景" : "外观";
                    sceneList.Items.Add(new ListBoxItem { Content = $"{p.Name} [{tag}]", Tag = p.Name });
                }
            }
            sp.Children.Add(sceneList);

            // 场景预设操作按钮
            var scRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var sceneNameTb = new TextBox { Width = 120, Background = Theme.BgSurface, Foreground = Theme.TextSecondary, BorderBrush = Theme.BorderDefault, Text = Loc.T("pagebg.sceneNameDefault") };
            scRow.Children.Add(sceneNameTb);
            var saveBtn = MakeToolBtn("💾", "pagebg.saveScene", (s, e) =>
            {
                var name = sceneNameTb.Text.Trim();
                if (!string.IsNullOrEmpty(name)) { _lumenOwner.SaveCurrentAsScenePreset(name); RebuildTabs(); }
            });
            scRow.Children.Add(saveBtn);
            var applyBtn = MakeToolBtn("▶", "pagebg.applyScene", (s, e) =>
            {
                if (sceneList.SelectedItem is ListBoxItem si && si.Tag is string sn)
                {
                    _lumenOwner.ApplyPresetByName(sn);
                    RebuildTabs();
                }
            });
            scRow.Children.Add(applyBtn);
            var delSceneBtn = MakeToolBtn("✕", "pagebg.delete", (s, e) =>
            {
                if (sceneList.SelectedItem is ListBoxItem si && si.Tag is string sn)
                {
                    Lumen.Presets.PresetLibrary.RemoveUser(sn);
                    RebuildTabs();
                }
            });
            scRow.Children.Add(delSceneBtn);
            sp.Children.Add(scRow);

            sv.Content = sp;
            return sv;
        }

        private UIElement BuildGridTab()
        {
            var sp = new StackPanel { Margin = new Thickness(8) };
            ShowGridCb = new CheckBox { Content = Loc.T("pagebg.showGrid"), Margin = new Thickness(0, 0, 0, 12), IsChecked = _page?.ShowGrid ?? false };
            ShowGridCb.Checked += GridShow_Changed;
            ShowGridCb.Unchecked += GridShow_Changed;
            sp.Children.Add(ShowGridCb);
            sp.Children.Add(new TextBlock { Text = Loc.T("pagebg.gridSpacing"), Margin = new Thickness(0, 0, 0, 4), Foreground = Theme.TextTertiary });
            GridSizeCb = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = true };
            foreach (var v in new[] { "10", "20", "30", "40", "50", "100" }) GridSizeCb.Items.Add(v);
            GridSizeCb.SelectedItem = _page?.GridSize.ToString("0") ?? "20";
            GridSizeCb.SelectionChanged += GridSize_Changed;
            sp.Children.Add(GridSizeCb);
            return sp;
        }

        private UIElement BuildBgTab()
        {
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var sp = new StackPanel { Margin = new Thickness(8) };
            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            RbSolid = new RadioButton { Content = Loc.T("settings.bg.solid"), GroupName = "Bg", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
            RbSolid.Checked += BgMode_Changed;
            RbImage = new RadioButton { Content = Loc.T("settings.bg.image"), GroupName = "Bg" };
            RbImage.Checked += BgMode_Changed;
            modeRow.Children.Add(RbSolid); modeRow.Children.Add(RbImage);
            sp.Children.Add(modeRow);

            ColorTb = new TextBox { Width = 160, Text = "#FF4488FF", Background = Theme.BgSurface, Foreground = Theme.TextSecondary, BorderBrush = Theme.BorderDefault };
            Swatch = new Border { Width = 28, Height = 24, Margin = new Thickness(8, 0, 0, 0), BorderBrush = Theme.BorderSoft, BorderThickness = new Thickness(1) };
            PickColorBtn = new Button { Content = Loc.T("pagebg.palette"), Margin = new Thickness(8, 0, 0, 0) };
            PickColorBtn.Click += PickColor_Click;
            var colorRow = new StackPanel { Orientation = Orientation.Horizontal };
            colorRow.Children.Add(ColorTb); colorRow.Children.Add(Swatch); colorRow.Children.Add(PickColorBtn);
            sp.Children.Add(colorRow);

            ImageTb = new TextBox { Width = 240, Background = Theme.BgSurface, Foreground = Theme.TextSecondary, BorderBrush = Theme.BorderDefault, Visibility = Visibility.Collapsed };
            BrowseBtn = new Button { Content = Loc.T("settings.browse"), Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
            BrowseBtn.Click += Browse_Click;
            var imgRow = new StackPanel { Orientation = Orientation.Horizontal };
            imgRow.Children.Add(ImageTb); imgRow.Children.Add(BrowseBtn);
            sp.Children.Add(imgRow);

            ApplyBgBtn = new Button { Content = Loc.T("settings.applyBg"), Width = 100, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
            ApplyBgBtn.Click += ApplyBg_Click;
            sp.Children.Add(ApplyBgBtn);
            sv.Content = sp;
            return sv;
        }

        // ========== 配置档 ==========

        private UIElement BuildProfileTab()
        {
            var sp = new StackPanel { Margin = new Thickness(8) };

            sp.Children.Add(new TextBlock { Text = Loc.T("profile.hint"), FontSize = 10, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });

            var profileList = new ListBox
            {
                Height = 120, Background = Theme.BgSunken, Foreground = Theme.TextSecondary,
                BorderBrush = Theme.BorderDefault, BorderThickness = new Thickness(1)
            };
            ReloadProfileList(profileList);
            sp.Children.Add(profileList);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            btnRow.Children.Add(MakeToolBtn("\uff0b", "profile.new", (s, e) =>
            {
                var name = InputBox.Show(this, Loc.T("profile.newTitle"), Loc.T("profile.namePrompt"), Loc.T("profile.defaultName"));
                if (!string.IsNullOrWhiteSpace(name)) { _lumenOwner.NewProfile(name.Trim()); ReloadProfileList(profileList); }
            }));
            btnRow.Children.Add(MakeToolBtn("\u25b6", "profile.switch", (s, e) =>
            {
                if (profileList.SelectedItem is ListBoxItem lbi && lbi.Tag is string sn)
                {
                    _lumenOwner.SwitchProfile(sn); ReloadProfileList(profileList);
                }
            }));
            btnRow.Children.Add(MakeToolBtn("\u270e", "profile.rename", (s, e) =>
            {
                var old = _lumenOwner.ActiveProfile;
                var name = InputBox.Show(this, Loc.T("profile.renameTitle"), Loc.T("profile.newNamePrompt"), old);
                if (!string.IsNullOrWhiteSpace(name)) { _lumenOwner.RenameProfile(old, name.Trim()); ReloadProfileList(profileList); }
            }));
            btnRow.Children.Add(MakeToolBtn("\u2715", "profile.delete", (s, e) =>
            {
                var name = _lumenOwner.ActiveProfile;
                var result = MessageBox.Show(this, Loc.T("profile.deleteConfirm", name), Loc.T("profile.deleteTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) { _lumenOwner.DeleteProfile(name); ReloadProfileList(profileList); }
            }));
            sp.Children.Add(btnRow);

            return sp;
        }

        private void ReloadProfileList(ListBox list)
        {
            list.Items.Clear();
            var active = _lumenOwner.ActiveProfile;
            foreach (var name in ConfigStore.ListProfiles())
            {
                var isActive = name == active;
                list.Items.Add(new ListBoxItem
                {
                    Content = (isActive ? "\u25cf " : "   ") + name,
                    Tag = name,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal
                });
            }
        }

        // ========== 设置 ==========

        private UIElement BuildSettingsTab()
        {
            var sp = new StackPanel { Margin = new Thickness(8) };

            // 语言
            sp.Children.Add(new TextBlock { Text = Loc.T("settings.language"), FontSize = 11, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 0, 0, 4) });
            var langCb = new ComboBox
            {
                Width = 200, HorizontalAlignment = HorizontalAlignment.Left,
                DisplayMemberPath = "Name", SelectedValuePath = "Code"
            };
            langCb.ItemsSource = Loc.Available;
            langCb.SelectedValue = Loc.Cur;
            langCb.SelectionChanged += (s, e) =>
            {
                if (langCb.SelectedValue is string code && code != Loc.Cur)
                    Loc.Load(code);
            };
            sp.Children.Add(langCb);

            sp.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });

            // 开机自启
            var chkAuto = new CheckBox
            {
                Content = Loc.T("settings.autostart"), Foreground = Theme.TextPrimary,
                IsChecked = Autostart.Enabled
            };
            chkAuto.Checked += (s, e) => Autostart.SetEnabled(true);
            chkAuto.Unchecked += (s, e) => Autostart.SetEnabled(false);
            sp.Children.Add(chkAuto);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("settings.autostart.hint"), FontSize = 10,
                Foreground = Theme.TextTertiary, Margin = new Thickness(22, 0, 0, 0)
            });

            sp.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });

            // 覆盖层显隐
            sp.Children.Add(new TextBlock { Text = Loc.T("settings.visibility"), FontSize = 11, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 0, 0, 4) });
            var toggleBtn = new Button
            {
                Content = Loc.T("settings.hide"), Width = 160, HorizontalAlignment = HorizontalAlignment.Left,
                Background = Theme.BgHover, Foreground = Theme.TextPrimary, BorderThickness = new Thickness(0), Padding = new Thickness(8, 4, 8, 4)
            };
            toggleBtn.Click += (s, e) =>
            {
                _lumenOwner.ToggleVisibility();
                toggleBtn.Content = (_lumenOwner.Visibility == Visibility.Visible) ? Loc.T("settings.hide") : Loc.T("settings.show");
            };
            sp.Children.Add(toggleBtn);
            sp.Children.Add(new TextBlock { Text = Loc.T("settings.hotkey"), FontSize = 11, Foreground = Theme.TextTertiary, Margin = new Thickness(0, 4, 0, 0) });

            sp.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });

            // 多屏提示
            sp.Children.Add(new TextBlock { Text = Loc.T("settings.multi"), FontSize = 11, Foreground = Theme.TextTertiary });
            sp.Children.Add(new TextBlock { Text = Loc.T("settings.multi.tip"), FontSize = 11, Foreground = Theme.TextTertiary, TextWrapping = TextWrapping.Wrap });

            return sp;
        }

        private void LoadPageSettings()
        {
            if (_page == null) return;
            _loading = true;
            if (ShowGridCb != null) ShowGridCb.IsChecked = _page.ShowGrid;
            if (GridSizeCb != null) GridSizeCb.SelectedItem = _page.GridSize.ToString("0");
            _loading = false;
        }

        private void GridShow_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || _page == null) return;
            _page.ShowGrid = ShowGridCb?.IsChecked ?? false;
            _lumenOwner?.ApplyGridShow(_page.ShowGrid);
        }

        private void GridSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _page == null) return;
            _loading = true;
            if (GridSizeCb.SelectedItem is string s && double.TryParse(s, out var g))
            {
                _page.GridSize = g;
                _lumenOwner?.SetGridGear(g);
            }
            _loading = false;
        }

        private void BgMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isImage = RbImage?.IsChecked == true;
            if (ColorTb != null) ColorTb.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
            if (Swatch != null) Swatch.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
            if (PickColorBtn != null) PickColorBtn.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
            if (ImageTb != null) ImageTb.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
            if (BrowseBtn != null) BrowseBtn.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
        }
        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            if (_page == null) return;
            var picked = ColorPickerWindow.PickColor(ColorTb.Text, AllowsTransparency ? null : this);
            if (picked == null) return;
            ColorTb.Text = picked;
            if (_page.Background == null) _page.Background = new Render.BackgroundRef();
            _page.Background.Kind = "solid";
            _page.Background.Source = picked;
        }
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (_page == null) return;
            var picked = FilePickerWindow.PickFile(AllowsTransparency ? null : this, Loc.T("dlg.bgImage.filter"), ImageTb.Text);
            if (picked == null) return;
            ImageTb.Text = picked;
            if (_page.Background == null) _page.Background = new Render.BackgroundRef();
            _page.Background.Kind = "image";
            _page.Background.Source = picked;
        }
        private void ApplyBg_Click(object sender, RoutedEventArgs e)
        {
            _externalStructural?.Invoke();
        }

        private void Clear_Click(object sender, RoutedEventArgs e) => LoadAtom(null);
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Apply_Click(object sender, RoutedEventArgs e) { _propPanel?.Apply(); _onApply?.Invoke(); }
    }
}
