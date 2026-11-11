using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A context-independent runtime descriptor of a table-valued function used as a query source.
/// <para>
/// A TVF factory is an ordinary static method that returns this descriptor, for example
/// <c>public static TvfEntityBuilder&lt;Row&gt; MyTvf(int id) =&gt; new("my_tvf", "dbo");</c>. The scalar
/// parameters of the factory only name the argument positions of the call expression; the metadata
/// (name, schema, clauses, verbatim index list, result schema) <b>must not depend on the argument
/// values</b>. The arguments themselves stay in the caller's expression tree and are lowered when the
/// descriptor is consumed by <c>IDataContext.From</c>/Join/APPLY, so a correlated argument is never
/// evaluated as a CLR value.
/// </para>
/// <para>
/// The descriptor carries no data context and never gets one written into it: composing
/// <c>Where</c>/<c>OrderBy</c> and the other builder operators clones an unbound chain that fails
/// explicitly on any context-dependent operation until it is bound by the context that consumes it.
/// Metadata is constructor-only and immutable; the verbatim index list is defensively copied.
/// </para>
/// </summary>
/// <typeparam name="T">The mapped row type the function returns.</typeparam>
public class TvfEntityBuilder<T> : EntityBuilder<T>
{
    private readonly IReadOnlyList<int> _verbatimArguments;

    /// <summary>Creates a descriptor for a table-valued function without a schema.</summary>
    /// <param name="name">The SQL function name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public TvfEntityBuilder(string name)
        : this(name, null)
    {
    }

    /// <summary>Creates a descriptor for a table-valued function in an optional schema.</summary>
    /// <param name="name">The SQL function name.</param>
    /// <param name="schema">Optional schema/owner name; <see langword="null"/> renders the bare name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public TvfEntityBuilder(string name, string? schema)
        : this(name, schema, null, null, Array.Empty<int>(), TableFunctionSchema.None)
    {
    }

    /// <summary>
    /// Creates a descriptor with the full runtime metadata of the function.
    /// </summary>
    /// <param name="name">The SQL function name.</param>
    /// <param name="schema">Optional schema/owner name; <see langword="null"/> renders the bare name.</param>
    /// <param name="withClause">Optional trailing <c>WITH (...)</c> clause body appended after the call.</param>
    /// <param name="callClause">Optional verbatim SQL appended inside the call parentheses after the arguments.</param>
    /// <param name="verbatimArguments">
    /// Indices of the arguments emitted verbatim as SQL (identifiers, not parameters); an empty list means
    /// every argument is a value. The list is defensively copied and must contain distinct, non-negative
    /// indices. It is never <see langword="null"/>: "no verbatim argument" is expressed by an empty list.
    /// </param>
    /// <param name="resultSchema">Where the caller-declared result schema is rendered.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="verbatimArguments"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or whitespace, or <paramref name="verbatimArguments"/> contains a
    /// negative or duplicate index.
    /// </exception>
    public TvfEntityBuilder(
        string name,
        string? schema,
        string? withClause,
        string? callClause,
        IReadOnlyList<int> verbatimArguments,
        TableFunctionSchema resultSchema)
        : base()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(verbatimArguments);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("The table-valued function name cannot be empty.", nameof(name));

        var verbatim = CopyVerbatimArguments(verbatimArguments);

        Name = name;
        Schema = schema;
        WithClause = withClause;
        CallClause = callClause;
        _verbatimArguments = verbatim;
        ResultSchema = resultSchema;
    }

    /// <summary>The SQL function name.</summary>
    public string Name { get; }

    /// <summary>Optional schema/owner prefix rendered as <c>schema.name</c>.</summary>
    public string? Schema { get; }

    /// <summary>Optional trailing <c>WITH (...)</c> clause body appended after the call.</summary>
    public string? WithClause { get; }

    /// <summary>Optional verbatim SQL appended inside the call parentheses after the arguments.</summary>
    public string? CallClause { get; }

    /// <summary>
    /// The argument indices emitted verbatim as SQL; empty when every argument is a parameterised value.
    /// </summary>
    public IReadOnlyList<int> VerbatimArguments => _verbatimArguments;

    /// <summary>Where the caller-declared result schema is rendered.</summary>
    public TableFunctionSchema ResultSchema { get; }

    /// <summary>
    /// Clones the unbound descriptor, preserving its runtime metadata. Unlike a context-bound builder
    /// clone this copy needs no data context, so <c>Where</c>/<c>OrderBy</c> composition on a descriptor
    /// never touches a context.
    /// </summary>
    /// <returns>A descriptor with the same metadata and composition state.</returns>
    protected override object CloneImp()
    {
        var clone = new TvfEntityBuilder<T>(Name, Schema, WithClause, CallClause, _verbatimArguments, ResultSchema)
        {
            Logger = Logger,
        };

        CopyTo(clone);

        return clone;
    }

    private static IReadOnlyList<int> CopyVerbatimArguments(IReadOnlyList<int> verbatimArguments)
    {
        if (verbatimArguments.Count == 0)
            return Array.Empty<int>();

        var copy = new int[verbatimArguments.Count];
        var seen = new HashSet<int>();
        for (var i = 0; i < copy.Length; i++)
        {
            var index = verbatimArguments[i];
            if (index < 0)
                throw new ArgumentException($"The verbatim argument index {index} cannot be negative.", nameof(verbatimArguments));
            if (!seen.Add(index))
                throw new ArgumentException($"The verbatim argument index {index} is declared more than once.", nameof(verbatimArguments));

            copy[i] = index;
        }

        return copy;
    }
}
