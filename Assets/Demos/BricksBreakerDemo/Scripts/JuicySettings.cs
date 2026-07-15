using System.Linq;
using System.Reflection;

namespace BricksBreakerDemo
{
    /// <summary>
    /// Juice 效果开关注册表（移植 juicy-breakout 的 Settings.as + Toggler 反射思想）。
    /// 所有 bool 字段默认 false（默认全关，还原 juicy 教学对比意图：玩家自行开启效果感受提升）。
    /// 字段名 = UXML Toggle/Slider name，JuicyTogglePanel 用反射按名字段双向绑定。
    ///
    /// 砖块销毁效果遵循 juicy 的"独立作用"模型（Block.as:63-117）：任一销毁效果开 → 生成碎片对象
    /// （BlockShatter 决定 1 整块或 2 切片），Push/Gravity/Rotate/Scale/Darken 各自独立作用在碎片上，
    /// 互不依赖。详见 Brick.Shatter / AnimateFragmentAsync。
    ///
    /// 扩展：加新效果只需 3 步，本类与面板控制器零改绑定逻辑——
    ///   ① 这里加一个 public static bool 字段（数值参数用 float + Slider）
    ///   ② JuicyPanel.uxml 加一个同名 Toggle（或 Slider）
    ///   ③ 效果触发点加 if (!JuicySettings.X) return; 或包裹
    /// </summary>
    public static class JuicySettings
    {
        // ===== Ball（自定义球果冻，juicy BALL_* 的超集）=====
        public static bool BallPop;                  // 碰撞放大脉冲（指数衰减）
        public static bool BallWobble;               // 阻尼弹簧晃动
        public static bool BallRotation;             // 旋转缓动朝向运动方向
        public static bool BallStretch;              // 速度拉伸
        public static bool BallCollisionParticles;   // 碰撞冲击粒子（5 个橙色，juicy PARTICLE_BALL_COLLISION）

        // ===== Paddle =====
        public static bool PaddleSquash;             // 鼠标拉伸挤压（体积守恒）
        public static bool PaddleTweenIn;            // 下落入场动画（EaseOutBack）
        public static bool PaddleConfetti;           // 碰撞彩纸烟花（juicy PARTICLE_PADDLE_COLLISION）

        // ===== Brick 销毁（1:1 对齐 juicy BLOCK_*，各效果独立作用）=====
        public static bool BrickTweenIn;             // 下落入场动画（错落延迟，juicy TWEENIN_ENABLED）
        public static bool BlockShatter;             // 切片碎裂 1→2（juicy BLOCK_SHATTER）；决定碎片数量，不阻塞其他效果
        public static bool BlockBurstParticles;      // 碎裂冲击粒子（juicy PARTICLE_BLOCK_SHATTER）
        public static bool BlockPush;                // 球→砖方向推开（juicy BLOCK_PUSH）
        public static bool BlockGravity;             // 重力下落（juicy BLOCK_GRAVITY）
        public static bool BlockRotate;              // 旋转（juicy BLOCK_ROTATE）
        public static bool BlockScale;               // (1-t)² 缩放到 0（juicy BLOCK_SCALE）
        public static bool BlockDarken;              // 碎片变暗（juicy BLOCK_DARKEN）

        // ===== 数值参数（Slider）=====
        public static float DestructionDuration = 2f; // 碎片销毁动画时长（juicy BLOCK_DESTRUCTION_DURATION，秒）

        /// <summary>
        /// 全开 / 全关所有 bool 效果字段（反射遍历，加新字段零改）。不影响数值参数。
        /// </summary>
        public static void SetAll(bool value)
        {
            foreach (var f in typeof(JuicySettings)
                         .GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(bool)))
            {
                f.SetValue(null, value);
            }
        }
    }
}
