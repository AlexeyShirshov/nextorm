# PostgreSQL native json/jsonb column mapping — task D131 / issue #131

- status: DO complete (plan r=1, n=1/3) — awaiting CHECK
- task: D131
- issue: #131 (https://github.com/AlexeyShirshov/nextorm/issues/131)
- collection: 1.0.9-rc1
- group: G01
- branch: 1.0.9-rc1
- cycle: N=1, revision r=1, attempt n=1/3
- mode: autonomous + auto-commit (no push/merge); this DO performs **no commit/push**

## Goal

PostgreSQL: native `json`/`jsonb` column mapping (read+write); acceptance per issue = tests + EN/RU docs.

## Locked plan (revision r=1)

### Decision

**#131 closes as already-implemented + docs/verification closure.** No production mapping is
re-implemented. At HEAD the committed PostgreSQL-native `[JsonColumn]` read/write path already exists
(commits `df475601`, `723776a0`; native `jsonb` round-trip verified 4/4 on real PostgreSQL in
`docs/specs/design/API-NAMING-REVIEW.md:4951`). The issue premise ("mapping only in the parameter
path") is **stale at HEAD** and is a documentation defect, not a missing implementation. The remaining
work is (a) a provider-specific regression test that pins read/write for **both** physical column
types `json` and `jsonb`, (b) correcting the parameter-only claims in EN/RU docs, and (c) spec sync
plus a `JsonNode` follow-up issue. If the new test finds a genuine native `[JsonColumn]` defect, DO
STOPs and reports it back to PLAN instead of papering over it.

### Acceptance criteria

| ID | Criterion | Positive | Negative |
|---|---|---|---|
| **R131-01** | `[JsonColumn]` Auto read/write against a physical `jsonb` column | insert→read and update→read return a semantically-equal object on real PostgreSQL | any round-trip mismatch fails the test |
| **R131-02** | `[JsonColumn]` Auto read/write against a physical `json` column | same as R131-01 on a `json`-typed column | a genuine engine defect (e.g. jsonb parameter not coercible to `json`) is reported to PLAN, not silently skipped/weakened |
| **R131-03** | Nullable POCO / SQL NULL | a null property inserts SQL NULL and reads back `null` | a non-null value is not turned into SQL NULL |
| **R131-04** | Default-valued properties + nested structure | an object whose scalar properties are default/zero and with a nested object round-trips semantically equal | no field is dropped or defaulted on read |
| **R131-05** | Unicode | a Unicode payload round-trips unchanged | mojibake/escaping regression |
| **R131-06** | Docs EN+RU accuracy | `[JsonColumn]` native read/write (Auto→jsonb) documented; bare `JsonDocument`/`JsonElement`, unsupported bare `JsonNode` read, and plain string text / explicit `json_cast` correctly separated; no DDL generation promised | no stale "columns are mapped only in the parameter path" claim remains |
| **R131-07** | Closure + follow-up | #131 linked to verification evidence; `JsonNode` follow-up issue opened/verified in milestone `1.0.9-rc1`; existing spec "Done" rows kept | evidence URLs unverified or spec rows downgraded without cause |

### DO units

| Unit | Scope |
|---|---|
| **D131.1** | Persist locked plan in this status file (this section). |
| **D131.2** | PG-specific regression test (new `tests/nextorm.integration.tests/PostgresJsonColumnTests.cs`) covering `[JsonColumn]` Auto against physical `json` **and** `jsonb`: insert→read, update→read, nullable/null, default-valued + nested, Unicode. Follow existing fixture/cleanup conventions; do not duplicate/weaken `CommonTestSuite.JsonColumn.cs`. |
| **D131.3** | Docs EN+RU: fix stale parameter-path-only claims in `docs/guide/14-json.md` (+RU), `docs/providers/postgres.md` (+RU), `docs/guide/provider-specific/postgresql.md` (+RU); document `[JsonColumn]` native read/write and separate bare `JsonDocument`/`JsonElement`, unsupported bare `JsonNode` read, plain string/explicit `json_cast`; no DDL promises. `docs/advanced/limitations.md` untouched. |
| **D131.4** | Spec sync: link #131 to new verification evidence in `docs/specs/design/API-NAMING-REVIEW.md:4951`; check consistency of `linq2db-backlog-gap-analysis.md:56,143-150`, `linq2db-comparison.md:147`, `capability-matrix.md:89,107` (keep existing Done); find or create the `JsonNode` follow-up issue in milestone `1.0.9-rc1` and record its URL. |
| **D131.5** | Verify: build `nextorm.slnx` 0/0; PG integration (`PostgresJsonColumnTests` + the 3 existing `CommonTestSuite.JsonColumn` scenarios, none skipped, PG version recorded); `nextorm.postgres.tests` SQL-gen suite; `dotnet docfx`; `git diff --check`; CRLF. Append results here. |

### Variant matrix

| Dimension | Variants | Decision |
|---|---|---|
| Physical column type | `json`, `jsonb` | both exercised by D131.2 |
| `[JsonColumn]` storage | `Auto` (default) | exercised; `Native`/`Text` already covered by core/sqlgen tests |
| Operation | insert→read, update→read | both exercised |
| Nullability | non-null POCO, nullable POCO = null | both exercised |
| Payload | default-valued props, nested object, Unicode | all exercised |
| Mapping re-implementation | none vs new provider hook | **none** — already implemented at HEAD |
| DDL generation | promise vs none | **none** — docs must not promise it |

### Perf

**Not required** — no production code changes (docs/test/verification closure only). No query-path,
plan-cache or per-row code is touched.

### Reconnaissance

**None required** — the reconnaissance below is carried forward unchanged from the plan-of-record;
the API-location facts were re-validated while drafting this plan.

## Reconnaissance (persisted; not yet re-verified)

- Issue #131 "PostgreSQL: маппинг нативной колонки json/jsonb"; OPEN; enhancement; milestone 1.0.9-rc1; no PRs/comments. Body: native json/jsonb column not mapped (mapping only in parameter path); asks column mapping read+write; acceptance = tests, docs EN+RU.
- **MAJOR contradiction:** HEAD **already has** a committed, documented, PG-native `[JsonColumn]` jsonb path (commits `df475601 #31`, `723776a0 #88`, 2026-09-25); `docs/specs/design/API-NAMING-REVIEW.md:4951` records a verified native jsonb round-trip on real PostgreSQL (4/4). So the issue premise is **stale at HEAD**; `docs/guide/14-json.md:357-358,:32` still frame native column mapping as parameter-path-only.
- **Scope ambiguity:** read+write yes, but which CLR types — POCO via `[JsonColumn]` (already works), or bare `JsonDocument`/`JsonElement`/`JsonNode`/`string`? Likely remaining work is (a) documentation/verification closure or (b) direct non-`[JsonColumn]` mapping + `string`→jsonb binding. Needs a PLAN decision (possibly escalate).
- Code: `PostgresDialect.cs:325 SupportsJson=>true`; `PostgresDataContext.cs:151-152` (JsonDocument/JsonElement/JsonNode param → `NpgsqlDbType.Jsonb`; `:149-150` plain string → text); `SelectExpression.cs:246-253` (`GetFieldValue<JsonDocument|JsonElement>`); `RowMapperFactory.cs:56-58,111-147`; `SqlDialectBase.cs:93`/`ISqlDialect.cs:285`; `MemberTranslator.cs:166` (`IJsonColumnConverter`).
- Existing converter/registration: `Meta/JsonColumnConverter.cs` (`JsonColumnStorage{Auto,Native,Text}:9`, `JsonColumnAttribute:28`, `JsonColumnOptions:40`, `JsonColumnConverter<TModel,TProvider>:62`, `JsonColumnAutoConverter<TModel>:145`, Auto picks native when `dialect.SupportsJson` `:177-190`); `Meta/EntityMetadataBuilder.cs:336-360`; `Meta/EntityPropertyBuilder.cs:221-277`; `TableParameterBinder.cs:158-160`. No provider/DDL registry mapping bare CLR json types outside `[JsonColumn]`.
- Tests: `tests/nextorm.integration.tests/CommonTestSuite.JsonColumn.cs:8,17,41,92,121` (round-trip/returning/queries) run on PG via `PostgresIntegrationTests.cs:8`; `tests/nextorm.postgres.tests/JsonColumnSqlGenerationTests.cs:44-82`; `tests/nextorm.core.tests/JsonColumnTests.cs:44`; `PostgresDialectTests.cs:183`; `PostgresFunctionsTests.cs:681`.
- Docs EN/RU: `docs/guide/14-json.md:16-17,171-181,331,341-343,357-358` + RU; `docs/infrastructure/04-value-converters.md:120-125,180,206,247` + RU; `docs/guide/provider-specific/postgresql.md:108-126` + RU; `docs/providers/postgres.md:26-27,216-217,330` + RU; `docs/scalar-functions/07-json-and-xml.md` + RU. Specs `comparison/capability-matrix.md:89,107`, `comparison/linq2db-comparison.md:147`, `comparison/linq2db-backlog-gap-analysis.md:56,144-145`.
- Issue doc links point to non-existent `docs/guide/16-json.md` (renumbered to `14-json.md`).

### Evidence contract

| ID | Invocation / artifact | Phase | Required result |
|---|---|---|---|
| **E131-01** | this status file, `## Locked plan (revision r=1)` | plan | plan persisted with acceptance, DO units, variant matrix, evidence contract |
| **E131-02** | `dotnet build nextorm.slnx -c Debug` | build | exit 0, 0 warnings / 0 errors; log `artifacts/d131/build.log` |
| **E131-03** | `dotnet run --project tests/nextorm.integration.tests -c Debug -- -class nextorm.integration.tests.PostgresJsonColumnTests` with `DOCKER_HOST` | integration | exit 0; new json + jsonb cases executed, none skipped; log + result XML `artifacts/d131/pg-integration*.xml` |
| **E131-04** | PG integration run, `CommonTestSuite.JsonColumn` scenarios | integration | 3 existing scenarios executed (pass), not skipped; PG server version recorded |
| **E131-05** | `dotnet test tests/nextorm.postgres.tests -c Debug` | sqlgen | exit 0 + total/passed/failed/skipped counts; log `artifacts/d131/pg-sqlgen.log` |
| **E131-06** | `dotnet docfx docs/docfx.json` | docs | exit 0 (pre-existing warnings acceptable, recorded); log `artifacts/d131/docfx.log` |
| **E131-07** | `git diff --check` + CRLF normalisation of every touched file | hygiene | exit 0, no whitespace errors, CRLF preserved; log `artifacts/d131/git-diff-check.log` |
| **E131-08** | `docs/specs/**` spec consistency (API-NAMING-REVIEW link, backlog/comparison/capability rows) | specs | updated/linked, existing Done kept |
| **E131-09** | `gh issue` `JsonNode` follow-up in milestone `1.0.9-rc1` | issue | number + verified URL recorded here |

CHECK re-gather budget: **2** targeted evidence requests per CHECK invocation.

### Evidence results (DO verification, 2026-10-06)

| ID | Result | Evidence |
|---|---|---|
| **E131-01** | PASS — plan persisted (decision, acceptance R131-01..R131-07, DO units D131.1–D131.5, variant matrix, evidence contract) | this file, `## Locked plan (revision r=1)` |
| **E131-02** | PASS — `dotnet build nextorm.slnx -c Debug` exit 0, **0 Warning(s) / 0 Error(s)** | `artifacts/d131/build.log` |
| **E131-03** | PASS — new `PostgresJsonColumnTests` **2/2** against real PostgreSQL **17.11** (Total 2, Errors 0, Failed 0, Skipped 0); physical `json` and `jsonb` (insert→read, update→read, nullable/SQL NULL, default-valued + nested, Unicode) | `artifacts/d131/pg-new-json-column.log`, `artifacts/d131/pg-new-json-column.xml` |
| **E131-04** | PASS — `CommonTestSuite.JsonColumn` **3/3** (Total 3, Errors 0, Failed 0, Skipped 0); server version recorded | `artifacts/d131/pg-common-json-column.log`, `artifacts/d131/pg-common-json-column.xml`, `artifacts/d131/pg-version.txt` |
| **E131-05** | PASS — `dotnet test tests/nextorm.postgres.tests -c Debug` exit 0, total **770**, succeeded **770**, failed **0**, skipped **0** | `artifacts/d131/pg-sqlgen.log` |
| **E131-06** | PASS — `dotnet docfx docs/docfx.json` exit 0, **0 error(s)**, 2 pre-existing warnings (duplicate `AnalyzerReleases.{Shipped,Unshipped}.md` in `nextorm.core.sourcegenerator`) | `artifacts/d131/docfx.log` |
| **E131-07** | PASS — `git diff --check` exit 0 (no whitespace errors); every touched file CRLF, **0 lone LF** | `artifacts/d131/git-diff-check.log` |
| **E131-08** | PASS — EN+RU docs updated (`guide/14-json`, `guide/provider-specific/postgresql`, `providers/postgres` + RU); specs synced: `API-NAMING-REVIEW.md` #131 cross-check, `linq2db-backlog-gap-analysis.md` native `json`/`jsonb` regression + #197, `capability-matrix.md:89,107` native `[JsonColumn]` (#131), `linq2db-comparison.md:147` unchanged | git working tree |
| **E131-09** | PASS — follow-up **#197** in milestone `1.0.9-rc1`, state OPEN | `artifacts/d131/jsonnode-issue.txt`, https://github.com/AlexeyShirshov/nextorm/issues/197 |

No production code changed (`src/**` untouched); the change set is EN+RU docs, one new integration test, and the spec/status files.

## Progress log

`2026-10-06T00:26:25Z | DO | r1 | 1/3 | plan locked (decision: already-implemented + docs/verification closure; acceptance R131-01..R131-07; DO units D131.1-D131.5; variant matrix; evidence contract E131-01..E131-09; CHECK re-gather budget 2); recon carried forward | docs/specs/status/rc1-131-pg-json-column-1.md`

Recon persisted (run 7 start).

- Notice: host has no todowrite tool; this file is the progress log.
`2026-10-06T00:33:05Z | DO | r1 | 1/3 | verification complete: build 0/0; postgres sqlgen 770/770 (0 failed/skipped); docfx exit 0 (0 errors, 2 pre-existing warnings); git diff --check exit 0 + CRLF clean; PG integration new PostgresJsonColumnTests 2/2 + CommonTestSuite.JsonColumn 3/3 (PostgreSQL 17.11, 0 skipped); follow-up #197; capability-matrix row 107 finished consistent with row 89; no production code changed | docs/specs/status/rc1-131-pg-json-column-1.md, artifacts/d131/`

## Re-run evidence (rv1, 2026-10-06)

Re-run on revision r=1 to capture explicit process exit codes (`; echo $? > <file>`), with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`. `--no-build` resolved the already-built integration assembly in both runs (no fallback to a building run needed).

| ID | Exit | Counts | Artifacts |
|---|---|---|---|
| **E131-03** | **0** | Total 2, Errors 0, Failed 0, Skipped 0 (`PostgresJsonColumnTests`: `JsonColumn_PhysicalJsonb_ShouldRoundTrip`, `JsonColumn_PhysicalJson_ShouldRoundTrip`) | `artifacts/d131/pg-new-json-column-exit.txt`, `artifacts/d131/pg-new-json-column.log`, `artifacts/d131/pg-new-json-column.xml` |
| **E131-04** | **0** | Total 3, Errors 0, Failed 0, Skipped 0 (`PostgresIntegrationTests`: `JsonColumn_ShouldRoundTrip`, `JsonColumn_Returning_ShouldMaterialize`, `JsonColumn_Queries_ShouldConvertConstantsAndProjections`; filter `-method "NextORM.Integration.Tests.PostgresIntegrationTests.JsonColumn_*"`) | `artifacts/d131/pg-common-json-column-exit.txt`, `artifacts/d131/pg-common-json-column.log`, `artifacts/d131/pg-common-json-column.xml` |
| **E131-05** | **0** | total 770, succeeded 770, failed 0, skipped 0 | `artifacts/d131/pg-sqlgen-exit.txt`, `artifacts/d131/pg-sqlgen.log` |

PostgreSQL server version: 17.11 (`artifacts/d131/pg-version.txt`). No production code changed.

`2026-10-06T05:37:50Z | DO | r1 | 1/3 | re-run evidence: E131-03 exit 0 (Total 2, Failed 0, Skipped 0); E131-04 exit 0 (Total 3, Failed 0, Skipped 0); E131-05 exit 0 (total 770, succeeded 770, failed 0, skipped 0); explicit exit files + logs in artifacts/d131/ | artifacts/d131/pg-new-json-column-exit.txt, artifacts/d131/pg-common-json-column-exit.txt, artifacts/d131/pg-sqlgen-exit.txt`

## ACT (2026-10-06)

- CHECK: **PASS** — plan freeze point `r=1, n=1/3, rv1`; all E131-01..E131-09 satisfied with explicit exit codes (build 0/0; PG new `PostgresJsonColumnTests` 2/2 + existing `CommonTestSuite.JsonColumn` 3/3, 0 skipped, PostgreSQL 17.11; `nextorm.postgres.tests` 770/770; docfx 0 errors, 2 pre-existing warnings; `git diff --check` clean + CRLF).
- Commit plan: one D131 commit for `docs/guide/14-json.md` + RU, `docs/providers/postgres.md` + RU, `docs/guide/provider-specific/postgresql.md` + RU, `docs/specs/comparison/capability-matrix.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`, `tests/nextorm.integration.tests/PostgresJsonColumnTests.cs`, this status file; plus a separate collection bookkeeping commit for `docs/specs/status/collection-1.0.9-rc1.md`. No push, no merge, no `git add -A`.
- Disposition: **already-implemented + verification/docs closure** — no production mapping change; the committed PG-native `[JsonColumn]` read/write path (commits `df475601`, `723776a0`) is verified on real PostgreSQL for both physical `json` and `jsonb`; EN/RU docs corrected (stale parameter-path-only claims removed).
- Follow-up: **#197** — bare `JsonNode` read support, milestone `1.0.9-rc1`, OPEN (https://github.com/AlexeyShirshov/nextorm/issues/197).
- Issue outcome: **#131 closed** as already-implemented + docs/verification closure.

`2026-10-06T05:40:00Z | ACT | r1 | 1/3 | CHECK PASS (rv1); disposition already-implemented + verification/docs closure (no production change); D131 change set committed; collection bookkeeping separate; follow-up #197; #131 closed | this file, commit SHA in collection status`
