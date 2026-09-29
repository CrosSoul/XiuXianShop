# 内容作者操作步骤（DP-64）

1. 在 Confluence 编辑 04.4–04.7，保留稳定 ID；批准的新内容或修改标记「待同步」。
2. 导出到 `ContentSources/<快照名>/`，四表分别命名 `visits.csv`、`visit-items.csv`、`scenes.csv`、`nodes.csv`。保留完整表头；空表也必须有表头。支持普通 CSV 与 Confluence CSV 的 schema/views/records 包装。
3. 同目录创建 `snapshot.json`，例如 `{"formatVersion":1,"snapshotId":"2026-09-28-v1"}`。这是可读版本标识，不是校验和。
4. Unity 菜单 **XiuXianShop → 内容同步 (Content Sync)**，刷新并选择快照，点击 **Validate**；错误显示表、行、稳定 ID、字段和原因。
5. 点击 **Preview Diff** 检查新增、修改、停用与遗漏保留；无错误才可 **Import**。预览后修改源文件或目标资产，需要重新预览。
6. 检查 Git 中 CSV 与 `Assets/Data/AuthoredContent.asset` 的差异，再 Play。DP-62 读取来访数据，DP-52 的 StoryRunner 执行剧情节点。

## 同步约定

- 四域共用一个导入入口、一个运行时资产；引用现有 ShopCatalog 的物品、地点和配方，不另造商品目录。
- 草稿不导入；已生效必须与目标一致；停用保留 ID 和旧字段；遗漏保留旧记录。可选空值保留旧值，新记录为空则保持未指定，不猜成 0。不提供隐式清空操作。
- visitId、条目ID、SceneID、NodeID 各自唯一。按 G-15，同一 customerId 可以多次来访，显示名和立绘必须一致；改名时应同步该身份的各条来访。
- 类型使用 `Special / Story`；队列阶段 `BeforeOrdinary / AfterOrdinary`；完成条件 `SceneEnd / TradeSuccess`。04.5 用途编码 `Sell / Carry`；求购类别使用现有 ItemCategory 枚举名称。
- 每个 Scene 的首个有效节点为入口。Choice 选项文本、后继 ID 均为单元格内换行分隔；Branch 两个后继依次为真、假。NextNode 必须在同一 Scene；End 无后继。
- Action 白名单：SetFlag、UnlockLocation、UnlockProfession、UnlockKnowledge、UnlockRecipe；当前 Action值只接受 `true`。地点和配方必须存在于现有 Catalog；当前职业仅支持已实现的 `alchemy`。知识和实例预设尚无接入目录，非空引用会明确报错，不接受未声明目标。
- 运行时仅使用本地资产，无 Confluence 凭据、联网、热更新或运行时导入。

## 随附源快照的已知问题

`Confluence-2026-09-28` 保留原始导出。`nodes.csv` 的 `alchemy_hint_001` 行存在字段错位：`草稿` 位于 NextNodeID，备注文本位于数据状态。因此该快照会被拒绝；没有静默移动字段，也没有将草稿作为正式内容导入。修正 Confluence 源表并重新导出后再校验。默认运行时资产现已导入另一个 `DP52-MVP` 快照：其灰盒语义依据 DP-52 明确授权，不是对旧草稿的自动批准。

首批仅接入 04.4–04.7；原有物品/配方 DP-14 校验脚本保持独立且未修改。后续其他内容域可添加 ContentDomainAdapter，不需要另建菜单或同步框架。

## DP-62 来访运行与试玩

批准内容仍按上述四表流程导入。每条 visit 在开业时检查固定/最早/最晚回合、必须/禁止 Flag 和前置 visit 完成；所有合法记录按阶段、顺序、稳定 ID 入队，额外加入普通客流。Flag 列用逗号或单元格内换行分隔。可选「重复策略」列为 `Once`（出现过不再来）或 `UntilCompleted`（未完成可在后续合法回合再来，新记录默认）；固定回合始终只命中指定月份。完成后均不再出现，多个 visit 可复用同一 customerId。

求购/预算、固定货物、Scene 引用和对白更新只需重新导入。存档保存来访进度、Flag、地点结果，不保存对白定义。旧 DP-61 存档缺少来访进度时视为尚无进度。

来访通过 PendingVisitScene / FinishVisitScene 交接，运行时显示现由 DP-52 StoryRunner 负责。正常 End 时统一检查及应用结果，取消不应用。交易结果仅成功结算后触发；要求交易完成的来访不能提前执行结果动作。原 VisitSceneGraybox 仅保留用于既有线性适配测试，不再驱动游戏 UI。

隔离试玩：打开 ShopPrototype 并 Play，选择 **XiuXianShop → Validation → Start DP62 Authored Visits (resets Play session)**，然后开始营业。先出现消息客，结束对白后获得线索并解锁「炼丹房求学」；取消则不解锁。下一位为预算 40、求购丹药、携带草药与露水的买卖客。闭店后可正常外出查看新目的地；该入口只允许访问一次，结果随现有存档保存。真正授业、职业和长期炼丹房访问由 DP-52 后续实现，本切片不授予。

隔离数据源位于 `Docs/Fixtures/DP62`，生成资产为 `Assets/Data/DP62VerificationContent.asset`；这是专用测试输入，未写入默认 AuthoredContent。若编辑正式内容，请使用 ContentSources 快照入口，不把隔离样例自动当作批准内容。

## DP-52 剧情运行器与求学链路

直接打开 ShopPrototype 并 Play，开始营业，结束线索顾客对白；营业结束后完成携带准备，前往「炼丹房求学」。完成当值人员与长老的对白和选择后，解锁 `profession:alchemy` 与长期炼丹房。该求学目的地在成功 End 后消失；中途取消不写结果，可在后续回合再去。没有赠送具体丹方。`DP52-MVP` 的人物称谓、对白和分镜均为灰盒，不是正式剧本。

同一运行器支持 Dialogue、Visual、Choice、Branch、Action、End。Visual 自动推进到下一段对白/选择；Action 暂存到 End 才提交，但当前 Scene 的后续 Branch 能读取暂存结果。取消丢弃暂存结果。End 返回原顾客或原外出地点；不自动送别、回店或推进月份。剧情遮罩阻止普通输入，Esc 不打开系统菜单，剧情中不能保存；现有安全保存阶段仍遵循 G-14。

新增可选快照列（原必需表头不变）：

| 表 | 列 | 用法 |
| --- | --- | --- |
| scenes.csv | 触发地点ID | 已有地点 ID；一个地点一个进入 Scene。已完成 Scene 的读档不重播。 |
| nodes.csv | 背景、分镜 | 灰盒背景/静态分镜说明；分镜填写 `Hide` 隐藏。 |
| nodes.csv | 立绘槽、立绘显示 | `Left / Right` 与 `true / false`；配合既有立绘ID、说话者、表情状态。 |
| nodes.csv | 背景图片、立绘图片、分镜图片 | 可选 Unity 已导入 Sprite 的 `Assets/...` 路径；空值保留旧引用，没有图片时显示灰盒占位。错误路径阻止导入。 |
| nodes.csv | 选项条件类型、选项条件键、选项条件值 | 三列按换行与选项逐项对应；无条件项填 `None / - / true`。不满足的选项禁用。 |

条件支持 FlagExists、FlagAbsent、LocationUnlocked、ProfessionUnlocked、RecipeUnlocked；条件值为空/true 表示满足，false 表示取反。Choice 不使用整节点条件，逐项条件避免隐式选择分支。知识内容尚不存在，因此 Knowledge Action/条件仍拒绝导入。未知引用、不可达节点、无 End 路径和不经过交互的自动循环都会阻止导入。修改这些字段仍通过同一 Validate → Preview Diff → Import 流程，不修改 C#。

## DP-65：百事堂内容

使用 `DP65-Migration` 快照（snapshot.json 的 domains 为 commissions），在同一个内容同步窗口执行 Validate → Preview Diff → Import。原始 Confluence 导出保留在 Confluence-DP65；该导出尚缺机器结构字段，不能直接导入。迁移快照把现有批准规则标为待同步，17 个现有物品成员仍明确标注原型，不代表最终成员批准。未回写远端数据库。

- 新增普通委托：复制 commission-templates.csv 一行，填写新稳定ID、委托名、场景文案、正候选权重、已有奖励池ID与选择前显示，状态待同步。文案、权重和奖励方向直接改对应列；不要改稳定ID来改名。
- 全局参数在 commission-global.csv：candidateCount、extraGiftChance（0–1 或百分数）、extraGiftCount 等；每回合刷新、无放回等固定能力不接受任意新字符串。默认仍为 3 选 1、谢礼 8%。
- 奖励池在 commission-pools.csv：货币最小值/最大值；实物抽取组写作 `组ID:最少抽取次数:最多抽取次数`，多组用分号连接。每组各抽一次所配置次数，从而保留混合池的材料与商品各一件。此结构列是当前原型快照对文字规则的显式展开，后续数据库导出也需保留这些列。
- 添加成员：commission-members.csv 新增唯一条目ID，填奖励池ID、抽取组ID、已存在物品ID、权重、每次选中该成员后的最小/最大件数（当前均为 1）；状态待同步。谢礼每次只能一件。删除用停用；漏行保留原记录。引用未知、停用池或未知物品及重复ID都会阻止导入。
- 当前原型成员：材料 herb/dew/cinnabar，普通商品 sword，谢礼 pill；共 17 条跨池成员记录。没有补入新高价值物。运行时读取 ShopCatalog 的同步结果；CommissionVerification 仅供显式 SetPrototypeDefaults 隔离测试/原型重置，不是正常 Play 的内容源。
- 同步仅更新静态配置，不重抽运行中或已保存的委托候选。对已生成候选的模板应保留稳定ID。

## DP-66：市场事件

使用 DP66-Prototype 快照，domains 为 market，market-events.csv 对应 04.12。原始空表保留在 Confluence-DP66；迁移的 medicine-demand / medicine-supply / materials-supply / equipment-demand 均为当前 Unity 测试配置。数据状态待同步表示本次已授权导入，修改说明中的“原型测试值”不代表正式平衡。草稿不会新增到运行池，不支持把任意“测试”状态自动启用。

新增同类行情：复制一行并填写新行情ID、名称、说明、目标类别（Medicine/Material/Equipment 等现有枚举）、交易方向（PlayerSells 玩家出售、PlayerBuys 玩家买入、Both 双向）、修正百分比（20% 或 0.2）、生成权重、持续最小/最大回合、冷却回合，标待同步再校验导入。权重 0 不参与新生成；显式停用保留身份。修改既有名称不改ID。

普通年度生成与茶肆秘闻共用同一事件池及合法性检查。每年最多3次仍是既有隔离原型频率，未作为正式月度平衡。已生成事件和会话内定义在存档中固定，内容导入只影响新会话，不重抽旧存档；未来未开始行情保持隐藏，秘闻可提前公开，报价仅在有效回合应用。

## DP-67：炼丹灰盒配置

选择 `DP67-Migration`，沿用同一 Validate → Preview Diff → Import 流程，退出 Play 后导入，再 Play 验证。`Confluence-DP67` 是未经改写的原始导出；迁移快照仅保留本次授权的三张既有灰盒配方，非新增正式配方、非最终平衡。历史候选材料和废止灵石原料没有导入。

- `recipes.csv` 对应 04.2：稳定配方 ID、显示名、说明；`recipe-lines.csv` 对应 04.3：唯一静态输入/输出主表。当前内核每炉一件丹药产物。
- `alchemy-global.csv` 对应 04.13：研磨秒数、完美/合格窗口、低中高火耗能倍率、普通/良品/上品价值倍率。例：grindDuration 改 4，导入后两处炼丹共用 4 秒研磨。
- `alchemy-materials.csv` 对应 04.14：物品、数量、投料顺序、首味预置、完整/研磨、目标在炉秒数。修改材料身份/数量时同步修改 04.3；修改要求状态时同步对应炉程投料行。材料评分只比较收丹时刻减有效入炉时刻，不按绝对投料时间评分。
- `alchemy-steps.csv` 对应 04.15：预置、开炉、研磨、投料、调火、收丹及现有灰盒提示。仅支持当前动作，不执行自定义代码。`目标炉钟秒` 只用于调火，迁移保留既有高阶目标 8 秒高火 / 24 秒低火；它不参与材料计时。这一列补足原始导出缺失的现有火候评分参数，不代表新增评分规则。
- 所有改动行标为待同步，未知引用、重复 ID/顺序、材料主表不一致、无合法产物等会阻止整个导入。缺行不表示删除；状态与稳定 ID 沿用统一同步契约。

正常 Play 读取同步后的 Catalog；原三张硬编码配方已收拢到显式测试夹具 AlchemyRecipeVerification，不作为正常运行备用数据。灰盒物品定义沿用原实现，不给仓库自动加测试材料。仍通过已有开发菜单领取炉子/材料包。炉息标尺、体力消耗、品质分值/阈值、外出一访一炉和店内逐炉操作没有重做。
