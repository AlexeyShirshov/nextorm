using LinqToDB.Mapping;

namespace NextORM.Examples.ClickHouse.Analytics;

// linq2db mappings for the tables modelled by `Entities.cs`.

[Table("hits_v1", Schema = "datasets")]
public sealed class ChHit
{
    [PrimaryKey, Column("WatchID")] public ulong WatchId { get; set; }
    [Column("UserID")] public ulong UserId { get; set; }
    [Column("EventDate")] public DateTime EventDate { get; set; }
    [Column("EventTime")] public DateTime EventTime { get; set; }
    [Column("URL")] public string Url { get; set; } = "";
    [Column("RefererDomain")] public string RefererDomain { get; set; } = "";
    [Column("SearchPhrase")] public string SearchPhrase { get; set; } = "";
    [Column("IsMobile")] public byte IsMobile { get; set; }
    [Column("IsNotBounce")] public byte IsNotBounce { get; set; }
}

// AggregateFunction(uniq, UInt64): the state is never materialised, only passed to uniqMerge().
[Table("daily_unique_users_mv", Schema = "datasets")]
public sealed class ChDailyUniqueUsers
{
    [Column("EventDate")] public DateTime EventDate { get; set; }
    [Column("users_state")] public byte[]? UsersState { get; set; }
}
