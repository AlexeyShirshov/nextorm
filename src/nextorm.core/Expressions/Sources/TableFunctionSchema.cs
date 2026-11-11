namespace NextORM.Core;

/// <summary>
/// Where the result-schema of a table function is rendered, when it is derived from the mapped row
/// type (<see cref="TvfEntityBuilder{T}.ResultSchema"/>). The listed providers recognize the
/// corresponding native form; a provider that cannot express it rejects the source with a
/// <see cref="System.NotSupportedException"/>.
/// </summary>
public enum TableFunctionSchema
{
    /// <summary>The schema is not rendered (the default; the function supplies its own result shape).</summary>
    None = 0,
    /// <summary>
    /// A quoted structure string is emitted as the leading call argument, before the arguments declared
    /// on the factory. ClickHouse <c>values('a UInt8, b String', (1,'x'), ...)</c>.
    /// </summary>
    LeadingArgument = 1,
    /// <summary>
    /// A column-definition list is emitted after the source alias, as
    /// <c>as x(col type, ...)</c>. PostgreSQL <c>jsonb_to_record(json) AS x(a int, b text)</c>.
    /// </summary>
    AliasColumnList = 2,
}
