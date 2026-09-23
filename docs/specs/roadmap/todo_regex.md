# TODO: трансляция `Regex` в запросах (`Regex.IsMatch`/`Regex.Replace`)

> Рабочий план (design RFC). Источник — **G7** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md):
> `linq2db#698` (`area: extensions`, `area: sql`). Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** в предикате/проекции транслировать `System.Text.RegularExpressions.Regex` в нативный
  regex провайдера: `Regex.IsMatch(value, pattern[, options])` / `regex.IsMatch(value)`,
  `Regex.Replace(value, pattern, replacement[, options])` / `regex.Replace(value, replacement)`.
- **Критерий приёмки:** константный паттерн в LINQ даёт нативный SQL на PG/MySQL/MariaDB/ClickHouse/
  SQLite (при регистрации функции) и корректно отклоняется на SQL Server; `RegexOptions.IgnoreCase`
  и `^(…)$`-анкера отображаются; in-memory использует `System.Text.RegularExpressions`.
- **Не про TVF:** `regexp_matches`/`regexp_split_to_table` как **источники** уже есть
  (`SqlFunctions.Postgres.cs:578-592`); здесь — про функции-предикаты/скаляры.

## 2. Почему это нужно

1. nextorm умеет `LIKE`-семейство (`like`/`contains`/`freetext`), но не `Regex`; сложные паттерны
   приходится обходить `LIKE`/ручными `PostgresFunctions.regexp_*`.
2. linq2db транслирует `Regex.IsMatch`/`Regex.Replace` (`#698`), у nextorm — нет.
3. Провайдерные ядра regex различаются (RE2 у ClickHouse, POSIX/advanced у PG, ICU у MySQL 8) —
   важно явно зафиксировать границы переносимости, а не обещать одинаковую семантику.

## 3. Текущее состояние (проверено по коду)

- Трансляции `Regex` нет: `StringFunctionTranslator.cs:30-75` содержит только `_ToUpper`/`_Substring`/
  `_Replace`/`_Contains`/… и не знает `Regex`.
- PG уже имеет regex-функции: `regexp_replace`, `regexp_like`, `regexp_split_to_array`,
  `regexp_count`, `regexp_instr` (`SqlFunctions.Postgres.cs:386-404`) и TVF
  `regexp_matches`/`regexp_split_to_table` (`:574-592`, `IRegexpMatchesRow`/`IRegexpSplitToTableRow`).
- `PostgresDialect.cs:205,220` регистрирует `regexp_matches`/`regexp_split_to_table` как TVF.
- SQL Server нативного regex не имеет; SQLite `REGEXP` — оператор, требующий регистрации функции.

## 4. Матрица провайдеров

Источники: PostgreSQL 18 §9.7 (pattern matching); MySQL/MariaDB reference (`REGEXP*`); sqlite.org
(`REGEXP` operator); ClickHouse string functions; MS Learn (regex отсутствует).

| Провайдер | match | replace | case-insensitive | Источник |
|---|---|---|---|---|
| PostgreSQL | `value ~ pattern` (partial), `~*` (ci), `!~`/`!~*` | `regexp_replace(value, pattern, replacement[, flags])` | `~*` или флаг `i` | PG 18 §9.7 |
| MySQL | `value REGEXP pattern` (`RLIKE`), `REGEXP_LIKE` (8.0.4+) | `REGEXP_REPLACE` (8.0+) | `REGEXP_LIKE(..., 'i')`/ICU | MySQL ref |
| MariaDB | `value REGEXP pattern` (`RLIKE`) | `REGEXP_REPLACE` (10.0.5+) | `(?i)`/флаги | MariaDB KB |
| SQL Server | — (нет regex; `LIKE`/`PATINDEX` — не regex) | — | — | MS Learn |
| ClickHouse | `match(haystack, pattern)` (RE2) | `replaceRegexpAll`/`replaceRegexpOne` | `(?i)`-префикс паттерна | ClickHouse string functions |
| SQLite | `value REGEXP pattern` — **нужна регистрация** `regexp()` через `sqlite3_create_function` | `regexp_replace` (не встроена) | по функции | sqlite.org |
| InMemory | `Regex.IsMatch` | `Regex.Replace` | `RegexOptions` | BCL |

**Единообразие:** `SupportsRegex` (default `false`) + `MakeRegexMatch`/`MakeRegexReplace`. PG/MySQL/
MariaDB/ClickHouse — включены; **SQL Server гейтится off** (нет нативного regex, `LIKE`-эвристика даст
неверную семантику); SQLite — только если провайдер зарегистрирует `regexp()` (см. §9).

## 5. Ближайший CLR-аналог и тир

- Аналог — `System.Text.RegularExpressions.Regex` (BCL). Тир **(a)**: новый визитор
  `RegexSqlTranslator` (или ветка в `StringFunctionTranslator`), публичного API не добавляем.
- Паттерн/опции требуют быть **константными** (как `date_trunc` field); неконстантный паттерн —
  `NotSupportedException` (regex нельзя биндить как параметр у части провайдеров, а семантика
  компиляции C#-regex≠SQL-regex).

## 6. Дизайн и публичный API

```csharp
// dialect
bool SupportsRegex { get; }                         // default false
string MakeRegexMatch(string value, string pattern, bool ignoreCase);
string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase);
```

- Визитор распознаёт `Regex.IsMatch` (static/instance) и `Regex.Replace`; берёт константный
  `pattern`/`replacement` и читает `RegexOptions.IgnoreCase` (прочие опции либо маппятся, либо
  отклоняются).
- Маппинг: PG `~*`/`~` + `regexp_replace` с `'i'`; MySQL `REGEXP_LIKE`/`REGEXP_REPLACE`; ClickHouse
  `match` + `replaceRegexpAll` с `(?i)`; SQLite — `regexp(value, pattern)` (если зарегистрирована).
- In-memory: `Regex.IsMatch`/`Regex.Replace` в CLR.
- Публичного API не добавляем; опционально `CommonFunctions.RegexMatch(value, pattern, ignoreCase)`
  как escape-hatch, если понадобится неконстантный паттерн (фаза 2).

## 7. Этапы внедрения

1. `SupportsRegex`/`MakeRegexMatch` + визитор `IsMatch`; PG/MySQL/MariaDB/ClickHouse.
2. `Replace` (`MakeRegexReplace`) + `IgnoreCase`.
3. SQLite: регистрация `regexp()` в провайдере (или явный gate-off + `NotSupportedException`).
4. SQL Server: задокументировать отсутствие (gate-off) либо эвристики `LIKE` для простых паттернов — отдельно.

## 8. План тестов

- SQL-gen (`tests/nextorm.<provider>.tests/SqlGenerationTests.cs`): `IsMatch`/`Replace` с/без
  `IgnoreCase`, константный паттерн; SQL Server — `ShouldThrow`.
- Диалект: `<Provider>DialectTests` — gate и рендер.
- Интеграция (`CommonTestSuite.Functions.cs`): `IsMatch`-фильтр и `Replace`-проекция на PG/MySQL/
  MariaDB/ClickHouse/SQLite; `*SpecificTests.cs` — отличия ядер regex.
- In-memory: `tests/nextorm.core.tests/InMemoryTests.cs`.

## 9. Открытые вопросы

1. Границы переносимости: RE2 (ClickHouse) не поддерживает backreferences/lookaround — отклонять или
   документировать?
2. Частичное совпадение (`~`/`match` ищут подстроку) vs полное (`^(…)$`) — какая семантика `IsMatch`?
3. `RegexOptions` (IgnoreCase/Multiline/Singleline) — какие маппить, какие отклонять.
4. `Regex.Match` (значение) и группы захвата — фаза 2 или вне scope.
5. Неконстантный паттерн/`Regex`-инстанс из поля — поддерживать ли через `CommonFunctions.RegexMatch`.
6. SQLite: регистрировать `regexp()` (расширение) или просто gate-off.

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Visitors/RegexSqlTranslator.cs`.
- Правки: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `Dialect/SqlDialectBase.cs`,
  `Visitors/BaseExpressionVisitor.cs` (вызов визитора), `nextorm.postgres/PostgresDialect.cs`,
  `nextorm.mysql/MySqlDialect.cs`, `nextorm.mariadb/MariaDbDialect.cs`,
  `nextorm.clickhouse/ClickHouseDialect.cs`, `nextorm.sqlite/SqliteDialect.cs` (+ `SQLiteFunctions`).
- Доки: `docs/guide/11-scalar-functions.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.
