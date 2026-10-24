# EV08 - cache/template/lifecycle

Green on the four shared-suite providers (10/10 facts each), and on the MySQL/MariaDB 12-cell matrix
and the ClickHouse class.

- safe -> hostile -> safe and safe -> NUL -> safe through one captured variable on the same prepared
  command (PostgreSQL throws on the NUL execution and the following safe execution succeeds; other
  providers match exactly);
- a second NUL execution on the already-warm command;
- two independently created contexts over the same connection string, full corpus exact;
- the shared `Any` command's `Cache` flag and `QueryCacheEnabled` are unchanged after the sequence.

## Select-list occurrence correction (D4 fix)

`src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`MakeColumn`) registers the rendered column
fragment with the statement capture; `src/nextorm.core/DataContext/SqlBuilder.cs` records each select
item's offset and merges the fragment occurrences after the select-list and WITH head insertions.
Result: a captured value used only as a projected column now has a template occurrence
(`OccurrenceCount` 1; previously 0) and the MySQL-family specialization lowers it to
`_utf8mb4 X'...'`, independent of driver text escaping.

Regression pinned in `tests/nextorm.mysql.tests`:
- `CapturedValueInSelectList_ShouldBeRecordedAsATemplateOccurrence` (projection occurrence = 1);
- `PredicateCapturedValue_OnAWarmCommand_ShouldKeepItsTemplateOccurrences` (predicate membership is
  unchanged: 3 occurrences on both the cold and warm checkout).

Logs: `.pdca/T327/rv2/logs/D4-shape-mysql.log`, `.pdca/T327/rv2/logs/D4-core-tests.log`,
`.pdca/T327/rv2/logs/D8a-mysql-modes.log`, `.pdca/T327/rv2/logs/D8a-mariadb.log`.

## D8b full fail-closed boundary (r=3, single run)

One canonical full-suite run, serialized under the collection container lease, with no `NEXTORM_SKIP_PROVIDERS`: Total 5352, Failed 7, Skipped 197, Not Run 0, exit 1; skip budget 197/197 (delta 0). Log `.pdca/T327/rv2/logs/D8b-full-boundary.log`, details in `.pdca/T327/rv2/rows/EV09/d8b-boundary.json`. The boundary is red: 2 product regressions (DEF-T327-03, DEF-T327-04), 4 stale expectations under the declared representation (DEF-T327-06), 1 pre-existing unrelated SQLite failure (DEF-T327-05).

The ClickHouse failure (`ClickHouse/ClickHouseExtendedFunctionTests.cs:74`, `SqlParameterTemplateRecorder.AdvanceTo` -> `ArgumentOutOfRangeException`) is the D4 template-recorder/array-capture defect DEF-T327-04, so the cache/template row stays open even though the D8a lifecycle cases did not fail.
