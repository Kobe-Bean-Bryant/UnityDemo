using System.Collections.Generic;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract
{
    /// <summary>
    /// 局部空间命中测试小工具（纯数学，无 Shapes 依赖）。让「绘制几何」与「命中几何」
    /// 共用同一份定义，覆盖 Shapes 常见可绘制类型。所有判定都在 2D（z=0）局部空间。
    /// </summary>
    public static class ShapesHitArea
    {
        /// <summary>点是否在以 <paramref name="center"/> 为心、<paramref name="size"/> 为宽高的矩形内（轴对齐）。</summary>
        public static bool Box(Vector2 point, Vector2 center, Vector2 size)
        {
            Vector2 d = point - center;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f;
        }

        /// <summary>点是否在以 <paramref name="center"/> 为心、半径 <paramref name="radius"/> 的实心圆内。</summary>
        public static bool Circle(Vector2 point, Vector2 center, float radius)
            => (point - center).sqrMagnitude <= radius * radius;

        /// <summary>点是否在以 <paramref name="center"/> 为心的圆环内（<paramref name="inner"/>~<paramref name="outer"/> 之间）。</summary>
        public static bool Ring(Vector2 point, Vector2 center, float inner, float outer)
        {
            float sqr = (point - center).sqrMagnitude;
            return sqr >= inner * inner && sqr <= outer * outer;
        }

        /// <summary>
        /// 点是否在扇形/弧内：到 <paramref name="center"/> 的距离在 [<paramref name="inner"/>, <paramref name="outer"/>]，
        /// 且角度在 [<paramref name="fromAngleRad"/>, <paramref name="toAngleRad"/>]（弧度，逆时针）。
        /// 实心扇用 inner=0；整圆用 from=0,to=2π。
        /// </summary>
        public static bool Sector(Vector2 point, Vector2 center, float inner, float outer, float fromAngleRad, float toAngleRad)
        {
            Vector2 d = point - center;
            float sqr = d.sqrMagnitude;
            if (sqr < inner * inner || sqr > outer * outer) return false;

            float ang = Mathf.Atan2(d.y, d.x);                  // (-π, π]
            float sweep = toAngleRad - fromAngleRad;
            if (sweep >= Mathf.PI * 2f) return true;            // 整圈
            float rel = Mathf.Repeat(ang - fromAngleRad, Mathf.PI * 2f);
            return rel <= Mathf.Repeat(sweep, Mathf.PI * 2f);
        }

        /// <summary>点是否在「线段 <paramref name="a"/>→<paramref name="b"/> + 半宽 <paramref name="thickness"/>/2」的胶囊内（用于 Line/滑块轨道）。</summary>
        public static bool Capsule(Vector2 point, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / len2) : 0f;
            Vector2 closest = a + t * ab;
            float r = thickness * 0.5f;
            return (point - closest).sqrMagnitude <= r * r;
        }

        /// <summary>点是否在三角形 <paramref name="a"/>,<paramref name="b"/>,<paramref name="c"/> 内（符号面积法，顶点任意绕序）。</summary>
        public static bool Triangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(point, a, b);
            float d2 = Sign(point, b, c);
            float d3 = Sign(point, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        /// <summary>点是否在多边形 <paramref name="points"/> 内（射线交叉法，支持凹多边形）。</summary>
        public static bool Polygon(Vector2 point, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 pi = points[i], pj = points[j];
                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// 点是否落在折线 <paramref name="points"/>（每段半宽 <paramref name="thickness"/>/2 的胶囊链）上。
        /// <paramref name="closed"/> 时额外测末点→首点的闭合段。用于 Polyline 命中。
        /// </summary>
        public static bool PolylineCapsule(Vector2 point, IReadOnlyList<Vector2> points, float thickness, bool closed)
        {
            if (points == null || points.Count < 2) return false;
            for (int i = 0; i < points.Count - 1; i++)
                if (Capsule(point, points[i], points[i + 1], thickness)) return true;
            if (closed && points.Count > 2)
                return Capsule(point, points[points.Count - 1], points[0], thickness);
            return false;
        }

        /// <summary>
        /// 把局部点换算成网格 cell 索引。网格从 <paramref name="origin"/> 起、每格 <paramref name="cellSize"/>，
        /// 共 <paramref name="width"/>×<paramref name="height"/> 格。落在网格外返回 false。
        /// </summary>
        public static bool TryGetCell(Vector2 point, Vector2 origin, float cellSize, int width, int height, out Vector2Int cell)
        {
            cell = default;
            if (cellSize <= 0f) return false;
            int x = Mathf.FloorToInt((point.x - origin.x) / cellSize);
            int y = Mathf.FloorToInt((point.y - origin.y) / cellSize);
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            cell = new Vector2Int(x, y);
            return true;
        }

        /// <summary>把点绕 <paramref name="pivot"/> 旋转 <paramref name="radians"/> 弧度（逆时针）。radians=0 原样返回。</summary>
        public static Vector2 Rotate(Vector2 point, Vector2 pivot, float radians)
        {
            if (radians == 0f) return point;
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            Vector2 d = point - pivot;
            return new Vector2(pivot.x + d.x * c - d.y * s, pivot.y + d.x * s + d.y * c);
        }

        private static float Sign(Vector2 p, Vector2 a, Vector2 b)
            => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
