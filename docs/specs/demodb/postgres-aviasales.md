# PostgreSQL / aviasales (демо-БД «Авиаперевозки»)

Модельные запросы: [`docs/specs/postgres-demodb/`](../postgres-demodb).
Источник и установка демо-БД: <https://postgrespro.ru/education/demodb> (лицензия MIT).

> Запускаемая версия этих примеров (Testcontainers + подготовка БД) — проект
> [`examples/nextorm.examples.postgres.aviasales`](../../../examples/nextorm.examples.postgres.aviasales).
> Сниппеты ниже иллюстративны; при расхождении ориентируйтесь на код проекта (в нём, например,
> оконные значения оборачиваются производной таблицей, а условия по агрегатам идут в `Having` —
> `ctx.From(derivedQuery).Join(...)` движком не поддерживается).

## Актуальная схема (версия 01.09.2025)

Запросы в `postgres-demodb/` **приведены к актуальной схеме** из дампа `demo-20250901-3m`
(PostgreSQL 15+). Ключевые отличия от старой (2016/2017) схемы, под которую они были написаны:

| Было (2016/2017) | Стало (2025-09-01) |
|---|---|
| `aircrafts` (модель — jsonb) | `airplanes` (view, `model` text) + `airplanes_data` (jsonb `model`) |
| `airports` (таблица) | `airports` (view) + `airports_data` (jsonb `airport_name`/`city`/`country`) |
| `flights.aircraft_code`, `flights.departure_airport`, `flights.arrival_airport` | перенесены в `routes` (`airplane_code`, `departure_airport`, `arrival_airport`); `flights` содержит только `flight_id`, `route_no`, `status`, времена |
| `flights` + материализованное `routes` | таблица `routes` с темпоральным ключом (`route_no`, `validity tstzrange`); соединение `routes.validity @> flights.scheduled_departure` |
| `ticket_flights.amount` | `segments.price` |
| — | представление `timetable` (flights ⨝ routes ⨝ airports_data) со старым «плоским» набором полей |

В запросах для «плоских» данных о рейсе используется представление `timetable` (у него есть
`airplane_code`, `departure_airport`, `arrival_airport`, `scheduled_departure`, `actual_departure`,
`status`), а для перевода `model`/`city` — базовые таблицы `airplanes_data`/`airports_data` с
`jsonb ->> 'ru'`. Так сохраняется исходный `->> 'ru'` и демонстрация работы с JSON.

> Альтернатива без `_data`: использовать представления `airplanes`/`airports` (поле уже `text`) и
> задать язык сессии `SET bookings.lang = 'ru'` (по умолчанию в дампе — `en`).

Все объекты лежат в схеме `bookings`, поэтому имя таблицы в `[SqlTable]` указывается со схемой
(`bookings.timetable`) — nextorm подставляет имя в `FROM` дословно.

## Сущности

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.DemoDb.Postgres;

// airplanes_data: базовая таблица с jsonb-переводами (model)
[SqlTable("bookings.airplanes_data")]
public interface IAirplaneData
{
    [Key, Column("airplane_code")] string AirplaneCode { get; set; }
    [Column("model")] string? Model { get; set; }              // jsonb -> model ->> 'ru'
}

// airports_data: базовая таблица с jsonb-переводами (city и др.)
[SqlTable("bookings.airports_data")]
public interface IAirportData
{
    [Key, Column("airport_code")] string AirportCode { get; set; }
    [Column("city")] string? City { get; set; }                // jsonb -> city ->> 'ru'
}

// timetable: flights + routes + airports_data (заменяет старый flights с aircraft_code/airports)
[SqlTable("bookings.timetable")]
public interface ITimetable
{
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("route_no")] string RouteNo { get; set; }
    [Column("departure_airport")] string DepartureAirport { get; set; }
    [Column("arrival_airport")] string ArrivalAirport { get; set; }
    [Column("status")] string Status { get; set; }
    [Column("airplane_code")] string AirplaneCode { get; set; }
    [Column("scheduled_departure")] DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] DateTime? ActualDeparture { get; set; }
}

// flights: только «собственные» поля рейса (нужен для статусов и joins по flight_id)
[SqlTable("bookings.flights")]
public interface IFlight
{
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("route_no")] string RouteNo { get; set; }
    [Column("status")] string Status { get; set; }
    [Column("scheduled_departure")] DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] DateTime? ActualDeparture { get; set; }
}

[SqlTable("bookings.tickets")]
public interface ITicket
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Column("passenger_id")] string PassengerId { get; set; }
    [Column("passenger_name")] string PassengerName { get; set; }
    [Column("outbound")] bool Outbound { get; set; }
}

[SqlTable("bookings.bookings")]
public interface IBooking
{
    [Key, Column("book_ref")] string BookRef { get; set; }
    [Column("book_date")] DateTime BookDate { get; set; }
    [Column("total_amount")] decimal TotalAmount { get; set; }
}

// segments: бывший ticket_flights, amount -> price
[SqlTable("bookings.segments")]
public interface ISegment
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("fare_conditions")] string FareConditions { get; set; }
    [Column("price")] decimal Price { get; set; }
}

[SqlTable("bookings.seats")]
public interface ISeat
{
    [Key, Column("airplane_code")] string AirplaneCode { get; set; }
    [Key, Column("seat_no")] string SeatNo { get; set; }
    [Column("fare_conditions")] string FareConditions { get; set; }
}

[SqlTable("bookings.boarding_passes")]
public interface IBoardingPass
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("seat_no")] string? SeatNo { get; set; }
}
```

Общие правила моделирования:

* **JSON**: `model ->> 'ru'` → `SqlFunctions.Postgres.json_get_text(x.Model, "ru")`. Первый аргумент —
  сам jsonb-столбец (nextorm рендерит его как `model ->> 'ru'`).
* **`EXTRACT(EPOCH …)/60`** штатной функции `extract` в API нет; ближайшая эквивалентная замена —
  `SqlFunctions.Sql.date_diff("milliseconds", start, end) / 60000.0`.
* **`EXTRACT(ISODOW FROM d)`** → `SqlFunctions.Sql.date_diff("day", date_trunc("week", d), d) + 1`
  (`date_trunc('week')` в PostgreSQL — понедельник, `date_diff('day')` на PostgreSQL — разность дат).
* **правило `GroupBy`**: лямбда `Select` после `GroupBy` получает исходную сущность (не ключ группы).
  Вычисляемый ключ группы нужно **повторить** в `Select` дословно; ссылаться можно только на
  столбцы-ключи и агрегаты.

---

## 1. `aircraft_delay_chains.sql` — цепочки задержек

Идея: для каждого борта упорядочить рейсы по факту вылета, взять `LAG` задержки и оставить строки,
где и текущая, и предыдущая задержка больше порога.

```csharp
var flights = ctx.From<ITimetable>()
    .Where(f => (f.Status == "Departed" || f.Status == "Arrived")
                && f.ActualDeparture != null);

// CTE flight_delays: delay_minutes + LAG(delay) over (partition by airplane_code order by actual_departure)
var delays = flights.Select(f => new
{
    f.FlightId,
    f.AirplaneCode,
    f.ScheduledDeparture,
    DelayMinutes = SqlFunctions.Sql.date_diff("milliseconds", f.ScheduledDeparture, f.ActualDeparture) / 60000.0,
    PrevDelayMinutes = SqlFunctions.Sql
        .lag(SqlFunctions.Sql.date_diff("milliseconds", f.ScheduledDeparture, f.ActualDeparture) / 60000.0, 1)
        .Over(partitionBy: () => f.AirplaneCode, orderBy: () => f.ActualDeparture)
});

// delay_chains + join airplanes_data + фильтр > 30 (порог 15 из промежуточного CASE на результат не влияет)
var rows = await ctx.From(delays)
    .Join(ctx.From<IAirplaneData>(), (d, a) => d.AirplaneCode == a.AirplaneCode)
    .Where(p => p.Item1.DelayMinutes > 30 && p.Item1.PrevDelayMinutes > 30)
    .OrderBy(p => p.Item1.AirplaneCode)
    .OrderBy(p => p.Item1.ScheduledDeparture)
    .Select(p => new
    {
        Model = SqlFunctions.Postgres.json_get_text(p.Item2.Model, "ru"),
        AirplaneCode = p.Item1.AirplaneCode,
        ScheduledDeparture = p.Item1.ScheduledDeparture,
        DelayMinutes = Math.Round(p.Item1.DelayMinutes ?? 0, 1),
        PrevDelayMinutes = Math.Round(p.Item1.PrevDelayMinutes ?? 0, 1)
    })
    .ToListAsync();
```

Примечание: точность `date_diff('milliseconds')/60000.0` выше, чем у `EXTRACT(EPOCH …)/60`, но
граничные рейсы с задержкой ровно `30:00.x` могут попасть/не попасть в выборку иначе — см.
[verification-plan.md](verification-plan.md#пограничные-семантики).

## 2. `business_occupancy_matrix.sql` — матрица заполняемости бизнес-класса

Четыре шага: вместимость по борту → занятость по рейсу → день недели → средний процент по дням.

```csharp
// CTE flight_business_capacity
var capacity = ctx.From<ISeat>()
    .Where(s => s.FareConditions == "Business")
    .GroupBy(s => new { s.AirplaneCode })
    .Select(s => new
    {
        s.AirplaneCode,
        TotalBusinessSeats = SqlFunctions.Sql.count()
    });

// CTE flight_occupancy
var occupancy = ctx.From<ITimetable>()
    .Where(f => f.Status == "Departed" || f.Status == "Arrived")
    .Join(ctx.From<ISegment>(),
        (f, tf) => f.FlightId == tf.FlightId && tf.FareConditions == "Business")
    .LeftJoin(ctx.From<IBoardingPass>(),
        (p, bp) => p.Item2.FlightId == bp.FlightId && p.Item2.TicketNo == bp.TicketNo)
    .GroupBy(p => new { p.Item1.FlightId, p.Item1.AirplaneCode, p.Item1.ScheduledDeparture })
    .Select(p => new
    {
        p.Item1.FlightId,
        p.Item1.AirplaneCode,
        p.Item1.ScheduledDeparture,
        // EXTRACT(ISODOW ...)  ->  разница с понедельником + 1
        DayOfWeek = SqlFunctions.Sql.date_diff("day",
            SqlFunctions.Sql.date_trunc("week", p.Item1.ScheduledDeparture), p.Item1.ScheduledDeparture) + 1,
        OccupiedBusinessSeats = SqlFunctions.Sql.count(p.Item3.SeatNo)
    });

// JOIN capacity + airplanes_data, доля заполняемости
var enriched = ctx.From(occupancy)
    .Join(capacity, (o, c) => o.AirplaneCode == c.AirplaneCode)
    .Join(ctx.From<IAirplaneData>(), (p, a) => p.Item1.AirplaneCode == a.AirplaneCode)
    .Select(p => new
    {
        Model = SqlFunctions.Postgres.json_get_text(p.Item3.Model, "ru"),
        p.Item1.DayOfWeek,
        Ratio = (double?)p.Item1.OccupiedBusinessSeats
                / SqlFunctions.Sql.nullif(p.Item2.TotalBusinessSeats, 0)
    });

// финальная матрица: AVG(CASE WHEN day = n THEN ratio END) -> filtered avg
var rows = await ctx.From(enriched)
    .GroupBy(e => new { e.Model })
    .OrderBy(e => e.Model)
    .Select(e => new
    {
        e.Model,
        Mon = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 1) ?? 0) * 100, 1),
        Tue = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 2) ?? 0) * 100, 1),
        Wed = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 3) ?? 0) * 100, 1),
        Thu = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 4) ?? 0) * 100, 1),
        Fri = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 5) ?? 0) * 100, 1),
        Sat = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 6) ?? 0) * 100, 1),
        Sun = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 7) ?? 0) * 100, 1)
    })
    .ToListAsync();
```

`avg(x, () => predicate)` рендерится как `avg(x) filter (where …)` — только PostgreSQL/SQLite
(`SupportsFilter`). `nullif` защищает от деления на ноль так же, как в оригинале.

## 3. `passenger_noshow_analysis.sql` — «серийные невозвращенцы»

```csharp
// CTE passenger_flight_history
var history = ctx.From<ITicket>()
    .Join(ctx.From<ISegment>(), (t, tf) => t.TicketNo == tf.TicketNo)
    .Join(ctx.From<IFlight>(), (p, f) => p.Item2.FlightId == f.FlightId)
    .LeftJoin(ctx.From<IBoardingPass>(),
        (p, bp) => p.Item2.FlightId == bp.FlightId && p.Item2.TicketNo == bp.TicketNo)
    .Where(p => p.Item3.Status == "Departed" || p.Item3.Status == "Arrived")
    .Select(p => new
    {
        p.Item1.PassengerId,
        p.Item1.PassengerName,
        p.Item3.FlightId,
        p.Item2.Price,
        IsNoShow = p.Item4.SeatNo == null ? 1 : 0
    });

// CTE passenger_stats
var stats = ctx.From(history)
    .GroupBy(h => new { h.PassengerId, h.PassengerName })
    .Select(h => new
    {
        h.PassengerId,
        h.PassengerName,
        TotalBooked = SqlFunctions.Sql.count(),
        TotalNoShows = SqlFunctions.Sql.sum(h.IsNoShow),
        Wasted = SqlFunctions.Sql.sum(h.Price)
    });

var rows = await ctx.From(stats)
    .Where(s => s.TotalBooked >= 3 && s.TotalBooked == s.TotalNoShows)
    .OrderByDescending(s => s.Wasted)
    .OrderBy(s => s.PassengerName)
    .Select(s => new
    {
        s.PassengerId,
        s.PassengerName,
        TotalBooked = s.TotalBooked,
        Wasted = s.Wasted
    })
    .ToListAsync();
```

Фильтр по агрегатам вынесен в отдельный запрос (`From(stats).Where(...)`) — это простое и
предсказуемое решение; альтернативно можно писать `.GroupBy(...).Having(h => …)`, повторив агрегаты
в `Having`.

## 4. `rolling_revenue_metrics.sql` — скользящая выручка

```csharp
// CTE daily_revenue
var daily = ctx.From<IBooking>()
    .GroupBy(b => new { SalesDate = SqlFunctions.Sql.date_trunc("day", b.BookDate) })
    .Select(b => new
    {
        SalesDate = SqlFunctions.Sql.date_trunc("day", b.BookDate),   // повтор ключа группы
        DailyAmount = SqlFunctions.Sql.sum(b.TotalAmount)
    });

var rows = await ctx.From(daily)
    .OrderByDescending(d => d.SalesDate)
    .Select(d => new
    {
        d.SalesDate,
        d.DailyAmount,
        Cumulative = SqlFunctions.Sql.sum_over(d.DailyAmount).Over(
            SqlFunctions.Sql.asc(() => d.SalesDate),
            WindowFrame.RowsUnboundedPrecedingToCurrentRow),
        MovingAvg7 = Math.Round(
            SqlFunctions.Sql.avg_over(d.DailyAmount).Over(
                SqlFunctions.Sql.asc(() => d.SalesDate),
                WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow)) ?? 0,
            2)
    })
    .ToListAsync();
```

## 5. `route_network_abc_xyz.sql` — ABC/XYZ маршрутной сети

```csharp
// CTE route_monthly_revenue
var monthly = ctx.From<ITimetable>()
    .Join(ctx.From<IAirportData>(), (f, dep) => f.DepartureAirport == dep.AirportCode)
    .Join(ctx.From<IAirportData>(), (p, arr) => p.Item1.ArrivalAirport == arr.AirportCode)
    .Join(ctx.From<ISegment>(), (p, tf) => p.Item1.FlightId == tf.FlightId)
    .Join(ctx.From<ITicket>(), (p, t) => p.Item4.TicketNo == t.TicketNo)
    .GroupBy(p => new
    {
        Route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
        FlightMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture)
    })
    .Select(p => new
    {
        // вычисляемые ключи группы повторяются дословно
        Route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
        FlightMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture),
        MonthlyRevenue = SqlFunctions.Sql.sum(p.Item4.Price),
        PassengerCount = SqlFunctions.Sql.count_distinct(p.Item5.TicketNo)
    });

// CTE route_aggregates
var aggregates = ctx.From(monthly)
    .GroupBy(r => new { r.Route })
    .Select(r => new
    {
        r.Route,
        TotalRevenue = SqlFunctions.Sql.sum(r.MonthlyRevenue),
        AvgPassengers = SqlFunctions.Sql.avg((double)r.PassengerCount),
        StddevPassengers = SqlFunctions.Sql.stdev((double)r.PassengerCount)
    });

// CTE abc_analys
var abc = ctx.From(aggregates)
    .Select(a => new
    {
        a.Route,
        a.TotalRevenue,
        a.AvgPassengers,
        a.StddevPassengers,
        RunningPercent = SqlFunctions.Sql.sum_over(a.TotalRevenue).Over(SqlFunctions.Sql.desc(() => a.TotalRevenue))
                         / SqlFunctions.Sql.sum_over(a.TotalRevenue).Over()
    });

var rows = await ctx.From(abc)
    .OrderByDescending(a => a.TotalRevenue)
    .Select(a => new
    {
        a.Route,
        RevenueMln = Math.Round((a.TotalRevenue ?? 0m) / 1000000.0m, 2),
        Abc = a.RunningPercent <= 0.80m ? "A"
            : a.RunningPercent <= 0.95m ? "B"
            : "C",
        Xyz = a.AvgPassengers == 0 || a.StddevPassengers == null ? "Z"
            : a.StddevPassengers / a.AvgPassengers < 0.10 ? "X"
            : a.StddevPassengers / a.AvgPassengers <= 0.25 ? "Y"
            : "Z"
    })
    .ToListAsync();
```

`stdev` на PostgreSQL рендерится как `stddev(...)` (выборочное СКО) — совпадает с `STDDEV` в
оригинале. `sum_over(x).Over()` без аргументов даёт `sum(x) over ()` (общий итог для знаменателя).
