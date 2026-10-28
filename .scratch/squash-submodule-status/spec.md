# Align squash and local changes submodule status

Status: done

## Scope

Use `--ignore-submodules=dirty` for squash preparation and execution revalidation, matching the local changes list. Ignore files modified or untracked inside submodules, while continuing to reject changed submodule commit pointers and staged/unstaged/untracked files in the parent repository. Do not alter files in the user's merge repository or execute a squash there.

## Validation

Exercise the public squash command in disposable repositories with untracked and modified submodule files, and unstaged/staged changed submodule pointers. Compare acceptance with QueryLocalChanges and verify file contents, status and final tree are preserved. Run the existing integration suite, then package and install the native macOS app without launching it.

## Results

The new untracked-submodule case failed against the old implementation and passed after switching the status flag. All four new submodule cases and the existing integration suite pass. Production code changes only the status flag shared by preparation and execution revalidation.
