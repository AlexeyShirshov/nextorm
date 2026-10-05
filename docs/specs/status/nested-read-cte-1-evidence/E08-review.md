# E08 — scope / CRLF / D5-§9 review (C8, refreshed DO r2 n1 D6.4), issue #166

## `git diff --check`

- exit code = **0** (no whitespace/conflict errors).

## Changed files under `src/` + `tests/`

`git diff --stat -- src/nextorm.core tests/nextorm.core.tests tests/nextorm.sqlite.tests`:

- `src/nextorm.core/Query/QueryCommand.cs` (+21 / -6, frozen F1 from r1)
- `tests/nextorm.core.tests/Iteration14CteLookupTests.cs` (+248 / -0, r1)
- `tests/nextorm.sqlite.tests/TypedCteTests.cs` (+50 / -0, r2 D6.1)

No other `src/` or `tests/` file changed. The status file and evidence dir are new/untracked.

## SQLite diff (r2 D6-only scope)

The r2 SQLite hunk inserts exactly two tests after `ChainedCte_SameProjectionType...`
(`TypedCteTests.cs:497-545`): `NestedReadCte_SyncTerminal_Executes` (sync `ToList` + a second warm
`ToList`) and `NestedReadCte_AsyncTerminal_Executes` (async `ToListAsync`), each asserting the
independently specified fixture rows `{1,2,3}` and `Total(Id==2)==20`. No production/API surface,
no helper, no other test touched — additive and D6-scope-only.

## Fix scope — no forbidden mutation

The product hunks are the `HasDataModifyingCte` getter (`if (IsPrepared) return false;` before the
recursive `HashSet` path) plus its XML-doc remark and adjacent control-flow comments. r2 adds no
product change (F1 frozen); SQLite-only test addition. Forbidden-pattern scan over the `src`+`tests`
diff for `_ctes =`, `_ctes.Clear`, `Cache = false`, `_dontCache`, `.Clear()` → **none found**.

- No `_ctes` clear/mutation.
- No cache-flag change; no `Cache=false` / `_dontCache`.
- SQL, parameters, order, plan-key equality, shared state unchanged (E04-behavior.md).

## CRLF

`file` reports all three changed text files with **CRLF** line terminators:

- `src/nextorm.core/Query/QueryCommand.cs`
- `tests/nextorm.core.tests/Iteration14CteLookupTests.cs`
- `tests/nextorm.sqlite.tests/TypedCteTests.cs`

## Allowed unrelated files left untouched

Pre-existing dirty/untracked, not part of this cycle and not modified by it:

- `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md` (pre-existing modified)
- `.opencode/skills/nextorm-brainstorming/SKILL.md` (pre-existing modified)
- `docs/superpowers/**` (pre-existing untracked)
- `benchmarks/BenchmarkDotNet.Artifacts/**` (pre-existing untracked; count unchanged)

## Git hygiene

No commit / push / merge / stage performed.
