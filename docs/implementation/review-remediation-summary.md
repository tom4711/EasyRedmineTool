# Review remediation: issues #47–#58

Base: main `1df316bc3879c6348b83e49cfe02b138ab04a091` (v0.12.0).
Branch: `codex/review-remediation`. Implementation/review completed 02.11.2026.

## Issue coverage

| Issue | Change | Regression coverage |
| --- | --- | --- |
| #47 | Hashed connection identity, conservative reset on key/server change, generation guards on pending responses and old commands | AppSettingsPersistenceTests, ConnectionBoundaryTests, SeriesBookingViewModelTests |
| #48 | Remote mutation success remains final; defaults/cache/refresh failures get separate warnings | BookingLifecycleTests, SeriesBookingViewModelTests |
| #49 | Ticket and today rows reconciled by stable identity; drafts and pending mutations retained; context generation prevents ABA publication | BookingLifecycleTests, ConnectionBoundaryTests, rendered recycling test |
| #50 | Successful series dates immediately marked as conflicts; refresh after booking guard clears, partial success retained | SeriesBookingViewModelTests |
| #51 | Complete CanBook notifications for busy transitions | SeriesBookingViewModelTests observes PropertyChanged values |
| #52 | Preserve corrupt/unreadable sources, block destructive writes, show recovery error | AppSettingsPersistenceTests |
| #53 | Shared response-aware pagination; explicit incomplete-fetch errors for bounds/repeated pages | EasyRedmineApiClientTests |
| #54 | Invariant Gregorian from/to query parameters | Culture-sensitive fake-handler test |
| #55 | Bounded virtualized ListBox; demand-loaded cards, at most four concurrent loads; canceled viewport loads retry on reactivation | BookingLifecycleTests, TimeEntriesViewVirtualizationTests with actual 500-row Avalonia layout |
| #56 | Defensive in-memory snapshots, asynchronous serialized persistence, separate immutable ticket revisions committed by settings reference, awaited initialization/flush | 3000-ticket I/O test, migration/restart/failure tests, storage independent review |
| #57 | Explicit five-minute pooled connection lifetime for session-long clients | Actual DI-handler configuration test; manual reconnect check in desktop-result.md |
| #58 | Cache generations own publication/inflight cleanup; per-waiter cancellation | TicketServiceCacheTests, TimeEntryFormDataCacheTests |

## Compatibility and decisions

- Existing combined settings migrate asynchronously. Current small settings refer to an immutable ticket revision; a failed second write leaves the previous committed pair usable on restart.
- Key rotation conservatively clears active data because an offline key comparison cannot establish account identity. Old connection data is never repurposed for the new key/server.
- Local Load/Update work against defensive snapshots. Explicit save and shutdown await pending persistence. Background failures are observed and shown with credential-free messages.
- Issue #45 (locally configurable custom-field definitions) stays separate.
- The test run corrected F11: the existing .NET 10 factory already supplied a two-minute pooled connection lifetime. The change makes a five-minute policy explicit and tested; no prior infinite lifetime or DNS outage is claimed. Issue #57 and the review report were updated accordingly.

## Review corrections

Independent source reviews found and led to additional regressions for: immutable two-file commits; reactivation after canceled virtualized loads; retained today-entry drafts/commit warnings; series input changes during delayed Flush; and old-connection status-load errors. Each source fix is covered by deterministic fake tasks or temporary-file tests. See `storage-transport-review.md`, `ui-integration-review.md` and the subsystem result files.

The test harness uses synthetic data, fake HTTP/services and temporary files. No live Redmine writes or firm settings were accessed. Headless UI tests verify layout/binding/recycling, not native OS browser/DNS behavior or a full manual theme/accessibility acceptance pass.

## Final verification (02.11.2026)

Release build/test via `dotnet test EasyRedmineTool.slnx --no-restore --configuration Release -p:DisableGitVersionTask=true -p:UseSharedCompilation=false -m:1 -nodeReuse:false --verbosity minimal`: **197 Core + 2 Desktop = 199 passed, 0 failed, 0 skipped**. The application and all XAML compiled in this run. Existing CA1859/CA1861 analyzer warnings remain. Local verification disabled GitVersion; repository CI checks normal Release version generation separately.

Storage has 32 isolated file tests, including failed second commit, bounded retention and two live services. Writers hold a process-shared file lease around settings/cache reads and commit/prune, refresh the last committed history, and regenerate a locally cached revision if another writer pruned it. The current and previous committed revisions of each identity are retained; unknown files and other identities are not pruned by that transaction.

Both independent reviews are resolved: `storage-transport-review.md` and `ui-integration-review.md`. All blockers were reproduced with targeted tests and corrected. The final code has no deferred blocker from those reviews.
