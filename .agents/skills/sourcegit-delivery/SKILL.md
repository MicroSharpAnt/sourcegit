---
name: sourcegit-delivery
description: Complete SourceGit project requests by validating changes, packaging the macOS application, reinstalling it locally, and verifying the restarted app. Use for every completed change in this repository and explicit install requests.
---

# SourceGit 本地交付

## 默认完成条件

用户已明确要求：本项目每次需求实现完成后，都自动打包、重新安装并启动验证，再报告完成。此约定持续有效；无需再次询问是否安装。用户当次明确要求仅分析、仅修改或暂不安装时，以当次要求为准。

代码、测试或提交完成只是中间步骤。完成交付需要：
- 相关验证通过，发布产物与本次最终源码一致。
- 新版安装到本机 `/Applications/SourceGit.app`，旧应用与配置有可恢复备份。
- 已安装的应用正常启动，原有仓库页签和偏好保留；涉及 UI 时验证本次功能。
- 最终回复简述实现结果、验证结果和安装状态；若实际受阻，明确未完成哪一步。

## 构建与验证

从仓库根目录执行，按当前机器架构选择 macOS RID（Apple Silicon 为 `osx-arm64`）。参考 `build/README.md` 和 `build/scripts/package.osx-app.sh` 的当前布局。

1. 运行与改动相关的检查。集成测试用编译出的 `tests/SourceGit.IntegrationTests/bin/Debug/net10.0/SourceGit.IntegrationTests` 可执行文件启动；`dotnet <dll>` 会导致 Git editor 子进程用错程序。表格 UI 的独立入口为 `--table-sizing`、`--table-scroll`。
2. 将 Release 原生发布输出放到新建的临时目录，避免混入旧文件：
   ```sh
   dotnet publish src/SourceGit.csproj -c Release -r osx-arm64 -o "$publish_dir" --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:UsedAvaloniaProducts=
   ```
   `publish_dir` 须先设为本次独立目录。保留项目默认 Native AOT。`UsedAvaloniaProducts` 空值用于跳过构建遥测；仅在离线漏洞源阻断且依赖已缓存时，可额外传 `-p:NuGetAudit=false`，并区分离线发布与安全审计。
3. 按打包脚本组装临时 `SourceGit.app`：发布文件放入 `Contents/MacOS`，复制应用图标，使用 `VERSION` 替换 plist 模板版本，编译匹配架构的 `tools/setsid-macos/setsid.c`。避免直接运行会移动固定构建目录的脚本而覆盖其他产物。
4. 执行 `codesign --force --deep --sign -`，再用 `codesign --verify --deep --strict` 验证包，并检查主程序架构。成功后再开始替换安装。

## 安装与启动

1. 确认正在运行的是哪个 SourceGit 实例；若从构建目录启动，也应正常退出，避免旧实例接管新版本启动。等待正常退出；无法退出时停止替换并报告，保留用户进行中的操作。
2. 在用户 Library 的独立时间戳备份目录中保存旧应用和 `~/Library/Application Support/SourceGit` 配置。替换前备份必须成功，配置留在原位置。
3. 将已校验的包安装到 `/Applications/SourceGit.app`；核对安装后的签名和主程序与发布产物一致。复制或校验失败时恢复旧应用，保留失败产物供排查。
4. 启动准确的安装路径，确认窗口可用，并检查相关交互。用完整应用路径定位 UI：备份副本可能导致 bundle identifier 匹配多个应用。

需要写入 `/Applications`、用户 Library 或访问系统构建工具时，使用工具的权限提升流程。只在工具实际阻断时报告具体原因；不要把通常的安装授权再交回用户确认。

## GitHub

本技能的持续授权涵盖本地构建、安装与重启。推送按用户当次指令或已有明确推送授权执行；确认远端和分支，只提交本次相关文件，正常推送并验证远端提交。保留无关未提交文件，避免强制推送。
