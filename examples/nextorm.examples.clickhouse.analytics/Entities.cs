using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Examples.ClickHouse.Analytics;

// Schema: ClickHouse's official "anonymized web analytics" sample (datasets.hits_v1 + a
// stand-specific AggregatingMergeTree materialized view).

[SqlTable("datasets.hits_v1")]
public interface IHit
{
    [Key, Column("WatchID")] ulong WatchId { get; set; }
    [Column("UserID")] ulong UserId { get; set; }
    [Column("EventDate")] DateTime EventDate { get; set; }
    [Column("EventTime")] DateTime EventTime { get; set; }
    [Column("URL")] string Url { get; set; }
    [Column("SearchPhrase")] string SearchPhrase { get; set; }
}

[SqlTable("datasets.daily_unique_users_mv")]
public interface IDailyUniqueUsers
{
    [Key, Column("EventDate")] DateTime EventDate { get; set; }

    // AggregateFunction(uniq, UInt64): the state cannot be projected as a scalar, it is only read
    // through uniqMerge(...) inside a query.
    [Column("users_state")] object? UsersState { get; set; }
}
