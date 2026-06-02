using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Samples
{
    /// <summary>
    /// 多 Drawer HUD 样例的<b>共享状态对象</b>：几个独立 Drawer 都引用它、读写它，从而互相影响——
    /// 不靠互相引用、更不靠 <c>IDraw.Owners</c>（句柄只存交互逻辑、不存外观）。
    /// <para>用 MonoBehaviour（非 ScriptableObject）：状态随 Play 自然复位、无写回 asset 副作用。</para>
    /// </summary>
    [AddComponentMenu("Shapes UI/Samples/HUD Shared State")]
    public class HudSharedState : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float health = 1f;
        [Range(0f, 1f)]
        public float mana = 1f;
        [Range(0f, 1f)]
        public float stamina = 0.7f;

        public int themeIndex;
        public Color[] themes =
        {
            new Color(0.20f, 0.70f, 1.00f),
            new Color(1.00f, 0.45f, 0.35f),
            new Color(0.50f, 0.85f, 0.40f)
        };

        /// <summary>当前主题色（themeIndex 越界时安全回退）。</summary>
        public Color Theme =>
            themes != null && themes.Length > 0
                ? themes[Mathf.Clamp(themeIndex, 0, themes.Length - 1)]
                : Color.white;
    }
}
