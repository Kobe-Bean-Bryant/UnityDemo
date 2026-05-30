using System.Collections.Generic;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 指针交互的中央派发器（类比 uGUI 的 EventSystem）。场景里需要且仅需要一个。
    /// <para>
    /// 每帧：读鼠标 → 从相机发射线 → 在所有注册的 <see cref="IShapesRaycastTarget"/> 中找最上层命中者 →
    /// 按状态机（hover / down / drag / click）派发给目标实现的 handler 接口。
    /// 它从不调用任何 Shapes 绘制 API。
    /// </para>
    /// </summary>
    [AddComponentMenu("Shapes UI/Shapes Interaction Manager")]
    [DisallowMultipleComponent]
    public class ShapesInteractionManager : MonoBehaviour
    {
        [Tooltip("用于把屏幕坐标换算成射线的相机；留空则回退到 Camera.main。")]
        [SerializeField]
        private Camera _camera;

        // 静态注册表：与本组件实例的生命周期解耦，目标可在 Manager 出现之前就注册。
        private static readonly List<IShapesRaycastTarget> Targets = new List<IShapesRaycastTarget>();

        /// <summary>注册一个命中目标（通常在目标的 OnEnable 调用）。</summary>
        public static void Register(IShapesRaycastTarget target)
        {
            if (target != null && !Targets.Contains(target))
                Targets.Add(target);
        }

        /// <summary>注销一个命中目标（必须在目标的 OnDisable 调用，否则销毁后会抛异常）。</summary>
        public static void Unregister(IShapesRaycastTarget target) => Targets.Remove(target);

        private IShapesRaycastTarget _hovered;
        private IShapesRaycastTarget _pressed;
        private Vector2 _lastPressedLocal;

        private void Update()
        {
            // 清理「被销毁但未注销」的状态引用，避免后续访问其 Transform 抛 MissingReferenceException。
            if (_hovered is Object ho && ho == null) _hovered = null;
            if (_pressed is Object po && po == null) _pressed = null;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            if (!ShapesPointerInput.TryGetMouse(out Vector2 screen, out bool pressed, out bool held, out bool released))
                return;

            Ray ray = cam.ScreenPointToRay(screen);
            var hit = Raycast(ray, out Vector2 hitLocal, out Vector3 hitWorld);

            // —— 悬停 enter / exit ——
            if (!ReferenceEquals(hit, _hovered))
            {
                if (_hovered is IShapesPointerExitHandler exit)
                    exit.OnPointerExit(MakeEvent(_hovered, screen, default, default, default));
                if (hit is IShapesPointerEnterHandler enter)
                    enter.OnPointerEnter(MakeEvent(hit, screen, hitWorld, hitLocal, default));
                _hovered = hit;
            }

            // —— 悬停期间每帧移动 ——
            if (hit is IShapesPointerMoveHandler move)
                move.OnPointerMove(MakeEvent(hit, screen, hitWorld, hitLocal, default));

            // —— 按下 ——
            if (pressed && hit != null)
            {
                _pressed = hit;
                _lastPressedLocal = hitLocal;
                if (hit is IShapesPointerDownHandler down)
                    down.OnPointerDown(MakeEvent(hit, screen, hitWorld, hitLocal, default));
            }

            // —— 拖拽（在按下的目标上，拖出范围仍跟手）——
            if (held && _pressed != null && _pressed is IShapesDragHandler drag)
            {
                if (TryLocal(_pressed, ray, out Vector2 dragLocal, out Vector3 dragWorld))
                {
                    Vector2 delta = dragLocal - _lastPressedLocal;
                    _lastPressedLocal = dragLocal;
                    drag.OnDrag(MakeEvent(_pressed, screen, dragWorld, dragLocal, delta));
                }
            }

            // —— 抬起 + 点击 ——
            if (released && _pressed != null)
            {
                // 抬起事件用 _pressed 自己的局部点（指针可能已移开 _pressed）；命中 _pressed 时直接用 hit 的点。
                Vector2 upLocal = hitLocal;
                Vector3 upWorld = hitWorld;
                if (!ReferenceEquals(hit, _pressed) && TryLocal(_pressed, ray, out Vector2 l, out Vector3 w))
                {
                    upLocal = l;
                    upWorld = w;
                }

                if (_pressed is IShapesPointerUpHandler up)
                    up.OnPointerUp(MakeEvent(_pressed, screen, upWorld, upLocal, default));
                if (ReferenceEquals(hit, _pressed) && _pressed is IShapesPointerClickHandler click)
                    click.OnPointerClick(MakeEvent(_pressed, screen, hitWorld, hitLocal, default));
                _pressed = null;
            }
        }

        /// <summary>在所有目标里找命中点，返回 SortingOrder 最大者；无命中返回 null。</summary>
        private static IShapesRaycastTarget Raycast(Ray ray, out Vector2 bestLocal, out Vector3 bestWorld)
        {
            IShapesRaycastTarget best = null;
            bestLocal = default;
            bestWorld = default;
            int bestOrder = int.MinValue;

            foreach (var target in Targets)
            {
                // 防御：目标若被销毁但忘了注销，UnityEngine.Object 的重载 == 能识别出 fake-null。
                if (target is Object obj && obj == null) continue;

                if (TryLocal(target, ray, out Vector2 local, out Vector3 world)
                    && target.ContainsLocalPoint(local)
                    && target.SortingOrder >= bestOrder)
                {
                    best = target;
                    bestOrder = target.SortingOrder;
                    bestLocal = local;
                    bestWorld = world;
                }
            }

            return best;
        }

        /// <summary>把世界射线转到目标本地空间，与 z=0 平面求交，得到本地点与对应世界点。</summary>
        private static bool TryLocal(IShapesRaycastTarget target, Ray worldRay, out Vector2 local, out Vector3 world)
        {
            Transform t = target.Transform;
            if (t == null)
            {
                local = default;
                world = default;
                return false; // 目标/其 owner 已被销毁却未注销时的防御，避免 MissingReferenceException
            }

            var localRay = new Ray(
                t.InverseTransformPoint(worldRay.origin),
                t.InverseTransformDirection(worldRay.direction));

            if (new Plane(Vector3.back, 0f).Raycast(localRay, out float dist))
            {
                Vector3 lp = localRay.GetPoint(dist);
                local = lp;
                world = t.TransformPoint(lp);
                return true;
            }

            local = default;
            world = default;
            return false;
        }

        private static ShapesPointerEvent MakeEvent(
            IShapesRaycastTarget target, Vector2 screen, Vector3 world, Vector2 local, Vector2 delta)
            => new ShapesPointerEvent
            {
                Target = target,
                ScreenPosition = screen,
                WorldPoint = world,
                LocalPoint = local,
                LocalDelta = delta,
            };
    }
}
