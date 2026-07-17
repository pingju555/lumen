using System.Collections.Generic;

namespace Lumen.Atoms
{
    /// <summary>可编辑字段的输入类型（属性编辑器据此选择控件）。</summary>
    public enum EditKind { Text, Color, Number, Choice, File, Bool, Slider }

    /// <summary>字段归类，用于属性编辑器分页（单选标签页）。</summary>
    public enum FieldCategory { Content, Layout, Animation }

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
        /// <summary>归类（决定落在属性编辑器的哪个标签页）。默认 Content。</summary>
        public FieldCategory Category = FieldCategory.Content;
    }
}
