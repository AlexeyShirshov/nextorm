using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models, states whether it currently works, and — when it does not — why (with the roadmap document
/// that tracks the gap). Queries with no LINQ surface are <b>not</b> worked around: they throw
/// <see cref="NotSupportedException"/> instead. Each working method prints the number of rows and the
/// first few rows as JSON.
/// </summary>
public static class ClickHouseQueries
{
    // 1. Sql/clickhouse_array_analytics.sql
    // NOT WORKING: arrayMap/arrayFilter over array columns (higher-order lambdas) and grouping by the
    // resulting array have no LINQ surface. Not worked around with raw SQL on purpose; tracked in
    // docs/specs/roadmap/todo_clickhouse_arrays.md.
    public static Task ArrayAnalytics(IDataContext ctx, CancellationToken ct) =>
        throw new NotSupportedException(
            "arrayMap/arrayFilter over arrays have no LINQ surface (see docs/specs/roadmap/todo_clickhouse_arrays.md).");

    // 2. Sql/clickhouse_funnel.sql
    // WORKING: windowFunnel(1800)(...) → SqlFunctions.ClickHouse.window_funnel(...); the inner
    // GROUP BY UserID and the outer GROUP BY level map to two derived queries.
    public static async Task Funnel(IDataContext ctx, CancellationToken ct)
    {
        var perUser = ctx.From<IHit>()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20))
            .GroupBy(h => new { h.UserId })
            .Select(h => new
            {
                h.UserId,
                Level = SqlFunctions.ClickHouse.window_funnel(1800, h.EventTime,
                    h.Url.Contains("/product/"), h.Url.Contains("/cart"), h.Url.Contains("/checkout/success"))
            });

        var rows = await ctx.From(perUser)
            .GroupBy(p => new { p.Level })
            .OrderBy(p => p.Level)
            .Select(p => new { p.Level, ConversionCount = SqlFunctions.Sql.count() })
            .ToListAsync(ct);

        Print("2. funnel", rows);
    }

    // 3. Sql/clickhouse_incremental.sql
    // NOT WORKING: uniqMerge over an AggregateFunction(uniq, ...) state has no LINQ surface. Not worked
    // around with raw SQL on purpose; tracked in docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md.
    public static Task Incremental(IDataContext ctx, CancellationToken ct) =>
        throw new NotSupportedException(
            "uniqMerge over an AggregateFunction state has no LINQ surface (see docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md).");

    // 4. Sql/clickhouse_retention.sql
    // NOT WORKING: the reference is `WITH first_visits AS (...), cohort_sizes AS (...) SELECT ...
    // groupArray(...) ...`. The CTEs are expressible, but `groupArray((tuple))` builds an array/tuple
    // result that has no LINQ surface, so the query cannot be modelled as CTEs + LINQ. Not worked
    // around with raw SQL on purpose; tracked in docs/specs/roadmap/todo_clickhouse_arrays.md.
    public static Task Retention(IDataContext ctx, CancellationToken ct) =>
        throw new NotSupportedException(
            "groupArray/tuple results have no LINQ surface (see docs/specs/roadmap/todo_clickhouse_arrays.md).");

    // 5. Sql/clickhouse_sessions.sql
    // WORKING: three chained CTEs (sessions, session_flags, session_counts) expressed as CTEs;
    // lagInFrame → lag_in_frame(...).Over(...); runningAccumulate(if(…)) → framed sum_over(c ? 1 : 0)
    // (runningAccumulate still has no LINQ surface); the quantile threshold is computed once and
    // passed as a parameter.
    public static async Task Sessions(IDataContext ctx, CancellationToken ct)
    {
        // CTE sessions.
        var sessions = ctx.From<IHit>()
            .Select(h => new
            {
                // hits_v1.UserID is UInt64; cast it to a signed 64-bit value so the CTE column is
                // materialisable by the row reader (ClickHouse UInt64 has no CLR reader getter).
                UserID = (long)h.UserId,
                EventTime = h.EventTime,
                time_diff = SqlFunctions.Sql.date_diff("second",
                    SqlFunctions.ClickHouse.lag_in_frame(h.EventTime).Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime),
                    h.EventTime)
            });

        // CTE session_flags.
        var flags = ctx.From("sessions")
            .Select(s => new
            {
                UserID = s.GetInt64("UserID"),
                EventTime = s.GetDateTime("EventTime"),
                // runningAccumulate(if(...)): the if() result is an unsigned ClickHouse integer, so
                // sum() returns UInt64; cast the frame to a signed value before it leaves the CTE.
                session_id = (long)SqlFunctions.Sql
                    .sum_over(s.GetNullableInt64("time_diff") == null || s.GetNullableInt64("time_diff") > 1800 ? 1 : 0)
                    .Over(
                        partitionBy: () => s.GetInt64("UserID"),
                        orderBy: () => s.GetDateTime("EventTime"),
                        frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            });

        // CTE session_counts.
        var counts = ctx.From("session_flags")
            .GroupBy(f => new { UserID = f.GetInt64("UserID"), session_id = f.GetInt64("session_id") })
            .Select(f => new
            {
                UserID = f.GetInt64("UserID"),
                session_id = f.GetInt64("session_id"),
                hits_in_session = SqlFunctions.Sql.count()
            });

        var scope = ctx.With("sessions", sessions).With("session_flags", flags).With("session_counts", counts);

        var threshold = await scope.From("session_counts")
            .Select(c => SqlFunctions.ClickHouse.quantile(0.99, c.GetInt32("hits_in_session")))
            .FirstAsync(ct);

        var rows = await scope.From("session_counts")
            .Where(c => c.GetInt32("hits_in_session") > threshold)
            .OrderByDescending(c => c.GetInt32("hits_in_session"))
            .Limit(100)
            .Select(c => new
            {
                UserID = c.GetInt64("UserID"),
                SessionId = c.GetInt64("session_id"),
                Hits = c.GetInt32("hits_in_session")
            })
            .ToListAsync(ct);

        Print("5. sessions", rows);
    }

    // 6. daily_traffic
    // WORKING: GROUP BY EventDate with count() + uniqExact.
    public static async Task DailyTraffic(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .GroupBy(h => new { h.EventDate })
            .OrderBy(h => h.EventDate)
            .Select(h => new
            {
                h.EventDate,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq_exact(h.UserId)
            })
            .ToListAsync(ct);

        Print("6. daily_traffic", rows);
    }

    // 7. top_landing_pages
    // WORKING: URL grouping with count() + uniq, top-N by hits.
    public static async Task TopLandingPages(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20) && h.Url != "")
            .GroupBy(h => new { h.Url })
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .Select(h => new
            {
                h.Url,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserId)
            })
            .ToListAsync(ct);

        Print("7. top_landing_pages", rows);
    }

    // 8. device_split
    // WORKING: if(IsMobile = 1, …) → ternary; countIf → count_if; uniq → uniq; ROUND → Math.Round.
    public static async Task DeviceSplit(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .GroupBy(h => new { Device = h.IsMobile == 1 ? "mobile" : "desktop" })
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Select(h => new
            {
                Device = h.IsMobile == 1 ? "mobile" : "desktop",
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserId),
                SearchShare = Math.Round(
                    (double)SqlFunctions.ClickHouse.count_if(() => h.SearchPhrase != "") / SqlFunctions.Sql.count(), 4),
                BounceShare = Math.Round(
                    (double)SqlFunctions.ClickHouse.count_if(() => h.IsNotBounce == 0) / SqlFunctions.Sql.count(), 4)
            })
            .ToListAsync(ct);

        Print("8. device_split", rows);
    }

    // 9. top_referrers
    // WORKING: RefererDomain grouping with count() + uniq, top-N by hits.
    public static async Task TopReferrers(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20) && h.RefererDomain != "")
            .GroupBy(h => new { h.RefererDomain })
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .Select(h => new
            {
                h.RefererDomain,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserId)
            })
            .ToListAsync(ct);

        Print("9. top_referrers", rows);
    }

    // 10. session_depth
    // WORKING: lagInFrame → lag_in_frame; running sum → framed sum_over; multiIf → multi_if.
    public static async Task SessionDepth(IDataContext ctx, CancellationToken ct)
    {
        var hits = ctx.From<IHit>()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20))
            .Select(h => new
            {
                h.UserId,
                h.EventTime,
                PrevEvent = SqlFunctions.ClickHouse.lag_in_frame((DateTime?)h.EventTime)
                    .Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime)
            });

        var flags = ctx.From(hits)
            .Select(h => new
            {
                h.UserId,
                h.EventTime,
                SessionId = SqlFunctions.Sql
                    .sum_over(h.PrevEvent == null || SqlFunctions.Sql.date_diff("second", h.PrevEvent, h.EventTime) > 1800 ? 1 : 0)
                    .Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime, frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            });

        var sessions = ctx.From(flags)
            .GroupBy(f => new { f.UserId, f.SessionId })
            .Select(f => new { f.UserId, f.SessionId, Hits = SqlFunctions.Sql.count() });

        var rows = await ctx.From(sessions)
            .GroupBy(s => new
            {
                Bucket = SqlFunctions.ClickHouse.multi_if(
                        SqlFunctions.ClickHouse.when(s.Hits == 1, "01: single"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 3, "02: 2-3"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 10, "03: 4-10"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 30, "04: 11-30"),
                        SqlFunctions.ClickHouse.otherwise("05: 30+"))
            })
            .OrderBy(s => SqlFunctions.ClickHouse.multi_if(
                        SqlFunctions.ClickHouse.when(s.Hits == 1, "01: single"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 3, "02: 2-3"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 10, "03: 4-10"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 30, "04: 11-30"),
                        SqlFunctions.ClickHouse.otherwise("05: 30+")))
            .Select(s => new
            {
                Bucket = SqlFunctions.ClickHouse.multi_if(
                        SqlFunctions.ClickHouse.when(s.Hits == 1, "01: single"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 3, "02: 2-3"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 10, "03: 4-10"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 30, "04: 11-30"),
                        SqlFunctions.ClickHouse.otherwise("05: 30+")),
                Sessions = SqlFunctions.Sql.count()
            })
            .ToListAsync(ct);

        Print("10. session_depth", rows);
    }

    // 11. rolling_activity
    // WORKING: window over an aggregate with 7-day and cumulative frames.
    public static async Task RollingActivity(IDataContext ctx, CancellationToken ct)
    {
        var daily = ctx.From<IHit>()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .GroupBy(h => new { h.EventDate })
            .Select(h => new
            {
                h.EventDate,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq_exact(h.UserId)
            });

        var rows = await ctx.From(daily)
            .OrderBy(d => d.EventDate)
            .Select(d => new
            {
                d.EventDate,
                d.Hits,
                d.Users,
                // sum() widens the Int32 count to Int64 in ClickHouse; cast so the CLR type matches.
                Rolling7DayHits = (long)SqlFunctions.Sql.sum_over(d.Hits).Over(
                    SqlFunctions.Sql.asc(() => d.EventDate),
                    WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow)),
                CumulativeHits = (long)SqlFunctions.Sql.sum_over(d.Hits).Over(
                    SqlFunctions.Sql.asc(() => d.EventDate),
                    WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            })
            .ToListAsync(ct);

        Print("11. rolling_activity", rows);
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
