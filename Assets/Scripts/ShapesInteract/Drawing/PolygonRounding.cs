using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Drawing
{
    /// <summary>
    /// 多边形圆角工具——为任意简单多边形（含凹角）生成带圆角的 <see cref="PolygonPath"/>。
    /// <para>
    /// 仅依赖 ShapesRuntime，不依赖 ShapesInteract 交互层。
    /// 可独立用于 <c>Draw.Polygon</c> 的纯视觉圆角多边形绘制，
    /// 也可通过 <c>IDraw.Polygon(id, points, roundRadius, ...)</c> 用于可交互场景。
    /// </para>
    /// <para>
    /// 核心算法：对每个顶点，沿两条边各退 t = r/tan(α/2) 得到切点 P1、P2，
    /// 再沿角平分线 (-dIn + dOut) 放置圆心 C = B + bisector × r/sin(α/2)，
    /// 最后在 P1→P2 之间做 atan2 角度扫描生成弧线点。
    /// </para>
    /// <para>
    /// 关键特性：角平分线 (-dIn + dOut) 对凸角自然指向外侧、对凹角自然指向外侧，
    /// 圆心始终落在多边形外，无需凸/凹判定或符号翻转。
    /// 弧线用 atan2 + 角度扫描（非 Slerp），保证方向与多边形缠绕一致。
    /// </para>
    /// <para>数学原理详见 POLYGON_ROUNDING_MATH.md 文档（同目录下）。</para>
    /// </summary>
    public static class PolygonRounding
    {
        /// <summary>
        /// 构建带圆角的 <see cref="PolygonPath"/>。
        /// </summary>
        /// <param name="verts">多边形顶点数组（缠绕方向不限）。</param>
        /// <param name="roundRadius">圆角半径（世界单位）。≤0 时退化为平直多边形。</param>
        /// <returns>可用于 <c>Draw.Polygon</c> 的路径。调用方负责 <c>Dispose()</c>。</returns>
        public static PolygonPath BuildRoundedPath(Vector2[] verts, float roundRadius)
        {
            int n = verts.Length;
            var path = new PolygonPath();

            if (n < 3 || roundRadius <= 0.0001f)
            {
                path.AddPoints(verts);
                return path;
            }

            float pointsPerTurn = ShapesConfig.Instance.polylineDefaultPointsPerTurn;
            float twoPi = Mathf.PI * 2f;

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;
                Vector2 B = verts[i];
                Vector2 A = verts[prev];
                Vector2 C = verts[next];

                // 沿两条边远离 B 的单位方向
                Vector2 dIn = (B - A).normalized;
                Vector2 dOut = (C - B).normalized;

                // 接近共线或折叠 → 退化为顶点
                float edgeDot = Vector2.Dot(dIn, dOut);
                if (edgeDot > 0.999f || edgeDot < -0.999f)
                {
                    path.AddPoint(B);
                    continue;
                }

                // 两边夹角 α ∈ (0, π)
                float cosAngle = Mathf.Clamp(-edgeDot, -1f, 1f);
                float angle = Mathf.Acos(cosAngle);
                float halfAngle = angle * 0.5f;
                float sinHalf = Mathf.Sin(halfAngle);
                float tanHalf = Mathf.Tan(halfAngle);

                // 切点距顶点距离 t，钳制到不超过半边长
                float r = roundRadius;
                float t = r / tanHalf;
                float maxT = Mathf.Min((B - A).magnitude, (C - B).magnitude) * 0.49f;
                if (t > maxT)
                {
                    t = maxT;
                    r = t * tanHalf;
                }

                // 切点（落在各自边上）
                Vector2 P1 = B - dIn * t;
                Vector2 P2 = B + dOut * t;

                // 角平分线方向：始终指向圆心正确一侧
                Vector2 bisector = (-dIn + dOut).normalized;
                Vector2 center = B + bisector * (r / sinHalf);

                // 圆弧：从 P1 到 P2 绕 center 角度扫描
                float startAngle = Mathf.Atan2(P1.y - center.y, P1.x - center.x);
                float endAngle = Mathf.Atan2(P2.y - center.y, P2.x - center.x);
                float sweep = endAngle - startAngle;
                if (sweep > Mathf.PI) sweep -= twoPi;
                if (sweep < -Mathf.PI) sweep += twoPi;

                int pointCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(sweep) / twoPi * pointsPerTurn));
                for (int j = 0; j < pointCount; j++)
                {
                    float frac = pointCount <= 1 ? 0f : j / (float)(pointCount - 1);
                    float a = startAngle + sweep * frac;
                    path.AddPoint(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            return path;
        }
    }
}
