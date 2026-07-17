using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Lumen.Core;
using Lumen.I18n;
using Lumen.Pages;
using Lumen.Persistence;

namespace Lumen.Ui
{
    /// <summary>
    /// P6-02 设置面板：独立 Topmost 窗口（不污染覆盖层主窗，浮于其上）。
    /// 配置项：自启 / 网格间距 / 当前页背景 / 覆盖层显隐 / 配置导入导出 / 界面语言。
    /// 多屏为 v2 占位。所有文案经 Loc（zh-CN / en-GB），切换语言后本窗通过 LangChanged 自刷新。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly LumenWindow _owner;
        private bool _loading;

        public SettingsWindow(LumenWindow owner)
        {
            InitializeComponent();
            _owner = owner;
            BindEvents();
            LoadCurrent();

            InitLanguageCombo();
            Loc.LangChanged += OnLangChanged;
            Closing += (s, e) => Loc.LangChanged -= OnLangChanged;
            RefreshTexts();
        }

        private void BindEvents()
        {
            ChkAutostart.Checked += (s, e) => { if (!_loading) Autostart.SetEnabled(true); };
            ChkAutostart.Unchecked += (s, e) => { if (!_loading) Autostart.SetEnabled(false); };
            CmbGrid.SelectionChanged += CmbGrid_SelectionChanged;
            RbSolid.Checked += (s, e) => SyncBgRows();
            RbImage.Checked += (s, e) => SyncBgRows();
            TxtBgSolid.TextChanged += (s, e) => UpdateSwatch();
            BtnBrowseBg.Click += BtnBrowseBg_Click;
            BtnApplyBg.Click += BtnApplyBg_Click;
            BtnToggle.Click += (s, e) => { _owner.ToggleVisibility(); UpdateToggleLabel(); };
            BtnExport.Click += BtnExport_Click;
            BtnImport.Click += BtnImport_Click;
            BtnClose.Click += (s, e) => Close();
        }

        private void InitLanguageCombo()
        {
            CmbLang.ItemsSource = Loc.Available;
            CmbLang.SelectedValue = Loc.Cur;
        }

        private void CmbLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLang.SelectedValue is string code && code != Loc.Cur)
                Loc.Load(code); // 触发 LangChanged -> OnLangChanged -> RefreshTexts
        }

        private void OnLangChanged(object sender, EventArgs e) => RefreshTexts();

        private void RefreshTexts()
        {
            TxtTitle.Text = Loc.T("settings.title");
            ChkAutostart.Content = Loc.T("settings.autostart");
            TxtAutostartHint.Text = Loc.T("settings.autostart.hint");
            TxtGrid.Text = Loc.T("settings.gridSize");
            TxtPageBg.Text = Loc.T("settings.pageBg");
            RbSolid.Content = Loc.T("settings.bg.solid");
            RbImage.Content = Loc.T("settings.bg.image");
            BtnBrowseBg.Content = Loc.T("settings.browse");
            BtnApplyBg.Content = Loc.T("settings.applyBg");
            TxtVisibility.Text = Loc.T("settings.visibility");
            UpdateToggleLabel();
            TxtHotkey.Text = Loc.T("settings.hotkey");
            TxtProfileIo.Text = Loc.T("settings.profileIo");
            BtnExport.Content = Loc.T("settings.export");
            BtnImport.Content = Loc.T("settings.import");
            TxtMulti.Text = Loc.T("settings.multi");
            TxtMultiTip.Text = Loc.T("settings.multi.tip");
            BtnClose.Content = Loc.T("settings.close");
            CmbLang.SelectedValue = Loc.Cur;
        }

        private void LoadCurrent()
        {
            _loading = true;
            ChkAutostart.IsChecked = Autostart.Enabled;
            var g = (int)(_owner.CurrentPage?.GridSize ?? 40);
            int idx = Array.IndexOf(new[] { 20, 40, 60, 120 }, g);
            CmbGrid.SelectedIndex = idx < 0 ? 1 : idx;
            _loading = false;

            SyncBgRows();
            UpdateSwatch();
            UpdateToggleLabel();
        }

        private void CmbGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbGrid.SelectedItem is ComboBoxItem it)
            {
                var txt = it.Content.ToString().Replace(" px", "").Trim();
                if (int.TryParse(txt, out var g)) _owner.ApplyGridGear(g);
            }
        }

        private void SyncBgRows()
        {
            bool solid = RbSolid.IsChecked == true;
            TxtBgSolid.IsEnabled = solid;
            Swatch.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;
            TxtBgImage.IsEnabled = !solid;
            BtnBrowseBg.IsEnabled = !solid;
        }

        private void UpdateSwatch()
        {
            try { Swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(TxtBgSolid.Text.Trim())); }
            catch { Swatch.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }
        }

        private void BtnBrowseBg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Loc.T("dlg.bgImage.filter"), Title = Loc.T("dlg.bgImage.title") };
            if (dlg.ShowDialog() == true) TxtBgImage.Text = dlg.FileName;
        }

        private void BtnApplyBg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RbSolid.IsChecked == true)
                {
                    var c = (Color)ColorConverter.ConvertFromString(TxtBgSolid.Text.Trim());
                    _owner.ApplyBackground("solid", TxtBgSolid.Text.Trim());
                }
                else
                {
                    var path = TxtBgImage.Text.Trim();
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        throw new Exception(Loc.T("msg.bgPathInvalid"));
                    _owner.ApplyBackground("image", path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("msg.bgFail", ex.Message), Loc.T("settings.applyBg"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateToggleLabel() =>
            BtnToggle.Content = (_owner.Visibility == Visibility.Visible) ? Loc.T("settings.hide") : Loc.T("settings.show");

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = Loc.T("dlg.profileExport.filter"), FileName = _owner.ActiveProfile + ".lumenprofile" };
            if (dlg.ShowDialog() == true) _owner.ExportActiveProfile(dlg.FileName);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Loc.T("dlg.profileExport.filter"), Title = Loc.T("dlg.profileImport.title") };
            if (dlg.ShowDialog() == true) _owner.ImportProfileFile(dlg.FileName);
        }
    }
}
