# 仅移动或重命名的图片预览

Status: done

当 Git 报告图片内容完全相同、仅路径变化时，变更对比继续显示图片，并提示“图片内容未变”。覆盖历史提交和暂存区重命名，保留实际内容变化的现有行为。完成后验证、打包并安装，不自动启动。

## Comments

- 复现：merge 仓库提交 45c307eeb 中 img_title_bg_yellow.png 从 Sprite 移至 Texture；Git 返回 R100，没有 Binary files 行，界面落入 NoOrEOLChange。

- 已实现：解析 Git 的 `similarity index 100%`，图片进入预览并显示本地化“图片内容未变”；修正暂存区新图读取旧路径的问题。
- 验证：`--image-rename` 集成测试修复前失败于 NoOrEOLChange，修复后历史/暂存区重命名、文本重命名兼容、真实图片修改共四项通过；`git diff --check` 通过。
- 发布：macOS arm64 Native AOT 发布成功（依赖 DataGrid 的 IL3053 警告），签名验证通过。沙箱内发布连接等待，权限提升后重试成功。
- 已安装 `/Applications/SourceGit.app`，安装后签名和主程序一致性校验通过，未启动。
- 旧应用与配置备份：`/Users/tu/Library/Application Support/SourceGitBackups/20261022-193227-image-rename`。
