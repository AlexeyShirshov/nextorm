using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// The statement shape an assignment set is rendered into. The shape decides which roles have a real SQL
/// reference: a general <c>MERGE</c> reads the target alias and the source relation, while a native key
/// upsert reads the target table and the provider's proposed row (<c>excluded</c>) and binds the original
/// input row as statement parameters.
/// </summary>
internal enum MergeAssignmentForm
{
    /// <summary>A general <c>MERGE</c> statement over a derived source relation.</summary>
    FullMerge,

    /// <summary>A native key upsert (<c>ON CONFLICT ... DO UPDATE</c>, <c>ON DUPLICATE KEY UPDATE</c>).</summary>
    NativeUpsert,
}

/// <summary>Which fragment of a statement an assignment set produces.</summary>
internal enum MergeAssignmentFragmentKind
{
    /// <summary>The <c>SET</c> list of an update branch (<c>&lt;target&gt;.&lt;column&gt; = &lt;value&gt;, ...</c>).</summary>
    UpdateSet,

    /// <summary>The column list and the value list of an insert branch.</summary>
    Insert,
}

/// <summary>
/// Everything the assignment renderer needs to resolve the three roles against one statement shape. It is
/// resolved by the caller (the data context), which knows which renderer branch the command will take.
/// </summary>
internal sealed class MergeAssignmentRenderInfo
{
    /// <summary>The statement shape being rendered.</summary>
    public MergeAssignmentForm Form { get; init; }

    /// <summary>Which fragment the set produces.</summary>
    public MergeAssignmentFragmentKind Kind { get; init; }

    /// <summary>The dialect name used in the frozen diagnostics.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>The form name used in the frozen diagnostics (<c>MERGE</c> or <c>key-upsert</c>).</summary>
    public string FormName { get; init; } = "MERGE";

    /// <summary>The SQL that names the target row: the <c>target</c> alias, or the quoted table reference.</summary>
    public string TargetReference { get; init; } = "target";

    /// <summary>Whether the written column is qualified with <see cref="TargetReference"/>.</summary>
    public bool QualifyTarget { get; init; }

    /// <summary>Whether the provider exposes a native proposed-row (<c>excluded</c>) reference.</summary>
    public bool SupportsExcluded { get; init; }

    /// <summary>The single materialized original input row, or <see langword="null"/> when there is none.</summary>
    public object? SourceRow { get; init; }

    /// <summary>The target entity metadata, used to resolve a right-hand-side member to its mapped column.</summary>
    public IEntityMetadata? Metadata { get; init; }

    /// <summary>The active SQL dialect.</summary>
    public ISqlDialect Dialect { get; init; } = null!;

    /// <summary>Whether physical identifiers must be quoted.</summary>
    public bool QuoteIdentifiers { get; init; }

    /// <summary>Convention applied to auto-derived column names, or <see langword="null"/>.</summary>
    public INamingConvention? NamingConvention { get; init; }

    /// <summary>Returns a copy of this shape that produces the other kind of fragment.</summary>
    /// <param name="kind">The fragment kind the copy produces.</param>
    /// <returns>The copy carrying the same roles and render settings.</returns>
    internal MergeAssignmentRenderInfo WithKind(MergeAssignmentFragmentKind kind)
        => new()
        {
            Form = Form,
            Kind = kind,
            ProviderName = ProviderName,
            FormName = FormName,
            TargetReference = TargetReference,
            QualifyTarget = QualifyTarget,
            SupportsExcluded = SupportsExcluded,
            SourceRow = SourceRow,
            Metadata = Metadata,
            Dialect = Dialect,
            QuoteIdentifiers = QuoteIdentifiers,
            NamingConvention = NamingConvention,
        };

    /// <summary>Resolves a CLR property of the target entity to its mapped column metadata.</summary>
    /// <param name="property">The CLR property read through a role.</param>
    /// <returns>The mapped property, or <see langword="null"/> when the metadata does not map it.</returns>
    internal IPropertyMetadata? Mapped(PropertyInfo property)
    {
        if (Metadata is null)
            return null;

        foreach (var candidate in Metadata.Properties)
        {
            if (ReferenceEquals(candidate.PropertyInfo, property))
                return candidate;
        }

        return null;
    }
}

/// <summary>
/// The rendered fragments of one assignment set: the update <c>SET</c> list, or the insert column and value
/// lists (in assignment order). Exactly one of the two shapes is populated.
/// </summary>
/// <param name="SetList">The rendered <c>SET</c> list, or <see langword="null"/> for an insert set.</param>
/// <param name="InsertColumns">The rendered insert column list, or <see langword="null"/> for an update set.</param>
/// <param name="InsertValues">The rendered insert value list, or <see langword="null"/> for an update set.</param>
internal sealed record MergeAssignmentFragment(string? SetList, IReadOnlyList<string>? InsertColumns, IReadOnlyList<string>? InsertValues);

/// <summary>
/// Renders an immutable <see cref="MergeAssignmentSet"/> against the roles of one statement shape. The
/// right-hand side of every assignment is rendered by the expression visitor the caller supplies, so the
/// assignment editing (which role a member read belongs to, which columns are written, the key-upsert
/// column list) stays here while the scalar SQL translation stays in the shared visitor pipeline.
/// </summary>
internal static class MergeAssignmentRenderer
{
    /// <summary>
    /// Renders one assignment set. Role capabilities and the shape of every right-hand side are validated
    /// before any SQL is produced, so an unsupported role or expression fails closed with a named
    /// diagnostic instead of emitting invalid SQL.
    /// </summary>
    /// <param name="assignments">The assignment set to render.</param>
    /// <param name="info">The statement shape the set is rendered into.</param>
    /// <param name="renderValue">Renders one right-hand-side expression to SQL, binding its parameters.</param>
    /// <returns>The rendered fragment.</returns>
    /// <exception cref="NotSupportedException">A used role has no reference in this statement shape, or a right-hand side cannot be translated.</exception>
    internal static MergeAssignmentFragment Render(
        MergeAssignmentSet assignments,
        MergeAssignmentRenderInfo info,
        Func<Expression, string> renderValue)
    {
        ValidateRoles(assignments, info);

        if (info.Kind == MergeAssignmentFragmentKind.Insert)
        {
            var columns = new string[assignments.Count];
            var values = new string[assignments.Count];
            for (var i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments.Assignments[i];
                ValidateValueShape(assignment);
                columns[i] = RenderColumn(assignment.Property, info);
                values[i] = renderValue(assignment.Value);
            }

            return new MergeAssignmentFragment(null, columns, values);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < assignments.Count; i++)
        {
            var assignment = assignments.Assignments[i];
            ValidateValueShape(assignment);
            if (i > 0)
                builder.Append(", ");

            if (info.QualifyTarget)
                builder.Append(info.TargetReference).Append('.');

            builder.Append(RenderColumn(assignment.Property, info)).Append(" = ").Append(renderValue(assignment.Value));
        }

        return new MergeAssignmentFragment(builder.ToString(), null, null);
    }

    /// <summary>
    /// Rejects a set that reads a role the statement shape cannot express. The provider capability is decided
    /// from the declared dialect form only, so a provider without a native proposed row fails closed instead
    /// of producing a reference that does not exist.
    /// </summary>
    /// <param name="assignments">The assignment set.</param>
    /// <param name="info">The statement shape.</param>
    /// <exception cref="NotSupportedException">A used role has no reference in this shape.</exception>
    private static void ValidateRoles(MergeAssignmentSet assignments, MergeAssignmentRenderInfo info)
    {
        if (assignments.UsesExcluded && !info.SupportsExcluded)
            throw new NotSupportedException(
                $"Provider '{info.ProviderName}' does not support role 'excluded' in {info.FormName} assignments.");

        if (info.Form == MergeAssignmentForm.NativeUpsert && assignments.UsesSource && info.SourceRow is null)
            throw new NotSupportedException(
                $"Provider '{info.ProviderName}' requires one materialized source row for original-source references in key-upsert UPDATE assignments.");
    }

    /// <summary>
    /// Fails closed on a right-hand side that is not a single-row scalar expression: a nested query or an
    /// aggregate would execute per statement with an unrelated row set, and a nested object initializer has no
    /// column form.
    /// </summary>
    /// <param name="assignment">The assignment whose value is checked.</param>
    /// <exception cref="NotSupportedException">The value contains an unsupported shape.</exception>
    private static void ValidateValueShape(MergeAssignment assignment)
    {
        var unsupported = MergeAssignmentShapeGuard.Find(assignment.Value);
        if (unsupported is null)
            return;

        throw new NotSupportedException(
            $"MERGE assignment for '{assignment.Property.PropertyInfo.Name}' cannot translate expression '{unsupported}'.");
    }

    /// <summary>Renders the written column of an assignment.</summary>
    /// <param name="property">The mapped destination property.</param>
    /// <param name="info">The statement shape.</param>
    /// <returns>The quoted column reference.</returns>
    private static string RenderColumn(IPropertyMetadata property, MergeAssignmentRenderInfo info)
        => SqlMutationBuilder.RenderColumnReference(info.Dialect, info.QuoteIdentifiers, property, info.NamingConvention);
}

/// <summary>
/// Detects the right-hand-side shapes that have no single-row scalar form: a nested query or aggregate, a
/// lambda, and a nested object initializer. The walk reports the most specific description it finds so the
/// frozen diagnostic names the actual node kind.
/// </summary>
internal sealed class MergeAssignmentShapeGuard : ExpressionVisitor
{
    private string? _found;

    /// <summary>Finds the first unsupported shape of an expression.</summary>
    /// <param name="expression">The right-hand side to inspect.</param>
    /// <returns>The description of the offending node, or <see langword="null"/> when the shape is supported.</returns>
    internal static string? Find(Expression expression)
    {
        var guard = new MergeAssignmentShapeGuard();
        guard.Visit(expression);
        return guard._found;
    }

    /// <inheritdoc/>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _found ??= "Lambda";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitInvocation(InvocationExpression node)
    {
        _found ??= "Invoke";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // A value-type construction is folded to a parameter by the scalar visitor; a reference-typed object
        // graph is a row shape and has no column form.
        if (!node.Type.IsValueType)
            _found ??= "MemberInit";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitListInit(ListInitExpression node)
    {
        _found ??= "ListInit";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitNew(NewExpression node)
    {
        if (!node.Type.IsValueType)
            _found ??= "New";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        _found ??= "NewArray";
        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // A LINQ operator is a nested query or an aggregate over one; neither is a scalar of this row.
        var declaringType = node.Method.DeclaringType;
        if (declaringType == typeof(Queryable) || declaringType == typeof(Enumerable))
            _found ??= $"MethodCall '{node.Method.Name}'";

        return base.VisitMethodCall(node);
    }
}

/// <summary>
/// The scalar visitor of a MERGE assignment right-hand side. It resolves a member read through a role
/// parameter to that role's SQL reference — the target row, the source relation, the provider's proposed row,
/// or a bound parameter holding the original input value of a native key upsert — and binds every literal as
/// a statement parameter instead of inlining it.
/// </summary>
internal sealed class MergeAssignmentValueVisitor : BaseExpressionVisitor
{
    private readonly MergeAssignmentSet _assignments;
    private readonly MergeAssignmentRenderInfo _info;
    private readonly IParameterProvider _parameterProvider;

    /// <summary>Creates the visitor.</summary>
    /// <param name="options">The visitor collaborators (dialect, columns, parameter list, quoting).</param>
    /// <param name="assignments">The assignment set whose bindings define the roles.</param>
    /// <param name="info">The statement shape the set is rendered into.</param>
    public MergeAssignmentValueVisitor(VisitorOptions options, MergeAssignmentSet assignments, MergeAssignmentRenderInfo info)
        : base(options)
    {
        _assignments = assignments;
        _info = info;
        _parameterProvider = options.ParameterProvider;
    }

    /// <summary>
    /// Clones into another role-aware visitor. A nested render (a function argument, for example) goes
    /// through <see cref="BaseExpressionVisitor.VisitToString"/>, which clones; the base clone would drop
    /// the role bindings and render a role read as a plain target column.
    /// </summary>
    /// <returns>A fresh visitor over the same assignment set and statement shape.</returns>
    public override BaseExpressionVisitor Clone()
    {
        if (IsParamMode)
            throw new NotSupportedException("Cannot clone in param mode");

        var clone = new MergeAssignmentValueVisitor(Options, _assignments, _info)
        {
            SuppressColumnCollation = SuppressColumnCollation,
            ConverterContext = ConverterContext,
        };
        Adopt(clone);
        return clone;
    }

    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member is PropertyInfo property && node.Expression is ParameterExpression parameter
            && _assignments.RoleOf(parameter) is { } role)
        {
            switch (role)
            {
                case MergeAssignmentRole.Target:
                    AppendRoleReference(_info.TargetReference, property);
                    return node;

                case MergeAssignmentRole.Source:
                    if (_info.Form == MergeAssignmentForm.FullMerge)
                    {
                        AppendRoleReference("source", property);
                        return node;
                    }

                    AppendSourceParameter(node, property);
                    return node;

                default:
                    AppendExcludedReference(property);
                    return node;
            }
        }

        return base.VisitMember(node);
    }

    /// <inheritdoc/>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        // An assignment value is a stored value, never an inline literal: the provider binds it as a
        // statement parameter, and a CLR null stays a null parameter value (see R08).
        var parameter = TryAddCapturedParameter(_parameterProvider.GetParamName(), node.Value, node);
        EmitParameter(parameter);
        return node;
    }

    /// <summary>Appends <c>&lt;reference&gt;.&lt;column&gt;</c> for the target row or the source relation.</summary>
    /// <param name="reference">The SQL reference naming the row.</param>
    /// <param name="property">The mapped property read through the role.</param>
    private void AppendRoleReference(string reference, PropertyInfo property)
    {
        _builder!.Append(reference).Append('.').Append(RenderColumn(property));
    }

    /// <summary>
    /// Appends the provider's native proposed-row reference (<c>excluded.&lt;column&gt;</c>). The renderer has
    /// already refused a shape without one, so this branch only exists on a provider that has it.
    /// </summary>
    /// <param name="property">The mapped property read through the excluded role.</param>
    private void AppendExcludedReference(PropertyInfo property)
    {
        _builder!.Append(Dialect.MakeUpsertValueReference(RenderColumn(property), KeywordCase));
    }

    /// <summary>
    /// Binds the original input value of a native key upsert as a statement parameter. The value is read from
    /// the single materialized source row (before any insert expression), converted through the same parameter
    /// normalization as a captured value, and never inlined.
    /// </summary>
    /// <param name="node">The member read being replaced.</param>
    /// <param name="property">The mapped property read through the source role.</param>
    private void AppendSourceParameter(MemberExpression node, PropertyInfo property)
    {
        // The renderer has already refused a source reference without a single materialized row.
        var value = property.GetValue(_info.SourceRow);
        var parameter = TryAddCapturedParameter(_parameterProvider.GetParamName(), value, node);
        EmitParameter(parameter);
    }

    /// <summary>Renders the column of a mapped property read through a role.</summary>
    /// <param name="property">The CLR property read through the role.</param>
    /// <returns>The quoted physical column.</returns>
    private string RenderColumn(PropertyInfo property)
    {
        var mapped = _info.Mapped(property);
        if (mapped is not null)
            return SqlMutationBuilder.RenderColumnReference(Dialect, Options.QuoteIdentifiers, mapped, Options.NamingConvention);

        var name = property.GetPropertyColumnName(Options.NamingConvention, Options.EntityType);
        return Options.QuoteIdentifiers ? Dialect.QuoteIdentifier(name) : name;
    }
}
