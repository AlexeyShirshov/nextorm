# WIP: ClickHouse — функции приведения и частей даты

- Пункт бэклога: `docs/specs/roadmap/todo_clickhouse.md:72-77` (уровень 1),
  `docs/specs/roadmap/todo_phase2.md:36`.
- Целевой провайдер: ClickHouse. Модель запроса не меняется.
- Критерий приёмки: `SqlFunctions.ClickHouse` предоставляет `to_date`/`to_date_time`/`to_date32`,
  `to_year`/`to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/`to_hour`/
  `to_minute`/`to_second`, `to_start_of_*`, `to_monday`, `to_yyyymm`/`to_yyyymmdd`,
  `to_unix_timestamp`; клики рендерятся в нативные имена ClickHouse; остальные провайдеры
  отвергают вызов `NotSupportedException`. `date_trunc`→`dateTrunc` и `end_of_month`→
  `toLastDayOfMonth` переиспользуются без изменений.

## Матрица «провайдер × форма»

Источники документации: ClickHouse function reference
(clickhouse.com/docs/en/sql-reference/functions/date-time-functions), PostgreSQL official function
reference (postgresql.org/docs/current/functions-datetime.html), Microsoft Learn (T-SQL
`CAST`, `DATEPART`, `DATETRUNC`, `DATEFROMPARTS`, `DATEDIFF`), MySQL 8.4
`Date and time functions`, MariaDB `Date and Time Functions`, SQLite `Date And Time Functions`.

| Функция | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite | InMemory |
|---|---|---|---|---|---|---|---|
| приведение к дате | `x::date` | `CAST(x AS date)` | `DATE(x)` | `DATE(x)` | `toDate(x)` | `date(x)` | не выражается в SQL; in-memory не поддерживает |
| приведение к дате/времени | `x::timestamp` | `CAST(x AS datetime2)` | `CAST(x AS DATETIME)` | `CAST(x AS DATETIME)` | `toDateTime(x)` | `datetime(x)` | — |
| `toDate32` | — (нет типа Date32) | — | — | — | `toDate32(x)` | — | — |
| год | `extract(year from x)` | `DATEPART(year,x)` | `YEAR(x)` | `YEAR(x)` | `toYear(x)` | `strftime('%Y',x)` | — |
| квартал | `extract(quarter from x)` | `DATEPART(quarter,x)` | `QUARTER(x)` | `QUARTER(x)` | `toQuarter(x)` | — (эмулируется) | — |
| месяц | `extract(month from x)` | `DATEPART(month,x)` | `MONTH(x)` | `MONTH(x)` | `toMonth(x)` | `strftime('%m',x)` | — |
| день месяца | `extract(day from x)` | `DATEPART(day,x)` | `DAYOFMONTH(x)` | `DAYOFMONTH(x)` | `toDayOfMonth(x)` | `strftime('%d',x)` | — |
| день недели | `extract(dow from x)` (0=Sun) | `DATEPART(weekday,x)` (зависит от `DATEFIRST`) | `DAYOFWEEK(x)` (1=Sun) | `DAYOFWEEK(x)` (1=Sun) | `toDayOfWeek(x)` (1=Mon) | `strftime('%w',x)` (0=Sun) | — |
| день года | `extract(doy from x)` | `DATEPART(dayofyear,x)` | `DAYOFYEAR(x)` | `DAYOFYEAR(x)` | `toDayOfYear(x)` | `strftime('%j',x)` | — |
| час/минута/секунда | `extract(hour/minute/second from x)` | `DATEPART(hour/minute/second,x)` | `HOUR/MINUTE/SECOND(x)` | `HOUR/MINUTE/SECOND(x)` | `toHour/toMinute/toSecond(x)` | `strftime('%H/%M/%S',x)` | — |
| начало года/квартала/месяца | `date_trunc('year/quarter/month',x)` | `DATETRUNC(year/quarter/month,x)` (2022+) | `DATE_FORMAT`+`STR_TO_DATE` | то же | `toStartOfYear/Quarter/Month(x)` | `strftime`+конкатенация | — |
| начало недели | `date_trunc('week',x)` (Mon) | `DATETRUNC(week,x)` (Mon) | `DATE_SUB(DATE(x),INTERVAL WEEKDAY(x) DAY)` (Mon) | то же | `toStartOfWeek(x)` (Sun) | эмуляция | — |
| `toMonday` | `date_trunc('week',x)` | `DATETRUNC(week,x)` | `DATE_SUB(...WEEKDAY...)` | то же | `toMonday(x)` | эмуляция | — |
| начало дня/часа/минуты/секунды | `date_trunc('day/hour/minute/second',x)` | `DATETRUNC(...)` | эмуляция | эмуляция | `toStartOfDay/Hour/Minute/Second(x)` | эмуляция | — |
| `toYYYYMM`/`toYYYYMMDD` | `to_char(x,'YYYYMM'/'YYYYMMDD')` | `FORMAT(x,'yyyyMM'/'yyyyMMdd')` | `DATE_FORMAT(x,'%Y%m'/'%Y%m%d')` | то же | `toYYYYMM`/`toYYYYMMDD(x)` | `strftime('%Y%m'/'%Y%m%d',x)` | — |
| `toUnixTimestamp` | `extract(epoch from x)` | `DATEDIFF_BIG(second,'1970-01-01',x)` | `UNIX_TIMESTAMP(x)` | `UNIX_TIMESTAMP(x)` | `toUnixTimestamp(x)` | `strftime('%s',x)` | — |

Вывод по единообразию: набор — **ClickHouse-идиоматический** (единый `to*`-нейминг, включая
`toDate32`/`toYYYYMM`/`toMonday`/`toStartOfWeek`, которых нет как точных аналогов на ≥2 провайдерах,
а `toDayOfWeek` имеет провайдер-специфичную базу отсчёта). Точные кросс-провайдерные формы уже
покрыты `DateTime.*`/`date_trunc`/`date_add`/`end_of_month`; здесь добавляется ClickHouse-поверхность,
как `uniq`/`JSONExtract`/`numbers`. Флаг `SupportsDateConversionFunctions` (default `false`; ClickHouse
`true`) гейтит всю поверхность; не-ClickHouse-диалекты бросают `NotSupportedException` с понятным
сообщением.

## C#-аналог и уровень реализации

- Части даты переиспользуют существующий хук `MakeDatePart` (расширен для ClickHouse:
  `toYear`/`toMonth`/…), конверсии/начала периодов/`toYYYYMM`/`toUnixTimestamp` — новый хук
  `MakeDateConversion`. Tier (b): новые методы `ClickHouseFunctions` + ветки транслятора.
- `end_of_month` (→ `toLastDayOfMonth`) и `date_trunc` (→ `dateTrunc`) не дублируются.

## Диалектный план

- `ISqlDialect`: `bool SupportsDateConversionFunctions { get; }`,
  `string MakeDateConversion(string name, IReadOnlyList<string> args)`.
- `SqlDialectBase`: флаг `false`, `MakeDateConversion` бросает `NotSupportedException`.
- `ClickHouseDialect`: флаг `true`; `MakeDateConversion` маппит snake_case → camelCase, оборачивает
  `toYYYYMM`/`toYYYYMMDD` в `toInt32`, `toUnixTimestamp` — в `toInt64`; `MakeDatePart` расширен
  на `toYear`/`toQuarter`/`toMonth`/`toDayOfMonth`/`toDayOfWeek`/`toHour`/`toMinute`/`toSecond`.

## Публичный API

- `DateTime? ClickHouseFunctions.to_date<T>(T? value)`, `to_date_time`, `to_date32`;
- `int to_year<T>`/`to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/`to_day_of_year`/
  `to_hour`/`to_minute`/`to_second`;
- `DateTime? to_start_of_year<T>`/`_quarter`/`_month`/`_week`/`_day`/`_hour`/`_minute`/`_second`,
  `to_monday<T>`;
- `int to_yyyymm<T>`, `int to_yyyymmdd<T>`, `long to_unix_timestamp<T>`.

## План тестов

- SQL-gen (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`): `DateConversionFunctions_*`.
- Хуки (`ClickHouseDialectTests.cs`): `MakeDateConversion_*`, обновление `MakeDatePart`-ожидания.
- Rejection (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): `DateConversionFunctions_*`.
- Интеграция (`ClickHouseIntegrationTests.cs`): реальный ClickHouse, значения.
- In-memory: поверхность ClickHouse-only, in-memory не участвует.
- Покрытие: `coverage.settings.xml` не включает `nextorm.clickhouse`, поэтому число не изменится;
  SQL-gen тесты всё равно обязательны.

## Документация

`docs/providers/clickhouse.md` (EN) + `docs/ru/providers/clickhouse.md` (RU), `docs/providers/overview.md`
(+RU) при необходимости, `docs/advanced/limitations.md` (+RU), `docs/guide/11-scalar-functions.md`
(+RU), `docs/advanced/api-reference.md` (+RU), `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
`docs/specs/roadmap/todo_clickhouse.md`, `docs/specs/roadmap/todo_phase2.md`.
