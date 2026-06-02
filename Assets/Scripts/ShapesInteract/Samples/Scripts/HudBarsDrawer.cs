using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// HUD 样例之一：<b>最原始的 ImmediateModeShapeDrawer</b>（非交互，不实现任何 handler、不注册 target）。
    /// 只用原生 <c>Draw</c> 读 <see cref="HudSharedState"/> 画血/蓝/耐力三条进度条。
    /// 它由别的 Drawer（Controls/Theme）改写共享状态后，下一帧自动反映——演示"多种 Drawer 共渲染 + 经共享状态互相影响"。
    /// </summary>
    [ExecuteAlways]     // 没有它，立即模式 Drawer 的 OnEnable 不会在编辑器里订阅渲染 → 只有运行才看得到
    [AddComponentMenu("Shapes UI/Samples/HUD Bars (raw ImmediateModeShapeDrawer)")]
    public class HudBarsDrawer : ImmediateModeShapeDrawer
    {
        [SerializeField]
        private HudSharedState shared;
        [Tooltip("第一条的左缘中心（局部空间）")]
        [SerializeField]
        private Vector3 origin = new Vector3(-7.5f, 3.6f, 0f);
        [SerializeField]
        private Vector2 barSize = new Vector2(4f, 0.45f);
        [SerializeField]
        private float rowGap = 0.7f;
        [SerializeField]
        private Color manaColor = new Color(0.40f, 0.60f, 1f);
        [SerializeField]
        private Color staminaColor = new Color(0.95f, 0.85f, 0.30f);
        [Header("Text")]
        [Tooltip("字号 = TMP 点数（不是世界高度！世界空间下约为目标世界高度的 ~10 倍：条高 0.45 → 字号约 4~5。画布像素空间才用几百，如 240）")]
        [SerializeField]
        private float labelSize = 4f;
        [SerializeField]
        private Color labelColor = Color.white;

        public override void DrawShapes(Camera cam)
        {
            if (shared == null) return;

            using (Draw.Command(cam))
            {
                Draw.Matrix = transform.localToWorldMatrix;
                DrawBar(0, "HP", shared.health, shared.Theme); // 血条用主题色 → 受 Theme Drawer 影响
                DrawBar(1, "MP", shared.mana, manaColor); // 蓝条 → 受 Controls Drawer 影响
                DrawBar(2, "SP", shared.stamina, staminaColor);
            }
        }

        private void DrawBar(int row, string label, float t, Color fill)
        {
            float leftX = origin.x;
            float y = origin.y - row * rowGap;
            float z = origin.z;

            // 背景（居中绘制：中心 = 左缘 + 半宽）
            Draw.Rectangle(new Vector3(leftX + barSize.x * 0.5f, y, z), barSize, 0.08f, new Color(0f, 0f, 0f, 0.35f));

            // 填充（左对齐：宽度随 t，中心 = 左缘 + 半填充宽）
            float fw = barSize.x * Mathf.Clamp01(t);
            if (fw > 0.0001f)
                Draw.Rectangle(new Vector3(leftX + fw * 0.5f, y, z), new Vector2(fw, barSize.y), 0.08f, fill);

            // 文本：左侧标签（右对齐贴条左缘）+ 右侧百分比（左对齐贴条右缘）。Midline* = 垂直居中。
            Draw.FontSize = labelSize;
            Draw.Color = labelColor;
            Draw.Text(new Vector3(leftX - 0.25f, y, z), label, TextAlign.MidlineRight);
            Draw.Text(new Vector3(leftX + barSize.x + 0.25f, y, z), Mathf.RoundToInt(t * 100f) + "%", TextAlign.MidlineLeft);
        }
    }
}
