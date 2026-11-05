# Review Remediation Implementation Plan

**Goal:** Implement GitHub issues #47–#58 with regression tests on `codex/review-remediation`, then create one PR to main.

**Spec:** `docs/reviews/2026-11-01-architecture-clean-code-dotnet-avalonia.md`; the twelve issues contain the acceptance criteria. The user explicitly authorized implementation, tests and PR creation.

**Architecture:** Preserve the existing projects and MVVM toolkit. Introduce explicit connection identity and persistence lifecycle, keep successful remote mutations separate from optional local work, retain row identities and lazily load virtualized forms. Correct transport pagination, invariant dates, cache generations and connection lifetime.

**Constraints:** No live Redmine writes or firm data. Existing configuration migrates without loss. Issue #45 is outside scope. Tests use fake handlers/services and deterministic gates. API writes are never automatically retried. Work happens in the existing isolated worktree.

## Tasks and interfaces

### 1. API/cache correctness (#53, #54, #58)
- Own API pagination/date code, TicketService and TimeEntryFormDataCache plus associated tests.
- Test server page size 25/80, explicit failure at safety bound, th-TH invariant dates and invalidation during pending loads before implementing.
- Respect response limits/offsets, report incomplete fetches, generation-check publication and in-flight ownership; cancellation of one waiter must not cancel shared loads.
- Keep public service signatures unchanged.

### 2. Series booking state (#50, #51, series part of #48)
- Own SeriesBookingViewModel/preview rows and SeriesBookingViewModelTests.
- Test PropertyChanged values at load/book boundaries, refresh after successful and partial series, and local persistence failure after successful POST.
- Mark successful days immediately, refresh only after booking guard clears. Optional local writes must not reverse remote success.
- Later connection-reset integration may cancel preview loads and clear old state; coordinate before editing those entry points.

### 3. Persistence architecture (#52, #56, storage part of #47)
- Own AppSettingsService, IAppSettingsService and persistence model/helpers/tests.
- Keep Load/Save/Update available for existing callers/fakes; production Load/Update use an in-memory snapshot, queue serialized writes off the UI thread, split cached tickets from small settings.
- Add default-interface `Task InitializeAsync(CancellationToken=default)` and `Task FlushAsync(CancellationToken=default)` so existing in-memory fakes remain compatible. Default no-op implementations are acceptable only for these fakes; production overrides both.
- Expose a persistence failure event with default no-op accessors plus a last error property for startup/Settings feedback. Production initialization reads once asynchronously; corrupt source is preserved and automatic writes blocked. Flush reports failures; background failures are observed.
- Add a stable hashed ConnectionIdentity from normalized URL + exact key. Key rotation deliberately clears active cached data because same-user identity cannot be verified offline. Credential changes clear tickets, favorites, last IDs and field defaults. Persist ticket profiles under their identity, never import A data into B. Schema migration reads the old combined JSON without overwriting it on read failures.
- Tests: corrupt/read-failed source preservation, legacy migration/restart, small setting update does not rewrite ticket file, snapshot-only reads, serialized concurrent updates, background write errors and connection isolation.

### 4. UI/application integration (#47–#49, #55, #56, #57)
- Own TimeEntries/Favorite/Today/TicketList/Settings/Weekly ViewModels, Desktop App/MainWindow/DI, TimeEntriesView and code behind, new helpers/tests.
- Reconcile rows by connection+ticket identity; retain drafts and submitting state across navigation/reload. Load forms only on visible row activation, bound concurrency and reuse completed form loads. Use ListBox virtualization with no infinite outer ScrollViewer.
- Capture connection identity at request start and suppress stale publication/persistence; clear old view state when credentials change. Old row commands cannot send to a new connection.
- Treat API success as final and isolate defaults/cache/refresh failures with separate warning text. Share the helper across mutation paths where appropriate.
- Initialize settings before constructing view models; await Flush on explicit settings saves/shutdown as appropriate and surface background errors safely on UI thread.
- Configure an explicit finite SocketsHttpHandler.PooledConnectionLifetime for long-lived typed clients and verify configuration with meaningful tests.

## Review focus / tests
- Delayed response from old connection after settings save cannot populate new cache or active views.
- POST completes after navigation or connection switch: no duplicate request, old success cannot update new connection.
- Local write fails after API success: successful result remains and no automatic retry occurs.
- Virtualized recycling preserves all draft fields and pending-operation identity; 500-ticket fake counts verify bounded demand.
- Corrupt JSON/restart/migration never deletes original source or silently resets recoverable data.

## Completion
- [x] API/cache tests and implementation
- [x] Series state tests and implementation
- [x] Persistence tests and implementation
- [x] UI/application integration tests and implementation
- [x] Full suite and desktop build pass; independent final review and findings addressed
- [x] Review report, implementation notes and plan committed; branch pushed; PR has `Closes #47` through `Closes #58`

PR: https://github.com/tom4711/EasyRedmineTool/pull/59
