# Последовательности и генераторы UUID

Серверные генераторы значений: последовательности продвигаются и читаются, а генераторы UUID создают
уникальные идентификаторы. Ни у того, ни у другого нет единого переносимого написания, потому что
набор операций у провайдеров различается. Для последовательностей сегодня поддержаны PostgreSQL,
MariaDB и SQL Server.

| Операция | PostgreSQL | MariaDB | SQL Server |
|---|---|---|---|
| Следующее значение | `SqlFunctions.Postgres.nextval(seq)` | `SqlFunctions.MySql.nextval(seq)` / `next_value_for(seq)` | `SqlFunctions.SqlServer.next_value_for(seq)` / `nextval(seq)` |
| Задать следующее значение | `SqlFunctions.Postgres.setval(seq, value)` | `SqlFunctions.MySql.setval(seq, value)` | — |
| Текущее значение (сессия) | `SqlFunctions.Postgres.currval(seq)` | — | — |
| Последнее значение (сессия/соединение) | `SqlFunctions.Postgres.lastval()` | `SqlFunctions.MySql.lastval(seq)` | — |

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

SQL Server читает последовательность через `NEXT VALUE FOR`, это открывают
`SqlFunctions.SqlServer.next_value_for(sequence)` и его псевдоним `nextval(sequence)` (оба возвращают
CLR `long`). Имя должно быть константной строкой, называющей один объект, и записываться либо одной
компонентой (`name`), либо двумя (`schema.name`); каждая компонента квотируется квадратными скобками
отдельно, а встроенная `]` удваивается, поэтому сырой текст имени не может выйти за пределы
идентификатора:

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.next_value_for(sequence)` | `next value for [sequence]` |
| `SqlFunctions.SqlServer.next_value_for("dbo.sequence")` | `next value for [dbo].[sequence]` |
| `SqlFunctions.SqlServer.nextval(sequence)` | `next value for [sequence]` |

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Id = SqlFunctions.SqlServer.next_value_for("order_seq") })
    .ToList();
```

Не-константное или захваченное имя (колонка, захваченная локальная переменная, `default`/`null`)
отклоняется с `NotSupportedException`, и имя никогда не привязывается как SQL-параметр — это
идентификатор, а не значение. У SQL Server нет привязанных к сессии `currval`/`lastval` и нет
возвращающего значение `setval` (его эквивалент — DDL `ALTER SEQUENCE ... RESTART WITH`), поэтому
открыто только следующее значение. Эти имена требуют провайдера, который сообщает их через
[`ISqlDialect.SqlServerFunctions`](xref:NextORM.Core.ISqlDialect.SqlServerFunctions); все прочие
провайдеры их отклоняют. Отслеживается в
[#218](https://github.com/AlexeyShirshov/nextorm/issues/218).

## Генераторы UUID

`SqlFunctions.Sql.gen_random_uuid()` (случайный v4) и `uuidv7()` — кросс-провайдерные
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `uuid_v4()` (11.7+) | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+, требуется явная версия) | — | — | `uuid_v7()` (11.7+) | `generateUUIDv7()` | — |

В PostgreSQL `uuidv7()` рендерится только когда контекст настроен на явную версию сервера 18+; версия
ниже 18 или **незаданная** версия отклоняет вызов при подготовке обычным `NotSupportedException`:
`The uuidv7 function is not supported by this provider.` Генератор v4 никогда не подставляется, а
`NULL` никогда не возвращается; `gen_random_uuid()` (v4) сохраняет прежнее поведение на PostgreSQL 13+.

У MySQL есть только `UUID()` (v1), у SQLite генератора UUID нет, поэтому оба отклоняют вызов. В MariaDB оба
генератора требуют явно заданной версии сервера 11.7 или новее; **контекст без версии**
(`MariaDbDataContext`) отклоняет их с `NotSupportedException`, а `UUID()` (v1) не подставляется. Это
серверные генераторы, вычисляемые на каждую строку базой; `Guid.NewGuid()` — клиентское значение и
заменой не является. SQL Server дополнительно открывает `newsequentialid()`
(`SqlFunctions.SqlServer.newsequentialid()`), допустимый только как `DEFAULT` колонки, но не в
обычном `SELECT`.

PostgreSQL также читает данные из сохранённого UUID — на `SqlFunctions.Postgres`, с гейтом по имени
через [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)):

| C# | SQL | Возвращает |
|---|---|---|
| `SqlFunctions.Postgres.uuid_extract_timestamp(value)` | `uuid_extract_timestamp(value)` | `timestamptz` (`DateTime?`) — NULL для UUID версии 4 или nil-UUID |
| `SqlFunctions.Postgres.uuid_extract_version(value)` | `uuid_extract_version(value)` | `smallint` (`short?`) — встроенная версия, NULL для nil-UUID |
