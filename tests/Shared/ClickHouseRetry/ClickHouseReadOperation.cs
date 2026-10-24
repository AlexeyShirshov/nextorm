namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// The lifecycle category of a ClickHouse test operation. Only the four read categories can be
/// audited as retry-eligible; everything else is rejected by construction (T328 AC10, V09).
/// </summary>
internal enum ClickHouseReadCategory
{
    /// <summary>A single-row lookup ("get by key").</summary>
    PointRead,

    /// <summary>A read that materialises a bounded list before control returns to the test.</summary>
    MaterialisedListRead,

    /// <summary>A scalar aggregate (<c>COUNT</c>, <c>MAX</c>, …) over a read-only query.</summary>
    ScalarAggregate,

    /// <summary>
    /// A read consumed incrementally through a stream. Never retry-eligible: a partially consumed
    /// stream cannot be replayed safely — the second attempt would restart mid-sequence, so the
    /// caller could observe duplicated or missing rows, and the outstanding enumeration is not
    /// guaranteed to be quiescent when the first attempt fails.
    /// </summary>
    StreamedRead,

    /// <summary>An INSERT/UPDATE/DELETE or any other mutating statement.</summary>
    Write,

    /// <summary>A schema statement (CREATE/ALTER/DROP/TRUNCATE/OPTIMIZE …).</summary>
    Ddl,

    /// <summary>Fixture setup/teardown, seeding or cleanup work.</summary>
    FixtureMutation,

    /// <summary>An operation whose effects (or resource lifetime) are not established.</summary>
    UnknownEffect,
}

/// <summary>
/// An explicitly opted-in ClickHouse test operation. The only way to construct a non-default value
/// is <see cref="Audited"/>, which rejects every category whose replay could mutate data, change
/// schema, leave fixture state behind, or consume a stream that cannot be restarted.
/// </summary>
internal readonly record struct ClickHouseReadOperation
{
    private ClickHouseReadOperation(string testIdentity, string operationIdentity, ClickHouseReadCategory category)
    {
        TestIdentity = testIdentity;
        OperationIdentity = operationIdentity;
        Category = category;
    }

    /// <summary>The stable identity of the owning test (used by the attempt records).</summary>
    public string TestIdentity { get; }

    /// <summary>The stable identity of the read operation inside that test.</summary>
    public string OperationIdentity { get; }

    /// <summary>The audited lifecycle category.</summary>
    public ClickHouseReadCategory Category { get; }

    /// <summary>
    /// Creates an audited read-only operation. <paramref name="category"/> must be one of
    /// <see cref="ClickHouseReadCategory.PointRead"/>, <see cref="ClickHouseReadCategory.MaterialisedListRead"/>
    /// or <see cref="ClickHouseReadCategory.ScalarAggregate"/>; a streamed read and every mutating or
    /// unknown-effect category is rejected with the category name in the message.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An identity is empty/whitespace, or the category is not retry-eligible.
    /// </exception>
    internal static ClickHouseReadOperation Audited(
        string testIdentity,
        string operationIdentity,
        ClickHouseReadCategory category)
    {
        if (string.IsNullOrWhiteSpace(testIdentity))
            throw new ArgumentException("A test identity is required for an audited operation.", nameof(testIdentity));
        if (string.IsNullOrWhiteSpace(operationIdentity))
            throw new ArgumentException("An operation identity is required for an audited operation.", nameof(operationIdentity));

        return category switch
        {
            ClickHouseReadCategory.PointRead
                or ClickHouseReadCategory.MaterialisedListRead
                or ClickHouseReadCategory.ScalarAggregate
                => new ClickHouseReadOperation(testIdentity, operationIdentity, category),

            // StreamedRead is rejected for the same reason it is documented above: a partially
            // consumed stream cannot be replayed safely.
            ClickHouseReadCategory.StreamedRead
                or ClickHouseReadCategory.Write
                or ClickHouseReadCategory.Ddl
                or ClickHouseReadCategory.FixtureMutation
                or ClickHouseReadCategory.UnknownEffect
                => throw new ArgumentException(
                    $"The ClickHouse read category '{category}' is not retry-eligible; only PointRead, MaterialisedListRead and ScalarAggregate are audited for a repeat attempt.",
                    nameof(category)),

            _ => throw new ArgumentException(
                $"The ClickHouse read category '{category}' is not known and cannot be audited.",
                nameof(category)),
        };
    }
}
