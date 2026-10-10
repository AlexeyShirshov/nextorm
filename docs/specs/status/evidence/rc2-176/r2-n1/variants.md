# E176.11 / E176.12 — variant matrix and evidence ledger

Every §10 row is mapped to an **executed test symbol** or an **evidenced guard**. Symbols and
`file:line` anchors were resolved from the actual sources (Roslyn `members` + direct inspection); no
symbol is invented.

## Variant rows

| §10 variant | Closure | Executed test symbol(s) / guard anchor |
|---|---|---|
| Existing scalar and flat object; nullable/default/value/reference leaves | Test | core `JsonShapeWriterTests.FlatObject_NullableAndDefaultLeaves_ShouldFollowPhase1`, `FlatScalar_NullAndDefault_ShouldFollowPhase1`; sqlite `JsonStreamingTests.Array_ScalarInt_ShouldEqualJsonSerializerSerialize`, `.Array_FlatDto_ShouldEqualJsonSerializerSerialize`, `.TypedColumns_ShouldMatchJsonSerializer`; integration `WriteJson_TypedColumns_ShouldMatchJsonSerializer` (all providers) |
| Nested anonymous `new` | Test | core `.NestedObject_ShouldWriteResolvedMemberNames`, `.NestedObject_MultipleLevels_ShouldPreserveBoundaries`, `.AllNullChildren_ShouldStillWriteNestedObjects`, `.RepeatedSiblingNames_InSeparateScopes_ShouldBeAccepted`; sqlite `.NestedObject_ShouldMatchJsonSerializer`, `.NestedObject_MultipleLevels_ShouldMatchSerializer`; integration `WriteJson_NestedAnonymous_ShouldMatchSerializer`, `WriteJson_NestedMultipleLevels_ShouldMatchSerializer` |
| Named construction/member-initialization | Test | core `.MemberOrderDifferentFromOrdinal_ShouldBindDeclaredOrdinals`; sqlite `.NestedMemberInit_ShouldMatchSerializer`, `.ConditionalConstruction_MemberInit_ShouldMatchSerializer`; integration `WriteJson_NestedNamed_ShouldMatchSerializer` |
| Null-valued conditional nested construction with translatable predicate | Test | sqlite `.ConditionalConstruction_NullArmTrue_ShouldMatchSerializer`, `.ConditionalConstruction_NullArmFalse_ShouldMatchSerializer`, `.ConditionalConstruction_AllNullProperties_ShouldStayObject`, `.ConditionalConstruction_Root_ShouldMatchSerializer`, `.ConditionalConstruction_Async_ShouldMatchSync`; core `.ConditionalConstruction_PresentSentinelWithAllNullMembers_ShouldWriteObject`, `.ConditionalConstruction_AbsentSentinelWithPresentMembers_ShouldWriteNull`; integration `WriteJson_Conditional*` |
| Opaque factory/method-produced object or unresolvable ctor member | Guard | `QueryCommand.QueryPreparer.JsonShapeNeedsCapture` (`src/.../QueryCommand.QueryPreparer.cs:905`) only captures `New`/`MemberInit`/supported `Conditional`; anything else stays a scalar leaf and fails in `JsonShapePlan.Classify` (`JsonShapePlan.cs:264`). Executed guard: sqlite `.UnsupportedType_ShouldThrowBeforeOutput`, core `.UnsupportedScalarMemberType_ShouldThrowBeforeWriting` |
| `Projection<T1,T2>` entity/entity | Test | sqlite `.BareProjection_LeftJoin_ShouldMatchItem1Item2`, `.BareProjection_InnerJoin_ShouldMatchItem1Item2`, `.BareProjection_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted`; integration `WriteJson_BareProjectionLeftJoin_ShouldMatchItem1Item2`, `WriteJson_BareProjectionInnerJoin_ShouldMatchItem1Item2`; anchor `QueryCommand.QueryPreparer.BuildJsonProjectionShape` (`:1277`) |
| Supported scalar/entity and scalar/scalar slot forms | Test | sqlite `.EntityItemWithScalarMember_ShouldNotWrapScalarInObject`, `.ScalarScalarSlots_ShouldBeFlatScalars`; integration `WriteJson_EntityAndScalarSlot_ShouldNotWrapScalar`, `WriteJson_ScalarScalarSlots_ShouldBeFlatScalars` |
| Inner/outer joins, matched/unmatched entity slots | Test | sqlite `.BareProjection_LeftJoin_ShouldMatchItem1Item2` (asserts `"Item2":null`), `.BareProjection_InnerJoin...`; integration `WriteJson_BareProjectionLeftJoin_ShouldMatchItem1Item2` |
| Duplicate effective JSON name within one object | Guard + test | `JsonRowWriterFactory.BuildMemberWriters` (`:232`, per-scope `seen`) + `ResolveMemberName`; core `.DuplicateNameWithinOneScope_ShouldThrowBeforeWriting`; sqlite `.NestedObject_DuplicateNameWithinScope_ShouldThrowBeforeOutput`; integration `WriteJson_ConditionalBothArmsConstruction...` |
| Same name in different object scopes | Test | core `.SameNameInDifferentScopes_ShouldBeAccepted`; sqlite `.NestedObject_SameNameInDifferentScopes_ShouldBeAccepted`, `.BareProjection_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted`; integration `WriteJson_NestedSameNameDifferentScopes_ShouldBeAccepted`, `WriteJson_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted` |
| Native rank-one scalar `T[]` | Test | core `.IntArray_ShouldRecurseElements`; integration `WriteJson_NativeArrays_ShouldMatchSerializer` (ClickHouse + PostgreSQL `WriteJson_NativeStringArrayAgg_ShouldMatchSerializer`); benchmark `JsonStreamPhase2WriterBenchmark.NativeArrayInt_Stream` |
| Jagged native arrays | Test | core `.JaggedIntArray_ShouldRecurseNestedArrays`; integration `WriteJson_NestedJaggedArray_ShouldMatchSerializer`; benchmark `NativeArrayJagged_Stream` |
| Null/empty arrays and supported nullable/reference elements | Test | core `.NullAndEmptyArray_ShouldWriteNullAndEmpty`, `.NullElementInsideArray_ShouldWriteNull`, `.NullableElementArray_ShouldWriteNullElement`, `.JaggedArray_WithNullInner_ShouldWriteNullElement`; integration `WriteJson_NativeArrays_EmptyRow_ShouldEmitEmptyArrays` |
| `byte[]`, incl. inside a jagged array — Base64 precedes array handling | Test | core `.ByteArrayMember_ShouldBeBase64`, `.JaggedByteArray_ShouldBeBase64Recursively`; sqlite `.BytesType_ShouldBase64Encode`; integration `WriteJson_ByteArrayNested_ShouldBeBase64`; guard `JsonRowWriterFactory.BuildElementWrite` (`:388`) |
| Multidimensional arrays; unsupported array element types | Guard + test | `JsonRowWriterFactory.ValidateArrayType` (`:432`) + `JsonShapePlan.Classify`; core `.MultidimensionalArray_ShouldThrowBeforeWriting`, `.UnsupportedElementType_ShouldThrowBeforeWriting`, `.CollectionElementType_ShouldThrowBeforeWriting` |
| `List<T>`, arbitrary `IEnumerable<T>`, dictionaries, converter-backed collections | Guard + test | core `.DictionaryAndEnumerableArrayElements_ShouldThrowBeforeWriting`; `Classify` rejects non-whitelisted element/value types |
| Enum/new converter types | Guard; **deferred #178** (trigger: its supported-type contract lands) | `JsonShapePlan.Classify` rejects enums; sqlite `.UnsupportedType_ShouldThrowBeforeOutput` |
| Child-collection queries | Guard; **deferred #172** (trigger: reachable query support) | `JsonShapeNeedsCapture` does not capture `List<T>`/`IEnumerable<T>` members; core `.CollectionElementType_ShouldThrowBeforeWriting` |
| New serializer options/naming-policy behaviour | **Deferred #177** (trigger: its contract lands); Phase-1 behaviour tested unchanged | sqlite `.PropertyNamingPolicy_CamelCase_ShouldApply`, `.NamingPolicy_CollisionOrNull_ShouldThrowBeforeOutput`, `.UnknownJsonStreamMode_ShouldThrowBeforeOutput`, `.NdJson_WithRoot_ShouldThrow`, `.NdJson_WithWriteIndented_ShouldThrow`; core `.NamingPolicy_ShouldApplyPerObjectScope` |
| DB-side JSON | Guard against accidental route; **deferred** (trigger: explicit scope approval) | `WriteJson` routes through `DataContext.PrepareJsonStream`/`QueryExecutor.WriteJson`, never `ForJson`/`json_agg` (`DataContext.cs:346–395`, `QueryExecutor.cs:1160`) |
| Providers without a native array source | Guard/capability record | provider ledger (`loopback/provider-counts.md`, n=2 current; `integration/provider-counts.md` is the pre-loopback n=1 run); object/slot tests remain mandatory on SQLite/MySQL/MariaDB where native arrays do not exist |
| Excessive depth / unsupported recursive shape | Guard + test | `JsonRowWriterFactory.EnsureDepth` (`:439`, `MaxShapeDepth=64`; const at `:65`); core `.ExcessiveDepth_ShouldThrowBeforeWriting` |
| Cancellation, reader failure, destination failure, disposal | Test | sqlite `.DestinationIsNotClosed`, `.Async_DestinationNotClosed`, `.PartialOutput_OnMidWriteFailure`, `.NestedPartialOutput_OnMidWriteFailure`, `.Cancellation_DuringWrite_ShouldAbort`, `.NestedCancellation_DuringWrite_ShouldAbort`, `.BufferRollover_ShouldStreamLargeResult`, `.TempTableQuery_ShouldThrow` |

## Deferred findings (from §7) with issue/trigger

| Finding | Issue | Trigger |
|---|---|---|
| Child-collection query projection | #172 | that issue supplies its query/materialization contract |
| New naming/options behaviour | #177 | its contract lands |
| Enum/converter expansion | #178 | its supported-type contract lands |
| Stryker automation | D151 | a working validated repo-local invocation lands |
| DB-side JSON and non-array collection support | — | separate issue / explicit scope approval |
| `Projection<T1>` | #160 | unavailable; not a prerequisite of #176 |

## P1 changed-path inventory

| Path | Priority | Nature |
|---|---|---|
| `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` | **P1** | JSON-only shape capture, ordinal binding, slot grouping, hidden conditional sentinel, leaf-alias disambiguation |
| `src/nextorm.core/Query/Json/JsonRowWriterFactory.cs` | **P1** | recursive writer: objects/arrays, per-scope names, presence, Base64 precedence, depth/rank guards |
| `src/nextorm.core/Query/Json/JsonShapePlan.cs` | **P1** | validated flat/captured plan, scalar whitelist (`Classify`), fail-closed converter/LOB guards |
| `src/nextorm.core/Query/Json/JsonShapeNode.cs` | **P1** | shape descriptor + presence/binding metadata (new) |
| `src/nextorm.core/Query/QueryCommand.cs` | **P1** | `JsonShapeMode`/`JsonShape` on the live clone only |
| `src/nextorm.core/DataContext/DataContext.cs` | **P1** | `PrepareJsonStream`: per-call clone + `storeInCache:false`; no sticky `Cache=false` |
| `src/nextorm.core/nextorm.core.csproj` | non-P1 | `InternalsVisibleTo("nextorm.benchmark")` — build visibility only, no runtime behaviour |
| `tests/nextorm.core.tests/JsonShapeWriterTests.cs` | P1 evidence | 34 direct recursive-writer tests |
| `tests/nextorm.sqlite.tests/JsonStreamingTests.cs` | P1 evidence | 53 end-to-end tests |
| dialect `JsonStreamingSqlLoweringTests` (postgres/sqlserver/mysql/clickhouse) | P1 evidence | lowered SQL/presence + SQL Server same-name regression |
| `tests/nextorm.integration.tests/*` | P1 evidence | shared + MariaDB/ClickHouse/PostgreSQL parity |
| `benchmarks/nextorm.benchmark/SqliteBenchmarkJsonStreamPhase2.cs` | P1 evidence | C08 focused benchmark (new) |

## Loop-back guard anchors (r=2, n=2)

The CHECK loop-back added prepare/writer guards; anchors re-resolved on the final tree (W-ids from
`loopback/dispositions.md`):

- `JsonShapePlan.ValidateShapeOrdinals` (`JsonShapePlan.cs:226`) / `EnsureOrdinal` (`:249`) —
  prepare-time descriptor↔selection ordinal validation; fails closed instead of a first-row
  `IndexOutOfRangeException` (W7/W8). Called from `Build` at `:165`.
- `JsonShapePlan.Build` explicit zero-column captured-shape reject (`JsonShapePlan.cs:139`, W3).
- `JsonRowWriterFactory.BuildElementWrite` CLR-`null` / boxed-`DBNull.Value` element handling
  (`:405`, W2); `ReadValue` typed `GetFieldValue<byte[]>` Base64 with plain-`IDataRecord` fallback
  (`:450`/`:473`).
- `QueryCommand.ResetPreparation` clears `JsonShapeMode`/`JsonShape` (`QueryCommand.cs:881–882`, W6).

## Evidence index

- Docs: `docfx.txt` (C09 exit 0).
- Perf: `perf/acceptance-postchange.txt` (C07), `perf/ratio-repro.txt` (C07 ratio reproducibility),
  `perf/c08-json-stream-phase2.txt` (C08), `perf/perf-notes.md`.
- Mutation: `mutation/manifest.md` + `mut1..mut6-*.patch` + per-mutant build/test/restore logs.
- Coverage: `coverage/c05-coverage-collect.txt`, `coverage/c06-reportgenerator.txt`, `coverage/coverage-notes.md`.
- Integration (D176.5, n=1 historical): `integration/c04-sweep.{log,xml}`, `integration/provider-counts.md` (pre-loopback, n=1), `integration/setup-recovery.log`.
- Integration (loop-back, n=2 current): `loopback/provider-counts.md` (current), `loopback/integration-full-sweep.{log,xml}`, `loopback/integration-writejson.txt`; the JSON subset is also mirrored at the task-named `integration/integration-writejson.txt` (identical copy, canonical current path).
- Build: `build-slnx.txt` (solution), `coverage/build-debug.txt`.
- Prior units: `../r1-n1/**`, `E176.13*`, `E176.14*`, `inner-loop/*`.
- Verification: `verification.md`.
