# Наследование и производные сущности

> nextorm не владеет стратегией наследования. Движок отображает один CLR-тип на одну таблицу и не несёт
> дискриминатора, поэтому реляционная форма иерархии — решение на вашей стороне, ровно как в SQL,
> написанном вручную. Движок даёт кирпичи: унаследованные записываемые свойства, соединения, проекции,
> `UNION ALL`, JSON- и динамические колонки. Ниже — четыре формы маппинга, чтение смешанного списка и то,
> что остаётся вашей ответственностью.

**Предварительно:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Соединения (JOIN)](04-joins.md) · [Операции над множествами](08-set-operations.md) · [Поддержка JSON](16-json.md) · [Динамические колонки](29-dynamic-columns.md)

## Где проходит граница

Единица маппинга — **CLR-тип**. Когда строятся метаданные для `T`, отображаются его публичные
записываемые свойства, включая унаследованные от базового класса (`GetProperties(FlattenHierarchy | Public | Instance)`,
см. `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs`). Это даёт обычное CLR-наследование
бесплатно: базовый класс может держать общие колонки, а каждый конкретный тип мапится на свою таблицу.

Чего движок **не** делает — не интерпретирует иерархию:

* у него нет колонки-дискриминатора и регистрации «база/наследник»;
* он никогда не переключает материализуемый CLR-тип в рантайме;
* он никогда не объединяет таблицы конкретных типов для запроса по базовому типу;
* он не создаёт таблицы, представления, внешние ключи и check-ограничения.

Именно эти части выбираете и обеспечиваете вы. Дальше — каталог форм, которые работают в этих границах.

## Выбор формы

| Если нужно | Форма | Что даёт nextorm |
|---|---|---|
| Одна таблица, немного необязательных полей подтипов, фильтрация по виду | Колонка-дискриминатор + плоская сущность или сущность на подтип (TPH вручную) | Обычные колонки и `Where` |
| У каждого подтипа своя таблица, запросы всегда адресованы известному типу | Базовый класс + таблица на конкретный тип (TPC вручную) | Унаследованные записываемые свойства попадают в каждый конкретный тип |
| Общие колонки + таблица только для подтипа, с целостностью на уровне подтипа | Базовая таблица + таблица подтипа (TPT вручную) | `Join`/`LeftJoin`, `HasOneToOne`/`JoinInto`, транзакции |
| Открытые или редко используемые поля подтипов | JSON-колонка или хранилище `[DynamicColumns]` | `[JsonColumn]`, `[DynamicColumns]` |
| Один смешанный список по нескольким таблицам | DTO-проекция через `UNION ALL`, представление БД или запросы по типам | `Select`, `UnionAll`, `[SqlTable]` на представлении |

## Одна таблица с дискриминатором (TPH вручную)

Это физическая форма TPH, но без переключения типа в рантайме: одна таблица, колонка-дискриминатор и
nullable-колонки для полей, которые нужны только части подтипов.

### Одна плоская сущность

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";       // "standard" / "express"
    public string Number { get; set; } = "";
    public decimal? Total { get; set; }          // standard orders
    public DateTime? PromisedAt { get; set; }    // express orders
}
```

```csharp
var express = ctx.From<Order>()
    .Where(o => o.Kind == "express" && o.PromisedAt < cutoff)
    .ToList();
```

* `Kind` — обычная отображаемая колонка. Значение задаётся вручную при вставке/обновлении;
  `CHECK`-ограничение или триггер — ваш выбор схемы, nextorm их не генерирует.
* Колонки, нужные только подтипу, должны быть nullable (или нести sentinel-значение), потому что
  таблица общая для всех строк.
* В этом варианте сущность всегда `Order`; экземпляра `ExpressOrder` для материализации нет.
* Это самая дешёвая форма, когда полей подтипов немного и большинство запросов фильтруются по
  дискриминатору.

### Сущность на подтип, одна таблица

Производная сущность может мапить ту же таблицу и раскрывать только колонки своего подтипа; движок не
запрещает двум типам сущностей делить таблицу. Это всё ещё не ORM-TPH: метаданных дискриминатора нет,
поэтому фильтр — на вас в каждом запросе.

```csharp
[SqlTable("orders")]
public abstract class OrderBase
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
}

// Оба наследуют [SqlTable("orders")].
public sealed class StandardOrder : OrderBase
{
    public decimal Total { get; set; }
}

public sealed class ExpressOrder : OrderBase
{
    public DateTime PromisedAt { get; set; }
}
```

```csharp
var express = ctx.From<ExpressOrder>()
    .Where(o => o.Kind == "express" && o.PromisedAt < cutoff)
    .ToList();
```

* `From<ExpressOrder>()` читает **все** строки `orders`; nextorm не добавляет `Kind = "express"`.
  `Where` обязателен. Если в материализатор попадёт standard-строка, её `PromisedAt` — `NULL`, и
  non-nullable `DateTime` бросит `InvalidOperationException: The data is NULL ...`.
* Общая таблица должна держать `PromisedAt` и `Total` nullable (или с default), потому что в строке
  используется только одна из них. `CHECK`-ограничение может зафиксировать
  `(Kind = 'express' AND PromisedAt IS NOT NULL) OR (Kind = 'standard' AND Total IS NOT NULL)`.
* `ExpressOrder` мапит общие колонки плюс `PromisedAt`; `Total` он не мапит, а `StandardOrder` не
  мапит `PromisedAt`. В этом и смысл разделения. Запись — по подтипу:
  `CreateInsertBuilder<ExpressOrder>()` пишет общие колонки и `PromisedAt`, а `Kind` вы задаёте сами;
  колонка другого подтипа должна быть nullable/с default.
* Для страховки повесьте глобальный фильтр на `ExpressOrder`, чтобы предикат дискриминатора
  добавлялся автоматически ([Глобальные фильтры запросов](../advanced/query-filters.md)); целостность
  всё равно обеспечивает `CHECK` в БД, а raw SQL фильтр обходит.
* `[SqlTable]` на базе наследуется — именно поэтому все подтипы мапятся на `orders`; в форме TPC ниже
  то же наследование требует явно называть таблицу на каждом конкретном типе.
* Дискриминатор-перечисление работает только там, где провайдер умеет читать enum; иначе храните
  `int`/`string` или мапьте enum в текст через `EnumToStringConverter<TEnum>`
  ([Конвертеры значений](../infrastructure/03-value-converters.md)).

## Таблица на конкретный тип (TPC вручную)

Общие колонки — в базовом классе, каждый конкретный тип мапится на свою таблицу. Это ближайшая форма к
TPC: каждая конкретная таблица содержит полный набор колонок, включая унаследованные.

```csharp
public abstract class OrderBase
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

[SqlTable("order_standard")]
public sealed class StandardOrder : OrderBase
{
    public decimal Total { get; set; }
}

[SqlTable("order_express")]
public sealed class ExpressOrder : OrderBase
{
    public decimal Total { get; set; }
    public DateTime PromisedAt { get; set; }
}
```

```csharp
var express = ctx.From<ExpressOrder>()
    .Where(o => o.PromisedAt < cutoff)
    .ToList();

ctx.CreateInsertBuilder<ExpressOrder>()
    .Values(new ExpressOrder
    {
        Id = 10,
        Number = "E-10",
        CreatedAt = DateTime.UtcNow,
        Total = 42m,
        PromisedAt = promised,
    })
    .Insert();
```

Правила и оговорки:

* `OrderBase` — удобство C#, **не сущность**. Он не запрашивается и не имеет собственной таблицы.
* Имя таблицы задавайте явно на каждом конкретном типе. `[SqlTable]`/`[Table]` наследуется от базы,
  поэтому атрибут на базовом классе иначе отправил бы все производные типы в одну таблицу.
* `[Key]`, `[Column]`, `[DatabaseGenerated]` и другие атрибуты маппинга, объявленные на свойствах
  базы, применяются к конкретному типу. Не скрывайте отображаемое свойство через `new`: побеждает
  член производного типа, а атрибуты скрытого базового члена не применяются. Лучше не переобъявлять
  свойство вовсе или использовать `virtual`/`override`, если действительно нужно изменить поведение.
* Колонками становятся только публичные **записываемые** свойства. Вычисляемый getter на базе — это
  обычный C# и в маппинг не входит; объявляйте колонки свойствами с сеттером.
* Чтение — по типу. Нет ни `OfType<T>()`, ни запроса по базовому типу, возвращающего смесь
  `StandardOrder` и `ExpressOrder`, — см. [Чтение смешанного списка](#чтение-смешанного-списка).
* Эволюция схемы ручная: добавление колонки в базу означает `ALTER` каждой конкретной таблицы.

## Общая базовая таблица (TPT вручную)

Если общих колонок много или важны внешние ключи и ограничения на уровне подтипа, держите общие
колонки в базовой таблице, а в боковой — только специфичные для подтипа.

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

[SqlTable("order_express")]
public sealed class OrderExpress
{
    [Key] public long OrderId { get; set; }  // same value as orders.Id
    public DateTime PromisedAt { get; set; }
}
```

Чтение через соединение:

```csharp
var rows = ctx.From<Order>()
    .Join<OrderExpress>((o, e) => o.Id == e.OrderId)
    .Select(p => new
    {
        p.Item1.Id,
        p.Item1.Number,
        p.Item2.PromisedAt,
    })
    .ToList();
```

Либо объявите связь один-к-одному через `HasOneToOne` на сущности, которая раскрывает деталь как
навигационное свойство, и позвольте `JoinInto` заполнить её за один round trip
([Связи](../relationships/relationships.md#one-to-one-ссылка)).

Пишите обе строки в одной транзакции — unit of work в nextorm нет, поэтому обе команды явные:

```csharp
var transactions = (ITransactionManager)ctx;
await using var tx = await transactions.BeginTransactionAsync();

ctx.CreateInsertBuilder<Order>().Values(order).Insert();
ctx.CreateInsertBuilder<OrderExpress>().Values(details).Insert();

await tx.CommitAsync();
```

Связь первичного и внешнего ключа и её уникальность — часть вашей схемы. nextorm не создаёт и не
проверяет ограничения; объявление один-к-одному — контракт времени загрузки, а не ограничение БД
(см. [Связь один-к-одному](../relationships/relationships.md#one-to-one-ссылка)).

## Боковой payload: JSON или динамические колонки

Когда поля подтипа открытые, разреженные или принадлежат расширению, держать их вне реляционной формы
часто лучше, чем добавлять nullable-колонки.

**JSON-колонка.** Храните объект-payload в одной колонке и считайте его непрозрачным в переносимом коде:

```csharp
public sealed record ExpressPayload(DateTime PromisedAt, string? Courier);

[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Kind { get; set; } = "";

    [JsonColumn]
    public ExpressPayload? Express { get; set; }
}
```

Свойство `[JsonColumn]` хранит любой сериализуемый CLR-объект; тип провайдера и режим хранения описаны в
[Поддержке JSON в разных провайдерах](16-json.md#отобразить-clr-объект-на-нативную-json-колонку).
Фильтрация внутри payload зависит от провайдера (нативные JSON-функции или JSON-как-текст), поэтому это
не переносимая поверхность запросов.

**Динамические колонки.** Держите на сущности словарь неотображённых физических колонок:

```csharp
[SqlTable("orders")]
public sealed class Order
{
    [Key] public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Kind { get; set; } = "";

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
```

Сторона чтения получает все неотображённые колонки, когда сущность запрашивается целиком, а сторона
записи сохраняет ключи словаря как физические колонки в `INSERT`, `UPDATE` и `MERGE`
([Динамические колонки](29-dynamic-columns.md)). Хранилище нетипизированное, и поверхности запросов по
его ключам нет: используйте его для хранения и проекции, а не для SQL-предикатов.

## Чтение смешанного списка

Автоматического полиморфного чтения нет: запрос по базовому типу не возвращает runtime-подтипы.
Выберите один из вариантов.

**Запросы по типам, объединение в памяти.** Простейший вариант, когда список небольшой или типы обычно
запрашиваются по отдельности:

```csharp
var standard = ctx.From<StandardOrder>().ToList();
var express = ctx.From<ExpressOrder>().ToList();

var all = standard.Cast<OrderBase>().Concat(express).ToList();
```

**Один `UNION ALL` поверх общего DTO.** Это один round trip, и в результате сохраняется дискриминатор:

```csharp
public sealed class OrderRow
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime? PromisedAt { get; set; }
}

var rows = ctx.From<StandardOrder>()
    .Select(o => new OrderRow
    {
        Id = o.Id,
        Kind = "standard",
        Number = o.Number,
    })
    .UnionAll(ctx.From<ExpressOrder>()
        .Select(o => new OrderRow
        {
            Id = o.Id,
            Kind = "express",
            Number = o.Number,
            PromisedAt = o.PromisedAt,
        }))
    .ToList();
```

Обе проекции должны иметь одинаковую форму; `UnionAll` рендерит `UNION ALL` на каждом провайдере
([Операции над множествами](08-set-operations.md)). Результат — DTO, а не конкретная сущность, поэтому
при необходимости сопоставьте его с доменной моделью в памяти.

**Представление БД.** Если смешанный список нужен во многих местах, создайте представление, объединяющее
конкретные таблицы, и мапьте его как плоскую сущность:

```sql
create view v_orders as
select Id, 'standard' as Kind, Number, null as PromisedAt from order_standard
union all
select Id, 'express'  as Kind, Number, PromisedAt      from order_express;
```

```csharp
[SqlTable("v_orders")]
public sealed class OrderView
{
    [Key] public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime? PromisedAt { get; set; }
}

var all = ctx.From<OrderView>().ToList();
```

Представление, его `UNION ALL` и дискриминатор — схема, которой владеете вы. Запись по-прежнему идёт в
конкретные таблицы, а не в представление.

**TPT.** Для формы с общей базовой таблицей смешанное чтение — это соединение плюс DTO-проекция
(показано выше); добавьте в проекцию константную колонку-дискриминатор, если потребителю нужно знать,
из какого подтипа пришла строка.

## Общий код и глобальные фильтры запросов

Базовый класс — естественное место для общего поведения C# (методы, вычисляемые свойства, константы).
Помните, что колонками становятся только публичные записываемые свойства, поэтому вычисляемое свойство
в маппинг не входит.

Глобальные фильтры запросов, объявленные на базовом классе, наследуются отображёнными производными
типами. Резолвер применяет атрибуты от базового типа к самому производному, поэтому фильтр с тем же
ключом на производном типе детерминированно переопределяет базовый
([Глобальные фильтры запросов](../advanced/query-filters.md)). Фильтр всё равно остаётся метаданными
каждого конкретного типа сущности, а не переключателем на всю иерархию.

## Интеграция с EF Core

У моста EF Core правило строже, чем у ядра: импорт EF-модели отклоняет любой тип сущности с базовым
типом или с производными типами, потому что получаемые метаданные nextorm не несут дискриминатора
([Интеграция с EF Core](../advanced/integration-efcore.md#ограничения)). Если в EF-модели есть иерархия,
либо уплостите её для импорта, либо объявите маппинг nextorm вручную одной из форм выше. Ручной маппинг
это не затрагивает: конкретный производный класс — обычная сущность.

## Что принадлежит вам, а что даёт движок

| Ваше | Движок даёт |
|---|---|
| Форма: дискриминатор, таблица на конкретный тип, базовая таблица, JSON/динамический payload | Маппинг по типу, включая унаследованные записываемые свойства |
| Схема: таблицы, представления, ключи, внешние ключи, `CHECK`-ограничения, миграции | `[Key]`, `[Column]`, `[DatabaseGenerated]`, fluent-маппинг |
| Значение дискриминатора при записи и диспетчеризация при чтении | `Join`/`LeftJoin`, `HasOneToOne`/`JoinInto`, транзакции |
| Композиция смешанного списка (DTO, представление или запросы по типам) | `Select`, `UnionAll`, `[SqlTable]` на представлении |
| Разреженные/открытые поля | `[JsonColumn]`, `[DynamicColumns]` |
| Политика фильтров для каждого конкретного типа | Наследование фильтров от базового типа |

В результате получается не скрытая ORM-иерархия, а плоский маппинг плюс выбранная вами реляционная
форма. Это делает генерируемый SQL предсказуемым, а схему — явной, ценой ручной диспетчеризации и
ручной схемы.
