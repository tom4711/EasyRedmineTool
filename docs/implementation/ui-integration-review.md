# UI/application integration review

Reviewed 2026-11-01 against baseline `1df316bc3879c6348b83e49cfe02b138ab04a091`, using `/private/tmp/ert-ui-integration-review.diff` and current source, including new lifecycle/connection tests and the desktop headless test project. Scope: plan tasks 2/4; F01–F05/F09–F11. Settings persistence and API internals were reviewed separately; this review assumes initialized snapshots and asynchronous writes completed by `FlushAsync`.

This was an independent static review. No tests were rerun while the implementer's all-suite run was active; no live Redmine calls or writes were made. Earlier reported passing groups were core 36, series 19, transport 40, persistence 25 and headless 2; these are implementer-reported results, not evidence that the races below are covered. Source line references describe the reviewed snapshot and may move during remediation.

## Initial findings

### UI-1 · P2 · Reactivated card can join its canceled form load

`FavoriteTimeEntryRowViewModel.cs:99–113` caches one task created with the first viewport's cancellation token. `TimeEntriesView.axaml.cs:38–54` cancels that token on unload/recycling and immediately invokes `EnsureFormLoadedAsync` on activation. A second activation while the first task is still pending joins the canceled task. If it throws, the new visible card never starts another load. If the service ignores cancellation and returns normally, `LoadActivitiesAsync` exits at its cancellation guard and the second waiter marks `_formReady=true` despite empty activities/custom fields. The card can then remain unusable for the session.

Minimal correction: make cancellation completion explicit and retry a canceled prior attempt for a still-active waiter, without clearing/replacing a running load or exceeding the semaphore bound. Only mark ready after a complete successful load. Regression: gate a cancellation-ignoring activity request; cancel first activation; reactivate before completion; release the first request; verify the current activation loads usable data and future activation reuses that successful form. The headless recycling test currently releases activities before scrolling, so it misses this case.

### UI-2 · P2 · Today refresh replaces pending edits and their busy identity

`TimeEntriesViewModel.cs:275–284` recreates every `TodayTimeEntryRowViewModel` after a successful GET. `MainWindowViewModel.OpenTimeEntries` invokes that GET on every navigation back. While an edit/delete request is pending, the refreshed server snapshot can still contain the old entry. The replacement has `IsBusy=false`, no edit draft and a new command instance, allowing another mutation while the original is pending. The same replacement loses an unsaved Today edit during navigation or a refresh triggered by another card.

Minimal correction: reconcile Today rows by connection generation + entry ID, preserving active edit and mutation state; update the remote snapshot separately without overwriting drafts. Regression: delay a Today PUT, reload the old GET snapshot, assert same row/busy state and one mutation; separately preserve unsaved hours/comments/activity/custom fields across a reload. Existing identity coverage only exercises favorite creation rows.

### UI-3 · P2 · Today post-commit warnings and success disappear during refresh

`TodayTimeEntryRowViewModel.cs:207–218` sets a local persistence warning and then refreshes. The row replacement described above discards that warning even when refresh succeeds. More generally, `RefreshAfterCommitAsync` (`:267–270`) only detects thrown exceptions, whereas `TimeEntryService.GetMyTimeEntriesAsync` normally reports GET failures as `Success=false`. The parent's failure branch (`TimeEntriesViewModel.cs:259–265`) clears the rows and overwrites the successful edit/delete message with the GET error; the helper neither preserves success nor displays its separate warning.

Minimal correction: preserve warnings on reconciled rows, propagate a refresh outcome, retain the last known snapshot on GET failure and show post-commit refresh failure separately from committed mutation success. Delete warnings require a parent-level location because the deleted row is removed. Regression: successful edit + defaults `Update`/`Flush` failure + successful refresh must leave the warning visible on the active row; successful edit/delete + failed GET result must retain an explicit success outcome and a distinct refresh warning. The existing edit test inspects the detached row and only checks that a GET occurred.

### UI-4 · P2 · Delayed defaults flush can apply A's booked dates to ticket B

`SeriesBookingViewModel.cs:679–703` checks `bookingPreviewVersion` before awaiting `FlushAsync`, then checks only connection identity before refreshing with `bookedDates`. `IsBooking` is already false (`:669–672`). Change ticket A to ticket B while Flush is gated, allow B's details to load, then complete Flush. `RefreshPreviewCoreAsync` reads B's current ticket/range and unions A's just-booked dates into B's conflicts, deselecting otherwise new B dates. Changes to range/selection can likewise be overwritten by the obsolete continuation.

Minimal correction: recheck the captured preview generation after Flush and after the automatic refresh before applying subsequent messages/state, and skip post-work when inputs changed. Regression: book A successfully, gate Flush, select B, complete B details and then Flush; verify no A conflict/selection is published to B and no stale summary overwrites newer view status.

### UI-5 · P2 · Old status-load failures replace the new connection's filter options

`TicketListViewModel.ReloadStatusFilterOptionsAsync` guards connection identity only on its successful response path. Its `RedmineApiException` and generic catch handlers unconditionally rebuild default options and restore selection. Gate an A status request, switch to B and complete B's statuses, then fail A: B's specific options disappear, its selected specific status falls back to Open, and A's error can overwrite B's status. This violates the requirement that late A results cannot alter B's active view, including failure results.

Minimal correction: apply the same identity/cancellation guard before publishing error fallback state; use a status-load generation if requests may return after A→B→A. Regression: pending A status request fails after B's successful status publication; B options/selection/status must remain unchanged.

## Areas with no additional blocker found

- Favorite creation retains row/command identity across reloads, snapshots submitted comments/custom fields, and preserves a next comment draft typed during POST. Connection generation rejects old A→B→A results.
- Series marks successful dates immediately, clears the booking guard before automatic refresh, and notifies `CanBook` at busy transitions. Existing tests cover partial successes and persistence/preview failures.
- Connection save resets weekly/series state and reloads ticket/favorite context before awaiting disk persistence; old mutation commands guard the captured identity.
- App initialization awaits settings before resolving view models. Background persistence failure is marshaled onto the UI thread and event subscriptions are removed on disposal.
- The ListBox has a finite layout constraint and the headless test checks realized-container counts, demand loading and completed-form draft preservation. The additional cancellation case above is still necessary.
- The composition root explicitly sets a five-minute pooled connection lifetime, with a test inspecting the actual factory handler. No DNS outage is claimed.

## Disposition

UI-1 and UI-2 were accepted by the implementer for correction while this review was being finalized. UI-3 through UI-5 were sent to the implementer as additional blockers. This document records the review findings; final verification and resolution should be appended after fixes and the full suite complete.

## Focused follow-up review · 2026-11-02

Reviewed the five corrections and their targeted regression source without rerunning tests or expanding the original scope. Release verification remains the implementer's responsibility.

- **UI-1 resolved by inspection:** a canceled joined load is retried for an active waiter, running tasks retain ownership until completion, and the parent checks cancellation after the loader returns. `Reactivating_card_while_old_viewport_load_cancels_retries_form_load` covers the cancellation-ignoring first request and reuse of the successful retry.
- **UI-2 resolved by inspection:** Today rows are reused by entry ID within a connection generation; `UpdateEntry` preserves editing/busy state. Both reload entry points synchronize connection state. Tests cover row/draft preservation, a direct Today-only connection change, and a delayed PUT retaining the same busy row with exactly one request. The committed-field follow-up below is also resolved.
- **UI-3 original mechanism resolved:** failed GET results retain the last snapshot; the post-commit helper restores the success message and publishes a separate parent warning. Successful deletes are removed immediately and cannot be restored by stale GET results. Reused rows retain persistence warnings. `Refresh_failure_after_edit_preserves_success_and_warns_without_removing_entry` exercises the failed-result path.
- **UI-4 resolved by inspection:** the booking continuation checks both captured input and preview-load generations after Flush and after automatic refresh. Six added cases cover a new ticket or preview during delayed Flush, failed Flush, and obsolete refresh completion. The implementer reports six failing regressions before correction and 25 passing series tests afterward; this reviewer did not independently execute them.
- **UI-5 resolved by inspection:** success and both exception fallback paths now require current identity and status-load generation. Two exception variants exercise the delayed A failure after B succeeds; the generation additionally rejects same-identity obsolete requests.

### Resolved follow-up within UI-2/UI-3 · committed custom-field snapshot

The reconciled Today row's `UpdateEntry` skips its own post-commit GET while `IsBusy=true`. The initial correction copied committed hours/comments/activity into `_entry` but omitted custom fields, so reopening could restore the old field value. The final correction copies submitted field IDs, names and trimmed values into `_entry.Custom_Fields` before `IsEditing=false`. `Today_edit_reopen_preserves_the_committed_custom_field_snapshot` changes `old`→`new`, saves and immediately reopens without an unrelated later refresh, asserting `new`. This exact source correction and regression were inspected. The implementer reports the regression failed before the correction and passed afterward; this reviewer did not rerun tests.

### Final review verdict

All five original UI/application findings and the committed-field follow-up are resolved by focused source inspection. No remaining blocker was found in these corrections. The UI/application review approves the reviewed changes subject to the implementer's pending final release verification; it does not claim an independently rerun full suite.
