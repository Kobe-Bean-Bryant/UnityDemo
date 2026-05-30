using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 交互示例的「桥」脚本：把 Button/Slider/Toggle 的事件接到这里的公开方法，
    /// 演示「交互改变一个本身不可交互的 Shape」（这里是一个 <see cref="Disc"/>）。
    /// 在 Inspector 里把对应方法拖到控件的事件上即可，无需改代码。
    /// </summary>
    [AddComponentMenu("Shapes UI/Samples/Interaction Sample Controller")]
    public class InteractionSampleController : MonoBehaviour
    {
        [Tooltip("被交互改变的目标圆（一个普通的、不可交互的 Shapes.Disc）。")]
        [SerializeField]
        private Disc targetDisc;

        [Tooltip("点击按钮时循环切换的颜色。")]
        [SerializeField]
        private Color[] palette =
        {
            new Color(1f, 1f, 1f), // 白
            new Color(1f, 0.07f, 0.33f), // 红 #FF1155
            new Color(0f, 0.58f, 1f), // 蓝 #0095FF
            new Color(0.20f, 0.80f, 0.40f), // 绿
        };

        [SerializeField]
        private float minRadius = 0.3f;
        [SerializeField]
        private float maxRadius = 1.5f;

        private int _colorIndex;

        /// <summary>接到 Button.onClick：循环切换目标圆的颜色，并打印日志。</summary>
        public void CycleDiscColor()
        {
            if (targetDisc != null && palette != null && palette.Length > 0)
            {
                _colorIndex = (_colorIndex + 1) % palette.Length;
                targetDisc.Color = palette[_colorIndex];
            }

            Debug.Log("[ShapesUI] Button clicked → cycle disc color");
        }

        /// <summary>接到 Slider.onValueChanged(float)：用 0..1 的值控制圆的半径。</summary>
        public void OnSliderChanged(float t)
        {
            if (targetDisc == null) return;
            targetDisc.Radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Clamp01(t));
        }

        /// <summary>接到 Toggle.onValueChanged(bool)：显隐目标圆。</summary>
        public void OnToggleChanged(bool isOn)
        {
            if (targetDisc != null) targetDisc.enabled = isOn;
        }
    }
}
