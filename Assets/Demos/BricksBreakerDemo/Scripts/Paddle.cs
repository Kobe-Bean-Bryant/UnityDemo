using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Paddle : MonoBehaviour
    {
        [Header("反弹")]
        [SerializeField]
        private float maxBounceAngle = 75f;

        [Header("发射前 - 方向键调节球位")]
        [SerializeField]
        private float offsetSpeed = 6f;

        [Header("拉伸挤压（纯视觉）")]
        [SerializeField]
        private float stretchDivisor = 3f; // 越小越夸张，Inspector 微调
        [SerializeField]
        private float maxStretch = 2f; // 拉伸上限
        [SerializeField]
        private Transform _visual; // 视觉子物体，Inspector 拖入

        [Header("下落入场动画")]
        [SerializeField]
        private float fallHeight = 8f;
        [SerializeField]
        private float fallDuration = 0.7f;
        [SerializeField]
        private float rotationRange = 45f;
        [SerializeField]
        private float startScale = 0.2f;

        [SerializeField]
        private Ball ballPrefab;

        private Rigidbody2D _rb;
        private Collider2D _collider;
        private float _paddleHalfWidth; // 基准（Collider）半宽
        private float _screenHalfWidth;
        private float _ballRadius;

        private Ball _ball;
        private Rigidbody2D _ballRb;

        private float _targetX; // 鼠标目标 X
        private float _ballOffsetInput; // 方向键输入 (-1/0/+1)
        private float _ballOffset; // 球相对挡板中心的 X 偏移

        private bool _isAnimating; // 下落期间为 true，禁用拉伸避免冲突
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            _paddleHalfWidth = _collider.bounds.extents.x;
            var cam = GameManager.Instance.Camera;
            _screenHalfWidth = cam.orthographicSize * cam.aspect - GameManager.WallThickness;
            _targetX = _rb.position.x; // 防首帧跳到 0

            if (ballPrefab == null)
            {
                Debug.LogError("[Paddle] ballPrefab 未赋值，请在 Inspector 拖入 Ball 预制体");
                return;
            }

            PlaySpawnAnimation(); // 下落动画 → 落地后才生成球（避免下落期间球穿帮）
        }

        // 生成球并吸附到挡板顶部（立即定位，避免 1 帧跳动）
        public void SpawnBall()
        {
            if (ballPrefab == null) return;
            _ball = Instantiate(ballPrefab, _rb.position, Quaternion.identity);
            _ballRb = _ball.GetComponent<Rigidbody2D>();
            var ballCollider = _ball.GetComponent<Collider2D>();
            _ballRadius = ballCollider != null ? ballCollider.bounds.extents.y : 0f;
            _ballOffset = 0f;
            _ballRb.position = new Vector2(_rb.position.x, _collider.bounds.max.y + _ballRadius);
        }

        // ===== 下落入场（由 Start 和 GameManager.ResetGame 调用）=====

        public void PlaySpawnAnimation()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            FallThenSpawnAsync(_cts.Token).Forget();
        }

        // 先 await 下落完成，再生成球（球在下落期间不存在 → 无穿帮）
        private async UniTaskVoid FallThenSpawnAsync(CancellationToken ct)
        {
            await PlayFallAsync(ct);
            SpawnBall();
        }

        // 下落入场：Y 位移 + 旋转归正 + 缩放归位，三属性共用同一缓动
        private async UniTask PlayFallAsync(CancellationToken ct)
        {
            if (_visual == null) return;
            if (!JuicySettings.PaddleTweenIn) return; // 关：跳过入场，挡板留在原位（球仍正常生成）
            _isAnimating = true; // 下落期间禁用拉伸
            try
            {
                Vector3 restPos = Vector3.zero;
                Vector3 startPos = restPos + Vector3.up * fallHeight;
                float startRotZ = Random.Range(-rotationRange, rotationRange);
                Vector3 startScaleVec = Vector3.one * startScale;

                _visual.localPosition = startPos;
                _visual.localRotation = Quaternion.Euler(0f, 0f, startRotZ);
                _visual.localScale = startScaleVec;

                float elapsed = 0f; // 挡板单个物体，不加随机 stagger
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
            finally
            {
                _isAnimating = false; // 无论完成还是取消，都恢复拉伸
            }
        }

        // ===== 由 GameManager 调用 =====

        public void SetTargetX(float worldX) => _targetX = worldX;

        public void SetBallOffsetInput(float input) => _ballOffsetInput = input;

        public void LaunchBall()
        {
            if (_ball == null || _ball.IsLaunched) return;
            _ball.Launch(ComputeDirection(_ball.transform.position));
        }

        // B 键：挡板上已有未发射球则不生成；否则生成新的待发射球
        public void RequestExtraBall()
        {
            if (_isAnimating) return; // 下落期间禁止，避免幽灵球
            if (_ball != null && !_ball.IsLaunched) return;
            SpawnBall();
        }

        // ===== 物理 =====

        private void FixedUpdate()
        {
            float clampedX = ClampX(_targetX);

            // 拉伸挤压：只缩放视觉子物体 _visual；下落期间禁用避免和入场动画冲突
            if (_visual != null && !_isAnimating && JuicySettings.PaddleSquash)
            {
                float delta = Mathf.Abs(clampedX - _rb.position.x);
                float scaleX = Mathf.Clamp(1f + delta / stretchDivisor, 1f, maxStretch);
                float scaleY = 1f / scaleX; // 体积守恒，永不为负
                _visual.localScale = new Vector3(scaleX, scaleY, 1f);
            }

            // 挡板跟随鼠标
            _rb.MovePosition(new Vector2(clampedX, _rb.position.y));

            // 发射前：球骑在挡板上，方向键调节位置
            if (_ball != null && !_ball.IsLaunched)
            {
                _ballOffset += _ballOffsetInput * offsetSpeed * Time.fixedDeltaTime;
                _ballOffset = Mathf.Clamp(_ballOffset, -_paddleHalfWidth, _paddleHalfWidth);

                float ballY = _collider.bounds.max.y + _ballRadius;
                _ballRb.MovePosition(new Vector2(clampedX + _ballOffset, ballY));
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.rigidbody == null) return;
            if (!collision.rigidbody.TryGetComponent<Ball>(out var ball)) return;
            if (!ball.IsLaunched) return;
            collision.rigidbody.linearVelocity = ComputeDirection(collision.transform.position) * ball.Speed;

            // juicy G09: PARTICLE_PADDLE_COLLISION — 彩纸烟花（随机亮色，向上爆发+旋转+飘荡下落）
            if (JuicySettings.PaddleConfetti)
            {
                Vector2 ballPos = collision.transform.position;
                Color[] confettiColors = {
                    new Color(0.969f, 0.827f, 0.478f), // 金
                    new Color(0.922f, 0.631f, 0.498f), // 橙
                    new Color(0.384f, 0.741f, 0.518f), // 绿
                    new Color(0.812f, 0.247f, 0.275f), // 红
                    new Color(0.482f, 0.620f, 0.878f), // 蓝
                    new Color(0.823f, 0.549f, 0.855f), // 紫
                };
                Brick.SpawnConfetti(ballPos, 20, confettiColors,
                    6f, 12f,      // 向上初速度范围
                    3f,           // 水平散开
                    15f,          // 重力
                    1f, 2f,       // 寿命
                    0.3f, 0.5f);  // 尺寸
            }
        }

        // 双面反弹：水平偏移→角度；垂直方向由球在挡板上/下方决定
        private Vector2 ComputeDirection(Vector2 ballPos)
        {
            Vector2 paddlePos = _rb.position;
            float offset = Mathf.Clamp((ballPos.x - paddlePos.x) / _paddleHalfWidth, -1f, 1f);
            float angleRad = offset * maxBounceAngle * Mathf.Deg2Rad;
            float ySign = Mathf.Sign(ballPos.y - paddlePos.y);
            return new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad) * ySign);
        }

        private float ClampX(float x)
        {
            float min = -_screenHalfWidth + _paddleHalfWidth;
            float max = _screenHalfWidth - _paddleHalfWidth;
            return Mathf.Clamp(x, min, max);
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
