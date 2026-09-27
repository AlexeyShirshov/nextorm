# Глобальные фильтры запросов

> Фильтр запроса — это предикат, привязанный к типу сущности в метаданных отображения; nextorm добавляет его через `and` в каждый запрос, где сущность участвует — основной `FROM`, источники соединений и подзапросы — если только запрос не вызывает `IgnoreFilters()`.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Обзор провайдеров](../providers/overview.md) · [Фильтрация (`Where`)](../guide/01-filtering-where.md)

## Обзор

Глобальные фильтры запросов привязывают повторяющийся предикат — типовые случаи: soft-delete и multi-tenancy — к типу сущности, вместо того чтобы повторять его в каждом `Where`. Фильтр может читать исполняющий [`IDataContext`](xref:NextORM.Core.IDataContext), поэтому идентификатор тенанта или флаг soft-delete определяется для каждого запроса, а сгенерированный план остаётся кэшированным и разделяется между контекстами.

Это аналог глобальных фильтров запросов EF Core и query filters linq2db.

## Объявление фильтра

### Fluent

Объявите фильтр там, где настраивается отображение сущности через `From<T>`. Объявление выполняется один раз, при первом построении метаданных типа, поэтому условие становится частью отображения, а не одного запроса:

```csharp
ctx.From<Document>(m => m.HasQueryFilter(d => !d.IsDeleted));
```

Перегрузка с контекстом получает исполняющий [`IDataContext`](xref:NextORM.Core.IDataContext) вторым параметром, поэтому фильтр может читать состояние контекста:

```csharp
ctx.From<Document>(m => m.HasQueryFilter((d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

[`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) предоставляет обе перегрузки:

```csharp
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
```

Повторный вызов добавляет ещё один предикат; все объявленные для типа фильтры объединяются через `and`.

### Атрибут

Те же фильтры можно объявить декларативно с помощью [`[QueryFilter]`](xref:NextORM.Core.QueryFilterAttribute) на классе или интерфейсе. `FilterLambda` называет **статический член** атрибутируемого типа — поле, свойство или метод без параметров — возвращающий лямбду фильтра:

```csharp
[QueryFilter(FilterLambda = nameof(Active))]
public sealed class Document
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public static Expression<Func<Document, bool>> Active() => d => !d.IsDeleted;
}
```

Именованный член может также возвращать предикат, учитывающий контекст:

```csharp
[QueryFilter(FilterLambda = nameof(ForTenant))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Expression<Func<Document, IDataContext, bool>> ForTenant()
        => (d, c) => d.TenantId == (int)c.Properties["tenant"];
}
```

`AllowMultiple = true` означает, что на один тип можно поместить несколько атрибутов `[QueryFilter]`; они объединяются через `and` так же, как повторные fluent-вызовы.

## Чтение исполняющего контекста

Фильтр, чья лямбда принимает `IDataContext`, читает значение из контекста во время выполнения:

```csharp
public static Expression<Func<Document, IDataContext, bool>> ForTenant()
    => (d, c) => d.TenantId == (int)c.Properties["tenant"];
```

Значение тенанта не встраивается в SQL. Оно становится **runtime-параметром**, а идентичность фильтра (а не его текущие значения) входит в ключ плана, поэтому:

- два контекста с разными значениями тенанта могут разделять один кэшированный план;
- каждое выполнение читает **своё** значение контекста, даже при попадании в кэш плана.

Тот же предикат компилируется и перечитывает контекст при каждом вызове в провайдере in-memory.

## Отключение фильтров для одного запроса

[`IgnoreFilters()`](xref:NextORM.Core.EntityBuilder`1.IgnoreFilters) возвращает копию билдера с отключёнными фильтрами для этого запроса — для типа сущности запроса и для каждой сущности, соединённой с ним:

```csharp
var deleted = ctx.From<Document>()
    .IgnoreFilters()
    .Where(d => d.IsDeleted)
    .Select(d => d.Id)
    .ToList();
```

```csharp
public EntityBuilder<TEntity> IgnoreFilters();
```

Исходный билдер не меняется; фильтры игнорирует только возвращённая копия.

## Область применения: основной источник, соединения и подзапросы

Фильтр привязан к типу сущности и подмешивается везде, где этот тип встречается в форме запроса:

| Позиция | Куда подмешивается |
|---|---|
| Основной `FROM` | в `WHERE` инструкции, в сочетании с явным `Where` |
| Источник соединения | в условие `ON` соединения для присоединяемой сущности |
| Подзапрос | в подзапрос при его подготовке |

`IgnoreFilters()` отключает все их для запроса.

## Провайдер in-memory

Провайдер in-memory применяет тот же предикат к зарегистрированной последовательности до проекции и соединений, поэтому запросы soft-delete и multi-tenancy ведут себя так же, как у SQL-провайдеров. `IgnoreFilters()` там тоже учитывается.

## Метаданные

Объявленный фильтр доступен через [`IQueryFilterMetadata`](xref:NextORM.Core.IQueryFilterMetadata):

```csharp
public interface IQueryFilterMetadata
{
    string? Key { get; }          // null для анонимного фильтра
    LambdaExpression Lambda { get; }
}
```

`Key` зарезервирован для именованных фильтров; фильтр фазы 1 всегда возвращает `null`.

## Ограничения

- **Процессно-глобальная регистрация, побеждает первая.** Фильтры регистрируются **глобально на процесс**, и для типа сущности побеждает первая регистрация (согласовано с метаданными nextorm): последующий `From<T>(cfg)` / `HasQueryFilter` для того же типа игнорируется — фильтры не привязаны к `DataContext`.
- **Область `IgnoreFilters()`.** `IgnoreFilters()` подавляет фильтры для **основного источника** запроса; подавление фильтра отдельного соединённого билдера пока не поддерживается (фаза 2).
- **Время жизни плана.** Значение контекста, читаемое фильтром, захватывается как runtime-параметр (безопасно для кэша плана). Подготовленный план сохраняет первый экземпляр `IDataContext` на всё время существования (ограниченно, один на форму плана).

## Пока недоступно (фаза 2)

Следующее отложено и в этом релизе **недоступно**:

- именованные / keyed-фильтры — `HasQueryFilter(string filterKey, ...)` и не-`null` `IQueryFilterMetadata.Key`;
- форма `FilterFunc` — `Func<IQueryable<T>, IDataContext, IQueryable<T>>`;
- выборочное отключение — `IgnoreFilters(params Type[])` и перегрузки по ключам;
- фильтры на DML (`INSERT` / `UPDATE` / `DELETE`) — фильтры запросов применяются только к чтению;
- фильтры на `FromSql` / сырых источниках.
