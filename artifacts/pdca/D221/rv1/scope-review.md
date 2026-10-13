# D221 (#221) scope review — raw material (E221-09), rv=1

Raw comparison material only. **No CHECK verdict is written here**; the verdict belongs to the `check`
role. (No `check-verdict.md` is authored by DO.)

## Baseline (D221-0, before any edit)

- `artifacts/pdca/D221/rv1/scope-baseline/head.txt` = `ca83498939a1c6e10ace9352becd3e91ce4655ef` (branch `1.0.9`).
- `scope-baseline/git-status-before.txt` (2 entries, both pre-existing dirty, unrelated to T221):
  `docs/assets/diagrams/nextorm-command-execution.svg`, `docs/ru/assets/diagrams/nextorm-command-execution.svg`.
- `scope-baseline/stash-list.txt` — empty (nothing stashed).
- `scope-baseline/owned-file-hashes-before.txt` — SHA-256 of the six files the plan expected to edit.

## After (A: `git status --porcelain=v1 --untracked-files=all`)

Exit 0. `scope-baseline/git-status-after.txt`:

```
 M docs/assets/diagrams/nextorm-command-execution.svg          <- pre-existing dirty (NOT touched)
 M docs/getting-started/04-dependency-injection.md
 M docs/infrastructure/02-interceptors.md
 M docs/ru/assets/diagrams/nextorm-command-execution.svg       <- pre-existing dirty (NOT touched)
 M docs/ru/getting-started/04-dependency-injection.md
 M docs/ru/infrastructure/02-interceptors.md
 M docs/specs/design/API-NAMING-REVIEW.md
 M docs/specs/design/code-smells-review.md
 M docs/specs/status/D53-public-api-freeze-1.md
 M docs/specs/status/collection-1.0.9-todo.md
 M src/nextorm.core/DI/DataContextBuilder.cs
 M src/nextorm.core/DI/ServiceCollectionExtensions.cs
 M tests/nextorm.core.tests/DependencyInjectionTests.cs
 M tests/nextorm.core.tests/InterceptorBuilderTests.cs
 M tests/nextorm.sqlite.tests/InterceptorTests.cs
?? tests/nextorm.core.tests/InterceptorDiRegistrationTests.cs
?? tests/nextorm.integration.tests/CommonTestSuite.InterceptorDiTests.cs
?? tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs
```

- `A: git diff --check -- <plan footprint>` → **exit 0**, no whitespace errors
  (`scope-baseline/git-diff-check.log`).
- The two `docs/**/assets/diagrams/*.svg` entries are the **pre-existing** dirty files from the baseline
  snapshot; they are not touched by T221 and must not be committed with it.

## Footprint vs the plan

| Plan footprint | Actual | In scope? |
|---|---|---|
| `src/nextorm.core/DI/DataContextBuilder.cs`, `ServiceCollectionExtensions.cs` | modified | yes (D221-1) |
| `tests/nextorm.core.tests/InterceptorBuilderTests.cs` | modified (interceptor contract + builder guards in the mutation surface) | yes (D221-2) |
| `tests/nextorm.core.tests/InterceptorDiRegistrationTests.cs` | new | yes (D221-2) |
| `tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs` | new | yes (D221-2) |
| `tests/nextorm.integration.tests/CommonTestSuite.InterceptorDiTests.cs` (+ ClickHouse class) | new | yes (D221-2) |
| `docs/infrastructure/02-interceptors.md` + RU | modified | yes (D221-3) |
| `docs/getting-started/04-dependency-injection.md` + RU | modified | yes (D221-3) |
| `API-NAMING-REVIEW.md`, `code-smells-review.md`, T53 status, collection list | modified | yes (D221-3) |

### Two additive test-only edits beyond the literal D221-2 list (flagged)

1. `tests/nextorm.core.tests/DependencyInjectionTests.cs` — adds null-argument-guard tests for the public
   `AddNextOrmContext`/`AddKeyedNextOrmContext` overloads. Reason: Stryker's `test-projects` are
   `nextorm.core.tests` + `nextorm.sqlite.tests`, so the only previous coverage of those guards lived in
   `nextorm.integration.tests` and never ran under mutation; E221-07 requires the mutation surface to be
   accounted for. Test-only, additive, no production change.
2. `tests/nextorm.sqlite.tests/InterceptorTests.cs` — the existing `Interceptors_AreInvokedInRegistrationOrder`
   test now registers two **distinct** interceptor types instead of two instances of one type, matching the
   A1 exact-key deduplication documented by the plan. Test-only adaptation of an existing assertion.

No other out-of-scope edit was made. No production file outside `src/nextorm.core/DI/` was changed.

## CRLF

All 16 edited/added in-scope files carry CRLF line terminators (checked with `file`); the new
`.sh` coverage driver is intentionally LF so `bash` can execute it.

## Evidence

- `scope-baseline/git-status-before.txt`, `git-status-after.txt`, `stash-list.txt`, `head.txt`,
  `owned-file-hashes-before.txt`, `git-diff-check.log`, `scope.json`.

## Corrective n=2/3 addendum (CHECK FAIL → DO)

- `src/nextorm.core/DI/DataContextBuilder.cs`: XML `<remarks>` reworded to the actual dedup semantics
  (B2); `AddInterceptor<T>()` remark notes the runtime check (B7). No behavior change.
- Tests added (all `Category=T221`): V07 reverse mixed dedupe
  (`InterceptorDiRegistrationTests.cs:270`), V05 abstract service key (`:294`), no-eager-resolve ownership
  (`:425`); V09 connection-overload dual-interface (`tests/nextorm.sqlite.tests/InterceptorDiRegistrationTests.cs:251`);
  `[Trait("Category","T221")]` on the six new `DependencyInjectionTests` guards (B12).
- `docs/infrastructure/02-interceptors.md` (+RU): EF Core bridge boundary note (B11).
- New evidence `coverage-driver-added.sh`, `ownership-evidence.md`; separated existing/added coverage.
- A re-run: `git status` exit 0, `git diff --check` exit 0; only the 2 pre-existing SVGs remain dirty
  (not touched, not staged).
