# D225 / T225 — §4 D3 independent semantic audit (EN/RU vs C1–C4)

Audit tree: DO start HEAD `ee3ee9fe96b81fa8b8bbcac124aee550ec98ac3b`, branch `1.0.9-rc3`, baseline B
`aea0a9ba644a8be34f5e9bf39e4daf4a33861f17`. Method: read the 16 footprint files at HEAD, compare with
C1–C4, and check each claim against the delivered implementation and the executable oracle. C# symbols
resolved with the `roslyn` tool; the two oracle test classes were executed (see `oracle-*.log`).

## Executable oracle (fresh run, HEAD, --no-build after `dotnet build tests/nextorm.core.tests -c Debug`)

| Selector | Command | Exit | Selected | Result |
|---|---|---|---|---|
| `FullyQualifiedName~JoinIntoCompositeJunctionRejectionTests` | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~JoinIntoCompositeJunctionRejectionTests` | 0 | 4 | `oracle-rejection.log` (4 passed / 0 failed / 0 skipped) |
| `FullyQualifiedName~JoinIntoCompositeRemainderTests` | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~JoinIntoCompositeRemainderTests` | 0 | 17 | `oracle-remainder.log` (17 passed / 0 failed / 0 skipped) |

`JoinIntoCompositeJunctionRejectionTests.cs:95-116` is named "…ShouldPrepareWithoutOpeningConnectionOrExecutingSql"
and now asserts `act.Should().NotThrow("the composite-junction M:N link source is supported again (#223)")`
(line 113). So the class that the plan calls the "rejection oracle" now proves **support**.

## C1 — scalar M:N `JoinInto` standalone AND under `EagerLoadMode.SingleQuery`

- Implementation: `src/nextorm.core/Builders/Joins/JunctionLinkSourceFactory.cs:205-206` keeps the scalar
  two-member `JoinIntoLink` carrier for a 1/1 junction; `EntityBuilder.cs:783-788` routes `ManyToMany` through
  `JoinIntoManyToManySpec<TEntity,TChild>`.
- Docs: the statements were landed by T223's D04 and are present at HEAD (`relationships.md:194` ("A junction whose parent and/or child foreign key spans several
  **components** is fully supported … The single-query dispatcher handles such a junction too"),
  `limitations.md:19` ("`JoinInto` supports a mapped relationship or junction whose key spans several
  components")).
- Verdict: **met at HEAD** (scalar + composite junction, standalone + SingleQuery documented). The EN/RU
  `eager-loading.md` and EN/RU `02-joins.md` still lack the M:N note (H01/H05/H07/H11) — a support-oriented
  gap, not a rejection claim.

## C2 — composite relationship keys documented for 1:N, 1:1 and the scalar M:N junction

- At HEAD, `relationships.md:194,292`, `limitations.md:19` (EN+RU) state full-component support.
- Stale counter-examples still present and contradicting C2: `docs/guide/29-implicit-navigation.md:257`
  (`* **Composite relationship keys** — only single-column foreign/principal/junction keys are supported.`)
  and its RU mirror `docs/ru/guide/29-implicit-navigation.md:263`; `docs/specs/comparison/linq2db-comparison.md:109`;
  `docs/specs/comparison/capability-matrix.md:110` tail; `docs/specs/comparison/linq2db-backlog-gap-analysis.md:54,345,404,504`.
- Verdict: **partially met** — the required removal of the stale "single-column / rejected" text is genuine
  outstanding work (H06/H12/H13–H18), but its correct replacement is **support**, not the frozen rejection text.

## C3 — SQL-generation rejection recorded exactly as `NotSupportedException("composite-junction M:N is not supported; see #223")`

- `git grep -n "composite-junction M:N is not supported" -- 'src/**'` → **no match** at HEAD. The only occurrence
  in the tree is a historical comment in a test (`JoinIntoCompositeCarrierSpikeTests.cs:185`).
- `JunctionLinkSourceFactory.cs:205-206` no longer throws for a multi-component junction FK; it builds the
  composite carrier. `CompositeJunctionLinkFactory.cs` exists (added by `d76c5ae3`).
- The oracle test that the plan designates as the C3 oracle now asserts **NotThrow** (line 113), exit 0.
- Verdict: **NOT MET and not implementable as written** — C3 describes behaviour that #223 removed. Writing the
  C3 text would introduce a false statement contradicting both the implementation and T223's already-landed,
  accurate docs (`limitations.md:19`, `relationships.md:194,292`). C3 is obsolete, not missing.

## C4 — in-memory provider still stitches a composite junction

- Implemented and unchanged: the in-memory path (`EntityBuilder.cs` JoinInto in-memory execution) is not gated by
  `JunctionLinkSourceFactory`. `JoinIntoCompositeRemainderTests` (17/17, exit 0) exercises the core/in-memory
  composite stitching.
- Docs: `limitations.md:19` (EN/RU) at HEAD still states the in-memory stitching; `relationships.md:194` states
  the junction is fully supported on the explicit path.
- Verdict: **met at HEAD**; a replacement for `29-implicit-navigation.md:257/263` must keep the in-memory
  stitching statement (must not widen any SQL prohibition to in-memory).

## EN/RU pair comparison

| Concern | EN | RU | Parity at HEAD |
|---|---|---|---|
| composite-junction support (limitations/relationships) | present (T223) | present (T223) | **parity** |
| stale "only single-column keys" | `guide/29-implicit-navigation.md:257` | `guide/29-implicit-navigation.md:263` | **stale in both, parity of staleness** |
| missing M:N note | `advanced/eager-loading.md`, `guide/02-joins.md` | mirrors | **missing in both, parity** |
| specs stale remainder | `comparison/{capability-matrix,linq2db-comparison,linq2db-backlog-gap-analysis}.md` | (specs are single-language, no RU mirrors in the footprint) | n/a |

## Audit conclusion

C1, C2 (partial), C4 are consistent with the delivered behaviour. **C3 is factually obsolete**: the composite-
junction M:N SQL link carrier was implemented by `d76c5ae3` (#223, T223) after PLAN r=2 froze. The decisive
oracle E01 ("proves the original AC07 meaning in the landed EN/RU docs against the implementation") is
therefore unsatisfiable: the original AC07 meaning (rejection) contradicts the implementation (support). This
is the DO→PLAN candidate in `DO_TO_PLAN.md`.
