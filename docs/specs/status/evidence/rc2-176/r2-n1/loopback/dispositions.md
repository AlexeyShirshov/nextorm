# D176 CHECK loop-back — W1–W8 / T1–T7 dispositions (r=2, n=2)

Source of the findings: CHECK verdict session `ses_ee2ec3639ffe...` (r2-n2), recovered from the
local opencode DB. The loop-back plan is `inner-loop/scope-d176.7-loopback.json`. No `r`/`rv` bump:
this is the same plan revision, second DO iteration (`n=2/3`).

Evidence root: `docs/specs/status/evidence/rc2-176/r2-n1/loopback/`.

## Warning findings (source code)

| ID | Finding | Disposition | Evidence / reason |
|---|---|---|---|
| **W1** | `JsonRowWriterFactory` array elements read via non-generic `Array.GetValue` + `Convert`, no `GetFieldType` dispatch → provider native-array element type mismatch. | **unverified + accepted** | Boxing measured: `NativeArrayInt_Stream` allocates **11,208 B/call (~11 KB)** for a 256-element `int[]` vs 1,360 B oracle (`perf/perf-notes.md`). Justification: no native-type conversion failure is demonstrated against any real driver, and the cost is within the supplied performance limits (C07/C08 exit 0; no case regressed), so this is accepted as a recorded future optimisation, not an in-scope defect. Covered indirectly by `JsonShapeWriterTests.{RootArray,NullableElementArray,JaggedArray_WithNullInner,OneElementArray}`. |
| **W2** | Array element nullability only `ReferenceEqual(item, null)`; a boxed `DBNull.Value` element made `Convert(DBNull,T)` throw instead of emitting `null`. | **fixed** | `JsonRowWriterFactory.BuildElementWrite` now tests both CLR `null` and boxed `DBNull.Value`. Guard: `JsonShapeWriterTests.DBNullElementInsideArray_ShouldWriteNull` (Pass). |
| **W3** | Nested empty construction (no-property type) contributes no column; if it is the only member the `lowered` list is empty and the user saw the misleading "requires an explicit Select projection" guard / invalid SQL. | **fixed** | `JsonShapePlan.Build` now throws an explicit `NotSupportedException` when a captured shape lowers zero scalar columns. Guards: `JsonStreamingTests.NestedEmptyConstruction_ShouldThrowBeforeOutput` (destination untouched) and `JsonShapeWriterTests.ZeroMemberObject_ShouldWriteEmptyObject` (member-less nested object allowed when the enclosing projection lowers ≥1 column). |
| **W4** | Capture classified a member via `TypeFacts.UnwrapConvert(...).Type` while the shape build classified from the declared member type → trigger/build divergence for an upcast/interface-typed member. | **guarded (no defect)** | Reproduced: ordinary materialization already rejects the same interface-typed member with `NotSupportedException`, so the JSON path is uniformly fail-closed rather than divergent. Probe: `w4-probe.log` / `w4-probe.md`. Guard: `JsonStreamingTests.InterfaceTypedUpcastMember_ShouldFailClosedBeforeOutput` (both `ToList` and `WriteJson` throw before output; Pass). |
| **W5** | `BuildJsonProjectionShape` returned `null` when any projected column lacked `ProjectionItem`, silently leaving `JsonShape` null so a mixed/partial `Projection<T1,T2>` fell through to the phase-1 flat writer (mapped names instead of `Item1`/`Item2`) instead of rejecting. | **fixed** | `QueryCommand.QueryPreparer.BuildJsonProjectionShape` now throws `NotSupportedException` on a mixed slot-tagged/untagged projection (`QueryCommand.QueryPreparer.cs:1285-1287`). Covered by the whole-entity slot tests. |
| **W6** | New `JsonShapeMode`/`JsonShape` were not cleared by `ResetPreparation()` → stale descriptor cross-call risk. | **fixed** | `QueryCommand.cs` (ResetPreparation) clears both flags; `DataContext.PrepareJsonStream` re-initialises them per call. Guards: `JsonStreamingTests.JsonThenOrdinary_AndReverse_ShouldNotLeakShape`, `RepeatedWriteJson_OnOneContext_ShouldBeStable`, `JsonWrite_ShouldNotDisablePlanCache` (all Pass). |
| **W7** | No guard that a descriptor ordinal is `< record.FieldCount` → provider `IndexOutOfRangeException` at first row read. | **fixed** | `JsonShapePlan.ValidateShapeOrdinals` / `EnsureOrdinal` validates every leaf, presence and array-element ordinal against the prepared selection at prepare time, failing closed with an explicit message. `JsonShapePlan.cs:218-248`. |
| **W8** | Captured branch validated converter/LOB but not shape↔select-list consistency. | **fixed** | Same `ValidateShapeOrdinals` walk; prepare-time reject rather than a first-row read failure. Covered with W7. |

## Test-obligation findings

| ID | Finding | Disposition | Evidence |
|---|---|---|---|
| **T1** | §10 row 5 (opaque factory/method-produced object) had only a code anchor. | **guard test added** | `JsonStreamingTests.OpaqueFactoryProducedObject_ShouldFailBeforeOutput` (Pass). |
| **T2** | §10 row 20 (DB-side JSON) had only a guard anchor. | **guard test added** | `JsonStreamingTests.DbSideJsonRoute_ShouldNotUseForJson` — SQL interceptor proves no `json_agg`/`json_object`/`FOR JSON` (Pass). |
| **T3** | Row 19 (duplicate name within one object) integration case was mapped to a both-arms-construction test (symbol/scenario mismatch). | **fixed** | `CommonTestSuite.JsonStreamNested.WriteJsonDuplicateNameWithinOneObject_ShouldThrowBeforeOutput` (rejects before output via a selective naming policy); mirrored in `MariaDbJsonStreamTests` and `ClickHouseIntegrationTests`; nested-scope SQLite unit `JsonStreamingTests.NestedObject_DuplicateNameWithinNestedScopeOnly_ShouldThrowBeforeOutput`. |
| **T4** | Missing terminal surfaces: whole-entity async, entity+scalar slot async, scalar+scalar slots async, conditional-root async. | **fixed** | Shared `CommonTestSuite.JsonStreamNested`: `WriteJsonWholeEntityAsyncMatchesSync`, `WriteJsonEntityAndScalarSlotAsyncMatchesSync`, `WriteJsonScalarScalarSlotsAsyncMatchesSync`, `WriteJsonConditionalRootAsyncMatchesSync`; mirrored MariaDB (32 WriteJson) + ClickHouse local slot async variants. |
| **T5** | Untested edges: reader failure mid-read, disposal, depth 64/65, zero-member object, one-element array, Postgres null/empty native array. | **fixed** | Core `ReaderFailure_MidRead_ShouldPropagate`, `DepthExactly64_ShouldBeAllowed`, `Depth65_ShouldThrowBeforeWriting`, `ZeroMemberObject_ShouldWriteEmptyObject`, `OneElementArray_ShouldWriteSingleElement`; SQLite `DisposedContext_ShouldThrowBeforeOutput`; Postgres `WriteJson_NativeArray_NullAndEmpty_ShouldMatchSerializer` (all Pass). |
| **T6** | ClickHouse lacked `WriteJson_ByteArrayNested_ShouldBeBase64` / `..._ByteArrayAsync...` and a duplicate-leaf-across-slots fixture; MariaDB lacked the Phase-1 `byte[]` test. | **fixed** | ClickHouse local `json_binary` fixture (`Array(UInt8)`→CLR `byte[]`) + both tests; ClickHouse `json_slot_dup_*` fixture + `WriteJson_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted`; MariaDB `WriteJson_ByteArray_ShouldMatchJsonSerializer` (Pass). |
| **T7** | E176.13/E176.14 logs were filtered-run summaries (totals), not per-symbol results. | **fixed** | `E176.13.log` / `E176.14.log` regenerated from xUnit v3 `-result-xml` with one `Pass <FQN>` line per symbol, and the case tables in `E176.13-cases.md` / `E176.14-cases.md` refreshed to the loop-back numbers (core 40, SQLite 63). |

## Also added this loop-back

- `JsonRowWriterFactory.ReadValue` Base64 now prefers typed `DbDataReader.GetFieldValue<byte[]>` with a
  plain-`IDataRecord` (boxed `GetValue`) fallback — enables provider-exposed `byte[]` values and the
  ClickHouse-local `Array(UInt8)` fixture without regressing the direct-writer tests.
- `JsonRowWriterFactory.MaxShapeDepth = 64` recursive depth guard (`DepthExactly64` / `Depth65`).
