# Обзор провайдеров

> Провайдер предоставляет диалект (правила SQL-текста) и подкласс `DbContext` (создание соединения + параметров); выберите тот, который соответствует базе данных, которую вы уже используете.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

nextorm состоит из нейтрального к провайдеру ядра (`nextorm`, пространство имён `nextorm.core`) и одного пакета на
базу данных: `nextorm.sqlite`, `nextorm.sqlserver` и `nextorm.postgres`. Провайдер in-memory встроен
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
| Модульные тесты, которые не должны обращаться к базе данных, тесты кэша планов, тесты формы запросов | `InMemoryContext` (ядро) |

Используйте один и тот же код запросов со всеми провайдерами; перечисленные ниже различия провайдеров — единственное, что
меняется.

## Матрица поддержки

| Возможность | SQLite | SQL Server | PostgreSQL | In-memory |
|---|---|---|---|---|
| Пакет | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | встроен в `nextorm` |
| Плейсхолдер параметра | `$name` | `@name` | `@name` | не применимо |
| Только limit | `limit n` | `top(n)` | `limit n` | take в процессе |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | skip/take в процессе |
| Только offset | `limit -1 offset m` | `offset m rows` | `offset m` | skip в процессе |
| Разбиение на страницы без `ORDER BY` | допускается | внедряет `order by (select null as anyorder)` | допускается | допускается |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | бросает `NotSupportedException` | бросает `NotSupportedException` | поддерживается | не применимо |
| Ключевое слово рекурсивного CTE | `with recursive` | `with` (плюс `option (maxrecursion n)`) | `with recursive` | не применимо |
| Конкатенация строк | `\|\|` | `+` | `\|\|` | не применимо |
| Логический литерал | `1` / `0` | `1` / `0` (через `bit`) | `true` / `false` | не применимо |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | не применимо |
| `stdev` / `stdevp` | `stdev` / `stdevp` (пользовательские) | `stdev` / `stdevp` (нативные) | `stddev` / `stddev_pop` | не применимо |
| `var` / `varp` | `var` / `varp` (пользовательские) | `var` / `varp` (нативные) | `variance` / `var_pop` | не применимо |
| Квотирование идентификаторов / псевдонимов | одинарные кавычки: `as 't1'` | квадратные скобки: `as [t1]` | двойные кавычки: `as "t1"` | не применимо |
| Псевдоним производной таблицы (подзапрос в `FROM`) | не требуется | требуется | требуется | не применимо |
| Псевдоним табличной функции | не требуется | требуется | требуется | источник TVF не поддерживается |
| Соединение `LEFT` / `RIGHT` / `FULL` / `CROSS` | да | да | да | да |
| Возможность соединения `RIGHT` / `FULL` | поддерживается | поддерживается | поддерживается | поддерживается |

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
- [In-memory](in-memory.md)
- [SQL capabilities gap analysis](../../sql-capabilities-gap-analysis.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`test/nextorm.sqlite.tests/SqliteDialectTests.cs`, `test/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`test/nextorm.postgres.tests/PostgresDialectTests.cs`.
