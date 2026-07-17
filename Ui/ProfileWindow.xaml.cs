using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Lumen;
using Lumen.I18n;
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

        public ProfileWindow()
        {
            InitializeComponent();
            Loc.LangChanged += OnLangChanged;
            Closing += (s, e) => Loc.LangChanged -= OnLangChanged;
        }

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
            var name = InputBox.Show(this, Loc.T("profile.newTitle"), Loc.T("profile.namePrompt"), Loc.T("profile.defaultName"));
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
            var name = InputBox.Show(this, Loc.T("profile.renameTitle"), Loc.T("profile.newNamePrompt"), old);
            if (string.IsNullOrWhiteSpace(name)) return;
            _owner.RenameProfile(old, name.Trim());
            Reload();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var name = SelectedName();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show(this, Loc.T("profile.deleteConfirm", name), Loc.T("profile.deleteTitle"),
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
                Filter = Loc.T("dlg.profileExport.filter"),
                FileName = name + ".lumenprofile",
                Title = Loc.T("profile.exportTitle")
            };
            if (dlg.ShowDialog() == true) ConfigStore.ExportProfile(name, dlg.FileName);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Loc.T("dlg.profileExport.filter"), Title = Loc.T("profile.importTitle") };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var n = ConfigStore.ImportProfileFromFile(dlg.FileName, null);
                Reload();
                MessageBox.Show(this, Loc.T("profile.importDone", n), Loc.T("profile.importDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("profile.importFail") + ex.Message, Loc.T("profile.importFailTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void OnLangChanged(object sender, EventArgs e) => Reload();
    }
}
