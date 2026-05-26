using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public static class UIAnimator
    {
        private static List<Animation> _activeAnimations = new List<Animation>();
        private static bool _isUpdating = false;
        private static double _lastTime = -1;

        private class Animation
        {
            public VisualElement Target;
            public string PropertyName;
            public float StartValue;
            public float EndValue;
            public float Duration;
            public float Elapsed;
            public Action<VisualElement, float> Setter;
            public Action OnComplete;
            public EaseType Ease;
            public bool IsComplete => Elapsed >= Duration;
        }

        public enum EaseType
        {
            Linear,
            EaseOut,
            EaseIn,
            EaseInOut,
            EaseOutCubic,
        }

        static UIAnimator()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (_activeAnimations.Count == 0)
            {
                _lastTime = -1;
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime;

            if (_lastTime < 0)
            {
                deltaTime = 0.016f;
            }
            else
            {
                deltaTime = (float)(currentTime - _lastTime);
            }
            _lastTime = currentTime;

            deltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.1f);

            _isUpdating = true;

            for (int i = _activeAnimations.Count - 1; i >= 0; i--)
            {
                var anim = _activeAnimations[i];
                anim.Elapsed += deltaTime;

                float t = Mathf.Clamp01(anim.Elapsed / anim.Duration);
                t = ApplyEase(t, anim.Ease);

                float value = Mathf.Lerp(anim.StartValue, anim.EndValue, t);
                anim.Setter?.Invoke(anim.Target, value);

                if (anim.IsComplete)
                {
                    anim.Setter?.Invoke(anim.Target, anim.EndValue);
                    anim.OnComplete?.Invoke();
                    _activeAnimations.RemoveAt(i);
                }
            }

            _isUpdating = false;
        }

        private static float ApplyEase(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EaseType.EaseOutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.EaseIn:
                    return t * t;
                case EaseType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }

        public static void AnimateWidth(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "width");

            float startValue = target.resolvedStyle.width;
            if (float.IsNaN(startValue))
                startValue = 0;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "width",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.width = val,
            };

            _activeAnimations.Add(anim);
        }

        public static void AnimateMarginLeft(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "marginLeft");

            float startValue = target.resolvedStyle.marginLeft;
            if (float.IsNaN(startValue))
                startValue = 0;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "marginLeft",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.marginLeft = val,
            };

            _activeAnimations.Add(anim);
        }

        public static void AnimateMarginRight(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "marginRight");

            float startValue = target.resolvedStyle.marginRight;
            if (float.IsNaN(startValue))
                startValue = 0;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "marginRight",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.marginRight = val,
            };

            _activeAnimations.Add(anim);
        }

        public static void AnimateOpacity(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "opacity");

            float startValue = target.resolvedStyle.opacity;
            if (float.IsNaN(startValue))
                startValue = 1;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "opacity",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.opacity = val,
            };

            _activeAnimations.Add(anim);
        }

        public static void AnimateScale(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "scale");

            float startValue = target.resolvedStyle.scale.value.x;
            if (float.IsNaN(startValue))
                startValue = 1;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "scale",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.scale = new Scale(new Vector2(val, val)),
            };

            _activeAnimations.Add(anim);
        }

        public static void AnimateTranslateX(
            VisualElement target,
            float endValue,
            float duration = 0.15f,
            EaseType ease = EaseType.EaseOut,
            Action onComplete = null
        )
        {
            CancelAnimation(target, "translateX");

            float startValue = target.resolvedStyle.translate.x;
            if (float.IsNaN(startValue))
                startValue = 0;

            var anim = new Animation
            {
                Target = target,
                PropertyName = "translateX",
                StartValue = startValue,
                EndValue = endValue,
                Duration = duration,
                Elapsed = 0,
                Ease = ease,
                OnComplete = onComplete,
                Setter = (el, val) => el.style.translate = new Translate(val, 0),
            };

            _activeAnimations.Add(anim);
        }

        public static void CancelAnimation(VisualElement target, string propertyName)
        {
            if (_isUpdating)
                return;

            for (int i = _activeAnimations.Count - 1; i >= 0; i--)
            {
                if (
                    _activeAnimations[i].Target == target
                    && _activeAnimations[i].PropertyName == propertyName
                )
                {
                    _activeAnimations.RemoveAt(i);
                }
            }
        }

        public static void CancelAllAnimations(VisualElement target)
        {
            if (_isUpdating)
                return;

            for (int i = _activeAnimations.Count - 1; i >= 0; i--)
            {
                if (_activeAnimations[i].Target == target)
                {
                    _activeAnimations.RemoveAt(i);
                }
            }
        }
    }
}
