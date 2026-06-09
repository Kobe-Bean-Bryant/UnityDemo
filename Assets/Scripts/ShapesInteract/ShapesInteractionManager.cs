using System.Collections.Generic;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 指针交互的中央派发器（类比 uGUI 的 EventSystem）。场景里需要且仅需要一个。
    /// <para>
    /// 每帧：读鼠标快照 → 从相机发射线 → 在所有注册的 <see cref="IShapesRaycastTarget"/> 中找最上层命中者 →
    /// 按状态机（hover / down / drag / click）派发给目标实现的 handler 接口。
    /// 支持左键和右键的独立状态跟踪，中键状态已读取但暂不派发。
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

        // —— 共享悬停状态（按钮无关，只有一个光标）——
        private IShapesRaycastTarget _hovered;

        // —— 每按钮独立按下状态 ——
        private struct ButtonState
        {
            public IShapesRaycastTarget PressedTarget;
            public Vector2 LastLocal;
        }

        private ButtonState _left;
        private ButtonState _right;

        private void Update()
        {
            // 清理「被销毁但未注销」的状态引用，避免后续访问其 Transform 抛 MissingReferenceException。
            if (_hovered is Object ho && ho == null) _hovered = null;
            CleanupIfDestroyed(ref _left);
            CleanupIfDestroyed(ref _right);

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            // 一次快照读取所有按钮状态
            if (!ShapesPointerInput.TryGetMouseState(out var mouse))
                return;

            Ray ray = cam.ScreenPointToRay(mouse.Position);
            var hit = Raycast(ray, out Vector2 hitLocal, out Vector3 hitWorld);

            // —— 悬停 enter / exit（按钮无关）——
            if (!ReferenceEquals(hit, _hovered))
            {
                if (_hovered is IShapesPointerExitHandler exit)
                    exit.OnPointerExit(MakeEvent(_hovered, mouse.Position, default, default, default, PointerButton.Left));
                if (hit is IShapesPointerEnterHandler enter)
                    enter.OnPointerEnter(MakeEvent(hit, mouse.Position, hitWorld, hitLocal, default, PointerButton.Left));
                _hovered = hit;
            }

            // —— 悬停期间每帧移动（按钮无关）——
            if (hit is IShapesPointerMoveHandler move)
                move.OnPointerMove(MakeEvent(hit, mouse.Position, hitWorld, hitLocal, default, PointerButton.Left));

            // —— 每按钮独立派发 ——
            UpdateButton(PointerButton.Left, ref _left, mouse.Left, hit, ray, mouse.Position, hitLocal, hitWorld);
            UpdateButton(PointerButton.Right, ref _right, mouse.Right, hit, ray, mouse.Position, hitLocal, hitWorld);
        }

        /// <summary>
        /// 单个按钮的完整按下/拖拽/抬起/点击状态机。
        /// </summary>
        private void UpdateButton(
            PointerButton button,
            ref ButtonState state,
            MouseButtonState input,
            IShapesRaycastTarget hit,
            Ray ray,
            Vector2 screen,
            Vector2 hitLocal,
            Vector3 hitWorld)
        {
            // —— 按下 ——
            if (input.Pressed && hit != null)
            {
                state.PressedTarget = hit;
                state.LastLocal = hitLocal;
                if (hit is IShapesPointerDownHandler down)
                    down.OnPointerDown(MakeEvent(hit, screen, hitWorld, hitLocal, default, button));
            }

            // —— 拖拽（在按下的目标上，拖出范围仍跟手）——
            if (input.Held && state.PressedTarget != null && state.PressedTarget is IShapesDragHandler drag)
            {
                if (TryLocal(state.PressedTarget, ray, out Vector2 dragLocal, out Vector3 dragWorld))
                {
                    Vector2 delta = dragLocal - state.LastLocal;
                    state.LastLocal = dragLocal;
                    drag.OnDrag(MakeEvent(state.PressedTarget, screen, dragWorld, dragLocal, delta, button));
                }
            }

            // —— 抬起 + 点击 ——
            if (input.Released && state.PressedTarget != null)
            {
                Vector2 upLocal = hitLocal;
                Vector3 upWorld = hitWorld;
                if (!ReferenceEquals(hit, state.PressedTarget) && TryLocal(state.PressedTarget, ray, out Vector2 l, out Vector3 w))
                {
                    upLocal = l;
                    upWorld = w;
                }

                if (state.PressedTarget is IShapesPointerUpHandler up)
                    up.OnPointerUp(MakeEvent(state.PressedTarget, screen, upWorld, upLocal, default, button));
                if (ReferenceEquals(hit, state.PressedTarget) && state.PressedTarget is IShapesPointerClickHandler click)
                    click.OnPointerClick(MakeEvent(state.PressedTarget, screen, hitWorld, hitLocal, default, button));
                state.PressedTarget = null;
            }
        }

        private static void CleanupIfDestroyed(ref ButtonState state)
        {
            if (state.PressedTarget is Object obj && obj == null)
                state.PressedTarget = null;
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
            IShapesRaycastTarget target, Vector2 screen, Vector3 world, Vector2 local, Vector2 delta,
            PointerButton button)
            => new ShapesPointerEvent
            {
                Target = target,
                ScreenPosition = screen,
                WorldPoint = world,
                LocalPoint = local,
                LocalDelta = delta,
                Button = button,
            };
    }
}
