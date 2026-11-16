using System.Diagnostics.CodeAnalysis;

namespace NextORM.Core;

/// <summary>The whole-second-or-larger interval units supported for a ClickHouse <see cref="DateTime"/>.</summary>
internal static class ClickHouseIntervalUnits
{
    private static readonly Dictionary<string, string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["second"] = "SECOND",
        ["minute"] = "MINUTE",
        ["hour"] = "HOUR",
        ["day"] = "DAY",
        ["week"] = "WEEK",
        ["month"] = "MONTH",
        ["quarter"] = "QUARTER",
        ["year"] = "YEAR"
    };

    internal static bool TryGetKeyword(string unit, [NotNullWhen(true)] out string? keyword)
        => Keywords.TryGetValue(unit, out keyword);
}
