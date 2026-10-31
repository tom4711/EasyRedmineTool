# Time Entry Only Ticket Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When time entry filtering is enabled, show only tickets with the current user's time entries in the selected lookback period.

**Architecture:** Make the existing time entry option exclusive in `TicketService`, retaining the existing API query and cache. In `TicketListViewModel` and `TicketListView.axaml`, expose the mode clearly and disable assignee/status controls while keeping local search and lookback selection usable.

**Tech Stack:** .NET, C#, Avalonia XAML, CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-10-31-time-entry-only-ticket-filter-design.md`

## Global Constraints

- Include only tickets found through the current user's time entries in the selected 3, 6, 9, or 12 month period.
- Disable and gray the assignee and status selectors in time entry only mode.
- Keep local ticket search and time entry period selection active.
- Preserve the existing persisted setting so existing configurations continue to open the time entry mode when it was enabled.
- Do not add or run tests unless requested; verify with a build and manual UI inspection.

## Review Focus

- No time entries in the period: show an empty result, not the regular ticket query.
- Tickets with entries outside the selected period: exclude them from the time entry only results.
- Duplicate entries for a ticket: show the ticket once.
- Assignee/status selectors: disabled while time entry mode is active and re-enabled when it is off.
- Search: still narrows the loaded time entry tickets locally.

---

### Task 1: Make the time entry option exclusive

**Files:**
- Modify: `src/EasyRedmineTool.Core/Services/TicketService.cs`
- Modify: `src/EasyRedmineTool.Core/ViewModels/TicketListViewModel.cs`
- Modify: `src/EasyRedmineTool.Core/Models/Tickets/TicketLoadFilter.cs` only if a clearer filter property is needed
- Modify: `src/EasyRedmineTool.Desktop/Views/TicketListView.axaml`

**Interfaces:**
- Consumes: the existing `ITicketService.GetTicketsForListAsync` and current user's paginated time entries.
- Produces: when `IncludeTimeEntryTickets` is true, `TicketListLoadResult.Tickets` contains only matching time-entry tickets; `OpenTicketCount` is zero and `TimeEntryTicketCount` equals the unique accepted ticket count.

- [x] **Step 1: Change the service query path** so the normal `GetIssuesAsync` request is skipped when time entry mode is enabled; collect unique issue IDs from entries returned for the selected period, load those issues, and keep the existing assignee/status match out of this exclusive mode.
- [x] **Step 2: Update the result status text** to say that only tickets with the user's time entries in the selected number of months were loaded, including when the result is empty.
- [x] **Step 3: Update the ticket list UI** to label the option “Nur Tickets mit eigenen Zeitbuchungen anzeigen”; bind the assignee and status selectors' `IsEnabled` to the inverse mode state and retain the date-range buttons and local search.
- [x] **Step 4: Build and inspect the changed behavior** with `dotnet build src/EasyRedmineTool.Desktop/EasyRedmineTool.Desktop.csproj --no-restore -p:DisableGitVersionTask=true -p:UseSharedCompilation=false`; build passed with 2 existing analyzer warnings and no errors. The app bundle was updated and its UI confirmed the new label and disabled assignee/status selectors. `git diff --check` passed. No tests were run; a test-project build attempt stopped because `project.assets.json` is missing and restore was not performed.

## Execution Notes

- The exclusive service path skips the all-issues request and loads unique issue IDs from the current user's time entries.
- Review finding fixed: an empty saved result in time-entry-only mode no longer falls back to unrelated cached tickets on restart.
- Review finding fixed: existing test references and the former additive behavior expectation were updated to the new exclusive filter contract; tests were not run.
- The current local app was left open on the Tickets view with time-entry-only mode active and assignment/status controls disabled.

## PR review — 2026-10-31

The subsequent user request authorized review and creation of a PR targeting `main`. Independent code review identified and corrected cancellation callback reentrancy, stale series previews, lost row-local booking feedback, mutable booking-date feedback, and swapped save/delete button styles. Two deterministic regression tests reproduced the preview/booking failures before the fixes.

Final verification: all 110 tests passed; the desktop build passed with 0 errors and one existing CA1816 warning; `git -c core.whitespace=cr-at-eol diff --check` passed. Live API writes and a complete light/dark/narrow-window visual acceptance pass were not performed during this review.
