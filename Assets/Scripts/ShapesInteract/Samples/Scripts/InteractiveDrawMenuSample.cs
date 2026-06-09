using Shapes;
using UnityDemo.Shared.ShapesInteract.Controls;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 立即模式「可交互绘制」示例：在普通 <see cref="ImmediateModeShapeDrawer"/> 的 <see cref="DrawShapes"/> 里
    /// 用 <see cref="IDraw"/> 一处绘制出几个可交互图形，拿到句柄后用可赋值委托加行为。
    /// <para>
    /// 演示：保留 <c>Draw.XXX</c> 风格、四态变色、点击改变「本 drawer 状态变量」和「另一个组件模式 Disc」、拖拽。
    /// 场景需有一个 <see cref="ShapesInteractionManager"/> 和相机。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Shapes UI/Samples/Interactive Draw Menu (code)")]
    public class InteractiveDrawMenuSample : ImmediateModeShapeDrawer
    {
        [Tooltip("可选：一个本身不可交互的组件模式 Disc，被「改色」按钮影响。")]
        [SerializeField]
        private Disc externalDisc;

        private static readonly Color[] Palette =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.07f, 0.33f),
            new Color(0f, 0.58f, 1f),
            new Color(0.20f, 0.80f, 0.40f),
        };

        private static readonly Color Normal = Color.white;
        private static readonly Color Hover = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color Press = new Color(0.6f, 0.6f, 0.6f);

        private int _bgIndex;
        private int _discIndex;
        private Vector3 _dotPos = new Vector3(0f, -1.4f, 0f);

        public override void OnDisable()
        {
            base.OnDisable();
            IDraw.Release(this); // 清理本 drawer 的所有句柄
        }

        public override void DrawShapes(Camera cam)
        {
            using (IDraw.Command(cam, this)) // 替代 Draw.Command(cam) + Draw.Matrix
            {
                // 纯装饰背景：用原生 Draw，先画 → 被后面的按钮盖住（绘制顺序即覆盖顺序）
                Draw.Rectangle(new Vector3(0f, 0f, 0.02f), new Vector2(7f, 5f), 0.25f, Palette[_bgIndex] * 0.25f);

                // 按钮 1：点击切换"本 drawer 状态变量"（背景色索引）
                var btnBg = IDraw.Rectangle("btn-bg", new Vector3(-1.7f, 1.4f, 0f), new Vector2(2.8f, 0.9f), 0.12f,
                    Normal, Hover, Press);
                btnBg.OnClick = _ => _bgIndex = (_bgIndex + 1) % Palette.Length;

                // 按钮 2：点击切换"另一个组件模式 Disc"的颜色（跨对象影响）
                var btnDisc = IDraw.Rectangle("btn-disc", new Vector3(1.7f, 1.4f, 0f), new Vector2(2.8f, 0.9f), 0.12f,
                    Normal, Hover, Press);
                btnDisc.OnClick = _ =>
                {
                    _discIndex = (_discIndex + 1) % Palette.Length;
                    if (externalDisc != null) externalDisc.Color = Palette[_discIndex];
                };

                // 可拖拽圆点：sortingOrder 调大以压在背景之上；OnDrag 用本地位移移动它
                var dot = IDraw.Disc("dot", _dotPos, 0.4f, Color.cyan, sortingOrder: 1);
                dot.OnDrag = e => _dotPos += (Vector3)e.LocalDelta;
            }
        }
    }
}
