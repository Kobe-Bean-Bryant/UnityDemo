# ShapesInteract —— Shapes 指针交互框架 + 类 uGUI 控件

一套与渲染解耦的指针交互框架，配合 [Shapes](https://acegikmo.com/shapes/) 使用。**三种实现方式**（底层都落到同一个 `IShapesRaycastTarget` + 同一个 `ShapesInteractionManager`，可混用）：

1. **组件模式**：类 uGUI 的组件控件（Button / Toggle / Slider）与通用 `ShapeInteractable`，挂在 `ShapeRenderer` 旁，`GetBounds()` 自动命中。
2. **立即模式自实现**：`: ImmediateModeShapeDrawer` 并实现框架接口、自写 `ContainsLocalPoint`。**网格复合范式**（一个 Drawer + 一个 target 管成百上千 cell）是它的应用。
3. **`IDraw` 可交互绘制**：`IDraw.Rectangle(...)` 既画又按参数自动带命中区、返回句柄加行为——立即模式的便捷封装。

> 📖 **想直接上手？看 [USAGE.md](./USAGE.md)**（必备场景设置、各脚本 API、组件模式 / 立即模式工作流、示例、排错）。本文档侧重设计理念。

## 设计理念

- **接口而非基类**：交互契约是接口 `IShapesRaycastTarget` + 一组细粒度 handler 接口。一个脚本既可以是 `ImmediateModeShapeDrawer`（立即模式绘制）又同时实现交互接口；也可以是挂在组件 Shape 旁的普通脚本。绘制方式与交互完全正交。
- **仿 uGUI EventSystem**：一个中央派发器 `ShapesInteractionManager` + 细粒度 handler 接口（`IShapesPointerClickHandler`、`IShapesDragHandler`…）。控件只实现自己需要的事件。
- **核心不依赖 Shapes**：`ShapesInteract` 程序集只用 `Camera`/`Ray`/`Plane`/`Input`（外加 `Unity.InputSystem`），从不调用 Shapes 绘制 API。它只负责把「指针在某目标的某局部点做了什么」投递出去。Shapes 依赖被隔离在 `ShapesInteract.Controls` 程序集。
- **新旧输入兼容**：`ShapesPointerInput` 用编译宏适配 Active Input Handling 的 Old / New / Both 三种设置。

## 数据流（每帧）

```
ShapesInteractionManager.Update
  → ShapesPointerInput.TryGetMouse  (新输入优先, 旧输入兜底)
  → camera.ScreenPointToRay
  → 遍历已注册 IShapesRaycastTarget:
        把世界射线转到目标局部空间 → 与 z=0 平面求交 → ContainsLocalPoint
        取 SortingOrder 最大的命中者
  → 状态机派发: Enter/Exit · Move(悬停每帧) · Down · Drag(含 LocalDelta) · Up · Click
```

坐标换算全在 Manager 完成，所以一切**与相机位置/缩放/宽高比无关**——控件只拿到干净的局部坐标。

## 程序集结构

| 程序集 | 内容 | 引用 |
|--------|------|------|
| `UnityDemo.Shared.ShapesInteract` | 接口、事件、输入适配、命中数学、Manager | `Unity.InputSystem` |
| `UnityDemo.Shared.ShapesInteract.Controls` | `ShapesSelectable` / Button / Toggle / Slider / `ShapeInteractable` | 核心 + `Shapes Runtime` |
| `UnityDemo.Shared.ShapesInteract.Controls.Editor` | `GameObject → Shapes UI →` 创建菜单 | Controls + `Shapes Runtime`（仅编辑器） |

## 快速上手

1. 场景里放**一台相机**（2D UI 建议正交）。
2. 新建空物体，挂 **`ShapesInteractionManager`**，把 `_camera` 指向相机（留空则用 `Camera.main`）。
3. `GameObject → Shapes UI → Button`（或 `Slider` / `Toggle`）创建控件；也可在任意物体上 `Add Component → Shapes Button`（会自动带一个 `Shapes.Rectangle`）。
4. 在控件的 Inspector 里接线 `onClick` / `onValueChanged`，调四态颜色、`interactable` 等。

> 渲染前提：Shapes 在 URP 下需要对应 Renderer 挂 `ShapesRenderFeature`（本项目 PC/Mobile Renderer 已配置）。

## 控件用法

- **ShapesButton**：`UnityEvent onClick`。命中区 = 自带 `Rectangle` 的 `GetBounds()`；hover/press 自动变色。
- **ShapesToggle**：`bool IsOn` + `onValueChanged(bool)`；`checkmark` 图形随状态显隐。
- **ShapesSlider**：`float Value` + `onValueChanged(float)`；约定 轨道=本物体 Rectangle、`fill`=左对齐填充、`handle`=把手（创建菜单已搭好）。
- **ShapeInteractable**（低层通用）：挂在任意带 `ShapeRenderer` 的物体上即可点击，暴露 `onClick/onEnter/onExit/onDown/onUp/onDrag` 等 UnityEvent，无需写代码。

四态颜色与 `interactable` 由基类 `ShapesSelectable` 提供（normal/highlighted/pressed/disabled + 过渡时长）。

## 一个 Drawer 画了多个 Shape，怎么给「特定 Shape」加交互？

**交互的粒度是「逻辑元素」，不是「单次 Draw 调用」**，绘制与交互彻底解耦。不需要为每个可交互 Shape 单开一个 Drawer。按「这些 Shape 之间是什么关系」分三种情形：

### 模式①：多个**互相独立**的可交互 Shape（如工具栏的几个按钮）
每个 Shape 是一个独立逻辑元素 → 各自一个 target。最省事用 `IDraw`：一个 Drawer 里画多个，每个 `IDraw.XXX` 自动带命中区、返回各自句柄。
```csharp
public class Toolbar : ImmediateModeShapeDrawer
{
    public override void OnDisable() { base.OnDisable(); IDraw.Release(this); }
    public override void DrawShapes(Camera cam)
    {
        using (IDraw.Command(cam, this))
        {
            IDraw.Rectangle("save", new Vector3(-2, 0, 0), new Vector2(1.5f, 0.8f), 0.1f, Color.white)
                 .OnClick = () => Debug.Log("save");
            IDraw.Rectangle("load", new Vector3(0, 0, 0), new Vector2(1.5f, 0.8f), 0.1f, Color.white)
                 .OnClick = () => Debug.Log("load");
            IDraw.Disc("help", new Vector3(2, 0, 0), 0.5f, Color.cyan)
                 .OnClick = () => Debug.Log("help");
        }
    }
}
```
> 完整示例：`Samples/Scripts/InteractiveDrawMenuSample.cs`。（不想写代码画的话：每个 Shape 放一个 GameObject + `Shapes.Rectangle` + `ShapeInteractable`，在 Inspector 接 `onClick`。）

### 模式②：一个**复合控件**的多个子部件（如 ColorPicker 的色环 + SV 方块）
它们属于**同一个**控件 → 用**一个** target 覆盖整体，在 handler 里按 `LocalPoint` 判断点了哪个子部件。
```csharp
public class TwoZoneWidget : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerClickHandler
{
    [SerializeField] float radius = 1f;                              // 左：圆
    [SerializeField] Rect box = new Rect(1.2f, -0.5f, 1f, 1f);       // 右：方块
    public Transform Transform => transform;  public int SortingOrder => 0;
    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); }

    public bool ContainsLocalPoint(Vector2 p)                        // 命中区 = 圆 ∪ 方块
        => ShapesHitArea.Circle(p, Vector2.zero, radius) || box.Contains(p);

    public void OnPointerClick(ShapesPointerEvent e)                 // 在 handler 里分流
    {
        if (ShapesHitArea.Circle(e.LocalPoint, Vector2.zero, radius)) Debug.Log("圆");
        else if (box.Contains(e.LocalPoint)) Debug.Log("方块");
    }
    public override void DrawShapes(Camera cam) { /* 画圆 + 画方块 */ }
}
```
> 完整示例：`Samples/Scripts/ShapesColorPickerSample.cs`（色环 + SV 方块，按 `LocalPoint` 分流）。

### 模式③：**大量同质**的项（如网格的成百上千 cell）
也是「一个 target 覆盖整片」，但用 `ShapesHitArea.TryGetCell` 把点换算成 cell 索引（不要一个 cell 一个 target）。
```csharp
public void OnPointerClick(ShapesPointerEvent e)
{
    if (ShapesHitArea.TryGetCell(e.LocalPoint, Vector2.zero, cellSize, width, height, out var cell))
        Debug.Log($"点了第 ({cell.x},{cell.y}) 格");
}
```
> 完整示例：`Samples/Scripts/GridInteractionSample.cs`。

**怎么选**：互相独立 → ①；同一控件的几个区域 → ②；海量同质项 → ③。共同点都是：**命中区你定义、handler 里做事，绘制照旧**。

## 写一个新控件

**组件模式**（继承基类）：
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

**立即模式**（一个脚本既画又交互）：
```csharp
public class MyImmediateWidget : ImmediateModeShapeDrawer,
    IShapesRaycastTarget, IShapesPointerClickHandler
{
    [SerializeField] Vector2 size = Vector2.one;
    public Transform Transform => transform;
    public int SortingOrder => 0;

    public override void OnEnable()  { base.OnEnable();  ShapesInteractionManager.Register(this); }
    public override void OnDisable() { base.OnDisable(); ShapesInteractionManager.Unregister(this); }

    public bool ContainsLocalPoint(Vector2 p)
        => new Rect(-size/2, size).Contains(p);            // 与下面 DrawShapes 用同一几何

    public void OnPointerClick(ShapesPointerEvent e) { /* ... */ }

    public override void DrawShapes(Camera cam)
    {
        using (Draw.Command(cam)) { Draw.Matrix = transform.localToWorldMatrix;
            Draw.Rectangle(Vector3.zero, size); }
    }
}
```
> 注意：实现者**必须**在 `OnEnable/OnDisable` 里 `Register/Unregister`，否则对象销毁后会因悬空引用抛异常。

## 命中说明
- 命中测试在目标**局部空间**的 `z=0` 平面进行；组件模式默认用 `ShapeRenderer.GetBounds()`（局部 AABB）。
- 命中重叠时 `SortingOrder` 大者优先。
- 框架对相机投影（正交/透视）皆可用；2D UI 用正交相机更直观。
