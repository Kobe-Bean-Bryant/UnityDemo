using System.Collections.Generic;
using UnityEngine;

namespace PathfindingDemo
{
    public class Grid
    {
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private readonly Cell[,] _cells;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public Grid(int width = 20, int height = 20)
        {
            Width = width;
            Height = height;
            _cells = new Cell[Width, Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _cells[x, y] = new Cell(x, y);
                }
            }
        }

        public Cell GetCell(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return _cells[x, y];
        }

        public Cell GetCell(Vector2Int index) => GetCell(index.x, index.y);

        /// <summary>
        /// 获取指定格子的四方向可达邻居（跳过越界和障碍）。
        /// </summary>
        public List<Vector2Int> GetNeighbors(int x, int y)
        {
            var result = new List<Vector2Int>(4);
            foreach (var dir in Directions)
            {
                int nx = x + dir.x;
                int ny = y + dir.y;
                var cell = GetCell(nx, ny);
                if (cell != null && cell.Type != CellType.Obstacle)
                    result.Add(new Vector2Int(nx, ny));
            }

            return result;
        }

        public List<Vector2Int> GetNeighbors(Vector2Int index) => GetNeighbors(index.x, index.y);
    }
}
