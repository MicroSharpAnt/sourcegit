# 复制文件名

Status: done

在已有复制路径的文件右键菜单旁增加“复制文件名”。保留扩展名，不包含目录；多选逐行复制，目录项复制末级名称。覆盖工作区、提交详情、文件树、版本对比和储藏文件菜单。保留既有复制路径快捷键。

验证：Release 编译、原生发布、应用签名与安装一致性检查。按本地交付流程备份并安装，不启动应用。

## Comments

- 已实现所有目标菜单与中英繁文案。
- Release Native AOT 发布通过（0 错误；Avalonia DataGrid 的 AOT 分析警告），git diff --check 通过，arm64 应用包签名校验通过。NuGet 漏洞源不可达，本次发布跳过在线漏洞审计。
- 安装受阻：请求当前 SourceGit 正常退出时返回 AppleScript -128（用户已取消）。遵循交付规则停止替换，原应用及配置未改动。待正常退出应用后运行 /tmp/sourcegit-copy-name-install.sh 完成备份、安装和一致性校验。

- 2026-10-26：已正常退出旧实例，完成备份及安装。安装后签名与主程序一致性校验通过，未启动新版。备份：`/Users/tu/Library/Application Support/SourceGit Backups/20261026-144612-copy-file-name`。
