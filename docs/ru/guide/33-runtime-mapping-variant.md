# Runtime mapping variant (вариант сопоставления)

Один класс сущности может обращаться более чем к одному физическому месту, и какое именно место он
адресует — решение работающего приложения, а не класса: база на арендатора, таблица `orders` на
арендатора в одной базе (`orders_acme`, `orders_beta`), схема на арендатора. Имя таблицы, схема и даже
имена колонок зависят от того, *кто спрашивает*.

Обычные альтернативы хуже. Сгенерированный класс на арендатора размножает модель и миграции; имя,
задаваемое в каждом вызове, размазывает одно решение по коду и ломается, как только join смешивает два
источника; глобальное переименование не обслуживает двух арендаторов из одного процесса. NextORM
оставляет один отображённый класс, а слой имён делает **поиском во время выполнения**, поэтому один и тот
же `Order` рендерит `orders_acme` для одного вызывающего и `orders_beta` для другого на том же соединении.

Этот поиск и есть *вариант сопоставления* (mapping variant): ключ, который слой маппинга превращает в имя
таблицы, схему, имена колонок и префикс параметров.

## Термины

| Термин | Значение |
| --- | --- |
| [`MappingVariant`](xref:NextORM.Core.MappingVariant) | `readonly record struct` вокруг nullable-строки-ключа. Это значение передаётся, хранится и сравнивается. |
| Ключ | То, что приложение кладёт внутрь варианта. Здесь — slug арендатора (`acme`), в [шардировании](35-sharding-and-horizontal-partitioning.md) — номер бакета. Библиотека его не интерпретирует. |
| Вариант по умолчанию | `MappingVariant.Default` — пустой ключ, то есть обычный маппинг: текст SQL и имена параметров остаются ровно такими, какими были до появления вариантов. |
| [`IEntityVariantResolver`](xref:NextORM.Core.IEntityVariantResolver) | Отвечает, *какой* вариант применяется к типу сущности, к экземпляру или к набору значений ключа. |
| [`IEntityMetadataProvider`](xref:NextORM.Core.IEntityMetadataProvider) | Отвечает, *во что* вариант отображается: метаданные сущности (таблица, схема, свойства) и имена колонок. |

## Как это работает

Движок задаёт два вопроса, по одному на каждый контракт:

1. **Какой вариант?** Билдеры обращаются к `IEntityVariantResolver` — `Resolve(Type)` для чтения,
   вставки, update/delete по предикату или truncate, `Resolve(Type, object)` для команды, чья цель —
   экземпляр сущности, `Resolve(Type, IReadOnlyList<object?>)` для явных значений ключа. Возврат
   `MappingVariant.Default` сохраняет обычный маппинг — именно так поступает
   [`DefaultEntityVariantResolver`](xref:NextORM.Core.DefaultEntityVariantResolver) для всего.
2. **Во что отображается вариант?** `IEntityMetadataProvider.GetMetadata(entityType, variant)` возвращает
   таблицу и схему для этого варианта, а `GetColumnName(property, variant, convention)` — имя колонки.
   [`DefaultEntityMetadataProvider`](xref:NextORM.Core.DefaultEntityMetadataProvider) — маппинг через
   рефлексию, он вариант игнорирует, поэтому имена меняются только когда провайдер выводит их из ключа.

Обе реализации по умолчанию уже установлены, пока их не подменили на билдере:

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(resolver)               // IEntityVariantResolver
    .UseEntityMetadataProvider(provider);       // IEntityMetadataProvider
```

Команда может взять вариант напрямую — `WithVariant(...)` у билдеров запросов и merge; для этой команды
он приоритетнее резолвера и никогда не меняет маппинг контекста по умолчанию. У insert, update, delete и
truncate `WithVariant` нет — они берут ответ резолвера.

Всё выведенное из варианта ключуется им, и благодаря этому два варианта сосуществуют в одном контексте:
метаданные кэшируются по `(тип сущности, вариант)`, планы — по варианту, имена параметров получают префикс
варианта (`MappingVariant.ParameterPrefix`), а DML рендерит целевую таблицу и колонки варианта.

## Что изолирует вариант

- **Метаданные** кэшируются по `(тип сущности, вариант)`, поэтому два варианта никогда не делят таблицу,
  схему или список колонок. Этим кэшем владеет провайдер по умолчанию; собственный провайдер держит своё
  состояние и обязан изолировать его так же.
- **Планы** тоже ключуются вариантом: один и тот же текст запроса переиспользуется для того же варианта и
  никогда — между разными вариантами, в том числе через быстрые пути переиспользования планов без значений.
- **Имена параметров** получают префикс варианта (`MappingVariant.ParameterPrefix`, например `acme_`),
  поэтому два варианта в одном батче не конфликтуют: `... where total > $acme_minimum`.
- **DML** рендерит целевую таблицу и колонки с учётом варианта: insert, update, delete и truncate
  определяют его до построения команды.
- **Соединение не является частью варианта сопоставления.** Вариант меняет только имена на соединении
  самого контекста; выход на другой сервер — отдельный слой.

## Вариант арендатора

Дальше на странице — один пример: сущность `Order` отображена на таблицу `orders` без схемы, а ключ —
slug арендатора, поэтому `acme` даёт `orders_acme` / `total_acme`, `beta` — `orders_beta` / `total_beta`,
а вариант по умолчанию — `orders` / `total`.

**1. Получить ключ.**

```csharp
public sealed class TenantVariantResolver : IEntityVariantResolver
{
    // Ключ — slug арендатора, здесь "acme"; текущий арендатор определяет его для каждого вызова.
    public MappingVariant Resolve(Type entityType)
        => new(AmbientTenant.Current
            ?? throw new InvalidOperationException("A tenant must be set before querying tenant-mapped entities."));

    public MappingVariant Resolve(Type entityType, object entity) => Resolve(entityType);

    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
}
```

**2. Отобразить ключ на физические имена.**

```csharp
public sealed class TenantMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingVariant variant)
    {
        var metadata = _inner.GetMetadata(entityType, MappingVariant.Default);
        if (variant == MappingVariant.Default)
            return metadata;

        var tenant = variant.Key ?? throw new InvalidOperationException("A tenant variant needs a key.");
        return new TenantMetadata(metadata, tenant);
    }

    public string GetColumnName(PropertyInfo property, in MappingVariant variant, INamingConvention? convention)
        => variant == MappingVariant.Default
            ? _inner.GetColumnName(property, variant, convention)
            : $"{variant.Key}_{_inner.GetColumnName(property, MappingVariant.Default, convention)}";
}

// IEntityMetadata с суффиксом арендатора: таблица "orders" -> "orders_acme"; свойства переиспользуются как есть.
internal sealed class TenantMetadata(IEntityMetadata inner, string tenant) : IEntityMetadata
{
    public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
    public string? TableName => $"{inner.TableName}_{tenant}";
    public string? Schema => inner.Schema;
    public bool IsTableNameAuto => inner.IsTableNameAuto;
}
```

**3. Связать оба с контекстом.**

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(new TenantVariantResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

using var context = new PostgresDataContext(connectionString, builder);   // строка подключения провайдера
```

Дальше `context.From<Order>()` читает `orders_acme`, пока `AmbientTenant.Current` равен `"acme"`, и
`orders_beta`, пока он `"beta"`.

Что важно не потерять:

- **Всегда делегируйте вариант по умолчанию.** Провайдер, который переписывает `MappingVariant.Default`,
  меняет поведение всех существующих запросов.
- **Переиспользуйте внутренние `Properties`**, если вариант меняет только таблицу или схему. Новый
  `IPropertyMetadata` только с `PropertyInfo` и `ColumnName` теряет `IsKey`, `IsIdentity`, конвертер
  значений, range-колонки, точность `duration` и `decimal` — после этого ломаются DML и материализация.
- **`GetColumnName` — точка подмены имён колонок.** Для варианта, отличного от варианта по умолчанию,
  рендерер спрашивает имя каждой колонки у провайдера (проекция запроса, `WHERE`, список колонок
  `INSERT`, `UPDATE`/`DELETE`, `RETURNING`), а результат материализуется позиционно, поэтому
  переименованный список колонок остаётся согласованным между SQL и строками.
- **Провайдер — доверенный код.** Ключ не экранируется, кавычки не делают произвольный ключ безопасным.
  Валидируйте ключ (например `^[a-z0-9_]{1,32}$`) или отображайте его в разрешённое физическое имя.

## Явный вариант для одной команды

Резолвер — не единственный источник ключа: `WithVariant` принимает тот же ключ явно, для одной команды:

```csharp
var variant = new MappingVariant("acme");      // slug арендатора — тот же ключ, что выше

var orders = context.From<Order>()
    .WithVariant(variant)                      // читает orders_acme
    .Where(order => order.Total > 100m)
    .ToList();

var incoming = new Order { Id = 1, Tenant = "acme", Total = 250m };

context.CreateMergeBuilder<Order>()
    .Using(incoming)                           // пишет в orders_acme
    .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
    // Не обязательно, если текущий арендатор уже выбирает "acme": резолвер вернёт тот же вариант.
    // `Tenant` только записывается в строку и таблицу не выбирает (см. пояснение ниже).
    .WithVariant(variant)
    .Merge();
```

Вариант действует только на эту команду и никогда не меняет маппинг контекста по умолчанию. Вариант у
join, подзапроса или CTE принадлежит своему источнику, поэтому один запрос может отрендерить
`orders_acme` для внешнего запроса и `orders_beta` для подзапроса с вариантом `beta`.

Явный вызов повторяет ключ, который резолвер вернул бы при создании билдера, поэтому его можно убрать,
когда текущий арендатор уже выбирает нужную таблицу, и нужно передать, когда нет. Обратите внимание:
`incoming.Tenant` в этом решении не участвует — билдер получает вариант по **типу** сущности
(`CreateMergeBuilder` один раз вызывает `Resolve(typeof(TEntity))`), а не по значениям источника. По
экземпляру маршрутизируются только шардированные сущности, и шардированный merge всё равно требует явный
вариант.

## Ограничения

- Провайдер по умолчанию строит одинаковый маппинг для любого варианта: имена меняются только тогда, когда
  провайдер выводит их из ключа.
- `WithVariant` есть у билдеров запросов и merge. Insert, update, delete и truncate берут ответ резолвера.
- Один вариант — один закэшированный план на форму запроса: много арендаторов, умноженное на много форм,
  растит план-кэш, и каждая запись держит собственную подготовленную команду.
- Вариант переименовывает объекты, но не добавляет предикаты. Таблица, общая для нескольких арендаторов,
  по-прежнему требует колонку арендатора в запросе или в
  [глобальном фильтре запросов](../advanced/query-filters.md).
