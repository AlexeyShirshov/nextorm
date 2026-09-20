# WIP: публичный `EXTRACT` / `date_part` (PostgreSQL)

> Статус: **реализовано** (`todo_postgres.md:47`, ветка `todo-pg`).

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_postgres.md:47`,
> `todo_phase2.md` → PostgreSQL — не заблокировано.

## Пункт и цель

- Пункт: **Публичный `EXTRACT`/`date_part`** (`quarter`, `week`, `epoch`, `dow`, `isodow`), ранее
  доступный только через `DateTime.Year`/`Month`/… (`MakeDatePart`).
- Провайдер-источник: PostgreSQL; конструкция кросс-провайдерная (см. матрицу).
- Критерий приёмки: `SqlFunctions.Sql.extract(part, value)` возвращает целочисленную часть даты для
  `year`/`quarter`/`month`/`week`/`day`/`doy`/`dow`/`isodow`/`hour`/`minute`/`second`; отдельный
  `SqlFunctions.Sql.date_part(part, value)` возвращает числовую часть — `epoch` (секунды с дробной
  частью). Семантика согласована по всем провайдерам: `week` — ISO 8601, `dow` — 0=воскресенье …
  6=суббота, `isodow` — 1=понедельник … 7=воскресенье, `epoch` — секунды от 1970-01-01.

## Матрица «провайдер × форма» (шаг 1)

Заполнено по официальной документации СУБД (не по коду nextorm).

| Провайдер | quarter | week (ISO) | epoch | dow (0=Sun) | isodow (1=Mon) | Источник |
| --- | --- | --- | --- | --- | --- | --- |
| PostgreSQL | `extract(quarter from x)` | `extract(week from x)` | `extract(epoch from x)` (приводится к `double precision`) | `extract(dow from x)` | `extract(isodow from x)` | https://www.postgresql.org/docs/current/functions-datetime.html |
| SQL Server | `datepart(quarter, x)` | `datepart(isowk, x)` | `cast(datediff_big(millisecond, '19700101', x) as float) / 1000.0` | `((datepart(weekday, x) + @@datefirst - 1) % 7)` | `(((datepart(weekday, x) + @@datefirst - 2) % 7) + 1)` | https://learn.microsoft.com/sql/t-sql/functions/datepart-transact-sql |
| MySQL | `quarter(x)` | `weekofyear(x)` | `cast(unix_timestamp(x) as double)` | `(dayofweek(x) - 1)` | `(weekday(x) + 1)` | https://dev.mysql.com/doc/refman/8.4/en/date-and-time-functions.html |
| MariaDB | `quarter(x)` | `weekofyear(x)` | `cast(unix_timestamp(x) as double)` | `(dayofweek(x) - 1)` | `(weekday(x) + 1)` | https://mariadb.com/kb/en/quarter/ , https://mariadb.com/kb/en/weekofyear/ , https://mariadb.com/kb/en/dayofweek/ , https://mariadb.com/kb/en/weekday/ , https://mariadb.com/kb/en/unix_timestamp/ |
| ClickHouse | `toQuarter(x)` | `toISOWeek(x)` | `toFloat64(toUnixTimestamp(x))` | `(toDayOfWeek(x) % 7)` (toDayOfWeek: 1=Mon) | `toDayOfWeek(x)` | https://clickhouse.com/docs/en/sql-reference/functions/date-time-functions |
| SQLite | `cast((cast(strftime('%m', x) as integer) + 2) / 3 as integer)` | `cast((cast(strftime('%j', date(x, '-3 days', 'weekday 4')) as integer) + 6) / 7 as integer)` | `((julianday(x) - 2440587.5) * 86400.0)` | `cast(strftime('%w', x) as integer)` | `((cast(strftime('%w', x) as integer) + 6) % 7 + 1)` | https://www.sqlite.org/lang_datefunc.html |
| InMemory | `DateTime.Quarter` нет; вычисляется CLR-движком только для агрегатов; скалярные `SqlFunctions` в in-memory не оцениваются | — | — | — | — | — |

Пояснения к формам:
- SQL Server `datepart(weekday)` зависит от `SET DATEFIRST`; нормализация выполняется арифметикой с
  `@@datefirst` (проверено по таблице возвратов документации `DATEPART`). `datepart(isowk, …)` —
  ISO-неделя, независима от `DATEFIRST`.
- MySQL/MariaDB `EXTRACT(WEEK …)` зависит от `default_week_format`, поэтому используется
  `weekofyear` (ISO 8601). `dayofweek` = 1=Sun..7=Sat, `weekday` = 0=Mon..6=Sun.
- SQLite `strftime('%W')` — не ISO (есть «нулевая» неделя); ISO-неделя вычисляется через четверг
  (`date(x,'-3 days','weekday 4')`, затем `(%j + 6) / 7`), не требуя SQLite ≥ 3.46 (`%V`).
- SQLite `%s` игнорирует дробную часть; `julianday` даёт epoch с долями.
- ClickHouse `toDayOfWeek` по умолчанию ISO (1=Monday..7=Sunday), поэтому `dow` = `toDayOfWeek % 7`.

**Единообразие провайдеров.** Все шесть SQL-провайдеров выражают все пять частей нативно с точной
семантикой, поэтому ни один не гейтится. Гейт `SupportsDatePart(part)` остаётся рабочим (база
`true` только для ANSI-частей); диалекты, которые не смогли бы выразить часть, возвращают `false` и
получают `NotSupportedException`. `InMemory` не участвует — скалярные `CommonFunctions` в нём не
оцениваются (как и `date_trunc`).

## Ближайший C#-аналог и уровень

- Готового CLR-члена для извлечения произвольной части нет. Свойства `DateTime.Year`/… уже идут
  через `MakeDatePart`, но произвольная часть задаётся строкой.
- Уровень **(b)**: новый метод `CommonFunctions` + транслятор + диалектный хук. `MakeDatePart` и
  `SupportsDatePart` расширяются (параллельный хук не вводится — по требованию скилла).

## Диалектный план

- `ISqlDialect.SupportsDatePart(string part)` — новый аддитивный гейт; `SqlDialectBase` возвращает
  `true` для ANSI-частей (`year/quarter/month/week/day/doy/hour/minute/second`).
- `MakeDatePart(part, value)` расширяется новыми ветками в `SqlServerDialect`, `MySqlDialect`,
  `SqliteDialect`, `ClickHouseDialect`; `PostgresDialect` использует базовый `extract`, добавляя
  приведение к `double precision` для `epoch`.
- `BuiltinFunctionTranslator` — ветки `extract`/`date_part` поверх одного `EmitDatePart`.
- `DateTime.DayOfWeek` не маппится (сознательно): нормализованный `dow` доступен через `extract`
  (см. `todo_mssql.md:84`).

## Публичный API

```csharp
public int? CommonFunctions.extract(string part, DateTime? value);
public double? CommonFunctions.date_part(string part, DateTime? value);
```

- `extract` — целочисленные части (`year`, `quarter`, `month`, `week`, `day`, `doy`, `dow`,
  `isodow`, `hour`, `minute`, `second`); `epoch` отклоняется с указанием на `date_part`.
- `date_part` — числовые части (`epoch`); целочисленные части отклоняются с указанием на `extract`.

## План тестов

- SQL-gen PostgreSQL (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): по одной проверке на
  `quarter`/`week`/`dow`/`isodow` и на `date_part("epoch")`; rejection для неизвестной части.
- Прямые хуки (`tests/nextorm.postgres.tests/PostgresDialectTests.cs`): `SupportsDatePart` и
  `MakeDatePart` для новых частей.
- SQL-gen остальных провайдеров (`tests/nextorm.{sqlserver,mysql,mariadb,sqlite,clickhouse}.tests/`).
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs`): реальные значения
  `quarter`/`week`/`dow`/`isodow` и `date_part("epoch")` на PostgreSQL/SQL Server/MySQL/SQLite.
- Базовая линия покрытия: снимается после сборки.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбокс), `todo_mssql.md` (устаревшая заметка о `extract`),
  `docs/guide/11-scalar-functions.md` + RU, `docs/providers/postgres.md` + RU.
