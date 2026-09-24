namespace NextORM.Core;

/// <summary>
/// The kind of statement a <see cref="MutationCommand"/> represents.
/// </summary>
internal enum SqlStatementType
{
    /// <summary>A <c>SELECT</c> query (rendered by <see cref="SqlBuilder"/>, not a mutation).</summary>
    Select,
    /// <summary>An <c>INSERT</c> statement.</summary>
    Insert,
    /// <summary>An <c>UPDATE</c> statement.</summary>
    Update,
    /// <summary>A multi-table <c>UPDATE</c> that changes rows of the target based on a join.</summary>
    UpdateJoin,
    /// <summary>A <c>DELETE</c> statement.</summary>
    Delete,
    /// <summary>A multi-table <c>DELETE</c> that removes rows of the target based on a join.</summary>
    DeleteJoin,
    /// <summary>A <c>MERGE</c> statement.</summary>
    Merge,
    /// <summary>A <c>TRUNCATE TABLE</c> statement.</summary>
    Truncate,
    /// <summary>A <c>CREATE [TEMPORARY] TABLE ... AS SELECT</c> statement.</summary>
    CreateTableAsSelect,
}

/// <summary>
/// Base of the DML command axis. Mutations do not derive from <see cref="QueryCommand"/>: a query is
/// bound to result materialisation (compiled mapper, result-set enumerator), while a mutation only needs
/// the affected-row count and, optionally, a returned scalar. An insert may still <em>embed</em> a
/// <see cref="QueryCommand"/> as its source (<c>INSERT ... SELECT</c>). Keeping the axes separate leaves
/// the hot <c>SELECT</c> path untouched.
/// </summary>
internal abstract class MutationCommand
{
    /// <summary>Creates a mutation command of <paramref name="statementType"/> over <paramref name="entityType"/>.</summary>
    protected MutationCommand(SqlStatementType statementType, Type entityType)
    {
        StatementType = statementType;
        EntityType = entityType;
    }

    /// <summary>The kind of statement this command represents.</summary>
    public SqlStatementType StatementType { get; }

    /// <summary>The CLR entity type targeted by the mutation.</summary>
    public Type EntityType { get; }

    /// <summary>
    /// The mapped columns the statement returns through <c>RETURNING</c>/<c>OUTPUT</c>, or
    /// <see langword="null"/> when the statement only reports the affected-row count. Overridden by the
    /// commands whose builders expose a <c>Returning</c> terminal.
    /// </summary>
    public virtual IReadOnlyList<IPropertyMetadata>? ReturningColumns => null;
}

/// <summary>One column written by an <see cref="InsertCommand"/>: its mapping plus the value of each row.</summary>
internal sealed class InsertColumn
{
    /// <summary>Creates a column binding.</summary>
    /// <param name="property">The mapped property written by this column.</param>
    /// <param name="values">The per-row values, one entry for each inserted row.</param>
    public InsertColumn(IPropertyMetadata property, IReadOnlyList<InsertValue> values)
    {
        Property = property;
        Values = values;
    }

    /// <summary>The mapped property written by this column.</summary>
    public IPropertyMetadata Property { get; }

    /// <summary>The per-row values, in row order.</summary>
    public IReadOnlyList<InsertValue> Values { get; }
}

/// <summary>
/// A single value written by an insert: a captured CLR constant (bound as a parameter), a reference to
/// another mapped column of the same entity, or the column's database <c>DEFAULT</c>.
/// </summary>
internal sealed class InsertValue
{
    private InsertValue(object? constant, IPropertyMetadata? column, bool isDefault)
    {
        Constant = constant;
        Column = column;
        IsDefault = isDefault;
    }

    /// <summary>Creates a value bound as a query parameter.</summary>
    /// <param name="value">The captured CLR value, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    public static InsertValue FromConstant(object? value) => new(value, null, false);

    /// <summary>Creates a value that references another mapped column of the entity.</summary>
    /// <param name="column">The referenced column.</param>
    public static InsertValue FromColumn(IPropertyMetadata column) => new(null, column, false);

    /// <summary>Creates a value that writes the column's database <c>DEFAULT</c>.</summary>
    public static InsertValue FromDefault() => new(null, null, true);

    /// <summary>The captured CLR value when this is a parameter value; otherwise <see langword="null"/>.</summary>
    public object? Constant { get; }

    /// <summary>The referenced column when this is a column reference; otherwise <see langword="null"/>.</summary>
    public IPropertyMetadata? Column { get; }

    /// <summary>Whether this value references another column rather than carrying a parameter value.</summary>
    public bool IsColumn => Column is not null;

    /// <summary>Whether this value writes the column's database <c>DEFAULT</c>.</summary>
    public bool IsDefault { get; }
}

/// <summary>
/// An <c>INSERT ... VALUES</c> command: the target table, the written columns and their per-row values,
/// and optionally the identity column whose generated value should be returned.
/// </summary>
internal sealed class InsertCommand : MutationCommand
{
    /// <summary>Creates an insert command.</summary>
    /// <param name="entityType">The CLR entity type being inserted.</param>
    /// <param name="tableName">The mapped (still unconventioned, unquoted) table name.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    /// <param name="columns">The written columns.</param>
    /// <param name="rowCount">The number of inserted rows.</param>
    /// <param name="identityColumn">The identity column to return, or <see langword="null"/> for a plain insert.</param>
    /// <param name="returningColumns">The columns to return through <c>RETURNING</c>/<c>OUTPUT</c>, or <see langword="null"/> for a plain insert.</param>
    /// <param name="source">The server-side <c>SELECT</c> the rows are read from, or <see langword="null"/> for a <c>VALUES</c> insert.</param>
    /// <param name="sourceColumns">The target columns written from <paramref name="source"/>, or <see langword="null"/> for a <c>VALUES</c> insert.</param>
    /// <param name="ignoreConflicts">Whether rows that violate a unique constraint should be skipped through the dialect's ignore form.</param>
    /// <param name="keepIdentity">Whether explicit values are written to identity columns.</param>
    public InsertCommand(Type entityType, string tableName, bool isTableNameAuto, IReadOnlyList<InsertColumn> columns, int rowCount, IPropertyMetadata? identityColumn, IReadOnlyList<IPropertyMetadata>? returningColumns = null, QueryCommand? source = null, IReadOnlyList<IPropertyMetadata>? sourceColumns = null, bool ignoreConflicts = false, bool keepIdentity = false)
        : base(SqlStatementType.Insert, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
        Columns = columns;
        RowCount = rowCount;
        IdentityColumn = identityColumn;
        ReturningColumns = returningColumns;
        Source = source;
        SourceColumns = sourceColumns;
        IgnoreConflicts = ignoreConflicts;
        KeepIdentity = keepIdentity;
    }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }
    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }
    /// <summary>The written columns.</summary>
    public IReadOnlyList<InsertColumn> Columns { get; }
    /// <summary>The number of inserted rows.</summary>
    public int RowCount { get; }
    /// <summary>The identity column to return, or <see langword="null"/> for a plain insert.</summary>
    public IPropertyMetadata? IdentityColumn { get; }

    /// <summary>
    /// The mapped columns the statement returns through <c>RETURNING</c>/<c>OUTPUT</c>, or
    /// <see langword="null"/> for a plain insert. Mutually exclusive with <see cref="IdentityColumn"/>
    /// in practice: they are produced by different terminals.
    /// </summary>
    public override IReadOnlyList<IPropertyMetadata>? ReturningColumns { get; }

    /// <summary>
    /// The server-side <c>SELECT</c> whose rows are inserted (<c>INSERT ... SELECT</c>), or
    /// <see langword="null"/> for a <c>VALUES</c> insert. When set, <see cref="Columns"/> is empty and
    /// <see cref="SourceColumns"/> lists the written columns.
    /// </summary>
    public QueryCommand? Source { get; }

    /// <summary>The written columns of an <see cref="Source"/> insert, in select-list order; otherwise <see langword="null"/>.</summary>
    public IReadOnlyList<IPropertyMetadata>? SourceColumns { get; }

    /// <summary>
    /// Whether rows that violate a unique constraint should be skipped through the dialect's ignore
    /// form (<c>INSERT OR IGNORE</c>, <c>INSERT IGNORE</c>, <c>ON CONFLICT DO NOTHING</c>). Used by the
    /// bulk-insert portable path; a plain insert leaves it <see langword="false"/>.
    /// </summary>
    public bool IgnoreConflicts { get; }

    /// <summary>
    /// Whether explicit values are written to identity columns (<c>SET IDENTITY_INSERT</c>,
    /// <c>OVERRIDING SYSTEM VALUE</c>). Used by the bulk-insert portable path; a plain insert leaves it
    /// <see langword="false"/> because identity columns are excluded from the written set.
    /// </summary>
    public bool KeepIdentity { get; }
}
