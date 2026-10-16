# 冲突来源显示
Status: done

- 冲突概览与内置编辑器以实际来源、分支/提交名替换 Mine/Theirs，采用按钮与两侧对应。
- 显示 hash 末 8 位（加省略号，避免误作 Git 前缀短号），保留完整 hash 提示和提交摘要。
- 合并：当前分支 / 合入分支；变基：目标侧当前内容（真实 HEAD）/ 正在重放的分支及提交；拣选：当前分支 / 正在拣选的提交。
- 撤销与暂存/补丁等场景使用准确保守的说明，不伪造分支或内容来源。
- 只更改展示，采用哪一侧的行为保持不变。双侧采用顺序用左侧/右侧表述。
- 已确认测试边界：冲突视图模型的来源与 hash；界面绑定一致性；不测试外部合并工具。
- 审查基准 ed985187；完成后按本地交付技能安装，不启动。


## 验证与交付
- Debug build、冲突来源/界面绑定测试和完整集成测试通过。stash 审查修复经过先失败再通过的回归验证。
- Standards：0 项发现；Spec：1 项 stash 本地内容被错误归属 HEAD，已修复，复审剩余 0 项。
- Release osx-arm64 Native AOT、签名、安装后可执行文件一致性检查通过。使用缓存依赖并跳过不可用的在线漏洞审计；Avalonia.Controls.DataGrid 有 IL3053 分析警告。
- 安装到 /Applications/SourceGit.app，未启动。
- 备份：/Users/tu/Library/Application Support/SourceGit-backups/20261016-112211-conflict-sources。
