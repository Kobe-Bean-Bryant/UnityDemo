using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 立即模式按钮示例：<b>一个脚本既绘制又交互</b>。
    /// <para>
    /// 它继承 <see cref="ImmediateModeShapeDrawer"/>（在 <see cref="DrawShapes"/> 里用代码画），
    /// 同时实现 <see cref="IShapesRaycastTarget"/> 与若干 handler 接口。命中用的矩形与绘制用的矩形
    /// 是<b>同一份几何</b>（同一个 <see cref="size"/>），保证「看到的」就是「能点的」。
    /// </para>
    /// <para>
    /// 用法：把本脚本挂到任意空 GameObject 即可（无需任何 Shapes 组件）；场景里需有一个
    /// <see cref="ShapesInteractionManager"/>（类比 uGUI 的 EventSystem）。它与组件模式控件共用同一个 Manager。
    /// </para>
    /// </summary>
    [ExecuteAlways] // 让编辑器里也能预览绘制（交互仍只在运行时由 Manager 触发）
    [AddComponentMenu("Shapes UI/Samples/Immediate Button (code)")]
    public class ImmediateButtonSample : ImmediateModeShapeDrawer,
        IShapesRaycastTarget,
        IShapesPointerEnterHandler, IShapesPointerExitHandler,
        IShapesPointerDownHandler, IShapesPointerUpHandler,
        IShapesPointerClickHandler
    {
        [Header("Geometry")]
        [SerializeField]
        private Vector2 size = new Vector2(3f, 1f);
        [SerializeField]
        private float cornerRadius = 0.15f;
        [Tooltip("命中重叠时数值大者优先。")]
        [SerializeField]
        private int sortingOrder;

        [Header("State Colors")]
        [SerializeField]
        private Color normalColor = Color.white;
        [SerializeField]
        private Color hoverColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField]
        private Color pressedColor = new Color(0.6f, 0.6f, 0.6f);

        private bool _hovered;
        private bool _pressed;

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        public override void OnEnable()
        {
            base.OnEnable(); // 务必调用：注册 Shapes 渲染回调
            ShapesInteractionManager.Register(this); // 务必注册到 Manager
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ShapesInteractionManager.Unregister(this); // 务必注销，避免悬空引用
        }

        // 命中区与绘制用同一份 size：以本物体中心为原点的矩形。
        public bool ContainsLocalPoint(Vector2 localPoint)
            => new Rect(-size * 0.5f, size).Contains(localPoint);

        public void OnPointerEnter(ShapesPointerEvent e) => _hovered = true;
        public void OnPointerExit(ShapesPointerEvent e) => _hovered = false;
        public void OnPointerDown(ShapesPointerEvent e) => _pressed = true;
        public void OnPointerUp(ShapesPointerEvent e) => _pressed = false;

        public void OnPointerClick(ShapesPointerEvent e)
            => Debug.Log($"[Immediate Button] clicked, local = {e.LocalPoint}");

        public override void DrawShapes(Camera cam)
        {
            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix; // 让绘制跟随本物体的 Transform
                Color c = _pressed ? pressedColor : _hovered ? hoverColor : normalColor;
                Draw.Rectangle(Vector3.zero, size, cornerRadius, c);
            }
        }
    }
}
