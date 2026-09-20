# WIP: `array_shuffle` / `array_sample` (PostgreSQL)

> Статус: **реализовано** (`todo_postgres.md:78-84`, ветка `todo-pg`).

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_postgres.md:78-84`,
> `todo_phase2.md` → PostgreSQL — не заблокировано.

## Пункт и цель

- Пункт: **`array_shuffle` / `array_sample`** — скалярные array-функции.
- Провайдер-источник: PostgreSQL.
- Критерий приёмки: `SqlFunctions.Postgres.array_shuffle(a)` и `array_sample(a, n)` рендерятся в
  одноимённые PostgreSQL-функции; провайдеры без поддержки массивов бросают `NotSupportedException`
  (общий гейт `SupportsArrays`).

## Матрица «провайдер × форма» (шаг 1)

| Провайдер | shuffle | sample | Источник |
| --- | --- | --- | --- |
| PostgreSQL | `array_shuffle(anyarray)` (16+) | `array_sample(anyarray, n)` (16+) | https://www.postgresql.org/docs/current/functions-array.html |
| ClickHouse | `arrayShuffle(arr [, seed])` | `arrayRandomSample(arr, samples)` | https://clickhouse.com/docs/en/sql-reference/functions/array-functions |
| MySQL | —: нет array-типа | — | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | —: нет array-типа (`JSON` — псевдоним `LONGTEXT`) | — | https://mariadb.com/kb/en/json-data-type/ |
| SQL Server | —: нет array-типа | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| SQLite | —: нет array-типа (JSON-массивы только через `json_*`) | — | https://www.sqlite.org/json1.html |
| InMemory | CLR `Array` (не транслируется: скалярные `SqlFunctions` в in-memory не оцениваются) | — | — |

**Единообразие провайдеров.** ClickHouse выражает ту же операцию (`arrayShuffle`/
`arrayRandomSample`), но nextorm не умеет материализовать результат `Array(T)` (нет row reader;
см. `todo_clickhouse.md:39,249`), поэтому ClickHouse-ветка не заводится — это подтверждённый блокер
чтения, а не отсутствие функции в СУБД. PostgreSQL — единственный провайдер, где результат-массив
используется внутри выражения (предикат/having/вложенное выражение). Гейт — `SupportsArrays`
(PostgreSQL), как у остальных array-функций.

## Ближайший C#-аналог и уровень

- CLR-аналога нет (`Random.Shuffle` — мутирующий метод над `Span`, не выразим в выражении).
- Уровень **(b)**: методы `PostgresFunctions` + ветки `ArraySqlTranslator` + существующий гейт.

## Диалектный план

- `ArraySqlTranslator.TryTranslateFunction`: новые ветки `array_shuffle`/`array_sample`, обе через
  `EmitFunction` (=> `RequireArraySupport` → `SupportsArrays`).
- Новых флагов/`Make*` не требуется: PostgreSQL рендерит одноимённые функции базовым
  `SqlOperandTranslator.EmitFunction`.
- Версия PostgreSQL 16+ документируется, но не гейтится по версии (как `uuidv7` PG18+).

## Публичный API

```csharp
public T[] PostgresFunctions.array_shuffle<T>(T[] array);
public T[] PostgresFunctions.array_sample<T>(T[] array, int n);
```

## План тестов

- SQL-gen PostgreSQL: `array_shuffle(@arr)` / `array_sample(@arr, 2)`.
- Rejection (ClickHouse/SQLite/MySQL/SQL Server): вызов бросает `NotSupportedException`.
- Прямой хук не требуется (поведение — базовый `EmitFunction`).
- Интеграция PostgreSQL: `array_length(array_sample(...), 1) == n` (реальная PG17). На практике
  вложить array-возвращающую функцию в другую нельзя: `SqlOperandTranslator.AppendArrayOperand`
  материализует любой не-`SqlFunctions.Parameter` array-операнд и падает на `SqlFunctions.Postgres`;
  поэтому интеграционный тест выполняет `array_shuffle`/`array_sample` через `PrepareFromSql`
  (реальный сервер, `PostgresSpecificTests.ArrayShuffleSample_ShouldExecute`), а трансляция
  проверяется SQL-gen тестом. Это ограничение движка (материализация array-операнда), отдельная
  задача — не часть этого пункта.
- Базовая линия покрытия: снимается после сборки.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбокс), `docs/guide/11-scalar-functions.md` + RU,
  `docs/providers/postgres.md` + RU.
