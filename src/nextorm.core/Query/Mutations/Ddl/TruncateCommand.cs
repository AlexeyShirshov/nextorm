namespace NextORM.Core;

/// <summary>
/// A <c>TRUNCATE TABLE</c> command: the target table with no row filter. The dialect renders the native
/// form; providers without <c>TRUNCATE</c> reject it through <see cref="ISqlDialect.SupportsTruncate"/>.
/// </summary>
internal sealed class TruncateCommand : MutationCommand
{
    /// <summary>Creates a truncate command.</summary>
    /// <param name="entityType">The CLR entity type whose table is truncated.</param>
    /// <param name="tableName">The mapped (still unconventioned, unquoted) table name.</param>
    /// <param name="isTableNameAuto">Whether <paramref name="tableName"/> was derived from the CLR type name.</param>
    public TruncateCommand(Type entityType, string tableName, bool isTableNameAuto)
        : base(SqlStatementType.Truncate, entityType)
    {
        TableName = tableName;
        IsTableNameAuto = isTableNameAuto;
    }

    /// <summary>The mapped table name, before the naming convention and identifier quoting are applied.</summary>
    public string TableName { get; }

    /// <summary>Whether <see cref="TableName"/> was auto-derived and the naming convention applies to it.</summary>
    public bool IsTableNameAuto { get; }
}
