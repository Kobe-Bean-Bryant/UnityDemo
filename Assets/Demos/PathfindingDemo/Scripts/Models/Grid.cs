using UnityEngine;

namespace PathfindingDemo
{
    public class Grid
    {
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
    }
}
