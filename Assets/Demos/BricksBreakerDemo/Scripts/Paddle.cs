using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Paddle : MonoBehaviour
    {
        [SerializeField]
        private float moveSpeed = 12f;
        [SerializeField]
        private float maxBounceAngle = 75f;

        private Rigidbody2D _rb;
        private Collider2D _collider;
        private float _paddleHalfWidth;
        private float _screenHalfWidth;
        [SerializeField]
        private Ball ballPrefab; // 球预制体，由 Paddle 生成
        private Ball _ball; // 当前初始球实例
        private float _ballStartX;
        private float _moveInput; // 由 GameManager 推送

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            _paddleHalfWidth = _collider.bounds.extents.x;
            _screenHalfWidth = GameManager.Instance.Camera.orthographicSize * GameManager.Instance.Camera.aspect - GameManager.WallThickness;

            if (ballPrefab == null)
            {
                Debug.LogError("[Paddle] ballPrefab 未赋值，请在 Inspector 拖入 Ball 预制体");
                return;
            }

            SpawnBall();
        }

        // 生成初始球并吸附到挡板顶部
        private void SpawnBall()
        {
            _ball = Instantiate(ballPrefab, _rb.position, Quaternion.identity);
            SnapBallToPaddle();
            _ballStartX = _ball.transform.position.x;
        }

        // ===== 由 GameManager 调用 =====

        public void SetMoveInput(float input) => _moveInput = input;

        public void LaunchBall()
        {
            if (_ball == null || _ball.IsLaunched) return;
            _ball.Launch(ComputeDirection(_ball.transform.position));
        }

        // ===== 物理 =====

        private void FixedUpdate()
        {
            if (_moveInput == 0f) return;
            float targetX = _rb.position.x + _moveInput * moveSpeed * Time.fixedDeltaTime;
            _rb.MovePosition(new Vector2(ClampX(targetX), _rb.position.y));
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
            return new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad) * ySign);
        }

        private float ClampX(float x)
        {
            float min = -_screenHalfWidth + _paddleHalfWidth;
            float max = _screenHalfWidth - _paddleHalfWidth;
            if (_ball != null && !_ball.IsLaunched)
            {
                min = Mathf.Max(min, _ballStartX - _paddleHalfWidth);
                max = Mathf.Min(max, _ballStartX + _paddleHalfWidth);
            }

            return Mathf.Clamp(x, min, max);
        }

        private void SnapBallToPaddle()
        {
            var ballCollider = _ball.GetComponent<Collider2D>();
            if (ballCollider == null) return;
            Vector3 pos = _ball.transform.position;
            pos.x = _rb.position.x; // 水平居中
            pos.y = _collider.bounds.max.y + ballCollider.bounds.extents.y; // 球底贴挡板顶
            _ball.transform.position = pos;
        }
    }
}
