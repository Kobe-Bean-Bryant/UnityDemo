using System;
using UnityDemo.Shared;
using UnityEngine;

namespace PathfindingDemo
{
    public class PathfindingManager : Singleton<PathfindingManager>
    {
        protected override bool IsPersistent => false;

        public new Camera camera;
        public PathfindingDrawer pathfindingDrawer;

        [Header("Grid Properties")]
        public int width;
        public int height;

        public Grid Grid { get; private set; }

        private float _lastAspect;
        // 缓存四边 margin 用于轮询检测，分量为 (left, right, top, bottom)
        private Vector4 _lastMargins;

        private void Start()
        {
            Grid = new Grid(width,height);
            AdjustFieldOfView();
        }

        private void Update()
        {
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
    }
}
