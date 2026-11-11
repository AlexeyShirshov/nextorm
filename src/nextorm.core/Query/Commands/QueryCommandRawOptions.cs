using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The rarely used raw query options of a <see cref="QueryCommand"/>, held behind one reference so the
/// command object stays small (issue #319: the command is created and cloned by every fluent step and
/// the common query sets none of these).
/// </summary>
/// <remarks>
/// Writers go through the command's properties, which allocate this payload on the first non-default
/// value and clear the member in place when the value is reset to default. A read of an absent payload
/// yields the default value through the null-conditional accessor, so a command that never carries
/// these options pays only the single reference.
/// </remarks>
internal sealed class QueryCommandRawOptions
{
    /// <summary>The trailing ClickHouse <c>SETTINGS</c> entries.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Settings;
    /// <summary>The declared table hints.</summary>
    public IReadOnlyList<string>? TableHints;
    /// <summary>The declared index hints.</summary>
    public IReadOnlyList<string>? IndexHints;
    /// <summary>The hints that apply to every table in scope.</summary>
    public IReadOnlyList<string>? TablesInScopeHints;
    /// <summary>The <c>FOR JSON</c> clause.</summary>
    public ForJsonClause? ForJsonClause;
    /// <summary>The <c>FOR XML</c> clause.</summary>
    public ForXmlClause? ForXmlClause;
    /// <summary>The <c>TABLESAMPLE</c> clause.</summary>
    public TableSampleClause? TableSample;
    /// <summary>The temporal-table clause.</summary>
    public TemporalClause? Temporal;
    /// <summary>The result-row lock clause.</summary>
    public LockClause? RowLock;
    /// <summary>The primary-source lock clause.</summary>
    public LockClause? SourceLockClause;
    /// <summary>The <c>LIMIT BY</c> clause.</summary>
    public LimitByClause? LimitBy;
    /// <summary>The <c>DISTINCT ON</c> clause.</summary>
    public DistinctOnClause? DistinctOn;
    /// <summary>The extreme-row (<c>SelectWhereMin/Max</c>) clause.</summary>
    public ExtremeRowClause? ExtremeRow;
    /// <summary>The member paths the in-memory navigation expansion materializes.</summary>
    public IReadOnlyDictionary<ParameterExpression, string>? NavigationPaths;
    /// <summary>The child filter of an in-memory junction expansion.</summary>
    public JunctionChildFilter? JunctionChildFilter;
    /// <summary>The in-memory navigation chain of the command.</summary>
    public InMemoryNavigationChain? NavigationChain;
    /// <summary>The registry correlated expressions resolve against.</summary>
    public IQueryRegistry? OuterRegistry;
}
