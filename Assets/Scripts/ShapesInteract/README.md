# ShapesInteract —— Shapes 指针交互框架 + 类 uGUI 控件

> **ShapesInteract 文档地图** ·
> **README（本篇）** 总览与选型 · [USAGE](./USAGE.md) 实操指南 · [RENDERING](./RENDERING.md) 渲染与层级 · [IDRAW_INTERNALS](./IDRAW_INTERNALS.md) IDraw 原理

一套与渲染解耦的指针交互框架，配合 [Shapes](https://acegikmo.com/shapes/) 使用：让 Shapes 图形能响应鼠标/触摸，并提供一套类 uGUI 的控件（Button / Toggle / Slider）。

本篇是**门户**：讲清「它是什么、怎么组织、该用哪种方式、从哪读起」。**怎么落地用**看 [USAGE](./USAGE.md)。

---

## 核心心智模型

整套框架围绕**一个派发器 + 一个契约**：

- **`ShapesInteractionManager`**（类比 uGUI 的 `EventSystem`）：场景里**有且仅有一个**，每帧读输入、发射线、做命中、派发事件。它本身**不绘制**。
- **`IShapesRaycastTarget`**（唯一契约）：任何「可被点」的东西都实现它——`Transform Transform`、`int SortingOrder`、`bool ContainsLocalPoint(Vector2)`。

### 数据流（每帧）
```
ShapesInteractionManager.Update
  → ShapesPointerInput.TryGetMouse        (新输入优先, 旧输入兜底)
  → camera.ScreenPointToRay
  → 遍历已注册 IShapesRaycastTarget:
        世界射线 → 目标局部空间 → 与 z=0 平面求交 → ContainsLocalPoint
        取 SortingOrder 最大的命中者
  → 状态机派发: Enter / Exit · Move(悬停每帧) · Down · Drag(含 LocalDelta) · Up · Click
```
坐标换算全在 Manager 完成，所以一切**与相机位置/缩放/宽高比无关**——目标只拿到干净的**局部坐标**。

---

## 设计理念

- **接口而非基类**：交互契约是接口 `IShapesRaycastTarget` + 一组细粒度 handler 接口（`IShapesPointerClickHandler`、`IShapesDragHandler`…）。绘制方式与交互**完全正交**——一个脚本既可以是 `ImmediateModeShapeDrawer`（立即模式绘制）又同时实现交互接口，也可以是挂在组件 Shape 旁的普通脚本。
- **仿 uGUI EventSystem**：一个中央派发器 + 细粒度 handler，控件只实现自己需要的事件。
- **核心不依赖 Shapes**：`ShapesInteract` 程序集只用 `Camera`/`Ray`/`Plane`/`Input`（外加 `Unity.InputSystem`），从不调用 Shapes 绘制 API；只负责把「指针在某目标的某局部点做了什么」投递出去。Shapes 依赖被隔离在 `Controls` 程序集。
- **新旧输入兼容**：`ShapesPointerInput` 用编译宏适配 Active Input Handling 的 Old / New / Both 三种设置。

### 程序集结构
| 程序集 | 内容 | 引用 |
|--------|------|------|
| `UnityDemo.Shared.ShapesInteract` | 接口、事件、输入适配、命中数学（`ShapesHitArea`）、Manager | `Unity.InputSystem` |
| `UnityDemo.Shared.ShapesInteract.Controls` | `ShapesSelectable` / Button / Toggle / Slider / `ShapeInteractable` / `IDraw` | 核心 + `Shapes Runtime` |
| `UnityDemo.Shared.ShapesInteract.Controls.Editor` | `GameObject → Shapes UI →` 创建菜单 | Controls + `Shapes Runtime`（仅编辑器） |

---

## 三种实现方式 · 选型表（全文档唯一的「①②③」）

所有方式**底层都落到同一个 `IShapesRaycastTarget` + 同一个 `ShapesInteractionManager`**，所以天然共存、可混用，重叠时统一由 `SortingOrder` 裁决命中。

```
            ShapesInteractionManager（唯一，= EventSystem，每帧射线命中 + 派发）
                          │
                          ▼
            IShapesRaycastTarget（唯一契约：Transform / SortingOrder / ContainsLocalPoint）
   ┌──────────────────────┬────────────────────────┬────────────────────────┐
 ① 组件模式               ② 立即模式自实现           ③ IDraw 可交互绘制
 Button/Toggle/Slider     : ImmediateModeShape       IDraw.Rectangle/Disc…
 /ShapeInteractable       Drawer + 自实现接口         返回句柄，OnClick = …
 GetBounds() 自动命中      自写 ContainsLocalPoint     按参数自动建命中区
```

| 实现方式 | 何时用 | 命中区怎么来 | 写代码量 | 详见 |
|------|--------|------------|---------|------|
| **① 组件模式** | 常规 UI（按钮/开关/滑块），或给现成 Shape 就地加交互 | 自动 `GetBounds()` | 无（菜单 / Add Component）；也可代码拿引用 `onClick.AddListener` / `slider.Value=…` | [USAGE §1](./USAGE.md) |
| **② 立即模式自实现** | 独立复杂控件（如 ColorPicker）；或一个 Drawer 管大量项（网格） | 自写 `ContainsLocalPoint` | 一个类、实现接口 | [USAGE §2](./USAGE.md) |
| **③ `IDraw` 可交互绘制** | 代码画的少量可交互图形（菜单 / HUD / 工具栏） | **按 `IDraw.XXX` 参数自动** | 在 `DrawShapes` 里几行 | [USAGE §3](./USAGE.md) |

**③ 覆盖全部 2D 图元**（Rectangle/Disc/Ring/Triangle/Line/Pie/Arc/Polygon/Polyline/Quad/RegularPolygon），其中 Rectangle/Pie/Arc 支持**每形状旋转**（画与命中同步）。

### ② vs ③：交互的「最小单位」
| | ② 立即模式自实现 | ③ `IDraw` |
|---|---|---|
| 注册的 target 数 | **1 个 / Drawer** | **N 个 / Drawer**（每个 `IDraw.XXX(id,…)` 一个句柄） |
| 交互最小单位 | **整个 Drawer**（你在 `ContainsLocalPoint` 定义的区域） | **单个 Shape**（每次 `IDraw` 调用） |
| 子部件怎么分 | **你自己**在 handler 里按 `LocalPoint` 分流 | **框架自动**分（每个句柄独立命中、独立 `OnClick`） |
| 典型场景 | 复合控件、海量同质项（网格） | 数量固定的若干独立图形 |

### 何时**不用**写 `ContainsLocalPoint`
`ShapeRenderer.GetBounds()` 返回 Shape 的**局部 AABB**：对 `Rectangle` 就是矩形本身（精确），对 `Disc` 是外接正方形（略大）。**组件模式默认就用它命中**，所以简单情况你不写 `ContainsLocalPoint`。只有立即模式、或要精确非矩形命中（圆/环/扇形）时才自写（用 `ShapesHitArea`）。

### 性能
- Manager 每帧开销 ∝ **注册的 target 数量**（与画了多少 Shape 无关）。
- 每个 `ImmediateModeShapeDrawer` 各发一批 `Draw.Command` 并订阅渲染——**别为大量项各建一个 Drawer**。
- 结论：少量可交互图形用 ③；**大量动态项用 ② 的网格范式**（一个 Drawer + 一个 target，见 [USAGE §2](./USAGE.md)），别一个 cell 一个 target。

---

## 该读哪篇

| 你想… | 看 |
|-------|----|
| 搭场景、按三种方式落地、写控件、接事件、排错、查 API | **[USAGE](./USAGE.md)** |
| 搞清楚「谁压谁上面 / 谁先收点击 / 文字为何时大时小」 | **[RENDERING](./RENDERING.md)** |
| 了解 `IDraw` 内部怎么实现（句柄、Command 上下文、生命周期、版本耦合面） | **[IDRAW_INTERNALS](./IDRAW_INTERNALS.md)** |

---

## Quick Start（30 秒）

1. 场景里放**一台相机**（2D UI 建议正交）。
2. 新建空物体，挂 **`ShapesInteractionManager`**，把 `_camera` 指向相机（留空则用 `Camera.main`）。
3. `GameObject → Shapes UI → Button`（或 `Slider` / `Toggle`）创建控件；也可在任意物体上 `Add Component → Shapes Button`（会自动带一个 `Shapes.Rectangle`）。
4. 在控件 Inspector 接线 `onClick` / `onValueChanged`，调四态颜色、`interactable` 等。

> 渲染前提：Shapes 在 URP 下需对应 Renderer 挂 `ShapesRenderFeature`（本项目 PC/Mobile Renderer 已配置）。

完整工作流、示例与排错见 **[USAGE](./USAGE.md)**。
