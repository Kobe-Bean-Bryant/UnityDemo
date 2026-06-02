using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// HUD 样例之一：<b>mode② 自实现 target</b> 的 Drawer。自己实现 <see cref="IShapesRaycastTarget"/>，
    /// 画一排主题色块、点击设 <see cref="HudSharedState.themeIndex"/> → 影响 Bars 的血条色与 Controls 的主题条。
    /// 与另外两个 Drawer 互不引用，只共享 <see cref="HudSharedState"/>。
    /// </summary>
    [ExecuteAlways]     // 否则编辑器里看不到（立即模式 Drawer 的 OnEnable 不在编辑态订阅渲染）
    [AddComponentMenu("Shapes UI/Samples/HUD Theme Picker (mode② self-implemented)")]
    public class HudThemeDrawer : ImmediateModeShapeDrawer,
        IShapesRaycastTarget, IShapesPointerClickHandler
    {
        [SerializeField]
        private HudSharedState shared;
        [Tooltip("第一个色块中心（局部空间）")]
        [SerializeField]
        private Vector3 origin = new Vector3(5f, 3.2f, 0f);
        [SerializeField]
        private float swatch = 1f;
        [SerializeField]
        private float gap = 0.18f;
        [SerializeField]
        private int sortingOrder;
        [Header("Text")]
        [Tooltip("字号 = TMP 点数（不是世界高度！世界空间下约为目标世界高度的 ~10 倍。画布像素空间才用几百，如 240）")]
        [SerializeField]
        private float labelSize = 4f;
        [SerializeField]
        private Color labelColor = Color.white;

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        private int Count => shared != null && shared.themes != null ? shared.themes.Length : 0;
        private float Pitch => swatch + gap;

        public override void OnEnable()
        {
            base.OnEnable();
            ShapesInteractionManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ShapesInteractionManager.Unregister(this);
        }

        // 命中区 = 整排色块的包围矩形（从首块左缘到末块右缘）
        public bool ContainsLocalPoint(Vector2 p)
        {
            if (Count == 0) return false;
            float x0 = origin.x - swatch * 0.5f;
            float totalW = Count * Pitch - gap;
            float y0 = origin.y - swatch * 0.5f;
            return p.x >= x0 && p.x <= x0 + totalW && p.y >= y0 && p.y <= y0 + swatch;
        }

        public void OnPointerClick(ShapesPointerEvent e)
        {
            if (Count == 0) return;
            float x0 = origin.x - swatch * 0.5f;
            int i = Mathf.FloorToInt((e.LocalPoint.x - x0) / Pitch);
            if (i >= 0 && i < Count) shared.themeIndex = i;
        }

        public override void DrawShapes(Camera cam)
        {
            if (Count == 0) return;

            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;

                // 标题
                Draw.FontSize = labelSize;      // 世界单位字号（非画布像素）
                Draw.Color = labelColor;
                Draw.Text(new Vector3(origin.x - swatch * 0.5f, origin.y + swatch * 0.5f + 0.3f, origin.z),
                    "Theme", TextAlign.MidlineLeft);

                for (int i = 0; i < Count; i++)
                {
                    Vector3 c = origin + Vector3.right * (i * Pitch);
                    // 选中：先画一块略大的白底当描边
                    if (i == shared.themeIndex)
                        Draw.Rectangle(c, new Vector2(swatch + 0.18f, swatch + 0.18f), 0.16f, Color.white);
                    Draw.Rectangle(c, new Vector2(swatch, swatch), 0.12f, shared.themes[i]);
                }
            }
        }
    }
}
