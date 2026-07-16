using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Lumen.Core;
using Lumen.Pages;
using Lumen.Persistence;

namespace Lumen.Ui
{
    /// <summary>
    /// P6-02 设置面板：独立 Topmost 窗口（不污染覆盖层主窗，浮于其上）。
    /// 配置项：自启 / 网格间距 / 当前页背景 / 覆盖层显隐 / 配置导入导出。
    /// 多屏为 v2 占位。
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
            var dlg = new OpenFileDialog { Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*", Title = "选择背景图片" };
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
                        throw new Exception("图片路径无效或文件不存在");
                    _owner.ApplyBackground("image", path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "背景应用失败：" + ex.Message, "背景", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateToggleLabel() =>
            BtnToggle.Content = (_owner.Visibility == Visibility.Visible) ? "隐藏覆盖层" : "显示覆盖层";

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Lumen 配置档|*.json|所有文件|*.*", FileName = _owner.ActiveProfile + ".lumenprofile" };
            if (dlg.ShowDialog() == true) _owner.ExportActiveProfile(dlg.FileName);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Lumen 配置档|*.json|所有文件|*.*", Title = "导入配置档" };
            if (dlg.ShowDialog() == true) _owner.ImportProfileFile(dlg.FileName);
        }
    }
}
