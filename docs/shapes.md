# Shapes 集成指南

[Shapes](https://acegikmo.com/shapes/docs) 是 Freya Holmér 开发的实时矢量图形库，支持在代码中绘制线条、圆环、矩形、多边形等几何形状，常用于调试可视化、HUD 和程序化艺术。Shapes 声明支持 BIRP、URP、HDRP 三条管线，但在 URP 中需要额外配置。

## 为什么必须添加 ShapesRenderFeature

Shapes 在 URP 中的渲染依赖一条完整的链条：

```
每帧渲染流程：
1. beginCameraRendering 事件触发 → Shapes 绘制代码执行 → 分配绘制资源（文本元素等）
2. ShapesRenderFeature.AddRenderPasses() → 为每个绘制命令创建渲染 Pass 并入队
3. Render Graph 执行 → RecordRenderGraph() 将绘制命令录入 GPU 命令缓冲
4. FrameCleanup() → 释放绘制资源（文本元素归还对象池等）
```

如果 Renderer 上没有 `ShapesRenderFeature`，**步骤 2-4 永远不会执行**：
- 绘制命令无法提交给 GPU → 形状不显示
- 分配的资源永远不会被释放 → 内存泄漏
- 最终触发 `Text element allocation cap of 1000 reached` 错误和 NullReferenceException

> Shapes 的导入脚本（`ShapesImportState.cs`）本应自动添加该 Feature，但在当前版本中该自动配置代码已被注释掉（`EnsureShapesPassExistsInTheUrpRenderer()` 方法被 `/* */` 包裹），因此需要手动操作。

### BIRP 为什么不需要这一步

在 Built-in RP 中，Shapes 通过 `Camera.onPostRender` 回调来完成资源回收，这个回调在 `DrawCommand` 的 static 构造函数中自动注册，不依赖任何外部配置。URP 废弃了这套 Camera 事件，改用 Renderer Feature 机制，因此必须显式挂载。

## 配置步骤

### 添加 Renderer Feature

选中 Renderer 资产 → Inspector → **Add Renderer Feature** → **Shapes Render Feature**。

所有会运行 Shapes 绘制代码的 Renderer 都需要添加：

| Renderer | 是否需要 | 当前状态 |
|----------|---------|---------|
| `PC_Renderer.asset` | 是 | 已添加 |
| `Mobile_Renderer.asset` | 是 | 已添加 |
| `Renderer_2D.asset`（如有） | 如果 2D Demo 也使用 Shapes 则需要 | 按需添加 |

### 从 BIRP 迁移资源到 URP

如果从 Asset Store 导入的 Shapes 示例资源（或任何 BIRP 项目资源）显示紫色，说明材质使用了 BIRP 的 `Standard` 着色器：

1. 选中紫色的材质文件
2. 菜单：**Edit → Rendering → Materials → Convert Selected Built-in Materials to URP**
3. 如果场景漆黑，检查灯光是否为 Baked 模式 → 改为 Realtime（BIRP 的烘焙数据不跨管线）

## Shapes 兼容 2D 吗

Shapes 本身是渲染管线无关的矢量图形库，它通过 `beginCameraRendering` 事件钩入任意 URP 摄像机。只要对应的 Renderer 上挂载了 `ShapesRenderFeature`，**无论 3D 还是 2D 场景都可以使用 Shapes 绘制**。

如果你创建了 2D Demo 并希望在其中使用 Shapes，需要把 `ShapesRenderFeature` 也添加到 `Renderer_2D.asset` 上。

## 画质常见问题

### Game View 有锯齿，Scene View 没有

Scene View 使用 Unity Editor 内部的抗锯齿设置（默认开启），不受项目配置影响。Game View 使用项目的 Pipeline Asset 配置。

如果 Game View 中 Shapes 图形边缘有锯齿，检查 Pipeline Asset 的 **Anti Aliasing (MSAA)** 设置：

- 选中 `Assets/Settings/PC_RPAsset.asset`
- Inspector → Quality → **Anti Aliasing (MSAA)** → 改为 **4x**（推荐）或 2x
- Mobile 端可保持 Disabled 或设为 2x（MSAA 有性能开销）

### Free Aspect 画面模糊

Game View 使用 **Free Aspect** 时，实际渲染分辨率等于 Game View 面板的像素尺寸。面板越小，分辨率越低，画面越糊。叠加以下因素会更明显：

- **Render Scale < 1.0**：`Mobile_RPAsset` 的 Render Scale 为 0.8（只渲染 80% 分辨率再放大）。如果当前使用的是 Mobile 质量等级，画面会额外模糊
- **无 MSAA**：关闭抗锯齿时，低分辨率的锯齿感更强

解决方法：
1. Game View 顶部分辨率下拉框选择固定分辨率（如 1920×1080）替代 Free Aspect
2. 确认 `Edit → Project Settings → Quality` 中当前平台使用的是 **PC** 质量等级（Render Scale = 1.0）

## 学习资源

- [Shapes 官方文档](https://acegikmo.com/shapes/docs) — API 参考与使用指南
- [Shapes Asset Store 页面](https://assetstore.unity.com/packages/tools/particles-effects/shapes-173167) — 功能介绍与支持的管线说明
- [Shapes 展示视频（YouTube）](https://www.youtube.com/watch?v=WrAaGn-8qsk) — Freya Holmér 的官方展示
- [Shapes 教程（YouTube）](https://www.youtube.com/watch?v=W9GeaAlIoEg) — 基础 Math、Coding 和 UI 示例
