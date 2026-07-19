using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Atoms;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 添加组件面板：卡片网格形式展示所有可创建的原子类型，
    /// 点击后创建对应原子并注入到指定页面/容器。
    /// </summary>
    public partial class AddComponentPanel : Window
    {
        /// <summary>用户选中创建的原子类型（null = 取消）。</summary>
        public string SelectedType { get; private set; }

        public AddComponentPanel()
        {
            InitializeComponent();
            BuildCards();
        }

        private void BuildCards()
        {
            var cards = new (string type, string icon, string nameLoc, string descLoc)[]
            {
                ("Text",    "T",   Loc.T("addcomp.text"),  Loc.T("addcomp.text.desc")),
                ("Shape",   "◆",   Loc.T("addcomp.shape"), Loc.T("addcomp.shape.desc")),
                ("Image",   "▣",   Loc.T("addcomp.image"), Loc.T("addcomp.image.desc")),
                ("Icon",    "✦",   Loc.T("addcomp.icon"),  Loc.T("addcomp.icon.desc")),
                ("Progress","◉",   Loc.T("addcomp.progress"), Loc.T("addcomp.progress.desc")),
                ("Stack",   "≡",   Loc.T("addcomp.stack"), Loc.T("addcomp.stack.desc")),
                ("Overlap", "⊞",   Loc.T("addcomp.overlap"), Loc.T("addcomp.overlap.desc")),
                ("Series",  "⏱",   Loc.T("addcomp.series"), Loc.T("addcomp.series.desc")),
            };

            foreach (var (type, icon, name, desc) in cards)
            {
                var card = new Border
                {
                    Width = 150, Height = 110,
                    Margin = new Thickness(6),
                    Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Cursor = Cursors.Hand,
                    Tag = type
                };
                var sp = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                sp.Children.Add(new TextBlock
                {
                    Text = icon, FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0))
                });
                sp.Children.Add(new TextBlock
                {
                    Text = desc, FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 130
                });
                card.Child = sp;

                card.MouseLeftButtonUp += (s, e) =>
                {
                    if (s is Border b && b.Tag is string t)
                    {
                        SelectedType = t;
                        DialogResult = true;
                    }
                };

                CardGrid.Children.Add(card);
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>显示面板模态弹窗，返回用户选择的类型名（null=取消）。</summary>
        public static string ShowPick(Window owner)
        {
            var dlg = new AddComponentPanel();
            if (owner != null && !owner.AllowsTransparency) dlg.Owner = owner;
            return dlg.ShowDialog() == true ? dlg.SelectedType : null;
        }
    }
}
