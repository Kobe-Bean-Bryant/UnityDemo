using System;
using System.Collections.Generic;
using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 立即模式「可交互绘制」入口，呼应 Shapes 的 <c>Draw.XXX</c>（内部就是调用 <c>Draw.XXX</c>）。
    /// 在你自己的 <c>ImmediateModeShapeDrawer.DrawShapes</c> 里这样用：
    /// <code>
    /// using (IDraw.Command(cam, this)) {          // 替代 Draw.Command(cam) + Draw.Matrix
    ///     var btn = IDraw.Rectangle("play", pos, size, 0.15f, Color.white);
    ///     btn.OnClick = () => ...;                // 每帧赋值，幂等
    /// }
    /// </code>
    /// 每个 <c>IDraw.XXX(id, ...)</c> 既绘制、又按参数自动建好命中区、并返回一个跨帧持久的句柄（按 id 复用）。
    /// owner 在 <c>OnDisable</c> 里调一次 <see cref="Release"/> 以清理句柄。
    /// </summary>
    public static class IDraw
    {
        private sealed class OwnerState
        {
            public Transform Transform;
            public readonly Dictionary<string, InteractiveShapeHandle> Handles =
                new Dictionary<string, InteractiveShapeHandle>();
            public readonly HashSet<string> Seen = new HashSet<string>();
            public int LastFrame = -1;
        }

        private static readonly Dictionary<MonoBehaviour, OwnerState> Owners =
            new Dictionary<MonoBehaviour, OwnerState>();
        private static readonly List<string> RemoveBuffer = new List<string>();
        private static OwnerState _current;

        /// <summary>开启一次可交互绘制上下文（内部 Draw.Command + Draw.Matrix）。返回值用于 using。</summary>
        public static Scope Command(Camera cam, MonoBehaviour owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            if (!Owners.TryGetValue(owner, out var st))
            {
                st = new OwnerState();
                Owners[owner] = st;
            }

            st.Transform = owner.transform;
            _current = st;

            // 每帧每 owner 只做一次：按上一帧的 Seen 裁剪掉本帧没再画的句柄，然后清空 Seen
            if (st.LastFrame != Time.frameCount)
            {
                Prune(st);
                st.Seen.Clear();
                st.LastFrame = Time.frameCount;
            }

            IDisposable cmd = Draw.Command(cam);
            Draw.Matrix = owner.transform.localToWorldMatrix;
            return new Scope(cmd);
        }

        /// <summary>注销并清除该 owner 的全部句柄；请在 owner 的 OnDisable 调用一次。</summary>
        public static void Release(MonoBehaviour owner)
        {
            if (owner == null || !Owners.TryGetValue(owner, out var st)) return;
            foreach (var h in st.Handles.Values)
                ShapesInteractionManager.Unregister(h);
            st.Handles.Clear();
            Owners.Remove(owner);
        }

        // —— 可交互绘制方法（单色）——

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, Vector2 size, float cornerRadius,
            Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            Draw.Rectangle(center, size, cornerRadius, color);
            return h;
        }

        public static InteractiveShapeHandle Disc(string id, Vector3 center, float radius, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetCircle(center, radius);
            Draw.Disc(center, radius, color);
            return h;
        }

        public static InteractiveShapeHandle Ring(string id, Vector3 center, float radius, float thickness, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetRing(center, radius - thickness * 0.5f, radius + thickness * 0.5f);
            Draw.Ring(center, radius, thickness, color);
            return h;
        }

        public static InteractiveShapeHandle Triangle(string id, Vector3 a, Vector3 b, Vector3 c, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetTriangle(a, b, c);
            Draw.Triangle(a, b, c, color);
            return h;
        }

        // —— 四态颜色重载（Rectangle / Disc）：按句柄实时状态自动选色，hover/press 开箱即用 ——

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, Vector2 size, float cornerRadius,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            Draw.Rectangle(center, size, cornerRadius, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Disc(string id, Vector3 center, float radius, Color normal, Color hover,
            Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetCircle(center, radius);
            Draw.Disc(center, radius, Pick(h, normal, hover, pressed));
            return h;
        }

        // —— internals ——

        private static Color Pick(InteractiveShapeHandle h, Color normal, Color hover, Color pressed)
            => h.Pressed ? pressed : h.Hovered ? hover : normal;

        private static InteractiveShapeHandle Ensure(string id, int sortingOrder)
        {
            var st = _current ?? throw new InvalidOperationException(
                "IDraw.XXX 必须在 using(IDraw.Command(cam, owner)) 块内调用。");

            if (!st.Handles.TryGetValue(id, out var h))
            {
                h = new InteractiveShapeHandle { Transform = st.Transform };
                st.Handles[id] = h;
                ShapesInteractionManager.Register(h);
            }

            h.SortingOrder = sortingOrder;
            st.Seen.Add(id);
            return h;
        }

        private static void Prune(OwnerState st)
        {
            RemoveBuffer.Clear();
            foreach (var kv in st.Handles)
                if (!st.Seen.Contains(kv.Key))
                    RemoveBuffer.Add(kv.Key);

            for (int i = 0; i < RemoveBuffer.Count; i++)
            {
                ShapesInteractionManager.Unregister(st.Handles[RemoveBuffer[i]]);
                st.Handles.Remove(RemoveBuffer[i]);
            }
        }

        /// <summary>`IDraw.Command` 的返回值，用于 <c>using</c>；Dispose 时关闭底层 Draw.Command 并清上下文。</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly IDisposable _cmd;
            public Scope(IDisposable cmd) => _cmd = cmd;

            public void Dispose()
            {
                _current = null;
                _cmd?.Dispose();
            }
        }
    }
}
