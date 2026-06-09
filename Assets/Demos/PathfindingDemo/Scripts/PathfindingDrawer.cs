using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shapes;
using UnityDemo.Shared.ShapesInteract;
using UnityDemo.Shared.ShapesInteract.Controls;
using UnityEngine;

namespace PathfindingDemo
{
    [ExecuteAlways]
    public class PathfindingDrawer : ImmediateModeShapeDrawer,
        IShapesRaycastTarget,
        IShapesPointerClickHandler
    {
        [SerializeField]
        private int sortingOrder;

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

        [Header("Star")]
        public float starOuterRadius = .67f;
        [Range(0.1f, 1f)]
        [Tooltip("内顶点半径占外顶点半径的比例（0.38=标准尖星, 0.5=胖星）")]
        public float starInnerRadiusRatio = 0.5f;
        [Range(0f, 0.3f)]
        [Tooltip("五角星顶点圆角半径（世界单位）")]
        public float starRoundRadius = 0.05f;
        public float starScaleFactor = 1.1f;

        [Header("Star Face")]
        [Tooltip("眼间距（占 outerR 的比例）")]
        [Range(0.05f, 0.5f)]
        public float faceEyeSpacing = 0.22f;
        [Tooltip("眼睛 Y 偏移（占 outerR 的比例，正值朝上）")]
        [Range(-0.3f, 0.5f)]
        public float faceEyeY = 0.15f;
        [Tooltip("眼白半径（占 outerR 的比例）")]
        [Range(0.02f, 0.25f)]
        public float faceEyeRadius = 0.1f;
        [Tooltip("眼白椭圆纵/横比（1=正圆，>1=纵向拉长）")]
        [Range(0.5f, 2f)]
        public float faceEyeEccentricity = 1.3f;
        [Tooltip("瞳孔半径（占 outerR 的比例）")]
        [Range(0.01f, 0.15f)]
        public float facePupilRadius = 0.06f;
        [Tooltip("瞳孔向内/向上偏移（占 outerR 的比例）")]
        [Range(0f, 0.1f)]
        public float facePupilOffset = 0.02f;
        [Tooltip("嘴巴 Y 基线偏移（占 outerR 的比例，相对 center.y）")]
        [Range(-0.3f, 0.2f)]
        public float faceMouthBaseOffset = -0.05f;
        [Tooltip("正常态嘴巴半宽（占 outerR 的比例）")]
        [Range(0.05f, 0.4f)]
        public float faceMouthWidth = 0.15f;
        [Tooltip("拖拽态嘴巴半宽（占 outerR 的比例）")]
        [Range(0.05f, 0.4f)]
        public float faceDragMouthWidth = 0.22f;
        [Tooltip("正常态嘴巴弧半径（占 outerR 的比例）")]
        [Range(0.05f, 0.3f)]
        public float faceMouthArcRadius = 0.12f;
        [Tooltip("拖拽态嘴巴弧半径（占 outerR 的比例）")]
        [Range(0.05f, 0.4f)]
        public float faceDragMouthArcRadius = 0.20f;
        [Tooltip("嘴巴线条粗细（占 outerR 的比例）")]
        [Range(0.01f, 0.1f)]
        public float faceMouthThickness = 0.035f;

        [Header("Cross")]
        public float crossArmLength = 0.4f;
        public float crossArmWidth = 0.15f;
        [Range(0f, 0.3f)]
        [Tooltip("十字叉顶点圆角半径（世界单位）")]
        public float crossRoundRadius = 0f;
        public float crossScaleFactor = 1.1f;

        private GridDraggable _star;
        private GridDraggable _cross;

        private Color _blue;
        private Color _darkBlue;
        private Color _red;
        private Color _darkRed;
        private Color _cellWhite1;
        private Color _cellWhite2;
        private Color _starColor;
        private Color _crossColor;
        private Color _obstacleColor;
        private Color _pathColor;

        private int Width => PathfindingManager.Instance.Grid.Width;
        private int Height => PathfindingManager.Instance.Grid.Height;

        public override void OnEnable()
        {
            base.OnEnable();
            InitColors();
            EnsureDraggables();
            if (Application.isPlaying)
                ShapesInteractionManager.Register(this);
        }

        private void InitColors()
        {
            ColorUtility.TryParseHtmlString("#0095FF", out _blue);
            ColorUtility.TryParseHtmlString("#00219A", out _darkBlue);
            ColorUtility.TryParseHtmlString("#FF1155", out _red);
            ColorUtility.TryParseHtmlString("#A00845", out _darkRed);
            ColorUtility.TryParseHtmlString("#ddd5d5", out _cellWhite1);
            ColorUtility.TryParseHtmlString("#ccbfb3", out _cellWhite2);
            ColorUtility.TryParseHtmlString("#bf4040", out _starColor);
            ColorUtility.TryParseHtmlString("#bf40aa", out _crossColor);
            ColorUtility.TryParseHtmlString("#868679", out _obstacleColor);
            ColorUtility.TryParseHtmlString("#9540bf", out _pathColor);
        }

        private void EnsureDraggables()
        {
            if (_star != null) return;
            var grid = PathfindingManager.Instance?.Grid;
            if (grid == null) return;

            _star = new GridDraggable(
                Math.Min(3, grid.Width - 1), Math.Min(3, grid.Height - 1),
                cellSize, starScaleFactor, () => Width, () => Height);

            _cross = new GridDraggable(
                Math.Max(0, grid.Width - 4), Math.Max(0, grid.Height - 4),
                cellSize, crossScaleFactor, () => Width, () => Height);
        }

        private void Start()
        {
            EnsureDraggables();
        }

        public override void DrawShapes(Camera cam)
        {
            var grid = PathfindingManager.Instance?.Grid;
            if (grid == null) return;

            using (IDraw.Command(cam, this))
            {
                var size = Vector2.one * cellSize * (1f - cellMargin);
                for (int x = 0; x < grid.Width; x++)
                {
                    for (int y = 0; y < grid.Height; y++)
                    {
                        var cell = grid.GetCell(x, y);
                        var color = cell != null && cell.Type == CellType.Obstacle
                            ? _obstacleColor
                            : _cellWhite1;
                        var origin = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
                        Draw.Rectangle(origin, Quaternion.identity, size, cellCornerRadius, color);
                    }
                }

                // 绘制路径
                if (_star != null && _cross != null)
                {
                    var pathVertices = PathfindingManager.Instance.GetPathVertices(
                        _star.PosIndex, _cross.PosIndex);

                    if (pathVertices.Count > 1)
                    {
                        using (var pathShape = new PolylinePath())
                        {
                            foreach (var pt in pathVertices)
                                pathShape.AddPoint(pt);
                            Draw.Polyline(pathShape, false, 0.15f, _pathColor);
                        }
                    }
                }

                if (_star != null)
                {
                    var star = IDraw.Polygon("star",
                        GetStarVertices(_star.Pos, starOuterRadius * _star.Scale, starInnerRadiusRatio),
                        starRoundRadius * _star.Scale, _starColor, 1);
                    star.OnDrag = _star.OnDrag;
                    star.OnUp = _star.OnUp;

                    DrawStarFace(_star.Pos, starOuterRadius * _star.Scale);
                }

                if (_cross != null)
                {
                    var cross = IDraw.Polygon("cross",
                        GetCrossVertices(_cross.Pos, crossArmLength * _cross.Scale, crossArmWidth * _cross.Scale),
                        crossRoundRadius, _crossColor, 1);
                    cross.OnDrag = _cross.OnDrag;
                    cross.OnUp = _cross.OnUp;
                }
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (Application.isPlaying)
                ShapesInteractionManager.Unregister(this);
        }

        public Transform Transform => transform;
        public int SortingOrder => sortingOrder;

        public bool ContainsLocalPoint(Vector2 p)
            => p.x >= 0f && p.x < Width * cellSize && p.y >= 0f && p.y < Height * cellSize;

        public void OnPointerClick(ShapesPointerEvent e)
        {
            var grid = PathfindingManager.Instance?.Grid;
            if (grid == null) return;

            if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, Width, Height, cellMargin, out var cell))
            {
                // 不允许在起点或终点上放置障碍
                if (_star != null && cell.x == _star.PosIndex.x && cell.y == _star.PosIndex.y) return;
                if (_cross != null && cell.x == _cross.PosIndex.x && cell.y == _cross.PosIndex.y) return;

                var gridCell = grid.GetCell(cell.x, cell.y);
                if (gridCell != null)
                {
                    gridCell.ToggleType();
                    Debug.Log($"[Grid] toggled cell ({cell.x}, {cell.y}) → {gridCell.Type}");
                }
            }
            else
            {
                Debug.Log("[Grid] clicked outside the grid");
            }
        }

        /// <summary>
        /// 生成标准五角星的10个顶点坐标（交替外顶点和内顶点）
        /// </summary>
        /// <param name="center">几何中心</param>
        /// <param name="outerRadius">外接圆半径（中心到外顶点距离）</param>
        /// <param name="innerRadiusRatio">内顶点半径占外顶点半径的比例</param>
        /// <param name="startAngleDegrees">第一个外顶点的起始角度（度），默认90°（尖朝上）</param>
        /// <returns>10个顶点的只读列表，顺序可用于连线绘制五角星</returns>
        public static IReadOnlyList<Vector2> GetStarVertices(
            Vector2 center,
            float outerRadius = 0.6f,
            float innerRadiusRatio = 0.5f,
            float startAngleDegrees = 90f)
        {
            double innerRadius = outerRadius * innerRadiusRatio;

            double startRad = startAngleDegrees * Math.PI / 180.0;
            double angleStep = 72.0 * Math.PI / 180.0; // 72° 弧度
            double offsetRad = 36.0 * Math.PI / 180.0; // 内顶点相对外顶点的偏移36°

            var vertices = new List<Vector2>(10);

            for (int k = 0; k < 5; k++)
            {
                // 外顶点角度
                double outerAngle = startRad + k * angleStep;
                float xOuter = center.x + (float)(outerRadius * Math.Cos(outerAngle));
                float yOuter = center.y + (float)(outerRadius * Math.Sin(outerAngle));
                vertices.Add(new Vector2(xOuter, yOuter));

                // 内顶点角度（在外顶点之后36°）
                double innerAngle = startRad + offsetRad + k * angleStep;
                float xInner = center.x + (float)(innerRadius * Math.Cos(innerAngle));
                float yInner = center.y + (float)(innerRadius * Math.Sin(innerAngle));
                vertices.Add(new Vector2(xInner, yInner));
            }

            return new ReadOnlyCollection<Vector2>(vertices);
        }

        /// <summary>
        /// 生成 X 形（×）十字叉的 12 个顶点坐标（CCW，4 条对角臂）。
        /// </summary>
        /// <param name="center">几何中心。</param>
        /// <param name="armLength">中心到臂端的距离。</param>
        /// <param name="armWidth">臂宽。</param>
        /// <returns>12 个顶点的只读列表。</returns>
        public static IReadOnlyList<Vector2> GetCrossVertices(
            Vector2 center,
            float armLength,
            float armWidth)
        {
            float sqrt2 = Mathf.Sqrt(2f);
            float halfW = armWidth * 0.5f;
            float A = (armLength - halfW) / sqrt2;
            float B = (armLength + halfW) / sqrt2;
            float h = armWidth / sqrt2;

            var verts = new Vector2[]
            {
                center + new Vector2(B, A), // 0  右上臂外角
                center + new Vector2(A, B), // 1  右上臂外角
                center + new Vector2(0, h), // 2  内角（上）
                center + new Vector2(-A, B), // 3  左上臂外角
                center + new Vector2(-B, A), // 4  左上臂外角
                center + new Vector2(-h, 0), // 5  内角（左）
                center + new Vector2(-B, -A), // 6  左下臂外角
                center + new Vector2(-A, -B), // 7  左下臂外角
                center + new Vector2(0, -h), // 8  内角（下）
                center + new Vector2(A, -B), // 9  右下臂外角
                center + new Vector2(B, -A), // 10 右下臂外角
                center + new Vector2(h, 0), // 11 内角（右）
            };

            return new ReadOnlyCollection<Vector2>(verts);
        }

        /// <summary>
        /// 在五角星上绘制面部特征（眼睛 + 嘴巴），使用原生 Draw 调用（纯装饰，不可交互）。
        /// 所有比例参数由 Inspector 的 Star Face 区域控制。
        /// </summary>
        private void DrawStarFace(Vector2 center, float outerR)
        {
            float s = outerR;

            // —— 眼睛 ——
            float eyeSpacing = s * faceEyeSpacing;
            float eyeY = s * faceEyeY;
            float eyeR = s * faceEyeRadius;
            float pupilR = s * facePupilRadius;
            float pupilOffset = s * facePupilOffset;

            Vector2 leftEye = center + new Vector2(-eyeSpacing, eyeY);
            Vector2 rightEye = center + new Vector2(eyeSpacing, eyeY);

            // 眼白（椭圆：Disc + 纵向缩放）
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= Matrix4x4.TRS(leftEye, Quaternion.identity, new Vector3(1f, faceEyeEccentricity, 1f));
                Draw.Disc(Vector3.zero, eyeR, Color.white);
            }

            using (Draw.MatrixScope)
            {
                Draw.Matrix *= Matrix4x4.TRS(rightEye, Quaternion.identity, new Vector3(1f, faceEyeEccentricity, 1f));
                Draw.Disc(Vector3.zero, eyeR, Color.white);
            }

            // 瞳孔（正圆，略偏内偏上）
            Draw.Disc(leftEye + new Vector2(pupilOffset, pupilOffset), pupilR, Color.black);
            Draw.Disc(rightEye + new Vector2(-pupilOffset, pupilOffset), pupilR, Color.black);

            // —— 嘴巴：统一圆弧方案 ——
            // 正常态：温和小弧微笑；拖拽态：更宽更深的圆弧
            // t ∈ [0,1] 表示拖拽程度
            float t = Mathf.Clamp01((_star.Scale - 1f) / (starScaleFactor - 1f));

            float mouthW = Mathf.Lerp(s * faceMouthWidth, s * faceDragMouthWidth, t);
            float arcR = Mathf.Lerp(s * faceMouthArcRadius, s * faceDragMouthArcRadius, t);
            float mouthBase = center.y + s * faceMouthBaseOffset;
            float mouthThick = s * faceMouthThickness;

            Vector2[] mouthPts = GenerateSmileArc(center, s, mouthBase, mouthW, arcR);

            using (var path = new PolylinePath())
            {
                for (int i = 0; i < mouthPts.Length; i++)
                    path.AddPoint(mouthPts[i]);
                Draw.Polyline(path, false, mouthThick, Color.black);
            }
        }

        /// <summary>生成圆弧微笑嘴巴的采样点。</summary>
        private static Vector2[] GenerateSmileArc(Vector2 center, float s, float mouthBase, float mouthW,
            float arcR)
        {
            float arcCenterY = mouthBase + arcR;
            int n = 16;
            var pts = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                // 角度从 π 到 2π（圆的下半部分 = ∪ 微笑弧）
                float theta = Mathf.PI + i / (float)(n - 1) * Mathf.PI;
                float x = center.x + arcR * Mathf.Cos(theta);
                float y = arcCenterY + arcR * Mathf.Sin(theta);
                // 钳制宽度
                x = Mathf.Clamp(x, center.x - mouthW, center.x + mouthW);
                pts[i] = new Vector2(x, y);
            }

            return pts;
        }
    }
}
