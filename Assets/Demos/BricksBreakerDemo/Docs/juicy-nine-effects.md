# juicy-breakout 九大效果移植文档

> 本文档记录将 [grapefrukt/juicy-breakout](https://github.com/grapefrukt/juicy-breakout) 的 9 个核心效果移植到 Unity 6.3 URP 2D 的完整过程。
>
> 参考源码本地路径：`C:\Users\ClemT\AppData\Local\Temp\opencode\juicy-breakout\`

---

## 效果总览

球撞砖块时，以下效果**同时触发**（按 `Block.collide()` 执行顺序）：

| 序号 | juicy 开关 | 默认值 | 效果 |
|------|-----------|--------|------|
| 1 | EFFECT_PARTICLE_BALL_COLLISION (G0) | false | 球碰任何物体 → 橙色冲击粒子 |
| 2 | EFFECT_BLOCK_DESTRUCTION_DURATION (G01) | 2 秒 | 碎片/动画存活时长 |
| 3 | EFFECT_BLOCK_SCALE (G02) | false | 碎片从原始大小缩到 0 |
| 4 | EFFECT_BLOCK_GRAVITY (G03) | false | 碎片受重力下落 |
| 5 | EFFECT_BLOCK_PUSH (G04) | false | 碎片被球推开 |
| 6 | EFFECT_BLOCK_ROTATE (G05) | false | 碎片旋转（50/50 方向） |
| 7 | EFFECT_BLOCK_DARKEN (G06) | false | 碎片变暗 |
| 8 | EFFECT_PARTICLE_BLOCK_SHATTER (G08) | false | 砖色小方块从撞击点炸开 |
| 9 | EFFECT_PARTICLE_PADDLE_COLLISION (G09) | false | 球碰挡板 → 彩纸烟花 |

> juicy 默认全关，演示时由 Toggler 面板手动全开。我们的实现**全部默认开启**。

---

## ① PARTICLE_BALL_COLLISION

**球碰任何物体（墙/砖/挡板）时，在球的位置生成 5 个橙色小方块。**

### juicy 源码

`Main.as:305-316` — `handleBallCollide()`：
```actionscript
if (Settings.EFFECT_PARTICLE_BALL_COLLISION) {
    ParticleSpawn.burst(
        e.ball.x, e.ball.y,        // 位置：球当前位置
        5,                           // 数量
        90,                          // 散布角度
        -Math.atan2(e.ball.velocityX, e.ball.velocityY) * 180 / Math.PI,  // 基准角度
        e.ball.velocity * 5,        // 速度 = 球速 × 5
        .5,                          // 速度方差
        _particles_impact            // BallImpactParticle 粒子池
    );
}
```

`BallImpactParticle.as` — 14×14 像素实心方块，`COLOR_SPARK`(0xeba17f) 填色 + 随机明暗 0.8~1.0，GTween 位移 + scale 1→0.1，寿命 0.3~0.6 秒。

### Unity 实现

`Ball.cs:157-162` — `OnCollisionEnter2D()` 内：
```csharp
float baseAngleDeg = -Mathf.Atan2(_rb.linearVelocity.x, _rb.linearVelocity.y) * Mathf.Rad2Deg;
Brick.SpawnBurst(transform.position, 5, 90f, baseAngleDeg,
    _currentSpeed * 0.25f, 0.5f,
    new Color(0.922f, 0.631f, 0.498f), // COLOR_SPARK
    0.3f, 0.6f, 1.5f);
```

**触发点**：Ball 的 `OnCollisionEnter2D` 对**所有碰撞**触发（墙/砖/挡板），和 juicy 一致。

---

## ② BLOCK DESTRUCTION DURATION

**碎片的存活时长。juicy 用它作为 GTween 动画时长 + 延时移除计时器。**

### juicy 源码

`Settings.as:57`：
```actionscript
[min("0")] [max("3")]
[o("G01")] static public var EFFECT_BLOCK_DESTRUCTION_DURATION : Number = 2;
```

`Block.as:115` — 延时移除：
```actionscript
new GTween(this, Settings.EFFECT_BLOCK_DESTRUCTION_DURATION, null, { onComplete : handleRemoveTweenComplete });
```

`Block.as:104` — SCALE 动画时长：
```actionscript
new GTween(slice, Settings.EFFECT_BLOCK_DESTRUCTION_DURATION, { scaleY:0, scaleX:0 }, { ease:Quadratic.easeOut });
```

### Unity 实现

`Brick.cs:39`：
```csharp
[SerializeField] private float fragLifetime = 2f;
```

在 `AnimateFragmentAsync()` 的 while 循环条件中使用：`while (age < fragLifetime)`。到时间后销毁碎片 GameObject。

---

## ③ BLOCK SCALE

**碎片从原始大小逐渐缩小到 0，过程中配合 GRAVITY 下落，视觉上是"边落边缩到消失"。**

### juicy 源码

`Block.as:102-108`：
```actionscript
if (Settings.EFFECT_BLOCK_SCALE) {
    for each (var slice:Shape in _sliceEffect.slices) {
        new GTween(slice, Settings.EFFECT_BLOCK_DESTRUCTION_DURATION,
            { scaleY : 0, scaleX : 0 },
            { ease : Quadratic.easeOut }
        );
    }
}
```

GTween 从 scale 1 → 0，缓动 `Quadratic.easeOut`，持续 2 秒。

### Unity 实现

`Brick.cs:231-232` — `AnimateFragmentAsync()` 内：
```csharp
float st = Mathf.Clamp01(age / fragLifetime);
go.transform.localScale = initialScale * ((1f - st) * (1f - st));
```

**缓动公式推导**：GTween `Quadratic.easeOut` 对 progress `t` 的映射为 `f(t) = 1-(1-t)²`。GTween 从 1→0：`value(t) = 1 - f(t) = 1-(1-(1-t)²) = (1-t)²`。所以 Unity 用 `(1-age/lifetime)²`。

| 时间占比 | (1-t)² | 视觉 |
|---------|--------|------|
| 0% | 1.0 | 原始大小 |
| 25% | 0.5625 | 略缩 |
| 50% | 0.25 | 明显缩小 |
| 75% | 0.0625 | 几乎消失 |
| 100% | 0 | 销毁 |

---

## ④ BLOCK GRAVITY

**碎片受重力影响下落。配合 PUSH 的向上初速度，形成抛物线弧。**

### juicy 源码

`Block.as:134-136` — `Block.update()`：
```actionscript
if (Settings.EFFECT_BLOCK_GRAVITY && !_collidable) {
    velocityY += .4 * timeDelta;  // +Y 朝下（Flash 坐标）
}
```

每帧（`timeDelta ≈ 1.0`）Y 速度增加 0.4 像素/帧。

### Unity 实现

`Brick.cs:225`：
```csharp
vel.y -= fragGravity * dt;  // Unity +Y 朝上，重力取负
```

**参数换算**：

| | juicy | 换算 | Unity |
|--|-------|------|-------|
| 值 | 0.4 px/frame² | ×(屏高/600)×60² | 24 world/s² |
| 方向 | +Y（Flash 朝下） | 取负 | -Y（Unity 朝下） |

**注意**：juicy 的重力加在**砖块本身**（父物体）的 velocity 上，切片作为子物体跟着下落。我们的实现没有父子关系，重力直接加在每个碎片的独立 velocity 上，效果等价。

---

## ⑤ BLOCK PUSH

**碎片被球撞击的力度推开。这是碎片的主要初速度，决定飞行方向。**

### juicy 源码

`Block.as:71-78`：
```actionscript
if (Settings.EFFECT_BLOCK_PUSH) {
    var v:Point = new Point(this.x - ball.x, this.y - ball.y);
    v.normalize(ball.velocity * 1);  // 方向：(砖-球).normalized，大小：球速
    
    velocityX += v.x;
    velocityY += v.y;
}
```

`ball.velocity` 是 getter：`Math.sqrt(velocityX² + velocityY²)`，即球的速率（4~5 px/frame）。

`Point.normalize(value)` 将向量归一化后缩放到指定长度。所以 v 的方向是从球指向砖块，大小等于球速。

### Unity 实现

`Brick.cs:142-143`：
```csharp
Vector2 pushDir = ((Vector2)_visual.position - ballWorldPos).normalized;
Vector2 pushVel = pushDir * pushSpeed;  // pushSpeed = 5f world/s
```

**参数换算**：

| | juicy | 换算 | Unity |
|--|-------|------|-------|
| 值 | ball.velocity ≈ 5 px/frame | ×(屏高/600)×60 | 5 world/s |
| 方向 | (砖-球).normalized | 不变 | (砖-球).normalized |

**球从下撞击砖块** → pushDir 朝上 → 碎片向上飞 → GRAVITY 拉回 → 抛物线弧。与用户体验描述一致。

---

## ⑥ BLOCK ROTATE

**碎片旋转。50/50 概率正/反方向，全幅角速度。**

> **注意**：`EFFECT_BLOCK_ROTATE` 仅在 `!EFFECT_BLOCK_SHATTER` 时生效（`Block.as:89`）。如果同时开了 SHATTER，旋转由切片算法内部的分离力隐含提供。

### juicy 源码

`Block.as:89-92`：
```actionscript
if (Settings.EFFECT_BLOCK_ROTATE && !Settings.EFFECT_BLOCK_SHATTER) {
    _sliceEffect.slices[0].velocityR = Math.random() > .5 ? Settings.EFFECT_BLOCK_SHATTER_ROTATION : -Settings.EFFECT_BLOCK_SHATTER_ROTATION;
}
```

`EFFECT_BLOCK_SHATTER_ROTATION = 5`（`Settings.as:107`）。

**关键细节**：不是随机范围 ±R，而是 50/50 选 +R 或 -R（全幅）。`Math.random() > .5` = 50% 概率。

`velocityR` 在 `SliceEffect.update()` 中每帧衰减：`slice.velocityR -= slice.velocityR * 0.01 * timeDelta`。

### Unity 实现

`Brick.cs:152-153`：
```csharp
float a1 = UnityEngine.Random.value > 0.5f ? fragAngular : -fragAngular;
float a2 = UnityEngine.Random.value > 0.5f ? fragAngular : -fragAngular;
```

`Brick.cs:227` — 角速度阻尼：
```csharp
angVel *= Mathf.Max(0f, 1f - fragDamping * dt);
```

**参数换算**：

| | juicy | 换算 | Unity |
|--|-------|------|-------|
| 值 | 5 deg/frame | ×60 | 300 deg/s |
| 阻尼 | 0.01/frame | ×60 | 0.6 /s |
| 选择方式 | random > 0.5 ? +R : -R | 不变 | Random > 0.5 ? +R : -R |

每个碎片独立 50/50 选方向，所以两个碎片可能同向、也可能反向旋转。

---

## ⑦ BLOCK DARKEN

**碎片颜色变暗偏蓝，和原砖块形成对比，视觉上像"被打碎后暗淡"。**

### juicy 源码

`Block.as:69`：
```actionscript
if (Settings.EFFECT_BLOCK_DARKEN) transform.colorTransform = new ColorTransform(.7, .7, .8);
```

Flash `ColorTransform(r, g, b)` 将 RGB 分量分别乘以给定系数。`.7, .7, .8` = R×70%, G×70%, B×80%，变暗偏蓝。

### Unity 实现

`Brick.cs:26, 129`：
```csharp
[SerializeField] private Color fragmentTint = new Color(0.7f, 0.7f, 0.8f, 1f);

Color fragColor = brickColor * fragmentTint;  // 分量相乘，等价于 ColorTransform
```

Unity `Color * Color` 是分量相乘：`(r1*r2, g1*g2, b1*b2, a1*a2)`，和 Flash `ColorTransform` 的乘法语义一致。

在 `SpawnSlice()` 中设到材质实例上：
```csharp
var mat = new Material(fragmentMaterial);
mat.SetColor(ColorId, fragColor);
```

**Inspector 可调**：`fragmentTint` 在 Inspector 里可改为任意颜色。设 (1,1,1,1) 则不变色。

---

## ⑧ PARTICLE_BLOCK_SHATTER

**砖块被击碎时，从球的撞击位置炸开 5 个砖色小方块。**

### juicy 源码

`Main.as:362-374` — `handleBlockDestroyed()`：
```actionscript
if (Settings.EFFECT_PARTICLE_BLOCK_SHATTER) {
    ParticleSpawn.burst(
        e.ball.x, e.ball.y,       // 位置：球的位置（不是砖块位置！）
        5,                          // 数量
        45,                         // 散布角度
        -Math.atan2(e.ball.velocityX, e.ball.velocityY) * 180 / Math.PI,  // 基准角度
        50 + e.ball.velocity * 10, // 速度 = 50 + 球速×10
        .5,                         // 速度方差
        _particles_shatter          // BlockShatterParticle 粒子池
    );
}
```

`BlockShatterParticle.as` — 14×14 像素实心方块：
```actionscript
graphics.beginFill(Settings.COLOR_BLOCK);  // 砖块绿色 0x62bd84
graphics.drawRect(-7, -7, 14, 14);
var shade:Number = .8 + Math.random() * .2;  // 随机明暗 0.8~1.0
transform.colorTransform = new ColorTransform(shade, shade, shade);
```

GTween 位移 + scale 1→0.1，寿命 0.3~0.6 秒。

### Unity 实现

`Brick.cs:171-176` — `Shatter()` 内：
```csharp
if (shatterParticleCount > 0)
{
    float baseAngleDeg = -Mathf.Atan2(ballWorldVel.x, ballWorldVel.y) * Mathf.Rad2Deg;
    SpawnBurst(ballWorldPos, shatterParticleCount, 45f, baseAngleDeg,
        shatterParticleSpeed, 0.5f, brickColor, 0.3f, 0.6f, particleSize);
}
```

`SpawnBurst()` 公式（移植自 `ParticleSpawn.burst()`，`Brick.cs:248-262`）：
```csharp
float spreadRnd = Random.value * spread - spread * 0.5f;      // ±spread/2
float speedRnd  = Random.value * speedVariance - speedVariance * 0.5f;
float angleRad  = (-baseAngleDeg + spreadRnd) * Mathf.Deg2Rad;
float s         = speed * (1f + speedRnd);
Vector2 disp    = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * s;
```

粒子动画（`AnimateParticleAsync()`）：位移用 `easeOutQuad` tween（`1-(1-t)²`），缩放 1→0.1。

---

## ⑨ PARTICLE_PADDLE_COLLISION

**球碰挡板时，20 个彩纸从撞击点向上爆发，弧线下落 + 旋转 + 飘荡。**

### juicy 源码

`Main.as:335-346` — `handleBallCollide()` 内（检测到 Paddle）：
```actionscript
if (Settings.EFFECT_PARTICLE_PADDLE_COLLISION) {
    ParticleSpawn.burst(
        e.ball.x, e.ball.y,   // 位置：球的位置
        20,                     // 数量（比其他粒子多很多）
        90,                     // 散布角度
        -180,                   // 基准角度：Flash 中 -180° = 向上
        600,                    // 速度（非常快，600 px/帧）
        1,                      // 速度方差
        _particles_confetti     // ConfettiParticle 粒子池
    );
}
```

> ConfettiParticle.as 源码未包含在本地克隆中。根据 juicy 演示视频，彩纸是多色、旋转、飘荡的纸片效果。

### Unity 实现

与 juicy 不同，我们的彩纸用**物理动画**（非 tween），模拟纸袋烟花效果。

`Paddle.cs:200-214` — `OnCollisionEnter2D()` 内：
```csharp
Color[] confettiColors = {
    new Color(0.969f, 0.827f, 0.478f), // 金
    new Color(0.922f, 0.631f, 0.498f), // 橙
    new Color(0.384f, 0.741f, 0.518f), // 绿
    new Color(0.812f, 0.247f, 0.275f), // 红
    new Color(0.482f, 0.620f, 0.878f), // 蓝
    new Color(0.823f, 0.549f, 0.855f), // 紫
};
Brick.SpawnConfetti(ballPos, 20, confettiColors,
    6f, 12f,      // 向上初速度范围（world/s）
    3f,           // 水平散开
    15f,          // 重力（world/s²）
    1f, 2f,       // 寿命范围
    0.3f, 0.5f);  // 尺寸范围
```

`SpawnConfetti()` → `AnimateConfettiAsync()`（`Brick.cs:350-372`）：
```csharp
vel.y -= gravity * dt;                                    // 重力下落
vel.x += Mathf.Sin(age * flutterFreq) * flutterAmp * dt;  // 飘荡（空气阻力）
pos += vel * dt;                                          // 积分
go.transform.Rotate(0f, 0f, angVel * dt);                 // 持续旋转
// 最后 30% 寿命缩小消失
float scl = t > 0.7f ? Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f) : 1f;
go.transform.localScale = Vector3.one * (baseSize * scl);
```

**与 juicy 的区别**：juicy 用 GTween tween（直线位移 + 缩小），我们用物理动画（重力弧线 + 旋转 + 飘荡）。这是基于用户体验反馈的改进——纸袋烟花需要弧线和飘荡感，直线 tween 无法实现。

**坐标翻转**：juicy `baseAngle = -180`（Flash 中 -180° = 屏幕上方）。我们不用角度公式，直接设 `vel.y = +upward`（Unity +Y = 上方）。

---

## 附录 A：SpawnBurst 公式详解

`Brick.SpawnBurst()` 是粒子系统的核心方法，移植自 juicy `ParticleSpawn.burst()`。

### 输入参数

| 参数 | 含义 | juicy 对应 |
|------|------|-----------|
| `pos` | 生成位置 | spawnX, spawnY |
| `count` | 粒子数量 | count |
| `spread` | 散布角度（度） | spread |
| `baseAngleDeg` | 基准角度（度） | baseAngle |
| `speed` | 位移大小 | speed |
| `speedVariance` | 速度方差（0~1） | speedVariance |
| `color` | 基础颜色 | COLOR_SPARK / COLOR_BLOCK |
| `minLife`, `maxLife` | 寿命范围 | 粒子构造函数 `.3 + random*.3` |
| `size` | 方块大小 | drawRect(-7,-7,14,14) |

### 每个粒子的计算

```
spreadRnd = random × spread - spread/2        // [-spread/2, +spread/2)
speedRnd  = random × variance - variance/2    // [-variance/2, +variance/2)
angle     = (-baseAngleDeg + spreadRnd) × Deg2Rad
speed     = speed × (1 + speedRnd)
dispX     = sin(angle) × speed
dispY     = cos(angle) × speed
```

粒子从 `pos` 用 easeOutQuad tween 到 `pos + (dispX, dispY)`，同时 scale 从 1 缩到 0.1。

### 随机明暗

```csharp
float shade = 0.8f + Random.value * 0.2f;  // [0.8, 1.0)
color = color * shade;                      // 乘法变暗
```

等价于 juicy `ColorTransform(shade, shade, shade)`。

---

## 附录 B：参数标度方法

### 帧标度（td）——适用于弹簧类物理（球果冻）

juicy 的 `timeDelta ≈ 1.0`（帧），Unity 的 `Time.fixedDeltaTime ≈ 0.0167` 秒。用 `td = Time.fixedDeltaTime × 60` 可以让 juicy 的弹簧常数原样使用。

详见 `Docs/juicy-ball-effect-notes.md`。

### 世界单位/秒——适用于碎片/粒子的绝对位移

碎片物理是绝对位移型，需要从 juicy 的像素/帧换算到世界单位/秒：

| juicy 原始单位 | 换算系数 | 目标单位 |
|--------------|---------|---------|
| px/frame² (加速度) | × (屏高/600) × 60² | world/s² |
| px/frame (速度) | × (屏高/600) × 60 | world/s |
| /frame (阻尼率) | × 60 | /s |
| deg/frame (角速度) | × 60 | deg/s |

假设屏高 = 10 世界单位（orthographicSize = 5），换算举例：
- 0.4 px/frame² → 0.4 × (10/600) × 3600 = 24 world/s²
- 5 px/frame → 5 × (10/600) × 60 = 5 world/s
- 0.01/frame → 0.01 × 60 = 0.6 /s
- 5 deg/frame → 5 × 60 = 300 deg/s

---

## 附录 C：juicy 常量表

| 常量 | 值 | 出处 |
|------|-----|------|
| STAGE_W / STAGE_H | 800 / 600 | Settings.as:8-9 |
| BLOCK_W / BLOCK_H | 50 / 20 | Settings.as:11-12 |
| BALL_MIN_VELOCITY | 4 | Settings.as:118 |
| BALL_MAX_VELOCITY | 5 | Settings.as:117 |
| SHATTER_FORCE | 2 | Settings.as:109 |
| SHATTER_ROTATION | 5 | Settings.as:108 |
| DESTRUCTION_DURATION | 2 | Settings.as:57 |
| 重力 | 0.4 px/frame² | Block.as:135（硬编码） |
| 阻尼 | 0.01/frame | SliceEffect.as:76-78（硬编码） |
| DARKEN | ColorTransform(.7,.7,.8) | Block.as:69 |
| COLOR_SPARK | 0xeba17f | Settings.as:129 |
| COLOR_BLOCK | 0x62bd84 | Settings.as:125 |
| COLOR_TRAIL | 0xf7d37a | Settings.as:128 |
