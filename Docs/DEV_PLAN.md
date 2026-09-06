# XiuXianShop 开发计划

## 工作方式

一次完成一个可说明、可验证的小步骤。先明确当前需求和验收结果，再实现必要代码；不提前搭建未来系统。以下后续步骤是建议顺序，不是本次任务的执行清单或自动授权。

## 当前任务：项目基础整理

- [x] 只读检查 Unity 项目、Git、Packages 和 Project Settings。
- [x] 建立 AGENTS.md 与三份 Docs 文档。
- [x] 建立 Assets 一级分类目录，保留模板资产和现有 Scenes。
- [x] 完成后复查 Console、Editor 状态和文件变更范围（结果记录于 DEVLOG）。

本次不创建游戏脚本、GameObject、预制体、数据资产或新场景；不安装 Package，不改变 Render Pipeline 和 Project Settings。

## 最小可玩原型的建议顺序

| 步骤 | 范围 | 预期验收 |
| --- | --- | --- |
| 1. 固定视角占位场景 | 明确 2D 视角、最少显示元素和输入方式，按任务要求搭建 | Play Mode 能看清店铺布局与必要反馈 |
| 2. 最小商品与交易 | 确定少量商品、持有数量和资金规则，再实现一次买卖 | 数量和资金正确变化，资源不足时有明确反馈 |
| 3. 顾客与营业流程 | 用简单顾客交互承载交易，加入必要营业状态 | 能完成一轮营业，不出现重复结算 |
| 4. 一条加工链 | 确定投入、产出和加工时机 | 投入被扣除、产出可用于经营，资源不足不执行 |
| 5. 配置与跨日闭环 | 连接营业前配置、结束营业、睡觉推进日期 | 连续完成两天，跨日状态符合确定的规则 |
| 6. 原型整体验证 | 修正影响闭环的问题，补充必要反馈 | 按明确步骤跑通完整经营循环 |

每个步骤的细节在对应任务开始时确定。现有输入、测试等 Package 仅表示可用工具，不要求为了使用它们增加设计复杂度。

## 验证约定

- 自动/工具检查：Console 的 Error、Warning，Editor ready 和编译状态，变更文件范围；有实际逻辑用例时再添加适当测试。
- 手动 Play Mode：操作流程、显示效果、按钮反馈、交易与加工结果、跨日行为。任务交付必须给出该任务所需步骤及预期结果。
- 当前已完成文档、目录与 URP 2D 空场景基线，无玩法可测试；不把 Editor ready 当作玩法完成的证据。

## 工程现状与后续注意

- Unity 为 6000.6.0f1，项目级 Default Behavior Mode 已设为 2D。
- Graphics、Mobile 和 PC 质量档统一引用 Assets/Settings/XiuXianShop_2D_RPAsset.asset，唯一 Renderer 为 XiuXianShop_2D_Renderer.asset（Renderer2DData）。旧 3D 管线资产保留原位，已脱离 Graphics 和 Quality 引用。
- 默认 Sprite 材质为 URP Sprite-Unlit-Default，空场景无需 Light2D。以后若明确需要受光 Sprite，再单独设计材质与 2D 光照。
- URP 在 manifest 中为 17.7.0，锁文件及 Editor 实际版本为内置 17.6.0；该既有差异未调整。已通过 Package Manager 添加官方 2D Sprite 1.0.0，未添加其他 2D 包。
- 各次任务开始时的 Git 状态分别记录于 DEVLOG，不把历史状态当作当前状态。
