namespace NextORM.Core;

/// <summary>
/// The synthetic link item a many-to-many <c>JoinInto</c> projects between the parent and the child: the
/// junction keys that tie a row to its parent and child, plus an <see cref="Occurrence"/> used to tell
/// apart duplicate junction rows.
/// </summary>
/// <remarks>
/// The type is not a mapped entity in the model; its metadata is registered internally so the pair
/// command can expand the derived junction join (see <c>JunctionLinkSourceFactory</c>) exactly
/// like any other projection item. <see cref="ParentKey"/> carries the junction's parent foreign key,
/// <see cref="ChildKey"/> its child foreign key, and <see cref="Occurrence"/> the
/// <c>row_number()</c> that distinguishes repeated <c>(parent, child)</c> junction rows.
/// </remarks>
/// <typeparam name="TParentKey">The property type shared by the parent key and the junction parent foreign key.</typeparam>
/// <typeparam name="TChildKey">The property type shared by the child key and the junction child foreign key.</typeparam>
internal sealed class JoinIntoLink<TParentKey, TChildKey>
{
    /// <summary>The junction foreign-key value referencing the parent key.</summary>
    public TParentKey ParentKey { get; set; } = default!;

    /// <summary>The junction foreign-key value referencing the child key.</summary>
    public TChildKey ChildKey { get; set; } = default!;

    /// <summary>
    /// The 1-based occurrence of this <c>(parent, child)</c> junction row, produced by
    /// <c>row_number() over (partition by parent key, child key order by child key)</c>.
    /// </summary>
    public int Occurrence { get; set; }
}
