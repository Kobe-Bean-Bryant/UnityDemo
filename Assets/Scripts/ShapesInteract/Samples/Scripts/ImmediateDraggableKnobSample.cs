using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 立即模式拖拽示例：一条轨道线 + 一个可沿线拖动的圆点把手，输出 0..1 的归一化值。
    /// <para>
    /// 演示更复杂的交互（Down + Drag）与 <see cref="SortingOrder"/> 的用法——把它设大一点，
    /// 让把手压在其它重叠目标之上优先被命中。命中区同样用代码定义（把手圆 + 轨道附近的带状区域）。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Shapes UI/Samples/Immediate Draggable Knob (code)")]
    public class ImmediateDraggableKnobSample : ImmediateModeShapeDrawer,
        IShapesRaycastTarget,
        IShapesPointerDownHandler, IShapesDragHandler
    {
        [Header("Geometry")]
        [SerializeField]
        private float trackLength = 4f;
        [SerializeField]
        private float trackThickness = 0.06f;
        [SerializeField]
        private float knobRadius = 0.3f;
        [Tooltip("命中重叠时数值大者优先；把手通常设大一点以压在其它目标之上。")]
        [SerializeField]
        private int sortingOrder = 10;

        [Header("Value")]
        [Range(0f, 1f)]
        [SerializeField]
        private float value = 0.5f;

        [Header("Colors")]
        [SerializeField]
        private Color trackColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField]
        private Color knobColor = new Color(0f, 0.584f, 1f);

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        /// <summary>当前归一化值 0..1。</summary>
        public float Value => value;

        private float HalfLength => trackLength * 0.5f;
        private float KnobX => Mathf.Lerp(-HalfLength, HalfLength, value);

        public override void OnEnable()
        {
            base.OnEnable();
            ShapesInteractionManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ShapesInteractionManager.Unregister(this);
        }

        // 命中区：把手圆，或轨道附近一条带状区域（用 knobRadius 当作可抓取的厚度）。
        public bool ContainsLocalPoint(Vector2 p)
            => ShapesHitArea.Circle(p, new Vector2(KnobX, 0f), knobRadius)
               || (Mathf.Abs(p.x) <= HalfLength && Mathf.Abs(p.y) <= knobRadius);

        public void OnPointerDown(ShapesPointerEvent e) => SetFromLocalX(e.LocalPoint.x);
        public void OnDrag(ShapesPointerEvent e) => SetFromLocalX(e.LocalPoint.x);

        private void SetFromLocalX(float localX)
        {
            float clamped = Mathf.Clamp(localX, -HalfLength, HalfLength);
            value = Mathf.InverseLerp(-HalfLength, HalfLength, clamped);
        }

        public override void DrawShapes(Camera cam)
        {
            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;
                Draw.Thickness = trackThickness;
                Draw.Line(new Vector3(-HalfLength, 0f, 0f), new Vector3(HalfLength, 0f, 0f), trackColor);
                Draw.Disc(new Vector3(KnobX, 0f, 0f), knobRadius, knobColor);
            }
        }
    }
}
