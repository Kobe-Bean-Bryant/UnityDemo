using Shapes;
using UnityEngine;

namespace PathfindingDemo
{
    public class PathfindingDrawer : ImmediateModeShapeDrawer
    {
        [Header("Cell Properties")]
        public float cellSize = 1f;
        public float cellMargin = 0.1f;
        public float cellCornerRadius = 0.1f;
        public float cellOutlineThickness = 0.1f;

        [Header("Camera Margin (世界单位)")]
        [Tooltip("网格左侧保留的空白边距")]
        [Range(0, 10)]
        public float marginLeft = 1f;
        [Tooltip("网格右侧保留的空白边距")]
        [Range(0, 10)]
        public float marginRight = 1f;
        [Tooltip("网格上方保留的空白边距")]
        [Range(0, 10)]
        public float marginTop = 1f;
        [Tooltip("网格下方保留的空白边距")]
        [Range(0, 10)]
        public float marginBottom = 1f;

        private Color _blue;
        private Color _darkBlue;
        private Color _red;
        private Color _darkRed;
        private Color _cellWhite1;
        private Color _cellWhite2;

        public override void OnEnable()
        {
            base.OnEnable();
            ColorUtility.TryParseHtmlString("#0095FF", out _blue);
            ColorUtility.TryParseHtmlString("#00219A", out _darkBlue);
            ColorUtility.TryParseHtmlString("#FF1155", out _red);
            ColorUtility.TryParseHtmlString("#A00845", out _darkRed);
            ColorUtility.TryParseHtmlString("#ddd5d5", out _cellWhite1);
            ColorUtility.TryParseHtmlString("#ccbfb3", out _cellWhite2);
        }

        public override void DrawShapes(Camera cam)
        {
            if (PathfindingManager.Instance.Grid == null) return;

            using (Draw.Command(cam))
            {
                for (int x = 0; x < PathfindingManager.Instance.Grid.Width; x++)
                {
                    for (int y = 0; y < PathfindingManager.Instance.Grid.Height; y++)
                    {
                        var origin = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
                        var size = Vector2.one * cellSize * (1f - cellMargin);

                        // 绘制 Cell
                        Draw.Rectangle(origin, Quaternion.identity, size, cellCornerRadius, _cellWhite1);
                        // Draw.RectangleBorder(origin, Quaternion.identity, size, cellOutlineThickness, cellCornerRadius, _darkRed);
                    }
                }
            }
        }
    }
}
