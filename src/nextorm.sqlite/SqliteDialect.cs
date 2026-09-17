using System.Text;
using nextorm.core;

namespace nextorm.sqlite;

/// <summary>SQLite dialect: <c>||</c> concatenation, <c>$name</c> parameters, <c>limit/offset</c> paging.</summary>
public sealed class SqliteDialect : SqlDialectBase
{
    public static readonly SqliteDialect Instance = new();

    public override string ConcatStringOperator => "||";

    public override string MakeCoalesce(string v1, string v2) => $"ifnull({v1}, {v2})";

    public override string MakeParam(string name) => $"${name}";

    // SQLite has no now(); datetime('now') is UTC and is used for both local and UTC requests.
    public override string MakeNow(bool utc) => "datetime('now')";

    // SQLite extracts date parts through strftime; the result is cast back to an integer so that it
    // materialises like the C# DateTime.Year/Month/... int properties.
    public override string MakeDatePart(string part, string value) => part switch
    {
        "year" => $"cast(strftime('%Y', {value}) as integer)",
        "month" => $"cast(strftime('%m', {value}) as integer)",
        "day" => $"cast(strftime('%d', {value}) as integer)",
        "hour" => $"cast(strftime('%H', {value}) as integer)",
        "minute" => $"cast(strftime('%M', {value}) as integer)",
        "second" => $"cast(strftime('%S', {value}) as integer)",
        _ => base.MakeDatePart(part, value)
    };

    // SQLite's log() is base 10; the natural logarithm (Math.Log) is ln().
    public override string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        name == "log" && args.Count == 1
            ? $"ln({args[0]})"
            : base.MakeMathFunction(name, args);

    public override void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        sqlBuilder.Append("limit ").Append(paging.Limit > 0
            ? paging.Limit
            : -1);

        if (paging.Offset > 0)
            sqlBuilder.Append(" offset ").Append(paging.Offset);
    }
}
