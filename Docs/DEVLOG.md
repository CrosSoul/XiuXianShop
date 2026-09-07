# XiuXianShop 开发日志

按任务记录日期、范围、实际改动、验证和未完成内容。不将计划功能写成已实现功能。

## 2026-09-07 — 每日五位随机顾客与展示预算档位

- 读取 FigJam 节点 28:1049 的最新展示吸引判定。按用户后续选择，五个顾客逐位随机决定买卖方向，不采用固定 2 卖家＋3 买家的建议；按用户补充将预算改为离散档位，不使用展示价值连续函数。
- 空展示柜也允许营业，每天只生成五位；反复点下一位或同一位连续成交不会增加名额。展示物品只影响开门时的抽样，开门后搬动不重生成，睡觉后重新计算。
- 默认卖家概率为基础 40%＋有效广告牌 40 个百分点－有可售商品展示 20 个百分点，截断到 0–100%。因此空柜/仅商品/仅广告/两者都有分别为 40%/20%/80%/60%，全部在 Inspector 可调。广告牌重复不叠加；单日可能全买家或全卖家。
- 买家只求购展示总售价最高的单一类别，同类不同商品合计；并列按类别枚举顺序稳定决定。没有可售展示时从配置中的可售类别随机求购，不要求玩家持有。其他展示类别不贡献预算。
- 默认预算档位：0–29→基准 20（18–22）；30–99→60（54–66）；100–299→150（135–165）；≥300→400（360–440）。范围为约 ±10% 内的整数值，每位买家独立随机；门槛、基准和浮动比例可调。同档展示 20/22 不再改变预算范围。
- 广告牌新增 advertisedCategory；默认收购牌为材料。卖家从广告类别对应的可供货商品中随机携带一件；没有有效广告时从所有可供货商品随机选择。当前材料包括草、露、朱砂，不再保证固定两组草露供货。没有可供货商品时只生成买家；异常空商品配置有明确失败提示。
- 修改原 ShopCatalog、ShopSession、ShopPrototype 和两个测试文件；新增轻量 DisplayAttraction 数据快照用于预览和开门，复用现有队列，没有引入 Manager/Service。随机种子仅作为 ShopSession 可选构造参数便于测试，正常游玩逐局随机。
- ShopCatalog.asset 经 Unity MCP/Editor API 更新档位和概率、广告类别/描述，移除旧固定预算数组。核对保留七种定义原价格、形状、类别、开局库存和场景引用；未手写 Unity YAML。同步设计、计划、试玩和验收文档。
- Edit Mode 39/39（约 1.29 秒）、Play Mode 7/7（约 12.27 秒）通过。覆盖 400 个种子的五人队列与概率分配、10 组档位边界、每位预算波动、最高类别、广告类别、重复预览不改变随机结果、营业快照和次日重新生成。Play Mode 通过实际鼠标/键盘/UGUI，从保存场景完成两天随机经营和空柜→两盒丹药跨档；原多件/连续交易用隔离配置回归，不冒充现在默认仍固定 20/40/60。
- 已查看真实 Play 预览截图：两盒丹药价值 36，档位 ≥30，资金 54–66，材料招牌下买家 40% / 卖家 60%。截图与 MCP 请求均在 Temp 中，不提交。人工试玩、全分辨率与 Player 构建未执行，手动步骤已更新。
- 最终 MCP：Editor ready、Play stopped、无编译/Domain Reload；ShopPrototype 与 Catalog 均已保存无脏标记，原两个根对象及资产引用有效；输入测试临时值已恢复。Console MCP 缓冲与原生计数均 Error=0、Warning=0。本次没有清空日志，起始存在的旧截图错误仅作为历史记录保留，不归因于本次玩法修复。
- Git 开始时已包含上一轮代码/资产/测试/文档变更，以及其他任务的 AGENTS 与 .agents Skill；保留已有工作。未改 Packages、ProjectSettings、场景、.meta、容器尺寸或其他 FigJam 功能；未暂存、提交或推送。无需开发者手动配置 Unity Editor。

## 2026-09-07 — 买家类别与预算、多件结算和自由搬动

- 按本次三项反馈调整现有原型，不扩展产业或其他玩法。为所有商品定义增加单一 Category 标签：丹药、材料、装备、容器、业务招牌；顾客改为求购类别 + 剩余预算，不再绑定某一件商品实例。
- 默认每个不同的展示类别吸引预算 20、40、60 的三位买家，同类别不按展示件数重复生成。预算数组放在 ShopCatalog 的 Inspector 中；这是本次采用的简单确定性客流配置。招牌仍带来四次供货。全部客源只在开始营业时生成，之后增减展示物品不会影响当天队列。
- 柜台接受任意自有物品。确认出售统一验证整批可出售、类别符合、总价不超过预算；空柜台、不可出售品、类别错误和资金不足均禁用按钮并显示原因。UI 显示类别、剩余资金、总价值与按商品分组的单价×数量小计。
- 成交整批移除物品、增加玩家资金并扣除顾客预算，买家保留，可连续购买。下一位允许未成交跳过；拒绝、换客、闭店保留自有物品的当前位置，不自动归还展示。供货者未付款物品仍受归属保护。
- 三个区域在准备、营业、闭店阶段均允许玩家自由拖放，保留形状、碰撞、旋转和翻转规则。柜台被自有物品占满时，供货请求保留，腾空后可重试下一位，不丢失物品。
- 修改 ShopCatalog.cs、ShopSession.cs、ShopPrototype.cs，两个既有测试文件及 ShopCatalog.asset。配置资产由 Unity MCP/Editor API 保值迁移，只增加类别和预算字段，不手写 YAML。同步 GAME_DESIGN、DEV_PLAN、试玩指南与验收记录。
- 验证：Edit Mode 22/22，通过用户预算 20、40、60 的示例、多件交易原子性、任意副本、同类别不同商品、跨阶段搬动和客源快照。Play Mode 6/6（约 11.94 秒），通过 Input System 虚拟鼠标、键盘与实际 UGUI 按钮完成两天循环及新交易流程；不是人工试玩。
- UI 测试调试中修复 Game View 失焦造成模拟输入失效、临时设置对象被 Unity 自动销毁导致 SetUp 失败的问题。最终仅在测试期间临时调整并恢复两项输入值；MCP 确认原值恢复，Project Settings 没有修改。
- 已进入临时 Play 会话查看预算 60/三盒丹药的交易画面，18×3=54 与确认按钮正常。首次截图因 Temp 子目录缺失失败；补目录后重试成功并查看图片。最终 Console 保留该条已解决的截图 Error，0 Warning；没有清空日志。测试结束、截图前 Console 原生计数和 MCP 缓冲均为 0 Error / 0 Warning，未发现玩法脚本异常。
- 最终退出 Play，Editor ready、未编译、未 Domain Reload；ShopPrototype 已保存无脏标记，原两个根对象和 Catalog 引用保留。未改 Package、输入或 Build 设置、管线、场景和 .meta。
- 续作时 Unity 工具未出现在当前工具列表，使用原本配置的 Unity MCP stdio 服务完成测试和收尾；Temp 中的客户端/请求/截图均不提交。未创建本地提交或推送。并行出现的 AGENTS/Skill/其他 DEVLOG 变更保留，不归入本次实现。
- 开发者无需手动配置 Editor。人工操作手感、全分辨率和 Player 构建未验证；具体手动路径及预期结果已补入 FIRST_PLAYABLE_GUIDE.md。收尾读取并应用工作区新增的 unity-goal-driven-development Skill，未扩展已授权范围。

## 2026-09-07 — Karpathy 社区准则适配为项目级 Codex Skill

- 本次仅研究、适配和安装开发行为准则，不推进游戏功能，不提交或推送 Git。使用 skill-creator 与 OpenAI Docs 指导，未执行上游脚本或安装命令，未安装全局 Skill 或改其他项目。
- 联网核查上游 README、CLAUDE.md、Skill、Claude 插件清单、Cursor 规则/说明、EXAMPLES 及完整文件树；main 为 `2c606141936f1eeef17fa3043a72095b4765b9c2`。上游值得作为轻量底稿，但不照搬“困惑就停下询问”、示例中的逐步确认或字面最小化。来源、完整适配映射及许可现状见 [.agents/skills/unity-goal-driven-development/references/UPSTREAM.md](../.agents/skills/unity-goal-driven-development/references/UPSTREAM.md)。
- 当前代码中已有 Storage/Display/Counter 三个容器、形状旋转/翻转、拖动、交易、炼丹与睡觉路径。ShopPrototype 的静态布局创建背包/仓库、展示柜、谈判柜台；没有据此认定用户背景中的独立“下方物品栏”已完成。已有验收文档中的 16 项 Edit Mode / 4 项 Play Mode 通过是历史记录，本次未重跑，也不代表当前已有改动或人工试玩已验证。
- 安装前发现根 AGENTS.md；项目内未发现其他 AGENTS.override.md、CLAUDE.md、Skill 或 .codex 配置。原有六项未提交改动为 ShopCatalog.asset、ShopCatalog.cs、ShopPrototype.cs、ShopSession.cs、ShopSessionTests.cs、ShopPlayableTests.cs，本任务均未编辑。

### 实际文件改动

- 新增 `.agents/skills/unity-goal-driven-development/SKILL.md`：保留理解问题、简单方案、精准修改、可验证目标四项原则；允许补齐当前目标所需交互/状态/边界，自主处理小决定，保护 Unity 序列化与资源引用，持续至已授权里程碑，按证据层级汇报。
- 新增该 Skill 的 `references/UPSTREAM.md`：保存上游提交、作者 forrestchang、Karpathy 思想来源、MIT 声明及 Codex 官方依据。上游完整文件树没有独立 LICENSE 或版权年份正文，本次不伪造它们。
- 根 AGENTS.md 仅增加一条有范围限制的 Skill 阅读引用；原文逐项保留。此引用是代理阅读指令，不是虚构的 include 配置。当前日志追加本节，其他设计与计划未调整。
- 按官方 [Build skills](https://learn.chatgpt.com/docs/build-skills) 使用项目 `.agents/skills`，以 description 供匹配、正文按需加载；没有添加可选 openai.yaml，沿用隐式调用默认允许。按官方 [AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md) 保留根规则并增量引用，不复制 Claude/Cursor 配置。

### 实际验证与边界

- Codex CLI 0.153.4 的 `codex debug prompt-input` 成功返回有效 JSON，技能目录映射指向当前项目 `.agents/skills`，技能列表含新名称、完整中文 description 和 SKILL.md 路径，项目 AGENTS 文本含新增引用。没有发送模型执行任务；这证明本地 CLI 的发现、元数据解析和提示装配，不能等同于桌面当前旧会话已刷新，或证明模型已在真实玩法任务中选择并遵循正文。
- 静态检查通过：UTF-8 文本、必需前言字段、名称与目录一致、名称/描述长度、来源相对链接和无模板占位；Git 未忽略新 Skill。`git diff --check` 通过。独立阅读核对触发边界：玩法实现/修复适用，普通可逆实现决定不应询问，纯文档/Skill 维护不强制触发；未进行模型行为测试。
- skill-creator 的 quick_validate.py 已尝试，但环境缺少 PyYAML，未运行成功；未为此安装全局依赖。实际 Codex 元数据解析和上述静态检查作为本次格式验证证据，不冒称该脚本通过。
- 文件 SHA-256 比对：Assets、Packages、ProjectSettings 共 117 个文件中 116 个与本次写入前快照一致；暂停与续作期间 `Assets/Tests/PlayMode/ShopPlayableTests.cs` 内容发生变化，本任务没有对其发出写操作，保留现状，不回退或归入本次成果。AGENTS.md 去除唯一新增引用后与原文完全一致。
- 收尾 Git 状态另出现 Docs/FIRST_PLAYABLE_GUIDE.md 与 Docs/GAME_DESIGN.md 的改动；本任务未写入这两份文件，作为并行或外部工作区变化保留，不将它们列为本次安装文件。
- Unity MCP 最终只读检查：Editor ready、compiling=false、domainReloadInProgress=false、Play Mode stopped；当前捕获缓冲 Error/Warning 为 0。较早检查曾捕获同一 FindFirstObjectByType 弃用警告两条；任务跨暂停后缓冲已变化，不声称是本次修复。本任务没有清空 Console，没有触发编译、测试、进入 Play 或保存场景。
- 无需手动 Unity Editor 操作。后续项目开发可自动匹配，也可显式写 `$unity-goal-driven-development`；若桌面技能列表未出现，按官方说明重启 Codex，并用新会话读取更新后的 AGENTS.md。真实任务中的选择、执行效果及玩家试玩仍未验证。

## 2026-09-07 — Dev 任务停留在 editor_status 的排查

- 本次仅调查任务执行、连接与日志，保留工作区已有变更；项目内仅补充本条日志，未修改代码、资产、设置或 Play Mode 状态，未运行玩法测试。
- 原任务 01a0789f-4f3f-7333-9012-b4230b300ffb 的最近一轮在北京时间 11:55:38 发起 editor_status，外层 exec 于 11:55:39 返回运行中的 cell 2；Unity 工具已于 11:56:14 成功完成，工具执行耗时约 4.66 ms，返回 playing、compiling=false、domainReloadInProgress=false。之后任务记录没有继续推进。
- Codex 本机 logs_2.sqlite 中，该轮 11:50:35–11:51:38 连续记录模型响应流请求超时并重试 5 次，11:51:56 回退 HTTP，11:55:50、11:56:03 向 Codex 后端 /responses 发送 POST 请求失败。已确认模型请求链路故障；尚不能仅凭这些日志区分代理、本机网络、服务端或客户端连接状态问题。
- 因此，界面停在 editor_status 不代表 Unity 状态查询仍未完成。证据指向模型请求失败及后续执行未推进；外层 cell 的最终结果也未出现在原任务记录中。
- 本次约 12:33 经 Unity MCP 实测：正确连接 XiuXianShop，Unity 6000.6.0f1，仍在 Play Mode，无编译或 Domain Reload；Console 捕获缓冲中有 1 条 UnityConnect Token Exchange 网络错误，无 Warning，未清空日志。该错误不是 C# 编译错误，未证明它导致任务停滞。
- 当前 Codex 用量快照：5 小时窗口已用 23%，周窗口已用 69%，未触达上限。历史记录另有自动审批超时及用量限制，不将其当作当前额度耗尽的证据。
- 建议由用户停止原任务当前轮次后重新继续；如再次复现，检查网络/代理并重启 Codex 后重试。无需根据现有证据重建 Unity 工程或提高 MCP 超时。本次未中断原任务、重启程序或更改连接配置；恢复效果尚未验证。

## 2026-09-07 — 第一阶段可玩经营原型

- 按用户提供的 XiuXianShop_First_Playable_OneShot.md 推进，未扩大到第二产业或范围外系统。
- 新增 ShopPrototype 主场景、ShopCatalog 配置资产、形状格子/交易/炼丹/日期逻辑、中文操作界面和打开场景菜单。Unity 资产由 Editor API 创建，.meta 由 Unity 生成。
- 背包 10×7、展示 6×4、柜台 5×4；7 种占位商品；鼠标拖放、R 旋转、F 水平翻转、Esc 取消与绿色/红色预览。
- 收购牌带来确定的原料供货，展示商品逐件吸引买家；交易验证资金、空间和归属，拒绝及闭店归还未售商品。
- 开局丹炉、一草一露炼一盒 2×2 回气丹；成本 7、出售 18；七日房租及跨日资源保留。
- 修复首次配置导入、拖动抓取锚点及旋转后水平翻转问题。最终分开运行 Edit Mode 16/16、Play Mode 4/4 通过；Play Mode 使用真实输入事件验证两天买卖加工链，非人工试玩。
- 已查看初始界面和供货状态截图。逐项目标证据见 FIRST_PLAYABLE_VERIFICATION.md，试玩步骤见 FIRST_PLAYABLE_GUIDE.md。
- 未新增 Package，未改变原 URP、输入或 Build 设置，原 SampleScene 保留。未实现磁盘存档，退出 Play 重置本局；睡觉可跨日持续经营。
- 运行中一项额外截图操作被自动审批以账户用量限制拒绝，未绕过；该截图不影响已经完成的两天 UI 自动验证。
- Git 本次开始时仅目标书未跟踪；本次没有创建本地提交或推送，所有实现和文档保留工作区。git diff --check 通过；Packages、ProjectSettings、原管线资产和 SampleScene 均未改变。
- 最终 MCP 检查：Editor ready，Play stopped，ShopPrototype 场景已保存、无脏标记、Catalog 引用完整。Console 有 1 条 Unity Connect 登录令牌交换 Exception、0 Warning，未发现原型脚本错误；没有清空日志或调整账号配置。详情见验收记录。

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
