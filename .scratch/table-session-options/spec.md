# 表格查看选项在本次运行中共享
Status: done

- CSV/XLSX 单元格差异的「只看差异」在本次应用运行期间共享，切换文件、工作表和重新打开视图均沿用；已打开的视图同步更新。
- CSV 预览的「原文」选择同样共享，经过 XLSX 预览不清除 CSV 的选择。
- 保持现有 CSV/XLSX 显示偏好独立且持久化；新增会话状态不写入配置。
- 验证筛选、跨文件继承、同步与 CSV → XLSX → CSV，再打包安装，不启动应用。

## Validation
- Debug build passed using cached dependencies with NuGetAudit=false (online vulnerability feeds unavailable).
- --table-settings passed: inherited filters, live synchronization, reopen, CSV/XLSX transitions, new preview inheritance.
- --table-preview passed: table differences and preview source/format regressions.
- Release osx-arm64 Native AOT publish and ad-hoc package signature verification passed; Avalonia.Controls.DataGrid emitted IL3053 analysis warning.
- Package: /private/tmp/sourcegit-table-settings.reCRU5/SourceGit.app
- Installed /Applications/SourceGit.app after confirming the previous process had exited. Signature and executable comparison passed. App not launched.
- Old app and configuration backed up to /Users/tu/Library/Application Support/SourceGit-backups/20261016-105616-table-settings.
