using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BricksBreakerDemo
{
    /// <summary>
    /// Juice 效果开关面板控制器（移植 juicy-breakout 的 Toggler）。
    /// 挂在与 UIDocument 同一 GameObject 上（RequireComponent）。反射双向绑定：
    ///   · Toggle.name ↔ JuicySettings 同名 public static bool 字段
    ///   · Slider.name ↔ JuicySettings 同名 public static float 字段
    /// 快捷键：Tab 显隐面板 / Enter 全开 / 数字键 2 全关。
    /// 按钮：All On / All Off / Respawn（调 GameManager.ResetGame 重新生成，便于观察入场动画）。
    ///
    /// 扩展：JuicySettings 加字段 + JuicyPanel.uxml 加同名 Toggle/Slider 即可，本脚本零改。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class JuicyTogglePanel : MonoBehaviour
    {
        private VisualElement _panel;
        private bool _visible; // 面板显隐单一真相源（避免读取 style.display 的脆弱性）

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("[JuicyTogglePanel] UIDocument 无 rootVisualElement（panelSettings 可能未赋值），禁用面板");
                enabled = false;
                return;
            }
            _panel = root.Q<VisualElement>("Panel");
            if (_panel != null) _panel.focusable = false; // 防御：未来加 focusable 控件也不抢 Tab
            BuildBindings();
            if (_panel != null) _panel.style.display = DisplayStyle.None; // 默认隐藏
        }

        private void Update()
        {
            if (ReadTabPressed()) TogglePanel();
            else if (ReadEnterPressed()) SetAllAndSync(true);
            else if (ReadAlpha2Pressed()) SetAllAndSync(false);
        }

        private void TogglePanel()
        {
            if (_panel == null) return;
            _visible = !_visible;
            _panel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 反射绑定：Toggle↔bool、Slider↔float、Button→动作
        private void BuildBindings()
        {
            if (_panel == null) return;

            _panel.Query<Toggle>().ForEach(t =>
            {
                // 跳过 Foldout 展开开关等内置 Toggle（名为空或 "unity-" 前缀）
                if (string.IsNullOrEmpty(t.name) || t.name.StartsWith("unity-")) return;
                t.focusable = false;
                var field = LookupField<bool>(t.name);
                if (field == null)
                {
                    Debug.LogWarning($"[JuicyTogglePanel] Toggle '{t.name}' 在 JuicySettings 找不到同名 bool 字段，未绑定");
                    return;
                }
                t.SetValueWithoutNotify((bool)field.GetValue(null));
                t.RegisterValueChangedCallback(e => field.SetValue(null, e.newValue));
            });

            _panel.Query<Slider>().ForEach(s =>
            {
                // 跳过 ScrollView 滚动条等内置滑块（名为空或 "unity-" 前缀）
                if (string.IsNullOrEmpty(s.name) || s.name.StartsWith("unity-")) return;
                s.focusable = false;
                var field = LookupField<float>(s.name);
                if (field == null)
                {
                    Debug.LogWarning($"[JuicyTogglePanel] Slider '{s.name}' 在 JuicySettings 找不到同名 float 字段，未绑定");
                    return;
                }
                s.SetValueWithoutNotify((float)field.GetValue(null));
                s.RegisterValueChangedCallback(e => field.SetValue(null, e.newValue));
            });

            BindButton("AllOn", () => SetAllAndSync(true));
            BindButton("AllOff", () => SetAllAndSync(false));
            BindButton("Respawn", Respawn);
        }

        private void BindButton(string name, Action action)
        {
            var btn = _panel.Q<Button>(name);
            if (btn == null) return;
            btn.focusable = false;
            btn.RegisterCallback<ClickEvent>(_ => action());
        }

        // 全开/全关 bool（数值滑块不受影响），并回写所有 Toggle
        private void SetAllAndSync(bool value)
        {
            JuicySettings.SetAll(value);
            if (_panel == null) return;
            _panel.Query<Toggle>().ForEach(t =>
            {
                var field = LookupField<bool>(t.name);
                if (field != null) t.SetValueWithoutNotify((bool)field.GetValue(null));
            });
        }

        // 重新生成所有砖块/挡板/球（便于观察入场动画等"生成瞬间"效果）
        private void Respawn()
        {
            if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        }

        private static FieldInfo LookupField<T>(string name)
        {
            var f = typeof(JuicySettings).GetField(name, BindingFlags.Public | BindingFlags.Static);
            return f != null && f.FieldType == typeof(T) ? f : null;
        }

        #region 输入读取（匹配 GameManager 新旧输入兼容范式）

        private bool ReadTabPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.tabKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.Tab);
#else
            return false;
#endif
        }

        private bool ReadEnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.enterKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.Return);
#else
            return false;
#endif
        }

        private bool ReadAlpha2Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.digit2Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.Alpha2);
#else
            return false;
#endif
        }

        #endregion
    }
}
