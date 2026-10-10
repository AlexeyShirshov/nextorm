# E177-11 — EN/RU docs verification: DB-side JSON fast-path (SQL Server `FOR JSON`)

- Unit: `R177-D177-R6` (cycle N=1, plan revision r=2, evidence contract rv=2, iteration n=1)
- Product baseline: `2ac20818bc5bdb429406f99758b1db50a470561e` (`#177 D177 native JSON fast-path`)
- Verification revision: HEAD `5dcc3f09b7eba1d2cf5491605768a822a16a8c61`
- Scope: verify only; no `src/**` change. Public docs edited only on a demonstrated discrepancy.

## Pages read (EN + RU, touched by product `2ac20818`)

| EN | RU |
|---|---|
| `docs/guide/28-streaming-data.md` (`### SQL Server native fast-path`, :187-226) | `docs/ru/guide/28-streaming-data.md` (:189-229) |
| `docs/guide/14-json.md` (:40-52, :102-108) | `docs/ru/guide/14-json.md` (:40-52, :102-108) |
| `docs/advanced/api-reference.md` (`JsonStreamMode`/`JsonStreamOptions` row, :89) | `docs/ru/advanced/api-reference.md` (:89) |

## Claims checked against in-tree code / evidence

| Claim (docs) | Verified against | Verdict |
|---|---|---|
| Native route = trailing `FOR JSON PATH`; single document column copied in bounded chunks | `DataContext.PrepareJsonStream` sets `ForJsonClause(Path)` (`src/nextorm.core/DataContext/DataContext.cs:402`); `SqlBuilder.cs:552-555`; `SqlServerDialect.cs:304/307-319`; `JsonNativeStream.WriteDocument` (`src/nextorm.core/Query/Json/JsonNativeStream.cs:131-174`) | accurate |
| Eligibility: flat object, no nested/`Projection`/scalar, simple identifier aliases, `string`/`bool`/`short`/`int`/`long` incl. nullable, `Array`/no root/indent/naming policy, `IgnoreNull` maps to `INCLUDE_NULL_VALUES` | `JsonNativeStream.IsEligible` :39-99; mapping `IncludeNullValues: !options.IgnoreNull` at `DataContext.cs:400-402`; `IsSimpleAlias` :105-122 | accurate |
| Everything else (recursive shapes, enums, provider conversions, special aliases, root/indent, `NdJson`, `*OrDefault`, other providers) stays managed, decided before execution; no retry after a native failure | `DataContext.cs:393-417` (managed clone selected before execution); `JsonNativeStream.cs:33-34`; `:64-65` (`DefaultOnNull`); `:74-75` (enum); `:83-84` (`ProviderType`) | accurate |
| Output logically equivalent, not byte-identical; SQL Server escaping differs (`<`/`>`/`&`, non-ASCII left raw) | integration `Native_UnicodeAndHtml_ShouldBeRawAndLogicallyEquivalent` (`tests/nextorm.integration.tests/SqlServerNativeJsonStreamTests.cs:215-232`): raw `é`/`中`/`😀`, no `\u003C`; `ShouldBeRaw...` | accurate |
| Large document split across reader rows, concatenated; surrogate-safe across chunk boundary | `JsonNativeStream.cs:141-160`; `Native_SurrogateAcrossReaderRow_ShouldRoundTrip` (:315) | accurate |
| Empty result writes `[]` in `Array` mode | `JsonNativeStream.cs:164-165`; `Native_EmptyResult_ShouldBeEmptyArray` (:240) | accurate |
| Caller owns destination; never `Stream.Flush`/dispose; cancellation/error fail-stop with partial output retained; pre-output refusals untouched | `QueryExecutor.cs:1249-1279`; core `WriteDocument*` fault/sink tests (`JsonNativeStreamTests.cs:387-505`); integration `Native_DestinationIsCallerOwned...` (:281), `NativeAsync_MidStreamCancellation...` (:368), `NativeSync_MidStreamWriteFailure...` (:388) | accurate |
| Scalar `ForJson`/`ForJsonAsync` unchanged, one materialized `string`, `null` on empty | `docs/guide/14-json.md:102-108`; scalar tests retained in `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:853/866/889` | accurate |

## Discrepancy check

- **No demonstrated discrepancy; no public-doc edit made.** Every statement above is consistent with the
  in-tree product and its tests.
- Internal nuance recorded (not a contradiction): the detailed eligibility list (`docs/guide/28-streaming-data.md:195-207`,
  RU :197-210) is phrased as a necessary condition ("eligible ... only when **all** of these hold") and does not
  spell out the internal direct-pass-through guard `if (!column.IsDirectPassThrough) return false;`
  (`src/nextorm.core/Query/Json/JsonNativeStream.cs:80`; `JsonShapePlan.IsDirectProjection`, `JsonShapePlan.cs:348-368`).
  That guard keeps raw named-table accessors (`RawAccessorProjection_ShouldStayManaged`,
  `tests/nextorm.sqlserver.tests/SqlServerNativeJsonSqlTests.cs:71`) and computed non-direct projections on the
  documented managed fallback; it adds no eligibility promise the docs contradict, so no edit is warranted.

## Public → internal-spec link check

- `rg -n "docs/specs|\.\./specs|specs/"` over the six touched pages: no matches, rc=1.
- No new public→`docs/specs/**` link was introduced by this unit.

## Related route confirmation

- `dotnet docfx docs/docfx.json` exit 0, 2 warnings / 0 errors — `logs/docfx.log` (AC5 / E177-11).
