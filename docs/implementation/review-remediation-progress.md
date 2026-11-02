# Review remediation progress

Branch: codex/review-remediation; base main 1df316bc3879c6348b83e49cfe02b138ab04a091.
Plan: docs/superpowers/plans/2026-11-01-review-remediation.md.
Authorization: user requested all twelve issues, tests, collective branch and PR.

Independent implementation ownership: API/cache, series state, persistence, UI integration. Shared interfaces in plan. Agents do not commit or touch others' files. All dotnet test runs use /private/tmp/ert-dotnet-test.py to serialize builds.

Decision: API-key changes conservatively reset active per-connection data; offline rotation cannot prove same account. Stable connection hash never persists raw keys in filenames. Existing data migrates only into its original connection.

- API/cache: pending.
- Series state: pending.
- Persistence: pending.
- UI integration: pending.
- Full verification/review/PR: pending.

## Implementation and review record (02.11.2026)

- API/cache complete: commit `1593b94`; targeted 40/40 passed. #53/#54/#58.
- Series complete: 25 focused tests passed; delayed Flush/input-generation races reproduced RED and corrected. #48/#50/#51 and series connection guard.
- UI integration complete: stable favorite/today identities, pending mutation guards, canceled viewport retry, connection/status generations and custom-field commit snapshot. Core boundary/lifecycle regressions and actual 500-row headless layout test added.
- HTTP discovery: .NET10 previously provided a finite two-minute pooled connection lifetime. #57/report corrected; explicit five-minute policy tested through actual DI handler.
- Persistence: async snapshot/queue, schema migration and immutable revision commit implemented; split-file failure test and independent review passed. Bounded retention refinement undergoing a final two-instance writer check before commit.
- Latest full Release check before final retention refinement: 196 Core + 2 Desktop tests passed (198 total), 0 failed/skipped; XAML compiled. Existing analyzer warnings remain.
- Final blockers raised by independent review all have targeted regressions and source fixes. Last storage writer-coordination check, final verification, remaining commits and PR pending.

## Final verification

- Persistence complete: 32/32 targeted tests, including two live writers; reviewed file lease and immutable revision cleanup accepted.
- API/cache commit: 1593b94. UI/application/series commit: 926e34d.
- Final Release run: 197 Core + 2 Desktop =199 passed,0failed,0skipped; all Desktop/XAML compiled.
- Independent storage and UI reviews: all identified blockers resolved.
- Complete: persistence/docs committed, collective branch pushed, PR #59 created with Closes #47 through #58. GitHub CI is checked at handoff.

Final commits: 1593b94 (transport),926e34d (UI/application),e3e9865 (persistence),93dfef1 (documentation). PR: https://github.com/tom4711/EasyRedmineTool/pull/59
