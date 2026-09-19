# DP-50 普通随机顾客 v1

当前状态：实现与最终验证已完成，待用户验收。主体已在现有提交 2b0960a；2026-09-20 的来货价值显示修正和交付记录仅本地未提交。

## 正式依据与范围

按 DP-50、G-02/G-08 v3、G-01/G-03 v7、G-11 v8 听风茶肆部分、G-04 v12 的 AC-40/46/48/56–62，以及现有 DP-45/48 实现工作。未全量阅读 GDD。

普通顾客统一在开始营业时生成，复用原有队列、顾客对象和混合交易结算。基础 5 人，体力溢出与茶肆宣传各加 1，可叠加为 7。重复打开界面或重复开始营业不会生成第二批。

## 配置入口

Inspector 中打开 `Assets/Data/ShopCatalog.asset` 的 Customers：

- Buying/Selling/Trading Weight：只求购、只出售、同时买卖，默认 40/25/35。
- Base Category Weight / Displayed Item Weight：所有合法类别基础 10，每件展示商品为对应类别加 5；空展示柜也能营业，多类别同时贡献。
- Ordinary/Wealthy/Lavish Weight：预算三档权重 75/23/2。
- Budgets：类别 × 三档金额。材料普通 20、丹药普通 45 是正式确认值，其余均在配置中标记为原型金额，不代表最终平衡。
- Supply Pools：先抽类别，再抽池内物品；正常池为材料 herb/dew/cinnabar、丹药 pill、法器 sword。非法或空类别池不参与抽取。
- One/Two Supply Weight：来货 1 件或 2 件，默认 75/25。
- Base Customer Count / Stamina Overflow Customers：基础人数和溢出加人。

茶肆既有配置保留：商旅到访将同时买卖概率加 25 个百分点、封顶 85%，其余按原比例分配；求购/供货风向在正常类别权重后乘 2；阔客风声随机一人升一档、封顶。只出售顾客即使命中升档也不新增求购计划或预算。

本版本替代旧的“最高展示总价值类别 + 全局预算档位 + 金额波动”生成规则。广告牌资产保留，旧广告牌方向概率和强制来货筛选不进入本轮普通顾客生成；没有实现新的广告牌系统。没有发现需要阻塞实施的正式规则冲突。

## 改动文件

- 新增 `CustomerGenerationSettings.cs`：序列化配置；`ShopSession.Customers.cs`：统一生成和权重计算。
- `ShopCatalog.cs`、`ShopCatalog.asset`：新配置及移除旧顾客生成配置；资产通过 Unity API 保存。
- `ShopSession.cs`：营业快照、三种行为数据、原队列接入；月度切换清空三类人数。原交易支付不补充求购预算的结算保持不变。
- `ShopSession.TeaHouse.cs`：风向接入合法类别池，移除旧全局预算升档路径。
- `ShopPrototype.cs`、`ShopNegotiationView.cs`：三种行为显示、只出售无求购提示及展示说明。
- `Editor/PrototypeBuilder.cs`：已有隔离验证入口改用新配置。
- 新增 EditMode `CustomerGenerationTests.cs`、PlayMode `CustomerGenerationPlayableTests.cs`；更新旧测试配置与已被新正式规则替代的断言。相关旧测试涉及交易、行情、灵石、体力、储存、茶肆；没有改动这些系统的玩法。
- 新文件 `.meta` 由 Unity 生成。未改 Scene、Package、Project Settings。

## 已执行验证

- 保存记录 `Evidence/DP50/editmode.json`：Edit Mode 139/139，通过，0 跳过。
- 保存记录 `Evidence/DP50/playmode-customers.json`：DP-50 Play Mode 3/3，通过，0 跳过。覆盖同时买卖进入现有谈判并按净额扣预算、只出售来货成交后不获得求购预算、叠加 7 人与重复刷新不重生成。
- Edit Mode 包含概率分布、展示快照、风向非必然命中、商旅归一化与上限、可编辑类别预算、空池排除和随机一名升档。
- 收尾版定向 Edit Mode 10/10、DP23 净额预算 Play 回归 1/1 通过，见 `editmode-final-customers.json`、`playmode-budget-regression.json`。此前中断的测试未计入通过；后来实际重新执行并保存了结果。
- 实际正常 Play：空展示柜生成 5 人；丹药普通档预算 45；只出售显示无求购计划；跨月三类人数均清零。这些通过运行时调用检查，区别于鼠标操作的自动 Play 测试。
- 2026-09-20 修正顾客来货总价值误用谈判柜台总价的问题，编译完成无错误；正常 Play 读取实际 UI 并查看截图，来货 1 件正确显示 10 灵石。此显示修正不改交易结算，没有无差别重跑全套测试。
- 最终 Unity Pipeline CLI 检查：Console Error 0 / Warning 0，未清日志；Editor ready/stopped，compiling=false、domainReloadInProgress=false；ShopPrototype isDirty=false。当前连接通过 CLI 完成，未将其冒称为 MCP 工具调用。
- 未执行玩家人工手感验收、Player 构建或平台测试。无未解决正式规则冲突。
## 开发者试玩步骤

1. 正常场景 Play，空展示柜开始营业，确认仍有 5 位普通顾客，可跳过顾客。
2. 下一次营业前展示不同类别商品，查看各类别均产生权重；营业中搬动展示商品不改变本批顾客。
3. 遇到同时买卖顾客，把符合其求购类别的自有商品和来货加入原谈判，确认按净额结算；玩家净付款不增加顾客求购预算。
4. 遇到只出售顾客，界面显示无求购计划，可购买其来货。
5. 叠加溢出客流与茶肆宣传，确认本次 7 人，反复打开界面不会加人。

概率效果不保证单次出现；自动测试采用多个种子验证分布。无需新增手动资产配置，可直接 Play 按以上步骤验收。
