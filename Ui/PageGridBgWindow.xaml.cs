using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Core;
using Lumen.Pages;
using Lumen.Atoms;
using Lumen.Formula;
using Lumen.I18n;
using Lumen.Presets;

namespace Lumen.Ui
{
    public partial class PageGridBgWindow : Window
    {
        private LumenWindow _owner;
        private PageManager _pageManager;
        private bool _loading;
        private PropMode _bgMode = PropMode.Static;

        public PageGridBgWindow()
        {
            InitializeComponent();
            InitGridSizes();
            // 色值变化即时更新色块（只绑一次）
            ColorTb.TextChanged += (s, e) => UpdateSwatch();
            Loc.LangChanged += OnLangChanged;
            Closing += (s, e) => Loc.LangChanged -= OnLangChanged;
        }

        public void Init(LumenWindow owner, PageManager mgr)
        {
            _owner = owner;
            _pageManager = mgr;
            ReloadAll();
        }

        private void InitGridSizes()
        {
            int[] sizes = { 20, 40, 60, 120 };
            foreach (var s in sizes) GridSizeCb.Items.Add(Loc.T("pagebg.gridSizeItem", s));
        }

        private void ReloadAll()
        {
            _loading = true;
            try
            {
                ReloadPages();
                ReloadGrid();
                ReloadBg();
                ReloadScenes();
            }
            catch (Exception ex) { Logger.Log("PageGridBgWindow ReloadAll: " + ex); }
            _loading = false;
        }

        // ---------- 页面 ----------
        private void ReloadPages()
        {
            PageList.Items.Clear();
            for (int i = 0; i < _pageManager.Pages.Count; i++)
            {
                var pg = _pageManager.Pages[i];
                var item = new ListBoxItem { Content = pg.Name, Tag = i, IsSelected = i == _pageManager.Current };
                PageList.Items.Add(item);
            }
        }

        private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (PageList.SelectedItem is ListBoxItem lbi && lbi.Tag is int idx)
            {
                _owner.GotoPage(idx);
                ReloadAll();
            }
        }

        private void NewPage_Click(object sender, RoutedEventArgs e)
        {
            _owner.AddNewPage();
            ReloadAll();
        }

        private void DeletePage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageManager.Pages.Count <= 1) return;
            _owner.RemoveCurrentPage();
            ReloadAll();
        }

        private void RenamePage_Click(object sender, RoutedEventArgs e)
        {
            var page = _pageManager.CurrentPage;
            if (page == null) return;
            _owner.RenameCurrentPage();
            ReloadAll();
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            _owner.MovePage(-1);
            ReloadAll();
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            _owner.MovePage(1);
            ReloadAll();
        }

        // ---------- 网格 ----------
        private void ReloadGrid()
        {
            var page = _pageManager.CurrentPage;
            if (page == null) return;
            ShowGridCb.IsChecked = page.ShowGrid;
            int[] sizes = { 20, 40, 60, 120 };
            int idx = Array.IndexOf(sizes, (int)page.GridSize);
            GridSizeCb.SelectedIndex = idx < 0 ? 1 : idx;
        }

        private void GridShow_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _owner.ToggleGridShow();
        }

        private void GridSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || GridSizeCb.SelectedIndex < 0) return;
            int[] sizes = { 20, 40, 60, 120 };
            _owner.ApplyGridGear(sizes[GridSizeCb.SelectedIndex]);
        }

        // ---------- 背景 ----------
        private void ReloadBg()
        {
            var page = _pageManager.CurrentPage;
            if (page?.Background == null) return;
            if (page.Background.Kind == "image")
            {
                RbImage.IsChecked = true;
                ImageTb.Text = page.Background.Source ?? "";
                return;
            }
            RbSolid.IsChecked = true;
            var src = page.Background.Source ?? "#FF1E1E1E";
            PropMode mode;
            if (src.StartsWith("gv:", StringComparison.OrdinalIgnoreCase)) mode = PropMode.Global;
            else if (src.Contains("$")) mode = PropMode.Formula;
            else mode = PropMode.Static;

            if (mode == PropMode.Formula)
            {
                var e2 = src;
                if (e2.StartsWith("$") && e2.EndsWith("$") && e2.Length >= 2) e2 = e2.Substring(1, e2.Length - 2);
                FormulaTb.Text = e2;
            }
            else if (mode == PropMode.Global)
            {
                PopulateGvCombo();
                var name = src.Substring(3).Trim();
                BgGvCb.SelectedItem = name;
            }
            else
            {
                ColorTb.Text = src;
            }
            SetBgMode(mode);
        }

        private void PopulateGvCombo()
        {
            BgGvCb.Items.Clear();
            var gv = _owner?.Ctx?.Gv;
            if (gv == null) return;
            foreach (var key in gv.All.Keys) BgGvCb.Items.Add(key);
        }

        private void BgMode_Changed(object sender, RoutedEventArgs e)
        {
            // XAML 解析期间（InitializeComponent）Checked 事件已触发，此时控件尚未全部创建，需 null 保护
            if (SolidRow == null || ImageRow == null) return;
            bool solid = RbSolid.IsChecked == true;
            SolidRow.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;
            ImageRow.Visibility = solid ? Visibility.Collapsed : Visibility.Visible;
        }

        // ---------- 纯色模式切换（值 / 公式 / 变量） ----------
        private void BgModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is PropMode m) SetBgMode(m);
        }

        private void SetBgMode(PropMode m)
        {
            _bgMode = m;
            BgValueHost.Visibility = m == PropMode.Static ? Visibility.Visible : Visibility.Collapsed;
            BgFormulaHost.Visibility = m == PropMode.Formula ? Visibility.Visible : Visibility.Collapsed;
            BgGvHost.Visibility = m == PropMode.Global ? Visibility.Visible : Visibility.Collapsed;
            HighlightBgButtons(m);
            if (m == PropMode.Global) PopulateGvCombo();
            if (m == PropMode.Formula) UpdateFormulaPreview();
        }

        private void HighlightBgButtons(PropMode active)
        {
            SetBtnBg(BgModeVal, active == PropMode.Static);
            SetBtnBg(BgModeFormula, active == PropMode.Formula);
            SetBtnBg(BgModeGv, active == PropMode.Global);
        }

        private static void SetBtnBg(Button b, bool on)
        {
            b.Background = new SolidColorBrush(on ? Color.FromRgb(0x00, 0x7A, 0xCC) : Color.FromRgb(0x3A, 0x3D, 0x41));
            b.Foreground = new SolidColorBrush(on ? Colors.White : Color.FromRgb(0xF0, 0xF0, 0xF0));
        }

        private void FormulaTb_TextChanged(object sender, TextChangedEventArgs e) => UpdateFormulaPreview();

        private void UpdateFormulaPreview()
        {
            var raw = FormulaTb.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                FormulaPreviewSwatch.Visibility = Visibility.Collapsed;
                FormulaPreviewText.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                var expr = (raw.StartsWith("$") && raw.EndsWith("$")) ? raw : "$" + raw + "$";
                var v = PropertyValue.Parse(expr).Resolve(_owner.Ctx);
                string hex = v.Type == Formula.ValueType.Color ? "#" + v.ColorArgb.ToString("X8") : v.AsStr();
                var c = (Color)ColorConverter.ConvertFromString(hex.Trim());
                FormulaPreviewSwatch.Background = new SolidColorBrush(c);
                FormulaPreviewSwatch.Visibility = Visibility.Visible;
                FormulaPreviewText.Text = "= " + hex;
                FormulaPreviewText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                FormulaPreviewSwatch.Visibility = Visibility.Collapsed;
                FormulaPreviewText.Text = Loc.T("pagebg.formulaError") + ex.Message.Split('\n')[0];
                FormulaPreviewText.Visibility = Visibility.Visible;
            }
        }

        private void UpdateSwatch()
        {
            try { Swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorTb.Text.Trim())); }
            catch { Swatch.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }
        }

        // ---------- 场景预设 ----------
        private void ReloadScenes()
        {
            if (SceneList == null) return;
            SceneList.Items.Clear();
            foreach (var p in PresetLibrary.User)
            {
                var tag = p.Kind == PresetKind.Scene ? Loc.T("pagebg.presetScene") : Loc.T("pagebg.presetLook");
                SceneList.Items.Add(new ListBoxItem { Content = Loc.T("pagebg.presetItem", p.Name, tag), Tag = p.Name });
            }
            SyncSceneButtons();
        }

        private void SyncSceneButtons()
        {
            bool has = SceneList != null && SceneList.SelectedItem != null;
            if (ApplySceneBtn != null) ApplySceneBtn.IsEnabled = has;
            if (DeleteSceneBtn != null) DeleteSceneBtn.IsEnabled = has;
        }

        private void SaveScene_Click(object sender, RoutedEventArgs e)
        {
            var name = SceneNameTb.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, Loc.T("pagebg.sceneNameEmpty"), Loc.T("pagebg.tab.scene"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _owner.SaveCurrentAsScenePreset(name);
            ReloadScenes();
        }

        private void SceneList_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncSceneButtons();

        private void ApplyScene_Click(object sender, RoutedEventArgs e)
        {
            if (SceneList.SelectedItem is ListBoxItem item && item.Tag is string name)
            {
                _owner.ApplyPresetByName(name);
                ReloadAll();
            }
        }

        private void DeleteScene_Click(object sender, RoutedEventArgs e)
        {
            if (SceneList.SelectedItem is ListBoxItem item && item.Tag is string name)
            {
                PresetLibrary.RemoveUser(name);
                ReloadScenes();
                _owner.SaveAll();
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var picked = FilePickerWindow.PickFile(this, Loc.T("dlg.bgImage.filter"), ImageTb.Text);
            if (picked != null) ImageTb.Text = picked;
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = ColorPickerWindow.PickColor(ColorTb.Text, this);
            if (picked != null) ColorTb.Text = picked;
        }

        private void ApplyBg_Click(object sender, RoutedEventArgs e)
        {
            if (RbImage.IsChecked == true)
            {
                string imgSrc = ImageTb.Text.Trim();
                if (!File.Exists(imgSrc))
                {
                    MessageBox.Show(this, Loc.T("pagebg.imgNotExist"), Loc.T("pagebg.tab.bg"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _owner.ApplyBackground("image", imgSrc);
                return;
            }
            // 纯色：按模式归一化 Source
            string src;
            switch (_bgMode)
            {
                case PropMode.Formula:
                    {
                        var raw = FormulaTb.Text.Trim();
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            MessageBox.Show(this, Loc.T("pagebg.formulaEmpty"), Loc.T("pagebg.tab.bg"), MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        src = (raw.StartsWith("$") && raw.EndsWith("$")) ? raw : "$" + raw + "$";
                        break;
                    }
                case PropMode.Global:
                    {
                        var nm = BgGvCb.SelectedItem as string;
                        if (string.IsNullOrEmpty(nm))
                        {
                            MessageBox.Show(this, Loc.T("pagebg.gvEmpty"), Loc.T("pagebg.tab.bg"), MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        src = "gv:" + nm;
                        break;
                    }
                default:
                    src = ColorTb.Text.Trim();
                    break;
            }
            _owner.ApplyBackground("solid", src);
        }

        // ---------- 窗口 ----------
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void OnLangChanged(object sender, EventArgs e)
        {
            GridSizeCb.Items.Clear();
            InitGridSizes();
            ReloadAll();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
