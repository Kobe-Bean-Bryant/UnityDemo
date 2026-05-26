# 渲染管线与渲染模式

本项目基于 **URP**（Universal Render Pipeline）框架，同时支持 **3D** 和 **2D** 两种渲染模式。本文从零开始介绍 URP 的核心概念，帮助不熟悉 Unity 渲染体系的开发者快速上手。

## URP 渲染管线基础

**渲染管线**（Render Pipeline）是将场景中的 3D/2D 数据（网格、材质、灯光、摄像机）转换为最终屏幕像素的完整流水线。Unity 提供了三条管线：

| 管线 | 全称 | 定位 |
|------|------|------|
| **BIRP** | Built-in Render Pipeline | Unity 旧版内置管线，不可扩展，逐步被替代 |
| **URP** | Universal Render Pipeline | 通用管线，覆盖移动端到 PC，性能与画质平衡，**本项目使用** |
| **HDRP** | High Definition Render Pipeline | 高清管线，面向高端 PC/主机，追求电影级画质 |

三条管线的**材质和着色器互不兼容**：BIRP 的 `Standard` 着色器在 URP 中显示为紫色（品红色），反之亦然。如果从 Asset Store 导入的资源显示紫色，通常需要通过 `Edit → Rendering → Materials → Convert Selected Built-in Materials to URP` 转换材质。同样，烘焙光照数据（Lightmap）也不跨管线，切换后需重新烘焙或改用实时光照。

### URP 的三层架构

URP 的配置分为三层，从上到下依次是：

```
Project Settings → Graphics → Scriptable Render Pipeline Settings
└── Pipeline Asset（管线资产）          ← 全局渲染参数：HDR、MSAA、阴影距离……
    └── Renderer（渲染器）              ← 决定"怎么画"：3D 或 2D，挂载 Renderer Feature
        └── Camera（摄像机）            ← 选择使用哪个 Renderer，决定"从哪看"
```

- **Pipeline Asset**（`UniversalRenderPipelineAsset`）：管线级别的全局配置。项目中有两个：`PC_RPAsset`（PC 平台，开启 HDR/MSAA）和 `Mobile_RPAsset`（移动平台，关闭高开销特性）。通过 `Project Settings → Quality` 为不同平台指定不同的 Pipeline Asset。
- **Renderer**（渲染器）：挂载在 Pipeline Asset 上，决定具体的渲染方式。URP 提供两种渲染器——**Universal Renderer**（实际是 3D 渲染器，名字复用了管线名）和 **2D Renderer**。一个 Pipeline Asset 可以持有多个 Renderer，Camera 通过 index 选择使用哪个。
- **Camera**（摄像机）：每个场景的 Camera 通过 `Renderer Index` 属性指定使用哪个 Renderer，从而决定 3D 还是 2D 渲染。

### Render Graph（Unity 6）

Unity 6 引入了 **Render Graph** 作为 URP 的渲染调度机制。Render Graph 将每一帧的渲染拆解为一系列有依赖关系的 Pass（渲染步骤），由系统自动优化执行顺序和资源分配。对于使用者而言，最直接的影响是第三方插件需要实现 `RecordRenderGraph()` 方法来注册自己的渲染步骤（旧版的 `Execute()` 方法在 Unity 6.3 中已被移除，仅在兼容模式下可用）。

## Settings/ 配置文件说明

`Assets/Settings/` 目录存放所有渲染管线相关的配置资产：

| 文件 | 类型 | 职责 |
|------|------|------|
| `PC_RPAsset.asset` | Pipeline Asset | PC 平台管线配置（HDR、MSAA、阴影距离、深度/不透明纹理等） |
| `Mobile_RPAsset.asset` | Pipeline Asset | 移动平台管线配置（关闭 HDR、降低阴影精度等轻量化参数） |
| `PC_Renderer.asset` | Universal Renderer | 3D 渲染器（PC），挂载 Renderer Feature（如 SSAO、ShapesRenderFeature） |
| `Mobile_Renderer.asset` | Universal Renderer | 3D 渲染器（Mobile），挂载 Renderer Feature（如 ShapesRenderFeature） |
| `Renderer_2D.asset` | 2D Renderer | 2D 渲染器（首次创建 2D Demo 时自动生成并注册） |
| `DefaultVolumeProfile.asset` | Volume Profile | 默认后处理配置（Bloom、色调映射等） |
| `SampleSceneProfile.asset` | Volume Profile | 示例场景的后处理配置 |
| `UniversalRenderPipelineGlobalSettings.asset` | 全局设置 | Light Layer 命名、Shader Stripping 等全局配置 |

它们之间的层级关系：

```
GraphicsSettings（项目级）
└── UniversalRenderPipelineAsset (PC_RPAsset / Mobile_RPAsset)
    └── m_RendererDataList:
        [0] Universal Renderer (PC_Renderer / Mobile_Renderer)
        │   └── Renderer Features: [SSAO, ShapesRenderFeature, ...]
        [N] 2D Renderer (Renderer_2D)  ← 首次创建 2D Demo 时自动添加
```

### PC vs Mobile 质量差异

两套 Pipeline Asset 针对不同平台做了差异化配置：

| 参数 | PC (`PC_RPAsset`) | Mobile (`Mobile_RPAsset`) |
|------|-----|--------|
| Render Scale | 1.0（原生分辨率） | 0.8（80% 分辨率，提升性能） |
| 阴影级联 | 4 级 | 1 级 |
| 阴影贴图分辨率 | 2048px | 1024px |
| 额外光源阴影 | 支持 | 不支持 |
| Soft Shadows | 开启 | 关闭 |
| SSAO | 有（Renderer Feature） | 无（节省 GPU 开销） |
| Depth / Opaque 纹理 | 开启 | 关闭 |

### Quality Level 映射

`Project Settings → Quality` 中配置了两个质量等级，按平台自动选择：

| 质量等级 | 关联的 Pipeline Asset | 默认平台 |
|----------|----------------------|----------|
| Mobile | `Mobile_RPAsset` | Android、iOS |
| PC | `PC_RPAsset` | Windows、macOS、Linux |

## 2D / 3D 切换原理

一个 Pipeline Asset 可以持有多个 Renderer。Camera 通过 **Renderer Index** 选择使用哪个：

- 创建 **3D Demo** 时，Camera 使用默认 Renderer（index 0，即 Universal Renderer）
- 创建 **2D Demo** 时，工具自动查询 2D Renderer 在列表中的实际 index 并设置给 Camera

打开不同 Demo 的场景就自动切换渲染模式，无需额外操作。

> **注意**：2D Renderer 资产（`Renderer_2D.asset`）在首次创建 2D Demo 时自动生成并注册到两个 Pipeline Asset 中，未使用 2D 功能前不会修改任何现有配置。

### SceneSetupHelper 实现细节

`Assets/Scripts/Editor/Shared/SceneSetupHelper.cs` 负责所有场景的创建，其核心逻辑如下：

**3D 场景创建** (`Setup3DScene`)：
- 创建透视 Camera（position: 0, 1, -10），ClearFlags 设为 Skybox
- 不添加 `UniversalAdditionalCameraData` 组件 → Camera 自动使用 Pipeline Asset 的默认 Renderer（index 0，即 Universal Renderer）
- 创建 Directional Light（暖白色，模拟太阳光）

**2D 场景创建** (`Setup2DScene`)：
- 先调用 `EnsureRenderer2DSetup()` 确保 2D Renderer 已就绪（见下文）
- 创建正交 Camera（orthographic size: 5），ClearFlags 设为 SolidColor
- 添加 `UniversalAdditionalCameraData` 组件并调用 `SetRenderer(index)` 指向 2D Renderer
- 创建 Global Light 2D（2D 专用光源）

**2D Renderer 注册** (`EnsureRenderer2DSetup`)：
1. 尝试加载 `Assets/Settings/Renderer_2D.asset`，不存在则创建新的 `Renderer2DData`
2. 遍历两个 Pipeline Asset（PC / Mobile），通过 `SerializedObject` 反射访问 `m_RendererDataList` 数组
3. 如果该数组中已存在 Renderer_2D → 直接返回其 index（**幂等**，不重复注册）
4. 如果不存在 → 追加到数组末尾，返回新 index

这一机制确保了：无论创建多少个 2D Demo，`Renderer_2D.asset` 只会被创建一次、注册一次。

## 3D 与 2D 的关键差异

| 维度 | 3D（Universal Renderer） | 2D（2D Renderer） |
|------|--------------------------|-------------------|
| **Camera** | 透视投影（Perspective） | 正交投影（Orthographic） |
| **光照** | 基于物理渲染（PBR），Directional / Point / Spot Light | 专用 2D 光照公式，Light2D（Global / Freeform / Point） |
| **阴影** | Shadow Mapping（基于深度贴图） | Shadow Caster 2D（基于多边形） |
| **排序** | Z-Buffer 深度排序 | Sorting Layer + Order in Layer（画家算法） |
| **材质** | `URP/Lit`、`URP/Simple Lit` | `URP/2D/Sprite-Lit-Default`、`Sprite-Unlit-Default` |
| **专属特效** | SSAO、景深、运动模糊 | Pixel Perfect Camera、2D 骨骼动画集成 |

> **材质不互通**：3D Lit 材质赋给 Sprite 无法接收 2D 光源，反之亦然。在 2D Demo 中使用 Sprite-Lit 系列材质，在 3D Demo 中使用 Lit 系列材质。

## Renderer Feature 与第三方渲染插件

**Renderer Feature** 是 URP 的扩展机制——可以挂载到 Renderer 上的模块，用于在渲染流程中注入自定义的渲染步骤（Render Pass）。例如内置的 SSAO（屏幕空间环境光遮蔽）就是一个 Renderer Feature。

第三方渲染插件（如 [Shapes](docs/shapes.md)）如果需要参与 URP 的渲染流程，通常会提供自己的 Renderer Feature。**必须手动将其添加到 Renderer 上，插件才能正常工作**。操作方式：选中 Renderer 资产（如 `PC_Renderer.asset`）→ Inspector → Add Renderer Feature → 选择插件提供的 Feature。

需要添加到哪些 Renderer 取决于插件的使用范围：

| Renderer | 说明 |
|----------|------|
| `PC_Renderer.asset` | 所有 PC 端 3D Demo 使用的渲染器 |
| `Mobile_Renderer.asset` | 所有移动端 3D Demo 使用的渲染器 |
| `Renderer_2D.asset`（如有） | 如果 2D Demo 也需要该插件，则也要添加 |

> **注意**：部分插件的导入脚本声称会自动添加 Renderer Feature，但自动配置可能因版本更新而失效。如果插件导入后渲染异常（形状不显示、资源泄漏等），首先检查对应的 Renderer Feature 是否已添加。

## 学习资源

**官方文档：**

- [URP 概述（Unity 6 手册）](https://docs.unity3d.com/6000.4/Documentation/Manual/universal-render-pipeline.html) — URP 完整指南
- [URP 入门（Unity 6）](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/urp-introduction.html) — 概念介绍与快速上手
- [Renderer Feature 入门](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/scriptable-renderer-features/intro-to-scriptable-renderer-features.html) — 什么是 Renderer Feature，如何使用
- [Renderer Feature API 参考](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/renderer-features/scriptable-renderer-features/scriptable-renderer-feature-reference.html) — ScriptableRendererFeature / ScriptableRenderPass 接口文档
- [自定义 Renderer Feature 完整示例](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/create-custom-renderer-feature.html) — 从零实现一个模糊效果的 Renderer Feature
- [Unity 渲染管线策略 2026](https://unity.com/topics/render-pipelines-strategy-for-2026) — Unity 对 URP/HDRP 的长期规划
- [Introduction to URP for advanced creators（Unity 6 edition）](https://unity.com/resources/introduction-to-urp-advanced-creators-unity-6) — Unity 官方进阶电子书

**社区教程（中文）：**

- [Unity URP 通用渲染管线基础教程（B站）](https://www.bilibili.com/video/BV19i4y1P74Y/) — 4 集视频覆盖 URP 基础、后处理、光照
- [Unity SRP 入门教程——渲染管线基础（B站）](https://www.bilibili.com/video/BV1uu411R7WD/) — 从 SRP 底层理解渲染管线
- [一文看懂 Unity 通用渲染管线 URP（掘金）](https://juejin.cn/post/7187978828978290744) — 图文并茂的 URP 架构解析
- [URP/SRP 渲染管线浅入深出（知乎）](https://zhuanlan.zhihu.com/p/353687806) — 从原理到实践的深度文章
