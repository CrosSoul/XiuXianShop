# Jira / Confluence 工作流安装验证

日期：2026-09-08。范围仅为项目 Skill 与路由的安装、只读连接验证和本地案例推演。没有执行 DP-14 / DP-16，没有真实状态流转、评论或文档回写。

## 环境与实际读取

- 仓库 origin 为 https://github.com/CrosSoul/XiuXianShop.git，分支 main；初始 HEAD 为 `a8653361296a8f122ccedb700bcb86ec34568107`，初始工作区干净。这是本次快照，不是未来 HEAD。
- 项目已有 unity-goal-driven-development；未发现其他项目 Skill/.codex 配置。用户级自建 Skill 为 ck3-vanilla-research，系统和插件提供其他技能；未修改它们。根规则、既有 Skill、用户 AGENTS 中没有当前强制 Notion 回写条款；旧任务书/日志中的 Notion 链接与历史事实保留。
- 复用已可调用的 Atlassian Rovo 插件；用户 config.toml 的 MCP 名称仅有 node_repl、禁用的 cua_repl、unity，不因此重复配置 Atlassian。Rovo 返回 Jira/Confluence read-write 声明，实际只读调用成功，无 OAuth 需求。没有读取或输出密钥、令牌、Cookie。
- listJiraProjects 核实 DP/修仙当铺、projectId=10100，及真实 Task/Story/Bug/Epic/Subtask 类型；getConfluenceSpace 核实 D/修仙当铺、spaceId=425986 和正确站点。Space 未返回额外 instructions。
- 候选 JQL 一页返回 isLast=true，包含 DP-14（Highest）与 DP-16（High），均为 executor-codex、To Do、assignee=null、parent=DP-3。单独读取 DP-14 全部详情，取得描述、链接、角色、父任务、优先级和空评论/issue links；没有据此声称其设计依赖全部消除。
- DP-14 可用 transitions 指向 To Do、In Progress、Ready for Review、Done，字段为空；未执行 transition。Ready for Review 的类别仍为 indeterminate，说明仅过滤 statusCategory != Done 不够。
- 单独读取 DP-5，确认 Done、accepted-v3 和旧验收提交；未重新领取。
- listJiraBoards 取得 Board 100 / DP board / simple；专用 board 查询返回 DP-14、DP-16，backlog 查询为空。工具提示 board 可能含 backlog 且短期不准确，故未宣称人工界面列位置已核实。
- 工作台 655361 和 DP-14 关联数据规范 688131 已读全文；后者 Markdown 存在宏有损警告，另读 HTML（11,137 字符）核实面板及正文，snapshotToken=v:1。其他给定正式页面已读摘要/元数据并核对 Space；没有将摘要当全文。
- 三个数据库均返回 CSV 字段定义、视图与记录正文：物品 426177、配方 458755、明细 163850。实际字段使用稳定 ID 文本连接，配方接入状态为待接入，耗时/设备为空。这里只证明可读取结构和内容，没有执行导入、规则完整性测试或同步，不能宣称 DP-14 已完成。
- FigJam 仅保留来源链接，未读画板或创建副本。未访问 Notion，也未迁移或删除旧资料。

## 可发现性与格式

- 按 [Codex 官方 Skills 文档](https://learn.chatgpt.com/docs/build-skills) 安装到 `.agents/skills/pawnshop-atlassian-workflow/SKILL.md`，使用必需的 name/description 前言，参考资料位于 references/project-contract.md；无额外脚本、服务或全局安装。
- Codex CLI 0.153.4 实际执行 `codex debug prompt-input '领取下一个 Codex 任务'` 返回 0。JSON 中项目技能目录映射正确，技能列表含 pawnshop-atlassian-workflow 名称、描述及文件路径；AGENTS 段包含新路由和不再写回 Notion 的规则。该命令只装配提示，没有发送模型执行任务。
- 项目入口与描述支持“处理 DP-14”“领取下一个 Codex 任务”“继续开发”“汇报 Jira 进度”“更新正式数据规范”；普通代码补全/无关问答排除。上述真实提示装配证明可发现性，不能证明所有未来自然语言请求都会正确选取并执行 Skill，也不保证旧会话立即刷新。
- skill-creator 的 quick_validate.py 实际尝试后因缺少 PyYAML 失败；未安装全局依赖，未冒称脚本通过。使用实际 Codex 元数据解析及本地 UTF-8、字段、命名、长度、相对链接、路由与 Git 差异检查作为格式证据。

## 本地模拟案例

以下为本次 Codex 在写完 Skill 后按规则逐例进行的本地情境推演，记录实际决策；所有状态与评论动作都是假设动作，没有调用远程写工具。这是规则行为审阅，不是可执行同步器测试，也不是独立代理或玩家人工验收。

| 假设输入 | 推演决策 | 结果 |
| --- | --- | --- |
| 用户要求下一个 Codex 任务；最高优先候选可执行，另有较低优先候选 | 只选第一项；读正文、依赖和当前实现后，真正开工才查询并应用 In Progress transition；不改 assignee，不执行整个队列 | 符合 |
| 仅要求查看 DP-14 | 读取并报告，不领取、不改状态 | 符合 |
| 最高候选被未定设备规则阻塞，下一项可独立执行 | 不猜任意设备；按本轮目标选择可推进项或报告阻塞，不为读到阻塞候选制造状态变化 | 符合 |
| 已开工发现阻塞，当前没有 Blocked transition | 保留实际状态，写清具体阻塞与需要的决定；继续独立部分，不设 Done | 符合 |
| 修复后验收必需测试失败 | 不报通过，不转 Ready for Review；修复或报告实际失败/阻塞 | 符合 |
| 必需自动测试通过、代码仍未提交、尚未人工验收 | 交付评论写真实测试及“仅本地未提交”，提供人工步骤，流转 Ready for Review；不编造 SHA，不设 Done | 符合 |
| 用户明确验收当前交付版本，之后无新变更 | 核对版本/范围与当前状态，使用实时可用 transition 转 Done，并记录授权证据 | 符合 |
| 用户仅验收旧版本，或只说“编译通过” | 不视为当前版本最终验收，不转 Done | 符合 |
| 队列里出现 Ready for Review，statusCategory 不是 Done | 作为等待验收排除，不重新领取 | 符合 |
| 候选带 executor-user / executor-chatgpt，或与 executor-codex 混标 | 不自动领取；指出角色/分工，等待明确重新分工授权；owner-user 不用于改 assignee | 符合 |
| 输入 Done 的 DP-5，未要求重开 | 保留历史证据，只报告完成状态，不重新实现 | 符合 |
| 本地完成但 Jira MCP 不可用 | 保留本地成果及 Key、待写评论、目标状态与错误；不写 Notion，不假称已回写 | 符合 |
| 评论写入超时，可能实际成功 | 恢复后先读近期评论/状态，确认已存在就不重复提交，只补缺失动作 | 符合 |
| 页面读取为有损 Markdown，且最新版本已变化 | 重新取 HTML/快照与工具格式契约，保留嵌入等未改内容；不旧版本整页覆盖 | 符合 |
| 草稿或缺页记录，设备为空，某明细引用不存在 | 保留现有数据，报告缺口；不删记录、不猜 0/设备、不提前标同步成功 | 符合 |
| 完成一次普通开发，正式规则没有变化 | Jira 记录交付，DEVLOG 留工程事实；不建 Confluence 日报，不去 Notion 汇报 | 符合 |

## 交付边界

本次仅新增 Skill、参考契约、本验证记录，增量修改 AGENTS.md 与 DEVLOG.md。原 Skill 与历史指令保留；没有改游戏 C#、场景、Prefab、资源、Package、Project Settings，也没有添加同步器或后台进程。

Unity MCP 只读检查：6000.6.0f1，ready、compiling=false、domainReloadInProgress=false、Play Mode stopped；当前捕获缓冲 Error/Warning 0，未清空。没有为纯工作流安装运行 Unity 编译/玩法测试。

未实测 Jira 评论/状态写入、Confluence 页面/数据库写入、冲突重试或真实完整开发回写；没有明确测试对象，不制造垃圾任务或评论。真实写权限仍待首次已授权业务动作验证。无需用户重新授权或手动 Unity 操作。

没有默认自动提交的项目常驻约定，本次保留本地可审阅变更，不为证明工作流制造 commit；没有推送。下一次可说“领取下一个 Codex 任务”或“处理 DP-14”，届时重新查询当前状态，按依赖选择，不将本次读取当成领取。
