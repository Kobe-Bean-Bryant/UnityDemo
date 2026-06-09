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

        private int _cost = 1;

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

        public void ToggleType()
        {
            Type = Type == CellType.Obstacle ? CellType.Normal : CellType.Obstacle;
        }

        public void SetType(CellType type)
        {
            Type = type;
        }

        /// <summary>设置格子移动代价（仅对 Normal 类型有效）。</summary>
        public void SetCost(int cost)
        {
            _cost = Mathf.Clamp(cost, 1, 10);
        }
    }
}
