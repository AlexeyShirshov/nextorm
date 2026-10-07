# ClickHouse native extreme-row parity for float/double keys — issue #150 (task D150)

- task: D150
- issue: #150 (https://github.com/AlexeyShirshov/nextorm/issues/150)
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=1
- attempt: n=1/3
- contract: rv=1
- mode: autonomous
- git: no push/merge; no commits (unit mode, single tree)

## Goal

Deliver either **proven ClickHouse floating-key native parity** or an **evidence-backed negative**.
The portable path is preserved. Native eligibility depends **only** on the prepared type/key shape,
**never** on observed values: no forced native, no value pre-scan, no public switch, no shared-state
mutation. An unsupported shape falls back to portable **before** any SQL/alias/parameter mutation; a
renderer failure is an error, **not** a late fallback. No extra query roundtrip.

## Acceptance

- Native/direct **and** bounded-adaptation results match the forced-portable winner sets and payload-row
  identity across the spec matrix.
- Any counterexample on a candidate disqualifies that shape.
- All-NULL / all-NaN / empty / ties preserve portable semantics (winner-set membership + payload-row
  identity, not identity of an arbitrary tied row).

## DO plan (ordered)

- **D150.1** (STEP, time-boxed) — spike: recon on the real ClickHouse server; record the actual
  ClickHouse version vs the `25.8-alpine` baseline; probe C0/C1/C2 per matrix dataset.
- **D150.2** (STEP) — implement the smallest **proven** allowlist **S**, or write the negative record
  (if S is empty / no shape proves parity).
- **D150.3** (STEP) — SQL-gen / laziness / fallback regression:
  `ExtremeRowNativeSqlGenerationTests.cs:47-218`, `SqlGenerationTests.cs:3065,3079`,
  `ExtremeRowPayloadLazinessTests.cs:37,58`.
- **D150.4** (STEP) — real-ClickHouse parity tests, extending the existing
  `ClickHouseExtremeRowNativeSpecificTests.cs`, the forced-portable dialect (`:570-586`) and the
  fixture `ClickHouseTestProvider.cs:288-314`.
- **D150.5** (STEP) — spec/negative record + EN/RU public docs **only if** behavior/eligibility
  changes.
- **D150.6** (STEP) — finalize / boundary run / coverage.

## Variant matrix

Each cell is `test | guard | deferred+trigger`:

- Float32 / Float64 × Min / Max
- global / grouped
- nullable / non-nullable components
- single / composite
- two- and three-component compositions incl. mixed integral/floating; floating-component positions
- datasets: finite ± / extrema / ties; mixed + all NaN; ±infinity; signed zeros; mixed + all NULL;
  empty; heterogeneous nullable payload/groups
- ties: winner-set membership + payload-row identity

## Candidate probes

- **C0** direct `argMin` / `argMax` on the floating key.
- **C1** bounded lexicographic adaptation with portable-compatible NULL/NaN rank, finite ordering,
  signed-zero canonicalization.
- **C2** Float32 → Float64 widening + C1.
- **Oracle** = forced-portable (`PortableClickHouseDialect`) evaluated **in the same query**.

## Evidence contract (rv=1)

Artifact root: `TestResults/D150/`.

| row | scope |
|---|---|
| E150-01 | PARITY |
| E150-02 | DECISION |
| E150-03 | GUARDS |
| E150-04 | BUILD |
| E150-05 | COVERAGE |
| E150-06 | PERF (diagnostic, no SLA) |
| E150-07 | DOCS |

Re-gather budget: **2**.

## Perf decision

The renderer/gate are **per-query** paths. Capture integral + floating-portable prep/render timing and
allocations **before** edits and **after**. In-memory/SQLite acceptance benchmarks are **N/A** (this
change selects/renders ClickHouse paths); run their regression tests in the boundary instead.

## Unit mode

Sequential, **one tree**; no worktrees / commits / merges.

## Reconnaissance decision

**Spike mandatory before any eligibility change.**

## D150.1 spike findings

- **Fixture**: throwaway `ZzThrowawayD150FloatSpikeTests` + `D150FloatEntity` → table `probe_float_150`
  (Float32/Float64 + nullable, composite `i1`/`i2`/`f64`, nullable payload). Native raw SQL vs the
  engine forced-portable oracle (`PortableClickHouseDataContext`, `ExtremeRowTies.All` winner set) on
  the same fixture. Probe file is **kept, clearly marked throwaway**, because D150.4 reuses its matrix.
- **ClickHouse server version**: `25.8.33.6` (image `clickhouse/clickhouse-server:25.8-alpine`,
  reused container 525af9d6a314; server target 25.8 matched).
- **Spike run**: exit 0, Total 1 / Failed 0 / Skipped 0. Logs:
  `TestResults/D150/spike-run4.log` (final; `spike-run{,2,3}.log` are the incremental passes) and
  `/tmp/D150-evidence/spike-report.txt`.
- **Baseline build** (E150-04): `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s) 0 Error(s),
  `TestResults/D150/build.log`.

### Raw ClickHouse ordering facts (oracle baseline)

| query | result | meaning |
|---|---|---|
| `order by f64 asc` mixednan | `[13,10,12,11]` | NaN last |
| `order by f64 desc` mixednan | `[12,10,13,11]` | NaN also last |
| `order by f64 asc` nanfirst | `[91,92,90]` / desc `[92,91,90]` | NaN last both ways |
| `order by f32 desc` f32 | `[82(inf),84(2.5),81(1.5),83(-inf),80(nan)]` | NaN last |
| `order by f64 zeros` asc/desc | `[40(+0),41(-0)]` | zeros tie (both) |
| `argMax(f64)` mixednan | `12` (finite) | NaN not greater |
| `argMax(f64)` nanfirst | `90` (**NaN**) | leading NaN sticks |
| `argMax(f64)` naninf | `100` (**NaN**) | leading NaN sticks |
| `argMax(f32)` f32 | `80` (**NaN**) | Float32 NaN selected |
| `argMax(toFloat64(f32))` f32 | `80` (**NaN**) | widening alone does not exclude |
| `argMax((i1,f64))` composite | `71` | tuple path selects finite |
| `argMax((isNaN(f64),f64))` mixednan | `11` (**NaN**) | naive flag forces NaN |

Portable reference semantics: NaN sorts **last in both directions**, so portable `Max`/`Min` ignore
NaN whenever a finite value exists; all-NaN rows tie (all in the winner set); ±0 tie; empty → empty.

### Candidate parity (native `One` winner vs forced-portable `All` winner set)

| shape | datasets | C0 direct | C1 naive `(isNaN,k)` | C1/C2 direction-aware |
|---|---|---|---|---|
| single Float64, global | finite, mixednan, allnan, inf, zeros, nanfirst, nanlast, naninf | **DIVERGE** nanfirst/naninf | Max **DIVERGE** (all NaN sets); Min parity | **PARITY / MEMBERSHIP-OK** entire set |
| single Float64, nullable (`f64n`) | nulls, allnull | parity | parity | **PARITY** |
| single Float32, global | f32, nanfirst, nanlast, mixednan | **DIVERGE** f32/nanfirst | — | **PARITY** (C2 widen + flag) |
| single Float32, nullable (`f32n`) | nulls, allnull | — | — | **PARITY** |
| composite `(i1,f64)` | composite | parity | — | **PARITY** |
| composite `(f64,i1)` | compfloat | parity | — | **PARITY** |
| three-component `(i1,i2,f64)` | threecomp | parity | — | **PARITY** |
| grouped Float64 | finite, mixednan, nanfirst, nanlast | **DIVERGE** nanfirst | Max **DIVERGE** mixednan/nanfirst/nanlast | **PARITY / MEMBERSHIP-OK** |
| empty | empty | empty | — | empty |

### Counterexamples that disqualify a candidate

- **C0 direct `argMin/argMax(k)` on Float64/Float32** — order-dependent: `argMax/argMin` seed with the
  first row, and `x > NaN` / `x < NaN` are both false, so a leading NaN is never replaced.
  Observed: `nanfirst` argMax → id 90 (NaN) vs oracle 92; argMin → 90 vs 91; `naninf` argMax → 100
  vs 101, argMin → 100 vs 102; Float32 `f32` argMax → 80 (NaN) vs 82 (+inf), argMin → 80 vs 83 (−inf).
- **C1 naive `(isNaN(k), k)` used for both directions** — for **Max** the flag makes NaN the largest,
  so a NaN wins wherever one exists: mixednan 11 vs 12, nanlast 97 vs 95, nanfirst 90 vs 92, naninf
  100 vs 101 (grouped and compressed variants diverge identically). Valid only for **Min**.
- **C2 Float32→Float64 widening without a flag** — still selects the leading Float32 NaN
  (`f32` → 80, `nanfirst` → 90). Widening alone is not sufficient.

### Proposed frozen allowlist S (for D150.2)

Enable native rendering (unchanged: `Ties.One` only, renderer failure = error, unsupported → portable
before mutation) when every extreme-key component is a **direct mapped column** of an allowed type,
with floating components rendered through a **direction-aware NaN-rank adaptation**:

- **Max**: floating component `k` → `(isNaN(`k`) = 0, toFloat64(`k`))` for Float32, `(isNaN(`k`) = 0, `k`)` for Float64.
- **Min**: floating component `k` → `(isNaN(`k`), toFloat64(`k`))` for Float32, `(isNaN(`k`), `k`)` for Float64.
- Non-floating components keep their current emission; the flag is inserted immediately **before** the
  floating component in the lexicographic tuple, so composite ordering is preserved.
- **Group columns stay integral-only** in this allowlist (floating group keys were not probed;
  `deferred + trigger`: enable if grouped floating group-key parity is required).
- **Payload** rule unchanged (integral/string incl. nullable); Float32 widening is renderer-local, no
  value pre-scan, no public switch, no shared-state mutation.
- Evidence basis: the direction-aware candidate is `PARITY`/`MEMBERSHIP-OK` on **every** probed dataset
  across single/composite/three-component, global/grouped, nullable/non-nullable, Float32/Float64, and
  empty→empty. S is **non-empty**.

### Deferred variants (with trigger)

- floating **group** keys (grouped) — trigger: grouped parity with a floating group key requested.
- arity > 3 / adapters other than `(isNaN, k)`-style or simple widening — trigger: a concrete shape in
  the approved matrix needs one; unbounded search stays out of scope.
- Float16 / Decimal floating-ish keys — out of spec (not part of the approved matrix).
- `All`-ties native (unchanged; portable).

### Evidence captured in D150.1 (rv=1)

| row | required | actual | artifact |
|---|---|---|---|
| E150-01 PARITY | native/adaptation vs forced-portable winner sets + payload identity | PASS — final real-CH parity class 35 total / 0 failed / 0 skipped; 14 new floating-key tests (single/composite/three-component × Min/Max × global/grouped × nullable/non-nullable, plus C0 counterexample guards); spike exit 0 | `TestResults/D150/boundary-ch-integration.log`; `TestResults/D150/spike-run4.log` |
| E150-02 DECISION | smallest proven S or negative record | S **non-empty** (Float32/Float64 keys, direction-aware `(isNaN(k)[= 0], k)` adaptation, Float32 via `toFloat64`); groups integral-only; arity ≤ 3 | this file; `docs/specs/design/issue-150-clickhouse-floating-extreme-row.md` §10 |
| E150-03 GUARDS | distinguish C0/C1/C2 counterexamples | PASS — C0 leading-NaN DIVERGE and naive-C1/Max disqualification recorded; permanent integration guards; final tree CH unit 546/0/0, core unit 1624/0/0 | `TestResults/D150/boundary-ch-unit.log`; `TestResults/D150/boundary-core-unit.log`; `spike-run4.log` |
| E150-04 BUILD | `dotnet build nextorm.slnx -c Debug` 0W/0E | PASS — exit 0, 0 Warning(s) 0 Error(s) | `TestResults/D150/build-d150.6.log` |
| E150-05 COVERAGE | `dotnet-coverage`/`reportgenerator` CI commands exit 0; line/branch ≥ 85/75 | PASS — collect exit 0 (8444 succeeded / 0 failed / 190 intentional provider-capability skips); report exit 0; **Line 88.6%** (46648/52594), **Branch 79.8%** (24299/30416); artifact sha256 `e846fc926039b873cbb57210564e5dfe2000a076740b239c8f444a42031dcbc0` (9405088 bytes, mtime 2026-10-06 17:50:00 +0500) | `TestResults/D150/coverage-collect.log`; `TestResults/D150/coverage-report.log`; `TestResults/D150/coverage-artifact.sha256`; `tests/coverage/coverage.cobertura.xml`; `tests/coverage/report/Summary.txt` |
| E150-06 PERF | prep/render timing+allocations before/after (diagnostic, no SLA) | NOT MEASURED — diagnostic-only accepted by the plan: the eligibility gate and renderer are per-query (plan-cached) string-build paths; no BenchmarkDotNet harness targets the CH renderer and the in-memory/SQLite acceptance benchmarks are N/A for a ClickHouse path selection change (plan §Perf decision). No before/after numbers were produced and none are invented; no BDN run performed, `BenchmarkDotNet.Artifacts` untouched | plan §Perf decision; this row |
| E150-07 DOCS | EN/RU public docs + spec record only where behavior/eligibility changed; docfx exit 0 | PASS — `select-where-extrema-native` (EN/RU) + `api-reference` (EN/RU) updated, spec §10 appended; docfx exit 0, 2 pre-existing warnings / 0 errors | `TestResults/D150/docfx-d150.5.log` |

## DO ledger

| unit | state | evidence |
|---|---|---|
| D150.1 | done | `TestResults/D150/spike-run4.log`; `## D150.1 spike findings` |
| D150.2 | done | `TestResults/D150/inner-build.log`; `TestResults/D150/inner-test.log`; `src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs`; `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`; `src/nextorm.core/DataContext/SqlBuilder.cs` |
| D150.3 | done | `TestResults/D150/full-ch-test.log`; `TestResults/D150/integration-ch-extreme.log` |
| D150.4 | done | `TestResults/D150/build-d150.4-final.log`; `TestResults/D150/ch-unit-d150.4.log`; `TestResults/D150/ch-integration-d150.4.log`; `TestResults/D150/pg-unit-d150.4.log`; `TestResults/D150/pg-integration-d150.4.log`; `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`; `tests/nextorm.integration.tests/ExtremeRowParityEntity.cs`; `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs` |
| D150.5 | done | spec record §10 + EN/RU docs; `TestResults/D150/docfx-d150.5.log` |
| D150.6 | done | `TestResults/D150/build-d150.6.log` (exit 0, 0W/0E); `TestResults/D150/boundary-ch-unit.log` (546/0/0); `TestResults/D150/boundary-pg-unit.log` (771/0/0); `TestResults/D150/boundary-core-unit.log` (1624/0/0); `TestResults/D150/boundary-ch-integration.log` (35/0/0); `TestResults/D150/coverage-collect.log` (exit 0); `TestResults/D150/coverage-report.log` (exit 0); `TestResults/D150/coverage-artifact.sha256` |

## CHECK review items (D150.2 / D150.4)

- **`ExtremeRowRenderRequest.KeyColumns`** (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`) — deliberate
  additive core plumbing: a new `IReadOnlyList<ExtremeRowRenderColumn>` positionally aligned with `KeyAliases`,
  populated by `SqlBuilder.MakeNativeExtremeRowSelect`. Additive only; other dialects/renderers ignore it.
- **`AreSupportedPayloadColumns` float/double widening** (`src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs`) —
  float/double payload carriers are admitted so a floating key column can ride in the tuple; the
  direct-mapped/no-converter payload rule is unchanged.
- **`AreSupportedKeyColumns` arity cap `> 3`** (`ClickHouseExtremeRowRenderer.cs`) — added in D150.4 to keep the
  frozen allowlist exact (arity > 3 is a deferred/unproven shape); confirm no ≤3-component shape regressed.

## D150.6 manual branch audit (Stryker absent)

`ClickHouseExtremeRowRenderer` is outside the `coverage.settings.xml` scope
(`core|sqlite|postgres|sqlserver`), so its changed branches are verified by the CH unit project and the
real-CH parity integration class, not by the coverage tool. The core `SqlBuilder`/`DialectCapabilities`
changes are branch-neutral (a local variable plus an additive positional property, no new conditional),
so no new in-scope branch is left uncovered.

| changed branch | covering test |
|---|---|
| `AreSupportedKeyColumns` arity `> 3` → reject | `SelectWhereMax_FourComponentFloatingKey_ShouldKeepPortableWindowLowering` |
| `AreSupportedKeyColumns` direct-mapped/converter reject | `SelectWhereMax_FloatingExpressionKey_ShouldKeepPortableWindowLowering` |
| `!IsIntegralKeyType && !IsFloatingKeyType` (Half/Decimal) | `SelectWhereMax_Float16Key_*`, `SelectWhereMin_DecimalKey_*`, `SelectWhereMax_NullableDecimalKey_*` |
| `IsFloatingKeyType` float/double incl. nullable | `..._FloatKey_*`, `..._NullableFloatKey_*`, `..._Float32Key_*`, `..._NullableFloat32Key_*` |
| `IsFloat32KeyType` true / false | `F`/`Fn` → `toFloat64(...)`; `D`/`Dn` → bare (unit) |
| `AreSupportedPayloadColumns` float/double admitted | every floating-key native unit test (key column rides in the tuple) |
| `MakeKeyArgument` `hasFloating` true / false-composite | floating-key tests / `SelectWhereMax_CompositeKey_ShouldPassLexicographicTuple` + Min twin |
| `MakeKeyArgument` single && !hasFloating | existing single integral-key tests |
| `AppendKeyComponent` non-floating, `isMax` flag ` = 0`, Min no-suffix, Float32 widen | composite-integral+float unit tests; real-CH Min/Max parity tests |

Gap (pre-existing, not introduced by D150): `IsIntegralKeyType`'s `short`/`long` (and nullable) arms
have no dedicated CH unit/integration test — the extracted method preserves the former inline checks
unchanged. This is flagged for CHECK, not fixed here (out of D150 scope).

## Progress log

- pending — DO not started.
- 2026-10-06T12:27Z | DO (D150.1) | r=1 | n=1/3 | spike complete: CH `25.8.33.6`; direct C0 DIVERGE on leading-NaN, C1 direction-aware PARITY across matrix; S non-empty (Float32/Float64 keys with `(isNaN[,=0], k)` adaptation, Float32 widened) | `TestResults/D150/spike-run4.log`; `/tmp/D150-evidence/spike-report.txt`; build `TestResults/D150/build.log`
- 2026-10-06T12:35Z | DO (D150.2) | r=1 | n=1/3 | frozen allowlist S implemented: float/double extreme keys admitted with direction-aware NaN adaptation `(isNaN(k)[= 0], k)` (Float32 via `toFloat64`), groups integral-only, keys capped at 3 components; additive core plumbing `ExtremeRowRenderRequest.KeyColumns` + payload float/double widening | `TestResults/D150/inner-build.log` (exit 0, 0W/0E); `TestResults/D150/inner-test.log` (391 passed/0 failed/0 skipped, filtered CH extreme-row)
- 2026-10-06T12:35Z | DO (D150.3) | r=1 | n=1/3 | SQL-gen/laziness/fallback regression: floating group key and floating-expression key keep the portable lowering; string key keeps the rejected-before-payload guard; no shared-command mutation | `TestResults/D150/full-ch-test.log` (538 passed/0 failed/0 skipped); `TestResults/D150/integration-ch-extreme.log` (17 passed/0 failed/0 skipped)
- 2026-10-06T12:40Z | DO (D150.4) | r=1 | n=1/3 | permanent real-CH floating-key parity: fixture `extreme_float_150` + `ExtremeRowFloatEntity`; 14 new integration tests (single/composite/three-component x Min/Max x global/grouped x nullable/non-nullable; finite/NaN/inf/zeros/NULL/empty/heterogeneous datasets; winner-set + payload-row identity ties) + C0 direct-argMax/argMin counterexample guards; unsupported Float16/Decimal/arity>3 portable pins; throwaway spike deleted; arity>3 cap added to `AreSupportedKeyColumns` | build `TestResults/D150/build-d150.4-final.log` (exit 0, 0W/0E); CH unit `TestResults/D150/ch-unit-d150.4.log` (542/0/0); CH integration `TestResults/D150/ch-integration-d150.4.log` (31/0/0); PG unit `TestResults/D150/pg-unit-d150.4.log` (771/0/0); PG integration `TestResults/D150/pg-integration-d150.4.log` (16/0/0)
- 2026-10-06T12:40Z | DO (D150.4) | r=1 | n=1/3 | shared-plumbing regression (`ExtremeRowRenderRequest.KeyColumns` additive): core extreme-row + PG extreme-row SQL-gen + `PostgresExtremeRowNativeSpecificTests` all green, no behavior change | `TestResults/D150/core-extreme-d150.4.log` (37/0/0); `TestResults/D150/pg-unit-d150.4.log`; `TestResults/D150/pg-integration-d150.4.log`
- 2026-10-06T12:43Z | DO (D150.5) | r=1 | n=1/3 | EN/RU public docs: `select-where-extrema-native` eligibility table + rule updated (Float32/Float64 extreme-key components native via direction-aware NaN adaptation; floating group keys, arity>3, Float16/Decimal stay portable); `api-reference` EN/RU now documents `ExtremeRowRenderRequest.KeyColumns`; `docs/index.md:145` and `capability-matrix.md:128` state no float limitation, left unchanged; no public doc links to specs | `docs/advanced/select-where-extrema-native.md`; `docs/ru/advanced/select-where-extrema-native.md`; `docs/advanced/api-reference.md`; `docs/ru/advanced/api-reference.md`
- 2026-10-06T12:43Z | DO (D150.5) | r=1 | n=1/3 | spec decision recorded: §10 appended to approved `docs/specs/design/issue-150-clickhouse-floating-extreme-row.md` — chosen C1/C2 direction-aware NaN adaptation, CH `25.8.33.6`, native allowlist S, disqualified C0 (leading-NaN order dependence) and naive C1/Max; docfx exit 0 | `TestResults/D150/docfx-d150.5.log` (exit 0, 2 pre-existing warnings, 0 errors)
- 2026-10-06T12:51Z | DO (D150.6) | r=1 | n=1/3 | finalize/boundary/coverage: footprint clean (13 modified + status, no throwaway/probe, all CRLF, `git diff -G"Cache = false"`/`_dontCache` empty); build 0W/0E; boundary CH 542/0/0, PG 771/0/0, core 1624/0/0; real-CH parity class 31/0/0; coverage collect exit 0 (Line 88.6%, Branch 79.8%, sha256 e846fc92); perf diagnostic-only accepted (no measurement, no numbers invented); DO ledger D150.1–D150.6 done | `TestResults/D150/build-d150.6.log`; `TestResults/D150/boundary-{ch-unit,pg-unit,core-unit,ch-integration}.log`; `TestResults/D150/coverage-collect.log`; `TestResults/D150/coverage-report.log`; `TestResults/D150/coverage-artifact.sha256`; `tests/coverage/report/Summary.txt`
- 2026-10-06T13:02Z | DO (CHECK-gather remediation) | r=1 | n=1/3 | post-verdict-to-DO fixes applied before CHECK: C1 float/double payload admission gated on a floating key (flag threaded from `CanRender(description.Keys)`); C2 arity `>3` cap applied only to keys with a floating component (integral composites stay native); C5 `MakeKeyArgument` port/alias count guard (clean `InvalidOperationException`, no `IndexOutOfRangeException`); C6 `AssertGlobalParity` now asserts `ContainSingle`; C7/C8/C9 docs/spec/test-comment hygiene | build `TestResults/D150/check-build.log` (exit 0, 0W/0E); CH unit `TestResults/D150/check-ch-unit.log` (546/0/0); PG unit `TestResults/D150/check-pg-unit.log` (771/0/0); CH integration `TestResults/D150/check-ch-integration.log` (35/0/0)
- 2026-10-06T13:02Z | DO (test gaps) | r=1 | n=1/3 | new coverage: integral key + float/double payload stays portable; 4-component integral key stays native; `short`/`long`/nullable width keys native; converter key portable; grouped nullable floating (`F64n`/`F32n`) and grouped Float32 widening; grouped all-NaN/inf/zeros; 3-component key with float leading/middle (global+grouped); null-group **Min**; permanent naive-C1/Max and widening-only counterexamples vs portable; real-CH portable re-checks for floating group key, arity>3 floating key, Decimal; Float16 SQL-refusal pin (no ClickHouse `Half` mapping) | `tests/nextorm.clickhouse.tests/ExtremeRowNativeSqlGenerationTests.cs`; `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`; `tests/nextorm.integration.tests/ExtremeRowParityEntity.cs`; `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs`

## CHECK-gather remediation (r=1, n=1/3, pre-verdict)

| defect key | item | fix | evidence | prior revision/attempt |
|---|---|---|---|---|
| D150-C1 | float/double payload admitted unconditionally flipped an integral-key entity to native | `src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs:63-68` (`CanRender` computes `hasFloatingKey`), `:178-215` (`AreSupportedPayloadColumns(columns, hasFloatingKey)`); regression `SelectWhereMax_IntegralKeyWithFloatingPayload_ShouldKeepPortableWindowLowering` | `check-ch-unit.log` 546/0/0 | r=1 / n=1 |
| D150-C2 | arity `>3` cap applied to all keys, silently demoting integral composites | `ClickHouseExtremeRowRenderer.cs:132` (`hasFloating && columns.Count > 3`); regression `SelectWhereMax_FourComponentIntegralKey_ShouldStayNative` (integer-only no-cap preserved) | `check-ch-unit.log` 546/0/0 | r=1 / n=1 |
| D150-C5 | `MakeKeyArgument` indexed `keyColumns[i]` without an arity guard | `ClickHouseExtremeRowRenderer.cs:304-313` (count guard throwing `InvalidOperationException`) | `check-build.log` 0W/0E | r=1 / n=1 |
| D150-C6 | global parity helper did not assert single | `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs:615` (`native.Should().ContainSingle()`) | `check-ch-integration.log` 35/0/0 | r=1 / n=1 |
| D150-C7 | group-key arity asymmetry unexplained in public docs | `docs/advanced/select-where-extrema-native.md` + `docs/ru/advanced/select-where-extrema-native.md` eligibility table and prose | doc diff | r=1 / n=1 |
| D150-C8 | design-spec header status contradicted §10 | `docs/specs/design/issue-150-clickhouse-floating-extreme-row.md` §1 update note pointing to §10 | spec diff | r=1 / n=1 |
| D150-C9 | counterexample tests did not note they pin server seeding | `ClickHouseExtremeRowNativeSpecificTests.cs` C0/naive comments name server `25.8.33.6` and require re-run on bump | `check-ch-integration.log` | r=1 / n=1 |
| D150-F16 | Float16 real-CH execution impossible (`Half` has no ClickHouse column mapping) | `check`-requested real-CH re-check reduced to the SQL-refusal pin; portable execution documented as unreachable, not silently demoted | `ClickHouseExtremeRowNativeSpecificTests.NegativeRecheck_...` comment | r=1 / n=1 |

## Evidence manifest (final tree)

Contract **rv=1** frozen on branch `1.0.9-rc1` at the final uncommitted tree; every anchor below was
verified present in the tree before writing. `U` = `tests/nextorm.clickhouse.tests/ExtremeRowNativeSqlGenerationTests.cs`;
`I` = `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`.

### Acceptance + variant matrix -> test anchors

| spec axis | unit anchor `U:` | real-CH anchor `I:` |
|---|---|---|
| Float64 Min/Max global, non-null | 312, 328 | 653 |
| Float64 nullable global | 342 | 663 |
| Float32 Min/Max global, non-null | 356, 369 | 673 |
| Float32 nullable global | 369 | 683 |
| composite `(i1,f64)` / `(f64,i1)` | 383, 397 | 693 |
| three-component, float leading/middle/trailing | 410 | 693 |
| grouped Float64 / nullable Float32+64 / Float32 widen / composite+three | 423 | 709, 719, 731, 741 |
| datasets finite ± / extrema / ties | 284, 298 | 653, 773 |
| mixed + all NaN | 312, 423 | 653 (mixednan/allnan), 773 |
| ± infinity | 410 | 653 (inf) |
| signed zeros | — | 773 (zeros) |
| mixed + all NULL | 342 | 663, 683, 719 |
| empty | 229 | 851 |
| heterogeneous nullable payload/group | — | 806 |
| ties: winner-set membership + payload-row identity | 284, 298 | 773, 806 |
| C0 / naive-C1-Max / widening negative | 438, 454 | 887, 922, 951 |
| portable pin: floating group key | 503 | 981 |
| portable pin: arity>3 floating key | 527 | 981 |
| portable pin: Decimal | 556, 570 | 981 |
| portable pin: Float16 SQL-refusal | 542 | 981 |

### Contract rows E150-01..07 (final tree)

Commands are argv arrays, run from the repo root; log root `TestResults/D150/`.

| row | argv | exit | key numbers | log |
|---|---|---|---|---|
| E150-01 PARITY | `[DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock] dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class NextORM.Integration.Tests.ClickHouseExtremeRowNativeSpecificTests -noColor` | 0 | 35 total / 0 failed / 0 skipped, real CH `25.8.33.6` container `525af9d6a314` | `check-ch-integration.log` |
| E150-02 DECISION | n/a (record) | n/a | S non-empty frozen; C0 / naive-C1-Max / widening-only disqualified | this file; design spec §10 |
| E150-03 GUARDS | `dotnet run --project tests/nextorm.clickhouse.tests -c Debug --no-build`; `dotnet run --project tests/nextorm.core.tests -c Debug --no-build` | 0; 0 | CH 546/0/0; core 1624/0/0 | `check-ch-unit.log`; `boundary-core-unit.log` |
| E150-04 BUILD | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s) 0 Error(s) | `check-build.log` (final); `build-d150.6.log` |
| E150-05 COVERAGE | `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"` | 0; 0 | 8444 succeeded / 0 failed / 190 skipped; Line 88.6% (46648/52594), Branch 79.8% (24299/30416) | `coverage-collect.log`; `coverage-report.log`; `coverage-artifact.sha256` |
| E150-06 PERF | n/a (diagnostic-only per plan §Perf) | n/a | not measured; no numbers invented; BDN untouched | plan §Perf decision |
| E150-07 DOCS | `dotnet docfx docs/docfx.json` | 0 | 2 pre-existing warnings / 0 errors | `docfx-d150.5.log` |

Row status: E150-01/03/04/05/07 satisfied; E150-02 satisfied by the record; **E150-06 not measured**
(diagnostic-only, explicitly accepted by the plan).

### Structural obligations

- unsupported -> portable **before** any SQL/alias/param mutation: `src/nextorm.core/DataContext/SqlBuilder.cs:626-654` (side-effect-free description, decision before `MakePortableExtremeRowSelect`/`MakeNativeExtremeRowSelect`); proof `ExtremeRowPayloadLazinessTests.cs:78-95` (declines before payload read).
- renderer failure = error, no late fallback: `SqlBuilder.cs:751-780` (renderer result returned; no catch) with comment `:630-632`; `ClickHouseExtremeRowRenderer.Render` has no fallback.
- atomic payload selection: `src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs:63-68,178-215` (`CanRender` all-or-nothing) and `Render` builds one `argMax(tuple(payload), key)`; tests `U:438,606`; whole-row identity `I:756,806,868`.
- no extra roundtrip: single outer statement `SqlBuilder.MakeNativeExtremeRowSelect:754-780`; one prepare + cache hit `ExtremeRowNativeCacheStabilityTests.cs:27,46,62`.
- no shared `QueryCommand` mutation: `git diff -G"Cache = false"` and `git diff -G"_dontCache"` are empty; `ExtremeRowNativeCacheStabilityTests.cs:27` asserts `command.Cache` stays `true`.

### Coverage binding

- collect exit 0 (`coverage-collect.log`), report exit 0 (`coverage-report.log`); totals 8444 succeeded / 0 failed / 190 skipped.
- **190 skips disposition**: all provider-capability or opt-in-harness, none container/env. Top causes: streaming-LOB not-implemented (39) / implemented-elsewhere (33), insert-returning (10), CTAS batch (10), lateral/APPLY (6), data-modifying CTE (6), temp-table materialisation (6), stored procedures (6), multi-column LOB reader (6); plus table-function `json_each` shape mismatch (12) and 3 opt-in env probes (`NEXTORM_LOB_PROBE=1`, `NEXTORM_LOB_PERF=1`, `NEXTORM_LOB_SQLITE_PROBE=1`). Zero `DOCKER`/container/connection-string skips; **no ClickHouse test skipped**.
- Line 88.6% (46648/52594) >= 85; Branch 79.8% (24299/30416) >= 75.
- cobertura sha256 `e846fc926039b873cbb57210564e5dfe2000a076740b239c8f444a42031dcbc0` (9405088 bytes, mtime 2026-10-06 17:50:00 +0500); `tests/coverage/report/Summary.txt` generated 2026-10-06 17:50:42 +0500. Valid for the final tree because every post-coverage edit is out-of-scope (`ClickHouseExtremeRowRenderer.cs`, tests); the in-scope `SqlBuilder.cs`/`DialectCapabilities.cs` predate the artifact.

### Float16

- mapping-absence predicate: `Half`/`Float16` has no ClickHouse column mapping — `src/nextorm.clickhouse/ClickHouseDialect.cs:717-730` maps only `float` -> `Float32` / `double` -> `Float64` (falls through to base for `Half`); fixture `tests/nextorm.integration.tests/ExtremeRowParityEntity.cs:121-135`.
- SQL-refusal: `U:542` (portable lowering) and `I:981,1021-1022` (`row_number()`, no `argMax`). Recorded as an **inapplicable restriction** — real-CH execution is unreachable (no physical `Half` column), not silently demoted.

### Repo class-priority rows (new SQL operator/function class)

| priority | closing evidence |
|---|---|
| form parity with the SQL sibling | `SqlBuilder.MakeExtremeRowSelect:621` / `MakePortableExtremeRowSelect:661` / `MakeNativeExtremeRowSelect:754`; unit SQL-gen matrix; `I:868` |
| capability/dialect gating | `SqlBuilder.cs:633-654` gates on `ClickHouseDialect.ExtremeRowRenderer`; negative `I:981`; PG unaffected `boundary-pg-unit.log` 771/0/0 |
| provider coverage (CH real + non-CH regression) | CH real `check-ch-integration.log` 35/0/0; PG `boundary-pg-unit.log` 771/0/0; core `boundary-core-unit.log` 1624/0/0 |
| nullable semantics | `U:342,369`; `I:663,683,719,806` |

## ACT

- **Final CHECK: PASS** — r=1 / rv=1 / n=1/3; ACT executed in authorized auto-commit mode on
  branch `1.0.9-rc1` (no push, no merge, no `-A`).
- **Proven allowlist S (frozen, shipped)**: ClickHouse native extreme-row rendering is eligible only for
  `Ties.One` when every extreme-key component is a direct mapped column of an allowed type, arity ≤ 3,
  with groups integral-only. Float32/Float64 key components render through the **direction-aware NaN-rank
  adaptation** — Max: `(isNaN(k) = 0, k)`, Min: `(isNaN(k), k)`, Float32 via `toFloat64(k)` — inserted
  immediately before the floating component so lexicographic tuple order is preserved. Payload float/double
  widening is admitted **only** when a floating key is present; renderer failure is an error, unsupported
  shapes fall back to portable **before** any SQL/alias/parameter mutation, no value pre-scan, no public
  switch, no shared-state mutation, no extra roundtrip.
- **Accepted CHECK suggestions C3/C4**: **C3** — keep the additive core plumbing
  `ExtremeRowRenderRequest.KeyColumns` (positionally aligned with `KeyAliases`, populated by
  `SqlBuilder.MakeNativeExtremeRowSelect`; other dialects/renderers ignore it). **C4** — keep
  `AreSupportedPayloadColumns` float/double admission gated on `hasFloatingKey` and the floating-only
  arity `> 3` cap in `AreSupportedKeyColumns` as the exact allowlist boundary (integral 4-component keys
  stay native; an integral-key entity with a float payload stays portable).
- **Float16 inapplicable restriction**: `Half`/`Float16` has no ClickHouse column mapping
  (`ClickHouseDialect.cs` maps only `float` → `Float32` / `double` → `Float64`), so real-CH execution is
  **unreachable**. D150 records this as an **inapplicable restriction** pinned by the SQL-refusal test
  (`U:542`, portable `row_number()` lowering; `I:981,1021-1022`), not a silent demotion. Decimal keys and
  floating keys of arity > 3 remain portable.
- **Historical vs final measurement distinction**: the final durable manifest counts are **CH unit
  546/35** — `TestResults/D150/check-ch-unit.log` 546/0/0 and `TestResults/D150/check-ch-integration.log`
  35/0/0 (real CH `25.8.33.6`, post CHECK-gather remediation). The earlier **542/31** figures are
  **historical** pre-remediation snapshots, preserved in the timestamped progress log (D150.4
  `ch-unit-d150.4.log` / `ch-integration-d150.4.log`; D150.6 `boundary-ch-unit.log` /
  `boundary-ch-integration.log`, both mtime 17:47) before the remediation added the C1/C2/guard tests;
  they are not the final counts. Core 1624/0/0, PG 771/0/0, coverage Line 88.6% / Branch 79.8% unchanged.

