## #26 Ordinary struct projections: design and evidence

**Issue:** [#26](https://github.com/AlexeyShirshov/nextorm/issues/26) · open; milestone `1.0.10`  
**Status:** written spec approved by the user. The next stage is a separate implementation plan; that plan is not prepared or approved. Implementation is not authorized.

## Intent

Support ordinary projections into user-defined structs while eliminating the per-row heap allocation of a result object. The target is typed result storage and normal streaming, not zero total allocations. This is not a timing or speedup guarantee.

The scope is numeric readonly structs/record structs materialized through public constructors, and mutable structs with an implicit parameterless constructor and public settable properties materialized through member-init. Existing public query APIs remain unchanged. Apply the same successful shared struct materialization to existing row-result terminals rather than patching individual terminals.

Covered terminals are `ToList`, `ToArray`, `ToHashSet`, `ToDictionary` (sync and async); `ToEnumerable` and `ToAsyncEnumerable`; the two existing direct enumerator factories; and `First`, `FirstOrDefault`, `Last`, `LastOrDefault`, `Single`, `SingleOrDefault` (sync and async). Preserve current empty/default behavior, `Single` multiple-result behavior, dictionary key behavior, cancellation, and disposal. `OrDefault` on empty returns `default(TStruct)`. Populated-result assertions must verify actual values in both struct shapes, not merely non-null.

Aggregates remain unchanged. The scope is the SQL mapper only: do not claim in-memory support was verified, and do not regress in-memory, class, or scalar projections.

## Explicit exclusions

No new `ToAsyncEnumerableStruct`/ref API, custom public enumerators, or ref-mapper redesign. Do not promise universal field support, private-constructor support, or arbitrary immutable/unsupported shapes. No general boxing or converter redesign, no other-provider feature work, and no timing SLA. Do not broaden unsupported class projection shapes or blanket-default explicit projections.

## Current evidence and diagnosis

The current independent SQLite matrix has 100 cases: 50 constructor-based cases pass and 50 mutable member-init cases fail. The failing cases report `QueryPreparationException: Cannot get ctor` at `RowMaterializerBuilder.BuildCore` (line 312). The resolver selects the longest public constructor and returns null for implicit-default structs. This is a shared successful materialization-path defect, not a reason to add per-terminal patches. No full integration suite has been run for this draft.

The technical design direction is to correct shared materializer expression construction so implicit-default struct member-init can use type-based construction (for example, `Expression.New(resultType)`), while preserving constructor semantics and existing class behavior. Separate member-init expression construction from reflected constructor metadata as needed. This is a required design outcome, not a mandated implementation decomposition; the plan chooses exact method boundaries. No mandatory mapper-ref rewrite is implied.

## Allocation evidence

The diagnostic experiment compares benchmark-local class/value/ref-readonly wrappers with matched `Fill(ref T)` payloads of 32/128/256 bytes, 1000 rows, warmed cache, and a data-size series. It is a syntax/allocation proof only, not production NextORM integration. Environment: .NET SDK 11.0.100 / runtime 11.0.0, BenchmarkDotNet 0.15.8, InProcessEmit, 3 warmups and 5 iterations, Windows 11, AMD Ryzen 7 9800X3D.

| Payload | Class mean / allocation | Value mean / allocation | Ref-readonly mean / allocation | Error: class / value / ref |
|---|---:|---:|---:|---:|
| 32 B | 372.7 μs / 48.06 KB | 366.8 μs / 1.21 KB | 367.7 μs / 1.21 KB | 5.61 / 2.75 / 11.52 μs |
| 128 B | 1239.9 μs / 142.39 KB | 1227.9 μs / 1.89 KB | 1219.3 μs / 1.89 KB | 15.01 / 3.17 / 13.69 μs |
| 256 B | 2406.8 μs / 268.22 KB | 2369.5 μs / 2.84 KB | 2364.8 μs / 2.83 KB | 308.16 / 15.87 / 10.93 μs |

The class-vs-struct `ToList` comparison observed a 28 B/row delta for a one-int projection (24 B result object plus 4 B collection storage). Streams observed 24 B/row delta; struct allocation remained constant between 1,000 and 10,000 rows. These are observations, not universal byte guarantees. Collections retain per-element value storage and backing-array allocations; driver/read-field/query setup and other infrastructure may allocate. Accept evidence by allocation path and scaling trend, not brittle exact-byte assertions. No speedup is promised: value-vs-ref mean differences were under 1%, with no material gain demonstrated for a special API; the short in-process experiment is limited and does not establish that copies are universally free.

The report is `BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.Issue26StreamingMaterializerBenchmark-report-github.md`; experiment commit `7716ff962` and source `benchmarks/nextorm.benchmark/Focused/Issue26StreamingMaterializerBenchmark.cs` are on branch `1.0.10-a`. Related SQLite source: `tests/nextorm.integration.tests/Sqlite/StructProjectionLiveTests.cs`; allocation benchmark: `benchmarks/nextorm.benchmark/Focused/Issue26StructProjectionBenchmark.cs`.

## Acceptance criteria for the subsequent plan

1. All 100 independent SQLite matrix cases pass after the fix, with no skips. Each case independently exercises terminal × shape × scenario; populated projections assert numeric property values. Retain scalar and class-constructor regression coverage.
2. Add focused cases for implicit-default mutable member-init and explicit readonly constructor projection; protect constructor semantics and existing class behavior.
3. Existing terminal semantics listed above remain intact, including empty/default, multiple-result, dictionary-key, cancellation, and disposal behavior.
4. A controlled warmed-cache allocation series demonstrates no per-row result-object allocation for struct projections on both collection and streaming paths. Collection element storage and unrelated allocations are explicitly allowed; no exact-byte CI threshold or speed guarantee.
5. Follow ordinary repository conventions and warnings-as-errors. The plan selects exact tests and broader impact coverage. Run affected unit tests and targeted SQLite coverage, then the canonical capped integration suite at the DO→CHECK boundary; do not call provider-skipped runs green. The diagnostic benchmark is evidence, not a hard CI threshold.

## Risks and review gates

Value copies remain; this draft authorizes neither a custom ref API nor claims about arbitrary projection shapes. The short benchmark has limited power to establish timing differences. Keep the implementation narrowly scoped to shared SQL materialization and preserve unsupported shapes' existing behavior.

There are no open product questions in the design. Method decomposition, test-fixture organization, and implementation steps belong to the subsequent plan. The written spec is approved; the next gate is preparation and separate approval of an implementation plan. Implementation and tests are not authorized until that gate passes.
