# Status — corrective cycle `rc1-141-validate-1` (D141 verification close-out)

- Task id: `rc1-141-validate-1`
- Collection: `1.0.9-rc1`, group **G01**
- Branch: `1.0.9-rc1`; cycle-time HEAD `d60c80e3`
- Current cycle: `N = 1`
- Plan revision: `r = 2`
- Attempt: `n = 1`
- Evidence contract revision: `rv = 2` (supersedes r=1's method only; `R141-VERIFY` unchanged)
- Mode: autonomous + auto-commit **authorized** by the collection; this DO task performs **no commit/push** (ACT commits later)
- Worktree: `/home/alex/sources/nextorm`
- Notice: host has no `todowrite` tool; this status file carries the progress log instead.

## Goal

Make `validate_inner_loop.py report` exit **0** for D141 **honestly**, preserving `R141-VERIFY`
(fresh, real evidence; no waiver, no validator edit, no fabricated counts). The previous cycle
(`rc1-g01-c-evidence-1`) could not reach exit 0 because the helper's single-cycle caps
(one comprehensive boundary test sweep, one boundary solution build) rejected the collection-level
aggregate plus three historical full-project sweeps. This cycle replaces those with a single fresh
Debug solution build plus a single fresh comprehensive solution test sweep, keeping every affected
check filtered and fresh at HEAD `d60c80e3`.

## Verbatim `R141-VERIFY` (from `docs/specs/status/rc1-141-version-gates-1.md:41`)

```
| **R141-VERIFY** | Verification | Build, inner filtered runs and the affected boundary sweeps are green; `validate_inner_loop.py report` exits 0 | Any red run is not completion; stale/absent evidence is not completion |
```

## Acceptance criteria

| ID | Criterion | Positive | Negative |
|---|---|---|---|
| **V141-R01** | Fresh compliant evidence | `validate_inner_loop.py brief` exits 0 **and** `report` exits 0 on a freshly produced evidence JSON at HEAD `d60c80e3` | No waiver is recorded; `R141-VERIFY` is met by the real exit-0 result |
| **V141-R02** | Honesty | Every recorded execution has a real command array, revision, integer exit code and real `selected_count >= 1` | No fabricated counts; no reused historical results; the validator script is not edited |
| **V141-R03** | Boundary discipline | Exactly one unfiltered comprehensive boundary sweep (the full solution test run) and exactly one boundary solution build | No extra unfiltered boundary sweep; no second solution build |
| **V141-R04** | Containers | The fresh container integration run starts all five provider containers and reports no provider skipped for lack of `DOCKER_HOST` | A run that skips providers for a missing socket is not a passing run |
| **V141-R05** | Scope | Only `docs/specs/status/**` is touched; no `src/**` file, no `artifacts/**` file, and not `validate_inner_loop.py` | Any such change is a STOP defect |

## Scope JSON (written at `docs/specs/status/rc1-141-validate-1-evidence.json`)

```json
{
  "unit": "rc1-141-validate-1",
  "scope": {
    "projects": ["tests/nextorm.postgres.tests", "tests/nextorm.mariadb.tests", "tests/nextorm.core.tests", "tests/nextorm.integration.tests"],
    "selectors": ["FullyQualifiedName~VersionGate", "FullyQualifiedName~FilteredAggregates", "FullyQualifiedName~StringAgg_WithFilter", "FullyQualifiedName~CapabilityFlags", "FullyQualifiedName~Returning", "FullyQualifiedName~DialectCapabilityContractTests"],
    "files": ["docs/specs/status/rc1-141-validate-1.md", "docs/specs/status/rc1-141-validate-1-evidence.json", "docs/specs/status/rc1-141-version-gates-1.md", "docs/specs/status/collection-1.0.9-rc1.md"],
    "rebuild": "none",
    "boundary": "One fresh Debug solution build, one fresh comprehensive solution test sweep, and fresh container integration evidence",
    "rationale": "Documentation/status-only correction; freshly repeat affected checks and replace separate comprehensive provider sweeps with their encompassing solution sweep."
  }
}
```

## Execution plan

| ID | Phase | Invocation (argument array) | Required |
|---|---|---|---|
| E-B01 | boundary | `["dotnet","build","nextorm.slnx","-c","Debug"]` | exit 0; one solution build |
| E-T01 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~VersionGate"]` | exit 0, ≥1 selected |
| E-T02 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~FilteredAggregates"]` | exit 0, ≥1 selected |
| E-T03 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~StringAgg_WithFilter"]` | exit 0, ≥1 selected |
| E-T04 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~CapabilityFlags"]` | exit 0, ≥1 selected |
| E-T05 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T06 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~VersionGate"]` | exit 0, ≥1 selected |
| E-T07 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~CapabilityFlags"]` | exit 0, ≥1 selected |
| E-T08 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T09 | inner | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T10 | inner | `["dotnet","test","tests/nextorm.integration.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~DialectCapabilityContractTests"]` | exit 0, ≥1 selected |
| E-T11 | boundary | `["dotnet","test","nextorm.slnx","-c","Debug","--no-build"]` | exit 0; the single comprehensive sweep |
| E-T12 | boundary | `["dotnet","run","--project","tests/nextorm.integration.tests","-c","Debug","--no-build","--","-noColor"]` | exit 0; all five containers start, no provider skipped for lack of `DOCKER_HOST` |

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` is set in the environment
(not part of any command array) for all test/run commands.

## Test strategy

- **docs/status-only change**; `scope.rebuild = none` (no compiled input changes: all `scope.files` are `.md`/`.json`).
- One fresh Debug solution build (E-B01) is the only build; every subsequent `--no-build` run uses its artifacts.
- The inner loop is exactly the affected filtered subset (E-T01..E-T10); the single comprehensive boundary sweep is E-T11; the fresh container integration run is E-T12 (supporting boundary evidence, `dotnet run`, not a test-sweep command).
- All commands are plain argument arrays (no shell wrapper), `source_revision == artifact_revision == d60c80e3`.

## Perf decision

**N/A.** Documentation/status-only scope; no product code, query path or plan cache touched.

## Recon

**N/A.** Identity, environment and scope are fixed by the brief; no discovery required before execution.

## Unit execution mode

Single sequential unit (`rc1-141-validate-1`, DO), unit mode `single`. No parallel sub-units.

## Execution receipts — historical r=1, superseded by the r=2 block below (E-T12 retained/excluded)

> **Historical r=1 receipts.** These are the r=1 records. The separate E-T12 container `dotnet run` is
> **retained here as history** but **excluded** from the r=2 compliant evidence and **superseded** by the
> E-T11 in-sweep container proof. The r=2 compliant evidence is the `## rv=2 evidence contract` block and the
> `## Replan r=2` plan below; the exit-0 validator pair cited here belongs to r=1 and is not the r=2 result.
> Not hidden, not deleted.

| ID | Phase | Exit | Selected | Basis | Log |
|---|---|---|---|---|---|
| E-B01 | boundary | 0 | 24 | project output lines (0 w / 0 e) | `.../E-B01.log` |
| E-T01 | inner | 0 | 14 | run summary total | `.../E-T01.log` |
| E-T02 | inner | 0 | 1 | run summary total | `.../E-T02.log` |
| E-T03 | inner | 0 | 1 | run summary total | `.../E-T03.log` |
| E-T04 | inner | 0 | 1 | run summary total | `.../E-T04.log` |
| E-T05 | inner | 0 | 64 | run summary total | `.../E-T05.log` |
| E-T06 | inner | 0 | 9 | run summary total | `.../E-T06.log` |
| E-T07 | inner | 0 | 1 | run summary total | `.../E-T07.log` |
| E-T08 | inner | 0 | 17 | run summary total | `.../E-T08.log` |
| E-T09 | inner | 0 | 55 | run summary total | `.../E-T09.log` |
| E-T10 | inner | 0 | 3 | run summary total | `.../E-T10.log` |
| E-T11 | boundary | 0 | 8442 | solution run summary total (8248 pass / 0 fail / 194 skip) | `.../E-T11.log` |
| E-T12 | boundary | 0 | 3195 | integration summary Total (0 fail / 193 skip; 5 containers) | `.../E-T12.log` |

Validator exits: **`brief` = 0** (`validator-brief.txt`), **`report` = 0** (`validator-report.txt`).
`R141-VERIFY` is met on fresh evidence: no waiver, no validator edit, no fabricated counts,
no reused historical results, no extra unfiltered boundary sweep or solution build.
Provenance: `docs/specs/status/rc1-141-validate-1-evidence/provenance.json`.

## Replan r=2 (supersedes r=1's method only; `R141-VERIFY` verbatim above is unchanged)

`Replanned r=2: eliminate the second comprehensive sweep; container proof comes only from E-T11`

Rationale: CHECK rejected r=1 with defect `extra-unfiltered-boundary-run` — the r=1 E-T12
`dotnet run` was a second comprehensive boundary sweep beyond the single allowed one. r=2 keeps
exactly ONE comprehensive boundary sweep and ONE boundary solution build; the five container
providers are proven from inside E-T11 itself, with no separate integration run.

### r=2 execution plan (12 rows)

| ID | Phase | Invocation (argument array) | Required |
|---|---|---|---|
| E-B01 | boundary | `["dotnet","build","nextorm.slnx","-c","Debug"]` | exit 0; one solution build |
| E-T01 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~VersionGate"]` | exit 0, ≥1 selected |
| E-T02 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~FilteredAggregates"]` | exit 0, ≥1 selected |
| E-T03 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~StringAgg_WithFilter"]` | exit 0, ≥1 selected |
| E-T04 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~CapabilityFlags"]` | exit 0, ≥1 selected |
| E-T05 | inner | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T06 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~VersionGate"]` | exit 0, ≥1 selected |
| E-T07 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~CapabilityFlags"]` | exit 0, ≥1 selected |
| E-T08 | inner | `["dotnet","test","tests/nextorm.mariadb.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T09 | inner | `["dotnet","test","tests/nextorm.core.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~Returning"]` | exit 0, ≥1 selected |
| E-T10 | inner | `["dotnet","test","tests/nextorm.integration.tests","-c","Debug","--no-build","--filter","FullyQualifiedName~DialectCapabilityContractTests"]` | exit 0, ≥1 selected |
| E-T11 | boundary | `["dotnet","test","nextorm.slnx","-c","Debug","--no-build","--report-xunit-junit","--results-directory","docs/specs/status/rc1-141-validate-1-evidence/r2/junit"]` | exit 0; THE single comprehensive sweep; all five container providers proven here |

`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` is set in the
environment (not part of any command array) for all test/run commands. No E-T12: container proof
comes only from E-T11's in-sweep integration execution.

**Command-provenance note (P2 `ACT-R2-COMMAND-PROVENANCE`):** E-T11 was **actually executed** as
`["dotnet","test","nextorm.slnx","-c","Debug","--no-build","--report-xunit-junit","--results-directory","docs/specs/status/rc1-141-validate-1-evidence/r2/junit"]` —
the two Microsoft.Testing.Platform reporting flags were appended to the one unfiltered boundary sweep so
its own JUnit artifact (`docs/specs/status/rc1-141-validate-1-evidence/r2/junit/`) proves the five providers.
The recorded invocation in `rc1-141-validate-1-evidence.json` and `r2/provenance.json` is authoritative; the
plan row above now matches it (no execution changed, no second sweep added).

## rv=2 evidence contract (persisted for traceability)

- Evidence contract revision: **`rv = 2`** (supersedes r=1's method only; `R141-VERIFY` unchanged).
- `CHECK` re-gather budget: **2** targeted evidence requests.
- Perf predicate: **N/A** (docs/status-only scope; no product code, query path or plan cache touched).
- Recon predicate: **N/A** (identity, environment and scope fixed by the brief; no discovery required).
- Coverage policy: CI thresholds line **85** / branch **75** remain the policy; **no new coverage
  measurement** was required or taken for this docs/status-only change (scope `rebuild = none`).

| Row ID | Owner | Requirement | Required result | Applicability | Evidence pointer |
|---|---|---|---|---|---|
| E-B01 | coder | R141-VERIFY | `["dotnet","build","nextorm.slnx","-c","Debug"]` exit 0; exactly one boundary solution build; 24 projects | always | `.../r2/E-B01.log` |
| E-T01 | coder | R141-VERIFY | PG `FullyQualifiedName~VersionGate` exit 0; selected 14 | always | `.../r2/E-T01.log` |
| E-T02 | coder | R141-VERIFY | PG `FullyQualifiedName~FilteredAggregates` exit 0; selected 1 | always | `.../r2/E-T02.log` |
| E-T03 | coder | R141-VERIFY | PG `FullyQualifiedName~StringAgg_WithFilter` exit 0; selected 1 | always | `.../r2/E-T03.log` |
| E-T04 | coder | R141-VERIFY | PG `FullyQualifiedName~CapabilityFlags` exit 0; selected 1 | always | `.../r2/E-T04.log` |
| E-T05 | coder | R141-VERIFY | PG `FullyQualifiedName~Returning` exit 0; selected 64 | always | `.../r2/E-T05.log` |
| E-T06 | coder | R141-VERIFY | MariaDB `FullyQualifiedName~VersionGate` exit 0; selected 9 | always | `.../r2/E-T06.log` |
| E-T07 | coder | R141-VERIFY | MariaDB `FullyQualifiedName~CapabilityFlags` exit 0; selected 1 | always | `.../r2/E-T07.log` |
| E-T08 | coder | R141-VERIFY | MariaDB `FullyQualifiedName~Returning` exit 0; selected 17 | always | `.../r2/E-T08.log` |
| E-T09 | coder | R141-VERIFY | core `FullyQualifiedName~Returning` exit 0; selected 55 | always | `.../r2/E-T09.log` |
| E-T10 | coder | R141-VERIFY | integration `FullyQualifiedName~DialectCapabilityContractTests` exit 0; selected 3 | always | `.../r2/E-T10.log` |
| E-T11 | coder | R141-VERIFY | `dotnet test nextorm.slnx -c Debug --no-build --report-xunit-junit --results-directory .../r2/junit` exit 0; selected 8442 (8248/0/194); the single comprehensive boundary sweep | always | `.../r2/E-T11.log`, `.../r2/junit/` |
| E-PC-PG | coder | R141-VERIFY | PostgreSQL executed non-skipped 753, failures 0 (25 capability skips) | always (container integration) | `.../r2/provider-container-evidence.json` |
| E-PC-SQL | coder | R141-VERIFY | SQL Server executed non-skipped 675, failures 0 (43 capability skips) | always (container integration) | `.../r2/provider-container-evidence.json` |
| E-PC-MY | coder | R141-VERIFY | MySQL executed non-skipped 579, failures 0 (79 capability skips) | always (container integration) | `.../r2/provider-container-evidence.json` |
| E-PC-MDB | coder | R141-VERIFY | MariaDB executed non-skipped 50, failures 0 (0 skips) | always (container integration) | `.../r2/provider-container-evidence.json` |
| E-PC-CH | coder | R141-VERIFY | ClickHouse executed non-skipped 177, failures 0 (0 skips) | always (container integration) | `.../r2/provider-container-evidence.json` |
| E-VBRIEF | coder | R141-VERIFY | `validate_inner_loop.py brief docs/specs/status/rc1-141-validate-1-evidence.json` exit 0 | always | `.../r2/validator-brief.txt` |
| E-VREPORT | coder | R141-VERIFY | `validate_inner_loop.py report docs/specs/status/rc1-141-validate-1-evidence.json` exit 0 | always | `.../r2/validator-report.txt` |

All pointers are relative to `docs/specs/status/rc1-141-validate-1-evidence/`.

## Progress log

```
2026-10-06T03:44:42Z | DO | r1 | 1/3 | DO started | docs/specs/status/rc1-141-validate-1.md
2026-10-06T03:44:55Z | DO | r1 | 1/3 | brief validated exit 0 | docs/specs/status/rc1-141-validate-1-evidence/validator-brief.txt
2026-10-06T03:45:40Z | DO | r1 | 1/3 | E-B01 boundary solution build exit 0 (0 warnings/0 errors, 24 projects) | docs/specs/status/rc1-141-validate-1-evidence/E-B01.log
2026-10-06T03:46:01Z | DO | r1 | 1/3 | E-T01..E-T10 inner filtered runs exit 0 (14/1/1/1/64/9/1/17/55/3 selected) | docs/specs/status/rc1-141-validate-1-evidence/E-T0{1..9}.log,E-T10.log
2026-10-06T03:47:08Z | DO | r1 | 1/3 | E-T11 boundary solution sweep exit 0 (total 8442, failed 0, skipped 194) | docs/specs/status/rc1-141-validate-1-evidence/E-T11.log
2026-10-06T03:48:02Z | DO | r1 | 1/3 | E-T12 fresh container integration exit 0 (Total 3195, failed 0, skipped 193; all 5 containers started) | docs/specs/status/rc1-141-validate-1-evidence/E-T12.log
2026-10-06T03:48:16Z | DO | r1 | 1/3 | report validated exit 0; R141-VERIFY met on fresh evidence; no waiver, no validator edit, no src change, no commit | docs/specs/status/rc1-141-validate-1-evidence/validator-report.txt
2026-10-06T03:53:53Z | DO | r2 | 1/3 | replanned: eliminate second comprehensive sweep; container proof from E-T11 only; R141-VERIFY preserved verbatim | docs/specs/status/rc1-141-validate-1.md
2026-10-06T03:54:43Z | DO | r2 | 1/3 | E-B01 boundary solution build exit 0 (0 warnings/0 errors, 24 projects) | docs/specs/status/rc1-141-validate-1-evidence/r2/E-B01.log
2026-10-06T03:54:56Z | DO | r2 | 1/3 | E-T01..E-T10 inner filtered runs exit 0 (14/1/1/1/64/9/1/17/55/3 selected) | docs/specs/status/rc1-141-validate-1-evidence/r2/E-T01.log..E-T10.log
2026-10-06T03:55:56Z | DO | r2 | 1/3 | E-T11 single comprehensive boundary sweep exit 0 (total 8442, failed 0, skipped 194); no separate integration run | docs/specs/status/rc1-141-validate-1-evidence/r2/E-T11.log
2026-10-06T03:56:10Z | DO | r2 | 1/3 | five container providers proven executed inside E-T11 (PG 753, SQL Server 675, MySQL 579, MariaDB 50, ClickHouse 177; failures 0) | docs/specs/status/rc1-141-validate-1-evidence/r2/provider-container-evidence.json
2026-10-06T03:56:29Z | DO | r2 | 1/3 | brief exit 0 and report exit 0; R141-VERIFY met on fresh r=2 evidence; no src change, no commit | docs/specs/status/rc1-141-validate-1-evidence/r2/validator-brief.txt,docs/specs/status/rc1-141-validate-1-evidence/r2/validator-report.txt
2026-10-06T09:04Z | ACT | r2 | 1/3 | CHECK PASS finalized; rv=2 evidence contract table persisted; P2 fixes (E-T11 command provenance; r=1 receipts relabeled historical/superseded); R141-VERIFY met; gate C ready for parent revalidation; no src change | docs/specs/status/rc1-141-validate-1.md
```

## ACT

- **CHECK PASS** — `rc1-141-validate-1`: plan revision `r = 2`, attempt `n = 1`, evidence contract revision
  `rv = 2`. `R141-VERIFY` is **MET**: `validate_inner_loop.py brief` exit 0 and `report` exit 0 on
  `docs/specs/status/rc1-141-validate-1-evidence.json` at HEAD `d60c80e3`; one comprehensive full-solution
  boundary sweep (E-T11, 8442 = 8248/0/194) plus one boundary solution build (E-B01), ten inner filtered runs
  green, and all five container providers executed inside E-T11 with 0 failures.
- **Gate C:** ready for the **parent independent `Task → check` re-run** (parent revalidation); this ACT does
  **not** self-declare gate C PASS.
- **Waiver:** `W-C-D141-REPORT-CAP-1` is **WITHDRAWN** (replaced by the fresh compliant evidence above).
- **Scope:** only `docs/specs/status/**` touched; **no `src/**` change**, no test/config edit, and
  `validate_inner_loop.py` untouched.
- **Commit:** explicit paths only (`docs/specs/status/rc1-141-validate-1.md`, its `-evidence.json` and
  `-evidence/` dir, `rc1-141-version-gates-1.md`, `collection-1.0.9-rc1.md`); no `git add -A`; **no push**.

