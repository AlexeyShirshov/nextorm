namespace NextORM.Core;

/// <summary>
/// Translates a CLR type/property name into a physical table/column name when the name was not
/// declared explicitly (no <c>[SqlTable]</c>/<c>[Column]</c> attribute and no fluent mapping).
/// Explicit names are always taken verbatim, so a convention never overrides a declared mapping.
/// </summary>
/// <remarks>
/// Set the default convention on the context with
/// <see cref="DataContextBuilder.UseNamingConvention"/>; a single command can override it with
/// <c>WithNamingConvention</c>. When no convention is configured, names are emitted as-is.
/// <para>
/// Implementations are expected to be stateless and reused (for example
/// <see cref="SnakeCaseNamingConvention.Instance"/>). They are part of the plan key by reference, so
/// allocating a fresh instance per query defeats the plan cache and grows the internal name cache.
/// The override applies to the whole outer command; a convention set on a nested query is ignored.
/// </para>
/// </remarks>
public interface INamingConvention
{
    /// <summary>Converts the CLR table name (usually the type name).</summary>
    /// <param name="clrName">The CLR type name, for example <c>SimpleEntity</c>.</param>
    /// <param name="isInterface">Whether the source type is an interface.</param>
    string TableName(string clrName, bool isInterface);

    /// <summary>Converts a property name into its column name, for example <c>FirstName</c> to <c>first_name</c>.</summary>
    string ColumnName(string propertyName);
}
