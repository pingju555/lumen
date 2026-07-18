using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Atoms;
using Lumen.Pages;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 部件树窗口（P6-03）：树形展示当前页全部原子（含容器嵌套），
    /// 支持选择联动、增删排序、拖拽重排。
    /// 进入编辑模式时弹出，退出编辑模式时关闭。
    /// </summary>
    public partial class TreeWindow : ChromeWindow
    {
        private Lumen.Pages.Page _page;
        private PageManager _pageManager;

        /// <summary>选中原子变更事件（外部联到 PropWindow）。</summary>
        public event Action<Atom> SelectedAtomChanged;
        /// <summary>结构变更事件（增删/排序后触发，外部联到 ComposeCurrentPage + SaveAll）。</summary>
        public event Action StructureChanged;

        /// <summary>当前选中的原子。</summary>
        public Atom SelectedAtom { get; private set; }

        /// <summary>选中原子在父列表中的索引（用于上下移）。</summary>
        private int _selectedIndex;
        /// <summary>选中原子的父亲容器列表（null 表示页面顶层）。</summary>
        private IList<Atom> _parentList;

        /// <summary>钻取导航栈：从页面顶层向下逐层压入已进入的容器（栈顶=当前所在容器）。</summary>
        private readonly Stack<ContainerAtom> _navStack = new Stack<ContainerAtom>();
        /// <summary>上次 LoadPage 的页面引用，用于判断页面是否切换以决定是否重置导航栈。</summary>
        private Lumen.Pages.Page _loadedPage;

        // 拖拽状态
        private Point _dragStart;
        private bool _isDragging;

        public TreeWindow()
        {
            InitializeComponent();
        }

        /// <summary>加载页面数据并重建树。同一页面重复加载时保留钻取导航栈（仅刷新当前层）。</summary>
        public void LoadPage(Lumen.Pages.Page page, PageManager mgr)
        {
            bool pageChanged = !ReferenceEquals(page, _loadedPage);
            _page = page;
            _pageManager = mgr;
            _loadedPage = page;
            if (pageChanged)
            {
                _navStack.Clear();
                SelectedAtom = null;
                _parentList = null;
                _selectedIndex = -1;
            }
            Title = Loc.T("tree.title", page?.Name ?? Loc.T("tree.none"));
            RebuildTree();
        }

        /// <summary>重建当前层级的树节点（钻取视图：仅显示当前容器/页面顶层的直接子部件）。</summary>
        public void RebuildTree()
        {
            AtomsTree.Items.Clear();
            BuildBreadcrumb();
            UpdateBackButton();
            if (_page == null) return;

            var list = CurrentList();
            for (int i = 0; i < list.Count; i++)
            {
                bool last = i == list.Count - 1;
                AtomsTree.Items.Add(BuildNode(list[i], last));
            }

            // 尝试恢复选中
            if (SelectedAtom != null)
                SelectAtom(SelectedAtom);
        }

        /// <summary>当前层级对应的部件列表：栈顶容器之子，或页面顶层。</summary>
        private IList<Atom> CurrentList()
            => _navStack.Count == 0 ? _page.Atoms : _navStack.Peek().Children;

        private TreeViewItem BuildNode(Atom atom, bool isLast)
        {
            string conn = isLast ? "┗━ " : "┣━ ";
            // 容器且含子部件时给出可进入提示
            string drill = atom is ContainerAtom c && c.Children.Count > 0 ? "  ▸" : "";
            var item = new TreeViewItem
            {
                Header = conn + FormatLabel(atom) + drill,
                Tag = atom,
                IsExpanded = false
            };

            // 右键菜单：重命名 + 复制 ID
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

        private static string FormatLabel(Atom atom)
        {
            if (atom is ContainerAtom c)
            {
                string kind = Loc.T("atom.type." + c.Type.ToLowerInvariant());
                return $"{c.Name} [{kind}] ({c.Children.Count})";
            }
            return atom.Name;
        }

        // ---------- 钻取导航 ----------

        /// <summary>进入指定容器（双击触发），压栈后仅显示其直接子部件。</summary>
        private void EnterContainer(ContainerAtom container)
        {
            _navStack.Push(container);
            ClearSelection();
            RebuildTree();
        }

        /// <summary>返回上一级（出栈）。已在顶层时无效。</summary>
        private void Back_Click(object sender, RoutedEventArgs e) => GoToDepth(_navStack.Count - 1);

        /// <summary>跳转到指定深度（0 = 页面顶层）。</summary>
        private void GoToDepth(int depth)
        {
            if (depth < 0 || depth >= _navStack.Count) return;
            while (_navStack.Count > depth) _navStack.Pop();
            ClearSelection();
            RebuildTree();
        }

        /// <summary>清空当前选中并通知外部（属性窗/画布取消选中）。</summary>
        private void ClearSelection()
        {
            SelectedAtom = null;
            _parentList = null;
            _selectedIndex = -1;
            SelectedAtomChanged?.Invoke(null);
        }

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (tvi?.Tag is ContainerAtom c)
                EnterContainer(c);
        }

        private void BuildBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            if (_page == null) return;
            AddCrumb(_page.Name ?? Loc.T("tree.none"), 0);
            int depth = 1;
            // 栈底（最先进入）在最左
            foreach (var c in _navStack.Reverse())
                AddCrumb(c.Name, depth++);
        }

        private void AddCrumb(string text, int index)
        {
            if (BreadcrumbPanel.Children.Count > 0)
            {
                BreadcrumbPanel.Children.Add(new TextBlock
                {
                    Text = "   /   ",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            bool current = index == _navStack.Count;
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(current
                    ? Colors.White
                    : Color.FromRgb(0x4E, 0xC9, 0xB0))
            };
            int targetDepth = index;
            tb.MouseLeftButtonDown += (s, e) => GoToDepth(targetDepth);
            BreadcrumbPanel.Children.Add(tb);
        }

        private void UpdateBackButton()
        {
            if (BackBtn != null) BackBtn.IsEnabled = _navStack.Count > 0;
        }

        /// <summary>选中指定原子（按值匹配，不要求同一引用）；null 表示取消选中。</summary>
        public bool SelectAtom(Atom target)
        {
            // 取消当前选中
            if (AtomsTree.SelectedItem is TreeViewItem old)
                old.IsSelected = false;

            if (target == null) return false;
            var found = FindNode(AtomsTree.Items, target);
            if (found != null)
            {
                found.IsSelected = true;
                found.BringIntoView();
                return true;
            }
            return false;
        }

        private static TreeViewItem FindNode(ItemCollection items, Atom target)
        {
            foreach (var obj in items)
            {
                if (obj is TreeViewItem tvi)
                {
                    if (tvi.Tag is Atom a && AreSameAtom(a, target))
                        return tvi;
                    var found = FindNode(tvi.Items, target);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>按 Id 比较两个原子是否同一实例。</summary>
        private static bool AreSameAtom(Atom a, Atom b)
        {
            if (a == b) return true;
            if (a == null || b == null) return false;
            return a.Id == b.Id;
        }

        // ---------- 事件处理 ----------

        private void Tree_SelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is Atom atom)
            {
                SelectedAtom = atom;
                // 查找父列表（递归）
                _parentList = _page != null ? FindParentList(atom, _page.Atoms) : null;
                _selectedIndex = _parentList?.IndexOf(atom) ?? -1;
                SelectedAtomChanged?.Invoke(atom);
            }
        }

        /// <summary>递归查找原子所在的父列表（支持多层容器嵌套）。</summary>
        private static IList<Atom> FindParentList(Atom target, IEnumerable<Atom> topLevel)
        {
            if (topLevel == null) return null;
            // 先检查目标是否直接在当前层
            if (topLevel is IList<Atom> list && list.Contains(target)) return list;
            foreach (var top in topLevel)
            {
                if (top is ContainerAtom c)
                {
                    if (c.Children.Contains(target)) return c.Children;
                    // 递归深入子容器
                    var found = FindParentList(target, c.Children);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // ---- Drag 由 ChromeWindow 模板全权处理 ----

        /// <summary>右键重命名弹窗。</summary>
        private void RenameAtom(Atom atom)
        {
            var dlg = new Window
            {
                Title = Loc.T("tree.rename"),
                Width = 280, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize
            };
            var sp = new StackPanel { Margin = new Thickness(10) };
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("tree.newName"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var tb = new TextBox
            {
                Text = atom.Name,
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                MinHeight = 24
            };
            sp.Children.Add(tb);
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var ok = new Button { Content = Loc.T("common.ok"), Width = 70, Margin = new Thickness(0, 0, 6, 0) };
            ok.Click += (s, e) =>
            {
                var newName = tb.Text.Trim();
                if (!string.IsNullOrEmpty(newName))
                {
                    atom.Name = newName;
                    RebuildTree();
                }
                dlg.Close();
            };
            var cancel = new Button { Content = Loc.T("common.cancel"), Width = 70 };
            cancel.Click += (s, e) => dlg.Close();
            btnPanel.Children.Add(ok);
            btnPanel.Children.Add(cancel);
            sp.Children.Add(btnPanel);
            dlg.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                Child = sp
            };
            dlg.ShowDialog();
        }

        // ---------- 工具栏按钮 ----------

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            // 弹窗选类型（后续替换为类型选择器对话框）
            var dlg = new AtomTypePicker();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                var atom = AtomRegistry.Create(dlg.SelectedType);
                if (atom == null) return;
                // 加入当前钻取层级（栈顶容器之子 / 页面顶层）
                CurrentList().Add(atom);
                RebuildTree();
                SelectAtom(atom);
                SelectedAtomChanged?.Invoke(atom);
                StructureChanged?.Invoke();
            }
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAtom == null || _parentList == null || _selectedIndex <= 0) return;
            Swap(_parentList, _selectedIndex, _selectedIndex - 1);
            _selectedIndex--;
            RebuildTree();
            SelectAtom(SelectedAtom);
            StructureChanged?.Invoke();
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAtom == null || _parentList == null || _selectedIndex < 0 ||
                _selectedIndex >= _parentList.Count - 1) return;
            Swap(_parentList, _selectedIndex, _selectedIndex + 1);
            _selectedIndex++;
            RebuildTree();
            SelectAtom(SelectedAtom);
            StructureChanged?.Invoke();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAtom == null || _parentList == null) return;
            _parentList.Remove(SelectedAtom);
            SelectedAtom = null;
            _parentList = null;
            _selectedIndex = -1;
            RebuildTree();
            SelectedAtomChanged?.Invoke(null);
            StructureChanged?.Invoke();
        }

        private static void Swap(IList<Atom> list, int i, int j)
        {
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }

        // ---------- 拖拽排序 ----------

        private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(AtomsTree);
            if (!_isDragging && (Math.Abs(pos.X - _dragStart.X) > 5 || Math.Abs(pos.Y - _dragStart.Y) > 5))
            {
                _isDragging = true;
                var src = FindTreeViewItem(e.OriginalSource as DependencyObject);
                if (src?.Tag is Atom)
                    DragDrop.DoDragDrop(AtomsTree, src, DragDropEffects.Move);
            }
        }

        private void Tree_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // 滚动支持（靠近上下边界时自动滚动）
            var pos = e.GetPosition(AtomsTree);
            double margin = 20;
            if (AtomsTree.Items.Count > 0)
            {
                if (pos.Y < margin && AtomsTree.Items[0] is TreeViewItem first)
                    first.BringIntoView();
                else if (pos.Y > AtomsTree.ActualHeight - margin && AtomsTree.Items[AtomsTree.Items.Count - 1] is TreeViewItem last)
                    last.BringIntoView();
            }
        }

        private void Tree_Drop(object sender, DragEventArgs e)
        {
            _isDragging = false;
            if (!(e.Data.GetData(typeof(TreeViewItem)) is TreeViewItem srcItem)) return;
            if (!(srcItem.Tag is Atom srcAtom)) return;

            var target = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (target == null) return;
            if (!(target.Tag is Atom targetAtom)) return;

            // 只支持同层拖拽
            var srcParent = _page != null ? FindParentList(srcAtom, _page.Atoms) : null;
            var tgtParent = _page != null ? FindParentList(targetAtom, _page.Atoms) : null;
            if (srcParent == null || tgtParent == null) return;
            if (srcParent != tgtParent) return; // 同容器/同层

            int srcIdx = srcParent.IndexOf(srcAtom);
            int tgtIdx = srcParent.IndexOf(targetAtom);
            if (srcIdx < 0 || tgtIdx < 0 || srcIdx == tgtIdx) return;

            // 移动到目标位置
            srcParent.RemoveAt(srcIdx);
            int newIdx = tgtIdx > srcIdx ? tgtIdx - 1 : tgtIdx;
            srcParent.Insert(newIdx, srcAtom);

            RebuildTree();
            SelectAtom(srcAtom);
            StructureChanged?.Invoke();
        }

        private static TreeViewItem FindTreeViewItem(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is TreeViewItem tvi) return tvi;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        // TODO: v1 用简单弹窗选类型，后续可改成窗口内下拉
        internal class AtomTypePicker : Window
        {
            public string SelectedType { get; private set; }
            private static readonly string[] Types = { "Text", "Shape", "Icon", "Image", "Progress", "Stack", "Overlap", "Series" };

            public AtomTypePicker()
            {
                Title = Loc.T("tree.pickType");
                Width = 200; Height = 280;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;

                var sp = new StackPanel { Margin = new Thickness(10) };
                sp.Children.Add(new TextBlock
                {
                    Text = Loc.T("tree.pickType"), FontSize = 14, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(0, 0, 0, 8)
                });

                var lb = new ListBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46))
                };
                foreach (var t in Types)
                    lb.Items.Add(new ListBoxItem { Content = Loc.T("atom.type." + t.ToLower()), Tag = t });
                lb.SelectedIndex = 0;
                sp.Children.Add(lb);

                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var okBtn = new Button { Content = Loc.T("common.ok"), Width = 70, Margin = new Thickness(0, 0, 6, 0) };
                okBtn.Click += (s, e) =>
                {
                    if (lb.SelectedItem is ListBoxItem li && li.Tag is string tag) SelectedType = tag;
                    DialogResult = true;
                };
                var cancelBtn = new Button { Content = Loc.T("common.cancel"), Width = 70 };
                cancelBtn.Click += (s, e) => DialogResult = false;
                btnPanel.Children.Add(okBtn);
                btnPanel.Children.Add(cancelBtn);
                sp.Children.Add(btnPanel);

                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                    BorderThickness = new Thickness(1),
                    Child = sp
                };
            }
        }
    }
}
