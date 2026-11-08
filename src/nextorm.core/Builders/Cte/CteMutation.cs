namespace NextORM.Core;

/// <summary>
/// Tagged carrier for the data-modifying statement that forms the body of a common table expression.
/// Wraps the mutation command together with the prepared query its embedded dependencies live on, so the
/// CTE hoister can order the declarations a later CTE body depends on. The command's
/// <see cref="MutationCommand.StatementType"/> identifies the body kind (<c>INSERT</c>, <c>UPDATE</c>,
/// <c>DELETE</c>, multi-table <c>UPDATE</c> or multi-table <c>DELETE</c>).
/// </summary>
internal sealed class CteMutation
{
    /// <summary>Creates a data-modifying CTE body carrier.</summary>
    /// <param name="command">The mutation that forms the CTE body.</param>
    internal CteMutation(MutationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Command = command;
        Source = DependencyOf(command);
    }

    /// <summary>The mutation that forms the CTE body.</summary>
    internal MutationCommand Command { get; }

    /// <summary>
    /// The prepared query carrying the body's CTE dependencies, or <see langword="null"/> when the body
    /// declares none. Resolved from the command kind: the <c>INSERT ... SELECT</c> source of an
    /// <see cref="InsertCommand"/>, the predicate of an <see cref="UpdateCommand"/> or
    /// <see cref="DeleteCommand"/>, or the joined source of a multi-table
    /// <see cref="UpdateJoinCommand"/>/<see cref="DeleteJoinCommand"/>.
    /// </summary>
    internal QueryCommand? Source { get; }

    private static QueryCommand? DependencyOf(MutationCommand command) => command switch
    {
        InsertCommand insert => insert.Source,
        UpdateCommand update => update.Source,
        DeleteCommand delete => delete.Condition,
        UpdateJoinCommand updateJoin => updateJoin.Source,
        DeleteJoinCommand deleteJoin => deleteJoin.Source,
        _ => null,
    };
}
