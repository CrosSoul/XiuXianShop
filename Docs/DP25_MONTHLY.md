# DP-25 月度经营与年度日历

依据：[DP-25](https://zzrzzrzzr11.atlassian.net/browse/DP-25)、[G-01 / G-03](https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/1736718)。本地基线 209fbe2，2026-09-09 实施；交付待用户验收，不修改 Confluence 正式数值。

## 实现

- `ShopSession` 使用连续 Turn，显示 Year / Month；每回合一个月，12 月推进到下一年 1 月。营业前、营业中、闭店阶段及交易流程沿用；收入支出在推进后重置。
- 日历改为每年 12 格、上下半年两行，上一年 / 下一年 / 本年本月；保留模态阻挡、详情、滚动、存取档。
- ShopCatalog 的 rentPeriod 通过 Unity API 从 7 改为 6；首租 20、后续向上取整增加 5% 及欠租机制原样保留。每年 6 / 12 月末结算，推进后不能重复扣费。
- 行情仍在开始回合公开完整持续条；跨半年、跨年裁剪，历史保留；仅生效回合影响报价；按年缓存，翻页和读档不重抽。
- 月度频率、持续、冷却尚未正式平衡。MarketCalendar 的明确测试常量为每年 3 次、随机持续 1–2 回合、结束后 2 个完整回合冷却；不是正式规则。固定重叠和跨年验证样例可持续 3 回合，仅用于测试。行情效果百分比沿用现有原型配置。
- 月度存档版本 2，默认 `UserSettings/ShopMonthlyPreparation-v2.json`。旧 `ShopPreparation.json` 不修改、不删除、不自动读取；显式传入 v1 也会拒绝且保留当前会话。没有把旧日序号解释为月份，没有新增迁移框架。若需要保留旧进度转换，需另定转换规则。

## 本轮验证

- Unity 6000.6.0f1，Edit Mode 91 / 91；Play Mode 19 / 19；失败、跳过均 0。原始结果在 `Evidence/DP25/editmode.json`、`playmode.json`。
- 覆盖年度边界、两次收租、读档与重复推进不重复扣费、行情公开/结束/冷却/跨年/缓存、实际年度日历点击及拥挤条滚动、价格刷新、购买历史、DP-23 让价取消/失效/确认及连续交易。
- 首次 Play 15 / 19：3 项模拟输入未启动拖动，1 项沿用跨周样例断言。修改样例为跨半年，并聚焦 Editor 后实际重跑 19 / 19；未改输入系统或放宽拖动断言。不把失败轮次算作通过。
- 实际运行：第 1 年 6 月余额 120 → 第 1 年 7 月 100 → 第 2 年 1 月 79，下次收租第 18 回合。已查看 `year2-calendar.png`。
- 隔离跨年样例 12–14 回合在第 12 回合公开；浏览下一年显示 1–2 月延续条，截图 `cross-year-market.png`。浏览未推进当前第 1 年 12 月。
- 实际文件读取检查：写入临时 v1 样例，LoadPreparation 拒绝；sameSession=true、fileUnchanged=true，显示“旧日制存档”说明。没有接触玩家旧存档。
- 收尾 Console Error 0 / Warning 0（未清空），场景 ShopPrototype 无未保存修改。未执行 Player 构建或玩家人工手感验收。

## 手动验收入口

1. 打开 ShopPrototype 场景并 Play，无需配置资产。普通开局为第 1 年 1 月；开始营业、结束营业、推进下个月，原有交易照常。
2. Play 中菜单 `XiuXianShop > Validation > Start Calendar Overlap Example (resets Play session)` 建立隔离样例并停在第 1 年 6 月。它重置当前 Play 会话，用临时存档，勿用来保留本轮游玩进度。
3. 用 `Advance Calendar Example One Turn` 推进到 7 月：扣首租 20，7–9 月行情完整公开；8 月再公开叠加行情，10 月报价不再受其影响。翻页不改变安排。
4. 推进到第 2 年 1 月：余额在无交易情况下为 79。日历共 12 格，当前年/月正确；新年度 6 / 12 月标租金。保存、读取准备阶段不再次扣费。
5. 交易与让价手感按 DP23_BUDGET.md 的现有步骤复验；本轮自动测试已覆盖其结算回归，不需要重复验收所有旧日历功能。

修改范围：7 个运行/Editor 脚本、ShopCatalog.asset、6 个现有测试文件和相关 Docs。未改 Scene / Prefab / Meta、Packages、渲染、输入或 Build 设置；不包含新炼丹、外出、储存系统。
