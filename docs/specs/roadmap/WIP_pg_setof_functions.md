# WIP: наборные функции PostgreSQL через `[SqlTableFunction]`

> Статус: **реализовано** (ветка `todo-pg2`, item 1).

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_phase2.md` → PostgreSQL
> (`Наборные функции через [SqlTableFunction]`), `todo_postgres.md:51,65,121-125,142-144`.

## Пункт и цель

- Пункт: **set-returning (табличные) функции PostgreSQL**, заведённые как встроенные через
  `[SqlTableFunction]` + `FromTableFunction`: `regexp_matches`, `regexp_split_to_table`,
  `jsonb_array_elements`, `jsonb_array_elements_text`, `jsonb_each`, `jsonb_each_text`,
  `jsonb_object_keys`, `jsonb_path_query`, `ts_stat`.
- Провайдер-источник: PostgreSQL. Конструкции не выражаются одинаково у остальных провайдеров
  (у части есть аналоги, но не как табличная функция в `FROM`) — см. матрицу.
- Критерий приёмки: `SqlFunctions.Postgres.<fn>(...)` внутри `ctx.FromTableFunction(...)`
  рендерится как `[schema.]<fn>(args)` и исполняется на реальном PostgreSQL; у остальных
  провайдеров `SupportsTableFunction(<fn>)` = `false` и обращение бросает `NotSupportedException`.

## Матрица «провайдер × форма» (шаг 1)

Заполнена по официальной документации СУБД (ссылки в колонке «Источник»). Значение `—` означает,
что функции в этой форме у провайдера нет; аналог (если есть) описан в колонке.

| Провайдер | `regexp_matches` | `regexp_split_to_table` | `jsonb_array_elements(_text)` | `jsonb_each(_text)` | `jsonb_object_keys` | `jsonb_path_query` | `ts_stat` | Источник |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **PostgreSQL** | **да**: `setof text[]`, один столбец `regexp_matches` | **да**: `setof text`, столбец `regexp_split_to_table` | **да**: `setof jsonb` / `setof text`, столбец `value` | **да**: `setof record(key text, value jsonb\|text)` | **да**: `setof text`, столбец `jsonb_object_keys` | **да**: `setof jsonb`, столбец `jsonb_path_query` | **да**: `setof record(word text, ndoc int, nentry int)` | pg docs: string-functions, functions-json, functions-textsearch |
| **SQL Server** | — нет regexp-движка | — (аналог `STRING_SPLIT`, но без regexp; уже `SqlFunctions.SqlServer.string_split`) | — (аналог `OPENJSON`, уже заведён, но другой row-shape) | — (аналог `OPENJSON` default schema: `key`/`value`/`type`) | — (тот же `OPENJSON`) | — (`JSON_QUERY`/`JSON_VALUE` — скаляры, не setof) | — (`sys.dm_fts_index_keywords` — DMV, не TVF в `FROM`) | learn.microsoft: string-functions, json-functions, full-text |
| **MySQL** | — (есть `REGEXP_SUBSTR`, но скаляр) | — (нет set-returning; `JSON_TABLE` — другой row-shape) | — (`JSON_EXTRACT`/`JSON_TABLE`, разбор массива требует JSON_TABLE) | — (`JSON_TABLE` с колонками; другой синтаксис) | — (`JSON_KEYS` возвращает массив, не setof) | — (`JSON_EXTRACT` с wildcard возвращает массив) | — (`INFORMATION_SCHEMA.INNODB_FT_INDEX_*` — таблицы) | dev.mysql: regexp, json-functions, fulltext |
| **MariaDB** | как MySQL | как MySQL | как MySQL | как MySQL | как MySQL | как MySQL | как MySQL | mariadb.com/kb: regexp, json, fulltext |
| **ClickHouse** | — (`extract`/`match` — скаляры) | — (`splitByChar` возвращает `Array(String)`; `arrayJoin` разворачивает скалярный массив, не TVF) | — (`JSONExtractArrayRaw` возвращает `Array`, row reader его не материализует) | — (`JSONExtractKeysAndValues`, тот же array-блокер) | — (`JSONExtractKeys`, array-блокер) | — (`JSON_QUERY` нового типа `JSON`, не setof) | — (нет `ts_stat`; `tokens`/`splitByNonAlpha` — скаляры/arrays) | clickhouse.com/docs/en/sql-reference/functions |
| **SQLite** | — (нет regexp из коробки) | — (нет; `json_each` — аналог разворачивания JSON) | — (`json_each` возвращает `key`/`value`/`type`/... для массива и объекта) | — (`json_each`) | — (`json_each`/`json_tree`) | — (`json_extract` с `$[#]`, скаляр) | — | sqlite.org: lang_corefunc, json1 |
| **InMemory** | — (наборных `SqlFunctions` in-memory не оценивает) | — | — | — | — | — | — | — |

**Единообразие провайдеров.** Все восемь конструкций — PostgreSQL-only и остаются на
`PostgresFunctions`:
- `regexp_matches` / `regexp_split_to_table` — regex-специфичны; у SQL Server regexp нет, у
  MySQL/MariaDB/ClickHouse/PostgreSQL язык шаблонов разный, `STRING_SPLIT`/`splitByChar` не regexp.
- `jsonb_*` setof-функции — у SQL Server/MySQL есть аналоги (`OPENJSON`/`JSON_TABLE`/`JSON_KEYS`), но
  это **другой row-shape и другой синтаксис**, а не та же табличная функция; они не сводятся к одному
  кросс-провайдерному методу (и уже частично заведены отдельными поверхностями).
- `jsonb_path_query` — JSONPath-форма PostgreSQL; SQL Server/MySQL делают JSONPath скалярами, а
  ClickHouse — новым `JSON`-типом.
- `ts_stat` — полнотекстовая статистика; у остальных это DMV/`INFORMATION_SCHEMA`/отсутствует.

Гейт: `PostgresDialect.SupportsTableFunction(name)` возвращает `true` для имён всех восьми функций;
база (`SqlDialectBase`) и остальные диалекты — `false`, поэтому `SqlSourceRenderer.MakeTableFunction`
бросает `NotSupportedException` с именем функции. Пользовательский `[SqlTableFunction]` этим гейтом не
ограничен (он не на типе `CommonFunctions`).

## Ближайший C#-аналог и уровень

- CLR-аналога нет (возвращают последовательность строк, а не скаляр).
- Уровень **(b)**: методы на `PostgresFunctions` с `[SqlTableFunction]` + row-интерфейсы рядом с
  `IGenerateSeriesRow`/`IUnnestRow<T>`; диалектный гейт `SupportsTableFunction`. Новых
  `Make*`/`Supports*`-флагов не требуется — используется существующий строковый гейт по имени.
- `jsonb_path_query` требует `jsonpath`-аргумента: добавлен скалярный хелпер
  `PostgresFunctions.jsonpath(string?)`, транслируемый в `cast(<arg> as jsonpath)` (как это уже делает
  `JsonSqlTranslator.EmitJsonPathFunction` для `jsonb_path_query_first`).
- `regexp_matches` возвращает `setof text[]`. Чтобы материализовать `text[]`, в
  `SelectExpression.GetDataRecordMethod` добавлена ветка `string[] -> GetValueMI` (по аналогии с
  `byte[]`): Npgsql `GetValue` для `text[]` возвращает `string[]`. Это расширение row reader только
  для `string[]`; общая материализация `Array(T)`/`Tuple` (отдельный заблокированный пункт триажа)
  не затрагивается.

## Диалектный план

- `PostgresDialect.SupportsTableFunction`: добавить имена `regexp_matches`, `regexp_split_to_table`,
  `jsonb_array_elements`, `jsonb_array_elements_text`, `jsonb_each`, `jsonb_each_text`,
  `jsonb_object_keys`, `jsonb_path_query`, `ts_stat` к существующим `generate_series`/`unnest`.
- `SqlDialectBase.SupportsTableFunction(string)` остаётся `false`.
- `WrapTableFunction` не нужен: столбцы материализуются row reader напрямую (jsonb → `string` через
  Npgsql, как у `jsonb_agg`).
- `JsonSqlTranslator`: ветка `PostgresFunctions.jsonpath` → `cast(<arg> as jsonpath)`.

## Публичный API

```csharp
// row-shape (nested в SqlFunctions)
public interface IRegexpMatchesRow { [Column("regexp_matches")] string[] Matches { get; set; } }
public interface IRegexpSplitToTableRow { [Column("regexp_split_to_table")] string? Value { get; set; } }
public interface IJsonArrayElementsRow { [Column("value")] string? Value { get; set; } }
public interface IJsonbEachRow { [Column("key")] string? Key { get; set; } [Column("value")] string? Value { get; set; } }
public interface IJsonObjectKeysRow { [Column("jsonb_object_keys")] string? Key { get; set; } }
public interface IJsonPathQueryRow { [Column("jsonb_path_query")] string? Value { get; set; } }
public interface ITsStatRow { [Column("word")] string? Word; [Column("ndoc")] int? Ndoc; [Column("nentry")] int? Nentry; }

// PostgresFunctions
public IQueryable<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern);
public IQueryable<SqlFunctions.IRegexpMatchesRow> regexp_matches(string? source, string? pattern, string? flags);
public IQueryable<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern);
public IQueryable<SqlFunctions.IRegexpSplitToTableRow> regexp_split_to_table(string? source, string? pattern, string? flags);
public IQueryable<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements(object? json);
public IQueryable<SqlFunctions.IJsonArrayElementsRow> jsonb_array_elements_text(object? json);
public IQueryable<SqlFunctions.IJsonbEachRow> jsonb_each(object? json);
public IQueryable<SqlFunctions.IJsonbEachRow> jsonb_each_text(object? json);
public IQueryable<SqlFunctions.IJsonObjectKeysRow> jsonb_object_keys(object? json);
public IQueryable<SqlFunctions.IJsonPathQueryRow> jsonb_path_query(object? json, string? path);
public IQueryable<SqlFunctions.ITsStatRow> ts_stat(string? query);
public string? jsonpath(string? path);
```

Все члены получают `<summary>`; row-интерфейсы — рядом с существующими. Регистр
`API-NAMING-REVIEW.md` пополняется точным аудитом (P2/RD2-продолжение, как у предыдущих TVF).

## План тестов

- SQL-gen (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): по одному/несколько вызовов на
  функцию, проверка текста `[schema.]name(args)` и колонок проекции.
- Гейт (`tests/nextorm.postgres.tests/PostgresDialectTests.cs`): `SupportsTableFunction` для новых имён.
- Rejection (SQLite/SQL Server/MySQL/MariaDB/ClickHouse): обращение к PG-наборной функции бросает
  `NotSupportedException` с именем функции.
- Интеграция (`tests/nextorm.integration.tests/PostgresSpecificTests.cs`): реальные вызовы
  `regexp_match`-подобных, `jsonb_*` и `ts_stat` на контейнерном PostgreSQL.
- Покрытие: базовое снять после сборки; `regexp_matches`/`string[]` — новая ветка
  `SelectExpression`.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбоксы), `docs/specs/roadmap/todo_phase2.md` (строка PG),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
- `docs/guide/13-table-valued-functions.md` + RU, `docs/providers/postgres.md` + RU.
- `docs/specs/design/API-NAMING-REVIEW.md` (регистр).
