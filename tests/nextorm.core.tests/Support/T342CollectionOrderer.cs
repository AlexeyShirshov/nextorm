using Xunit.Sdk;
using Xunit.v3;

namespace NextORM.Core.Tests;

/// <summary>
/// T342 (#342) collection-order lever: a deterministic replacement for the runner's default
/// (randomized) collection order, selected at run time through
/// <c>NEXTORM_T342_COLLECTION_ORDER</c>, so the order-dependent cache contamination can be reproduced
/// and driven deliberately instead of waited for.
/// </summary>
/// <remarks>
/// <para>
/// Only the <b>collection</b> order changes: classes, methods and cases keep the runner's default
/// order inside a collection, because no class/method orderer is registered. With the variable unset
/// or empty the orderer delegates to <see cref="DefaultTestCollectionOrderer"/>, so an ordinary run
/// behaves exactly as before.
/// </para>
/// <para>
/// The three explicit modes are pure permutations of the collections' ordinal <c>UniqueID</c>
/// sequence: <c>id-asc</c> (ordinal ascending), <c>id-desc</c> (its reverse) and <c>id-rotate</c>
/// (ascending rotated to start at <c>count / 2</c>). Any other value throws instead of silently
/// falling back to a mode nobody asked for. The runtime path is a single pass over the input plus the
/// permutation, and writes the resulting sequence to <c>NEXTORM_T342_ORDER_TRACE</c> when that is set,
/// so a run can prove which collection order it actually used.
/// </para>
/// </remarks>
public sealed class T342CollectionOrderer : ITestCollectionOrderer
{
    /// <summary>Environment variable naming the collection-order mode.</summary>
    internal const string ModeVariable = "NEXTORM_T342_COLLECTION_ORDER";

    /// <summary>Environment variable naming the optional order-trace output file.</summary>
    internal const string TraceVariable = "NEXTORM_T342_ORDER_TRACE";

    /// <summary>Ordinal ascending collection order.</summary>
    internal const string AscendingMode = "id-asc";

    /// <summary>Reversed ordinal ascending collection order.</summary>
    internal const string DescendingMode = "id-desc";

    /// <summary>Ascending collection order rotated to start at <c>count / 2</c>.</summary>
    internal const string RotateMode = "id-rotate";

    /// <inheritdoc />
    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(
        IReadOnlyCollection<TTestCollection> testCollections)
        where TTestCollection : ITestCollection
    {
        ArgumentNullException.ThrowIfNull(testCollections);

        var mode = Environment.GetEnvironmentVariable(ModeVariable);
        if (string.IsNullOrEmpty(mode))
            return DefaultTestCollectionOrderer.Instance.OrderTestCollections(testCollections);

        var count = testCollections.Count;
        var ids = new string[count];
        var byId = new Dictionary<string, TTestCollection>(count, StringComparer.Ordinal);
        var index = 0;
        foreach (var collection in testCollections)
        {
            ids[index++] = collection.UniqueID;
            byId.Add(collection.UniqueID, collection);
        }

        var order = ReorderIds(ids, mode);
        var ordered = new TTestCollection[order.Count];
        for (var i = 0; i < ordered.Length; i++)
            ordered[i] = byId[order[i]];

        WriteTrace(Environment.GetEnvironmentVariable(TraceVariable), order);

        return ordered;
    }

    /// <summary>
    /// Applies one of the three explicit modes to an ordinal <c>UniqueID</c> sequence. Exposed so the
    /// permutation contract can be tested without a runner.
    /// </summary>
    /// <param name="ids">Collection identifiers, in any order.</param>
    /// <param name="mode">One of <see cref="AscendingMode"/>, <see cref="DescendingMode"/>, <see cref="RotateMode"/>.</param>
    /// <returns>The permuted identifiers; the input is never modified.</returns>
    /// <exception cref="InvalidOperationException">The mode is not one of the three explicit modes.</exception>
    internal static IReadOnlyList<string> ReorderIds(IReadOnlyList<string> ids, string mode)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var ascending = new string[ids.Count];
        for (var i = 0; i < ascending.Length; i++)
            ascending[i] = ids[i];

        Array.Sort(ascending, StringComparer.Ordinal);

        switch (mode)
        {
            case AscendingMode:
                return ascending;

            case DescendingMode:
                Array.Reverse(ascending);
                return ascending;

            case RotateMode:
                return RotateByHalf(ascending);

            default:
                throw new InvalidOperationException(
                    $"{ModeVariable}='{mode}' is not a supported collection-order mode; "
                    + $"expected '{AscendingMode}', '{DescendingMode}' or '{RotateMode}'.");
        }
    }

    /// <summary>Rotates <paramref name="ascending"/> to start at <c>count / 2</c>, keeping the ordinal sequence.</summary>
    private static IReadOnlyList<string> RotateByHalf(string[] ascending)
    {
        var count = ascending.Length;
        if (count < 2)
            return ascending;

        var start = count / 2;
        var rotated = new string[count];
        for (var i = 0; i < count; i++)
            rotated[i] = ascending[(start + i) % count];

        return rotated;
    }

    /// <summary>Writes the resulting <c>UniqueID</c> sequence, one per line, when a trace path is configured.</summary>
    private static void WriteTrace(string? path, IReadOnlyList<string> order)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var lines = new string[order.Count];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = order[i];

        File.WriteAllLines(path, lines);
    }
}
