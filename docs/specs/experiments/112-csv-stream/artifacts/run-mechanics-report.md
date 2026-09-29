# Эксперимент #112: сравнение сред (механика)

- база: `7aacaffb1491a6fffabbbf6863dcd6bc3b9a8089`
- upstream #112: ворктри реального репо (штатный PDCA, plan/check = Sol)

## Сводка

| цель | среда | rc | wall, s | cost | files | tests | overlap c upstream | skills |
|---|---|---|---|---|---|---|---|---|
| a1 | bare: без скиллов | 0 | 1212 | 0.182066 | 17 | 4 | 9/17 | нет |
| a2 | implementing-todo-features | 0 | 4346 | 0.178354 | 21 | 3 | 12/21 | implementing-todo-features |
| a3 | pdca-dotnet (все агенты flash) | 0 | 4081 | 0.076222 | 16 | 3 | 7/16 | nextorm-pdca, pdca-dotnet |
| upstream | штатный PDCA (plan/check = Sol) | - | 0 | None | 32 | 10 | 32/32 | нет |

## a1 — bare: без скиллов
- профиль: `deepseek.jsonc (oc-ds)`; shortstat: `17 files changed, 1377 insertions(+), 4 deletions(-)`
- недостаёт против upstream (23): `benchmarks/nextorm.benchmark/SqliteBenchmarkCsv.cs`, `docs/advanced/limitations.md`, `docs/guide/31-csv-export.md`, `docs/ru/advanced/limitations.md`, `docs/ru/guide/31-csv-export.md`, `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/todo_csv_streaming.md`, `src/nextorm.core/Expressions/SelectExpression.cs`, `src/nextorm.core/Query/Csv/CsvDialect.cs`, `src/nextorm.core/Query/Csv/CsvRowBuffer.cs`, `src/nextorm.core/Query/Csv/CsvStreamWriter.cs`, `src/nextorm.core/Query/Csv/CsvValueFormatter.cs`
- лишнее против upstream (8): `docs/guide/31-csv-streaming.md`, `docs/ru/guide/31-csv-streaming.md`, `src/nextorm.core/DataContext/CsvOptions.cs`, `src/nextorm.core/DataContext/CsvWriter.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `tests/nextorm.core.tests/CsvTerminalTests.cs`, `tests/nextorm.core.tests/CsvWriterTests.cs`, `tests/nextorm.sqlite.tests/CsvTerminalTests.cs`

## a2 — implementing-todo-features
- профиль: `deepseek.jsonc (oc-ds)`; shortstat: `21 files changed, 1780 insertions(+), 2 deletions(-)`
- недостаёт против upstream (20): `benchmarks/nextorm.benchmark/SqliteBenchmarkCsv.cs`, `docs/specs/roadmap/todo_csv_streaming.md`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/DataContext/QueryExecutor.cs`, `src/nextorm.core/Expressions/SelectExpression.cs`, `src/nextorm.core/Query/Csv/CsvDialect.cs`, `src/nextorm.core/Query/Csv/CsvRowBuffer.cs`, `src/nextorm.core/Query/Csv/CsvStreamWriter.cs`, `src/nextorm.core/Query/Csv/CsvValueFormatter.cs`, `src/nextorm.core/Query/CsvStreamOptions.cs`, `src/nextorm.sqlserver/SqlServerDataContext.cs`, `tests/nextorm.core.tests/CsvStreamWriterTests.cs`
- лишнее против upstream (9): `docs/specs/design/code-smells-review.md`, `docs/specs/design/solid-review.md`, `docs/specs/roadmap/todo_csv_stream.md`, `src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.core/Query/CsvOptions.cs`, `src/nextorm.core/Query/CsvWriter.cs`, `src/nextorm.core/Query/QueryCommandCsvExtensions.cs`, `tests/nextorm.core.tests/CsvWriterTests.cs`, `tests/nextorm.sqlite.tests/CsvExportTests.cs`

## a3 — pdca-dotnet (все агенты flash)
- профиль: `test-pdca.jsonc`; shortstat: `16 files changed, 1156 insertions(+)`
- коммиты: `afe6393 #112 Streaming CSV output to a Stream`
- недостаёт против upstream (25): `benchmarks/nextorm.benchmark/SqliteBenchmarkCsv.cs`, `docs/advanced/limitations.md`, `docs/guide/31-csv-export.md`, `docs/ru/advanced/limitations.md`, `docs/ru/guide/31-csv-export.md`, `docs/specs/roadmap/todo_csv_streaming.md`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/DataContext/QueryExecutor.cs`, `src/nextorm.core/Expressions/SelectExpression.cs`, `src/nextorm.core/Query/Csv/CsvDialect.cs`, `src/nextorm.core/Query/Csv/CsvRowBuffer.cs`, `src/nextorm.core/Query/Csv/CsvStreamWriter.cs`
- лишнее против upstream (9): `docs/guide/13-query-reuse.md`, `docs/guide/33-csv-streaming.md`, `docs/ru/guide/13-query-reuse.md`, `docs/ru/guide/33-csv-streaming.md`, `docs/specs/status/csv-stream-1.md`, `src/nextorm.core/Query/CsvExtensions.cs`, `src/nextorm.core/Query/CsvWriter.cs`, `tests/nextorm.core.tests/CsvExtensionsTests.cs`, `tests/nextorm.core.tests/CsvWriterTests.cs`

## upstream — штатный PDCA (plan/check = Sol)
- профиль: `real repo`; shortstat: `32 files changed, 3794 insertions(+), 10 deletions(-)`
- коммиты: `c29de9d #112 Потоковая выдача CSV в Stream`
- недостаёт против upstream (0): —
- лишнее против upstream (0): —

