# XiuXianShop 开发规则

适用于本仓库所有任务。先阅读本文件及与任务相关的 Docs 文档；用户当前明确指令优先。

## 项目与范围

- 这是一个单人开发的 2D 修仙店铺经营游戏，目前只服务于一个小型单机 Demo。
- 核心方向：营业前配置店铺 → 营业中与顾客买卖 → 通过产业链加工商品 → 结束营业后睡觉推进日期。
- 第一阶段只做最小可玩原型，使用占位图形，不制作正式美术。
- 不得擅自扩大 Scope。计划中的后续步骤不代表已获准实施；每次任务尽量只修改当前功能涉及的内容。
- 开发者熟悉策划、关卡设计和 UE5 Blueprint，C# / Unity 经验较少。实现与说明应简单、可读、容易在 Inspector 和 Console 中调试。

## 实现原则

- 优先简单、可读、数据驱动的实现。不为了未来可能的需求提前设计复杂架构。
- 不引入目前没有明确用途的 Manager、Service、Interface、Event Bus 等抽象层；不预建单例框架、依赖注入框架或通用系统。
- 优先用职责明确的小型 MonoBehaviour、直接方法调用和显式引用解决当前问题。确有复用需求时再提取代码。
- 重要数据以后优先考虑 ScriptableObject，但按实际需求使用；可复用的静态配置与运行时可变状态分开，避免把商品配置资产当作运行时库存或存档。
- 需要策划调整的参数通过有意义的序列化字段暴露；命名表达用途，必要时加 Tooltip。避免魔法数字和隐式全局依赖。
- 类名与 C# 文件名一致，类型和公共方法用 PascalCase，私有字段和局部变量用 camelCase。注释优先解释原因和 Inspector 配置方式。
- 不为了目录整齐预建空脚本、空组件或 asmdef；测试和程序集边界在实际需要时建立。

## Unity 资产与工程安全

- 不直接手工编辑 .unity / .prefab / .meta YAML，优先通过 Unity API、Editor Script 或 MCP 操作。目录的 .meta 也由 Unity 生成。
- 不删除、移动或重建模板现有资产，除非当前任务确有必要；需要移动资产时使用 Unity 资产操作，保留 GUID 和引用。
- 未被当前任务要求时，不添加第三方 Package，不升级或删除已有 Package，不修改 Render Pipeline 或 Project Settings。
- 修改前检查 Git 状态，区分已有变更与本次变更，不覆盖、回退或顺手修复用户已有改动。
- 不提交 Library、Temp、Logs、Obj 或 UserSettings 等生成目录。资产及其 .meta 应一起纳入版本控制。
- 空分类目录使用 .gitkeep 保留在 Git 中；不手写其 .meta。目录有实际内容后可在相关任务中移除占位文件。

## 目录约定

| 位置 | 用途 |
| --- | --- |
| Assets/Art | 游戏场景、角色、物品等美术和占位图形 |
| Assets/Audio | 音效与音乐 |
| Assets/Data | 按需创建的静态配置资产，如 ScriptableObject |
| Assets/Prefabs | 游戏对象预制体；UI 专用资源统一放 UI |
| Assets/Scenes | 场景；现有 SampleScene 保留 |
| Assets/Scripts | 游戏 C# 代码，包含 UI 行为代码；有需要再按功能分子目录 |
| Assets/UI | UI 专用预制体、字体、图标与布局资源 |
| Assets/Tests | 有实际用例时再建立 EditMode / PlayMode 测试及必要配置 |
| Assets/Settings、Assets/TutorialInfo | 保留的模板目录，不顺手整理 |
| Docs | 设计、开发计划和开发日志，不作为运行时资产导入 |

## 验证与交付

- 修改后必须检查 Console，优先通过 Unity MCP 读取 Error 和 Warning，不清空日志来掩盖问题。
- 检查 Editor 是否 ready、是否编译或进行 Domain Reload。发现错误时区分本次新增与已有问题。
- 编译通过不等于玩法验证通过。必须明确区分自动验证、已实际执行的 Play Mode 测试、需要开发者手动 Play Mode 测试的内容。
- 对玩法改动提供具体手动步骤和预期结果；没有运行的测试如实注明。文档和目录整理不需要虚构玩法测试或编写形式化空测试。
- 交付时说明改动文件、用途、验证结果、遗留问题及是否需要手动 Editor 操作。
- 完成任务后在 Docs/DEVLOG.md 记录实际结果；设计或计划有变化时同步对应文档，保持“计划”与“已实现”分离。
