# E40 / REQ-ROLLBACK — revert P03a flat COW root

Screening FAIL at r=5/r=6 (`.pdca/T297/rv5/screening/accessors/`) rejected the flat
`QueryDescription` root candidate, so its **production** changes are rolled back while all
P03b/P03c/P04 evidence and commits stay.

## What was reverted

Only the three product files touched by `4432fbcb` ("#297 r=5: flatten COW description storage (P03a)"):

- `src/nextorm.core/Query/QueryCommand.Description.cs`
- `src/nextorm.core/Query/QueryCommand.cs`
- `src/nextorm.core/Query/QueryCommand.EagerLoading.cs`

The patch (`.pdca/T297/rv6/rollback/rollback.patch`, sha256
`013fe01ab177ade390811e4aa730c8f9f3a36e807c972939543aba6b530b2f66`) is
`git show 4432fbcb -- <3 files>`; it was applied with `git apply -R` in the worktree only.
No history rewrite, no reset, no push, no `git add -A`.

## Result

- `git diff 9d633521 -- src/nextorm.core` → **empty** (0 lines); per-file blob SHAs match
  `9d633521` exactly, so the product tree is byte-identical to the pre-P03a state.
- CRLF normalization re-applied to the three reverted files.
- Build: `MSBUILDDISABLENODEREUSE=1 dotnet build nextorm.slnx -c Debug` → exit 0, 0 warnings, 0 errors.
- `nextorm.core.tests` full → exit 0, 2658/2658 passed (0 failed, 0 skipped).
- Focused `CteDescriptionCowTests|CachedPreparationRecipeTests` → exit 0, 33/33 passed.
- E19 `T297ProtectedFieldCompatibilityTests` → exit 0, 9/9 passed.

Logs: `build.log`, `core.log`, `core-focused.log`, `e19-focused.log` in this directory.
Machine-readable summary: `manifest.json`.

## Kept

P03b no-op evidence (`c4b34178`), P03c screening (`777e9233`), P04 recipe decomposition
(`39fef763`, `fc354db5`) and all rv5/rv6 evidence stay untouched. Foreign worktree dirt under
`src`/`tests` (45 porcelain entries) is left unstaged and out of the commit.
