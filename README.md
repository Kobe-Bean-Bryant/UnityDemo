# UnityDemo

[![Unity Version](https://img.shields.io/badge/Unity-6000.3.17f1%2B-blue)](https://unity.com/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](LICENSE)

用于实验各种奇思妙想和试用各种工具的 Unity 沙盒项目。

## 项目结构

采用**一个文件夹即一个完整 Demo** 的组织方式，所有 Demo 位于 `Assets/Demos` 下，彼此完全独立。

```
Assets/
├── Scripts/
│   ├── UnityDemo.Shared.asmdef      # 公共代码程序集（命名空间 UnityDemo.Shared，所有 Demo 可引用）
│   ├── Utilities/                   # 公共工具类（Singleton、SimplePool 等）
│   └── Editor/
│       ├── UnityDemo.Editor.asmdef  # Editor 工具程序集（命名空间 UnityDemo.Editor）
│       ├── DemoCreator/             # Demo 脚手架工具（UI Toolkit）
│       ├── SceneCreator/            # 独立场景创建工具
│       ├── AsmdefDoctor/            # 程序集定义诊断 & 修复工具
│       └── Shared/                  # 编辑器工具共享逻辑
│           └── SceneSetupHelper.cs  # 2D/3D 场景创建 & URP Renderer 管理
├── Settings/
│   ├── PC_RPAsset.asset             # PC 平台 URP 配置
│   ├── Mobile_RPAsset.asset         # 移动平台 URP 配置
│   ├── PC_Renderer.asset            # Universal Renderer（3D）
│   ├── Mobile_Renderer.asset        # Universal Renderer（3D，移动端优化）
│   └── Renderer_2D.asset            # 2D Renderer（首次创建 2D Demo 时自动生成）
├── Demos/
│   └── PuzzleDemo/                  # ← 由工具自动生成
│       ├── Scripts/
│       │   └── PuzzleDemo.asmdef
│       ├── Scenes/
│       │   └── PuzzleDemo.unity
│       ├── Prefabs/
│       ├── Sprites/
│       ├── Materials/
│       └── Resources/               # 可选，仅在需要动态加载时启用
│           └── PuzzleDemo/
├── Plugins/                         # Odin Inspector, DOTween 等
└── ThirdParty/                      # Feel, Shapes 等
```

## Demo 开发工作流

### 首次使用：环境准备

1. **修复第三方插件的程序集定义** — 打开 `Tools → DemoTools → ASMDEF Doctor`，点击 Scan，为未覆盖的插件生成 ASMDEF（详见[ASMDEF Doctor](#asmdef-doctor)章节）
2. 如果编译报错，在 Inspector 中为生成的 ASMDEF 添加缺失的引用

### 创建 Demo

1. 打开 `Tools → DemoTools → Demo Creator`
2. 输入 Demo 名称（如 `Puzzle`，自动补全为 `PuzzleDemo`）
3. 选择渲染模式：**3D**（透视摄像机 + 方向光）或 **2D**（正交摄像机 + Global Light 2D）
4. 按需勾选资源文件夹（Prefabs、Sprites、Materials 等）和 ASMDEF 引用
5. 点击 **Create Demo** — 自动生成目录结构、ASMDEF、默认场景并打开

### 开发

- **写代码**：在 `Demos/{DemoName}/Scripts/` 下编写 C# 脚本，使用与文件夹同名的命名空间（如 `namespace PuzzleDemo`）
- **引用插件**：Demo 的 ASMDEF 在创建时已选好引用；如需追加，在 Inspector 中编辑 `.asmdef` 文件的 References
- **资源**：Prefab、Sprite、Material 等放在 Demo 自己的文件夹中，通过 Inspector 直接引用

### 追加场景

一个 Demo 可能包含多个场景（如主菜单、关卡 1、关卡 2）。打开 `Tools → DemoTools → Scene Creator`，选择目标 Demo、输入场景名、选择 2D/3D 即可创建。

### 运行与测试

在 Project 窗口双击 Demo 的 `.unity` 场景文件打开，然后按 Play 即可运行。每个 Demo 的场景自带完整的渲染配置（Camera、灯光），无需额外设置。

## 规范

- **命名空间**：每个 Demo 的 C# 脚本必须使用与文件夹同名的命名空间（如 `PuzzleDemo`）
- **ASMDEF 隔离**：每个 Demo 拥有独立的 ASMDEF（`autoReferenced: false`），修改一个 Demo 不会触发其他 Demo 重编译
- **公共代码**：放在 `Assets/Scripts/` 下，属于 `UnityDemo.Shared` 程序集，Demo 按需引用
- **资源管理**：优先使用普通文件夹 + Inspector 直接引用；仅在需要 `Resources.Load()` 动态加载时启用 Resources 文件夹

## 渲染管线与渲染模式

项目基于 **URP**（Universal Render Pipeline），同时支持 **3D** 和 **2D** 两种渲染模式。

- **Pipeline Asset → Renderer → Camera** 三层架构：Pipeline Asset 定义全局渲染参数，Renderer 决定怎么画（3D / 2D），Camera 选择使用哪个 Renderer
- **3D Demo**：透视摄像机 + Directional Light，使用 Universal Renderer（index 0）
- **2D Demo**：正交摄像机 + Global Light 2D，使用 2D Renderer（首次创建时自动生成并注册）
- 打开不同 Demo 的场景即自动切换渲染模式，无需额外操作

### Settings/ 配置文件

| 文件 | 职责 |
|------|------|
| `PC_RPAsset` / `Mobile_RPAsset` | PC / 移动平台的 Pipeline Asset（全局渲染参数） |
| `PC_Renderer` / `Mobile_Renderer` | 3D 渲染器，挂载 Renderer Feature（SSAO、Shapes 等） |
| `Renderer_2D` | 2D 渲染器（按需自动生成） |
| `DefaultVolumeProfile` / `SampleSceneProfile` | 后处理配置（Bloom、色调映射等） |

### 第三方渲染插件

需要参与渲染流程的插件（如 Shapes）必须将其 **Renderer Feature** 手动添加到对应的 Renderer 资产上，否则插件的渲染和资源回收链条不完整。

> **详细文档：**
> - [渲染管线详细文档](docs/rendering.md) — URP 基础概念、三层架构、Settings 文件详解、2D/3D 切换实现原理、Renderer Feature 机制
> - [Shapes 集成指南](docs/shapes.md) — Shapes 在 URP 中的配置、常见画质问题（锯齿 / 模糊）、2D 兼容性

## 程序集隔离与第三方插件

每个 Demo 拥有独立的 ASMDEF 程序集（`autoReferenced: false`），修改一个 Demo 不会触发其他 Demo 重编译。

第三方插件如果没有自带 ASMDEF，其代码会编译到默认程序集，Demo 将无法引用。使用 **ASMDEF Doctor**（`Tools → DemoTools → ASMDEF Doctor`）可自动检测并生成缺失的 ASMDEF。

> **详细文档：** [程序集隔离详细文档](docs/assembly-isolation.md) — ASMDEF 隔离原理、插件覆盖方式、ASMDEF Doctor 自动化能力、Demo Creator 引用选择机制

## 项目工具

项目中的 Editor 工具均基于 [UI Toolkit](https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html) 开发，源码位于 `Assets/Scripts/Editor/`。

| 工具 | 菜单入口 | 用途 |
|------|---------|------|
| **Demo Creator** | Tools → DemoTools → Demo Creator | 创建新 Demo（目录结构 + ASMDEF + 场景） |
| **Scene Creator** | Tools → DemoTools → Scene Creator | 为已有 Demo 追加新场景 |
| **ASMDEF Doctor** | Tools → DemoTools → ASMDEF Doctor | 诊断并修复插件的程序集定义缺失 |

### UI Toolkit 简介

UI Toolkit 是 Unity 推荐的 UI 框架，架构类似 Web 前端：**UXML**（布局，≈ HTML）+ **USS**（样式，≈ CSS）+ **C#**（逻辑，≈ JS）。本项目的 `DemoCreator/` 源码可作为入门参考。

**学习资源：**

- [UI Toolkit 官方手册](https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html) — 完整指南
- [USS 简介](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-about-uss.html) — 样式系统与 CSS 的异同
- [USS 选择器](https://docs.unity3d.com/Manual/UIE-USS-Selectors.html) — `.class`、`#name`、`:hover` 等选择器用法
- [USS 最佳实践](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html) — 编写高效样式的建议
- [UI Builder 官方教程](https://docs.unity3d.com/6000.3/Documentation/Manual/UIBuilder.html) — 可视化拖拽编辑 UXML/USS
