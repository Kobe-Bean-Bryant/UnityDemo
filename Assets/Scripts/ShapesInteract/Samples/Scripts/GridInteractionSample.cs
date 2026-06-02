using Shapes;
using UnityEngine;
using UnityEngine.Events;
using UnityDemo.Shared.ShapesInteract.Controls;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 大量动态格子的高效交互范式：<b>一个 Drawer 画全部 cell + 一个 target 命中整片区域</b>，
    /// 命中后用 <see cref="ShapesHitArea.TryGetCell"/> 算出是哪个 cell。无论多少 cell 都只有 1 个注册 target，
    /// O(1) 命中。可直接照搬到 PathfindingDemo 的 Grid（让 PathfindingDrawer 这样实现即可）。
    /// <para>
    /// 还演示<b>多层可交互 Shape</b>：同一个 Drawer 里用 <see cref="IDraw"/> 叠一个**可拖拽的斜矩形 token**
    /// （<c>SortingOrder=1</c> &gt; 网格 0）。token <b>最后画=渲染在上</b>、<b>SortingOrder 更高=点击优先</b>，
    /// 两轴在同一处设定、一致。可在 token 的<b>倾斜范围</b>内抓起拖动、松手吸附到最近格子；点其轴对齐外接框的角
    /// （斜矩形之外）会**穿透命中底下的格子**——一次验证旋转命中 + 多层优先 + 拖拽吸附 + 空格子照常交互。
    /// </para>
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

        [Header("Draggable Token（斜矩形：演示旋转命中 + 多层优先）")]
        [SerializeField]
        private float tokenAngle = 30f;
        [SerializeField]
        private Vector2 tokenSize = new Vector2(0.8f, 0.8f);
        [SerializeField]
        private float tokenCorner = 0.12f;
        [SerializeField]
        private Color tokenNormal = new Color(1f, 0.78f, 0.2f);
        [SerializeField]
        private Color tokenHover = new Color(1f, 0.88f, 0.45f);
        [SerializeField]
        private Color tokenPressed = new Color(0.9f, 0.62f, 0.1f);

        private Vector2Int _hovered = new Vector2Int(-1, -1);
        private Vector2Int _selected = new Vector2Int(-1, -1);
        private Vector3 _tokenPos;
        private bool _tokenInit;

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        public override void OnEnable()
        {
            base.OnEnable();
            if (!_tokenInit)
            {
                int cx = Mathf.Min(2, width - 1);
                int cy = Mathf.Min(2, height - 1);
                _tokenPos = new Vector3((cx + 0.5f) * cellSize, (cy + 0.5f) * cellSize, 0f);
                _tokenInit = true;
            }

            ShapesInteractionManager.Register(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ShapesInteractionManager.Unregister(this);
            IDraw.Release(this); // 清理 token 句柄
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
            // IDraw.Command 等价于 Draw.Command(cam) + Draw.Matrix=localToWorldMatrix，并支撑 token 句柄。
            using (IDraw.Command(cam, this))
            {
                float size = cellSize * (1f - cellGap);

                // 网格 cell：纯装饰，用原生 Draw（先画 → 在底层）
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

                // 可拖拽斜矩形 token：最后画=渲染在最上；SortingOrder=1=点击最优先（两轴一致）。
                var token = IDraw.Rectangle("grid-token", _tokenPos, tokenAngle, tokenSize, tokenCorner,
                    tokenNormal, tokenHover, tokenPressed, sortingOrder: 1);
                token.OnDrag = e => _tokenPos += (Vector3)e.LocalDelta; // 局部空间位移（不受斜矩形旋转干扰）
                token.OnUp = SnapTokenToCell; // 松手吸附到最近格子
            }
        }

        private void SnapTokenToCell()
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(_tokenPos.x / cellSize), 0, width - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(_tokenPos.y / cellSize), 0, height - 1);
            _tokenPos = new Vector3((cx + 0.5f) * cellSize, (cy + 0.5f) * cellSize, 0f);
        }
    }
}
