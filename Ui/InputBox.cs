using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>轻量模态输入框：单一文本字段 + 确定/取消。用于「另存为场景预设」等取名的场景。</summary>
    public partial class InputBox : Window
    {
        private readonly TextBox _tb;

        public string Answer => (_tb.Text ?? "").Trim();

        public InputBox(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 360; Height = 150;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            Foreground = Brushes.White;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0)
            };
            var sp = new StackPanel { Margin = new Thickness(14) };
            sp.Children.Add(new TextBlock
            {
                Text = prompt,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _tb = new TextBox
            {
                Text = defaultValue,
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                Padding = new Thickness(4)
            };
            sp.Children.Add(_tb);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ok = new Button { Content = Loc.T("input.ok"), Width = 72, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4) };
            var cancel = new Button { Content = Loc.T("input.cancel"), Width = 72, Padding = new Thickness(0, 4, 0, 4) };
            ok.Click += (s, e) => { DialogResult = true; };
            cancel.Click += (s, e) => { DialogResult = false; };
            _tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { DialogResult = true; }
                else if (e.Key == Key.Escape) { DialogResult = false; }
            };
            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);
            sp.Children.Add(btnRow);

            border.Child = sp;
            Content = border;
            Loaded += (s, e) => _tb.Focus();
        }

        /// <summary>显示模态输入框，返回用户输入（取消/关闭返回 null）。</summary>
        public static string Show(Window owner, string title, string prompt, string def = "")
        {
            var dlg = new InputBox(title, prompt, def) { Owner = owner };
            var res = dlg.ShowDialog();
            return res == true ? dlg.Answer : null;
        }
    }
}
