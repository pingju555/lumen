using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lumen.Ui
{
    /// <summary>
    /// 统一窗口基类。三段式：TitleBar(36) + ContentArea(弹性) + Footer(48)。
    /// 视觉模板定义在 Theme.xaml。窗口继承此类自动获得统一外观。
    /// </summary>
    public class ChromeWindow : Window
    {
        // ---- Dependency Properties ----

        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(ChromeWindow),
                new PropertyMetadata(""));

        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }

        public static readonly DependencyProperty FooterContentProperty =
            DependencyProperty.Register(nameof(FooterContent), typeof(object), typeof(ChromeWindow),
                new PropertyMetadata(null));

        public object FooterContent
        {
            get => GetValue(FooterContentProperty);
            set => SetValue(FooterContentProperty, value);
        }

        /// <summary>TitleBar 右侧的辅助操作（如"清除"按钮）</summary>
        public static readonly DependencyProperty TitleActionContentProperty =
            DependencyProperty.Register(nameof(TitleActionContent), typeof(object), typeof(ChromeWindow),
                new PropertyMetadata(null));

        public object TitleActionContent
        {
            get => GetValue(TitleActionContentProperty);
            set => SetValue(TitleActionContentProperty, value);
        }

        public static readonly DependencyProperty TabBarContentProperty =
            DependencyProperty.Register(nameof(TabBarContent), typeof(object), typeof(ChromeWindow),
                new PropertyMetadata(null));

        public object TabBarContent
        {
            get => GetValue(TabBarContentProperty);
            set => SetValue(TabBarContentProperty, value);
        }

        public static readonly DependencyProperty ShowCloseButtonProperty =
            DependencyProperty.Register(nameof(ShowCloseButton), typeof(bool), typeof(ChromeWindow),
                new PropertyMetadata(true));

        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
        }

        // ---- Template parts ----

        private Border _titleBar;
        private Button _closeBtn;

        static ChromeWindow()
        {
        }

        public ChromeWindow()
        {
            // 默认窗口属性（Style 里的 #WindowStyle/AllowsTransparency 负责隐藏系统窗口chrome）
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _titleBar = GetTemplateChild("PART_TitleBar") as Border;
            _closeBtn = GetTemplateChild("PART_CloseButton") as Button;

            if (_titleBar != null)
                _titleBar.MouseLeftButtonDown += TitleBar_MouseDown;
            if (_closeBtn != null)
                _closeBtn.Click += (_, _) => Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
