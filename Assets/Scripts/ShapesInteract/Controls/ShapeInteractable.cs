using Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 低层通用交互件：挂在任意带 <see cref="ShapeRenderer"/> 的物体上，即可「就地」让该 Shape 可交互，
    /// 命中区取其 <c>GetBounds()</c>，并通过 UnityEvent 暴露各类指针事件——无需写代码。
    /// <para>
    /// 适合「给一个本只用于绘制的 Shape 临时加点击」（见 README「给特定 Shape 加交互」一节的模式①），
    /// 而不必做成完整控件。完整控件请用 <see cref="ShapesButton"/> / <see cref="ShapesToggle"/> / <see cref="ShapesSlider"/>。
    /// </para>
    /// </summary>
    [AddComponentMenu("Shapes UI/Shape Interactable (Generic)")]
    public class ShapeInteractable : MonoBehaviour,
        IShapesRaycastTarget,
        IShapesPointerEnterHandler, IShapesPointerExitHandler,
        IShapesPointerDownHandler, IShapesPointerUpHandler,
        IShapesDragHandler, IShapesPointerClickHandler, IShapesPointerMoveHandler
    {
        /// <summary>可序列化、携带 <see cref="ShapesPointerEvent"/> 的事件。</summary>
        [System.Serializable]
        public class PointerEvent : UnityEvent<ShapesPointerEvent>
        {
        }

        [Tooltip("命中区来源；留空时自动取本物体的 ShapeRenderer。应与本组件在同一 GameObject。")]
        [SerializeField]
        private ShapeRenderer shape;
        [SerializeField]
        private int sortingOrder;

        [Header("Events")]
        public UnityEvent onClick = new UnityEvent();
        public PointerEvent onEnter = new PointerEvent();
        public PointerEvent onExit = new PointerEvent();
        public PointerEvent onDown = new PointerEvent();
        public PointerEvent onUp = new PointerEvent();
        public PointerEvent onDrag = new PointerEvent();
        public PointerEvent onMove = new PointerEvent();

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        private void Reset() => shape = GetComponent<ShapeRenderer>();

        private void OnEnable()
        {
            if (shape == null) shape = GetComponent<ShapeRenderer>();
            ShapesInteractionManager.Register(this);
        }

        private void OnDisable() => ShapesInteractionManager.Unregister(this);

        public bool ContainsLocalPoint(Vector2 localPoint)
            => shape != null && shape.GetBounds().Contains(localPoint);

        public void OnPointerEnter(ShapesPointerEvent e) => onEnter?.Invoke(e);
        public void OnPointerExit(ShapesPointerEvent e) => onExit?.Invoke(e);
        public void OnPointerDown(ShapesPointerEvent e) => onDown?.Invoke(e);
        public void OnPointerUp(ShapesPointerEvent e) => onUp?.Invoke(e);
        public void OnDrag(ShapesPointerEvent e) => onDrag?.Invoke(e);
        public void OnPointerClick(ShapesPointerEvent e) => onClick?.Invoke();
        public void OnPointerMove(ShapesPointerEvent e) => onMove?.Invoke(e);
    }
}
