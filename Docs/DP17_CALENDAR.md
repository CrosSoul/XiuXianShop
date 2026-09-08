# DP-17 日历与行情交接

2026-09-09：用户已初步验收 calendar v1（当前基线 d36ca8f），本轮仅调整行情冷却与公开时间，不重复整套玩法回归。

## 当前规则

- 日历展示连续两周、今天和收租日；翻页不推进日期、不扣租金。睡觉仍是跨日与收租入口。
- 正常四种行情池每周生成 2–3 个行情，每个持续 1–3 天；不同类型可以重叠或跨周。价格效果按类别、交易方向及实际日期生效，沿用现有报价与结算路径。
- 同一行情按稳定类型 ID 判断，生效期间不能重复，结束后再隔两个完整游戏日才可出现。例如第 2–4 天法器升价，第 5、6 天禁止重复，最早第 7 天再次出现。
- 缺失周按时间顺序生成并缓存，翻页顺序不改变安排、不消耗顾客随机序列。若自定义行情池太小导致没有合法候选，宁可少生成，也不打破冷却限制。
- **行情到开始当天才公开**：之前不显示名称、持续条、详情或占用行。开始当天显示完整持续时间，包括延伸至未来日期或下一周的部分；结束后保留灰色历史记录。收租提示不受行情隐藏规则影响。
- 保留已有存档安排，不重抽或追溯删除旧事件。冷却约束适用于本轮代码新生成的事件；旧存档如果已含旧规则的重复安排会保留。公开时间限制同时适用于新旧存档。
- 营业准备阶段可在日历保存/读取；默认文件为项目 `UserSettings/ShopPreparation.json`，不纳入 Git。保存包含行情安排、日期、资源、购买历史及顾客随机进度。读取不重复结算房租。

## 本轮文件

- `Assets/Scripts/MarketCalendar.cs`：按类型检查冷却、顺序生成、公开事件查询。
- `Assets/Scripts/ShopCalendarView.cs`：持续条、自动选中、详情使用统一公开过滤；移除未来行情颜色与提示。
- `Assets/Tests/EditMode/MarketCalendarTests.cs`：新增 `CalendarFeedback` 两项针对性用例，覆盖间隔、跨周、翻页顺序、存档一致性与开始日公开完整条。
- `Assets/Tests/PlayMode/ShopPlayableTests.cs`：更新现有 UI 用例，先检查隐藏，再推进到开始日检查已公开的重叠条；未要求重复执行整套 Play Mode。
- 设计、计划、试玩指南和 DEVLOG 同步当前规则。没有修改 Scene、Prefab、资产配置、Package、Project Settings 或输入系统。

## 验证边界与可选查看入口

calendar v1 的历史自动结果为 Edit Mode 67/67、Play Mode 13/13；这不是本轮修改后的测试结果。用户已反馈该版本运行正常，按要求不重复运行这些整套回归。本轮编译成功，仅执行 `CalendarFeedback` 两项 Edit Mode，2/2 通过（0.6 秒），包括跨周冷却、翻页/恢复一致性、公开时点及完整持续条。更新后的 Play Mode UI 用例未重新执行。Console 历史记录与 Editor 状态见 DEVLOG。

无需手动配置。若之后想查看公开过程，可在 ShopPrototype 的 Play 中使用 `XiuXianShop > Validation > Start Calendar Overlap Example`（会重置当前 Play 会话，使用独立临时存档）。第 6 天仅公开当天收购行情；用 `Advance Calendar Example One Day` 到第 7 天显示求购潮第 7–9 天完整条，第 8 天才显示第二项跌价行情。此入口不是要求重新验收整套经营玩法。

`Docs/Images/dp17-calendar-initial.png` 与 `dp17-calendar-normal.png` 是旧版可提前看见行情的历史截图，不作为当前公开规则证据。FigJam 日历参考因 MCP 配额限制未能读取，不宣称完成像素级核对。
