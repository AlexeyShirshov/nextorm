# Обзор провайдеров

> Провайдер предоставляет диалект (правила SQL-текста) и подкласс `DbContext` (создание соединения + параметров); выберите тот, который соответствует базе данных, которую вы уже используете.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

nextorm состоит из нейтрального к провайдеру ядра (`nextorm`, пространство имён `nextorm.core`) и одного пакета на
базу данных: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`
и `nextorm.clickhouse`. Провайдер in-memory встроен
в пакет ядра. Провайдер вносит две вещи:

1. **диалект** — объект без состояния, который отрисовывает всё, что различается между базами данных (плейсхолдеры
   параметров, разбиение на страницы, квотирование, имена функций, флаги возможностей); и
2. **контекст** — подкласс `DbContext`, который знает, как создавать соединение и параметры, и
   предоставляет диалект через своё свойство `Dialect`.

Генерация SQL полностью управляется `ISqlDialect` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`),
поэтому построитель SQL и посетители выражений никогда не содержат имён провайдеров. Семантика запросов (проекция,
фильтрация, соединения, группировка, операции над множествами, CTE, оконные функции, поддержка scalar/UDF/TVF) является общей;
различается только отрисовка.

## Выбор провайдера

| Ситуация | Провайдер |
|---|---|
| Локальная разработка, тесты, встраиваемая база данных, небольшие приложения | `nextorm.sqlite` |
| Вы уже используете Microsoft SQL Server / Azure SQL | `nextorm.sqlserver` |
| Вы уже используете PostgreSQL | `nextorm.postgres` |
| Вы уже используете MySQL | `nextorm.mysql` |
| Вы уже используете MariaDB | `nextorm.mariadb` |
| Вы уже используете ClickHouse | `nextorm.clickhouse` |
| Модульные тесты, которые не должны обращаться к базе данных, тесты кэша планов, тесты формы запросов | `InMemoryContext` (ядро) |

Используйте один и тот же код запросов со всеми провайдерами; перечисленные ниже различия провайдеров — единственное, что
меняется.

## Матрица поддержки

| Возможность | SQLite | SQL Server | PostgreSQL | MySQL | MariaDB | ClickHouse | In-memory |
|---|---|---|---|---|---|---|---|
| Пакет | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | `nextorm.mysql` | `nextorm.mariadb` | `nextorm.clickhouse` | встроен в `nextorm` |
| Плейсхолдер параметра | `$name` | `@name` | `@name` | `@name` | `@name` | `@name` | не применимо |
| Только limit | `limit n` | `top(n)` | `limit n` | `limit n` | `limit n` | `limit n` | take в процессе |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | `limit n offset m` | `limit n offset m` | `limit n offset m` | skip/take в процессе |
| Только offset | `limit -1 offset m` | `offset m rows` | `offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | skip в процессе |
| Разбиение на страницы без `ORDER BY` | допускается | внедряет `order by (select null as anyorder)` | допускается | допускается | допускается | допускается | допускается |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | бросает `NotSupportedException` | бросает `NotSupportedException` | поддерживается | бросает `NotSupportedException` | поддерживается | поддерживается | не применимо |
| Ключевое слово рекурсивного CTE | `with recursive` | `with` (плюс `option (maxrecursion n)`) | `with recursive` | `with recursive` | `with recursive` | `with` (рекурсивные CTE не поддерживаются) | не применимо |
| Конкатенация строк | `\|\|` | `+` | `\|\|` | `concat(a, b)` | `concat(a, b)` | `concat(a, b)` | не применимо |
| Логический литерал | `1` / `0` | `1` / `0` (через `bit`) | `true` / `false` | `1` / `0` (`true` / `false`) | `1` / `0` (`true` / `false`) | `true` / `false` | не применимо |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | не применимо |
| `stdev` / `stdevp` | `stdev` / `stdevp` (пользовательские) | `stdev` / `stdevp` (нативные) | `stddev` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddevSamp` / `stddevPop` | не применимо |
| `var` / `varp` | `var` / `varp` (пользовательские) | `var` / `varp` (нативные) | `variance` / `var_pop` | `var_samp` / `var_pop` | `var_samp` / `var_pop` | `varSamp` / `varPop` | не применимо |
| `date_trunc` | бросает | `datetrunc(...)` (2022+) | поддерживается | бросает | бросает | `dateTrunc(...)` | бросает |
| Арифметика дат (`date_add`, `date_diff`, `end_of_month`, `date_from_parts`, `DateTime.Add*`) | `datetime(x, n \|\| ' days')` / `date(...)` / разность `strftime` | поддерживается | поддерживается | поддерживается | поддерживается | `addDays(...)` … / `toLastDayOfMonth(...)` | бросает |
| `string_agg` | `group_concat(x, delimiter)` | поддерживается (2017+) | поддерживается | `group_concat(x separator delimiter)` | `group_concat(x separator delimiter)` | `arrayStringConcat(groupArray(...), ...)` | бросает |
| Полнотекст `contains` / `freetext` | бросает | `contains` / `freetext` | `to_tsvector(...) @@ ...tsquery(...)` | `match(...) against(...)` | `match(...) against(...)` | бросает | бросает |
| Битовые / статистические / `-If` агрегаты | бросает | бросает | поддерживается | бросает | бросает | `groupBit*`, `corr`/`covarPop`, `countIf`/… | бросает |
| Квотирование идентификаторов / псевдонимов | одинарные кавычки: `as 't1'` | квадратные скобки: `as [t1]` | двойные кавычки: `as "t1"` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | не применимо |
| Псевдоним производной таблицы (подзапрос в `FROM`) | не требуется | требуется | требуется | требуется | требуется | требуется | не применимо |
| Псевдоним табличной функции | не требуется | требуется | требуется | требуется | требуется | требуется | источник TVF не поддерживается |
| Соединение `LEFT` / `RIGHT` / `FULL` / `CROSS` | да | да | да | без `FULL` | без `FULL` | да | да |
| Возможность соединения `RIGHT` / `FULL` | поддерживается | поддерживается | поддерживается | только `RIGHT` | только `RIGHT` | поддерживается | поддерживается |

Для сравнения по каждой возможности с EF Core и linq2db см.
[SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md).

## Как подключается диалект

Диалект реализует `ISqlDialect` или наследуется от `SqlDialectBase`. В `SqlDialectBase` абстрактными являются только
`MakeParam` и `MakePage`; у всех остальных членов есть рабочее значение по умолчанию ANSI, поэтому диалект
переопределяет только то, что отличается. Различия возможностей (разбиение на страницы, требующее `ORDER BY`, обязательные
псевдонимы подзапросов, `INTERSECT ALL`/`EXCEPT ALL`) выражаются свойствами, а не особыми случаями в
построителе SQL.

```csharp
// The built-in dialects are singletons exposed as a static Instance.
ISqlDialect sqlite = SqliteDialect.Instance;
ISqlDialect sqlServer = SqlServerDialect.Instance;
ISqlDialect postgres = PostgresDialect.Instance;
ISqlDialect mysql = MySqlDialect.Instance;
ISqlDialect mariaDb = MariaDbDialect.Instance;
ISqlDialect clickHouse = ClickHouseDialect.Instance;
```

Контекст провайдера возвращает свой диалект из переопределённого свойства:

```csharp
public class SqliteDbContext : DbContext
{
    public override ISqlDialect Dialect => SqliteDialect.Instance;
    // CreateDbConnection / CreateParam are provider specific.
}
```

Контракт диалекта намеренно исключает создание соединения/параметров (`CreateConnection`/
`CreateParam`) и отображение столбцов (`MapColumnExpression`): они находятся на контексте, потому что это
отдельная ось от SQL-текста.

## Регистрация провайдера

Каждый пакет провайдера добавляет методы расширения `UseXxx` на `DbContextBuilder`, и у каждого контекста также есть
публичный конструктор, принимающий строку подключения или существующий `DbConnection`:

```csharp
using nextorm.core;
using nextorm.sqlite;      // or nextorm.sqlserver / nextorm.postgres

var builder = new DbContextBuilder().UseSqlite("app.db");   // provider-specific overload
using var ctx = builder.CreateDbContext();                   // returns IDataContext
```

С внедрением зависимостей:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

См. [Dependency injection](../getting-started/04-dependency-injection.md) для регистраций с ключом и универсальных
регистраций.

## См. также

- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`test/nextorm.sqlite.tests/SqliteDialectTests.cs`, `test/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`test/nextorm.postgres.tests/PostgresDialectTests.cs`, `test/nextorm.mysql.tests/MySqlDialectTests.cs`,
`test/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `test/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`.
