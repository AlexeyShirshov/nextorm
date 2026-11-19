# Readability review: advanced query and relationship guides

**Date:** 2026-11-16

## Scope and method

This review covers English and Russian guide pages 24–36 and the five
relationship pages. The edit pass focuses on page-specific clarity, sentence
flow, and reader-oriented wording. It keeps every original heading in order and
preserves technical claims, provider qualifications, and link targets. It adds
83 subheadings across 28 pages, moves the existing eager-loading example to the
introduction in both languages, and edits eight existing fenced blocks without
adding or removing code blocks. The existing guide 30 CSV edits remain part of
the documentation.

## Per-page coverage

| Topic | English page | Russian page | Editorial focus |
| --- | --- | --- | --- |
| Duration columns | `docs/guide/24-duration-columns.md` | `docs/ru/guide/24-duration-columns.md` | Explain duration-column use and behavior clearly. |
| SQL batch | `docs/guide/25-sql-batch.md` | `docs/ru/guide/25-sql-batch.md` | Clarify when and how to use SQL batches. |
| Optimistic concurrency | `docs/guide/26-optimistic-concurrency.md` | `docs/ru/guide/26-optimistic-concurrency.md` | Make the concurrency workflow and outcomes easier to follow. |
| Range columns | `docs/guide/27-range-columns.md` | `docs/ru/guide/27-range-columns.md` | Clarify range-column concepts and provider scope. |
| Large objects | `docs/guide/28-large-objects.md` | `docs/ru/guide/28-large-objects.md` | Improve the flow of large-object guidance. |
| Dynamic columns | `docs/guide/29-dynamic-columns.md` | `docs/ru/guide/29-dynamic-columns.md` | Clarify dynamic-column mapping and usage. |
| Streaming data | `docs/guide/30-streaming-data.md` | `docs/ru/guide/30-streaming-data.md` | Improve streaming guidance while retaining the existing CSV examples. |
| Provider-specific features | `docs/guide/31-provider-specific-features.md` | `docs/ru/guide/31-provider-specific-features.md` | Make provider qualifications and feature selection easier to scan. |
| Inheritance and derived entities | `docs/guide/32-inheritance-and-derived-entities.md` | `docs/ru/guide/32-inheritance-and-derived-entities.md` | Clarify inheritance concepts and derived-entity usage. |
| Runtime mapping variant | `docs/guide/33-runtime-mapping-variant.md` | `docs/ru/guide/33-runtime-mapping-variant.md` | Clarify runtime mapping choices and their use. |
| Routing data to topologies | `docs/guide/34-routing-data-to-topologies.md` | `docs/ru/guide/34-routing-data-to-topologies.md` | Improve the explanation of topology routing. |
| Sharding and horizontal partitioning | `docs/guide/35-sharding-and-horizontal-partitioning.md` | `docs/ru/guide/35-sharding-and-horizontal-partitioning.md` | Clarify sharding concepts and distinctions. |
| AI and vector search | `docs/guide/36-ai-and-vector-search.md` | `docs/ru/guide/36-ai-and-vector-search.md` | Improve explanation of vector-search use and scope. |
| Eager loading | `docs/relationships/eager-loading.md` | `docs/ru/relationships/eager-loading.md` | Clarify eager-loading behavior and choices. |
| Relationships | `docs/relationships/relationships.md` | `docs/ru/relationships/relationships.md` | Make relationship concepts and setup easier to understand. |
| Implicit navigation | `docs/relationships/implicit-navigation.md` | `docs/ru/relationships/implicit-navigation.md` | Clarify implicit-navigation behavior and constraints. |
| Collection projection | `docs/relationships/collection-projection.md` | `docs/ru/relationships/collection-projection.md` | Explain collection-projection behavior more directly. |
| Eager loading with per-parent paging | `docs/relationships/eager-loading-per-parent-paging.md` | `docs/ru/relationships/eager-loading-per-parent-paging.md` | Clarify per-parent paging behavior and provider scope. |

## Before and after

Examples below compare the wording at `HEAD` (`7612b8f580296a56992a481921a0ce8c8747791b`)
with the revised wording. The edits clarify presentation; they do not add
product behavior.

| Page | Before | After |
| --- | --- | --- |
| Eager loading, EN (`docs/relationships/eager-loading.md`) | “`LoadWith` fills a parent-side collection and stitches the children onto the already materialized parents in memory.” The same opening paragraph also named all strategies and nested graphs before an example. | “Use `LoadWith` when you want each returned order to include its items.” The existing `Order`/`OrderItem` example now comes first, followed by its default split explanation and the strategy table. |
| Eager loading, RU (`docs/ru/relationships/eager-loading.md`) | “`LoadWith` заполняет коллекцию … и сшивает дочерние записи с уже материализованными родителями”; the paragraph also used “чанк” and “round trip.” | “Используйте `LoadWith`, когда нужны заказы вместе с их позициями.” The same parent/child example now precedes the strategy table; database round trips are described as “за одно обращение к базе данных,” and detailed `SingleQuery` limits are grouped under a subsection. |
| Nested split loading, EN/RU (`docs/relationships/eager-loading.md`, `docs/ru/relationships/eager-loading.md`) | “One statement per level” could imply one child query per level even though keys are chunked. | The text states that the parent query runs once and each child level runs one query per key chunk; the Russian SQL comment calls the parent statement a “запрос.” |
| SQL batches, RU (`docs/ru/guide/25-sql-batch.md`) | The body used “round trip” for a database request and “утверждение” for a SQL statement. | The body now uses “обращение к базе данных” for a database round trip and “SQL-оператор” for an individual statement, keeping those counts distinct. |
| Sharding, EN/RU (`docs/guide/35-sharding-and-horizontal-partitioning.md`, `docs/ru/guide/35-sharding-and-horizontal-partitioning.md`) | The opening said only “keep bucket identities independent of the current server set” / “сохранять идентичность бакета независимо от текущего набора серверов.” | The opening now says a bucket is the logical place for a table or schema and a shard is the physical server. A new section explains that the table/schema name stays stable when server ownership changes, then distinguishes application and nextorm responsibilities and gives a setup sequence. |
| Sharding, RU (`docs/ru/guide/35-sharding-and-horizontal-partitioning.md`) | The prose called the per-bucket SQL statement a “statement” and indented the multi-row insert sentence inconsistently. | It identifies one SQL operator per bucket, states that the multi-row insert uses one database request, and removes the stray indentation. |
| Dynamic columns, RU (`docs/ru/guide/29-dynamic-columns.md`) | `### Настройка хранилища` and `### Чтение дополнительных колонок` were inside C# code fences. | Both headings now sit outside the fences, immediately before their code examples. |
| Streaming, EN/RU (`docs/guide/30-streaming-data.md`, `docs/ru/guide/30-streaming-data.md`) | The format table used `Mode = NdJson`. | It now shows `new JsonStreamOptions { Mode = JsonStreamMode.NdJson }`, matching the options object already used in the page’s example. Existing CSV examples were retained. |
| Streaming CSV, EN/RU (`docs/guide/30-streaming-data.md`, `docs/ru/guide/30-streaming-data.md`) | The whole-entity example passed captured `from` as an extra positional argument. | The example now uses the captured value and passes only the cancellation token; its comment describes the value as captured rather than positional. |
| Streaming, RU (`docs/ru/guide/30-streaming-data.md`) | One sentence used the informal singular imperative “прибегай” among formal plural instructions. | It now uses the consistent formal plural “прибегайте.” |

## Verification limits

- The structural baseline is `HEAD`
  (`7612b8f580296a56992a481921a0ce8c8747791b`); the revised side is the current
  working tree. No claim is made from a released NuGet package or published
  version, and no package install or restore was repeated for this pass.
- A prior focused source inspection recorded here checked vector equality
  against `src/nextorm.core/Query/VectorSearch/Vector.cs`; this final editorial
  verification did not repeat that inspection. It was not comprehensive
  product, API, or provider validation.
- Static checks against that `HEAD`: all 36 scoped pages retain the original
  464 headings in order; 83 subheadings were added across 28 pages and none
  were removed. Neither side contains explicit custom anchors. Relative
  Markdown file targets were checked (379 links, no missing targets); link
  fragments and generated heading anchors were not validated. All 382 fenced
  blocks remain balanced, with no rendered headings inside them. The total
  block count is unchanged; eight existing blocks changed content, and one
  existing example moved earlier in each eager-loading page. `git diff --check`
  on the 36 tracked pages exits 0. The untracked report has no trailing
  whitespace. The command
  `git diff --no-index --check /dev/null docs/specs/design/readability-advanced-query-20261116.md`
  emitted no diagnostics and exits 1 because the new file differs from
  `/dev/null`. All 36 scoped pages and this report use CRLF.
- Builds, tests, DocFX, package installation, and restore were not run for this
  editorial pass. Generated API output and provider behavior were not
  independently validated.
- The guide 30 CSV examples remain; the whole-entity call now relies on its
  captured filter value and passes only the cancellation token.
