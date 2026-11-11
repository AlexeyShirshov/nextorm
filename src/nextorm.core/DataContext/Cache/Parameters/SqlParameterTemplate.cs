using System.Data;

namespace NextORM.Core;

/// <summary>
/// Where a statement-local binding comes from: a captured or stable planner-owned value, a runtime
/// argument, or another planner-owned source.
/// </summary>
internal enum SqlParameterBindingOrigin
{
    /// <summary>A captured closure member or another value that the fast refresh recipe can re-read.</summary>
    Captured,
    /// <summary>A value that is fully determined by the query shape (an inline list or a folded constant).</summary>
    Stable,
    /// <summary>A <c>SqlFunctions.Parameter</c>-style placeholder whose value arrives with the execution.</summary>
    Runtime,
    /// <summary>Any other planner-owned binding (procedures, batches, derived slots).</summary>
    Other,
}

/// <summary>
/// Immutable description of one statement-local parameter binding. <see cref="BindingId"/> is the
/// canonical parameter ordinal of the statement, so a binding is identified by its position, never by
/// its name. The name is a lookup aid only.
/// </summary>
internal readonly record struct SqlParameterBinding
{
    /// <summary>
    /// Creates a binding. A <see cref="SqlParameterBindingOrigin.Runtime"/> binding must carry its
    /// runtime argument ordinal; every other origin must not.
    /// </summary>
    internal SqlParameterBinding(
        int bindingId,
        string name,
        SqlParameterBindingOrigin origin,
        int? runtimeArgIndex = null,
        bool hasConversion = false,
        DbType? dbType = null,
        byte? precision = null,
        byte? scale = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bindingId);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (origin == SqlParameterBindingOrigin.Runtime)
        {
            if (runtimeArgIndex is not { } runtimeIndex)
                throw new ArgumentException("A runtime binding requires its runtime argument ordinal.", nameof(runtimeArgIndex));
            ArgumentOutOfRangeException.ThrowIfNegative(runtimeIndex, nameof(runtimeArgIndex));
        }
        else if (runtimeArgIndex is not null)
        {
            throw new ArgumentException("Only a runtime binding carries a runtime argument ordinal.", nameof(runtimeArgIndex));
        }

        BindingId = bindingId;
        Name = name;
        Origin = origin;
        RuntimeArgIndex = runtimeArgIndex;
        HasConversion = hasConversion;
        DbType = dbType;
        Precision = precision;
        Scale = scale;
    }

    /// <summary>The canonical parameter ordinal of the statement.</summary>
    internal int BindingId { get; init; }

    /// <summary>The original parameter name; unique within one statement, but never a slot identity.</summary>
    internal string Name { get; init; }

    /// <summary>Where the bound value comes from.</summary>
    internal SqlParameterBindingOrigin Origin { get; init; }

    /// <summary>The runtime argument ordinal for a <see cref="SqlParameterBindingOrigin.Runtime"/> binding.</summary>
    internal int? RuntimeArgIndex { get; init; }

    /// <summary>True when the value went through a converter or another non-reproducible normalization.</summary>
    internal bool HasConversion { get; init; }

    /// <summary>The expression-derived provider type, or <see langword="null"/> for value-based inference.</summary>
    internal DbType? DbType { get; init; }

    /// <summary>The decimal precision, or <see langword="null"/>.</summary>
    internal byte? Precision { get; init; }

    /// <summary>The decimal scale, or <see langword="null"/>.</summary>
    internal byte? Scale { get; init; }
}

/// <summary>
/// Immutable record of one placeholder emission. The span is the exact slice of the original statement
/// text that the placeholder occupied, retained for diagnostics, validation and unchanged-placeholder
/// composition.
/// </summary>
internal readonly record struct SqlParameterOccurrence
{
    /// <summary>Creates an occurrence. The span must be non-empty; the owning template validates it.</summary>
    internal SqlParameterOccurrence(int occurrenceId, int bindingId, SqlStringLiteralRole role, int start, int length)
        : this(occurrenceId, bindingId, role, start, length, null)
    {
    }

    /// <summary>
    /// Creates an occurrence that also retains the exact placeholder text emitted at the span, so the
    /// template can verify the span against the original statement instead of trusting the recorder's
    /// length accounting.
    /// </summary>
    internal SqlParameterOccurrence(
        int occurrenceId,
        int bindingId,
        SqlStringLiteralRole role,
        int start,
        int length,
        string? placeholder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(occurrenceId);
        ArgumentOutOfRangeException.ThrowIfNegative(bindingId);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (placeholder is not null && placeholder.Length != length)
        {
            throw new ArgumentException(
                $"The recorded placeholder '{placeholder}' is not {length} characters long.",
                nameof(placeholder));
        }

        OccurrenceId = occurrenceId;
        BindingId = bindingId;
        Role = role;
        Start = start;
        Length = length;
        Placeholder = placeholder;
    }

    /// <summary>Statement-local occurrence identifier, unique within the template.</summary>
    internal int OccurrenceId { get; init; }

    /// <summary>The canonical binding ordinal this occurrence resolves to.</summary>
    internal int BindingId { get; init; }

    /// <summary>The syntactic role the placeholder occupies.</summary>
    internal SqlStringLiteralRole Role { get; init; }

    /// <summary>Start of the placeholder span in the original statement text.</summary>
    internal int Start { get; init; }

    /// <summary>Length of the placeholder span in the original statement text.</summary>
    internal int Length { get; init; }

    /// <summary>
    /// The exact placeholder text emitted at <see cref="Start"/>, or <see langword="null"/> when the
    /// occurrence was recorded without it. Used by the template to validate the recorded span.
    /// </summary>
    internal string? Placeholder { get; init; }

    /// <summary>End (exclusive) of the placeholder span.</summary>
    internal int End => checked(Start + Length);
}

/// <summary>Whether a statement segment is literal text or a parameter occurrence.</summary>
internal enum SqlParameterTemplateSegmentKind
{
    /// <summary>A run of statement text with no placeholders.</summary>
    Text,
    /// <summary>One parameter occurrence.</summary>
    Occurrence,
}

/// <summary>
/// One ordered piece of the original statement. Text segments are slices of the original text; an
/// occurrence segment points at its entry in the template's occurrence array. Empty text segments are
/// never produced, so adjacent placeholders are two consecutive occurrence segments.
/// </summary>
internal readonly record struct SqlParameterTemplateSegment
{
    private SqlParameterTemplateSegment(SqlParameterTemplateSegmentKind kind, int start, int length, int occurrenceIndex)
    {
        Kind = kind;
        Start = start;
        Length = length;
        OccurrenceIndex = occurrenceIndex;
    }

    /// <summary>The segment kind.</summary>
    internal SqlParameterTemplateSegmentKind Kind { get; init; }

    /// <summary>Start of the segment in the original statement text.</summary>
    internal int Start { get; init; }

    /// <summary>Length of the segment in the original statement text.</summary>
    internal int Length { get; init; }

    /// <summary>Index into the occurrence array for an occurrence segment, or <c>-1</c> for text.</summary>
    internal int OccurrenceIndex { get; init; }

    /// <summary>Creates a text segment.</summary>
    internal static SqlParameterTemplateSegment Text(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        return new SqlParameterTemplateSegment(SqlParameterTemplateSegmentKind.Text, start, length, -1);
    }

    /// <summary>Creates an occurrence segment.</summary>
    internal static SqlParameterTemplateSegment Occurrence(int start, int length, int occurrenceIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegative(occurrenceIndex);
        return new SqlParameterTemplateSegment(SqlParameterTemplateSegmentKind.Occurrence, start, length, occurrenceIndex);
    }
}

/// <summary>
/// Pure renderer used by <see cref="SqlParameterTemplate.Compose(SqlParameterOccurrenceRenderer)"/>:
/// returns the replacement text for an occurrence, or <see langword="null"/> to keep the original
/// placeholder. It must be pure and deterministic — the composition runs it once for sizing and once
/// for writing.
/// </summary>
internal delegate string? SqlParameterOccurrenceRenderer(in SqlParameterOccurrence occurrence);

/// <summary>
/// Immutable, statement-local template of a rendered command: the original statement text, its ordered
/// text/occurrence segments and its canonical binding descriptors. The template is produced once per
/// planning render and shared by every execution of that prepared plan; execution-time specialization
/// composes a new statement from it and never mutates it.
/// </summary>
/// <remarks>
/// Construction validates the whole model: canonical binding ordinals, unique binding names, unique
/// occurrence ids, resolvable binding ids, non-overlapping in-bounds placeholder spans and exact
/// original-text reconstruction. A structural defect is an <see cref="ArgumentException"/>; a
/// recorder that finalizes against a different amount of text is an
/// <see cref="InvalidOperationException"/>. Checked size accounting and composition overflow surface
/// as <see cref="OverflowException"/> — the documented contract of these members.
/// </remarks>
internal sealed class SqlParameterTemplate
{
    /// <summary>Syntax overhead of one <c>_utf8mb4 X'…'</c> literal: the prefix, the introducer and the quotes.</summary>
    internal const int HexLiteralSyntaxOverhead = 12;

    private readonly string _statementText;
    private readonly SqlParameterBinding[] _bindings;
    private readonly SqlParameterOccurrence[] _occurrences;
    private readonly SqlParameterTemplateSegment[] _segments;

    /// <summary>
    /// Builds and validates the immutable template. Occurrences may arrive in any order; the template
    /// stores them ordered by span and validates them against <paramref name="statementText"/>.
    /// </summary>
    internal SqlParameterTemplate(
        string statementText,
        IReadOnlyList<SqlParameterOccurrence> occurrences,
        IReadOnlyList<SqlParameterBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(statementText);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(bindings);

        _statementText = statementText;
        _bindings = ValidateBindings(bindings);
        _occurrences = ValidateOccurrences(statementText, occurrences, _bindings.Length);
        _segments = BuildSegments(statementText.Length, _occurrences);

        if (!string.Equals(ReconstructOriginalText(), statementText, StringComparison.Ordinal))
            throw new InvalidOperationException("The template segments do not reconstruct the original statement text.");
    }

    /// <summary>The unmodified original statement text.</summary>
    internal string StatementText => _statementText;

    /// <summary>The canonical binding descriptors, ordered by <see cref="SqlParameterBinding.BindingId"/>.</summary>
    internal ReadOnlySpan<SqlParameterBinding> Bindings => _bindings;

    /// <summary>The occurrences, ordered by placeholder span.</summary>
    internal ReadOnlySpan<SqlParameterOccurrence> Occurrences => _occurrences;

    /// <summary>The ordered text/occurrence segments of the statement.</summary>
    internal ReadOnlySpan<SqlParameterTemplateSegment> Segments => _segments;

    /// <summary>The number of parameter occurrences.</summary>
    internal int OccurrenceCount => _occurrences.Length;

    /// <summary>The number of canonical bindings.</summary>
    internal int BindingCount => _bindings.Length;

    /// <summary>Returns the canonical binding for <paramref name="bindingId"/>.</summary>
    internal SqlParameterBinding GetBinding(int bindingId)
    {
        if ((uint)bindingId >= (uint)_bindings.Length)
            throw new ArgumentOutOfRangeException(nameof(bindingId), bindingId, "The binding id does not resolve in this template.");
        return _bindings[bindingId];
    }

    /// <summary>
    /// Returns the index of the occurrence with <paramref name="occurrenceId"/> in
    /// <see cref="Occurrences"/>, which is also its index in the replacement array of
    /// <see cref="Compose(string?[])"/>.
    /// </summary>
    internal int IndexOfOccurrence(int occurrenceId)
    {
        for (var i = 0; i < _occurrences.Length; i++)
        {
            if (_occurrences[i].OccurrenceId == occurrenceId)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Composes a statement from the immutable segments: text segments are copied unchanged, an
    /// occurrence with a <see langword="null"/> replacement keeps its original placeholder, and any
    /// other occurrence writes the replacement text. No substring search, regex or LINQ is involved.
    /// </summary>
    internal string Compose(SqlParameterOccurrenceRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        if (_occurrences.Length == 0)
            return _statementText;

        var replacements = new string?[_occurrences.Length];
        for (var i = 0; i < _occurrences.Length; i++)
            replacements[i] = renderer(in _occurrences[i]);

        return Compose(replacements);
    }

    /// <summary>
    /// Composes a statement from precomputed replacements indexed like <see cref="Occurrences"/>
    /// (<see langword="null"/> keeps the original placeholder). This is the allocation-conscious
    /// execution seam: one result string, no intermediate rendering.
    /// </summary>
    internal string Compose(string?[] replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Length != _occurrences.Length)
        {
            throw new ArgumentException(
                $"Expected {_occurrences.Length} replacements (one per occurrence), got {replacements.Length}.",
                nameof(replacements));
        }

        var length = _statementText.Length;
        for (var i = 0; i < replacements.Length; i++)
        {
            if (replacements[i] is { } replacement)
                length = checked(length - _occurrences[i].Length + replacement.Length);
        }

        return string.Create(length, (Template: this, Replacements: replacements), static (destination, state) =>
        {
            var position = 0;
            foreach (var segment in state.Template._segments)
            {
                if (segment.Kind == SqlParameterTemplateSegmentKind.Text)
                {
                    state.Template._statementText.AsSpan(segment.Start, segment.Length).CopyTo(destination[position..]);
                    position += segment.Length;
                    continue;
                }

                var occurrence = state.Template._occurrences[segment.OccurrenceIndex];
                if (state.Replacements[segment.OccurrenceIndex] is { } replacement)
                {
                    replacement.AsSpan().CopyTo(destination[position..]);
                    position += replacement.Length;
                }
                else
                {
                    state.Template._statementText.AsSpan(occurrence.Start, occurrence.Length).CopyTo(destination[position..]);
                    position += occurrence.Length;
                }
            }
        });
    }

    /// <summary>
    /// Rebuilds the original statement from the segments. Used by construction validation and by the
    /// model tests as the reconstruction oracle.
    /// </summary>
    internal string ReconstructOriginalText()
    {
        var length = 0;
        foreach (var segment in _segments)
            length = checked(length + segment.Length);

        return string.Create(length, this, static (destination, template) =>
        {
            var position = 0;
            foreach (var segment in template._segments)
            {
                var start = segment.Kind == SqlParameterTemplateSegmentKind.Text
                    ? segment.Start
                    : template._occurrences[segment.OccurrenceIndex].Start;
                template._statementText.AsSpan(start, segment.Length).CopyTo(destination[position..]);
                position += segment.Length;
            }
        });
    }

    /// <summary>
    /// Accumulates the checked size of one MySQL-family hex payload: two characters per UTF-8 byte plus
    /// the literal syntax overhead. Overflow is an <see cref="OverflowException"/> raised before any
    /// command is installed; no arbitrary product length limit is introduced.
    /// </summary>
    internal static long AccumulateHexPayloadSize(long current, int utf8ByteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(current);
        ArgumentOutOfRangeException.ThrowIfNegative(utf8ByteCount);
        return checked(current + ((long)utf8ByteCount * 2) + HexLiteralSyntaxOverhead);
    }

    private static SqlParameterBinding[] ValidateBindings(IReadOnlyList<SqlParameterBinding> bindings)
    {
        var copy = new SqlParameterBinding[bindings.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < copy.Length; i++)
        {
            var binding = bindings[i];
            if (binding.BindingId != i)
            {
                throw new ArgumentException(
                    $"The binding at position {i} has id {binding.BindingId}; a statement-local binding id must equal its canonical ordinal.",
                    nameof(bindings));
            }

            if (!names.Add(binding.Name))
                throw new ArgumentException($"Duplicate statement-local binding name '{binding.Name}'.", nameof(bindings));

            copy[i] = binding;
        }

        return copy;
    }

    private static SqlParameterOccurrence[] ValidateOccurrences(
        string statementText,
        IReadOnlyList<SqlParameterOccurrence> occurrences,
        int bindingCount)
    {
        var copy = new SqlParameterOccurrence[occurrences.Count];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = occurrences[i];

        Array.Sort(copy, static (left, right) => left.Start.CompareTo(right.Start));

        var ids = new HashSet<int>();
        var cursor = 0;
        foreach (var occurrence in copy)
        {
            if (!ids.Add(occurrence.OccurrenceId))
                throw new ArgumentException($"Duplicate occurrence id {occurrence.OccurrenceId}.", nameof(occurrences));
            if (occurrence.BindingId >= bindingCount)
            {
                throw new ArgumentException(
                    $"Occurrence {occurrence.OccurrenceId} references the unknown binding id {occurrence.BindingId}.",
                    nameof(occurrences));
            }

            if (occurrence.Start < cursor)
                throw new ArgumentException($"Occurrence {occurrence.OccurrenceId} overlaps the previous occurrence.", nameof(occurrences));
            if (occurrence.End > statementText.Length)
            {
                throw new ArgumentException(
                    $"Occurrence {occurrence.OccurrenceId} extends past the end of the statement text.",
                    nameof(occurrences));
            }

            if (occurrence.Placeholder is { } placeholder
                && !statementText.AsSpan(occurrence.Start, occurrence.Length).SequenceEqual(placeholder.AsSpan()))
            {
                throw new ArgumentException(
                    $"Occurrence {occurrence.OccurrenceId} at {occurrence.Start} does not carry the recorded placeholder '{placeholder}'.",
                    nameof(occurrences));
            }

            cursor = occurrence.End;
        }

        return copy;
    }

    private static SqlParameterTemplateSegment[] BuildSegments(
        int textLength,
        SqlParameterOccurrence[] occurrences)
    {
        var segments = new List<SqlParameterTemplateSegment>(occurrences.Length * 2 + 1);
        var cursor = 0;
        for (var i = 0; i < occurrences.Length; i++)
        {
            var occurrence = occurrences[i];
            if (occurrence.Start > cursor)
                segments.Add(SqlParameterTemplateSegment.Text(cursor, occurrence.Start - cursor));
            segments.Add(SqlParameterTemplateSegment.Occurrence(occurrence.Start, occurrence.Length, i));
            cursor = occurrence.End;
        }

        if (cursor < textLength)
            segments.Add(SqlParameterTemplateSegment.Text(cursor, textLength - cursor));

        return [.. segments];
    }
}
