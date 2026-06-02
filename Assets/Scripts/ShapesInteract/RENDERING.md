# 渲染与层级 —— `SortingOrder`、渲染顺序、字号

> **ShapesInteract 文档地图** ·
> [README](./README.md) 总览与选型 · [USAGE](./USAGE.md) 实操指南 · **RENDERING（本篇）** 渲染与层级 · [IDRAW_INTERNALS](./IDRAW_INTERNALS.md) IDraw 原理

本篇集中讲三件容易混、且和坐标空间强相关的事：**谁压在谁上面（渲染层级）**、**重叠时谁收点击（命中优先）**、**文字/形状为什么有时大有时小（字号与坐标空间）**。

一句话先记住:**`SortingOrder` 永远只决定「重叠时谁收点击」；它是否同时决定「显示层级」，取决于实现模式。**

---

## 1. `SortingOrder`：命中优先级

当多个 `IShapesRaycastTarget` 在指针下**重叠**时，`ShapesInteractionManager` 只把事件派给 `SortingOrder` **最大**的那个。

例：面板 `order 0`、其上的按钮 `order 1` → 点按钮只触发按钮，不穿透到面板。

它对**渲染层级**的影响则分两种模式，见下。

---

## 2. 组件模式：`SortingOrder` 渲染 + 点击一个旋钮（uGUI 同款）

`ShapesButton` / `ShapesToggle` / `ShapesSlider` / `ShapeInteractable` 用的 `ShapeRenderer`（`Shapes.Rectangle`/`Disc`…）**本身就是 Unity Renderer**，自带 `sortingOrder` / `SortingLayer` / `renderQueue`，渲染层级听它。

框架已把控件的 `SortingOrder` **桥接到底层 `ShapeRenderer.sortingOrder`**：在控件 Inspector 改一个 `Sorting Order`，**渲染层级和命中优先同时跟着变**——和 uGUI 完全一致。

- 写入 `SortingOrder`（属性或 Inspector）→ `ApplySortingOrder()` 同步到 `targetGraphic`/`shape` 的 renderer。
- 子图形自动叠在底图之上：`ShapesToggle` 的勾 = `SortingOrder + 1`；`ShapesSlider` 的 fill = `+1`、handle = `+2`。
- 整个控件作为一层移动：两个重叠按钮，调高其一的 `Sorting Order`，它既显示在上、点击也优先。

---

## 3. 立即模式 / `IDraw`：渲染靠绘制序，`SortingOrder` 只管点击

立即绘制（`Draw.XXX` / `IDraw.XXX`）**不创建 Renderer**，是直接往命令缓冲里画，所以**没有 sortingOrder 这个东西**。它的：

- **渲染层级 = 绘制调用先后**（画家序，**后画在上**）；或用 z + `Draw.ZTest` 靠深度排。
- **命中优先 = `SortingOrder`**（句柄/target 的那个字段），仅此而已，与渲染无关。

### 同一个 Drawer 内：两轴都在你手里，保持一致即可
想让某图形「显示在上 **且** 点击优先」，就在同一个 `Draw.Command`/`IDraw.Command` 块里**把它最后画** + 给它**更高 `SortingOrder`**。两个设定都在你一处代码里，天然一致。

> 范例：`GridInteractionSample` 的可拖拽 token 在所有 cell **之后**画（渲染在上）、`sortingOrder:1`（点击优先）——一处设定、两轴一致。

### 跨 Drawer / 跨模式：渲染顺序不可靠，三种解法
不同 `ImmediateModeShapeDrawer` 各自挂相机渲染回调，**先后 ≈ 注册顺序、不可控**；而立即绘制与组件 Renderer 又走不同排序机制，没有共同标尺。要可靠层叠，三选一：

1. **合进一个 Drawer**（最简单，回到「同 Drawer」情形）；
2. **用 z 坐标 + `Draw.ZTest`**（靠深度排序，跨 Drawer 也稳）；
3. **不同 `RenderPassEvent`**（`Draw.Command(cam, renderPassEvent)`，按管线阶段排序）。

> 不重叠的 UI（如 HUD 样例三个 Drawer 分处不同屏幕区）则无需关心跨 Drawer 渲染顺序。

### 为什么不让 `SortingOrder` 统管立即模式渲染
要么让 `IDraw` 缓存绘制再按 `SortingOrder` 排序 flush（破坏与原生 `Draw` 混用、且有分配），要么仍管不了跨 Drawer。把「**渲染＝绘制序/z**」「**命中＝SortingOrder**」分清更干净。组件模式能一个旋钮两用，纯粹是因为它底层有真 Renderer。

---

## 4. 字号与坐标空间（为什么文字时大时小）

`Draw.FontSize` 是 **TMP 的 `fontSize`（点数）**，**不是世界高度**——Shapes 内部直接 `tmp.fontSize = FontSize`（`Draw.cs:535`），最终屏幕大小 = 该 TMP 文本 × **当前 `Draw.Matrix` 的缩放**。所以**同一个数字在不同坐标空间，物理大小天差地别**：

| 坐标空间 | `Draw.Matrix` | 1 单位 = | 合适字号 |
|---|---|---|---|
| **世界空间**（本框架 / HUD） | `transform.localToWorldMatrix` | 1 世界单位（米） | 渲染高度 ≈ `FontSize / 10`，想要 ~0.4 高就写 `FontSize ≈ 4` |
| **画布像素空间**（`ImmediateModeCanvas`） | canvas→world 矩阵 | 1 画布像素 | 几百，官方 `IMPanelSample` 用 `FontSize = 240` |

> **形状尺寸（半径/宽高/线宽）同理随 `Draw.Matrix` 空间而定**——这就是画布样例里一切都是几百、而世界空间里是个位数的原因；不是 Canvas「把东西变大」，而是它把绘制切到了「像素」尺度的坐标系。

**绘制文本要点**：`Draw.Text(pos, "内容", TextAlign.Center)`（先设 `Draw.FontSize` / `Draw.Color`），Shapes 自带默认字体、无需指定；`TextAlign.Midline*` 为垂直居中、`Center` 为水平+垂直居中。HUD 样例用它画状态条标签与按钮文字。
