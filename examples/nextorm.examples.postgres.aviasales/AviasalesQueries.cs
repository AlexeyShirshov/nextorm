using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// The five demo queries from <c>docs/specs/postgres-demodb</c> plus six extra course-style exercises
/// (see <c>examples/README.md</c>), expressed with the nextorm LINQ API. Each method prints the number
/// of rows and the first few rows as JSON.
/// </summary>
public static class AviasalesQueries
{
    // 1. aircraft_delay_chains.sql — WITH flight_delays AS (...) ... JOIN airplanes_data
    public static async Task AircraftDelayChains(IDataContext ctx, CancellationToken ct)
    {
        // CTE flight_delays (delay_minutes + LAG over the same partition).
        var flightDelays = ctx.From<ITimetable>()
            .Where(f => (f.Status == "Departed" || f.Status == "Arrived") && f.ActualDeparture != null)
            .Select(f => new
            {
                f.FlightId,
                f.AirplaneCode,
                f.ScheduledDeparture,
                // EXTRACT(EPOCH FROM actual_departure - scheduled_departure) / 60
                DelayMinutes = SqlFunctions.Sql.date_diff("seconds", f.ScheduledDeparture, f.ActualDeparture) / 60.0,
                PrevDelayMinutes = SqlFunctions.Sql.lag(
                        SqlFunctions.Sql.date_diff("seconds", f.ScheduledDeparture, f.ActualDeparture) / 60.0)
                    .Over(partitionBy: () => f.AirplaneCode, orderBy: () => f.ActualDeparture)
            });

        // The original joins the CTE to airplanes_data; a derived query as the primary FROM source is
        // not supported yet (see docs/specs/roadmap/sql-capabilities-gap-analysis.md, known gap 10).
        var rows = await ctx.From(flightDelays)
            .Join(ctx.From<IAirplaneData>(), (d, a) => d.AirplaneCode == a.AirplaneCode)
            .Where(p => p.Item1.DelayMinutes > 30 && p.Item1.PrevDelayMinutes > 30)
            .OrderBy(p => p.Item1.AirplaneCode)
            .OrderBy(p => p.Item1.ScheduledDeparture)
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Item2.Model, "ru"),
                p.Item1.AirplaneCode,
                p.Item1.ScheduledDeparture,
                DelayMinutes = Math.Round((decimal)(p.Item1.DelayMinutes ?? 0.0), 1),
                PrevDelayMinutes = Math.Round((decimal)(p.Item1.PrevDelayMinutes ?? 0.0), 1)
            })
            .ToListAsync(ct);

        Print("1. aircraft_delay_chains", rows);
    }

    // 2. business_occupancy_matrix.sql — WITH flight_business_capacity + flight_occupancy, then joins
    public static async Task BusinessOccupancyMatrix(IDataContext ctx, CancellationToken ct)
    {
        // CTE flight_business_capacity: COUNT(*) over the business seats per airplane.
        var capacity = ctx.From<ISeat>()
            .Where(s => s.FareConditions == "Business")
            .GroupBy(s => new { s.AirplaneCode })
            .Select(s => new { s.AirplaneCode, TotalBusinessSeats = SqlFunctions.Sql.count() });

        // CTE flight_occupancy: COUNT(bp.seat_no) per flight (no seats join, so no fan-out).
        var occupancy = ctx.From<ITimetable>()
            .Where(f => f.Status == "Departed" || f.Status == "Arrived")
            .Join(ctx.From<ISegment>(), (f, tf) => f.FlightId == tf.FlightId && tf.FareConditions == "Business")
            .LeftJoin(ctx.From<IBoardingPass>(),
                (p, bp) => p.Item2.FlightId == bp.FlightId && p.Item2.TicketNo == bp.TicketNo)
            .GroupBy(p => new { p.Item1.FlightId, p.Item1.AirplaneCode, p.Item1.ScheduledDeparture })
            .Select(p => new
            {
                p.Item1.FlightId,
                p.Item1.AirplaneCode,
                p.Item1.ScheduledDeparture,
                // EXTRACT(ISODOW FROM scheduled_departure): days since the ISO Monday + 1
                DayOfWeek = SqlFunctions.Sql.date_diff("day",
                    SqlFunctions.Sql.date_trunc("week", p.Item1.ScheduledDeparture), p.Item1.ScheduledDeparture) + 1,
                OccupiedBusinessSeats = SqlFunctions.Sql.count(p.Item3.SeatNo)
            });

        // FROM flight_occupancy JOIN flight_business_capacity JOIN airplanes_data (see known gap 10).
        var enriched = ctx.From(occupancy)
            .Join(capacity, (o, c) => o.AirplaneCode == c.AirplaneCode)
            .Join(ctx.From<IAirplaneData>(), (p, a) => p.Item1.AirplaneCode == a.AirplaneCode)
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Item3.Model, "ru"),
                p.Item1.DayOfWeek,
                Ratio = (decimal?)p.Item1.OccupiedBusinessSeats
                        / SqlFunctions.Sql.nullif(p.Item2.TotalBusinessSeats, 0)
            });

        var rows = await ctx.From(enriched)
            .GroupBy(e => new { e.Model })
            .OrderBy(e => e.Model)
            .Select(e => new
            {
                e.Model,
                Mon = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 1) ?? 0m) * 100, 1),
                Tue = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 2) ?? 0m) * 100, 1),
                Wed = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 3) ?? 0m) * 100, 1),
                Thu = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 4) ?? 0m) * 100, 1),
                Fri = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 5) ?? 0m) * 100, 1),
                Sat = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 6) ?? 0m) * 100, 1),
                Sun = Math.Round((SqlFunctions.Sql.avg(e.Ratio, () => e.DayOfWeek == 7) ?? 0m) * 100, 1)
            })
            .ToListAsync(ct);

        Print("2. business_occupancy_matrix", rows);
    }

    // 3. passenger_noshow_analysis.sql — WITH passenger_flight_history + passenger_stats
    public static async Task PassengerNoShowAnalysis(IDataContext ctx, CancellationToken ct)
    {
        // CTE passenger_flight_history: carries the is_noshow flag exactly as the original.
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

        // CTE passenger_stats: COUNT(flight_id), SUM(is_noshow), SUM(price).
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
            .Select(s => new { s.PassengerId, s.PassengerName, s.TotalBooked, s.Wasted })
            .ToListAsync(ct);

        Print("3. passenger_noshow_analysis", rows);
    }

    // 4. rolling_revenue_metrics.sql — WITH daily_revenue, then SUM/AVG windows
    public static async Task RollingRevenueMetrics(IDataContext ctx, CancellationToken ct)
    {
        // CTE daily_revenue.
        var daily = ctx.From<IBooking>()
            .GroupBy(b => new { SalesDate = SqlFunctions.Sql.date_trunc("day", b.BookDate) })
            .Select(b => new
            {
                SalesDate = SqlFunctions.Sql.date_trunc("day", b.BookDate),
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
                        WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow)),
                    2)
            })
            .ToListAsync(ct);

        Print("4. rolling_revenue_metrics", rows);
    }

    // 5. route_network_abc_xyz.sql
    public static async Task RouteNetworkAbcXyz(IDataContext ctx, CancellationToken ct)
    {
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
                Route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                FlightMonth = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture),
                MonthlyRevenue = SqlFunctions.Sql.sum(p.Item4.Price),
                PassengerCount = SqlFunctions.Sql.count_distinct(p.Item5.TicketNo)
            });

        // CTE route_aggregates.
        var aggregates = ctx.From(monthly)
            .GroupBy(r => new { r.Route })
            .Select(r => new
            {
                r.Route,
                TotalRevenue = SqlFunctions.Sql.sum(r.MonthlyRevenue),
                AvgPassengers = SqlFunctions.Sql.avg(r.PassengerCount),
                StddevPassengers = SqlFunctions.Sql.stdev(r.PassengerCount)
            });

        // CTE abc_analys: the running share is a window over the route aggregate.
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
                RevenueMln = Math.Round(a.TotalRevenue / 1000000.0m, 2),
                Abc = a.RunningPercent <= 0.80m ? "A"
                    : a.RunningPercent <= 0.95m ? "B"
                    : "C",
                Xyz = a.AvgPassengers == 0 ? "Z"
                    : a.StddevPassengers / a.AvgPassengers < 0.10 ? "X"
                    : a.StddevPassengers / a.AvgPassengers <= 0.25 ? "Y"
                    : "Z"
            })
            .ToListAsync(ct);

        Print("5. route_network_abc_xyz", rows);
    }

    // 6. top_routes_by_city (course: "top-N per group" with RANK)
    public static async Task TopRoutesByCity(IDataContext ctx, CancellationToken ct)
    {
        var routeRevenue = ctx.From<ITimetable>()
            .Join(ctx.From<ISegment>(), (f, tf) => f.FlightId == tf.FlightId)
            .Join(ctx.From<IAirportData>(), (p, dep) => p.Item1.DepartureAirport == dep.AirportCode)
            .Join(ctx.From<IAirportData>(), (p, arr) => p.Item1.ArrivalAirport == arr.AirportCode)
            .Where(p => p.Item1.Status == "Departed" || p.Item1.Status == "Arrived")
            .GroupBy(p => new
            {
                DepartureCity = SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                Route = SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item4.City, "ru")
            })
            .Select(p => new
            {
                DepartureCity = SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                Route = SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item4.City, "ru"),
                Revenue = SqlFunctions.Sql.sum(p.Item2.Price)
            });

        // RANK() OVER (PARTITION BY city ORDER BY revenue DESC) has to be computed in a derived table,
        // because a window value cannot be filtered in the same SELECT.
        var ranked = ctx.From(routeRevenue)
            .Select(r => new
            {
                r.DepartureCity,
                r.Route,
                r.Revenue,
                Place = SqlFunctions.Sql.rank().Over(
                    partitionBy: new Expression<Func<object?>>[] { () => r.DepartureCity },
                    orderBy: new[] { SqlFunctions.Sql.desc(() => r.Revenue) })
            });

        var rows = await ctx.From(ranked)
            .Where(r => r.Place <= 3)
            .OrderBy(r => r.DepartureCity)
            .OrderBy(r => r.Place)
            .Select(r => new
            {
                r.DepartureCity,
                r.Place,
                r.Route,
                RevenueMln = Math.Round(r.Revenue / 1000000.0m, 2)
            })
            .ToListAsync(ct);

        Print("6. top_routes_by_city", rows);
    }

    // 7. airport_otp (course: conditional aggregation / FILTER)
    public static async Task AirportOnTimePerformance(IDataContext ctx, CancellationToken ct)
    {
        var flights = ctx.From<ITimetable>()
            .Join(ctx.From<IAirportData>(), (f, dep) => f.DepartureAirport == dep.AirportCode)
            .Where(p => p.Item1.Status == "Departed" || p.Item1.Status == "Arrived" || p.Item1.Status == "Cancelled")
            .Select(p => new
            {
                City = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru"),
                Delay = SqlFunctions.Sql.date_diff("minute",
                    p.Item1.ScheduledDeparture, p.Item1.ActualDeparture),
                IsDelayed = p.Item1.ActualDeparture != null
                            && SqlFunctions.Sql.date_diff("minute", p.Item1.ScheduledDeparture, p.Item1.ActualDeparture) > 15
                    ? 1 : 0,
                IsCancelled = p.Item1.Status == "Cancelled" ? 1 : 0
            });

        var stats = ctx.From(flights)
            .GroupBy(f => new { f.City })
            .Select(f => new
            {
                f.City,
                Flights = SqlFunctions.Sql.count(),
                Delayed = SqlFunctions.Sql.sum(f.IsDelayed),
                Cancelled = SqlFunctions.Sql.sum(f.IsCancelled),
                AvgDelayMinutes = SqlFunctions.Sql.avg(f.Delay)
            });

        var rows = await ctx.From(stats)
            .Where(s => s.Flights >= 50)
            .OrderByDescending(s => (double)s.Delayed / s.Flights)
            .OrderBy(s => s.City)
            .Select(s => new
            {
                s.City,
                s.Flights,
                s.Delayed,
                DelayedPct = Math.Round((double)s.Delayed / s.Flights * 100, 2),
                s.Cancelled,
                AvgDelayMinutes = Math.Round(s.AvgDelayMinutes ?? 0.0, 1)
            })
            .ToListAsync(ct);

        Print("7. airport_otp", rows);
    }

    // 8. delay_percentiles_by_model (course: percentile_disc ... WITHIN GROUP)
    public static async Task DelayPercentilesByModel(IDataContext ctx, CancellationToken ct)
    {
        var stats = ctx.From<ITimetable>()
            .Join(ctx.From<IAirplaneData>(), (f, a) => f.AirplaneCode == a.AirplaneCode)
            .Where(p => (p.Item1.Status == "Departed" || p.Item1.Status == "Arrived") && p.Item1.ActualDeparture != null)
            .GroupBy(p => new { Model = SqlFunctions.Postgres.json_get_text(p.Item2.Model, "ru") })
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Item2.Model, "ru"),
                Flights = SqlFunctions.Sql.count(),
                MedianDelay = SqlFunctions.Postgres.percentile_disc(0.5,
                    () => SqlFunctions.Sql.date_diff("minute", p.Item1.ScheduledDeparture, p.Item1.ActualDeparture)),
                P90Delay = SqlFunctions.Postgres.percentile_disc(0.9,
                    () => SqlFunctions.Sql.date_diff("minute", p.Item1.ScheduledDeparture, p.Item1.ActualDeparture))
            });

        var rows = await ctx.From(stats)
            .OrderBy(s => s.Model)
            .Select(s => new { s.Model, s.Flights, s.MedianDelay, s.P90Delay })
            .ToListAsync(ct);

        Print("8. delay_percentiles_by_model", rows);
    }

    // 9. frequent_flyers (course: string_agg / group_concat)
    public static async Task FrequentFlyers(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<ITicket>()
            .Join(ctx.From<ISegment>(), (t, tf) => t.TicketNo == tf.TicketNo)
            .Join(ctx.From<ITimetable>(), (p, f) => p.Item2.FlightId == f.FlightId)
            .Join(ctx.From<IAirportData>(), (p, dep) => p.Item3.DepartureAirport == dep.AirportCode)
            .Join(ctx.From<IAirportData>(), (p, arr) => p.Item3.ArrivalAirport == arr.AirportCode)
            .Where(p => p.Item3.Status == "Departed" || p.Item3.Status == "Arrived")
            .GroupBy(p => new { p.Item1.PassengerId, p.Item1.PassengerName })
            .Having(g => SqlFunctions.Sql.count() >= 5)
            .OrderByDescending(g => SqlFunctions.Sql.count())
            .Limit(10)
            .Select(g => new
            {
                g.Item1.PassengerId,
                g.Item1.PassengerName,
                Flights = SqlFunctions.Sql.count(),
                Routes = SqlFunctions.Sql.string_agg(
                    SqlFunctions.Postgres.json_get_text(g.Item4.City, "ru")
                    + " -> " + SqlFunctions.Postgres.json_get_text(g.Item5.City, "ru"),
                    ", ")
            })
            .ToListAsync(ct);

        Print("9. frequent_flyers", rows);
    }

    // 10. passenger_growth_mom (course: LAG over a monthly aggregate)
    public static async Task PassengerGrowth(IDataContext ctx, CancellationToken ct)
    {
        var monthly = ctx.From<ITimetable>()
            .Join(ctx.From<ISegment>(), (f, tf) => f.FlightId == tf.FlightId)
            .Join(ctx.From<ITicket>(), (p, t) => p.Item2.TicketNo == t.TicketNo)
            .Where(p => p.Item1.Status == "Departed" || p.Item1.Status == "Arrived")
            .GroupBy(p => new { Month = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture) })
            .Select(p => new
            {
                Month = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture),
                Passengers = SqlFunctions.Sql.count_distinct(p.Item3.PassengerId),
                Revenue = SqlFunctions.Sql.sum(p.Item2.Price)
            });

        var withPrevious = ctx.From(monthly)
            .Select(m => new
            {
                m.Month,
                m.Passengers,
                m.Revenue,
                PrevPassengers = SqlFunctions.Sql.lag(m.Passengers)
                    .Over(SqlFunctions.Sql.asc(() => m.Month))
            });

        var rows = await ctx.From(withPrevious)
            .OrderBy(m => m.Month)
            .Select(m => new
            {
                Month = SqlFunctions.Postgres.to_char(m.Month, "YYYY-MM"),
                m.Passengers,
                RevenueMln = Math.Round(m.Revenue / 1000000.0m, 2),
                GrowthPct = m.PrevPassengers <= 0
                    ? (double?)null
                    : Math.Round((double)(m.Passengers - m.PrevPassengers) / m.PrevPassengers * 100, 1)
            })
            .ToListAsync(ct);

        Print("10. passenger_growth_mom", rows);
    }

    // 11. cancellation_by_route (course: conditional count + rate, TOP N)
    public static async Task CancellationByRoute(IDataContext ctx, CancellationToken ct)
    {
        var perRoute = ctx.From<ITimetable>()
            .Join(ctx.From<IAirportData>(), (f, dep) => f.DepartureAirport == dep.AirportCode)
            .Join(ctx.From<IAirportData>(), (p, arr) => p.Item1.ArrivalAirport == arr.AirportCode)
            .GroupBy(p => new
            {
                Route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru")
            })
            .Select(p => new
            {
                Route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                Flights = SqlFunctions.Sql.count(),
                Cancelled = SqlFunctions.Sql.count(() => p.Item1.Status == "Cancelled")
            });

        var rows = await ctx.From(perRoute)
            .Where(r => r.Flights >= 30 && r.Cancelled > 0)
            .OrderByDescending(r => (double)r.Cancelled / r.Flights)
            .Limit(15)
            .Select(r => new
            {
                r.Route,
                r.Flights,
                r.Cancelled,
                CancelRatePct = Math.Round((double)r.Cancelled / r.Flights * 100, 2)
            })
            .ToListAsync(ct);

        Print("11. cancellation_by_route", rows);
    }

    private static void Print(string title, IEnumerable<object> rows)
    {
        var list = rows.ToList();
        Console.WriteLine($"-- {title}: {list.Count} row(s)");

        foreach (var row in list.Take(10))
            Console.WriteLine("   " + JsonSerializer.Serialize(row));

        if (list.Count > 10)
            Console.WriteLine("   ...");

        Console.WriteLine();
    }
}
