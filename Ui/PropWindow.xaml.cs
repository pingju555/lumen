using System;
using System.Windows;
using System.Windows.Input;
using Lumen.Atoms;
using Lumen.Formula;
using Lumen.Globals;
using Lumen.I18n;

namespace Lumen.Ui
{
    /// <summary>
    /// 属性编辑窗口（P6-03，替代 EditAtom/CreateEditorWindow）：
    /// 嵌入 PropertyEditorPanel，隐藏 OK/Cancel 按钮，由本窗口提供「应用」按钮。
    /// 与 TreeWindow 双向联动：树选中原子后加载其属性。
    /// </summary>
    public partial class PropWindow : Window
    {
        private GvStore _gv;
        private EvalContext _ctx;
        private Atom _currentAtom;
        private PropertyEditorPanel _panel;
        private Action _onApply;
        private Action _externalPreview;
        private Action _externalStructural;

        /// <summary>可选的树节点切换确认回调（外部由 TreeWindow 注入）。</summary>
        public Func<Atom, bool> BeforeAtomSwitch { get; set; }

        public PropWindow()
        {
            InitializeComponent();
        }

        /// <summary>初始化上下文（外部注入）。</summary>
        public void InitContext(GvStore gv, EvalContext ctx)
        {
            _gv = gv;
            _ctx = ctx;
        }

        /// <summary>加载指定原子到编辑器。</summary>
        public void LoadAtom(Atom atom)
        {
            _currentAtom = atom;
            TitleTb.Text = Loc.T("propwin.editing", atom?.Type ?? Loc.T("propwin.unknown"));

            // 隐藏占位、显示面板
            Placeholder.Visibility = atom == null ? Visibility.Visible : Visibility.Collapsed;
            PanelHost.Visibility = atom == null ? Visibility.Collapsed : Visibility.Visible;

            if (atom == null)
            {
                PanelHost.Content = null;
                _panel = null;
                return;
            }

            _panel = new PropertyEditorPanel(
                atom: atom,
                onPreview: () => { _externalPreview?.Invoke(); },
                onStructuralChange: () => { _externalStructural?.Invoke(); },
                onCommit: () => { },
                onCancel: () => { },
                gv: _gv,
                onOpenGvManager: () => { },
                ctx: _ctx
            );
            _panel.HideButtons();
            PanelHost.Content = _panel;
        }

        /// <summary>获取当前面板，用于外部触发 Apply。</summary>
        public void ApplyCurrent()
        {
            _panel?.Apply();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _panel?.Apply();
            _onApply?.Invoke();
        }

        /// <summary>设置外部回调（由 LumenWindow 注入）。</summary>
        public void SetCallbacks(Action onPreview, Action onStructural)
        {
            _externalPreview = onPreview;
            _externalStructural = onStructural;
        }

        /// <summary>设置应用后回调。</summary>
        public void SetOnApply(Action onApply) => _onApply = onApply;

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            // 清空选中（外部调用 LoadAtom(null) 触发 UI 更新）
            LoadAtom(null);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
