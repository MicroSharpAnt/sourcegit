# 合并上游 SourceGit 2026.23

Status: done

将上游稳定版 `c0f40a2e78d81a7474628847727644fc0c56f558` 合并至本地 master，保留 fork 定制功能与提交历史。验证通过后打包 macOS arm64 Native AOT 应用，备份原应用和配置、安装并按本次明确要求启动。

## Comments

- 2026-10-27：已无冲突 merge；上游新增的 Launcher.axaml 空白行包含行尾空格，随本次验证清理。
- Debug 构建及默认集成测试通过；图片重命名、表格复制、文件管理器、冲突来源、表格设置、列宽换行和同步滚动七个独立入口全部通过。`git diff --check 01714707` 通过。
- 构建有 NuGet 在线漏洞数据源不可达警告和既有 CA1820 测试代码风格警告，0 编译错误；不将功能验证视为已完成在线安全审计。
- Release arm64 Native AOT 发布通过，保留 Avalonia DataGrid 既有 IL3053 分析警告；因已确认在线漏洞源不可达，发布使用 `NuGetAudit=false`。应用与 setsid 均为 arm64，签名验证通过。
- 旧实例的 AppleScript 退出请求返回 -128，改用应用正常退出快捷键 `⌘Q` 后确认进程退出，未强制终止。
- 已安装到 `/Applications/SourceGit.app`，安装后签名、主程序和整个应用目录与发布包一致。源码版本为 `654285a8`。
- 旧应用及配置备份：`/Users/tu/Library/Application Support/SourceGit Backups/20261027-105100-upstream-2026-23`。
- 已按用户本次要求启动；窗口显示 `merge - Default`，原有五个仓库页签恢复，当前仓库分支和提交列表正常加载。
- 本次仅本地合并、提交与安装，未推送远端。
