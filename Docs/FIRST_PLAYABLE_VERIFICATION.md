# 第一阶段原型验收记录

目标来源：`XiuXianShop_First_Playable_OneShot.md`。本记录区分自动验证和人工试玩。

## 最新验收：每日五位随机顾客与离散资金档位（2026-09-07）

本节是当前有效结果。本节之后的“上一版验证记录”保留历史证据；其中旧客流、固定资金数组、固定 178 灵石结局和 Console 状态均不能作为当前规格。

- 设计依据：只读读取 [FigJam 展示柜吸引判定](https://www.figma.com/board/8m7hAGOqyp7WUF7oEXZ007/?node-id=28-1049)，用户进一步明确每日固定五位、逐位随机买卖方向、预算用离散档位且约 ±10% 浮动。
- 最终 Edit Mode **39/39**，0 失败（约 1.29 秒）；Play Mode **7/7**，0 失败（约 12.27 秒）。没有用编译成功替代玩法测试，没有重跑后扩大范围。

| 需求 | 当前证据 |
| --- | --- |
| 空柜、仅商品、仅广告、混放都固定五位，逐位随机买卖方向 | `FiveVisitorsAreSampledIndividuallyWithDisplayWeightedProbabilities` 四组用例，各 100 个固定种子，共 400 天；每轮接待五位后无第六位，实际卖家人数跨种子变化 |
| 展示改变方向概率 | 默认卖家概率依次为 40% / 20% / 80% / 60%，数据检查及多种子分布检查通过；真实 UI 展示概率预览 |
| 仅最高总售价类别生效 | `OnlyHighestCategoryCountsAndTiesUseStableCategoryOrder` 验证 3 件材料合计 30 与 1 件丹药 50 的竞争、并列稳定规则；其他类别不贡献预算 |
| 同档范围不连续增长，跨档切换 | `BudgetUsesDiscreteTierAndEachBuyerVariesWithinTenPercent` 覆盖 0、20、22、29、30、99、100、299、300、600；各 30 个种子验证资金在档内随机变化且不越界 |
| 广告指定供货类别，重复广告不叠加概率 | `AdvertisementSelectsItsConfiguredCategoryAndDuplicatesDoNotStackProbability` 把隔离测试中的招牌改为装备，实际生成木剑供应；默认资产仍是材料 |
| 预览不消耗随机数；开门时固定完整队列 | `PreviewDoesNotConsumeRandomnessAndOpeningFreezesTheWholeQueue` 比较同种子会话，重复预览并营业后移动全部展示物品，五位方向、货物、求购类别、资金均一致；睡觉后重新生成 |
| 真正可点击、拖放、跨日 | `ActualButtonsRunFiveRandomVisitorsAcrossTwoDays` 从保存场景运行两个营业日，逐笔核对收支，接待五位后禁止第六位；`EmptyDisplayStillReceivesFiveAndRealDragsUpdateBudgetTierPreview` 从空柜营业，再拖第二盒丹药跨入 30 档，实际 UI 显示 54–66 |
| 既有交易与形状不回归 | 20/40 预算连续交易与 60 预算多件结算由隔离测试配置保留；柜台错误品锁定、归属、空间、旋转翻转、炼丹、房租测试仍通过 |

- 已在实际 Play 画面查看“两盒丹药＋材料招牌”的预览：每日 5 位，买家 40% / 卖家 60%，生效价值 36，档位 ≥30，资金范围 54–66，供货材料。截图为 `Temp/FirstPlayable/04-five-visitors-budget-tier.png`，临时目录不提交。
- MCP 最终：Editor ready，Play stopped，compiling=false，domainReloadInProgress=false；ShopPrototype 场景及 ShopCatalog 均无脏标记，根对象 Main Camera / Shop Prototype，组件引用原配置资产。
- 最终 MCP 缓冲与 Unity 原生 Console 均 **Error=0、Warning=0**。起始缓冲有上一轮截图路径错误；本轮未清空 Console，最终记录的是重新读取后的实际状态，不将旧错误消失归因于玩法修复。
- 配置经 Unity API 保存：四档门槛 0/30/100/300，基准 20/60/150/400，浮动 .1，概率参数 .4/.4/.2。七种物品原价格、形状、类别、开局物品及场景引用均核对保留；招牌补充广告类别与描述，旧固定预算数组由 Unity 序列化更新移除。
- 输入设置恢复为 ResetAndDisableNonBackgroundDevices / PointersAndKeyboardsRespectGameViewFocus。Packages、ProjectSettings、场景、管线与 .meta 无本次改动；已有 AGENTS/Skill 等其他任务变更保留。
- 预算和精确买卖比例是可调原型参数，随机性不保证当天有两组炼丹原料。新增正式内容、FigJam 中其他容器尺寸、二级仓库、顾客柜台不在本次实现范围。
- 无需手动配置 Editor；人工试玩、全分辨率与 Player 构建未执行。具体手动步骤见 FIRST_PLAYABLE_GUIDE.md。

## 上一版验证记录（历史）

## 已执行证据

- Unity 6000.6.0f1，现有 URP Renderer2D 基线保持不变。
- 类别与预算调整后的 Edit Mode 测试：22 项全部通过，0 失败，耗时约 0.97 秒。
- 类别与预算调整后的 Play Mode 测试：6 项全部通过，0 失败，耗时约 11.94 秒。
- Play Mode 测试通过实际 Input System 虚拟鼠标和键盘、UGUI Raycast 及按钮事件执行，不仅是直接调用经营数据方法。
- 实际保存场景被反复加载，能自动取得配置并开始第 1 天。初版曾查看初始/供货截图；本次另查看三件丹药交易画面，类别、预算 60、总价 54 和“18×3=54”均可见。新截图在本地 `Temp/FirstPlayable/03-category-budget-basket.png`；Temp 不提交，旧截图可能随 Editor 重启清理。
- 人工试玩：未执行，开发者需要按 FIRST_PLAYABLE_GUIDE.md 体验操作手感。

## 六项主目标审计

| 目标 | 具体证据 | 结论 |
| --- | --- | --- |
| 有形物品拖动、旋转、翻转 | `RotationAndReflectionHaveExpectedOccupiedCells`、`FlippedLCanEnterPocketThatOriginalCannot`、`LongItemMustRotateAtBottomEdge`；Play Mode `RealMouseAndKeyboardHandleShapesPreviewsAndRollback`、`FlipButtonReflectsHorizontallyAfterRotation` | 自动验证通过；水平翻转在旋转后仍按屏幕水平方向执行 |
| 三个容器之间真实流转 | 两天 UI 测试；`CounterAcceptsPlayerGoodsButPreventsCustomerTheft`、`PlayerGoodsMoveInEveryPhaseAndStayPlacedOnCustomerDeparture` | 自动验证通过；自有物品所有阶段可自由搬动，顾客未付款供货保持归属限制 |
| 展示影响当天交易 | `DisplayActuallyDeterminesTradeDirection`、`CategoryAttractionIsDeduplicatedAndSnapshotSurvivesDisplayChanges`、`RemovingSignAfterOpeningKeepsSuppliersAndFullCounterCanBeCleared` | 自动验证通过；每个不同类别生成一组预算买家，开门后展示变化不改变客源 |
| 买卖真实结算 | 不足钱、满空间、拒绝、提前闭店、未摆上柜台、重复结算测试；UI 两天完整链路 | 自动验证通过；错误路径保留库存和灵石 |
| 炼丹增值与空间库存 | `CraftFailsAtomicallyInFragmentedFullStorage`、`CraftCanUseSpaceFreedByItsOwnIngredients`、`TwoProcureCraftSellLoopsAndTwoDaysAreProfitable` | 自动验证通过；两组买入原料成本 14，制成并售出两盒丹药收入 36，余额 120→142 |
| 睡觉推进与再次经营 | 数据层两轮闭环；UI `ActualButtonsAndDragsRunTwoDaysWithSupplyCraftAndSales`；14 天房租测试 | 自动验证通过；UI 路径到第 3 天、余额 178、4 次买入、4 次卖出、3 次炼丹 |

## 其他要求审计

- 形状：单格灵露、2×2 丹盒/玉匣、四格长剑、L 形草、T 形朱砂、异向 L 收购牌；都有可见真实占格。
- 合法/非法预览：实际拖放测试检查红色失败条件、变形后成功条件、越界回退、Esc 取消和可见按钮。
- 防损坏：2,000 次固定随机种子的重排，对每次失败比对完整状态，逐步检查唯一 ID、边界、重叠、归属与非负资金。
- 交易方向、价格、玩家/顾客归属：UI 卡片、己/客标签、顾客蓝色边框；供货截图已查看。
- 买家成交后停留并扣除预算，下一位可随时点击；拒绝、换客、闭店保留未售商品当前位置。供货买入后供货者离开。这些路径由数据和 UI 测试覆盖。
- 开局资源、设备、供应、利润：120 灵石、开局一组原料和一盒丹、免费已有丹炉、确定供应；无需购买设备或依靠随机概率。
- 周期支出：首租 20、七日结算、约 5% 上涨、欠租显示；14 天测试验证不足时不产生负资金。
- 日期/资源可见、操作提示：实际屏幕已查看；全部容器角点在 Game 视口内的自动测试通过。
- 无范围外玩法、无新 Package、未重做 2D 转换、未改输入与 Build 设置：交付前通过文件差异检查。
- 入口与文档：ShopPrototype 场景、打开菜单、试玩指南、实现状态、边界和下一步已记录。

## 调试中修复的问题

1. 首次创建原生配置资产后，第一次进入 Play 未取到配置。同步导入并重新载入后修复；Builder 增加同步导入，后续保存场景启动测试均通过。
2. 拖动阈值触发时鼠标已经移动，曾错误地使用当前位置计算抓取格。改为 `pressPosition` 后，真实鼠标拖放测试通过。
3. 旋转 90 度后翻转曾沿物品原始横轴发生。改为屏幕横轴反射，并新增真实 Flip 按钮测试。
4. 本次 UI 测试首次运行时 Game View 失焦导致模拟指针被输入系统拦截。测试期间临时调整两项输入路由值，并在 TearDown 恢复，不保存项目配置。曾尝试替换设置对象，但 Unity 会销毁默认临时设置，导致后续 SetUp 失败；现改为原对象值的保存与恢复，最终六项全部通过，MCP 确认两项原值已恢复。

未将之前的失败结果冒充成功；上述通过数来自修复后的明确 editor/playmode 分开运行。MCP 的一次 mode=all 调用返回旧结果，因此不作为最终证据。

## 类别、预算与自由搬动回归

| 玩家可见结果 | 自动证据 |
| --- | --- |
| 20 预算买一盒后剩 2；40 预算连续买两盒后剩 4，下一盒不足不能出售 | `BuyerCanBuyRepeatedlyUntilRemainingBudgetIsInsufficient` 两组用例；真实按钮测试 `BuyerBudgetAndInvalidBasketControlRealConfirmButton` |
| 60 预算一次购买三盒，总价 54，剩 6 | `BudgetSixtyBuysThreePillsAsOneAtomicBasket`；真实鼠标测试 `ThreeItemBasketAndOpenDisplayChangesWorkThroughRealDrags` |
| 任意副本可上柜台；混入错误类别、招牌时整笔锁定 | `MixedCategoryAndNonSaleBasketsDisableTheWholeSale`；真实 UI 测试先用新炼制的第三份副本，混入玉匣再移回背包 |
| 同类别不同商品也能出售；重复展示不重复生成一组客人 | `CategoryAttractionIsDeduplicatedAndSnapshotSurvivesDisplayChanges`（草/灵露同为材料，预算买家不绑定具体商品） |
| 开门后移走展示/招牌、添加新类别不会改变队列 | 数据快照测试及三件交易 UI 测试；新类别只在下次开门生效 |
| 未成交也可下一位；成交后不自动离开；换客、闭店不移动未售货 | 数据和真实按钮测试共同覆盖，包含最后一位顾客离场 |

## 当前交付检查（2026-09-07 类别与预算调整）

- 保存的 ShopCatalog 已通过 Unity API 添加 7 种定义的 Category 及 Buyer Budgets=[20,40,60]；原形状、价格、开局配置和引用保留。
- Play Mode 验证后退出；Editor ready、compiling=false、domainReloadInProgress=false。打开 ShopPrototype 场景，isDirty=false，仍是 Main Camera 与 Shop Prototype 两个根对象。
- 最终 Console 留有 1 条本次截图失败 Error、0 Warning：第一次截图时 Editor 重启已清理 Temp/FirstPlayable 目录，记录 `Failed to store screen shot (...)`。随后通过 API 创建临时目录，截图成功写入并实际查看。该条已解决的工具操作日志保留，未清空 Console；不把它说成玩法异常，也不声称 Error=0。
- 测试结束、截图前的 MCP 缓冲与 Unity 原生 Console 均为 Error=0、Warning=0；六项 Play Mode 均通过 LogAssert 检查。没有发现本次运行的玩法脚本异常。
- 两项测试用输入配置已恢复：ResetAndDisableNonBackgroundDevices / PointersAndKeyboardsRespectGameViewFocus；未保存输入设置，Packages、ProjectSettings、场景、管线和 .meta 无本任务修改。
- 另一项任务并行更新了 AGENTS.md、项目 Skill 和 DEVLOG 条目，本任务保留；当前提交差异不全部归于本次开发。没有暂存、提交或推送。
- 本轮工具列表未挂载 Unity 工具，后续经已配置的本机 Unity MCP stdio 服务完成验证。临时客户端和请求文件仅位于忽略的 Temp 目录，不添加包或修改连接配置。
- 人工试玩、所有分辨率及 Player 构建未执行；手动步骤见 FIRST_PLAYABLE_GUIDE.md 的“本次交易调整的手动验收”。

## 初版交付历史（以下为当时状态）

- 2026-09-07 收尾时通过 MCP 退出 Play，Editor 为 ready，compiling=false，domainReloadInProgress=false。
- 当前打开 Assets/Scenes/ShopPrototype.unity，isDirty=false；根对象为 Main Camera 和 Shop Prototype，配置引用为 ShopCatalog，包含 7 种商品；相机实际 Renderer2D。
- Console 最后读取：1 条 Exception、0 条 Warning。异常为 `UnityConnectWebRequestException: Token Exchange failed due a failure with the web request.`，时间 2026-09-07 03:26:50 UTC，堆栈全部来自 UnityEditor.Connect 登录令牌交换。没有原型脚本的 Error/Exception。没有清空日志，没有改变账号或云服务配置；若该连接提示持续出现，由开发者检查 Unity 登录和网络。
- 文件差异检查：Packages、ProjectSettings、原 Assets/Settings 和 SampleScene 未改变；新增源码和程序集配置的 .meta 完整；git diff --check 通过。
- 没有产生本地 Git 提交，没有推送。实现、资产、测试和文档保留在当前工作区，目标书本来就是未跟踪文件。
- 补充售出截图曾因自动审批返回账户用量限制而未执行，没有绕过拒绝。该截图不是主目标的必需证据；后续只读状态检查和退出 Play 已正常完成，不存在未完成的必需审批动作。
