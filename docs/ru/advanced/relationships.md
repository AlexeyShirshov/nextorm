# Связи и однозапросная загрузка (`JoinInto`)

> Связь объявляется явно в метаданных маппинга ([`HasMany`](xref:NextORM.Core.EntityMetadataBuilder`1)/[`HasOne`](xref:NextORM.Core.EntityMetadataBuilder`1) или [`[Relationship]`](xref:NextORM.Core.RelationshipAttribute)); затем [`JoinInto`](xref:NextORM.Core.EntityBuilder`1) загружает объявленную навигацию за один round trip и сшивает денормализованные строки обратно с дедуплицированными родителями: коллекцию one-to-many — через один `LEFT JOIN` (или `INNER JOIN`), nullable-ссылку one-to-one — через одно соединение, коллекцию many-to-many — через junction двумя плоскими соединениями (производное link-соединение плюс соединение child).

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Соединения](../guide/02-joins.md) · [Жадная загрузка дочерних коллекций](eager-loading.md)

## Обзор

nextorm разрешает граф из **объявленных** метаданных, а не по конвенции маппера: сущность без объявленных связей мапится в точности как раньше, а навигационное свойство исключается из маппинга колонок только если участвует в объявленной связи. Эта страница описывает модель метаданных и [`JoinInto`](xref:NextORM.Core.EntityBuilder`1) — явный однозапросный загрузчик связей. Неявные соединения, выводимые из навигации (`e.Parent.Name`), **не** реализованы.

`JoinInto` и [`LoadWith`](eager-loading.md) — два способа заполнить родительскую коллекцию, и они делят один контракт присваивания: `JoinInto` — один денормализованный запрос по всем родителям, `LoadWith` — split-запрос с одним дополнительным дочерним statement'ом на чанк ключей.

## Объявление связи

Связь объявляется на **каждой стороне независимо и симметрично**, во время маппинга через `From<T>`. Вид выводится из формы навигации — коллекция это one-to-many, ссылка это many-to-one — а обратная сторона **не** выводится.

### Fluent (`HasMany`/`HasOne`)

Principal-сторона объявляет навигацию-коллекцию и внешний ключ на зависимом типе:

```csharp
ctx.From<Order>(b => b.HasMany(o => o.Items, i => i.OrderId));
```

Dependent-сторона объявляет навигацию-ссылку и внешний ключ на **своём** типе:

```csharp
ctx.From<OrderItem>(b => b.HasOne(i => i.Order, i => i.OrderId));
```

Principal-ключ берётся из существующих метаданных ключа principal-типа (`[Key]`, [`EntityPropertyBuilder<T>.Key()`](xref:NextORM.Core.EntityPropertyBuilder`1) или конвенция `Id`/`<TypeName>Id`), поэтому обе стороны сходятся в пару one-to-many/many-to-one. Четыре перегрузки объявления:

```csharp
public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
    Expression<Func<T, ICollection<TChild>>> navigation,
    Expression<Func<TChild, TKey>> foreignKey);

public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, TKey>> foreignKey);
```

У каждой есть **fallback-перегрузка только по ключам** (`HasMany<TChild, TKey>(foreignKey, principalKey)` / `HasOne<TChild, TKey>(foreignKey, principalKey)`) для сущности, у которой связь не смоделирована навигационным свойством; она объявляет метаданные, не привязывая навигацию.

Первая регистрация для типа побеждает, как у фильтра запросов: последующая конфигурация `From<T>(...)` того же типа игнорируется. Свойство, участвующее в объявленной связи, исключается из [`IEntityMetadata.Properties`](xref:NextORM.Core.IEntityMetadata) и доступно только через [`IEntityMetadata.Relationships`](xref:NextORM.Core.IEntityMetadata); его маппинг как колонки бросает `InvalidOperationException`. Члены, не входящие в объявленную связь (включая ссылочный член без объявления), по-прежнему мапятся как колонки.

### Атрибут ([`[Relationship]`](xref:NextORM.Core.RelationshipAttribute))

Те же метаданные можно объявить декларативно на навигационном свойстве:

```csharp
public sealed class Order
{
    public int Id { get; set; }

    [Relationship(ForeignKey = nameof(OrderItem.OrderId))]
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

`ForeignKey` называет свойство внешнего ключа на зависимой стороне; вид выводится из формы навигации, как и во fluent API. Атрибут и fluent-объявление на **одной и той же** навигации дают одну модель, и при наличии обоих побеждает fluent-объявление.

## Загрузка через `JoinInto`

[`JoinInto`](xref:NextORM.Core.EntityBuilder`1) объявляет соединение, заполняющее объявленную навигацию — дочернюю коллекцию (one-to-many), nullable-ссылку (one-to-one) или коллекцию many-to-many через junction, — когда запрос перечисляется списочным терминалом. Он возвращает **копию** билдера, поэтому исходный билдер не меняется, а объявления выстраиваются в цепочку:

> **Примечание.** Прежний сценарий дочерней коллекции (issue #40) — загрузка детей из соединения родитель+ребёнок — сегодня выражается через `JoinInto` в **объявленное свойство-коллекцию**, как показано ниже. Проекция детей в произвольную (анонимную) форму, историческая форма `NORM.ChildCollection(...)`, **не предоставляется**: объявите коллекцию и загрузите её через `JoinInto` либо выберите дочерние строки явным соединением и материализуйте их самостоятельно.

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<OrderItem>(), (o, i) => o.Id == i.OrderId, o => o.Items)
    .ToList();
```

Три перегрузки:

```csharp
// LEFT (по умолчанию)
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection);

// явный вид соединения: только Inner или Left
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    JoinType joinType);

// fallback без объявленных метаданных: явные parent/child-ключи (всегда LEFT)
public EntityBuilder<TEntity> JoinInto<TChild, TKey>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    Expression<Func<TEntity, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey)
    where TKey : notnull;
```

Первые две разрешают parent/child-ключи из объявленной связи; третья симметрична fallback'у [`LoadWith`](eager-loading.md) для пары без метаданных связи:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<OrderItem>(), (o, i) => o.Id == i.OrderId, o => o.Items, o => o.Id, i => i.OrderId)
    .ToList();
```

`JoinInto` по умолчанию — **`LEFT`**: родитель без детей сохраняется с пустой коллекцией. Явная перегрузка принимает только [`JoinType.Inner`](xref:NextORM.Core.JoinType) и [`JoinType.Left`](xref:NextORM.Core.JoinType); `Inner` отбрасывает бездетных родителей.

### One-to-one (ссылка)

Связь one-to-one объявляется с principal-ключом на родителе и уникальным внешним ключом на ребёнке; уникальность внешнего ключа доверяется, ядром не проверяется:

```csharp
ctx.From<Order>(b => b.HasOneToOne(o => o.Invoice, o => o.Id, i => i.OrderId));
```

Загрузка использует перегрузки `JoinInto` по навигации-ссылке, которые присваивают единственного соединённого ребёнка ссылочному члену родителя:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(ctx.From<Invoice>(), (o, i) => o.Id == i.OrderId, o => o.Invoice)
    .ToList();
```

`JoinOptions.OneToOne<TParent, TChild, TKey>(parentKey, childForeignKey)` настраивает связь локально через лямбду опций `JoinInto` (те же два селектора), когда она не объявлена, полностью заменяя объявленные метаданные для этого вызова:

```csharp
var orders = ctx.From<Order>()
    .JoinInto(
        ctx.From<Invoice>(),
        (o, i) => o.Id == i.OrderId,
        o => o.Invoice,
        j => j.OneToOne<Order, Invoice, int>(o => o.Id, i => i.OrderId))
    .ToList();
```

Семантика LEFT/INNER повторяет загрузчики коллекций: при `Left` (по умолчанию) родитель без совпавшего ребёнка сохраняет ссылку `null`, а при `Inner` родитель без совпавшего ребёнка исключается. Родитель, совпавший с **более чем одним различным ребёнком**, бросает [`InvalidOperationException`](xref:System.InvalidOperationException) при материализации, тогда как декартовы повторы **одного и того же** ребёнка, вызванные соседним соединением, допускаются и схлопываются в единственное вхождение. Навигационный член должен быть доступен для записи — read-only ссылка бросает [`NotSupportedException`](xref:System.NotSupportedException).

### Many-to-many через junction

Связь many-to-many объявляется с явной сущностью-junction (связкой) — ядро её никогда не выводит. `HasManyThrough` принимает навигацию-коллекцию, ключи обеих principal-сторон и два внешних ключа junction, ссылающихся на них:

```csharp
ctx.From<Post>(b => b.HasManyThrough<Tag, TagLink, int, int>(
    p => p.Tags,      // навигация-коллекция на principal
    p => p.Id,        // родительский ключ
    l => l.PostId,    // внешний ключ junction на родительский ключ
    t => t.Id,        // ключ ребёнка
    l => l.TagId));   // внешний ключ junction на ключ ребёнка
```

Каждый внешний ключ junction должен совпадать по типу с ключом, на который он ссылается. Загрузка использует перегрузку `JoinInto` по коллекции с локальной конфигурацией `JoinOptions.ManyToMany` (те же четыре селектора), которая полностью заменяет объявленные метаданные для этого вызова:

```csharp
var posts = ctx.From<Post>()
    .JoinInto(
        ctx.From<Tag>(),
        (p, t) => t.Active,
        p => p.Tags,
        j => j.ManyToMany<Post, Tag, TagLink, int, int>(
            p => p.Id, l => l.PostId, t => t.Id, l => l.TagId))
    .ToList();
```

Предикат `(родитель, ребёнок) => ...` — дополнительный фильтр по соединённым строкам; ключи соединения берутся из конфигурации junction, а не из предиката. Используйте `(p, t) => true`, когда фильтр не нужен.

Семантика LEFT/INNER та же, что у коллекции one-to-many: при `Left` родитель без строки junction сохраняется с пустой коллекцией, а строка junction, ссылающаяся на отсутствующего ребёнка, не даёт элемента; при `Inner` родитель без совпавшего ребёнка исключается. Кратность отличается в одном месте: **дубли строк junction `(родитель, ребёнок)` сохраняются как отдельные элементы** — один и тот же ребёнок появляется по разу на каждую строку junction, — тогда как соединение one-to-many схлопывает дубли дочерних строк по идентичности ребёнка.

Когда в запросе две и более навигации-коллекции (one-to-one исключается), подготовка один раз на план выдаёт предупреждение `JoinInto.MultipleCollections`, потому что промежуточное число строк умножается в декартово произведение. Передайте `JoinOptions.SuppressCartesianWarning()` через лямбду опций `JoinInto` (`j => j.SuppressCartesianWarning()`), чтобы его заглушить; предупреждение информационное и не меняет результат.

**Составной** селектор junction и many-to-many `JoinInto` под [`AsSingleQuery`](eager-loading.md) отклоняются с [`NotSupportedException`](xref:System.NotSupportedException). Many-to-many `JoinInto` занимает **два** слота проекции, поэтому считается вдвойне против предела арности.

## Один round trip

Для списочного терминала соединение — **часть запроса**, и весь граф читается одним statement'ом:

```sql
select t1.id, t1.name, t2.id, t2.order_id, t2.name
from order as t1
left join order_item as t2 on t1.id = t2.order_id
```

Строки — денормализованный поток `(родитель, ребёнок)`; после выполнения запроса списочный терминал:

1. **дедуплицирует родителей** по их principal-ключу (побеждает экземпляр родителя из первой строки),
2. **группирует детей** по значению внешнего ключа,
3. присваивает каждую дочернюю коллекцию по общим правилам присваивания жадной загрузки (ниже).

Порядок родителей — порядок первого появления строк; порядок детей следует за statement'ом, поэтому добавляйте `OrderBy`, когда порядок важен. Дубли дочерних строк отбрасываются по идентичности ребёнка.

Дедупликация родителей и идентичность ребёнка опираются на **отображённый ключ** соответствующей стороны. Если у родителя (или ребёнка) не объявлен ключ, запрос с обычным `JoinInto` возвращается к **ссылочной идентичности**: на SQL-провайдере каждая денормализованная строка материализует новый экземпляр, поэтому повторы не схлопываются, а декартово произведение (две дочерние коллекции) остаётся продублированным. Single-query eager loading ([`LoadWith`](eager-loading.md) с `AsSingleQuery`) такого fallback'а не имеет — он требует отображённый ключ родителя и отклоняет родителя без ключа, — поэтому задайте родителю и ребёнку отображённый ключ (`[Key]`, `Key()` или соглашение `Id`/`<TypeName>Id`) либо используйте перегрузку с явными ключами и ключевым ребёнком для надёжной дедупликации. См. [Ограничения](limitations.md).

## Правило присваивания

Для каждого родителя загрузчик решает, как заполнить коллекцию, — так же, как [`LoadWith`](eager-loading.md#правило-назначения):

| Текущее значение `collection` | Член доступен для записи | Поведение |
|---|---|---|
| не null | любой | очищается и заполняется заново |
| `null` | да | назначается новый список |
| `null` | нет (read-only) | [`NotSupportedException`](xref:System.NotSupportedException) |

Read-only коллекция поддерживается, если она инициализирована пустой коллекцией и никогда не переприсваивается:

```csharp
public sealed class Order
{
    public int Id { get; set; }
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}
```

## Семантика `Where`

`Where` после `JoinInto` применяется к **соединённому запросу** — фильтрует денормализованные строки, как SQL `WHERE` поверх двух таблиц:

- при `Left` не-null-safe предикат (например, условие по ребёнку) может отбросить родителя целиком, когда ни один ребёнок не совпал, потому что действует семантика SQL `LEFT JOIN ... WHERE`;
- при `Inner` это обычный фильтр.

`Where` до `JoinInto` фильтрует родительский источник. Предикат, переданный в `JoinInto`, — это `ON`-условие: он только сопоставляет строки и никогда не убирает `LEFT`-родителя.

## Пагинация родителей

`Limit`/`Offset` (`Take`/`Page`) ограничивают **родителей**, а не денормализованные строки. На SQL-провайдере родительский источник выбирается в подзапросе с лимитом, а соединение оборачивается вокруг него, поэтому пагинация родителя не обрезает его детей. У in-memory провайдера лимит применяется к дедуплицированному списку родителей.

## Несколько коллекций

Каждый `JoinInto` добавляет своё соединение. Две дочерние коллекции у одного родителя дают **декартово произведение** строк в денормализованном потоке; дедупликация родителей и независимая группировка каждой коллекции сохраняют корректность результата, но промежуточное число строк умножается. Когда присутствуют две и более навигации-коллекции, подготовка один раз на план выдаёт предупреждение `JoinInto.MultipleCollections`; передайте `JoinOptions.SuppressCartesianWarning()` через лямбду опций `JoinInto`, чтобы его заглушить. Объявляйте несколько коллекций только когда наборы родителей невелики, или загружайте вторую коллекцию отдельным запросом ([`LoadWith`](eager-loading.md) — split-query альтернатива).

`JoinInto` **нельзя комбинировать с другими соединениями на одном билдере**: как только на билдере объявлен `JoinInto`, добавление `Join`, `LeftJoin`, `CrossJoin`, `SemiJoin`, `AntiJoin` (или любого другого явного соединения) бросает `NotSupportedException`, и наоборот. Объявляйте коллекции `JoinInto` на простом источнике сущности, чтобы не-списочные терминалы точно знали, какие соединения отбрасывать. Смешивание лишило бы родительскую команду возможности отличить явное соединение от `JoinInto`.

## Модификаторы соединения

Доступны модификаторы нижележащего соединения. В ClickHouse [`JoinOptions.WithStrictness`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithStrictness(NextORM.Core.JoinOptions,NextORM.Core.JoinStrictness)) можно передать в `JoinInto(...)` через его завершающую лямбду: `j => j.WithStrictness(Any)` оставляет только **первого** совпавшего ребёнка на родителя, поэтому загруженная коллекция усекается максимум до одного элемента; остальные модификаторы строгости (`All`, `Asof`) и [`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)) проходят без изменений. Опция, переданная в `JoinInto`, сохраняет поведение родительских терминалов — не-списочные терминалы по-прежнему исключают соединение.

## Граница терминалов

Соединение — часть команды, и сшивание выполняют **только** stitching-терминалы:

- [`ToList()`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) и [`ToListAsync()`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
- `ToArray()` и `ToArrayAsync()`.

[`ToCommand()`](xref:NextORM.Core.EntityBuilder`1.ToCommand) сохраняет соединение, но возвращает только **денормализованные** строки `(родитель, ребёнок)`, поэтому прямое перечисление может повторить родителя по разу на каждого совпавшего ребёнка. Любой другой терминал, построенный на родительской команде — `First`, `Count`, `Any`, `ToHashSet`, `ToEnumerable`, `ToAsyncEnumerable`, [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — исключает соединение и возвращает **только родителей**, не заполняя коллекции. Проекция `Select` или производный источник `As` отклоняются с [`NotSupportedException`](xref:System.NotSupportedException): `Select` сохранил бы соединение без сшивания (повторяя родителей и никогда не группируя детей), а `As` потерял бы метаданные сшивания. Материализуйте через `ToList`/`ToListAsync`, когда коллекции должны быть заполнены.

## Паритет in-memory

`JoinInto` работает на in-memory провайдере: соединение и группировка выполняются делегатами, а семантика LEFT/INNER, дедупликация родителей, порядок и пустые коллекции совпадают с SQL-провайдерами. In-memory провайдер пагинирует дедуплицированных родителей, а не оборачивает родительский подзапрос.

## Границы `NotSupportedException`

Следующее отклоняется с [`NotSupportedException`](xref:System.NotSupportedException):

- связь **many-to-many** под `AsSingleQuery` — `AsSingleQuery` это однозапросный режим [`LoadWith`](eager-loading.md); явный путь `JoinInto` M:N через junction поддерживает (см. выше);
- **составной** principal-, внешний или junction-ключ связи (поддерживаются только одноколоночные ключи);
- **необъявленная** связь, когда используется перегрузка по коллекции без метаданных связи, — объявите её через `HasMany`/`HasOne`/`HasManyThrough`/`[Relationship]` или используйте перегрузку с явными ключами; перегрузка по ссылке также требует объявленной one-to-one (`HasOneToOne`) либо локальной конфигурации `JoinOptions.OneToOne`;
- `JoinInto`, применённый к производному (`As`) или соединённому источнику проекции;
- проекция `Select` или производный источник `As` на билдере с `JoinInto` — оба нарушают контракт сшивания (повтор родителей или потеря метаданных); материализуйте через `ToList`/`ToListAsync`;
- `JoinInto` вместе с другим соединением на одном билдере (добавление любого явного соединения после `JoinInto` бросает исключение, как и добавление `JoinInto` после явного соединения) — объявляйте `JoinInto` только на простом источнике сущности;
- вид соединения, отличный от `Inner`/`Left`;
- селекторы явных ключей, выбирающие **разные типы свойств**, либо не являющиеся простым доступом к свойству/полю (у вычисляемого ключа нет стабильной идентичности плана);
- read-only член коллекции, равный `null` в рантайме;
- более семи дочерних коллекций в одном запросе (предел арности проекции).

Сами метаданные связи тоже «падают закрыто»: FK-свойство, не сопоставленное на зависимом типе, principal-тип без ключа или несовпадение типов FK/principal-ключа бросают `NotSupportedException` при разрешении ключей.

## Метаданные связей

Модель метаданных раскрывается через [`IEntityMetadata.Relationships`](xref:NextORM.Core.IEntityMetadata); каждый элемент — [`IRelationshipMetadata`](xref:NextORM.Core.IRelationshipMetadata) с [`Kind`](xref:NextORM.Core.IRelationshipMetadata) ([`RelationshipKind`](xref:NextORM.Core.RelationshipKind)), `DeclaringType`, `RelatedType`, `Navigation`, `IsCollection`, `ForeignKey`, `PrincipalKey` и `Inverse`. `Inverse` указывает на метаданные другой стороны, когда обе стороны объявлены и сходятся по внешнему ключу и типам; иначе он `null` — он никогда не выводится. `Inverse` разрешается **лениво при первом чтении и кэшируется**: зарегистрируйте связанный тип (и объявляющий тип) до чтения, потому что чтение, выполненное до регистрации другой стороны, закэширует `null` и не будет обновлено позднейшей регистрацией.

## См. также

- [Жадная загрузка дочерних коллекций (`LoadWith`)](eager-loading.md) — split-query альтернатива.
- [Соединения](../guide/02-joins.md) — явная поверхность соединений.
- [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) — ключи и маппинг колонок.

---

Источник: `tests/nextorm.sqlite.tests/JoinIntoSqlGenerationTests.cs:23`,
`tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs:43`,
`tests/nextorm.core.tests/JoinIntoInMemoryTests.cs:31`,
`tests/nextorm.core.tests/RelationshipMetadataTests.cs:173`.
