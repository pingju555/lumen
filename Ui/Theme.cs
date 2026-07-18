using System.Windows;
using System.Windows.Media;

namespace Lumen.Ui
{
    /// <summary>
    /// 设计令牌的 C# 访问入口。代码构建的弹窗（非 XAML）通过它引用与 Theme.xaml 同源的画刷，
    /// 保证「单一事实来源」——改 Theme.xaml 的色值，这里与 XAML 弹窗同步生效。
    /// </summary>
    public static class Theme
    {
        public static SolidColorBrush BgBase          => Get("BgBase", 0x1E, 0x1E, 0x1E);
        public static SolidColorBrush BgSurface       => Get("BgSurface", 0x2D, 0x2D, 0x30);
        public static SolidColorBrush BgSunken        => Get("BgSunken", 0x25, 0x25, 0x26);
        public static SolidColorBrush BgHover         => Get("BgHover", 0x3A, 0x3D, 0x41);
        public static SolidColorBrush BgActive        => Get("BgActive", 0x00, 0x7A, 0xCC);
        public static SolidColorBrush BgActivePressed  => Get("BgActivePressed", 0x00, 0x5A, 0x99);
        public static SolidColorBrush BorderDefault    => Get("BorderDefault", 0x3F, 0x3F, 0x46);
        public static SolidColorBrush BorderSoft       => Get("BorderSoft", 0x55, 0x55, 0x55);
        public static SolidColorBrush TextPrimary      => Get("TextPrimary", 0xFF, 0xFF, 0xFF);
        public static SolidColorBrush TextSecondary    => Get("TextSecondary", 0xE6, 0xE6, 0xE6);
        public static SolidColorBrush TextTertiary     => Get("TextTertiary", 0x9A, 0x9A, 0x9A);
        public static SolidColorBrush TextDisabled     => Get("TextDisabled", 0x6A, 0x6A, 0x6A);
        public static SolidColorBrush Accent          => Get("Accent", 0x00, 0x7A, 0xCC);
        public static SolidColorBrush OkGreen         => Get("OkGreen", 0x6A, 0xD1, 0x7A);
        public static SolidColorBrush ErrRed          => Get("ErrRed", 0xE5, 0x4B, 0x4B);
        public static SolidColorBrush UsedGreen       => Get("UsedGreen", 0x34, 0xD3, 0x99);

        private static SolidColorBrush Get(string key, byte r, byte g, byte b)
        {
            if (Application.Current != null && Application.Current.TryFindResource(key) is SolidColorBrush existing)
                return existing;
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
