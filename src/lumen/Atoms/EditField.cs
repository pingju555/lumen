using System.Collections.Generic;

namespace Lumen.Atoms
{
    /// <summary>可编辑字段的输入类型（属性编辑器据此选择控件）。</summary>
    public enum EditKind { Text, Color, Number, Choice, File, Bool, Slider, Anchor }

    /// <summary>属性编辑器标签页描述：Key 用于字段归属，LocKey 为本地化键（prop.tab.&lt;Key&gt;）。</summary>
    public class TabSpec
    {
        public string Key;
        public string LocKey;
    }

    /// <summary>自定义标签页提供方：原子可实现此接口，在 EditTabs() 中追加带自定义构建器的标签页（如 Component 的变量页）。</summary>
    public interface ICustomTabProvider
    {
        System.Windows.UIElement BuildCustomTab(string key);
    }

    /// <summary>
    /// 原子可编辑属性的元数据描述（P3 部件级菜单用）。
    /// Key 与原子 GetProps/SetProps 的字典键一一对应；编辑器读取当前值→用户改→SetProps 写回。
    /// 文本/颜色/数值字段的原始串支持 $公式$ 与 gv:名称 语法（与三元组体系一致）。
    /// </summary>
    public class EditField
    {
        /// <summary>对应 GetProps/SetProps 的键（如 "text"、"fill"）。</summary>
        public string Key;
        /// <summary>编辑器左侧显示的中文标签。</summary>
        public string Label;
        /// <summary>输入控件类型。</summary>
        public EditKind Kind;
        /// <summary>Choice 下拉候选项（规范值，用于持久化/序列化，不翻译）。</summary>
        public string[] Choices;
        /// <summary>Choice 下拉项的本地化键前缀：非空时，下拉显示 Loc.T(prefix + 值)，但存储仍用规范值。空则直接显示规范值。</summary>
        public string ChoiceLocPrefix;
        /// <summary>Number 的数值范围（仅提示用，不强制）。</summary>
        public double Min, Max;
        /// <summary>帮助/占位文本（如公式语法提示）。</summary>
        public string Hint;
        /// <summary>条件显隐：仅当依赖字段(ShowIfKey)的当前值 ∈ ShowIfValues 时才显示本字段；两者皆空表示始终显示。例如形状专参可设为 ShowIfKey="kind"、ShowIfValues={"Polygon"}，选其它形状时自动隐藏。</summary>
        public string ShowIfKey;
        public string[] ShowIfValues;
        /// <summary>所属标签页 Key（决定落在属性编辑器的哪个标签页）。默认 "content"。可选：content/style/layout/animation/interaction/trigger，或原子自定义的 Key。</summary>
        public string Tab;
    }
}
