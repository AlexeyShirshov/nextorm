using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models and states whether it is <c>WORKING</c>; when it is not, the comment says why (with the
/// roadmap document that tracks the gap). Each working method prints the number of rows and the first
/// few rows as JSON.
/// </summary>
public static class AviasalesQueries
{
    // 1. Sql/aircraft_delay_chains.sql — WITH flight_delays AS (...), delay_chains AS (...), then JOIN
    // WORKING: two CTEs; delay_chains reads flight_delays by name; the outer query joins the CTE to
    // airplanes_data (the CTE API supports the join, unlike the old derived-query form).
    public static async Task AircraftDelayChains(IDataContext ctx, CancellationToken ct)
    {
        // CTE flight_delays (delay_minutes + LAG over the same partition).
        var flightDelays = ctx.From<ITimetable>()
            .Where(f => (f.Status == "Departed" || f.Status == "Arrived") && f.ActualDeparture != null)
            .Select(f => new
            {
                flight_id = f.FlightId,
                airplane_code = f.AirplaneCode,
                actual_departure = f.ActualDeparture,
                scheduled_departure = f.ScheduledDeparture,
                // EXTRACT(EPOCH FROM actual_departure - scheduled_departure) / 60
                delay_minutes = SqlFunctions.Sql.date_diff("second", f.ScheduledDeparture, f.ActualDeparture) / 60.0,
                prev_departure = SqlFunctions.Sql.lag(f.ActualDeparture)
                    .Over(partitionBy: () => f.AirplaneCode, orderBy: () => f.ActualDeparture),
                prev_delay_minutes = SqlFunctions.Sql.lag(
                        SqlFunctions.Sql.date_diff("second", f.ScheduledDeparture, f.ActualDeparture) / 60.0)
                    .Over(partitionBy: () => f.AirplaneCode, orderBy: () => f.ActualDeparture)
            });

        // CTE delay_chains: SELECT *, CASE ... AS is_chain_link FROM flight_delays.
        var delayChains = ctx.From("flight_delays")
            .Select(d => new
            {
                flight_id = d.GetInt32("flight_id"),
                airplane_code = d.GetString("airplane_code"),
                actual_departure = d.GetNullableDateTime("actual_departure"),
                scheduled_departure = d.GetDateTime("scheduled_departure"),
                delay_minutes = d.GetDouble("delay_minutes"),
                prev_departure = d.GetNullableDateTime("prev_departure"),
                prev_delay_minutes = d.GetNullableDouble("prev_delay_minutes"),
                is_chain_link = d.GetDouble("delay_minutes") > 15 && d.GetNullableDouble("prev_delay_minutes") > 15
                    ? 1 : 0
            });

        var rows = await ctx.With("flight_delays", flightDelays).With("delay_chains", delayChains)
            .From("delay_chains")
            .Join(ctx.From<IAirplaneData>(), (dc, ac) => dc.GetString("airplane_code") == ac.AirplaneCode)
            .Where(p => p.Item1.GetDouble("delay_minutes") > 30 && p.Item1.GetNullableDouble("prev_delay_minutes") > 30)
            .OrderBy(p => p.Item1.GetString("airplane_code"))
            .OrderBy(p => p.Item1.GetDateTime("scheduled_departure"))
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Item2.Model, "ru"),
                AirplaneCode = p.Item1.GetString("airplane_code"),
                ScheduledDeparture = p.Item1.GetDateTime("scheduled_departure"),
                DelayMinutes = Math.Round(p.Item1.GetDouble("delay_minutes"), 1),
                PrevDelayMinutes = Math.Round(p.Item1.GetNullableDouble("prev_delay_minutes") ?? 0.0, 1)
            })
            .ToListAsync(ct);

        Print("1. aircraft_delay_chains", rows);
    }

    // 2. Sql/business_occupancy_matrix.sql — WITH flight_business_capacity, flight_occupancy, then joins
    // WORKING: two CTEs expressed as CTEs, joined to each other and to airplanes_data; the pivot is a
    // plain AVG(CASE ...) over day_of_week.
    public static async Task BusinessOccupancyMatrix(IDataContext ctx, CancellationToken ct)
    {
        // CTE flight_business_capacity: COUNT(*) over the business seats per airplane.
        var capacity = ctx.From<ISeat>()
            .Where(s => s.FareConditions == "Business")
            .GroupBy(s => new { s.AirplaneCode })
            .Select(s => new { airplane_code = s.AirplaneCode, total_business_seats = SqlFunctions.Sql.count() });

        // CTE flight_occupancy: COUNT(bp.seat_no) per flight (no seats join, so no fan-out).
        var occupancy = ctx.From<ITimetable>()
            .Join(ctx.From<ISegment>(), (f, tf) => f.FlightId == tf.FlightId && tf.FareConditions == "Business")
            .LeftJoin(ctx.From<IBoardingPass>(),
                (p, bp) => p.Item2.FlightId == bp.FlightId && p.Item2.TicketNo == bp.TicketNo)
            .Where(p => p.Item1.Status == "Departed" || p.Item1.Status == "Arrived")
            .GroupBy(p => new { p.Item1.FlightId, p.Item1.AirplaneCode, p.Item1.ScheduledDeparture })
            .Select(p => new
            {
                flight_id = p.Item1.FlightId,
                airplane_code = p.Item1.AirplaneCode,
                scheduled_departure = p.Item1.ScheduledDeparture,
                // EXTRACT(ISODOW FROM scheduled_departure): days since the ISO Monday + 1
                day_of_week = SqlFunctions.Sql.date_diff("day",
                    SqlFunctions.Sql.date_trunc("week", p.Item1.ScheduledDeparture), p.Item1.ScheduledDeparture) + 1,
                occupied_business_seats = SqlFunctions.Sql.count(p.Item3.SeatNo)
            });

        // FROM flight_occupancy JOIN flight_business_capacity JOIN airplanes_data.
        var scope = ctx.With("flight_business_capacity", capacity).With("flight_occupancy", occupancy);

        var rows = await scope
            .From("flight_occupancy")
            .Join(scope.From("flight_business_capacity"),
                (fo, cap) => fo.GetString("airplane_code") == cap.GetString("airplane_code"))
            .Join(ctx.From<IAirplaneData>(), (p, ac) => p.Item1.GetString("airplane_code") == ac.AirplaneCode)
            .GroupBy(p => new { model = SqlFunctions.Postgres.json_get_text(p.Item3.Model, "ru") })
            .OrderBy(p => SqlFunctions.Postgres.json_get_text(p.Item3.Model, "ru"))
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Item3.Model, "ru"),
                Mon = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 1
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Tue = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 2
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Wed = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 3
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Thu = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 4
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Fri = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 5
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Sat = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 6
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1),
                Sun = Math.Round(SqlFunctions.Sql.avg(p.Item1.GetInt32("day_of_week") == 7
                        ? (decimal?)p.Item1.GetInt32("occupied_business_seats")
                          / SqlFunctions.Sql.nullif(p.Item2.GetInt32("total_business_seats"), 0) * 100
                        : null) ?? 0m, 1)
            })
            .ToListAsync(ct);

        Print("2. business_occupancy_matrix", rows);
    }

    // 3. Sql/passenger_noshow_analysis.sql — WITH passenger_flight_history, passenger_stats
    // WORKING: two CTEs; passenger_stats groups passenger_flight_history by name.
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
                passenger_id = p.Item1.PassengerId,
                passenger_name = p.Item1.PassengerName,
                ticket_no = p.Item1.TicketNo,
                flight_id = p.Item3.FlightId,
                price = p.Item2.Price,
                is_noshow = p.Item4.SeatNo == null ? 1 : 0
            });

        // CTE passenger_stats: COUNT(flight_id), SUM(is_noshow), SUM(price).
        var stats = ctx.From("passenger_flight_history")
            .GroupBy(h => new { passenger_id = h.GetString("passenger_id"), passenger_name = h.GetString("passenger_name") })
            .Select(h => new
            {
                passenger_id = h.GetString("passenger_id"),
                passenger_name = h.GetString("passenger_name"),
                total_booked_flights = SqlFunctions.Sql.count(h.GetInt32("flight_id")),
                total_noshows = SqlFunctions.Sql.sum(h.GetInt32("is_noshow")),
                wasted_money = SqlFunctions.Sql.sum(h.GetDecimal("price"))
            });

        var rows = await ctx.With("passenger_flight_history", history).With("passenger_stats", stats)
            .From("passenger_stats")
            .Where(s => s.GetInt32("total_booked_flights") >= 3
                        && s.GetInt32("total_booked_flights") == s.GetInt32("total_noshows"))
            .OrderByDescending(s => s.GetDecimal("wasted_money"))
            .OrderBy(s => s.GetString("passenger_name"))
            .Select(s => new
            {
                PassengerId = s.GetString("passenger_id"),
                PassengerName = s.GetString("passenger_name"),
                TotalBookedFlights = s.GetInt32("total_booked_flights"),
                WastedMoney = s.GetDecimal("wasted_money")
            })
            .ToListAsync(ct);

        Print("3. passenger_noshow_analysis", rows);
    }

    // 4. Sql/rolling_revenue_metrics.sql — WITH daily_revenue, then SUM/AVG windows
    // WORKING: CTE daily_revenue; the outer query reads it by name and applies framed windows.
    public static async Task RollingRevenueMetrics(IDataContext ctx, CancellationToken ct)
    {
        // CTE daily_revenue.
        var daily = ctx.From<IBooking>()
            .GroupBy(b => new { sales_date = SqlFunctions.Sql.date_trunc("day", b.BookDate) })
            .Select(b => new
            {
                sales_date = SqlFunctions.Sql.date_trunc("day", b.BookDate),
                daily_amount = SqlFunctions.Sql.sum(b.TotalAmount)
            });

        var rows = await ctx.With("daily_revenue", daily)
            .From("daily_revenue")
            .OrderByDescending(d => d.GetDateTime("sales_date"))
            .Select(d => new
            {
                sales_date = d.GetDateTime("sales_date"),
                daily_amount = d.GetDecimal("daily_amount"),
                cumulative = SqlFunctions.Sql.sum_over(d.GetDecimal("daily_amount")).Over(
                    SqlFunctions.Sql.asc(() => (object?)d.GetDateTime("sales_date")),
                    WindowFrame.RowsUnboundedPrecedingToCurrentRow),
                moving_avg_7 = Math.Round(
                    SqlFunctions.Sql.avg_over(d.GetDecimal("daily_amount")).Over(
                        SqlFunctions.Sql.asc(() => (object?)d.GetDateTime("sales_date")),
                        WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow)),
                    2)
            })
            .ToListAsync(ct);

        Print("4. rolling_revenue_metrics", rows);
    }

    // 5. Sql/route_network_abc_xyz.sql — WITH route_monthly_revenue, route_aggregates, abc_analys
    // WORKING: three chained CTEs; the running share is a window over the route aggregate.
    public static async Task RouteNetworkAbcXyz(IDataContext ctx, CancellationToken ct)
    {
        // CTE route_monthly_revenue.
        var monthly = ctx.From<ITimetable>()
            .Join(ctx.From<IAirportData>(), (f, dep) => f.DepartureAirport == dep.AirportCode)
            .Join(ctx.From<IAirportData>(), (p, arr) => p.Item1.ArrivalAirport == arr.AirportCode)
            .Join(ctx.From<ISegment>(), (p, tf) => p.Item1.FlightId == tf.FlightId)
            .Join(ctx.From<ITicket>(), (p, t) => p.Item4.TicketNo == t.TicketNo)
            .GroupBy(p => new
            {
                route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                flight_month = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture)
            })
            .Select(p => new
            {
                route = SqlFunctions.Postgres.json_get_text(p.Item2.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Item3.City, "ru"),
                flight_month = SqlFunctions.Sql.date_trunc("month", p.Item1.ScheduledDeparture),
                monthly_revenue = SqlFunctions.Sql.sum(p.Item4.Price),
                passenger_count = SqlFunctions.Sql.count_distinct(p.Item5.TicketNo)
            });

        // CTE route_aggregates.
        var aggregates = ctx.From("route_monthly_revenue")
            .GroupBy(r => new { route = r.GetString("route") })
            .Select(r => new
            {
                route = r.GetString("route"),
                total_revenue = SqlFunctions.Sql.sum(r.GetDecimal("monthly_revenue")),
                avg_passengers = SqlFunctions.Sql.avg(r.GetDecimal("passenger_count")),
                stddev_passengers = SqlFunctions.Sql.stdev(r.GetDecimal("passenger_count"))
            });

        // CTE abc_analys: the running share is a window over the route aggregate.
        var abc = ctx.From("route_aggregates")
            .Select(a => new
            {
                route = a.GetString("route"),
                total_revenue = a.GetDecimal("total_revenue"),
                avg_passengers = a.GetNullableDecimal("avg_passengers"),
                stddev_passengers = a.GetNullableDecimal("stddev_passengers"),
                running_percent = SqlFunctions.Sql.sum_over(a.GetDecimal("total_revenue"))
                                      .Over(SqlFunctions.Sql.desc(() => (object?)a.GetDecimal("total_revenue")))
                                  / SqlFunctions.Sql.sum_over(a.GetDecimal("total_revenue")).Over()
            });

        var rows = await ctx.With("route_monthly_revenue", monthly)
            .With("route_aggregates", aggregates)
            .With("abc_analys", abc)
            .From("abc_analys")
            .OrderByDescending(a => a.GetDecimal("total_revenue"))
            .Select(a => new
            {
                Route = a.GetString("route"),
                RevenueMln = Math.Round(a.GetDecimal("total_revenue") / 1000000.0m, 2),
                Abc = a.GetDecimal("running_percent") <= 0.80m ? "A"
                    : a.GetDecimal("running_percent") <= 0.95m ? "B"
                    : "C",
                Xyz = a.GetNullableDecimal("avg_passengers") == 0 || a.GetNullableDecimal("stddev_passengers") == null ? "Z"
                    : a.GetNullableDecimal("stddev_passengers") / a.GetNullableDecimal("avg_passengers") < 0.10m ? "X"
                    : a.GetNullableDecimal("stddev_passengers") / a.GetNullableDecimal("avg_passengers") <= 0.25m ? "Y"
                    : "Z"
            })
            .ToListAsync(ct);

        Print("5. route_network_abc_xyz", rows);
    }

    // 6. top_routes_by_city
    // WORKING: RANK() OVER (PARTITION BY city ORDER BY revenue DESC) top-N per group.
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

    // 7. airport_otp
    // WORKING: conditional aggregation (SUM(CASE…)) plus a derived-query HAVING-equivalent filter.
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

    // 8. delay_percentiles_by_model
    // WORKING: percentile_disc(...) WITHIN GROUP (ORDER BY …).
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

    // 9. frequent_flyers
    // WORKING: string_agg + HAVING count() >= 5 + LIMIT.
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

    // 10. passenger_growth_mom
    // WORKING: LAG over a monthly aggregate.
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

    // 11. cancellation_by_route
    // WORKING: conditional COUNT + rate, top-N.
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
