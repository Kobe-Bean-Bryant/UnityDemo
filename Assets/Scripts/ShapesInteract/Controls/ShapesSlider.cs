using Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 一个 Shapes 滑块：按下/拖拽轨道即设定数值。约定（创建菜单会自动按此搭好层级）：
    /// <list type="bullet">
    /// <item>轨道 = 本物体上的 <see cref="Rectangle"/>（即 targetGraphic，也是命中区）。</item>
    /// <item><see cref="fill"/>（可选）= 左对齐（Corner pivot、贴在轨道左缘）的填充 Rectangle，宽度随值变化。</item>
    /// <item><see cref="handle"/>（可选）= 把手 Transform，x 随值在轨道内移动。</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("Shapes UI/Shapes Slider")]
    [RequireComponent(typeof(Rectangle))]
    public class ShapesSlider : ShapesSelectable, IShapesDragHandler
    {
        /// <summary>可序列化的 float 事件。</summary>
        [System.Serializable]
        public class FloatEvent : UnityEvent<float>
        {
        }

        [Header("Slider")]
        [SerializeField]
        private Rectangle fill;
        [SerializeField]
        private Transform handle;
        [SerializeField]
        private float minValue = 0f;
        [SerializeField]
        private float maxValue = 1f;
        [SerializeField]
        private float _value = 0f;
        public FloatEvent onValueChanged = new FloatEvent();

        /// <summary>当前值（clamp 在 min~max）。设置会更新视觉并触发事件。</summary>
        public float Value
        {
            get => _value;
            set => SetValue(value, true);
        }

        /// <summary>归一化值 0..1。</summary>
        public float NormalizedValue
            => Mathf.Approximately(maxValue, minValue) ? 0f : Mathf.InverseLerp(minValue, maxValue, _value);

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyVisuals();
        }

        protected override void ApplySortingOrder()
        {
            base.ApplySortingOrder();                       // 轨道 = SortingOrder
            if (fill != null) fill.SortingOrder = SortingOrder + 1;          // 填充在轨道之上
            if (handle != null)
                foreach (var r in handle.GetComponentsInChildren<ShapeRenderer>(true))
                    r.SortingOrder = SortingOrder + 2;                       // 把手在最上
        }

        public override void OnPointerDown(ShapesPointerEvent e)
        {
            base.OnPointerDown(e);
            if (IsInteractable) SetValueFromLocalX(e.LocalPoint.x, true);
        }

        public void OnDrag(ShapesPointerEvent e)
        {
            if (IsInteractable) SetValueFromLocalX(e.LocalPoint.x, true);
        }

        /// <summary>不触发事件地设置值（用于代码同步 UI）。</summary>
        public void SetValueWithoutNotify(float value) => SetValue(value, false);

        private void SetValueFromLocalX(float localX, bool notify)
        {
            if (targetGraphic == null) return;
            Bounds b = targetGraphic.GetBounds();
            float t = Mathf.Approximately(b.max.x, b.min.x) ? 0f : Mathf.InverseLerp(b.min.x, b.max.x, localX);
            SetValue(Mathf.Lerp(minValue, maxValue, t), notify);
        }

        private void SetValue(float value, bool notify)
        {
            float clamped = Mathf.Clamp(value, Mathf.Min(minValue, maxValue), Mathf.Max(minValue, maxValue));
            if (!Mathf.Approximately(clamped, _value))
            {
                _value = clamped;
                if (notify) onValueChanged?.Invoke(_value);
            }

            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (targetGraphic == null) return;
            Bounds track = targetGraphic.GetBounds();
            float t = NormalizedValue;

            if (handle != null)
            {
                Vector3 hp = handle.localPosition;
                hp.x = Mathf.Lerp(track.min.x, track.max.x, t);
                handle.localPosition = hp;
            }

            if (fill != null)
            {
                fill.Width = (track.max.x - track.min.x) * t;

                // 用 fill 自身的局部 AABB 推导位置：把左缘钉在轨道左缘、竖直中心对齐轨道中心。
                // GetBounds() 已计入 fill 的 pivot，因此无论 fill 是 Center 还是 Corner pivot、
                // 也无论改变其尺寸，都不会错位。
                Bounds fb = fill.GetBounds();
                Vector3 lp = fill.transform.localPosition;
                lp.x = track.min.x - fb.min.x;
                lp.y = track.center.y - fb.center.y;
                fill.transform.localPosition = lp;
            }
        }
    }
}
