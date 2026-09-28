# 内容作者操作步骤（DP-64）

1. 在 Confluence 编辑 04.4–04.7，保留稳定 ID；批准的新内容或修改标记「待同步」。
2. 导出到 `ContentSources/<快照名>/`，四表分别命名 `visits.csv`、`visit-items.csv`、`scenes.csv`、`nodes.csv`。保留完整表头；空表也必须有表头。支持普通 CSV 与 Confluence CSV 的 schema/views/records 包装。
3. 同目录创建 `snapshot.json`，例如 `{"formatVersion":1,"snapshotId":"2026-09-28-v1"}`。这是可读版本标识，不是校验和。
4. Unity 菜单 **XiuXianShop → 内容同步 (Content Sync)**，刷新并选择快照，点击 **Validate**；错误显示表、行、稳定 ID、字段和原因。
5. 点击 **Preview Diff** 检查新增、修改、停用与遗漏保留；无错误才可 **Import**。预览后修改源文件或目标资产，需要重新预览。
6. 检查 Git 中 CSV 与 `Assets/Data/AuthoredContent.asset` 的差异，再 Play。当前只生成静态中间数据，尚无特殊顾客或剧情 Runner 驱动这些记录。

## 同步约定

- 四域共用一个导入入口、一个运行时资产；引用现有 ShopCatalog 的物品、地点和配方，不另造商品目录。
- 草稿不导入；已生效必须与目标一致；停用保留 ID 和旧字段；遗漏保留旧记录。可选空值保留旧值，新记录为空则保持未指定，不猜成 0。不提供隐式清空操作。
- visitId、条目ID、SceneID、NodeID 各自唯一。按 G-15，同一 customerId 可以多次来访，显示名和立绘必须一致；改名时应同步该身份的各条来访。
- 类型使用 `Special / Story`；队列阶段 `BeforeOrdinary / AfterOrdinary`；完成条件 `SceneEnd / TradeSuccess`。04.5 用途编码 `Sell / Carry`；求购类别使用现有 ItemCategory 枚举名称。
- 每个 Scene 的首个有效节点为入口。Choice 选项文本、后继 ID 均为单元格内换行分隔；Branch 两个后继依次为真、假。NextNode 必须在同一 Scene；End 无后继。
- Action 白名单：SetFlag、UnlockLocation、UnlockProfession、UnlockKnowledge、UnlockRecipe；当前 Action值只接受 `true`。地点和配方必须存在于现有 Catalog。职业、知识和实例预设尚无接入目录，非空引用会明确报错，不接受未声明目标。
- 运行时仅使用本地资产，无 Confluence 凭据、联网、热更新或运行时导入。

## 随附源快照的已知问题

`Confluence-2026-09-28` 保留原始导出。`nodes.csv` 的 `alchemy_hint_001` 行存在字段错位：`草稿` 位于 NextNodeID，备注文本位于数据状态。因此当前快照会被拒绝；没有静默移动字段，也没有将草稿作为正式内容导入。修正 Confluence 源表并重新导出后再校验。默认运行时资产保持空内容。

首批仅接入 04.4–04.7；原有物品/配方 DP-14 校验脚本保持独立且未修改。后续其他内容域可添加 ContentDomainAdapter，不需要另建菜单或同步框架。
