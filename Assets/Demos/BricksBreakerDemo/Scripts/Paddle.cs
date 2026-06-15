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
        private float maxStretch = 2f; // 拉伸上限，避免拉得太细
        [SerializeField]
        private Transform _visual; // 视觉子物体，Inspector 拖入

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

            SpawnBall();
        }

        private void SpawnBall()
        {
            _ball = Instantiate(ballPrefab, _rb.position, Quaternion.identity);
            _ballRb = _ball.GetComponent<Rigidbody2D>();
            var ballCollider = _ball.GetComponent<Collider2D>();
            _ballRadius = ballCollider != null ? ballCollider.bounds.extents.y : 0f;
            _ballOffset = 0f;
        }

        // ===== 由 GameManager 调用 =====

        public void SetTargetX(float worldX) => _targetX = worldX;

        public void SetBallOffsetInput(float input) => _ballOffsetInput = input;

        public void LaunchBall()
        {
            if (_ball == null || _ball.IsLaunched) return;
            _ball.Launch(ComputeDirection(_ball.transform.position));
        }

        // ===== 物理 =====

        private void FixedUpdate()
        {
            float clampedX = ClampX(_targetX);

            // 拉伸挤压：只缩放视觉子物体 _visual，父物体 Collider 不受影响
            if (_visual != null)
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
        }

        // 双面反弹：水平偏移→角度；垂直方向由球在挡板上/下方决定
        private Vector2 ComputeDirection(Vector2 ballPos)
        {
            Vector2 paddlePos = _rb.position;
            float offset = Mathf.Clamp((ballPos.x - paddlePos.x) / _paddleHalfWidth, -1f, 1f);
            float angleRad = offset * maxBounceAngle * Mathf.Deg2Rad;
            float ySign = Mathf.Sign(ballPos.y - paddlePos.y);
            // 基准方向变了，sin 和 cos 的角色就互换。
            return new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad) * ySign);
        }

        private float ClampX(float x)
        {
            float min = -_screenHalfWidth + _paddleHalfWidth;
            float max = _screenHalfWidth - _paddleHalfWidth;
            return Mathf.Clamp(x, min, max);
        }
    }
}
