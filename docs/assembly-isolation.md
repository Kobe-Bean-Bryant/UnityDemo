# 程序集隔离与第三方插件

## Demo 的 ASMDEF 隔离

每个 Demo 拥有独立的 ASMDEF 程序集（`autoReferenced: false`）。Unity 的引用规则是**单向的**：

```
ASMDEF 程序集 → 可以引用其他 ASMDEF 程序集 ✅
ASMDEF 程序集 → 不能引用 Assembly-CSharp   ❌
```

如果第三方插件没有自带 ASMDEF 或 .asmref，其代码会编译到 Assembly-CSharp（默认程序集），Demo 将无法 `using` 该插件的类。

## 插件的程序集覆盖方式

不同插件采用不同方式提供程序集定义：

| 方式 | 示例 | Demo 如何引用 |
|------|------|-------------|
| 自带 ASMDEF | PrimeTween、Odin Inspector、Task Canvas | 在 Demo Creator 中勾选对应引用 |
| 通过 .asmref 路由到统一程序集 | Feel（全部代码路由到 `MoreMountains.Tools`） | 引用 `MoreMountains.Tools` 即可使用完整功能 |
| 无程序集定义 | DOTween、Easy Save 3 | 需先用 ASMDEF Doctor 生成 ASMDEF |

## ASMDEF Doctor

**Tools → DemoTools → ASMDEF Doctor** — 自动扫描 Assets/ 下所有 .cs 文件，找出未被 .asmdef 或 .asmref 覆盖的插件文件夹，一键生成 ASMDEF。

**使用方式：**

1. 点击 **Scan** — 扫描并列出未覆盖的文件夹及脚本数量
2. 勾选后点击 **Fix Selected** — 自动生成 Runtime ASMDEF 和 Editor ASMDEF

**自动化能力：**

- **依赖检测**：扫描脚本中的 `using` 声明，自动匹配需要引用的程序集（如检测到 `using TMPro;` 会自动添加 `Unity.TextMeshPro` 引用），内置 15 个常用 Unity 包的命名空间映射，同时动态扫描项目中已有 ASMDEF 的 `rootNamespace`
- **DLL 冲突避免**：如果插件文件夹中存在与 ASMDEF 同名的预编译 DLL（如 `ConsolePro.Editor.dll`），自动追加 `.Scripts` 后缀避免冲突
- **托管恢复**：生成的 ASMDEF 记录在 `managed_asmdefs.json` 中，插件更新后若 ASMDEF 被删除，再次扫描即可检测并一键恢复

## Demo Creator 中的 ASMDEF 引用选择

创建 Demo 时，Demo Creator 会自动发现项目中所有可用的 ASMDEF 并分三类展示：

| 分类 | 来源 | 默认展开 |
|------|------|---------|
| **项目 (Assets/)** | 用户和插件的 ASMDEF | 展开 |
| **常用 Unity 包** | Unity 官方高频包（TextMeshPro、InputSystem、Cinemachine 等共 13 个） | 展开 |
| **其他 Unity 包** | 其余 Unity 包 | 折叠 |

**自动过滤规则**（以下 ASMDEF 不会出现在选择列表中）：

- Editor-only 程序集（`includePlatforms` 非空）
- 名称包含 `.Tests`、`.Sample`、`.CodeGen`、`.Debug` 等后缀的程序集
- 项目自身的 `UnityDemo.Editor` 程序集

**默认预选**：`UnityDemo.Shared` 始终默认勾选，确保 Demo 可以访问公共代码。

**序列化格式**：选中的引用以 `GUID:{guid}` 格式写入 ASMDEF 文件，而非程序集名称。GUID 引用更稳定——即使 ASMDEF 被重命名，引用关系不会断裂。
