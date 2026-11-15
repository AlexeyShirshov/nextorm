# Runtime mapping scope (область сопоставления)

Один класс сущности может обращаться более чем к одному физическому месту: база на арендатора, таблица
`{tenant}_orders` рядом с `orders` на одном сервере, отдельная схема арендатора или — поверх всего
этого — шард. *Область сопоставления* (mapping scope) и есть это решение времени выполнения: ключ,
который слой маппинга превращает в имя таблицы, схему, имена колонок и префикс параметров.

Механизм не зависит ни от провайдера, ни от шардирования.
[Шардирование и горизонтальное партиционирование](33-sharding-and-horizontal-partitioning.md) — один из
его потребителей.

## Два контракта

| Контракт | На какой вопрос отвечает |
| --- | --- |
| [`IEntityScopeResolver`](xref:NextORM.Core.IEntityScopeResolver) | Какая область применяется к сущности, экземпляру или набору значений ключа |
| [`IEntityMetadataProvider`](xref:NextORM.Core.IEntityMetadataProvider) | Во что область отображается: метаданные сущности (таблица, схема, свойства) и имена колонок |

Область — это [`MappingScope`](xref:NextORM.Core.MappingScope): `readonly record struct` вокруг
nullable-строки-ключа. `MappingScope.Default` (пустой ключ) — обычный маппинг без области: текст SQL и
имена параметров остаются ровно такими, какими были до появления областей.

Реализации по умолчанию — [`DefaultEntityScopeResolver`](xref:NextORM.Core.DefaultEntityScopeResolver)
(всегда возвращает `Default`) и [`DefaultEntityMetadataProvider`](xref:NextORM.Core.DefaultEntityMetadataProvider)
(маппинг через рефлексию). Подменить их можно на билдере:

```csharp
var builder = new DataContextBuilder()
    .UseScopeResolver(new TenantScopeResolver())
    .UseEntityMetadataProvider(new TenantMetadataProvider());

using var context = new PostgresDataContext(connectionString, builder);
```

## Явная область для одной команды

`WithScope` есть у билдеров запросов (в том числе у соединённого билдера) и у `MergeBuilder<T>`:

```csharp
var scope = new MappingScope("acme");

var orders = context.From<Order>()
    .WithScope(scope)
    .Where(order => order.Total > 100m)
    .ToList();

context.CreateMergeBuilder<Order>()
    .Using(order)
    .OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert()
    .WithScope(scope)
    .Merge();
```

Область действует только на эту команду и никогда не меняет маппинг контекста по умолчанию. Область у
join, подзапроса или CTE принадлежит своему источнику, поэтому один запрос может отрендерить
`acme_orders` для внешнего запроса и `beta_orders` для подзапроса с областью `beta`.

## Автоматическое определение области

Обычно приложению не нужно передавать область в каждый вызов. Билдеры обращаются к
`IEntityScopeResolver`:

- `Resolve(Type)` — чтение (`From<T>()`), вставка, delete/update по предикату, truncate;
- `Resolve(Type, object)` — update, delete или merge, где цель — экземпляр сущности;
- `Resolve(Type, IReadOnlyList<object?>)` — явные значения ключа.

```csharp
public sealed class TenantScopeResolver : IEntityScopeResolver
{
    public MappingScope Resolve(Type entityType)
        => new(AmbientTenant.Current
            ?? throw new InvalidOperationException("A tenant must be set before querying tenant-mapped entities."));

    public MappingScope Resolve(Type entityType, object entity) => Resolve(entityType);

    public MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues) => Resolve(entityType);
}
```

Явный `WithScope(...)` на билдере запроса или merge приоритетнее резолвера. Возврат
`MappingScope.Default` сохраняет обычный маппинг — именно так поступает резолвер по умолчанию.

## Отображение области на физические имена

Провайдер читает базовый маппинг и выводит из него маппинг для области. Если выводить из метаданных
по умолчанию, все маппинги свойств остаются нетронутыми:

```csharp
public sealed class TenantMetadataProvider : IEntityMetadataProvider
{
    private readonly DefaultEntityMetadataProvider _inner = new();

    public IEntityMetadata GetMetadata(Type entityType, in MappingScope scope)
    {
        var metadata = _inner.GetMetadata(entityType, MappingScope.Default);
        if (scope == MappingScope.Default)
            return metadata;

        var tenant = scope.Key ?? throw new InvalidOperationException("A tenant scope needs a key.");
        return new TenantMetadata(metadata, tenant);
    }

    public string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention)
        => scope == MappingScope.Default
            ? _inner.GetColumnName(property, scope, convention)
            : $"{scope.Key}_{_inner.GetColumnName(property, MappingScope.Default, convention)}";
}

internal sealed class TenantMetadata(IEntityMetadata inner, string tenant) : IEntityMetadata
{
    public IReadOnlyList<IPropertyMetadata> Properties => inner.Properties;
    public string? TableName => $"{inner.TableName}_{tenant}";
    public string? Schema => inner.Schema;
    public bool IsTableNameAuto => inner.IsTableNameAuto;
}
```

Такой маппинг даёт `orders_acme` и `total_acme` для области `acme` и нетронутые `orders` / `total` для
области по умолчанию.

Что важно не потерять:

- **Всегда делегируйте область по умолчанию.** Провайдер, который переписывает `MappingScope.Default`,
  меняет поведение всех существующих запросов.
- **Переиспользуйте внутренние `Properties`**, если область меняет только таблицу или схему. Новый
  `IPropertyMetadata` только с `PropertyInfo` и `ColumnName` теряет `IsKey`, `IsIdentity`, конвертер
  значений, range-колонки, точность `duration` и `decimal` — после этого ломаются DML и материализация.
- **`GetColumnName` — точка подмены имён колонок.** Для области, отличной от области по умолчанию,
  рендерер спрашивает имя каждой колонки у провайдера (проекция запроса, `WHERE`, список колонок
  `INSERT`, `UPDATE`/`DELETE`, `RETURNING`), а результат материализуется позиционно, поэтому
  переименованный список колонок остаётся согласованным между SQL и строками.
- **Провайдер — доверенный код.** Ключ не экранируется, кавычки не делают произвольный ключ безопасным.
  Валидируйте ключ (например `^[a-z0-9_]{1,32}$`) или отображайте его в разрешённое физическое имя.

## Что изолирует область

- **Метаданные** кэшируются по `(тип сущности, область)`, поэтому две области никогда не делят таблицу,
  схему или список колонок. Этим кэшем владеет провайдер по умолчанию; собственный провайдер держит своё
  состояние и обязан изолировать его так же.
- **Планы** тоже ключуются областью: один и тот же текст запроса переиспользуется для той же области и
  никогда — между разными областями, в том числе через быстрые пути переиспользования планов без значений.
- **Имена параметров** получают префикс области (`MappingScope.ParameterPrefix`, например `acme_`),
  поэтому две области в одном батче не конфликтуют: `... where total > $acme_minimum`.
- **DML** рендерит целевую таблицу и колонки с учётом области: insert, update, delete и truncate
  определяют её до построения команды.
- **Соединение не является частью области сопоставления.** Область меняет только имена на соединении
  самого контекста. Маршрутизация области на другой сервер — шард — это отдельный слой.

## Область и шардирование

Шардирование построено на этом механизме и ничего к его контракту не добавляет:

- `ShardRouter` реализует `IEntityScopeResolver` и использует **номер бакета** как ключ;
- `ShardEntityMetadataProvider` реализует `IEntityMetadataProvider` и выводит `{table}_{bucket}` или
  `{schema}_{bucket}` из этого ключа;
- топология шардов, соединение на шард и fan-out по бакетам находятся поверх этих двух контрактов.

Ничего на этой странице не требует настроенного шардирования. Шард-специфичная часть — в разделе
[Шардирование и горизонтальное партиционирование](33-sharding-and-horizontal-partitioning.md).

## Ограничения

- Провайдер по умолчанию строит одинаковый маппинг для любой области: имена меняются только тогда, когда
  провайдер выводит их из ключа.
- `WithScope` есть у билдеров запросов и merge. Insert, update, delete и truncate берут ответ резолвера
  (шардирование добавляет `WithShardKey(...)` для явного бакета).
- Одна область — один закэшированный план на форму запроса: много арендаторов, умноженное на много форм,
  растит план-кэш, и каждая запись держит собственную подготовленную команду.
- Область переименовывает объекты, но не добавляет предикаты. Таблица, общая для нескольких арендаторов,
  по-прежнему требует колонку арендатора в запросе или в
  [глобальном фильтре запросов](../advanced/query-filters.md).
