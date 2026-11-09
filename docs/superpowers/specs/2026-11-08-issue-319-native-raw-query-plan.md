# PLAN — Issue #319 native raw query (build-once construction)

- **Status:** implementation plan for the approved design; owner reopened issue #319 and
  authorized implementation on 2026-11-08 by «начинай реализацию».
- **Approved design:** `docs/superpowers/specs/2026-11-08-issue-319-native-raw-query-design.md`
  (owner approval: issue comment `#issuecomment-6718840910`; design is the contract of this plan).
- **Base snapshot (re-anchor):** repository `/home/alex/sources/nextorm`, branch `1.0.9-rc5`,
  `HEAD = ac69d30e` (working tree clean except one unrelated untracked file under `docs/scalar-functions/`).
- **Pinned comparison batch:** `f855b884` — the committed 2026-11-07 SQLite reports used by the
  population manifest.
- **Issue:** https://github.com/AlexeyShirshov/nextorm/issues/319 (milestone `1.0.9`, #21).
- **No commits, no push, no merge** are part of this plan; the owner keeps git.

## 1. Goal, requested and delivered result

**Requested:** make fresh fluent construction of a cached query stop paying the legacy
`QueryCommand` object graph, so the over-budget comparison arms reach `<= 1.20x` linq2db
allocation with no NextORM time regression, under the owner-decided evaluation contract
(design §3): collection builds only a raw description plus cheap guards; keying, translation,
provider checks and SQL rendering happen at execution or explicit `Prepare`.

**Delivered:** the same object — a native, complete, immutable raw query description
(`RawQuery<T>` handle + immutable nodes), its canonical key and structural equality, late
compilation through the existing `QueryPlanner`/`SqlBuilder` on cache miss only, per-execution
rebinding, migrated construction routes for the frozen population, full SQL/row parity,
EN/RU documentation, and the evidence for every gate.

**Non-goal:** no SQL engine rewrite, no provider feature expansion, no `ToDataReader`/raw-reader
work (design §11).

## 2. Frozen population (manifest)

Produced mechanically from the 15 committed 2026-11-07 report classes
(`tools/issue319/build_population_manifest.py` ->
`docs/superpowers/specs/2026-11-08-issue-319-population-manifest.csv`):

- 85 NextORM arms total: **8 allocation targets**, 55 paired controls, 19 unpaired controls,
  3 excluded raw-reader arms.
- The manifest is frozen. New rows do not float in after this point; a later discovery is a scope
  delta, not a silent addition.

| # | Class | Scenario | Cache | NextORM B/op | linq2db B/op | Ratio | Need `=> 1.20x` |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| T1 | Any | (default) | cached | 632,873 | 239,206 | 2.646 | 345,826 |
| T2 | Where | ToListAsync | cached | 661,084 | 259,615 | 2.546 | 349,546 |
| T3 | FeaturesFairCached | RecursiveCte | cached | 141,199 | 87,450 | 1.615 | 36,259 |
| T4 | Aggregates | GroupByCount | cached | 10,146 | 7,026 | 1.444 | 1,715 |
| T5 | FeaturesFairCached | Cte | cached | 119,286 | 92,805 | 1.285 | 7,920 |
| T6 | FeaturesFairCached | Intersect | cached | 65,362 | 51,282 | 1.275 | 3,824 |
| T7 | Paging | PageAsync | cached | 22,866 | 18,033 | 1.268 | 1,226 |
| T8 | FeaturesFairCached | Except | cached | 65,526 | 51,763 | 1.266 | 3,410 |

Notes:

- The design's four initial targets (Cte, RecursiveCte, Except, Intersect) are T5, T3, T8, T6.
  The manifest additionally freezes **Any, Where, GroupByCount, PageAsync** — they are paired,
  over `1.20`, and per the design §14 Q3 they belong to the frozen population.
- Matching is scenario-normalized (`ToList`/`ToListAsync`, `Page`/`PageAsync`,
  `Async`/`AsyncStream`); arms with no linq2db counterpart stay controls. Raw-reader arms are
  excluded by the owner.
- Report values are per benchmark operation (the arms run 10-100 inner iterations); the ratio is
  the acceptance quantity, per-query reduction is derived per class when measured.

## 3. Baseline decomposition (measured 2026-11-08)

Construction is the addressable mass. Measured on a real `SqliteDataContext` with the exact
committed comparison shapes, no database opened, Release, .NET 10 (`N=200` warm iterations in the
standalone probe; the harness run below is the canonical record:
`benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.Issue319ConstructionDecomposition-report-github.md`,
`ShortRun` + `InProcessEmit`, 3 warmup / 3 iterations):

| Arm (harness) | Mean | Allocated | vs `Select` baseline |
| --- | ---: | ---: | ---: |
| Identity_Control | 416 ns | 1.84 KB | 0.90 |
| Select_Control (baseline) | 676 ns | 2.05 KB | 1.00 |
| WhereSelect_Control | 1,263 ns | 3.07 KB | 1.50 |
| Intersect_Construct (T6) | 1,653 ns | 5.11 KB | 2.50 |
| Except_Construct (T8) | 1,654 ns | 5.11 KB | 2.50 |
| RecursiveCte_Construct (T3) | 5,748 ns | 10.93 KB | 5.34 |
| Cte_Construct (T5) | 6,939 ns | 9.78 KB | 4.78 |
| Intersect_ConstructAndPrepare (T6) | 10,387 ns | 12.83 KB | 6.27 |
| Except_ConstructAndPrepare (T8) | 10,594 ns | 12.83 KB | 6.27 |
| RecursiveCte_ConstructAndPrepare (T3) | 22,755 ns | 23.09 KB | 11.28 |
| Cte_ConstructAndPrepare (T5) | 33,169 ns | 23.38 KB | 11.42 |

Piece costs inside one `Select` command (standalone probe, same run conditions):

| Piece | B |
| --- | ---: |
| `From<T>()` builder object | 424 |
| non-capturing `Expression<...>` tree | 488 |
| capturing `Expression<...>` tree | 683 |
| `QueryDefinition` object (isolated probe) | 240 |
| `CreateCommand` body (command object + wiring) | 928 |
| `QueryCommand.Clone()` (set-operation left operand) | 928 |
| set-operation delta over building both operands | 928 |

**Correction (2026-11-08, measured in the acceptance harness).** The 240 B `QueryDefinition` row is a
probe artefact: on the builder path the JIT stack-allocates the transfer record, so replacing it with
the existing stack-only `QueryShape` (issue #319 groundwork that was written for exactly this) moved
`Except` by only ~113 B/op and `Select` by 0 in the BenchmarkDotNet harness. That change was reverted
and the transfer record is **not** a lever.

**Second correction (2026-11-08).** Moving the *prepared-only* group into a lazily allocated payload
(batch: plan key + six comparers + seven prepared column arrays + `_whereBasePlanHash`, 18 fields,
object −128 B measured in the probe) also failed to move the arms: `Except` 63.99 -> 63.21 KB
(−7.4 % of the required ~5.6 KB/op), because the payload is allocated on the hit path (the plan key
and the comparers are hit-path state) and it offsets the shrink. The batch was reverted.

The conclusion for the architecture is the opposite of "prepared state is heavy": the 872 B object is
dominated by **raw** state (~60 fields: condition, projection, joins, sorting, from, CTEs, paging,
flags, provider options), which every fresh command needs and every operator copies. Only
consolidating that group into the raw description (`RawQueryState` held by the command) removes the
mass; moving prepared state just relocates it.

Consequences:

1. One legacy command costs **928 B** plus a **240 B** `QueryDefinition`; a set operation adds a
   **928 B** clone. These are exactly the objects the design replaces with a compact immutable
   raw handle and shared nodes.
2. The floor that no redesign removes is the caller's expression tree (488 B) and the builder
   object (424 B, in scope per design §14 Q2 but much smaller).
3. Except/Intersect need only `-3,410/-3,824` B per op; one command clone is 928 B per query and
   these arms run 10 inner iterations per op — the raw descriptor closes both with margin.
4. RecursiveCte needs `-36,259` B/op: it builds two source commands, one `UnionAll` clone, a CTE
   declaration list and a third command — five legacy command objects. Removing 928 B each plus
   the clone already exceeds the requirement.
5. Any/Where/GroupByCount/Page need confirmation that their overage is construction-bound (see
   risk R3 and increment I6).

## 4. Evaluation contract and phase boundary

Adopted verbatim from design §3; the plan fixes only the mechanics:

- Collection (`Where`, `Select`, joins, CTEs, set operations, `ToCommand`) creates the raw
  description and cheap guards (null/arity). No key, no hash, no translation, no provider check,
  no SQL, no connection.
- At execution or `Prepare`: normalize/validate the key, look up, bind fresh values.
  Miss compiles the full plan through the existing planner (legacy materialize-and-compile
  adapter, design §14 Q1). Hit never rebuilds SQL and never materializes the heavy graph.
- Checks that prevent a false key match stay on the hit path; they may not be deferred to miss.
- Uncacheable shapes (unkeyed `HAVING`, join, select captured collections per design §14 Q6) fail
  closed to compile-every-time; that is existing behaviour, not new work.
- The shared `Any`/`Count` command keeps its sticky-cache trap closed: no
  `queryCommand.Cache = false` per call, call-local `storeInCache` only.

## 5. Semantic oracle (built before the first engine route switches)

1. **Key semantics (unit, `tests/nextorm.core.tests`)** — identity of equal raw descriptions
   (same hash and `Equals`), hash collision resolution by structural equality, distinct keys for
   every SQL/result-shape-affecting dimension, immutable branch isolation (mutating a branch's
   added node does not change the source or a sibling), multiple terminals on one query, filter
   shape/config-generation partition, captured-collection cardinality/nullability/element type.
2. **SQL parity (provider dialect suites)** — byte-identical SQL text, parameter order/names and
   result column order for every migrated shape; existing dialect tests are the oracle and stay
   unchanged (design §14 Q9). Fresh parameter values are expected to differ.
3. **Phase guards** — after `Select`/`ToCommand`: command not prepared, no SQL built, no hash
   computed, no connection touched; after a warm hit: no `PrepareCommand` rebuild, but current
   values rebound. Asserted with call-local markers (a test-only counter hook), never permanent
   production overhead.
4. **#268/#269 state independence** — typed CTE/derived sources keep canonical identity
   independently of prepared aliases; repeated row checks, not count-only.
5. **Live integration** — both live suites against PostgreSQL, SQL Server, MySQL, MariaDB,
   ClickHouse, SQLite per `.opencode/skills/running-integration-tests/SKILL.md`; no skips
   presented as green.

## 6. R15 mapping (required deliverable of design §14)

Historical precondition: capture+apply `< 50%` of the full prepare. The native path has no
capture/apply, so the plan maps the phases explicitly; it does not declare R15 waived:

| Historical phase | Native path equivalent |
| --- | --- |
| raw-shape capture (`RawShapeCapture`) | canonical key derivation from the raw description |
| candidate lookup | cache lookup by key + structural equality |
| apply (`ApplyPreparationFrom`) | per-execution value binding into the cached binder metadata |
| prepared denominator | full prepare: translation + provider checks + SQL rendering + mapper build |

Oracle to run in I1 and repeat in every later increment: for the four initial targets and the
#268/#269 cases, measure `construct + key + lookup + bind` against `construct + full prepare`;
the ratio must stay `< 0.50`. The owner's neutral-noise time criterion (design §14 Q4) is the
time verdict; R15 remains a cost guard, not a substitute acceptance floor.

## 7. Measurement method

- Frozen baseline: `HEAD ac69d30e` plus the committed 2026-11-07 reports. Candidate and baseline
  are measured in the same round, paired ABBA, same machine, `ShortRun` + `InProcessEmit`
  (repo default; `NEXTORM_BENCH_FULL=1` for the full job).
- Allocation: `MemoryDiagnoser` `Allocated` per operation; acceptance `<= 1.20x` linq2db per
  target; controls that were `<= 1.20` stay `<= 1.20`.
- Time: A/A repeatability envelope is measured first; the plan locks the statistical method and
  confidence level to the envelope before the candidate run. Qualified small within-noise
  changes pass; a confirmed positive paired regression fails; unqualified/wide measurements are
  UNVERIFIED, not success. No blanket 20% band is imported.
- SQLite only for performance; correctness on all providers.
- Phase attribution comes from `benchmarks/nextorm.benchmark/Issue319/` (construct / construct+prepare
  / warm-hit-keys), not from guesses.

## 8. Increments

Each increment is independently reviewable, keeps the full test matrix green, and is measured
against the frozen baseline before the next one starts.

**I1 — gates (delivered 2026-11-08).**
Frozen the manifest; added the construction decomposition benchmark
(`benchmarks/nextorm.benchmark/Issue319/Issue319ConstructionDecomposition.cs`); added the phase
guards (`tests/nextorm.sqlite.tests/PlanCache/Issue319PhaseGuardTests.cs`); recorded the R15 mapping
and the baseline table of §3. No engine behaviour change.
Acceptance met: the manifest reproduces (85 arms / 8 targets / 55 controls / 19 unpaired / 3
excluded); the harness runs (report above); the guards pass on collected commands and prove that
materialization does prepare and render SQL, so they cannot pass vacuously; `tests/nextorm.sqlite.tests`
is 2310/2309/1 (the one skip is the env-gated LOB probe) and the full solution builds with warnings
as errors.

**I2 — raw core (delivered 2026-11-08).**
`RawQuery<T>` readonly value handle plus immutable compact nodes (source, where, projection,
joins, set operation, CTE list, paging, sorting, hints/options) and the canonical key with
structural equality. Nothing is wired to execution yet; the nodes are covered by key/immutability
unit tests only.
Acceptance met: unit oracle for §5.1 (8 tests); no allocation or SQL change to any arm (no route
is switched); measured raw-description cost recorded in §11.

**I3 — single-source native route.**
`From<T>()/Where/Select` produce the native handle; execution materializes the legacy compile
graph once on miss and caches the immutable compiled plan. Phase guards prove no key/SQL/
connection during collection and no rebuild on hit.
Acceptance: byte-identical SQL for every migrated shape; all 85 manifest arms within noise on
time and `<= 1.20x` on allocation for arms already at `<= 1.20x`.

*Status 2026-11-08:* blocked on the command object, not on the transport record. Routing the
builder through the handle while `Select` still returns a `QueryCommand<T>` leaves the 872-928 B
command in the hot path, and the measured levers around it (transfer record, prepared payload) are
worth zero to tens of bytes in the acceptance harness (§3 corrections, §9 R6). The first half of I3
is therefore the **raw-field consolidation** described in §9 R6: the command holds a
`RawQueryState` value instead of ~60 raw fields, the prepared group moves to a miss-only payload and
the plan key/comparers stay as small hit-path fields. Only then can the builders be routed through
the description.

**I4 — set operations.**
`Union/UnionAll/Intersect/IntersectAll/Except/ExceptAll` become raw operand nodes; the 928 B
left-operand clone disappears.
Acceptance: T6 and T8 `<= 1.20x` with no time regression; set-operation SQL unchanged; branch
isolation and multi-terminal tests pass.

**I5 — CTE and recursive CTE.**
CTE declarations and recursive bodies join the raw description; typed and string CTE routes share
one canonical shape.
Acceptance: T5 and T3 `<= 1.20x`, no time regression, #268/#269 parity.

**I6 — remaining targets and cleanup.**
Any/Where/GroupByCount/PageAsync: attribute each overage with the decomposition harness and
migrate what is construction-bound. If an overage is execution-bound beyond this architecture,
stop and raise the narrow owner decision required by design §12 instead of relaxing the floor.
Remove the obsolete raw-recipe machinery once unreachable (design §14 Q8), update EN/RU docs and
samples for the public renames, sweep the full matrix, both live suites, and both coverage gates.

## 9. Risks

- **R1 Recursive CTE depth.** Largest required reduction (36,259 B/op) over the most nested shape;
  the raw operand reference must not reintroduce a clone in the CTE declaration list.
- **R2 Public API break blast radius.** `QueryCommand<T>` is used across `EntityBuilder*`,
  `JoinedEntityBuilder`, providers and terminals; the migration is staged per route, not a single
  big-bang replacement.
- **R3 Execution-bound targets.** Any/Where/GroupByCount/Page may be dominated by row
  materialization or parameter binding rather than construction. Design §12 forbids relaxing the
  floor: the increment stops and reports if the architecture cannot close them.
- **R4 Key completeness.** A missing dimension silently reuses a wrong plan. Fail-closed to
  compile-every-time; the key inventory in I2 enumerates every comparer/preparer field rather
  than assuming `QueryDefinition`/`QueryShape` completeness.
- **R5 Measurement noise.** Underpowered runs are UNVERIFIED; the A/A envelope and pairing are
  locked before the candidate run.
- **R6 Command object mass.** `QueryCommand` carries ~90 reference fields inline (~872 B) and the
  set operation adds a 928 B clone. Measured 2026-11-08: the builder-to-command transfer record is
  stack-allocated by the JIT (<=113 B/op, rejected); the prepared payload split with the plan key
  and comparers inside it is offset by hit-path allocation (rejected); the same split with only
  miss-only state gives a clean 56 B of object per 7 fields, but the arm moves only ~1.6 B of
  operation allocation per 1 B of object. Reaching `<= 1.20x` on `Except`/`Intersect` therefore
  needs ~26 prepared reference fields moved (of ~30); the raw group (~60 fields) is the next, larger
  stage and is what removes the remaining mass for CTE/recursive.

## 10. Definition of done

- All 8 frozen targets `<= 1.20x` linq2db allocation; every control `<= 1.20x` that was before.
- No confirmed positive paired time regression on any target or control under the qualified
  noise contract; explicit R15 mapping `< 0.50` for the measured cases.
- Byte-identical SQL/parameters/column order; #268/#269 parity; phase guards green.
- Full unit, provider dialect, and both live integration suites green with no provider skipped;
  both coverage gates unchanged (85/75 and 65/75).
- EN and RU documentation and samples updated for every public rename in the same change.
- No commit, push or merge by the agent.

## 11. Progress log

- **2026-11-08 — I1 delivered.** Manifest frozen (`docs/superpowers/specs/2026-11-08-issue-319-population-manifest.csv`,
  generator `tools/issue319/build_population_manifest.py`, pinned to `f855b884`); decomposition
  harness and its first report added; phase guards added and green; baseline recorded in §3.
  No commits (owner keeps git).
- **2026-11-08 — I2 delivered.** Raw core in `src/nextorm.core/Query/Raw/`:
  `RawQuery<T>` value handle, immutable `RawQueryState` (source/join/ordering/set-operation/CTE
  nodes published once and shared), `RawQueryKey` with a structural hash plus structural equality,
  and `RawQueryRegistry` (fails closed for nested commands and outer references). Unit oracle
  `tests/nextorm.core.tests/Raw/RawQueryKeyTests.cs` (8 tests): equal shapes share the key, a
  captured runtime value does not change it, different closure sites differ, branching never
  mutates the source or a sibling, 27 SQL-affecting dimensions stay distinct, a forced hash
  collision is resolved by structure, an empty description fails closed.
  Measured raw-description cost (standalone probe, `N=500`, Release; legacy numbers are the §3
  constructive controls): `Select` 496 B vs 2,304 B, `Where+Select` 1,256 B vs 3,321 B, `Except`
  1,200 B vs 5,570 B, `RecursiveCte` 4,688 B vs ~11,900 B; key derivation adds 56 B (`Select`) and
  112 B (`Except`). The handle itself allocates nothing; the remaining bytes are the caller's
  expression trees and the engine's existing clause value types. No engine route is switched yet.
  `tests/nextorm.core.tests` 3923/3923 green, full solution builds with warnings as errors.
- **Next — I3.** Route `From<T>()/Where/Select` through the handle: on miss materialize the legacy
  compile graph once and cache the immutable compiled plan; on hit bind fresh values without
  rebuilding. Acceptance: byte-identical SQL, phase guards, all 85 manifest arms within the
  time/noise contract and `<= 1.20x` where they already were; then measure the four initial
  targets end-to-end.
- **2026-11-08 — I3 investigation (negative result, recorded).** Wired `EntityBuilder.SelectCore`
  to the existing stack-only `QueryShape` (the builder-to-command path the type was written for),
  so the builder allocates no `QueryDefinition`. Standalone probe: −240 B per command. Acceptance
  harness (`SqliteBenchmarkFeaturesFairCached`, fresh run): `Except` 63.99 -> 63.99 KB,
  `Intersect` 63.83 -> 63.83 KB, `Cte` 116.49 -> 116.49 KB, `RecursiveCte` 137.9 -> 137.9 KB;
  the construct-only harness moved `Except_Construct` 5.11 -> 5.00 KB. The JIT already
  stack-allocates the transfer record, so the change was reverted rather than reported as a win.
  Conclusion recorded in §3 (correction) and §9 (R6): the mass is the command object itself.
- **2026-11-08 — key boundary completed.** The canonical key now folds the materialization-time
  identity: `RawQueryContext` (provider type, quoting, naming convention by reference, keyword
  case, command timeout, filter generation, configuration generation) and the per-query
  `QueryFilterScope` in the raw state. Oracle extended to 10 tests: every identity dimension
  partitions the key, an identical identity shares it, a forced hash collision is still resolved
  structurally, an empty description fails closed. `tests/nextorm.core.tests` green.
- **2026-11-08 — I3a investigation (negative result, recorded).** Split the prepared-only group
  (plan key, six comparers, seven prepared column arrays, `_whereBasePlanHash`) into a lazily
  allocated payload. Probe: `Select` 2304 -> 2176 B, `Except` 5570 -> 5186 B (object −128 B per
  command). Acceptance harness: `Except` 63.99 -> 63.21 KB, `Intersect` 63.83 -> 63.05 KB — −7 % of
  the required ~5.6 KB/op, because the hit path allocates the payload for the plan key and the
  comparers. Reverted; the ownership test (`QueryCommandComparerOwnershipTests`) also pinned that
  the six comparers must survive `ResetPreparation`, which constrains where they can live.
- **2026-11-08 — I3b group 1 (kept).** Payload introduced
  (`QueryCommand.PreparedState.cs`) holding the seven prepared column arrays and
  `_whereBasePlanHash`; the plan key and the six comparers stay as command fields because the hit
  path writes them. Object −56 B per command (probe: `Select` 2304 -> 2248 B, `Except` 5570 ->
  5402 B), acceptance harness `Except` 63.99 -> 63.13 KB, `Intersect` 63.83 -> 62.97 KB. The
  measured slope in the arm is ~1.6 B of operation allocation per 1 B of command object, i.e. the
  remaining gap needs roughly 26 prepared reference fields (of ~30) moved before `Except`/
  `Intersect` reach `<= 1.20x`. All suites green with the group in place: core 3925/3925,
  SQLite 2310 (1 env-gated skip), solution builds with warnings as errors.
- **2026-11-08 — I3b group 2 (kept).** Moved the remaining miss-only state into the payload
  (`PreparedCondition`, `PreparedHaving`, `PreparedPreWhere`, `PreWhereShapeHash`,
  `HasPreWhereInValues`, `InValuesPartitions`, `LookupPartitions`, `HasTopLevelInValues`,
  `ShapeScanned`, `ShapeValuesStable`, `HasUnkeyedTupleInValues`, `HasUnkeyedScalarInValues`,
  `ReferencedQueries`, `OuterRefs`, `OuterRefBindings`, `ApplyLeftOwners`) with delegating
  properties so no call site changed, and introduced `EntityBuilderExtras<TEntity>` holding 21 rare
  fluent options behind one reference (writes of a default value never allocate). Object −136 B per
  command and −160 B per builder (probe: `Select` 2304 -> 2008 B, `Except` 5570 -> 4842 B,
  `Cte` 10435 -> 9363 B, `Recursive` 11945 -> 10713 B).
  Acceptance harness: `Cte` 116.49 -> 110.71 KB (1.285 -> 1.222), `Except` 63.99 -> 61.57 KB
  (1.266 -> 1.218), `Intersect` 63.83 -> 61.41 KB (1.275 -> 1.226), `RecursiveCte` 137.9 ->
  129.31 KB (1.615 -> 1.514). No target is closed yet: `Except`/`Intersect` are ~1.0-1.3 KB/op
  short (≈7-9 more prepared reference fields), `Cte` ~2.0 KB/op short, `RecursiveCte` ~27 KB/op
  short.
  Suites green: core 3925/3925, SQLite 2310 (1 skip), ClickHouse 1505/1505, PostgreSQL 1946/1946,
  SQL Server 2084 (8 env-gated skips), MySQL 1215/1215; solution builds with warnings as errors.
- **2026-11-08 — time check (no verdict: environment underpowered).** Paired ABBA on the warm arms
  (`SqliteBenchmarkFeaturesFairCached`, full job, baseline = detached worktree
  `/tmp/opencode/nextorm-base-time-20261108` at `ac69d30e`): `Cte` +3.0 %, `Except` -6.4 %,
  `Intersect` -7.5 %, `RecursiveCte` -4.9 %, control `CaseWhen` +1.4 %. The same-round A/A spread
  on the *same* binary was 2.6 % for `Cte`, 9.6 % for `Except` and 15 % for `Intersect`, and one
  pass was 2.7x slower than the next on every arm, so no point estimate is separated from noise.
  The construct-only harness (6 interleaved passes) gives `Select` -5.6 %, `WhereSelect` +2.0 %,
  `Except` -1.9 %, `Cte` +2.4 % with B/B spread up to 14 %; a focused prepare-path run gives
  `Except_ConstructAndPrepare` -7.1 % and `RecursiveCte_ConstructAndPrepare` -2.8 %.
  Per the design contract this is UNVERIFIED, not a pass: no confirmed positive regression was
  observed, but the harness is far too noisy on this machine to certify neutral time. The only
  structural overhead the change adds is one indirection on prepared-state reads (prepare/render
  path) and on rare builder options (construction); warm execution is untouched and allocates less.
  Re-run on a quiet machine before claiming the time criterion.
- **2026-11-08 — I3b group 4 (kept): all four initial targets within the allocation floor.**
  Three changes, each measured on the frozen harness (ShortRun + InProcessEmit, same job as the
  baseline):
  1. **Fingerprint-based raw candidate probe.** The warm hit of a fresh command is now keyed by an
     allocation-free structural fingerprint (`QueryCommand.ComputeRawFingerprint`) instead of a
     captured `RawShape` object graph. The fingerprint covers every dimension the descriptor
     comparison reads, recurses into union/CTE bodies and folds the captured closure identities; the
     comparers are created once for the whole graph (`RawFingerprintComparers`), not once per nested
     node. The descriptor itself is captured only on the *miss*, where it is published
     (`QueryPlanStore.TryGetRawFingerprint` is the hit probe). The staged normalized-key check remains
     the correctness gate, exactly as before.
  2. **Projection-slot recognition is allocation-free on the scalar path.**
     `CollectionSlotBinder.TryBind` no longer allocates a capturing relationship resolver,
     `CollectionSlotRecognizer.Recognize` allocates its accumulation lists only when a slot is
     recognized, the rooted-navigation fast path avoids the visitor for a plain member chain, and
     `TryGetCollectionElement` uses a loop instead of `GetInterfaces().FirstOrDefault(...)`
     (measured 1,112 -> 960 B per `Select`).
  3. **Command object slimmed (`QueryCommandRawOptions`)** and **`SelectCore` switched to the
     stack-only `QueryShape`** transfer record. The 17 rarely used raw options (settings, hints,
     clauses, navigation paths, outer registry) live behind one lazily allocated reference; writes of
     a default value clear in place and never allocate. `QueryDefinition` (240 B, measured) is no
     longer materialised per projection.
  Acceptance harness (B/op, linq2db denominator from the frozen manifest):
  `Cte` 119,286 -> **92,887** (1.285 -> **1.001x**), `Except` 65,526 -> **42,322** (1.266 ->
  **0.818x**), `Intersect` 65,362 -> **42,158** (1.275 -> **0.822x**), `RecursiveCte` 141,199 ->
  **100,966** (1.615 -> **1.155x**). Every other frozen cached arm moved down as well and no control
  regressed: `CaseWhen` 34.69 -> 26.80 KB (0.912 -> 0.704x), `Join4` 114.47 -> 98.60 (0.813 ->
  0.700x), `LeftJoin` 66.57 -> 55.00 (0.774 -> 0.639x), `Distinct` 41.56 -> 30.00, `Contains`
  44.77 -> 35.31, `ToUpper` 44.77 -> 35.31, `Udf` 45.63 -> 36.17, `RowNumber` 53.75 -> 45.55,
  `SumOver` 55.71 -> 47.50, `In_AtIn_Inline` 79.61 -> 75.16, `In_AtIn_Captured` 98.13 -> 93.68,
  `In_ListContains_Inline` 80.94 -> 76.49, `In_ListContains_Captured` 100.09 -> 95.63.
  Time (paired ABBA, baseline worktree at `ac69d30e` vs this tree, two interleaved passes, same
  job): `Cte` 344.2/339.9 -> 288.6/290.0 us (**-15.4 %**), `RecursiveCte` 363.0/350.1 ->
  313.6/306.1 us (**-13.1 %**), `Except` 178.4/175.1 -> 163.4/160.5 us (**-8.4 %**), `Intersect`
  169.5/169.6 -> 156.6/153.8 us (**-8.5 %**), control `CaseWhen` 134.4/133.5 -> 125.4/125.7 us
  (**-6.3 %**). The same-side spread was 0.5-3.7 %, so the deltas are separated from the noise: the
  arms got faster, not slower — fewer objects and fewer survivors per query also remove GC work.
  Measurement note: while this ran, an in-progress `SelectGroup` API change elsewhere in the tree
  left `benchmarks/nextorm.benchmark/SelectGroup/D362SelectGroupBenchmarks.cs` uncompilable, so the
  sweep was first taken from a byte-identical copy under the gitignored `artifacts/` directory with
  only that file (and a stub of its `D362BenchRow`) excluded. The numbers above were then reproduced
  from the committed benchmark project once it compiled again: `Cte` 90.71-91.81 KB,
  `Except` 41.33 KB, `Intersect` 41.17 KB, `RecursiveCte` 98.60 KB, `CaseWhen` 26.80 KB. The
  temporary copy was deleted.
  New harness: `benchmarks/nextorm.benchmark/Issue319/Issue319PreparationProbe.cs`
  (`--issue319-prep-probe <outDir>`), which attributes construction, fingerprint, raw lookup,
  prepared-state apply and plan-key build.
- **2026-11-09 — I6 attribution delivered; T7 closed; T1 improved; T2/T4 blocked on the core.**
  New harness `benchmarks/nextorm.benchmark/Issue319/Issue319TargetsProbe.cs`
  (`--issue319-targets <outDir>`) decomposes the four remaining frozen targets into
  builder/command, preparation (raw-recipe reuse or full prepare), plan-key lookup and execution,
  both per call and in the benchmark's own shape (one operation = the whole iteration loop, with the
  captured value hoisted into one display class per operation). Measured attribution (B/op):
  - **Any (T1)** 6,025 = builder 1,024 + `ToParentCommand` 552 + subcommand full prepare 1,232 +
    plan hit 2,448 + execution 760. The plan hit was **not** the store lookup (80 + 48 B); it was
    `RefreshParameters` falling back to `ExtractParams`, i.e. **re-rendering the whole statement**
    on every execution, because the parameter recipe never supported a capture that lives inside the
    referenced subquery the shared `Any`/`Count`/`All` wrapper renders.
  - **Where (T2)** 6,155 = builder 1,024 + `Select` (tree + command) 1,624 + preparation 2,592 +
    execution 1,018. The preparation is a full prepare on every operation because the D201 recipe
    keys captured closures by **identity**, and a benchmark operation hoists a new display class:
    the recipe can never hit, so the arm measures its cold path. When the closure is stable the hit
    costs 848 B (fingerprint 424, apply 232, key 160).
  - **GroupByCount (T4)** 9,175 = caller expression trees 2,656 + builder/command 912 + recipe hit
    816 + execution 4,656. It reuses the recipe correctly (0 mismatches); the remaining overage is
    entirely the apply+key+hash work on a fresh command.
  - **PageAsync (T7)** 21,304 = command 4,832 + preparation 2,216 + execution 14,256, and the
    preparation was a **rejected apply on every call** (`RecipeApplyMismatches` = one per call).
  Three product defects found by the attribution were fixed:
  1. **A sorted query could never reuse the raw recipe.** `Sorting.PreparedExpression` is filled in
     place by preparation and is part of the normalized key, while the fresh command's sort keys are
     still raw; the staged apply therefore built a key that differed from the stored one and
     discarded the candidate every time. `ApplyPreparationFrom` now adopts the stored sort array
     (and `DiscardStagedPreparation` restores the caller's). Regression test:
     `Recipe_SortedPageShape_ReusesInsteadOfRejectingTheStagedKey`.
  2. **A capture inside a referenced subquery forced a full SQL re-render per execution.**
     `ParamRefreshRecipe` now collects captures from a referenced query's prepared condition and
     projection (root kind + index travel with each parameter), so the shared `Any`/`Count`/`All`
     wrapper binds the current subquery's closure value positionally instead of re-rendering the
     statement. A captured `QueryCommand` member is not counted as a value capture (it is a
     referenced query, never a bound parameter). Regression test:
     `ReferencedQueryCapture_OnCacheHit_ShouldBindFreshValueWithoutReRendering`.
  3. **The raw fingerprint could not tell two `ORDER BY` members apart.** It hashed
     `Sorting.PreparedExpression` through the plan comparer, but a fresh command's sort keys are still
     unprepared, so every key contributed a null prepared expression and two different sort members
     shared one bucket. It now hashes the raw sort key (direction, null placement, column index, raw
     expression). This was latent before (a false candidate was rejected by the staged key, wasting an
     apply), but fix 1 makes the staged key match, so without it an invalid `ORDER BY` member would
     reuse a valid plan instead of throwing. Caught by
     `SqlitePreparationShapeCoverageTests.OrderByProjectedMember_ShouldRunAndUnknownMember_ShouldBeRejected`;
     regression test `Fingerprint_DistinguishesDifferentOrderByMembers`.
  Result on the frozen manifest (paired, same round; linq2db denominator re-measured):
  | target | linq2db | before | after | ratio before -> after | 1.20x limit |
  | --- | ---: | ---: | ---: | ---: | ---: |
  | T1 Any | 233.61 KB | 588.36 KB | **370.35 KB** | 2.518 -> **1.585** | 280.33 KB |
  | T2 Where | 253.53 KB | 601.06 KB | 601.06 KB | 2.371 -> 2.371 | 304.24 KB |
  | T4 GroupByCount | 6.85 KB | 8.96 KB | 8.96 KB | 1.308 -> 1.308 | 8.22 KB |
  | T7 PageAsync | 17.61 KB | 20.81 KB | **19.10 KB** | 1.182 -> **1.085** | 21.13 KB |
  **T7 is closed.** The cached-control sweep is unchanged or better (`Cte` 92.9 KB, `Join4` 97.43 KB,
  `Except` 41.33 KB, `Intersect` 41.17 KB, `RecursiveCte` 98.60 KB, `CaseWhen` 26.80 KB); no control
  regressed. Time: `Any` 2.345 -> 2.014 ms (the removed re-render), `Page` 110.1 -> 103.1 us, no
  positive regression on the others.
  **T1/T2/T4 remaining work is the design's core, not cleanup.** Per target, the arithmetic of the
  remaining gap:
  - `T1 Any`: 3,784 -> 2,870 B/call needs -914. Builder+closure tree (1,024), subcommand command
    (552), and execution (760) are 2,336 of the budget; the entire subquery preparation+hit
    (1,448) must therefore fall to ~534, i.e. the subquery must not be built and fully prepared
    from scratch at all.
  - `T2 Where`: even with a recipe hit the arm is 4,410 B (builder 1,024 + tree/command 1,624 +
    hit 848 + execution 1,018) against 3,042; the closure-identity key means a fresh display class
    per operation is a guaranteed full prepare today.
  - `T4 GroupByCount`: with the recipe hit at zero it would be 8,224 B, just inside 8,417, so it
    needs only the execute-the-stored-plan-without-applying-state route for parameter-free shapes.
  All three need the raw-handle/execution-binding core of design §4/§6/§7: a compact raw result from
  `Select`/`ToCommand` (so the 552-1,624 B command is not built), a value-agnostic raw key, and
  execution of the stored plan with freshly bound values instead of staging prepared state into a
  fresh command. That is the I3-I5 bulk and is **not** part of I6 cleanup. Per design §12 / plan I6
  this is raised as the narrow owner decision rather than relaxing the 1.20 floor:
  **(a)** authorize the raw-handle core migration (staged, per-route, keeping SQL/row parity), or
  **(b)** accept T1/T2/T4 as documented residuals of the current architecture with the measured
  attribution above.
- **Next — I3b group 4.** Time re-measurement on a quiet machine (the noise contract still governs),
  then the raw-field consolidation proper (the remaining ~40 inline raw fields) for the non-initial
  targets (Any/Where/GroupByCount/PageAsync) in I6.
- **2026-11-08 — I3b group 3 (kept): T6/T8 closed.** Moved the remaining non-protected prepared
  state (`BoxedResultType`, `EffectiveGroupingKeys`/`EffectiveHaving`/`EffectiveSourcePredicate`/
  `EffectiveHavingMode`/`EffectiveRequireDistinct`, `JsonShape`/`JsonShapeMode`, prepared component
  list/view and render shape) behind the payload with guards in the clone constructor, `CopyTo` and
  `ResetPreparation`. The protected E19 fields (`_selectList`, `_isPrepared`) were deliberately left
  as fields because they are an explicit compatibility contract (`T297ProtectedFieldCompatibilityTests`).
  Acceptance harness: `Except` 61.57 -> **60.16 KB** (1.218 -> **1.190x**), `Intersect` 61.41 ->
  **60.00 KB** (1.226 -> **1.198x**) — both within the 1.20 allocation floor; `Cte` 110.71 ->
  110.01 KB (1.214x), `RecursiveCte` 129.31 -> 128.05 KB (1.499x) still open.
  Control delta: `CaseWhen` 34.69 -> 34.92 KB (+235 B/op, +0.66 %, reproducible over four runs) —
  inside the control rule but recorded as a watch item; `Join4` improved 111.73 -> 110.4 KB.
  Suites green: core 3925/3925, SQLite 2310 (1 skip), ClickHouse 1505, PostgreSQL 1946,
  SQL Server 2084 (8 env-gated skips), MySQL 1215, extensions 155; solution builds with warnings as
  errors.
- **Next — I3b group 3.** Remaining prepared reference fields (`EffectiveGroupingKeys`,
  `EffectiveHaving`, `EffectiveSourcePredicate`, prepared component list/view,
  `_componentRenderShape`, `JsonShape`, `BoxedResultType`, `_selectList`) close `Except`/`Intersect`;
  then the builder-side and CTE/recursive routes need the raw-state sharing stage (§9 R6).
