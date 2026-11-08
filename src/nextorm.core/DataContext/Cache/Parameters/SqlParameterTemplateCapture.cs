using System.Data;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// Render-local capture of the planner-owned parameter occurrences of one statement. It owns the
/// immutable-template finalization and the identity table that lets a fragment rendered by a nested
/// renderer (a subquery, a source, a mutation body) be relocated into its parent:
/// <list type="bullet">
/// <item><description><see cref="CreateFragment"/> starts a recorder for one rendered fragment;</description></item>
/// <item><description><see cref="RegisterFragment"/> associates the fragment's returned string with its
/// recorder, so a caller that appends that exact string can merge the occurrences;</description></item>
/// <item><description><see cref="Finalize(string, IReadOnlyList{Parameter})"/> turns the root fragment's
/// recorder into the immutable <see cref="SqlParameterTemplate"/> attached to the prepared command.</description></item>
/// </list>
/// One capture exists per planner render (per statement), never per execution: the immutable template is
/// produced once and shared by the cached command.
/// </summary>
internal sealed class SqlParameterTemplateCapture
{
    private readonly Dictionary<string, SqlParameterTemplateRecorder> _fragments =
        new(ReferenceEqualityComparer.Instance);

    private bool _unresolved;

    /// <summary>Creates a recorder for one rendered fragment of this statement.</summary>
    internal SqlParameterTemplateRecorder CreateFragment() => new();

    /// <summary>
    /// Associates a rendered fragment's returned string with the recorder that holds its occurrences and
    /// returns the same string, so call sites can register inline. A fragment without occurrences is
    /// still registered (a no-parameter statement root must still finalize into an empty template).
    /// </summary>
    internal string RegisterFragment(string text, SqlParameterTemplateRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(recorder);
        _fragments[text] = recorder;
        return text;
    }

    /// <summary>Looks up the recorder of a rendered fragment by string identity.</summary>
    internal bool TryGetFragment(string text, out SqlParameterTemplateRecorder recorder)
        => _fragments.TryGetValue(text, out recorder!);

    /// <summary>
    /// Marks the capture as unable to reconstruct the statement: a fragment could not be located in its
    /// parent, so no template is attached (the emitted SQL and the bindings are unaffected).
    /// </summary>
    internal void NotifyUnresolved() => _unresolved = true;

    /// <summary>
    /// Produces the immutable template of <paramref name="statementText"/> from the root fragment's
    /// recorder and the statement's ordered parameter list. Returns <see langword="null"/> when the
    /// capture is unusable (an unresolved fragment or a duplicate binding name), so a caller never
    /// attaches a partially reconstructed template.
    /// </summary>
    internal SqlParameterTemplate? Finalize(string statementText, IReadOnlyList<Parameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(statementText);
        ArgumentNullException.ThrowIfNull(parameters);

        if (_unresolved || !_fragments.TryGetValue(statementText, out var recorder))
            return null;

        var bindings = CreateBindings(parameters);
        if (bindings is null)
            return null;

        try
        {
            // The recorded fragments only have to be a prefix of the finished statement: text after the
            // last occurrence (and text that no parameter-bearing fragment accounts for) is advanced
            // over here. A recorder that is already longer than the statement means the fragment
            // accounting and the emitted text diverged, so the capture is dropped instead of producing
            // spans that do not match the SQL.
            recorder.AdvanceTo(statementText.Length);
            return recorder.Finalize(statementText, bindings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            NotifyUnresolved();
            return null;
        }
    }

    /// <summary>
    /// Derives the canonical binding descriptors from the ordered parameter list: the binding id is the
    /// parameter's ordinal, and the origin/type facets are the existing <see cref="Parameter"/> metadata.
    /// Returns <see langword="null"/> for a statement whose parameter names are not unique.
    /// </summary>
    internal static SqlParameterBinding[]? CreateBindings(IReadOnlyList<Parameter> parameters)
    {
        var bindings = new SqlParameterBinding[parameters.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < bindings.Length; i++)
        {
            var parameter = parameters[i];
            if (!names.Add(parameter.Name))
                return null;

            var origin = NormParam.IsName(parameter.Name)
                ? SqlParameterBindingOrigin.Runtime
                : parameter.Stable
                    ? SqlParameterBindingOrigin.Stable
                    : parameter.CapturedKey is not null
                        ? SqlParameterBindingOrigin.Captured
                        : SqlParameterBindingOrigin.Other;

            int? runtimeArgIndex = null;
            if (origin == SqlParameterBindingOrigin.Runtime)
                runtimeArgIndex = RuntimeArgumentIndex(parameter.Name);

            bindings[i] = new SqlParameterBinding(
                i,
                parameter.Name,
                origin,
                runtimeArgIndex,
                parameter.HasConversion,
                parameter.DbType,
                parameter.Precision,
                parameter.Scale);
        }

        return bindings;
    }

    private static int RuntimeArgumentIndex(string name)
    {
        var suffix = name.AsSpan(NormParam.Prefix.Length);
        return int.TryParse(suffix, out var index) ? index : 0;
    }
}

/// <summary>
/// The single emission funnel for planner-owned placeholders. Every renderer site that emits a
/// <c>dialect.MakeParam(name)</c> token goes through this helper, which appends the placeholder exactly as
/// before and records the occurrence with its canonical binding id and role. In <c>paramMode</c> (the
/// parameter-extraction render) nothing is appended and nothing is recorded; the parameter is still
/// collected by the caller.
/// </summary>
internal static class SqlParameterEmission
{
    /// <summary>Appends the placeholder of <paramref name="parameter"/> and records its occurrence.</summary>
    /// <param name="builder">The builder that receives the placeholder or the lowered literal.</param>
    /// <param name="recorder">The fragment recorder, or <see langword="null"/> when the emission is not captured.</param>
    /// <param name="dialect">The dialect of the statement being rendered.</param>
    /// <param name="paramMode">Whether the render only collects parameters.</param>
    /// <param name="parameters">The statement's parameter accumulator.</param>
    /// <param name="parameter">The parameter whose occurrence is emitted.</param>
    /// <param name="role">The syntactic role of the string literal.</param>
    /// <param name="lowering">
    /// The lowering of the mutation statement this emission belongs to, or <see langword="null"/> when the
    /// emission does not take part in a mutation render. A lowering that renders the binding as a literal
    /// replaces the placeholder with that literal, so the planner-rendered <c>SET</c>/<c>WHERE</c>
    /// fragments of a mutation are lowered exactly like the statement the builder emits itself (D4g).
    /// </param>
    internal static void Append(
        StringBuilder builder,
        SqlParameterTemplateRecorder? recorder,
        ISqlDialect dialect,
        bool paramMode,
        List<Parameter> parameters,
        Parameter parameter,
        SqlStringLiteralRole role,
        MutationParameterLowering? lowering = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameter);

        if (paramMode)
            return;

        // The lowering inspects the final provider-bound value, so an occurrence it lowers is replaced by
        // the literal at the same position and the binding is left to the statement-level projection.
        if (lowering is not null && lowering.TryRender(parameter.Name, parameter.Value, out var literal))
        {
            builder.Append(literal);
            return;
        }

        var placeholder = dialect.MakeParam(parameter.Name);

        if (recorder is not null)
        {
            var bindingId = BindingIdOf(parameters, parameter);
            if (bindingId >= 0)
            {
                recorder.AdvanceTo(builder.Length);
                recorder.RecordPlaceholder(bindingId, role, placeholder);
            }
        }

        builder.Append(placeholder);
    }

    /// <summary>
    /// Creates the placeholder text of <paramref name="parameter"/> without appending it (a site that
    /// embeds the token in a larger composed expression) and records a one-occurrence fragment that the
    /// caller tracks through the visitor fragment table. In <c>paramMode</c> returns <see langword="null"/>
    /// and records nothing.
    /// </summary>
    internal static string? Create(
        SqlParameterTemplateRecorder fragment,
        ISqlDialect dialect,
        bool paramMode,
        List<Parameter> parameters,
        Parameter parameter,
        SqlStringLiteralRole role)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameter);

        if (paramMode)
            return null;

        var placeholder = dialect.MakeParam(parameter.Name);
        var bindingId = BindingIdOf(parameters, parameter);
        if (bindingId >= 0)
            fragment.RecordPlaceholder(bindingId, role, placeholder);

        return placeholder;
    }

    private static int BindingIdOf(List<Parameter> parameters, Parameter parameter)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (ReferenceEquals(parameters[i], parameter))
                return i;
        }

        return -1;
    }
}

/// <summary>
/// A render-local builder that owns a fragment recorder and the child fragments appended into it. The
/// recorder's occurrences are fragment-relative until <see cref="Complete"/> relocates every tracked
/// child fragment by locating its appended text in the finished fragment and merging the child's
/// occurrences at that offset (the D4a relocation model, extended for transformed fragments whose wrapper
/// embeds the child text unchanged).
/// </summary>
internal sealed class SqlParameterFragmentBuilder : IDisposable
{
    private readonly SqlParameterTemplateCapture? _capture;
    private readonly SqlParameterTemplateRecorder? _recorder;
    private readonly List<SqlParameterTrackedFragment>? _tracked;
    private readonly StringBuilder _builder;

    /// <summary>Creates a fragment builder; with no capture it only accumulates text.</summary>
    internal SqlParameterFragmentBuilder(SqlParameterTemplateCapture? capture)
    {
        _capture = capture;
        _builder = StringBuilderPool.Shared.Get();
        if (capture is not null)
        {
            _recorder = new SqlParameterTemplateRecorder();
            _tracked = [];
        }
    }

    /// <summary>The underlying builder.</summary>
    internal StringBuilder Builder => _builder;

    /// <summary>The fragment recorder, or <see langword="null"/> when the builder is not tracked.</summary>
    internal SqlParameterTemplateRecorder? Recorder => _recorder;

    /// <summary>Appends text without an occurrence.</summary>
    internal void AppendText(string text) => _builder.Append(text);

    /// <summary>Appends a character without an occurrence.</summary>
    internal void AppendText(char value) => _builder.Append(value);

    /// <summary>Appends an integer without an occurrence.</summary>
    internal void AppendText(int value) => _builder.Append(value);

    /// <summary>Appends a placeholder and records its occurrence.</summary>
    internal void AppendPlaceholder(string placeholder, int bindingId, SqlStringLiteralRole role)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        if (_recorder is not null)
        {
            _recorder.AdvanceTo(_builder.Length);
            _recorder.RecordPlaceholder(bindingId, role, placeholder);
        }

        _builder.Append(placeholder);
    }

    /// <summary>Appends a fragment whose recorder is known, or looked up by string identity.</summary>
    internal void AppendFragment(string text, SqlParameterTemplateRecorder? fragment = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_recorder is not null)
        {
            fragment ??= Lookup(text);
            if (fragment is { OccurrenceCount: > 0 })
                _tracked!.Add(new SqlParameterTrackedFragment(text, null, fragment));
        }

        _builder.Append(text);
    }

    /// <summary>Appends the text of a builder fragment whose recorder is known.</summary>
    internal void AppendFragment(StringBuilder fragment, SqlParameterTemplateRecorder? fragmentRecorder)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        AppendFragment(fragment.ToString(), fragmentRecorder);
    }

    /// <summary>
    /// Appends <paramref name="appendedText"/>, which embeds <paramref name="fragmentText"/> unchanged, and
    /// tracks the child occurrences at their offset inside the wrapper.
    /// </summary>
    internal void AppendTransformed(string appendedText, string fragmentText, SqlParameterTemplateRecorder? fragment)
    {
        ArgumentNullException.ThrowIfNull(appendedText);
        ArgumentNullException.ThrowIfNull(fragmentText);
        if (_recorder is not null && fragment is { OccurrenceCount: > 0 })
            _tracked!.Add(new SqlParameterTrackedFragment(appendedText, fragmentText, fragment));

        _builder.Append(appendedText);
    }

    /// <summary>Flattens the tracked fragments and returns the finished fragment text.</summary>
    internal string Complete()
    {
        var text = _builder.ToString();
        if (_recorder is not null)
        {
            SqlParameterFragmentRelocation.Flatten(
                text,
                _recorder,
                _tracked!,
                _capture ?? throw new InvalidOperationException("A tracked fragment builder requires a capture."));
        }

        return text;
    }

    /// <summary>Flattens the fragment and registers the returned string for its parent.</summary>
    internal string CompleteAndRegister()
    {
        var text = Complete();
        return _capture is null ? text : _capture.RegisterFragment(text, _recorder!);
    }

    /// <inheritdoc/>
    void IDisposable.Dispose() => StringBuilderPool.Shared.Return(_builder);

    private SqlParameterTemplateRecorder? Lookup(string text)
        => _capture is not null && _capture.TryGetFragment(text, out var recorder) ? recorder : null;
}

/// <summary>
/// One child fragment appended into a tracked parent: the appended (possibly wrapped) text, the child text
/// inside it when the two differ, and the child's recorder.
/// </summary>
internal readonly record struct SqlParameterTrackedFragment(
    string AppendedText,
    string? FragmentText,
    SqlParameterTemplateRecorder Recorder);

/// <summary>
/// Relocates tracked child fragments into their parent's recorder by locating the appended text in the
/// finished parent text. A fragment that cannot be located marks the capture as unresolved instead of
/// producing a wrong span.
/// </summary>
internal static class SqlParameterFragmentRelocation
{
    /// <summary>Merges every tracked child fragment into <paramref name="parent"/>.</summary>
    internal static void Flatten(
        string parentText,
        SqlParameterTemplateRecorder parent,
        List<SqlParameterTrackedFragment> tracked,
        SqlParameterTemplateCapture capture)
    {
        ArgumentNullException.ThrowIfNull(parentText);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(tracked);
        ArgumentNullException.ThrowIfNull(capture);

        Dictionary<string, int>? seen = null;
        foreach (var fragment in tracked)
        {
            seen ??= new Dictionary<string, int>(StringComparer.Ordinal);
            seen.TryGetValue(fragment.AppendedText, out var previous);
            seen[fragment.AppendedText] = previous + 1;

            var start = IndexOfOccurrence(parentText, fragment.AppendedText, previous);
            if (start < 0)
            {
                capture.NotifyUnresolved();
                continue;
            }

            var inner = 0;
            if (fragment.FragmentText is { } childText)
            {
                inner = fragment.AppendedText.IndexOf(childText, StringComparison.Ordinal);
                if (inner < 0)
                {
                    capture.NotifyUnresolved();
                    continue;
                }
            }

            parent.AppendOccurrences(fragment.Recorder, start + inner);
        }

        parent.AdvanceTo(parentText.Length);
    }

    /// <summary>Merges a fragment directly appended to <paramref name="target"/> at its current end.</summary>
    internal static void AppendAtEnd(
        StringBuilder target,
        SqlParameterTemplateRecorder? targetRecorder,
        string fragmentText,
        SqlParameterTemplateRecorder? fragmentRecorder)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(fragmentText);

        if (targetRecorder is not null && fragmentRecorder is { OccurrenceCount: > 0 })
        {
            AppendComposed(
                target,
                targetRecorder,
                fragmentText,
                [new SqlParameterTrackedFragment(fragmentText, null, fragmentRecorder)]);
            return;
        }

        target.Append(fragmentText);
    }

    /// <summary>
    /// Appends <paramref name="text"/> at the current end of <paramref name="target"/> and relocates every
    /// tracked fragment's occurrences to where that fragment's appended text actually occurs inside
    /// <paramref name="text"/>, so a wrapper that embeds rendered arguments unchanged (a cast prefix, a
    /// function name and its separators) shifts the placeholder spans by its real prefix instead of
    /// leaving them at their rewritten-away positions. Each fragment is located in order from the
    /// previous match, so repeated identical fragment text relocates one after another and no whole-SQL
    /// rescan is needed. A fragment that cannot be located contributes no occurrence (never a wrong
    /// span); only text is appended in that case.
    /// </summary>
    /// <param name="target">The builder that receives the text.</param>
    /// <param name="targetRecorder">The target's recorder, or <see langword="null"/> when it is untracked.</param>
    /// <param name="text">The composed text appended at the current end of the target.</param>
    /// <param name="fragments">The tracked fragments the text embeds, in emission order.</param>
    internal static void AppendComposed(
        StringBuilder target,
        SqlParameterTemplateRecorder? targetRecorder,
        string text,
        IReadOnlyList<SqlParameterTrackedFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fragments);

        var baseLength = target.Length;

        if (targetRecorder is not null)
        {
            var searchFrom = 0;
            foreach (var fragment in fragments)
            {
                if (fragment.Recorder.OccurrenceCount == 0)
                    continue;

                var start = text.IndexOf(fragment.AppendedText, searchFrom, StringComparison.Ordinal);
                if (start < 0)
                    continue;

                searchFrom = start + 1;

                var inner = 0;
                if (fragment.FragmentText is { } fragmentText)
                {
                    inner = fragment.AppendedText.IndexOf(fragmentText, StringComparison.Ordinal);
                    if (inner < 0)
                        continue;
                }

                var offset = baseLength + start + inner;
                targetRecorder.AdvanceTo(offset);
                targetRecorder.AppendOccurrences(fragment.Recorder, offset);
            }

            targetRecorder.AdvanceTo(baseLength + text.Length);
        }

        target.Append(text);
    }

    internal static int IndexOfOccurrence(string text, string value, int occurrence)
    {
        if (value.Length == 0)
            return -1;

        var index = -1;
        for (var i = 0; i <= occurrence; i++)
        {
            index = text.IndexOf(value, index + 1, StringComparison.Ordinal);
            if (index < 0)
                return -1;
        }

        return index;
    }
}
