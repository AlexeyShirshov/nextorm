# Runtime mapping variant (вариант сопоставления)

Один класс сущности может обращаться более чем к одному физическому месту: база на арендатора, таблица
`{tenant}_orders` рядом с `orders` на одном сервере, отдельная схема арендатора. *Вариант сопоставления*
(mapping variant) и есть это решение времени выполнения: ключ, который слой маппинга превращает в имя
таблицы, схему, имена колонок и префикс параметров.

## Два контракта

| Контракт | На какой вопрос отвечает |
| --- | --- |
| [`IEntityVariantResolver`](xref:NextORM.Core.IEntityVariantResolver) | Какой вариант применяется к сущности, экземпляру или набору значений ключа |
| [`IEntityMetadataProvider`](xref:NextORM.Core.IEntityMetadataProvider) | Во что вариант отображается: метаданные сущности (таблица, схема, свойства) и имена колонок |

Вариант — это [`MappingVariant`](xref:NextORM.Core.MappingVariant): `readonly record struct` вокруг
nullable-строки-ключа. `MappingVariant.Default` (пустой ключ) — обычный маппинг без варианта: текст SQL и
имена параметров остаются ровно такими, какими были до появления вариантов.

Реализации по умолчанию — [`DefaultEntityVariantResolver`](xref:NextORM.Core.DefaultEntityVariantResolver)
(всегда возвращает `Default`) и [`DefaultEntityMetadataProvider`](xref:NextORM.Core.DefaultEntityMetadataProvider)
(маппинг через рефлексию). Подменить их можно на билдере:

```csharp
var builder = new DataContextBuilder()
    .UseVariantResolver(new TenantVariantResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

using var context = new PostgresDataContext(connectionString, builder);
```

## Явный вариант для одной команды

`WithVariant` есть у билдеров запросов (в том числе у соединённого билдера) и у `MergeBuilder<T>`:

```csharp
var variant = new MappingVariant("acme");

var orders = context.From<Order>()
    .WithVariant(variant)
    .Where(order => order.Total > 100m)
    .ToList();

context.CreateMergeBuilder<Order>()
    .Using(order)
    .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
    .WithVariant(variant)
    .Merge();
```

Вариант действует только на эту команду и никогда не меняет маппинг контекста по умолчанию. Вариант у
join, подзапроса или CTE принадлежит своему источнику, поэтому один запрос может отрендерить
`acme_orders` для внешнего запроса и `beta_orders` для подзапроса с вариантом `beta`.

## Автоматическое определение варианта

Обычно приложению не нужно передавать вариант в каждый вызов. Билдеры обращаются к
`IEntityVariantResolver`:

- `Resolve(Type)` — чтение (`From<T>()`), вставка, delete/update по предикату, truncate;
- `Resolve(Type, object)` — update, delete или merge, где цель — экземпляр сущности;
- `Resolve(Type, IReadOnlyList<object?>)` — явные значения ключа.

```csharp
public sealed class TenantVariantResolver : IEntityVariantResolver
{
    public MappingVariant Resolve(Type entityType)
        => new(AmbientTenant.Current
            ?? throw new InvalidOperationException("A tenant must be set before querying tenant-mapped entities."));

    public MappingVariant Resolve(Type entityType, object entity) => Resolve(entityType);

    public MappingVariant Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
}
```

Явный `WithVariant(...)` на билдере запроса или merge приоритетнее резолвера. Возврат
`MappingVariant.Default` сохраняет обычный маппинг — именно так поступает резолвер по умолчанию.

## Отображение варианта на физические имена

Провайдер читает базовый маппинг и выводит из него маппинг для варианта. Если выводить из метаданных
по умолчанию, все маппинги свойств остаются нетронутыми:

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

internal sealed class TenantMetadata(IEntityMetadata inner, string tenant) : IEntityMetadata
{
    public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
    public string? TableName => $"{inner.TableName}_{tenant}";
    public string? Schema => inner.Schema;
    public bool IsTableNameAuto => inner.IsTableNameAuto;
}
```

Такой маппинг даёт `orders_acme` и `total_acme` для варианта `acme` и нетронутые `orders` / `total` для
варианта по умолчанию.

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

## Ограничения

- Провайдер по умолчанию строит одинаковый маппинг для любого варианта: имена меняются только тогда, когда
  провайдер выводит их из ключа.
- `WithVariant` есть у билдеров запросов и merge. Insert, update, delete и truncate берут ответ резолвера.
- Один вариант — один закэшированный план на форму запроса: много арендаторов, умноженное на много форм,
  растит план-кэш, и каждая запись держит собственную подготовленную команду.
- Вариант переименовывает объекты, но не добавляет предикаты. Таблица, общая для нескольких арендаторов,
  по-прежнему требует колонку арендатора в запросе или в
  [глобальном фильтре запросов](../advanced/query-filters.md).
