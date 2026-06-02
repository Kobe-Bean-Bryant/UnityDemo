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
    /// <para>
    /// 本文件是核心基础设施（上下文 / 句柄表 / 生命周期）；全部图形绘制方法见 <c>IDrawOverloads.cs</c>，
    /// 二者是同一个 <c>partial</c> 类的两半，镜像 Shapes 自己 <c>Draw.cs</c> / <c>DrawOverloads.cs</c> 的拆法。
    /// </para>
    /// </summary>
    public static partial class IDraw
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

        // —— internals（绘制方法见 IDrawOverloads.cs）——

        /// <summary>按句柄实时状态在三态颜色里选色，供四态颜色重载使用。</summary>
        internal static Color Pick(InteractiveShapeHandle h, Color normal, Color hover, Color pressed)
            => h.Pressed ? pressed : h.Hovered ? hover : normal;

        /// <summary>按 id 取/建当前 owner 的句柄：首次建即注册到 Manager；每次标记 seen 并刷新 sortingOrder。</summary>
        internal static InteractiveShapeHandle Ensure(string id, int sortingOrder)
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
            h.SetRotation(0f);          // 每帧复位旋转；旋转重载在 Ensure 之后再 SetRotation 覆盖
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
