using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NextORM.Core.Probe;

/// <summary>
/// T297 r=4 P02 phase attribution probe. Temporary instrumentation compiled only into the throwaway
/// probe copies under /tmp/opencode/t297-p02; never shipped.
/// <para>
/// Raw <see cref="Stopwatch.GetTimestamp"/> tick accumulators plus call counters. The probe is
/// single-threaded by construction (one harness process, one pinned core) and uses plain static
/// fields; the hot path performs no allocation and takes no lock.
/// </para>
/// <para>
/// Timers are mutually exclusive at the leaf level: <see cref="Prepare"/> contains the nested
/// Plan/Bind/EnumCreate/CowShape spans and must be corrected by subtraction during analysis, never by
/// double-counting inside the probe.
/// </para>
/// </summary>
public static class T297PhaseProbe
{
    /// <summary>Number of tracked phases (the width of every accumulator array).</summary>
    public const int PhaseCount = 32;

    /// <summary>Fluent command construction gap between two terminal calls.</summary>
    public const int Construct = 0;
    /// <summary><c>GetPreparedQueryCommand</c> span (contains Plan/Bind/EnumCreate/CowShape).</summary>
    public const int Prepare = 1;
    /// <summary>Plan-key construction plus plan-store lookup on the cached path.</summary>
    public const int Plan = 2;
    /// <summary><c>RefreshParameters</c> on a cached parameterized command.</summary>
    public const int Bind = 3;
    /// <summary>Lazy <c>CreateResultSetEnumerator</c> on a cached command.</summary>
    public const int EnumCreate = 4;
    /// <summary><c>RefreshInValuesShape</c> captured-collection shape refresh.</summary>
    public const int CowShape = 5;
    /// <summary><c>GetDbCommandCore</c> parameter/connection binding.</summary>
    public const int GetDbCommand = 6;
    /// <summary>Reader creation (<c>RunReader</c>/<c>RunReaderAsync</c>).</summary>
    public const int Execute = 7;
    /// <summary><c>DbDataReader.Read()</c> calls in the materialization loop.</summary>
    public const int ReadRows = 8;
    /// <summary>Row mapper calls in the materialization loop.</summary>
    public const int MapRows = 9;
    /// <summary>Tree-B-only COW description split (MarkShared/clone/WriteDescription).</summary>
    public const int CowDescription = 10;
    /// <summary>Tree-B-only shared-description adoption in the clone constructor.</summary>
    public const int CowClone = 11;
    /// <summary>Counter-only entry count for <c>QueryPreparer.Prepare</c> (expected once per query terminal).</summary>
    public const int PrepareCall = 12;
    /// <summary><c>GetPreparedQueryCommand</c> planner preamble: the data-modifying-CTE gate.</summary>
    public const int PlannerGate = 13;
    /// <summary><c>GetPreparedQueryCommand</c> planner preamble: the raw-recipe probe and apply block.</summary>
    public const int PlannerRecipe = 14;
    /// <summary>The <c>PrepareCommand</c> call itself (contains the <c>QueryPreparer.Prepare</c> sub-steps).</summary>
    public const int PreparePipeline = 15;
    /// <summary><c>QueryPreparer.Prepare</c> preamble: <c>InvalidatePlanKey</c> plus resolved-state setup.</summary>
    public const int PrepPreamble = 16;
    /// <summary>Navigation expansion into LEFT JOINs plus provider outer-join null settings.</summary>
    public const int NavExpand = 17;
    /// <summary>Primary from-source resolution (<c>GetFrom</c>).</summary>
    public const int GetFrom = 18;
    /// <summary><c>PrepareFrom</c> plus primary source lock attachment.</summary>
    public const int PrepareFromStep = 19;
    /// <summary><c>PrepareJoin</c>.</summary>
    public const int PrepareJoinStep = 20;
    /// <summary><c>PrepareColumns</c> plus projection nullability validation.</summary>
    public const int PrepareColumnsStep = 21;
    /// <summary><c>PrepareWhere</c> including main-source filter injection.</summary>
    public const int PrepareWhereStep = 22;
    /// <summary><c>PreparePreWhere</c>.</summary>
    public const int PreparePreWhereStep = 23;
    /// <summary><c>PrepareArrayJoin</c>.</summary>
    public const int PrepareArrayJoinStep = 24;
    /// <summary>Grouping cluster: grouping, limit-by, distinct-on, extreme-row, per-parent partition, windows.</summary>
    public const int PrepareGroupingStep = 25;
    /// <summary><c>PrepareSorting</c>.</summary>
    public const int PrepareSortingStep = 26;
    /// <summary>Set-operation (union) preparation.</summary>
    public const int PrepareUnionStep = 27;
    /// <summary><c>PrepareCtes</c>.</summary>
    public const int PrepareCtesStep = 28;
    /// <summary><c>PrepareHints</c>.</summary>
    public const int PrepareHintsStep = 29;
    /// <summary>Unkeyed tuple and scalar captured-collection scans.</summary>
    public const int UnkeyedScans = 30;
    /// <summary>Final from-expression plan hash.</summary>
    public const int FromHash = 31;

    /// <summary>Stable phase names, indexed by the phase constants above.</summary>
    public static readonly string[] PhaseNames =
    {
        "construct", "prepare", "plan", "bind", "enum_create", "cow_shape",
        "get_db_command", "execute", "read_rows", "map_rows", "cow_description", "cow_clone",
        "prepare_call", "planner_gate", "planner_recipe", "prepare_pipeline", "prep_preamble",
        "nav_expand", "get_from", "prepare_from", "prepare_join", "prepare_columns", "prepare_where",
        "prepare_prewhere", "prepare_arrayjoin", "prepare_grouping", "prepare_sorting",
        "prepare_union", "prepare_ctes", "prepare_hints", "unkeyed_scans", "from_hash",
    };

    private static readonly long[] _ticks = new long[PhaseCount];
    private static readonly long[] _calls = new long[PhaseCount];

    // Per-thread by contract; plain static is enough for the single-threaded probe process.
    private static long _lastEnd;

    /// <summary>Stopwatch frequency (ticks per second) for converting accumulator values.</summary>
    public static long Frequency => Stopwatch.Frequency;

    /// <summary>Returns the current <see cref="Stopwatch.GetTimestamp"/> value without side effects.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Tick() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Terminal entry: charges the gap since the previous terminal end to <see cref="Construct"/> and
    /// returns the entry tick that <see cref="OnPrepare"/> consumes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long OnEntry()
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastEnd != 0)
            _ticks[Construct] += now - _lastEnd;
        _calls[Construct]++;
        return now;
    }

    /// <summary>Charges entry-to-return time of <c>GetPreparedQueryCommand</c> to <see cref="Prepare"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnPrepare(long entry)
    {
        _ticks[Prepare] += Stopwatch.GetTimestamp() - entry;
        _calls[Prepare]++;
    }

    /// <summary>Records the terminal end used by the next <see cref="OnEntry"/> Construct gap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnEnd() => _lastEnd = Stopwatch.GetTimestamp();

    /// <summary>Bumps one phase's call counter without charging any time (counter-only phases).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Count(int phase) => _calls[phase]++;

    /// <summary>Charges <paramref name="start"/>-to-now time to one leaf phase and bumps its counter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(int phase, long start)
    {
        _ticks[phase] += Stopwatch.GetTimestamp() - start;
        _calls[phase]++;
    }

    /// <summary>Zeroes every accumulator, call counter and the terminal-end marker.</summary>
    public static void Reset()
    {
        Array.Clear(_ticks);
        Array.Clear(_calls);
        _lastEnd = 0;
    }

    /// <summary>Returns a detached copy of the current accumulators.</summary>
    public static ProbeSnapshot TakeSnapshot() => new((long[])_ticks.Clone(), (long[])_calls.Clone());
}

/// <summary>Immutable snapshot of the probe accumulators (arrays are private copies).</summary>
public readonly struct ProbeSnapshot
{
    /// <summary>Accumulated raw Stopwatch ticks per phase, indexed by the phase constants.</summary>
    public readonly long[] Ticks;
    /// <summary>Call counters per phase, indexed by the phase constants.</summary>
    public readonly long[] Calls;

    /// <summary>Creates a detached snapshot from private accumulator copies.</summary>
    /// <param name="ticks">Tick accumulator copies.</param>
    /// <param name="calls">Call-counter copies.</param>
    public ProbeSnapshot(long[] ticks, long[] calls)
    {
        Ticks = ticks;
        Calls = calls;
    }
}
