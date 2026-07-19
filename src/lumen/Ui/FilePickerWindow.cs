using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 带预览窗格的文件选择弹窗：左侧文件列表 + 右侧选中文件预览（图标/类型/大小，图片显示缩略图）。
    /// 用法：var path = FilePickerWindow.PickFile(owner, "可执行文件|*.exe;*.lnk|所有文件|*.*", initialPath);
    /// 返回选中文件的完整路径，取消返回 null。
    /// </summary>
    public class FilePickerWindow : Window
    {
        #region SHGetFileInfo（取系统文件图标/类型名）
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_TYPENAME = 0x400;
        private const uint SHGFI_DISPLAYNAME = 0x200;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSize, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static (ImageSource icon, string typeName, string displayName) GetFileInfo(string path, bool large)
        {
            var fi = new SHFILEINFO();
            uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON) | SHGFI_TYPENAME | SHGFI_DISPLAYNAME;
            var ptr = SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (ptr == IntPtr.Zero || fi.hIcon == IntPtr.Zero) return (null, "", Path.GetFileName(path));
            try
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    fi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return (src, fi.szTypeName, fi.szDisplayName);
            }
            finally { DestroyIcon(fi.hIcon); }
        }
        #endregion

        #region 内部数据项
        private class FileItem
        {
            public string FullPath;
            public bool IsFolder;
            public string Name => IsFolder ? System.IO.Path.GetFileName(FullPath.TrimEnd('\\')) : System.IO.Path.GetFileName(FullPath);
            public string Glyph => IsFolder ? "📁" : "📄";
            public string Type => IsFolder ? Loc.T("file.folder") : Loc.T("file.fileType", System.IO.Path.GetExtension(FullPath).ToUpper().TrimStart('.'));
            public string SizeText
            {
                get
                {
                    if (IsFolder) return "";
                    try { var len = new FileInfo(FullPath).Length; return FmtSize(len); }
                    catch { return ""; }
                }
            }
        }
        #endregion

        private readonly List<(string label, List<string> exts, bool all)> _filters = new();
        private int _filterIdx;
        private string _currentDir;
        private string _selectedPath;

        private TextBox _pathTb;
        private ListView _list;
        private StackPanel _preview;
        private ComboBox _filterCb;
        private ComboBox _placesCb;

        public FilePickerWindow(string filter, string initialPath)
        {
            Title = Loc.T("file.title");
            Width = 660; Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;

            ParseFilter(filter);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 工具栏
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 主体
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 底部

            root.Children.Add(BuildToolbar());
            root.Children.Add(BuildMain());
            root.Children.Add(BuildFooter());
            Grid.SetRow(root.Children[0], 0);
            Grid.SetRow(root.Children[1], 1);
            Grid.SetRow(root.Children[2], 2);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Child = root
            };

            // 初始目录：文件取其所在目录并预选；目录直接进；都没有则进桌面
            if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
            {
                _currentDir = Path.GetDirectoryName(initialPath);
                Navigate(_currentDir);
                Preselect(initialPath);
            }
            else if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            {
                Navigate(initialPath);
            }
            else
            {
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                Navigate(Directory.Exists(desk) ? desk : Path.GetPathRoot(Environment.SystemDirectory));
            }
        }

        public static string PickFile(Window owner, string filter, string initialPath)
        {
            var w = new FilePickerWindow(filter, initialPath);
            if (owner != null && !owner.AllowsTransparency) w.Owner = owner;
            return w.ShowDialog() == true ? w._selectedPath : null;
        }

        // ---------- 工具栏 ----------
        private UIElement BuildToolbar()
        {
            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(8, 8, 8, 4) };

            var back = new Button { Content = Loc.T("file.up"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
            back.Click += (s, e) =>
            {
                try { var p = Directory.GetParent(_currentDir); if (p != null) Navigate(p.FullName); }
                catch { }
            };
            DockPanel.SetDock(back, Dock.Left);
            bar.Children.Add(back);

            _placesCb = new ComboBox { Width = 170, Margin = new Thickness(0, 0, 4, 0), IsEditable = false };
            _placesCb.Items.Add(Loc.T("file.quickAccess"));
            try
            {
                foreach (var sf in new[] { Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.UserProfile })
                {
                    var p = Environment.GetFolderPath(sf);
                    if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) _placesCb.Items.Add(p);
                }
                foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
                    _placesCb.Items.Add(d.RootDirectory.FullName);
            }
            catch { }
            _placesCb.SelectedIndex = 0;
            _placesCb.SelectionChanged += (s, e) =>
            {
                if (_placesCb.SelectedIndex > 0 && _placesCb.SelectedItem is string sel && Directory.Exists(sel)) Navigate(sel);
            };
            DockPanel.SetDock(_placesCb, Dock.Left);
            bar.Children.Add(_placesCb);

            _pathTb = new TextBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                BorderThickness = new Thickness(1)
            };
            _pathTb.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter && Directory.Exists(_pathTb.Text.Trim()))
                    Navigate(_pathTb.Text.Trim());
            };
            bar.Children.Add(_pathTb);

            return bar;
        }

        // ---------- 主体：列表 + 预览 ----------
        private UIElement BuildMain()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            _list = new ListView { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), Foreground = new SolidColorBrush(Colors.White) };
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = Loc.T("file.colName"), DisplayMemberBinding = new System.Windows.Data.Binding("Name"), Width = 230 });
            gv.Columns.Add(new GridViewColumn { Header = Loc.T("file.colType"), DisplayMemberBinding = new System.Windows.Data.Binding("Type"), Width = 80 });
            gv.Columns.Add(new GridViewColumn { Header = Loc.T("file.colSize"), DisplayMemberBinding = new System.Windows.Data.Binding("SizeText"), Width = 70 });
            _list.View = gv;
            _list.SelectionChanged += (s, e) => ShowPreview(_list.SelectedItem as FileItem);
            _list.MouseDoubleClick += (s, e) =>
            {
                if (_list.SelectedItem is FileItem it)
                {
                    if (it.IsFolder) Navigate(it.FullPath);
                    else { _selectedPath = it.FullPath; DialogResult = true; Close(); }
                }
            };
            Grid.SetColumn(_list, 0);
            grid.Children.Add(_list);

            _preview = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x28)), Margin = new Thickness(4, 0, 0, 0) };
            Grid.SetColumn(_preview, 1);
            grid.Children.Add(_preview);

            return grid;
        }

        // ---------- 底部：过滤器 + 确定/取消 ----------
        private UIElement BuildFooter()
        {
            var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(8, 4, 8, 8) };

            _filterCb = new ComboBox { Width = 220, Margin = new Thickness(0, 0, 8, 0), IsEditable = false };
            foreach (var f in _filters) _filterCb.Items.Add(f.label);
            _filterCb.SelectedIndex = 0;
            _filterCb.SelectionChanged += (s, e) => { _filterIdx = _filterCb.SelectedIndex; Navigate(_currentDir); };
            DockPanel.SetDock(_filterCb, Dock.Left);
            bar.Children.Add(_filterCb);

            var ok = new Button { Content = Loc.T("file.select"), Width = 90, Padding = new Thickness(0, 3, 0, 3), Margin = new Thickness(0, 0, 6, 0) };
            ok.Click += (s, e) =>
            {
                if (_list.SelectedItem is FileItem it && !it.IsFolder) { _selectedPath = it.FullPath; DialogResult = true; }
                else if (File.Exists(_pathTb.Text.Trim())) { _selectedPath = _pathTb.Text.Trim(); DialogResult = true; }
            };
            DockPanel.SetDock(ok, Dock.Right);
            bar.Children.Add(ok);

            var cancel = new Button { Content = Loc.T("file.cancel"), Width = 90, Padding = new Thickness(0, 3, 0, 3) };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            DockPanel.SetDock(cancel, Dock.Right);
            bar.Children.Add(cancel);

            return bar;
        }

        // ---------- 导航与渲染 ----------
        private void Navigate(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
            _currentDir = dir;
            _pathTb.Text = dir;
            _list.Items.Clear();
            try
            {
                var pats = CurrentPatterns();
                foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    try { _list.Items.Add(new FileItem { FullPath = d, IsFolder = true }); } catch { }
                }
                foreach (var f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (Matches(f, pats)) _list.Items.Add(new FileItem { FullPath = f, IsFolder = false });
                }
            }
            catch (Exception ex)
            {
                ShowPreviewMessage(Loc.T("file.dirAccessFail") + ex.Message);
            }
        }

        private void Preselect(string file)
        {
            foreach (FileItem it in _list.Items)
            {
                if (!it.IsFolder && string.Equals(it.FullPath, file, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedItem = it;
                    _list.ScrollIntoView(it);
                    break;
                }
            }
        }

        private void ShowPreview(FileItem it)
        {
            _preview.Children.Clear();
                if (it == null) { _preview.Children.Add(new TextBlock { Text = Loc.T("file.noneSelected"), Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), Margin = new Thickness(10) }); return; }
            if (it.IsFolder)
            {
                var (icon, type, disp) = GetFileInfo(it.FullPath, true);
                AddPreviewRow(icon, disp, Loc.T("file.folder"), "", it.FullPath);
                return;
            }
            try
            {
                var (icon, type, disp) = GetFileInfo(it.FullPath, true);
                string size = FmtSize(new FileInfo(it.FullPath).Length);
                AddPreviewRow(icon, disp, type, size, it.FullPath);

                var ext = Path.GetExtension(it.FullPath).ToLowerInvariant();
                if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new Uri(it.FullPath, UriKind.Absolute);
                        bi.DecodePixelWidth = 200;
                        bi.EndInit();
                        bi.Freeze();
                        var img = new Image { Source = bi, Width = 200, Margin = new Thickness(10, 6, 10, 6), Stretch = Stretch.Uniform };
                        _preview.Children.Add(img);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ShowPreviewMessage(Loc.T("file.previewFail") + ex.Message);
            }
        }

        private void AddPreviewRow(ImageSource icon, string name, string type, string size, string fullPath)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 10, 10, 4) };
            if (icon != null)
                head.Children.Add(new Image { Source = icon, Width = 32, Height = 32, Margin = new Thickness(0, 0, 8, 0) });
            head.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            });
            _preview.Children.Add(head);
            _preview.Children.Add(new TextBlock { Text = Loc.T("file.typeLabel") + type, Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)), Margin = new Thickness(10, 0, 10, 0), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(size))
                _preview.Children.Add(new TextBlock { Text = Loc.T("file.sizeLabel") + size, Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)), Margin = new Thickness(10, 2, 10, 0) });
            _preview.Children.Add(new TextBlock { Text = fullPath, Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), Margin = new Thickness(10, 6, 10, 0), FontSize = 10, TextWrapping = TextWrapping.Wrap });
        }

        private void ShowPreviewMessage(string msg)
        {
            _preview.Children.Clear();
            _preview.Children.Add(new TextBlock { Text = msg, Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)), Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap });
        }

        // ---------- 过滤器解析 ----------
        private void ParseFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                _filters.Add((Loc.T("file.allFiles"), new List<string> { "*" }, true));
                return;
            }
            var parts = filter.Split('|');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                var label = parts[i];
                var pat = parts[i + 1];
                var all = pat.Trim() == "*.*";
                var exts = pat.Split(';')
                    .Select(p => p.Trim().ToLowerInvariant())
                    .Where(p => p.StartsWith("*.") && p.Length > 2)
                    .Select(p => p.Substring(1)) // .ext
                    .ToList();
                _filters.Add((label, exts, all));
            }
            if (_filters.Count == 0) _filters.Add((Loc.T("file.allFiles"), new List<string> { "*" }, true));
        }

        private (bool all, List<string> exts) CurrentPatterns()
        {
            var f = _filters[Math.Max(0, _filterIdx)];
            return (f.all, f.exts);
        }

        private bool Matches(string file, (bool all, List<string> exts) pats)
        {
            if (pats.all) return true;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            return pats.exts.Contains(ext);
        }

        private static string FmtSize(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / (1024.0 * 1024)).ToString("0.#") + " MB";
            return (b / (1024.0 * 1024 * 1024)).ToString("0.#") + " GB";
        }
    }
}
