# Таблица сопоставления провайдеров

| Возможность | SQLite | SQL Server | PostgreSQL |
|---|---|---|---|
| `ToUpper` / `ToLower` | `upper` / `lower` | `upper` / `lower` | `upper` / `lower` |
| `Length` | `length` | `len` | `length` |
| `Substring` | `substring`, 1-based | `substring`, 1-based | `substring`, 1-based |
| `Trim` / `TrimStart` / `TrimEnd` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` |
| `Replace` | `replace` | `replace` | `replace` |
| `Contains` / `StartsWith` / `EndsWith` / `like` | `like` (escape `\`) | `like` (escape `\`) | `like` (escape `\`) |
| `string.IsNullOrEmpty` | `(x is null or x = '')` | `(x is null or x = '')` | `(x is null or x = '')` |
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| Столбец с `[Collation]` | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `NotSupportedException` | `s ~ ...` / `regexp_replace(...)` |
| `Abs` | `abs` | `abs` | `abs` |
| `Round` | `round(x)` | `round(x, 0)` | `round(x)` |
| `Truncate` | `trunc` | `round(x, 0, 1)` | `trunc` |
| `Log` (натуральный) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` и т. д. | `extract(year from ...)` и т. д. |
| `??` объединение | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| Конкатенация строк (`+`) | `\|\|` | `+` | `\|\|` |
| Логический предикат как значение | без изменений | `cast(case when ... then 1 else 0 end as bit)` | без изменений |
| Массивы (`any`/`all`, функции массивов) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Текстовый JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | поддерживается | поддерживается | поддерживается |
| `greatest` / `least` | `max(...)` / `min(...)` (один аргумент -> `(...)`) | поддерживается (2022+) | поддерживается |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (только ClickHouse; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | поддерживается |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / разность `strftime` / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | интервальная арифметика / `date_trunc` / разность частей даты / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (нет `array_agg`) | `string_agg` (2017+); `array_agg` бросает исключение | поддерживается |
| `filter (where ...)` у агрегатов | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Расширенная библиотека скалярных функций (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Логические/битовые/статистические агрегаты | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Упорядоченные агрегаты (`percentile_cont`, ...) | `NotSupportedException` | оконный `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Встроенные табличные функции | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

Провайдер in-memory не рендерит SQL: он компилирует и вычисляет выражение для строк в памяти, поэтому
выполняется сам метод .NET. Приведённая выше матрица SQL относится к провайдерам SQLite, SQL Server и
PostgreSQL.

ClickHouse рендерит `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (и масштабированный
`addYears` для `decade`/`century`/`millennium`), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, комбинаторы `-If` и `multiIf`. Он отклоняет ANSI-предложение
`filter (where ...)`, агрегаты `regr_*` и логические агрегаты через `NotSupportedException`; см.
[Провайдер ClickHouse](../providers/clickhouse.md).

## Явно не поддерживается

Эти случаи бросают `NotSupportedException`, а не генерируют SQL с другой семантикой:

* `string.IsNullOrWhiteSpace(x)` — бросает исключение с сообщением, упоминающим `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` — у двухаргументной формы порядок аргументов зависит от провайдера, поэтому она
  оставлена неподдерживаемой (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:985`). Двухаргументная форма PostgreSQL доступна как
  `SqlFunctions.Postgres.log(base, x)` в расширенной библиотеке скалярных функций.
* Перегрузки `Math.Round`, принимающие `MidpointRounding` (больше двух аргументов), — не переносимы.
* `string.Substring(Range)` — нет эквивалента в SQL.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` — SQL обрезает только пробельные символы, не
  произвольный набор символов.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` с культурой, отличной от
  `CultureInfo.InvariantCulture` — переносимую форму имеет только инвариантный upper/lower.
* `string.Compare(a, b)` и `string.Compare(a, b, bool)` без `StringComparison` — culture-sensitive;
  используйте `string.Compare(a, b, StringComparison.Ordinal)` или `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (с `IgnoreCase` или без) — нет переносимой формы.
* Спецификаторы `string.Format`/`ToString(format)` вне документированного подмножества в
  [Форматировании дат и чисел в строки](03-date-and-time.md#форматирование-дат-и-чисел-в-строки) — никогда не
  отбрасываются.
* `Regex` в SQL Server — движка регулярных выражений нет; используйте `SqlFunctions.Sql.like` для
  простых шаблонов (см. [Регулярные выражения](01-string-functions.md#регулярные-выражения)).
* `Regex`-шаблон, замена или `RegexOptions`, не являющиеся константой времени компиляции; опция
  `RegexOptions`, отличная от `IgnoreCase` (при этом `Compiled`/`CultureInvariant` — no-op); прочие
  члены `Regex`, кроме `IsMatch`/`Replace` (например, `Match`, `Split`, группы захвата).
