# 多选压缩提交

Status: implemented; native UI acceptance pending

## Confirmed behavior

- Add “压缩提交” to the history context menu for multiple commits.
- Support at least two consecutive ordinary commits on the current attached branch, including a middle segment; replay later commits.
- Show a compact confirmation popup listing the selected commits and an editable combined message, oldest first.
- Reject dirty working trees, root commits, detached HEAD, nonconsecutive selections, commits outside the current branch and merge commits anywhere from the selection through HEAD.
- Allow already-pushed commits with a history-rewrite warning. Never push automatically.
- Revalidate immediately before execution so stale selection/HEAD or dirty work is not overwritten.
- Create a durable recovery reference pointing to the original HEAD before rewriting.
- Reuse SourceGit's interactive rebase editor and conflict continue/abort flow.
- Preserve the final tree and unselected commits' content; the oldest selected commit supplies authorship.

## Confirmed validation

Use the public squash operation in disposable Git repositories for result, rejection, recovery and conflict checks. Verify the menu and popup in the running app.
Review baseline: cb11d68782117ec4590a5863cdf3ffd25e15b1f7 (HEAD before this feature).

## Implementation and verification

- Implemented the multi-selection menu action, editable confirmation popup, validation, recovery branch and interactive rebase integration.
- All 19 integration scenarios pass, including middle/tail selections, invalid states, stale preparation, signing failure, conflict continuation, abort, empty results and linked worktrees.
- Standards and specification reviews reported no blocking findings. Whitespace verification passes.
- Built the native macOS arm64 app and installed it at `/Applications/SourceGit.app`; its executable hash matches `build/SourceGit.app` and code-signature verification passes.
- Native UI automation successfully opens the disposable test repository and selects the two middle commits, but secondary clicks and the keyboard context-menu shortcut do not expose a menu in either screenshots or accessibility output. The menu-to-popup-to-confirm UI path remains unverified; command-level success must not be treated as UI acceptance.
- Manual acceptance: in a disposable repository, select two consecutive non-root commits, right-click **压缩提交**, edit the message and confirm; check that later commits remain and the recovery branch points to the former HEAD. Also check rejection of a nonconsecutive selection.
- The user authorized committing and pushing this feature to `MicroSharpAnt/sourcegit` on 2026-10-14.
