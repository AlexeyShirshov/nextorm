namespace NextORM.Core;

/// <summary>
/// One projected column as a row materializer sees it: the ordinal to read, the member name to bind,
/// the CLR type the member expects, and whether the column may read as SQL <c>NULL</c>.
/// </summary>
/// <remarks>
/// The CLR type is the <em>bound</em> type — the member's declared type after any value converter,
/// not the provider's storage type. A provider that supplies a compile-time materializer reads the
/// value with the matching typed <see cref="System.Data.IDataRecord"/> accessor and converts it to
/// <see cref="Type"/> exactly like the built-in expression path does.
/// </remarks>
public readonly struct RowMaterializerColumn
{
    /// <summary>Creates a column description.</summary>
    /// <param name="ordinal">The zero-based index of the column in the result set.</param>
    /// <param name="name">The member or projection name, when known.</param>
    /// <param name="type">The CLR type the member expects.</param>
    /// <param name="nullable">Whether the CLR type can hold <see langword="null"/>.</param>
    public RowMaterializerColumn(int ordinal, string? name, Type type, bool nullable)
    {
        Ordinal = ordinal;
        Name = name;
        Type = type;
        Nullable = nullable;
    }

    /// <summary>Gets the zero-based index of the column in the result set.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the member or projection name, when the projection carries one.</summary>
    public string? Name { get; }

    /// <summary>Gets the CLR type the member expects.</summary>
    public Type Type { get; }

    /// <summary>Gets a value indicating whether the CLR type can hold <see langword="null"/>.</summary>
    public bool Nullable { get; }
}
