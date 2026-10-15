# Swap staged and unstaged panels

Status: done

Add a persistent General preference, default false, to swap the unstaged and staged panels in WorkingCopy. Apply immediately to open views. Keep each panel's toolbar, selection and actions together and retain the separator and resizing. False preserves unstaged on top, staged below.

Validation: build and real view layout check for default, enabled and restored states; verify preference persistence. Install and restart with default off.

## Validation results

- Debug build passed with zero warnings/errors; native arm64 publish/signature passed (existing DataGrid AOT warning).
- Temporary headless check loaded the real WorkingCopy view. Off: unstaged Y=36, staged Y=369; on: unstaged Y=369, staged Y=36; off restored original coordinates. Default false and JSON round trip verified.
- Installed binary hash matches build. Restarted and confirmed the new checkbox in General is visible and off. Left settings open for the user.
- Previous application/settings backup: `20261015-095314-before-swap-panels`.
