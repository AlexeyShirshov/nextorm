# D175 scope guard (EC175-10 / A4)

Branch `1.0.9-rc2`, tree `3ebaa4ee` + local edits. Commands from repo root.

## Changed files (exactly five, all core test files)

```
$ git diff --name-only
tests/nextorm.core.tests/ImplicitNavigationR3CountBoundaryTests.cs
tests/nextorm.core.tests/InMemoryTests.cs
tests/nextorm.core.tests/PlanKeyStructureTests.cs
tests/nextorm.core.tests/RawSourceBindingFilterTests.cs
tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs
```

- `git diff --stat`: 5 files changed, 4 insertions(+), 16 deletions(-).
- No `src/**`, no `docs/**` (public) / `docs/ru/**`, no `coverage.settings.xml`, no `Directory.*`,
  `.github/**`, `.slnx`, `.csproj`, no integration source changed.
- R175-SCOPE satisfied: production code / public docs / coverage policy unchanged.

## Endings

All five files: `CRLF` for every line terminator. Byte-level census (python read `rb`; `LF` = `\n`
count, `CRLF` = `\r\n` count, `bareLF = LF - CRLF`):

```
ImplicitNavigationR3CountBoundaryTests.cs bytes=9519  LF=183  CRLF=183 bareLF=0 endNewline=True
InMemoryTests.cs                          bytes=34739 LF=1051 CRLF=1051 bareLF=0 endNewline=False last=0x7d('}')
PlanKeyStructureTests.cs                  bytes=19788 LF=440  CRLF=440  bareLF=0 endNewline=True
RawSourceBindingFilterTests.cs            bytes=56382 LF=1372 CRLF=1372 bareLF=0 endNewline=True
SelectExpressionPlanEqualityComparerTests.cs bytes=12011 LF=244 CRLF=244 bareLF=0 endNewline=True
```

- **No bare LF** in any file (`bareLF=0` everywhere); no mixed endings; R175 introduced no non-CRLF line.
- **InMemoryTests.cs EOF-no-newline is pre-existing and unchanged by R175.** The last line is `}`
  (byte `0x7d`) with no terminating newline. The base blob
  `git show 3ebaa4ee:tests/nextorm.core.tests/InMemoryTests.cs` also ends `...{ get; set; }\n}` — final
  byte `}` with no trailing newline — so the EOF condition predates R175. The R175 hunk is a single
  deletion near :382 (`git diff` hunk `@@ -379,7 +379,6 @@`, one removed line, no `\ No newline at end
  of file` marker); it cannot have changed the EOF newline. Per the brief, this pre-existing condition
  is documented, not modified. Normalization `perl -pi -e 's/\r?\n/\r\n/g'` was applied after each edit
  and is idempotent here (no bare LF to convert).
- Verdict: **all five R175 files CRLF-only except the documented pre-existing EOF-no-newline condition
  in `InMemoryTests.cs`** (which has no bare LF; its 1051 line terminators are all CRLF).

## Build config

`dotnet build nextorm.slnx -c Debug` exit 0, `TreatWarningsAsErrors=true` → 0 Warning(s) / 0 Error(s)
both baseline and post (`build/baseline.log`, `build/post.log`).

## Coverage policy

`coverage.settings.xml` unchanged; includes only `nextorm.{core,sqlite,postgres,sqlserver}.dll`; thresholds
stay `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`. **Fresh aggregate (authoritative):
Line 88.3% / Branch 80.3%** (`collection/coverage-Summary.txt`: `Line coverage: 88.3%`,
`Branch coverage: 80.3% (27179 of 33817)`), above both thresholds. (The rv=1 `86.8/79.4` figures were
stale predecessor numbers; the current run supersedes them.)

Coverage commands, exit codes and logs (tree/artifact revision `3ebaa4ee+d175-post`):

- **collect** (exit 0; recorded at `evidence.json` `executions[]` coverage-collect entry, terminal
  `Test run summary: Passed!` + `Code coverage results: tests/coverage/coverage.cobertura.xml` in the log):
  `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
  → 9818 total / 0 failed / 198 skipped. Log: `collection/coverage-collect.log` (combined:
  `collection/coverage.log`).
- **report** (exit 0, freshly captured `2026-10-09T18:23Z`, log ends with `exit_code=0`):
  `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:+nextorm.*`
  → regenerated `collection/coverage-Summary.txt` numbers identical (88.3/80.3). Log:
  `collection/coverage-report.log`.

## Public docs

Only the new internal status + evidence under `docs/specs/status/`; no public article/readme change; no
spec link added from public docs. `docs/` and `docs/ru/` untouched (`git diff --name-only` empty for them).

## Collection reconciliation (R4)

The task-owned rv=2 rows are fully filled in `evidence-contract.md` (EC175-01..10, H01-H08, C01-C05,
X01-X05, AC175-01..05; states `run-pass`/`guard`). The collection-level §2/§3/§4/§4b/§5 slots in
`docs/specs/status/collection-1.0.9-rc2-C-evidence.md` are **not** edited here: that artifact pins the
integrated CHECK of the older `1.0.9-rc2` collection with its 16 DONE tasks (D153..D191) and contains no
R175 row. R175 belongs to the separate `rc2-reclaim` collection, whose `§2/§3/§4/§4b/§5` are filled at the
reclaim collection CHECK, not by one task's DO. Precedent: R184 (DONE, r=1/rv=2) did not add rows to
`collection-1.0.9-rc2-C-evidence.md`; its commit touched only `collection-rc2-reclaim.md`. DO hands the
R175 ledger + artifacts to CHECK and does not mark R175 done or issue a collection verdict.

