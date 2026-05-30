using Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 一个 Shapes 按钮：在 <see cref="ShapesSelectable"/>（命中 + 四态变色）基础上加 <see cref="onClick"/>。
    /// 通过 <c>RequireComponent</c> 自动带一个 <see cref="Rectangle"/> 作为外观与命中区，
    /// 所以「Add Component → Shapes Button」即得一个可用按钮。
    /// </summary>
    [AddComponentMenu("Shapes UI/Shapes Button")]
    [RequireComponent(typeof(Rectangle))]
    public class ShapesButton : ShapesSelectable, IShapesPointerClickHandler
    {
        [Header("Button")]
        public UnityEvent onClick = new UnityEvent();

        public void OnPointerClick(ShapesPointerEvent e)
        {
            if (!IsInteractable) return;
            onClick?.Invoke();
        }
    }
}
