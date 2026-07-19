using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Lumen.Atoms;
using Lumen.Pages;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 侧边面板（可折叠）：替代独立 TreeWindow，内嵌到 LumenWindow 右侧。
    /// 展开时显示部件树，收起时仅露出箭头标签。
    /// </summary>
    public partial class SidebarPanel : UserControl
    {
        private Pages.Page _page;
        private PageManager _pageManager;
        private bool _expanded = true;

        public Atom SelectedAtom { get; private set; }
        public event Action<Atom> SelectedAtomChanged;
        public event Action StructureChanged;

        private int _selectedIndex;
        private IList<Atom> _parentList;
        private readonly Stack<ContainerAtom> _navStack = new Stack<ContainerAtom>();
        private Pages.Page _loadedPage;

        // 拖拽状态
        private Point _dragStart;

        public SidebarPanel()
        {
            InitializeComponent();
        }

        public void LoadPage(Pages.Page page, PageManager mgr)
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
            RebuildTree();
        }

        public void RebuildTree()
        {
            try
            {
                AtomsTree.Items.Clear();
                if (_page == null) return;
                BuildBreadcrumb();
                UpdateBackButton();

                var list = CurrentList();
                for (int i = 0; i < list.Count; i++)
                {
                    bool last = i == list.Count - 1;
                    AtomsTree.Items.Add(BuildNode(list[i], last));
                }

                if (SelectedAtom != null)
                    SelectAtom(SelectedAtom);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RebuildTree error: " + ex);
            }
        }

        private IList<Atom> CurrentList()
            => _navStack.Count == 0 ? _page.Atoms : _navStack.Peek().Children;

        private TreeViewItem BuildNode(Atom atom, bool isLast)
        {
            string conn = isLast ? "┗━ " : "┣━ ";
            string drill = atom is ContainerAtom c && c.Children.Count > 0 ? "  ▸" : "";
            var item = new TreeViewItem
            {
                Header = conn + FormatLabel(atom) + drill,
                Tag = atom,
                IsExpanded = false
            };
            var m = new ContextMenu();
            var rename = new MenuItem { Header = Loc.T("tree.rename") };
            rename.Click += (s, e) => RenameAtom(atom);
            m.Items.Add(rename);
            var copyId = new MenuItem { Header = Loc.T("tree.copyId") };
            copyId.Click += (s, e) => System.Windows.Clipboard.SetText(atom.Id);
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

        private void BuildBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            var home = new TextBlock
            {
                Text = Loc.T("tree.pageRoot"),
                Foreground = _navStack.Count == 0 ? Brushes.White : SystemColors.GrayTextBrush,
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            if (_navStack.Count > 0)
            {
                home.MouseLeftButtonDown += (s, e) => { GoToDepth(0); };
            }
            BreadcrumbPanel.Children.Add(home);

            int depth = 1;
            foreach (var c in _navStack.Reverse())
            {
                var sep = new TextBlock { Text = " › ", FontSize = 11, Foreground = SystemColors.GrayTextBrush };
                var crumb = new TextBlock
                {
                    Text = c.Name,
                    FontSize = 11,
                    Foreground = depth == _navStack.Count ? Brushes.White : SystemColors.GrayTextBrush,
                    Cursor = Cursors.Hand
                };
                int d = depth;
                if (depth < _navStack.Count)
                    crumb.MouseLeftButtonDown += (s, e) => GoToDepth(d);
                BreadcrumbPanel.Children.Add(sep);
                BreadcrumbPanel.Children.Add(crumb);
                depth++;
            }
        }

        private void UpdateBackButton() => BackBtn.IsEnabled = _navStack.Count > 0;

        private void GoToDepth(int depth)
        {
            if (depth < 0 || depth >= _navStack.Count) return;
            while (_navStack.Count > depth) _navStack.Pop();
            ClearSelection();
            RebuildTree();
        }

        private void ClearSelection()
        {
            SelectedAtom = null;
            _parentList = null;
            _selectedIndex = -1;
            SelectedAtomChanged?.Invoke(null);
        }

        private void EnterContainer(ContainerAtom container)
        {
            _navStack.Push(container);
            ClearSelection();
            RebuildTree();
        }

        public void SelectAtom(Atom atom)
        {
            SelectedAtom = atom;
            if (atom == null)
            {
                ClearSelection();
                RebuildTree();
                return;
            }
            // 尝试在当前层级选中；如果不在，回到根层
            var list = CurrentList();
            if (!list.Contains(atom))
            {
                _navStack.Clear();
                list = CurrentList();
            }
            _selectedIndex = list.IndexOf(atom);
            _parentList = list;
            RebuildTree();
            SelectTreeItem(atom);
        }

        private void SelectTreeItem(Atom atom)
        {
            foreach (TreeViewItem item in AtomsTree.Items)
            {
                if (item.Tag == atom)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        // ---------- 事件 ----------

        private void Tree_SelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is Atom atom)
            {
                SelectedAtom = atom;
                var list = CurrentList();
                _selectedIndex = list.IndexOf(atom);
                _parentList = list;
                SelectedAtomChanged?.Invoke(atom);
            }
        }

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (tvi?.Tag is ContainerAtom c)
                EnterContainer(c);
        }

        private static TreeViewItem FindTreeViewItem(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);
            return source as TreeViewItem;
        }

        // ---------- 拖拽 ----------

        private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(AtomsTree);
            if (Math.Abs(pos.X - _dragStart.X) < 5 && Math.Abs(pos.Y - _dragStart.Y) < 5) return;
            if (AtomsTree.SelectedItem is TreeViewItem tvi && tvi.Tag is Atom atom)
                DragDrop.DoDragDrop(AtomsTree, atom, DragDropEffects.Move);
        }

        private void Tree_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(Atom)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void Tree_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Atom))) return;
            var dragged = (Atom)e.Data.GetData(typeof(Atom));
            var targetTvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
            var target = targetTvi?.Tag as Atom;

            var srcList = _page != null ? AtomTree.FindParentList(_page, dragged) : null;
            if (srcList == null) return;

            int srcIdx = srcList.IndexOf(dragged);
            int dstIdx = -1;
            IList<Atom> dstList = CurrentList();

            if (target is ContainerAtom targetContainer)
            {
                targetContainer.Children.Add(dragged);
                dstList = targetContainer.Children;
                dstIdx = dstList.Count - 1;
            }
            else if (target != null)
            {
                dstIdx = dstList.IndexOf(target);
                if (dstIdx >= srcIdx && srcList == dstList) dstIdx++;
                dstList.Insert(dstIdx, dragged);
            }
            else
            {
                dstList.Add(dragged);
                dstIdx = dstList.Count - 1;
            }

            if (srcList != dstList || srcIdx != dstIdx)
            {
                if (srcList == dstList && srcIdx < dstIdx && srcIdx >= 0) dstIdx--;
                if (srcIdx >= 0) srcList.RemoveAt(srcIdx);
            }

            StructureChanged?.Invoke();
            RebuildTree();
        }

        // ---------- 按钮 ----------

        private void Back_Click(object sender, RoutedEventArgs e) => GoToDepth(_navStack.Count - 1);

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var type = AddComponentPanel.ShowPick(Window.GetWindow(this));
            if (type == null) return;
            var atom = AtomRegistry.Create(type);
            if (atom == null) return;
            CurrentList().Add(atom);
            SelectedAtom = atom;
            RebuildTree();
            SelectedAtomChanged?.Invoke(atom);
            StructureChanged?.Invoke();
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIndex > 0)
            {
                var list = CurrentList();
                var atom = list[_selectedIndex];
                list.RemoveAt(_selectedIndex);
                list.Insert(_selectedIndex - 1, atom);
                _selectedIndex--;
                StructureChanged?.Invoke();
                RebuildTree();
            }
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            var list = CurrentList();
            if (_selectedIndex >= 0 && _selectedIndex < list.Count - 1)
            {
                var atom = list[_selectedIndex];
                list.RemoveAt(_selectedIndex);
                list.Insert(_selectedIndex + 1, atom);
                _selectedIndex++;
                StructureChanged?.Invoke();
                RebuildTree();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAtom == null) return;
            var list = _page != null ? AtomTree.FindParentList(_page, SelectedAtom) : null;
            if (list != null)
            {
                list.Remove(SelectedAtom);
                SelectedAtom = null;
                StructureChanged?.Invoke();
                RebuildTree();
            }
        }

        private void RenameAtom(Atom atom)
        {
            var result = InputBox.Show(Window.GetWindow(this), Loc.T("tree.renameTitle"), atom.Name, atom.Name);
            if (result != null)
            {
                atom.Name = result;
                StructureChanged?.Invoke();
                RebuildTree();
            }
        }

        // ---------- 展开/收起 ----------

        public bool IsExpanded => _expanded;

        public void Toggle()
        {
            if (_expanded) Collapse(); else Expand();
        }

        private void ToggleTab_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                Toggle();
        }

        public void Expand()
        {
            if (_expanded) return;
            _expanded = true;
            try { ContentPanel.Visibility = Visibility.Visible; } catch { }
            ToggleArrow.Text = "◀";
        }

        public void Collapse()
        {
            if (!_expanded) return;
            _expanded = false;
            try { ContentPanel.Visibility = Visibility.Collapsed; } catch { }
            ToggleArrow.Text = "▶";
        }
    }
}
