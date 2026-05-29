using UnityEngine;

namespace PathfindingDemo
{
    public enum CellType
    {
        Normal,
        Obstacle,
    }
    public class Cell
    {
        public CellType Type { get; private set; }

        public int X { get; private set; }
        public int Y { get; private set; }

        private int _cost;

        public int Cost
        {
            get => Type is CellType.Obstacle ? int.MaxValue : _cost;
            private set => _cost = value;
        }

        public Cell(int x, int y, CellType type = CellType.Normal)
        {
            X = x;
            Y = y;
            Type = type;
        }
    }
}
