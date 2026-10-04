# Issue #189 — `ToDataReader`/`ToDataReaderAsync` на SQLite для не-LOB проекций

Статус: дизайн согласован (user review пройден 2026-10-04). Реализация не начата — отложена до отдельного решения.
Tracking: [GitHub issue #189](https://github.com/AlexeyShirshov/nextorm/issues/189), milestone `1.0.9-rc2`.
Branch: `1.0.9-b`.
Связано: разблокирует item 10 в [#188](https://github.com/AlexeyShirshov/nextorm/issues/188).

## 1. Проблема

`ToDataReader` / `ToDataReaderAsync` сейчас бросают `NotSupportedException` на SQLite, хотя SQLite объявляет `SupportsSequentialAccess => true` (`src/nextorm.sqlite/SqliteDialect.cs:15`). Отказ мотивирован тем, что на SQLite в потоковую проекцию добавляется колонка-локатор `rowid`, которая попала бы наружу в ordinals вызывающего.

Регистр `code-smells-review.md:8071-8072` держит это как открытую фичу `P2`: «SQLite locator-backed multi-column `ToDataReader` (P2, фича). Нужна отдельная модель скрытия `rowid` + доказательство порядка чтения. Триггер: конкретный пользовательский сценарий». Этот issue — конкретный триггер (cross-library zero-materialization бенчмарк в #188).

## 2. Текущий контракт и причина отказа

- Locator добавляется в SELECT **только** на стриминговом пути: `SqlBuilder.cs:527` под `_ctx.SequentialAccess` и `_ctx.Dialect.LobLocatorColumn` (`SqliteDialect.LobLocatorColumn => "rowid"`, `SqliteDialect.cs:18`).
- `ToDataReader`/`ToDataReaderAsync` идут через `OpenLobReader` → `PrepareLobCommand` (`DataContext.cs:394-417`) — это стриминговый путь с `SequentialAccess`, поэтому на SQLite в SQL попадает `rowid`.
- Guard `EnsureDataReaderSupported` (`QueryCommandExtensions.cs:480-487`) fail-closed отвергает **весь диалект**, если `Dialect.LobLocatorColumn is not null` (сообщение `LobDataReaderLocatorMessage`, `:468-469`).
- Рядом уже существует locator-free шов `OpenResultReader` / `PrepareResultCommand` (`DataContext.cs:419-459`), который явно рассчитан на SQLite («demands neither sequential access nor a locator column, so it runs on every relational provider (including SQLite, whose rowid locator would otherwise be appended)») и уже используется терминалом `WriteCsv` (`QueryCommandExtensions.cs:412`). Он готовит план per-call со `storeInCache: false` и не мутирует shared-команды.

Вывод: отказ — осознанный fail-closed, а не техническое ограничение; нужный no-locator путь уже есть.

## 3. Решение (вариант A — выбран)

Маршрутизировать `ToDataReader`/`ToDataReaderAsync` по диалекту вместо безусловного LOB-пути:

- **locator-диалект (SQLite), не-LOB проекция** → `OpenResultReader`/`OpenResultReaderAsync` (no locator, default behavior); вернуть тот же owner-владеющий forward-only reader (`LobDataReader` переиспользуется как generic owner-wrapper, его remarks уточняются).
- **не-locator диалект с sequential access (PostgreSQL, SQL Server)** → текущий `OpenLobReader`-путь без изменений.
- **диалект без sequential access (MySQL/MariaDB, ClickHouse)** → `NotSupportedException`, называя терминал.
- **in-memory** → `NotSupportedException` (как сейчас, через `RequireRelationalContext`; нет `DbDataReader`).

LOB-проекция на SQLite (колонка `byte[]`/`string` внутри multi-column select): **поддерживается буферизованно** (без chunked `SqliteBlob`), т.к. терминал общий и отвергать его из-за одной колонки — неожиданно. Отличие от PG/SQL Server документируется: на SQLite reader не sequential, порядок чтения колонок не ограничен, крупный BLOB читается целиком.

`EnsureDataReaderSupported` сужается: locator-колонка больше не причина отказа; отказ остаётся только для диалектов без sequential access и для in-memory. `LobDataReaderLocatorMessage` удаляется/переформулируется.

## 4. Отвергнутая альтернатива B (hide rowid)

Оставить стриминговый путь на SQLite и **скрыть** завершающую локатор-колонку в `LobDataReader` (уменьшать `FieldCount`, транслировать ordinals, скрывать последнюю колонку от `GetName`/`GetFieldType`). Давало бы true sequential streaming (включая chunked LOB) на SQLite, но требует «доказательства порядка чтения» и рискованно для `GetSchemaTable`, `GetValues` и произвольного доступа по имени. Для текущего сценария (общая multi-column проекция без LOB-стриминга) избыточно.

Повторное рассмотрение — при конкретном сценарии chunked multi-column LOB streaming на SQLite; одноколоночный LOB уже закрыт `ToStream`/`ToTextReader`.

## 5. Публичная семантика и совместимость

- Расширение поведения, не удаление/переименование сигнатур: 4 публичных члена `ToDataReader`/`ToDataReaderAsync` (`QueryCommandExtensions.cs:306,322,370,…`) остаются, меняется только условие отказа.
- На SQLite `ToDataReader` теперь возвращает reader без лишней колонки; `FieldCount` равен числу колонок проекции; ordinals соответствуют `Select`.
- На PostgreSQL / SQL Server поведение не меняется (sequential access, forward-only, ascending ordinals).
- XML-doc `<remarks>` всех 4 членов: убрать «Not supported on SQLite», описать разницу путей (SQLite non-sequential / no chunked LOB).
- `data-reader` terminal остаётся relational-only; in-memory не затрагивается.

## 6. Изменения в коде

- `src/nextorm.core/Query/QueryCommandExtensions.cs`:
  - `ToDataReader`/`ToDataReaderAsync`: выбор пути по `context.Dialect.LobLocatorColumn`/`SupportsSequentialAccess`;
  - `EnsureDataReaderSupported`: убрать locator-ветку, оставить сообщение с именем терминала для non-sequential диалектов;
  - удалить/переформулировать `LobDataReaderLocatorMessage`.
- `src/nextorm.core/DataContext/DataContext.cs`:
  - переиспользовать `OpenResultReader`/`OpenResultReaderAsync`;
  - обобщить сообщение `PrepareResultCommand` про lazy temp-table (`:448-450` сейчас говорит только про CSV) — сделать нейтральным или параметризованным, чтобы `ToDataReader` не выдавал CSV-текст.
- `src/nextorm.core/DataContext/LobDataReader.cs`: скорректировать XML-doc (обобщить с «LOB/sequential» на owner-владеющий forward-only reader); поведение без изменений.

## 7. Доки (EN + RU, оба дерева)

Обновить утверждения «`ToDataReader` не поддерживается на SQLite / rowid locator»:

- `docs/advanced/api-reference.md` + `docs/ru/advanced/api-reference.md` (`:91`);
- `docs/advanced/limitations.md` + `docs/ru/advanced/limitations.md` (`:68-69`, `:89`);
- `docs/providers/overview.md` + RU (таблица `:61`), `docs/providers/sqlite.md` + RU, `docs/providers/postgres.md` + RU (`:275`, `:302`), `docs/providers/sqlserver.md` + RU, `docs/providers/in-memory.md` + RU;
- `docs/guide/26-large-objects.md` + RU, `docs/guide/28-streaming-data.md` + RU;
- `docs/comparisons/capabilities.md` + RU (`:33`);
- internal specs: `docs/specs/comparison/linq2db-comparison.md` + `docs/specs/ru/comparison/linq2db-comparison.md` (`:117`, `:253`), `docs/specs/comparison/capability-matrix.md` (`:132`), `docs/specs/design/API-NAMING-REVIEW.md` (реестр LOB1), `docs/specs/design/code-smells-review.md:8071-8072` (закрыть перенос «SQLite locator-backed multi-column ToDataReader»).

## 8. Тест-план

- Core: `tests/nextorm.core.tests/LobDataReaderTests.cs`, `tests/nextorm.sqlite.tests/ResultReaderTests.cs` — маршрутизация и ownership.
- SQLite: `tests/nextorm.sqlite.tests/LobSqlGenerationTests.cs` — отсутствие `rowid` в не-LOB проекции; новые behavioral sync/async (`ToDataReader` возвращает `FieldCount == проекция`, значения по ordinals, dispose освобождает per-call command, forward-only).
- SQLite LOB-проекция: буферизованное чтение колонки `byte[]`/`string` в multi-column reader.
- Негативные: MySQL/MariaDB/ClickHouse и in-memory по-прежнему `NotSupportedException` с именем терминала.
- PostgreSQL / SQL Server: существующие тесты `ToDataReader` зелёные без изменений.

## 9. Критерии приёмки

- `ToDataReader`/`ToDataReaderAsync` работают на SQLite для не-LOB проекций (sync + async): reader без лишней колонки, `FieldCount`/ordinals соответствуют проекции.
- `LobDataReaderLocatorMessage` отсутствует в поведении; non-sequential диалекты и in-memory отклоняются с actionable-сообщением, называющим `ToDataReader`.
- `dotnet build nextorm.slnx -c Debug` и `-c Release` — 0 warning / 0 error.
- Существующие PG/SQL Server тесты `ToDataReader` зелёные; регрессий в `ToStream`/`ToTextReader`/`WriteCsv` нет.
- Покрытие проекта не ниже `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`.
- Доки EN+RU из §7 обновлены.

## 10. Риски

- Смена публичной семантики (ранее — исключение на SQLite) может задеть код, полагающийся на бросок; риск низкий (fail-closed был ограничением, а не гарантией).
- Разница «SQLite без sequential access» vs «PG/SQL Server sequential» должна быть явно задокументирована, чтобы не создать ложных ожиданий chunked-чтения BLOB на SQLite.
- Обобщение temp-table-сообщения `PrepareResultCommand` затрагивает CSV-путь — проверить, что текст CSV остаётся корректным.

## 11. Ссылки

- `src/nextorm.core/Query/QueryCommandExtensions.cs:294-488`
- `src/nextorm.core/DataContext/DataContext.cs:394-459`
- `src/nextorm.core/DataContext/QueryExecutor.cs:395-472`
- `src/nextorm.core/DataContext/LobDataReader.cs`
- `src/nextorm.core/DataContext/SqlBuilder.cs:524-528`
- `src/nextorm.core/DataContext/QueryPlanner.cs:560-577`
- `docs/specs/design/code-smells-review.md:7995-8078`
- `docs/specs/design/API-NAMING-REVIEW.md:5323-5338`
