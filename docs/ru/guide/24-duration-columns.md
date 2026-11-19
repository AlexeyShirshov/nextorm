# Колонки-длительности (`TimeSpan`)

> Свойство `TimeSpan` представляет **длительность**. Если у провайдера есть нативный тип длительности или времени суток, nextorm использует его; в остальных случаях значение хранится в целочисленной колонке. Для целочисленного хранения задайте единицу атрибутом [`DurationAttribute`](xref:NextORM.Core.DurationAttribute) или fluent-маппингом [`Duration(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.Duration(NextORM.Core.DurationUnit,System.Int32)). Если единица не задана, используются тики.

**Что нужно знать заранее:** [Запросы и проекции](../engine/index.md) · [Изменение данных (INSERT)](17-insert-statement.md) · [Обзор провайдеров](../connecting-providers/overview.md) · [Ограничения](../advanced/limitations.md)

## Где хранится значение

| Провайдер | Хранение свойства `TimeSpan` |
|---|---|
| PostgreSQL | нативный `interval` |
| MySQL / MariaDB | нативный `TIME` |
| SQL Server | целочисленная колонка (`bigint`), по умолчанию тики — нативного типа длительности нет |
| SQLite | целочисленная колонка (`bigint`), по умолчанию тики |
| ClickHouse | целочисленная колонка (`Int64`), по умолчанию тики |
| In-memory | само значение CLR `TimeSpan` |

На провайдерах с целочисленным хранением nextorm записывает и читает значение в одной и той же единице `DurationUnit`. Например, `[Duration(DurationUnit.Seconds)]` хранит в колонке `bigint` целые секунды. При чтении, записи и сравнении применяется одно преобразование, поэтому свойство `TimeSpan` проходит round-trip без ручной конвертации.

Для объявлений свойств ниже генератор схемы создаёт такие типы колонок у разных провайдеров:

```sql
-- PostgreSQL: нативный interval
create table tasks (id bigint, estimate interval, paused interval(3));

-- MySQL / MariaDB: нативный тип времени суток
create table tasks (id bigint, estimate time, paused time(3));

-- SQL Server / SQLite: целочисленная колонка (`bigint`), тип не зависит от объявленной единицы
create table tasks (id bigint, estimate bigint, paused bigint);

-- ClickHouse: nullable TimeSpan становится Nullable(Int64)
create table tasks (id Int64, estimate Int64, paused Nullable(Int64));
```

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

Два параметра маппинга управляют разными аспектами хранения:

| Опция | Применяется к | Действие |
|---|---|---|
| `DurationUnit` | Провайдерам с целочисленным хранением | Можно выбрать `Ticks`, `Microseconds`, `Milliseconds`, `Seconds`, `Minutes`, `Hours` или `Days`. PostgreSQL и MySQL/MariaDB хранят значение нативно и игнорируют единицу. |
| `Precision` | Нативным типам | Задаёт точность дробных секунд, например `TIME(3)` или `interval(3)`. При целочисленном хранении игнорируется. |

## Запросы и сравнения

Сопоставленное свойство-длительность можно проецировать и фильтровать как любую другую колонку. В сравнении nextorm приводит константу `TimeSpan` к форме хранения колонки независимо от того, с какой стороны оператора она стоит:

```csharp
// Читает хранимое целое в объявленной единице на SQL Server/SQLite/ClickHouse
// и нативный interval/TIME на PostgreSQL/MySQL/MariaDB.
var overdue = ctx.From<Task>()
    .Where(x => x.Estimate > TimeSpan.FromMinutes(5))
    .Select(x => new { x.Id, x.Estimate })
    .ToList();
```

Форма SQL одинакова у всех провайдеров; провайдер определяет плейсхолдер и значение, связываемое с ним:

```sql
-- PostgreSQL / MySQL / MariaDB: @p0 несёт нативную длительность
select id, estimate
 from tasks
 where (estimate > @p0);   -- @p0 = interval '00:05:00' / time '00:05:00'

-- SQL Server / SQLite / ClickHouse: @p0 несёт целочисленное значение в единице хранения
select id, estimate
 from tasks
 where (estimate > @p0);   -- @p0 = 300 (секунды из [Duration(DurationUnit.Seconds)])
```

В SQLite плейсхолдер — `$p0`, а не `@p0`.

При записи применяется то же преобразование хранения, что и при чтении и сравнении, в том числе при bulk insert.

## Детали и ограничения

* PostgreSQL `interval` хранит и месяцы, которые `TimeSpan` не может представить без потерь. Если смэппированный `interval` возвращает ненулевую месячную составляющую любого знака, чтение в `TimeSpan` бросает `InvalidCastException`, а не приближает значение. Значение с нулевыми месяцами (дни плюс микросекунды) конвертируется точно, если помещается в `TimeSpan`; непредставимое значение вызывает `OverflowException`, а не циклическое переполнение. Если месяцы важны, используйте целочисленную колонку с единицей.
* В SQL Server есть тип `time`, но это время суток (меньше 24 часов); nextorm поэтому использует целочисленную колонку, чтобы длительности больше суток были представимы.
* Единица крупнее тиков отбрасывает остаток, как и колонка базы с этой единицей.
* Объявленную единицу несёт только **непосредственно проецируемое** свойство-длительность; вычисляемое выражение длительности (`Select(x => x.Estimate + something)`) читается в единице по умолчанию — тиках. Проецируйте само свойство, если используете не-тиковую единицу.
* `DateTimeOffset` читается напрямую драйвером провайдера; конвенция по дате (`value.Date` против `value.UtcDateTime.Date`) не применяется.
* У MySQL/MariaDB тоже ограничен нативный диапазон `TIME`: `−838:59:59`…`838:59:59` (примерно ±34.9 суток). Поскольку nextorm всегда использует там нативный `TIME`, свойство `TimeSpan` не хранит более длительную длительность; используйте отдельное целочисленное свойство, например `long`.

Генераторы схемы используют [`ISqlDialect.MakeDurationType`](xref:NextORM.Core.ISqlDialect.MakeDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) для non-nullable-колонки и [`ISqlDialect.MakeNullableDurationType`](xref:NextORM.Core.ISqlDialect.MakeNullableDurationType(System.Nullable{NextORM.Core.DurationUnit},System.Int32)) для nullable-колонки; [`SupportsNativeDuration`](xref:NextORM.Core.ISqlDialect.SupportsNativeDuration) также входит в эту диалектную поверхность. У всех провайдеров, кроме ClickHouse, оба метода возвращают один и тот же тип. В ClickHouse nullable-колонка использует `Nullable(Int64)` вместо `Int64`.
