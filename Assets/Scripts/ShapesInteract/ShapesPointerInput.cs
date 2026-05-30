using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityDemo.Shared.ShapesInteract
{
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
        /// 读取本帧鼠标状态。返回 false 表示当前没有可用的鼠标设备（输出参数均为默认值）。
        /// </summary>
        public static bool TryGetMouse(out Vector2 screenPosition, out bool pressed, out bool held, out bool released)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                pressed = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            pressed = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            released = Input.GetMouseButtonUp(0);
            return true;
#else
            screenPosition = default;
            pressed = held = released = false;
            return false;
#endif
        }
    }
}
