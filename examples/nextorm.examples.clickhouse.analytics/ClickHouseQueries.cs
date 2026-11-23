using NextORM.Core;
using NextORM.Examples.ClickHouse.Analytics.Generated;
using NextORM.Extensions;
using NextORM.Generated.nextorm_examples_clickhouse_analytics;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// The five demo queries from <c>Sql/</c> plus six extra course-style exercises (see
/// <c>README.md</c>), expressed with the nextorm LINQ API. Each method's comment names the SQL file it
/// models, states whether it currently works, and — when it does not — why (with the roadmap document
/// that tracks the gap). Queries with no LINQ surface are <b>not</b> worked around: they throw
/// <see cref="NotSupportedException"/> instead. Each method returns its row list; the runner prints it.
/// </summary>
public static class ClickHouseQueries
{
    // 1. Sql/clickhouse_array_analytics.sql
    // WORKING: splitByChar → split_by_char; arrayMap(x -> lower(x)) → array_map(x => x.ToLower(), ...);
    // arrayFilter(x -> length(x) > 3, ...) → array_filter(x => x.ToLower().Length > 3, ...). The
    // length() of a higher-order lambda parameter cannot be written as x.Length (member access on the
    // parameter is rejected by the translator), so it is reached through the lower() call — a no-op on
    // the already-lowercased element. Grouping by the resulting array is supported by ClickHouse.
    public static async Task<object> ArrayAnalytics(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.HitsV1.Query()
            .Where(h => h.SearchPhrase != "" && h.EventDate == new DateTime(2014, 3, 20))
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .SelectGroup(h => new
            {
                CleanWords = SqlFunctions.ClickHouse.array_filter(
                    x => x.ToLower().Length > 3,
                    SqlFunctions.ClickHouse.array_map(
                        x => x.ToLower(),
                        SqlFunctions.ClickHouse.split_by_char(" ", h.SearchPhrase))),
                Occurrence = SqlFunctions.Sql.count()
            })
            .ToListAsync(ct);

        return rows;
    }

    // 2. Sql/clickhouse_funnel.sql
    // WORKING: windowFunnel(1800)(...) → SqlFunctions.ClickHouse.window_funnel(...); the inner
    // GROUP BY UserID and the outer GROUP BY level map to two derived queries.
    public static async Task<object> Funnel(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var perUser = ctx.HitsV1.Query()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20))
            .SelectGroup(h => new
            {
                UserId = h.UserID,
                Level = SqlFunctions.ClickHouse.window_funnel(1800, h.EventTime,
                    h.URL.Contains("/product/"), h.URL.Contains("/cart"), h.URL.Contains("/checkout/success"))
            });

        var rows = await ctx.DataContext.From(perUser)
            .OrderBy(p => p.Level)
            .SelectGroup(p => new { p.Level, ConversionCount = SqlFunctions.Sql.count() })
            .ToListAsync(ct);

        return rows;
    }

    // 4. Sql/clickhouse_retention.sql
    // WORKING: the first_visits / cohort_sizes / weekly CTEs are declared as typed descriptors (`AsCte`)
    // and read back with member access; the inner t subquery joins hits with first_visits. cohort_sizes and
    // weekly are projected into named row types and the outer chain names both joins
    // (`WithAlias(Alias.Week)` + `Alias.Cohort`) — the alias surface refuses an anonymous chain type
    // (#229). groupArray((week_number, pct)) → group_array(Tuple.Create(...)), which materialises as a CLR
    // Tuple<...>[]. The matrix is formatted for display in C# (there is no portable toString()).
    public static async Task<object> Retention(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        // CTE first_visits: each user's first (Monday-aligned) visit. UserID is projected as Int64 so the
        // later join with hits_v1.UserID (UInt64) has a common key type on both sides.
        var firstVisits = ctx.HitsV1.Query()
            // Explicit GroupBy, not SelectGroup: the projection casts UserID, so key inference would
            // group by the cast expression instead of the column the reference SQL groups by.
            .GroupBy(h => new { h.UserID })
            .Select(h => new
            {
                UserID = (long)h.UserID,
                cohort_week = SqlFunctions.ClickHouse.to_monday(SqlFunctions.Sql.min(h.EventDate))
            })
            .AsCte("first_visits");

        // CTE cohort_sizes: how many users start in each cohort week.
        var cohortSizes = ctx.DataContext.From(firstVisits)
            .SelectGroup(f => new CohortSizeRow
            {
                CohortWeek = f.cohort_week,
                CohortSize = SqlFunctions.Sql.count()
            })
            .AsCte("cohort_sizes");

        // CTE weekly: each cohort's weekly distinct users (the inner `t` subquery of the reference).
        // GroupBy + Select rather than SelectGroup: this descriptor roots the aliased outer chain below,
        // and the shipped 1.0.9-rc4 alias generator recovers a consumed chain's element type only through
        // a `Select`, so a `SelectGroup` producer would drop `Alias.Week`/`Alias.Cohort` at the consumer.
        var weekly = ctx.HitsV1.Query().WithAlias(Alias.Hit)
            .Join(firstVisits, (h, fv) => (long)h.Hit.UserID == fv.UserID, Alias.FirstVisit)
            .GroupBy(p => new
            {
                cohort_week = p.FirstVisit.cohort_week,
                week_number = (byte)((SqlFunctions.Sql.date_diff("day", p.FirstVisit.cohort_week,
                    SqlFunctions.ClickHouse.to_monday(p.Hit.EventDate)) ?? 0) / 7)
            })
            .Select(p => new RetentionWeekRow
            {
                CohortWeek = p.FirstVisit.cohort_week,
                WeekNumber = (byte)((SqlFunctions.Sql.date_diff("day", p.FirstVisit.cohort_week,
                    SqlFunctions.ClickHouse.to_monday(p.Hit.EventDate)) ?? 0) / 7),
                DistinctUsers = SqlFunctions.Sql.count_distinct(p.Hit.UserID)
            })
            .AsCte("weekly");

        var rows = await ctx.DataContext.From(weekly).WithAlias(Alias.Week)
            .Join(cohortSizes, (w, cs) => w.Week.CohortWeek == cs.CohortWeek, Alias.Cohort)
            .Where(p => p.Week.WeekNumber <= 4)
            .OrderByDescending(p => p.Week.CohortWeek)
            .SelectGroup(p => new
            {
                CohortWeek = p.Week.CohortWeek,
                CohortSize = SqlFunctions.Sql.max(p.Cohort.CohortSize),
                RetentionMatrix = SqlFunctions.ClickHouse.group_array(Tuple.Create(
                    p.Week.WeekNumber,
                    Math.Round((double)p.Week.DistinctUsers / p.Cohort.CohortSize * 100, 2)))
            })
            .ToListAsync(ct);

        var display = rows.Select(r => new
        {
            r.CohortWeek,
            r.CohortSize,
            RetentionMatrix = "[" + string.Join(", ",
                r.RetentionMatrix.Select(t => $"(week {t.Item1}: {t.Item2:0.##}%)")) + "]"
        });

        return display.ToList();
    }

    // 5. Sql/clickhouse_sessions.sql
    // WORKING: three chained typed CTEs (`AsCte`: sessions, session_flags, session_counts);
    // lagInFrame → lag_in_frame(...).Over(...); runningAccumulate(if(…)) → framed sum_over(c ? 1 : 0)
    // (runningAccumulate still has no LINQ surface); the quantile threshold is computed once and
    // passed as a parameter.
    public static async Task<object> Sessions(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        // CTE sessions.
        var sessions = ctx.HitsV1.Query()
            .Select(h => new
            {
                // hits_v1.UserID is UInt64; cast it to a signed 64-bit value so the CTE column is
                // materialisable by the row reader (ClickHouse UInt64 has no CLR reader getter).
                UserID = (long)h.UserID,
                EventTime = h.EventTime,
                time_diff = SqlFunctions.Sql.date_diff("second",
                    SqlFunctions.ClickHouse.lag_in_frame(h.EventTime).Over(partitionBy: () => h.UserID, orderBy: () => h.EventTime),
                    h.EventTime)
            })
            .AsCte("sessions");

        // CTE session_flags.
        var flags = ctx.DataContext.From(sessions)
            .Select(s => new
            {
                s.UserID,
                s.EventTime,
                // runningAccumulate(if(...)): the if() result is an unsigned ClickHouse integer, so
                // sum() returns UInt64; cast the frame to a signed value before it leaves the CTE.
                session_id = (long)SqlFunctions.Sql
                    .sum_over(s.time_diff == null || s.time_diff > 1800 ? 1 : 0)
                    .Over(
                        partitionBy: () => s.UserID,
                        orderBy: () => s.EventTime,
                        frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            })
            .AsCte("session_flags");

        // CTE session_counts.
        var counts = ctx.DataContext.From(flags)
            .SelectGroup(f => new
            {
                f.UserID,
                f.session_id,
                hits_in_session = SqlFunctions.Sql.count()
            })
            .AsCte("session_counts");

        var threshold = await ctx.DataContext.From(counts)
            .Select(c => SqlFunctions.ClickHouse.quantile(0.99, c.hits_in_session))
            .FirstAsync(ct);

        var rows = await ctx.DataContext.From(counts)
            .Where(c => c.hits_in_session > threshold)
            .OrderByDescending(c => c.hits_in_session)
            .Limit(100)
            .Select(c => new
            {
                c.UserID,
                SessionId = c.session_id,
                Hits = c.hits_in_session
            })
            .ToListAsync(ct);

        return rows;
    }

    // 6. daily_traffic
    // WORKING: GROUP BY EventDate with count() + uniqExact.
    public static async Task<object> DailyTraffic(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.HitsV1.Query()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .OrderBy(h => h.EventDate)
            .SelectGroup(h => new
            {
                h.EventDate,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq_exact(h.UserID)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 7. top_landing_pages
    // WORKING: URL grouping with count() + uniq, top-N by hits.
    public static async Task<object> TopLandingPages(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.HitsV1.Query()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20) && h.URL != "")
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .SelectGroup(h => new
            {
                Url = h.URL,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserID)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 8. device_split
    // WORKING: if(IsMobile = 1, …) → ternary; countIf → Sql.count(() => c); uniq → uniq; ROUND → Math.Round.
    public static async Task<object> DeviceSplit(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.HitsV1.Query()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .SelectGroup(h => new
            {
                Device = h.IsMobile == 1 ? "mobile" : "desktop",
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserID),
                SearchShare = Math.Round(
                    (double)SqlFunctions.Sql.count(() => h.SearchPhrase != "") / SqlFunctions.Sql.count(), 4),
                BounceShare = Math.Round(
                    (double)SqlFunctions.Sql.count(() => h.IsNotBounce == 0) / SqlFunctions.Sql.count(), 4)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 9. top_referrers
    // WORKING: RefererDomain grouping with count() + uniq, top-N by hits.
    public static async Task<object> TopReferrers(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var rows = await ctx.HitsV1.Query()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20) && h.RefererDomain != "")
            .OrderByDescending(h => SqlFunctions.Sql.count())
            .Limit(10)
            .SelectGroup(h => new
            {
                h.RefererDomain,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq(h.UserID)
            })
            .ToListAsync(ct);

        return rows;
    }

    // 10. session_depth
    // WORKING: lagInFrame → lag_in_frame; running sum → framed sum_over; multiIf → multi_if.
    public static async Task<object> SessionDepth(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var hits = ctx.HitsV1.Query()
            .Where(h => h.EventDate == new DateTime(2014, 3, 20))
            .Select(h => new
            {
                UserId = h.UserID,
                h.EventTime,
                PrevEvent = SqlFunctions.ClickHouse.lag_in_frame((DateTime?)h.EventTime)
                    .Over(partitionBy: () => h.UserID, orderBy: () => h.EventTime)
            });

        var flags = ctx.DataContext.From(hits)
            .Select(h => new
            {
                h.UserId,
                h.EventTime,
                SessionId = SqlFunctions.Sql
                    .sum_over(h.PrevEvent == null || SqlFunctions.Sql.date_diff("second", h.PrevEvent, h.EventTime) > 1800 ? 1 : 0)
                    .Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime, frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            });

        var sessions = ctx.DataContext.From(flags)
            .SelectGroup(f => new { f.UserId, f.SessionId, Hits = SqlFunctions.Sql.count() });

        var rows = await ctx.DataContext.From(sessions)
            .OrderBy(s => SqlFunctions.ClickHouse.multi_if(
                        SqlFunctions.ClickHouse.when(s.Hits == 1, "01: single"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 3, "02: 2-3"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 10, "03: 4-10"),
                        SqlFunctions.ClickHouse.when(s.Hits <= 30, "04: 11-30"),
                        SqlFunctions.ClickHouse.otherwise("05: 30+")))
            .SelectGroup(s => new
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

        return rows;
    }

    // 11. rolling_activity
    // WORKING: window over an aggregate with 7-day and cumulative frames.
    public static async Task<object> RollingActivity(AnalyticsRepositoryContext ctx, CancellationToken ct)
    {
        var daily = ctx.HitsV1.Query()
            .Where(h => h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23))
            .SelectGroup(h => new
            {
                h.EventDate,
                Hits = SqlFunctions.Sql.count(),
                Users = SqlFunctions.ClickHouse.uniq_exact(h.UserID)
            });

        var rows = await ctx.DataContext.From(daily)
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

        return rows;
    }
}

// Named projections for the aliased Retention chain. The named join-alias surface (#160/#229) generates
// an extension whose receiver is the chain's concrete type, which cannot be rendered for an anonymous
// type (NORMGEN009), so both aliased CTEs are projected into named row types.
public sealed class RetentionWeekRow
{
    public DateTime? CohortWeek { get; init; }
    public byte WeekNumber { get; init; }
    public int DistinctUsers { get; init; }
}

public sealed class CohortSizeRow
{
    public DateTime? CohortWeek { get; init; }
    public int CohortSize { get; init; }
}
