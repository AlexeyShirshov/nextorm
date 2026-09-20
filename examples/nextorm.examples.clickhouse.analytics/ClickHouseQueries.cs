using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using NextORM.Core;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// The five demo queries from <c>docs/specs/clickhouse-demodb</c> plus six extra course-style
/// exercises (see <c>examples/README.md</c>). The ones with no portable LINQ API (higher-order array
/// functions, <c>windowFunnel</c>, <c>uniqMerge</c>, <c>groupArray</c>, ClickHouse-only date/aggregate
/// syntax) run through <see cref="EntityExtensions.WithSql"/>; the rest are expressed with LINQ.
/// </summary>
public static class ClickHouseQueries
{
    // 1. clickhouse_array_analytics.sql
    private const string ArrayAnalyticsSql =
        """
        select arrayFilter(x -> length(x) > 3,
                           arrayMap(x -> lower(x), splitByChar(' ', SearchPhrase))) as clean_words,
               count() as occurrence
        from datasets.hits_v1
        where SearchPhrase != '' and EventDate = '2014-03-20'
        group by clean_words
        order by occurrence desc
        limit 10
        """;

    public sealed class WordFrequencyRow
    {
        [Column("clean_words")] public string[] CleanWords { get; set; } = [];
        [Column("occurrence")] public ulong Occurrence { get; set; }
    }

    public static async Task ArrayAnalytics(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new WordFrequencyRow { CleanWords = Array.Empty<string>(), Occurrence = 0 })
            .WithSql(ArrayAnalyticsSql)
            .ToListAsync(ct);

        Print("1. array_analytics", rows);
    }

    // 2. clickhouse_funnel.sql
    private const string FunnelSql =
        """
        select level, count() as conversion_count
        from (
            select UserID,
                   windowFunnel(1800)(EventTime,
                       URL LIKE '%/product/%',
                       URL LIKE '%/cart%',
                       URL LIKE '%/checkout/success%') as level
            from datasets.hits_v1
            where EventDate = '2014-03-20'
            group by UserID
        )
        group by level
        order by level asc
        """;

    public sealed class FunnelRow
    {
        [Column("level")] public byte Level { get; set; }
        [Column("conversion_count")] public ulong ConversionCount { get; set; }
    }

    public static async Task Funnel(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new FunnelRow { Level = 0, ConversionCount = 0 })
            .WithSql(FunnelSql)
            .ToListAsync(ct);

        Print("2. funnel", rows);
    }

    // 3. clickhouse_incremental.sql
    private const string IncrementalSql =
        """
        select EventDate as "Дата",
               uniqMerge(users_state) as "Точное кол-во уникальных посетителей"
        from datasets.daily_unique_users_mv
        where EventDate >= '2014-03-01' and EventDate <= '2014-03-31'
        group by EventDate
        order by EventDate desc
        """;

    public sealed class DailyUniqueRow
    {
        [Column("Дата")] public DateTime EventDate { get; set; }
        [Column("Точное кол-во уникальных посетителей")] public ulong UniqueUsers { get; set; }
    }

    public static async Task Incremental(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IDailyUniqueUsers>()
            .Select(d => new DailyUniqueRow { EventDate = d.EventDate, UniqueUsers = 0 })
            .WithSql(IncrementalSql)
            .ToListAsync(ct);

        Print("3. incremental", rows);
    }

    // 4. clickhouse_retention.sql
    // The source retention query references UserID from the outer scope (not exposed by the inner
    // subquery) and divides by the cohort-week rather than the cohort; the corrected form below joins
    // the cohort size and keeps the array-to-string projection used by the verification plan.
    private const string RetentionSql =
        """
        with first_visits as (
            select UserID, toMonday(min(EventDate)) as cohort_week
            from datasets.hits_v1
            group by UserID
        ),
        cohort_sizes as (
            select cohort_week, count() as cohort_size
            from first_visits
            group by cohort_week
        )
        select t.cohort_week as "Неделя когорты",
               toInt64(any(cs.cohort_size)) as "Размер когорты",
               toString(groupArray((t.week_number, round(t.distinct_users / cs.cohort_size * 100, 2)))) as "Матрица удержания"
        from (
            select fv.cohort_week as cohort_week,
                   toUInt8((toMonday(h.EventDate) - fv.cohort_week) / 7) as week_number,
                   count(distinct h.UserID) as distinct_users
            from datasets.hits_v1 h
            join first_visits fv on h.UserID = fv.UserID
            where week_number <= 4
            group by fv.cohort_week, week_number
        ) t
        join cohort_sizes cs on t.cohort_week = cs.cohort_week
        group by t.cohort_week
        order by t.cohort_week desc
        """;

    public sealed class RetentionMatrixRow
    {
        [Column("Неделя когорты")] public DateTime CohortWeek { get; set; }
        [Column("Размер когорты")] public long CohortSize { get; set; }
        [Column("Матрица удержания")] public string Matrix { get; set; } = "";
    }

    public static async Task Retention(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new RetentionMatrixRow { CohortWeek = h.EventDate, CohortSize = 0, Matrix = "" })
            .WithSql(RetentionSql)
            .ToListAsync(ct);

        Print("4. retention", rows);
    }

    // 5. clickhouse_sessions.sql (lagInFrame -> lag, runningAccumulate -> cumulative sum_over)
    public static async Task Sessions(IDataContext ctx, CancellationToken ct)
    {
        var sessions = ctx.From<IHit>()
            .Select(h => new
            {
                h.UserId,
                h.EventTime,
                TimeDiff = SqlFunctions.Sql.date_diff("second",
                    SqlFunctions.Sql.lag(h.EventTime).Over(partitionBy: () => h.UserId, orderBy: () => h.EventTime),
                    h.EventTime)
            });

        var flags = ctx.From(sessions)
            .Select(s => new
            {
                s.UserId,
                s.EventTime,
                SessionId = SqlFunctions.Sql
                    .sum_over(s.TimeDiff == null || s.TimeDiff > 1800 ? 1 : 0)
                    .Over(
                        partitionBy: () => s.UserId,
                        orderBy: () => s.EventTime,
                        frame: WindowFrame.RowsUnboundedPrecedingToCurrentRow)
            });

        var counts = ctx.From(flags)
            .GroupBy(f => new { f.UserId, f.SessionId })
            .Select(f => new
            {
                f.UserId,
                f.SessionId,
                HitsInSession = SqlFunctions.Sql.count()
            });

        var threshold = await ctx.From(counts)
            .Select(c => SqlFunctions.ClickHouse.quantile(0.99, c.HitsInSession))
            .FirstAsync(ct);

        var rows = await ctx.From(counts)
            .Where(c => c.HitsInSession > threshold)
            .OrderByDescending(c => c.HitsInSession)
            .Limit(100)
            .Select(c => new
            {
                c.UserId,
                SessionId = c.SessionId,
                Hits = c.HitsInSession
            })
            .ToListAsync(ct);

        Print("5. sessions", rows);
    }

    // 6. daily_traffic (course: GROUP BY date + uniq, fully LINQ)
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

    // 7. top_landing_pages (raw: URL + uniq)
    private const string LandingPagesSql =
        """
        select URL as url,
               count() as hits,
               uniq(UserID) as users
        from datasets.hits_v1
        where EventDate = '2014-03-20' and URL != ''
        group by URL
        order by hits desc
        limit 10
        """;

    public sealed class LandingPageRow
    {
        [Column("url")] public string Url { get; set; } = "";
        [Column("hits")] public ulong Hits { get; set; }
        [Column("users")] public ulong Users { get; set; }
    }

    public static async Task TopLandingPages(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new LandingPageRow { Url = "", Hits = 0, Users = 0 })
            .WithSql(LandingPagesSql)
            .ToListAsync(ct);

        Print("7. top_landing_pages", rows);
    }

    // 8. device_split (raw: conditional aggregates via countIf)
    private const string DeviceSplitSql =
        """
        select if(IsMobile = 1, 'mobile', 'desktop') as device,
               count() as hits,
               uniq(UserID) as users,
               round(countIf(SearchPhrase != '') / count(), 4) as search_share,
               round(countIf(IsNotBounce = 0) / count(), 4) as bounce_share
        from datasets.hits_v1
        where EventDate between '2014-03-17' and '2014-03-23'
        group by device
        order by hits desc
        """;

    public sealed class DeviceSplitRow
    {
        [Column("device")] public string Device { get; set; } = "";
        [Column("hits")] public ulong Hits { get; set; }
        [Column("users")] public ulong Users { get; set; }
        [Column("search_share")] public double SearchShare { get; set; }
        [Column("bounce_share")] public double BounceShare { get; set; }
    }

    public static async Task DeviceSplit(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new DeviceSplitRow { Device = "", Hits = 0, Users = 0, SearchShare = 0, BounceShare = 0 })
            .WithSql(DeviceSplitSql)
            .ToListAsync(ct);

        Print("8. device_split", rows);
    }

    // 9. top_referrers (raw: RefererDomain + uniq)
    private const string TopReferrersSql =
        """
        select RefererDomain as referrer,
               count() as hits,
               uniq(UserID) as users
        from datasets.hits_v1
        where EventDate = '2014-03-20' and RefererDomain != ''
        group by RefererDomain
        order by hits desc
        limit 10
        """;

    public sealed class ReferrerRow
    {
        [Column("referrer")] public string Referrer { get; set; } = "";
        [Column("hits")] public ulong Hits { get; set; }
        [Column("users")] public ulong Users { get; set; }
    }

    public static async Task TopReferrers(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new ReferrerRow { Referrer = "", Hits = 0, Users = 0 })
            .WithSql(TopReferrersSql)
            .ToListAsync(ct);

        Print("9. top_referrers", rows);
    }

    // 10. session_depth (raw: sessionization + histogram buckets with multiIf)
    private const string SessionDepthSql =
        """
        with hits as (
            select UserID, EventTime,
                   lagInFrame(EventTime) over (partition by UserID order by EventTime) as prev_event
            from datasets.hits_v1
            where EventDate = '2014-03-20'
        ),
        flags as (
            select UserID, EventTime,
                   sum(if(prev_event is null or EventTime - prev_event > 1800, 1, 0))
                       over (partition by UserID order by EventTime rows between unbounded preceding and current row) as session_id
            from hits
        ),
        sessions as (
            select UserID, session_id, count() as hits
            from flags
            group by UserID, session_id
        )
        select multiIf(hits = 1, '01: single', hits <= 3, '02: 2-3', hits <= 10, '03: 4-10', hits <= 30, '04: 11-30', '05: 30+') as bucket,
               count() as sessions
        from sessions
        group by bucket
        order by bucket asc
        """;

    public sealed class SessionDepthRow
    {
        [Column("bucket")] public string Bucket { get; set; } = "";
        [Column("sessions")] public ulong Sessions { get; set; }
    }

    public static async Task SessionDepth(IDataContext ctx, CancellationToken ct)
    {
        var rows = await ctx.From<IHit>()
            .Select(h => new SessionDepthRow { Bucket = "", Sessions = 0 })
            .WithSql(SessionDepthSql)
            .ToListAsync(ct);

        Print("10. session_depth", rows);
    }

    // 11. rolling_activity (course: window over an aggregate, fully LINQ)
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
                Rolling7DayHits = SqlFunctions.Sql.sum_over(d.Hits).Over(
                    SqlFunctions.Sql.asc(() => d.EventDate),
                    WindowFrame.Rows(WindowFrameBound.Preceding(6), WindowFrameBound.CurrentRow)),
                CumulativeHits = SqlFunctions.Sql.sum_over(d.Hits).Over(
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
