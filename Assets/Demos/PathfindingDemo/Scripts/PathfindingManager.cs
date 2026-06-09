using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityDemo.Shared;
using UnityEngine;

namespace PathfindingDemo
{
    [ExecuteAlways]
    public class PathfindingManager : Singleton<PathfindingManager>
    {
        protected override bool IsPersistent => false;

        public new Camera camera;
        public PathfindingDrawer pathfindingDrawer;

        [Header("Grid Properties")]
        public int width = 10;
        public int height = 10;

        public Grid Grid { get; private set; }

        private float _lastAspect;
        // 缓存四边 margin 用于轮询检测，分量为 (left, right, top, bottom)
        private Vector4 _lastMargins;

        protected override void Awake()
        {
            base.Awake();
            EnsureGrid();
            if (camera != null && pathfindingDrawer != null)
                AdjustFieldOfView();
        }

        /// <summary>确保 Grid 存在且尺寸与序列化字段一致（编辑模式 + 运行时通用）。</summary>
        public void EnsureGrid()
        {
            if (Grid == null || Grid.Width != width || Grid.Height != height)
                Grid = new Grid(width, height);
        }

        // 编辑模式下 Inspector 修改 width/height 时触发重建
        private void OnValidate()
        {
            EnsureGrid();
            if (camera != null) AdjustFieldOfView();
        }

        private void Update()
        {
            if (camera == null || pathfindingDrawer == null) return;
            if (!Mathf.Approximately(camera.aspect, _lastAspect) || CurrentMargins() != _lastMargins)
                AdjustFieldOfView();
        }

        // 读取 Drawer 的四边 margin，打包为 (left, right, top, bottom)
        private Vector4 CurrentMargins() => new Vector4(
            pathfindingDrawer.marginLeft, pathfindingDrawer.marginRight,
            pathfindingDrawer.marginTop, pathfindingDrawer.marginBottom);

        /// <summary>
        /// 调整正交相机，使其视野刚好贴合整个 Grid，并在四边保留 Drawer 上设置的世界单位边距
        /// （<see cref="PathfindingDrawer.marginLeft"/> / <see cref="PathfindingDrawer.marginRight"/> /
        /// <see cref="PathfindingDrawer.marginTop"/> / <see cref="PathfindingDrawer.marginBottom"/>）。
        /// <para>
        /// 做法：以「网格 + 四边 margin」组成的包围盒为目标，相机对准盒子中心，再按宽高比缩放。
        /// 正交相机的可见高度为 orthographicSize*2，可见宽度为 orthographicSize·aspect*2；
        /// orthographicSize 取宽、高两个方向约束的最大值，从而在任意宽高比（含竖屏）下都不会裁切网格。
        /// 注意：四边 margin 不对称时网格不再居中，相机中心会向 margin 较大的一侧偏移。
        /// </para>
        /// <para>会在 <c>Start</c> 时调用一次，并在 Game 视图 / 屏幕宽高比或任一边 margin 变化时由 <c>Update</c> 重新调用。</para>
        /// </summary>
        public void AdjustFieldOfView()
        {
            var cellSize = pathfindingDrawer.cellSize;
            var left = pathfindingDrawer.marginLeft;
            var right = pathfindingDrawer.marginRight;
            var top = pathfindingDrawer.marginTop;
            var bottom = pathfindingDrawer.marginBottom;

            var worldWidth = Grid.Width * cellSize;
            var worldHeight = Grid.Height * cellSize;

            // 「网格 + 四边 margin」包围盒的尺寸
            var boxWidth = worldWidth + left + right;
            var boxHeight = worldHeight + top + bottom;

            // 相机对准包围盒中心：margin 不对称时向 margin 较大的一侧偏移
            camera.transform.position = new Vector3(
                worldWidth / 2f + (right - left) / 2f,
                worldHeight / 2f + (top - bottom) / 2f,
                camera.transform.position.z);

            // 同时满足宽、高两个方向的约束，取较大的 size 避免裁切
            var sizeByHeight = boxHeight / 2f;
            var sizeByWidth = boxWidth / (2f * camera.aspect);
            camera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);

            _lastAspect = camera.aspect;
            _lastMargins = new Vector4(left, right, top, bottom);
        }

        public void ResizeGrid(int w, int h)
        {
            width = w;
            height = h;
            Grid = new Grid(w, h);
            AdjustFieldOfView();
        }

        /// <summary>
        /// 使用 A* 算法计算从 starIndex 到 crossIndex 的最短路径，返回路径上各格子中心的世界坐标。
        /// <para>
        /// 算法参考：https://www.redblobgames.com/pathfinding/a-star/introduction.html
        /// </para>
        /// </summary>
        /// <param name="starIndex">起点格子索引</param>
        /// <param name="crossIndex">终点格子索引</param>
        /// <returns>路径顶点列表（含起止点）；无路径时返回空列表</returns>
        public IReadOnlyList<Vector2> GetPathVertices(Vector2Int starIndex, Vector2Int crossIndex)
        {
            var vertices = new List<Vector2>();
            if (Grid == null) return new ReadOnlyCollection<Vector2>(vertices);

            var startCell = Grid.GetCell(starIndex);
            var goalCell = Grid.GetCell(crossIndex);
            if (startCell == null || goalCell == null) return new ReadOnlyCollection<Vector2>(vertices);
            if (startCell.Type == CellType.Obstacle || goalCell.Type == CellType.Obstacle)
                return new ReadOnlyCollection<Vector2>(vertices);

            // A* 算法
            var frontier = new PriorityQueue<Vector2Int, float>();
            frontier.Enqueue(starIndex, 0f);

            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [starIndex] = starIndex };
            var costSoFar = new Dictionary<Vector2Int, float> { [starIndex] = 0f };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == crossIndex) break;

                foreach (var next in Grid.GetNeighbors(current))
                {
                    var nextCell = Grid.GetCell(next);
                    float newCost = costSoFar[current] + nextCell.Cost;

                    if (!costSoFar.TryGetValue(next, out float prevCost) || newCost < prevCost)
                    {
                        costSoFar[next] = newCost;
                        float priority = newCost + Heuristic(crossIndex, next);
                        frontier.Enqueue(next, priority);
                        cameFrom[next] = current;
                    }
                }
            }

            // 路径回溯
            if (!cameFrom.ContainsKey(crossIndex))
                return new ReadOnlyCollection<Vector2>(vertices);

            var path = new List<Vector2Int>();
            var step = crossIndex;
            while (step != starIndex)
            {
                path.Add(step);
                step = cameFrom[step];
            }

            path.Add(starIndex);
            path.Reverse();

            // 转换为世界坐标（格子中心）
            var cellSize = pathfindingDrawer != null ? pathfindingDrawer.cellSize : 1f;
            foreach (var idx in path)
                vertices.Add(new Vector2((idx.x + 0.5f) * cellSize, (idx.y + 0.5f) * cellSize));

            return new ReadOnlyCollection<Vector2>(vertices);
        }

        /// <summary>曼哈顿距离启发函数（四方向网格）。</summary>
        private static float Heuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
