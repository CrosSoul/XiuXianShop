# XiuXianShop 开发日志

按任务记录日期、范围、实际改动、验证和未完成内容。不将计划功能写成已实现功能。

## 2026-09-07 — Universal 3D 模板转 URP 2D 基线

### 检查与范围

- 本次开始时 Git 工作区干净。Unity 6000.6.0f1，实际 URP 17.6.0；manifest 声明的 URP 17.7.0 保持不变。
- Graphics 默认管线为空；Mobile、PC 分别引用原有 Mobile_RPAsset、PC_RPAsset，均使用 UniversalRendererData。
- 原 SampleScene 有透视 Main Camera、Directional Light 和 Global Volume，已保存；Console 起始 Error/Warning 均为 0。
- 仅转换 2D 工程基线，不实现玩法，不改变目录、输入系统或 Build 设置。

### 实际修改

- 经 Unity MCP 调用当前 URP 包官方 Renderer2D 创建逻辑和 UniversalRenderPipelineAsset.Create，新增 Assets/Settings/XiuXianShop_2D_Renderer.asset 与 XiuXianShop_2D_RPAsset.asset，.meta 由 Unity 生成。
- Renderer 列表只有一个 Renderer2DData，默认索引 0；默认 Sprite 材质选择 Unlit，实际为官方 Sprite-Unlit-Default，避免空场景需要额外光源。
- 通过 Unity API / SerializedObject 修改 GraphicsSettings.defaultRenderPipeline 及 Mobile、PC 的 Quality 管线引用，全部指向新 2D RPAsset。旧 PC/Mobile RPAsset、Renderer 和 Volume Profile 资产保留原位，未修改。
- Main Camera 设为 Orthographic，Size=5，位置 (0,0,-10)，旋转 (0,0,0)，裁剪范围 0.1–100，Solid Color 背景，关闭后处理，Renderer 设为跟随管线默认值。
- 删除场景中的 Directional Light 和 Global Volume。当前 Unlit Sprite 基线及关闭的后处理不依赖它们；未创建替代灯光或游戏对象。
- 使用项目级 EditorSettings.defaultBehaviorMode=Mode2D，并保存到 ProjectSettings/EditorSettings.asset。
- 通过 Package Manager 添加缺失的官方 com.unity.2d.sprite 1.0.0（BuiltIn），用于普通 Sprite 编辑与切片；没有新增传递依赖。未安装 2D Animation、Tilemap、Pixel Perfect 或第三方包。
- 更新现有设计和计划文档中的工程状态；未新建 AGENTS.md、Docs 目录或文档。
- 所有 Unity 序列化资产与设置修改均经 Unity API 完成，未手工编辑 YAML。Unity 保存时也迁移了 GraphicsSettings 和 SampleScene 的部分序列化字段；manifest 的排序和结尾换行由 Package Manager 更新。

### 验证

- MCP 确认实际 RenderPipelineManager.currentPipeline 为 UniversalRenderPipeline，相机实际 scriptableRenderer 为 Renderer2D。
- Graphics 与全部两档 Quality 引用同一新 URP 资产；其 Renderer 列表只有新 Renderer2DData，没有旧 3D Renderer 回退项。
- 相机正交参数、默认 Unlit 材质和 Shader 支持状态均通过 API 核对。
- SampleScene 已通过 MCP 保存，isDirty=false，Hierarchy 仅 Main Camera，保留 Camera、AudioListener、UniversalAdditionalCameraData。
- MCP 捕获日志与 Unity Console 原生计数均为 Error 0、Warning 0；未清空 Console。
- 一次 MCP 临时 C# 调用因 EditorSceneManager 命名空间拼写被拒绝，代码未执行；修正后调用成功。此工具编译诊断未进入 Unity Console，也未生成项目脚本。
- 最终 Editor 为 ready，未编译、未进行 Domain Reload、Play Mode stopped。文件哈希核对仅有预期的 9 个现有文件修改与 4 个新资产/元数据文件；输入、Build 设置、旧管线资产和原目录均未改变。不将临时 C# 调用成功视为场景验证。

### 手动操作与验证边界

无需开发者手动配置 Editor。未创建正式 Sprite、UI 或玩法，也未运行 Play Mode / Player 构建；真实美术、UI 显示及最终设备渲染效果需在对应内容任务中验证。当前任务验证的是管线、配置与空场景状态。

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
