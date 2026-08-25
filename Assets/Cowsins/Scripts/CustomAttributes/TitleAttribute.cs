// TitleAttribute.cs：标题属性特性。
// 用于在 Unity Inspector 中为被标注的字段显示一个可配置的标题标签，
// 可选启用分隔线，并支持设置标题与属性之间的上边距（upMargin），常用于对属性面板分组装饰。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 标题特性：在 Inspector 面板中为被标注的字段渲染一个标题，可配置是否显示分隔线以及标题与属性间的上边距。
    /// </summary>
    public class TitleAttribute : PropertyAttribute
    {
        // Title Label 
        // 标题显示的文本内容。
        public string title;
        // Enables a divider, true by default
        // 是否在标题下方启用分隔线，默认值为 true。
        public bool divider;
        //  Separation Between the Title and the Property
        // 标题与下方属性之间的上边距（用于调整 Inspector 中的间距）。
        public float upMargin;
        // 构造标题特性，接收标题文本、是否显示分隔线以及上边距三个参数。
        public TitleAttribute(string title, bool divider = true, float upMargin = 0f)
        {
            this.title = title;
            this.divider = divider;
            this.upMargin = upMargin;
        }
    }
}