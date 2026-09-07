# Game 视图清晰度排查（2026-09-08）

对应 Notion 任务：[排查 Game Mode Play 字体模糊与低分辨率](https://app.notion.com/p/3d45330126988147894afea9bbee13a5)。范围为当前 Windows Unity Editor 的预览清晰度，不包含正式 UI 重构。

## 结论与修复

Unity 6000.6.0f1 的 Game 视图处于 Free Aspect，启用了 **Low Resolution Aspect Ratios**。本机 Editor 像素缩放约为 1.145833，低分辨率画面随后被放大，造成文字细节损失。已通过 Unity MCP 调用 Editor API 关闭当前 Game 视图的该选项。

同一个 Game 窗口、同一初始场景，切换开关后再切回复核：

| 读数 | 开启低分辨率（原状态） | 关闭低分辨率（修复后） |
| --- | --- | --- |
| 实际 Screen 尺寸 | 1743×912 | 1997×1045 |
| Game 视图缩放 | 1.145833 | 1.0 |
| Canvas scaleFactor | 0.912127 | 1.045146 |

当前窗口恢复原生像素显示。修复是本机 Editor 布局/预览状态，不能通过 Git 为其他电脑或新布局自动应用；没有创建强制修改个人 Editor 设置的脚本。

## 排除项与限制

- 当前管线 XiuXianShop_2D_RPAsset，Quality 为 PC，URP Render Scale=1。
- Main Camera 的动态分辨率关闭，没有目标 RenderTexture。
- UI 为 Screen Space Overlay，CanvasScaler 为 Scale With Screen Size / Expand，参考尺寸 1600×1000。这是布局参考尺寸，不是锁定的输出分辨率。
- 当前文字使用动态系统字体，候选为 Microsoft YaHei、SimHei、Noto Sans CJK SC、Arial；运行中中文正常显示。未更换字体或修改字体导入设置。
- 窗口缩小时，整套固定布局和小字号仍会一起缩小。此次消除低分辨率再放大的因素，不代表已完成小窗口适配、字号与对比度重设计。
- 未验证其他电脑、显示缩放组合及独立 Player 构建；历史反馈没有原始截图，不能认定所有历史模糊现象都只有这一原因。

Unity 官方说明该选项会降低按宽高比预览的分辨率：[Game 视图手册](https://docs.unity3d.com/cn/2021.3/Manual/GameView.html)。本项目使用的属性及实际变化已在 6000.6.0f1 中独立检查。

## 实际画面证据

以下是 Play Mode 帧结束后由 ScreenCapture 捕获的原始 PNG，包含 Overlay UI；查看时请用图片查看器 100% 缩放。MCP screenshot 当前实现只渲染相机，遗漏 Overlay UI，因此未使用它产生的空背景作为清晰度证据。

![原低分辨率画面](Images/display-low-resolution.png)

![修复后原生分辨率画面](Images/display-native-resolution.png)

## 开发者复查

1. 打开 ShopPrototype 场景并 Play，展开 Game 视图的 Free Aspect/分辨率下拉菜单，确认 Low Resolution Aspect Ratios 未勾选。本次已设置，无需重复配置。
2. 保持 Game 视图 Scale 为 1x，查看中文说明、物品标签、顾客预算；需要更多空间时最大化 Game 窗口。
3. 拖动物品到展示柜，再点击开始营业，确认鼠标命中与格子位置一致。
4. 若换布局或换电脑后再次模糊，先复查该预览选项。使用固定分辨率预览时，避免把低分辨率输出放大后当成实际清晰度。

自动验证及最终 Console 状态见 DEVLOG 本日记录；人工舒适度验收由开发者按上述步骤完成。
