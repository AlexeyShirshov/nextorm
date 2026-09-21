# Провайдер MySQL

> Используйте `nextorm.mysql` для MySQL 8.x; он отрисовывает параметры `@name`, идентификаторы в обратных кавычках, конкатенацию `concat(...)`, разбиение на страницы `limit`/`offset` и имена агрегатов стандартного отклонения/дисперсии.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

[`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext) (`src/nextorm.mysql/MySqlDataContext.cs`) оборачивает `MySqlConnector`. Он создаёт
`MySqlConnection` из строки подключения и возвращает [`Instance`](xref:NextORM.MySql.MySqlDialect.Instance) из свойства `Dialect`.
[`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) (`src/nextorm.mysql/MySqlDialect.cs`) — это диалект:

- плейсхолдер параметра `@name`;
- идентификаторы и псевдонимы квотируются обратными кавычками;
- конкатенация строк использует функцию `concat(a, b, ...)` — инфиксный `||` в MySQL является
  логическим ИЛИ, если не включён режим `PIPES_AS_CONCAT`, поэтому диалект его не выдаёт;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect) отрисовывает `coalesce(a, b)`;
- [`MakeStringLength`](xref:NextORM.Core.ISqlDialect) отрисовывает `char_length(x)` (`length()` в MySQL считает байты);
- [`MakeNow`](xref:NextORM.Core.ISqlDialect) отрисовывает `now()` для локального времени и `utc_timestamp()` для UTC;
- `stdev`/`stdevp`/`var`/`varp` отображаются в `stddev_samp`/`stddev_pop`/`var_samp`/`var_pop`
  (`stddev` и `variance` в MySQL — синонимы *популяционных* величин);
- преобразование CLR-типа отрисовывается через цель MySQL `CAST` (`signed`/`unsigned` для целых,
  `double`, `decimal`, `char`, `datetime`), а не через ANSI `bigint`/`integer`;
- унарное дополнение отрисовывается как `(-(x) - 1)`, потому что `~` в MySQL даёт беззнаковое
  64-битное значение, которое переполняет знаковый CLR-целый тип;
- символ `ESCAPE` в предикате `LIKE` записывается как `'\\'`, так как MySQL также трактует
  обратный слэш как escape-символ строкового литерала;
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit превращается в
  `limit 18446744073709551615 offset m`, потому что MySQL принимает `offset` только вместе с `limit`;
- семейство session/info ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)) отрисовывает
  `SqlFunctions.Sql.current_user`/`session_user`/`current_database`/`version` как `current_user()`/`session_user()`/
  `database()`/`version()`, а `current_schema` — как `schema()`;
- агрегат произвольного значения [`SqlFunctions.Sql.any_agg`](xref:NextORM.Core.CommonFunctions.any_agg``1) отрисовывается как
  `ANY_VALUE(x)` ([`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate), MySQL 5.7+);
- переносимый условный [`SqlFunctions.Sql.iif`](xref:NextORM.Core.CommonFunctions.iif``1) отрисовывается как
  `if(condition, a, b)` ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif), [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render)).

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MySql;

// Из строки подключения.
var builder = new DataContextBuilder()
    .UseMySql("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

// Из существующего соединения, принадлежащего вызывающему коду.
using var connection = new MySqlConnector.MySqlConnection("Server=localhost;Database=app");
var byConnection = new DataContextBuilder().UseMySql(connection);

using var ctx = builder.CreateDataContext();   // IDataContext
```

Также можно создать контекст напрямую (именно так поступают тесты провайдера):

```csharp
using NextORM.Core;
using NextORM.MySql;

using IDataContext ctx = new MySqlDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
```

## Конкатенация строк

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Разбиение на страницы

```csharp
ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<ISimpleEntity>().Offset(10).Select(x => x.Id);    // limit 18446744073709551615 offset 10
```

## Различия провайдера

| Аспект | MySQL |
|---|---|
| Плейсхолдер параметра | `@name` |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Логический литерал | `1` / `0` (синонимы `true` / `false`) |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| Псевдоним TVF | требуется |
| `FULL JOIN` | не поддерживается (right join поддерживается) |
| `*ALL` | не поддерживается (в MySQL 8.0.31 есть `INTERSECT`/`EXCEPT`, но без вариантов `ALL`) |
| Текстовый JSON | `json_value`/`json_query`/`json_modify`/`isjson` (через `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET`/`JSON_VALID`) |
| Session/info-функции | `current_user()`, `session_user()`, `schema()`, `database()`, `version()` |
| Произвольное значение | `ANY_VALUE(x)` |
| Условная функция | `iif(cond, a, b)` → `if(cond, a, b)` |
| Оконные квантили | не поддерживаются (`PERCENTILE_CONT` только в MariaDB) |

MySQL 8.0.31 и новее поддерживает `INTERSECT`/`EXCEPT`; диалект отклоняет варианты `*ALL` с
`NotSupportedException`, соответствуя движку.

## См. также

- [Обзор провайдеров](overview.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mysql/MySqlDataContext.cs`,
`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mysql.tests/MySqlDialectTests.cs`, `tests/nextorm.mysql.tests/SqlGenerationTests.cs`.
