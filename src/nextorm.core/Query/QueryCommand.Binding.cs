namespace NextORM.Core;

/// <summary>
/// Node-owned binding, ownership and cache-policy state of a <see cref="QueryCommand"/>. Allocated only
/// when one of the non-default values is written and never shared between commands: a clone receives its
/// own instance through <c>CopyTo</c>, and the state is never cleared by
/// <see cref="QueryCommand.ResetPreparation"/>.
/// <para>
/// The executing context itself (<c>_dataContext</c>) stays a direct node reference: every real
/// command is constructed with one, so moving it behind a lazily allocated block would only add an
/// allocation to every command without ever saving one. It is the third reference in the sealed plan's
/// node layout (description, preparation, data context, binding).
/// </para>
/// </summary>
internal sealed class QueryCommandBinding
{
    internal bool DontCache;
    internal bool HasEverPrepared;
    internal CteReference? TypedRecursiveAnchor;
}

public partial class QueryCommand
{
    private QueryCommandBinding? _binding;

    private QueryCommandBinding Binding => _binding ??= new QueryCommandBinding();

    /// <summary>
    /// Whether this command instance has ever completed a preparation. Unlike <see cref="_isPrepared"/> it
    /// is not cleared by <see cref="ResetPreparation"/>: it distinguishes a genuinely fresh command (which
    /// may reuse a pre-preparation recipe) from one being re-prepared in place. Clones start <see langword="false"/>.
    /// </summary>
    internal bool HasEverPrepared
    {
        get => _binding?.HasEverPrepared ?? false;
        set
        {
            if (value == (_binding?.HasEverPrepared ?? false)) return;
            Binding.HasEverPrepared = value;
        }
    }

    private bool _dontCache
    {
        get => _binding?.DontCache ?? false;
        set
        {
            if (value == (_binding?.DontCache ?? false)) return;
            Binding.DontCache = value;
        }
    }
}
