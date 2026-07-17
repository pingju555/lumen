using System;
using System.Windows.Markup;

namespace Lumen.I18n
{
    /// <summary>
    /// XAML 查表标记扩展：<TextBlock Text="{loc:Loc settings.title}"/>。
    /// 在加载时解析一次；需要热切换的窗口请订阅 Loc.LangChanged 并手动重设（见 SettingsWindow）。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
            => Loc.T(Key);
    }
}
