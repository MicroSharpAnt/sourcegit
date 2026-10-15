# Synchronized table scrolling

Status: done

Keep corresponding rows and columns visible in both embedded table comparison grids when either side scrolls with wheel/trackpad, scrollbar or keyboard. Preserve alignment through filtering, reload and resizing. Verify actual rendered rows as well as scrollbar offsets with real DataGrid layout in a headless regression test; build, install and restart.

## Validation

- Regression command: `tests/SourceGit.IntegrationTests/bin/Debug/net10.0/SourceGit.IntegrationTests --table-scroll`.
- Before fix, second wheel input failed: left offset 300, right 150. Root cause: DataGrid VerticalScroll reports a wheel delta before layout, not an absolute offset.
- After fix, follow committed ScrollBar.Value for both axes. Template replacement detaches previous subscriptions. Existing guard prevents feedback.
- Real Headless DataGrid layout verifies scrollbar values and realized row identity/position for repeated wheel input, either side, both axes, selection reveal, keyboard PageDown, thumb movement, boundary, resize and filtering.
- Debug build has zero warnings/errors; regression and full integration suites passed. Standards and Spec reviews: no findings.
- Native arm64 build/signature passed with existing DataGrid AOT warning. Installed binary matches build; application restarted successfully. Native automation input was unreliable, so scroll behavior was verified in the headless real-control test rather than claimed as a native manual check.
- Previous application/settings backup: `20261015-094808-before-scroll-sync`.
