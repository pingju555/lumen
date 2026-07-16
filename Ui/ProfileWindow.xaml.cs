using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Lumen;
using Lumen.Persistence;

namespace Lumen.Ui
{
    /// <summary>
    /// 配置档（Profile）管理器：列出所有 profile，支持新建 / 切换 / 重命名 / 删除 / 导出 / 导入。
    /// 一个 profile = 全部页面（含部件）+ 每页设置 + 全局变量 + 用户预设，整体可切换、可独立导出分享。
    /// </summary>
    public partial class ProfileWindow : Window
    {
        private LumenWindow _owner;

        public ProfileWindow() { InitializeComponent(); }

        public void Init(LumenWindow owner)
        {
            _owner = owner;
            Reload();
        }

        private void Reload()
        {
            LstProfiles.Items.Clear();
            var active = _owner.ActiveProfile;
            foreach (var name in ConfigStore.ListProfiles())
                LstProfiles.Items.Add((name == active ? "● " : "   ") + name);
            for (int i = 0; i < LstProfiles.Items.Count; i++)
                if (LstProfiles.Items[i] is string s && s.Trim() == active) { LstProfiles.SelectedIndex = i; break; }
        }

        private string SelectedName()
        {
            if (LstProfiles.SelectedItem is not string s) return null;
            return s.Trim();
        }

        private void LstProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var name = InputBox.Show(this, "新建配置档", "配置档名称：", "我的配置档");
            if (string.IsNullOrWhiteSpace(name)) return;
            _owner.NewProfile(name.Trim());
            Reload();
        }

        private void BtnSwitch_Click(object sender, RoutedEventArgs e)
        {
            var name = SelectedName();
            if (string.IsNullOrWhiteSpace(name)) return;
            _owner.SwitchProfile(name);
            Reload();
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            var old = SelectedName();
            if (string.IsNullOrWhiteSpace(old)) return;
            var name = InputBox.Show(this, "重命名配置档", "新名称：", old);
            if (string.IsNullOrWhiteSpace(name)) return;
            _owner.RenameProfile(old, name.Trim());
            Reload();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var name = SelectedName();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show(this, $"确定删除配置档「{name}」？此操作不可撤销。", "删除配置档",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _owner.DeleteProfile(name);
            Reload();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var name = SelectedName();
            if (string.IsNullOrWhiteSpace(name)) return;
            var dlg = new SaveFileDialog
            {
                Filter = "Lumen 配置档|*.json|所有文件|*.*",
                FileName = name + ".lumenprofile",
                Title = "导出配置档"
            };
            if (dlg.ShowDialog() == true) ConfigStore.ExportProfile(name, dlg.FileName);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Lumen 配置档|*.json|所有文件|*.*", Title = "导入配置档" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var n = ConfigStore.ImportProfileFromFile(dlg.FileName, null);
                Reload();
                MessageBox.Show(this, $"已导入「{n}」。可在列表中切换使用。", "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
