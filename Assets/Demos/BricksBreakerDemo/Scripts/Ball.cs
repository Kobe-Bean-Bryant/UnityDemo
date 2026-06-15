using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Ball : MonoBehaviour
    {
        [SerializeField]
        private float speed = 8f;

        private Rigidbody2D _rb;
        private bool _launched;

        public bool IsLaunched => _launched;
        public float Speed => speed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // 由 Paddle 调用
        public void Launch(Vector2 normalizedDirection)
        {
            _launched = true;
            _rb.linearVelocity = normalizedDirection * speed;
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

            // 发射后：锁死速度大小（方向交给物理引擎 + Bouncy 材质）
            if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                _rb.linearVelocity = _rb.linearVelocity.normalized * speed;
                // 让球的朝向跟随运动方向（精灵默认朝上 +Y）
                _rb.rotation = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
            }
        }
    }
}
