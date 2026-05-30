using System;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 一次 <see cref="IDraw"/> 可交互绘制对应的持久句柄（普通 C# 对象，非 MonoBehaviour）。
    /// 实现 <see cref="IShapesRaycastTarget"/> 与各 handler 接口；命中区按形状用 <see cref="ShapesHitArea"/> 判定。
    /// <para>
    /// 立即模式每帧重画，所以事件用<b>可赋值委托</b>（<c>handle.OnClick = () => ...</c>，每帧重新赋值是幂等的），
    /// 不要用 AddListener（会每帧累积）。另暴露实时状态 <see cref="Hovered"/> / <see cref="Pressed"/>。
    /// </para>
    /// </summary>
    public class InteractiveShapeHandle :
        IShapesRaycastTarget,
        IShapesPointerEnterHandler, IShapesPointerExitHandler,
        IShapesPointerDownHandler, IShapesPointerUpHandler,
        IShapesDragHandler, IShapesPointerClickHandler, IShapesPointerMoveHandler
    {
        private enum Kind { Box, Circle, Ring, Triangle }

        public Transform Transform { get; internal set; }
        public int SortingOrder { get; set; }

        /// <summary>指针是否正悬停其上。</summary>
        public bool Hovered { get; private set; }
        /// <summary>是否正被按住。</summary>
        public bool Pressed { get; private set; }

        // 可赋值的行为委托（每帧赋值幂等）
        public Action OnClick;
        public Action OnEnter;
        public Action OnExit;
        public Action OnDown;
        public Action OnUp;
        public Action<ShapesPointerEvent> OnDrag;
        public Action<ShapesPointerEvent> OnMove;

        // —— 命中几何（由 IDraw 每帧更新）——
        private Kind _kind;
        private Vector2 _center, _size, _a, _b, _c;
        private float _radius, _inner;

        internal void SetBox(Vector2 center, Vector2 size) { _kind = Kind.Box; _center = center; _size = size; }
        internal void SetCircle(Vector2 center, float radius) { _kind = Kind.Circle; _center = center; _radius = radius; }
        internal void SetRing(Vector2 center, float inner, float outer) { _kind = Kind.Ring; _center = center; _inner = inner; _radius = outer; }
        internal void SetTriangle(Vector2 a, Vector2 b, Vector2 c) { _kind = Kind.Triangle; _a = a; _b = b; _c = c; }

        public bool ContainsLocalPoint(Vector2 p)
        {
            switch (_kind)
            {
                case Kind.Box: return ShapesHitArea.Box(p, _center, _size);
                case Kind.Circle: return ShapesHitArea.Circle(p, _center, _radius);
                case Kind.Ring: return ShapesHitArea.Ring(p, _center, _inner, _radius);
                case Kind.Triangle: return ShapesHitArea.Triangle(p, _a, _b, _c);
                default: return false;
            }
        }

        void IShapesPointerEnterHandler.OnPointerEnter(ShapesPointerEvent e) { Hovered = true; OnEnter?.Invoke(); }
        void IShapesPointerExitHandler.OnPointerExit(ShapesPointerEvent e) { Hovered = false; OnExit?.Invoke(); }
        void IShapesPointerDownHandler.OnPointerDown(ShapesPointerEvent e) { Pressed = true; OnDown?.Invoke(); }
        void IShapesPointerUpHandler.OnPointerUp(ShapesPointerEvent e) { Pressed = false; OnUp?.Invoke(); }
        void IShapesDragHandler.OnDrag(ShapesPointerEvent e) { OnDrag?.Invoke(e); }
        void IShapesPointerClickHandler.OnPointerClick(ShapesPointerEvent e) { OnClick?.Invoke(); }
        void IShapesPointerMoveHandler.OnPointerMove(ShapesPointerEvent e) { OnMove?.Invoke(e); }
    }
}
