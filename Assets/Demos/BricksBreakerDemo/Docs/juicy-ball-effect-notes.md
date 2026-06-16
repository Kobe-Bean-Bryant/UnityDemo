# Breakout 球的 Juicy 果冻效果实现笔记

> 本笔记记录了在 Unity 中移植 [grapefrukt/juicy-breakout](https://github.com/grapefrukt/juicy-breakout) 球的果冻效果（jelly effect）的完整过程，包括踩过的坑、数学原理和最终方案。

---

## 一、目标与参考

**目标**：球在碰撞时产生"果冻感"——瞬间放大、晃动、按速度拉伸，营造冲击反馈。

**参考项目**：`grapefrukt/juicy-breakout`（ActionScript 3 / Flash，Martin Jonasson，GDC "Juice It or Lose It" 演讲的配套 demo）。

**关键源码**：
- [`Ball.as`](https://github.com/grapefrukt/juicy-breakout/blob/master/src/com/grapefrukt/games/juicy/gameobjects/Ball.as) — 球的全部逻辑（弹簧、放大、拉伸、旋转）
- [`Timestep.as`](https://github.com/grapefrukt/grapelib/blob/master/com/grapefrukt/Timestep.as)（grapelib 库）— 时间系统，理解 `timeDelta` 的关键

---

## 二、核心机制（4 个子系统）

球的视觉效果由四个独立系统叠加而成：

| 系统 | 作用 | 触发 |
|------|------|------|
| **阻尼弹簧（wobble）** | 果冻晃动——X/Y 交替挤压拉伸，振荡衰减 | 碰撞 |
| **均匀放大脉冲（pop）** | 碰撞瞬间整体变大，快速缩回 | 碰撞 |
| **速度拉伸（stretch）** | 按当前速度沿运动方向拉长 | 持续（每帧） |
| **旋转缓动** | 球平滑转向运动方向 | 持续（每帧） |

最终缩放 = 速度拉伸 + 果冻晃动（先限幅）+ 放大脉冲。最终旋转 = 朝运动方向的缓动。

---

## 三、实现过程的问题与解决（本笔记的精华）

移植经历了 4 个问题，前 3 个都是**逐行读源码**才找全的。按发现顺序记录。

### 问题 1：缺少速度踢（velocity kick）

**现象**：球只是"塌回"平衡位置，没有 juicy 那种"被撞击后甩出去"的动感。

**根因**：碰撞 kick 只给了位移，没给速度。

对比 juicy 源码（`Ball.as` 第 139-140 行）：
```actionscript
_ball_shakiness = 0.1;       // 位移踢
_ball_shakiness_vel = 2.5;   // 速度踢 ← 我们漏了这个
```
我们最初写的：`_wobbleVel = 0f`（只位移，速度归零）。

**解决**：补上速度踢。
```csharp
_wobble = wobbleKick;            // 位移踢
_wobbleVel = wobbleKickVelocity; // 速度踢（juicy: 2.5）
```

**原理——位移踢 vs 速度踢是两个独立的初始条件**：

用单摆类比最直观：
- **只位移踢** = 把单摆拉到一边、松手 → 它摆回（位移先产生回复力，再产生速度，再摆动）
- **只速度踢** = 在平衡位置横敲一下 → 它飞出去再摆回（直接给动能）
- **两个都给** = 拉到一边**再**敲一下 → 摆得更猛

位移踢产生的回复力是"延迟的"（下一帧才转化为速度）。而速度踢是**立刻赋予动能**——这就是 juicy 那种冲量感的来源。

> ⚠ 易错点：不要把速度踢当成"位移踢的后果"。位移→回复力→速度是弹簧自身的演化；速度踢是**额外**注入的初始动能，两者独立。

---

### 问题 2：时间标度不匹配（dt vs td）——最隐蔽的坑

**现象**：用了 juicy 的原版常数（stiffness 0.25 等），效果却极慢，调任何参数都不对。

**根因**：Unity 用的是 `Time.fixedDeltaTime`（**≈0.02 秒**），而 juicy 用的是 `Timestep.timeDelta`（**≈1.0 帧**）。两者差约 50 倍。

**关键证据**——读 grapelib 的 `Timestep.as` 源码：
```actionscript
public function Timestep(fps:int = 60, ...) {
    _target_frametime = 1000 / fps;  // 60fps → 16.67ms
}
public function tick():Number {
    _real_speed = (getTimer() - _last_frame_time) / _target_frametime; // 实际帧时/目标帧时
    return _delta * _game_speed;  // 60fps 正常速度下 ≈ 1.0
}
```
确认：`timeDelta` 是"一帧的时间量"（≈1.0），**不是秒**。

**为什么不能"把系数调大 50 倍"来补偿？**

直觉上，时间慢了 50 倍，系数放大 50 倍应该能抵消。**但这是错的**，因为刚度和阻尼进入物理系统的方式不同，需要的补偿倍数也不同：

- **刚度 k 决定振荡频率**。弹簧频率 ω = √k，离散化后每步的相位 ∝ √k · dt。要匹配 juicy 的周期，需 `√k_unity · dt = √k_juicy · td`，即 **k 需 ×(td/dt)² = ×2500**。
- **阻尼 c 决定衰减快慢**。每步衰减率 ∝ c · dt。要匹配衰减，需 `c · dt` 相等，即 **c 需 ×(td/dt) = ×50**。

**一个乘数搞不定两个参数**（2500 ≠ 50）——这就是"调系数"思路的死穴。我们之前试过 stiffness 150、200 等各种大数字，永远凑不准，因为漏了这个区别。

**解决——用 td 一次性吸收时间差异**：
```csharp
float td = Time.fixedDeltaTime * 60f; // 帧标度（≈1.0 at 60fps）
```
用了 td 后，juicy 的原版常数（0.25 / 0.10 / 2.5…）**直接能用，一个都不用改**。

**类比**：juicy 的菜谱用"杯"计量，你的厨房用"毫升"。与其把每个食材都乘 237（容易算错），不如直接换一把"杯"的量勺。`td` 就是那把量勺。

---

### 问题 3：缺少 clamp（最关键的问题）

**现象**：球在碰撞时**翻转、爆炸**（scaleX 变负，scaleY 拉到 5 倍），看起来像 bug 而不是果冻。

**根因**：弹簧内部计算的 `_wobble` 值会飙到 ±4（这是正常的，速度踢给的能量很大），但我们**直接**把这个值塞进 `localScale`，导致：
```
scaleX = 1/1 - 4 + pop = -3  → 负值，球水平翻转
scaleY = 1/1 + 4 + pop = 5+  → 极度拉伸
```

**关键证据**——重读 juicy `Ball.as` 第 110-111 行，发现之前漏看的两行：
```actionscript
_gfx.scaleX = MathUtil.clamp( _gfx.scaleX, 1.35, 0.85 );  // 限幅 [0.85, 1.35]
_gfx.scaleY = MathUtil.clamp( _gfx.scaleY, 1.35, 0.85 );
```
> 注意：grapelib 的 `MathUtil.clamp` 签名是 `(value, max, min)`，与 Unity 的 `Mathf.Clamp(value, min, max)` **参数顺序相反**。

juicy 的弹簧**内部也算出巨大值**，但应用前先 **clamp 到 [0.85, 1.35]**——所以视觉上只在 85%~135% 间微妙形变，这才是"果冻"而非"爆炸"。

**顺序也很关键**：juicy 是 `拉伸+晃动 → clamp → 再加 pop`。pop（均匀放大）在 clamp 之后叠加，所以 pop 可以让球放大到 ~2.85 倍，但晃动形变本身被限制在 tasteful 范围。

**解决**：
```csharp
float baseX = Mathf.Clamp(1f / stretch - _wobble, wobbleMinScale, wobbleMaxScale);
float baseY = Mathf.Clamp(stretch + _wobble, wobbleMinScale, wobbleMaxScale);
_visual.localScale = new Vector3(baseX + _pop, baseY + _pop, 1f);
```

---

### 问题 4：旋转瞬切（细节优化）

**现象**：碰撞时球的速度方向突变，球的旋转**瞬间跳到**新方向，显得"抖"。

**根因**：旋转直接赋值，没有缓动。
```csharp
_visual.localRotation = Quaternion.Euler(0f, 0f, angle); // 瞬切
```

**证据**——juicy `Ball.as` 第 72-73 行：
```actionscript
_ball_rotation += ( target_rotation - _ball_rotation ) * timeDelta * 0.5; // 缓动！
```
juicy 用存储的角度，每帧朝目标**缓动**（关闭 50% 间隙），2-3 帧平滑过渡。

**解决**：
```csharp
_visualAngle = Mathf.LerpAngle(_visualAngle, targetAngle, td * 0.5f);
_visual.localRotation = Quaternion.Euler(0f, 0f, _visualAngle);
```
用 `Mathf.LerpAngle` 比 juicy 原版的裸 lerp **更稳健**——它自动处理 ±180° 环绕，避免球反弹时"绕远路转一圈"。

---

## 四、技术要点详解

### 阻尼弹簧的数学

球晃动的核心是一个**阻尼弹簧**，物理方程：
```
加速度 = -k × 位移 - c × 速度
(x'' = -k·x - c·v)
```
- `k`（刚度）：回复力强度，位移越大拉回越强（胡克定律）
- `c`（阻尼）：摩擦，速度越快阻力越大，让振荡逐渐停止

用"半隐式欧拉积分"离散成三行代码：

```csharp
_wobbleVel += td * -wobbleStiffness * _wobble;   // ① 回复力：速度 += 时间 × (负刚度 × 位移)
_wobbleVel -= td * _wobbleVel * wobbleDamping;   // ② 阻尼：速度 -= 时间 × 阻尼 × 当前速度
_wobble     += td * _wobbleVel;                   // ③ 积分：位移 += 时间 × 速度
```

| 行 | 物理意义 | 直觉 |
|----|---------|------|
| ① | 回复力转化为速度变化 | 离平衡越远，拉回的"冲量"越大（方向相反） |
| ② | 阻尼消耗速度 | 摩擦力，让运动逐渐停下 |
| ③ | 速度积分成位移 | "积分"= 速度 × 时间 = 这段时间的位移变化，累加到当前位置 |

这三行循环执行，就模拟了弹簧的振荡：位移→回复力→速度→新位移→……，阻尼让它逐渐收敛。

### 为什么用 td 而非调系数（深入）

刚度和阻尼进入物理系统的方式不同，导致补偿倍数不同：

| 参数 | 决定什么 | 离散匹配条件 | 补偿倍数 |
|------|---------|-------------|---------|
| 刚度 k | 振荡频率（通过 √k） | √k · dt 匹配 | ×(td/dt)² = **×2500** |
| 阻尼 c | 衰减率（线性） | c · dt 匹配 | ×(td/dt) = **×50** |

实际数据（juicy stiffness=0.25, damping=0.10）：

| 配置 | 振荡周期 |
|------|---------|
| juicy（td=1, 60fps） | **0.21 秒** ✓ |
| 我们原来（dt=0.02, 不改系数） | **12.6 秒** ✗ 太慢 |

**差别根源**：k 通过 √k 影响频率（二阶动力学），c 线性影响衰减（一阶）。两者补偿倍数不同（2500 vs 50），无法用一个乘数统一。

`td = Time.fixedDeltaTime × 60` 把"秒"重新标定成"帧"，和 juicy 的 `timeDelta` 同尺度，于是 juicy 的常数原样可用，零缩放误差。

### 为什么缩放是一加一减（squash and stretch 原理）

```csharp
float baseX = Mathf.Clamp(1f / stretch + _wobble, ...);  // 垂直于运动方向（+wobble 涨）
float baseY = Mathf.Clamp(stretch - _wobble, ...);        // 运动方向（−wobble 缩）
```

**核心：异号 = 面积守恒的形变。** 一个轴涨、另一个轴缩，总面积 ≈ 不变（`(X+w)(Y−w) ≈ XY`）。这是 squash and stretch 动画原理——物体形变但不变大。如果两个都 `+`，就是均匀放大，那是 pop（放大脉冲）的职责，不是 wobble 的。

**为什么运动方向（Y）是减号：撞击时压缩。** 我们的旋转约定是局部 Y = 运动方向（`atan2 − 90`）。碰撞 kick 时 `_wobble > 0`，我们希望运动方向**收缩**（球撞墙被压扁），所以 `baseY = stretch − _wobble`。这匹配经典动画原理和 juicy 的相对行为。

> ⚠ 轴映射细节：juicy 原版旋转无 −90（局部 X = 运动方向），写的是 `scaleX -= s`。我们减了 90°（局部 Y = 运动方向），所以符号要相应翻转成 `baseY −= wobble`，才能让"运动方向挤压"这个效果一致。**照搬字面符号会因轴映射反了而得到相反的初始形变方向**（变成"撞击时拉伸"，违反直觉）。这是我们调试过程中发现并修正的一个细节。

---

## 五、最终参数（juicy 验证值，帧标度）

> 这些值是 juicy-breakout 验证过的，**帧标度**（配合 `td = fixedDeltaTime × 60` 使用）。在 Inspector 里直接用这些默认值即可。

| 参数 | 值 | 含义 |
|------|-----|------|
| `popAmount` | 1.5 | 碰撞均匀放大量（juicy） |
| `popDecay` | 0.35 | 放大衰减率（juicy） |
| `wobbleKick` | 0.1 | 晃动初始位移（juicy） |
| `wobbleKickVelocity` | 2.5 | 晃动初始速度/速度踢（juicy） |
| `wobbleStiffness` | 0.25 | 弹簧刚度（juicy） |
| `wobbleDamping` | 0.10 | 阻尼（juicy） |
| `wobbleMinScale` | 0.85 | 形变下限（juicy clamp） |
| `wobbleMaxScale` | 1.35 | 形变上限（juicy clamp） |

旋转缓动因子：`td × 0.5`（juicy 第 73 行）。

---

## 六、最终代码结构（Ball.cs 关键部分）

### FixedUpdate 视觉部分
```csharp
if (_visual != null)
{
    float td = Time.fixedDeltaTime * 60f; // 帧标度时间，匹配 juicy 的 timeDelta

    // ① 放大脉冲：指数衰减
    if (_pop > 0.01f)
    {
        _pop -= td * _pop * popDecay;
        if (_pop < 0.01f) _pop = 0f;
    }

    // ② 果冻晃动：阻尼弹簧积分
    if (Mathf.Abs(_wobble) > 0.0001f)
    {
        _wobbleVel += td * -wobbleStiffness * _wobble; // 回复力
        _wobbleVel -= td * _wobbleVel * wobbleDamping; // 阻尼
        _wobble += td * _wobbleVel;                    // 积分
    }

    // ③ 旋转缓动（LerpAngle 处理 ±180° 环绕）
    float targetAngle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
    _visualAngle = Mathf.LerpAngle(_visualAngle, targetAngle, td * 0.5f);
    _visual.localRotation = Quaternion.Euler(0f, 0f, _visualAngle);

    // ④ 速度拉伸
    float speedRatio = Mathf.Clamp01((_currentSpeed - baseSpeed) / Mathf.Max(maxSpeed - baseSpeed, 0.0001f));
    float stretch = 1f + speedRatio * maxStretch;

    // ⑤ 拉伸+晃动先 clamp，再叠加 pop（juicy 的顺序）
    float baseX = Mathf.Clamp(1f / stretch + _wobble, wobbleMinScale, wobbleMaxScale);
    float baseY = Mathf.Clamp(stretch - _wobble, wobbleMinScale, wobbleMaxScale);
    _visual.localScale = new Vector3(baseX + _pop, baseY + _pop, 1f);
}
```

### OnCollisionEnter2D（碰撞 kick）
```csharp
private void OnCollisionEnter2D(Collision2D collision)
{
    if (!_launched) return;
    // ... 速度提升逻辑 ...
    _pop += popAmount;             // 放大脉冲（累加）
    _wobble = wobbleKick;          // 位移踢
    _wobbleVel = wobbleKickVelocity; // 速度踢
}
```

**架构要点**：所有视觉变换（旋转 + 缩放）都在 `_visual` 子物体上，父物体（Rigidbody2D + Collider2D）永不旋转/缩放——彻底解耦视觉与物理。

---

## 七、参考资料

- **juicy-breakout 仓库**：https://github.com/grapefrukt/juicy-breakout
- **grapelib（Timestep 等工具库）**：https://github.com/grapefrukt/grapelib
- **GDC 演讲 "Juice It or Lose It"**：https://www.youtube.com/watch?v=Fy0aCDmgnxg
- **在线试玩**：http://grapefrukt.com/f/games/juicy-breakout/

---

## 八、复盘心得

1. **逐行读源码是金标准**。前几轮诊断（速度踢、td）都是基于"部分阅读 + 推断"，结果漏了 clamp（第 110-111 行）和旋转缓动（第 72-73 行）。直到完整读完 `Ball.as` 166 行才找全。**移植别人的效果时，逐行读完关键源码，比反复猜参数高效得多。**

2. **时间标度是物理效果移植最容易踩的坑**。`dt`（秒）vs `td`（帧）的 50 倍差异，叠加弹簧的"双重积分平方关系"，会让所有常数失效。遇到"参数怎么调都不对"时，第一时间检查时间单位。

3. **内部值大 ≠ 视觉值大**。弹簧内部算出 ±4 是正常的（速度踢给的能量），但视觉输出必须 clamp。juicy 的聪明之处在于：让弹簧自由演化（物理真实），只在最后一步限幅（视觉 tasteful）。

4. **忠实移植 > 重新发明**。juicy 的常数是 GDC 演讲者验证过的，直接借用（配合 td）比自己从头调更可靠。重新发明的诱惑很大，但参考实现的经验价值更高。
