using UnityEngine;

namespace Shapes
{
    public class Compass : MonoBehaviour
    {
        /// <summary>罗盘在屏幕空间中的中心位置（UI坐标）</summary>
        public Vector2 position;

        /// <summary>罗盘弧形的宽度（单位：世界空间长度）</summary>
        public float width = 1f;

        /// <summary>弧形基线的粗细</summary>
        [Range(0, 0.01f)]
        public float lineThickness = 0.1f;

        /// <summary>弧形的弯曲半径，决定弧形的曲率</summary>
        [Range(0.1f, 2f)]
        public float bendRadius = 1f;

        /// <summary>罗盘的视野范围（弧度），决定显示多少方向刻度</summary>
        [Range(0.05f, ShapesMath.TAU * 0.49f)]
        public float fieldOfView = ShapesMath.TAU / 4;

        /// <summary>每个象限（90度）内的刻度数量，总刻度数 = (此值-1) × 4</summary>
        [Header("Ticks")]
        public int ticksPerQuarterTurn = 12;

        /// <summary>主方向刻度的长度比例（相对于 tickSize）</summary>
        [Range(0, 0.2f)]
        public float tickSize = 0.1f;

        /// <summary>边缘淡出区域的比例（0~1），控制刻度在视野边缘的透明度渐变</summary>
        [Range(0f, 1f)]
        public float tickEdgeFadeFraction = 0.1f;

        /// <summary>方向标签（N/E/S/W）的字体大小</summary>
        [Range(0.01f, 0.26f)]
        public float fontSizeTickLabel = 1f;

        /// <summary>方向标签与刻度线末端的偏移距离</summary>
        [Range(0, 0.1f)]
        public float tickLabelOffset = 0.01f;

        /// <summary>当前朝向角度标签（如 "180°"）的字体大小</summary>
        [Header("Degree Marker")]
        [Range(0.01f, 0.26f)]
        public float fontSizeLookLabel = 1f;

        /// <summary>角度标签相对于罗盘中心的偏移量</summary>
        public Vector2 lookAngLabelOffset;

        /// <summary>顶部三角形指示器的大小</summary>
        [Range(0, 0.05f)]
        public float triangleNootSize = 0.1f;

        /// <summary>四个主方向的标签数组，按顺序为南、西、北、东</summary>
        string[] directionLabels = { "S", "W", "N", "E" };

        /// <summary>
        /// 绘制罗盘 HUD 元素
        /// </summary>
        /// <param name="worldDir">玩家当前的朝向向量（3D世界空间）</param>
        public void DrawCompass(Vector3 worldDir)
        {
            // prepare all variables
            Vector2 compArcOrigin = position + Vector2.down * bendRadius; // 弧形基线的圆心位置（在 position 下方 bendRadius 处）
            float angUiMin = ShapesMath.TAU * 0.25f - (width / 2) / bendRadius; // UI 弧形的起始角度（左侧边界）
            float angUiMax = ShapesMath.TAU * 0.25f + (width / 2) / bendRadius; // UI 弧形的结束角度（右侧边界）
            Vector2 dirWorld = new Vector2(worldDir.x, worldDir.z).normalized; // 提取水平方向向量（忽略 Y 轴），并归一化
            float lookAng = ShapesMath.DirToAng(dirWorld); // 将方向向量转换为弧度角（0~2π）
            float angWorldMin = lookAng + fieldOfView / 2; // 视野的右边界角度（lookAng + FOV/2）
            float angWorldMax = lookAng - fieldOfView / 2; // 视野的左边界角度（lookAng - FOV/2）
            Vector2 labelPos = compArcOrigin + Vector2.up * (bendRadius) + lookAngLabelOffset * 0.1f; // 当前朝向角度标签的显示位置
            string lookLabel = Mathf.RoundToInt(-lookAng * Mathf.Rad2Deg + 180f) + "°"; // 格式化的角度字符串（如 "180°"）

            // prepare draw state
            Draw.LineEndCaps = LineEndCap.Square;
            Draw.Thickness = lineThickness;

            // draw the horizontal line/arc of the compass
            Draw.Arc(compArcOrigin, bendRadius, lineThickness, angUiMin, angUiMax, ArcEndCap.Round);

            // draw the look angle label
            Draw.FontSize = fontSizeLookLabel;
            Draw.Text(labelPos, lookLabel, TextAlign.Center);

            // triangle arrow
            Vector2 trianglePos = compArcOrigin + Vector2.up * (bendRadius + 0.01f);
            Draw.RegularPolygon(trianglePos, 3, triangleNootSize, -ShapesMath.TAU / 4);

            // draw ticks
            int tickCount = (ticksPerQuarterTurn - 1) * 4; // 总刻度数量（4个象限 × 每象限刻度数）
            for (int i = 0; i < tickCount; i++)
            {
                float t = i / ((float)tickCount); // 当前刻度的归一化位置（0~1，对应 0°~360°）
                float ang = ShapesMath.TAU * t; // 当前刻度的世界空间角度（弧度）
                bool cardinal = i % (tickCount / 4) == 0; // 是否为主方向刻度（N/E/S/W，每象限第一个）

                string label = null; // 主方向标签（N/E/S/W），非主方向为 null
                if (cardinal)
                {
                    int angInt = Mathf.RoundToInt((1f - t) * 4); // 根据归一化位置计算方向索引（0=S, 1=W, 2=N, 3=E）
                    label = directionLabels[angInt % 4];
                }

                float tCompass = ShapesMath.InverseLerpAngleRad(angWorldMax, angWorldMin, ang); // 计算当前角度在视野范围内的相对位置（0=左边界，0.5=中心，1=右边界）
                if (tCompass < 1f && tCompass > 0f)
                    DrawTick(ang, cardinal ? 0.8f : 0.5f, label);
            }

            // 绘制单个刻度线和可选的方向标签
            // worldAng: 刻度的世界空间角度（弧度）
            // size: 刻度长度的比例系数（主方向=0.8，普通=0.5）
            // label: 方向标签（N/E/S/W），null 表示不显示标签
            void DrawTick(float worldAng, float size, string label = null)
            {
                float tCompass = ShapesMath.InverseLerpAngleRad(angWorldMax, angWorldMin, worldAng); // 重新计算刻度在视野内的相对位置（用于后续映射）
                float uiAng = Mathf.Lerp(angUiMin, angUiMax, tCompass); // 将视野相对位置映射到 UI 弧形的角度
                Vector2 uiDir = ShapesMath.AngToDir(uiAng); // UI 弧形上当前角度的方向向量
                Vector2 a = compArcOrigin + uiDir * bendRadius; // 刻度线的外端点（位于弧形基线上）
                Vector2 b = compArcOrigin + uiDir * (bendRadius - size * tickSize); // 刻度线的内端点（向圆心方向偏移）
                float fade = Mathf.InverseLerp(0, tickEdgeFadeFraction, (1f - Mathf.Abs(tCompass * 2 - 1))); // 计算边缘淡出透明度：中心=1.0，边缘=0.0，平滑过渡
                Draw.Line(a, b, LineEndCap.None, new Color(1, 1, 1, fade)); // 绘制刻度线，应用淡出透明度
                if (label != null)
                {
                    Draw.FontSize = fontSizeTickLabel; // 设置方向标签的字体大小
                    Quaternion rotation = Quaternion.Euler(0, 0, (uiAng - ShapesMath.TAU / 4f) * Mathf.Rad2Deg); // 计算标签旋转角度，使其垂直于弧形切线方向
                    Draw.Text(b - uiDir * tickLabelOffset, rotation, label, TextAlign.Center, new Color(1, 1, 1, fade)); // 绘制旋转后的方向标签，应用相同的淡出透明度
                }
            }
        }
    }
}
