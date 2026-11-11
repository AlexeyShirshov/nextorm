using System.Globalization;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S4/T302 (VM-S4-09, VM-S4-10): the raw ranking oracle the <c>ExtremeRowTies.All</c> winner set is
/// compared against for collation and floating-point parity.
/// <para>
/// <c>All</c> no longer routes through the window path (approved plan §7), so production SQL always
/// lowers to aggregate + join-back. The oracle therefore runs <c>rank() over (...)</c> itself, through
/// raw ADO.NET on the provider's own connection and over the same live table, and the fact compares
/// only the resulting id sets. A divergence is a parity blocker, never a test to adjust.
/// </para>
/// <para>
/// Value keys are filtered to non-<c>NULL</c> before ranking: an <c>All</c> query never treats a row
/// with a <c>NULL</c> value key as a candidate (the aggregate ignores it and the join-back guard
/// excludes it), so the oracle must exclude it too.
/// </para>
/// </summary>
internal static class T302S4ParityOracle
{
    /// <summary>Runs a statement through raw ADO.NET on the provider's own connection.</summary>
    public static void Execute(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>Runs a query and returns the first column of every row as an integer.</summary>
    public static IReadOnlyList<int> Ids(IDataContext context, string sql)
    {
        var dataContext = (DataContext)context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        using var reader = command.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
        return ids;
    }

    /// <summary>
    /// The <c>rank() = 1</c> id set over the same table, ordered ascending so the comparison is
    /// sequence-free: both sides are <c>order by id</c>. A non-<c>null</c>
    /// <paramref name="partitionColumn"/> ranks inside each group, matching the grouped <c>All</c> shape.
    /// </summary>
    public static IReadOnlyList<int> RankedWinnerIds(
        IDataContext context,
        string table,
        string valueColumn,
        bool descending,
        string? partitionColumn = null)
    {
        var partition = partitionColumn is null ? string.Empty : $"partition by {partitionColumn} ";
        var direction = descending ? "desc" : "asc";
        return Ids(
            context,
            "select id from ("
            + $"select id, rank() over ({partition}order by {valueColumn} {direction}) as rnk "
            + $"from {table} where {valueColumn} is not null"
            + ") ranked where rnk = 1 order by id");
    }
}
