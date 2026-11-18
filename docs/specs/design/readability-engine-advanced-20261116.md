# Public documentation readability pass — engine, comparisons, and advanced

## Scope and method

This record covers the 32 English and Russian pages under `docs/engine/`,
`docs/comparisons/`, and `docs/advanced/`. Each page was read in full. Editorial
changes prioritize the reader's mental model, practical decision criteria, and
scannable reference material while retaining facts, constraints, examples,
public links, and heading anchors. Comparison benchmarks keep their source,
date, methodology, and limitations. English and Russian counterparts remain
aligned; Russian prose avoids calques where a precise natural phrasing exists.

API facts were treated as facts about the current working tree, not released
NuGet packages or published versions. No package was installed or restored, and
no product behavior or workflow was added.

No full-site score is claimed. The checks below cover only this bounded set of
targets; no DocFX build, product build, or test run is part of this
documentation-only pass.

## Per-page coverage

### Engine

- `docs/engine/index.md` — clarified the query-to-result mental model, the role
  of terminal operations, and how the four engine pages fit together.
- `docs/engine/01-query-pipeline.md` — made the route from fluent query intent
  through `QueryCommand`, preparation, plan key, and dialect rendering easier
  to follow; kept SQL generation distinct from execution.
- `docs/engine/02-execution-and-rows.md` — clarified command/reader ownership,
  row materialization, nullable aggregate behavior, and the available result
  forms.
- `docs/engine/03-plan-cache-and-reuse.md` — separated plan reuse, per-connection
  state, cache controls, streaming lifetime, and collection-shape behavior.
- `docs/ru/engine/index.md` — mirrored the query-to-result overview and made the
  role of terminal calls and page navigation explicit in natural Russian.
- `docs/ru/engine/01-query-pipeline.md` — clarified the builder-to-SQL sequence,
  preparation, plan key, and dialect responsibilities using consistent Russian
  terminology.
- `docs/ru/engine/02-execution-and-rows.md` — made resource ownership,
  materialization, nullable aggregates, and result choices easier to scan.
- `docs/ru/engine/03-plan-cache-and-reuse.md` — untangled cache reuse, connection
  state, streaming lifetime, cache controls, and collection-shape rules.

### Comparisons

- `docs/comparisons/index.md` — clarified how to navigate the comparison
  material and distinguish capability comparisons from measured results.
- `docs/comparisons/capabilities.md` — made comparison dimensions and
  provider-specific qualifications easier to scan without treating API
  availability as identical behavior.
- `docs/comparisons/provider-features.md` — clarified the provider-by-provider
  organization and kept support boundaries with the corresponding features.
- `docs/comparisons/benchmarks.md` — foregrounded the benchmark setup,
  comparison categories, and limits on generalizing the SQLite results.
- `docs/comparisons/feature-benchmarks.md` — explained scenario groupings and
  query-reuse differences so readers can interpret each table on its own terms.
- `docs/ru/comparisons/index.md` — mirrored the comparison-page navigation and
  clarified the distinction between capability and benchmark material.
- `docs/ru/comparisons/capabilities.md` — refined Russian descriptions of
  comparison dimensions and provider qualifications.
- `docs/ru/comparisons/provider-features.md` — clarified provider-specific
  support boundaries and terminology.
- `docs/ru/comparisons/benchmarks.md` — improved the explanation of setup,
  reuse categories, raw-reader scenarios, and benchmark caveats; retained
  dates, methodology, and reported values.
- `docs/ru/comparisons/feature-benchmarks.md` — clarified scenario groups,
  `DbDataReader` comparisons, and direct-writer versus
  materialize-then-serialize differences; retained the reported results.

### Advanced

- `docs/advanced/api-reference.md` — improved navigation from the curated type
  index into focused entity-builder, `MERGE`, streaming, and SQL-function
  details. This file also contains pre-existing uncommitted API-reference
  material, which was retained rather than attributed wholesale to this pass.
- `docs/advanced/extensions-package.md` — clarified the package's role and
  separated its usage guidance from the core feature surface.
- `docs/advanced/extensibility.md` — made extension points easier to compare
  and clarified converter and interceptor behavior.
- `docs/advanced/integration-efcore.md` — clarified the EF Core bridge and
  retained the `CreateNextOrmContext()` live-server integration-test detail.
- `docs/advanced/select-where-extrema-native.md` — made native-renderer
  selection, portable fallback, and eligibility constraints easier to follow.
- `docs/advanced/limitations.md` — grouped current restrictions so readers can
  identify the affected operation or provider.
- `docs/advanced/query-filters.md` — clarified filter scope and write-target
  isolation, including the role of `IgnoreFilters`.
- `docs/ru/advanced/api-reference.md` — mirrored the focused API-reference
  navigation and improved Russian descriptions. Pre-existing uncommitted
  API-reference material was retained; one malformed `SelectWhereMin` xref was
  corrected to match the English generic `Func` signature.
- `docs/ru/advanced/extensions-package.md` — clarified the package's role and
  distinguished it from core functionality in natural Russian.
- `docs/ru/advanced/extensibility.md` — refined terminology and clarified
  converter and interceptor behavior.
- `docs/ru/advanced/integration-efcore.md` — clarified the bridge and retained
  the `CreateNextOrmContext()` live-server integration-test detail.
- `docs/ru/advanced/select-where-extrema-native.md` — clarified native
  rendering, fallback, and eligibility constraints in Russian.
- `docs/ru/advanced/limitations.md` — organized current restrictions for
  quicker lookup.
- `docs/ru/advanced/query-filters.md` — clarified filter scope and write-target
  isolation, including `IgnoreFilters`.

## Concrete edits

- The engine explanation now describes cache reuse in terms of query structure,
  rather than the identity of a lambda instance. The nullable-aggregate
  explanation was split into shorter steps.
- Benchmark introductions now distinguish fastest-per-library end-to-end
  results from comparisons that hold query-reuse strategy more nearly
  constant. The Russian feature-benchmark prose explains the shared
  non-retaining reader sink and states that “без материализации” does not mean
  zero allocations.
- The Russian benchmark prose uses “раза” for several multiplicative
  comparisons previously written with `×`; the underlying ratios and table
  values are unchanged.
- API-reference details are divided into navigable sections rather than
  carrying several unrelated behaviors in one inventory entry. The Russian
  `SelectWhereMin` reference now has the same generic `Func` parameter shape as
  its English counterpart.
- The EF Core integration descriptions explicitly name the bridge exercised by
  live-server tests; no new workflow or product behavior was introduced.

## Checks and limits

- Coverage: 32/32 target pages (8 engine, 10 comparisons, 14 advanced), in
  English and Russian. All original headings remain; added API-reference
  headings provide navigation within the existing pages.
- A bounded static scan checked 703 relative Markdown links across the 32
  targets: all file targets and local heading/explicit anchors resolved. This
  does not validate external URLs or DocFX xrefs. The identified malformed
  Russian `SelectWhereMin` xref was corrected manually against the English
  counterpart.
- All 184 fenced code blocks are unchanged. Benchmark dates, setup, caveats,
  and reported table values were retained.
- `git diff --check` passed (exit 0). All 32 target pages and this report use
  CRLF line endings.
- No DocFX build, product build, test, package install, or package restore was
  run. No released NuGet package or published version was used as a validation
  target. These checks do not establish a full-site readability score.
