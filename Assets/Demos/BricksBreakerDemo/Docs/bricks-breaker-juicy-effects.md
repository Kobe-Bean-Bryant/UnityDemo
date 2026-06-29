# BricksBreakerDemo 技术文档

> 本文档记录 BricksBreakerDemo 的全部 juicy 效果实现，包括架构设计、核心算法、参数标度和踩坑心得。

---

## 一、项目概述

**目标**：在 Unity 6.3（URP 2D）中移植 [grapefrukt/juicy-breakout](https://github.com/grapefrukt/juicy-breakout) 的核心 juicy 效果。

**技术栈**：Unity 6000.3 + URP 2D + UniTask + 新旧输入兼容。

**参考**：juicy-breakout 是 GDC 演讲 "Juice It or Lose It" 的配套 demo（ActionScript 3 / Flash），源码可在本地路径读取。

---

## 二、架构

```
GameManager（持久 Singleton，管理墙/输入/重置）
├── Paddle（Kinematic，鼠标控制 + 拉伸挤压 + 下落入场）
│   └── Visual（SpriteRenderer，纯视觉子物体）
├── Ball × N（Dynamic，velocity 锁死 + 碰撞果冻 + 速度拉伸）
│   └── Visual（SpriteRenderer）
├── Brick × N（Collider2D，下落入场 + 多边形切片碎裂 + 粒子）
│   └── Visual（SpriteRenderer）
└── BrickFragments（静态容器，存放碎片/粒子/彩纸）
```

### 视觉/物理解耦原则

所有游戏对象的 **Collider2D / Rigidbody2D 在父物体**（永不旋转/缩放），**SpriteRenderer 在子物体 `_visual`**（可自由旋转/缩放）。物理碰撞不受视觉变换影响。

---

## 三、核心效果实现

### 3.1 球的果冻效果（Ball.cs）

**4 个子系统叠加**：

| 系统 | 作用 | 触发 |
|------|------|------|
| 阻尼弹簧（wobble） | X/Y 交替挤压拉伸，振荡衰减 | 碰撞 |
| 均匀放大脉冲（pop） | 碰撞瞬间整体变大，快速缩回 | 碰撞 |
| 速度拉伸（stretch） | 按当前速度沿运动方向拉长 | 持续（每帧） |
| 旋转缓动 | 球平滑转向运动方向 | 持续（每帧） |

**阻尼弹簧积分（半隐式欧拉）**：
```csharp
float td = Time.fixedDeltaTime * 60f; // 帧标度，匹配 juicy timeDelta ≈ 1.0

_wobbleVel += td * -wobbleStiffness * _wobble;  // 回复力（胡克定律）
_wobbleVel -= td * _wobbleVel * wobbleDamping;   // 阻尼
_wobble += td * _wobbleVel;                      // 积分
```

**最终缩放 = 限幅后叠加**：
```csharp
float baseX = Mathf.Clamp(1f / stretch + _wobble, wobbleMinScale, wobbleMaxScale); // 0.85~1.35
float baseY = Mathf.Clamp(stretch - _wobble, wobbleMinScale, wobbleMaxScale);
_visual.localScale = new Vector3(baseX + _pop, baseY + _pop, 1f);
```

**关键教训**（详见 `juicy-ball-effect-notes.md`）：
1. `td = Time.fixedDeltaTime × 60` 把秒标度转成帧标度，juicy 常数（0.25/0.10/2.5）原样可用。
2. 弹簧内部值会飙到 ±4，**必须 clamp 到 [0.85, 1.35]** 才不会翻转/爆炸。
3. 旋转用 `Mathf.LerpAngle` 缓动（处理 ±180° 环绕），不瞬切。

### 3.2 挡板鼠标控制 + 拉伸挤压（Paddle.cs）

**鼠标跟随**：GameManager 读鼠标世界 X → `Paddle.SetTargetX()` → FixedUpdate 用 `MovePosition` 跟随。

**拉伸挤压（纯视觉）**：
```csharp
float delta = Mathf.Abs(clampedX - _rb.position.x);
float scaleX = Mathf.Clamp(1f + delta / stretchDivisor, 1f, maxStretch);
float scaleY = 1f / scaleX; // 体积守恒
_visual.localScale = new Vector3(scaleX, scaleY, 1f);
```

**下落入场**：UniTask 异步序列——先 await 下落动画完成，再 `SpawnBall()`。`_isAnimating` 标志在下落期间禁用拉伸（避免冲突）。

### 3.3 砖块下落入场（Brick.cs）

三属性（Y 位移 + 旋转 + 缩放）共用同一缓动 `EaseOutBack`（过冲回弹），加随机延迟（错落感）：

```csharp
float k = EaseOutBack(elapsed / fallDuration);
_visual.localPosition  = LerpUnclamped(startPos, restPos, k);
_visual.localRotation  = Euler(0, 0, LerpUnclamped(startRotZ, 0, k));
_visual.localScale     = LerpUnclamped(startScaleVec, Vector3.one, k);
```

### 3.4 砖块碎裂——多边形切片（Brick.cs #region 碎裂）

这是最复杂的效果，移植自 juicy-breakout 的 `SliceEffect.as` + `LineSliceObject`。

#### 3.4.1 切割算法

**输入**：砖块外接矩形 4 顶点 + 对应 UV + 切割线（球的轨迹）。

**`Slice()` 算法**（移植自 `LineSliceObject.slice()`）：
1. 遍历多边形每条边，用 `SegIntersect()` 检测与切割线的交点。
2. 恰好 2 个交点 → 沿交点分裂为 2 个新多边形。
3. 交点处 UV 沿边线性插值：`hitUv = Lerp(uvs[i], uvs[j], t)`。

**切割线构建**：
```csharp
Vector2 lp = _visual.InverseTransformPoint(ballWorldPos); // 球位置（砖块本地空间）
Vector2 ld = _visual.InverseTransformDirection(ballWorldVel).normalized;
Vector2 p1 = lp + ld * 50f;  // 球轨迹线两端
Vector2 p2 = lp - ld * 50f;
```

球撞正中 → 两块差不多大；球擦边 → 一大一小。**每次形状不同**。

#### 3.4.2 Mesh 创建

每块碎片创建运行时 Mesh：
```csharp
var mesh = new Mesh();
mesh.SetVertices(polygonVerts);        // 多边形顶点（本地空间）
mesh.SetUVs(0, polygonUvs);           // UV 映射到砖块纹理的正确区域
mesh.SetTriangles(TriangulateFan(n), 0); // 凸多边形扇形三角化：(0,i,i+1)
```

挂在 `MeshFilter` + `MeshRenderer` 上。材质用 `new Material(fragmentMaterial)` 实例，设 `_MainTex`（砖块纹理）+ `_Color`（变暗色）。

#### 3.4.3 碎片物理（无 Rigidbody，UniTask 手动驱动）

```csharp
// 每帧：
pos += vel * dt;                       // 位移
vel.y -= fragGravity * dt;             // 重力
vel *= 1f - fragDamping * dt;          // 阻尼
angVel *= 1f - fragDamping * dt;       // 角速度阻尼
transform.position = basePos + pos;    // 应用位置
transform.Rotate(0, 0, angVel * dt);   // 应用旋转
transform.localScale = initialScale * (1-age/lifetime)²; // SCALE 缩放
```

**初始速度 = PUSH + SHATTER**：
- PUSH：`(砖块位置 - 球位置).normalized × pushSpeed`（沿撞击方向抛出）
- SHATTER：`切线垂直方向 × pushSpeed × 0.4`（两块碎片向相反方向分离）
- ROTATE：`Random > 0.5 ? +fragAngular : -fragAngular`（50/50 全幅，juicy Block.as:90）

**SCALE 公式**：`(1-t)²` 匹配 juicy GTween `Quadratic.easeOut`（从 1 缩到 0）。

#### 3.4.4 URP 2D 着色器

URP 2D Renderer 的 Sprite 着色器需要内部属性 `unity_SpriteColor` / `unity_SpriteProps`，只有 SpriteRenderer 自动设置。MeshRenderer 不设置 → 用普通 URP Unlit 着色器碎片不可见。

**解决方案**：自定义着色器 `FragmentUnlit2D.shader`，包含 `LightMode = "Universal2D"` Pass（URP 2D Renderer 执行的 Pass）。Shader 属性：`_MainTex`（纹理）+ `_Color`（颜色），不依赖 sprite 内部属性。

### 3.5 粒子系统（Brick.cs #region 粒子）

**`SpawnBurst()`**（静态方法，Ball/Paddle/Brick 共用）：
- 移植自 juicy `ParticleSpawn.burst()`。
- 生成 N 个 SpriteRenderer 小方块（白色纹理 × 颜色），沿 `baseAngle ± spread/2` 方向飞出。
- 位移用 easeOutQuad tween，缩放 1→0.1。
- 随机明暗（0.8~1.0 × color）。

**三种粒子**：

| 调用方 | juicy 功能 | 参数 | 颜色 |
|--------|-----------|------|------|
| Ball.OnCollisionEnter2D | PARTICLE_BALL_COLLISION | 5 个，spread=90° | COLOR_SPARK 橙色 |
| Brick.Shatter | PARTICLE_BLOCK_SHATTER | 5 个，spread=45° | 砖块颜色 |
| Paddle.OnCollisionEnter2D | PARTICLE_PADDLE_COLLISION | → SpawnConfetti | 多色 |

### 3.6 彩纸烟花（Brick.cs #region 彩纸）

**`SpawnConfetti()`**：与粒子不同，用**物理动画**（非 tween）：
- 向上初速度（6-12 world/s）+ 水平散开 → 抛物线弧
- 重力 15 world/s² 下落
- 持续旋转（±540 deg/s 随机）
- 飘荡：sin 波形水平速度摆动（模拟空气阻力）
- 随机亮色（金/橙/绿/红/蓝/紫）
- 最后 30% 寿命缩小消失

---

## 四、参数标度

### 4.1 帧标度（td）——适用于弹簧类物理

juicy 用 `timeDelta ≈ 1.0`（帧），Unity 用 `Time.fixedDeltaTime ≈ 0.0167s`。差约 60 倍。

```csharp
float td = Time.fixedDeltaTime * 60f; // ≈1.0 at 60fps
```

用了 td 后，juicy 的常数（stiffness=0.25, damping=0.10, kick=2.5...）**原样可用**。

**适用场景**：球的果冻效果（比率型物理，不依赖绝对尺寸）。

### 4.2 世界单位/秒——适用于绝对位移

碎片/粒子的物理是绝对位移型，需要从 juicy 像素/帧换算到世界单位/秒：

| juicy 值 | 换算公式 | 世界单位/秒 |
|---------|---------|-----------|
| 0.4 px/frame²（重力） | × (屏高/600) × 60² | ≈24 world/s² |
| 0.01/frame（阻尼） | × 60 | 0.6 /s |
| 5 px/frame（球速） | × (屏高/600) × 60 | ≈5 world/s |
| 5 deg/frame（角速度） | × 60 | 300 deg/s |

---

## 五、踩坑记录

### 5.1 URP 2D + MeshRenderer 不兼容

**现象**：MeshRenderer 碎片不可见。

**根因**：URP 2D Renderer 的 sprite 着色器需要 `unity_SpriteColor` / `unity_SpriteProps`，只有 SpriteRenderer 自动设置。

**解决**：自定义着色器 `FragmentUnlit2D.shader`，用 `Universal2D` Pass。详见 `discussions.unity.com` 相关讨论。

### 5.2 Flash Y 轴翻转

juicy 用 Flash 坐标（+Y 朝下），Unity 用 +Y 朝上。
- 重力：juicy `velocityY += 0.4` → Unity `vel.y -= gravity`（取负）。
- 彩纸 baseAngle：juicy `-180`（向上）→ Unity `0`（向上）。

### 5.3 弹簧内部值会爆炸

不加 clamp 的弹簧内部 `_wobble` 值会飙到 ±4，直接塞进 `localScale` 导致球翻转/拉爆。**必须在应用前 clamp 到 [0.85, 1.35]**。

### 5.4 SetActive(false) 会连带隐藏子物体

碎片不能挂在砖块子物体下——砖块 `SetActive(false)` 会连带隐藏子物体，碎片瞬间消失。所有碎片/粒子挂在**独立的静态容器** `BrickFragments` 下。

### 5.5 Kinematic 刚体穿过 Static 墙

Kinematic 挡板不会被 Static 墙物理阻挡（`MovePosition` 穿墙）。必须代码 `Mathf.Clamp` 限制活动范围。

---

## 六、文件清单

| 文件 | 行数 | 说明 |
|------|------|------|
| `Scripts/Brick.cs` | ~500 | 下落入场 + 多边形切片碎裂 + 粒子 + 彩纸 + 容器 + 几何算法 |
| `Scripts/Ball.cs` | ~165 | 鼠标发射 + velocity 锁死 + 碰撞果冻 + 速度拉伸 + 冲击粒子 |
| `Scripts/Paddle.cs` | ~240 | 鼠标跟随 + 拉伸挤压 + 下落入场 + 球管理 + 彩纸 |
| `Scripts/GameManager.cs` | ~280 | 持久 Singleton + 输入 + 墙管理 + 重置 + sceneLoaded |
| `Scripts/Wall.cs` | ~20 | 墙标识枚举 |
| `Shaders/FragmentUnlit2D.shader` | ~126 | URP 2D 兼容的碎片渲染着色器 |
| `Docs/juicy-ball-effect-notes.md` | ~315 | 球果冻效果移植笔记 |

---

## 七、Inspector 配置清单

### 砖块 Prefab
- `_visual`：拖入 Visual 子物体（SpriteRenderer）
- `fragmentMaterial`：拖入 FragmentUnlit2D 材质

### 挡板 Prefab
- `_visual`：拖入 Visual 子物体
- `ballPrefab`：拖入 Ball 预制体

### Ball 预制体
- `_visual`：拖入 Visual 子物体
- Rigidbody2D：Dynamic / Continuous / Gravity Scale 0 / Freeze Rotation Z
- Collider2D：挂 Bouncy 材质（Bounciness=1, Friction=0, Bounciness Combine=Maximum）

### GameManager
- 放在入口场景（Level 1 或 Boot 场景），持久化（`IsPersistent => true`）
- 关卡场景不放 GameManager

---

## 八、参考资料

- [juicy-breakout 仓库](https://github.com/grapefrukt/juicy-breakout)
- [GDC 演讲 "Juice It or Lose It"](https://www.youtube.com/watch?v=Fy0aCDmgnxg)
- [在线试玩](http://grapefrukt.com/f/games/juicy-breakout/)
- [Unity URP 2D + MeshRenderer 讨论](https://discussions.unity.com/t/using-meshrenderer-with-the-2d-renderer-and-sprite-shaders/1591243)
- [UniTask 官方文档](https://github.com/Cysharp/UniTask/blob/master/README.md)
