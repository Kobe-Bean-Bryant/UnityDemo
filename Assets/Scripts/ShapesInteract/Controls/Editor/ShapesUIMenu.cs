using Shapes;
using UnityDemo.Shared.ShapesInteract.Controls;
using UnityEditor;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.ControlsEditor
{
    /// <summary>
    /// <c>GameObject → Shapes UI →</c> 创建菜单：一键生成配好 Shapes 图形与控件组件的 GameObject，
    /// 仿 uGUI 的 <c>GameObject → UI →</c>。所有创建都登记 Undo 并自动选中。
    /// </summary>
    public static class ShapesUIMenu
    {
        private static readonly Color Accent = new Color(0.0f, 0.584f, 1f); // #0095FF

        [MenuItem("GameObject/Shapes UI/Button", false, 10)]
        private static void CreateButton(MenuCommand cmd)
        {
            GameObject go = NewGO("Shapes Button", cmd);
            Rectangle rect = AddRectangle(go, 3f, 1f, 0.15f, Color.white);

            var button = Undo.AddComponent<ShapesButton>(go);
            WireGraphic(button, rect);

            Finish(go);
        }

        [MenuItem("GameObject/Shapes UI/Toggle", false, 11)]
        private static void CreateToggle(MenuCommand cmd)
        {
            GameObject go = NewGO("Shapes Toggle", cmd);
            Rectangle box = AddRectangle(go, 1f, 1f, 0.12f, Color.white);

            // 勾选指示（一个圆点子物体），默认关闭时隐藏。
            GameObject checkGO = new GameObject("Checkmark");
            Undo.RegisterCreatedObjectUndo(checkGO, "Create Checkmark");
            GameObjectUtility.SetParentAndAlign(checkGO, go);
            Disc check = checkGO.AddComponent<Disc>();
            check.Radius = 0.3f;
            check.Color = Accent;
            check.enabled = false; // 默认 isOn = false，初始隐藏勾选

            var toggle = Undo.AddComponent<ShapesToggle>(go);
            WireGraphic(toggle, box);
            SetRef(toggle, "checkmark", check);

            Finish(go);
        }

        [MenuItem("GameObject/Shapes UI/Slider", false, 12)]
        private static void CreateSlider(MenuCommand cmd)
        {
            GameObject go = NewGO("Shapes Slider", cmd);
            const float trackW = 4f, trackH = 0.3f;
            Rectangle track = AddRectangle(go, trackW, trackH, trackH * 0.5f, new Color(0.8f, 0.8f, 0.8f));

            // 填充：用 Center pivot（竖直方向天然与轨道居中对齐，改尺寸也不错位）。
            // 运行时 ShapesSlider.ApplyVisuals 会按值重设其宽度与位置，把左缘钉在轨道左缘。
            GameObject fillGO = new GameObject("Fill");
            Undo.RegisterCreatedObjectUndo(fillGO, "Create Fill");
            GameObjectUtility.SetParentAndAlign(fillGO, go);
            Rectangle fill = fillGO.AddComponent<Rectangle>();
            fill.Type = Rectangle.RectangleType.RoundedSolid;
            fill.Pivot = RectPivot.Center;
            fill.Height = trackH;
            fill.CornerRadius = trackH * 0.5f;
            fill.Color = Accent;
            float initFillW = trackW * 0.5f;            // 初始铺满一半（编辑器预览）
            fill.Width = initFillW;
            fillGO.transform.localPosition = new Vector3(-trackW / 2f + initFillW / 2f, 0f, -0.01f);

            // 把手。
            GameObject handleGO = new GameObject("Handle");
            Undo.RegisterCreatedObjectUndo(handleGO, "Create Handle");
            GameObjectUtility.SetParentAndAlign(handleGO, go);
            Disc handle = handleGO.AddComponent<Disc>();
            handle.Radius = trackH;
            handle.Color = Color.white;
            handleGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            var slider = Undo.AddComponent<ShapesSlider>(go);
            WireGraphic(slider, track);
            SetRef(slider, "fill", fill);
            SetRef(slider, "handle", handle.transform);
            SetFloat(slider, "_value", 0.5f);
            SetVector2(slider, "hitPadding", new Vector2(0f, trackH)); // 让细轨道更易点中

            Finish(go);
        }

        // —— helpers ——

        private static GameObject NewGO(string name, MenuCommand cmd)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            return go;
        }

        private static Rectangle AddRectangle(GameObject go, float w, float h, float corner, Color color)
        {
            var rect = go.AddComponent<Rectangle>();
            rect.Type = Rectangle.RectangleType.RoundedSolid;
            rect.Pivot = RectPivot.Center;
            rect.Width = w;
            rect.Height = h;
            rect.CornerRadius = corner;
            rect.Color = color;
            return rect;
        }

        // 通过 SerializedObject 写入控件的私有/保护序列化字段（编辑器里设置引用的正规做法）。
        private static void WireGraphic(MonoBehaviour control, ShapeRenderer graphic)
            => SetRef(control, "targetGraphic", graphic);

        private static void SetRef(MonoBehaviour control, string field, Object value)
        {
            var so = new SerializedObject(control);
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(MonoBehaviour control, string field, float value)
        {
            var so = new SerializedObject(control);
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetVector2(MonoBehaviour control, string field, Vector2 value)
        {
            var so = new SerializedObject(control);
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.vector2Value = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Finish(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
