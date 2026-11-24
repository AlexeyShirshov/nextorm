# Репозитории и контекст приложения

В этом руководстве вы настроите контекст приложения, чтобы использовать
одни и те же именованные репозитории и запросы в нескольких сервисах.

## Зачем нужен `RepositoryContext`?

Код приложения может работать с `IDataContext` провайдера напрямую.
Свой класс на базе `RepositoryContext` пригодится, когда нескольким сервисам
нужны одни и те же точки входа и запросы. Например, внедрите `AppRepositoryContext`, чтобы
каждый сервис мог начать с `app.Orders` или вызвать `app.QueryUnpaidOrders()`,
не повторяя условие «заказ не оплачен».

NextORM предоставляет базовый класс `NextORM.Core.Repositories.RepositoryContext`.
Напишите `AppRepositoryContext` вручную или [сгенерируйте его из существующей
базы](../getting-started/05-existing-database.md). Базовый класс хранит переданный
`IDataContext` и предоставляет защищённый метод `Repository<T>()`, который
создаёт и переиспользует репозиторий для типа сущности. Запросы по-прежнему
выполняет провайдер. Этот слой не создаёт отдельного соединения или транзакции.

`IRepository<Order>` — точка входа для одного типа сущностей. Контекст
приложения объединяет такие точки входа (`Orders`, `Customers`) и хранит
методы с общими запросами. Для каждой сущности не нужно писать отдельный
класс репозитория. Если вам достаточно `dataContext.From<Order>()`,
используйте его напрямую: и свой контекст на базе `RepositoryContext`, и
репозитории необязательны.

## Именованный доступ к сущности

Оба вызова считают заказы, используя один и тот же контекст базы данных,
маппинг и фильтры. Здесь и в следующих примерах используйте токен отмены,
полученный от вызывающего кода:

```csharp
var countFromContext = await dataContext.From<Order>()
    .CountAsync(cancellationToken);
var countFromRepository = await app.Orders.CountAsync(cancellationToken);
```

Здесь `dataContext` — тот же экземпляр, что и `app.DataContext`. Свойство
`app.Orders` имеет тип `IRepository<Order>`: это не таблица и не список уже
загруженных заказов. Обращение к свойству не читает строки. В этом примере
запрос выполняет `CountAsync()`.

Доменный репозиторий может предоставлять прикладную операцию, например
`FindUnpaidOrders`, и скрывать, как устроен запрос к базе. `IRepository<T>`
в NextORM не определяет такие операции: запрос и его условия по-прежнему
задаёт вызывающий код. Репозиторий также не отслеживает изменения сущностей
и не сохраняет их через `SaveChanges`.

**Перед началом:** Сопоставьте `Order` и `Customer` с таблицами базы данных с
уникальными значениями `Id`. В примерах также используются `Order.CustomerId`,
`Order.IsArchived` и `Order.IsPaid`. См. [«Сущности и метаданные»](../getting-started/03-entities-and-metadata.md)
и [«Внедрение зависимостей»](../getting-started/04-dependency-injection.md).

## Настройте контекст приложения

1. Унаследуйте контекст приложения от `RepositoryContext`.
   Объявите свойства для сущностей и вынесите повторно используемые запросы
   в методы, такие как `QueryUnpaidOrders()`:

   ```csharp
   using NextORM.Core;
   using NextORM.Core.Repositories;

   public sealed class AppRepositoryContext(IDataContext provider)
       : RepositoryContext(provider)
   {
       public IRepository<Order> Orders => Repository<Order>();
       public IRepository<Customer> Customers => Repository<Customer>();

       public EntityBuilder<Order> QueryUnpaidOrders() =>
           Orders.Where(order => !order.IsPaid);
   }
   ```

2. После регистрации провайдера как `IDataContext` с временем жизни `scoped`
   зарегистрируйте контекст приложения в той же коллекции сервисов:

   ```csharp
   services.AddScoped<AppRepositoryContext>();
   ```

3. Внедрите `AppRepositoryContext` в сервис, который разрешается в этой области
   видимости. Чтобы проверить настройку, дождитесь результата
   `app.Orders.CountAsync(cancellationToken)`: метод возвращает число заказов,
   доступных с учётом фильтров провайдера.

Контекст приложения заимствует провайдер: он не выполняет команды сам и не
освобождает провайдер. Через `app.DataContext` доступен тот же экземпляр
`IDataContext`. Маппинг, глобальные фильтры, транзакции и кеш остаются у провайдера.

## Запускайте независимые запросы

Начинайте каждый запрос с репозитория. Например, прочитайте активные заказы,
а затем посчитайте архивные: условие первого запроса не перейдёт во второй.

```csharp
var activeOrders = await app.Orders.Where(o => !o.IsArchived)
    .ToListAsync(cancellationToken);
var archivedCount = await app.Orders.Where(o => o.IsArchived)
    .CountAsync(cancellationToken);
```

Чтобы прочитать только идентификаторы заказов, вызовите `Select` прямо на репозитории:

```csharp
var orderIds = await app.Orders.Select(order => order.Id)
    .ToListAsync(cancellationToken);
```

Запрос можно также начать с `GroupBy`, `OrderBy`, `Page` или `LeftJoin`.
Методы вроде `AnyAsync` и `ToArrayAsync` сразу выполняют запрос. Каждый вызов
начинает независимый запрос: контекст кеширует репозиторий, а не построитель.
Если нужная перегрузка или метод конкретного провайдера недоступны на
репозитории, получите построитель через `Query()`.

Для `Repository<T>(configure)` действует то же правило первой регистрации
маппинга на процесс, что и для `From<T>(configure)`.

## Переиспользуйте и дополняйте запросы из своего контекста

Вынесите условие в метод контекста, если оно нужно в нескольких местах, но
каждое из них дополняет запрос по-своему. `QueryUnpaidOrders()` возвращает новый
`EntityBuilder<Order>` с условием «заказ не оплачен». Метод не выполняет SQL и
не загружает заказы. Например, добавьте фильтр по покупателю, чтобы получить
его неоплаченные заказы, или вызовите тот же метод, чтобы посчитать все
неоплаченные заказы:

```csharp
var orders = await app.QueryUnpaidOrders()
    .Where(order => order.CustomerId == customerId)
    .ToListAsync(cancellationToken);

var unpaidCount = await app.QueryUnpaidOrders().CountAsync(cancellationToken);
```

`customerId` — идентификатор покупателя, чьи заказы нужно получить. Запрос
списка учитывает оба условия. Подсчёт выполняет отдельный запрос без фильтра
по покупателю. Выполняйте оба запроса в области видимости, которой
принадлежит контекст провайдера.

Возвращайте `EntityBuilder<Order>`, если вызывающему коду нужно дополнить
запрос до выполнения. Чтобы скрыть API доступа к данным, выполните запрос
внутри метода и верните данные, а не построитель.

## Переиспользуйте подготовленный запрос между экземплярами контекста

Если один и тот же запрос выполняется с разными значениями параметров,
подготовьте его один раз и сохраните в статическом поле. Добавьте эти члены
в `AppRepositoryContext` и подключите `System.Collections.Generic`, `System.Threading`
и `System.Threading.Tasks`:

```csharp
private static IPreparedQueryCommand<Order>? _unpaidByCustomerPrepared;
private static object? _prepareLock;

public Task<List<Order>> FindUnpaidByCustomer(
    int customerId, CancellationToken cancellationToken)
{
    var prepared = LazyInitializer.EnsureInitialized(
        ref _unpaidByCustomerPrepared,
        ref _prepareLock,
        () => QueryUnpaidOrders()
            .Where(o => o.CustomerId == SqlFunctions.Parameter<int>(0))
            .Prepare(cancellationToken: cancellationToken));

    return prepared.ToListAsync(this.DataContext, cancellationToken, customerId);
}
```

При первом вызове `LazyInitializer` подготавливает запрос под блокировкой.
Следующие вызовы, в том числе из других областей видимости DI, переиспользуют
запрос и передают `customerId` как параметр `0`. В статическом поле нет ни
результатов, ни контекста. `this.DataContext` — свойство, унаследованное от
`RepositoryContext`. Оно возвращает тот экземпляр `IDataContext`, который конструктор
контекста приложения получил в параметре `provider`. В `ToListAsync`
передавайте провайдер, а не сам `AppRepositoryContext`. Метод передаёт токен отмены
при первичной подготовке и при каждом выполнении.

Используйте общее поле только для экземпляров с одним провайдером, одинаковым
маппингом и одинаковой формой запроса. Если арендаторы используют разные схемы
или таблицы, подготовьте отдельный запрос для каждого маппинга. На одном
соединении выполняйте один и тот же подготовленный запрос последовательно.
Подробнее см. [«Кэш планов и повторное использование»](../engine/03-plan-cache-and-reuse.md).

## Используйте репозиторий в `From` или `Join`

Чтобы обратиться к отображённой таблице напрямую, передайте репозиторий.
Например, соедините заказы с покупателями без подзапросов:

```csharp
var withCustomers = app.DataContext.From(app.Orders)
    .Join(app.Customers, (order, customer) => order.CustomerId == customer.Id);
```

`From` и `Join` отклоняют репозитории, привязанные к другому экземпляру
`IDataContext`. Чтобы использовать отфильтрованный запрос как производный
источник, передайте его построитель:

```csharp
var archived = app.Orders.Where(o => o.IsArchived);
var fromArchived = app.DataContext.From(archived);
```

## Изменяйте данные через репозиторий

`MakeInsert()`, `MakeUpdate()` и `MakeDelete()` каждый раз создают новый
построитель. Например, чтобы удалить архивный заказ, передайте его
идентификатор в `orderId` и явно выполните удаление:

```csharp
var deleted = await app.Orders.MakeDelete()
    .Where(o => o.Id == orderId && o.IsArchived)
    .DeleteAsync(cancellationToken);
```

На провайдере, который сообщает число затронутых строк, `deleted` равен `1`,
если заказ подходит под условие и доступен с учётом активных фильтров, и `0`,
если подходящего заказа нет. Команда использует тот же провайдер и участвует
в его активной транзакции, если она открыта. О лимитах и ограничениях
провайдеров см. [«Изменение данных (DELETE)»](18-delete-statement.md).
