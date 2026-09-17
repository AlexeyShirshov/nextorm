# Быстрый старт

> Определите сущность, создайте контекст SQLite, выполните типизированный запрос и выведите строки - самая маленькая сквозная программа на NextORM.

**Предварительные требования:** [Установка](01-installation.md).

## Обзор

Запрос NextORM всегда состоит из четырёх частей:

1. **сущность** (класс или интерфейс), свойства которой отображаются на столбцы;
2. **контекст** (`IDataContext`), созданный из подключения или строки подключения;
3. **запрос**, построенный с помощью `Create<T>()`, `Select`, `Where` и так далее;
4. **терминальный метод**, такой как `ToList()`, `First()`, `Any()` или `ToAsyncEnumerable()`, который его выполняет.

`Create<T>()` также регистрирует метаданные `T` при первом обращении. Контекст поддерживает освобождение: контекст, созданный из строки подключения, владеет подключением и закрывает его, тогда как контекст, созданный из переданного `DbConnection`, оставляет это подключение открытым.

## Полная минимальная программа

Следующая программа самодостаточна. Она создаёт базу данных SQLite в памяти, наполняет одну таблицу, строит контекст с помощью `UseSqlite` и возвращает строки как анонимный тип.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using nextorm.core;
using nextorm.sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table simple_entity (id integer primary key);" +
        "insert into simple_entity (id) values (1), (2), (3);";
    setup.ExecuteNonQuery();
}

var builder = new DbContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDbContext();

await foreach (var row in dataContext.Create<ISimpleEntity>()
                                   .Select(entity => new { Id = (long)entity.Id })
                                   .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}

// The entity can live in its own file. In a single-file top-level program the type
// declaration must come after the top-level statements.
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
```

Вывод:

```text
Id = 1
Id = 2
Id = 3
```

Сгенерированный SQL - это обычный `select` по отображённым таблице и столбцу:

```sql
select id from simple_entity
```

## Использование строки подключения

`UseSqlite` также принимает путь к файлу (или фрагмент строки подключения SQLite), и тогда контекст создаёт подключение и владеет им:

```csharp
var builder = new DbContextBuilder().UseSqlite("app.db");
using var dataContext = builder.CreateDbContext();
```

> **Примечание:** в сборках `DEBUG` `UseSqlite(string filepath)` выбрасывает `ArgumentException`, когда файл
> не существует. При разработке передавайте `:memory:` или существующий путь; передавайте `DbConnection`,
> чтобы сохранить полный контроль. `UseSqlServer(connectionString)` и `UsePostgres(connectionString)` имеют
> соответствующие строковые перегрузки.

## Чтение данных

В запросе выше используется `ToAsyncEnumerable()`. Другие распространённые терминальные методы:

| Терминал | Результат |
|---|---|
| `ToList()` / `ToListAsync()` | `List<TResult>` |
| `First()` / `FirstAsync()` | первая строка; выбрасывает исключение, если последовательность пуста |
| `FirstOrDefault()` / `FirstOrDefaultAsync()` | первая строка или `default` |
| `Single()` / `SingleOrDefault()` (и `…Async`) | ровно одна строка (или `default`) |
| `Any()` / `AnyAsync()` | `bool` |
| `Count()` / `CountAsync()` | `int` |

Каждый терминальный метод имеет синхронную и асинхронную форму; в коде приложения предпочитайте асинхронную форму.

## См. также

* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс,
  режим `TableAlias` без сущностей и fluent-конфигурация.
* [Внедрение зависимостей](04-dependency-injection.md) - зарегистрируйте контекст вместо его ручного
  создания.

---

Source: `test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`test/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`test/nextorm.integration.tests/Providers/SqliteTestProvider.cs:32`;
`test/nextorm.sqlite.tests/ConnectionManagementTests.cs:115`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:111`.
