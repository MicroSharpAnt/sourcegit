# 清除索引锁

Status: ready-for-human

用户确认（2026-10-19）：顶部清理按钮旁增加独立清除索引锁按钮。

- 仅操作当前仓库／工作树 GitDir 下的 index.lock。
- 无锁时提示无需清理。
- 点击后确认，提醒结束终端和其他工具中的 Git 操作。
- SourceGit 执行仓库操作时禁止清理，确认执行时重新检查。
- 显示成功或失败结果；不清除其他锁，不自动执行其他 Git 操作。
- 按本地交付技能打包安装，不启动。

## Validation

- Debug build passed; existing integration executable passed (27 PASS summaries).
- Standards review: no findings. Spec review: busy-guard gaps corrected for initial status, external mergetool and unavailable watcher.
- Release uses Native AOT; NuGetAudit disabled because vulnerability feeds were unavailable. This is not a successful online security audit.
- No new test seam was agreed during design; validation uses compilation, existing integration tests and source review. Button interaction remains for user acceptance after installation.

## Delivery

Native AOT ARM64 package built and codesign verification passed at `/tmp/sourcegit-unlock-delivery/SourceGit.app`.
Installation is pending: normal quit returned AppleScript error -128 (user canceled). No app replacement or configuration change was made. Resume installation after the user closes SourceGit normally.
