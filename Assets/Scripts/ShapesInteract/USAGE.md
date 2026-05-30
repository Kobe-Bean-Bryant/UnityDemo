# ShapesInteract 使用说明

本框架让 Shapes 图形能响应鼠标/触摸，并提供一套类 uGUI 的控件。它有**三种实现方式**（① 组件模式 ② 立即模式自实现 ③ `IDraw` 可交互绘制），但**底层完全相通**、可在同一场景共存（像 uGUI 那样「组件 + 脚本」协同）。先看 **§6 的总表**选对方式。

> 设计原理见 [README.md](./README.md)；本文是**怎么用**。

---

## 0. 必备前提（类比 uGUI 的 EventSystem）

任何交互生效，场景里都**必须有且仅有一个 `ShapesInteractionManager`**，并能拿到一台相机：

1. 一台相机（2D UI 建议 **Orthographic**）。
2. 新建空 GameObject → Add Component → **Shapes Interaction Manager**。
3. 把它的 `_camera` 指到该相机（留空则用 `Camera.main`，要求相机 Tag = MainCamera）。

> 就像 uGUI：没有 EventSystem，按钮也不会响应。我们的 `ShapesInteractionManager` 就是这个角色——每帧读输入、做射线命中、派发事件。它本身**不绘制**任何东西。

输入：自动兼容新/旧输入系统（`Active Input Handling` = Old / New / Both 都行）。

---

## 1. 脚本职责与 API 速查

### 核心程序集 `UnityDemo.Shared.ShapesInteract`（不依赖 Shapes）

| 脚本 | 职责 / 关键 API |
|------|----------------|
| `ShapesInteractionManager` | 中央派发器（场景里放一个）。`[SerializeField] _camera`；静态 `Register(target)` / `Unregister(target)`。 |
| `IShapesRaycastTarget` | 可被命中的契约：`Transform Transform`、`int SortingOrder`、`bool ContainsLocalPoint(Vector2)`。 |
| `IShapesPointer*Handler`（7 个） | 细粒度事件：`Enter` / `Exit` / `Move`（悬停期间每帧）/ `Down` / `Up` / `Drag` / `Click`。**只实现你需要的**。 |
| `ShapesPointerEvent`（struct） | 事件数据：`ScreenPosition`、`WorldPoint`、`LocalPoint`、`LocalDelta`、`Target`。 |
| `ShapesPointerInput` | 新旧输入适配：`TryGetMouse(out screenPos, out pressed, out held, out released)`。 |
| `ShapesHitArea` | 命中数学：`Box`、`Circle`、`Ring`、`Sector`、`Capsule`、`Triangle`、`Polygon`，以及网格助手 `TryGetCell(p, origin, cellSize, w, h, out cell)`。 |

### 控件程序集 `...ShapesInteract.Controls`（依赖 Shapes）

| 组件 | 用途 / 关键 API |
|------|----------------|
| `ShapesSelectable`（抽象基类） | 命中 + 四态颜色（normal/highlighted/pressed/disabled）+ `interactable` + `hitPadding`（命中区每侧扩展，方便点中细控件）。 |
| `ShapesButton` | `UnityEvent onClick`。 |
| `ShapesToggle` | `bool IsOn`、`onValueChanged(bool)`、`SetIsOnWithoutNotify`。 |
| `ShapesSlider` | `float Value`、`NormalizedValue`、`onValueChanged(float)`、`SetValueWithoutNotify`。 |
| `ShapeInteractable` | 低层通用：给任意 Shape「就地」加交互，暴露 `onClick/onEnter/...` UnityEvent。 |
| `IDraw`（静态） | 立即模式可交互绘制（见 §7）：`Command(cam, owner)` / `Rectangle/Disc/Ring/Triangle(id, …)` / `Release(owner)`。 |
| `InteractiveShapeHandle` | `IDraw.XXX` 返回的句柄：`Hovered/Pressed` + 可赋值委托 `OnClick/OnEnter/OnExit/OnDown/OnUp/OnDrag/OnMove`。 |

### `SortingOrder` 怎么用

当多个目标在指针下**重叠**时，框架只把事件派给 `SortingOrder` **最大**的那个（类似 UI 层级）。两种模式写法一致：

- 组件模式：在控件/`ShapeInteractable` 的 Inspector 填 `Sorting Order` 字段。
- 立即模式：让 `public int SortingOrder => sortingOrder;` 返回你的字段。

例：一个面板（order 0）上盖一个按钮（order 1）——点按钮只触发按钮，不会穿透到面板。

---

## 2. 组件模式工作流（所见即所得）

适合常规 UI。用 Shapes 的组件 Shape 当外观，挂我们的控件组件。

1. 确保场景有 `ShapesInteractionManager`（见第 0 节）。
2. 菜单 **GameObject → Shapes UI → Button / Slider / Toggle** 一键创建；或在某个带 `Shapes.Rectangle` 的物体上 **Add Component → Shapes Button**。
3. 在 Inspector 里调外观（Rectangle 的 Width/Height/颜色）、四态颜色、`interactable`。
4. 接线事件：
   - **Button**：`On Click ()` → `+` → 拖入目标对象 → 选其方法。
   - **Slider**：`On Value Changed (Single)` → `+` → 拖对象 → 选方法。⚠️ 选**最上方「Dynamic float」分组**里的方法，才会把实时数值传进去（下方 Static 分组是填死值）。
   - **Toggle**：同理选 **Dynamic bool**。

> `onClick` 是无参事件，无法在 Inspector 里直接设颜色等复杂参数（UnityEvent 不支持 Color 静态参数）。需要时写一个「桥」脚本暴露方法（见 `Samples/Scripts/InteractionSampleController.cs`），再把方法拖到事件上——这正是 uGUI 里写 `GameManager.OnClick()` 的常规做法。

### 事件绑定的三种风格（都不过时，按喜好选）

控件的事件（`onClick` / `onValueChanged`）可以用任一种方式绑定，**没有谁淘汰谁**，只是侧重不同：

| 风格 | 怎么写 | 适合 |
|------|--------|------|
| **Inspector 连线**（桥脚本） | 控件 `On Click ()` → 拖一个脚本 → 选其方法（如 `InteractionSampleController`） | 设计师友好、可视化、与代码解耦——uGUI 的标准玩法 |
| **代码绑定** | 拿到控件引用后 `btn.onClick.AddListener(() => …)`、`slider.Value = 0.5f`（字段/属性是 public） | 纯代码工作流、逻辑集中在脚本里 |
| **`IDraw` 闭包** | `IDraw.Rectangle(...).OnClick = () => …`（见 §7） | 立即模式、代码画的图形，最直接 |

> 「桥脚本」不是老旧写法，它就是 uGUI 里 `GameManager.OnButtonClick()` 的常规做法；嫌它绕就用后两种。三者底层都走同一个 Manager。

### 给现成 Shape 加交互：`ShapeInteractable`（无需写控件）

当你只想让一个**已存在的 Shape**（自己摆的 `Shapes.Rectangle`/`Disc`…）能点/能 hover，又不想做成完整控件，就用 `ShapeInteractable`——它是「裸交互」组件：不管外观，命中区自动取该 Shape 的 `GetBounds()`，并暴露 `onClick / onEnter / onExit / onDown / onUp / onDrag / onMove` 这全套 UnityEvent（覆盖 7 个 handler）。

和 `ShapesButton` 的区别：`ShapesButton` = 完整按钮（带四态变色、`RequireComponent(Rectangle)`）；`ShapeInteractable` = 只让一个现成 Shape 发出指针事件，外观由你自己（或那个 ShapeRenderer）决定。

**用法（无代码，Inspector 连线）：**
1. 一个 GameObject 上挂好 `Shapes.Disc`（或任意 `ShapeRenderer`）当外观。
2. 再 **Add Component → Shapes UI/Shape Interactable (Generic)**（`shape` 留空会自动取同物体的 ShapeRenderer）。
3. 在 Inspector 的 `On Click ()` 里连一个方法（同 §2 的连线方式）。运行后点这个 Shape 即触发。

**也可代码监听**（`onClick` 等是 public UnityEvent）：
```csharp
[SerializeField] ShapeInteractable hot;     // 把那个物体拖进来
void Start() => hot.onClick.AddListener(() => Debug.Log("clicked"));
```

> 它是「模式①：多个独立可交互 Shape」最省事的实现——多个独立 Shape 各挂一个 `ShapeInteractable` 即可，无需写任何控件类。

### `ShapesSelectable` vs `ShapeInteractable`（容易混，看这张表）

组件模式里有两个容易混的类，定位完全不同（**没有继承关系**）：

| | `ShapesSelectable` | `ShapeInteractable` |
|---|---|---|
| 是什么 | 做「**控件**」的**抽象基类** | 给「**一个现成 Shape**」挂上收事件的独立组件 |
| 外观 | Shapes 组件当 `targetGraphic` + **内置四态颜色/过渡** | 不管外观（由那个 ShapeRenderer 自己决定） |
| 逻辑怎么来 | **继承它写代码**，或直接用现成子类 | **连 UnityEvent**，零代码 |
| uGUI 类比 | **`Selectable`**（`Button`/`Toggle`/`Slider` 继承它） | **`EventTrigger`**（给任意对象挂上转发事件） |

**`ShapesSelectable` 的两种用法**（它是抽象类，不直接用）：
- 想要**新种类**的控件（如自定义 RadioButton）→ 继承它写代码。
- 只是要按钮/开关/滑块 → **直接用已基于它写好的 `ShapesButton`/`ShapesToggle`/`ShapesSlider`**，不必自己继承，照样在 Inspector 连 `onClick`。

一句话：**`ShapesSelectable` = 写/用「控件」（有四态外观）；`ShapeInteractable` = 给「一个现成 Shape」挂上收事件（无外观状态、零代码）。**

---

## 3. 立即模式工作流（写代码）

适合自定义/复杂控件，或喜欢用代码完全掌控绘制的场景。**一个脚本既绘制又交互**。

骨架（完整范例见 `Samples/Scripts/ImmediateButtonSample.cs`、`Samples/Scripts/ImmediateDraggableKnobSample.cs`）：

```csharp
using Shapes;
using UnityEngine;
using UnityDemo.Shared.ShapesInteract;

[ExecuteAlways] // 想在编辑器里预览绘制就加；交互仍只在运行时由 Manager 触发
public class MyWidget : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerClickHandler   // 只实现需要的 handler
{
    [SerializeField] Vector2 size = new Vector2(2, 1);
    [SerializeField] int sortingOrder;

    public Transform Transform => transform;
    public int SortingOrder => sortingOrder;

    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }   // 必须注册
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); } // 必须注销

    // 命中区与绘制用「同一份几何」，保证看到的=能点的
    public bool ContainsLocalPoint(Vector2 p) => new Rect(-size * 0.5f, size).Contains(p);

    public void OnPointerClick(ShapesPointerEvent e) => Debug.Log($"clicked at {e.LocalPoint}");

    public override void DrawShapes(Camera cam)
    {
        using (Draw.Command(cam))
        {
            Draw.Matrix = transform.localToWorldMatrix;     // 绘制跟随本物体 Transform
            Draw.Rectangle(Vector3.zero, size, 0.1f, Color.white);
        }
    }
}
```

要点：
- **必须** `base.OnEnable()/OnDisable()`（注册 Shapes 渲染）+ `Register/Unregister`（注册到 Manager）。漏掉注销会在对象销毁后抛异常。
- `ContainsLocalPoint` 收到的是**本物体局部坐标**（Manager 已按相机/宽高比换算好，与这些无关）。用 `ShapesHitArea` 或 `Rect`/`Bounds` 做命中，**和 `DrawShapes` 用同一份几何**。
- 拖拽：实现 `IShapesDragHandler`，用 `e.LocalPoint`（含 `e.LocalDelta`）。拖出命中区仍会持续收到 `OnDrag`（跟手）。

---

## 4. 两种模式如何协同（能像 uGUI 一样工作吗？能）

- **同一个 `ShapesInteractionManager` 同时服务两种模式**：组件控件与立即模式 widget 都注册到它，走同一套命中与派发。
- **组件 + 脚本协作**：和 uGUI 一样——组件控件（Button/Slider）在 Inspector 接线，回调里调用你写的脚本方法；脚本再去改其它对象（包括一个**不可交互**的 Shape）。
- **混用**：一个场景里可以同时有「GameObject → Shapes UI → Button」生成的组件按钮，和挂了 `ImmediateButtonSample` 的代码按钮，二者都能正常 hover/click，互不干扰，靠 `SortingOrder` 决定重叠时谁优先。

---

## 5. 排错清单（点了没反应）

1. 场景里有没有 `ShapesInteractionManager`？它的 `_camera` 是否指到在用的相机（或相机 Tag=MainCamera）？
2. 控件是否在相机视野内、在 z=0 附近？（正交 Size 5 时大约 x∈[-8,8]、y∈[-5,5] 可见）
3. 是否在 **Game 视图**里点击（Scene 视图不会触发）？
4. 控件能否被命中：组件模式确认有 `targetGraphic`（控件会在 Console 警告缺失）；细控件可调大 `hitPadding`。
5. 立即模式确认实现里调了 `Register`，且 `ContainsLocalPoint` 的几何与绘制一致。

---

## 6. 三种实现方式底层相通 + 用法层级（先读这个）

按「谁实现 `IShapesRaycastTarget`、命中区怎么来、图怎么画」分，**只有三种实现方式**，但它们**底层都落到同一个 `IShapesRaycastTarget` + 同一个 `ShapesInteractionManager`**，所以**天然共存、混用**，重叠时统一由 `SortingOrder` 裁决。

```
            ShapesInteractionManager（唯一，= EventSystem，每帧射线命中+派发）
                          │
                          ▼
            IShapesRaycastTarget（唯一契约：Transform / SortingOrder / ContainsLocalPoint）
   ┌──────────────────────┬────────────────────────┬────────────────────────┐
 ① 组件模式               ② 立即模式自实现           ③ IDraw 可交互绘制
 ShapeInteractable        : ImmediateModeShape       IDraw.Rectangle/Disc…
 /Button/Toggle/Slider    Drawer + 自实现接口         返回句柄，OnClick=…
 GetBounds() 命中          自写 ContainsLocalPoint     按参数自动命中
 (子模式: 代码控制已有控件)  (应用: 网格复合范式 §8)     (立即模式的便捷封装)
```

| 实现方式 | 何时用 | 命中区怎么来 | 写代码量 |
|------|--------|------------|---------|
| **① 组件模式** | 常规 UI（按钮/开关/滑块），或给现成 Shape 就地加交互 | 自动 `GetBounds()` | 无（菜单/Add Component）；也可代码拿引用 `onClick.AddListener` / `slider.Value=…`（字段 public） |
| **② 立即模式自实现** | 独立复杂控件；或一个 Drawer 管大量项（网格 §8） | 自写 `ContainsLocalPoint` | 一个类、实现接口 |
| **③ `IDraw` 可交互绘制** | 代码画的少量可交互图形（菜单/HUD） | **按 `IDraw.XXX` 参数自动** | 在 `DrawShapes` 里几行（见 §7） |

> 「代码控制已有控件」是 ① 的用法细节（不是独立方式）；「网格复合范式」是 ② 的一个应用（见 §8）。复刻官方 ColorPicker 的 `Samples/Scripts/ShapesColorPickerSample.cs` 就是 ② 的范例（一个 target 承载色环+方块两个子部件）。

### 立即模式自实现 vs `IDraw`：交互的「最小单位」

②和③都在 `ImmediateModeShapeDrawer` 里用代码画，区别在**交互的粒度**：

| | ② 立即模式自实现 | ③ `IDraw` |
|---|---|---|
| 注册的 target 数 | **1 个 / Drawer** | **N 个 / Drawer**（每个 `IDraw.XXX(id,…)` 一个句柄） |
| 交互最小单位 | **整个 Drawer**（你在 `ContainsLocalPoint` 里定义的那片区域） | **单个 Shape**（每次 `IDraw` 调用） |
| 子部件怎么分 | **你自己**在 handler 里按 `LocalPoint` 手动分流 | **框架自动**分（每个句柄独立命中、独立 `OnClick`） |
| 典型场景 | 复合控件（ColorPicker）、海量同质项（网格 §8） | 数量固定的若干独立图形（菜单/HUD/工具栏） |

> 简记：**②「一个复杂可交互 Drawer」，单位是 Drawer**（内部可逻辑细分）；**③「一个 Drawer 里若干可交互 Shape」，单位是 Shape**（框架替你分）。

### AABB / `GetBounds()` —— 何时不用写 `ContainsLocalPoint`
`ShapeRenderer.GetBounds()` 返回该 Shape 的**局部轴对齐包围盒**：对 `Rectangle` **就是矩形本身（精确）**，对 `Disc` 是外接正方形（略大）。组件模式（`ShapeInteractable`/`ShapesSelectable`）默认就用它命中，所以**简单情况你不用写 `ContainsLocalPoint`**。只有立即模式、或想要精确非矩形命中（圆/环/扇形）时才需要自写（用 `ShapesHitArea`）。

### 性能与组织
- Manager 每帧开销 ∝ **注册的 target 数量**（与画了多少 Shape 无关）。
- 每个 `ImmediateModeShapeDrawer` 都会发起自己的一批 `Draw.Command` 并订阅渲染事件——**别为大量项各建一个 drawer**。
- 结论：少量可交互图形用 ③（`IDraw`）；**大量动态项用 ② 的网格范式**（一个 drawer + 一个 target，见 §8），千万别一个 cell 一个 target。

---

## 7. `IDraw`：立即模式可交互绘制

保留 `Draw.XXX` 风格，但用 `IDraw.XXX` 让画出来的图形「天生」可交互。在你**自己的** `ImmediateModeShapeDrawer.DrawShapes` 里一处绘制：

```csharp
using UnityDemo.Shared.ShapesInteract.Controls;

public class CodeMenu : ImmediateModeShapeDrawer
{
    [SerializeField] Shapes.Disc external;             // 跨对象目标（可空）
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }  // 清理句柄

    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))               // 替代 Draw.Command(cam)+Draw.Matrix
        {
            Draw.Disc(new Vector3(0,-2,0), 1f, Color.gray);          // 纯装饰：照常用原生 Draw

            var play = IDraw.Rectangle("play", new Vector3(0,1,0), new Vector2(3,1), 0.15f,
                                       normal: Color.white, hover: Color.gray, pressed: Color.grey);
            play.OnClick = () => { if (external) external.Color = Color.red; };   // 每帧赋值，幂等

            IDraw.Disc("dot", new Vector3(3,1,0), 0.4f, Color.cyan, sortingOrder: 1)
                 .OnDrag = e => Debug.Log(e.LocalDelta);
        }
    }
}
```

要点：
- **入口**：`IDraw.Command(cam, this)`（内部就是 `Draw.Command` + `Draw.Matrix`）。块内可混用原生 `Draw.XXX`（纯装饰），按调用顺序绘制（**后画盖先画**）。
- **方法**：`IDraw.Rectangle / Disc / Ring / Triangle`，每个都「画 + 自动建命中区 + 返回句柄」。`Rectangle`/`Disc` 另有 `(normal, hover, pressed)` 四态颜色重载，hover/press 自动变色。
- **id**：每个图形给一个稳定字符串 id，句柄按 id **跨帧持久**（监听器/状态因此保留）；某帧不再画它就自动注销。
- **加行为**：句柄是**可赋值委托**——`handle.OnClick = () => …`（每帧赋值是幂等的，别用 `AddListener`，会每帧累积）。还有 `OnEnter/OnExit/OnDown/OnUp/OnDrag/OnMove` 与实时状态 `Hovered/Pressed`。
- **跨 Shape/跨 Drawer 影响**：见下方专节——`OnClick` 是闭包，能碰到什么就能改什么。
- **清理**：owner 在 `OnDisable` 调一次 `IDraw.Release(this)`。
- **边界**：适合少量图形；大量动态项请用 §8。

### 让交互改变「别的 Shape」（本 drawer / 别的 drawer / 组件 Shape 都行）

句柄的 `OnClick` 等是普通委托（闭包），**能引用到什么就能改什么**，不限于 Inspector 拖进来的目标。按目标的「身份」分两种：

**A. 目标是组件模式的持久 Shape**（`Shapes.Disc`/`Rectangle` 等 GameObject）——拿到引用直接改其 `Color`/`Radius`…。引用来源随意：`[SerializeField]` 拖、`GetComponent`、`FindObjectOfType`、单例、注册表都行（不止 Inspector）。

**B. 目标是「另一个立即模式 Drawer」画的 Shape**——立即模式的图不是持久对象，是**每帧按状态变量重画**的；所以「改它」=「改那个 Drawer 绘制时读的状态变量」：

```csharp
// Drawer A：按钮点击改 Drawer B 的状态变量
public class MenuDrawer : ImmediateModeShapeDrawer
{
    [SerializeField] private OtherDrawer other;        // 引用另一个 Drawer（拖 / Find / 单例皆可）
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }

    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))
            IDraw.Rectangle("btn", new Vector3(-2, 0, 0), new Vector2(2, 1), 0.1f, Color.white)
                 .OnClick = () => other.tint = Color.red;   // ← 改另一个 Drawer 的状态
    }
}

// Drawer B：纯绘制，按 tint 画；改了 tint，下一帧自动变
public class OtherDrawer : ImmediateModeShapeDrawer
{
    public Color tint = Color.white;                   // 状态变量
    public override void DrawShapes(Camera cam)
    {
        using (Draw.Command(cam))
        {
            Draw.Matrix = transform.localToWorldMatrix;
            Draw.Disc(Vector3.zero, 1f, tint);
        }
    }
}
```

> 一句话：**组件 Shape 改引用、立即模式 Shape 改它的状态变量**。`InteractiveDrawMenuSample` 里就同时演示了「改本 drawer 状态变量」和「改一个组件 `Disc` 的颜色」两种。

### 让「外部」监听 `IDraw` 的事件

句柄的 `OnClick` 是 public `Action`，既能 `=` 也能 `+=`。但若你在 `DrawShapes` 里**每帧 `=` 赋值**，会把外部 `+=` 的监听每帧冲掉。所以让外部监听有两种干净写法：

```csharp
// 写法 A（推荐）：Drawer 暴露自己的事件，inline OnClick 只转发
public class Menu : ImmediateModeShapeDrawer
{
    public event System.Action PlayClicked;                 // 外部订阅它
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }
    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))
            IDraw.Rectangle("play", pos, size, 0.1f, Color.white)
                 .OnClick = () => PlayClicked?.Invoke();      // 内部只转发
    }
}
// 外部： menu.PlayClicked += () => Debug.Log("外部收到点击");

// 写法 B：把句柄存下来暴露，外部自己赋值（注意首帧绘制后才存在）
//   public InteractiveShapeHandle Play { get; private set; }
//   ... Play = IDraw.Rectangle("play", ...);   // DrawShapes 里不再写 .OnClick
//   外部： menu.Play.OnClick += MyHandler;
```

> 想要「Inspector 连线 / 多个订阅者 / 设计师可视化」开箱即用 → 直接用**组件模式**（`ShapesButton` / `ShapeInteractable` 的 UnityEvent，见 §2），那是为外部监听而设计的；`IDraw` 句柄是为「就地内联」设计的，外部监听就转发一层。

---

## 8. 大量格子：网格复合范式（→ PathfindingDemo）

成百上千个 cell **不要**一个 cell 一个 target。范式：**一个 Drawer 画全部 + 一个 target 命中整片 + `TryGetCell` 算坐标**（完整示例见 `Samples/Scripts/GridInteractionSample.cs`）：

```csharp
public class MyGrid : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerMoveHandler, IShapesPointerClickHandler
{
    [SerializeField] int width = 8, height = 6; [SerializeField] float cellSize = 1f;
    public Transform Transform => transform;  public int SortingOrder => 0;

    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); }

    public bool ContainsLocalPoint(Vector2 p)              // 命中区=整片网格
        => p.x >= 0 && p.x < width*cellSize && p.y >= 0 && p.y < height*cellSize;

    public void OnPointerMove(ShapesPointerEvent e)        // 逐格 hover
        { if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var c)) _hover = c; }
    public void OnPointerClick(ShapesPointerEvent e)
        { if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var c)) Select(c); }

    public override void DrawShapes(Camera cam) { /* 循环画所有 cell */ }
}
```

**套到 PathfindingDemo**：让 `PathfindingDrawer`（已在循环里画所有 cell）实现 `IShapesRaycastTarget`（`ContainsLocalPoint` = `[0, W·cellSize]×[0, H·cellSize]`）+ `IShapesPointerClickHandler`，在 `OnPointerClick` 里 `ShapesHitArea.TryGetCell` 得到 `(x,y)`、查 `PathfindingManager.Instance.Grid.GetCell`，即可点击/高亮 cell——一个 target、O(1) 命中、cell 动态增减都不卡。

---

## 9. 示例一览（`Samples/Scripts/`）

| 脚本 | 演示 | 方式 |
|------|------|------|
| `InteractionSampleController.cs` | 组件控件事件接到代码方法（「桥」脚本），改一个非交互 Disc | ① 组件 |
| `ImmediateButtonSample.cs` | 一个脚本既画又交互（hover/press/click） | ② 立即自实现 |
| `ImmediateDraggableKnobSample.cs` | 代码实现可拖拽把手（down/drag）、`SortingOrder` | ② 立即自实现 |
| `GridInteractionSample.cs` | 一个 Drawer + 单 target 管 N×M 格、逐格 hover/点击 | ② 网格范式 |
| `ShapesColorPickerSample.cs` | **复刻官方 ColorPicker**：色环+SV 方块由一个 target 承载，交互全走框架 | ② 立即自实现（复合） |
| `InteractiveDrawMenuSample.cs` | `IDraw` 画按钮/圆点、四态变色、跨对象改色、拖拽 | ③ `IDraw` |

> 用法前提同 §0：场景里要有一个 `ShapesInteractionManager` 和相机。把示例脚本挂到空 GameObject 即可（立即模式无需任何 Shapes 组件）。
