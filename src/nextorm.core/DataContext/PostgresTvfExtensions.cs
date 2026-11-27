using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>Starts typed queries over built-in PostgreSQL table functions.</summary>
public static class PostgresTvfExtensions
{
    /// <summary>Starts a query over PostgreSQL <c>string_to_table</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="value">The SQL expression for <c>value</c>.</param>
    /// <param name="delimiter">The SQL expression for <c>delimiter</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IStringToTableRow> FromStringToTable(
        this IDataContext dataContext,
        Expression<Func<string?>> value,
        Expression<Func<string?>> delimiter)
        => FromCall<SqlFunctions.IStringToTableRow>(dataContext, () => SqlFunctions.Postgres.string_to_table(default!, default!), value, delimiter);

    /// <summary>Starts a query over PostgreSQL <c>string_to_table</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="value">The SQL expression for <c>value</c>.</param>
    /// <param name="delimiter">The SQL expression for <c>delimiter</c>.</param>
    /// <param name="nullString">The SQL expression for <c>nullString</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IStringToTableRow> FromStringToTable(
        this IDataContext dataContext,
        Expression<Func<string?>> value,
        Expression<Func<string?>> delimiter,
        Expression<Func<string?>> nullString)
        => FromCall<SqlFunctions.IStringToTableRow>(dataContext, () => SqlFunctions.Postgres.string_to_table(default!, default!, default!), value, delimiter, nullString);

    /// <summary>Starts a query over PostgreSQL <c>generate_series</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="start">The SQL expression for <c>start</c>.</param>
    /// <param name="stop">The SQL expression for <c>stop</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IGenerateSeriesRow> FromGenerateSeries(
        this IDataContext dataContext,
        Expression<Func<long>> start,
        Expression<Func<long>> stop)
        => FromCall<SqlFunctions.IGenerateSeriesRow>(dataContext, () => SqlFunctions.Postgres.generate_series(default!, default!), start, stop);

    /// <summary>Starts a query over PostgreSQL <c>generate_series</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="start">The SQL expression for <c>start</c>.</param>
    /// <param name="stop">The SQL expression for <c>stop</c>.</param>
    /// <param name="step">The SQL expression for <c>step</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IGenerateSeriesRow> FromGenerateSeries(
        this IDataContext dataContext,
        Expression<Func<long>> start,
        Expression<Func<long>> stop,
        Expression<Func<long>> step)
        => FromCall<SqlFunctions.IGenerateSeriesRow>(dataContext, () => SqlFunctions.Postgres.generate_series(default!, default!, default!), start, stop, step);

    /// <summary>Starts a query over PostgreSQL <c>unnest</c>.</summary>
    /// <typeparam name="T">The corresponding array element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="array">The SQL expression for <c>array</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IUnnestRow<T>> FromUnnest<T>(
        this IDataContext dataContext,
        Expression<Func<T[]>> array)
        => FromCall<SqlFunctions.IUnnestRow<T>>(dataContext, () => SqlFunctions.Postgres.unnest<T>(default!), array);

    /// <summary>Starts a query over PostgreSQL <c>unnest</c>.</summary>
    /// <typeparam name="T1">The corresponding array element type.</typeparam>
    /// <typeparam name="T2">The corresponding array element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="first">The SQL expression for <c>first</c>.</param>
    /// <param name="second">The SQL expression for <c>second</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IUnnestMultiRow<T1, T2>> FromUnnest<T1, T2>(
        this IDataContext dataContext,
        Expression<Func<T1[]>> first,
        Expression<Func<T2[]>> second)
        => FromCall<SqlFunctions.IUnnestMultiRow<T1, T2>>(dataContext, () => SqlFunctions.Postgres.unnest<T1, T2>(default!, default!), first, second);

    /// <summary>Starts a query over PostgreSQL <c>unnest</c>.</summary>
    /// <typeparam name="T1">The corresponding array element type.</typeparam>
    /// <typeparam name="T2">The corresponding array element type.</typeparam>
    /// <typeparam name="T3">The corresponding array element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="first">The SQL expression for <c>first</c>.</param>
    /// <param name="second">The SQL expression for <c>second</c>.</param>
    /// <param name="third">The SQL expression for <c>third</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IUnnestMultiRow<T1, T2, T3>> FromUnnest<T1, T2, T3>(
        this IDataContext dataContext,
        Expression<Func<T1[]>> first,
        Expression<Func<T2[]>> second,
        Expression<Func<T3[]>> third)
        => FromCall<SqlFunctions.IUnnestMultiRow<T1, T2, T3>>(dataContext, () => SqlFunctions.Postgres.unnest<T1, T2, T3>(default!, default!, default!), first, second, third);

    /// <summary>Starts a query over PostgreSQL <c>generate_subscripts</c>.</summary>
    /// <typeparam name="T">The corresponding array element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="array">The SQL expression for <c>array</c>.</param>
    /// <param name="dimension">The SQL expression for <c>dimension</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IGenerateSubscriptsRow> FromGenerateSubscripts<T>(
        this IDataContext dataContext,
        Expression<Func<T[]>> array,
        Expression<Func<int?>> dimension)
        => FromCall<SqlFunctions.IGenerateSubscriptsRow>(dataContext, () => SqlFunctions.Postgres.generate_subscripts<T>(default!, default!), array, dimension);

    /// <summary>Starts a query over PostgreSQL <c>generate_subscripts</c>.</summary>
    /// <typeparam name="T">The corresponding array element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="array">The SQL expression for <c>array</c>.</param>
    /// <param name="dimension">The SQL expression for <c>dimension</c>.</param>
    /// <param name="reverse">The SQL expression for <c>reverse</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IGenerateSubscriptsRow> FromGenerateSubscripts<T>(
        this IDataContext dataContext,
        Expression<Func<T[]>> array,
        Expression<Func<int?>> dimension,
        Expression<Func<bool?>> reverse)
        => FromCall<SqlFunctions.IGenerateSubscriptsRow>(dataContext, () => SqlFunctions.Postgres.generate_subscripts<T>(default!, default!, default!), array, dimension, reverse);

    /// <summary>Starts a query over PostgreSQL <c>regexp_matches</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="source">The SQL expression for <c>source</c>.</param>
    /// <param name="pattern">The SQL expression for <c>pattern</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IRegexpMatchesRow> FromRegexpMatches(
        this IDataContext dataContext,
        Expression<Func<string?>> source,
        Expression<Func<string?>> pattern)
        => FromCall<SqlFunctions.IRegexpMatchesRow>(dataContext, () => SqlFunctions.Postgres.regexp_matches(default!, default!), source, pattern);

    /// <summary>Starts a query over PostgreSQL <c>regexp_matches</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="source">The SQL expression for <c>source</c>.</param>
    /// <param name="pattern">The SQL expression for <c>pattern</c>.</param>
    /// <param name="flags">The SQL expression for <c>flags</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IRegexpMatchesRow> FromRegexpMatches(
        this IDataContext dataContext,
        Expression<Func<string?>> source,
        Expression<Func<string?>> pattern,
        Expression<Func<string?>> flags)
        => FromCall<SqlFunctions.IRegexpMatchesRow>(dataContext, () => SqlFunctions.Postgres.regexp_matches(default!, default!, default!), source, pattern, flags);

    /// <summary>Starts a query over PostgreSQL <c>regexp_split_to_table</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="source">The SQL expression for <c>source</c>.</param>
    /// <param name="pattern">The SQL expression for <c>pattern</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IRegexpSplitToTableRow> FromRegexpSplitToTable(
        this IDataContext dataContext,
        Expression<Func<string?>> source,
        Expression<Func<string?>> pattern)
        => FromCall<SqlFunctions.IRegexpSplitToTableRow>(dataContext, () => SqlFunctions.Postgres.regexp_split_to_table(default!, default!), source, pattern);

    /// <summary>Starts a query over PostgreSQL <c>regexp_split_to_table</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="source">The SQL expression for <c>source</c>.</param>
    /// <param name="pattern">The SQL expression for <c>pattern</c>.</param>
    /// <param name="flags">The SQL expression for <c>flags</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IRegexpSplitToTableRow> FromRegexpSplitToTable(
        this IDataContext dataContext,
        Expression<Func<string?>> source,
        Expression<Func<string?>> pattern,
        Expression<Func<string?>> flags)
        => FromCall<SqlFunctions.IRegexpSplitToTableRow>(dataContext, () => SqlFunctions.Postgres.regexp_split_to_table(default!, default!, default!), source, pattern, flags);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_array_elements</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonArrayElementsRow> FromJsonbArrayElements(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<SqlFunctions.IJsonArrayElementsRow>(dataContext, () => SqlFunctions.Postgres.jsonb_array_elements(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_array_elements_text</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonArrayElementsRow> FromJsonbArrayElementsText(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<SqlFunctions.IJsonArrayElementsRow>(dataContext, () => SqlFunctions.Postgres.jsonb_array_elements_text(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_each</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonbEachRow> FromJsonbEach(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<SqlFunctions.IJsonbEachRow>(dataContext, () => SqlFunctions.Postgres.jsonb_each(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_each_text</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonbEachRow> FromJsonbEachText(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<SqlFunctions.IJsonbEachRow>(dataContext, () => SqlFunctions.Postgres.jsonb_each_text(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_array_elements</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonArrayElementsRow> FromJsonArrayElements(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<SqlFunctions.IJsonArrayElementsRow>(dataContext, () => SqlFunctions.Postgres.json_array_elements(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_array_elements_text</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonArrayElementsRow> FromJsonArrayElementsText(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<SqlFunctions.IJsonArrayElementsRow>(dataContext, () => SqlFunctions.Postgres.json_array_elements_text(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_each</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonbEachRow> FromJsonEach(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<SqlFunctions.IJsonbEachRow>(dataContext, () => SqlFunctions.Postgres.json_each(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_each_text</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonbEachRow> FromJsonEachText(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<SqlFunctions.IJsonbEachRow>(dataContext, () => SqlFunctions.Postgres.json_each_text(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_path_query_tz</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <param name="path">The SQL expression for <c>path</c>.</param>
    /// <param name="vars">The SQL expression for <c>vars</c>.</param>
    /// <param name="silent">The SQL expression for <c>silent</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonPathQueryTzRow> FromJsonbPathQueryTz(
        this IDataContext dataContext,
        Expression<Func<string?>> json,
        Expression<Func<string?>> path,
        Expression<Func<string?>> vars,
        Expression<Func<bool?>> silent)
        => FromCall<SqlFunctions.IJsonPathQueryTzRow>(dataContext, () => SqlFunctions.Postgres.jsonb_path_query_tz(default!, default!, default!, default!), json, path, vars, silent);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_object_keys</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonObjectKeysRow> FromJsonbObjectKeys(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<SqlFunctions.IJsonObjectKeysRow>(dataContext, () => SqlFunctions.Postgres.jsonb_object_keys(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_path_query</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <param name="path">The SQL expression for <c>path</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IJsonPathQueryRow> FromJsonbPathQuery(
        this IDataContext dataContext,
        Expression<Func<object?>> json,
        Expression<Func<string?>> path)
        => FromCall<SqlFunctions.IJsonPathQueryRow>(dataContext, () => SqlFunctions.Postgres.jsonb_path_query(default!, default!), json, path);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_to_record</c>.</summary>
    /// <typeparam name="TRow">The mapped result row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<TRow> FromJsonbToRecord<TRow>(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<TRow>(dataContext, () => SqlFunctions.Postgres.jsonb_to_record<TRow>(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>jsonb_to_recordset</c>.</summary>
    /// <typeparam name="TRow">The mapped result row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<TRow> FromJsonbToRecordset<TRow>(
        this IDataContext dataContext,
        Expression<Func<object?>> json)
        => FromCall<TRow>(dataContext, () => SqlFunctions.Postgres.jsonb_to_recordset<TRow>(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_to_record</c>.</summary>
    /// <typeparam name="TRow">The mapped result row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<TRow> FromJsonToRecord<TRow>(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<TRow>(dataContext, () => SqlFunctions.Postgres.json_to_record<TRow>(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>json_to_recordset</c>.</summary>
    /// <typeparam name="TRow">The mapped result row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="json">The SQL expression for <c>json</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<TRow> FromJsonToRecordset<TRow>(
        this IDataContext dataContext,
        Expression<Func<string?>> json)
        => FromCall<TRow>(dataContext, () => SqlFunctions.Postgres.json_to_recordset<TRow>(default!), json);

    /// <summary>Starts a query over PostgreSQL <c>ts_stat</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="query">The SQL expression for <c>query</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ITsStatRow> FromTsStat(
        this IDataContext dataContext,
        Expression<Func<string?>> query)
        => FromCall<SqlFunctions.ITsStatRow>(dataContext, () => SqlFunctions.Postgres.ts_stat(default!), query);

    /// <summary>Starts a query over PostgreSQL <c>ts_debug</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="document">The SQL expression for <c>document</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ITsDebugRow> FromTsDebug(
        this IDataContext dataContext,
        Expression<Func<string?>> document)
        => FromCall<SqlFunctions.ITsDebugRow>(dataContext, () => SqlFunctions.Postgres.ts_debug(default!), document);

    /// <summary>Starts a query over PostgreSQL <c>ts_parse</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="parser">The SQL expression for <c>parser</c>.</param>
    /// <param name="document">The SQL expression for <c>document</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ITsParseRow> FromTsParse(
        this IDataContext dataContext,
        Expression<Func<string?>> parser,
        Expression<Func<string?>> document)
        => FromCall<SqlFunctions.ITsParseRow>(dataContext, () => SqlFunctions.Postgres.ts_parse(default!, default!), parser, document);

    /// <summary>Starts a query over PostgreSQL <c>ts_token_type</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="parser">The SQL expression for <c>parser</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ITsTokenTypeRow> FromTsTokenType(
        this IDataContext dataContext,
        Expression<Func<string?>> parser)
        => FromCall<SqlFunctions.ITsTokenTypeRow>(dataContext, () => SqlFunctions.Postgres.ts_token_type(default!), parser);

    /// <summary>Starts a query over PostgreSQL <c>pg_control_system</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgControlSystemRow> FromPgControlSystem(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgControlSystemRow>(() => SqlFunctions.Postgres.pg_control_system());

    /// <summary>Starts a query over PostgreSQL <c>pg_control_init</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgControlInitRow> FromPgControlInit(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgControlInitRow>(() => SqlFunctions.Postgres.pg_control_init());

    /// <summary>Starts a query over PostgreSQL <c>pg_control_recovery</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgControlRecoveryRow> FromPgControlRecovery(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgControlRecoveryRow>(() => SqlFunctions.Postgres.pg_control_recovery());

    /// <summary>Starts a query over PostgreSQL <c>pg_control_checkpoint</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgControlCheckpointRow> FromPgControlCheckpoint(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgControlCheckpointRow>(() => SqlFunctions.Postgres.pg_control_checkpoint());

    /// <summary>Starts a query over PostgreSQL <c>pg_get_keywords</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgKeywordRow> FromPgGetKeywords(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgKeywordRow>(() => SqlFunctions.Postgres.pg_get_keywords());

    /// <summary>Starts a query over PostgreSQL <c>pg_listening_channels</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IListeningChannelRow> FromPgListeningChannels(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IListeningChannelRow>(() => SqlFunctions.Postgres.pg_listening_channels());

    /// <summary>Starts a query over PostgreSQL <c>pg_snapshot_xip</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="snapshot">The SQL expression for <c>snapshot</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ISnapshotXipRow> FromPgSnapshotXip(
        this IDataContext dataContext,
        Expression<Func<string?>> snapshot)
        => FromCall<SqlFunctions.ISnapshotXipRow>(dataContext, () => SqlFunctions.Postgres.pg_snapshot_xip(default!), snapshot);

    /// <summary>Starts a query over PostgreSQL <c>pg_tablespace_databases</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="tablespaceOid">The SQL expression for <c>tablespaceOid</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.ITablespaceDatabaseRow> FromPgTablespaceDatabases(
        this IDataContext dataContext,
        Expression<Func<string?>> tablespaceOid)
        => FromCall<SqlFunctions.ITablespaceDatabaseRow>(dataContext, () => SqlFunctions.Postgres.pg_tablespace_databases(default!), tablespaceOid);

    /// <summary>Starts a query over PostgreSQL <c>pg_get_catalog_foreign_keys</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgCatalogForeignKeyRow> FromPgGetCatalogForeignKeys(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgCatalogForeignKeyRow>(() => SqlFunctions.Postgres.pg_get_catalog_foreign_keys());

    /// <summary>Starts a query over PostgreSQL <c>pg_options_to_table</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="options">The SQL expression for <c>options</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgOptionRow> FromPgOptionsToTable(
        this IDataContext dataContext,
        Expression<Func<string?[]>> options)
        => FromCall<SqlFunctions.IPgOptionRow>(dataContext, () => SqlFunctions.Postgres.pg_options_to_table(default!), options);

    /// <summary>Starts a query over PostgreSQL <c>pg_available_wal_summaries</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgAvailableWalSummaryRow> FromPgAvailableWalSummaries(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgAvailableWalSummaryRow>(() => SqlFunctions.Postgres.pg_available_wal_summaries());

    /// <summary>Starts a query over PostgreSQL <c>pg_get_multixact_members</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="multixactId">The SQL expression for <c>multixactId</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgMultixactMemberRow> FromPgGetMultixactMembers(
        this IDataContext dataContext,
        Expression<Func<uint?>> multixactId)
        => FromCall<SqlFunctions.IPgMultixactMemberRow>(dataContext, () => SqlFunctions.Postgres.pg_get_multixact_members(default!), multixactId);

    /// <summary>Starts a query over PostgreSQL <c>pg_get_object_address</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="type">The SQL expression for <c>type</c>.</param>
    /// <param name="objectNames">The SQL expression for <c>objectNames</c>.</param>
    /// <param name="objectArgs">The SQL expression for <c>objectArgs</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgObjectAddressRow> FromPgGetObjectAddress(
        this IDataContext dataContext,
        Expression<Func<string?>> type,
        Expression<Func<string?[]>> objectNames,
        Expression<Func<string?[]>> objectArgs)
        => FromCall<SqlFunctions.IPgObjectAddressRow>(dataContext, () => SqlFunctions.Postgres.pg_get_object_address(default!, default!, default!), type, objectNames, objectArgs);

    /// <summary>Starts a query over PostgreSQL <c>pg_get_wal_summarizer_state</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgWalSummarizerStateRow> FromPgGetWalSummarizerState(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgWalSummarizerStateRow>(() => SqlFunctions.Postgres.pg_get_wal_summarizer_state());

    /// <summary>Starts a query over PostgreSQL <c>pg_last_committed_xact</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgCommittedXactRow> FromPgLastCommittedXact(this IDataContext dataContext)
        => dataContext.From<SqlFunctions.IPgCommittedXactRow>(() => SqlFunctions.Postgres.pg_last_committed_xact());

    /// <summary>Starts a query over PostgreSQL <c>pg_identify_object</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="classId">The SQL expression for <c>classId</c>.</param>
    /// <param name="objectId">The SQL expression for <c>objectId</c>.</param>
    /// <param name="objectSubId">The SQL expression for <c>objectSubId</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgIdentifyObjectRow> FromPgIdentifyObject(
        this IDataContext dataContext,
        Expression<Func<string?>> classId,
        Expression<Func<string?>> objectId,
        Expression<Func<int?>> objectSubId)
        => FromCall<SqlFunctions.IPgIdentifyObjectRow>(dataContext, () => SqlFunctions.Postgres.pg_identify_object(default!, default!, default!), classId, objectId, objectSubId);

    /// <summary>Starts a query over PostgreSQL <c>pg_identify_object_as_address</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="classId">The SQL expression for <c>classId</c>.</param>
    /// <param name="objectId">The SQL expression for <c>objectId</c>.</param>
    /// <param name="objectSubId">The SQL expression for <c>objectSubId</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgIdentifyObjectAddressRow> FromPgIdentifyObjectAsAddress(
        this IDataContext dataContext,
        Expression<Func<string?>> classId,
        Expression<Func<string?>> objectId,
        Expression<Func<int?>> objectSubId)
        => FromCall<SqlFunctions.IPgIdentifyObjectAddressRow>(dataContext, () => SqlFunctions.Postgres.pg_identify_object_as_address(default!, default!, default!), classId, objectId, objectSubId);

    /// <summary>Starts a query over PostgreSQL <c>pg_input_error_info</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="value">The SQL expression for <c>value</c>.</param>
    /// <param name="typeName">The SQL expression for <c>typeName</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgInputErrorInfoRow> FromPgInputErrorInfo(
        this IDataContext dataContext,
        Expression<Func<string?>> value,
        Expression<Func<string?>> typeName)
        => FromCall<SqlFunctions.IPgInputErrorInfoRow>(dataContext, () => SqlFunctions.Postgres.pg_input_error_info(default!, default!), value, typeName);

    /// <summary>Starts a query over PostgreSQL <c>pg_wal_summary_contents</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="timelineId">The SQL expression for <c>timelineId</c>.</param>
    /// <param name="startLsn">The SQL expression for <c>startLsn</c>.</param>
    /// <param name="endLsn">The SQL expression for <c>endLsn</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgWalSummaryContentRow> FromPgWalSummaryContents(
        this IDataContext dataContext,
        Expression<Func<long?>> timelineId,
        Expression<Func<string?>> startLsn,
        Expression<Func<string?>> endLsn)
        => FromCall<SqlFunctions.IPgWalSummaryContentRow>(dataContext, () => SqlFunctions.Postgres.pg_wal_summary_contents(default!, default!, default!), timelineId, startLsn, endLsn);

    /// <summary>Starts a query over PostgreSQL <c>pg_xact_commit_timestamp_origin</c>.</summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="xid">The SQL expression for <c>xid</c>.</param>
    /// <returns>A builder for composing a query over the function.</returns>
    public static EntityBuilder<SqlFunctions.IPgXactCommitOriginRow> FromPgXactCommitTimestampOrigin(
        this IDataContext dataContext,
        Expression<Func<uint?>> xid)
        => FromCall<SqlFunctions.IPgXactCommitOriginRow>(dataContext, () => SqlFunctions.Postgres.pg_xact_commit_timestamp_origin(default!), xid);

    private static EntityBuilder<TRow> FromCall<TRow>(
        IDataContext dataContext,
        Expression<Func<EntityBuilder<TRow>>> template,
        params LambdaExpression[] arguments)
    {
        // Preserve the original argument nodes so From can translate parameter markers
        // and captured values without invoking the argument lambdas.
        var call = (MethodCallExpression)template.Body;
        var nodes = new Expression[arguments.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            var argument = arguments[i];
            ArgumentNullException.ThrowIfNull(argument);
            nodes[i] = argument.Body;
        }

        return dataContext.From(Expression.Lambda<Func<EntityBuilder<TRow>>>(call.Update(call.Object, nodes)));
    }
}
