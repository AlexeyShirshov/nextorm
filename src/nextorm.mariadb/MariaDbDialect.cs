using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MariaDb;

/// <summary>
/// MariaDB dialect: the MySQL family plus the <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c> set-operation
/// variants (MariaDB 10.4+) and the window percentile functions <c>PERCENTILE_CONT</c>/<c>MEDIAN</c>
/// (MariaDB 10.3+), neither of which MySQL implements.
/// </summary>
public sealed class MariaDbDialect : MySqlDialect
{
    public static new readonly MariaDbDialect Instance = new();

    public override bool SupportsIntersectExceptAll => true;

    /// <summary>MariaDB 10.3+ renders <c>PERCENTILE_CONT</c>/<c>PERCENTILE_DISC</c> as window functions.</summary>
    public override bool SupportsPercentileWindow => true;

    /// <summary>
    /// MariaDB has no <c>ANY_VALUE</c> in the supported range: the SQL-2023 <c>T626</c> feature
    /// (MDEV-10426) is still pending and targeted for 13.2, so <c>any_agg</c> is gated off even though
    /// MySQL implements it.
    /// </summary>
    public override bool SupportsAnyValueAggregate => false;

    /// <summary>MariaDB 11.7+ renders both UUID generators; MySQL has no v4/v7 generator.</summary>
    public override bool SupportsUuidGenerators => true;

    /// <summary>MariaDB supports the random v4 (<c>UUID_v4()</c>) and v7 (<c>UUID_v7()</c>, 11.7+) generators.</summary>
    public override bool SupportsUuidGenerator(string name) => name is "gen_random_uuid" or "uuidv7";

    /// <summary>MariaDB spells the UUID generators <c>UUID_v4()</c>/<c>UUID_v7()</c>.</summary>
    public override string MakeUuidGenerator(string name) => name switch
    {
        "gen_random_uuid" => "uuid_v4()",
        "uuidv7" => "uuid_v7()",
        _ => base.MakeUuidGenerator(name)
    };

    /// <summary>MariaDB 10.3+ supports the <c>FOR SYSTEM_TIME</c> temporal-table clause on system-versioned tables.</summary>
    public override bool SupportsTemporalTable => true;

    /// <summary>MariaDB has no <c>CONTAINED IN</c>, so it is gated off; the other kinds are supported.</summary>
    public override bool SupportsTemporalKind(TemporalKind kind) => kind != TemporalKind.ContainedIn;
}
