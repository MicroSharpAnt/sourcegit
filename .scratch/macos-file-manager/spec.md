# macOS 文件管理器选择
Status: done

- 偏好设置 → 通用配置新增 macOS 专用选项：Finder（默认）、QSpace、QSpace Pro，保存并立即生效。
- 共用“在文件浏览器中查看”入口遵循设置；目录打开、文件定位选中。其他平台不变。
- 参数独立传递，支持空格、中文、引号；缺失路径不启动，应用启动失败显示错误。
- 本机 QSpace Pro 已通过 open -b com.jinghaoshe.qspace.pro VERSION 实测，并由界面确认 VERSION 被选中。
- 完成编译、参数与配置保存验证、macOS 打包安装，安装后不启动。


## 验证与交付
- Debug build 和 --file-manager 测试通过：默认值、设置持久化、非法值兜底、Finder 文件/目录参数、两个 QSpace 版本、特殊路径、缺失路径。
- 用最终参数 open -a QSpace Pro -- <path> 实测，QSpace 界面确认选中了含中文、空格、引号、美元符号的文件。
- Release ARM64 Native AOT、包签名和安装文件一致性验证通过；沿用本会话缓存依赖/跳过不可用漏洞源的离线构建方式。Avalonia.Controls.DataGrid 有 IL3053 分析警告。
- 已安装 /Applications/SourceGit.app，未启动，保留原配置。
- 备份：/Users/tu/Library/Application Support/SourceGit-backups/20261016-113532-file-manager。
