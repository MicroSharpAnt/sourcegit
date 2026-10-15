# Excel and CSV cell differences

Status: done

User confirmed: .xlsx and CSV cell comparison, automatic content-based row alignment plus optional key column, side-by-side old/new values, green additions, red deletions, yellow modifications, worksheet switching, only-changes filter. Remain read-only and keep existing preview/text diff.

Match XLSX worksheets by name; added/deleted sheets remain visible. CSV file renames still compare as one table. Columns compare by position (A/B/C); column rename/move and formula/style comparison are not included. Values use the existing stored-value parser.

Automatic mode anchors unchanged rows and aligns edited blocks with bounded work; ambiguous/unbounded blocks must be explicitly indicated rather than silently declaring equality. A selected key column matches rows across position changes. Empty or duplicate keys show a warning and fall back to automatic alignment, never overwrite rows in a key map. Preserve original row numbers on both sides. Sheet additions/deletions and differences remain visible even for empty sheets. Truncated or unreadable files must not be reported as a complete comparison.

Use the exact two Git sources, including commits, index, worktree, renames, added/deleted files, root commits and history comparisons. Expected absent sides are empty tables; read failures are errors.

## Confirmed validation seams

- Table pairs to cell differences: inserted/deleted/modified rows/cells, key-based reordering, duplicate keys and worksheet additions/deletions.
- Git two-source loading including renames and absent sides.
- Build, complete integration tests, actual native UI, then update installed app.

## Comments

- Review baseline: d253fbc2f1e92fd3881a0036d58bfb7dcae2fa68.

- Completed automatic and keyed row alignment, cell colors, sheet union, only-changes filtering, original coordinates and synchronized scrolling.
- Validation: full integration suite passed; Debug build passed without warnings; macOS arm64 native publish and signature verification passed (existing DataGrid IL3053 AOT warning remains).
- Native UI checked XLSX and CSV color rendering and corrected automatic alignment; key selection, only-changes, empty added/deleted sheets, vertical and horizontal synchronized scrolling verified.
- Reviews: standards had no mandatory violations (one optional column-name helper deduplication suggestion); spec ambiguity warning finding fixed and re-reviewed with no further findings.
