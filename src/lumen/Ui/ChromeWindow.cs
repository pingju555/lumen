using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace Lumen.Ui
{
    /// <summary>
    /// 统一窗口基类。三段式：TitleBar(36) + ContentArea(弹性) + Footer(48)。
    /// 视觉模板定义在 Theme.xaml。窗口继承此类自动获得统一外观。
    /// 支持无边框（WindowStyle=None + AllowsTransparency=True）下的拖拽与边缘缩放：
    /// 标题栏拖拽由模板 PART_TitleBar 的 MouseLeftButtonDown→DragMove 处理；
    /// 边缘缩放在 ResizeMode=CanResize 时通过 WM_NCHITTEST 命中测试实现（NoResize 窗口不受影响）。
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
            // 默认窗口属性（Style 里的 WindowStyle/AllowsTransparency 负责隐藏系统窗口 chrome）
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            SourceInitialized += ChromeWindow_SourceInitialized;
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

        // ========== 无边框缩放（仅 CanResize 时启用） ==========
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int RESIZE_EDGE = 6;

        private void ChromeWindow_SourceInitialized(object sender, EventArgs e)
        {
            if (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var src = HwndSource.FromHwnd(hwnd);
                if (src != null) src.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST) return IntPtr.Zero;

            // lParam 低位=屏幕X，高位=屏幕Y
            int x = (short)(lParam.ToInt32() & 0xFFFF);
            int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
            var pt = PointFromScreen(new Point(x, y));
            double w = ActualWidth, h = ActualHeight;

            // 标题栏区域：交还给 MouseLeftButtonDown→DragMove（返回 HTCLIENT，不拦截）
            if (pt.Y <= GetTitleBarHeight())
            {
                handled = false;
                return IntPtr.Zero;
            }

            bool left = pt.X <= RESIZE_EDGE;
            bool right = pt.X >= w - RESIZE_EDGE;
            bool top = pt.Y <= RESIZE_EDGE;
            bool bottom = pt.Y >= h - RESIZE_EDGE;

            int ht;
            if (top && left) ht = HTTOPLEFT;
            else if (top && right) ht = HTTOPRIGHT;
            else if (bottom && left) ht = HTBOTTOMLEFT;
            else if (bottom && right) ht = HTBOTTOMRIGHT;
            else if (left) ht = HTLEFT;
            else if (right) ht = HTRIGHT;
            else if (top) ht = HTTOP;
            else if (bottom) ht = HTBOTTOM;
            else ht = HTCLIENT;

            handled = true;
            return new IntPtr(ht);
        }

        private double GetTitleBarHeight()
        {
            if (Template?.FindName("PART_TitleBar", this) is FrameworkElement tb && tb.ActualHeight > 0)
                return tb.ActualHeight;
            return 36;
        }
    }
}
