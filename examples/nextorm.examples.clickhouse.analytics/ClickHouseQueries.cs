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
    // WORKING: splitByChar → split_by_char; arrayMap(x -> lower(x)) → array_map(x => x.ToLower(), ...);
    // arrayFilter(x -> length(x) > 3, ...) → array_filter(x => x.ToLower().Length > 3, ...). The
    // length() of a higher-order lambda parameter cannot be written as x.Length (member access on the
    // parameter is rejected by the translator), so it is reached through the lower() call — a no-op on
    // the already-lowercased element. Grouping by the resulting array is supported by ClickHouse.
    public static async Task ArrayAnalytics(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Where(h => h.SearchPhrase != "" && h.EventDate == new DateTime(2014, 3, 20))
            .GroupBy(h => new
            {
                CleanWords = SqlFunctions.ClickHouse.array_filter(
                    x => x.ToLower().Length > 3,
                    SqlFunctions.ClickHouse.array_map(
                        x => x.ToLower(),
                        SqlFunctions.ClickHouse.split_by_char(" ", h.SearchPhrase)))
            })
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .Select(h => new
            {
                CleanWords = SqlFunctions.ClickHouse.array_filter(
                    x => x.ToLower().Length > 3,
                    SqlFunctions.ClickHouse.array_map(
                        x => x.ToLower(),
                        SqlFunctions.ClickHouse.split_by_char(" ", h.SearchPhrase))),
                Occurrence = SqlFunctions.Sql.count()
            })
            .ToListAsync(ct);

        Print("1. array_analytics", rows);
    }

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
    // NOT WORKING: uniqMerge over an AggregateFunction(uniq, UInt64) state has no LINQ surface in the
    // released nextorm packages (there is no uniqMerge/`-Merge` combinator), so the state column cannot
    // be projected as a scalar. Not worked around with raw SQL on purpose; tracked in
    // docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md.
    public static Task Incremental(IDataContext ctx, CancellationToken ct) =>
        throw new NotSupportedException(
            "uniqMerge over an AggregateFunction state has no LINQ surface (see docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md).");

    // 4. Sql/clickhouse_retention.sql
    // WORKING: the WITH first_visits / cohort_sizes CTEs are declared with ctx.With and read back by
    // name; the inner t subquery joins hits with first_visits; groupArray((week_number, pct)) →
    // group_array(Tuple.Create(...)), which materialises as a CLR Tuple<...>[]. The matrix is formatted
    // for display in C# (there is no portable toString()).
    public static async Task Retention(IDataContext ctx, CancellationToken ct)
    {
        // CTE first_visits: each user's first (Monday-aligned) visit. UserID is projected as Int64 so the
        // later join with hits_v1.UserID (UInt64) has a common key type on both sides.
        var firstVisits = ctx.From<IHit>()
            .GroupBy(h => new { h.UserId })
            .Select(h => new
            {
                UserID = (long)h.UserId,
                cohort_week = SqlFunctions.ClickHouse.to_monday(SqlFunctions.Sql.min(h.EventDate))
            });

        // CTE cohort_sizes: how many users start in each cohort week.
        var cohortSizes = ctx.From("first_visits")
            .GroupBy(f => new { cohort_week = f.GetDateTime("cohort_week") })
            .Select(f => new
            {
                cohort_week = f.GetDateTime("cohort_week"),
                cohort_size = SqlFunctions.Sql.count()
            });

        // CTE weekly: each cohort's weekly distinct users (the inner `t` subquery of the reference).
        var weekly = ctx.From<IHit>()
            .Join(ctx.From("first_visits"), (h, fv) => (long)h.UserId == fv.GetInt64("UserID"))
            .GroupBy(p => new
            {
                cohort_week = p.Item2.GetDateTime("cohort_week"),
                week_number = (byte)((SqlFunctions.Sql.date_diff("day", p.Item2.GetDateTime("cohort_week"),
                    SqlFunctions.ClickHouse.to_monday(p.Item1.EventDate)) ?? 0) / 7)
            })
            .Select(p => new
            {
                cohort_week = p.Item2.GetDateTime("cohort_week"),
                week_number = (byte)((SqlFunctions.Sql.date_diff("day", p.Item2.GetDateTime("cohort_week"),
                    SqlFunctions.ClickHouse.to_monday(p.Item1.EventDate)) ?? 0) / 7),
                distinct_users = SqlFunctions.Sql.count_distinct(p.Item1.UserId)
            });

        var scope = ctx.With("first_visits", firstVisits)
            .With("cohort_sizes", cohortSizes)
            .With("weekly", weekly);

        var rows = await scope.From("weekly")
            .Join(scope.From("cohort_sizes"), (w, cs) => w.GetDateTime("cohort_week") == cs.GetDateTime("cohort_week"))
            .Where(p => p.Item1.GetInt32("week_number") <= 4)
            .GroupBy(p => new { cohort_week = p.Item1.GetDateTime("cohort_week") })
            .OrderByDescending(p => p.Item1.GetDateTime("cohort_week"))
            .Select(p => new
            {
                CohortWeek = p.Item1.GetDateTime("cohort_week"),
                CohortSize = SqlFunctions.Sql.max(p.Item2.GetInt32("cohort_size")),
                RetentionMatrix = SqlFunctions.ClickHouse.group_array(Tuple.Create(
                    p.Item1.GetByte("week_number"),
                    Math.Round((double)p.Item1.GetInt32("distinct_users") / p.Item2.GetInt32("cohort_size") * 100, 2)))
            })
            .ToListAsync(ct);

        var display = rows.Select(r => new
        {
            r.CohortWeek,
            r.CohortSize,
            RetentionMatrix = "[" + string.Join(", ",
                r.RetentionMatrix.Select(t => $"(week {t.Item1}: {t.Item2:0.##}%)")) + "]"
        });

        Print("4. retention", display);
    }

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
    // WORKING: if(IsMobile = 1, …) → ternary; countIf → Sql.count(() => c); uniq → uniq; ROUND → Math.Round.
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
                    (double)SqlFunctions.Sql.count(() => h.SearchPhrase != "") / SqlFunctions.Sql.count(), 4),
                BounceShare = Math.Round(
                    (double)SqlFunctions.Sql.count(() => h.IsNotBounce == 0) / SqlFunctions.Sql.count(), 4)
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
