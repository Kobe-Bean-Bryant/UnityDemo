using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 用本框架复刻 Shapes 官方示例（<c>IMColorPickerRenderer</c> + <c>IMColorPickerInteraction</c>），行为完全一致。
    /// <para>
    /// 对比官方实现：
    /// <list type="bullet">
    /// <item><b>绘制</b>：与官方 <c>IMColorPickerRenderer.DrawShapes</c> 一字不差地照搬。</item>
    /// <item><b>交互</b>：官方用一个单独的 MonoBehaviour 在 <c>Update</c> 里 <c>Camera.main.ScreenPointToRay</c> +
    /// 手写 <c>RaycastInteract</c>；这里改为<b>实现本框架接口</b>，由 <see cref="ShapesInteractionManager"/> 统一派发——
    /// 命中区是「色环 ∪ SV 方块」的复合区域，按下时判定命中了哪个子部件、拖拽时更新对应值。</item>
    /// </list>
    /// 这证明：一个复合控件的多个子部件（色环/方块）可由<b>一个 target</b> 承载（按 <c>LocalPoint</c> 分流），
    /// 且无需任何手写的输入/射线代码。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Shapes UI/Samples/Color Picker (framework)")]
    public class ShapesColorPickerSample : ImmediateModeShapeDrawer,
        IShapesRaycastTarget,
        IShapesPointerDownHandler, IShapesDragHandler, IShapesPointerUpHandler
    {
        private enum Element
        {
            None,
            HueStrip,
            Rectangle
        }

        [Header("Color value")]
        [Range(0, 1)]
        public float hue = 0f;
        [Range(0, 1)]
        public float saturation = 1f;
        [Range(0, 1)]
        public float value = 1f;

        [Header("Styling")]
        [Range(0, 0.3f)]
        public float hueStripThickness = 0.12f;
        [Range(0, 0.1f)]
        public float outline = 0.02f;
        [Range(0, 0.1f)]
        public float quadMargin = 0.05f;
        [Range(0, 1.5f)]
        public float hueDotScale = 1f;
        public Vector2 labelSize = new Vector2(1f, 0.25f);

        [Header("Interaction")]
        [SerializeField]
        private int sortingOrder;

        private PolylinePath _hueStripPath;
        private Element _active;

        // —— 与官方一致的属性 ——
        public Color CurrentPureColor => Color.HSVToRGB(hue, 1, 1);
        public Color CurrentColor => Color.HSVToRGB(hue, saturation, value);
        public float QuadScale => (1f - hueStripThickness / 2 - quadMargin) / Mathf.Sqrt(2);
        public Rect QuadRect => new Rect(default, Vector2.one * QuadScale * 2) { center = default };
        public float HueStripRadiusOuter => 1 + hueStripThickness / 2 + outline;
        public float HueStripRadiusInner => 1 - hueStripThickness / 2 - outline;
        public static Vector2 HueToVector(float hue) => ShapesMath.AngToDir(hue * ShapesMath.TAU);
        public static float VectorToHue(Vector2 v) => ShapesMath.Frac(ShapesMath.DirToAng(v) / ShapesMath.TAU);

        // —— 框架契约 ——
        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        public override void OnEnable()
        {
            base.OnEnable();
            ConstructHueStripPolyline();
            ShapesInteractionManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ShapesInteractionManager.Unregister(this);
            _active = Element.None;
            _hueStripPath.Dispose();
        }

        // —— 交互（替代官方 IMColorPickerInteraction，全部走框架）——

        // 命中区 = 色环 ∪ SV 方块（复合控件用一个 target 覆盖整体）
        public bool ContainsLocalPoint(Vector2 p) => HueStripContains(p) || QuadRect.Contains(p);

        // 按下时判定命中了哪个子部件（与官方 GetPickerElementAt 一致）
        public void OnPointerDown(ShapesPointerEvent e) => _active = ElementAt(e.LocalPoint);

        // 拖拽时更新对应子部件的值（与官方 UpdatePickerColor 一致）
        public void OnDrag(ShapesPointerEvent e)
        {
            if (_active == Element.HueStrip)
            {
                hue = VectorToHue(e.LocalPoint);
            }
            else if (_active == Element.Rectangle)
            {
                Vector2 sv = ShapesMath.InverseLerp(QuadRect, e.LocalPoint);
                saturation = Mathf.Clamp01(sv.x);
                value = Mathf.Clamp01(sv.y);
            }
        }

        public void OnPointerUp(ShapesPointerEvent e) => _active = Element.None;

        private Element ElementAt(Vector2 p)
        {
            if (HueStripContains(p)) return Element.HueStrip;
            if (QuadRect.Contains(p)) return Element.Rectangle;
            return Element.None;
        }

        private bool HueStripContains(Vector2 p)
        {
            float r = p.magnitude;
            return r >= HueStripRadiusInner && r <= HueStripRadiusOuter;
        }

        // —— 绘制（照搬官方 IMColorPickerRenderer.DrawShapes）——
        public override void DrawShapes(Camera cam)
        {
            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;

                // 色环
                Draw.Ring(Vector3.zero, 1f, hueStripThickness + outline, Color.black); // 背景/描边
                Draw.PolylineJoins = PolylineJoins.Simple;
                Draw.PolylineGeometry = PolylineGeometry.Flat2D;
                Draw.Polyline(_hueStripPath, closed: true, hueStripThickness);

                // SV 方块（黑→白→纯色 渐变）
                float quadScale = QuadScale;
                Draw.Rectangle(Vector3.zero, Vector2.one * ((quadScale * 2) + outline), Color.black); // 背景/描边
                using (Draw.MatrixScope)
                {
                    Draw.Scale(quadScale);
                    Draw.Quad(
                        new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1),
                        Color.black, Color.black, CurrentPureColor, Color.white);
                }

                // 标签
                Rect labelRect = new Rect(-labelSize.x / 2, -quadScale - labelSize.y, labelSize.x, labelSize.y);
                Draw.Rectangle(labelRect, 0.1f, Color.black); // 背景
                string hexColor = "#" + ColorUtility.ToHtmlStringRGB(CurrentColor);
                Draw.FontSize = labelSize.y * 8.5f;
                Draw.TextAlign = TextAlign.Center;
                Draw.TextRect(labelRect, hexColor);

                // 色相圆点
                float dotRadius = (hueStripThickness / 2) * hueDotScale;
                Vector2 hueDotPos = HueToVector(hue);
                Draw.Disc(hueDotPos, dotRadius + outline / 2, Color.black);
                Draw.Disc(hueDotPos, dotRadius, CurrentPureColor);

                // 饱和度/明度圆点
                Vector2 satValDot = ShapesMath.Lerp(QuadRect, new Vector2(saturation, value));
                Draw.Disc(satValDot, dotRadius + outline / 2, Color.black);
                Draw.Disc(satValDot, dotRadius, CurrentColor);
            }
        }

        private void ConstructHueStripPolyline()
        {
            _hueStripPath = new PolylinePath();
            const int DETAIL = 100;
            for (int i = 0; i < DETAIL; i++)
            {
                float tHue = i / (float)DETAIL;
                Color color = Color.HSVToRGB(tHue, 1, 1);
                Vector3 pt = HueToVector(tHue);
                _hueStripPath.AddPoint(pt, color);
            }
        }
    }
}
