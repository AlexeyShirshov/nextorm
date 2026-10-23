namespace NextORM.Core;

/// <summary>
/// How a mutation statement is rendered. The distinction decides whether a text value may be lowered into
/// a literal while the statement is rendered: a prepared render (a data-modifying CTE body placed inside a
/// prepared <c>SELECT</c>) is transported by the D4 parameter template instead, and an ad-hoc or batch
/// render builds a fresh <see cref="System.Data.Common.DbCommand"/> per execution and has no template to
/// specialize.
/// <para>
/// <see cref="Prepared"/> is the default value, so a renderer that does not state its mode keeps today's
/// binding behaviour and its byte-identical SQL.
/// </para>
/// </summary>
internal enum MutationRenderKind
{
    /// <summary>
    /// A render whose statement is executed from a prepared plan and specialized by the parameter template.
    /// No literal lowering happens here.
    /// </summary>
    Prepared,

    /// <summary>
    /// An ORM-rendered mutation executed directly: SQL and values are rendered together and a fresh command
    /// is created for the single execution.
    /// </summary>
    AdHoc,

    /// <summary>
    /// A side-effecting step of a batch, rendered by the same builders with the batch's shared parameter
    /// sequence.
    /// </summary>
    Batch,
}

/// <summary>
/// The render-time lowering of a mutation statement's planner-owned parameter bindings (D4g). On the
/// MySQL family a bound string is interpreted under the connection's escaping mode, so an ad-hoc or batch
/// mutation cannot rely on the driver's text binding: a text value becomes a charset-introduced
/// hexadecimal literal, which no session flag or driver option can reinterpret, and its binding is left out
/// of the statement's driver projection.
/// <para>
/// One instance belongs to one rendered statement. It decides per binding from the value the writer has
/// already resolved (after conversion), renders the literal through the same
/// <see cref="SqlStringLiteralPolicies"/> seam the prepared path uses, records the omitted binding names,
/// and returns the ordered driver projection. It performs no SQL search, no parameter-name substitution and
/// no template capture; the statement's own allocation and naming order is untouched, so the remaining
/// bindings keep their names and types.
/// </para>
/// </summary>
internal sealed class MutationParameterLowering
{
    /// <summary>The shared no-op instance used by every render that does not lower.</summary>
    private static readonly MutationParameterLowering None = new(policy: null);

    private readonly SqlStringLiteralPolicy? _policy;

    // PostgreSQL never lowers a value, but the same render seam is its final-value boundary (D5): an
    // ad-hoc or batch mutation whose value became NUL-bearing after conversion is rejected here, before
    // the statement's SQL is returned to the caller.
    private readonly bool _guardPostgres;

    // Per-occurrence bookkeeping, in emission order. A name may be recorded more than once: a caller
    // that shares its accumulator (for example a batch, or a captured value referenced twice) can bind
    // one name at several occurrences. Both lists stay null for a render that never lowers a value.
    private List<string>? _lowered;
    private List<string>? _emitted;

    private MutationParameterLowering(SqlStringLiteralPolicy? policy, bool guardPostgres = false)
    {
        _policy = policy;
        _guardPostgres = guardPostgres;
    }

    /// <summary>
    /// Resolves the render's value seam: the MySQL-family policy lowers text into a literal, the PostgreSQL
    /// policy only guards the final values, and every other combination gets the shared no-op, which leaves
    /// SQL text and bindings exactly as they were. A prepared render gets the no-op on every dialect: its
    /// statement is specialized (or guarded) by the prepared command at execution time.
    /// </summary>
    /// <param name="dialect">The dialect of the statement being rendered.</param>
    /// <param name="renderKind">How the statement is rendered.</param>
    /// <returns>The lowering for this render.</returns>
    internal static MutationParameterLowering For(ISqlDialect dialect, MutationRenderKind renderKind)
    {
        if (renderKind == MutationRenderKind.Prepared)
            return None;

        // MariaDB resolves to the same policy instance as MySQL, so the MySQL-family test is an identity
        // check against that one seam rather than a second provider table in Core.
        var policy = SqlStringLiteralPolicies.For(dialect);
        if (ReferenceEquals(policy, SqlStringLiteralPolicy.MySql))
            return new MutationParameterLowering(policy);
        if (ReferenceEquals(policy, SqlStringLiteralPolicy.Postgres))
            return new MutationParameterLowering(policy: null, guardPostgres: true);
        return None;
    }

    /// <summary>Whether this render lowers any value at all.</summary>
    internal bool IsActive => _policy is not null;

    /// <summary>
    /// Renders <paramref name="finalValue"/> as a lowered literal when the policy qualifies it. The value is
    /// the final provider-bound one: the caller has already applied the column converter, so a converter
    /// producing text lowers and a converter producing a non-text value does not, and no converter runs
    /// twice.
    /// </summary>
    /// <param name="name">The placeholder name the emission had already allocated.</param>
    /// <param name="finalValue">The converted value that would otherwise be bound.</param>
    /// <param name="literal">The literal to emit instead of the placeholder.</param>
    /// <returns><see langword="true"/> when the placeholder must be replaced by <paramref name="literal"/>.</returns>
    internal bool TryRender(string name, object? finalValue, out string literal)
    {
        literal = string.Empty;
        if (_policy is not { } policy)
            return false;

        switch (finalValue)
        {
            case string text:
                // Includes the empty string (_utf8mb4 X'') and every representable character; an unpaired
                // surrogate is rejected by the policy's strict UTF-8 encoder, as on the prepared path.
                literal = policy.Render(text);
                (_lowered ??= []).Add(name);
                return true;
            case char character:
                // default(char) is U+0000 and renders as _utf8mb4 X'00'; it is a one-character text value,
                // never a NULL and never an empty string.
                literal = policy.Render(character.ToString());
                (_lowered ??= []).Add(name);
                return true;
            default:
                // null, DBNull, Guid, numbers, binary and every other non-text value keep their bound
                // representation, order and type.
                (_emitted ??= []).Add(name);
                return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="name"/> must be left out of the driver projection: at least one occurrence
    /// was lowered and no occurrence of the same name was emitted as a placeholder. The parameter itself
    /// stays in the render's accumulator until the projection is taken, which keeps the statement's own
    /// parameter accounting intact.
    /// </summary>
    /// <param name="name">The placeholder name the emission had already allocated.</param>
    /// <returns><see langword="true"/> when no emitted placeholder still needs the binding.</returns>
    internal bool IsOmitted(string name)
        => _lowered is { Count: > 0 } && Count(_lowered, name) > (_emitted is null ? 0 : Count(_emitted, name));

    /// <summary>
    /// The ordered driver projection of the rendered statement: the accumulator without the bindings whose
    /// every occurrence was lowered. A binding that still has an emitted placeholder keeps its slot, in its
    /// original order and with its original name and type.
    /// </summary>
    /// <param name="parameters">The render's parameter accumulator.</param>
    /// <returns>The parameters to bind, in emission order.</returns>
    internal List<Parameter> Project(List<Parameter> parameters)
    {
        // The statement's final values are known here: every renderer that consults this seam ends with
        // this call, so the PostgreSQL guard rejects a NUL-bearing value before the SQL is returned.
        if (_guardPostgres)
            PostgresParameterGuard.Validate(SqlStringLiteralPolicy.Postgres, parameters);

        if (_lowered is not { Count: > 0 })
            return parameters;

        var projection = new List<Parameter>(parameters.Count);
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (!IsOmitted(parameter.Name))
                projection.Add(parameter);
        }

        return projection;
    }

    private static int Count(List<string> names, string name)
    {
        var count = 0;
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal))
                count++;
        }

        return count;
    }
}
