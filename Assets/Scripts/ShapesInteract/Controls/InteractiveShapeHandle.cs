using System;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 一次 <see cref="IDraw"/> 可交互绘制对应的持久句柄（普通 C# 对象，非 MonoBehaviour）。
    /// 实现 <see cref="IShapesRaycastTarget"/> 与各 handler 接口；命中区按形状用 <see cref="ShapesHitArea"/> 判定。
    /// <para>
    /// 立即模式每帧重画，所以事件用<b>可赋值委托</b>（<c>handle.OnClick = e => ...</c>，每帧重新赋值是幂等的），
    /// 不要用 AddListener（会每帧累积）。另暴露实时状态 <see cref="Hovered"/> / <see cref="Pressed"/>。
    /// </para>
    /// </summary>
    public class InteractiveShapeHandle :
        IShapesRaycastTarget,
        IShapesPointerEnterHandler, IShapesPointerExitHandler,
        IShapesPointerDownHandler, IShapesPointerUpHandler,
        IShapesDragHandler, IShapesPointerClickHandler, IShapesPointerMoveHandler
    {
        private enum Kind
        {
            Box,
            Circle,
            Ring,
            Triangle,
            Capsule,
            Sector,
            Polygon,
            Polyline
        }

        public Transform Transform { get; internal set; }
        public int SortingOrder { get; set; }

        /// <summary>指针是否正悬停其上。</summary>
        public bool Hovered { get; private set; }
        /// <summary>是否正被按住。</summary>
        public bool Pressed { get; private set; }

        // 可赋值的行为委托（每帧赋值幂等）。所有委托都携带 ShapesPointerEvent 以便消费者检查 e.Button。
        public Action<ShapesPointerEvent> OnClick;
        public Action<ShapesPointerEvent> OnEnter;
        public Action<ShapesPointerEvent> OnExit;
        public Action<ShapesPointerEvent> OnDown;
        public Action<ShapesPointerEvent> OnUp;
        public Action<ShapesPointerEvent> OnDrag;
        public Action<ShapesPointerEvent> OnMove;

        // —— 命中几何（由 IDraw 每帧更新）——
        private Kind _kind;
        private Vector2 _center, _size, _a, _b, _c;
        private float _radius, _inner, _thickness, _from, _to;
        private float _rotation;            // 弧度，绕 _center 旋转（仅 Rectangle/Pie/Arc 会设非零）
        private Vector2[] _points;
        private bool _closed;

        internal void SetBox(Vector2 center, Vector2 size)
        {
            _kind = Kind.Box;
            _center = center;
            _size = size;
        }

        internal void SetCircle(Vector2 center, float radius)
        {
            _kind = Kind.Circle;
            _center = center;
            _radius = radius;
        }

        internal void SetRing(Vector2 center, float inner, float outer)
        {
            _kind = Kind.Ring;
            _center = center;
            _inner = inner;
            _radius = outer;
        }

        internal void SetTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            _kind = Kind.Triangle;
            _a = a;
            _b = b;
            _c = c;
        }

        internal void SetCapsule(Vector2 a, Vector2 b, float thickness)
        {
            _kind = Kind.Capsule;
            _a = a;
            _b = b;
            _thickness = thickness;
        }

        internal void SetSector(Vector2 center, float inner, float outer, float from, float to)
        {
            _kind = Kind.Sector;
            _center = center;
            _inner = inner;
            _radius = outer;
            _from = from;
            _to = to;
        }

        internal void SetPolygon(Vector2[] points)
        {
            _kind = Kind.Polygon;
            _points = points;
        }

        internal void SetPolyline(Vector2[] points, float thickness, bool closed)
        {
            _kind = Kind.Polyline;
            _points = points;
            _thickness = thickness;
            _closed = closed;
        }

        /// <summary>设置绕 <c>_center</c> 的旋转（弧度）；命中时把待测点逆旋转回正坐标系再判定。</summary>
        internal void SetRotation(float radians) => _rotation = radians;

        public bool ContainsLocalPoint(Vector2 p)
        {
            // 形状绕 center 旋转了 _rotation，则把待测点逆旋转回去，再按未旋转几何判定。
            if (_rotation != 0f) p = ShapesHitArea.Rotate(p, _center, -_rotation);
            switch (_kind)
            {
                case Kind.Box: return ShapesHitArea.Box(p, _center, _size);
                case Kind.Circle: return ShapesHitArea.Circle(p, _center, _radius);
                case Kind.Ring: return ShapesHitArea.Ring(p, _center, _inner, _radius);
                case Kind.Triangle: return ShapesHitArea.Triangle(p, _a, _b, _c);
                case Kind.Capsule: return ShapesHitArea.Capsule(p, _a, _b, _thickness);
                case Kind.Sector: return ShapesHitArea.Sector(p, _center, _inner, _radius, _from, _to);
                case Kind.Polygon: return ShapesHitArea.Polygon(p, _points);
                case Kind.Polyline: return ShapesHitArea.PolylineCapsule(p, _points, _thickness, _closed);
                default: return false;
            }
        }

        void IShapesPointerEnterHandler.OnPointerEnter(ShapesPointerEvent e)
        {
            Hovered = true;
            OnEnter?.Invoke(e);
        }

        void IShapesPointerExitHandler.OnPointerExit(ShapesPointerEvent e)
        {
            Hovered = false;
            OnExit?.Invoke(e);
        }

        void IShapesPointerDownHandler.OnPointerDown(ShapesPointerEvent e)
        {
            Pressed = true;
            OnDown?.Invoke(e);
        }

        void IShapesPointerUpHandler.OnPointerUp(ShapesPointerEvent e)
        {
            Pressed = false;
            OnUp?.Invoke(e);
        }

        void IShapesDragHandler.OnDrag(ShapesPointerEvent e)
        {
            OnDrag?.Invoke(e);
        }

        void IShapesPointerClickHandler.OnPointerClick(ShapesPointerEvent e)
        {
            OnClick?.Invoke(e);
        }

        void IShapesPointerMoveHandler.OnPointerMove(ShapesPointerEvent e)
        {
            OnMove?.Invoke(e);
        }
    }
}
