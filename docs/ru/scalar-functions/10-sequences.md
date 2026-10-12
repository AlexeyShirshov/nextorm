# Последовательности и генераторы UUID

Серверные генераторы значений: последовательности продвигаются и читаются, а генераторы UUID создают
уникальные идентификаторы. Ни у того, ни у другого нет единого переносимого написания, потому что
набор операций у провайдеров различается. Для последовательностей сегодня поддержаны PostgreSQL и
MariaDB, а SQL Server отслеживается в
[#218](https://github.com/AlexeyShirshov/nextorm/issues/218).

| Операция | PostgreSQL | MariaDB |
|---|---|---|
| Следующее значение | `SqlFunctions.Postgres.nextval(seq)` | `SqlFunctions.MySql.nextval(seq)` / `next_value_for(seq)` |
| Задать следующее значение | `SqlFunctions.Postgres.setval(seq, value)` | `SqlFunctions.MySql.setval(seq, value)` |
| Текущее значение (сессия) | `SqlFunctions.Postgres.currval(seq)` | — |
| Последнее значение (сессия/соединение) | `SqlFunctions.Postgres.lastval()` | `SqlFunctions.MySql.lastval(seq)` |

## PostgreSQL

Имя последовательности приводится к `regclass`. Функции входят в расширенную библиотеку скалярных
функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.nextval(sequence)` | `nextval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.setval(sequence, value)` | `setval(cast(sequence as regclass), value)` |
| `SqlFunctions.Postgres.currval(sequence)` | `currval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.lastval()` | `lastval()` |

```csharp
var next = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.nextval("order_id_seq"))
    .First();
```

`currval` и `lastval` привязаны к **сессии**: они требуют того же соединения, которое последним
продвинуло последовательность.

## MariaDB

Имя последовательности принимается строкой и рендерится идентификатором (не строковым литералом),
поэтому `SqlFunctions.MySql.next_value_for("order_seq")` даёт `next value for order_seq`:

| C# | SQL |
|---|---|
| `SqlFunctions.MySql.next_value_for(sequence)` | `next value for sequence` |
| `SqlFunctions.MySql.nextval(sequence)` | `nextval(sequence)` |
| `SqlFunctions.MySql.setval(sequence, value)` | `setval(sequence, value)` |
| `SqlFunctions.MySql.lastval(sequence)` | `lastval(sequence)` |

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Id = SqlFunctions.MySql.nextval("order_seq") })
    .ToList();
```

Эти имена — только для MariaDB: MySQL, PostgreSQL, SQL Server, SQLite, ClickHouse и in-memory-контекст
отклоняют их с `NotSupportedException`. `lastval` читает последнее сгенерированное значение
**соединения**.

## SQL Server

Пока не поддержано. SQL Server читает последовательность через `NEXT VALUE FOR <sequence>`; у него нет
привязанных к сессии `currval`/`lastval` и нет возвращающего значение `setval` (его эквивалент — DDL
`ALTER SEQUENCE ... RESTART WITH`). Отслеживается в
[#218](https://github.com/AlexeyShirshov/nextorm/issues/218).

## Генераторы UUID

`SqlFunctions.Sql.gen_random_uuid()` (случайный v4) и `uuidv7()` — кросс-провайдерные
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `UUID_v4()` | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `UUID_v7()` (11.7+) | `generateUUIDv7()` | — |

У MySQL есть только `UUID()` (v1), у SQLite генератора UUID нет, поэтому оба отклоняют вызов. Это
серверные генераторы, вычисляемые на каждую строку базой; `Guid.NewGuid()` — клиентское значение и
заменой не является. SQL Server дополнительно открывает `newsequentialid()`
(`SqlFunctions.SqlServer.newsequentialid()`), допустимый только как `DEFAULT` колонки, но не в
обычном `SELECT`.
