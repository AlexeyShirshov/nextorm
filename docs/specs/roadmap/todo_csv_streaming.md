# TODO: потоковая выдача CSV в `Stream` (`WriteCsv`/`WriteCsvAsync`)

> Tracking issue: [#112](https://github.com/AlexeyShirshov/nextorm/issues/112).
> Milestone: `1.0.9-b`.

> **Изменение объёма (PLAN).** Все оставшиеся срезы ниже остаются в milestone `1.0.9-b`
> и не переносятся в будущий milestone (в частности, не в `1.1`): кэш колонкового плана
> и остаток memory-bound бинарных (`byte[]`) полей (поддерживаемый direct-binary
> sequential-путь закрыт в #167; см. §2(b)). Fail-closed ленивого temp-источника
> `AsTempTable` и CSV в in-memory-контексте **отгружены** и покрыты тестами (§1).
> Общая буферизованная материализация (срез c) в объём #112 не входит.

> Публичное поведение и ограничения опубликованы в `docs/guide/28-streaming-data.md`
> (+RU) и `docs/advanced/limitations.md` (+RU). Публичные страницы на `docs/specs/**`
> не ссылаются.

## 1. Отгружено

- `WriteCsv`/`WriteCsvAsync` над `QueryCommand<TResult>` и `EntityBuilder<TEntity>`: sync
  принимает `params ReadOnlySpan<object?>`, async — `params object?[]`; поток назначения
  не закрывается.
- Диалект CSV: UTF-8 без BOM, RFC 4180-экранирование, CRLF, invariant-культура,
  `DateTime`/`DateTimeOffset` в формате `O`, `Guid` в формате `D`, `bool` как `true`/`false`,
  `byte[]` как Base64; заголовок из имён свойств проекции с фолбэком `ColumnN`.
- `CsvStreamOptions`: `IncludeHeader` (по умолчанию `true`), `Delimiter` (по умолчанию `,`),
  `NullMarker` (по умолчанию `\N`), `ExcelMode` (по умолчанию `false`), `ValueTransform`
  (по умолчанию `null`). Контракт: SQL NULL → `NullMarker` дословно (через `ValueTransform`
  не проходит); пустая строка → пустое поле; текст, равный маркеру, принудительно
  квотируется; маркер валидируется (непустой, без `"`/CR/LF/разделителя); Excel-защита
  добавляет `'` перед `=`/`+`/`-`/`@` после `ValueTransform` и до RFC 4180-экранирования;
  `ValueTransform` применяется только к не-NULL значениям (возврат `null` → маркер NULL);
  путь по умолчанию остаётся box-free (бокинг — только при заданном `ValueTransform`).
- Все реляционные провайдеры: PostgreSQL, SQL Server, SQLite, MySQL, MariaDB, ClickHouse.
- Форматтер CSV без боксинга на построчном пути: типизированные getters, включая числовые
  колонки SQL Server. Терминал читает их через провайдерный хук `MapTypedColumnExpression`
  (`protected virtual` на `DataContext`, переопределён в `SqlServerDataContext`):
  storage-типизированный getter (`GetFieldType(ordinal)`) + типизированный `Convert.To<T>(storage)`,
  без `object`/`IDataRecord.GetValue`/`Convert.ChangeType(object)`. В #168 общий буферизованный
  `MapColumnExpression` переведён на ту же storage-типизированную диспетчеризацию (`GetFieldType`
  в рантайме, общий с CSV хук `GetNumericGetter`/`GetTypedConversion`) и больше не боксит числовые
  SQL Server; CSV-хук и общий путь теперь делят один маппинг (см. (c)).
- Неизвестный или неконвертируемый storage-тип числовой колонки SQL Server бросает
  `NotSupportedException` **до записи заголовка** и любой строки данных.
- Публичное поведение и ограничения: `docs/guide/28-streaming-data.md` + `docs/ru/guide/28-streaming-data.md`,
  строка в `docs/advanced/limitations.md` + `docs/ru/advanced/limitations.md`.
- **Fail-closed ленивого temp-источника `AsTempTable`.** `DataContext.PrepareResultCommand`
  проверяет `QueryCommand.HasTemporaryTableSource()` и бросает `NotSupportedException`
  **до записи заголовка** и любой строки данных; батч `DROP` + `CREATE TEMPORARY TABLE ... AS SELECT`
  + чтение (не одна инструкция) не выполняется, частичного файла не остаётся
  (`src/nextorm.core/DataContext/DataContext.cs:448-450`). Покрыто
  `tests/nextorm.sqlite.tests/CsvStreamTests.cs:410-437`.
- **CSV в контексте in-memory — `NotSupportedException`.** У in-memory-контекста нет
  `DbDataReader`, терминал отклоняется (как `ToDataReader`) без буферизованного фолбэка.
  Покрыто `tests/nextorm.integration.tests/CommonTestSuite.Csv.cs:255-274`.

## 2. Открытые срезы (все — `1.0.9-b`)

### (a) Компиляция колонкового плана CSV не кэшируется

План столбцов компилируется на каждый вызов; результат не попадает в кэш планов
(подготовка идёт с `storeInCache: false`). Для повторяющихся мелких экспортов это лишняя
компиляция одного и того же плана.

- Триггер: повторяющиеся мелкие экспорты; добавить кэш плана с ключом
  (форма проекции, провайдер, опции).

### (b) Memory-bound бинарных (`byte[]`) полей — частично закрыто (поддерживаемый direct-binary sequential-путь)

**Отгружено** для прямого хранимого `byte[]`-столбца на провайдере с подтверждённым
sequential access (PostgreSQL, SQL Server, SQLite): поле стримится через
`IDataRecord.GetBytes` фиксированными 12-KiB input-буферами и кодируется в Base64
3-байтными группами; пиковая память — O(буфер) и не зависит от размера поля, а
whole-field getter (`GetFieldValue<byte[]>`) на этом пути не компилируется.

Остаются на прежнем буферизованном пути (поле читается целиком, Base64 — построчно,
память ограничена наибольшим полем/строкой): MySQL/MariaDB/ClickHouse (нет
подтверждённого sequential access), а также на любом провайдере — bound-параметр,
функция/expression-проекция, computed-член, value-конвертер или typed mapping
провайдера, заданный `ValueTransform`, и диалект, чей разделитель или NULL-маркер
конфликтует с Base64-обрамлением. Гарантия памяти относится только к реализации CSV,
не к драйверу БД и не к потоку назначения.

Файл плана сохраняется: срез закрыт только в поддерживаемой области.

- Триггеры оставшейся области: экспорт крупных бинарных колонок на
  MySQL/MariaDB/ClickHouse при появлении подтверждённой sequential/`GetBytes`
  capability; bounded-memory для bound-параметров/функций/конвертеров/transform.

### (c) Общая буферизованная материализация (не CSV) — закрыто в #168

Общий путь буферизованной материализации больше не боксит числовые значения SQL Server:
`SqlServerDataContext.MapColumnExpression` в #168 перешёл на рантайм-диспетчеризацию по
`GetFieldType` и storage-типизированные геттеры, разделив `GetNumericGetter`/`GetTypedConversion`
с CSV-хуком `MapTypedColumnExpression` (§1). Бокинг-фолбэк `GetValue`/`Convert.ChangeType`
остался только для storage-типа вне закрытого числового набора. Нулевой бокинг и отсутствие
регрессии по времени подтверждены paired-бенчмарком (`BenchmarkCategory("acceptance")`).

## 3. Источники

- `src/nextorm.core/Query/QueryCommandExtensions.cs`,
  `src/nextorm.core/Builders/EntityBuilderExtensions.cs`,
  `src/nextorm.core/Query/Csv/`, `src/nextorm.core/Query/CsvStreamOptions.cs`.
- `docs/guide/28-streaming-data.md`, `docs/ru/guide/28-streaming-data.md`,
  `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md`.
