# Колонки-длительности (`TimeSpan`)

> Свойство `TimeSpan` — это **длительность**. nextorm хранит её нативно там, где у базы есть тип длительности или времени суток, и в целочисленной колонке во всех остальных случаях. Единица целочисленного хранения объявляется атрибутом [`DurationAttribute`](xref:NextORM.Core.DurationAttribute) или fluent-маппингом [`Duration(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.Duration(NextORM.Core.DurationUnit,System.Int32)); по умолчанию — тики.

**Что нужно знать заранее:** [Запросы и проекции](01-querying-and-projections.md) · [Изменение данных (INSERT)](19-insert-statement.md) · [Обзор провайдеров](../providers/overview.md) · [Ограничения](../advanced/limitations.md)

## Где хранится значение

| Провайдер | Хранение свойства `TimeSpan` |
|---|---|
| PostgreSQL | нативный `interval` |
| MySQL / MariaDB | нативный `TIME` |
| SQL Server | целочисленная колонка (`bigint`), по умолчанию тики — нативного типа длительности нет |
| SQLite | целочисленная колонка (`bigint`), по умолчанию тики |
| ClickHouse | целочисленная колонка (`bigint`), по умолчанию тики |
| In-memory | само значение CLR `TimeSpan` |

На целочисленных провайдерах значение записывается в единицах `DurationUnit` и читается обратно в той же единице: `[Duration(DurationUnit.Seconds)]` над колонкой `bigint` хранит целые секунды. Чтение, запись и сравнения применяют одно и то же преобразование, поэтому свойство `TimeSpan` проходит round-trip без ручной конвертации.

## Объявление единицы

Атрибут на свойстве:

```csharp
public class Task
{
    public long Id { get; set; }

    [Duration(DurationUnit.Seconds)]
    public TimeSpan Estimate { get; set; }

    // Без атрибута: тики на целочисленных провайдерах, нативный тип на PostgreSQL/MySQL/MariaDB.
    public TimeSpan Elapsed { get; set; }

    [Duration(DurationUnit.Milliseconds, Precision = 3)]
    public TimeSpan? Paused { get; set; }
}
```

или fluent-маппинг:

```csharp
ctx.From<Task>(b => b
    .Table("tasks")
    .Property(x => x.Estimate).Duration(DurationUnit.Seconds).HasColumnName("estimate"));
```

`DurationUnit` — одно из `Ticks`, `Microseconds`, `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days`. `Precision` — точность дробных секунд **нативного** типа (например, `TIME(3)` или `interval(3)`); для целочисленной формы игнорируется. На PostgreSQL и MySQL/MariaDB единица игнорируется, потому что значение хранится нативно.

## Запросы и сравнения

Колонка-длительность проецируется и фильтруется как любая другая. Константа `TimeSpan` в сравнении приводится к форме хранения колонки, с любой стороны от оператора:

```csharp
// Читает хранимое целое в объявленной единице на SQL Server/SQLite/ClickHouse
// и нативный interval/TIME на PostgreSQL/MySQL/MariaDB.
var overdue = ctx.From<Task>()
    .Where(x => x.Estimate > TimeSpan.FromMinutes(5))
    .Select(x => new { x.Id, x.Estimate })
    .ToList();
```

Запись идёт через то же преобразование, включая bulk insert.

## Детали и ограничения

* PostgreSQL `interval` хранит и месяцы, которых `TimeSpan` не выражает; значение с целыми месяцами не пройдёт round-trip достоверно. Если месяцы важны — используйте целочисленную колонку с единицей.
* В SQL Server есть тип `time`, но это время суток (меньше 24 часов); nextorm поэтому использует целочисленную колонку, чтобы длительности больше суток были представимы.
* Единица крупнее тиков отбрасывает остаток, как и колонка базы с этой единицей.
* Объявленную единицу несёт только **непосредственно проецируемое** свойство-длительность; вычисляемое выражение длительности (`Select(x => x.Estimate + something)`) читается в единице по умолчанию — тиках. Проецируйте само свойство, если используете не-тиковую единицу.
* `DateTimeOffset` читается напрямую драйвером провайдера; конвенция по дате (`value.Date` против `value.UtcDateTime.Date`) не применяется.

Диалектная поверхность для генераторов схемы: [`ISqlDialect.MakeDurationType`](xref:NextORM.Core.ISqlDialect.MakeDurationType(NextORM.Core.DurationUnit,System.Int32)) для non-nullable-колонки и [`ISqlDialect.MakeNullableDurationType`](xref:NextORM.Core.ISqlDialect.MakeNullableDurationType(NextORM.Core.DurationUnit,System.Int32)) для nullable, а также [`SupportsNativeDuration`](xref:NextORM.Core.ISqlDialect.SupportsNativeDuration). У всех провайдеров, кроме ClickHouse, тип совпадает; у ClickHouse non-nullable `Int64` для nullable-случая становится `Nullable(Int64)`.
