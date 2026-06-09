using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 触发指针事件的鼠标按钮。默认值 <c>Left = 0</c> 保证未初始化时行为与旧代码一致。
    /// </summary>
    public enum PointerButton
    {
        Left   = 0,
        Right  = 1,
        Middle = 2
    }

    /// <summary>
    /// 一次指针事件携带的数据。坐标换算全部由 <see cref="ShapesInteractionManager"/> 完成。
    /// </summary>
    public struct ShapesPointerEvent
    {
        /// <summary>屏幕像素坐标。</summary>
        public Vector2 ScreenPosition;

        /// <summary>命中点的世界坐标。</summary>
        public Vector3 WorldPoint;

        /// <summary>命中点在目标本地空间的坐标（z=0 平面）。</summary>
        public Vector2 LocalPoint;

        /// <summary>拖拽时相对上一帧的本地位移；非拖拽事件为 <see cref="Vector2.zero"/>。</summary>
        public Vector2 LocalDelta;

        /// <summary>触发本事件的命中目标。</summary>
        public IShapesRaycastTarget Target;

        /// <summary>触发本事件的鼠标按钮。默认 <see cref="PointerButton.Left"/>。</summary>
        public PointerButton Button;
    }

    // 仿 uGUI EventSystem 的细粒度 handler 接口：目标只需实现自己关心的那几个。

    /// <summary>指针进入目标时回调（hover 开始）。</summary>
    public interface IShapesPointerEnterHandler
    {
        void OnPointerEnter(ShapesPointerEvent e);
    }

    /// <summary>指针离开目标时回调（hover 结束）。</summary>
    public interface IShapesPointerExitHandler
    {
        void OnPointerExit(ShapesPointerEvent e);
    }

    /// <summary>在目标上按下时回调。</summary>
    public interface IShapesPointerDownHandler
    {
        void OnPointerDown(ShapesPointerEvent e);
    }

    /// <summary>抬起时回调（在最初按下的目标上触发，无论指针是否仍在其上）。</summary>
    public interface IShapesPointerUpHandler
    {
        void OnPointerUp(ShapesPointerEvent e);
    }

    /// <summary>按住并移动时每帧回调（在最初按下的目标上触发，拖出范围仍跟手）。</summary>
    public interface IShapesDragHandler
    {
        void OnDrag(ShapesPointerEvent e);
    }

    /// <summary>在同一目标上完成「按下并抬起」时回调。</summary>
    public interface IShapesPointerClickHandler
    {
        void OnPointerClick(ShapesPointerEvent e);
    }

    /// <summary>悬停期间每帧回调（指针在目标上移动时持续触发，用于逐格高亮等）。</summary>
    public interface IShapesPointerMoveHandler
    {
        void OnPointerMove(ShapesPointerEvent e);
    }
}
