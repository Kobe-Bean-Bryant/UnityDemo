using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Ball : MonoBehaviour
    {
        [Header("速度")]
        [Min(0.1f)]
        [SerializeField]
        private float baseSpeed = 8f;
        [Min(0.1f)]
        [SerializeField]
        private float maxSpeed = 16f;
        [SerializeField]
        private float speedIncreasePerHit = 1.5f;

        [Header("减速回归")]
        [SerializeField]
        private float decayRate = 3f; // 越大衰减越快
        [SerializeField]
        private float gracePeriod = 2.5f; // 多久没碰撞开始减速

        [Header("速度拉伸（纯视觉）")]
        [SerializeField]
        private float maxStretch = 0.5f; // 最大速度时的拉伸量
        [SerializeField]
        private Transform _visual; // 视觉子物体，Inspector 拖入

        private Rigidbody2D _rb;
        private bool _launched;
        private float _currentSpeed;
        private float _timeSinceLastCollision;

        public bool IsLaunched => _launched;
        public float Speed => _currentSpeed; // 动态速度，Paddle 反射时读取

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _currentSpeed = baseSpeed;
        }

        // 由 Paddle 调用
        public void Launch(Vector2 normalizedDirection)
        {
            _launched = true;
            _currentSpeed = baseSpeed; // 每次发射从基础速度开始
            _timeSinceLastCollision = 0f;
            _rb.linearVelocity = normalizedDirection * _currentSpeed;
        }

        private void FixedUpdate()
        {
            if (!_launched)
            {
                // 兜底：发射前清零速度，防止求解器让球轻微上漂
                if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
                    _rb.linearVelocity = Vector2.zero;
                return;
            }

            // 速度回归：超过宽限期后指数衰减回 baseSpeed
            _timeSinceLastCollision += Time.fixedDeltaTime;
            if (_timeSinceLastCollision >= gracePeriod && _currentSpeed > baseSpeed)
            {
                float decayFactor = Mathf.Exp(-decayRate * Time.fixedDeltaTime);
                _currentSpeed = baseSpeed + (_currentSpeed - baseSpeed) * decayFactor;
                if (_currentSpeed - baseSpeed < 0.01f) _currentSpeed = baseSpeed;
            }

            // 锁死速度大小到当前速度
            if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
                _rb.linearVelocity = _rb.linearVelocity.normalized * _currentSpeed;

            // 视觉：朝向运动方向 + 按速度拉伸（纯视觉，不影响 Collider）
            if (_visual != null)
            {
                // Atan2 从 +X 轴量角度，但精灵默认的"朝向"是 +Y（上），两者差 90°，所以要补 -90f。
                float angle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
                _visual.localRotation = Quaternion.Euler(0f, 0f, angle);

                float speedRatio =
                    Mathf.Clamp01((_currentSpeed - baseSpeed) / Mathf.Max(maxSpeed - baseSpeed, 0.0001f));
                float stretch = 1f + speedRatio * maxStretch;
                _visual.localScale = new Vector3(1f / stretch, stretch, 1f); // 沿运动方向拉长，垂直方向压扁
            }
        }

        // 碰撞加速（不区分对象）+ 重置宽限期
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_launched) return;
            _timeSinceLastCollision = 0f;
            // 递减回报：越接近 maxSpeed 加得越少
            float speedProgress = _currentSpeed / maxSpeed;
            _currentSpeed = Mathf.Min(_currentSpeed + speedIncreasePerHit * (1f - speedProgress), maxSpeed);
        }
    }
}
