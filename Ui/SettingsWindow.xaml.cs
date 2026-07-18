using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    public partial class SettingsWindow : ChromeWindow
    {
        private readonly LumenWindow _owner;
        private bool _loading;
        private readonly ObservableCollection<string> _coverDirs = new ObservableCollection<string>();

        public SettingsWindow(LumenWindow owner)
        {
            InitializeComponent();
            _owner = owner;
            LstCoverDirs.ItemsSource = _coverDirs;
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
            BtnAddCoverDir.Click += (s, e) => AddCoverDir();
            BtnRemoveCoverDir.Click += (s, e) => RemoveCoverDir();
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
            TitleText = Loc.T("settings.title");
            ChkAutostart.Content = Loc.T("settings.autostart");
            TxtAutostartHint.Text = Loc.T("settings.autostart.hint");
            TxtVisibility.Text = Loc.T("settings.visibility");
            UpdateToggleLabel();
            TxtHotkey.Text = Loc.T("settings.hotkey");
            TxtMulti.Text = Loc.T("settings.multi");
            TxtMultiTip.Text = Loc.T("settings.multi.tip");
            TxtCover.Text = Loc.T("settings.cover");
            BtnAddCoverDir.Content = Loc.T("settings.cover.add");
            BtnRemoveCoverDir.Content = Loc.T("settings.cover.remove");
            TxtCoverTip.Text = Loc.T("settings.cover.tip");
            BtnClose.Content = Loc.T("settings.close");
            CmbLang.SelectedValue = Loc.Cur;
        }

        private void LoadCurrent()
        {
            _loading = true;
            ChkAutostart.IsChecked = Autostart.Enabled;
            LoadCoverDirs();
            _loading = false;
            UpdateToggleLabel();
        }

        private void UpdateToggleLabel() =>
            BtnToggle.Content = (_owner.Visibility == Visibility.Visible) ? Loc.T("settings.hide") : Loc.T("settings.show");

        // ---- 媒体封面缓存目录（SMTC 无封面时回退扫描）----

        private void LoadCoverDirs()
        {
            _coverDirs.Clear();
            var saved = AppSettings.Instance.CoverCacheDirs;
            if (saved != null)
                foreach (var d in saved)
                    if (!string.IsNullOrWhiteSpace(d) && !_coverDirs.Contains(d)) _coverDirs.Add(d);
        }

        private void AddCoverDir()
        {
            var handle = new WindowInteropHelper(this).Handle;
            var path = FolderPicker.Pick(Loc.T("settings.cover.add"), handle);
            if (string.IsNullOrWhiteSpace(path)) return;
            if (_coverDirs.Contains(path)) return;
            _coverDirs.Add(path);
            PersistCoverDirs();
        }

        private void RemoveCoverDir()
        {
            if (LstCoverDirs.SelectedItem is string sel)
            {
                _coverDirs.Remove(sel);
                PersistCoverDirs();
            }
        }

        private void PersistCoverDirs()
        {
            AppSettings.Instance.CoverCacheDirs = new System.Collections.Generic.List<string>(_coverDirs);
            AppSettings.Instance.Save();
        }
    }
}
