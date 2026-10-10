using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using NextORM.Core;
using NextORM.Generated.nextorm_benchmark;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>Benchmark-local root mapped to the shared <c>large_table</c> (issue #160 perf harness).</summary>
[SqlTable("large_table")]
public sealed class AliasBenchOrder
{
    public long Id { get; set; }
}

/// <summary>Benchmark-local join source mapped to the shared <c>simple_entity</c>; joined twice.</summary>
[SqlTable("simple_entity")]
public sealed class AliasBenchPerson
{
    public int Id { get; set; }
}

/// <summary>
/// Issue #160 alias-mixed paired-arm harness. Every arm executes the SAME SQL over the SAME data: a
/// three-slot join <c>large_table t1 join simple_entity t2 on t1.id = t2.id join simple_entity t3 on
/// t2.id = t3.id where t1.id = @p</c>, filtered to one row. The pure positional chain is the
/// zero-overhead comparison arm; the mixed arms route the same chain through the generated
/// <c>Alias.*</c> surface and the <c>JoinAlias</c> seam (positional->alias and alias->positional).
/// DB cost cancels, so the delta is the builder/renderer/plan-cache cost of the alias seam.
/// </summary>
[BenchmarkCategory("alias-mixed")]
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkAliasMixedPlan
{
    private const int Iterations = 100;

    // simple_entity holds ids 1..10 in the shared benchmark database; keep the filtered row non-empty.
    private const int MaxFilterId = 10;

    private readonly IDataContext _db;
    private readonly EntityBuilder<AliasBenchOrder> _orders;
    private readonly EntityBuilder<AliasBenchPerson> _people;

    public SqliteBenchmarkAliasMixedPlan()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _orders = _db.From<AliasBenchOrder>();
        _people = _db.From<AliasBenchPerson>();

        // Prove the paired arms execute byte-identical SQL over the same data. The probe uses a
        // dedicated context so recording allocations never enter the measured arms.
        VerifyIdenticalSql();

        // Warm every measured plan cache so the loops only exercise the cached path.
        _ = Positional_Query(_orders, _people).ToList(1);
        _ = PositionalAlias_Query(_orders, _people).ToList(1);
        _ = AliasPositional_Query(_orders, _people).ToList(1);
    }

    private static void VerifyIdenticalSql()
    {
        var recorder = new SqlRecordingInterceptor();
        var probeBuilder = new DataContextBuilder();
        probeBuilder.UseSqlite(BenchDb.FilePath);
        probeBuilder.AddInterceptor(recorder);
        var probe = probeBuilder.CreateDataContext();
        ((IConnectionManager)probe).EnsureConnectionOpen();

        try
        {
            var orders = probe.From<AliasBenchOrder>();
            var people = probe.From<AliasBenchPerson>();

            var positional = Positional_Query(orders, people).ToList(1);
            var positionalSql = recorder.Statements[^1];
            recorder.Statements.Clear();

            var positionalAlias = PositionalAlias_Query(orders, people).ToList(1);
            var positionalAliasSql = recorder.Statements[^1];
            recorder.Statements.Clear();

            var aliasPositional = AliasPositional_Query(orders, people).ToList(1);
            var aliasPositionalSql = recorder.Statements[^1];

            if (!string.Equals(positionalSql, positionalAliasSql, StringComparison.Ordinal)
                || !string.Equals(positionalSql, aliasPositionalSql, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "alias-mixed arms executed different SQL:" + Environment.NewLine
                    + positionalSql + Environment.NewLine + positionalAliasSql + Environment.NewLine + aliasPositionalSql);

            if (!positional.SequenceEqual(positionalAlias) || !positional.SequenceEqual(aliasPositional))
                throw new InvalidOperationException("alias-mixed arms returned different results on identical SQL/data");
        }
        finally
        {
            (probe as IDisposable)?.Dispose();
        }
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(eventData.Sql ?? command.CommandText);
    }

    /// <summary>Pure positional three-slot chain: root -> positional -> positional (baseline / zero-overhead arm).</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("alias-mixed")]
    public int Positional_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var id = (i % MaxFilterId) + 1;
            foreach (var row in Positional_Query(_orders, _people).ToList(id))
                sum += row;
        }

        return sum;
    }

    /// <summary>Positional -> alias: root -> positional -> alias (the same SQL, alias step routed through the seam).</summary>
    [Benchmark]
    [BenchmarkCategory("alias-mixed")]
    public int PositionalAlias_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var id = (i % MaxFilterId) + 1;
            foreach (var row in PositionalAlias_Query(_orders, _people).ToList(id))
                sum += row;
        }

        return sum;
    }

    /// <summary>Alias -> positional: root -> alias -> positional (inverse direction, same SQL).</summary>
    [Benchmark]
    [BenchmarkCategory("alias-mixed")]
    public int AliasPositional_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var id = (i % MaxFilterId) + 1;
            foreach (var row in AliasPositional_Query(_orders, _people).ToList(id))
                sum += row;
        }

        return sum;
    }

    /// <summary>No DB: isolates plan-cache lookup + parameter extraction on the alias-mixed chain.</summary>
    [Benchmark]
    [BenchmarkCategory("alias-mixed")]
    public IPreparedQueryCommand<int> AliasPositional_PlanOnly_Param()
    {
        IPreparedQueryCommand<int>? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _db.GetPreparedQueryCommand(AliasPositional_Query(_orders, _people), false, true, CancellationToken.None);

        return result!;
    }

    /// <summary>No DB: root alias (slot 1) followed by a positional join, exercising the WithAlias surface.</summary>
    [Benchmark]
    [BenchmarkCategory("alias-mixed")]
    public IPreparedQueryCommand<int> RootAliasPositional_PlanOnly_Param()
    {
        IPreparedQueryCommand<int>? result = null;
        for (var i = 0; i < Iterations; i++)
            result = _db.GetPreparedQueryCommand(RootAliasPositional_Query(_orders, _people), false, true, CancellationToken.None);

        return result!;
    }

    private static QueryCommand<int> Positional_Query(EntityBuilder<AliasBenchOrder> orders, EntityBuilder<AliasBenchPerson> people) =>
        orders
            .Join(people, (t1, t2) => t1.Id == t2.Id)
            .Join(people, (p, t3) => p.Item2.Id == t3.Id)
            .Where(p => p.Item1.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => p.Item3.Id);

    private static QueryCommand<int> PositionalAlias_Query(EntityBuilder<AliasBenchOrder> orders, EntityBuilder<AliasBenchPerson> people) =>
        orders
            .Join(people, (t1, t2) => t1.Id == t2.Id)
            .Join<AliasBenchPerson>(people, (p, t3) => p.Item2.Id == t3.Id, Alias.Mid)
            .Where(p => p.Item1.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => p.Item3.Id);

    private static QueryCommand<int> AliasPositional_Query(EntityBuilder<AliasBenchOrder> orders, EntityBuilder<AliasBenchPerson> people) =>
        orders
            .Join<AliasBenchPerson>(people, (t1, t2) => t1.Id == t2.Id, Alias.Lead)
            .Join(people, (p, t3) => p.Lead.Id == t3.Id)
            .Where(p => p.Item1.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => p.Item3.Id);

    private static QueryCommand<int> RootAliasPositional_Query(EntityBuilder<AliasBenchOrder> orders, EntityBuilder<AliasBenchPerson> people) =>
        orders
            .WithAlias(Alias.Root)
            .Join(people, (p, t3) => p.Root.Id == t3.Id)
            .Where(p => p.Item2.Id == SqlFunctions.Parameter<int>(0))
            .Select(p => p.Item2.Id);
}
