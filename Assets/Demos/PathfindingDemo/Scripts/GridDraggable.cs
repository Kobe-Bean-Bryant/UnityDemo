using System;
using UnityDemo.Shared.ShapesInteract;
using UnityEngine;

namespace PathfindingDemo
{
    /// <summary>
    /// 轻量级可拖拽状态对象——封装一个可吸附网格的 shape 的全部拖拽数据与行为。
    /// <para>
    /// 构造一次，每帧由 <see cref="PathfindingDrawer.DrawShapes"/> 读取 <see cref="Pos"/>/<see cref="Scale"/>
    /// 作为绘制参数；<see cref="OnDrag"/>/<see cref="OnUp"/> 直接赋给
    /// <c>InteractiveShapeHandle</c> 的同名委托槽。
    /// </para>
    /// </summary>
    public sealed class GridDraggable
    {
        // ── 每帧可变状态（由 OnDrag / OnUp 写入，由 DrawShapes 读取）──

        /// <summary>吸附后的网格格子中心位置。</summary>
        public Vector2 Pos;

        /// <summary>当前所在格子索引。</summary>
        public Vector2Int PosIndex;

        /// <summary>视觉位置（拖拽中跟随指针，松手后回归 <see cref="Pos"/>）。</summary>
        public Vector2 DragPos;

        /// <summary>当前缩放倍数（1 = 正常态，>1 = 拖拽态放大）。</summary>
        public float Scale = 1f;

        // ── 配置（构造时固定）──

        /// <summary>拖拽时的放大倍数。</summary>
        public float ScaleFactor { get; }

        // ── 外部依赖（lambda 注入，避免 stale-value）──

        private readonly float _cellSize;
        private readonly Func<int> _getWidth;
        private readonly Func<int> _getHeight;

        /// <summary>
        /// 构造一个可拖拽对象。
        /// </summary>
        /// <param name="startX">初始格子 X。</param>
        /// <param name="startY">初始格子 Y。</param>
        /// <param name="cellSize">格子大小（世界单位）。</param>
        /// <param name="scaleFactor">拖拽放大倍数。</param>
        /// <param name="getWidth">获取当前网格宽度的委托。</param>
        /// <param name="getHeight">获取当前网格高度的委托。</param>
        public GridDraggable(int startX, int startY, float cellSize, float scaleFactor,
            Func<int> getWidth, Func<int> getHeight)
        {
            _cellSize = cellSize;
            ScaleFactor = scaleFactor;
            _getWidth = getWidth;
            _getHeight = getHeight;

            PosIndex = new Vector2Int(startX, startY);
            Pos = CellCenter(startX, startY);
            DragPos = Pos;
        }

        /// <summary>
        /// 拖拽回调——吸附到最近格子，同时放大视觉比例。
        /// 直接赋给 <c>InteractiveShapeHandle.OnDrag</c>。
        /// </summary>
        public void OnDrag(ShapesPointerEvent e)
        {
            DragPos = e.LocalPoint;
            Scale = ScaleFactor;

            int cx = Mathf.Clamp(Mathf.FloorToInt(DragPos.x / _cellSize), 0, _getWidth() - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(DragPos.y / _cellSize), 0, _getHeight() - 1);

            if (PosIndex.x != cx || PosIndex.y != cy)
            {
                PosIndex = new Vector2Int(cx, cy);
                Pos = CellCenter(cx, cy);
                DragPos = Pos;
            }
        }

        /// <summary>
        /// 松手回调——重置视觉比例。
        /// 直接赋给 <c>InteractiveShapeHandle.OnUp</c>。
        /// </summary>
        public void OnUp(ShapesPointerEvent e)
        {
            Scale = 1f;
        }

        private Vector2 CellCenter(int x, int y)
            => new Vector2((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize);
    }
}
