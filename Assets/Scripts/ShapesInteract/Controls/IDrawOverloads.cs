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
    }
}
