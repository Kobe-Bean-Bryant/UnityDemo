# IDraw 原理 —— 立即模式可交互绘制是怎么实现的

> **ShapesInteract 文档地图** ·
> [README](./README.md) 总览与选型 · [USAGE](./USAGE.md) 实操指南 · [RENDERING](./RENDERING.md) 渲染与层级 · **IDRAW_INTERNALS（本篇）** IDraw 原理

想直接上手用 `IDraw` 看 [USAGE §3](./USAGE.md) 即可；本篇拆解**内部机制**，便于扩展与排错。

相关源码：
- `Controls/IDraw.cs` —— 核心基础设施（上下文 / 句柄表 / 生命周期）
- `Controls/IDrawOverloads.cs` —— 全部图形绘制方法（与上面同属一个 `partial` 类）
- `Controls/InteractiveShapeHandle.cs` —— 一次可交互绘制对应的持久句柄
- `ShapesHitArea.cs`（核心程序集）—— 纯数学命中判定

---

## 1. 它解决什么问题

Shapes 的立即模式让你在 `ImmediateModeShapeDrawer.DrawShapes` 里用 `Draw.XXX` 画图，但画出来的东西**没有身份、不可交互**——它只是这一帧的一笔像素。

`IDraw` 想要的是：**保留 `Draw.XXX` 的书写风格**，但让「这一笔画出来的图形」能 hover / click / drag，且**不破坏立即模式「每帧重画」的心智模型**。

为此它**没有**引入新基类、也**没有**把绘制拆成 `OnDrawInteractive` 之类的回调（那会割裂「绘制」与「逻辑」）。做法是：你照旧在自己的 `DrawShapes` 里一处绘制，只是把 `Draw.Rectangle(...)` 换成 `IDraw.Rectangle("id", ...)`——后者既绘制、又按参数自动建好命中区、并返回一个**跨帧持久的句柄**让你挂行为。

```csharp
public override void DrawShapes(Camera cam)
{
    using (IDraw.Command(cam, this))                  // 替代 Draw.Command(cam) + Draw.Matrix
        IDraw.Rectangle("play", pos, size, 0.1f, Color.white)
             .OnClick = () => Debug.Log("play");       // 拿句柄挂行为
}
```

---

## 2. 为什么不能 partial 扩展 `Shapes.Draw`，而要自建 `IDraw`

最自然的想法是「给 `Draw` 加一组 `Draw.IRectangle` 重载」。**做不到**，因为：

- `Draw` 是 `Shapes` 程序集里的 `public static partial class`（见 `Draw.cs:20`、`DrawOverloads.cs:10` 等多个文件）。
- **C# 的 `partial` 不跨程序集**：partial 类的所有部分必须在同一个程序集里编译。我们的代码在 `UnityDemo.Shared.ShapesInteract.Controls` 程序集，无法成为 `Shapes.Draw` 的一部分。
- 静态类也不能用扩展方法。

所以只能**自建一个静态类 `IDraw`**，内部去调 `Draw.XXX`。为了让它的组织方式和官方一致、也便于维护，`IDraw` 自身拆成两个 `partial` 文件，**镜像 Shapes 自己的拆法**：

| 我们的 | 对应 Shapes 的 | 内容 |
|--------|---------------|------|
| `IDraw.cs` | `Draw.cs` / `DrawState.cs` | 上下文、句柄表、生命周期、`Scope` |
| `IDrawOverloads.cs` | `DrawOverloads.cs` | 所有 `IDraw.XXX(id, ...)` 绘制方法 |

---

## 3. `InteractiveShapeHandle` 拆解：句柄里存了什么

句柄是一次 `IDraw.XXX` 调用对应的**持久对象**。它是一个**普通 C# 对象（不是 MonoBehaviour）**，同时实现 `IShapesRaycastTarget` 和全部 7 个 handler 接口——所以它能像组件控件一样被 `ShapesInteractionManager` 命中和派发。它保存三类东西：

### ① 身份（让 Manager 能命中它）
- `Transform Transform { get; internal set; }` —— 指向**所属 drawer 的 transform**（由 `IDraw` 创建句柄时写入）。Manager 用它把世界射线转进局部空间。
- `int SortingOrder { get; set; }` —— 重叠时谁优先，由每次 `IDraw.XXX` 的参数刷新。

### ② 命中几何（让 `ContainsLocalPoint` 知道形状）
一个 `Kind` 枚举 + 一组按需复用的字段：

```
Kind: Box | Circle | Ring | Triangle | Capsule | Sector | Polygon | Polyline
字段: _center _size _radius _inner _thickness _from _to _a _b _c _points _closed _rotation
```

`IDraw` 每帧调对应的 `SetXXX(...)` 写入这些字段（如 `SetBox`、`SetSector`、`SetPolygon`、`SetCapsule`、`SetPolyline`）。其中 `_rotation`（弧度，绕 `_center`）由 `SetRotation` 写入,只有带旋转重载的 Rectangle/Pie/Arc 会设非零;`ContainsLocalPoint` 命中前先把待测点**逆旋转**回正坐标系，再按 `Kind` 把判定**转交给 `ShapesHitArea`**：

```csharp
public bool ContainsLocalPoint(Vector2 p)
{
    if (_rotation != 0f) p = ShapesHitArea.Rotate(p, _center, -_rotation);   // 形状绕 center 转了，待测点逆转回去
    switch (_kind)
    {
        case Kind.Box:      return ShapesHitArea.Box(p, _center, _size);
        case Kind.Circle:   return ShapesHitArea.Circle(p, _center, _radius);
        case Kind.Ring:     return ShapesHitArea.Ring(p, _center, _inner, _radius);
        case Kind.Triangle: return ShapesHitArea.Triangle(p, _a, _b, _c);
        case Kind.Capsule:  return ShapesHitArea.Capsule(p, _a, _b, _thickness);
        case Kind.Sector:   return ShapesHitArea.Sector(p, _center, _inner, _radius, _from, _to);
        case Kind.Polygon:  return ShapesHitArea.Polygon(p, _points);
        case Kind.Polyline: return ShapesHitArea.PolylineCapsule(p, _points, _thickness, _closed);
        default:            return false;
    }
}
```

> **绘制几何与命中几何同源**：`IDraw.XXX` 里既调 `Draw.XXX(...)` 画、又用同一份参数 `h.SetXXX(...)` 写命中——所以「看到的」就是「能点的」。

### ③ 实时状态 + 行为委托
- `bool Hovered`、`bool Pressed` —— 由 Enter/Exit、Down/Up 维护，**跨帧保留**（不是每帧重置）。四态颜色重载就是读它俩选色。
- 可赋值委托：`Action OnClick/OnEnter/OnExit/OnDown/OnUp`、`Action<ShapesPointerEvent> OnDrag/OnMove`。7 个 handler 接口的实现里只做两件事：更新状态、转调对应委托。例如：
  ```csharp
  void IShapesPointerDownHandler.OnPointerDown(ShapesPointerEvent e) { Pressed = true; OnDown?.Invoke(); }
  ```

---

## 4. `IDraw.Command` 如何开启一次「可交互绘制上下文」

`using (IDraw.Command(cam, this))` 是整套机制的入口（`IDraw.cs` 的 `Command`）。它做了四件事：

```csharp
public static Scope Command(Camera cam, MonoBehaviour owner)
{
    // 1) 取/建该 owner 的状态袋（句柄表 + seen 集 + 上一帧号）
    if (!Owners.TryGetValue(owner, out var st)) { st = new OwnerState(); Owners[owner] = st; }
    st.Transform = owner.transform;
    _current = st;                                  // 2) 设当前上下文，让块内 IDraw.XXX 找得到

    // 3) 每帧每 owner 只做一次：裁剪上一帧没再画的句柄，再清空 seen
    if (st.LastFrame != Time.frameCount) { Prune(st); st.Seen.Clear(); st.LastFrame = Time.frameCount; }

    IDisposable cmd = Draw.Command(cam);            // 4) 开底层渲染批次 + 设矩阵
    Draw.Matrix = owner.transform.localToWorldMatrix;
    return new Scope(cmd);                          //    Scope.Dispose 时清 _current 并关闭批次
}
```

- **`_current`（静态当前上下文）**：块内每个 `IDraw.XXX` 调 `Ensure(id, ...)` 时从 `_current` 取句柄表。没在 `using` 块里调用会抛 `InvalidOperationException`，明确报错。
- **它就是 `Draw.Command + Draw.Matrix` 的封装**：所以用 `IDraw` 时**不必再手写** `using(Draw.Command(cam)){ Draw.Matrix = ...; }`。`Scope.Dispose` 时 `_current = null` 并关闭底层批次。

> 关于「为什么基类 `DrawShapes` 不自带 `Draw.Command`、以及 `Draw.Scope`/`MatrixScope` 等」见 §7。

---

## 5. 句柄生命周期：id 持久 + 按帧裁剪

句柄**按字符串 id 跨帧复用**，这正是「立即模式每帧重画，但监听器/hover 状态不丢」的关键。

- **取/建**（`Ensure`）：按 id 在当前 owner 的句柄表里查；没有就 `new` 一个、写 `Transform`、`Register` 到 Manager。每次都把 id 加进 `Seen`、刷新 `SortingOrder`。
- **裁剪**（`Prune`，每帧每 owner 一次）：上一帧画过、这一帧没再画的 id（不在 `Seen` 里）会被 `Unregister` 并从表里删除。所以**改 id、或不再画某图形，旧句柄会自动注销、不再响应**。
- **释放**（`Release`）：owner 在 `OnDisable` 调一次 `IDraw.Release(this)`，注销并清空该 owner 的全部句柄，防止禁用后句柄残留在 Manager 里。

```
DrawShapes 第 N 帧:  using(IDraw.Command) { IDraw.Rectangle("a") IDraw.Disc("b") }
                       → Ensure("a") Ensure("b") , Seen={a,b}
DrawShapes 第 N+1 帧: 只画了 "a"
                       → Command 进入时 Prune: "b" 不在上一帧 Seen? (其实在) → 实际裁剪发生在
                         «下一次 Command 进入、对比刚结束这帧的 Seen»，没画到的 id 被注销
```

> 实现细节：`Prune` 在每帧**进入** `Command` 时、基于**上一帧累积的 `Seen`** 执行，然后清空 `Seen` 重新累积。等价于「上一帧没画到的，这一帧开头清掉」。

---

## 6. 为什么用「可赋值委托」而不是 `AddListener`

立即模式**每帧都会重跑 `DrawShapes`**，于是每帧都会执行一次 `IDraw.Rectangle("play", ...).OnClick = ...`。

- **可赋值委托（`OnClick = ...`）是幂等的**：每帧重新赋同一个值，结果不变。✓
- 若改用 `AddListener` 风格，会**每帧累积一个监听器**，一秒钟挂上几十个，点一次触发几十次。✗

所以句柄的事件设计成 `public Action OnClick;` 这种**直接赋值**字段。需要让外部监听时的两种干净写法（暴露 Drawer 自己的 `event`、或把句柄存出来）见 [USAGE §3「让外部监听 IDraw 的事件」](./USAGE.md)。

---

## 7. `IDraw.Command` vs Shapes 的状态 Scope（含组合的坑）

这是最容易混淆的一组概念。`Draw.Command` 与各种 `Draw.XXXScope` **是正交的两类东西**：

### `Draw.Command(cam)` = 开「渲染批次」
它返回一个池化的 `DrawCommand`（`IDisposable`），`Dispose` 那一刻把这批 `Draw.XXX` 提交渲染。**`using` 的括号边界 = 批次边界。**

这就是**为什么基类 `ImmediateModeShapeDrawer.DrawShapes` 不替你包** `using(Draw.Command(cam))`：

- 一次 `DrawShapes` 里你可能想开多个批次（不同 `RenderPassEvent` / `CameraEvent` / ZTest 分组），或条件性地一个都不开；
- 批次从哪开到哪结束只有你知道；
- 基类故意保持最小——它的源码注释直接写着 `using(Draw.Command(cam)){ // Draw here }`（`ImmediateModeShapeDrawer.cs:15`）。

Shapes 自带的「封装版」是 **`ImmediateModeCanvas`**：它重写 `DrawShapes`、替你开 `Draw.Command` + 设 canvas 矩阵，只让你重写 `DrawCanvasShapes(ctx)`（但需要 `Canvas` 组件，面向 UGUI Canvas）。
**我们的 `IDraw.Command(cam, this)` 就是同类思路的封装**——所以用 `IDraw` 时不必再手写 `Draw.Command`。

### `Draw.Scope / MatrixScope / StyleScope / ColorScope / ...` = 状态存档栈
它们与批次无关，而是**进入时快照一部分全局 `Draw` 状态、离开时自动还原**（基于栈，可嵌套）：

| Scope | 存/还原 | 源码 |
|---|---|---|
| `Draw.MatrixScope` | 仅 `Draw.Matrix` | `DrawStateMatrix.cs` |
| `Draw.StyleScope` | 整个 style（颜色/线宽/blend…） | `DrawStateStyle.cs` |
| `Draw.ColorScope` | 仅颜色 | `DrawStateStyle.cs` |
| `Draw.Scope` | style + matrix 全存 | `DrawState.cs` |
| `Draw.DashedScope()` / `GradientFillScope()` | 临时开虚线 / 渐变 | `DrawStateStyle.cs` |
| `Draw.Push()` / `Draw.Pop()` | 手动版（不用 using） | `DrawState.cs` |

用法是在批次块内**局部**改状态而不污染后续：
```csharp
using (Draw.Command(cam)) {
    Draw.Matrix = transform.localToWorldMatrix;
    using (Draw.ColorScope) { Draw.Color = Color.red; Draw.Line(a, b, 0.1f); }   // 离开后颜色还原
    using (Draw.MatrixScope) { Draw.Matrix *= Matrix4x4.Rotate(q); Draw.Rectangle(...); }
}
```

### 与 `IDraw.Command` 组合 —— 一个必须知道的坑
块内可以用这些 Scope 包裹 `IDraw.XXX` 或原生 `Draw.XXX`。但：

> **`IDraw` 的命中区按你传的参数、在 `owner.localToWorldMatrix`（即 `IDraw.Command` 设的那个矩阵）下计算。**
> 如果把一个 `IDraw.XXX` 嵌进额外的 `MatrixScope` 再叠加变换，**画出来的图会动，但命中区不会跟着动 → 视觉与命中错位。**

- `ColorScope / StyleScope`（不改几何）与 `IDraw` 组合**安全**。
- 需要额外矩阵变换时：让可交互的 `IDraw.XXX` 留在 owner 矩阵下，把变换**烘进你传的坐标参数**；`MatrixScope` 只用来包**纯装饰**的原生 `Draw.XXX`。
- **想旋转一个可交互形状,别自己包 `MatrixScope`**——用 `Rectangle/Pie/Arc` 的**旋转重载**（见 §8）。它内部同样用 `MatrixScope` 绘制,但**同时**把同一旋转写进句柄 `_rotation`,命中端逆旋转,所以画与点始终同步——这正是「我们同时掌控绘制与命中」的受控用法。

### 渲染序 vs 命中序
`IDraw` 句柄的 `SortingOrder` **只管命中、不管显示层级**（立即绘制无 Renderer，层级靠绘制序）。完整规则（含组件模式一个旋钮两用、跨 Drawer 三招、字号）见 [RENDERING](./RENDERING.md)。

---

## 8. 覆盖范围（只覆盖 2D 图元）

本框架在局部 `z=0` 平面做 2D 点-在-形状判定，所以 `IDraw` 只覆盖**能做 2D 命中**的图元。每个图元的命中都落到 `ShapesHitArea` 的一个纯数学函数：

| `IDraw.XXX` | 绘制 | 命中（`ShapesHitArea`） |
|---|---|---|
| `Rectangle` | `Draw.Rectangle` | `Box` |
| `Disc` | `Draw.Disc` | `Circle` |
| `Ring` | `Draw.Ring` | `Ring` |
| `Triangle` | `Draw.Triangle` | `Triangle` |
| `Line` | `Draw.Line` | `Capsule`（半宽=thickness/2） |
| `Pie` | `Draw.Pie` | `Sector`（inner=0） |
| `Arc` | `Draw.Arc` | `Sector`（环形带） |
| `Polygon` | `Draw.Polygon` | `Polygon`（射线法，支持凹） |
| `Polyline` | `Draw.Polyline` | `PolylineCapsule`（胶囊链） |
| `Quad` | `Draw.Quad` | `Polygon`（4 顶点） |
| `RegularPolygon` | `Draw.RegularPolygon` | `Polygon`（按 sideCount/angle 算顶点） |

其中 `Rectangle / Disc / Triangle / Pie / Polygon / Quad / RegularPolygon`（实心可填充）另有 `(normal, hover, pressed)` **四态颜色重载**，按句柄 `Hovered/Pressed` 自动选色。

### 圆角 / roundness 扩展重载

三个图元新增了 Shapes 原生支持但 `IDraw` 之前未暴露的圆角能力参数，每种提供单色 + 四态色两个重载：

| 图元 | 新增参数 | 类型 | 实现方式 |
|------|---------|------|---------|
| `Polygon` | `roundRadius` | `float`，世界单位绝对圆角半径 | 自研 `BuildRoundedPolygonPath`（见下） |
| `RegularPolygon` | `roundness` | `float`，0~1 圆角程度 | 直接透传 Shapes 原生 `Draw.RegularPolygon(..., roundness, ...)` |
| `Triangle` | `roundness` | `float`，0~1 圆角程度 | 直接透传 Shapes 原生 `Draw.Triangle(..., roundness, ...)` |

**命中区**：扩展重载的命中区仍用**不含圆角的原始几何**（`Polygon` → 原始顶点平直多边形，`RegularPolygon` / `Triangle` → 不传 roundness 的原始顶点）。圆角半径较小时差异可忽略。

### `BuildRoundedPolygonPath` 算法

Shapes 原生 `PolygonPath.ArcTo` 不区分凸角/凹角——凹角处圆心落在多边形外侧，导致弧线穿越相邻边形成自相交，EarClipping 三角剖分因此失败。本方法用**角平分线算法**独立构建圆角路径，同时对凸角和凹角正确工作：

```
dIn = (B-A).normalized,  dOut = (C-B).normalized       // 沿边远离顶点 B 的方向
α = acos(-dot(dIn, dOut))                                // 两边夹角 ∈ (0, π)
t = r / tan(α/2)                                         // 切点到顶点距离
P1 = B - dIn·t,  P2 = B + dOut·t                         // 边上切点
bisector = (-dIn + dOut).normalized                       // 角平分线方向
center = B + bisector · (r / sin(α/2))                    // 圆心
sweep = atan2(P2-center) - atan2(P1-center), 归一化 [-π,π]
按 sweep 方向角度扫描生成弧点
```

关键性质：
- `(-dIn + dOut)` 对**凸角**自然指向多边形内侧、对**凹角**自然指向外侧 → 圆心始终在正确位置
- 无需缠绕方向检测（Shoelace）、无需凸/凹判定、无需符号翻转
- 角度扫描（`atan2`）替代 `Slerp`，方向天然与多边形缠绕一致
- 切点距离钳制到 `min(edgeInLen, edgeOutLen) × 0.49`，防止相邻角弧线重叠
- `roundRadius ≤ 0` 时退化为 `AddPoints`（无圆角）
- 弧线细分密度取自 `ShapesConfig.Instance.polylineDefaultPointsPerTurn`

### 参数顺序规范（遵循 Shapes）
`IDraw.XXX` 的参数顺序严格沿用 Shapes 官方约定：

> **`ShapeName( [Positioning], [Essentials], [Specials], [Coloring] )`**
> Positioning：position / **rotation** / pivot；Essentials：radius/size/thickness/start-end/vertices；Specials：cornerRadius / **angles(pie/arc)** / joins / dash；**Coloring 永远在最后**。

本框架的两个附加参数不破坏这个规范：`id`（句柄键）置于**最前**、`sortingOrder`（图层，非 Shapes 概念）作可选项置于**最后**。即整体为 `IDraw.XXX( id, [Positioning], [Essentials], [Specials], [Coloring], sortingOrder=0 )`。

### 旋转覆盖
**只有 `Rectangle / Pie / Arc` 提供旋转重载**——把 `rotation`（**度数**）放在 `center` 之后的 Positioning 槽位（因可选参数须在末尾,故做成独立重载而非尾加默认值）：
```csharp
IDraw.Rectangle("r", center, 30f, size, cornerRadius, color);   // 斜 30°，命中同步倾斜
IDraw.Pie("p", center, 45f, radius, from, to, color);
IDraw.Arc("a", center, 45f, radius, thickness, from, to, color);
```
为什么只有这三个：2D 内绕 z 旋转,只有它们「旋转有视觉效果且无现成角度旋钮」。其余图元的定向方式——`RegularPolygon` 用 `angle`、`Disc/Ring` 旋转对称(无效)、点列图元(`Triangle/Quad/Polygon/Polyline/Line`)直接给旋转后的顶点(同 Shapes,对点列图元不提供 pos/rot)。

> **单位区分**：`rotation` 用**度数**（Positioning，直配 `Quaternion.Euler`）；而 `Pie/Arc` 的 `from/to`、`RegularPolygon` 的 `angle` 用**弧度**（沿用 Shapes 的角度约定）。

**不在覆盖内**：`Draw` 的 3D 图元（`Sphere / Cuboid / Cube / Cone / Torus` 等）——它们没有 2D 点-在-形状的命中意义。`Text / Texture` 暂未纳入（需要时可按 `Box` 退化命中）。

**注意分配**：`Polygon` / `Polyline` 每帧会构建 `PolygonPath` / `PolylinePath` 与顶点数组，有少量 GC（立即模式本就每帧重建）。`Polygon` 带 `roundRadius` 时 `BuildRoundedPolygonPath` 会生成更多点（每个角按弧度细分），GC 略增。`IDrawOverloads.cs` 的 `ToArray` 处留有 TODO：可按 id 缓存数组/Path、点不变时复用。

---

## 9. 与 Shapes 的耦合面 / 版本兼容（升级指南）

Shapes 是放在 `Assets/ThirdParty/Shapes` 的 **vendored 源码**（手动重新导入才会变，不会自动升级）。万一升级导致 API 变动，按本节核对即可。

- **已适配版本**：Shapes **4.6.0**（见 `Assets/ThirdParty/Shapes/package.json`），Unity 6000.0。
- **隔离事实**：核心程序集 `UnityDemo.Shared.ShapesInteract`（接口 / 事件 / 输入 / `ShapesHitArea` / Manager）**0 依赖 Shapes**，升级影响为零。耦合**全部**在 `Controls` / `Editor` / `Samples`，且对 `Draw` 的调用**收口在 `IDraw`**（一处可修）。
- **耦合面清单**（升级时只需复查这些）：
  - `IDraw` 用到：`Draw.Command`、`Draw.Matrix`、`Draw.{Rectangle, Disc, Ring, Triangle, Line, Pie, Arc, Polygon, Polyline, Quad, RegularPolygon}`；`PolygonPath` / `PolylinePath`（`AddPoint(s)`）；`DiscColors`（依赖 `implicit operator DiscColors(Color)`，`DiscColors.cs`）；`ShapesConfig.Instance.polylineDefaultPointsPerTurn`（`BuildRoundedPolygonPath` 弧线细分密度）。
  - 控件用到：`ImmediateModeShapeDrawer`（基类）、`ShapeRenderer.GetBounds()`、组件 `Rectangle` / `Disc`。
- **升级 checklist**：
  1. 重新导入 Shapes 后**先编译**——任何签名变化都会编译期报错并指到具体行；
  2. 按上面的耦合面清单逐项核对（重点：`Draw.XXX` 重载、`DiscColors` 隐式转换、`Path` 构建 API、`RegularPolygon` 的角度/顶点约定）；
  3. 跑一遍验证场景（每个 `IDraw` 图元能画能点、命中边界正确）；
  4. 更新本节的版本号。
- **原则**：**绝不改 Shapes 源码**，所有适配只动我们自己的程序集（避免重新导入时被覆盖、也无 merge 冲突）。
