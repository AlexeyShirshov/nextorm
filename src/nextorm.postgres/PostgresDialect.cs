using System.Text;
using nextorm.core;

namespace nextorm.postgres;

/// <summary>
/// PostgreSQL dialect: <c>||</c> concatenation, <c>@name</c> parameters, double-quoted identifiers,
/// <c>limit/offset</c> paging and the aggregate name mapping (<c>stdev</c> -> <c>stddev</c>, ...).
/// </summary>
public sealed class PostgresDialect : SqlDialectBase
{
    public static readonly PostgresDialect Instance = new();

    public override string ConcatStringOperator => "||";

    public override string MakeParam(string name) => $"@{name}";

    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    public override string MakeBool(bool v) => v ? "true" : "false";

    // PostgreSQL only accepts double-quoted identifiers; single-quoted aliases are a syntax error.
    public override string Escape(string keyword) => "\"" + keyword + "\"";

    public override string MakeColumnReference(string name) => Escape(name);

    public override bool RequireSubqueryAlias => true;

    // PostgreSQL is the only supported provider that implements INTERSECT ALL / EXCEPT ALL.
    public override bool SupportsIntersectExceptAll => true;

    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddev",
        "stdevp" => "stddev_pop",
        "var" => "variance",
        "varp" => "var_pop",
        _ => name
    };

    // PostgreSQL's log() is base 10; the natural logarithm (Math.Log) is ln().
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        name == "log" && args.Count == 1
            ? $"ln({args[0]})"
            : base.MakeMathFunction(name, args);

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        // PostgreSQL uses "limit N offset M"; OFFSET may appear on its own, but LIMIT must come first.
        if (paging.Limit > 0)
            sqlBuilder.Append("limit ").Append(paging.Limit);

        if (paging.Offset > 0)
        {
            if (paging.Limit > 0)
                sqlBuilder.Append(' ');

            sqlBuilder.Append("offset ").Append(paging.Offset);
        }
    }
}
