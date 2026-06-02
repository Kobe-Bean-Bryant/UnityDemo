using Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 一个 Shapes 开关：点击在开/关之间翻转，并显隐 <see cref="checkmark"/> 图形。
    /// </summary>
    [AddComponentMenu("Shapes UI/Shapes Toggle")]
    [RequireComponent(typeof(Rectangle))]
    public class ShapesToggle : ShapesSelectable, IShapesPointerClickHandler
    {
        /// <summary>可序列化的 bool 事件（Unity 不能直接序列化泛型 UnityEvent&lt;T&gt;）。</summary>
        [System.Serializable] public class BoolEvent : UnityEvent<bool> { }

        [Header("Toggle")]
        [SerializeField] private bool isOn;
        [Tooltip("勾选状态显示的图形（开时显示、关时隐藏）。")]
        [SerializeField] private ShapeRenderer checkmark;
        public BoolEvent onValueChanged = new BoolEvent();

        /// <summary>开关状态。设置时会更新视觉并触发 <see cref="onValueChanged"/>。</summary>
        public bool IsOn
        {
            get => isOn;
            set => SetIsOn(value, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyCheckmark();
        }

        protected override void ApplySortingOrder()
        {
            base.ApplySortingOrder();                       // 底图 = SortingOrder
            if (checkmark != null) checkmark.SortingOrder = SortingOrder + 1;   // 勾在底图之上
        }

        public void OnPointerClick(ShapesPointerEvent e)
        {
            if (!IsInteractable) return;
            SetIsOn(!isOn, true);
        }

        /// <summary>不触发事件地设置状态（用于代码同步 UI）。</summary>
        public void SetIsOnWithoutNotify(bool value) => SetIsOn(value, false);

        private void SetIsOn(bool value, bool notify)
        {
            if (isOn != value)
            {
                isOn = value;
                if (notify) onValueChanged?.Invoke(isOn);
            }
            ApplyCheckmark();
        }

        private void ApplyCheckmark()
        {
            if (checkmark != null) checkmark.enabled = isOn;
        }
    }
}
