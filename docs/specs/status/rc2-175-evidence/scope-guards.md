# D175 r1 — scope guards

Unit: D175 (branch `1.0.9-rc2`, HEAD `04836505fdb506d2e2176e74e2a7cc283a132d2f`).
Plan L350 (EC175-10 / R175-SCOPE, EVIDENCE) requires `scope-guards.md`.

## (a) `git diff --check` — whitespace/conflict guard

```
$ git diff --check
(output is empty — no whitespace errors, no conflict markers)
exit_code = 0
```

Also checked staged tree: `git diff --cached --check` is empty (nothing staged).

## (b) CRLF preservation of the 5 changed test files

`file <path>` output:

```
tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs: ASCII text, with CRLF line terminators
tests/nextorm.core.tests/ImplicitNavigationR3CountBoundaryTests.cs:    Unicode text, UTF-8 text, with CRLF line terminators
tests/nextorm.core.tests/RawSourceBindingFilterTests.cs:               Unicode text, UTF-8 text, with CRLF line terminators
tests/nextorm.core.tests/PlanKeyStructureTests.cs:                     ASCII text, with CRLF line terminators
tests/nextorm.core.tests/InMemoryTests.cs:                             ASCII text, with CRLF line terminators
```

CRLF vs bare-LF byte counts (`.count(b"\r\n")` vs `.count(b"\n")`): every file has
`CRLF == LF`, i.e. **100% of line endings are CRLF, 0 LF-only lines**:

| file | CRLF | LF | LF-only |
| --- | --- | --- | --- |
| SelectExpressionPlanEqualityComparerTests.cs | 84 | 84 | 0 |
| ImplicitNavigationR3CountBoundaryTests.cs | 183 | 183 | 0 |
| RawSourceBindingFilterTests.cs | 1372 | 1372 | 0 |
| PlanKeyStructureTests.cs | 440 | 440 | 0 |
| InMemoryTests.cs | 990 | 990 | 0 |

## (c) Changed-file allow-list

`git diff --name-only` (unstaged) is **exactly** the 5 allowed core test files:

```
tests/nextorm.core.tests/ImplicitNavigationR3CountBoundaryTests.cs
tests/nextorm.core.tests/InMemoryTests.cs
tests/nextorm.core.tests/PlanKeyStructureTests.cs
tests/nextorm.core.tests/RawSourceBindingFilterTests.cs
tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs
```

`git diff --cached --name-only` (staged): empty — nothing staged.

Guard results:
- No `src/**` change: confirmed (`git diff --name-only` contains no `src/` path).
- No config change (`.csproj`, `Directory.*.props`, `global.json`, `*.json` under repo): confirmed — none in the diff.
- No public-doc change (`docs/**`, `docs/ru/**`, `readme.md`): confirmed — none in the diff (the untracked `docs/specs/status/**` files from other cycles are not part of this diff and specs are not public docs).
- Product delta = 0: all 5 paths are `tests/nextorm.core.tests/*.cs`.
