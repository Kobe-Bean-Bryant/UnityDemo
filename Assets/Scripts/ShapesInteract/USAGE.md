# ShapesInteract 实操指南

> **ShapesInteract 文档地图** ·
> [README](./README.md) 总览与选型 · **USAGE（本篇）** 实操指南 · [RENDERING](./RENDERING.md) 渲染与层级 · [IDRAW_INTERNALS](./IDRAW_INTERNALS.md) IDraw 原理

本篇是**怎么用**。三种实现方式（① 组件 / ② 立即自实现 / ③ `IDraw`）的**选型见 [README](./README.md)**；本篇按这三种方式分章给出工作流与代码。涉及「层级 / 渲染 / 字号」只给一句话 + 指针到 [RENDERING](./RENDERING.md)。

**目录**：[§0 场景搭建](#0-场景搭建) · [§1 组件模式](#1-组件模式所见即所得) · [§2 立即模式自实现](#2-立即模式自实现写代码) · [§3 IDraw 可交互绘制](#3-idraw可交互绘制) · [§4 协同与混用](#4-协同与混用) · [§5 排错](#5-排错清单点了没反应) · [§6 示例一览](#6-示例一览samplesscripts) · [§7 API 速查](#7-api-速查附录)

---

## 0. 场景搭建（类比 uGUI 的 EventSystem）

任何交互生效，场景里都**必须有且仅有一个 `ShapesInteractionManager`**，并能拿到一台相机：

1. 一台相机（2D UI 建议 **Orthographic**）。
2. 新建空 GameObject → Add Component → **Shapes Interaction Manager**。
3. 把它的 `_camera` 指到该相机（留空则用 `Camera.main`，要求相机 Tag = MainCamera）。

> 就像 uGUI 没有 EventSystem 按钮就不响应：`ShapesInteractionManager` 每帧读输入、做射线命中、派发事件，但它本身**不绘制**任何东西。

输入：自动兼容新/旧输入系统（`Active Input Handling` = Old / New / Both 都行）。

---

## 1. 组件模式（所见即所得）

适合常规 UI。用 Shapes 的组件 Shape 当外观，挂我们的控件组件。

### 工作流
1. 确保场景有 `ShapesInteractionManager`（§0）。
2. 菜单 **GameObject → Shapes UI → Button / Slider / Toggle** 一键创建；或在带 `Shapes.Rectangle` 的物体上 **Add Component → Shapes Button**。
3. Inspector 调外观（Rectangle 的 Width/Height/颜色）、四态颜色、`interactable`、`Sorting Order`（同时管渲染层级与命中，见 [RENDERING §2](./RENDERING.md)）。
4. 接线事件：
   - **Button**：`On Click ()` → `+` → 拖目标对象 → 选方法。
   - **Slider**：`On Value Changed (Single)` → 拖对象 → 选方法。⚠️ 选**最上方「Dynamic float」分组**里的方法才会传实时值（下方 Static 是填死值）。
   - **Toggle**：同理选 **Dynamic bool**。

### 控件清单
- **`ShapesButton`**：`UnityEvent onClick`。命中区 = 自带 `Rectangle` 的 `GetBounds()`；hover/press 自动变色。
- **`ShapesToggle`**：`bool IsOn` + `onValueChanged(bool)` + `SetIsOnWithoutNotify`；`checkmark` 图形随状态显隐。
- **`ShapesSlider`**：`float Value` / `NormalizedValue` + `onValueChanged(float)` + `SetValueWithoutNotify`；约定 轨道=本物体 Rectangle、`fill`=左对齐填充、`handle`=把手（创建菜单已搭好）。
- **`ShapeInteractable`**（低层通用）：给任意现成 Shape「就地」加交互，暴露 `onClick/onEnter/onExit/onDown/onUp/onDrag/onMove` 全套 UnityEvent，无需写控件类（见下）。

四态颜色（normal/highlighted/pressed/disabled + 过渡时长）与 `interactable` 由基类 `ShapesSelectable` 提供。

### 事件绑定的三种风格（都不过时，按喜好选）
| 风格 | 怎么写 | 适合 |
|------|--------|------|
| **Inspector 连线**（桥脚本） | 控件 `On Click ()` → 拖一个脚本 → 选其方法（如 `InteractionSampleController`） | 设计师友好、可视化、与代码解耦——uGUI 标准玩法 |
| **代码绑定** | `btn.onClick.AddListener(() => …)`、`slider.Value = 0.5f`（字段/属性是 public） | 纯代码工作流、逻辑集中在脚本里 |
| **`IDraw` 闭包** | `IDraw.Rectangle(...).OnClick = () => …`（§3） | 立即模式、代码画的图形，最直接 |

> `onClick` 是无参事件，无法在 Inspector 直接设颜色等复杂参数（UnityEvent 不支持 Color 静态参数）。需要时写一个「桥」脚本暴露方法（见 `Samples/Scripts/InteractionSampleController.cs`）再拖上去——这正是 uGUI 里 `GameManager.OnButtonClick()` 的常规做法，不是老旧写法；嫌绕就用后两种。三者底层都走同一个 Manager。

### 给现成 Shape 加交互：`ShapeInteractable`（无需写控件）
只想让一个**已存在的 Shape** 能点/能 hover、又不想做成完整控件，就用 `ShapeInteractable`——「裸交互」组件：不管外观，命中区自动取该 Shape 的 `GetBounds()`，暴露全套 7 个事件的 UnityEvent。

**用法（无代码，Inspector 连线）：**
1. 一个 GameObject 上挂好 `Shapes.Disc`（或任意 `ShapeRenderer`）当外观。
2. 再 **Add Component → Shapes UI/Shape Interactable (Generic)**（`shape` 留空会自动取同物体的 ShapeRenderer）。
3. 在 `On Click ()` 里连方法，运行后点这个 Shape 即触发。

**也可代码监听**（`onClick` 等是 public UnityEvent）：
```csharp
[SerializeField] ShapeInteractable hot;     // 把那个物体拖进来
void Start() => hot.onClick.AddListener(() => Debug.Log("clicked"));
```

### `ShapesSelectable` vs `ShapeInteractable`（容易混，看这张表）
两个**没有继承关系**、定位完全不同的类：

| | `ShapesSelectable` | `ShapeInteractable` |
|---|---|---|
| 是什么 | 做「**控件**」的**抽象基类** | 给「**一个现成 Shape**」挂上收事件的独立组件 |
| 外观 | Shapes 组件当 `targetGraphic` + **内置四态颜色/过渡** | 不管外观（由那个 ShapeRenderer 决定） |
| 逻辑怎么来 | **继承它写代码**，或直接用现成子类 | **连 UnityEvent**，零代码 |
| uGUI 类比 | **`Selectable`**（Button/Toggle/Slider 继承它） | **`EventTrigger`**（给任意对象挂上转发事件） |

一句话：**`ShapesSelectable` = 写/用「控件」（有四态外观）；`ShapeInteractable` = 给「一个现成 Shape」挂上收事件（无外观状态、零代码）。**

### 写一个新控件（继承 `ShapesSelectable`）
想要**新种类**控件（如自定义 RadioButton）→ 继承基类；只是要按钮/开关/滑块 → 直接用现成子类。
```csharp
public class MyControl : ShapesSelectable, IShapesPointerClickHandler
{
    public void OnPointerClick(ShapesPointerEvent e)
    {
        if (!IsInteractable) return;
        // 用 e.LocalPoint / e.WorldPoint 做事，或改 targetGraphic 等
    }
}
```

---

## 2. 立即模式自实现（写代码）

适合自定义/复杂控件，或喜欢用代码完全掌控绘制。**一个脚本既绘制又交互**——自己实现 `IShapesRaycastTarget` + 需要的 handler。

骨架（完整范例见 `Samples/Scripts/ImmediateButtonSample.cs`、`ImmediateDraggableKnobSample.cs`）：
```csharp
using Shapes;
using UnityEngine;
using UnityDemo.Shared.ShapesInteract;

[ExecuteAlways] // 想在编辑器里预览绘制就加；交互仍只在运行时由 Manager 触发（见 §5）
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
- `ContainsLocalPoint` 收到的是**本物体局部坐标**（Manager 已按相机/宽高比换算好）。用 `ShapesHitArea` 或 `Rect`/`Bounds` 做命中，**和 `DrawShapes` 用同一份几何**。
- 拖拽：实现 `IShapesDragHandler`，用 `e.LocalPoint`（含 `e.LocalDelta`）；拖出命中区仍持续收到 `OnDrag`（跟手）。
- `SortingOrder` 在立即模式只管命中、不管渲染层级，详见 [RENDERING](./RENDERING.md)。

### 命中粒度的三种情形（A / B / C）
> 这是**②立即模式内部**的话题——「一个 Drawer 画了多个 Shape，怎么给特定 Shape 加交互」，**不是**与「实现方式 ①②③」并列的东西。核心始终是：**命中区你定义、handler 里做事，绘制照旧；交互的粒度是「逻辑元素」而非「单次 Draw 调用」，无需为每个可交互 Shape 单开 Drawer。**

**情形 A：多个互相独立的可交互 Shape（如工具栏几个按钮）** — 各自一个 target。最省事其实用 ③ `IDraw`（一个 Drawer 画多个、每个自动带命中区与句柄）：
```csharp
public class Toolbar : ImmediateModeShapeDrawer
{
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }
    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))
        {
            IDraw.Rectangle("save", new Vector3(-2,0,0), new Vector2(1.5f,0.8f), 0.1f, Color.white).OnClick = () => Debug.Log("save");
            IDraw.Rectangle("load", new Vector3( 0,0,0), new Vector2(1.5f,0.8f), 0.1f, Color.white).OnClick = () => Debug.Log("load");
            IDraw.Disc("help", new Vector3(2,0,0), 0.5f, Color.cyan).OnClick = () => Debug.Log("help");
        }
    }
}
```
> 完整示例 `Samples/Scripts/InteractiveDrawMenuSample.cs`。不想写代码画：每个 Shape 放一个 GameObject + `Shapes.Rectangle` + `ShapeInteractable`，Inspector 接 `onClick`（即 §1 的 `ShapeInteractable`）。

**情形 B：一个复合控件的多个子部件（如 ColorPicker 的色环 + SV 方块）** — 属于**同一个**控件 → 用**一个** target 覆盖整体，在 handler 里按 `LocalPoint` 分流：
```csharp
public class TwoZoneWidget : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerClickHandler
{
    [SerializeField] float radius = 1f;                        // 左：圆
    [SerializeField] Rect box = new Rect(1.2f, -0.5f, 1f, 1f); // 右：方块
    public Transform Transform => transform;  public int SortingOrder => 0;
    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); }

    public bool ContainsLocalPoint(Vector2 p)                  // 命中区 = 圆 ∪ 方块
        => ShapesHitArea.Circle(p, Vector2.zero, radius) || box.Contains(p);

    public void OnPointerClick(ShapesPointerEvent e)           // handler 里分流
    {
        if (ShapesHitArea.Circle(e.LocalPoint, Vector2.zero, radius)) Debug.Log("圆");
        else if (box.Contains(e.LocalPoint)) Debug.Log("方块");
    }
    public override void DrawShapes(Camera cam) { /* 画圆 + 画方块 */ }
}
```
> 完整示例 `Samples/Scripts/ShapesColorPickerSample.cs`（复刻官方 ColorPicker，色环 + SV 方块按 `LocalPoint` 分流）。

**情形 C：大量同质项（网格成百上千 cell）** — 同样「一个 target 覆盖整片」，用 `ShapesHitArea.TryGetCell` 把点换算成 cell 索引（**别**一个 cell 一个 target），见下「网格范式」。

**怎么选**：互相独立 → A；同一控件几个区域 → B；海量同质项 → C。

### 网格范式（→ PathfindingDemo）
成百上千 cell 的高效写法：**一个 Drawer 画全部 + 一个 target 命中整片 + `TryGetCell` 算坐标**（完整示例 `Samples/Scripts/GridInteractionSample.cs`，它还演示在网格上叠一个可拖拽斜矩形 token）：
```csharp
public class MyGrid : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerMoveHandler, IShapesPointerClickHandler
{
    [SerializeField] int width = 8, height = 6; [SerializeField] float cellSize = 1f;
    public Transform Transform => transform;  public int SortingOrder => 0;

    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); }

    public bool ContainsLocalPoint(Vector2 p)              // 命中区 = 整片网格
        => p.x >= 0 && p.x < width*cellSize && p.y >= 0 && p.y < height*cellSize;

    public void OnPointerMove(ShapesPointerEvent e)        // 逐格 hover
        { if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var c)) _hover = c; }
    public void OnPointerClick(ShapesPointerEvent e)
        { if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var c)) Select(c); }

    public override void DrawShapes(Camera cam) { /* 循环画所有 cell */ }
}
```
**套到 PathfindingDemo**：让 `PathfindingDrawer`（已循环画所有 cell）实现 `IShapesRaycastTarget`（`ContainsLocalPoint` = `[0, W·cellSize]×[0, H·cellSize]`）+ `IShapesPointerClickHandler`，在 `OnPointerClick` 里用 `TryGetCell` 得 `(x,y)` 查 `PathfindingManager.Instance.Grid.GetCell`——一个 target、O(1) 命中、cell 动态增减都不卡。

---

## 3. `IDraw`：可交互绘制

保留 `Draw.XXX` 风格，但用 `IDraw.XXX` 让画出来的图形「天生」可交互。在你**自己的** `ImmediateModeShapeDrawer.DrawShapes` 里一处绘制：
```csharp
using UnityDemo.Shared.ShapesInteract.Controls;

public class CodeMenu : ImmediateModeShapeDrawer
{
    [SerializeField] Shapes.Disc external;             // 跨对象目标（可空）
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }  // 清理句柄

    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))               // 替代 Draw.Command(cam) + Draw.Matrix
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
- **入口** `IDraw.Command(cam, this)`（内部就是 `Draw.Command` + `Draw.Matrix`）。块内可混用原生 `Draw.XXX`（纯装饰），按调用顺序绘制（**后画盖先画**，见 [RENDERING §3](./RENDERING.md)）。
- **方法（覆盖全部 2D 图元）**：`Rectangle / Disc / Ring / Triangle / Line / Pie / Arc / Polygon / Polyline / Quad / RegularPolygon`，每个都「画 + 自动建命中区 + 返回句柄」。其中实心可填充的 `Rectangle / Disc / Triangle / Pie / Polygon / Quad / RegularPolygon` 另有 `(normal, hover, pressed)` 四态颜色重载，hover/press 自动变色。（命中区 ↔ 图元对应、参数顺序规范见 [IDRAW_INTERNALS §8](./IDRAW_INTERNALS.md)。）
- **旋转**：`Rectangle / Pie / Arc` 有旋转重载，把 `rotation`（**度数**）放在 `center` 之后，画与命中同步倾斜：
  ```csharp
  IDraw.Rectangle("r", new Vector3(-1,0,0), 30f, new Vector2(2,1), 0.1f, Color.white); // 斜 30°
  ```
  其余图元的定向：`RegularPolygon` 用 `angle`、`Pie/Arc` 用 `from/to`、点列图元给旋转后的点、`Disc/Ring` 旋转对称无需。（`rotation` 是度数，`from/to`/`angle` 是弧度。）
- **id**：每个图形给稳定字符串 id，句柄按 id **跨帧持久**（监听器/状态保留）；某帧不再画它就自动注销。
- **加行为**：句柄是**可赋值委托**——`handle.OnClick = () => …`（每帧赋值幂等，别用 `AddListener` 会每帧累积）。还有 `OnEnter/OnExit/OnDown/OnUp/OnDrag/OnMove` 与实时状态 `Hovered/Pressed`。
- **清理**：owner 在 `OnDisable` 调一次 `IDraw.Release(this)`。
- **边界**：适合少量图形；大量动态项用 §2 网格范式。
- 原理（句柄/Command/生命周期）见 [IDRAW_INTERNALS](./IDRAW_INTERNALS.md)。

### 让交互改变「别的 Shape」（本 drawer / 别的 drawer / 组件 Shape 都行）
句柄的 `OnClick` 等是普通委托（闭包），**能引用到什么就能改什么**，不限于 Inspector 拖进来的目标。按目标身份分两种：

**A. 目标是组件模式的持久 Shape**（`Shapes.Disc`/`Rectangle` 等 GameObject）——拿引用直接改其 `Color`/`Radius`…。引用来源随意（`[SerializeField]` 拖 / `GetComponent` / `FindObjectOfType` / 单例 / 注册表）。

**B. 目标是「另一个立即模式 Drawer」画的 Shape**——立即模式的图不是持久对象，是**每帧按状态变量重画**的；「改它」=「改那个 Drawer 绘制时读的状态变量」：
```csharp
// Drawer A：按钮点击改 Drawer B 的状态变量
public class MenuDrawer : ImmediateModeShapeDrawer
{
    [SerializeField] private OtherDrawer other;        // 引用另一个 Drawer（拖 / Find / 单例皆可）
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }
    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))
            IDraw.Rectangle("btn", new Vector3(-2,0,0), new Vector2(2,1), 0.1f, Color.white)
                 .OnClick = () => other.tint = Color.red;   // ← 改另一个 Drawer 的状态
    }
}
// Drawer B：纯绘制，按 tint 画；改了 tint，下一帧自动变
public class OtherDrawer : ImmediateModeShapeDrawer
{
    public Color tint = Color.white;                   // 状态变量
    public override void DrawShapes(Camera cam)
    {
        using (Draw.Command(cam)) { Draw.Matrix = transform.localToWorldMatrix; Draw.Disc(Vector3.zero, 1f, tint); }
    }
}
```
> 一句话：**组件 Shape 改引用、立即模式 Shape 改它的状态变量**。多个 Drawer 互相影响时，推荐用一个**共享状态对象**（不靠互相引用、更不靠 `IDraw` 内部登记表）——见 `Samples/Scripts/Hud*`（`HudSharedState` + 三个 Drawer）。`InteractiveDrawMenuSample` 演示了「改本 drawer 状态变量」和「改一个组件 `Disc` 颜色」两种。

### 让「外部」监听 `IDraw` 的事件
句柄的 `OnClick` 是 public `Action`，能 `=` 也能 `+=`。但若在 `DrawShapes` 里**每帧 `=`**，会把外部 `+=` 的监听每帧冲掉。两种干净写法：
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
> 想要「Inspector 连线 / 多订阅者 / 设计师可视化」开箱即用 → 直接用**组件模式**（§1 的 UnityEvent）；`IDraw` 句柄是为「就地内联」设计的，外部监听就转发一层。

---

## 4. 协同与混用

- **同一个 `ShapesInteractionManager` 同时服务三种方式**：组件控件、立即模式 widget、`IDraw` 句柄都注册到它，走同一套命中与派发。
- **组件 + 脚本协作**（和 uGUI 一样）：组件控件在 Inspector 接线，回调里调你写的脚本方法，脚本再改其它对象（包括一个不可交互的 Shape）。
- **混用**：一个场景里「菜单生成的组件按钮」+「挂了立即模式脚本的代码按钮」+「`IDraw` 画的图形」都能正常 hover/click，互不干扰；重叠时由 `SortingOrder` 决定**命中**优先（**渲染**层级见 [RENDERING](./RENDERING.md)）。
- **多 Drawer 互相影响**：用共享状态对象，见 §3「改别的 Shape」与 `Samples/Scripts/Hud*` 多 Drawer 样例。

---

## 5. 排错清单（点了没反应）

1. 场景里有没有 `ShapesInteractionManager`？它的 `_camera` 是否指到在用的相机（或相机 Tag=MainCamera）？
2. 控件是否在相机视野内、z=0 附近？（正交 Size 5 时大约 x∈[-8,8]、y∈[-5,5] 可见）
3. 是否在 **Game 视图**里点击（Scene 视图不触发交互）？
4. 控件能否被命中：组件模式确认有 `targetGraphic`（缺失会 Console 警告）；细控件可调大 `hitPadding`。
5. 立即模式确认实现里调了 `Register`，且 `ContainsLocalPoint` 的几何与绘制一致。
6. **不运行时 SceneView/GameView 看不到立即模式 Drawer 的绘制？** 给该脚本加 `[ExecuteAlways]`。立即模式 Drawer 靠 `OnEnable` 订阅相机渲染，而 `OnEnable` **默认只在 Play 执行**；加 `[ExecuteAlways]` 才会在编辑态也订阅（交互逻辑仍只在 Play，因 `Manager.Update` 不在编辑态跑）。组件模式 Shape（真 Renderer）无此问题。
7. **谁压谁上面 / 文字时大时小** → 见 [RENDERING](./RENDERING.md)（层级 = `SortingOrder` 还是绘制序、字号 = TMP 点数随坐标空间）。

---

## 6. 示例一览（`Samples/Scripts/`）

| 脚本 | 演示 | 方式 |
|------|------|------|
| `InteractionSampleController.cs` | 组件控件事件接到代码方法（「桥」脚本），改一个非交互 Disc | ① 组件 |
| `ImmediateButtonSample.cs` | 一个脚本既画又交互（hover/press/click） | ② 立即自实现 |
| `ImmediateDraggableKnobSample.cs` | 代码实现可拖拽把手（down/drag）、`SortingOrder` | ② 立即自实现 |
| `GridInteractionSample.cs` | 一个 Drawer + 单 target 管 N×M 格、逐格 hover/点击；**并叠一个可拖拽斜矩形 token**（同 Drawer 多层命中 + 旋转命中 + 拖拽吸附） | ② 网格 + ③ `IDraw` |
| `ShapesColorPickerSample.cs` | **复刻官方 ColorPicker**：色环+SV 方块由一个 target 承载 | ② 立即自实现（复合，情形 B） |
| `InteractiveDrawMenuSample.cs` | `IDraw` 画按钮/圆点、四态变色、跨对象改色、拖拽 | ③ `IDraw` |
| `HudBarsDrawer` + `HudControlsDrawer` + `HudThemeDrawer`(+`HudSharedState`) | **多种 Drawer 共渲染 + 经共享状态互相影响**：原始 Drawer 画血/蓝/耐力条、IDraw 画控制按钮、mode② 画主题选择器，三者互不引用、只共享 `HudSharedState` | ①原始 + ②自实现 + ③`IDraw` |

> 前提同 §0：场景要有一个 `ShapesInteractionManager` 和相机。把示例脚本挂到空 GameObject 即可（立即模式无需任何 Shapes 组件）。
> HUD 样例：三个 Drawer 各挂一个空物体、再建一个挂 `HudSharedState` 的物体，把三个 Drawer 的 `shared` 都指向它（`origin` 已默认错开不重叠）。

---

## 7. API 速查（附录）

### 核心程序集 `UnityDemo.Shared.ShapesInteract`（不依赖 Shapes）
| 脚本 | 职责 / 关键 API |
|------|----------------|
| `ShapesInteractionManager` | 中央派发器（场景放一个）。`[SerializeField] _camera`；静态 `Register(target)` / `Unregister(target)`。 |
| `IShapesRaycastTarget` | 命中契约：`Transform Transform`、`int SortingOrder`、`bool ContainsLocalPoint(Vector2)`。 |
| `IShapesPointer*Handler`（7 个） | 细粒度事件：`Enter`/`Exit`/`Move`（悬停每帧）/`Down`/`Up`/`Drag`/`Click`。**只实现你需要的**。 |
| `ShapesPointerEvent`（struct） | 事件数据：`ScreenPosition`、`WorldPoint`、`LocalPoint`、`LocalDelta`、`Target`。 |
| `ShapesPointerInput` | 新旧输入适配：`TryGetMouse(out screenPos, out pressed, out held, out released)`。 |
| `ShapesHitArea` | 命中数学：`Box`/`Circle`/`Ring`/`Sector`/`Capsule`/`Triangle`/`Polygon`/`PolylineCapsule`/`Rotate`，网格助手 `TryGetCell(p, origin, cellSize, w, h, out cell)`。 |

### 控件程序集 `…ShapesInteract.Controls`（依赖 Shapes）
| 组件 | 用途 / 关键 API |
|------|----------------|
| `ShapesSelectable`（抽象基类） | 命中 + 四态颜色 + `interactable` + `hitPadding` + `SortingOrder`（桥接到 renderer，见 [RENDERING](./RENDERING.md)）。 |
| `ShapesButton` | `UnityEvent onClick`。 |
| `ShapesToggle` | `bool IsOn`、`onValueChanged(bool)`、`SetIsOnWithoutNotify`。 |
| `ShapesSlider` | `float Value`、`NormalizedValue`、`onValueChanged(float)`、`SetValueWithoutNotify`。 |
| `ShapeInteractable` | 低层通用：给任意 Shape 就地加交互，暴露 `onClick/onEnter/…` 全套 UnityEvent。 |
| `IDraw`（静态） | 立即模式可交互绘制（§3）：`Command(cam, owner)` / `Release(owner)` + 全部 2D 图元方法。原理见 [IDRAW_INTERNALS](./IDRAW_INTERNALS.md)。 |
| `InteractiveShapeHandle` | `IDraw.XXX` 返回的句柄：`Hovered/Pressed` + 可赋值委托 `OnClick/OnEnter/OnExit/OnDown/OnUp/OnDrag/OnMove`。 |
