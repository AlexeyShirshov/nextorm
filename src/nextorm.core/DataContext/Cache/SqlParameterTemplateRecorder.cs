using System.Data;

namespace NextORM.Core;

/// <summary>
/// Render-local recorder for planner-owned parameter occurrences. It is created next to one statement
/// render and fed at placeholder-emission time, never by searching the finished SQL:
/// <list type="bullet">
/// <item><description><see cref="AppendText(int)"/> / <see cref="AppendText(string)"/> account for text the
/// renderer appended without a parameter slot;</description></item>
/// <item><description><see cref="RecordOccurrence(int, SqlStringLiteralRole, int)"/> records a placeholder
/// at the current end of the fragment;</description></item>
/// <item><description><see cref="AppendFragment(SqlParameterTemplateRecorder)"/> appends an already
/// recorded child fragment (a subquery or another composed piece), shifting every child occurrence span
/// by the parent's current length and remapping child bindings onto the parent's canonical binding
/// table by name.</description></item>
/// </list>
/// A suppressed <c>paramMode</c> emission records nothing, so it produces no occurrence. The recorder
/// owns no SQL builder: it only needs the number of characters emitted, which keeps it independent of
/// <c>SqlBuilder</c>'s internals.
/// </summary>
/// <remarks>
/// Length arithmetic is checked, so a recording that cannot be represented fails with
/// <see cref="OverflowException"/> instead of wrapping. Finalization owns the immutable template; the
/// recorder is single-use and further recording after <see cref="Finalize(string)"/> throws
/// <see cref="InvalidOperationException"/>.
/// </remarks>
internal sealed class SqlParameterTemplateRecorder
{
    private readonly List<SqlParameterBinding> _bindings = [];
    private readonly List<SqlParameterOccurrence> _occurrences = [];
    private int _length;
    private bool _finalized;

    /// <summary>The number of characters recorded so far in this fragment.</summary>
    internal int Length => _length;

    /// <summary>The number of canonical bindings registered so far.</summary>
    internal int BindingCount => _bindings.Count;

    /// <summary>The number of occurrences recorded so far.</summary>
    internal int OccurrenceCount => _occurrences.Count;

    /// <summary>
    /// Registers a canonical binding and returns its statement-local id (the canonical parameter
    /// ordinal). Names must be unique within the statement; repeated uses of one value reference the
    /// same binding instead of registering it twice.
    /// </summary>
    internal int RegisterBinding(
        string name,
        SqlParameterBindingOrigin origin,
        int? runtimeArgIndex = null,
        bool hasConversion = false,
        DbType? dbType = null,
        byte? precision = null,
        byte? scale = null)
    {
        ThrowIfFinalized();
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (FindBinding(name) >= 0)
            throw new ArgumentException($"Duplicate statement-local binding name '{name}'.", nameof(name));

        return AddBinding(name, origin, runtimeArgIndex, hasConversion, dbType, precision, scale);
    }

    /// <summary>
    /// Records a placeholder occurrence of <paramref name="placeholderLength"/> characters at the current
    /// end of the fragment and returns its occurrence id.
    /// </summary>
    internal int RecordOccurrence(int bindingId, SqlStringLiteralRole role, int placeholderLength)
    {
        ThrowIfFinalized();
        ArgumentOutOfRangeException.ThrowIfNegative(bindingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(placeholderLength);

        var start = _length;
        _length = checked(_length + placeholderLength);
        return AddOccurrence(bindingId, role, start, placeholderLength, null);
    }

    /// <summary>
    /// Records a placeholder whose text was just emitted at the current end of the fragment and returns
    /// its occurrence id. The text is retained so the finished template can validate the span.
    /// </summary>
    internal int RecordPlaceholder(int bindingId, SqlStringLiteralRole role, string placeholder)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        ThrowIfFinalized();
        ArgumentOutOfRangeException.ThrowIfNegative(bindingId);

        var start = _length;
        _length = checked(_length + placeholder.Length);
        return AddOccurrence(bindingId, role, start, placeholder.Length, placeholder);
    }

    /// <summary>
    /// Adds the occurrences of a child fragment whose binding ids are already statement-canonical (the
    /// renderer derives them from the statement's shared parameter list) at <paramref name="offset"/> in
    /// this fragment. The recorded length is left untouched, so fragments may be merged in any order and
    /// the caller advances the cursor to the final text length with <see cref="AdvanceTo"/>.
    /// </summary>
    internal void AppendOccurrences(SqlParameterTemplateRecorder fragment, int offset)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ThrowIfFinalized();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        foreach (var occurrence in fragment._occurrences)
        {
            _occurrences.Add(new SqlParameterOccurrence(
                _occurrences.Count,
                occurrence.BindingId,
                occurrence.Role,
                checked(offset + occurrence.Start),
                occurrence.Length,
                occurrence.Placeholder));
        }
    }

    /// <summary>
    /// Shifts every occurrence that starts at or after <paramref name="position"/> by
    /// <paramref name="delta"/> characters and grows the recorded length accordingly. Used when the
    /// dialect rewrites the finished statement text by inserting a fragment (a hint comment or a folded
    /// option clause) after the renderer accounted for the original text.
    /// </summary>
    internal void RelocateInsertion(int position, int delta)
    {
        ThrowIfFinalized();
        if (delta <= 0)
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "An insertion must grow the text.");
        if ((uint)position > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(position), position, "The insertion position is outside the recorded fragment.");

        for (var i = 0; i < _occurrences.Count; i++)
        {
            var occurrence = _occurrences[i];
            if (occurrence.Start < position)
                continue;

            _occurrences[i] = new SqlParameterOccurrence(
                occurrence.OccurrenceId,
                occurrence.BindingId,
                occurrence.Role,
                checked(occurrence.Start + delta),
                occurrence.Length,
                occurrence.Placeholder);
        }

        _length = checked(_length + delta);
    }

    /// <summary>Accounts for <paramref name="charCount"/> text characters appended without a slot.</summary>
    internal void AppendText(int charCount)
    {
        ThrowIfFinalized();
        ArgumentOutOfRangeException.ThrowIfNegative(charCount);
        _length = checked(_length + charCount);
    }

    /// <summary>Accounts for text appended without a slot.</summary>
    internal void AppendText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        AppendText(text.Length);
    }

    /// <summary>
    /// Advances the recorded cursor to an absolute emitted length. The intended call at a placeholder
    /// site is <c>recorder.AdvanceTo(builder.Length - placeholder.Length)</c> followed by
    /// <see cref="RecordOccurrence"/>; the cursor never moves backwards.
    /// </summary>
    internal void AdvanceTo(int emittedLength)
    {
        ThrowIfFinalized();
        if (emittedLength < _length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(emittedLength),
                emittedLength,
                $"The recorded length {_length} cannot shrink to {emittedLength}.");
        }

        _length = emittedLength;
    }

    /// <summary>
    /// Appends an already recorded child fragment at the current end of this fragment. Child occurrence
    /// spans are shifted by the current length, new occurrence ids are assigned, and child bindings are
    /// remapped onto this recorder's canonical binding table by name (registering any binding this
    /// recorder does not know yet). A fragment can be appended more than once; earlier copies are not
    /// affected by later recording into the child.
    /// </summary>
    internal void AppendFragment(SqlParameterTemplateRecorder fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ThrowIfFinalized();
        if (ReferenceEquals(fragment, this))
            throw new ArgumentException("A recorder cannot append itself as a fragment.", nameof(fragment));

        var offset = _length;

        int[]? remap = null;
        if (fragment._bindings.Count != 0)
        {
            remap = new int[fragment._bindings.Count];
            for (var i = 0; i < remap.Length; i++)
            {
                var child = fragment._bindings[i];
                var existing = FindBinding(child.Name);
                if (existing < 0)
                {
                    existing = AddBinding(
                        child.Name,
                        child.Origin,
                        child.RuntimeArgIndex,
                        child.HasConversion,
                        child.DbType,
                        child.Precision,
                        child.Scale);
                }
                else if (!HasSameMetadata(_bindings[existing], child))
                {
                    throw new ArgumentException(
                        $"The fragment binding '{child.Name}' conflicts with the binding already registered under that name.",
                        nameof(fragment));
                }

                remap[i] = existing;
            }
        }

        foreach (var occurrence in fragment._occurrences)
        {
            var bindingId = occurrence.BindingId;
            if (remap is not null)
            {
                if ((uint)bindingId >= (uint)remap.Length)
                {
                    throw new ArgumentException(
                        $"The fragment occurrence {occurrence.OccurrenceId} references the unknown binding id {bindingId}.",
                        nameof(fragment));
                }

                bindingId = remap[bindingId];
            }

            _occurrences.Add(new SqlParameterOccurrence(
                _occurrences.Count,
                bindingId,
                occurrence.Role,
                checked(offset + occurrence.Start),
                occurrence.Length,
                occurrence.Placeholder));
        }

        _length = checked(_length + fragment._length);
    }

    /// <summary>
    /// Freezes the recorder and produces the immutable statement template. <paramref name="statementText"/>
    /// must be exactly the text the recorder accounted for; any other length means the recording and the
    /// rendered statement came from different fragments and throws <see cref="InvalidOperationException"/>.
    /// </summary>
    internal SqlParameterTemplate Finalize(string statementText)
    {
        ArgumentNullException.ThrowIfNull(statementText);
        ThrowIfFinalized();
        if (_length != statementText.Length)
        {
            throw new InvalidOperationException(
                $"The recorder accounted for {_length} characters but the statement has {statementText.Length}; " +
                "the occurrence spans would not be contiguous with the original text.");
        }

        _finalized = true;
        return new SqlParameterTemplate(statementText, _occurrences, _bindings);
    }

    /// <summary>
    /// Freezes the recorder against a canonical binding table that the caller derived from the statement's
    /// ordered parameter list. Used by the renderer, whose occurrences already carry canonical binding ids,
    /// so the recorder does not own the binding descriptors.
    /// </summary>
    internal SqlParameterTemplate Finalize(string statementText, IReadOnlyList<SqlParameterBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(statementText);
        ArgumentNullException.ThrowIfNull(bindings);
        ThrowIfFinalized();
        if (_length != statementText.Length)
        {
            throw new InvalidOperationException(
                $"The recorder accounted for {_length} characters but the statement has {statementText.Length}; " +
                "the occurrence spans would not be contiguous with the original text.");
        }

        _finalized = true;
        return new SqlParameterTemplate(statementText, _occurrences, bindings);
    }

    private int AddOccurrence(
        int bindingId,
        SqlStringLiteralRole role,
        int start,
        int length,
        string? placeholder)
    {
        var occurrenceId = _occurrences.Count;
        _occurrences.Add(new SqlParameterOccurrence(occurrenceId, bindingId, role, start, length, placeholder));
        return occurrenceId;
    }

    private int AddBinding(
        string name,
        SqlParameterBindingOrigin origin,
        int? runtimeArgIndex,
        bool hasConversion,
        DbType? dbType,
        byte? precision,
        byte? scale)
    {
        var bindingId = _bindings.Count;
        _bindings.Add(new SqlParameterBinding(bindingId, name, origin, runtimeArgIndex, hasConversion, dbType, precision, scale));
        return bindingId;
    }

    private int FindBinding(string name)
    {
        for (var i = 0; i < _bindings.Count; i++)
        {
            if (string.Equals(_bindings[i].Name, name, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static bool HasSameMetadata(SqlParameterBinding left, SqlParameterBinding right) =>
        left.Origin == right.Origin
        && left.RuntimeArgIndex == right.RuntimeArgIndex
        && left.HasConversion == right.HasConversion
        && left.DbType == right.DbType
        && left.Precision == right.Precision
        && left.Scale == right.Scale;

    private void ThrowIfFinalized()
    {
        if (_finalized)
            throw new InvalidOperationException("The recorder was already finalized and cannot record more text or occurrences.");
    }
}
