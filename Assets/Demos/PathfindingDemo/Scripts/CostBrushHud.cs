using Shapes;
using UnityDemo.Shared.ShapesInteract;
using UnityDemo.Shared.ShapesInteract.Controls;
using UnityEngine;

namespace PathfindingDemo
{
    /// <summary>
    /// 代价画刷 HUD：在网格上方居中显示当前 brushCost，◀ ▶ 三角按钮可调整 (1-10)。
    /// 读取和写入 <see cref="PathfindingManager.brushCost"/>。
    /// </summary>
    [ExecuteAlways]
    public class CostBrushHud : ImmediateModeShapeDrawer
    {
        [SerializeField]
        private PathfindingManager manager;

        [Header("HUD Layout")]
        [Tooltip("HUD 在网格上方的偏移（世界单位）")]
        public float yOffset = 1.0f;
        public float buttonSize = 0.6f;
        public float previewSize = 0.6f;
        public float spacing = 1.8f;
        [Tooltip("字体大小（世界单位）")]
        public float fontSize = 0.5f;

        [Header("Colors")]
        public Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color hoverColor = new Color(1f, 1f, 1f, 1f);
        public Color pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private int _displayedCost;

        /// <summary>
        /// 根据代价值返回格子颜色。cost=1 返回默认色，cost 2→10 从黄色渐变到深橙。
        /// 使用 HSV 插值获得更好的色彩区分度。
        /// </summary>
        public static Color CostCellColor(int cost)
        {
            if (cost <= 1) return new Color(0.87f, 0.84f, 0.84f, 1f); // #ddd5d5
            float t = (cost - 1) / 9f;
            // HSV：H 从 50°(黄) → 20°(橙红)，S 从 0.6 → 1.0，V 从 0.95 → 1.0
            float h = Mathf.Lerp(50f / 360f, 20f / 360f, t);
            float s = Mathf.Lerp(0.6f, 1.0f, t);
            float v = Mathf.Lerp(0.95f, 1.0f, t);
            Color c = Color.HSVToRGB(h, s, v);
            c.a = 1f;
            return c;
        }

        public override void DrawShapes(Camera cam)
        {
            if (manager == null) return;
            var drawer = manager.pathfindingDrawer;
            if (drawer == null) return;

            _displayedCost = manager.brushCost;

            // HUD 居中于网格上方
            float gridWidth = manager.width * drawer.cellSize;
            float centerX = gridWidth / 2f;
            float centerY = manager.height * drawer.cellSize + yOffset;

            using (IDraw.Command(cam, this))
            {
                float halfBtn = buttonSize / 2f;

                // ◀ 按钮（左三角）
                Vector3 leftCenter = new Vector3(centerX - spacing, centerY, 0f);
                var leftBtn = IDraw.Triangle("cost-hud-left",
                    leftCenter + new Vector3(-halfBtn, 0f, 0f),   // 尖端朝左
                    leftCenter + new Vector3(halfBtn, -halfBtn, 0f),
                    leftCenter + new Vector3(halfBtn, halfBtn, 0f),
                    normalColor, hoverColor, pressedColor);
                leftBtn.OnClick = e =>
                {
                    if (e.Button == PointerButton.Left)
                        manager.brushCost = Mathf.Max(1, manager.brushCost - 1);
                };

                // ▶ 按钮（右三角）
                Vector3 rightCenter = new Vector3(centerX + spacing, centerY, 0f);
                var rightBtn = IDraw.Triangle("cost-hud-right",
                    rightCenter + new Vector3(halfBtn, 0f, 0f),    // 尖端朝右
                    rightCenter + new Vector3(-halfBtn, -halfBtn, 0f),
                    rightCenter + new Vector3(-halfBtn, halfBtn, 0f),
                    normalColor, hoverColor, pressedColor);
                rightBtn.OnClick = e =>
                {
                    if (e.Button == PointerButton.Left)
                        manager.brushCost = Mathf.Min(10, manager.brushCost + 1);
                };

                // 中央：代价值 + 预览色块
                float t = (_displayedCost - 1) / 9f;
                Color previewCol = CostCellColor(_displayedCost);

                // 预览色块（圆角方块）
                Draw.Rectangle(
                    new Vector3(centerX, centerY, 0f),
                    new Vector2(previewSize, previewSize), 0.1f, previewCol);

                // 代价数字叠加在色块上
                Draw.FontSize = fontSize;
                Draw.Color = _displayedCost >= 6 ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
                Draw.Text(new Vector3(centerX, centerY, -0.01f),
                    _displayedCost.ToString(), TextAlign.Center);
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            IDraw.Release(this);
        }
    }
}
