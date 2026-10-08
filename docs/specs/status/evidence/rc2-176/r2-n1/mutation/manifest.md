# D176 / E176.10 — manual targeted mutation manifest (refreshed r=2, n=2 final tree)

Six critical decisions, mutated one at a time against the **final loop-back tree** (r=2, n=2). Each
mutant was applied to the uncommitted D176 working tree, the affected test project rebuilt, the
filtered suite run, the diagnosed failure recorded, then the file restored from a byte-exact backup
and the same unmutated suite rerun to exit 0. No working-repo Stryker invocation was available (D151
is not a prerequisite).

**No mutation target changed** from the D176.6 run: every decision, file and anchor (ordinal binding,
scoped duplicate-name rejection, constructed-object presence, joined-slot presence, `byte[]` Base64
precedence, unsupported-kind rejection) was re-resolved against the final tree and is intact. Only the
executed suite sizes grew (loop-back added tests: core `JsonShapeWriterTests` 34 → 40, sqlite
`JsonStreamingTests` 53 → 63). Per-mutant patches were regenerated against the final unmutated files.

Common commands (C02 / C03a subsets):

```text
core:  dotnet build tests/nextorm.core.tests -c Debug
       dotnet test  tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~JsonShapeWriterTests"
sqlite: dotnet build tests/nextorm.sqlite.tests -c Debug
        dotnet test  tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~JsonStreamingTests"
```

Unmutated baselines (both exit 0): core `JsonShapeWriterTests` 40 passed / 0 failed / 0 skipped;
sqlite `JsonStreamingTests` 63 passed / 0 failed / 0 skipped. Every mutant build exit 0, every mutant
test exit 2 (killed), every restored suite exit 0.

| # | Critical decision | File (mutation) | Patch | Affected suite | Mutant result | Diagnosed failures | Restored |
|---|---|---|---|---|---|---|---|
| 1 | Ordinal binding | `JsonRowWriterFactory.BuildScalarMember` — read/`IsDBNull` ordinal forced to 0 instead of `binding.Ordinal` | `mut1-ordinal-binding.patch` | C02 core writer | **killed** — exit 2 | 9 failed / 40, e.g. `MemberOrderDifferentFromOrdinal_ShouldBindDeclaredOrdinals`, `NestedObject_ShouldWriteResolvedMemberNames`, `NestedObject_MultipleLevels_ShouldPreserveBoundaries`, `PresentJoinedSlot_ShouldWriteObject`, `AnyColumnNotNull_OnePresent_ShouldWriteObject`, `NamingPolicy_ShouldApplyPerObjectScope`, `SameNameInDifferentScopes_ShouldBeAccepted`, `RepeatedSiblingNames_InSeparateScopes_ShouldBeAccepted`, `ConditionalConstruction_PresentSentinelWithAllNullMembers_ShouldWriteObject` (wrong ordinal → value/type mismatch) | exit 0 |
| 2 | Scoped duplicate-name rejection | `JsonRowWriterFactory.BuildMemberWriters` — dropped the `if (!seen.Add(name)) throw` guard | `mut2-scoped-duplicate-rejection.patch` | C02 core writer | **killed** — exit 2 | 1 failed / 40: `DuplicateNameWithinOneScope_ShouldThrowBeforeWriting` | exit 0 |
| 3 | Constructed-object presence | `QueryCommand.QueryPreparer.BuildJsonObjectNode` (NewExpression overload) — `Always` replaced by `AnyColumnNotNull` over scalar member ordinals | `mut3-constructed-object-presence.patch` | C03a sqlite | **killed** — exit 2 | 3 failed / 63: `NestedObject_ShouldMatchJsonSerializer`, `NestedObject_MultipleLevels_ShouldMatchSerializer`, `NestedObject_AllNullChild_ShouldStayObject` (all-null child became `null`) | exit 0 |
| 4 | Joined-slot presence | `QueryCommand.QueryPreparer.BuildJsonProjectionShape` — slot `AnyColumnNotNull` replaced by `Always` | `mut4-joined-slot-presence.patch` | C03a sqlite | **killed** — exit 2 | 1 failed / 63: `BareProjection_LeftJoin_ShouldMatchItem1Item2` (absent `Item2` became an all-null object) | exit 0 |
| 5 | `byte[]` Base64 precedence | `JsonRowWriterFactory.BuildElementWrite` — dropped the `elementValueType != typeof(byte[])` guard | `mut5-bytearray-base64-precedence.patch` | C02 core writer | **killed** — exit 2 | 1 failed / 40: `JaggedByteArray_ShouldBeBase64Recursively` (element emitted as numbers, not base64) | exit 0 |
| 6 | Unsupported-kind rejection | `JsonShapePlan.Classify` — replaced the fallback `throw NotSupportedException` with a permissive `(valueType, String)` | `mut6-unsupported-kind-rejection.patch` | C02 core writer | **killed** — exit 2 | 4 failed / 40: `UnsupportedElementType_ShouldThrowBeforeWriting`, `CollectionElementType_ShouldThrowBeforeWriting`, `UnsupportedScalarMemberType_ShouldThrowBeforeWriting`, `DictionaryAndEnumerableArrayElements_ShouldThrowBeforeWriting` | exit 0 |

**Result: 6/6 mutants killed, 0 survivors.** No equivalence demonstration was required.
Per-mutant logs: `mutN-build.txt`, `mutN-test.txt`, `mutN-restore-build.txt`, `mutN-restore-test.txt`;
patches: `mutN-*.patch` (CR-stripped unified diffs against the final pre-mutation file). The working
tree was restored byte-exact (`cmp` clean) and `rg "Mutation:" src tests benchmarks` finds nothing.
