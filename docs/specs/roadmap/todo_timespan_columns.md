# TODO: `TimeSpan`/interval-колонки и точность дат (`[Duration]`)
> Tracking issue: [#72](https://github.com/AlexeyShirshov/nextorm/issues/72).

> Рабочий план (design RFC). Источник — **G9** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md):
> `linq2db#5759` (`TimeSpan`-члены и сравнения на native interval-колонках, `[Duration]`),
> `#4306`/`#2950` (`TimeSpan`-типы на SQL Server/PostgreSQL). Сюда же сведены все находки по
> датам/точности из того же разбора: `#5961`/G20 (ширина `date_diff`), `#5965` (sub-day
> date-функции над date-only операндами), `#5914` (`DateTimeOffset`). Публичный API → обновить
> `docs/specs/design/API-NAMING-REVIEW.md`.

> **Статус (1.0.6-alpha):** этапы 1–4 реализованы. Сквозной duration-round-trip проверен на
> PostgreSQL/MySQL/SQLServer/SQLite (`CommonTestSuite.Duration.cs`) и ClickHouse
> (`ClickHouseIntegrationTests.DurationColumns`, nullable-колонка через `MakeNullableDurationType`);
> MariaDB покрыт диалектными/SQL-gen тестами (в integration-матрице провайдера нет). Смежные фиксы:
> `DateTimeOffset` read; sub-day promotion (`date_add`/`DateTime.Add*`); `date_diff_big` (`long?`);
> PG `make_interval` (сдвиг `weeks`) + интеграционный тест interval-функций. Известный дефект:
> PG `current_time()` (см. §8.7). Публичная документация — `docs/guide/26-duration-columns.md` (+RU)
> и `docs/guide/11-scalar-functions.md` (+RU).
> Отклонение от матрицы §4: ClickHouse хранит целочисленный `Int64`+unit, а не `Interval*`
> (драйвер не отдаёт `Interval` как `TimeSpan`; см. §9.3).

## 1. Пункт и цель

- **Ядро (G9):** дать `TimeSpan`-свойству сущности смысл «длительность с объявленной единицей»:
  маппинг на native interval/`TIME`-колонку у тех, кто её имеет, либо на целочисленную колонку
  (тики/секунды/…) с объявленной единицей; корректное чтение/запись, сравнения и арифметика.
- **Критерий приёмки:** `From<T>()`/`InsertInto`/`Update` с `TimeSpan`-свойством работает на
  PostgreSQL (native `interval`), MySQL/MariaDB (`TIME`), ClickHouse (`Interval*`/`Int64`), SQL Server
  и SQLite (declared-unit integer); `TimeSpan` больше не падает при материализации; сравнение
  (`x.Dur > TimeSpan.FromMinutes(5)`) транслируется; in-memory работает через тот же materializer.
- **Сопутствующее (вынесено из разбора):** закрыть `TimeSpan`-read-путь (сейчас падает на всех SQL
  провайдерах), продвижение операнда для sub-day date-функций (`#5965`), ширину `date_diff` (G20),
  решить судьбу `DateTimeOffset` (`#5914`).

## 2. Почему это нужно

1. **`TimeSpan` вообще не читается.** `SelectExpression.GetDataRecordMethod()`
   (`src/nextorm.core/Expressions/SelectExpression.cs:78-151`) не имеет ветки `typeof(TimeSpan)` и
   бросает `NotSupportedException`; SQL Server-маппер обрабатывает только numeric
   (`SqlServerDataContext.cs:71-76`). При этом `PostgresFunctions` уже объявляет `TimeSpan`-функции
   (`make_interval`, `justify_days`/`justify_hours`, `current_time`, `localtime`,
   `SqlFunctions.Postgres.cs:407-443`) — они покрыты **только SQL-gen**
   (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:2791-2829`) и при материализации упадут.
2. **Нет объявленной единицы/точности.** `IPropertyMetadata`
   (`DataContext/Meta/IPropertyMetadata.cs:17-57`) не несёт ни unit, ни precision; `MakeTypeName`
   (`SqlDialectBase.cs:356`) не знает `TimeSpan`.
3. **Нет native interval-поверхности.** `TimeSpan` не участвует в арифметике/сравнениях; linq2db
   закрывает это атрибутом `[Duration]` и interval-колонками.
4. **Точность дат — общий корень.** Sub-day функции и `date_diff` упираются в отсутствие
   type/precision-метаданных (см. §8).

## 3. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Read | `Expressions/SelectExpression.cs:78-151` | нет ветки `TimeSpan` → `NotSupportedException` |
| Read (SQL Server) | `SqlServerDataContext.cs:71-99` | numeric-only, `TimeSpan` уходит в base → throw |
| Метаданные | `DataContext/Meta/IPropertyMetadata.cs:17-57` | нет `DurationUnit`/`Precision`/SQL-типа |
| Тип | `SqlDialectBase.MakeTypeName:356` | `TimeSpan` → `type.Name` (неверно) |
| PG-поверхность | `SqlFunctions.Postgres.cs:407-443` | `TimeSpan?`-функции есть, но не материализуются |
| In-memory | `InMemoryDataContext.MapColumn:255` | возвращает выражение напрямую → `TimeSpan` работает |
| Визиторы | `DateTimeFunctionTranslator.cs`, `MemberTranslator.cs:62-82` | `DateTime`-члены; `TimeSpan`-членов нет |

## 4. Матрица провайдеров (native-формы)

Источники: PostgreSQL 18 doc §8.5/§9.9; MS Learn date/time data types + `DATEADD`/`DATEPART`;
MariaDB KB `TIME` (MySQL — та же семантика); ClickHouse Interval; `sqlite.org/lang_datefunc`.

| Провайдер | native тип | Гранулярность / диапазон | Чтение | Запись | Арифметика | Сравнение |
|---|---|---|---|---|---|---|
| PostgreSQL | `interval` | months+days+microseconds; precision `p` 0–6 | Npgsql `interval`↔`TimeSpan` (месяцы в `TimeSpan` не влезают) | параметр `NpgsqlInterval`/`TimeSpan` | `date ± interval`, `make_interval`, `justify_days/hours` | обычные операторы |
| SQL Server | нет duration-типа; есть `time` | `time` = 00:00:00.0000000–23:59:59.9999999 (time-of-day, не >24ч), precision 0–7 | `time`→`TimeSpan` (SqlClient); либо тики из `bigint` | `TimeSpan`→`time`; иначе тики | `DATEADD`; разность `DATEDIFF_BIG` | `time` сравним |
| MySQL | `TIME` | `-838:59:59.999999`..`838:59:59.999999`, precision 0–6 | `TimeSpan` из `TIME` | `TimeSpan`→`TIME` | `TIMEDIFF`/`ADDTIME`/`TIME_TO_SEC`/`SEC_TO_TIME` | обычные операторы |
| MariaDB | `TIME` | то же, что MySQL | то же | то же | то же | то же |
| ClickHouse | `IntervalX` (special) | unsigned + kind; единицы **несопоставимы** между собой; колонка фиксирует unit | `IntervalMillisecond` и пр. | `toIntervalX(n)` | `Date/DateTime ± Interval` | `toIntervalMs(…) < toIntervalMinute(…)` |
| SQLite | нет date/time storage class | TEXT / REAL (julian) / INTEGER (unix) | `TimeSpan.FromTicks` из INTEGER | ticks/seconds INTEGER | `datetime(x, '+N unit')`; `timediff()` 3.43+ | числовое |
| InMemory | CLR `TimeSpan` | полная | прямое значение | прямое значение | LINQ/CLR | CLR |

## 5. Единообразие провайдеров

- **Cross-provider** (tier b): одна публичная поверхность `TimeSpan`-колонки + атрибут/fluent с
  объявленной единицей. Native-форма — `Make*`-хук на диалект: PG `interval`, MySQL/MariaDB `TIME`,
  ClickHouse `Interval*`; SQL Server/SQLite/ClickHouse-без-unit — целочисленная колонка с unit.
- **Gating:** `SupportsDurationColumns` (default `false`) + `SupportsDurationUnit(unit)`; PG отдаёт
  native `interval`, MySQL/MariaDB `TIME`, остальные — declared-unit integer; неподдержанную
  комбинацию отклонять понятным `NotSupportedException`, а не молча.
- **Оговорки:** SQL Server `time` — time-of-day, `TimeSpan` >24ч не представим нативно (для
  длительностей — тики); ClickHouse `Interval*` пишет только unsigned и единица фиксирована колонкой;
  PG `interval` хранит месяцы, которые `TimeSpan` не выражает (документировать/гейтить).

## 6. Ближайший CLR-аналог и тир

- CLR-аналог — `System.TimeSpan` (сам по себе), BCL→SQL маппинга нет.
- Тир **(b)**: новый `CommonFunctions`/dialect-хук + атрибут; `[SqlFunction]` не подходит (нужны
  операторы/точность, а не простой name-swap). `TimeSpan`-члены (`Duration()`, `TotalSeconds` и т.п.)
  можно добавить в `DateTimeFunctionTranslator`-соседа отдельным визитором.

## 7. Дизайн и публичный API

```csharp
public enum DurationUnit { Ticks, Milliseconds, Microseconds, Seconds, Minutes, Hours, Days }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DurationAttribute : Attribute
{
    public DurationAttribute(DurationUnit unit) { }
    public int Precision { get; set; }
}
```

- Fluent: `EntityPropertyBuilder<T>.Duration(DurationUnit unit, int? precision = null)`.
- Метаданные: `IPropertyMetadata.DurationUnit`/`Precision` (default `null`/`0` — как
  `IsColumnNameAuto`); проброс из `EntityMetadataBuilder` (атрибут + интерфейсный fallback, как
  `ColumnAttribute`).
- Диалект: `string MakeDurationType(Type type, DurationUnit unit, int precision)` (default — integer),
  `string? MakeDurationLiteral(...)` (для native-форм), gate `SupportsDurationColumns`.
- Read: ветка `TimeSpan` в `GetDataRecordMethod()` → `GetFieldValue<TimeSpan>` (native PG/MySQL/CH)
  либо чтение integer + `TimeSpan.From…` (declared-unit). SQL Server `time` — `GetFieldValue<TimeSpan>`.
- Write: параметр `DbParameter` из `TimeSpan` (native) либо конвертация в declared-unit integer
  (общая точка — `SqlMutationBuilder.cs:801`, где уже строится `Parameter`).
- Сравнения/арифметика: на native-провайдерах — прямые операторы; declared-unit — целочисленные.
- In-memory: ничего дополнительно (уже работает).

## 8. Смежные находки (вынесены из разбора linq2db)

### 8.1. `TimeSpan` не читается вообще — блокер
См. §3/§7. Пока нет read-пути, существующая PG-поверхность (`make_interval`/`justify_*`/`current_time`/
`localtime`) недостижима при материализации. Закрывается первым шагом §9.

### 8.2. Sub-day date-функции над date-only операндами (`linq2db#5965`)
nextorm не хранит SQL-тип/точность колонки и не продвигает операнд: `date_add`/`DateTime.Add*` и
`.Hour/.Minute/.Second` рендерятся прямо на колонке (`SqlServerDialect.cs:351` →
`dateadd(millisecond, n, value)`, `ClickHouseDialect.cs:531` → `addMilliseconds(value, n)`). На SQL Server
`date`-колонка даёт 9810, на ClickHouse `Date` sub-day часть, вероятно, молча теряется (класс
`#5955`/`#5959`). PG/MySQL/SQLite через interval/`datetime()` безопасны. `DateTime.Millisecond` вообще
не маппится.
**✅ Реализовано 24.09.2026 (без новых метаданных).** Добавлен хук
`ISqlDialect.PromoteDateOperand(field, value)` (DIM) + `SqlDialectBase` virtual: для sub-day полей
(`hour`/`minute`/`second`/`milliseconds`/`microseconds`) операнд `date_add`/`DateTime.Add*` продвигается
перед вызовом хука — SQL Server `cast(value as datetime2)`, ClickHouse `toDateTime` (hour..second) и
`toDateTime64(value, 3|6)` (milliseconds/microseconds); PG/MySQL/SQLite возвращают значение как есть.
Полноценные precision-метаданные (§8.5) остаются открытыми для отдельного прохода.

### 8.3. Ширина `date_diff` (G20, из `#5961`)
`CommonFunctions.date_diff` объявлен `int?` (`Query/SqlFunctions.cs:525`), но ClickHouse
`dateDiff('unit', …)` → Int64 (`ClickHouseDialect.cs:556`), а `SelectExpression.GetDataRecordMethod`
читает `GetInt32` → переполнение на `milliseconds` (>~24.8 дн) / `microseconds` (>~35.8 мин). SQL
Server/PG base отдают `int` (PG кастит явно) — расхождения нет; MySQL/MariaDB `timestampdiff` — тот
же класс. Решение (аддитивное, не ломает API):
**✅ Реализовано 24.09.2026.** Добавлен `SqlFunctions.Sql.date_diff_big(field, start, end) → long?` с
хуком `ISqlDialect.MakeDateDiffBig` (DIM → `MakeDateDiff`): SQL Server рендерит `datediff_big`, PG
расширяет под-дневной cast до `bigint` (`PostgresDialect.MakeDateDiffBig` → `MakeDateDiffCore(..., big:true)`),
остальные делегируют `MakeDateDiff` (ClickHouse/MySQL уже Int64). Старый `date_diff` остаётся `int?` —
задокументированное ограничение, для широких диапазонов использовать `date_diff_big`. Добавлены
SQL-gen/диалектные/интеграционные тесты (ClickHouse `date_diff`/`date_diff_big`, common suite `date_diff_big`).

### 8.4. `DateTimeOffset` не поддержан (`linq2db#5914`)
`DateTimeOffset` в `src/` не встречается; свойство упадёт в `GetDataRecordMethod`. Расхождение
linq2db (Date vs DateTime64 на границе полуночи) поэтому не воспроизводится, но при добавлении
поддержки нужно **осознанно** выбрать конвенцию (`value.UtcDateTime.Date` — следует за инстантом, как
datetime-ветки, либо `value.Date` — как `DateTimeOffset.Date` в CLR) и зафиксировать это у конвертера.

### 8.5. Общий корень — type/precision-метаданные колонки
`#5965` (sub-day), `#5961` (ширина), `TimeSpan`-unit и `DateTimeOffset` — всё упирается в отсутствие
`DataType`/`Precision`/unit на `IPropertyMetadata`. Ввести их одним расширением (совместимо с G1
value-converters) и переиспользовать.

### 8.6. PG `make_interval` — сдвиг параметров (найдено при тесте материализации)
**✅ Исправлено 24.09.2026.** C#-поверхность `SqlFunctions.Postgres.make_interval(years, months, days,
hours, minutes, seconds)` не содержит PostgreSQL-параметр `weeks`, а ранжирование шло «как есть»,
из-за чего 3-й аргумент попадал в `weeks`, и интервал получался другим (напр. `make_interval(0,0,20,…)`
трактовался как 20 недель → 4 месяца 20 дней). `ExtendedScalarFunctionTranslator.EmitMakeInterval`
теперь рендерит `make_interval(y, mo, 0, d, h, mi, s)`; сигнатура не менялась (не breaking).

### 8.7. PG `current_time()` не материализуется (известный дефект)
`SqlFunctions.Postgres.current_time()` объявлен `TimeSpan?`, но PostgreSQL возвращает `time with time
zone`, который Npgsql не читает как `TimeSpan` (`InvalidCastException: ... DataTypeName 'time with time
zone'`). `localtime()` (тип `time`) материализуется корректно и в тесте покрыт. Исправление потребует
смены типа возврата на `DateTimeOffset?` (breaking) либо отдельного метода — вынесено отдельно; пока
задокументировано и исключено из теста.

## 9. Этапы внедрения

1. **Read-путь `TimeSpan`** — ✅ реализовано. Ветка `TimeSpan` в `GetDataRecordMethod`
   (`GetFieldValue<TimeSpan>`) + integer-путь в `RowMapperFactory.MapColumn`
   (`supportsNativeDuration == false`): `GetValue` → `long` → `DurationStorage.FromStorage`.
   `DateTimeOffset` тоже получил read-ветку (`GetFieldValue<DateTimeOffset>`).
2. **Метаданные unit/precision** — ✅ реализовано. `DurationUnit`, `DurationAttribute`
   (`Unit`/`Precision`), `EntityPropertyBuilder<T>.Duration(unit, precision)`,
   `IPropertyMetadata.DurationUnit/DurationPrecision` (DIM-дефолты),
   `EntityMetadataBuilder` (атрибут + interface-fallback), `MakeDurationType` + gate
   `SupportsNativeDuration`. `MakeDurationType`/`Precision` — публичная поверхность для
   генераторов DDL (в самом движке DDL не генерируется).
3. **Native-диалекты** — ✅ реализовано. PG `interval`(`p`), MySQL/MariaDB `TIME`(`p`);
   SQL Server/SQLite/ClickHouse — целочисленная колонка (тики по умолчанию). Запись
   нормализуется в `SqlMutationBuilder`/`QueryPlanner`/`SqlSourceRenderer`/bulk-пути;
   сравнения конвертируют `TimeSpan`-константу через `DurationUnitContext` (с восстановлением
   контекста, чтобы единица не «протекала»). Сквозной round-trip проверен на
   PostgreSQL/MySQL/SQLServer/SQLite (`CommonTestSuite.Duration.cs`, который наследуют эти четыре
   провайдера) и ClickHouse (`ClickHouseIntegrationTests.DurationColumns_ShouldRoundTrip` через общий
   `CommonTestSuite.DurationRoundTrip`; nullable-колонка создаётся как `Nullable(Int64)` через новый
   `MakeNullableDurationType`, иначе ClickHouse-`Int64` хранит NULL как 0). MariaDB покрыт
   диалектными и SQL-gen тестами: в integration-матрице провайдера нет (нет контейнера/провайдера),
   но `MariaDbDialect : MySqlDialect`, поэтому SQL идентичен MySQL.
4. **Смежные фиксы — ✅ реализовано.**
   - **8.4 `DateTimeOffset`** — ✅ read-путь закрыт (`GetFieldValue<DateTimeOffset>`),
     type-name на PG/MySQL/SQL Server/ClickHouse; конвенция по дате не навязывается.
   - **8.2 sub-day operand promotion** — ✅ sub-day-операнд (`hour`/`minute`/`second`/`milliseconds`/
     `microseconds`) `date_add`/`DateTime.Add*` продвигается хуком `ISqlDialect.PromoteDateOperand`:
     SQL Server `cast(value as datetime2)`, ClickHouse `toDateTime`/`toDateTime64(value, 3|6)`;
     PG/MySQL/SQLite не меняются. Precision-метаданные (§8.5) — отдельный проход.
   - **8.3 ширина `date_diff`** — ✅ аддитивно: `date_diff_big(field, start, end) → long?` +
     `ISqlDialect.MakeDateDiffBig` (SQL Server `datediff_big`, PG `bigint` через `MakeDateDiffCore`,
     остальные делегируют `MakeDateDiff`); `date_diff` остаётся `int?`. Тесты ClickHouse
     `date_diff`/`date_diff_big` + common integration.

5. **PG interval-поверхность и nullable-тип — ✅ реализовано.**
   - **8.6 `make_interval`** — ✅ исправлен сдвиг параметров: C#-сигнатура без `weeks`, рендер
     вставляет литерал `0` в позицию PostgreSQL-`weeks` (`EmitMakeInterval`).
   - **Nullable duration DDL** — ✅ `ISqlDialect.MakeNullableDurationType` (DIM → `MakeDurationType`),
     ClickHouse → `Nullable(Int64)`.
   - **Интеграционный тест** `PostgresFunctionsTests.IntervalFunctions_ShouldMaterialiseTimeSpan`:
     `make_interval`/`justify_hours`/`justify_days`/`localtime` материализуются в `TimeSpan?`.
   - **8.7 `current_time()`** — ⚠ известный дефект (не исправлен): PG `timetz` не читается Npgsql как
     `TimeSpan`; в тесте исключён, см. §8.7.

### Прочее

- Известные ограничения после аудита: unit не несёт вычисляемая проекция длительности
  (читается как тики) — задокументировано в guide; non-native чтение использует
  `GetValue`+`Convert.ToInt64` (бокс на строку×колонку) — под будущий замер.

## 10. План тестов и покрытие

- SQL-gen (`tests/nextorm.<provider>.tests/SqlGenerationTests.cs`): `[Duration]`-свойство в
  проекции/INSERT/UPDATE; native форма на PG/MySQL/MariaDB/ClickHouse, integer — SQL Server/SQLite.
- Диалектные хуки: `<Provider>DialectTests.cs` — `MakeDurationType`/gate.
- Sub-day/ширина: SQL-gen `date_add`/`AddMilliseconds` (promotion-каст), `date_diff_big`; диалектные
  `PromoteDateOperand`/`MakeDateDiffBig`; common integration `date_add("hour",…)`/`AddHours`/
  `date_diff_big("milliseconds", 1970, …)` + ClickHouse-специфичные.
- Интеграция: `tests/nextorm.integration.tests/CommonTestSuite.Duration.cs` — round-trip `TimeSpan`,
  сравнение, арифметика, `null`, отрицательные значения, precision; `*SpecificTests.cs` для
  native-отличий; `ClickHouseIntegrationTests.DurationColumns_ShouldRoundTrip` — ClickHouse через общий
  `DurationRoundTrip` (`Nullable(Int64)` для nullable-колонки).
- Core/in-memory: `tests/nextorm.core.tests/InMemoryTests.cs`.
- Mixed: PG `make_interval`/`justify_hours`/`justify_days`/`localtime` материализуются
  (`PostgresFunctionsTests.IntervalFunctions_ShouldMaterialiseTimeSpan`); `current_time()` — известный
  дефект (§8.7), из теста исключён.
- Покрытие: `coverage.settings.xml` включает только `nextorm.{core,sqlite,postgres,sqlserver}` — шаг 1
  и PG-часть двигают число, ClickHouse/MySQL/MariaDB-часть — **нет** (зафиксировать явно).

## 11. Открытые вопросы

1. Порядок: сначала read-путь `TimeSpan` (§9.1) или сразу метаданные unit (§9.2)?
2. SQL Server `TimeSpan`: нативный `time` (только time-of-day) или declared-unit `bigint` тики?
3. ClickHouse: `Interval*`-колонка (unsigned, фиксированный unit) или `Int64` + unit?
4. PG `interval` хранит месяцы, которых нет в `TimeSpan` — отклонять/документировать?
5. Имя атрибута: `[Duration]` (как linq2db) vs `[TimeSpanColumn]`/`[IntervalColumn]`; единицы enum.
6. `date_diff`: ✅ решено аддитивно — `date_diff` остаётся `int?`, добавлен `date_diff_big → long?` (см. §8.3).
7. Нужен ли `DateTimeOffset` вообще; если да — какая конвенция Date vs DateTime64.
8. Сводить ли всё в один PR или разбить: read-путь / metadata / диалекты / смежные фиксы.

## 12. Файлы к изменению

- Read: `src/nextorm.core/Expressions/SelectExpression.cs`, `DataContext/RowMapperFactory.cs`,
  `src/nextorm.sqlserver/SqlServerDataContext.cs`.
- Метаданные/API: `DataContext/Meta/IPropertyMetadata.cs`, `Meta/Implementation/PropertyMetadata.cs`,
  `Meta/EntityMetadataBuilder.cs`, `Meta/EntityPropertyBuilder.cs`, новый `DurationAttribute.cs`.
- Диалекты: `DataContext/Dialect/ISqlDialect.cs`, `Dialect/SqlDialectBase.cs`, `nextorm.postgres/
  PostgresDialect.cs`, `nextorm.mysql/MySqlDialect.cs`, `nextorm.mariadb/*`, `nextorm.clickhouse/
  ClickHouseDialect.cs`, `nextorm.sqlserver/SqlServerDialect.cs`, `nextorm.sqlite/SqliteDialect.cs`.
- Функции/визиторы: `Query/SqlFunctions.cs` (при необходимости), `Visitors/DateTimeFunctionTranslator.cs`,
  `Visitors/BuiltinFunctionTranslator.cs` (`date_diff` ширина), `Visitors/DateTimeFunctionTranslator.cs`
  (sub-day promotion).
- Доки: `docs/guide/11-scalar-functions.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **0 блокеров**; 1 открытая реализованная деградация (бокс) с триггером; нужна ресинхронизация §5/§7 плана с кодом.

- **[PERF] 🟡** Non-native чтение duration боксует на каждой строке×колонке: `RowMapperFactory.cs:44-45` `GetValueMI` → `object`, затем `Convert.ToInt64(object)`. Для non-native провайдеров колонка всегда целочисленная (`SqlDialectBase.cs:107` → `bigint`; ClickHouse `Int64`), доступен типизированный `GetInt64`. Deferred с триггером (замер аллокаций материализации, `:231-232`): эмитить `GetInt64(index)` + `DurationStorage.FromStorage`.
- **[DRY]/[TYPE] ℹ️** План рассинхронизирован с реализованным API (§5 `:82-90`, §7 `:112-121`): обещаны `SupportsDurationColumns` + `SupportsDurationUnit(unit)` + `MakeDurationLiteral(...)`, фактически `SupportsNativeDuration` (один bool) + `MakeDurationType`/`MakeNullableDurationType` (`ISqlDialect.cs:314-338`, `SqlDialectBase.cs:105-111`); per-unit гейта и literal-хука нет, промис про `NotSupportedException` для неподдержанных комбинаций не реализован (напр. PG `interval` с месяцами). Fix: привести §5/§7 к фактическому API.
- **[TYPE]/[SRP] ℹ️** `DurationUnitContext` — скрытое изменяемое состояние визитора (`BaseExpressionVisitor.cs:75`; save/restore вручную `PredicateTranslator.cs:349-422`); unit «протекает» при любом новом пути, забывшем восстановить контекст. Deferred с триггером (новый путь, эмитящий `TimeSpan`-константу): передавать unit явно либо добавить assert/регресс.
- **[ISP] ℹ️** `IPropertyMetadata` — 9 ортогональных опциональных капабилити (`IPropertyMetadata.cs:17-85`; план добавил `DurationUnit`/`DurationPrecision`). DIM-дефолты сохраняют source-compat; дробление по F7/F12 не оправдано (единый потребитель) — не переоткрывать. Fix: сохранить DIM-паттерн для будущего `Converter`.
- **ℹ️** Позитив: `DurationAttribute` — `sealed` (`DurationAttribute.cs:16`), `DurationUnit` — enum, `DurationStorage` — `internal static` (`DurationStorage.cs:9`); `MakeNullableDurationType` — DIM → `MakeDurationType`, без роста числа capability-интерфейсов.
