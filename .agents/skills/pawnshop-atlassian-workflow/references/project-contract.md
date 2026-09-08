# 项目入口与执行契约

核查日期：2026-09-08。下面的 ID、名称及状态是可重新验证的入口快照，不是永久配置或领取授权。日常只读取当前任务所需资料。

## 项目入口

- 站点：https://zzrzzrzzr11.atlassian.net
- Jira：修仙当铺 / DP，projectId `10100`，软件项目，team-managed；可创建类型本次读到 Task、Story、Bug、Epic、Subtask。必填字段以当次工具返回为准。
- Confluence：修仙当铺 / D，spaceId `425986`；https://zzrzzrzzr11.atlassian.net/wiki/spaces/D
- 本次 Rovo 返回 cloudId `00008e9b-f2d1-407b-898e-4bbb305e64aa`，声明 Jira/Confluence read-write。读取成功不等于写权限实测；新会话重新解析并核对站点。
- 仓库：https://github.com/CrosSoul/XiuXianShop
- 历史已验收 v3：`a8653361296a8f122ccedb700bcb86ec34568107`。本次初始 HEAD 恰与它相同；未来必须读当前 HEAD，不能因此认定通用配方接入已完成。
- 视觉参考：https://www.figma.com/board/8m7hAGOqyp7WUF7oEXZ007 。本次仅保留工作台中的引用，没有读取/编辑画板；具体设计任务需定位相关节点。

| 用途 | Confluence 链接 | 本次入口检查 |
| --- | --- | --- |
| 工作台 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/655361 | 正文已读 |
| GDD | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/393222 | 摘要/元数据已读 |
| UI 规格 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/458759 | 摘要/元数据已读 |
| 游戏数据规范 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/688131 | 全文 Markdown 与 HTML 已读，v:1 |
| 设计决策 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/98306 | 摘要/元数据已读 |
| 开发与验收基线 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/720899 | 摘要/元数据已读 |
| 物品目录 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/database/426177 | CSV schema/视图/记录返回，v:3 |
| 炼丹配方 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/database/458755 | CSV schema/视图/记录返回，v:1 |
| 配方材料与产出 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/database/163850 | CSV schema/视图/记录返回，v:1 |
| 原始资料与迁移台账 | https://zzrzzrzzr11.atlassian.net/wiki/spaces/D/pages/458780 | 摘要/元数据已读 |

## 队列、角色与状态

候选 JQL 见 SKILL.md。必须分页至 isLast，保持服务端优先级顺序。executor-codex 可供 Codex 技术实现，executor-user 由用户操作/验收，executor-chatgpt 由 ChatGPT 策划/文档整理；owner-user 只标识 Epic 项目负责人。角色不映射 assignee。

本次队列：DP-14（Highest）、DP-16（High），均 To Do、assignee=null、parent=DP-3。DP-14 详情无 issue links/评论，但描述中的数据约束仍需满足，不能仅凭空链接判定无依赖。DP-5 为 Done、accepted-v3，是历史证据，不重新领取。

DP-14 本次实际 transitions：To Do、In Progress、Ready for Review、Done，无专用 Blocked；Ready for Review 属 In Progress 类别，因此 statusCategory != Done 仍会返回待验收任务，必须额外排除。未来从该 issue 当前 transitions 读取目标状态对应的 ID，不缓存本次 ID。

Board 100 名为 DP board，type=simple。专用 board 查询返回 DP-14/DP-16，backlog 查询为空；API 同时提示 board 数据可能含 backlog 且短期不准确。因此本次确认了 Board API 返回，不宣称人工看板界面位置/列已验证。

## 工具使用要点

复用当前可用的 Atlassian Rovo 插件工具。它可由插件提供，不一定出现在用户 config.toml 的 mcp_servers 中；不要因 config 没有 atlassian 就重复注册。

- getAccessibleAtlassianResources 解析一次 cloudId；getJiraIssue 读取已知 Key；searchJiraIssuesUsingJql 查询候选。详情字段和评论分页按需要读取。
- 不在当前工具列表的操作用 discover 找确切契约，再调用 executeRead/executeWrite，不猜方法名。已实测的只读操作包括 listJiraProjects、listJiraIssueTransitions、getConfluenceSpace、listJiraBoards、getJiraBoardIssueData。
- Confluence getConfluenceContent 默认 summary，不是全文。规则阅读用 detail=full；编辑前用 HTML 和最新 snapshotToken，按当前 update 工具与 getContentFormatGuide 契约组装差异。数据读取用 content_format=csv，注意返回既有字段定义和视图，也有记录区，不能把整个正文当成只有一张普通 CSV 表。
- Markdown 返回 lossyConversion/lostFeatures 时不能直接拿它覆写；若存在尚未解析而任务必需的宏内容，按当前工具指引读取该宏内容，否则明确资料缺口。当前数据规范 Markdown 报面板宏有损，HTML 已包含面板正文。
- 编辑前遵循当次 Space 指令和版本检查；写后确认状态或正文。评论独立写入并确认，不能依赖 transition 附加 comment 一并成功。
- 连接缺失才检查当前配置与 [Atlassian 官方 Rovo MCP 指南](https://support.atlassian.com/atlassian-rovo-mcp-server/docs/getting-started-with-the-atlassian-remote-mcp-server/)，再按当前支持方式配置。本文没有安装重复 MCP，也不保存凭据。

## 数据合同中的易错点

以 688131 的当前正式正文为准。数据状态：已生效是已核对仓库，待同步是用户确认的新值，草稿不导入，停用也不等于自动删除。

物品/配方/明细各用稳定 ID；配方明细用所属配方ID和物品ID文本键连接，当前不是原生双向 entry-link。数量为正整数；主配方待同步时完整关联明细共同形成目标。缺失分页/明细、重复 ID、未知引用和草图冲突都应阻止不安全应用。

当前形状草图实际含字面量反斜线+n，坐标为机器数据；未来读取需基于实际原始值验证解码，不从经过多层转义的日志数反斜线。耗时/设备为空代表未定。配方“数据已生效”与“接入待接入”可以同时存在，不能误报通用配方已实现。只有数据任务实际完成同步、相关测试和提交后才标同步成功。

## Skill 安装依据

[Codex 官方 Build skills](https://learn.chatgpt.com/docs/build-skills)：项目技能在 `.agents/skills/<name>/SKILL.md`，前言需 name/description，按描述或显式调用加载，openai.yaml 可选。本项目沿用默认隐式调用，用根 AGENTS.md 短路由提高发现性。无脚本、后台服务或同步守护进程。验证记录见仓库 Docs/ATLASSIAN_WORKFLOW_VALIDATION.md。
