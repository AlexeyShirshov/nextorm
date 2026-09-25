# TODO: Кросс-провайдерные строковые/числовые обёртки `SqlFunctions.Sql`
> Tracking issue: [#77](https://github.com/AlexeyShirshov/nextorm/issues/77).

> **Статус: отгружено (shipped).** Реализовано как `IScalarFunctions` (per-name `Supports`/`Render`)
> на `ISqlDialect`, члены `CommonFunctions` `left`/`right`/`lpad`/`rpad`/`repeat`/`reverse`/`space`/
> `concat_ws`/`translate`/`ascii`/`@char`, in-memory через CLR-эквиваленты.
> **Пост-ревью правка (YAGNI):** `mod`/`log10`/`power` из RFC §4–§5 **не отгружены** — их дублируют
> уже транслируемые BCL-пути (`%`, `Math.Log10`, `Math.Pow`); попутно исправлен `Math.Pow` на SQL Server
> (`pow` → `POWER`). Ниже исходный RFC сохранён исторически, перечисляя в т.ч. эти три члена.
> Публичные доки: [Scalar functions](../../guide/11-scalar-functions.md#cross-provider-scalar-functions)
> (+RU). Остаточные follow-up (P2) — в `docs/specs/design/code-smells-review.md` (Находки 182–185) и
> `docs/specs/design/API-NAMING-REVIEW.md` (SC1–SC5).

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **Cross-provider**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«Cross-provider»); заводится по пункту §4.36
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) (regex-часть item 35 отгружена и перенесена в §5.35).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** перенести в кросс-провайдерную поверхность `SqlFunctions.Sql` (`CommonFunctions`)
  функции, которые уже есть на ≥3 провайдерах нативной формой, но сегодня достижимы только через
  `SqlFunctions.Postgres.*`, сырой фрагмент или `[SqlFunction]`-UDF:
  `left`/`right`, `lpad`/`rpad`, `repeat`, `reverse`, `space`, `concat_ws`, `translate`,
  `ascii`/`char`, `mod`, `log10`, `power`.
- **Почему:** это «single highest-leverage change» (см. gap-analysis §Cross-provider): одна обёртка
  «зажигает» несколько провайдеров. Сейчас `PostgresFunctions` — единственная поверхность с этими
  обёртками, а «второй набор» для SQL Server/MySQL/MariaDB/SQLite/ClickHouse отсутствует, хотя
  функции там есть.
- **Критерий приёмки:** `SqlFunctions.Sql.left(x, 3)` и остальные члены из списка транслируются в
  нативную форму каждого способного провайдера; на провайдере без формы — понятный
  `NotSupportedException`; in-memory вычисляет те, у которых есть точная CLR-семантика, и бросает на
  остальных; SQL-gen тесты на каждый провайдер + `CommonTestSuite`.
- **Не входит:** `format` (документировано как непереносимое из-за несовместимых языков шаблонов,
  см. §6 ниже и `sql-capabilities-gap-analysis.md` §5.15); вторая волна (`bit_length`/`octet_length`,
  `ln`, `cot`, `degrees`/`radians`, `pi`, `acos`/`asin`/`atan`/`atan2`, `date_bin`/`age`/`timediff`,
  `now`/`current_timestamp`, `bit_and`/`bit_or`/`bit_xor`, `bool_and`/`bool_or`/`every`, `any_value`,
  `mode`) — отдельный follow-up.

## 2. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Публичная поверхность | `src/nextorm.core/Query/SqlFunctions.cs` (`CommonFunctions`) | строковых/числовых скаляров нет: только `like`/`@in`/`greatest`/`least`/`iif`/`nullif`/`date_*`/агрегаты/окна |
| PG-обёртки | `src/nextorm.core/Query/SqlFunctions.Postgres.cs` | есть `left`/`right`/`lpad`/`rpad`/`repeat`/`reverse`/`translate`/`concat_ws`/`format`/`mod`/`log(double,double)`/… + `SupportsExtendedScalarFunctions` |
| Диалект-хуки | `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs` | семейного рендера скаляров нет; `SupportsExtendedScalarFunctions` = PG-only |
| Трансляторы | `src/nextorm.core/Visitors/StringFunctionTranslator.cs`, `MathFunctionTranslator.cs` | CLR-члены (`Substring`, `PadLeft`, `Math.Pow`, `Math.Log`, …) — без `SqlFunctions.Sql`-форм |
| Прочие провайдеры | `src/nextorm.{sqlserver,mysql,mariadb,sqlite,clickhouse}` | поверхностей `*Functions` для строк/чисел нет (ClickHouse — только массивы/JSON/даты) |

## 3. Матрица провайдеров (провайдер × форма)

Источники: PostgreSQL 18 function reference (`functions-string`, `functions-math`);
Microsoft Learn T-SQL functions (string / mathematical, проверено MCP `mslearn`: `CONCAT_WS` — 2017+,
`TRANSLATE` — 2017+, `PATINDEX`/`SOUNDEX`/`DIFFERENCE`/`STRING_ESCAPE`; `JSON_*AGG` — 2025);
MySQL 8.4 built-in function reference; MariaDB built-in functions; SQLite `lang_corefunc` /
`lang_mathfunc`; ClickHouse functions (string / arithmetic).

| Функция | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite | Решение |
|---|---|---|---|---|---|---|---|
| `left(s,n)` | `left` | `LEFT` | `LEFT` | `LEFT` | `left` | `substr(s,1,n)` (эмуляция) | обёртка; SQLite — эмуляция |
| `right(s,n)` | `right` | `RIGHT` | `RIGHT` | `RIGHT` | `right` | `substr(s,-n)` (эмуляция) | обёртка; SQLite — эмуляция |
| `lpad`/`rpad` | `lpad`/`rpad` | — нативной нет (`RIGHT(REPLICATE(pad,n)+s,n)`) | `LPAD`/`RPAD` | `LPAD`/`RPAD` | `leftPad`/`rightPad` | — (нет) | обёртка; SQL Server — эмуляция, SQLite — gate |
| `repeat(s,n)` | `repeat` | `REPLICATE` | `REPEAT` | `REPEAT` | `repeat` | — (нет) | обёртка; SQLite — gate |
| `reverse(s)` | `reverse` | `REVERSE` | `REVERSE` | `REVERSE` | `reverse` | — (нет) | обёртка; SQLite — gate |
| `space(n)` | `repeat(' ',n)` (эмуляция) | `SPACE` | `SPACE` | `SPACE` | `space` | — (нет) | обёртка; PG — `repeat(' ',n)`, SQLite — gate |
| `concat_ws(sep,…)` | `concat_ws` | `CONCAT_WS` (2017+) | `CONCAT_WS` | `CONCAT_WS` | `concatWithSeparator` | `concat_ws` (3.44+) | обёртка |
| `translate(s,from,to)` | `translate` | `TRANSLATE` (2017+) | — (нет) | — (нет; только Oracle-mode `TRANSLATE`) | `translate` | — (нет) | обёртка; MySQL/MariaDB/SQLite — gate |
| `ascii(s)`/`char(n)` | `ascii`/`chr` | `ASCII`/`CHAR` | `ASCII`/`CHAR` | `ASCII`/`CHAR` | `char` (`ascii` ≈ `toUInt8(substring(s,1,1))`, уточнить) | `unicode`/`char` | обёртка; ClickHouse `ascii` — gate, если точной формы нет |
| `mod(a,b)` | `mod` | `%` | `MOD` | `MOD` | `modulo` | `%` | обёртка |
| `log10(x)` | `log(x)` (base 10) | `LOG10` | `LOG10` | `LOG10` | `log10` | `log10` (math ext) | обёртка / CLR `Math.Log10` |
| `power(x,y)` | `power` | `POWER` | `POW`/`POWER` | `POW`/`POWER` | `pow`/`power` | `pow`/`power` (math ext) | обёртка; уже достижимо через `Math.Pow` (tier a) |

**Единообразие провайдеров:** функции, которые выражают ≥2 провайдера, идут в `CommonFunctions` +
`Make*`-хук; каждая неподдерживаемая ячейка — `Supports(name) == false` и `NotSupportedException`
с явным сообщением. **Не гейтить семейным флагом**: у `ISqlDialect` добавляется объект-возможность
`IScalarFunctionRenderer` (по образцу `ISessionInfoFunctions`) с `bool Supports(string name)` и
`string Render(string name, IReadOnlyList<string> args)`, чтобы решение принималось per-функция
(`lpad` есть у 4 из 7, `translate` — у 3 из 7, …).

## 4. Ближайший CLR-аналог и тиры

- `power` → **tier (a)**: `Math.Pow` уже транслируется в `POWER`/`POW`; обёртку `SqlFunctions.Sql.power`
  добавляем только для симметрии поверхности (низкий приоритет).
- `log10` → **tier (a)** для `Math.Log10` (добавить ветку в `MathFunctionTranslator`; сейчас есть
  только натуральный `Math.Log`) + опциональная обёртка `SqlFunctions.Sql.log10`.
- `mod` → **tier (a)/(b)**: `%`-оператор уже рендерится; обёртка нужна для явного имени и
  согласованной семантики отрицательных операндов.
- `left`/`right`/`lpad`/`rpad`/`repeat`/`space` → **tier (b)**: `string.Substring`/`PadLeft`/`PadRight`/
  `new string(c,n)` **не** эквивалентны (Substring кидает/усекает, `PadLeft` не усекает, `lpad`
  усекает при `len < s.Length`), поэтому CLR-маппинг запрещён — именно поэтому семейство попало в gap.
- `reverse`/`translate`/`ascii`/`char`/`concat_ws` → **tier (b)**: точного BCL аналога нет.
- `[SqlFunction]` (tier c) не используется: нужна вариативность/разная форма по провайдерам.

## 5. Дизайн и публичный API

Новые члены `CommonFunctions` (имена зеркалят SQL-токены; XML-doc обязателен, CS1591):

```csharp
public static string? left(string? value, int n);
public static string? right(string? value, int n);
public static string? lpad(string? value, int length, string? pad = " ");
public static string? rpad(string? value, int length, string? pad = " ");
public static string? repeat(string? value, int count);
public static string? reverse(string? value);
public static string? space(int count);
public static string? concat_ws(string? separator, params object?[] values);
public static string? translate(string? value, string? from, string? to);
public static int? ascii(string? value);
public static string? @char(int code);              // `char` — ключевое слово C#
public static T? mod<T>(T? a, T? b);
public static double? log10(double? value);
public static double? power(double? value, double? exponent);
```

- Вариативные (`concat_ws`) и перегрузки с `params` рендерятся в трансляторе в одну строку SQL.
- `char` объявляется как `@char`, в SQL уходит как `CHAR`/`chr`/`char`.
- Имена `left`/`right`/`space`/`reverse` конфликтуют с CLR-методами `string.Left`? — нет; конфликтов с
  `object` нет. Проверить `API-NAMING-REVIEW.md` P1-10 (имена зеркалят SQL-токены — согласованный стиль).
- In-memory: точная реализация только там, где BCL совпадает семантически (`reverse`, `space`,
  `repeat` через `new string(...)`, `concat_ws`, `mod` как `%`, `log10`, `power`, `ascii`);
  `left`/`right`/`lpad`/`rpad` — воспроизвести SQL-семантику усечения вручную; `translate` — как
  BCL-независимая реализации; иначе `NotSupportedException`.

## 6. Диалект-план

- `ISqlDialect`: новое свойство `IScalarFunctionRenderer? ScalarFunctions { get; }` (default `null`
  → `NotSupportedException`), по образцу `ISessionInfoFunctions`.
- `SqlDialectBase`: `ScalarFunctions => null`.
- Реализации:
  - `PostgresDialect` → `left`/`right`/`lpad`/`rpad`/`repeat`/`reverse`/`translate`/`concat_ws`/`mod`
    (native), `space` → `repeat(' ', n)`, `log10` → `log(x)`, `ascii` → `ascii`, `char` → `chr`,
    `power` → `power`.
  - `SqlServerDialect` → `LEFT`/`RIGHT`/`CONCAT_WS`/`TRANSLATE`/`ASCII`/`CHAR`/`LOG10`/`POWER`,
    `lpad`/`rpad` → `RIGHT(REPLICATE(pad,n)+s,n)`, `mod` → `%`, `space` → `SPACE`;
    `repeat` → `REPLICATE`, `reverse` → `REVERSE`.
  - `MySqlDialect` → `LEFT`/`RIGHT`/`LPAD`/`RPAD`/`REPEAT`/`REVERSE`/`SPACE`/`CONCAT_WS`/`ASCII`/
    `CHAR`/`MOD`/`LOG10`/`POW`; `translate`/`ascii`-edge — gate; `char` → `CHAR`.
  - `MariaDbDialect` (наследует MySQL) — то же; `translate` gate.
  - `ClickHouseDialect` → `left`/`right`/`leftPad`/`rightPad`/`repeat`/`reverse`/`space`/
    `concatWithSeparator`/`translate`/`char`/`modulo`/`log10`/`pow`; `ascii` — уточнить/гейт.
  - `SqliteDialect` → `left`/`right` через `substr`, `mod` через `%`, `log10`/`power` (math ext),
    `concat_ws` (3.44+), `ascii`/`char` через `unicode`/`char`; `lpad`/`rpad`/`repeat`/`reverse`/
    `space`/`translate` — gate (`Supports` = false).
- `format` **не** добавляем в кросс-провайдерную поверхность: языки шаблонов PG (`to_char`), .NET
  (`FORMAT`) и `%`-строки (`strftime`/`DATE_FORMAT`/`formatDateTime`) несовместимы — решение
  зафиксировано в `docs/providers/overview.md` «Provider differences» и gap-analysis §5.15.
  `SqlFunctions.Postgres.format` остаётся PG-only.

## 7. Этапы внедрения

1. `IScalarFunctionRenderer` + `SqlDialectBase` default + базовая реализация PostgreSQL; SQL-gen тесты.
2. SQL Server и MySQL/MariaDB реализации (+ эмуляции `lpad`/`rpad`, `space`); SQL-gen тесты.
3. ClickHouse и SQLite реализации/гейты; SQL-gen тесты.
4. In-memory вычисление; `CommonTestSuite`; документация EN+RU.

## 8. План тестов

- SQL-gen по провайдерам (`tests/nextorm.<provider>.tests/SqlGenerationTests.cs`): каждая функция →
  нативная форма; гейт → `Supports`/`NotSupportedException`.
- Диалект: `*DialectTests.cs` — `ScalarFunctions.Supports`/`Render` per name (`lpad` SQLite false,
  `translate` MySQL false, `ascii` ClickHouse TBD).
- Core/in-memory: `tests/nextorm.core.tests/InMemoryTests.cs` — `reverse`/`mod`/`log10`/`power`/
  `concat_ws` и `NotSupportedException` для неподдержанных.
- Интеграция: `CommonTestSuite.Functions.cs` на провайдерах с контейнерами (PostgreSQL/SQL Server/
  MySQL/ClickHouse) + SQLite локально.
- Coverage: фича трогает `nextorm.core`/`nextorm.sqlite`/`nextorm.postgres`/`nextorm.sqlserver`
  (входят в `coverage.settings.xml`), поэтому линию/бранчи отчитаться до/после; MySQL/MariaDB/
  ClickHouse-специфичные ветки числа не двигают — указать явно.

## 9. Открытые вопросы

1. Один объект `IScalarFunctionRenderer` на строки и числа или два (`IStringFunctions`/`INumericFunctions`)?
2. `left`/`right` на SQLite — эмуляция `substr` сразу или gate до соответствия? (`substr(s,-n)` не
   идентичен `right` при `n=0`.)
3. `lpad`/`rpad` на SQL Server — эмуляция `RIGHT(REPLICATE(...))` или gate?
4. `ascii` на ClickHouse: есть ли нативная форма (иначе выражать через `toUInt8(substring(s,1,1))`).
5. Нужны ли `left`/`right` вообще как отдельные обёртки, учитывая `string.Substring` (семантика
   различается на границах) — склоняемся к «да, ради предсказуемости».
6. Судьба `space` на PostgreSQL: эмуляция `repeat(' ', n)` или не заводить.

## 10. Файлы к изменению

- Правки: `src/nextorm.core/Query/SqlFunctions.cs` (`CommonFunctions`), новый
  `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs`-branch или расширение
  `BuiltinFunctionTranslator.cs`, `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`,
  `SqlDialectBase.cs`, `DialectCapabilities.cs`, все `src/nextorm.<provider>/*Dialect.cs`,
  `src/nextorm.core/DataContext/InMemoryDataContext.cs` (+ `InMemory*` хелперы).
- Тесты: `tests/nextorm.*.tests/SqlGenerationTests.cs`, `*DialectTests.cs`,
  `tests/nextorm.core.tests/InMemoryTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.Functions.cs`.
- Доки: `docs/guide/11-scalar-functions.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`,
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **план дизайн-здоров** (код отгружен как `IScalarFunctions` + `SqlFunctions.Sql`); остаточные риски — дрейф RFC и неосознанные дубликаты.

- **[DRY] ℹ️** `:127` RFC называет тип свойства `IScalarFunctionRenderer?`, фактически отгружено `IScalarFunctions? ScalarFunctions` (`DialectCapabilities.cs:340`, `ISqlDialect.cs:424`); этот же неверный пример копируют SQL Server (`todo_sqlserver_function_gaps.md:104`) и SQLite (`todo_sqlite_function_gaps.md:127`). Fix: исправить имя в RFC.
- **[TYPE] 🟡** `:111` (и §4 `:87`) `public static T? mod<T>(T? a, T? b)` без ограничения допускает `string`/`bool`/`DateTime` для `%`; в текущем `CommonFunctions` `mod` нет. Deferred (YAGNI): при реанимации ограничить `where T : INumber<T>` (совпадает с записью SC2).
- **[TYPE] ℹ️** `:83-86` обёртки `power`/`log10` дублируют CLR-пути, а `Math.Log10` уже маппится (`MathFunctionTranslator.cs:23`). Deferred: не добавлять (RFC сам помечает tier a), открывать только под конкретного не-CLR потребителя.
- **[TYPE] ℹ️** `:100-114` остаточный блок API написан `public static`, тогда как члены `CommonFunctions` — instance (`SqlFunctions.cs:530+`). Fix: привести текст RFC к отгруженной instance-форме.
