# Качество решений #112 — 4-way (a1 / a2 / a3 / upstream)

Слои: **L1** приёмочные тесты (свои + портированные из upstream), **L2** single-DS-судья (один агент DeepSeek), **L3** single-DS-аудит. Имена API свободны.

## L1 — поведение

| вариант | свои core | свои sqlite | порт core | порт sqlite | src тронут | портировано |
|---|---|---|---|---|---|---|
| a1 | ok (885) | ok (714) | — | — | 0 | 0 |
| a2 | ok (895) | ok (718) | — | — | 0 | 0 |
| a3 | ok (894) | ok (706) | — | — | 0 | 0 |
| upstream | ok (948) | ok (727) | — | — | 0 | 0 |

## L2 — рубрика (single-DS судья)

| вариант | R1 | R2 | R3 | R4 | R5 | R6 | correctness | design | api | tests | edge | docs | scope | total |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| a1 | 2 | 2 | 2 | 2 | 1 | 2 | 4 | 4 | 5 | 4 | 3 | 5 | 5 | 89 |
| a2 | 2 | 2 | 2 | 2 | 2 | 2 | 4 | 4 | 5 | 4 | 4 | 5 | 5 | 94 |
| a3 | 2 | 2 | 2 | 2 | 2 | 1 | 4 | 4 | 5 | 4 | 3 | 4 | 5 | 88 |
| upstream | 2 | 2 | 2 | 2 | 2 | 2 | 5 | 4 | 5 | 5 | 5 | 5 | 4 | 97 |

## L3 — аудит

| вариант | P0 | P1 | P2 | coverage |
|---|---|---|---|---|
| a1 | 0 | 2 | 1 | 3 |
| a2 | 0 | 0 | 6 | 4 |
| a3 | 0 | 1 | 3 | 4 |
| upstream | 0 | 0 | 2 | 4 |

## Механика

| вариант | rc | wall, s | cost | files | tests | overlap | skills |
|---|---|---|---|---|---|---|---|
| a1 | 0 | 1212 | 0.182066 | 17 | 4 | 9/17 | нет |
| a2 | 0 | 4346 | 0.178354 | 21 | 3 | 12/21 | implementing-todo-features |
| a3 | 0 | 4081 | 0.076222 | 16 | 3 | 7/16 | nextorm-pdca,pdca-dotnet |

## Детали

### a1

- L2 total=89: Реализованы CSV-терминалы ToCsv/ToCsvAsync на QueryCommand<TResult> и EntityBuilder<TEntity> (Stream и TextWriter, sync+async), читающие типизированные колонки DbDataReader без материализации TResult и без боксинга стандартных типов; raw-columns путь через новый флаг планировщика не трогает кэш планов и не портит его (storeInCache:false, map=null). Корректность CSV (заголовок из имён колонок, экранирование разделителя/кавычки/перевода строки, NULL/NullText, UTF-8 без BOM), владение приёмником (не закрывается, ридер и команда освобождаются) и отказы in-memory/temp-table (NotSupportedException) задокументированы в EN+RU (guide 31, toc, api-reference) и покрыты тестами writer/sqlite/integration. Основные пробелы: AlwaysQuote не кавычит NULL-поле, отказ in-memory переиспользует LOB-сообщение, async-путь пишет в приёмник синхронно, нет тестов на пустой результат/отмену/struct-проекцию, и поведение при исключении явно не определено. Дополнительно: in-memory-тесты проверяют только тип исключения, а не его сообщение; сборка/тесты не запускались (clean-room).
  - P2: src/nextorm.core/DataContext/CsvWriter.cs:74-79 — при AlwaysQuote=true и NullText=null NULL-поле пишется пустым без кавычек, тогда как пустая строка даёт "" (CsvWriter.cs:182), что противоречит контракту «Quote every field» (docs/guide/31-csv-streaming.md:68)
  - P2: src/nextorm.core/Query/QueryCommandExtensions.cs:581 — отказ in-memory переиспользует LOB-сообщение («LOB reads require a database provider… supports only the single-column ToStream/ToTextReader terminals»), вводящее в заблуждение для вызова ToCsv
  - P2: src/nextorm.core/Query/QueryCommandExtensions.cs:500-519 и CsvWriter.Flush (CsvWriter.cs:53) — async-терминал пишет CSV-буфер в приёмник синхронно (у CsvWriter нет async-flush), асинхронны только ReadAsync и финальный FlushAsync
  - P2: tests — нет кейсов пустого результата (0 строк), реальной отмены через CancellationToken и проекции struct/без parameterless ctor (CsvWriterTests.cs, CsvTerminalTests.cs, CommonTestSuite.Csv.cs)
  - P2: R5 — поведение при исключении (что уже записано в поток) не определено: при ошибке буфер CsvWriter не сбрасывается, частично записанные данные остаются; в docs/guide/31-csv-streaming.md это не оговорено
- L3:
  - P1 src/nextorm.core/DataContext/QueryExecutor.cs:459 — OpenRawReader (and :475 OpenRawReaderAsync) execute a per-call command prepared with storeInCache:false (DataContext.cs:326) and never dispose its DbCommand: only the DbDataReader is disposed (QueryCommandExtensions.cs:482/505), and an ADO.NET reader does not dispose its command, so every ToCsv/ToCsvAsync call leaks a provider command; the analogous LOB path disposes it via CommandReaderOwner (QueryExecutor.cs:408/432), and the comment at QueryExecutor.cs:446-447 ('the prepared command stays owned by the plan cache') is factually wrong for this uncached path.
  - P1 tests/nextorm.sqlite.tests/CsvTerminalTests.cs:55 — the async surface is covered only for ToCsvAsync(Stream); the distinct public overload ToCsvAsync(TextWriter) (QueryCommandExtensions.cs:469) has no test at all, and no test observes the CancellationToken (cancellation during open/ReadAsync) although it is part of the public contract.

### a2

- L2 total=94: Реализация закрывает #112 полностью: терминалы ToCsv/ToCsvAsync есть и на QueryCommand<TResult>, и на EntityBuilder<TEntity>, пишут в переданный Stream построчно из DbDataReader без компиляции маппера (suppressMapper + storeInCache:false), с типизированными геттерами, корректным RFC 4180-экранированием, фолбэком NotSupportedException для in-memory и AsTempTable, и полным EN+RU доком. Сборки nextorm.core.tests / sqlite.tests / integration.tests проходят 0 warnings/0 errors; CSV-тесты 23/23 (core) и 12/12 (SQLite). Существенных дефектов (P0/P1) не найдено; замечания — уровня P2: неразличимость NULL и пустой строки по умолчанию, отсутствие sync-перегрузки с CancellationToken, валидация опций после открытия reader'а и неосторожное абсолютное обещание про отсутствие буферизации. Провайдерная применимость вне SQLite заявлена, но эмпирически не проверена (только компиляция CommonTestSuite.Csv.cs).
  - P2: src/nextorm.core/Query/CsvOptions.cs:25 — NullText по умолчанию пустая строка, поэтому SQL NULL и пустая строка дают одинаковый вывод; теста, различающего эти случаи в одной колонке, нет (CsvWriter.cs:176-180), а rubric L3 явно требует покрытия NULL vs пустая строка.
  - P2: src/nextorm.core/Query/QueryCommandCsvExtensions.cs:22,32,81,91 — sync-терминалы ToCsv не имеют перегрузки с CancellationToken (в семействе LOB, QueryCommandExtensions.cs:105,163,220,263, такая есть); async-путь токен принимает, так что R6 закрыт, но паритет с sibling-терминалами нарушен.
  - P2: src/nextorm.core/Query/CsvOptions.cs:37-40 — XML-doc Encoding говорит про «caller that already opened a StreamWriter controls the encoding itself», хотя публичный терминал всегда сам создаёт StreamWriter (на входе Stream), формулировка вводит в заблуждение.
  - P2: src/nextorm.core/Query/QueryCommandCsvExtensions.cs:144-149 — валидация Delimiter/LineEnding выполняется в ctor CsvWriter уже после context.OpenCsvReader, т.е. при некорректных опциях сначала выполняется лишний round-trip к БД, и только потом бросается ArgumentException.
  - P2: docs/guide/31-csv-export.md:13 (и CsvWriter.cs:9-14) — абсолютное обещание «the result is not buffered in memory beyond the writer's own buffer», тогда как QueryPlanner.cs:461-465 прямо фиксирует buffered behavior у MySQL/MariaDB/ClickHouse; на этих провайдерах O(буфера) не гарантируется.
  - P2: docs/specs/design/API-NAMING-REVIEW.md (+code-smells-review.md, solid-review.md) и docs/specs/roadmap/todo_csv_stream.md — в трекаемые внутренние спеки попали имена экспериментальной ветки/агентов (exp-a2-skill, nextorm-design-engineer); шум в репозитории, не влияет на поведение.
- L3:
  - P2 src/nextorm.core/Query/QueryCommandCsvExtensions.cs:188-195 — RequireRelationalContext copy-pasted from QueryCommandExtensions.cs:442-449 (identical body + ThrowIfDisposed, only the message differs); DRY consolidation into the existing internal helper was not done.
  - P2 src/nextorm.core/DataContext/DataContext.cs:355-373 — OpenCsvReader/OpenCsvReaderAsync duplicate the OpenLobReader/OpenLobReaderAsync pair (:330-342) and reuse QueryExecutor.OpenLobReader whose XML-doc/summary (QueryExecutor.cs:391-395) is explicitly LOB/SequentialAccess-specific, so a non-LOB CSV read is documented as a LOB read (SRP/naming drift at the reuse boundary).
  - P2 docs/specs/roadmap/todo_csv_stream.md:131,139,140,142 — the shipped variant matrix marks enum columns, raw SQL (WithSql/PrepareFromSql), Join/GroupBy/aggregates/DISTINCT and CancellationToken ('отмена до/во время перечисления') as `test`, but no such tests exist: grep for Cancellation/enum/GroupBy/WithSql/Join/Distinct over CsvWriterTests/CsvExportTests/CommonTestSuite.Csv returns nothing except uncancelled TestContext tokens, so the committed test plan overstates coverage (sync ToCsv/ToCsvCore pass CancellationToken.None and the sync family has no ct overload either).
  - P2 src/nextorm.core/Query/CsvOptions.cs:37-40 — Encoding XML-doc says the value is 'only read when the terminal creates its own writer; a caller that already opened a StreamWriter controls the encoding itself', but the public API accepts only a Stream (QueryCommandCsvExtensions.cs:22/32/41/51/61/72/81/91/100/110/120/131); there is no TextWriter/StreamWriter overload, so the second half of the contract describes an unusable path.
  - P2 src/nextorm.core/Query/QueryCommandCsvExtensions.cs:7-9 — public class doc promises 'no value is boxed' generally and only carves out a 'provider-specific field type without a dedicated getter'; sbyte/ushort/uint/ulong/DateTimeOffset/TimeSpan are BCL types routed through DbDataReader.GetFieldValue<T> (CsvWriter.cs:194/200/206/212/230/233), whose base implementation is (T)GetValue -> boxes, so the absolute no-boxing contract is inaccurate on drivers that do not specialise GetFieldValue (CsvWriter.cs:10-12 hedges it, the public page does not).
  - P2 src/nextorm.core/Query/CsvWriter.cs:255-258 — WriteFormatted's else branch (value.ToString(format.IsEmpty ? null : format.ToString(), InvariantCulture)) is unreachable for every FieldKind the switch can pass (DateTime 'O' <=33, TimeSpan 'c' <=26, Guid 'D' 36, ulong <=20, decimal <=31, all < the 64-char stack buffer) and has no covering test, i.e. untested dead defensive code.
  - P2 src/nextorm.core/Query/QueryCommandCsvExtensions.cs:139-144 and 161-166 — ToCsvCore/ToCsvCoreAsync duplicate the 4 ArgumentNullException guards plus context resolution; only two occurrences (Rule of Three not yet met) and the null-check for options.Encoding is repeated verbatim, so the validation surface must be kept in sync manually.

### a3

- L2 total=88: Solid streaming CSV terminal: both surfaces exist (QueryCommand<TResult> and EntityBuilder<TEntity>), rows are written through the reader without materialising TResult, values go through typed GetFieldValue<T>/closed-generic ISpanFormattable formatters, RFC-4180 quoting (comma/quote/CR/LF doubled), header, CRLF, no BOM, invariant culture, NULL as empty field, and the output Stream is left open with reader/command disposed. Verified locally: core+integration projects build 0 warnings/0 errors and the 22 CSV unit tests pass. The main defect is that all four terminals omit the `params` runtime-parameter argument that every sibling terminal has, so queries using SqlFunctions.Parameter placeholders silently bind NULL (R6 partial; doc/code inconsistency in 13-query-reuse). Remaining gaps are minor: LobDataReader reuse on a non-sequential path, a per-row linked CTS in the async loop, and no empty-result/cancellation tests.
  - P1: src/nextorm.core/Query/CsvExtensions.cs:16,37,57,74 — none of the four ToCsv/ToCsvAsync overloads accepts `params object?[]`/`ReadOnlySpan<object?>`, unlike every sibling terminal (src/nextorm.core/Query/QueryCommandExtensions.cs:306,322,354,370 and QueryCommand.TResult.cs:145), so queries with SqlFunctions.Parameter<N>(i) runtime placeholders bind no value (NULL) and produce silently wrong/empty CSV; rubric R6 explicitly requires `params`.
  - P2: docs/guide/13-query-reuse.md:54 and docs/ru/guide/13-query-reuse.md — ToCsv is listed as a terminal while line 56 states the terminals' `params object[]` carry the runtime values for SqlFunctions.Parameter placeholders; ToCsv has no such parameter, so the docs contradict the code.
  - P2: src/nextorm.core/Query/CsvExtensions.cs:25,46 — the default-access, non-sequential path wraps the reader in `LobDataReader`, whose contract documents it as a multi-column LOB/sequential-access reader (src/nextorm.core/DataContext/LobDataReader.cs:7-17); name/contract mismatch for a plain CSV reader.
  - P2: src/nextorm.core/DataContext/DataContext.cs:355-382 — OpenQueryReader/OpenQueryReaderAsync/PrepareQueryReaderCommand duplicate OpenLobReader/PrepareLobCommand and reuse _executor.OpenLobReader; DRY/maintainability smell.
  - P2: src/nextorm.core/Query/CsvWriter.cs:87 + src/nextorm.core/DataContext/LobDataReader.cs:90-99 — the async loop calls ReadAsync(cancellationToken) with the same token the wrapper already stores, so LobDataReader allocates a linked CancellationTokenSource on every row (O(rows) allocations), contradicting R2's O(buffer) claim.
  - P2/tests: tests/nextorm.core.tests/CsvWriterTests.cs and tests/nextorm.integration.tests/CommonTestSuite.Csv.cs — no zero-row (empty result) case and no cancellation case; no test binds a runtime query parameter.
  - P2: src/nextorm.core/Query/CsvWriter.cs:106-116 — a per-column formatter is built with MakeGenericType/Activator.CreateInstance on every call rather than cached per column type; setup cost repeats per export.
- L3:
  - P1 src/nextorm.core/DataContext/DataContext.cs:355-359 — новый OpenQueryReader берёт prepared из GetPreparedQueryCommand, который для источника AsTempTable навешивает PendingBatch (DataContext.cs:299-303,326), а _executor.OpenLobReader/OpenLobReaderAsync (QueryExecutor.cs:400-416) этот PendingBatch не выполняет (DROP/CREATE TEMP TABLE не эмитится). Поэтому ToCsv/ToCsvAsync по запросу с ленивой temp-table падает сырой ошибкой провайдера (таблицы нет), тогда как все буферизованные терминалы (ToList/CreateEnumerator, QueryExecutor.cs:583-613) ветку PendingBatch обрабатывают; ни одного теста на этот вариант нет, и LOB-терминалы этой дыры не имеют, т.к. PrepareLobCommand идёт напрямую в _planner (DataContext.cs:392) и PendingBatch не создаёт.
  - P2 src/nextorm.core/Query/CsvWriter.cs:87 + src/nextorm.core/Query/CsvExtensions.cs:46 — на async-горячем пути while(await reader.ReadAsync(cancellationToken)) вызывается раз на строку; LobDataReader.ReadAsync при cancellable токене каждый раз делает CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken) (LobDataReader.cs:94-98). Для больших выгрузок это одна лишняя CTS-аллокация на строку, что противоречит заявленному в CsvExtensions/CsvWriter и docs/guide/33-csv-streaming.md «allocation-light» контракту; sync-путь этим не страдает.
  - P2 src/nextorm.core/Query/CsvExtensions.cs:16,37 — API-асимметрия с остальными терминалами: ToCsv/ToCsvAsync не принимают рантайм-значения параметров (нет params/перегрузки), поэтому запрос с плейсхолдером SqlFunctions.Parameter<T> (используется, например, в CommonTestSuite.Binary.cs:47) через ToCsv невыгружаем — привязывается пустой набор (ReadOnlySpan<object?>.Empty / null, CsvExtensions.cs:25,46), и провайдер падает на неразрешённом параметре; у ToStream/ToTextReader/ToDataReader (QueryCommandExtensions.cs:96-378) такая перегрузка есть.
  - P2 tests/nextorm.core.tests/CsvWriterTests.cs, CsvExtensionsTests.cs, tests/nextorm.integration.tests/CommonTestSuite.Csv.cs — не покрыты: пустой результат (только строка заголовка), отмена (ни один тест не отменяет токен) и fallback-ветка форматтера `static v => v?.ToString() ?? string.Empty` (CsvWriter.cs:196) для типа, не являющегося string/byte[]/ISpanFormattable. При этом fallback использует текущую культуру, а инвариантность (CsvWriter.cs:203,206) действует только для ISpanFormattable, что расходится с формулировкой «Invariant formatting. Values are formatted with the invariant culture» (docs/guide/33-csv-streaming.md, docs/advanced/api-reference.md).

### upstream

- L2 total=97: All six acceptance criteria are met: WriteCsv/WriteCsvAsync exist on both QueryCommand<TResult> and EntityBuilder<TEntity> (QueryCommandExtensions.cs:397,434; EntityBuilderExtensions.cs:21,35), stream rows from the open reader without materialising TResult (CountingRow tests, CsvStreamTests.cs:250-270, CommonTestSuite.Csv.cs:240-252), read every supported column through typed getters/static Convert.To<T> and reject object-based accessors before any output (CsvStreamWriter.cs:88-109,274-315), implement RFC 4180 quoting/header/NULL (CsvDialect.cs, CsvRowBuffer.cs:73-100), never dispose the destination while releasing reader+command with defined exception behaviour, and cover sync+async, cancellation, in-memory fail-closed and every relational provider. I independently ran the core (948 passed), SQLite (728 passed, 1 pre-existing skip) and SQL Server (489 passed) suites plus clean builds of core, integration tests and the benchmark — 0 warnings, 0 errors. Quality is high: extensive EN+RU guide/limitations/API-reference parity and a very broad edge-case test matrix; the P2s are the shared byte[] mapper change, the whole-entity-without-Select behaviour versus R1's literal wording, and per-call plan compilation.
  - P2: src/nextorm.core/Expressions/SelectExpression.cs:229-233 — byte[] now reads via GetFieldValue<byte[]>() in the shared buffered mapper for every provider just to make the CSV path box-free; broader blast radius than the feature, though SelectExpressionTests (tests/nextorm.core.tests/SelectExpressionTests.cs:14-24) and all three passing suites cover it.
  - P2: docs/guide/31-csv-export.md:22-27 + tests/nextorm.integration.tests/CommonTestSuite.Csv.cs:89-100 — a builder without an explicit Select exports the whole entity instead of failing; the rubric's 'requires explicit Select / clear error' guard is only enforced for an empty SelectList (CsvStreamWriter.cs:93-94,123-124, test CsvStreamWriterTests.cs:326-334). Intent (no silent empty output) is met, but the literal requirement is not enforced on the EntityBuilder surface.
  - P2: src/nextorm.core/Query/Csv/CsvStreamWriter.cs:145-158 — ValidateOptions throws with paramName 'options' rather than the offending member (CsvStreamOptions.Delimiter); misleading argument name only.
  - P2: docs/specs/roadmap/todo_csv_streaming.md:57-64 — the compiled column plan is rebuilt on every call (storeInCache:false, DataContext.cs PrepareResultCommand); documented open slice, but a real per-call cost for many small exports and no writer/plan cache exists to audit.
  - P2: tests/nextorm.sqlite.tests/ResultReaderSpikeTests.cs:15 — a 'spike' test retained as the permanent regression cover for the internal DataContext.OpenResultReader seam; misleading name for durable coverage.
  - P2: src/nextorm.core/DataContext/DataContext.cs MapTypedColumn/SupportsTypedColumn internal seams duplicate the same comment block verbatim; cosmetic maintainability wart.
- L3:
  - P2 src/nextorm.core/DataContext/QueryExecutor.cs:452,476 — OpenResultReader/OpenResultReaderAsync are byte-identical copies of OpenLobReader/OpenLobReaderAsync (:400,:424); the DataContext wrappers OpenResultReader/OpenResultReaderAsync (:360,:367) likewise duplicate OpenLobReader/OpenLobReaderAsync (:330,:337), differing only in PrepareLobCommand vs PrepareResultCommand — DRY smell, the new reader seam could reuse the existing one.
  - P2 tests/nextorm.core.tests/CsvStreamWriterTests.cs:64-76,440-451 — the multi-chunk Base64 path (payload > 768 bytes, src/nextorm.core/Query/Csv/CsvRowBuffer.cs:137-160) is never exercised: the largest byte[] tested is 4 bytes and the integration Blob is 4 bytes, so the chunk loop, cross-chunk concatenation and the TryToBase64Chars-false guard are unverified.

