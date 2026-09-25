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
            var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);

            var ignoreViaPrefix = command.IgnoreConflicts && dialect.SupportsInsertIgnore;
            if (command.IgnoreConflicts && !ignoreViaPrefix && !dialect.SupportsOnConflictDoNothing)
                throw new NotSupportedException(
                    $"{dialect.GetType().Name} cannot skip conflicting rows: it has neither an INSERT OR IGNORE/INSERT IGNORE form nor ON CONFLICT DO NOTHING.");

            var emitConflictDoNothing = command.IgnoreConflicts && !ignoreViaPrefix;
            var overrideIdentity = command.KeepIdentity ? dialect.MakeOverridingSystemValue(keywordCase) : string.Empty;

            writer.Append(ignoreViaPrefix ? dialect.MakeInsertIgnoreInto(keywordCase) : SqlKeywords.Of(keywordCase, "insert into "));
            AppendIdentifier(writer, dialect, quoteIdentifiers, table);

            var returningColumns = RenderReturningColumns(dialect, quoteIdentifiers, namingConvention, command);

            if (command.Source is not null)
                RenderSourceInsert(writer, dialect, quoteIdentifiers, namingConvention, command, returningColumns, sourceSql, sourceParameters, parameters, keywordCase, overrideIdentity, emitConflictDoNothing);
            else if (command.Columns.Count == 0)
                RenderDefaultValuesInsert(writer, dialect, returningColumns, keywordCase);
            else
                RenderValuesInsert(writer, dialect, quoteIdentifiers, namingConvention, command, returningColumns, parameters, keywordCase, parameterProvider, overrideIdentity, emitConflictDoNothing);

            var sql = writer.ToString();

            if (command.KeepIdentity && dialect.RequiresIdentityInsertToggle)
                sql = dialect.MakeIdentityInsertOn(table, keywordCase) + "; " + sql + "; " + dialect.MakeIdentityInsertOff(table, keywordCase);

            return (sql, parameters);
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
        KeywordCase keywordCase,
        string overrideIdentity,
        bool emitConflictDoNothing)
    {
        if (sourceSql is null || sourceParameters is null)
            throw new BuildSqlCommandException("An INSERT ... SELECT command is missing its rendered source SQL.");

        AppendColumnList(writer, dialect, quoteIdentifiers, namingConvention, command.SourceColumns!);

        if (overrideIdentity.Length > 0)
            writer.Append(overrideIdentity);

        if (returningColumns is not null && dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

        writer.Append(' ').Append(sourceSql);

        if (emitConflictDoNothing)
            writer.Append(dialect.MakeOnConflictDoNothing(keywordCase));

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
        IParameterProvider? parameterProvider,
        string overrideIdentity,
        bool emitConflictDoNothing)
    {
        parameterProvider ??= new DefaultParameterProvider();

        AppendColumnList(writer, dialect, quoteIdentifiers, namingConvention, ColumnProperties(command.Columns));

        if (overrideIdentity.Length > 0)
            writer.Append(overrideIdentity);

        // T-SQL places OUTPUT between the column list and VALUES; the ANSI RETURNING clause is
        // appended after VALUES (below). The returned column set is either the explicit
        // ReturningColumns of a Returning()/Returning(projection) terminal, or the single
        // identity/key column of the ReturningIdentity/ReturningKey terminals.
        if (returningColumns is not null && dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

        writer.Append(SqlKeywords.Of(keywordCase, " values "));

        AppendValuesRows(writer, dialect, quoteIdentifiers, namingConvention, command.Columns, command.RowCount, parameters, keywordCase, parameterProvider);

        if (emitConflictDoNothing)
            writer.Append(dialect.MakeOnConflictDoNothing(keywordCase));

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
    /// <param name="sourceSql">The rendered <c>SELECT</c> of a query-sourced full <c>MERGE</c> (<c>USING (&lt;select&gt;) AS source</c>), or <see langword="null"/> for a <c>VALUES</c> source.</param>
    /// <param name="sourceParameters">The parameters referenced by <paramref name="sourceSql"/>, or <see langword="null"/>.</param>
    /// <param name="parameters">The shared parameter accumulator (branch conditions and <c>VALUES</c> rows), or <see langword="null"/> to start a new one.</param>
    /// <param name="matchConditionSql">The rendered <c>ON &lt;condition&gt;</c> of an explicit match, or <see langword="null"/> to match on the keys.</param>
    /// <param name="branchConditions">The rendered <c>AND &lt;condition&gt;</c> of each branch in order, or <see langword="null"/>.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    /// <exception cref="NotSupportedException">The dialect has no <c>ON CONFLICT</c>, <c>ON DUPLICATE KEY</c> or <c>MERGE</c> form.</exception>
    internal static (string Sql, List<Parameter> Parameters) MakeMerge(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        KeywordCase keywordCase = KeywordCase.Lower,
        IParameterProvider? parameterProvider = null,
        string? sourceSql = null,
        IReadOnlyList<Parameter>? sourceParameters = null,
        List<Parameter>? parameters = null,
        string? matchConditionSql = null,
        IReadOnlyList<string?>? branchConditions = null)
    {
        parameterProvider ??= new DefaultParameterProvider();
        parameters ??= new List<Parameter>();
        var writer = StringBuilderPool.Shared.Get();

        try
        {
            if (command.Branches is not null)
                RenderFullMerge(writer, dialect, quoteIdentifiers, namingConvention, command, parameters, keywordCase, parameterProvider, sourceSql, sourceParameters, matchConditionSql, branchConditions);
            else if (dialect.SupportsMerge)
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
    /// <param name="parameterProvider">The provider that names the key parameters, or <see langword="null"/> to start a fresh sequence. A batch passes the shared provider so its parameters do not collide with the other statements'.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    internal static (string Sql, List<Parameter> Parameters) MakeDelete(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        DeleteCommand command,
        string? whereSql,
        List<Parameter> parameters,
        KeywordCase keywordCase = KeywordCase.Lower,
        IParameterProvider? parameterProvider = null)
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
                var provider = parameterProvider ?? new DefaultParameterProvider();
                writer.Append(SqlKeywords.Of(keywordCase, " where "));

                for (var i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                        writer.Append(SqlKeywords.Of(keywordCase, " and "));

                    var column = RenderColumnReference(dialect, quoteIdentifiers, keys[i].Property, namingConvention);
                    var name = provider.GetParamName();
                    parameters.Add(new Parameter(name, DurationStorage.ToParameterValue(keys[i].Value, keys[i].Property, dialect)));
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
    /// Renders an <c>UPDATE</c> statement for <paramref name="command"/>. The <c>SET</c> list arrives
    /// already rendered (its parameters are in <paramref name="parameters"/>); the row filter is either
    /// the already-rendered <paramref name="whereSql"/> of the predicate form, the declared key
    /// equalities of the <c>Update(entity)</c> form, or nothing when no predicate was given (the whole
    /// table is updated).
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="namingConvention">Convention applied to auto-derived table/column names, or <see langword="null"/>.</param>
    /// <param name="command">The update command to render.</param>
    /// <param name="setSql">The rendered <c>&lt;column&gt; = &lt;value&gt;</c> list, without the <c>SET</c> keyword.</param>
    /// <param name="parameters">The parameters of <paramref name="setSql"/> and of <paramref name="whereSql"/>; key values are appended to it.</param>
    /// <param name="whereSql">The rendered condition of the predicate form, or <see langword="null"/>.</param>
    /// <param name="parameterProvider">The provider that names the key parameters; shared with the <c>SET</c>/<c>WHERE</c> renderers so parameter names do not collide.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    internal static (string Sql, List<Parameter> Parameters) MakeUpdate(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        UpdateCommand command,
        string setSql,
        List<Parameter> parameters,
        string? whereSql,
        IParameterProvider parameterProvider,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var writer = StringBuilderPool.Shared.Get();

        try
        {
            var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);
            if (quoteIdentifiers)
                table = dialect.QuoteIdentifier(table);

            writer.Append(dialect.MakeUpdateHead(table, keywordCase));
            writer.Append(setSql);

            var returningColumns = RenderColumnsOrNull(dialect, quoteIdentifiers, namingConvention, command.ReturningColumns);

            // T-SQL places OUTPUT between the SET list and the filter; the ANSI RETURNING clause is
            // appended at the very end (below). MySQL/MariaDB have neither.
            if (returningColumns is not null && dialect.SupportsOutput)
                writer.Append(dialect.MakeOutput(returningColumns, keywordCase));

            if (command.Keys is { Count: > 0 } keys)
            {
                writer.Append(SqlKeywords.Of(keywordCase, " where "));

                for (var i = 0; i < keys.Count; i++)
                {
                    if (i > 0)
                        writer.Append(SqlKeywords.Of(keywordCase, " and "));

                    var column = RenderColumnReference(dialect, quoteIdentifiers, keys[i].Property, namingConvention);
                    var name = parameterProvider.GetParamName();
                    parameters.Add(new Parameter(name, DurationStorage.ToParameterValue(keys[i].Value, keys[i].Property, dialect)));
                    writer.Append(column).Append(" = ").Append(dialect.MakeParam(name));
                }
            }
            else if (!string.IsNullOrEmpty(whereSql))
            {
                writer.Append(SqlKeywords.Of(keywordCase, " where ")).Append(whereSql);
            }
            else if (dialect.UpdateRequiresWhere)
            {
                writer.Append(SqlKeywords.Of(keywordCase, " where 1"));
            }

            if (returningColumns is not null && dialect.SupportsReturning)
                writer.Append(dialect.MakeReturning(returningColumns, keywordCase));

            if (dialect.MakeUpdateSuffix(keywordCase) is { } suffix)
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

    /// <summary>
    /// Validates a materialisation command against the dialect's capability flags, resolves the quoted
    /// target and column list, and — for a dialect that renders <c>SELECT ... INTO</c> — returns the
    /// <c>INTO</c> clause to inject into the top-level select list (otherwise <see langword="null"/>).
    /// The optional parts are rejected up front so the user gets an actionable error instead of invalid SQL.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="command">The materialisation command to render.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The <c>INTO</c> clause for a <c>SELECT ... INTO</c> dialect, or <see langword="null"/>.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the materialisation or one of the requested options.</exception>
    internal static string? ResolveCreateTableAsInto(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        CreateTableAsCommand command,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        ValidateCreateTableAs(dialect, command);
        if (!dialect.CreateTableAsSelectUsesSelectInto)
            return null;

        return dialect.MakeCreateTableAsSelectInto(BuildCreateTableAsClause(dialect, quoteIdentifiers, command), keywordCase);
    }

    /// <summary>
    /// Renders a materialisation statement for <paramref name="command"/>: the body query — its own
    /// <c>WITH</c> clause placed before it — is wrapped in the dialect's native form over the raw,
    /// optionally quoted target table. A dialect that renders <c>SELECT ... INTO</c> has already had the
    /// clause injected into the body by <see cref="ResolveCreateTableAsInto"/>, so its body is returned
    /// as is.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="quoteIdentifiers">Whether physical identifiers must be quoted.</param>
    /// <param name="withSql">The body's <c>WITH</c> clause, or <see langword="null"/> when it declares no CTEs.</param>
    /// <param name="sourceSql">The rendered body query.</param>
    /// <param name="sourceParameters">The parameters referenced by the body query.</param>
    /// <param name="command">The materialisation command to render.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered SQL and the parameters it references.</returns>
    /// <exception cref="NotSupportedException">The dialect cannot express the materialisation or one of the requested options.</exception>
    internal static (string Sql, List<Parameter> Parameters) MakeCreateTableAsSelect(
        ISqlDialect dialect,
        bool quoteIdentifiers,
        string? withSql,
        string sourceSql,
        IReadOnlyList<Parameter> sourceParameters,
        CreateTableAsCommand command,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        ValidateCreateTableAs(dialect, command);

        if (dialect.CreateTableAsSelectUsesSelectInto)
            return ((withSql ?? string.Empty) + sourceSql, new List<Parameter>(sourceParameters));

        var body = withSql is null ? sourceSql : withSql + sourceSql;
        var sql = dialect.MakeCreateTableAsSelect(BuildCreateTableAsClause(dialect, quoteIdentifiers, command), body, keywordCase);
        return (sql, new List<Parameter>(sourceParameters));
    }

    private static void ValidateCreateTableAs(ISqlDialect dialect, CreateTableAsCommand command)
    {
        if (!dialect.SupportsCreateTableAsSelect)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot materialise a query into a table.");

        var options = command.Options;
        if (command.Temporary && !dialect.SupportsTemporaryCreateTableAsSelect)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot materialise a query into a temporary table; only a persistent ToTable is supported.");
        if (options.Columns is { Count: > 0 } && !dialect.SupportsCreateTableAsSelectColumnList)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot declare a column list on a materialised table.");
        if (options.IfNotExists && !dialect.SupportsCreateTableAsSelectIfNotExists)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot render IF NOT EXISTS on a materialised table.");
        if (options.OnCommit is not TempTableOnCommit.PreserveRows && !command.Temporary)
            throw new NotSupportedException(
                "ON COMMIT applies only to a temporary table; use ToTempTable instead of ToTable.");
        if (options.OnCommit is not TempTableOnCommit.PreserveRows && !dialect.SupportsCreateTableAsSelectOnCommit)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot use ON COMMIT on a materialised table.");
        if (!options.WithData && !dialect.SupportsCreateTableAsSelectWithNoData)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} cannot render WITH NO DATA on a materialised table.");
    }

    private static CreateTableAsClause BuildCreateTableAsClause(ISqlDialect dialect, bool quoteIdentifiers, CreateTableAsCommand command)
    {
        var options = command.Options;
        var table = quoteIdentifiers ? dialect.QuoteIdentifier(command.TargetName) : command.TargetName;

        IReadOnlyList<string>? columns = null;
        if (options.Columns is { Count: > 0 } rawColumns)
        {
            var rendered = new string[rawColumns.Count];
            for (var i = 0; i < rawColumns.Count; i++)
                rendered[i] = quoteIdentifiers ? dialect.QuoteIdentifier(rawColumns[i]) : rawColumns[i];

            columns = rendered;
        }

        return new CreateTableAsClause(table, command.Temporary, options.IfNotExists, columns, options.OnCommit, options.WithData);
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

    // A general MERGE with an arbitrary set of branches:
    // MERGE INTO <target> AS target USING (VALUES ...) AS source (<cols>) ON ... <branches> [OUTPUT|RETURNING] [;].
    private static void RenderFullMerge(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider parameterProvider,
        string? sourceSql,
        IReadOnlyList<Parameter>? sourceParameters,
        string? matchConditionSql,
        IReadOnlyList<string?>? branchConditions)
    {
        ValidateMergeBranches(dialect, command);

        var table = ResolveTableName(command.TableName, command.IsTableNameAuto, command.EntityType, namingConvention);
        if (quoteIdentifiers)
            table = dialect.QuoteIdentifier(table);

        var columns = RenderColumns(dialect, quoteIdentifiers, namingConvention, ColumnProperties(command.Columns));
        var keys = RenderColumns(dialect, quoteIdentifiers, namingConvention, command.Keys);
        var qualifyTarget = dialect.SupportsMergeTargetQualification;

        var rows = StringBuilderPool.Shared.Get();
        try
        {
            writer.Append(SqlKeywords.Of(keywordCase, "merge into ")).Append(table);
            AppendMergeUsingSource(writer, rows, dialect, quoteIdentifiers, namingConvention, command, columns, parameters, keywordCase, parameterProvider, sourceSql, sourceParameters);
            AppendMergeOnKeys(writer, keys, matchConditionSql, keywordCase);

            for (var i = 0; i < command.Branches!.Count; i++)
            {
                var condition = branchConditions is not null && i < branchConditions.Count ? branchConditions[i] : null;
                AppendMergeBranch(writer, dialect, quoteIdentifiers, namingConvention, command.Branches[i], condition, keywordCase, qualifyTarget);
            }

            AppendMergeReturning(writer, dialect, quoteIdentifiers, namingConvention, command, keywordCase);
            writer.Append(dialect.MakeMergeStatementTerminator(keywordCase));
        }
        finally
        {
            StringBuilderPool.Shared.Return(rows);
        }
    }

    private static void ValidateMergeBranches(ISqlDialect dialect, MergeCommand command)
    {
        if (!dialect.SupportsMergeStatement)
            throw new NotSupportedException(
                $"{dialect.GetType().Name} does not support the full MERGE statement; use its native upsert form (WhenMatchedUpdate/WhenNotMatchedInsert).");

        var branches = command.Branches!;
        var hasDelete = false;
        var hasNothing = false;
        var hasBySource = false;
        var hasCondition = command.MatchCondition is not null;
        foreach (var branch in branches)
        {
            if (branch.Action == MergeActionKind.Delete)
                hasDelete = true;
            if (branch.Action == MergeActionKind.Nothing)
                hasNothing = true;
            if (branch.Match == MergeMatchKind.NotMatchedBySource)
                hasBySource = true;
            if (branch.Condition is not null)
                hasCondition = true;
        }

        if (hasDelete && !dialect.SupportsMergeDelete)
            throw new NotSupportedException($"{dialect.GetType().Name} does not support a DELETE branch in MERGE.");
        if (hasNothing && !dialect.SupportsMergeDoNothing)
            throw new NotSupportedException($"{dialect.GetType().Name} does not support a DO NOTHING branch in MERGE.");
        if (hasBySource && !dialect.SupportsMergeBySourceDelete)
            throw new NotSupportedException($"{dialect.GetType().Name} does not support WHEN NOT MATCHED BY SOURCE.");
        if (hasCondition && !dialect.SupportsMergeConditionalBranches)
            throw new NotSupportedException($"{dialect.GetType().Name} does not support a merge search condition (On(...)/WhenMatched(condition)/WhenNotMatched(condition)).");
    }

    private static void AppendMergeUsingSource(
        StringBuilder writer,
        StringBuilder rows,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        string[] columns,
        List<Parameter> parameters,
        KeywordCase keywordCase,
        IParameterProvider parameterProvider,
        string? sourceSql,
        IReadOnlyList<Parameter>? sourceParameters)
    {
        if (command.Source is not null)
        {
            if (sourceSql is null)
                throw new BuildSqlCommandException("A MERGE query source is missing its rendered source SQL.");

            writer.Append(SqlKeywords.Of(keywordCase, " as target using (")).Append(sourceSql)
                .Append(SqlKeywords.Of(keywordCase, ") as source"));

            if (sourceParameters is { Count: > 0 })
                parameters.AddRange(sourceParameters);
        }
        else
        {
            AppendValuesRows(rows, dialect, quoteIdentifiers, namingConvention, command.Columns, command.RowCount, parameters, keywordCase, parameterProvider);

            writer.Append(SqlKeywords.Of(keywordCase, " as target using (values ")).Append(rows)
                .Append(SqlKeywords.Of(keywordCase, ") as source (")).Append(string.Join(", ", columns)).Append(')');
        }
    }

    private static void AppendMergeOnKeys(StringBuilder writer, string[] keys, string? matchConditionSql, KeywordCase keywordCase)
    {
        writer.Append(SqlKeywords.Of(keywordCase, " on "));

        if (matchConditionSql is not null)
        {
            writer.Append(matchConditionSql);
            return;
        }

        for (var i = 0; i < keys.Length; i++)
        {
            if (i > 0)
                writer.Append(SqlKeywords.Of(keywordCase, " and "));

            writer.Append("target.").Append(keys[i]).Append(" = source.").Append(keys[i]);
        }
    }

    // Renders the WHEN <match> [AND <condition>] head of a branch; the action is appended by the caller.
    private static void AppendMergeWhen(StringBuilder writer, MergeMatchKind match, string? condition, KeywordCase keywordCase)
    {
        writer.Append(match switch
        {
            MergeMatchKind.NotMatchedBySource => SqlKeywords.Of(keywordCase, " when not matched by source"),
            MergeMatchKind.NotMatchedByTarget => SqlKeywords.Of(keywordCase, " when not matched"),
            _ => SqlKeywords.Of(keywordCase, " when matched"),
        });

        if (condition is not null)
            writer.Append(SqlKeywords.Of(keywordCase, " and ")).Append(condition);
    }

    private static void AppendMergeBranch(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeBranch branch,
        string? condition,
        KeywordCase keywordCase,
        bool qualifyTarget)
    {
        switch (branch.Action)
        {
            case MergeActionKind.Update:
                AppendMergeWhen(writer, MergeMatchKind.Matched, condition, keywordCase);
                writer.Append(SqlKeywords.Of(keywordCase, " then update set "));
                AppendMergeAssignments(writer, dialect, quoteIdentifiers, namingConvention, branch.Columns, qualifyTarget);
                break;
            case MergeActionKind.Insert:
                AppendMergeWhen(writer, MergeMatchKind.NotMatchedByTarget, condition, keywordCase);
                var insertColumns = RenderColumns(dialect, quoteIdentifiers, namingConvention, branch.Columns);
                writer.Append(SqlKeywords.Of(keywordCase, " then insert (")).Append(string.Join(", ", insertColumns))
                    .Append(SqlKeywords.Of(keywordCase, ") values ("));
                for (var i = 0; i < insertColumns.Length; i++)
                {
                    if (i > 0)
                        writer.Append(", ");

                    writer.Append("source.").Append(insertColumns[i]);
                }

                writer.Append(')');
                break;
            case MergeActionKind.Nothing:
                AppendMergeWhen(writer, branch.Match, condition, keywordCase);
                writer.Append(SqlKeywords.Of(keywordCase, " then do nothing"));
                break;
            default:
                AppendMergeWhen(writer, branch.Match, condition, keywordCase);
                writer.Append(SqlKeywords.Of(keywordCase, " then delete"));
                break;
        }
    }

    private static void AppendMergeReturning(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        MergeCommand command,
        KeywordCase keywordCase)
    {
        var returningColumns = RenderColumnsOrNull(dialect, quoteIdentifiers, namingConvention, command.ReturningColumns);
        if (returningColumns is null)
            return;

        if (dialect.SupportsOutput)
            writer.Append(dialect.MakeOutput(returningColumns, keywordCase));
        else if (dialect.SupportsReturning)
            writer.Append(dialect.MakeMergeReturning(returningColumns, keywordCase));
    }

    // Renders the "target.col = source.col, ..." list of a MERGE update branch. PostgreSQL forbids
    // qualifying the target column, SQL Server requires the target alias; the source is always qualified.
    private static void AppendMergeAssignments(
        StringBuilder writer,
        ISqlDialect dialect,
        bool quoteIdentifiers,
        INamingConvention? namingConvention,
        IReadOnlyList<IPropertyMetadata> columns,
        bool qualifyTarget)
    {
        var rendered = RenderColumns(dialect, quoteIdentifiers, namingConvention, columns);
        for (var i = 0; i < rendered.Length; i++)
        {
            if (i > 0)
                writer.Append(", ");

            if (qualifyTarget)
                writer.Append("target.");

            writer.Append(rendered[i]).Append(" = source.").Append(rendered[i]);
        }
    }

    private static IPropertyMetadata[] ColumnProperties(IReadOnlyList<InsertColumn> columns)
    {
        var properties = new IPropertyMetadata[columns.Count];
        for (var c = 0; c < properties.Length; c++)
            properties[c] = columns[c].Property;

        return properties;
    }

    internal static string[] RenderColumns(ISqlDialect dialect, bool quoteIdentifiers, INamingConvention? namingConvention, IReadOnlyList<IPropertyMetadata> columns)
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
                    parameters.Add(new Parameter(name, DurationStorage.ToParameterValue(value.Constant, columns[c].Property, dialect)));
                    writer.Append(dialect.MakeParam(name));
                }
            }
            writer.Append(')');
        }
    }

    internal static string ResolveTableName(string tableName, bool isTableNameAuto, Type entityType, INamingConvention? namingConvention)
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

    internal static string ResolveColumnName(IPropertyMetadata property, INamingConvention? namingConvention)
        => property.IsColumnNameAuto && namingConvention is not null
            ? namingConvention.ColumnName(property.ColumnName)
            : property.ColumnName;

    internal static string RenderColumnReference(ISqlDialect dialect, bool quoteIdentifiers, IPropertyMetadata property, INamingConvention? namingConvention)
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
