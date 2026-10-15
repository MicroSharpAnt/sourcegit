# Embedded table display

Status: done

Embed XLSX/CSV cell differences in the existing change comparison region. Remove the extra diff window. Preserve sheets, key column, filtering, original coordinates and synchronized scrolling. Add independent persistent CSV and XLSX display settings, both default true; use them in diff and single-version content previews. Disabled CSV uses text; disabled XLSX uses binary. Provide settings in General and a format-specific shortcut in the diff toolbar. Switching must discard stale async content and release view subscriptions.

## Validation
Build, preference defaults/independence/serialization tests, full integration suite and native UI for inline display, toggles and persistence. Install and restart after validation.

## Comments

- Converted the separate table diff window into a reusable embedded control; retained row alignment, key columns, filters and synchronized scrolling.
- CSV/XLSX settings are independent and default true, shared by differences and single-version previews. General preferences and the comparison toolbar both control them.
- Added request generation guards and paired view lifecycle subscriptions; cache and content publish together on the UI thread.
- Native debug UI verified inline CSV/XLSX differences, table/text or binary toggling, General settings, and immediate single-version XLSX refresh.
- Standards review: no mandatory violations. Spec review: stale reload and live-preview refresh findings fixed and re-reviewed with no further findings. File history subscriptions use window Opened/Closed events, verified in UI.

- Final verification: full integration suite passed; native arm64 publish/signature passed (existing DataGrid AOT warning). Installed application displays real config repository CSV and XLSX differences inline. Preferences independently persisted false/true and were restored to true/true. Backup: 20261015-093226-before-inline-tables.
