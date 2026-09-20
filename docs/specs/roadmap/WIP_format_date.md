# WIP: `FORMAT(value, formatString)` / форматирование дат

> Рабочий план по скилу `implementing-todo-features`. Источник: `todo_mssql.md:64-70`
> (дубль в `todo_phase2.md:25`).

## Пункт и цель

- Пункт бэклога `FORMAT(value, formatString)` (дата/число), уровень «простое».
- Пробел: у SQL Server `FORMAT(value, 'yyyy-MM-dd')` принимает **.NET-шаблон**, у PostgreSQL
  `to_char(value, 'YYYY-MM-DD')` — собственный шаблон, у остальных — `%`-шаблоны. Единого
  кросс-провайдерного маппинга нет; в demo-запросах пользовательский UDF `[SqlFunction("format")]`
  был убран, чтобы не маскировать пробел стабом.
- Допустимые исходы по бэклогу: **(a)** кросс-провайдерный метод `CommonFunctions.format_date(value, template)`
  с флагом/хуком, либо **(b)** явная фиксация «форматирование дат — только через `[SqlFunction]`».
- Решение: **(b) явная фиксация** (обоснование ниже). Кода не добавляем; фиксируем правило в
  документации EN/RU и закрепляем тестом на `[SqlFunction]`.

## Матрица «провайдер × форма» (шаг 1)

Заполнена по документации самих СУБД (не по коду nextorm). Формы: «дата → строка по шаблону»,
«число → строка», «язык шаблона».

| Провайдер | Функция даты | Функция числа | Язык шаблона | Источник |
| --- | --- | --- | --- | --- |
| PostgreSQL | `to_char(timestamp, text)` | `to_char(numeric, text)` | **собственный** PG-шаблон (`YYYY`, `MM`, `DD`, `HH24`, `MI`, `SS`) | https://www.postgresql.org/docs/current/functions-formatting.html (9.8) |
| SQL Server | `FORMAT(value, format[, culture])` (2012+, CLR) | `FORMAT(value, format[, culture])` | **.NET Framework** (стандартный или custom: `yyyy-MM-dd`, `N`, `C`) | MS Learn: `FORMAT (Transact-SQL)`, `Custom date and time format strings` |
| MySQL | `DATE_FORMAT(date, format)` | `FORMAT(x, d)` | **printf-подобный** (`%Y`, `%m`, `%d`, `%H`, `%i`, `%s`) | https://dev.mysql.com/doc/refman/8.4/en/date-and-time-functions.html |
| MariaDB | `DATE_FORMAT(date, format)` | `FORMAT(x, d)` | **printf-подобный** (совместим с MySQL) | https://mariadb.com/kb/en/date_format-function/ |
| ClickHouse | `formatDateTime(datetime, format[, timezone])` | `formatReadableQuantity`/`formatReadableSize` (иного нет) | **printf-подобный** (`%Y`, `%m`, `%d`, `%H`, `%M`/`%i`, `%S`) | https://clickhouse.com/docs/en/sql-reference/functions/date-time-functions |
| SQLite | `strftime(format, time-value, ...)` | `printf(format, ...)` | **strftime-подобный** (`%Y`, `%m`, `%d`, `%H`, `%M`, `%S`; `%y` отсутствует) | https://www.sqlite.org/lang_datefunc.html |
| InMemory | `DateTime.ToString(template)` (CLR) | `IFormattable.ToString(template)` (CLR) | **.NET** (тот же, что у SQL Server) | — (BCL) |

Наблюдения:

- Форматирование даты в строку умеют **все** провайдеры — значит, зонтичное «СУБД не умеет» неприменимо.
- Но язык шаблона различается **фундаментально**: `.NET` (SQL Server, InMemory), PG-шаблон, `%`-шаблон
  (MySQL/MariaDB/ClickHouse/SQLite), причём даже `%`-диалекты расходятся (SQLite не знает `%y`;
  ClickHouse/MySQL называют минуту по-разному). Один C#-литерал `template` не имеет одного смысла на
  ≥2 провайдерах.
- SQL Server `FORMAT` дополнительно: требует **CLR** (.NET Framework), не удаляется на linked server,
  недетерминирован, SQL Server **2012+**; для типа `time` двоеточие/точку надо экранировать `\`.
- PostgreSQL-половина уже есть: `SqlFunctions.Postgres.to_char` под зонтичным
  `SupportsExtendedScalarFunctions` (PG-only), но это именно PG-шаблон, а не общий.

## Решение (и почему не вариант (a))

**Явная фиксация (b): форматирование дат и чисел — только через `[SqlFunction]`, объявляемый
пользователем под конкретный диалект.**

Причины:

1. «Кросс-провайдерный `format_date(value, template)`» возможен только как **passthrough** строки
   шаблона в диалектный вызов. Тогда один и тот же C#-код даёт разный (и чаще всего неверный)
   результат: `'yyyy-MM-dd'` — валидный .NET-шаблон на SQL Server, но для PG `to_char` последовательность
   `y` = «последняя цифра года», поэтому `to_char(x, 'yyyy-MM-dd')` вернёт `4444-01-01`, а не дату.
   Это скрытая портируемость-ловушка — ровно то, от чего предостерегает скил.
2. Чтобы вариант (a) был корректен, нужно **изобрести нормализованный язык шаблонов** и транслировать
   его в 5 диалектов (с escaping `%`, обработкой литералов, разницей в токенах минут/секунд/года).
   Это новая подсистема уровня `Visitors/`, а не «простое» изменение; объём не соответствует оценке
   пункта и рискует неверными краевыми случаями без интеграционных тестов на каждом диалекте.
3. PostgreSQL `to_char` уже доступен и честно назван PG-only; остальные провайдеры дают выбрать
   нативную формулировку через UDF — это существующий, прозрачный и документированный механизм
   (`docs/specs/demodb/mssql-adventureworks.md:138-148`).
4. Бэклог прямо разрешает исход (b).

## Что значит «реализовать» для (b)

- Никаких новострок в `CommonFunctions`/`ISqlDialect`/диалектах (иначе появится ungated
  кросс-провайдерная поверхность с несовместимым шаблоном).
- Зафиксировать правило в гайде скалярных функций EN+RU и в provider-таблицах; отметить требования
  SQL Server `FORMAT` (CLR, 2012+, детерминизм, экранирование времени).
- Закрепить поддерживаемый путь SQL-gen-тестом `[SqlFunction("format")]` на SQL Server.
- Обновить `todo_mssql.md` (чекбокс + указатель), `todo_phase2.md` (строка закрыта),
  `sql-capabilities-gap-analysis.md` (формулировка про `FORMAT`).

## Диалектный план

- `Supports*`/`Make*` не добавляются: кросс-провайдерный контракт не вводится.
- `SqlServerFunctions` тоже не расширяем: пункт закрывается как «только `[SqlFunction]`».
- InMemory: не применимо (UDF-механизм — только SQL; см. `CommonTestSuite.Udf`).

## Публичный API

- Additions: **нет**. Изменений существующего API нет (extend-only не затрагивается).

## План тестов

- SQL-gen (SQL Server): `SqlGenerationTests.SqlFunction_FormatDate_ShouldEmitFormatFunction` —
  `[SqlFunction("format")]` с `DateTime`-аргументом и строковым шаблоном рендерится как
  `format(dt, 'yyyy-MM')`. База — существующий `Udf`-хелпер в
  `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`.
- Интеграционные: не требуется (поведение — существующий UDF-механизм, уже покрыт
  `CommonTestSuite.Udf.cs`).
- Coverage baseline: изменение docs+один тест, доля строк не меняется (новых строк прод-кода нет).

## Файлы документации

- `docs/guide/11-scalar-functions.md` и `docs/ru/guide/11-scalar-functions.md` — правило и SQL Server
  `FORMAT`.
- `docs/providers/sqlserver.md` и `docs/ru/providers/sqlserver.md` — отмечаем `FORMAT` как UDF-only.
- `docs/specs/roadmap/todo_mssql.md`, `todo_phase2.md`, `sql-capabilities-gap-analysis.md`.

## Итог

- Решение (b) реализовано: правило зафиксировано в EN/RU-гайде и provider-доках, чекбокс
  `todo_mssql.md` `[x]`, строка `todo_phase2.md` помечена «закрыто», gap-analysis дополнена.
- Тест `SqlGenerationTests.SqlFunction_FormatDate_ShouldEmitFormatFunction` — зелёный; полный прогон
  `tests/nextorm.sqlserver.tests` — 193/193; `dotnet build nextorm.sln -c Release` — 0/0.
- Нового прод-кода нет (изменений публичного API тоже), поэтому интеграционный прогон и покрытие не
  затронуты. WIP закрыт.
