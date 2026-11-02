using System.Linq.Expressions;
using System.Text.Json;
using NextORM.Core;
using NextORM.Generated.nextorm_examples_postgres_aviasales;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models and states whether it is <c>WORKING</c>; when it is not, the comment says why (with the
/// roadmap document that tracks the gap). Each method returns its row list; the runner prints it.
/// </summary>
public static class AviasalesQueries
{
    // 1. Sql/aircraft_delay_chains.sql — WITH flight_delays AS (...), delay_chains AS (...), then JOIN
    // WORKING: two typed CTEs (`AsCte`); delay_chains reads flight_delays with member access and is
    // projected into the named `DelayChainRow`; the outer query names the root and the joined table
    // (`WithAlias(Alias.DelayChain)` + `Alias.Airplane`). The named type is required by the alias
    // surface (#229), which renders the chain's concrete type and refuses an anonymous one.
    public static async Task<object> AircraftDelayChains(IDataContext ctx, CancellationToken ct)
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
            })
            .AsCte("flight_delays");

        // CTE delay_chains: SELECT *, CASE ... AS is_chain_link FROM flight_delays.
        var delayChains = ctx.From(flightDelays)
            .Select(d => new DelayChainRow
            {
                FlightId = d.flight_id,
                AirplaneCode = d.airplane_code,
                ActualDeparture = d.actual_departure,
                ScheduledDeparture = d.scheduled_departure,
                DelayMinutes = d.delay_minutes,
                PrevDeparture = d.prev_departure,
                PrevDelayMinutes = d.prev_delay_minutes,
                IsChainLink = d.delay_minutes > 15 && d.prev_delay_minutes > 15 ? 1 : 0
            })
            .AsCte("delay_chains");

        var rows = await ctx.From(delayChains).WithAlias(Alias.DelayChain)
            .Join<IAirplaneData>(
                (dc, ac) => dc.DelayChain.AirplaneCode == ac.AirplaneCode, Alias.Airplane)
            .Where(p => p.DelayChain.DelayMinutes > 30 && p.DelayChain.PrevDelayMinutes > 30)
            .OrderBy(p => p.DelayChain.AirplaneCode)
            .OrderBy(p => p.DelayChain.ScheduledDeparture)
            .Select(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Airplane.Model, "ru"),
                AirplaneCode = p.DelayChain.AirplaneCode,
                ScheduledDeparture = p.DelayChain.ScheduledDeparture,
                DelayMinutes = Math.Round(p.DelayChain.DelayMinutes ?? 0.0, 1),
                PrevDelayMinutes = Math.Round(p.DelayChain.PrevDelayMinutes ?? 0.0, 1)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 2. Sql/business_occupancy_matrix.sql — WITH flight_business_capacity, flight_occupancy, then joins
    // WORKING: two typed CTEs (`AsCte`), joined to each other and to airplanes_data; the pivot is a
    // plain AVG(CASE ...) over day_of_week. flight_occupancy is projected into the named
    // `FlightOccupancyRow`, so the outer chain can name every join (`WithAlias(Alias.Occupancy)` +
    // `Alias.Capacity`/`Alias.Airplane`); an anonymous chain type is refused by the alias surface (#229).
    public static async Task<object> BusinessOccupancyMatrix(IDataContext ctx, CancellationToken ct)
    {
        // CTE flight_business_capacity: COUNT(*) over the business seats per airplane.
        var capacity = ctx.From<ISeat>()
            .Where(s => s.FareConditions == "Business")
            .SelectGroup(s => new BusinessCapacityRow
            {
                AirplaneCode = s.AirplaneCode,
                TotalBusinessSeats = SqlFunctions.Sql.count()
            })
            .AsCte("flight_business_capacity");

        // CTE flight_occupancy: COUNT(bp.seat_no) per flight (no seats join, so no fan-out).
        var occupancy = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<ISegment>((f, tf) => f.Timetable.FlightId == tf.FlightId && tf.FareConditions == "Business", Alias.Segment)
            .LeftJoin<IBoardingPass>(
                (p, bp) => p.Segment.FlightId == bp.FlightId && p.Segment.TicketNo == bp.TicketNo, Alias.BoardingPass)
            .Where(p => p.Timetable.Status == "Departed" || p.Timetable.Status == "Arrived")
            // Explicit GroupBy, not SelectGroup: DayOfWeek is a computed projection column that the
            // reference GROUP BY does not list, so key inference would add it as a fourth key.
            .GroupBy(p => new { p.Timetable.FlightId, p.Timetable.AirplaneCode, p.Timetable.ScheduledDeparture })
            .Select(p => new FlightOccupancyRow
            {
                FlightId = p.Timetable.FlightId,
                AirplaneCode = p.Timetable.AirplaneCode,
                ScheduledDeparture = p.Timetable.ScheduledDeparture,
                // EXTRACT(ISODOW FROM scheduled_departure): days since the ISO Monday + 1
                DayOfWeek = SqlFunctions.Sql.date_diff("day",
                    SqlFunctions.Sql.date_trunc("week", p.Timetable.ScheduledDeparture), p.Timetable.ScheduledDeparture) + 1,
                OccupiedBusinessSeats = SqlFunctions.Sql.count(p.BoardingPass.SeatNo)
            })
            .AsCte("flight_occupancy");

        // FROM flight_occupancy JOIN flight_business_capacity JOIN airplanes_data.
        var rows = await ctx.From(occupancy).WithAlias(Alias.Occupancy)
            .Join(capacity, (fo, cap) => fo.Occupancy.AirplaneCode == cap.AirplaneCode, Alias.Capacity)
            .Join<IAirplaneData>(
                (p, ac) => p.Occupancy.AirplaneCode == ac.AirplaneCode, Alias.Airplane)
            .OrderBy(p => SqlFunctions.Postgres.json_get_text(p.Airplane.Model, "ru"))
            .SelectGroup(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Airplane.Model, "ru"),
                Mon = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 1
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Tue = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 2
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Wed = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 3
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Thu = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 4
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Fri = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 5
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Sat = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 6
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1),
                Sun = Math.Round(SqlFunctions.Sql.avg(p.Occupancy.DayOfWeek == 7
                        ? (decimal?)p.Occupancy.OccupiedBusinessSeats
                          / SqlFunctions.Sql.nullif(p.Capacity.TotalBusinessSeats, 0) * 100
                        : null) ?? 0m, 1)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 3. Sql/passenger_noshow_analysis.sql — WITH passenger_flight_history, passenger_stats
    // WORKING: two typed CTEs (`AsCte`); passenger_stats groups passenger_flight_history with member access.
    public static async Task<object> PassengerNoShowAnalysis(IDataContext ctx, CancellationToken ct)
    {
        // CTE passenger_flight_history: carries the is_noshow flag exactly as the original.
        var history = ctx.From<ITicket>().WithAlias(Alias.Ticket)
            .Join<ISegment>((t, tf) => t.Ticket.TicketNo == tf.TicketNo, Alias.Segment)
            .Join<IFlight>((p, f) => p.Segment.FlightId == f.FlightId, Alias.Flight)
            .LeftJoin<IBoardingPass>(
                (p, bp) => p.Segment.FlightId == bp.FlightId && p.Segment.TicketNo == bp.TicketNo, Alias.BoardingPass)
            .Where(p => p.Flight.Status == "Departed" || p.Flight.Status == "Arrived")
            .Select(p => new
            {
                passenger_id = p.Ticket.PassengerId,
                passenger_name = p.Ticket.PassengerName,
                ticket_no = p.Ticket.TicketNo,
                flight_id = p.Flight.FlightId,
                price = p.Segment.Price,
                is_noshow = p.BoardingPass.SeatNo == null ? 1 : 0
            })
            .AsCte("passenger_flight_history");

        // CTE passenger_stats: COUNT(flight_id), SUM(is_noshow), SUM(price).
        var stats = ctx.From(history)
            .SelectGroup(h => new
            {
                h.passenger_id,
                h.passenger_name,
                total_booked_flights = SqlFunctions.Sql.count(h.flight_id),
                total_noshows = SqlFunctions.Sql.sum(h.is_noshow),
                wasted_money = SqlFunctions.Sql.sum(h.price)
            })
            .AsCte("passenger_stats");

        var rows = await ctx.From(stats)
            .Where(s => s.total_booked_flights >= 3 && s.total_booked_flights == s.total_noshows)
            .OrderByDescending(s => s.wasted_money)
            .OrderBy(s => s.passenger_name)
            .Select(s => new
            {
                PassengerId = s.passenger_id,
                PassengerName = s.passenger_name,
                TotalBookedFlights = s.total_booked_flights,
                WastedMoney = s.wasted_money
            })
            .ToListAsync(ct);

        return rows;
    }

    // 4. Sql/rolling_revenue_metrics.sql — WITH daily_revenue, then SUM/AVG windows
    // WORKING: typed CTE daily_revenue (`AsCte`); the outer query reads it with member access and applies
    // framed windows.
    public static async Task<object> RollingRevenueMetrics(IDataContext ctx, CancellationToken ct)
    {
        // CTE daily_revenue.
        var daily = ctx.From<IBooking>()
            .SelectGroup(b => new
            {
                sales_date = SqlFunctions.Sql.date_trunc("day", b.BookDate),
                daily_amount = SqlFunctions.Sql.sum(b.TotalAmount)
            })
            .AsCte("daily_revenue");

        var rows = await ctx.From(daily)
            .OrderByDescending(d => d.sales_date)
            .Select(d => new
            {
                d.sales_date,
                d.daily_amount,
                cumulative = SqlFunctions.Sql.sum_over(d.daily_amount).Over(
                    SqlFunctions.Sql.asc(() => d.sales_date),
                    WindowFrame.RowsUnboundedPrecedingToCurrentRow),
                moving_avg_7 = Math.Round(
                    SqlFunctions.Sql.avg_over(d.daily_amount).Over(
                        SqlFunctions.Sql.asc(() => d.sales_date),
                        WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow))!.Value,
                    2)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 5. Sql/route_network_abc_xyz.sql — WITH route_monthly_revenue, route_aggregates, abc_analys
    // WORKING: three chained typed CTEs (`AsCte`); the running share is a window over the route aggregate.
    // The two grouped CTEs use `SelectGroup`, which infers `GROUP BY` from the projection instead of a
    // separate `GroupBy` call. route_monthly_revenue names all five slots of its join chain
    // (`WithAlias(Alias.Timetable)` plus `Alias.Departure`/`Arrival`/`Segment`/`Ticket`): the root is the
    // mapped `ITimetable` table, so no DTO is needed — NORMGEN009 only rejects an anonymous root or
    // intermediate join step (#229).
    // `avg`/`stdev` are projected as nullable (`(decimal?)`) so the one-month-per-route case stays
    // distinguishable from zero.
    public static async Task<object> RouteNetworkAbcXyz(IDataContext ctx, CancellationToken ct)
    {
        // CTE route_monthly_revenue. SelectGroup infers the GROUP BY keys from the projection: the
        // route expression and the month (first-appearance order), so the two aggregate calls never
        // become keys; the rendered GROUP BY matches the explicit GroupBy(route, flight_month) it replaced.
        var monthly = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<IAirportData>((f, dep) => f.Timetable.DepartureAirport == dep.AirportCode, Alias.Departure)
            .Join<IAirportData>((p, arr) => p.Timetable.ArrivalAirport == arr.AirportCode, Alias.Arrival)
            .Join<ISegment>((p, tf) => p.Timetable.FlightId == tf.FlightId, Alias.Segment)
            .Join<ITicket>((p, t) => p.Segment.TicketNo == t.TicketNo, Alias.Ticket)
            .SelectGroup(p => new
            {
                route = SqlFunctions.Postgres.json_get_text(p.Departure.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Arrival.City, "ru"),
                flight_month = SqlFunctions.Sql.date_trunc("month", p.Timetable.ScheduledDeparture),
                monthly_revenue = SqlFunctions.Sql.sum(p.Segment.Price),
                passenger_count = SqlFunctions.Sql.count_distinct(p.Ticket.TicketNo)
            })
            .AsCte("route_monthly_revenue");

        // CTE route_aggregates: one key (`r.route`) and three aggregates, inferred the same way.
        var aggregates = ctx.From(monthly)
            .SelectGroup(r => new
            {
                r.route,
                total_revenue = SqlFunctions.Sql.sum(r.monthly_revenue),
                avg_passengers = (decimal?)SqlFunctions.Sql.avg((decimal)r.passenger_count),
                stddev_passengers = (decimal?)SqlFunctions.Sql.stdev((decimal)r.passenger_count)
            })
            .AsCte("route_aggregates");

        // CTE abc_analys: the running share is a window over the route aggregate.
        var abc = ctx.From(aggregates)
            .Select(a => new
            {
                a.route,
                a.total_revenue,
                a.avg_passengers,
                a.stddev_passengers,
                running_percent = SqlFunctions.Sql.sum_over(a.total_revenue)
                                      .Over(SqlFunctions.Sql.desc(() => a.total_revenue))
                                  / SqlFunctions.Sql.sum_over(a.total_revenue).Over()
            })
            .AsCte("abc_analys");

        var rows = await ctx.From(abc)
            .OrderByDescending(a => a.total_revenue)
            .Select(a => new
            {
                Route = a.route,
                RevenueMln = Math.Round(a.total_revenue / 1000000.0m, 2),
                Abc = a.running_percent <= 0.80m ? "A"
                    : a.running_percent <= 0.95m ? "B"
                    : "C",
                Xyz = a.avg_passengers == 0 || a.stddev_passengers == null ? "Z"
                    : a.stddev_passengers / a.avg_passengers < 0.10m ? "X"
                    : a.stddev_passengers / a.avg_passengers <= 0.25m ? "Y"
                    : "Z"
            })
            .ToListAsync(ct);

        return rows;
    }

    // 6. top_routes_by_city
    // WORKING: RANK() OVER (PARTITION BY city ORDER BY revenue DESC) top-N per group.
    public static async Task<object> TopRoutesByCity(IDataContext ctx, CancellationToken ct)
    {
        var routeRevenue = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<ISegment>((f, tf) => f.Timetable.FlightId == tf.FlightId, Alias.Segment)
            .Join<IAirportData>((p, dep) => p.Timetable.DepartureAirport == dep.AirportCode, Alias.Departure)
            .Join<IAirportData>((p, arr) => p.Timetable.ArrivalAirport == arr.AirportCode, Alias.Arrival)
            .Where(p => p.Timetable.Status == "Departed" || p.Timetable.Status == "Arrived")
            .SelectGroup(p => new
            {
                DepartureCity = SqlFunctions.Postgres.json_get_text(p.Departure.City, "ru"),
                Route = SqlFunctions.Postgres.json_get_text(p.Departure.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Arrival.City, "ru"),
                Revenue = SqlFunctions.Sql.sum(p.Segment.Price)
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

        return rows;
    }

    // 7. airport_otp
    // WORKING: conditional aggregation (SUM(CASE…)) plus a derived-query HAVING-equivalent filter.
    public static async Task<object> AirportOnTimePerformance(IDataContext ctx, CancellationToken ct)
    {
        var flights = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<IAirportData>((f, dep) => f.Timetable.DepartureAirport == dep.AirportCode, Alias.Departure)
            .Where(p => p.Timetable.Status == "Departed" || p.Timetable.Status == "Arrived" || p.Timetable.Status == "Cancelled")
            .Select(p => new
            {
                City = SqlFunctions.Postgres.json_get_text(p.Departure.City, "ru"),
                Delay = SqlFunctions.Sql.date_diff("minute",
                    p.Timetable.ScheduledDeparture, p.Timetable.ActualDeparture),
                IsDelayed = p.Timetable.ActualDeparture != null
                            && SqlFunctions.Sql.date_diff("minute", p.Timetable.ScheduledDeparture, p.Timetable.ActualDeparture) > 15
                    ? 1 : 0,
                IsCancelled = p.Timetable.Status == "Cancelled" ? 1 : 0
            });

        var stats = ctx.From(flights)
            .SelectGroup(f => new
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

        return rows;
    }

    // 8. delay_percentiles_by_model
    // WORKING: percentile_disc(...) WITHIN GROUP (ORDER BY …).
    public static async Task<object> DelayPercentilesByModel(IDataContext ctx, CancellationToken ct)
    {
        var stats = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<IAirplaneData>((f, a) => f.Timetable.AirplaneCode == a.AirplaneCode, Alias.Airplane)
            .Where(p => (p.Timetable.Status == "Departed" || p.Timetable.Status == "Arrived")
                        && p.Timetable.ActualDeparture != null)
            .SelectGroup(p => new
            {
                Model = SqlFunctions.Postgres.json_get_text(p.Airplane.Model, "ru"),
                Flights = SqlFunctions.Sql.count(),
                MedianDelay = SqlFunctions.Postgres.percentile_disc(0.5,
                    () => SqlFunctions.Sql.date_diff("minute", p.Timetable.ScheduledDeparture, p.Timetable.ActualDeparture)),
                P90Delay = SqlFunctions.Postgres.percentile_disc(0.9,
                    () => SqlFunctions.Sql.date_diff("minute", p.Timetable.ScheduledDeparture, p.Timetable.ActualDeparture))
            });

        var rows = await ctx.From(stats)
            .OrderBy(s => s.Model)
            .Select(s => new { s.Model, s.Flights, s.MedianDelay, s.P90Delay })
            .ToListAsync(ct);

        return rows;
    }

    // 9. frequent_flyers
    // WORKING: string_agg + HAVING count() >= 5 + LIMIT.
    public static async Task<object> FrequentFlyers(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<ITicket>().WithAlias(Alias.Ticket)
            .Join<ISegment>((t, tf) => t.Ticket.TicketNo == tf.TicketNo, Alias.Segment)
            .Join<ITimetable>((p, f) => p.Segment.FlightId == f.FlightId, Alias.Flight)
            .Join<IAirportData>((p, dep) => p.Flight.DepartureAirport == dep.AirportCode, Alias.Departure)
            .Join<IAirportData>((p, arr) => p.Flight.ArrivalAirport == arr.AirportCode, Alias.Arrival)
            .Where(p => p.Flight.Status == "Departed" || p.Flight.Status == "Arrived")
            .Having(g => SqlFunctions.Sql.count() >= 5)
            .OrderByDescending(g => SqlFunctions.Sql.count())
            .Limit(10)
            .SelectGroup(g => new
            {
                g.Ticket.PassengerId,
                g.Ticket.PassengerName,
                Flights = SqlFunctions.Sql.count(),
                Routes = SqlFunctions.Sql.string_agg(
                    SqlFunctions.Postgres.json_get_text(g.Departure.City, "ru")
                    + " -> " + SqlFunctions.Postgres.json_get_text(g.Arrival.City, "ru"),
                    ", ")
            })
            .ToListAsync(ct);

        return rows;
    }

    // 10. passenger_growth_mom
    // WORKING: LAG over a monthly aggregate.
    public static async Task<object> PassengerGrowth(IDataContext ctx, CancellationToken ct)
    {
        var monthly = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<ISegment>((f, tf) => f.Timetable.FlightId == tf.FlightId, Alias.Segment)
            .Join<ITicket>((p, t) => p.Segment.TicketNo == t.TicketNo, Alias.Ticket)
            .Where(p => p.Timetable.Status == "Departed" || p.Timetable.Status == "Arrived")
            .SelectGroup(p => new
            {
                Month = SqlFunctions.Sql.date_trunc("month", p.Timetable.ScheduledDeparture),
                Passengers = SqlFunctions.Sql.count_distinct(p.Ticket.PassengerId),
                Revenue = SqlFunctions.Sql.sum(p.Segment.Price)
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

        return rows;
    }

    // 11. cancellation_by_route
    // WORKING: conditional COUNT + rate, top-N.
    public static async Task<object> CancellationByRoute(IDataContext ctx, CancellationToken ct)
    {
        var perRoute = ctx.From<ITimetable>().WithAlias(Alias.Timetable)
            .Join<IAirportData>((f, dep) => f.Timetable.DepartureAirport == dep.AirportCode, Alias.Departure)
            .Join<IAirportData>((p, arr) => p.Timetable.ArrivalAirport == arr.AirportCode, Alias.Arrival)
            .SelectGroup(p => new
            {
                Route = SqlFunctions.Postgres.json_get_text(p.Departure.City, "ru")
                        + " -> " + SqlFunctions.Postgres.json_get_text(p.Arrival.City, "ru"),
                Flights = SqlFunctions.Sql.count(),
                Cancelled = SqlFunctions.Sql.count(() => p.Timetable.Status == "Cancelled")
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

        return rows;
    }
}

// Named projections for the two aliased CTE chains. The named join-alias surface (#160/#229) generates
// an extension whose receiver is the chain's concrete type, which cannot be rendered for an anonymous
// type (NORMGEN009), so every aliased root is a named type.
public sealed class DelayChainRow
{
    public int FlightId { get; init; }
    public string AirplaneCode { get; init; } = "";
    public DateTime? ActualDeparture { get; init; }
    public DateTime ScheduledDeparture { get; init; }
    public double? DelayMinutes { get; init; }
    public DateTime? PrevDeparture { get; init; }
    public double? PrevDelayMinutes { get; init; }
    public int IsChainLink { get; init; }
}

public sealed class FlightOccupancyRow
{
    public int FlightId { get; init; }
    public string AirplaneCode { get; init; } = "";
    public DateTime ScheduledDeparture { get; init; }
    public int? DayOfWeek { get; init; }
    public int OccupiedBusinessSeats { get; init; }
}

public sealed class BusinessCapacityRow
{
    public string AirplaneCode { get; init; } = "";
    public int TotalBusinessSeats { get; init; }
}
