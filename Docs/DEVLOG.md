# XiuXianShop 开发日志

按任务记录日期、范围、实际改动、验证和未完成内容。不将计划功能写成已实现功能。

## 2026-09-07 — 第一次正式开发：基础整理

### 范围

只读检查工程现状，建立开发规则、文档和资源目录。不实现任何玩法，不改动现有模板资产、Packages、Render Pipeline 或 Project Settings。

### 开始时的检查

- Unity MCP 连接到 D:/Unity/UnityProjects/XiuXianShop，版本 6000.6.0f1。
- Editor ready，Play Mode stopped，未编译，未进行 Domain Reload；MCP 捕获日志为 0。
- 唯一打开的场景为 Assets/Scenes/SampleScene.unity，已加载、活动、无未保存修改。三个根对象为 Main Camera、Directional Light、Global Volume。
- Build Target 为 StandaloneWindows64，构建场景列表只启用 SampleScene。
- 产品名 XiuXianShop，公司名 DefaultCompany，版本 0.1.0；使用 Linear 色彩空间、新 Input System、Force Text 序列化。默认编辑行为仍为 Mode3D。
- Graphics 默认 Render Pipeline 引用为空，但 Quality 的 PC 档和实际当前管线使用 PC_RPAsset；Mobile 档保留 Mobile_RPAsset。因此不能仅凭默认引用为空判断项目没有使用 URP。

### Packages 快照

| Package | manifest 声明版本 | lock 版本 |
| --- | --- | --- |
| com.unity.ai.navigation | 2.0.14 | 2.0.14 |
| com.unity.collab-proxy | 2.13.6 | 2.13.6 |
| com.unity.ide.rider | 3.0.40 | 3.0.40 |
| com.unity.ide.visualstudio | 2.0.27 | 2.0.27 |
| com.unity.inputsystem | 1.20.0 | 1.20.0 |
| com.unity.render-pipelines.universal | 17.7.0 | 17.6.0 |
| com.unity.test-framework | 1.8.0 | 1.8.0 |
| com.unity.timeline | 6.6.0 | 6.6.0 |
| com.unity.ugui | 2.6.0 | 2.6.0 |
| com.unity.pipeline | 0.6.0-exp.1 | 0.6.0-exp.1 |

Editor API 确认实际注册的 URP 为 BuiltIn 17.6.0，与锁文件一致。本次仅记录差异，未改包或执行依赖解析。

### 已有 Git 状态（本次修改之前）

分支 main，最近提交 ef39e99（初次签入）。工作区已有以下六项变更，来源不作推断，本次保留：

```text
D Assets/Editor.meta
D Assets/Editor/HubForceResolve.cs
D Assets/Editor/HubForceResolve.cs.meta
M Packages/packages-lock.json
M ProjectSettings/ProjectSettings.asset
M ProjectSettings/QualitySettings.asset
```

只读 Git 检查遇到所有权检查和 LFS clean 临时目录权限限制，使用当前命令的 safe.directory 参数并临时禁用 LFS clean/process 完成状态检查，关闭可选锁；未修改仓库或全局 Git 配置。上述为该方式读取的状态，不是一次 LFS 完整性验证。

### 本次新增

- 根目录 AGENTS.md：项目范围、简单实现、Unity 资产安全、目录与验证规则。
- Docs/GAME_DESIGN.md：核心体验、原型目标、范围和待定设计。
- Docs/DEV_PLAN.md：本次清单、后续建议顺序和验收约定。
- Docs/DEVLOG.md：本次检查快照与结果。
- Assets/Art、Audio、Data、Prefabs、Scripts、UI、Tests：一级分类目录，目录 .meta 由 Unity MCP 创建时生成；每个新目录有 .gitkeep 以保留 Git 中的空目录。
- Assets/Scenes 已存在，继续沿用。Settings、TutorialInfo、Readme.asset 和 InputSystem_Actions.inputactions 保留原位。

### 完成验证

- Unity MCP 复查：捕获的 Error 0 条、Warning 0 条；未清空 Console。
- Editor 为 ready，compiling=false，domainReloadInProgress=false，playMode=stopped。
- SampleScene 仍是唯一活动场景，isDirty=false，根对象数仍为 3。
- 对开始时 Assets、Packages、ProjectSettings 中的 70 个现有文件进行 SHA-256 对比，全部保持不变。
- Git 中本次仅新增 4 份 Markdown 文档、7 个 Unity 自动生成的目录 .meta 和 7 个 .gitkeep，共 18 个文件。开始时的六项变更保持原状；未暂存或提交。
- 本次无代码变更，未主动触发编译、构建或运行测试。以上是工程状态检查，不代表玩法已验证。

### 玩法与手动操作

未实现 Inventory、Customer、Trading 或其他玩法；未执行 Play Mode 玩法测试。本次无需开发者手动配置 Unity Editor，后续功能任务再提供具体测试步骤。
