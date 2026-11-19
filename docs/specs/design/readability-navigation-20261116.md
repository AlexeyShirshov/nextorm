# Public documentation navigation review — 2026-11-16

## Scope

Reviewed every tracked public `toc.yml` under `docs/`, excluding generated API
navigation, `_site`, specs, and superpowers. Only navigation files were edited;
Markdown files and their headings were left untouched.

## Reader goals

- Give new readers a path from setup to common query tasks before engine internals.
- Make provider setup distinct from the query guide.
- Make reference and advanced material identifiable from navigation labels.
- Keep the English and Russian navigation in the same logical order while
  retaining the existing flat English root and nested Russian root.

## Before and after

### English root navigation

Before:

`Home → Getting started → Engine → Relationships → Guide → Infrastructure →
Scalar functions → Culture-invariant string formatting → Connecting providers →
Comparisons → Advanced → API reference → Русская документация`

After:

`Home → Getting started → Query guide → Provider configuration → Relationships →
Infrastructure → Scalar function reference → Culture-invariant string formatting →
Comparisons → Advanced topics → Engine internals → API reference →
Русская документация`

The query guide now precedes internals; provider setup is a clearly named,
separate destination; the function section is marked as reference material.
Engine internals follow user-facing tasks and advanced topics.

### Russian root navigation

Before:

`Обзор → Постановка задачи → Начало работы → Движок → Связи → Руководство →
Инфраструктура → Скалярные функции → Культуронезависимое форматирование строк →
Подключение провайдеров → Сравнения → Продвинутое`

After:

`Обзор → Постановка задачи → Начало работы → Руководство по запросам →
Настройка провайдеров → Связи → Инфраструктура → Справочник скалярных функций →
Культуронезависимое форматирование строк → Сравнения → Дополнительные темы →
Внутреннее устройство движка`

This preserves the nested Russian navigation while aligning its reader path with
English. The Russian SQLite provider-specific SQL link is now nested with the
other provider SQL links, matching the English grouping.

### Query guide

Before, the 43 links were mostly one flat list, with provider-specific SQL as the
only nested group. After, the same links are grouped as follows in both
languages:

| Group | Contents |
| --- | --- |
| Query basics / Основы запросов | Query sources through `SELECT DISTINCT` (01–09) |
| Advanced query features / Дополнительные возможности запросов | CTE, windows, functions, raw SQL, hints, JSON, AI and vector search (10–16, 36) |
| Data changes and transactions / Изменение данных и транзакции | INSERT, DELETE, UPDATE, table materialization, MERGE, bulk insert, transactions, batch execution, optimistic concurrency (17–23, 25–26) |
| Data mapping and delivery / Сопоставление и передача данных | Duration and range columns, large objects, dynamic columns, streaming, inheritance, runtime mapping, routing, sharding (24, 27–30, 32–35) |
| Provider-specific SQL and features / Возможности и SQL провайдеров | Provider features (31) and the nested SQL overview plus provider pages |

The groups make common query tasks visible before specialized topics without
removing or renaming any linked page.

## Complete TOC coverage

The counts below are existing `href` entries per TOC. The three edited TOCs are
marked; the remaining section TOCs already presented a clear local order and
were left unchanged.

| TOC | Links | Result |
| --- | ---: | --- |
| `docs/toc.yml` | 13 | Reordered and clarified root navigation |
| `docs/getting-started/toc.yml` | 4 | Reviewed; unchanged |
| `docs/engine/toc.yml` | 4 | Reviewed; unchanged |
| `docs/relationships/toc.yml` | 5 | Reviewed; unchanged |
| `docs/guide/toc.yml` | 43 | Grouped by reader task |
| `docs/infrastructure/toc.yml` | 5 | Reviewed; unchanged |
| `docs/scalar-functions/toc.yml` | 11 | Reviewed; unchanged |
| `docs/connecting-providers/toc.yml` | 8 | Reviewed; unchanged |
| `docs/comparisons/toc.yml` | 5 | Reviewed; unchanged |
| `docs/advanced/toc.yml` | 7 | Reviewed; unchanged |
| `docs/ru/toc.yml` | 91 | Reordered, clarified, and grouped query guide |
| `docs/ru/comparisons/toc.yml` | 5 | Reviewed; unchanged |

Generated `docs/api/toc.yml` was excluded from editing and coverage as requested;
the root `api/` destination exists.

## Checks

- Parsed all 12 in-scope TOCs as YAML with PyYAML 5.4.1.
- Checked all 201 local `href` entries; every target exists.
- Compared each TOC's `href` multiset with its `HEAD` version; all per-file
  multisets are identical, so no destination was added or removed.
- Compared the English guide links with the Russian query-guide subtree: 43
  links on each side, with exact path and order parity after removing the
  `guide/` prefix.
- Verified CRLF line endings and no bare-LF lines in all three edited TOCs.
- No build, test, or DocFX command was run; this task is navigation-only and
  explicitly excludes DocFX.
