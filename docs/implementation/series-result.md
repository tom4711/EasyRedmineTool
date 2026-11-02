# Series booking remediation (#48, #50, #51)

## Changes

- Busy-state setters notify `CanBook` after their backing state changes. Binding observers receive false at start and the current bookability at completion, including failed preview/book operations.
- Each successful POST immediately marks the corresponding preview row as booked and deselects it. The row also notifies its conflict/status bindings.
- Full and partial success refresh the preview only after `IsBooking` is cleared. The refresh combines just-confirmed dates with server conflicts, protecting the local success state from an immediately stale read, and preserves the remaining selected dates for a targeted retry.
- Defaults updates and asynchronous settings flushes are optional post-booking work. Their errors populate `WarningMessage`; created-entry counts remain final. A failed preview refresh also gets its own warning and preserves booked rows. The booking summary survives the refresh. `HasWarningMessage` notifies visibility bindings.
- `ResetForConnectionChange()` cancels detail/filter/preview reads, invalidates the connection generation and clears tickets, selected ticket, activities, custom fields, preview, confirmation and messages. The in-flight POST is allowed to finish; a series stops before its next POST after a connection switch, and its old completion cannot save defaults or populate the new view.
- Preview/detail requests and confirmed series capture their connection identity and generation. Responses from an earlier connection are suppressed even after an A→B→A switch. Old ticket selection cannot initiate a detail request using newly changed credentials.

## Reproduction and verification

The regression fixture uses in-memory settings and fake time-entry services. Deterministic pending tasks exercise transitions while preview and POST requests are in flight; it performs no live API writes or firm-data access.

Before implementation, the focused run reported **9 failures, 5 passed**. Failures showed missing `CanBook` events, a final false observed value although its getter was true, only one preview read after success, an unmarked successful day during a partial series, and an escaped defaults-write exception.

After implementation:

```sh
python3 /private/tmp/ert-dotnet-test.py --filter FullyQualifiedName~SeriesBookingViewModelTests
```

Initial scope: **14 passed, 0 failed, 0 skipped** (including explicit local-warning and failed-refresh-warning assertions).

Connection scope: the three delayed preview/detail/series tests first failed against the prior implementation. The old-ticket preflight test then caught one additional stale-credential request before its guard was added. The reset test verifies cancellation, cleared state and A→B→A suppression.

Final core result: **19 passed, 0 failed, 0 skipped**. The final solution-based command exited 1 because the newly added Desktop.Tests project separately failed to compile `TestApplication.cs(15)` (`MaterialIconStyles` constructor requires `serviceProvider`); this is owned by the root integration/transport work. The core test assembly built and all 19 filtered series tests ran successfully. The wrapper serializes builds and passes `-m:1 -nodeReuse:false`; execution outside the sandbox was required for MSBuild IPC. Existing analyzer warnings remain. `git diff --check` on the three owned files passed.

Full-solution verification and Avalonia/UI binding verification remain part of the root integration pass. The root must bind `WarningMessage`/`HasWarningMessage` in the series view and call `ResetForConnectionChange()` when saved credentials change identity. No commits were created by this subtask.

## Independent-review race fix (2026-11-02)

The review found that a successful POST could wait in `FlushAsync` after `IsBooking` had cleared. Selecting another ticket in that interval changed the active preview, but the old continuation then refreshed the current ticket with dates booked for the previous ticket. A pending newer preview could also have its status or warning overwritten.

Added deterministic tests with a delayed flush and delayed preview responses. They cover changing tickets, a manual preview refresh on the same ticket, a completed or still-loading newer preview, a failed flush after inputs changed, and changing tickets while the old automatic refresh is pending. Before the fix, the focused run had **6 failures and 19 passed**: completed newer previews received an extra read/replacement, and pending previews lost their loading status to the old booking summary.

The booking continuation now captures both the input revision and the preview-load revision. It validates these after the POST loop, after flush success/failure, after its own refresh success/failure, and before final comment/status updates. Starting a newer preview increments the load revision even if its ticket and other inputs stay the same. The automatic refresh captures its own load revision before awaiting; later responses cannot overwrite a newer preview or busy state.

Verified results from the existing serialized runner logs (no repeated tests were needed when resuming):

```sh
python3 /private/tmp/ert-dotnet-test.py --filter FullyQualifiedName~SeriesBookingViewModelTests
# 25 passed, 0 failed, 0 skipped; exit 0

python3 /private/tmp/ert-dotnet-test.py
# Core: 186 passed, 0 failed, 0 skipped
# Desktop: 2 passed, 0 failed, 0 skipped
# Overall: 188 passed; exit 0
```

Evidence: `/private/tmp/ert-series-review-red.log`, `/private/tmp/ert-series-review-green.log`, and `/private/tmp/ert-series-review-full.log`. The successful full-solution run supersedes the earlier Desktop.Tests constructor compilation failure recorded above. Existing analyzer warnings remain; `git diff --check` for the series source/tests passed. No live API writes and no commits were performed.
