# #178 JSON streaming: enum unsupported (plan intended numeric) — cycle status

## Durable state

- task_id: `D178` (GitHub issue [#178](https://github.com/AlexeyShirshov/nextorm/issues/178), milestone `1.0.9-rc2`)
- selected_variant: `pdca-dotnet`
- Current cycle N: 1
- Plan revision r: 1
- Attempt n: 2/3
- Evidence contract revision rv: 1
- Phase: ACT
- plan_state: **ACT complete** — CHECK PASS r=1 n=2 rv=1; D178 complete (all R178-01..10 met, T01–T17 closed, E178-01..12 closed)
- Defect history: `D178-T11-precancel-wrong-exception` (rev r=1, attempts n=1→n=2; 1 fix applied: `cancellationToken.ThrowIfCancellationRequested()` at `DataContext.PrepareJsonStream` `DataContext.cs:375-377`, mirroring `OpenLobReader`); observed revisions/attempts: r1/n1 (red `InvalidOperationException "no selected columns"`) → r1/n2 (green `OperationCanceledException`); last outcome: closed, evidence `artifacts/pdca/178/r1/red-precancel.log`, `shape-tests.log`
- Base: branch `1.0.9-rc2` at `18659e41` (clean except untracked `StrykerOutput/`)

## Goal

JSON streaming must serialize `enum` projections without materializing rows: by default as a JSON number equal to the underlying numeric value, and — for a supported STJ `[JsonConverter]` string-enum form — as the string representation, without breaking the existing streaming writer, fail-fast converter policy, resource ownership or the shared query-command/plan-cache invariants.

## Minimal solution (first principles)

- **Q1 goal in essence:** support enum on the JSON-streaming path (number by default; string for a supported STJ enum-string converter), no buffering.
- **Q2 non-negotiable constraints:** keep the compiled per-row writer (`JsonRowWriterFactory.cs:74,85,121`); do not change the buffered mapper (`SelectExpression.cs:178,289-290` — separate problem); keep `column.Converter != null` rejected (`JsonShapePlan.cs:128-130`); do not ignore a detected unsupported JSON converter; shape/metadata errors before output; no per-row reflection/converter resolution; preserve nullable/default-on-null, resource ownership, SQL/parameter parity, no shared command mutation; CRLF, .NET 10, warnings-as-errors; no new public API.
- **Q3 minimal solution:** (1) in the shape plan, classify enum by its underlying integral type → numeric writer; (2) recognize the stock `JsonConverterAttribute` carrying `JsonStringEnumConverter` / `JsonStringEnumConverter<TEnum>` → pre-built typed string converter writer; (3) property attribute takes precedence over enum-type attribute; (4) `column.Converter != null` remains an unconditional rejection (a text-stored enum column is not what string representation means); (5) all eight integral underlying types without narrowing `uint`/`ulong`.
- **YAGNI:** no global serializer options, converter registry, generic object serialization, arbitrary text-enum parsing, or raw/buffered mapping changes.

## `[JsonConverter]` decision

Options: **A** numeric-only + reject attributes (does not satisfy the string requirement; even honest fail-fast needs detection); **B** numeric + narrow allowance of stock STJ string-enum attributes (satisfies the issue, keeps the general converter ban; needs member provenance + preflight validation + a cached typed converter); **C** allow nextorm `EnumToStringConverter` (changes the storage-converter contract `JsonShapePlan.cs:128-130`, does not satisfy the STJ `[JsonConverter]` promise, higher risk).

**Chosen: B.** It is a documented enum-only exception for JSON representation, not a removal of the general fail-fast. The internal draft `todo_json_streaming.md` is not an approved design (`:4-6`); its converter stance is updated explicitly together with the public limitations. Confidence: medium (see Residual reconnaissance).

## Acceptance criteria

| ID | Observable behavior | Negative case |
|---|---|---|
| R178-01 | Enum without a JSON attribute yields a JSON number equal to the underlying value, incl. signed negatives and `ulong.MaxValue` | Must not write the enum name, narrow the number, or overflow a signed type |
| R178-02 | A supported string-enum attribute on type/member yields output equivalent to STJ for the same converter (names, flags, unnamed values) | An unsupported attribute is not ignored; error before output |
| R178-03 | Scalar `oneColumn` and flat object member; nullable enum keeps existing null/default rules | `NULL` must not be silently turned into a zero/default enum |
| R178-04 | Numeric provider fields are read provider-aware; SQL and parameters match normal execution of the same query | A text/native-unsupported field is not silently converted; out-of-range is not truncated |
| R178-05 | Both API surfaces work sync/async; token honoured; existing modes/root/options preserved | In-memory stays fail-closed; unsupported option combinations still rejected |
| R178-06 | Shape/converter errors occur before any output, including for an empty result; a valid empty result keeps its existing shape | Missing validation may not be excused by "no rows" |
| R178-07 | Destination stays owned by the caller; reader/connection disposed on success/error/cancellation | An enum-writer error must not leave an open reader or close the user stream |
| R178-08 | Existing JSON/CSV/converter/cache behavior does not regress | Must not allow storage converters or mutate the sticky cache flag for enum |
| R178-09 | EN/RU docs match the implemented support boundary | Must not promise enum support while leaving enums in the generic rejected list |
| R178-10 | New path has measured runtime/allocations; no per-row reflection/converter resolution | "Expression built once" does not replace measuring per-row work |

Scenario IDs below are planning IDs, not claims about existing CLR test symbols; real identities are recorded after tests are added (Roslyn discovery).

## PLAN gather results (provenance)

Resolved statically in PLAN (facts, `file:line`):
- `JsonShapeColumn` copies only `Index`, name, `valueType`, `Kind`, `Nullable`, `DefaultOnNull` from `SelectExpression` (`JsonShapePlan.cs:166`); it has no member/converter field (`:35-69`).
- `SelectExpression` exposes `internal PropertyInfo? PropertyInfo` (`SelectExpression.cs:29`), `Expression` (`:26`), `PropertyType` (`:28`), `ProviderType` (`:49`), `Converter` (`:55`), `ProjectionItem` (`:117`).
- Whole-entity select populates `PropertyInfo` (`EntitySelectListBuilder.cs:68`).
- Expression-lambda scalar `x => x.SomeEnum` does NOT set `PropertyInfo` (`QueryCommand.QueryPreparer.cs:563-577`); the member is reachable by walking `SelectExpression.Expression` (LambdaExpression body → `MemberExpression.Member`) (`:571`, `MemberTranslator.cs:277`).
- Raw/`FromSql` path copies `PropertyInfo` from entity metadata (`RawMapperFactory.cs:642,651`); scalar/CTE output reference (`ProjectionOutputColumn`) does not copy `PropertyInfo` (`QueryPreparer.cs:761-780`).
- No STJ attribute reading exists in core; the nearest metadata is per-column `SelectExpression.PropertyInfo`/`Expression` (no `IEntityMetadata` is passed to `JsonShapePlan.Build`).
- Internals are visible to test projects (`src/nextorm.core/nextorm.core.csproj:54,57,60,63,64,65,68`); JSON internals already used at `tests/nextorm.clickhouse.tests/JsonRowWriterFactoryClickHouseDecimalTests.cs:93,97`.

Conclusion: property-level enum converter provenance is reachable within the `Query/Json/*` footprint + same-assembly `SelectExpression` access; **no shared-mapping change is required**. `Classify` must be widened to receive the member provenance (from `column.PropertyInfo` and/or the lambda body of `column.Expression`) in addition to `Type`.

## Residual reconnaissance (executable probe, DO unit D178.1)

Provider runtime `GetFieldType` for enum columns cannot be answered statically and is an executable probe (test infrastructure), with observable criteria: confirmed attribute→plan path, successful numeric-enum `PrepareJsonStream`, and a table of actual per-provider field types. If the probe refutes the minimal design, this is a DO→PLAN loop-back, not improvised.

## Variant matrix (every row closed: test / guard / deferred + trigger)

| Scenario | Closure and evidence |
|---|---|
| T01 Numeric underlying widths (sbyte/byte/short/ushort/int/uint/long/ulong) | **Test:** zero, signed min, max; direct controlled `IDataRecord`. SQLite is not used to prove `ulong.MaxValue` storage |
| T02 Undefined + Flags numeric | **Test:** unnamed numeric value and flags stay numbers |
| T03 Nullable + DefaultOnNull | **Test:** nullable value/null; existing column nullable/default semantics; negative: no unintended zero substitution |
| T04 Scalar + flat member | **Test:** scalar and named object member incl. projection/source member provenance |
| T05 String attribute on type + member | **Test:** both stock converter forms; member precedence; bytes/values vs STJ |
| T06 String converter edge values | **Test:** flags, unnamed value; `allowIntegerValues=false` variant via derived converter — **guard** (custom converter out of scope) |
| T07 Unsupported converter before output | **Test:** custom JSON attribute/converter and non-null nextorm `column.Converter`; error identifies property/type; output length zero |
| T08 Numeric storage + text rejection | **Test:** numeric stored column. **Guard/test:** string field, `EnumToStringConverter`, unsupported native field |
| T09 Numeric provider mismatch / out-of-range | **Test:** incompatible field type and out-of-range; no truncation (data error may leave previously written rows) |
| T10 Both surfaces sync/async | **Test:** `QueryCommand` and `EntityBuilder` × sync/async; identical JSON and SQL/parameters |
| T11 Cancellation + ownership | **Test:** pre-cancel + cancel during enumeration; token observed; stream stays open; reader disposed on error/cancellation |
| T12 In-memory fail-closed | **Test:** both surfaces/async stay unsupported; `tests/nextorm.core.tests/InMemoryTests.cs:127-154` |
| T13 Empty + non-empty | **Test:** valid empty/non-empty shape; invalid attributed shape rejected even when empty |
| T14 Modes + options | **Test:** enum in existing modes/root/indent/naming-policy cases. **Guard:** NdJson+Root / NdJson+WriteIndented stay errors (`JsonShapePlan.cs:112-114`) |
| T15 Provider field types | **Test:** SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse; record actual field type and expected output; unknown types probed first |
| T16 Existing streaming regression | **Test:** decimal/bytes/unsupported `DateTimeOffset`; CSV converter rejection preserved (`CsvStreamTests.cs:72-87,478-504`) |
| T17 Parameters + shared cache | **Test + audit:** parameter parity and repeated calls in one context; no sticky command-state mutation |
| Native/text enum parsing; arbitrary converters | **Guard**, expansion **deferred + trigger** (see DO §Deferred); the guard obligation is not deferred |

## Priority matrix (PLAN assigns; CHECK applies and may not downgrade)

| Row | Priority | Basis |
|---|---|---|
| Numeric/default and string-attribute behavior, exact widths, null, scalar/member | P1 | #178 invariants |
| Both surfaces, sync/async, CancellationToken | P1 | Streaming-terminal class table |
| SQL/parameter parity, fail-closed | P1 | Streaming-terminal class table |
| Resource ownership/error cleanup | P1 | Streaming-terminal class table |
| All six providers, actual field types | P1 | Streaming-terminal class table |
| Unsupported converters before output; empty-result validation | P1 | PLAN-added fail-fast protection |
| Shared command/cache immutability; no unrelated mapper change | P1 | Project invariants |
| New-code coverage and branch sensitivity | P1 | Execution-path proof |
| Public EN/RU contract documentation | P1 | New public support boundary |
| Perf evidence and no per-row reflection/resolution | P1 | New per-row execution path |
| Supplemental prose/style cleanup | P2 | Does not affect criteria |

## Test strategy

- **Direct writer/unit:** exhaustive type/converter/null/overflow validation with a controlled `IDataRecord`; do not widen production visibility for a test.
- **SQLite end-to-end:** `tests/nextorm.sqlite.tests/JsonStreamingTests.cs` (real in-memory SQLite fixture `:19-76`), surfaces/options/output.
- **Real integration:** shared helpers `tests/nextorm.integration.tests/CommonTestSuite.JsonStream.cs:8-18`; provider storage behavior in `*SpecificTests.cs`. Load the integration-test skill before running; PostgreSQL/SQL Server/MySQL/ClickHouse must really run via `DOCKER_HOST` — skips are not a pass.
- Regression is red→green: numeric enum currently throws (`JsonShapePlan.cs:196-198`).

Affected subsets (existing class selectors; integration per-unit selector to be confirmed by D178.1 gather):

```text
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~JsonStreamingTests"
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~CsvStreamTests"
dotnet test tests/nextorm.core.tests  -c Debug --filter "FullyQualifiedName~InMemoryTests"
dotnet test tests/nextorm.clickhouse.tests -c Debug --filter "FullyQualifiedName~JsonRowWriterFactoryClickHouseDecimalTests"
```

Single DO→CHECK boundary sweep (build + full test + coverage):

```text
dotnet build -c Debug
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
dotnet-coverage collect "dotnet test -c Debug" \
  --settings coverage.settings.xml --output-format cobertura \
  --output artifacts/pdca/178/r1/boundary.cobertura.xml
reportgenerator \
  -reports:artifacts/pdca/178/r1/boundary.cobertura.xml \
  -targetdir:artifacts/pdca/178/r1/coverage \
  -reporttypes:"Html;TextSummary;Cobertura"
```

- Coverage: new executable lines ≥ 85%, branches ≥ 75% (`.github/workflows/dotnet.yml:27-28`); show covered/total new branches and branch delta. Included modules core|sqlite|postgres|sqlserver (`coverage.settings.xml:12`).
- Stryker: no separate run by default; CHECK must prove sensitivity of the negative/boundary tests to enum-branch deletion, numeric narrowing and removal of the converter guard; if not proven, add a targeted mutant experiment on the two changed JSON files only (not solution-wide).

## Documentation plan

Change: `docs/guide/28-streaming-data.md` (`:67-87`) and RU (`:75-77`) — supported enums, stock string attributes, numeric-storage scope, custom/storage converter rejection; `docs/advanced/api-reference.md:89` and RU (`:89`); internal `docs/specs/roadmap/todo_json_streaming.md` (`:123,:280`, stale `DataContext/Json/` path, converter stance `:24-25,:135,:200,:234-236,:326`). Do not touch `docs/advanced/limitations.md` (no enum row), `14-json.md`, `26-large-objects.md`, readme unless gather finds a contradiction. Public docs must not link to `docs/specs/**`; verify `dotnet docfx docs/docfx.json` exit 0.

## Performance decision

**Needed.** Enum conversion/write runs per row (`JsonRowWriterFactory.cs:85,121,130-155`) even though the expression is compiled once (`:74`). Planned benchmark: integral streaming baseline and new numeric/string enum path, same data/stream/buffering, warm writer, varying result sizes; BenchmarkDotNet runtime + allocated bytes/row; converter resolution/shape construction measured separately. Acceptance: no per-row reflection/resolution, numeric enum adds no boxing allocation vs the integral baseline; a sustained throughput regression > 10% requires investigation, not an automatic PASS. Raw results, environment and repeatability required.

## Reconnaissance decision

**Needed.** A bounded spike (D178.1) resolves the executable unknowns: per-provider actual `GetFieldType` for enum columns and the confirmed attribute→plan path. Expected result = observable facts (probe command + numbers); this is a spike inside DO, not a search for a new general serialization design. The static provenance question is already closed (see PLAN gather results).

## Unit execution mode

**Sequential, one tree, one active unit `D178`.** Shape and writer share a contract; parallel edits are not allowed. No worktree.

## Footprint

- `src/nextorm.core/Query/Json/JsonShapePlan.cs`
- `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs`
- `src/nextorm.core/DataContext/DataContext.cs` — only if a minimal JSON metadata handoff is confirmed
- `tests/nextorm.sqlite.tests/JsonStreamingTests.cs`
- `tests/nextorm.core.tests/InMemoryTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite.JsonStream.cs` + confirmed `*SpecificTests.cs`
- `tests/nextorm.clickhouse.tests/JsonRowWriterFactoryClickHouseDecimalTests.cs` (probe/regression only, no decimal rework)
- planned benchmark source in `benchmarks/nextorm.benchmark/`
- the docs above and this status file

Not expected to change: `RowMapperFactory`, `SelectExpression` public surface, public terminal signatures, metadata conversion subsystem.

**Uncertainty:** per-provider `GetFieldType` for enum columns; availability of the property attribute for the scalar-lambda form (resolved to `Expression` walking — may be fragile, confirm in D178.1); whether `uint/ulong` round-trip on each provider.

**Predecessor-result requirements:** no functional dependency on nested/native JSON. D176/D177/D179/D180 overlap on `src/nextorm.core/Query/Json/*` and JSON tests — the collection must serialize this cluster. If D176/D177 ran first, require their integrated changes, CHECK verdict and current shape/writer contract before D178; after D178, later tasks must adopt the updated contract. No commit/merge/push authorization.

**Assumptions/prerequisites:** enum is numeric at provider level (params/TVP evidence: `TableParameterBinder.cs:94-95,257-260`); JSON streaming phase-1 whitelist is intentional and extension is additive; no STJ-attribute-scan infrastructure exists today; in-memory stays fail-closed; base `1.0.9-rc2` clean.

## DO task list (all fix-now; D178 stays active until CHECK)

1. **D178.1 — recon probe + red regression.** Fix that a plain numeric enum currently throws (`JsonShapePlan.cs:196-198`); run the confirmed prepare/provider probes and record actual per-provider field types. (`tests/nextorm.sqlite.tests/JsonStreamingTests.cs:412,586`; `CommonTestSuite.JsonStream.cs:8-18`.)
2. **D178.2 — shape/preflight.** `JsonShapePlan.cs:10-29,107-166,172-198`; widen `Classify` to receive member provenance; add enum classification, prepared metadata/converter info, explicit supported-attribute validation; keep early rejection of storage converters, LOB, invalid options.
3. **D178.3 — typed reads/writes.** `JsonRowWriterFactory.cs:130-155,184-194,230-275`; cover all integral types; do not narrow `uint`/`ulong`; build numeric/string enum expressions once; preserve null branching.
4. **D178.4 — execution-path tests and regressions.** Matrix scenarios; provider-specific cases in confirmed `*SpecificTests.cs`.
5. **D178.5 — benchmark and documentation.** Planned benchmark source; public EN/RU docs + internal plan doc Q3/table.
6. **D178.6 — normalization and handoff.** CRLF, XML-doc if mandated, build diagnostics, evidence index, single boundary sweep. The DO report does not declare D178 done.

**Deferred:** arbitrary custom JSON converters and native/text storage parsing — explicit guard + tracked record in #178 or a separate verified issue **in the same milestone 1.0.9-rc2**. Triggers: a concrete requirement for such a converter/storage plus an agreed contract. If judged an obligatory part of #178, D178 does not close until done; nothing is silently moved to a future milestone.

## Evidence contract rv1

Artifact root (planned, not yet existing): `artifacts/pdca/178/r1/`. Every row carries: stable IDs, priority, scenario, evidence kinds/sources, exact invocation, exit/result requirements, artifacts, owner, applicability, `rv`. CHECK re-gather budget: max **2** targeted packs, owner `check`.

| Row / requirements | P | Check + evidence sources + invocation | Result / artifacts | Owner; applicability; rv |
|---|---|---|---|---|
| E178-01 / R01–R08 | P1 | Code audit: `scout`/Roslyn audit of final changed symbols/call paths; `git diff --check`; `git diff -- src/nextorm.core` | exit 0; actual `file:line`; no unexplained scope/cache/converter relaxation; `audit.md`, base/diff ids | scout→check; always; 1 |
| E178-02 / R01 | P1 | Historical regression: SQLite filtered run before/after; T01/T04 | before: enum `NotSupportedException` (not setup failure); after: assertions pass; `red.log`,`green.log`, identities | coder; always; 1 |
| E178-03 / R01–R03,R06 | P1 | T01–T07,T13 via SQLite class + direct-record cases | exit 0, nonzero applicable tests, width/converter/null assertions; `shape-tests.log`, identity index | coder→check; always; 1 |
| E178-04 / R05,R07,R08 | P1 | T10–T12,T14,T16,T17; the four affected-subset commands | exit 0; cancellation/ownership/cache/CSV regressions checked; `terminal-tests.log` | coder→check; always; 1 |
| E178-05 / R04,R05 | P1 | T08,T09,T15; provider probes + integration execution from the boundary sweep | all six provider outcomes and actual field types; required providers not skipped; `provider-matrix.md`, provider logs | coder; verdict check; always; 1 |
| E178-06 / R01–R08 | P1 | Single boundary build/test/coverage sequence | exit 0 all commands; positive executed tests; no unexplained skip/failure; `boundary.log`, coverage XML/HTML | coder; verdict check; always; 1 |
| E178-07 / R01–R08 | P1 | `check`: match boundary coverage XML to final diff; branch-sensitivity scenarios | new lines ≥85%, branches ≥75%; covered/total + delta; explicit sensitive negatives; `coverage-delta.md` | check; always; 1 |
| E178-08 / R09 | P1 | Doc audit EN/RU/internal pages; `dotnet docfx docs/docfx.json` | exit 0; no contradictory enum/converter claim, no public specs links; `docs-audit.md`, `docfx.log` | coder+check; always; 1 |
| E178-09 / R10 | P1 | Benchmark invocation; per-row source audit | exit 0, cases executed; baseline/final BDN exports, allocation/ratio analysis; `perf.md`, raw artifacts | coder; verdict check; always; 1 |
| E178-10 / all | P1 | `check`: verify evidence index against every R/E row, final diff and status | no missing/mismatched sources or open mandatory variants; `check.md` with verdict | check; always; 1 |
| E178-11 / security | P1 if triggered | `scout` security-impact audit then `check` boundary assessment | expected justified N/A now; if arbitrary converter execution/dynamic loading/auth/secrets change appears → mandatory threat/negative evidence before PASS | scout+check; observable trigger; 1 |
| E178-12 / tracking | P1 | `gh issue view 178 --json url,milestone,title`; status/doc audit | exit 0; verified URL, milestone 1.0.9-rc2; unsupported slices tracked, not lost; `tracking.json` | scout/coder; always; 1 |

Security is not silently waived: the task does not change the auth/secrets boundary and arbitrary converter execution is deliberately forbidden; JSON/DB input safety is covered by the P1 field/converter guards and negative tests. If the global contract contains additional obligations, they are added before DO. Current `rv=1`; no supersession. A justified revision writes `rv1 → rv2`, retains all E/R IDs and obligations, and adds IDs for new variants. A plan revision increases `r` and resets `n=1`; a missing report or rename does neither.

## Risks

- Main risk: property attribute provenance / a possible early enum getter — classified as a missing-evidence gap, closed by the D178.1 probe (static provenance already resolved).
- Native/text enum and `ulong` storage differ across providers; a direct writer test does not replace DB evidence.
- String-enum flags/undefined behavior must follow the declared converter contract, not a hand-rolled `.ToString()`.
- Overlap with D176/D177/D179/D180 requires sequential collection integration.
- A numeric-only completion would be a reduction of the original requirement and is not accepted automatically.
- If an extra prerequisite appears, D178 stays active/blocked, not superseded and not done.
- If the probe still leaves no confident design, the orchestrator escalates (trigger 5) with the question: "How to ensure property-level STJ enum converter provenance in JSON streaming without violating the mapper/fail-fast contracts and without widening the shared mapping scope?"

## Progress log

```text
2026-10-07T18:21Z | PLAN | revision 1 | iteration 1/3 | PLAN ready — collection handoff (plan_state=ready; DO not started) | docs/specs/status/rc2-178-json-enum-1.md
2026-10-08T20:51Z | DO | revision 1 | iteration 1/3 | DO started (D178.1–D178.3) | artifacts/pdca/178/r1/scope.json
2026-10-08T20:57Z | DO | revision 1 | iteration 1/3 | red→green enum JSON: JsonStreamingTests red exit 2 (8 failed/63 passed) → green exit 0 (71/71); CsvStreamTests 23/23 exit 0; InMemoryTests 159/159 exit 0; JsonRowWriterFactoryClickHouseDecimalTests 1/1 exit 0; core + nextorm.slnx build 0 warnings/0 errors | artifacts/pdca/178/r1/{red,green,csv,inmemory,clickhouse-decimal}.log
2026-10-08T20:59Z | DO | revision 1 | iteration 1/3 | D178.4 matrix added (T01–T17 subset: widths, undefined/flags, default-on-null, string flags/unnamed, storage-converter + unsupported-attribute rejection, bad provider field, sync/async, options/modes, empty, cache): JsonStreamingTests 81/81 exit 0; subsets Csv 23/23, InMemory 159/159, ClickHouseDecimal 1/1 all exit 0; provider-specific/integration T15 deferred to boundary | artifacts/pdca/178/r1/green.log
2026-10-08T21:00Z | DO | revision 1 | iteration 1/3 | D178.5 docs EN/RU guide + api-reference + internal roadmap updated (enums numeric/string-converter, storage-converter rejection, deferral bullet corrected); benchmark source `JsonStreamEnumBenchmark.cs` added (container-free direct-writer; BDN run pending); `validate_inner_loop.py report evidence.json` exit 0; final `dotnet build nextorm.slnx -c Debug` 0 warnings/0 errors | artifacts/pdca/178/r1/{evidence.json,final-build.log}
2026-10-09T02:12Z | DO | revision 1 | iteration 1/3 | D178.4–D178.6 finished: integration enum matrix 24/24 across PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse/SQLite (0 skipped; field types Int32 except SQLite Int64) + provider-matrix.md; benchmark enum 6 cases (EnumNumeric == IntegralInt allocations) + acceptance 7/7 no failures (cached/prepared 2.02x); CRLF-normalized; build nextorm.slnx Debug 0/0 + `dotnet build -c Release` 0/0; boundary sweep exit 0 (9519 total, 9321 passed, 0 failed, 198 capability skips); coverage JsonShapePlan 93.0%/86.3%, JsonRowWriterFactory 94.8%/82.8% lines/branches; `validate_inner_loop.py report` exit 0; NOTE pre-existing scalar-enum endpoint preparer gap (TypeFacts.IsSingleColumnProjection excludes enums) — scalar oneColumn covered by direct-writer tests; Phase stays DO, D178 not marked complete | artifacts/pdca/178/r1/{boundary.log,boundary.cobertura.xml,coverage-delta.md,provider-matrix.md,perf.md}
2026-10-09T02:44Z | DO | revision 1 | iteration 2/3 | CHECK FAIL r=1 n=1 -> DO loop-back n=2: gaps T05/T09/T10/T11/T12/T17 + provenance/precedence/nullable-string + missing evidence artifacts (audit.md/docs-audit.md/shape-tests.log/terminal-tests.log/tracking.json/check.md); Phase stays DO | artifacts/pdca/178/r1/check.md
2026-10-09T03:55Z | DO | revision 1 | iteration 2/3 | loop-back n=2 closed all 11 CHECK gaps + re-gathered evidence: JsonStreamingTests 99/99, InMemoryTests 162/162 (+3 T12), CsvStreamTests 23/23, ClickHouseDecimal 1/1; integration EnumStorage 30/30 (6 providers, 0 skipped, generic mirror added); slnx Debug 0/0 + Release 0/0; docfx exit 0 (0 errors, 2 pre-existing warnings); boundary sweep 9546 total / 9348 passed / 0 failed / 198 skipped; coverage JsonShapePlan 93.1% lines / 91.3% branches (+5.0pp branches), JsonRowWriterFactory 94.8% / 82.8%; validate_inner_loop.py report exit 0; mutation 73.51% CHECK score, 4 mandatory mutants now covered, classification in mutation-classification.md (Stryker re-run pending budget); P1 non-enum [JsonConverter] now reject-before-output; P2 wording fixed. Phase stays DO, D178 NOT complete | artifacts/pdca/178/r1/{check.md,audit.md,docs-audit.md,coverage-delta.md,mutation-classification.md,evidence.json,shape-tests.log,terminal-tests.log,inmemory.log,boundary.log,docfx.log,tracking.json}
2026-10-09T03:07Z | DO | revision 1 | iteration 2/3 | loop-back n=2 (cont.): T11 pre-cancellation + direct reader/connection disposal reuse + T13 empty-result invalid attributed shape. Added JsonStreamingTests Enum_PreCancelled_ShouldThrowAndKeepOwnership (:2090) and Enum_InvalidAttributedShape_EmptyResult_ShouldThrowBeforeOutput (:2116, empty table json_enum_empty); strengthened Enum ownership tests (:2064,:2080) with ConnectionState.Open + repeated reuse. Defect D178-T11-precancel-wrong-exception: pre-cancel RED as InvalidOperationException "no selected columns" (PrepareColumns cancellation bail-out, QueryCommand.QueryPreparer.cs:573) -> GREEN OperationCanceledException after adding cancellationToken.ThrowIfCancellationRequested() at DataContext.PrepareJsonStream (DataContext.cs:373-377, new changed file; 1 fix applied). build tests/nextorm.sqlite.tests Debug 0/0; JsonStreamingTests 101/101 exit 0; CsvStreamTests 23/23 exit 0; validate_inner_loop.py report exit 0; boundary evidence r1-n2 predates the guard -> CHECK must re-gather. Phase stays DO, D178 NOT complete | artifacts/pdca/178/r1/{shape-tests.log,red-precancel.log,coverage-delta.md,evidence.json}
2026-10-09T03:17Z | DO | revision 1 | iteration 2/3 | CHECK evidence artifacts materialized: E178-04/R178-07/T11 anchored lifecycle audit (reader disposed by the single finally on success/mid-write error/cancellation at QueryExecutor.cs:1175/:1208; pre-cancel throws at DataContext.cs:377 before any reader; destination stream never closed; sync surface has no CancellationToken); E178-06 per-execution evidence index (13 current executions, all exit 0; stale pre-fix EnumStorage exit-2 run moved to evidence.json historical_executions and excluded from the current index; benchmark-enum/benchmark-acceptance are E178-09 supporting logs without an executions[] row, exit 0 recorded in perf.md); E178-11 security assessment (only the bounded fail-closed JsonStringEnumConverter factory changed; arbitrary converter execution not reachable). evidence.json validator exit 0. Phase stays DO, D178 NOT complete | artifacts/pdca/178/r1/{lifecycle-audit.md,evidence-index.md,security-assessment.md,evidence.json}
```

## ACT (CHECK PASS r=1 n=2 rv=1) — finalized 2026-10-09

- CHECK verdict: **PASS** — plan revision r=1, attempt n=2/3, evidence contract rv=1. The attempt n=1 CHECK FAIL loop-back (n=2) closed all 11 gaps; no plan revision was triggered (rejected/incomplete candidate work does not bump `r`).
- Requirements closed: **R178-01..R178-10 all met**; scenario matrix **T01–T17 all closed**; evidence contract rows **E178-01..E178-12 all closed**.
- Boundary sweep (single DO→CHECK, re-gathered after the T11 guard): **9548 total / 9350 passed / 0 failed / 198 skipped**, all 12 test projects `passed`, exit 0 — `artifacts/pdca/178/r1/boundary.log`. The 198 skips are the named capability gates; no provider skipped for availability.
- Integration enum matrix: **30/30 passed, 0 skipped, 6 providers** (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, SQLite) — `artifacts/pdca/178/r1/boundary-integration-enum.log`, `provider-matrix.md`.
- Coverage (changed files, CI thresholds ≥85% lines / ≥75% branches): **DataContext.cs 89.1% / 83.3%**, **JsonShapePlan.cs 93.1% / 91.3%**, **JsonRowWriterFactory.cs 94.8% / 82.8%** — `artifacts/pdca/178/r1/coverage-delta.md`.
- Performance acceptance: **7/7 cases, no failures**; numeric enum == integral baseline allocations; cached/prepared path **2.02×** — `artifacts/pdca/178/r1/perf.md`, `benchmark-enum.log`, `benchmark-acceptance.log`.
- Mutation: **73.51%** (Stryker, two changed JSON files only) — **nonblocking**; **34 observed survivors** classified (mandatory behavioral mutants on the enum converter logic have direct killing tests; post-fix kills are **inferred, not measured** — no re-run in the loop-back budget) — `artifacts/pdca/178/r1/mutation-classification.md`, `stryker-survivors.txt`.
- Docs: EN/RU guide 28 + api-reference + internal roadmap updated; `dotnet docfx docs/docfx.json` exit 0 (0 errors, 2 pre-existing warnings) — `artifacts/pdca/178/r1/docfx.log`, `docs-audit.md`.
- Evidence index: `artifacts/pdca/178/r1/evidence-index.md` / `evidence.json` (validator exit 0).

### Accepted residuals (with triggers)

| Residual | Trigger |
|---|---|
| `JsonShapePlan.cs:350` over-restrictive non-enum `[JsonConverter]` rejection (defer-with-trigger) | a concrete need for a non-enum JSON converter on the streaming path + agreed contract |
| `JsonShapePlan.cs:342,352` blank converter name when `ConverterType` is null in the rejection message | message-quality polish; no behavioral impact |
| Stale "phase 1" wording in a comment/message | documentation/comment cleanup pass |
| `tests/nextorm.sqlite.tests/JsonStreamingTests.cs:2149` artificial repeat-write parity case (enum projection cannot be buffered-matched) | pre-existing buffered-mapper enum gap closed upstream |

- Footprint note: `src/nextorm.core/DataContext/DataContext.cs:375-377` joined the changed set only for the T11 pre-cancellation guard (`cancellationToken.ThrowIfCancellationRequested()` at the start of `PrepareJsonStream`), mirroring `OpenLobReader`; it is the sole reason a fourth changed file exists beyond the two JSON writer/shape files.
- Cycle finalized as **complete**; this status file is retained (not deleted).
