# 第一阶段原型验收记录

目标来源：`XiuXianShop_First_Playable_OneShot.md`。本记录区分自动验证和人工试玩。

## 已执行证据

- Unity 6000.6.0f1，现有 URP Renderer2D 基线保持不变。
- 最终代码修改后的 Edit Mode 测试：16 项全部通过，0 失败，耗时约 0.75 秒。
- 最终代码修改后的 Play Mode 测试：4 项全部通过，0 失败，耗时约 6.26 秒。
- Play Mode 测试通过实际 Input System 虚拟鼠标和键盘、UGUI Raycast 及按钮事件执行，不仅是直接调用经营数据方法。
- 实际保存场景被反复加载，能自动取得配置并开始第 1 天；界面与供货画面已截图查看。截图在本地 `Temp/FirstPlayable/01-preparation.png` 和 `02-supplier.png`，不提交临时目录。
- 人工试玩：未执行，开发者需要按 FIRST_PLAYABLE_GUIDE.md 体验操作手感。

## 六项主目标审计

| 目标 | 具体证据 | 结论 |
| --- | --- | --- |
| 有形物品拖动、旋转、翻转 | `RotationAndReflectionHaveExpectedOccupiedCells`、`FlippedLCanEnterPocketThatOriginalCannot`、`LongItemMustRotateAtBottomEdge`；Play Mode `RealMouseAndKeyboardHandleShapesPreviewsAndRollback`、`FlipButtonReflectsHorizontallyAfterRotation` | 自动验证通过；水平翻转在旋转后仍按屏幕水平方向执行 |
| 三个容器之间真实流转 | 两天 UI 测试通过鼠标在背包→展示→柜台流转；收购物品从柜台进入背包；`CounterRejectsUnrelatedGoodsAndCustomerTheft` | 自动验证通过；三个区域共享同一占格逻辑 |
| 展示影响当天交易 | `DisplayActuallyDeterminesTradeDirection` 对比只展示丹药、只展示收购牌；UI 测试组合展示 | 自动验证通过；招牌产生四次原料供货，普通商品逐件产生买家 |
| 买卖真实结算 | 不足钱、满空间、拒绝、提前闭店、未摆上柜台、重复结算测试；UI 两天完整链路 | 自动验证通过；错误路径保留库存和灵石 |
| 炼丹增值与空间库存 | `CraftFailsAtomicallyInFragmentedFullStorage`、`CraftCanUseSpaceFreedByItsOwnIngredients`、`TwoProcureCraftSellLoopsAndTwoDaysAreProfitable` | 自动验证通过；两组买入原料成本 14，制成并售出两盒丹药收入 36，余额 120→142 |
| 睡觉推进与再次经营 | 数据层两轮闭环；UI `ActualButtonsAndDragsRunTwoDaysWithSupplyCraftAndSales`；14 天房租测试 | 自动验证通过；UI 路径到第 3 天、余额 178、4 次买入、4 次卖出、3 次炼丹 |

## 其他要求审计

- 形状：单格灵露、2×2 丹盒/玉匣、四格长剑、L 形草、T 形朱砂、异向 L 收购牌；都有可见真实占格。
- 合法/非法预览：实际拖放测试检查红色失败条件、变形后成功条件、越界回退、Esc 取消和可见按钮。
- 防损坏：2,000 次固定随机种子的重排，对每次失败比对完整状态，逐步检查唯一 ID、边界、重叠、归属与非负资金。
- 交易方向、价格、玩家/顾客归属：UI 卡片、己/客标签、顾客蓝色边框；供货截图已查看。
- 成交后离开、下一位、拒绝与提前闭店：按钮流程与数据测试覆盖；未成交展示商品归回原位和朝向。
- 开局资源、设备、供应、利润：120 灵石、开局一组原料和一盒丹、免费已有丹炉、确定供应；无需购买设备或依靠随机概率。
- 周期支出：首租 20、七日结算、约 5% 上涨、欠租显示；14 天测试验证不足时不产生负资金。
- 日期/资源可见、操作提示：实际屏幕已查看；全部容器角点在 Game 视口内的自动测试通过。
- 无范围外玩法、无新 Package、未重做 2D 转换、未改输入与 Build 设置：交付前通过文件差异检查。
- 入口与文档：ShopPrototype 场景、打开菜单、试玩指南、实现状态、边界和下一步已记录。

## 调试中修复的问题

1. 首次创建原生配置资产后，第一次进入 Play 未取到配置。同步导入并重新载入后修复；Builder 增加同步导入，后续保存场景启动测试均通过。
2. 拖动阈值触发时鼠标已经移动，曾错误地使用当前位置计算抓取格。改为 `pressPosition` 后，真实鼠标拖放测试通过。
3. 旋转 90 度后翻转曾沿物品原始横轴发生。改为屏幕横轴反射，并新增真实 Flip 按钮测试。

未将之前的失败结果冒充成功；上述通过数来自修复后的明确 editor/playmode 分开运行。MCP 的一次 mode=all 调用返回旧结果，因此不作为最终证据。

## 当前交付检查

- 2026-09-07 收尾时通过 MCP 退出 Play，Editor 为 ready，compiling=false，domainReloadInProgress=false。
- 当前打开 Assets/Scenes/ShopPrototype.unity，isDirty=false；根对象为 Main Camera 和 Shop Prototype，配置引用为 ShopCatalog，包含 7 种商品；相机实际 Renderer2D。
- Console 最后读取：1 条 Exception、0 条 Warning。异常为 `UnityConnectWebRequestException: Token Exchange failed due a failure with the web request.`，时间 2026-09-07 03:26:50 UTC，堆栈全部来自 UnityEditor.Connect 登录令牌交换。没有原型脚本的 Error/Exception。没有清空日志，没有改变账号或云服务配置；若该连接提示持续出现，由开发者检查 Unity 登录和网络。
- 文件差异检查：Packages、ProjectSettings、原 Assets/Settings 和 SampleScene 未改变；新增源码和程序集配置的 .meta 完整；git diff --check 通过。
- 没有产生本地 Git 提交，没有推送。实现、资产、测试和文档保留在当前工作区，目标书本来就是未跟踪文件。
- 补充售出截图曾因自动审批返回账户用量限制而未执行，没有绕过拒绝。该截图不是主目标的必需证据；后续只读状态检查和退出 Play 已正常完成，不存在未完成的必需审批动作。
