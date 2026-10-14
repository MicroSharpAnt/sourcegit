# Excel / CSV table preview

Status: done

Provide read-only .xlsx previews with worksheet switching and CSV table/raw-text switching. Preserve existing diff behavior; cell-level comparison and legacy .xls are out of scope.

Integrate with commit file browsing, file-history content mode, and a preview button in the diff toolbar. Preview the selected revision/index/working-tree source, never silently substitute current disk contents. Deleted files preview the old side. LFS pointers remain explicit; no automatic downloads.

Display row numbers and spreadsheet column letters without consuming the first data row as a header. Use bounded previews with a visible truncation notice; malformed or unsupported content must show an error. XLSX displays stored values, not charts or Excel layout; formulas are not recalculated.

## Confirmed validation seams

- File bytes to table: CSV quoting, embedded newline, empty cells, XLSX sheets and values.
- Git source to preview: committed, staged, working-tree and renamed historical paths.
- Build plus UI smoke check for entry points, worksheet and table/raw switching.

## Comments

- User confirmed .xlsx + CSV, read-only, no cell diff, and the testing seams.
- Review baseline: 43e30425f4b9e48d17217976623a5167778616cd.

## Verification

- `dotnet build tests/SourceGit.IntegrationTests --no-restore -p:UseSharedCompilation=false`: passed, 0 warnings / 0 errors.
- `tests/SourceGit.IntegrationTests/bin/Debug/net10.0/SourceGit.IntegrationTests`: all preview and existing squash integration scenarios passed. Use the apphost executable: existing rebase-editor tests cannot re-enter correctly when launched as `dotnet ...dll`.
- Actual macOS UI: history content renders XLSX and CSV, diff toolbar opens a separate preview, Data → Other changes worksheet/value, CSV table → raw → table preserves content. Tested using an isolated app bundle and temporary Git fixtures.
- Standards review: 0 findings. Spec review: 2 deletion-source findings fixed and rechecked. Also disabled external-open on deleted table history to avoid exporting a nonexistent blob.
- Table preview is bounded and read-only. It shows stored values rather than Excel formatting/charts, does not recalculate formulas or download LFS content. CSV accepts UTF-8 and BOM-detected Unicode; unsupported encodings report errors.
- Native AOT publication and replacing the installed application were not performed.
