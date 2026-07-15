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
        private float baseSpeed = 20f;
        [Min(0.1f)]
        [SerializeField]
        private float maxSpeed = 30f;
        [SerializeField]
        private float speedIncreasePerHit = 2f;

        [Header("减速回归")]
        [SerializeField]
        private float decayRate = 3f; // 越大衰减越快
        [SerializeField]
        private float gracePeriod = 1.5f; // 多久没碰撞开始减速

        [Header("速度拉伸（纯视觉）")]
        [SerializeField]
        private float maxStretch = 0.5f; // 最大速度时的拉伸量
        [SerializeField]
        private Transform _visual; // 视觉子物体，Inspector 拖入

        [Header("碰撞果冻效果（帧标度参数，参考 juicy-breakout）")]
        [SerializeField]
        private float popAmount = 1.5f; // 碰撞均匀放大量（juicy: 1.5）
        [SerializeField]
        private float popDecay = 0.35f; // 放大衰减（juicy: 0.35）
        [SerializeField]
        private float wobbleKick = 0.1f; // 晃动初始位移（juicy: 0.1）
        [SerializeField]
        private float wobbleKickVelocity = 2.5f; // 晃动初始速度（juicy: 2.5）
        [SerializeField]
        [Tooltip("弹簧刚度，越大晃得越快")]
        private float wobbleStiffness = 0.25f; // 弹簧刚度（juicy: 0.25）
        [SerializeField]
        [Tooltip("弹簧阻尼，越大停得越快")]
        private float wobbleDamping = 0.10f; // 阻尼（juicy: 0.10）
        [SerializeField]
        private float wobbleMinScale = 0.85f; // 形变下限（juicy: 0.85）
        [SerializeField]
        private float wobbleMaxScale = 1.35f; // 形变上限（juicy: 1.35）

        private Rigidbody2D _rb;
        private bool _launched;
        private float _currentSpeed;
        private float _timeSinceLastCollision;
        private float _pop;
        private float _wobble;
        private float _wobbleVel;
        private float _visualAngle; // 缓动旋转的当前角度

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
            _pop = 0f; // 重置果冻状态，确保每次发射干净启动
            _wobble = 0f;
            _wobbleVel = 0f;
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

            // 视觉：朝向运动方向 + 按速度拉伸 + 碰撞果冻（纯视觉，不影响 Collider）
            if (_visual != null)
            {
                // 帧标度时间（≈1.0 at 60fps），匹配 juicy-breakout 的 timeDelta
                float td = Time.fixedDeltaTime * 60f;

                // 放大脉冲：指数衰减（juicy: 0.35）
                if (JuicySettings.BallPop)
                {
                    if (_pop > 0.01f)
                    {
                        _pop -= td * _pop * popDecay;
                        if (_pop < 0.01f) _pop = 0f;
                    }
                }
                else _pop = 0f;

                // 果冻晃动：阻尼弹簧积分（juicy: 0.25 刚度, 0.10 阻尼）
                if (JuicySettings.BallWobble)
                {
                    if (Mathf.Abs(_wobble) > 0.0001f)
                    {
                        _wobbleVel += td * -wobbleStiffness * _wobble; // 回复力
                        _wobbleVel -= td * _wobbleVel * wobbleDamping; // 阻尼
                        _wobble += td * _wobbleVel; // 积分
                    }
                }
                else { _wobble = 0f; _wobbleVel = 0f; }

                // 旋转缓动（juicy 第 72-73 行：朝目标角度平滑过渡，不瞬切；LerpAngle 处理 -180/180 环绕）
                if (JuicySettings.BallRotation)
                {
                    float targetAngle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
                    _visualAngle = Mathf.LerpAngle(_visualAngle, targetAngle, td * 0.5f);
                    _visual.localRotation = Quaternion.Euler(0f, 0f, _visualAngle);
                }

                // 速度拉伸
                float stretch = 1f;
                if (JuicySettings.BallStretch)
                {
                    float speedRatio =
                        Mathf.Clamp01((_currentSpeed - baseSpeed) / Mathf.Max(maxSpeed - baseSpeed, 0.0001f));
                    stretch = 1f + speedRatio * maxStretch;
                }

                // 拉伸+晃动限幅（juicy 第 110-111 行），再叠加 pop（juicy: extra_scale 在 clamp 之后）
                // 符号：Y 是运动方向，碰撞时(wobble>0)Y 收缩=挤压、X 涨=拉伸，匹配 squash&stretch 与 juicy 相对行为
                float baseX = Mathf.Clamp(1f / stretch + _wobble, wobbleMinScale, wobbleMaxScale);
                float baseY = Mathf.Clamp(stretch - _wobble, wobbleMinScale, wobbleMaxScale);
                _visual.localScale = new Vector3(baseX + _pop, baseY + _pop, 1f);
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

            // 果冻效果：放大脉冲（累加）+ 晃动（位移+速度踢）
            if (JuicySettings.BallPop) _pop += popAmount;
            if (JuicySettings.BallWobble)
            {
                _wobble = wobbleKick;
                _wobbleVel = wobbleKickVelocity; // 速度踢（juicy: 2.5）
            }

            // juicy G0: PARTICLE_BALL_COLLISION — 5 个橙色冲击粒子从球位置炸开
            if (JuicySettings.BallCollisionParticles)
            {
                float baseAngleDeg = -Mathf.Atan2(_rb.linearVelocity.x, _rb.linearVelocity.y) * Mathf.Rad2Deg;
                Brick.SpawnBurst(transform.position, 5, 90f, baseAngleDeg,
                    _currentSpeed * 0.25f, 0.5f,
                    new Color(0.922f, 0.631f, 0.498f), // juicy COLOR_SPARK 0xeba17f
                    0.3f, 0.6f, 1.5f);
            }
        }
    }
}
