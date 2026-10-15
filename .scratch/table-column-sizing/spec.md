# Table difference column sizing
Status: done

CSV and XLSX share these read-only comparison interactions:
- Drag column-header boundaries to resize; synchronize corresponding old/new columns.
- Double-click a boundary to fit content from both versions.
- Add a word-wrap switch, off by default; wrapped rows grow to fit both versions and stay aligned.
- Preserve existing scrolling, difference colors, filtering and tooltips.

Validation: build, existing integration and headless scrolling suites, focused UI verification.

## Validation
- Build succeeded with no warnings/errors.
- Headless table sizing: actual boundary drag, bidirectional width sync, double-click fit, wrap toggle, old/new long values, shrinking row height, filtering, scrolling to bottom and back.
- Existing headless scrolling suite passed.
- Standards and spec reviews found no actionable issues.
- Full integration suite passed using the test apphost executable (Git editor subprocesses require it rather than `dotnet <dll>`).
