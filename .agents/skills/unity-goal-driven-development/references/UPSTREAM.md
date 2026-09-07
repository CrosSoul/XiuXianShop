# 来源与适配记录

核查日期：2026-09-07。本文件为维护与溯源资料，不是另一套开发指令。

## 上游与许可

- 仓库：[multica-ai/andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills)。本次 GitHub API 返回的 main 提交为 `2c606141936f1eeef17fa3043a72095b4765b9c2`。
- 上游 `.claude-plugin/plugin.json` 的作者和 marketplace 所有者为 `forrestchang`；README 中部分安装地址仍使用该旧命名。保留这项作者信息，不将仓库组织名当作原作者姓名。
- 思想来源：[Andrej Karpathy 的观察](https://x.com/karpathy/status/2015883857489522876)，按上游署名保留；本次未另行核实原推文全文。本项目版本由 Codex 按项目开发者要求改编，不是 Karpathy 或 OpenAI 官方发布的 Skill。
- 上游 README、SKILL.md 及 plugin.json 均明确声明 **MIT**，本 Skill 保留 `license: MIT`。
- 已检查上述提交的完整递归文件树：没有独立 LICENSE/COPYING 文件，也没有可逐字保留的完整 MIT 许可正文或版权年份声明。因此保留实际存在的许可声明、作者和来源，不伪造版权持有人、年份或声称复制了不存在的 LICENSE。后续若上游补充许可证正文，更新此记录并保留该正文。

## 实际结构与研究范围

已联网阅读全文：README.md、CLAUDE.md、skills/karpathy-guidelines/SKILL.md、EXAMPLES.md、CURSOR.md、.cursor/rules/karpathy-guidelines.mdc，以及 .claude-plugin/plugin.json 和 marketplace.json。文件树另含 README.zh.md，是中文说明入口；英文 README 与 Skill 正文作为本次规则依据。未执行仓库脚本或安装命令。

原版确实包含独立 Skill，YAML 前言已有 name、description、license；CLAUDE.md 与 Skill 的四条核心规则基本一致。Cursor 规则也复制相同原则，并设 alwaysApply: true。插件清单声明版本 1.0.0，并把 skills 指向 ./skills/karpathy-guidelines。它们是不同宿主的分发与加载方式，不可混用。

| 原版思想/做法 | 本项目处理 |
| --- | --- |
| Think Before Coding：理解问题、公开假设、说明歧义与更简单方案 | 保留；普通技术选择自主决定，只有重大玩法/破坏性/产品歧义暂停相关工作 |
| Simplicity First：避免推测性功能、一次性抽象和过度配置 | 保留；以完成目标为最小单位，允许必要交互、边界与 Inspector 参数，不以行数或字面要求裁掉完整性 |
| Surgical Changes：范围可追溯、遵循现有风格、只清理自身引入的冗余 | 保留；补充 Unity 序列化、GUID、Prefab 覆盖、回调及 Inspector 引用保护 |
| Goal-Driven Execution：可验证目标与迭代 | 保留；持续至授权里程碑，分层汇报证据，工具受阻不伪称可玩 |
| 不确定就问、困惑就停；示例中每阶段再询问 | 改为按影响分级，避免把普通实现问题反复交还策划 |
| 测试先行与极简代码示例 | 采用复现与针对性验证思想，不强制每次新建测试；EXAMPLES 的排序示例仅断言分数，未断言同分顺序，不能照抄为有效回归证据 |
| Claude marketplace 命令、CLAUDE.md、.claude-plugin | 不安装；使用 Codex 项目 Skill 目录与既有 AGENTS.md 的短引用 |
| Cursor .mdc、alwaysApply、个人技能目录 | 不迁移；不把 alwaysApply 当作 Codex 字段 |

原版适合用作轻量原则底稿，不能原样当成 Unity 自主开发流程；尤其其“有疑惑就问”和示例中的重复确认不符合本项目分工。

## Codex 官方依据与安装选择

- [Build skills](https://learn.chatgpt.com/docs/build-skills)：项目技能从工作目录到 Git 根目录逐层扫描 `.agents/skills`；Skill 必须有 SKILL.md 及 name、description 前言，正文按需读取；支持描述匹配和显式调用。`agents/openai.yaml` 是可选配置，隐式调用默认允许。本次没有添加可选 UI 文件、脚本、插件清单或全局配置。
- [AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md)：启动时按项目根目录到工作目录组合指令；同层优先 AGENTS.override.md，再 AGENTS.md，之后才是已配置的备用名称。本文不通过改名 CLAUDE.md 或配置备用文件来复制上游规则。
- 本项目安装在 `.agents/skills/unity-goal-driven-development/SKILL.md`。已有 AGENTS.md 增加单条条件引用，要求 Unity 开发任务读取 Skill；该链接是给代理的阅读指令，不是特殊 include 语法，也不是 Skill 注册所必需的配置。
- 后续正常开发靠清晰的 description 匹配与项目条件引用使用；CLI/IDE 也可在提示中写 `$unity-goal-driven-development`。官方称 Skill 变更会自动检测，未出现时重启 Codex。AGENTS 指令链需要新一轮启动/新会话读取，不保证正在运行的旧会话立即刷新。
- 安装与格式检查不等于实际行为验证；本次实际检查结果及尚未验证事项记录在项目 Docs/DEVLOG.md。
