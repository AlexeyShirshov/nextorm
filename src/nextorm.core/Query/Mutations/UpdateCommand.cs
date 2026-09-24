using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>How an <see cref="UpdateAssignment"/> produces the value written to its column.</summary>
internal enum UpdateValueKind
{
    /// <summary>A captured CLR constant, bound as a query parameter.</summary>
    Constant,
    /// <summary>A reference to another mapped column of the same entity.</summary>
    Column,
    /// <summary>An arbitrary value expression, rendered by the shared expression pipeline.</summary>
    Expression,
}

/// <summary>
/// One <c>SET column = value</c> assignment of an <see cref="UpdateCommand"/>. The right-hand side is a
/// captured constant (bound as a parameter), a reference to another mapped column of the same entity, or
/// an arbitrary expression rendered through the same translator as a <c>SELECT</c> projection.
/// </summary>
internal sealed class UpdateAssignment
{
    private UpdateAssignment(IPropertyMetadata property, UpdateValueKind kind, object? constant, IPropertyMetadata? column, Expression? expression)
    {
        Property = property;
        Kind = kind;
        Constant = constant;
        Column = column;
        Expression = expression;
    }

    /// <summary>Creates an assignment writing a captured constant value, bound as a parameter.</summary>
    /// <param name="property">The written column.</param>
    /// <param name="value">The captured CLR value, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    public static UpdateAssignment FromConstant(IPropertyMetadata property, object? value) => new(property, UpdateValueKind.Constant, value, null, null);

    /// <summary>Creates an assignment copying another mapped column of the same entity.</summary>
    /// <param name="property">The written column.</param>
    /// <param name="column">The referenced column.</param>
    public static UpdateAssignment FromColumn(IPropertyMetadata property, IPropertyMetadata column) => new(property, UpdateValueKind.Column, null, column, null);

    /// <summary>Creates an assignment writing an arbitrary value expression.</summary>
    /// <param name="property">The written column.</param>
    /// <param name="expression">The value expression, rendered by the shared expression pipeline.</param>
    public static UpdateAssignment FromExpression(IPropertyMetadata property, Expression expression) => new(property, UpdateValueKind.Expression, null, null, expression);

    /// <summary>The written column.</summary>
    public IPropertyMetadata Property { get; }

    /// <summary>How the right-hand side produces its value.</summary>
    public UpdateValueKind Kind { get; }

    /// <summary>The captured CLR value when <see cref="Kind"/> is <see cref="UpdateValueKind.Constant"/>; otherwise <see langword="null"/>.</summary>
    public object? Constant { get; }

    /// <summary>The referenced column when <see cref="Kind"/> is <see cref="UpdateValueKind.Column"/>; otherwise <see langword="null"/>.</summary>
    public IPropertyMetadata? Column { get; }

    /// <summary>The value expression when <see cref="Kind"/> is <see cref="UpdateValueKind.Expression"/>; otherwise <see langword="null"/>.</summary>
    public Expression? Expression { get; }
}

/// <summary>
/// An <c>UPDATE</c> command: the target table, the <c>SET</c> assignments, and exactly one of the two
/// row filters — a prepared predicate (<see cref="Source"/>, the <c>Where(...)</c> form) or the declared
/// key values (<see cref="Keys"/>, the <c>Update(entity)</c> form). A command whose predicate is empty
/// and that carries no keys updates every row of the table.
/// </summary>
internal sealed class UpdateCommand : MutationCommand
{
    /// <summary>Creates an update command.</summary>
    /// <param name="entityType">The CLR entity type being updated.</param>
    /// <param name="tableName">The mapped (still unconventioned, unquoted) table name.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    /// <param name="assignments">The <c>SET</c> assignments.</param>
    /// <param name="source">The command whose prepared condition forms the <c>WHERE</c> (the <c>Where(...)</c> form) and whose mapping/parameters drive the assignments.</param>
    /// <param name="keys">The declared key values of the <c>Update(entity)</c> form, or <see langword="null"/> for the predicate form.</param>
    /// <param name="returningColumns">The mapped columns to return through <c>RETURNING</c>/<c>OUTPUT</c>, or <see langword="null"/> for a plain update.</param>
    public UpdateCommand(Type entityType, string tableName, bool isTableNameAuto, IReadOnlyList<UpdateAssignment> assignments, QueryCommand source, IReadOnlyList<KeyValue>? keys, IReadOnlyList<IPropertyMetadata>? returningColumns = null)
        : base(SqlStatementType.Update, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Assignments = assignments;
        Source = source;
        Keys = keys;
        ReturningColumns = returningColumns;
    }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }

    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }

    /// <summary>The <c>SET</c> assignments.</summary>
    public IReadOnlyList<UpdateAssignment> Assignments { get; }

    /// <summary>The predicate command whose prepared condition forms the <c>WHERE</c>, and whose mapping drives the assignments.</summary>
    public QueryCommand Source { get; }

    /// <summary>The declared key values rendered as equality predicates, or <see langword="null"/> for the predicate form.</summary>
    public IReadOnlyList<KeyValue>? Keys { get; }

    /// <summary>
    /// The mapped columns the statement returns through <c>RETURNING</c>/<c>OUTPUT</c>, or
    /// <see langword="null"/> for a plain update that only reports the affected-row count.
    /// </summary>
    public override IReadOnlyList<IPropertyMetadata>? ReturningColumns { get; }
}
