using System.Collections.Generic;
using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// <see cref="IDraw"/> 的图形绘制方法部分（与 <c>IDraw.cs</c> 同属一个 <c>partial</c> 类，
    /// 镜像 Shapes 自己 <c>Draw.cs</c> / <c>DrawOverloads.cs</c> 的拆法）。
    /// <para>
    /// 每个方法 <c>Ensure(id)</c> 取/建句柄 → <c>h.SetXXX(...)</c> 写入命中几何 → 调对应 <c>Draw.XXX(...)</c> 绘制 → 返回句柄。
    /// 仅覆盖能在局部 z=0 平面做 2D 命中的图元；3D 图元（Sphere/Cuboid/Cone/Torus 等）不在此列。
    /// </para>
    /// </summary>
    public static partial class IDraw
    {
        // —— 单色 ——

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, Vector2 size, float cornerRadius,
            Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            Draw.Rectangle(center, size, cornerRadius, color);
            return h;
        }

        public static InteractiveShapeHandle Disc(string id, Vector3 center, float radius, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetCircle(center, radius);
            Draw.Disc(center, radius, color);
            return h;
        }

        public static InteractiveShapeHandle Ring(string id, Vector3 center, float radius, float thickness, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetRing(center, radius - thickness * 0.5f, radius + thickness * 0.5f);
            Draw.Ring(center, radius, thickness, color);
            return h;
        }

        public static InteractiveShapeHandle Triangle(string id, Vector3 a, Vector3 b, Vector3 c, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetTriangle(a, b, c);
            Draw.Triangle(a, b, c, color);
            return h;
        }

        /// <summary>线段（命中区为半宽 <paramref name="thickness"/>/2 的胶囊）。</summary>
        public static InteractiveShapeHandle Line(string id, Vector3 a, Vector3 b, float thickness, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetCapsule(a, b, thickness);
            Draw.Line(a, b, thickness, color);
            return h;
        }

        /// <summary>实心扇形（命中区为 inner=0 的扇区）。角度单位弧度、逆时针。</summary>
        public static InteractiveShapeHandle Pie(string id, Vector3 center, float radius, float from, float to,
            Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, 0f, radius, from, to);
            Draw.Pie(center, radius, from, to, color);
            return h;
        }

        /// <summary>弧（命中区为 [radius-thickness/2, radius+thickness/2] 的环形扇区）。角度单位弧度、逆时针。</summary>
        public static InteractiveShapeHandle Arc(string id, Vector3 center, float radius, float thickness,
            float from, float to, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, radius - thickness * 0.5f, radius + thickness * 0.5f, from, to);
            Draw.Arc(center, radius, thickness, from, to, color);
            return h;
        }

        /// <summary>任意多边形（命中区为多边形内部，支持凹多边形）。每帧构建 PolygonPath 有少量 GC。</summary>
        public static InteractiveShapeHandle Polygon(string id, IReadOnlyList<Vector2> points, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            Vector2[] verts = ToArray(points);
            h.SetPolygon(verts);
            Draw.Polygon(BuildPolygonPath(verts), color);
            return h;
        }

        /// <summary>折线（命中区为半宽 <paramref name="thickness"/>/2 的胶囊链；<paramref name="closed"/> 时含闭合段）。每帧构建 PolylinePath 有少量 GC。</summary>
        public static InteractiveShapeHandle Polyline(string id, IReadOnlyList<Vector2> points, bool closed,
            float thickness, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            Vector2[] verts = ToArray(points);
            h.SetPolyline(verts, thickness, closed);
            Draw.Polyline(BuildPolylinePath(verts), closed, thickness, color);
            return h;
        }

        /// <summary>四边形（命中区为四点构成的多边形）。</summary>
        public static InteractiveShapeHandle Quad(string id, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color,
            int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(new[] { (Vector2)a, (Vector2)b, (Vector2)c, (Vector2)d });
            Draw.Quad(a, b, c, d, color);
            return h;
        }

        /// <summary>正多边形（命中区为按 <paramref name="sideCount"/>/<paramref name="angle"/> 算出的顶点多边形，不含 roundness）。角度单位弧度。</summary>
        public static InteractiveShapeHandle RegularPolygon(string id, Vector3 center, float radius, int sideCount,
            float angle, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(RegularPolygonVerts(center, radius, sideCount, angle));
            Draw.RegularPolygon(center, sideCount, radius, angle, color);
            return h;
        }

        // —— 四态颜色重载（实心可填充图元）：按句柄实时状态自动选色，hover/press 开箱即用 ——

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, Vector2 size, float cornerRadius,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            Draw.Rectangle(center, size, cornerRadius, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Disc(string id, Vector3 center, float radius, Color normal, Color hover,
            Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetCircle(center, radius);
            Draw.Disc(center, radius, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Triangle(string id, Vector3 a, Vector3 b, Vector3 c,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetTriangle(a, b, c);
            Draw.Triangle(a, b, c, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Pie(string id, Vector3 center, float radius, float from, float to,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, 0f, radius, from, to);
            Draw.Pie(center, radius, from, to, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Polygon(string id, IReadOnlyList<Vector2> points,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            Vector2[] verts = ToArray(points);
            h.SetPolygon(verts);
            Draw.Polygon(BuildPolygonPath(verts), Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle Quad(string id, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(new[] { (Vector2)a, (Vector2)b, (Vector2)c, (Vector2)d });
            Draw.Quad(a, b, c, d, Pick(h, normal, hover, pressed));
            return h;
        }

        public static InteractiveShapeHandle RegularPolygon(string id, Vector3 center, float radius, int sideCount,
            float angle, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(RegularPolygonVerts(center, radius, sideCount, angle));
            Draw.RegularPolygon(center, sideCount, radius, angle, Pick(h, normal, hover, pressed));
            return h;
        }

        // —— 旋转重载（rotation 紧跟 center，属 [Positioning]，单位度数）——
        // 仅 Rectangle / Pie / Arc：2D 内绕 z 旋转有视觉效果且无现成角度旋钮。
        // 绘制绕 center 旋转 rotation 度、命中端逆旋转同一 (center, rotation)，二者同步。

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, float rotation, Vector2 size,
            float cornerRadius, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            h.SetRotation(rotation * Mathf.Deg2Rad);
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= RotationAbout(center, rotation);
                Draw.Rectangle(center, size, cornerRadius, color);
            }

            return h;
        }

        public static InteractiveShapeHandle Rectangle(string id, Vector3 center, float rotation, Vector2 size,
            float cornerRadius, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetBox(center, size);
            h.SetRotation(rotation * Mathf.Deg2Rad);
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= RotationAbout(center, rotation);
                Draw.Rectangle(center, size, cornerRadius, Pick(h, normal, hover, pressed));
            }

            return h;
        }

        public static InteractiveShapeHandle Pie(string id, Vector3 center, float rotation, float radius,
            float from, float to, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, 0f, radius, from, to);
            h.SetRotation(rotation * Mathf.Deg2Rad);
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= RotationAbout(center, rotation);
                Draw.Pie(center, radius, from, to, color);
            }

            return h;
        }

        public static InteractiveShapeHandle Pie(string id, Vector3 center, float rotation, float radius,
            float from, float to, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, 0f, radius, from, to);
            h.SetRotation(rotation * Mathf.Deg2Rad);
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= RotationAbout(center, rotation);
                Draw.Pie(center, radius, from, to, Pick(h, normal, hover, pressed));
            }

            return h;
        }

        public static InteractiveShapeHandle Arc(string id, Vector3 center, float rotation, float radius,
            float thickness, float from, float to, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetSector(center, radius - thickness * 0.5f, radius + thickness * 0.5f, from, to);
            h.SetRotation(rotation * Mathf.Deg2Rad);
            using (Draw.MatrixScope)
            {
                Draw.Matrix *= RotationAbout(center, rotation);
                Draw.Arc(center, radius, thickness, from, to, color);
            }

            return h;
        }

        // —— 圆角/roundness 扩展重载：补全 Shapes Draw 原生支持但 IDraw 未暴露的能力参数 ——

        /// <summary>
        /// 任意多边形，所有顶点以 <paramref name="roundRadius"/> 为半径做圆角。
        /// <para>凸角（外顶点）由 Shapes 原生 <c>PolygonPath.ArcTo</c> 处理；
        /// 凹角（内顶点）由 <see cref="BuildRoundedPolygonPath"/> 翻转圆心方向处理，
        /// 确保 ArcTo 圆心落在多边形内侧，避免自相交。</para>
        /// <para>命中区仍用原始顶点平直多边形（<c>h.SetPolygon</c>），圆角半径较小时差异可忽略。</para>
        /// </summary>
        /// <param name="id">跨帧持久的句柄标识。</param>
        /// <param name="points">多边形顶点列表（缠绕方向不限）。</param>
        /// <param name="roundRadius">每个顶点的圆角半径（世界单位）。传 0 等同于无圆角版。</param>
        /// <param name="color">填充颜色。</param>
        /// <param name="sortingOrder">命中排序。</param>
        public static InteractiveShapeHandle Polygon(string id, IReadOnlyList<Vector2> points,
            float roundRadius, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            Vector2[] verts = ToArray(points);
            h.SetPolygon(verts);
            using (var path = BuildRoundedPolygonPath(verts, roundRadius))
                Draw.Polygon(path, color);
            return h;
        }

        /// <inheritdoc cref="Polygon(string,System.Collections.Generic.IReadOnlyList{UnityEngine.Vector2},float,UnityEngine.Color,int)"/>
        public static InteractiveShapeHandle Polygon(string id, IReadOnlyList<Vector2> points,
            float roundRadius, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            Vector2[] verts = ToArray(points);
            h.SetPolygon(verts);
            using (var path = BuildRoundedPolygonPath(verts, roundRadius))
                Draw.Polygon(path, Pick(h, normal, hover, pressed));
            return h;
        }

        /// <summary>
        /// 正多边形，支持 Shapes 原生 <c>roundness</c> 参数（0=锐角，1=完全圆润）。
        /// <para>命中区为不含 roundness 的平直顶点多边形。</para>
        /// </summary>
        /// <param name="id">跨帧持久的句柄标识。</param>
        /// <param name="center">几何中心。</param>
        /// <param name="radius">中心到顶点的距离。</param>
        /// <param name="sideCount">边数（≥3）。</param>
        /// <param name="angle">首个顶点朝向角度（弧度）。</param>
        /// <param name="roundness">圆角程度，0~1。0 为锐角，1 为完全圆润。</param>
        /// <param name="color">填充颜色。</param>
        /// <param name="sortingOrder">命中排序。</param>
        public static InteractiveShapeHandle RegularPolygon(string id, Vector3 center, float radius, int sideCount,
            float angle, float roundness, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(RegularPolygonVerts(center, radius, sideCount, angle));
            Draw.RegularPolygon(center, sideCount, radius, angle, roundness, color);
            return h;
        }

        /// <inheritdoc cref="RegularPolygon(string,UnityEngine.Vector3,float,int,float,float,UnityEngine.Color,int)"/>
        public static InteractiveShapeHandle RegularPolygon(string id, Vector3 center, float radius, int sideCount,
            float angle, float roundness, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetPolygon(RegularPolygonVerts(center, radius, sideCount, angle));
            Draw.RegularPolygon(center, sideCount, radius, angle, roundness, Pick(h, normal, hover, pressed));
            return h;
        }

        /// <summary>
        /// 三角形，支持 Shapes 原生 <c>roundness</c> 参数（0=锐角，1=完全圆润）。
        /// <para>命中区仍用原始三角形（不含 roundness）。</para>
        /// </summary>
        /// <param name="id">跨帧持久的句柄标识。</param>
        /// <param name="a">顶点 A。</param>
        /// <param name="b">顶点 B。</param>
        /// <param name="c">顶点 C。</param>
        /// <param name="roundness">圆角程度，0~1。0 为锐角，1 为完全圆润。</param>
        /// <param name="color">填充颜色。</param>
        /// <param name="sortingOrder">命中排序。</param>
        public static InteractiveShapeHandle Triangle(string id, Vector3 a, Vector3 b, Vector3 c,
            float roundness, Color color, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetTriangle(a, b, c);
            Draw.Triangle(a, b, c, roundness, color);
            return h;
        }

        /// <inheritdoc cref="Triangle(string,UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3,float,UnityEngine.Color,int)"/>
        public static InteractiveShapeHandle Triangle(string id, Vector3 a, Vector3 b, Vector3 c,
            float roundness, Color normal, Color hover, Color pressed, int sortingOrder = 0)
        {
            var h = Ensure(id, sortingOrder);
            h.SetTriangle(a, b, c);
            Draw.Triangle(a, b, c, roundness, Pick(h, normal, hover, pressed));
            return h;
        }

        // —— internals ——

        /// <summary>绕 <paramref name="pivot"/> 旋转 <paramref name="deg"/> 度的局部矩阵（叠加到 Draw.Matrix 上）。</summary>
        private static Matrix4x4 RotationAbout(Vector3 pivot, float deg)
            => Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, deg)) *
               Matrix4x4.Translate(-pivot);

        private static Vector2[] ToArray(IReadOnlyList<Vector2> points)
        {
            int n = points?.Count ?? 0;
            var arr = new Vector2[n]; // TODO: 可按 id 缓存数组/Path 以省去每帧 GC（点不变时复用）
            for (int i = 0; i < n; i++) arr[i] = points[i];
            return arr;
        }

        private static PolygonPath BuildPolygonPath(Vector2[] verts)
        {
            var path = new PolygonPath();
            path.AddPoints(verts);
            return path;
        }

        private static PolylinePath BuildPolylinePath(Vector2[] verts)
        {
            var path = new PolylinePath();
            for (int i = 0; i < verts.Length; i++) path.AddPoint(verts[i]);
            return path;
        }

        private static Vector2[] RegularPolygonVerts(Vector2 center, float radius, int sideCount, float angle)
        {
            if (sideCount < 3) sideCount = 3;
            var verts = new Vector2[sideCount];
            float step = Mathf.PI * 2f / sideCount;
            for (int i = 0; i < sideCount; i++)
            {
                float a = angle + i * step; // 与 Shapes 约定一致：angle 指向首个顶点、逆时针
                verts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            return verts;
        }

        /// <summary>
        /// 构建带圆角的 <see cref="PolygonPath"/>，同时支持凸角和凹角。
        /// <para>
        /// 对每个顶点，沿两条边各退 <c>t = r / tan(α/2)</c> 得到切点 P1、P2，
        /// 再沿角平分线 <c>(-dIn + dOut)</c> 放置圆心 <c>C = B + bisector × r / sin(α/2)</c>，
        /// 最后在 P1→P2 之间做角度扫描生成弧线点。
        /// </para>
        /// <para>
        /// 关键：平分线 <c>(-dIn + dOut)</c> 对凸角自然指向多边形内侧、对凹角自然指向外侧，
        /// 圆心始终落在正确位置，无需凸/凹判定或符号翻转。
        /// 弧线用 <c>atan2</c> + 角度扫描（非 Slerp），保证方向与多边形缠绕一致。
        /// </para>
        /// </summary>
        private static PolygonPath BuildRoundedPolygonPath(Vector2[] verts, float roundRadius)
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

                // 角平分线方向：对凸角指向内侧，对凹角指向外侧 → 始终正确
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
