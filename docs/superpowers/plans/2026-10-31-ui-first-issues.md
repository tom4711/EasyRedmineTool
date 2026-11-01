# First UI Issues Implementation Plan

> **For agentic workers:** Implementation tasks follow this plan in order. Tests are excluded by the user's explicit instruction; compile verification only.

**Goal:** Implement GitHub issues #33–#35 on `codex/ui-first-issues`.

**Architecture:** Keep Avalonia views bound to their existing view models. Derive the selected navigation state from existing visibility flags, simplify the ticket list header without changing persisted filter behavior, and expose booking status on each favorite row while retaining the global status for broader messages.

**Tech Stack:** .NET 10, Avalonia 12, CommunityToolkit.Mvvm, XAML.

**Spec:** GitHub issues #33, #34, #35 in `tom4711/EasyRedmineTool`.

## Global Constraints

- Preserve cross-platform Avalonia behavior and light/dark themes.
- Keep existing ticket loading, search, favorite, and booking logic intact.
- Do not add or run tests, per the user's instruction; a project build is allowed.

## Review Focus

- Programmatic navigation transitions must update the active nav marker.
- Expanded ticket filters and ID entry must remain reachable at narrow widths.
- Booking outcomes must stay associated with their ticket row and not erase input on failure.
- Filter persistence and the distinction between load filters and local search must remain intact.
- AX automation labels must remain meaningful for icon-only controls.

---

### Task 1: Active navigation — issue #33

**Files:** Modify `src/EasyRedmineTool.Desktop/ViewModels/MainWindowViewModel.cs`, `src/EasyRedmineTool.Desktop/Views/MainWindow.axaml`, `src/EasyRedmineTool.Desktop/Styles/AppButtons.axaml`.

**Interfaces:** Consume existing `Is*Visible` properties; expose a derived active-view name/property for the nav view.

- [ ] Add a derived active-navigation property based on the existing six visibility flags and notify it whenever a view flag changes.
- [ ] Bind each nav button's active class and accessible selected state to that property; add a secondary presentation for Info while keeping it reachable.
- [ ] Compile the desktop project and inspect the diff for all navigation states.

### Task 2: Compact ticket filters — issue #34

**Files:** Modify `src/EasyRedmineTool.Desktop/Views/TicketListView.axaml`; add narrowly scoped UI state to `src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs` only if needed.

**Interfaces:** Preserve existing persisted assignee/status/time-entry filters, load command, local search, ticket-ID command and status/count summary.

- [ ] Move search and result summary into the primary toolbar with assignee, status and load action.
- [ ] Put additional time-entry loading and lookback options behind an expanded “Weitere Filter” section; show when an advanced option is active while collapsed.
- [ ] Make ticket-ID entry reveal/focus on demand and provide a way to close it without changing the query behavior.
- [ ] Compile the desktop project and inspect the diff for narrow-width layout and unchanged bindings.

### Task 3: Row-local booking feedback — issue #35

**Files:** Modify `src/EasyRedmineTool.Core/ViewModels/FavoriteTimeEntryRowViewModel.cs`, `src/EasyRedmineTool.Desktop/Views/TimeEntriesView.axaml`.

**Interfaces:** Keep `IsSubmitting` as the in-flight guard and retain parent `StatusMessage` for overview-level errors.

- [ ] Add row-local validation/status fields and clear stale field errors when corrected.
- [ ] Populate row-local progress and outcome for create requests; preserve entered values after failure and clear comment only on success.
- [ ] Bind inline validation, in-flight state and outcome to the corresponding card with text/icon in addition to color.
- [ ] Compile the desktop project and inspect the diff for existing submission/refresh behavior.

## Branch verification

- Build `EasyRedmineTool.slnx` once after the three tasks.
- Confirm the original checkout remains untouched and the worktree is on `codex/ui-first-issues`.
- Do not commit or push; leave the shared branch ready for review.

## PR review — 2026-10-31

The subsequent user request authorized review and creation of a PR targeting `main`. Independent code review identified and corrected cancellation callback reentrancy, stale series previews, lost row-local booking feedback, mutable booking-date feedback, and swapped save/delete button styles. Two deterministic regression tests reproduced the preview/booking failures before the fixes.

Final verification: all 110 tests passed; the desktop build passed with 0 errors and one existing CA1816 warning; `git -c core.whitespace=cr-at-eol diff --check` passed. Live API writes and a complete light/dark/narrow-window visual acceptance pass were not performed during this review.
