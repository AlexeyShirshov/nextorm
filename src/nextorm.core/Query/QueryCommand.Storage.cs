using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Node-owned preparation state of a <see cref="QueryCommand"/>. Allocated lazily on the first write and
/// never shared between commands: a clone receives its own instance through <c>CopyTo</c>. The default
/// getters in <see cref="QueryCommand"/> read the fields through a null check, so an unprepared command
/// holds no preparation storage at all.
/// </summary>
internal sealed class QueryCommandPreparation
{
    internal SelectExpression[]? SelectList;
    internal FromExpression? From;
    internal bool IsPrepared;
    internal Expression[]? PreparedArrayJoin;
    internal SelectExpression[]? GroupingList;
    internal SelectExpression[]? LimitByColumns;
    internal SelectExpression[]? DistinctOnColumns;
    internal SelectExpression[]? ExtremeRowColumns;
    internal SelectExpression[]? ExtremeRowGroupByColumns;
    internal SelectExpression[]? PerParentPartitionColumns;
    internal Expression? PreparedCondition;
    internal Expression? PreparedHaving;
    internal Expression? PreparedPreWhere;
    internal int PreWhereShapeHash;
    internal bool HasPreWhereInValues;
    internal int InValuesShapeHash;
    internal int WhereBasePlanHash;
    internal bool HasTopLevelInValues;
    internal Dictionary<Expression, InValuesPartition>? InValuesPartitions;
    internal Dictionary<Expression, List<LookupEntry>>? LookupPartitions;
    internal bool ShapeScanned;
    internal bool ShapeValuesStable;
    internal bool HasUnkeyedTupleInValues;
    internal bool HasUnkeyedScalarInValues;
    internal int ColumnsPlanHash;
    internal int JoinPlanHash;
    internal int SortingPlanHash;
    internal int WherePlanHash;
    internal int GroupingPlanHash;
    internal int ResultPlanHash;
    internal int FromPlanHash;
    internal int UnionPlanHash;
    internal int ReferencedQueriesPlanHash;
    internal int CtesPlanHash;
    internal int HintsPlanHash;
    internal int WindowsPlanHash;
    internal Type? ResultType;
    internal bool DefaultOnEmpty;
    internal bool SingleScalar;
    internal bool JsonShapeMode;
    internal JsonShapeNode? JsonShape;
    internal QueryPlanEqualityComparer? QueryPlanComparer;
    internal ExpressionPlanEqualityComparer? ExpressionPlanComparer;
    internal SelectExpressionPlanEqualityComparer? SelectExpressionPlanComparer;
    internal FromExpressionPlanEqualityComparer? FromExpressionPlanComparer;
    internal JoinExpressionPlanEqualityComparer? JoinExpressionPlanComparer;
    internal SortingExpressionPlanEqualityComparer? SortingExpressionPlanComparer;
    internal QueryPlan? PlanKey;
    internal string? PlanKeySql;
    internal int PlanKeyInValuesShapeHash;
    internal int PlanKeyPreWhereShapeHash;
    internal bool ResolvedQuoteIdentifiers;
    internal INamingConvention? ResolvedNamingConvention;
    internal KeywordCase ResolvedKeywordCase;
    internal int? ResolvedCommandTimeout;
    internal EagerLoadMode ResolvedEagerLoadMode;
    internal bool PendingJoinIntoCartesianWarning;
    internal List<QueryCommand.RawSourceFilterSkip>? PendingRawSourceFilterSkips;
    internal List<QueryCommand.PendingCrossJoinFilter>? PendingCrossJoinFilters;

    /// <summary>
    /// Clears exactly the pieces <see cref="QueryCommand.ResetPreparation"/> cleared before the storage
    /// migration. The eleven plan hashes other than <see cref="ReferencedQueriesPlanHash"/> and the
    /// node-owned shape flags intentionally survive a reset, matching the original field behaviour.
    /// </summary>
    internal void Reset()
    {
        IsPrepared = false;
        SelectList = null;
        GroupingList = null;
        LimitByColumns = null;
        DistinctOnColumns = null;
        ExtremeRowColumns = null;
        ExtremeRowGroupByColumns = null;
        PerParentPartitionColumns = null;
        PreparedPreWhere = null;
        PreparedArrayJoin = null;
        PreWhereShapeHash = 0;
        HasPreWhereInValues = false;
        From = null;
        PreparedCondition = null;
        PreparedHaving = null;
        InValuesShapeHash = 0;
        InValuesPartitions = null;
        HasTopLevelInValues = false;
        LookupPartitions = null;
        ShapeScanned = false;
        ShapeValuesStable = false;
        HasUnkeyedTupleInValues = false;
        HasUnkeyedScalarInValues = false;
        WhereBasePlanHash = 0;
        ReferencedQueriesPlanHash = 0;
        JsonShapeMode = false;
        JsonShape = null;
        PlanKey = null;
        PlanKeySql = null;
    }
}

/// <summary>
/// Node-owned binding and cache-policy state of a <see cref="QueryCommand"/>. Allocated only when one of
/// the non-default values is written and never shared between commands.
/// </summary>
internal sealed class QueryCommandBinding
{
    internal bool DontCache;
    internal bool HasEverPrepared;
    internal CteReference? TypedRecursiveAnchor;
}

public partial class QueryCommand
{
    private QueryCommandPreparation? _preparation;
    private QueryCommandBinding? _binding;

    private QueryCommandPreparation Preparation => _preparation ??= new QueryCommandPreparation();
    private QueryCommandBinding Binding => _binding ??= new QueryCommandBinding();

    /// <summary>The prepared projection columns after <c>PrepareCommand</c>, or <c>null</c> before preparation.</summary>
    protected SelectExpression[]? _selectList
    {
        get => _preparation?.SelectList;
        set
        {
            if (ReferenceEquals(value, _preparation?.SelectList)) return;
            Preparation.SelectList = value;
        }
    }

    /// <summary>The prepared <c>FROM</c> source, or <c>null</c> before preparation.</summary>
    protected FromExpression? _from
    {
        get => _preparation?.From;
        set
        {
            if (ReferenceEquals(value, _preparation?.From)) return;
            Preparation.From = value;
        }
    }

    /// <summary>The prepared <c>GROUP BY</c> columns, or <c>null</c> when the query has no grouping.</summary>
    private SelectExpression[]? _groupingList
    {
        get => _preparation?.GroupingList;
        set
        {
            if (ReferenceEquals(value, _preparation?.GroupingList)) return;
            Preparation.GroupingList = value;
        }
    }

    internal Expression[]? _preparedArrayJoin
    {
        get => _preparation?.PreparedArrayJoin;
        set
        {
            if (ReferenceEquals(value, _preparation?.PreparedArrayJoin)) return;
            Preparation.PreparedArrayJoin = value;
        }
    }

    private SelectExpression[]? _limitByColumns
    {
        get => _preparation?.LimitByColumns;
        set
        {
            if (ReferenceEquals(value, _preparation?.LimitByColumns)) return;
            Preparation.LimitByColumns = value;
        }
    }

    private SelectExpression[]? _distinctOnColumns
    {
        get => _preparation?.DistinctOnColumns;
        set
        {
            if (ReferenceEquals(value, _preparation?.DistinctOnColumns)) return;
            Preparation.DistinctOnColumns = value;
        }
    }

    private SelectExpression[]? _extremeRowColumns
    {
        get => _preparation?.ExtremeRowColumns;
        set
        {
            if (ReferenceEquals(value, _preparation?.ExtremeRowColumns)) return;
            Preparation.ExtremeRowColumns = value;
        }
    }

    private SelectExpression[]? _extremeRowGroupByColumns
    {
        get => _preparation?.ExtremeRowGroupByColumns;
        set
        {
            if (ReferenceEquals(value, _preparation?.ExtremeRowGroupByColumns)) return;
            Preparation.ExtremeRowGroupByColumns = value;
        }
    }

    private SelectExpression[]? _perParentPartitionColumns
    {
        get => _preparation?.PerParentPartitionColumns;
        set
        {
            if (ReferenceEquals(value, _preparation?.PerParentPartitionColumns)) return;
            Preparation.PerParentPartitionColumns = value;
        }
    }

    /// <summary>Whether the command has been prepared and its cached plan hashes are current.</summary>
    protected bool _isPrepared
    {
        get => _preparation?.IsPrepared ?? false;
        set
        {
            if (value == (_preparation?.IsPrepared ?? false)) return;
            Preparation.IsPrepared = value;
        }
    }

    internal Expression? PreparedCondition
    {
        get => _preparation?.PreparedCondition;
        set
        {
            if (ReferenceEquals(value, _preparation?.PreparedCondition)) return;
            Preparation.PreparedCondition = value;
        }
    }

    internal Expression? PreparedHaving
    {
        get => _preparation?.PreparedHaving;
        set
        {
            if (ReferenceEquals(value, _preparation?.PreparedHaving)) return;
            Preparation.PreparedHaving = value;
        }
    }

    internal Expression? PreparedPreWhere
    {
        get => _preparation?.PreparedPreWhere;
        set
        {
            if (ReferenceEquals(value, _preparation?.PreparedPreWhere)) return;
            Preparation.PreparedPreWhere = value;
        }
    }

    internal int PreWhereShapeHash
    {
        get => _preparation?.PreWhereShapeHash ?? 0;
        set
        {
            if (value == (_preparation?.PreWhereShapeHash ?? 0)) return;
            Preparation.PreWhereShapeHash = value;
        }
    }

    internal bool HasPreWhereInValues
    {
        get => _preparation?.HasPreWhereInValues ?? false;
        set
        {
            if (value == (_preparation?.HasPreWhereInValues ?? false)) return;
            Preparation.HasPreWhereInValues = value;
        }
    }

    internal int InValuesShapeHash
    {
        get => _preparation?.InValuesShapeHash ?? 0;
        set
        {
            if (value == (_preparation?.InValuesShapeHash ?? 0)) return;
            Preparation.InValuesShapeHash = value;
        }
    }

    private int _whereBasePlanHash
    {
        get => _preparation?.WhereBasePlanHash ?? 0;
        set
        {
            if (value == (_preparation?.WhereBasePlanHash ?? 0)) return;
            Preparation.WhereBasePlanHash = value;
        }
    }

    internal bool HasTopLevelInValues
    {
        get => _preparation?.HasTopLevelInValues ?? false;
        set
        {
            if (value == (_preparation?.HasTopLevelInValues ?? false)) return;
            Preparation.HasTopLevelInValues = value;
        }
    }

    internal Dictionary<Expression, InValuesPartition>? InValuesPartitions
    {
        get => _preparation?.InValuesPartitions;
        set
        {
            if (ReferenceEquals(value, _preparation?.InValuesPartitions)) return;
            Preparation.InValuesPartitions = value;
        }
    }

    internal Dictionary<Expression, List<LookupEntry>>? LookupPartitions
    {
        get => _preparation?.LookupPartitions;
        set
        {
            if (ReferenceEquals(value, _preparation?.LookupPartitions)) return;
            Preparation.LookupPartitions = value;
        }
    }

    internal bool ShapeScanned
    {
        get => _preparation?.ShapeScanned ?? false;
        set
        {
            if (value == (_preparation?.ShapeScanned ?? false)) return;
            Preparation.ShapeScanned = value;
        }
    }

    internal bool ShapeValuesStable
    {
        get => _preparation?.ShapeValuesStable ?? false;
        set
        {
            if (value == (_preparation?.ShapeValuesStable ?? false)) return;
            Preparation.ShapeValuesStable = value;
        }
    }

    internal bool HasUnkeyedTupleInValues
    {
        get => _preparation?.HasUnkeyedTupleInValues ?? false;
        set
        {
            if (value == (_preparation?.HasUnkeyedTupleInValues ?? false)) return;
            Preparation.HasUnkeyedTupleInValues = value;
        }
    }

    internal bool HasUnkeyedScalarInValues
    {
        get => _preparation?.HasUnkeyedScalarInValues ?? false;
        set
        {
            if (value == (_preparation?.HasUnkeyedScalarInValues ?? false)) return;
            Preparation.HasUnkeyedScalarInValues = value;
        }
    }

    internal int ColumnsPlanHash
    {
        get => _preparation?.ColumnsPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.ColumnsPlanHash ?? 0)) return;
            Preparation.ColumnsPlanHash = value;
        }
    }

    internal int JoinPlanHash
    {
        get => _preparation?.JoinPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.JoinPlanHash ?? 0)) return;
            Preparation.JoinPlanHash = value;
        }
    }

    internal int SortingPlanHash
    {
        get => _preparation?.SortingPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.SortingPlanHash ?? 0)) return;
            Preparation.SortingPlanHash = value;
        }
    }

    internal int WherePlanHash
    {
        get => _preparation?.WherePlanHash ?? 0;
        set
        {
            if (value == (_preparation?.WherePlanHash ?? 0)) return;
            Preparation.WherePlanHash = value;
        }
    }

    internal int GroupingPlanHash
    {
        get => _preparation?.GroupingPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.GroupingPlanHash ?? 0)) return;
            Preparation.GroupingPlanHash = value;
        }
    }

    internal int ResultPlanHash
    {
        get => _preparation?.ResultPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.ResultPlanHash ?? 0)) return;
            Preparation.ResultPlanHash = value;
        }
    }

    internal int FromPlanHash
    {
        get => _preparation?.FromPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.FromPlanHash ?? 0)) return;
            Preparation.FromPlanHash = value;
        }
    }

    internal int UnionPlanHash
    {
        get => _preparation?.UnionPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.UnionPlanHash ?? 0)) return;
            Preparation.UnionPlanHash = value;
        }
    }

    internal int ReferencedQueriesPlanHash
    {
        get => _preparation?.ReferencedQueriesPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.ReferencedQueriesPlanHash ?? 0)) return;
            Preparation.ReferencedQueriesPlanHash = value;
        }
    }

    internal int CtesPlanHash
    {
        get => _preparation?.CtesPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.CtesPlanHash ?? 0)) return;
            Preparation.CtesPlanHash = value;
        }
    }

    internal int HintsPlanHash
    {
        get => _preparation?.HintsPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.HintsPlanHash ?? 0)) return;
            Preparation.HintsPlanHash = value;
        }
    }

    internal int WindowsPlanHash
    {
        get => _preparation?.WindowsPlanHash ?? 0;
        set
        {
            if (value == (_preparation?.WindowsPlanHash ?? 0)) return;
            Preparation.WindowsPlanHash = value;
        }
    }

    internal Type? ResultType
    {
        get => _preparation?.ResultType;
        set
        {
            if (ReferenceEquals(value, _preparation?.ResultType)) return;
            Preparation.ResultType = value;
        }
    }

    internal bool DefaultOnEmpty
    {
        get => _preparation?.DefaultOnEmpty ?? false;
        set
        {
            if (value == (_preparation?.DefaultOnEmpty ?? false)) return;
            Preparation.DefaultOnEmpty = value;
        }
    }

    internal bool SingleScalar
    {
        get => _preparation?.SingleScalar ?? false;
        set
        {
            if (value == (_preparation?.SingleScalar ?? false)) return;
            Preparation.SingleScalar = value;
        }
    }

    private QueryPlanEqualityComparer? _queryPlanComparer
    {
        get => _preparation?.QueryPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.QueryPlanComparer)) return;
            Preparation.QueryPlanComparer = value;
        }
    }

    private ExpressionPlanEqualityComparer? _expressionPlanComparer
    {
        get => _preparation?.ExpressionPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.ExpressionPlanComparer)) return;
            Preparation.ExpressionPlanComparer = value;
        }
    }

    private SelectExpressionPlanEqualityComparer? _selectExpressionPlanComparer
    {
        get => _preparation?.SelectExpressionPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.SelectExpressionPlanComparer)) return;
            Preparation.SelectExpressionPlanComparer = value;
        }
    }

    private FromExpressionPlanEqualityComparer? _fromExpressionPlanComparer
    {
        get => _preparation?.FromExpressionPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.FromExpressionPlanComparer)) return;
            Preparation.FromExpressionPlanComparer = value;
        }
    }

    private JoinExpressionPlanEqualityComparer? _joinExpressionPlanComparer
    {
        get => _preparation?.JoinExpressionPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.JoinExpressionPlanComparer)) return;
            Preparation.JoinExpressionPlanComparer = value;
        }
    }

    private SortingExpressionPlanEqualityComparer? _sortingExpressionPlanComparer
    {
        get => _preparation?.SortingExpressionPlanComparer;
        set
        {
            if (ReferenceEquals(value, _preparation?.SortingExpressionPlanComparer)) return;
            Preparation.SortingExpressionPlanComparer = value;
        }
    }

    private QueryPlan? _planKey
    {
        get => _preparation?.PlanKey;
        set
        {
            if (ReferenceEquals(value, _preparation?.PlanKey)) return;
            Preparation.PlanKey = value;
        }
    }

    private string? _planKeySql
    {
        get => _preparation?.PlanKeySql;
        set
        {
            if (string.Equals(value, _preparation?.PlanKeySql, StringComparison.Ordinal)) return;
            Preparation.PlanKeySql = value;
        }
    }

    private int _planKeyInValuesShapeHash
    {
        get => _preparation?.PlanKeyInValuesShapeHash ?? 0;
        set
        {
            if (value == (_preparation?.PlanKeyInValuesShapeHash ?? 0)) return;
            Preparation.PlanKeyInValuesShapeHash = value;
        }
    }

    private int _planKeyPreWhereShapeHash
    {
        get => _preparation?.PlanKeyPreWhereShapeHash ?? 0;
        set
        {
            if (value == (_preparation?.PlanKeyPreWhereShapeHash ?? 0)) return;
            Preparation.PlanKeyPreWhereShapeHash = value;
        }
    }

    /// <summary>
    /// Whether this command instance has ever completed a preparation. Unlike <see cref="_isPrepared"/> it
    /// is not cleared by <see cref="ResetPreparation"/>.
    /// </summary>
    internal bool HasEverPrepared
    {
        get => _binding?.HasEverPrepared ?? false;
        set
        {
            if (value == (_binding?.HasEverPrepared ?? false)) return;
            Binding.HasEverPrepared = value;
        }
    }

    private bool _dontCache
    {
        get => _binding?.DontCache ?? false;
        set
        {
            if (value == (_binding?.DontCache ?? false)) return;
            Binding.DontCache = value;
        }
    }
}
