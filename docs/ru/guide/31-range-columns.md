# Range-колонки (диапазон, хранимый парой скалярных колонок)

> Свойство `Range<T>` можно хранить **парой скалярных колонок** на провайдере без нативного range-типа. Обе границы кладутся в обычные колонки, SQL `NULL` означает неограниченную сторону, а предикаты и функции инспекции диапазона транслируются поверх пары.

**См. также:** [Фильтрация (WHERE)](02-filtering-where.md) · [Конвертеры значений и JSON-колонки](30-value-converters.md) · [Специфичный для PostgreSQL SQL](provider-specific/postgresql.md) · [Ограничения](../advanced/limitations.md)

В PostgreSQL есть [нативные range-типы](provider-specific/postgresql.md#range-типы) (`int4range`…`daterange`), поэтому там свойство `Range<T>` отображается прямо на range-колонку. У остальных провайдеров range-типа нет; nextorm закрывает разрыв, позволяя отобразить обе границы на две скалярные колонки и транслируя операторы диапазона поверх них.

## Модель хранения

| Провайдер | Как хранится свойство `Range<T>` |
|---|---|
| PostgreSQL | нативная range-колонка по умолчанию; пара скалярных колонок тоже допустима |
| SQL Server | пара скалярных колонок ([`RangeColumnsAttribute`](xref:NextORM.Core.RangeColumnsAttribute)) |
| MySQL / MariaDB | пара скалярных колонок |
| SQLite | пара скалярных колонок |
| ClickHouse | пара скалярных колонок |
| In-memory | CLR-значение `Range<T>` |

Пара — это **две nullable-колонки**: нижняя граница и верхняя граница. `NULL` в колонке означает неограниченную сторону (`LowerInfinite` / `UpperInfinite`). **Инклюзивность границ — часть маппинга**, а не хранимого значения: она объявляется один раз вместе с атрибутом и переприменяется при каждом чтении, потому что обычная скалярная колонка её не несёт.

Поверхность гейтится [`SupportsRangeColumns`](xref:NextORM.Core.ISqlDialect.SupportsRangeColumns) и включена на PostgreSQL, SQL Server, MySQL/MariaDB, SQLite и ClickHouse.

## Объявление пары

Используйте атрибут на свойстве ([`RangeColumnsAttribute`](xref:NextORM.Core.RangeColumnsAttribute)):

```csharp
[SqlTable("reservation")]
public class Reservation
{
    public int Id { get; set; }

    // Две integer-колонки; диапазон по умолчанию [lower, upper).
    [RangeColumns("during_lower", "during_upper")]
    public Range<int> During { get; set; }
}
```

Инклюзивность по умолчанию — `[)` (нижняя включена, верхняя исключена), как у дискретного диапазона PostgreSQL. Задайте её явно или используйте флюентный [`EntityPropertyBuilder<T>.RangeColumns(...)`](xref:NextORM.Core.EntityPropertyBuilder`1.RangeColumns(System.String,System.String,System.Boolean,System.Boolean)):

```csharp
ctx.From<Reservation>(b => b
    .Table("reservation")
    .Property(x => x.During).RangeColumns("during_lower", "during_upper", lowerInclusive: false, upperInclusive: true));
```

Обе колонки должны быть nullable, чтобы неограниченную сторону можно было записать как `NULL`.

## Чтение и запись

Свойство-пара читается и пишется как любое другое. Запись `Range<int>(1, 10)` кладёт `1` и `10` в две колонки; чтение собирает `new Range<int>(lower, upper, …, lowerIsNull, upperIsNull, …)` с инклюзивностью из маппинга:

```csharp
ctx.InsertInto<Reservation>()
    .Values(new Reservation { Id = 1, During = new Range<int>(1, 10) })
    .Insert();

var r = ctx.From<Reservation>().Where(x => x.Id == 1).Single();
// r.During.Lower == 1, r.During.Upper == 10,
// r.During.LowerInclusive == true, r.During.UpperInclusive == false (из маппинга)
```

Обе границы попадают в объявленные колонки, а чтение выбирает обе обратно (вывод SQLite; другие провайдеры иначе квотируют идентификаторы и используют свои маркеры параметров):

```sql
-- Insert
insert into reservation (id, during_lower, during_upper) values ($p0, $p1, $p2);

-- Read
select id, during_lower, during_upper from reservation where id = 1;
```

Неограниченная сторона записывается как SQL `NULL`: `new Range<int>(…, lowerInfinite: true)` пишет `during_lower = NULL`.

Запись идёт через формы по сущности (`Values(entity)` / `Set(entity)`); про формы по селектору см. ограничения ниже.

## Фильтрация и инспекция

Операторы диапазона PostgreSQL транслируются в сравнения между двумя границами. Константный аргумент `Range<T>` сворачивается в параметры, а неограниченная граница сокращает соответствующую сторону.

```csharp
var overlapping = ctx.From<Reservation>()
    .Where(x => SqlFunctions.Postgres.overlaps(x.During, new Range<int>(5, 15)))
    .Select(x => x.Id)
    .ToList();
```

Константный диапазон сворачивается в параметры (SQLite рендерит приведённый запрос так):

```sql
select id from reservation
 where (((during_lower is null) or during_lower < $p1)
    and ((during_upper is null) or $p0 < during_upper));
-- $p0 = 5, $p1 = 15
```

| Функция | Как транслируется поверх пары |
|---|---|
| `overlaps(a, b)` | диапазоны имеют общую точку |
| `range_contains(range, value)` / `range_contains(outer, inner)` | диапазон содержит значение / другой диапазон |
| `range_contained_by(inner, outer)` | диапазон содержится в другом |
| `range_adjacent(a, b)` | диапазоны соприкасаются, но не перекрываются |
| `range_strictly_left_of(a, b)` / `range_strictly_right_of(a, b)` | упорядочены без касания |
| `range_not_extend_right_of(a, b)` / `range_not_extend_left_of(a, b)` | `a` не выходит за `b` с этой стороны |
| `lower(range)` / `upper(range)` | сама колонка границы |
| `lower_inc` / `upper_inc` | инклюзивность из маппинга, `false` при неограниченной границе |
| `lower_inf` / `upper_inf` | колонка границы `is null` |
| `isempty(range)` | всегда `false` (пара никогда не пустая) |

Семантика совпадает с PostgreSQL и провайдером in-memory, включая граничные случаи (равные значения при разной инклюзивности).

### Сгенерированные предикаты

Для маппинга по умолчанию `[)` колонки пары — `a = [al, au)`, константа — `b = [bl, bu)`; неограниченная граница делает соответствующий терм истинным. SQLite рендерит:

| Вызов | Предикат |
|---|---|
| `overlaps(a, b)` | `(al is null or al < bu) and (au is null or bl < au)` |
| `range_contains(a, v)` | `(al is null or al <= v) and (au is null or v < au)` |
| `range_contains(a, b)` | `(al is null or al <= bl) and (au is null or au >= bu)` |
| `range_contained_by(a, b)` | `(al is not null and bl <= al) and (au is not null and au <= bu)` |
| `range_adjacent(a, b)` | `(au is not null and au = bl) or (al is not null and bu = al)` |
| `range_strictly_left_of(a, b)` | `au is not null and au <= bl` |
| `range_strictly_right_of(a, b)` | `al is not null and al >= bu` |
| `range_not_extend_right_of(a, b)` | `au is not null and au <= bu` |
| `range_not_extend_left_of(a, b)` | `al is not null and al >= bl` |
| `lower(a)` / `upper(a)` | колонка `al` / `au` |
| `lower_inf(a)` / `upper_inf(a)` | `al is null` / `au is null` |
| `lower_inc(a)` / `upper_inc(a)` | инклюзивность из маппинга, `false` при `null` этой границы |
| `isempty(a)` | `false` |

Термы `<=`/`>=` несут равенство на границе; при другой инклюзивности равенство добавляется, только когда соответствующая граница включена. Например, закрытый маппинг `(]` рендерит `lower_inc` как `1=0`, а `upper_inc` — как `not (au is null)`.

## Ограничения

* **Пустой диапазон не представим.** Пара `NULL`-границ — это *неограниченный* диапазон, поэтому `Range<T>.Empty` отклоняется при записи и при трансляции с `NotSupportedException`.
* **Range-возвращающие операторы отклоняются.** `range_union`, `range_intersection`, `range_difference`, конструкторы диапазонов и агрегаты `range_agg`/`range_intersect_agg` не имеют скалярной формы над парой и бросают `NotSupportedException`. Используйте PostgreSQL, если они нужны.
* **Multirange — не пара.** `Range<T>[]` отображается только на нативную multirange-колонку PostgreSQL.
* **Запись по селектору и returning отклоняются.** `InsertInto<T>().Value(x => x.During, …)`, `Values(source, mapping)` по маппингу и `Returning(x => x.During)` адресуют одну колонку и бросают `NotSupportedException`; используйте формы по сущности.
* Свойство `[RangeColumns]` нельзя комбинировать с `[ValueConverter]` или `[JsonColumn]`.

## См. также

* [Специфичный для PostgreSQL SQL — Range-типы](provider-specific/postgresql.md#range-типы) и [Multirange](provider-specific/postgresql.md#multirange) — нативная поверхность.
* [Ограничения](../advanced/limitations.md) — матрица возможностей провайдеров.
* [Справочник API — атрибуты маппинга](../advanced/api-reference.md).
