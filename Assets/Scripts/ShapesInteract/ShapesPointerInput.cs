using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 单个鼠标按钮在一帧内的状态快照。
    /// </summary>
    public struct MouseButtonState
    {
        /// <summary>本帧刚按下。</summary>
        public bool Pressed;
        /// <summary>持续按住中。</summary>
        public bool Held;
        /// <summary>本帧刚抬起。</summary>
        public bool Released;
    }

    /// <summary>
    /// 整个鼠标在一帧内的完整状态快照（仿 MonoGame <c>Mouse.GetState()</c> 模式）。
    /// 一次读取捕获所有按钮，避免多次查询间的状态不一致。
    /// </summary>
    public struct MouseFrameState
    {
        public Vector2 Position;
        public MouseButtonState Left;
        public MouseButtonState Right;
        public MouseButtonState Middle;
    }

    /// <summary>
    /// 兼容新旧输入系统的鼠标读取。用 Unity 内置宏在编译期选择实现：
    /// <list type="bullet">
    /// <item>仅新系统：用 <c>Mouse.current</c>。</item>
    /// <item>仅旧系统：用 <c>Input</c>。</item>
    /// <item>两者皆启用（Active Input Handling = Both）：新系统优先、旧系统兜底。</item>
    /// </list>
    /// </summary>
    public static class ShapesPointerInput
    {
        /// <summary>
        /// 一次读取本帧所有鼠标按钮状态。返回 false 表示当前没有可用的鼠标设备。
        /// </summary>
        public static bool TryGetMouseState(out MouseFrameState state)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                state = new MouseFrameState
                {
                    Position = mouse.position.ReadValue(),
                    Left = new MouseButtonState
                    {
                        Pressed = mouse.leftButton.wasPressedThisFrame,
                        Held = mouse.leftButton.isPressed,
                        Released = mouse.leftButton.wasReleasedThisFrame
                    },
                    Right = new MouseButtonState
                    {
                        Pressed = mouse.rightButton.wasPressedThisFrame,
                        Held = mouse.rightButton.isPressed,
                        Released = mouse.rightButton.wasReleasedThisFrame
                    },
                    Middle = new MouseButtonState
                    {
                        Pressed = mouse.middleButton.wasPressedThisFrame,
                        Held = mouse.middleButton.isPressed,
                        Released = mouse.middleButton.wasReleasedThisFrame
                    }
                };
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            state = new MouseFrameState
            {
                Position = Input.mousePosition,
                Left = new MouseButtonState
                {
                    Pressed = Input.GetMouseButtonDown(0),
                    Held = Input.GetMouseButton(0),
                    Released = Input.GetMouseButtonUp(0)
                },
                Right = new MouseButtonState
                {
                    Pressed = Input.GetMouseButtonDown(1),
                    Held = Input.GetMouseButton(1),
                    Released = Input.GetMouseButtonUp(1)
                },
                Middle = new MouseButtonState
                {
                    Pressed = Input.GetMouseButtonDown(2),
                    Held = Input.GetMouseButton(2),
                    Released = Input.GetMouseButtonUp(2)
                }
            };
            return true;
#else
            state = default;
            return false;
#endif
        }

        /// <summary>
        /// 向后兼容的旧 API：只读取左键状态。内部转调 <see cref="TryGetMouseState"/>。
        /// </summary>
        public static bool TryGetMouse(out Vector2 screenPosition, out bool pressed, out bool held, out bool released)
        {
            if (TryGetMouseState(out var state))
            {
                screenPosition = state.Position;
                pressed = state.Left.Pressed;
                held = state.Left.Held;
                released = state.Left.Released;
                return true;
            }
            screenPosition = default;
            pressed = held = released = false;
            return false;
        }
    }
}
