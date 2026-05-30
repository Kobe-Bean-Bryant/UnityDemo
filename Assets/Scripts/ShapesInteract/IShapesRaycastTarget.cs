using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 可被指针命中的目标。任何想接收交互的对象实现此接口，并注册到 <see cref="ShapesInteractionManager"/>。
    /// <para>
    /// 这是框架的核心契约：它只描述「如何命中」，不关心「如何绘制」——因此立即模式
    /// （<c>ImmediateModeShapeDrawer</c> 子类）和组件模式（挂 <c>Shapes.Rectangle</c>/<c>Disc</c> 的对象）
    /// 都可以实现它。
    /// </para>
    /// </summary>
    public interface IShapesRaycastTarget
    {
        /// <summary>目标的 Transform，用于把世界射线换算到本地空间做命中测试。</summary>
        Transform Transform { get; }

        /// <summary>命中重叠时的优先级，数值大者优先（类似 UI 的层级）。</summary>
        int SortingOrder { get; }

        /// <summary>
        /// 在本地空间（z=0 平面）做命中测试。<paramref name="localPoint"/> 已由 Manager
        /// 将屏幕射线换算到本对象的本地坐标，因此与相机位置、缩放、宽高比均无关。
        /// </summary>
        bool ContainsLocalPoint(Vector2 localPoint);
    }
}
