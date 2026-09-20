# WIP: триаж провайдерных пробелов (средний/низкий риск, без блокеров)

> Управляющий отчёт по скилу `implementing-todo-features`. Оценка сложности и масштаба правок по
> оставшимся пунктам `todo_postgres.md`/`todo_mssql.md`/`todo_clickhouse.md`. Выбран набор
> «средний/низкий риск без блокеров»; на каждый пункт — отдельный `WIP_<feature>.md`.

## Методика оценки

- Опорные точки: `SelectExpression.GetDataRecordMethod()`
  (`src/nextorm.core/Expressions/SelectExpression.cs:117`) — там живёт «нет row reader»;
  `RowMapperFactory.MapColumn` (`src/nextorm.core/DataContext/RowMapperFactory.cs:24-53`) — проводка
  `object -> T` для ссылочных типов уже есть.
- Драйвер `ClickHouse.Driver 1.4.0` отдаёт из `GetValue`: `Array(String)`→`string[]`,
  `Array(Int64)`→`long[]`, `Array(UInt64)`→`ulong[]`, `Tuple(...)`→`System.Tuple<...>`
  (источник: deepwiki `ClickHouse/clickhouse-cs`, `ClickHouseDataReader`/`ArrayType.Read`/`TupleType.Read`).
- Binding массивов-параметров в ClickHouse **уже работает** (тест
  `ArrayFunction_WithCapturedArray_ShouldBindSingleParameter`), поэтому
  `WIP_clickhouse_array_scalars.md` устарел в части «блокирует binding `T[]`-параметров».

## Сводка оценки

| Пункт | Масштаб | Новые механизмы | Блокеры | Риск | Дни |
| --- | --- | --- | --- | --- | --- |
| Row reader `Array(T)`/`Tuple` | core `SelectExpression` + `RowMapperFactory` + конвертер | материализация массива/кортежа, приведение элементов | формы типов драйвера | средний | 3–5 |
| Higher-order `arrayMap`/… | `ArraySqlTranslator` + `ClickHouseFunctions` (+6) | трансляция лямбда-аргумента | array-результат → row reader | средний | 2–3 |
| Мелкие array-скаляры | `ClickHouseFunctions` + `ArraySqlTranslator` | нет | array-результат → row reader | низкий | 0.5–1 |
| `tuple`/`tupleElement`/`untuple` | `ClickHouseFunctions` + селект-лист | материализация `Tuple`, разворачивание | row reader `Tuple` | средний | 2–3 |
| PG `DISTINCT ON` | `DistinctOnClause` + builder + диалект + рендер (копия `LimitByClause`) | новый query-модификатор | нет | низкий | 1–2 |
| PG `LIMIT … WITH TIES` | диалект-хук + `MakePage`/`ORDER BY` | хвост пагинации | нет | средний | 1–2 |
| PG `TABLESAMPLE` | `FromExpression` + `MakeFrom` | FROM-модификатор | нет | низкий | 1 |
| PG full-text (`@@`/`ts_rank`/…) | `MakeFullText` уже есть; экспорт функций | нет | нет | низкий | 1–2 |
| PG `jsonb_array_elements`/`each` | table functions + row reader setof | TVF setof | форма колонки | средний | 2–3 |
| MSSQL `FOR SYSTEM_TIME` | `FromExpression` + рендер `FROM` | FROM-модификатор | нет | средний | 2 |
| MSSQL `CONTAINSTABLE`/`FREETEXTTABLE` | TVF с `RANK` | TVF | нет | средний | 2 |
| MSSQL `PIVOT`/`UNPIVOT` | новая конструкция запроса | PIVOT-модель | нет | высокий | 4–6 |
| MSSQL XML-методы | постфиксная трансляция вызовов | вызов метода на значении | `nodes` как источник | высокий | 3–4 |
| join `SEMI`/`ANTI`/`PASTE` | меняет набор колонок | новая модель результата | дизайн не выбран | высокий | 3+ |

Граф зависимостей: row reader — узловое звено (`groupArray`/`topK`/`quantiles`, array-половина
higher-order, `tuple`/`untuple`, `hasSubstr`-префикс, нативный JSON-массив, `JSONExtractArrayRaw`).
Полнотекст PG, `DISTINCT ON`, `TABLESAMPLE`, `WITH TIES`, `FOR SYSTEM_TIME`,
`CONTAINSTABLE`/`FREETEXTTABLE` от него **не** зависят.

## Выбранный набор (этот проход)

Средний/низкий риск, без блокеров:

1. PG `SELECT DISTINCT ON (...)` — `DistinctOnClause` + `EntityBuilder.DistinctOn`.
2. PG `TABLESAMPLE` — модификатор `FROM`.
3. PG `LIMIT ... WITH TIES` — хвост пагинации.
4. PG `FOR UPDATE` / `FOR SHARE` — хвостовая блокирующая клауза (тот же пункт бэклога, что 1–3:
   `todo_postgres.md:108`).
5. PG полнотекстовый поиск: явные `to_tsvector`/`to_tsquery`/`ts_rank`/`ts_headline` поверх уже
   существующего `MakeFullText`.
6. MSSQL `FOR SYSTEM_TIME` (temporal) — источник в `FROM`.
7. MSSQL `CONTAINSTABLE`/`FREETEXTTABLE` — TVF с колонкой `RANK`.

Вне этого прохода (блокеры/высокий риск): row reader `Array`/`Tuple`, higher-order, `tuple`/`untuple`,
PG `jsonb_array_elements`/`each`, MSSQL `PIVOT`/`UNPIVOT`/XML, join `SEMI`/`ANTI`/`PASTE`.

## План на каждый пункт

Каждый пункт выполняется по шагам 1–8 скила: провайдер × форма матрица → WIP → surface (tier a/b/c) →
Release 0/0 → аудит `nextorm-code-auditor` → SQL-gen/integration тесты → покрытие → docs EN/RU +
`sql-capabilities-gap-analysis.md` + `todo_*.md`.

Базовое покрытие на момент старта (после A1 arrays): **84.7% строк / 73.1% ветвей**
(coverable 16338, covered 13844, uncovered 2494).

## Статус прохода (средний/низкий риск, без блокеров)

Выполнено и запушено в рабочий каталог (аудит + тесты + docs EN/RU):

1. **PG `DISTINCT ON`** — `docs/guide/provider-specific/postgresql.md` (батч из 4 модификаторов).
2. **PG `TABLESAMPLE`** — там же (кросс-провайдерно: PostgreSQL + SQL Server).
3. **PG `LIMIT ... WITH TIES`** — там же (PostgreSQL + SQL Server).
4. **PG `FOR UPDATE`/`FOR SHARE`** — там же (PostgreSQL + MySQL/MariaDB).
5. **PG full-text native surface** (`to_tsvector`/`to_tsquery`/`ts_rank`/`ts_headline`/`@@`) —
   `docs/guide/provider-specific/postgresql.md`.
6. **MSSQL/MariaDB `FOR SYSTEM_TIME`** — `docs/guide/provider-specific/sqlserver.md`.

Переклассифицировано как **заблокированное** (не «средний риск без блокеров»):

7. **MSSQL `CONTAINSTABLE`/`FREETEXTTABLE`** — первый аргумент ссылается на базовую таблицу по имени
   внутри `FROM`-источника; это тот же пробел, что «сырой SQL как композируемый источник» и
   «коррелированный `APPLY`» (внешняя ссылка внутри `FROM`). Без него TVF-модель `[SqlTableFunction]`
   не выражает конструкцию. Оставлено отложенным, см. `todo_mssql.md`.

Не входили в проход (высокий риск/блокеры): row reader `Array`/`Tuple`, higher-order array-функции,
`tuple`/`untuple`, PG `jsonb_array_elements`/`each`, MSSQL `PIVOT`/`UNPIVOT`/XML, join
`SEMI`/`ANTI`/`PASTE`.
