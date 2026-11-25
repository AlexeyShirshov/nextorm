# Быстрый старт

Выполните типизированный запрос к SQLite в памяти и выведите результат. Эта страница предназначена для разработчиков на .NET, которые установили NextORM и хотят увидеть полный пример запроса до знакомства с его возможностями.

**Предварительные требования:** [Установка](01-installation.md).

## Полная минимальная программа

Создайте консольный проект .NET и замените содержимое его файла `Program.cs` этим кодом. Оставьте
объявления интерфейсов сущностей после операторов верхнего уровня, как в примере: в C# операторы
верхнего уровня должны идти перед объявлениями типов.

Программа одним батчем создаёт таблицы `customers` и `orders`, добавляет тестовые данные и читает
идентификатор и статус каждого заказа. SQLite работает в памяти, поэтому сервер базы данных не
нужен. Сначала установите в консольный проект пакеты `nextorm` и `nextorm.sqlite`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Sqlite;

using var dataContext = new SqliteDataContext(
    "Data Source=:memory:", new DataContextBuilder());

var customers = new[] { (Id: 1, Name: "Amina"), (Id: 2, Name: "Ravi") };
var sampleOrders = new[]
{
    (Id: 1, CustomerId: 1, Status: "Paid", TotalCents: 2550),
    (Id: 2, CustomerId: 2, Status: "Pending", TotalCents: 1800),
    (Id: 3, CustomerId: 1, Status: "Paid", TotalCents: 4250),
};

var orders = await dataContext.CreateBatchBuilder()
    .Raw("""
        create table customers (
            id integer primary key,
            name text not null
        )
        """)
    .Raw("""
        create table orders (
            id integer primary key,
            customer_id integer not null,
            status text not null,
            total_cents integer not null
        )
        """)
    .Insert<ICustomer>(insert => insert
        .Values(customers, customer => new { customer.Id, customer.Name }))
    .Insert<IOrder>(insert => insert
        .Values(sampleOrders, order => new
        {
            order.Id, order.CustomerId, order.Status, order.TotalCents
        }))
    .Query(db => db.From<IOrder>()
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, order.Status }))
    .ToListAsync();

foreach (var order in orders)
{
    Console.WriteLine($"Order {order.Id}: {order.Status}");
}

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("customer_id")]
    int CustomerId { get; set; }

    [Column("status")]
    string Status { get; set; }

    [Column("total_cents")]
    int TotalCents { get; set; }
}

[SqlTable("customers")]
public interface ICustomer
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }
}
```

Из каталога проекта запустите программу:

```bash
dotnet run
```

Программа выведет:

```text
Order 1: Paid
Order 2: Pending
Order 3: Paid
```

Запрос выбирает две сопоставленные колонки из таблицы `orders`:

```sql
select id, status
 from orders
 order by id
```

## Обзор

`CreateBatchBuilder()` собирает шаги в порядке выполнения. `Raw` добавляет SQL для создания таблиц,
а `Insert<TEntity>` передаёт в лямбду типизированный билдер вставки для настройки через `Values`.
`Query` передаёт в лямбду контекст батча для построения итогового запроса. `ToListAsync()` выполняет
весь батч и возвращает строки итогового запроса. См. [Батчи](../guide/25-sql-batch.md) и
[Вставка данных](../guide/17-insert-statement.md).

В итоговом запросе
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
выбирает источник `orders`, а
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
задаёт возвращаемые колонки.

## Использование строки подключения

Контекст сам создаёт подключение из строки подключения и закрывает его при освобождении. Чтобы
работать с файлом SQLite вместо базы в памяти, укажите путь к нему в `Data Source`:

```csharp
using var dataContext = new SqliteDataContext(
    "Data Source=app.db", new DataContextBuilder());
```

## Чтение данных

Для отдельного запроса, без батча, также используйте `ToListAsync`, чтобы собрать строки в список.
Чтобы читать результат потоком, используйте
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
Другие распространённые терминальные методы:

| Терминал | Результат |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `List<TResult>` |
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка; выбрасывает исключение, если последовательность пуста |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка или `default` |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) (и `…Async`) | ровно одна строка (или `default`) |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) / [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `bool` |
| [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})) / [`CountAsync`](xref:NextORM.Core.EntityBuilderExtensions.CountAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `int` |

Каждый терминальный метод имеет синхронную и асинхронную форму; в коде приложения предпочитайте асинхронную форму.

## См. также

* [Фильтрация строк](../guide/03-filtering-where.md) - добавление условий перед выборкой результата.
* [Выбор нужных полей](../guide/02-projections.md) - формирование возвращаемых строк.
* [Соединение таблиц](../guide/04-joins.md) - добавление сведений о клиентах к заказам.
* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс,
  режим [`TableAlias`](xref:NextORM.Core.TableAlias) без сущностей и fluent-конфигурация.
* [Внедрение зависимостей](04-dependency-injection.md) - зарегистрируйте контекст вместо его ручного
  создания.
