using Shapes;
using UnityEngine;

namespace UnityDemo.Shared.ShapesInteract.Controls
{
    /// <summary>
    /// 所有 Shapes UI 控件的基类（类比 uGUI 的 Selectable）：负责命中区、四态颜色与过渡、可交互开关。
    /// <para>
    /// 命中区默认取 <see cref="targetGraphic"/> 的局部 AABB（<c>ShapeRenderer.GetBounds()</c>），
    /// 因此与 Inspector 里设置的几何自动同步。子类（Button/Toggle/Slider）实现具体的点击/拖拽行为。
    /// </para>
    /// </summary>
    public abstract class ShapesSelectable : MonoBehaviour,
        IShapesRaycastTarget,
        IShapesPointerEnterHandler, IShapesPointerExitHandler,
        IShapesPointerDownHandler, IShapesPointerUpHandler
    {
        [Header("Selectable")]
        [Tooltip("用于命中与变色的图形；留空时自动取本物体上的 ShapeRenderer。")]
        [SerializeField]
        protected ShapeRenderer targetGraphic;
        [SerializeField]
        private int sortingOrder;
        [SerializeField]
        private bool interactable = true;
        [Tooltip("命中区在每侧额外扩展的距离（x/y 世界单位）。细控件（如滑块轨道）可调大以便点中。")]
        [SerializeField]
        private Vector2 hitPadding = Vector2.zero;

        [Header("State Colors")]
        [SerializeField]
        private Color normalColor = Color.white;
        [SerializeField]
        private Color highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField]
        private Color pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField]
        private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [Tooltip("颜色过渡时长（秒），0 为瞬切。")]
        [SerializeField]
        private float fadeDuration = 0.1f;

        private bool _hovered;
        private bool _pressed;
        private Color _currentColor;
        private Color _targetColor;
        private bool _colorSettled = true;

        public Transform Transform => transform;

        /// <summary>
        /// 层级。组件模式下它**同时驱动渲染与点击**（类比 uGUI）：写入会同步到
        /// <see cref="targetGraphic"/> 的 <c>ShapeRenderer.SortingOrder</c>（Unity Renderer 排序，决定渲染），
        /// 也被 <see cref="ShapesInteractionManager"/> 用于命中优先。
        /// </summary>
        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                ApplySortingOrder();
            }
        }

        /// <summary>把 <see cref="sortingOrder"/> 推到图形的 Renderer 排序（决定渲染）。子类可重写以一并推子图形。</summary>
        protected virtual void ApplySortingOrder()
        {
            if (targetGraphic == null) targetGraphic = GetComponent<ShapeRenderer>();
            if (targetGraphic != null) targetGraphic.SortingOrder = sortingOrder;
        }

        /// <summary>是否可交互。设为 false 时不响应点击且显示 disabled 颜色。</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                RefreshTargetColor();
            }
        }

        /// <summary>供子类判断是否应响应交互。</summary>
        protected bool IsInteractable => interactable;

        protected virtual void Reset()
        {
            // 在编辑器里添加组件时自动抓取同物体的图形。
            targetGraphic = GetComponent<ShapeRenderer>();
        }

        protected virtual void OnEnable()
        {
            if (targetGraphic == null) targetGraphic = GetComponent<ShapeRenderer>();
            if (targetGraphic == null)
                Debug.LogWarning(
                    $"[ShapesUI] {name} 的 {GetType().Name} 没有 targetGraphic（也找不到同物体的 ShapeRenderer），将无法命中与变色。", this);
            _hovered = _pressed = false;
            ApplySortingOrder();
            ShapesInteractionManager.Register(this);
            ApplyColorImmediate(StateColor());
        }

        protected virtual void OnDisable()
        {
            ShapesInteractionManager.Unregister(this);
        }

        protected virtual void OnValidate()
        {
            // 编辑器里改 Sorting Order 即时反映到渲染（WYSIWYG）。
            ApplySortingOrder();
        }

        public virtual bool ContainsLocalPoint(Vector2 localPoint)
        {
            if (targetGraphic == null) return false;
            Bounds b = targetGraphic.GetBounds();
            if (hitPadding != Vector2.zero)
                b.Expand(new Vector3(hitPadding.x * 2f, hitPadding.y * 2f, 0f));
            return b.Contains(localPoint);
        }

        public virtual void OnPointerEnter(ShapesPointerEvent e)
        {
            _hovered = true;
            RefreshTargetColor();
        }

        public virtual void OnPointerExit(ShapesPointerEvent e)
        {
            _hovered = false;
            RefreshTargetColor();
        }

        public virtual void OnPointerDown(ShapesPointerEvent e)
        {
            if (interactable)
            {
                _pressed = true;
                RefreshTargetColor();
            }
        }

        public virtual void OnPointerUp(ShapesPointerEvent e)
        {
            _pressed = false;
            RefreshTargetColor();
        }

        private Color StateColor()
        {
            if (!interactable) return disabledColor;
            if (_pressed) return pressedColor;
            if (_hovered) return highlightedColor;
            return normalColor;
        }

        private void RefreshTargetColor()
        {
            _targetColor = StateColor();
            _colorSettled = false;
        }

        private void ApplyColorImmediate(Color c)
        {
            _currentColor = _targetColor = c;
            _colorSettled = true;
            if (targetGraphic != null) targetGraphic.Color = c;
        }

        protected virtual void Update()
        {
            if (_colorSettled || targetGraphic == null) return;

            if (fadeDuration <= 0f)
            {
                _currentColor = _targetColor;
            }
            else
            {
                float t = Mathf.Clamp01(Time.unscaledDeltaTime / fadeDuration);
                _currentColor = Color.Lerp(_currentColor, _targetColor, t);
            }

            if (ColorClose(_currentColor, _targetColor))
            {
                _currentColor = _targetColor;
                _colorSettled = true;
            }

            targetGraphic.Color = _currentColor;
        }

        private static bool ColorClose(Color a, Color b)
            => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < 0.004f;
    }
}
