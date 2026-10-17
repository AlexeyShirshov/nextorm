using LinqToDB;
using LinqToDB.Async;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// The comparison arm: the same eleven queries as <see cref="AviasalesQueries"/> (which model the files
/// in <c>Sql/</c>), expressed with the linq2db LINQ API over <see cref="Linq2DbEntities"/>. Constructs
/// the linq2db LINQ surface does not model — PostgreSQL's two-argument <c>percentile_disc</c> aggregate
/// and the <c>jsonb -&gt;&gt;</c> operator — are expressed with linq2db's <see cref="Sql.Expr{T}"/>
/// escape hatch, so the query still goes through the linq2db translator instead of falling back to raw
/// SQL. Methods return the row list and never print: the runner and the benchmarks share them.
/// </summary>
public static class Linq2DbQueries
{
    // 1. Sql/aircraft_delay_chains.sql
    public static async Task<object> AircraftDelayChains(IDataContext db, CancellationToken ct)
    {
        var flightDelays = (from f in db.GetTable<AviasalesTimetable>()
                            where (f.Status == "Departed" || f.Status == "Arrived") && f.ActualDeparture != null
                            select new
                            {
                                flight_id = f.FlightId,
                                airplane_code = f.AirplaneCode,
                                actual_departure = f.ActualDeparture,
                                scheduled_departure = f.ScheduledDeparture,
                                delay_minutes = (double?)Sql.DateDiff(Sql.DateParts.Second, f.ScheduledDeparture, f.ActualDeparture) / 60.0,
                                prev_departure = Sql.Window.Lag(f.ActualDeparture,
                                    w => w.PartitionBy(f.AirplaneCode).OrderBy(f.ActualDeparture)),
                                prev_delay_minutes = (double?)Sql.Window.Lag(
                                    Sql.DateDiff(Sql.DateParts.Second, f.ScheduledDeparture, f.ActualDeparture),
                                    w => w.PartitionBy(f.AirplaneCode).OrderBy(f.ActualDeparture)) / 60.0
                            }).AsCte("flight_delays");

        var delayChains = (from d in flightDelays
                           select new
                           {
                               d.flight_id,
                               d.airplane_code,
                               d.actual_departure,
                               d.scheduled_departure,
                               d.delay_minutes,
                               d.prev_departure,
                               d.prev_delay_minutes,
                               is_chain_link = d.delay_minutes > 15 && d.prev_delay_minutes > 15 ? 1 : 0
                           }).AsCte("delay_chains");

        return await (from dc in delayChains
                      join ac in db.GetTable<AviasalesAirplaneData>() on dc.airplane_code equals ac.AirplaneCode
                      where dc.delay_minutes > 30 && dc.prev_delay_minutes > 30
                      orderby dc.airplane_code, dc.scheduled_departure
                      select new
                      {
                          Model = Sql.Expr<string?>($"{ac.Model} ->> 'ru'"),
                          AirplaneCode = dc.airplane_code,
                          ScheduledDeparture = dc.scheduled_departure,
                          DelayMinutes = Math.Round(dc.delay_minutes ?? 0.0, 1),
                          PrevDelayMinutes = Math.Round(dc.prev_delay_minutes ?? 0.0, 1)
                      }).ToListAsync(ct);
    }

    // 2. Sql/business_occupancy_matrix.sql
    public static async Task<object> BusinessOccupancyMatrix(IDataContext db, CancellationToken ct)
    {
        var capacity = (from s in db.GetTable<AviasalesSeat>()
                        where s.FareConditions == "Business"
                        group s by s.AirplaneCode into g
                        select new
                        {
                            airplane_code = g.Key,
                            total_business_seats = g.Count()
                        }).AsCte("flight_business_capacity");

        var occupancy = (from f in db.GetTable<AviasalesTimetable>()
                         join tf in db.GetTable<AviasalesSegment>() on f.FlightId equals tf.FlightId
                         join bp in db.GetTable<AviasalesBoardingPass>()
                             on new { tf.FlightId, tf.TicketNo } equals new { bp.FlightId, bp.TicketNo } into bps
                         from bp in bps.DefaultIfEmpty()
                         where tf.FareConditions == "Business"
                               && (f.Status == "Departed" || f.Status == "Arrived")
                         group new { f, bp } by new { f.FlightId, f.AirplaneCode, f.ScheduledDeparture } into g
                         select new
                         {
                             flight_id = g.Key.FlightId,
                             airplane_code = g.Key.AirplaneCode,
                             scheduled_departure = g.Key.ScheduledDeparture,
                             day_of_week = Sql.Expr<int>($"CAST(EXTRACT(ISODOW FROM {g.Key.ScheduledDeparture}) AS int)"),
                             occupied_business_seats = g.Count(x => x.bp.SeatNo != null)
                         }).AsCte("flight_occupancy");

        return await (from fo in occupancy
                      join cap in capacity on fo.airplane_code equals cap.airplane_code
                      join ac in db.GetTable<AviasalesAirplaneData>() on fo.airplane_code equals ac.AirplaneCode
                      group new { fo, cap, ac } by new { model = Sql.Expr<string?>($"{ac.Model} ->> 'ru'") } into g
                      orderby g.Key.model
                      select new
                      {
                          Model = g.Key.model,
                          Mon = Math.Round(g.Average(x => x.fo.day_of_week == 1
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Tue = Math.Round(g.Average(x => x.fo.day_of_week == 2
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Wed = Math.Round(g.Average(x => x.fo.day_of_week == 3
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Thu = Math.Round(g.Average(x => x.fo.day_of_week == 4
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Fri = Math.Round(g.Average(x => x.fo.day_of_week == 5
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Sat = Math.Round(g.Average(x => x.fo.day_of_week == 6
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1),
                          Sun = Math.Round(g.Average(x => x.fo.day_of_week == 7
                              ? (decimal?)x.fo.occupied_business_seats / Sql.NullIf(x.cap.total_business_seats, 0) * 100
                              : null) ?? 0m, 1)
                      }).ToListAsync(ct);
    }

    // 3. Sql/passenger_noshow_analysis.sql
    public static async Task<object> PassengerNoShowAnalysis(IDataContext db, CancellationToken ct)
    {
        var history = (from t in db.GetTable<AviasalesTicket>()
                       join tf in db.GetTable<AviasalesSegment>() on t.TicketNo equals tf.TicketNo
                       join f in db.GetTable<AviasalesFlight>() on tf.FlightId equals f.FlightId
                       join bp in db.GetTable<AviasalesBoardingPass>()
                           on new { f.FlightId, tf.TicketNo } equals new { bp.FlightId, bp.TicketNo } into bps
                       from bp in bps.DefaultIfEmpty()
                       where f.Status == "Departed" || f.Status == "Arrived"
                       select new
                       {
                           passenger_id = t.PassengerId,
                           passenger_name = t.PassengerName,
                           ticket_no = t.TicketNo,
                           flight_id = f.FlightId,
                           price = tf.Price,
                           is_noshow = bp.SeatNo == null ? 1 : 0
                       }).AsCte("passenger_flight_history");

        var stats = (from h in history
                     group h by new { h.passenger_id, h.passenger_name } into g
                     select new
                     {
                         g.Key.passenger_id,
                         g.Key.passenger_name,
                         total_booked_flights = g.Count(x => x.flight_id != 0),
                         total_noshows = g.Sum(x => x.is_noshow),
                         wasted_money = g.Sum(x => x.price)
                     }).AsCte("passenger_stats");

        return await (from s in stats
                      where s.total_booked_flights >= 3 && s.total_booked_flights == s.total_noshows
                      orderby s.wasted_money descending, s.passenger_name
                      select new
                      {
                          PassengerId = s.passenger_id,
                          PassengerName = s.passenger_name,
                          TotalBookedFlights = s.total_booked_flights,
                          WastedMoney = s.wasted_money
                      }).ToListAsync(ct);
    }

    // 4. Sql/rolling_revenue_metrics.sql
    public static async Task<object> RollingRevenueMetrics(IDataContext db, CancellationToken ct)
    {
        var daily = (from b in db.GetTable<AviasalesBooking>()
                     group b by new { sales_date = Sql.Expr<DateTime>($"date_trunc('day', {b.BookDate})") } into g
                     select new
                     {
                         g.Key.sales_date,
                         daily_amount = g.Sum(x => x.TotalAmount)
                     }).AsCte("daily_revenue");

        return await (from d in daily
                      orderby d.sales_date descending
                      select new
                      {
                          d.sales_date,
                          d.daily_amount,
                          cumulative = Sql.Window.Sum(d.daily_amount,
                              w => w.OrderBy(d.sales_date).RowsBetween.Unbounded.And.CurrentRow),
                          moving_avg_7 = Math.Round(Sql.Window.Average(d.daily_amount,
                              w => w.OrderBy(d.sales_date).RowsBetween.ValuePreceding(6).And.CurrentRow), 2)
                      }).ToListAsync(ct);
    }

    // 5. Sql/route_network_abc_xyz.sql
    public static async Task<object> RouteNetworkAbcXyz(IDataContext db, CancellationToken ct)
    {
        var monthly = (from f in db.GetTable<AviasalesTimetable>()
                       join dep in db.GetTable<AviasalesAirportData>() on f.DepartureAirport equals dep.AirportCode
                       join arr in db.GetTable<AviasalesAirportData>() on f.ArrivalAirport equals arr.AirportCode
                       join tf in db.GetTable<AviasalesSegment>() on f.FlightId equals tf.FlightId
                       join t in db.GetTable<AviasalesTicket>() on tf.TicketNo equals t.TicketNo
                       group new { f, dep, arr, tf, t } by new
                       {
                           route = Sql.Expr<string?>($"{dep.City} ->> 'ru'") + " -> " + Sql.Expr<string?>($"{arr.City} ->> 'ru'"),
                           flight_month = Sql.Expr<DateTime>($"date_trunc('month', {f.ScheduledDeparture})")
                       } into g
                       select new
                       {
                           g.Key.route,
                           g.Key.flight_month,
                           monthly_revenue = g.Sum(x => x.tf.Price),
                           passenger_count = g.Select(x => x.t.TicketNo).Distinct().Count()
                       }).AsCte("route_monthly_revenue");

        var aggregates = (from r in monthly
                          group r by new { r.route } into g
                          select new
                          {
                              g.Key.route,
                              total_revenue = g.Sum(x => x.monthly_revenue),
                              avg_passengers = (decimal?)g.Average(x => (decimal)x.passenger_count),
                              stddev_passengers = (decimal?)LinqToDB.AnalyticFunctions.StdDevSamp(g, x => (double)x.passenger_count)
                          }).AsCte("route_aggregates");

        var abc = (from a in aggregates
                   select new
                   {
                       a.route,
                       a.total_revenue,
                       a.avg_passengers,
                       a.stddev_passengers,
                       running_percent = Sql.Window.Sum(a.total_revenue, w => w.OrderByDesc(a.total_revenue))
                                         / Sql.Window.Sum(a.total_revenue, w => w.RowsBetween.Unbounded.And.Unbounded)
                   }).AsCte("abc_analys");

        return await (from a in abc
                      orderby a.total_revenue descending
                      select new
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
                      }).ToListAsync(ct);
    }

    // 6. top_routes_by_city
    public static async Task<object> TopRoutesByCity(IDataContext db, CancellationToken ct)
    {
        var routeRevenue = from f in db.GetTable<AviasalesTimetable>()
                           join tf in db.GetTable<AviasalesSegment>() on f.FlightId equals tf.FlightId
                           join dep in db.GetTable<AviasalesAirportData>() on f.DepartureAirport equals dep.AirportCode
                           join arr in db.GetTable<AviasalesAirportData>() on f.ArrivalAirport equals arr.AirportCode
                           where f.Status == "Departed" || f.Status == "Arrived"
                           group new { tf, dep, arr } by new
                           {
                               DepartureCity = Sql.Expr<string?>($"{dep.City} ->> 'ru'"),
                               Route = Sql.Expr<string?>($"{dep.City} ->> 'ru'") + " -> " + Sql.Expr<string?>($"{arr.City} ->> 'ru'")
                           } into g
                           select new
                           {
                               g.Key.DepartureCity,
                               g.Key.Route,
                               Revenue = g.Sum(x => x.tf.Price)
                           };

        var ranked = from r in routeRevenue
                     select new
                     {
                         r.DepartureCity,
                         r.Route,
                         r.Revenue,
                         Place = Sql.Window.Rank(w => w.PartitionBy(r.DepartureCity).OrderByDesc(r.Revenue))
                     };

        return await (from r in ranked
                      where r.Place <= 3
                      orderby r.DepartureCity, r.Place
                      select new
                      {
                          r.DepartureCity,
                          r.Place,
                          r.Route,
                          RevenueMln = Math.Round(r.Revenue / 1000000.0m, 2)
                      }).ToListAsync(ct);
    }

    // 7. airport_otp
    public static async Task<object> AirportOnTimePerformance(IDataContext db, CancellationToken ct)
    {
        var flights = from f in db.GetTable<AviasalesTimetable>()
                      join dep in db.GetTable<AviasalesAirportData>() on f.DepartureAirport equals dep.AirportCode
                      where f.Status == "Departed" || f.Status == "Arrived" || f.Status == "Cancelled"
                      select new
                      {
                          City = Sql.Expr<string?>($"{dep.City} ->> 'ru'"),
                          // nextorm's date_diff("minute") is truncated integer minutes (SQL Server DATEDIFF
                          // semantics), while linq2db's DateDiff for PostgreSQL renders fractional minutes;
                          // emit the truncated form so both arms compute the same delays.
                          Delay = Sql.Expr<int?>($"CAST(trunc(EXTRACT(EPOCH FROM ({f.ActualDeparture} - {f.ScheduledDeparture})) / 60) AS integer)"),
                          IsDelayed = f.ActualDeparture != null
                                      && Sql.Expr<int?>($"CAST(trunc(EXTRACT(EPOCH FROM ({f.ActualDeparture} - {f.ScheduledDeparture})) / 60) AS integer)") > 15
                              ? 1
                              : 0,
                          IsCancelled = f.Status == "Cancelled" ? 1 : 0
                      };

        var stats = from f in flights
                    group f by new { f.City } into g
                    select new
                    {
                        g.Key.City,
                        Flights = g.Count(),
                        Delayed = g.Sum(x => x.IsDelayed),
                        Cancelled = g.Sum(x => x.IsCancelled),
                        AvgDelayMinutes = g.Average(x => x.Delay)
                    };

        return await (from s in stats
                      where s.Flights >= 50
                      orderby (double)s.Delayed / s.Flights descending, s.City
                      select new
                      {
                          s.City,
                          s.Flights,
                          s.Delayed,
                          DelayedPct = Math.Round((double)s.Delayed / s.Flights * 100, 2),
                          s.Cancelled,
                          AvgDelayMinutes = Math.Round(s.AvgDelayMinutes ?? 0.0, 1)
                      }).ToListAsync(ct);
    }

    // 8. delay_percentiles_by_model
    // The percentile_disc ordered-set aggregate needs a per-row ORDER BY expression, which the liq2db
    // LINQ group projection cannot reference; the reference SQL is issued through linq2db's FromSql.
    public static async Task<object> DelayPercentilesByModel(IDataContext db, CancellationToken ct)
    {
        var rows = db.FromSql<DelayPercentileRow>($"""
            SELECT
                ac.model ->> 'ru' AS "Model",
                count(*) AS "Flights",
                percentile_disc(0.5) WITHIN GROUP (
                    ORDER BY CAST(trunc(EXTRACT(EPOCH FROM (f.actual_departure - f.scheduled_departure)) / 60) AS integer)) AS "MedianDelay",
                percentile_disc(0.9) WITHIN GROUP (
                    ORDER BY CAST(trunc(EXTRACT(EPOCH FROM (f.actual_departure - f.scheduled_departure)) / 60) AS integer)) AS "P90Delay"
            FROM bookings.timetable f
            JOIN bookings.airplanes_data ac ON f.airplane_code = ac.airplane_code
            WHERE f.status IN ('Departed', 'Arrived') AND f.actual_departure IS NOT NULL
            GROUP BY ac.model ->> 'ru'
            ORDER BY ac.model ->> 'ru'
            """);

        return await rows.ToListAsync(ct);
    }

    // 9. frequent_flyers
    public static async Task<object> FrequentFlyers(IDataContext db, CancellationToken ct)
    {
        var query = from t in db.GetTable<AviasalesTicket>()
                    join tf in db.GetTable<AviasalesSegment>() on t.TicketNo equals tf.TicketNo
                    join f in db.GetTable<AviasalesTimetable>() on tf.FlightId equals f.FlightId
                    join dep in db.GetTable<AviasalesAirportData>() on f.DepartureAirport equals dep.AirportCode
                    join arr in db.GetTable<AviasalesAirportData>() on f.ArrivalAirport equals arr.AirportCode
                    where f.Status == "Departed" || f.Status == "Arrived"
                    group new { t, dep, arr } by new { t.PassengerId, t.PassengerName } into g
                    where g.Count() >= 5
                    orderby g.Count() descending
                    select new
                    {
                        g.Key.PassengerId,
                        g.Key.PassengerName,
                        Flights = g.Count(),
                        Routes = Sql.StringAggregate(
                            g.Select(x => Sql.Expr<string?>($"{x.dep.City} ->> 'ru'") + " -> " + Sql.Expr<string?>($"{x.arr.City} ->> 'ru'")),
                            ", ").ToValue()
                    };

        return await query.Take(10).ToListAsync(ct);
    }

    // 10. passenger_growth_mom
    public static async Task<object> PassengerGrowth(IDataContext db, CancellationToken ct)
    {
        var monthly = from f in db.GetTable<AviasalesTimetable>()
                      join tf in db.GetTable<AviasalesSegment>() on f.FlightId equals tf.FlightId
                      join t in db.GetTable<AviasalesTicket>() on tf.TicketNo equals t.TicketNo
                      where f.Status == "Departed" || f.Status == "Arrived"
                      group new { f, tf, t } by new
                      {
                          Month = Sql.Expr<DateTime>($"date_trunc('month', {f.ScheduledDeparture})")
                      } into g
                      select new
                      {
                          g.Key.Month,
                          Passengers = g.Select(x => x.t.PassengerId).Distinct().Count(),
                          Revenue = g.Sum(x => x.tf.Price)
                      };

        var withPrevious = from m in monthly
                           select new
                           {
                               m.Month,
                               m.Passengers,
                               m.Revenue,
                               PrevPassengers = Sql.Window.Lag(m.Passengers, w => w.OrderBy(m.Month))
                           };

        return await (from m in withPrevious
                      orderby m.Month
                      select new
                      {
                          Month = Sql.Expr<string?>($"to_char({m.Month}, 'YYYY-MM')"),
                          m.Passengers,
                          RevenueMln = Math.Round(m.Revenue / 1000000.0m, 2),
                          GrowthPct = m.PrevPassengers <= 0
                              ? (double?)null
                              : Math.Round((double)(m.Passengers - m.PrevPassengers) / m.PrevPassengers * 100, 1)
                      }).ToListAsync(ct);
    }

    // 11. cancellation_by_route
    public static async Task<object> CancellationByRoute(IDataContext db, CancellationToken ct)
    {
        var perRoute = from f in db.GetTable<AviasalesTimetable>()
                       join dep in db.GetTable<AviasalesAirportData>() on f.DepartureAirport equals dep.AirportCode
                       join arr in db.GetTable<AviasalesAirportData>() on f.ArrivalAirport equals arr.AirportCode
                       group f by new
                       {
                           Route = Sql.Expr<string?>($"{dep.City} ->> 'ru'") + " -> " + Sql.Expr<string?>($"{arr.City} ->> 'ru'")
                       } into g
                       select new
                       {
                           g.Key.Route,
                           Flights = g.Count(),
                           Cancelled = g.Count(x => x.Status == "Cancelled")
                       };

        return await (from r in perRoute
                      where r.Flights >= 30 && r.Cancelled > 0
                      orderby (double)r.Cancelled / r.Flights descending
                      select new
                      {
                          r.Route,
                          r.Flights,
                          r.Cancelled,
                          CancelRatePct = Math.Round((double)r.Cancelled / r.Flights * 100, 2)
                      }).Take(15).ToListAsync(ct);
    }
}

public sealed class DelayPercentileRow
{
    [LinqToDB.Mapping.Column("Model")] public string? Model { get; set; }
    [LinqToDB.Mapping.Column("Flights")] public int Flights { get; set; }
    [LinqToDB.Mapping.Column("MedianDelay")] public decimal? MedianDelay { get; set; }
    [LinqToDB.Mapping.Column("P90Delay")] public decimal? P90Delay { get; set; }
}
