using System;
using System.Windows;
using System.Windows.Controls;
using Lumen.Core;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 程序设置面板：独立 Topmost 窗口。仅承载「程序级」配置项：界面语言 / 开机自启动 / 覆盖层显隐。
    /// 页面级设置（网格、背景、页面管理、预设）统一在 PageGridBgWindow；
    /// 配置档的导入/导出统一在 ProfileWindow。
    /// 所有文案经 Loc（zh-CN / en-GB），切换语言后本窗通过 LangChanged 自刷新。
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
            BtnToggle.Click += (s, e) => { _owner.ToggleVisibility(); UpdateToggleLabel(); };
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
            TxtVisibility.Text = Loc.T("settings.visibility");
            UpdateToggleLabel();
            TxtHotkey.Text = Loc.T("settings.hotkey");
            TxtMulti.Text = Loc.T("settings.multi");
            TxtMultiTip.Text = Loc.T("settings.multi.tip");
            BtnClose.Content = Loc.T("settings.close");
            CmbLang.SelectedValue = Loc.Cur;
        }

        private void LoadCurrent()
        {
            _loading = true;
            ChkAutostart.IsChecked = Autostart.Enabled;
            _loading = false;
            UpdateToggleLabel();
        }

        private void UpdateToggleLabel() =>
            BtnToggle.Content = (_owner.Visibility == Visibility.Visible) ? Loc.T("settings.hide") : Loc.T("settings.show");
    }
}
