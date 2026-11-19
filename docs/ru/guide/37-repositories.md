# Репозитории и контекст приложения

Используйте контекст приложения `DbContext`, чтобы предоставить именованные
репозитории, например `app.Orders`. В этом руководстве вы зарегистрируете контекст
и выполните запрос и изменение через репозиторий. В NextORM `IRepository<T>` —
типизированный источник для построителей запросов и изменений, а не доменный
репозиторий, скрывающий хранение данных, и не контекст Entity Framework Core
с отслеживанием изменений и `SaveChanges`.

**Перед началом:** Сопоставьте `Order` и `Customer` с таблицами базы данных с
уникальными значениями `Id`. В примерах также используются `Order.CustomerId`
и `Order.IsArchived`. См. [«Сущности и метаданные»](../getting-started/03-entities-and-metadata.md)
и [«Внедрение зависимостей»](../getting-started/04-dependency-injection.md).

## Настройте контекст приложения

1. Унаследуйте контекст приложения от `NextORM.Core.Repositories.DbContext`
   и объявите свойство для каждой сущности:

   ```csharp
   using NextORM.Core;
   using NextORM.Core.Repositories;

   public sealed class AppDbContext(IDataContext provider) : DbContext(provider)
   {
       public IRepository<Order> Orders => Repository<Order>();
       public IRepository<Customer> Customers => Repository<Customer>();
   }
   ```

2. После регистрации провайдера как `IDataContext` с временем жизни `scoped`
   зарегистрируйте контекст приложения в той же коллекции сервисов:

   ```csharp
   services.AddScoped<AppDbContext>();
   ```

3. Внедрите `AppDbContext` в сервис, который разрешается в этой области
   видимости. Чтобы проверить настройку, вызовите `app.Orders.Count()`: метод
   вернёт число заказов, доступных с учётом фильтров провайдера.

Контекст приложения заимствует провайдер: он не выполняет команды сам и не
освобождает провайдер. Через `app.DataContext` доступен тот же экземпляр
`IDataContext`. Маппинг, глобальные фильтры, транзакции и кеш остаются у провайдера.

## Запускайте независимые запросы

Начинайте каждый запрос с репозитория. Например, прочитайте активные заказы,
а затем посчитайте архивные: условие первого запроса не перейдёт во второй.

```csharp
var activeOrders = await app.Orders.Where(o => !o.IsArchived)
    .ToListAsync(cancellationToken);
var archivedCount = app.Orders.Where(o => o.IsArchived).Count();
```

Контекст кеширует один репозиторий на тип сущности, а не его построители.
Вызовите `Query()`, чтобы получить новый построитель, или начните запрос с
метода расширения, например `Where`. Для `Repository<T>(configure)` действует
то же правило первой регистрации маппинга на процесс, что и для `From<T>(configure)`.

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
