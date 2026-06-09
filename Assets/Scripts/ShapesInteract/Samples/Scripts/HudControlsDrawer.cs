using Shapes;
using UnityEngine;
using UnityDemo.Shared.ShapesInteract.Controls;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// HUD 样例之一：<b>IDraw 模式</b>的 Drawer。用 <see cref="IDraw"/> 画几个按钮，点击改写
    /// <see cref="HudSharedState"/>（扣血/回血/耗蓝）→ Bars Drawer 下一帧反映。
    /// 并画一条主题色条 → 受 Theme Drawer 影响，体现跨 Drawer 双向。
    /// </summary>
    [ExecuteAlways]     // 否则立即模式 Drawer 只有运行时才渲染（编辑器里 OnEnable 不订阅渲染）
    [AddComponentMenu("Shapes UI/Samples/HUD Controls (IDraw)")]
    public class HudControlsDrawer : ImmediateModeShapeDrawer
    {
        [SerializeField]
        private HudSharedState shared;
        [Tooltip("第一个按钮中心（局部空间）")]
        [SerializeField]
        private Vector3 origin = new Vector3(-6f, -3.6f, 0f);
        [SerializeField]
        private Vector2 btnSize = new Vector2(2.4f, 0.9f);
        [SerializeField]
        private float gap = 0.35f;
        [Header("Text")]
        [Tooltip("字号 = TMP 点数（不是世界高度！世界空间下约为目标世界高度的 ~10 倍：按钮高 0.9 → 字号约 4~5。画布像素空间才用几百，如 240）")]
        [SerializeField]
        private float labelSize = 4f;
        [SerializeField]
        private Color labelColor = Color.white;

        public override void OnDisable()
        {
            base.OnDisable();
            IDraw.Release(this);
        }

        public override void DrawShapes(Camera cam)
        {
            if (shared == null) return;

            using (IDraw.Command(cam, this))
            {
                Button("dmg", 0, "Damage", new Color(0.80f, 0.30f, 0.30f),
                    () => shared.health = Mathf.Clamp01(shared.health - 0.1f));
                Button("heal", 1, "Heal", new Color(0.30f, 0.70f, 0.40f),
                    () => shared.health = Mathf.Clamp01(shared.health + 0.1f));
                Button("mana", 2, "Use Mana", new Color(0.35f, 0.50f, 0.90f),
                    () => shared.mana = Mathf.Clamp01(shared.mana - 0.2f));

                // 主题色条（纯装饰，原生 Draw）：随 Theme Drawer 改变 → 体现 Theme→Controls
                float rowW = 3 * btnSize.x + 2 * gap;
                Draw.Rectangle(
                    new Vector3(origin.x - btnSize.x * 0.5f + rowW * 0.5f, origin.y + btnSize.y * 0.5f + 0.2f,
                        origin.z),
                    new Vector2(rowW, 0.12f), 0.05f, shared.Theme);
            }
        }

        private void Button(string id, int i, string label, Color col, System.Action onClick)
        {
            Vector3 c = origin + Vector3.right * (i * (btnSize.x + gap));
            var h = IDraw.Rectangle(id, c, btnSize, 0.12f, col, col * 1.25f, col * 0.8f);
            h.OnClick = _ => onClick();

            // 标签：在按钮之后画=显示在按钮之上（纯装饰、不参与命中）
            Draw.FontSize = labelSize;
            Draw.Color = labelColor;
            Draw.Text(c, label, TextAlign.Center);
        }
    }
}
