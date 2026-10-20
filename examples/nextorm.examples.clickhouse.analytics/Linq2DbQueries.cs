using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Mapping;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// The comparison arm: the same eleven queries as <see cref="ClickHouseQueries"/> (which model the
/// files in <c>Sql/</c>), expressed with linq2db over <see cref="Linq2DbEntities"/>. ClickHouse
/// functions with no linq2db LINQ surface (<c>windowFunnel</c>, <c>lagInFrame</c>, <c>uniq</c>,
/// <c>groupArray</c>, <c>multiIf</c>, <c>quantile</c>, arrays) are emitted with
/// <see cref="Sql.Expr{T}"/> so the whole query still goes through the linq2db translator. Retention
/// and the session queries format ClickHouse tuples/arrays with <c>toString</c> — the CLR comparison
/// arm has no tuple-array type, while the nextorm arm materialises <c>Tuple&lt;…&gt;[]</c>.
/// Methods return the row list and never print: the runner and the benchmarks share them.
/// </summary>
public static class Linq2DbQueries
{
    // ClickHouse calls the schema a database; the linq2db ClickHouse dialect ignores TableAttribute.Schema
    // and needs the database attached to the table.
    private static ITable<ChHit> Hits(IDataContext db) => db.GetTable<ChHit>().DatabaseName("datasets");

    private static ITable<ChDailyUniqueUsers> DailyUniqueUsers(IDataContext db) =>
        db.GetTable<ChDailyUniqueUsers>().DatabaseName("datasets");

    // 1. Sql/clickhouse_array_analytics.sql
    public static async Task<object> ArrayAnalytics(IDataContext db, CancellationToken ct)
    {
        return await (from h in Hits(db)
                      where h.SearchPhrase != "" && h.EventDate == new DateTime(2014, 3, 20)
                      group h by new
                      {
                          CleanWords = Sql.Expr<string>(
                              $"arrayStringConcat(arrayFilter(x -> length(x) > 3, arrayMap(x -> lower(x), splitByChar(' ', {h.SearchPhrase}))), ' ')")
                      } into g
                      orderby g.Count() descending
                      select new { g.Key.CleanWords, Occurrence = g.Count() })
                     .Take(10).ToListAsync(ct);
    }

    // 2. Sql/clickhouse_funnel.sql
    // windowFunnel is an aggregate over per-row columns; the linq2db LINQ surface has no raw-aggregate
    // API, so the per-user part is issued with FromSql and the level histogram is LINQ on top.
    public static async Task<object> Funnel(IDataContext db, CancellationToken ct)
    {
        var perUser = db.FromSql<FunnelLevelRow>($"""
            (
            SELECT UserID, windowFunnel(1800)(
                EventTime,
                URL LIKE '%/product/%',
                URL LIKE '%/cart%',
                URL LIKE '%/checkout/success%') AS Level
            FROM datasets.hits_v1
            WHERE EventDate = {new DateTime(2014, 3, 20)}
            GROUP BY UserID
            )
            """);

        return await (from p in perUser
                      group p by new { p.Level } into g
                      orderby g.Key.Level
                      select new { g.Key.Level, ConversionCount = g.Count() }).ToListAsync(ct);
    }

    // 3. Sql/clickhouse_incremental.sql — the nextorm arm throws (no uniqMerge surface); linq2db can
    // express the combinator with FromSql.
    public static async Task<object> Incremental(IDataContext db, CancellationToken ct)
    {
        var rows = db.FromSql<IncrementalRow>($"""
            (
            SELECT EventDate, uniqMerge(users_state) AS UniqueVisitors
            FROM datasets.daily_unique_users_mv
            WHERE EventDate >= {new DateTime(2014, 3, 1)} AND EventDate <= {new DateTime(2014, 3, 31)}
            GROUP BY EventDate
            ORDER BY EventDate DESC
            )
            """);

        return await rows.ToListAsync(ct);
    }

    // 4. Sql/clickhouse_retention.sql
    // The cohort matrix needs groupArray over a per-row tuple; the LINQ surface has no raw-aggregate
    // API, so the corrected reference query is issued with FromSql.
    public static async Task<object> Retention(IDataContext db, CancellationToken ct)
    {
        var rows = db.FromSql<RetentionRow>($"""
            (
            WITH
                first_visits AS (
                    SELECT UserID, toMonday(min(EventDate)) AS cohort_week
                    FROM datasets.hits_v1
                    GROUP BY UserID
                ),
                cohort_sizes AS (
                    SELECT cohort_week, count() AS cohort_size
                    FROM first_visits
                    GROUP BY cohort_week
                )
            SELECT
                t.cohort_week AS CohortWeek,
                toInt64(any(cs.cohort_size)) AS CohortSize,
                toString(groupArray((t.week_number, round(t.distinct_users / cs.cohort_size * 100, 2)))) AS RetentionMatrix
            FROM (
                SELECT
                    fv.cohort_week AS cohort_week,
                    toUInt8((toMonday(h.EventDate) - fv.cohort_week) / 7) AS week_number,
                    count(DISTINCT h.UserID) AS distinct_users
                FROM datasets.hits_v1 h
                JOIN first_visits fv ON h.UserID = fv.UserID
                WHERE week_number <= 4
                GROUP BY fv.cohort_week, week_number
            ) t
            JOIN cohort_sizes cs ON t.cohort_week = cs.cohort_week
            GROUP BY t.cohort_week
            ORDER BY t.cohort_week DESC
            )
            """);

        return await rows.ToListAsync(ct);
    }

    // 5. Sql/clickhouse_sessions.sql
    public static async Task<object> Sessions(IDataContext db, CancellationToken ct)
    {
        var sessions = (from h in Hits(db)
                        select new
                        {
                            UserID = (long)h.UserId,
                            EventTime = h.EventTime,
                            time_diff = Sql.Expr<int?>(
                                $"dateDiff('second', lagInFrame({h.EventTime}) OVER (PARTITION BY {h.UserId} ORDER BY {h.EventTime}), {h.EventTime})")
                        }).AsCte("sessions");

        var flags = (from s in sessions
                     select new
                     {
                         s.UserID,
                         s.EventTime,
                         session_id = Sql.Expr<long>(
                             $"sum(if({s.time_diff} IS NULL OR {s.time_diff} > 1800, 1, 0)) OVER (PARTITION BY {s.UserID} ORDER BY {s.EventTime} ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)")
                     }).AsCte("session_flags");

        var counts = (from f in flags
                      group f by new { f.UserID, f.session_id } into g
                      select new { g.Key.UserID, g.Key.session_id, hits_in_session = g.Count() }).AsCte("session_counts");

        // FirstAsync over a Sql.Expr scalar projection is not reducible by the linq2db builder;
        // ToListAsync returns the same single row.
        var threshold = (await (from c in counts
                                select Sql.Expr<double>($"quantile(0.99)({c.hits_in_session})")).ToListAsync(ct))
            .First();

        return await (from c in counts
                      where c.hits_in_session > threshold
                      orderby c.hits_in_session descending
                      select new { c.UserID, SessionId = c.session_id, Hits = c.hits_in_session })
                     .Take(100).ToListAsync(ct);
    }

    // 6. daily_traffic
    public static async Task<object> DailyTraffic(IDataContext db, CancellationToken ct)
    {
        return await (from h in Hits(db)
                      where h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23)
                      group h by new { h.EventDate } into g
                      orderby g.Key.EventDate
                      select new
                      {
                          g.Key.EventDate,
                          Hits = g.Count(),
                          Users = g.Select(x => x.UserId).Distinct().Count()
                      }).ToListAsync(ct);
    }

    // 7. top_landing_pages
    public static async Task<object> TopLandingPages(IDataContext db, CancellationToken ct)
    {
        return await (from h in Hits(db)
                      where h.EventDate == new DateTime(2014, 3, 20) && h.Url != ""
                      group h by new { h.Url } into g
                      orderby g.Count() descending
                      select new
                      {
                          g.Key.Url,
                          Hits = g.Count(),
                          Users = g.Select(x => x.UserId).Distinct().Count()
                      }).Take(10).ToListAsync(ct);
    }

    // 8. device_split
    public static async Task<object> DeviceSplit(IDataContext db, CancellationToken ct)
    {
        return await (from h in Hits(db)
                      where h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23)
                      group h by new { Device = h.IsMobile == 1 ? "mobile" : "desktop" } into g
                      orderby g.Count() descending
                      select new
                      {
                          g.Key.Device,
                          Hits = g.Count(),
                          Users = g.Select(x => x.UserId).Distinct().Count(),
                          SearchShare = Math.Round((double)g.Count(x => x.SearchPhrase != "") / g.Count(), 4),
                          BounceShare = Math.Round((double)g.Count(x => x.IsNotBounce == 0) / g.Count(), 4)
                      }).ToListAsync(ct);
    }

    // 9. top_referrers
    public static async Task<object> TopReferrers(IDataContext db, CancellationToken ct)
    {
        return await (from h in Hits(db)
                      where h.EventDate == new DateTime(2014, 3, 20) && h.RefererDomain != ""
                      group h by new { h.RefererDomain } into g
                      orderby g.Count() descending
                      select new
                      {
                          g.Key.RefererDomain,
                          Hits = g.Count(),
                          Users = g.Select(x => x.UserId).Distinct().Count()
                      }).Take(10).ToListAsync(ct);
    }

    // 10. session_depth
    public static async Task<object> SessionDepth(IDataContext db, CancellationToken ct)
    {
        var hits = (from h in Hits(db)
                    where h.EventDate == new DateTime(2014, 3, 20)
                    select new
                    {
                        h.UserId,
                        h.EventTime,
                        PrevEvent = Sql.Expr<DateTime?>(
                            $"lagInFrame({h.EventTime}) OVER (PARTITION BY {h.UserId} ORDER BY {h.EventTime})")
                    }).AsCte("hits");

        var flags = (from h in hits
                     select new
                     {
                         h.UserId,
                         h.EventTime,
                         SessionId = Sql.Expr<long>(
                             $"sum(if({h.PrevEvent} IS NULL OR dateDiff('second', {h.PrevEvent}, {h.EventTime}) > 1800, 1, 0)) OVER (PARTITION BY {h.UserId} ORDER BY {h.EventTime} ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)")
                     }).AsCte("flags");

        var sessions = (from f in flags
                        group f by new { f.UserId, f.SessionId } into g
                        select new { g.Key.UserId, g.Key.SessionId, Hits = g.Count() }).AsCte("sessions");

        return await (from s in sessions
                      group s by new
                      {
                          Bucket = Sql.Expr<string>(
                              $"multiIf({s.Hits} = 1, '01: single', {s.Hits} <= 3, '02: 2-3', {s.Hits} <= 10, '03: 4-10', {s.Hits} <= 30, '04: 11-30', '05: 30+')")
                      } into g
                      orderby g.Key.Bucket
                      select new { g.Key.Bucket, Sessions = g.Count() }).ToListAsync(ct);
    }

    // 11. rolling_activity
    public static async Task<object> RollingActivity(IDataContext db, CancellationToken ct)
    {
        var daily = from h in Hits(db)
                    where h.EventDate >= new DateTime(2014, 3, 17) && h.EventDate <= new DateTime(2014, 3, 23)
                    group h by new { h.EventDate } into g
                    select new
                    {
                        g.Key.EventDate,
                        Hits = g.Count(),
                        Users = g.Select(x => x.UserId).Distinct().Count()
                    };

        return await (from d in daily
                      orderby d.EventDate
                      select new
                      {
                          d.EventDate,
                          d.Hits,
                          d.Users,
                          Rolling7DayHits = Sql.Expr<long>(
                              $"sum({d.Hits}) OVER (ORDER BY {d.EventDate} ROWS BETWEEN 6 PRECEDING AND CURRENT ROW)"),
                          CumulativeHits = Sql.Expr<long>(
                              $"sum({d.Hits}) OVER (ORDER BY {d.EventDate} ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)")
                      }).ToListAsync(ct);
    }
}

// Projection types for the FromSql terminals above.
public sealed class FunnelLevelRow
{
    [Column("UserID")] public ulong UserId { get; set; }
    [Column("Level")] public byte Level { get; set; }
}

public sealed class IncrementalRow
{
    [Column("EventDate")] public DateTime EventDate { get; set; }
    [Column("UniqueVisitors")] public ulong UniqueVisitors { get; set; }
}

public sealed class RetentionRow
{
    [Column("CohortWeek")] public DateTime CohortWeek { get; set; }
    [Column("CohortSize")] public long CohortSize { get; set; }
    [Column("RetentionMatrix")] public string RetentionMatrix { get; set; } = "";
}
