# Table content copying
Status: ready-for-agent

Confirmed: table preview and both table difference panes support single-cell and rectangular selection (drag or Shift extension), Cmd+C/Ctrl+C and a localized Copy context menu. Copy full stored values, preserve rows/columns for pasting into Excel, including quoted tabs/newlines. Each difference pane copies its own values. Selection must reset on sheet/filter/data replacement and remain correct across virtualization. Preserve scroll synchronization and column resizing.

Validation: exercise the approved pointer/keyboard/menu interactions and clipboard output through the public UI; run existing table sizing, scrolling and integration checks. Local delivery includes a signed macOS build and backed-up reinstall without launching.

## Validation and delivery

- Implemented shared rectangular selection and full-value TSV copying in preview and both comparison panes.
- Passed UI clipboard checks (both modifiers, reverse drag, Shift click/arrow, menu preservation, sheet/filter resets, virtualized rows, old/new and missing values).
- Passed table sizing, synchronized scrolling and full integration suite.
- Standards review: no actionable findings. Spec review: no confirmed violations; identified coverage gaps were tested before delivery.
- Native ARM64 package built and signature verified: `/private/tmp/sourcegit-copy-delivery.pW52ia/SourceGit.app`.
- Installation pending: normal quit returned application error -128 (user canceled); existing app has not been replaced. Resume `/tmp/sourcegit-copy-install.sh` after the user exits SourceGit. Do not launch after installation.
