using System.Text;

namespace NextORM.Core;

/// <summary>
/// Renders the SQL text of a mutation command. The DML analogue of <see cref="SqlBuilder"/>: it turns
/// the command's target table, columns and values into a parameterised statement, resolving the naming
/// convention and identifier quoting for the target provider. Split from the statement model
/// (<see cref="MutationCommand"/>) and from execution (<see cref="QueryExecutor"/>) so each stays
/// cohesive.
/// </summary>
internal static class SqlMutationBuilder
{
    /// <summary>
    /// Renders an <c>INSERT ... VALUES</c> statement for <paramref name="command"/>, returning the SQL
    /// text together with its bound parameters. Column references are emitted verbatim; captured values
    /// become parameters named by <see cref="DefaultParameterProvider"/>.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="namingConvention">Convention applied to auto-derived table/column names, or <see langword="null"/>.</param>
    /// <param name="command">The insert command to render.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <param name="sourceSql">The rendered <c>SELECT</c> of an <c>INSERT ... SELECT</c> command, or <see langword="null"/> for a <c>VALUES</c> insert.</param>
    /// <param name="sourceParameters">The parameters referenced by <paramref name="sourceSql"/>, or <see langword="null"/> for a <c>VALUES</c> insert.</param>
    /// <param name="parameterProvider">The provider that names the literal parameters, or <see langword="null"/> to start a fresh sequence. A data-modifying CTE passes the enclosing command's provider so its parameters keep the whole statement's numbering.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    internal static (string Sql, List<Parameter> Parameters) MakeInsert(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        InsertCommand command,
        KeywordCase keywordCase = KeywordCase.Lower,
        string? sourceSql = null,
        IReadOnlyList<Parameter>? sourceParameters = null,
        IParameterProvider? parameterProvider = null)
    {
        var parameters = new List<Parameter>();
        var writer = StringBuilderPool.Shared.Get();

        try
        {
            writer.Append(SqlKeywords.Of(keywordCase, "insert into "));
            AppendIdentifier(writer, dialect, quoteIdentifiers, ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention));

            var returningColumns = RenderReturningColumns(dialect, quoteIdentifiers, namingConvention, command);

            if (command.Source is not null)
                RenderSourceInsert(writer, dialect, quoteIdentifiers, namingConvention, command, returningColumns, sourceSql, sourceParameters, parameters, keywordCase);
            else if (command.Columns.Count == 0)
                RenderDefaultValuesInsert(writer, dialect, returningColumns, keywordCase);
            else
                RenderValuesInsert(writer, dialect, quoteIdentifiers, namingConvention, command, returningColumns, parameters, keywordCase, parameterProvider);

            return (writer.ToString(), parameters);
        }
        finally
        {
            StringBuilderPool.Shared.Return(writer);
        }
    }

    // INSERT ... SELECT: the target columns are followed by the source query, not by VALUES. The
    // provider-specific return clauses keep their usual placement (OUTPUT before the source,
    // RETURNING after it).
    private static void RenderSourceInsert(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        InsertCommand command,
        IReadOnlyList<string>? returningColumns,
        string? sourceSql,
        IReadOnlyList<Parameter>? sourceParameters,
        List<Parameter> parameters,
        KeywordCase keywordCase)
    {
        if (sourceSql is null || sourceParameters is null)
            throw new BuildSqlCommandException("An INSERT ... SELECT command is missing its rendered source SQL.");

        AppendColumnList(writer, dialect, quoteIdentifiers, namingConvention, command.SourceColumns!);

        if (returningColumns is not null && dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

        writer.Append(' ').Append(sourceSql);

        if (returningColumns is not null && dialect.SupportsReturning)
            writer.Append(dialect.MakeReturning(returningColumns, keywordCase));

        if (sourceParameters.Count > 0)
            parameters.AddRange(sourceParameters);
    }

    // An insert that writes no column at all is an all-defaults row. T-SQL places OUTPUT before the
    // default-values form, the ANSI RETURNING clause after it (MySQL has neither).
    private static void RenderDefaultValuesInsert(
        StringBuilder writer,
        ISqlDialect dialect,
        IReadOnlyList<string>? returningColumns,
        KeywordCase keywordCase)
    {
        if (!dialect.SupportsDefaultValues)
            throw new NotSupportedException($"{dialect.GetType().Name} cannot insert a row that writes only column defaults.");

        if (returningColumns is not null && dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

        writer.Append(dialect.MakeDefaultValues(keywordCase));

        if (returningColumns is not null && dialect.SupportsReturning)
            writer.Append(dialect.MakeReturning(returningColumns, keywordCase));
    }

    private static void RenderValuesInsert(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        InsertCommand command,
        IReadOnlyList<string>? returningColumns,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider? parameterProvider)
    {
        parameterProvider ??= new DefaultParameterProvider();

        AppendColumnList(writer, dialect, quoteIdentifiers, namingConvention, ColumnProperties(command.Columns));

        // T-SQL places OUTPUT between the column list and VALUES; the ANSI RETURNING clause is
        // appended after VALUES (below). The returned column set is either the explicit
        // ReturningColumns of a Returning()/Returning(projection) terminal, or the single
        // identity/key column of the ReturningIdentity/ReturningKey terminals.
        if (returningColumns is not null && dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

        writer.Append(SqlKeywords.Of(keywordCase, " values "));

        AppendValuesRows(writer, dialect, quoteIdentifiers, namingConvention, command.Columns, command.RowCount, parameters, keywordCase, parameterProvider);

        if (returningColumns is not null && dialect.SupportsReturning)
            writer.Append(dialect.MakeReturning(returningColumns, keywordCase));
    }

    /// <summary>
    /// Renders a key upsert for <paramref name="command"/> as the dialect's native form:
    /// <c>INSERT ... ON CONFLICT ... DO UPDATE</c>, <c>INSERT ... ON DUPLICATE KEY UPDATE</c> or
    /// <c>MERGE</c>. The rows are the would-be inserted rows; the matched branch assigns the non-key
    /// columns from the incoming row.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="namingConvention">Convention applied to auto-derived table/column names, or <see langword="null"/>.</param>
    /// <param name="command">The key-upsert command to render.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <param name="parameterProvider">The provider that names the literal parameters, or <see langword="null"/> to start a fresh sequence.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    /// <exception cref="NotSupportedException">The dialect has no <c>ON CONFLICT</c>, <c>ON DUPLICATE KEY</c> or <c>MERGE</c> form.</exception>
    internal static (string Sql, List<Parameter> Parameters) MakeMerge(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        KeywordCase keywordCase = KeywordCase.Lower,
        IParameterProvider? parameterProvider = null)
    {
        parameterProvider ??= new DefaultParameterProvider();
        var parameters = new List<Parameter>();
        var writer = StringBuilderPool.Shared.Get();

        try
        {
            if (dialect.SupportsMerge)
                RenderMerge(writer, dialect, quoteIdentifiers, namingConvention, command, parameters, keywordCase, parameterProvider);
            else if (dialect.SupportsOnConflict || dialect.SupportsOnDuplicateKey)
                RenderOnConflictUpsert(writer, dialect, quoteIdentifiers, namingConvention, command, parameters, keywordCase, parameterProvider);
            else
                throw new NotSupportedException(
                    $"{dialect.GetType().Name} does not support key upsert: it has no ON CONFLICT, ON DUPLICATE KEY or MERGE form.");

            return (writer.ToString(), parameters);
        }
        finally
        {
            StringBuilderPool.Shared.Return(writer);
        }
    }

    /// <summary>
    /// Renders a <c>DELETE</c> statement for <paramref name="command"/>. The row filter is either the
    /// already-rendered <paramref name="whereSql"/> of the predicate form, the declared key equalities of
    /// the <c>Delete(entity)</c> form, or nothing (the explicit <c>All()</c> full-table delete).
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="namingConvention">Convention applied to auto-derived table/column names, or <see langword="null"/>.</param>
    /// <param name="command">The delete command to render.</param>
    /// <param name="whereSql">The rendered condition of the predicate form, or <see langword="null"/>; its parameters are already in <paramref name="parameters"/>.</param>
    /// <param name="parameters">The parameters of <paramref name="whereSql"/>; key values are appended to it.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    internal static (string Sql, List<Parameter> Parameters) MakeDelete(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        DeleteCommand command,
        string? whereSql,
        List<Parameter> parameters,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var writer = StringBuilderPool.Shared.Get();

        try
        {
            var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);
            if (quoteIdentifiers)
                table = dialect.QuoteIdentifier(table);

            writer.Append(dialect.MakeDeleteHead(table, keywordCase));

            var returningColumns = RenderColumnsOrNull(dialect, quoteIdentifiers, namingConvention, command.ReturningColumns);

            // T-SQL places OUTPUT between the target and the filter; the ANSI RETURNING clause is appended
            // after the filter (below). MySQL/MariaDB have neither.
            if (returningColumns is not null && dialect.SupportsOutput)
                writer.Append(dialect.MakeDeletedOutput(returningColumns, keywordCase));

            if (command.Keys is { Count: > 0 } keys)
            {
                var provider = new DefaultParameterProvider();
                writer.Append(SqlKeywords.Of(keywordCase, " where "));

                for (var i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                        writer.Append(SqlKeywords.Of(keywordCase, " and "));

                    var column = RenderColumnReference(dialect, quoteIdentifiers, keys[i].Property, namingConvention);
                    var name = provider.GetParamName();
                    parameters.Add(new Parameter(name, keys[i].Value));
                    writer.Append(column).Append(" = ").Append(dialect.MakeParam(name));
                }
            }
            else if (!string.IsNullOrEmpty(whereSql))
            {
                writer.Append(SqlKeywords.Of(keywordCase, " where ")).Append(whereSql);
            }
            else if (dialect.DeleteRequiresWhere)
            {
                writer.Append(SqlKeywords.Of(keywordCase, " where 1"));
            }

            if (returningColumns is not null && dialect.SupportsReturning)
                writer.Append(dialect.MakeReturning(returningColumns, keywordCase));

            if (dialect.MakeDeleteSuffix(keywordCase) is { } suffix)
                writer.Append(suffix);

            return (writer.ToString(), parameters);
        }
        finally
        {
            StringBuilderPool.Shared.Return(writer);
        }
    }

    /// <summary>
    /// Renders a <c>TRUNCATE TABLE</c> statement for <paramref name="command"/>.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="namingConvention">Convention applied to auto-derived table/column names, or <see langword="null"/>.</param>
    /// <param name="command">The truncate command to render.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered SQL text.</returns>
    internal static string MakeTruncate(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        TruncateCommand command,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);
        if (quoteIdentifiers)
            table = dialect.QuoteIdentifier(table);

        return dialect.MakeTruncate(table, keywordCase);
    }

    // INSERT ... VALUES ... <ON CONFLICT ... DO UPDATE SET> | <ON DUPLICATE KEY UPDATE>, sharing the
    // insert head and the value rows; only the conflict clause and the incoming-value reference differ.
    private static void RenderOnConflictUpsert(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider parameterProvider)
    {
        writer.Append(SqlKeywords.Of(keywordCase, "insert into "));
        AppendIdentifier(writer, dialect, quoteIdentifiers, ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention));
        AppendColumnList(writer, dialect, quoteIdentifiers, namingConvention, ColumnProperties(command.Columns));
        writer.Append(SqlKeywords.Of(keywordCase, " values "));
        AppendValuesRows(writer, dialect, quoteIdentifiers, namingConvention, command.Columns, command.RowCount, parameters, keywordCase, parameterProvider);

        if (dialect.SupportsOnConflict)
            writer.Append(dialect.MakeOnConflict(RenderColumns(dialect, quoteIdentifiers, namingConvention, command.Keys), keywordCase));
        else
            writer.Append(dialect.MakeOnDuplicateKey(keywordCase));

        var updates = RenderColumns(dialect, quoteIdentifiers, namingConvention, command.UpdateColumns);
        for (var i = 0; i < updates.Length; i++)
        {
            if (i > 0)
                writer.Append(", ");

            writer.Append(updates[i]).Append(" = ").Append(dialect.MakeUpsertValueReference(updates[i], keywordCase));
        }
    }

    // MERGE INTO <target> USING (VALUES ...) AS source (<cols>) ON ... WHEN MATCHED THEN UPDATE SET ...
    // WHEN NOT MATCHED THEN INSERT ...; the skeleton is provider-specific and lives on the dialect.
    private static void RenderMerge(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider parameterProvider)
    {
        var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);
        if (quoteIdentifiers)
            table = dialect.QuoteIdentifier(table);

        var columns = RenderColumns(dialect, quoteIdentifiers, namingConvention, ColumnProperties(command.Columns));
        var keys = RenderColumns(dialect, quoteIdentifiers, namingConvention, command.Keys);
        var updates = RenderColumns(dialect, quoteIdentifiers, namingConvention, command.UpdateColumns);

        var rows = StringBuilderPool.Shared.Get();
        try
        {
            AppendValuesRows(rows, dialect, quoteIdentifiers, namingConvention, command.Columns, command.RowCount, parameters, keywordCase, parameterProvider);
            writer.Append(dialect.MakeMerge(table, columns, keys, updates, rows.ToString(), keywordCase));
        }
        finally
        {
            StringBuilderPool.Shared.Return(rows);
        }
    }

    private static IPropertyMetadata[] ColumnProperties(IReadOnlyList<InsertColumn> columns)
    {
        var properties = new IPropertyMetadata[columns.Count];
        for (var c = 0; c < properties.Length; c++)
            properties[c] = columns[c].Property;

        return properties;
    }

    private static string[] RenderColumns(ISqlDialect dialect, bool quoteIdentifiers, INamingConvention? namingConvention, IReadOnlyList<IPropertyMetadata> columns)
    {
        var rendered = new string[columns.Count];
        for (var i = 0; i < rendered.Length; i++)
            rendered[i] = RenderColumnReference(dialect, quoteIdentifiers, columns[i], namingConvention);

        return rendered;
    }

    private static string[]? RenderColumnsOrNull(ISqlDialect dialect, bool quoteIdentifiers, INamingConvention? namingConvention, IReadOnlyList<IPropertyMetadata>? columns)
        => columns is { Count: > 0 } ? RenderColumns(dialect, quoteIdentifiers, namingConvention, columns) : null;

    // One "(<values>)" tuple per row, separated by ", " (without the leading VALUES keyword).
    private static void AppendValuesRows(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        IReadOnlyList<InsertColumn> columns,
        int rowCount,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider parameterProvider)
    {
        for (var r = 0; r < rowCount; r++)
        {
            if (r > 0)
                writer.Append(", ");

            writer.Append('(');
            for (var c = 0; c < columns.Count; c++)
            {
                if (c > 0)
                    writer.Append(", ");

                var value = columns[c].Values[r];
                if (value.IsDefault)
                {
                    if (!dialect.SupportsColumnDefault)
                        throw new NotSupportedException($"{dialect.GetType().Name} cannot write DEFAULT as a value; omit the column instead.");

                    writer.Append(dialect.MakeColumnDefault(keywordCase));
                }
                else if (value.IsColumn)
                {
                    AppendIdentifier(writer, dialect, quoteIdentifiers, ResolveColumnName(value.Column!, namingConvention));
                }
                else
                {
                    var name = parameterProvider.GetParamName();
                    parameters.Add(new Parameter(name, value.Constant));
                    writer.Append(dialect.MakeParam(name));
                }
            }
            writer.Append(')');
        }
    }

    private static string ResolveTableName(string tableName, bool isTableNameAuto, Type entityType, INamingConvention? namingConvention)
        => isTableNameAuto && namingConvention is not null
            ? namingConvention.TableName(tableName, entityType.IsInterface)
            : tableName;

    private static IReadOnlyList<string>? RenderReturningColumns(ISqlDialect dialect, bool quoteIdentifiers, INamingConvention? namingConvention, InsertCommand command)
    {
        if (command.ReturningColumns is { Count: > 0 } columns)
        {
            var rendered = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
                rendered[i] = RenderColumnReference(dialect, quoteIdentifiers, columns[i], namingConvention);

            return rendered;
        }

        return command.IdentityColumn is not null
            ? [RenderColumnReference(dialect, quoteIdentifiers, command.IdentityColumn, namingConvention)]
            : null;
    }

    private static string ResolveColumnName(IPropertyMetadata property, INamingConvention? namingConvention)
        => property.IsColumnNameAuto && namingConvention is not null
            ? namingConvention.ColumnName(property.ColumnName)
            : property.ColumnName;

    private static string RenderColumnReference(ISqlDialect dialect, bool quoteIdentifiers, IPropertyMetadata property, INamingConvention? namingConvention)
    {
        var name = ResolveColumnName(property, namingConvention);
        return quoteIdentifiers ? dialect.QuoteIdentifier(name) : name;
    }

    private static void AppendIdentifier(StringBuilder writer, ISqlDialect dialect, bool quoteIdentifiers, string name)
    {
        if (quoteIdentifiers)
            writer.Append(dialect.QuoteIdentifier(name));
        else
            writer.Append(name);
    }

    private static void AppendColumnList(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        IReadOnlyList<IPropertyMetadata> columns)
    {
        writer.Append(" (");
        for (var c = 0; c < columns.Count; c++)
        {
            if (c > 0)
                writer.Append(", ");

            AppendIdentifier(writer, dialect, quoteIdentifiers, ResolveColumnName(columns[c], namingConvention));
        }
        writer.Append(')');
    }
}
