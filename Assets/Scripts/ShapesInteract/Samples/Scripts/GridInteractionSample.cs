using Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 大量动态格子的高效交互范式：<b>一个 Drawer 画全部 cell + 一个 target 命中整片区域</b>，
    /// 命中后用 <see cref="ShapesHitArea.TryGetCell"/> 算出是哪个 cell。无论多少 cell 都只有 1 个注册 target，
    /// O(1) 命中。可直接照搬到 PathfindingDemo 的 Grid（让 PathfindingDrawer 这样实现即可）。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Shapes UI/Samples/Grid Interaction (code)")]
    public class GridInteractionSample : ImmediateModeShapeDrawer,
        IShapesRaycastTarget,
        IShapesPointerExitHandler, IShapesPointerMoveHandler, IShapesPointerClickHandler
    {
        /// <summary>携带被点 cell 坐标的事件。</summary>
        [System.Serializable]
        public class CellEvent : UnityEvent<Vector2Int>
        {
        }

        [Header("Grid")]
        [SerializeField]
        private int width = 8;
        [SerializeField]
        private int height = 6;
        [SerializeField]
        private float cellSize = 1f;
        [Range(0f, 0.9f)]
        [SerializeField]
        private float cellGap = 0.12f;
        [SerializeField]
        private float cornerRadius = 0.1f;
        [SerializeField]
        private int sortingOrder;

        [Header("Colors")]
        [SerializeField]
        private Color cellColor = new Color(0.20f, 0.22f, 0.28f);
        [SerializeField]
        private Color hoverColor = new Color(0.0f, 0.58f, 1f);
        [SerializeField]
        private Color selectedColor = new Color(1f, 0.07f, 0.33f);

        public CellEvent onCellClicked = new CellEvent();

        private Vector2Int _hovered = new Vector2Int(-1, -1);
        private Vector2Int _selected = new Vector2Int(-1, -1);

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

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

        // 命中区 = 整个网格矩形（从局部原点向 +X/+Y 延伸）
        public bool ContainsLocalPoint(Vector2 p)
            => p.x >= 0f && p.x < width * cellSize && p.y >= 0f && p.y < height * cellSize;

        public void OnPointerExit(ShapesPointerEvent e) => _hovered = new Vector2Int(-1, -1);

        public void OnPointerMove(ShapesPointerEvent e)
        {
            if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var cell))
                _hovered = cell;
        }

        public void OnPointerClick(ShapesPointerEvent e)
        {
            if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var cell))
            {
                _selected = cell;
                Debug.Log($"[Grid] clicked cell ({cell.x}, {cell.y})");
                onCellClicked.Invoke(cell);
            }
        }

        public override void DrawShapes(Camera cam)
        {
            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;
                float size = cellSize * (1f - cellGap);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var center = new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
                        Color c = (x == _selected.x && y == _selected.y) ? selectedColor
                            : (x == _hovered.x && y == _hovered.y) ? hoverColor
                            : cellColor;
                        Draw.Rectangle(center, new Vector2(size, size), cornerRadius, c);
                    }
                }
            }
        }
    }
}
