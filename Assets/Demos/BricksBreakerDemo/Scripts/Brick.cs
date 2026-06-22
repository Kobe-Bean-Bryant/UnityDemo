using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Collider2D))]
    public class Brick : MonoBehaviour
    {
        [Header("下落动画（纯视觉，作用于 _visual 子物体）")]
        [SerializeField]
        private Transform _visual;
        [SerializeField]
        private float fallHeight = 8f; // 从多高处落下
        [SerializeField]
        private float fallDuration = 0.7f; // juicy 默认值
        [SerializeField]
        private float maxStagger = 0.4f; // 最大随机延迟（错落感）
        [SerializeField]
        private float rotationRange = 45f; // 初始随机旋转 ±45°
        [SerializeField]
        private float startScale = 0.2f; // 初始缩放

        private CancellationTokenSource _cts;

        private void Start() => PlaySpawnAnimation(); // 初始入场

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.rigidbody != null && other.rigidbody.TryGetComponent<Ball>(out _))
                gameObject.SetActive(false);
        }

        // 由 GameManager（重置）和 Start（初始）调用
        public void PlaySpawnAnimation()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            PlayFallAsync(_cts.Token).Forget();
        }

        // 下落入场：Y 位移 + 旋转归正 + 缩放归位，三属性共用同一缓动（juicy-breakout 风格）
        private async UniTaskVoid PlayFallAsync(CancellationToken ct)
        {
            if (_visual == null) return;

            Vector3 restPos = Vector3.zero;
            Vector3 startPos = restPos + Vector3.up * fallHeight;
            float startRotZ = UnityEngine.Random.Range(-rotationRange, rotationRange);
            Vector3 startScaleVec = Vector3.one * startScale;

            _visual.localPosition = startPos;
            _visual.localRotation = Quaternion.Euler(0f, 0f, startRotZ);
            _visual.localScale = startScaleVec;

            await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Random.value * maxStagger), cancellationToken: ct);

            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float k = EaseOutBack(Mathf.Clamp01(elapsed / fallDuration));
                _visual.localPosition = Vector3.LerpUnclamped(startPos, restPos, k);
                _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startRotZ, 0f, k));
                _visual.localScale = Vector3.LerpUnclamped(startScaleVec, Vector3.one, k);
                await UniTask.Yield(ct);
            }

            _visual.localPosition = restPos;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // EaseOutBack：过冲后回弹（落地时的弹性手感）
        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }
    }
}
