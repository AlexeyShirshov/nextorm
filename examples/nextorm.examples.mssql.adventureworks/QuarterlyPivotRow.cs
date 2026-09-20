namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>One row of the quarterly margin cross-tab (see <c>mssql_quarterly_pivot.sql</c>).</summary>
public sealed class QuarterlyPivotRow
{
    public QuarterlyPivotRow(string categoryName, decimal? q1, decimal? q2, decimal? q3, decimal? q4)
    {
        CategoryName = categoryName;
        Q1 = Round(q1);
        Q2 = Round(q2);
        Q3 = Round(q3);
        Q4 = Round(q4);
    }

    public string CategoryName { get; }
    public decimal? Q1 { get; }
    public decimal? Q2 { get; }
    public decimal? Q3 { get; }
    public decimal? Q4 { get; }

    // SQL ROUND is "half away from zero"; .NET decimal.Round defaults to banker's rounding.
    private static decimal? Round(decimal? value) =>
        value is null ? null : decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
}
