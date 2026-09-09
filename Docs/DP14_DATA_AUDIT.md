# DP-14 首轮数据核对结果

## 结论

2026-09-09 完整读取 Confluence 数据规范 v6（688131）、物品目录 v19（426177）、配方 v3（458755）、明细 v4（163850）。CSV 包含全部视图、字段定义及记录：**60 件物品、7 张配方、35 条明细**。当前物品和配方均没有标记“待同步”的记录，故本轮 **应用 0 条、修改 Unity Asset 0 个、修改同步状态 0 条**。未导入草稿，未创建通用配方或灵石经济系统。

## 首轮基线：7 件物品、1 张配方、3 条明细

| 稳定 ID | 当前核对结果 | 处理 |
| --- | --- | --- |
| herb、dew、pill、cinnabar、sword、sign | Unity 已实现的名称、类别、基础价值、坐标/草图、说明、颜色、供货、业务招牌及有效广告类别一致 | 保留资产 |
| jade | Confluence 已改为草稿 StorageContainer、3×3；Unity 仍是 Container、2×2，说明也不同 | 未批准待同步，保留原 ID 和原资产 |
| recipe_pill_basic | 原料 herb×1、dew×1 → pill×1 与 Unity 固定演示配方一致 | 保留现有行为 |
| recipe_pill_basic:input:herb、recipe_pill_basic:input:dew、recipe_pill_basic:output:pill | 三个稳定明细 ID、所属配方、物品关系、用途、数量均完整 | 不以显示名匹配、不新增或删除 |

配方目录的 recipe_pill_basic 稳定 ID 尚未写入 Unity；其“数据状态=已生效”表示演示行为基线，“接入状态=待接入”表示通用配方尚未接入。制作耗时与设备字段均为空，不解释为0天、0回合或任意设备。若未来批准通用接入，需要明确字段、设备及时间合同，并评估 CatalogSignature/存档兼容；本轮不进行该迁移。

物品目录新增的属性、材料形态、储存宽高等字段原样保留在快照与报告中。比如 herb 的“材料形态=草药”尚无 Unity 对应字段，不因基础字段相同便宣称新能力已实现。

## 安全校验与未定数据

- 完整目录稳定 ID 无重复或空 ID；35 条明细引用均存在，数量为正整数，用途合法，同配方/用途/物品不重复，每张配方均有原料和产物。
- 草图中的字面量 `\n` 被恢复为行分隔，再与整数占格坐标比对；非空已配置形状一致，无重复坐标或负坐标。
- 14 件草稿形状为空，完整 ID 列表见 audit.json 的 shapeIssues；其中包括 stone_mid、stone_high 和未定设备/食物。保持空值，不生成零格或默认形状。
- 三品灵石唯一稳定 ID 为 stone_low、stone_mid、stone_high，均为草稿。活动目录没有旧属性灵石或 stone_common_*。规范历史段落仍出现旧 stone_common_low，仅作为历史文字保留，以用户当前指令和实际目录为准。
- 不因目录缺行推断删除，不将草稿候选的数量、形状、分类或说明覆盖当前运行配置。根层储存、月度回合、设备生产和灵石动态价值均不在本次实施范围。

## 证据与复现

原始 CSV、规范全文、Unity API 导出的实际 ShopCatalog、逐项差异和校验结果保存在 [Evidence/DP14](Evidence/DP14)。Unity 数据从 AssetDatabase 加载后经 JsonUtility 导出，未手工修改 YAML。

在仓库根目录执行：

```powershell
python -B Docs/Tools/audit_confluence_data.py
python -B Docs/Tools/test_audit_confluence_data.py
```

两个脚本只读取固定快照，无网络或资产写入能力。核对脚本保留原字段和空值并输出 JSON；六项 Python 测试实际 **6/6 通过、0失败、0跳过**，覆盖CSV空值、稳定ID重复、字面换行、非法/冲突形状、未知关系及非法数量、草稿保留与0写入。

DP-14 未执行新的 Unity 测试：针对 Craft 的请求被自动审批以“用量额度耗尽”拒绝；未绕过，未将旧状态文件冒充结果。误复制的旧结果文件已移除。由于没有应用数据或改动 Unity 代码，无同步后运行验证可声称完成。当前相同资产下的 DP-23 Edit 88/88、Play 19/19 是上一项任务证据，包含已有空间不足不消耗原料的回归，可在 Evidence/DP23 查阅，不计为 DP-14 新执行次数。无人工 Play 或 Player 构建。

交付是当前批准数据的真实核对及0变更结果，不宣称完整自动同步器已实现。无需用户手动编辑 Unity；未来新增待同步记录时需重新读取完整最新快照，不能直接应用本次冻结数据。
