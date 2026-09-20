namespace NextORM.Core;

/// <summary>
/// The <c>FOR SYSTEM_TIME</c> temporal-table clause attached to a query through
/// <c>EntityBuilder.ForSystemTime</c>. Create it with one of the static factory methods; the native
/// syntax is rendered by the provider dialect.
/// </summary>
public sealed record TemporalClause
{
    private TemporalClause(TemporalKind kind, DateTime from, DateTime to)
    {
        Kind = kind;
        From = from;
        To = to;
    }

    /// <summary>Which temporal clause is applied.</summary>
    public TemporalKind Kind { get; }

    /// <summary>The <c>AS OF</c> point, or the start of the range.</summary>
    public DateTime From { get; }

    /// <summary>The end of the range; unused by <see cref="TemporalKind.AsOf"/> and <see cref="TemporalKind.All"/>.</summary>
    public DateTime To { get; }

    /// <summary>Rows that were valid at <paramref name="point"/> in time (<c>AS OF</c>).</summary>
    /// <param name="point">The point in time to query.</param>
    public static TemporalClause AsOf(DateTime point) => new(TemporalKind.AsOf, point, default);

    /// <summary>Rows valid at some point in the closed-open range (<c>BETWEEN ... AND ...</c>).</summary>
    /// <param name="from">The inclusive range start.</param>
    /// <param name="to">The range end; must be after <paramref name="from"/>.</param>
    public static TemporalClause Between(DateTime from, DateTime to)
    {
        Validate(from, to);
        return new(TemporalKind.Between, from, to);
    }

    /// <summary>Rows valid in the closed-open range (<c>FROM ... TO ...</c>).</summary>
    /// <param name="from">The inclusive range start.</param>
    /// <param name="to">The exclusive range end; must be after <paramref name="from"/>.</param>
    public static TemporalClause FromTo(DateTime from, DateTime to)
    {
        Validate(from, to);
        return new(TemporalKind.FromTo, from, to);
    }

    /// <summary>Rows whose validity period lies entirely within the range (<c>CONTAINED IN</c>).</summary>
    /// <param name="from">The inclusive range start.</param>
    /// <param name="to">The inclusive range end; must be after <paramref name="from"/>.</param>
    public static TemporalClause ContainedIn(DateTime from, DateTime to)
    {
        Validate(from, to);
        return new(TemporalKind.ContainedIn, from, to);
    }

    /// <summary>Every row version (<c>ALL</c>).</summary>
    public static TemporalClause All() => new(TemporalKind.All, default, default);

    private static void Validate(DateTime from, DateTime to)
    {
        if (to <= from)
            throw new ArgumentOutOfRangeException(nameof(to), to, "The temporal range end must be after the start.");
    }
}
