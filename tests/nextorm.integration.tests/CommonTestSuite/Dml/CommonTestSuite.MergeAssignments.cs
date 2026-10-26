using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T301 (#301) live assignment semantics for <c>MERGE</c>/upsert expressions. Every fact runs against the
/// four providers that derive <see cref="CommonTestSuite"/> (SQLite, PostgreSQL, SQL Server, MySQL), and the
/// fixture is class-owned (<c>t301a_*</c> created from portable DDL) so the suite never depends on a
/// provider's seed schema.
/// <para>
/// The target row is seeded with <c>a = 7, b = 3, total = 1000</c> and the source row with
/// <c>a = 1, b = 10, total = 0</c>, so the three roles are numerically distinguishable: a source-bound
/// <c>s.A + s.B</c> is <c>11</c> (never the target-bound <c>10</c>, the prior <c>1000</c>, the concatenation
/// <c>110</c>, or either column alone <c>1</c>/<c>10</c>), and a two-role expression
/// <c>t.Total + (s.A * 1000) + s.B</c> is <c>2010</c> (a source-bound target read gives <c>1010</c>,
/// target-bound source reads give <c>8003</c>).
/// </para>
/// <para>
/// Refusals are asserted as named diagnostics with the recorded command count unchanged and the stored row
/// read back untouched: no invalid SQL is sent to a server to discover the limitation. Every observed
/// statement and diagnostic is written to the test output, so a run log carries the per-engine matrix.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string T301ADdl = """
        drop table if exists t301a_target;
        """;

    // "total" carries a server default so the proposed-row fact can omit it from the INSERT: the native
    // proposed row then differs from the original source row (whose Total is 0) on every advertised engine.
    private const string T301ACreate = """
        create table t301a_target (id int not null primary key, a int not null, b int not null, total int not null default 500);
        """;

    private const int T301ATargetId = 7301;
    private const int T301AMissingId = 7302;

    // E13: the proposed row is the row the server would insert, so a BEFORE INSERT trigger rewrite is part
    // of it. The trigger overwrites "total" with 777 whenever the marker column is set, which keeps the
    // trigger-written value distinguishable from the column default (500), the source value (0) and the
    // pre-update target value (1000). PostgreSQL only: PL/pgSQL DDL plus the ON CONFLICT proposed row.
    // Each DDL step is a separate command, so no batch protocol behaviour is assumed.
    private const string T301BDdl = """
        drop table if exists t301b_trigger;
        """;

    private const string T301BCreate = """
        create table t301b_trigger (id int not null primary key, a int not null, b int not null, total int not null default 500, marker int not null default 0);
        """;

    private const string T301BTriggerFunction = """
        create or replace function t301b_trigger_fn() returns trigger language plpgsql as $t301b$
        begin
            if new.marker <> 0 then
                new.total := 777;
            end if;
            return new;
        end
        $t301b$;
        """;

    private const string T301BTrigger = """
        create trigger t301b_before_insert before insert on t301b_trigger for each row execute function t301b_trigger_fn();
        """;

    private const int T301BTargetId = 7311;
    private const int T301BTriggerTotal = 777;
    private const int T301BDefaultTotal = 500;

    private DataContext T301ADataContext => (DataContext)_sut.DataProvider;

    /// <summary>True when the active provider renders a general multi-branch <c>MERGE</c> statement.</summary>
    private bool T301ASupportsFullMerge => T301ADataContext.Dialect.SupportsMergeStatement;

    /// <summary>True when the active provider has a native key-upsert form with a proposed row reference.</summary>
    private bool T301ASupportsNativeUpsert =>
        T301ADataContext.Dialect.SupportsOnConflict || T301ADataContext.Dialect.SupportsOnDuplicateKey;

    /// <summary>Recreates the fixture tables and the deterministic target row.</summary>
    private void T301AResetFixture()
    {
        var ctx = _sut.DataProvider;
        T301AExecute(ctx, T301ADdl);
        T301AExecute(ctx, T301ACreate);
        T301AExecute(ctx, $"insert into t301a_target (id, a, b, total) values ({T301ATargetId}, 7, 3, 1000)");
    }

    private static void T301AExecute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private MergeAssignTarget T301ARead(int id)
    {
        var rows = _sut.DataProvider.From<MergeAssignTarget>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.A, x.B, x.Total })
            .ToList();

        rows.Should().ContainSingle();
        return new MergeAssignTarget { Id = rows[0].Id, A = rows[0].A, B = rows[0].B, Total = rows[0].Total };
    }

    private static MergeAssignTarget T301ASource(int id, int a, int b) => new() { Id = id, A = a, B = b, Total = 0 };

    /// <summary>Recreates the PostgreSQL trigger fixture (table, trigger function, BEFORE INSERT trigger) and its target row.</summary>
    private void T301BResetFixture()
    {
        var ctx = _sut.DataProvider;
        T301AExecute(ctx, T301BDdl);
        T301AExecute(ctx, T301BCreate);
        T301AExecute(ctx, T301BTriggerFunction);
        T301AExecute(ctx, T301BTrigger);
        T301AExecute(ctx, $"insert into t301b_trigger (id, a, b, total) values ({T301BTargetId}, 7, 3, 1000)");
    }

    private MergeTriggerTarget T301BRead()
        => _sut.DataProvider.From<MergeTriggerTarget>().Where(x => x.Id == T301BTargetId).Single();

    /// <summary>The original source row for the trigger fixture: marker set, so the insert path rewrites <c>total</c>.</summary>
    private static MergeTriggerTarget T301BSource() => new() { Id = T301BTargetId, A = 1, B = 10, Total = 0, Marker = 1 };

    private MergeCommandRecorder T301ARecord(IDataContext ctx)
    {
        var recorder = new MergeCommandRecorder();
        ((DataContext)ctx).AddInterceptor(recorder);
        return recorder;
    }

    private static void T301ALog(string message) => T301MatrixLog.Write(message);

    /// <summary>Records the observed statement (text plus bound parameters) of one form on this engine.</summary>
    private void T301ALogCommand(string label, MergeCommandRecorder recorder, string table = "t301a_target")
    {
        var command = recorder.Commands.LastOrDefault(c => c.Sql.Contains(table));
        var parameters = command is null
            ? "-"
            : string.Join(", ", command.Parameters.Select(p => $"{p.Name}={p.Value ?? "null"}"));
        T301ALog($"[T301:{T301ADataContext.Dialect.GetType().Name}] {label} | executes | {command?.Sql ?? "<none>"} | {parameters}");
    }

    /// <summary>Records the observed refusal diagnostic of one form on this engine.</summary>
    private void T301ALogRefusal(string label, string message)
        => T301ALog($"[T301:{T301ADataContext.Dialect.GetType().Name}] {label} | refuses | - | {message}");

    /// <summary>
    /// R1/R04: on a full-<c>MERGE</c> engine the right-hand side <c>s.A + s.B</c> reads the source row, the
    /// destination column is the target row, and the target's prior value survives as a third, distinct
    /// number. The round trip goes through the library read path.
    /// </summary>
    [Fact]
    public void MergeAssignments_FullMerge_SourceExpressionShouldBindTheSourceRow()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var merge = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((t, s) => new { Total = s.A + s.B })
            .Merge();

        if (!T301ASupportsFullMerge)
        {
            var refusal = merge.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("full-merge source expression", refusal.Message);
            refusal.Message.Should().Match("Provider '*does not support MERGE assignment expressions*");
            recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
            recorder.Executing.Should().Be(0, "no statement is executed for an unsupported form");
            T301ARead(T301ATargetId).Total.Should().Be(1000);
            return;
        }

        merge.Should().NotThrow();

        recorder.Commands.Should().ContainSingle(command => command.Sql.Contains("t301a_target"));
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once (no per-row replay)");
        T301ALogCommand("full-merge source expression", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(11, "the right-hand side binds the source row (1 + 10)");
        T301ARead(T301ATargetId).Total.Should().NotBe(110, "the value is an arithmetic sum, not a concatenation");
        T301ARead(T301ATargetId).Total.Should().NotBe(1000, "the target's prior value is not the written value");
        T301ARead(T301ATargetId).Total.Should().NotBe(1, "a single source column is not the written value");
        T301ARead(T301ATargetId).Total.Should().NotBe(10, "a single source column is not the written value");
    }

    /// <summary>
    /// R04: one expression that reads both roles at once — <c>t.Total + (s.A * 1000) + s.B</c> — proves the
    /// two parameters are resolved against different rows. <c>2010</c> only comes out of the documented
    /// binding; a source-bound target read yields <c>1010</c> and target-bound source reads yield <c>8003</c>.
    /// </summary>
    [Fact]
    public void MergeAssignments_FullMerge_TargetAndSourceInOneExpressionShouldBindEachRoleSeparately()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var merge = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((t, s) => new { Total = t.Total + (s.A * 1000) + s.B })
            .Merge();

        if (!T301ASupportsFullMerge)
        {
            var refusal = merge.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("full-merge target+source expression", refusal.Message);
            refusal.Message.Should().Match("Provider '*does not support MERGE assignment expressions*");
            recorder.Commands.Should().BeEmpty();
            T301ARead(T301ATargetId).Total.Should().Be(1000);
            return;
        }

        merge.Should().NotThrow();
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");

        T301ALogCommand("full-merge target+source expression", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(2010, "1000 (target) + 1 * 1000 (source) + 10 (source)");
        T301ARead(T301ATargetId).Total.Should().NotBe(1010, "the target read did not bind the source row");
        T301ARead(T301ATargetId).Total.Should().NotBe(8003, "the source reads did not bind the target row");
    }

    /// <summary>
    /// R05: the insert arm of a full <c>MERGE</c> reads the unmatched source row, including a computed
    /// <c>Total</c>; the inserted row is read back through the library.
    /// </summary>
    [Fact]
    public void MergeAssignments_FullMerge_InsertArmShouldReadTheUnmatchedSourceRow()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var merge = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301AMissingId, 1, 10))
            .On((t, s) => t.Id == s.Id)
            .WhenMatched().ThenUpdate((t, s) => new { Total = t.Total + s.B })
            .WhenNotMatched().ThenInsert(s => new { Id = s.Id, A = s.A, B = s.B, Total = s.A + s.B })
            .Merge();

        if (!T301ASupportsFullMerge)
        {
            var refusal = merge.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("full-merge insert arm", refusal.Message);
            refusal.Message.Should().Match("Provider '*does not support MERGE assignment expressions*");
            recorder.Commands.Should().BeEmpty();
            return;
        }

        merge.Should().NotThrow();

        T301ALogCommand("full-merge insert arm", recorder);

        var inserted = T301ARead(T301AMissingId);
        inserted.A.Should().Be(1);
        inserted.B.Should().Be(10);
        inserted.Total.Should().Be(11, "the insert arm reads the source row it inserts");
    }

    /// <summary>
    /// R04/R09 binding proof: <c>s.A + s.B</c> is commutative, so a column-order swap inside the source row is
    /// invisible to it; <c>s.A - s.B</c> is not. With <c>A = 1</c>, <c>B = 10</c> the stored <c>-9</c> differs
    /// from <c>s.B - s.A</c> (<c>9</c>, the swapped binding), from the target's own <c>t.A - t.B</c> (<c>4</c>,
    /// the role swap) and from the target's previous <c>Total</c> (<c>1000</c>). Every engine executes it.
    /// </summary>
    [Fact]
    public void MergeAssignments_KeyUpsert_SourceColumnOrderShouldBindEachColumnByName()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A - s.B })
            .WhenNotMatchedInsert()
            .Merge();

        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        T301ALogCommand("key-upsert source column order", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(-9, "the right-hand side binds source A (1) then source B (10)");
        T301ARead(T301ATargetId).Total.Should().NotBe(9, "the source columns were not bound in swapped order");
        T301ARead(T301ATargetId).Total.Should().NotBe(4, "the target row's own columns are not the source row");
        T301ARead(T301ATargetId).Total.Should().NotBe(1000, "the target's prior value is not the written value");
    }

    /// <summary>
    /// R04/R06: the native key-upsert form (all four engines, each through its own lowering — <c>ON
    /// CONFLICT</c>, <c>ON DUPLICATE KEY UPDATE</c>, or the key <c>MERGE</c>) binds the pre-update target row
    /// and the original source row in one expression.
    /// </summary>
    [Fact]
    public void MergeAssignments_KeyUpsert_SourceExpressionShouldExecuteOnEveryEngine()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = t.Total + (s.A * 1000) + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        recorder.Commands.Should().ContainSingle(command => command.Sql.Contains("t301a_target"));
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once (no per-row replay)");
        T301ALogCommand("key-upsert source expression", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(2010, "the update reads the target row and the source row");
    }

    /// <summary>
    /// R04/R06: the proposed-row role (<c>excluded</c>) is a real reference only on a native upsert. The two
    /// native forms read it (<c>excluded.&lt;col&gt;</c> / <c>VALUES(&lt;col&gt;)</c>); an engine that renders
    /// the key upsert as a general <c>MERGE</c> has no proposed row and must refuse with a named diagnostic
    /// before any statement is sent.
    /// </summary>
    [Fact]
    public void MergeAssignments_KeyUpsert_ExcludedRoleShouldReadTheNativeProposedRow()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var upsert = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s, excluded) => new { Total = excluded.A + excluded.B })
            .WhenNotMatchedInsert()
            .Merge();

        if (!T301ASupportsNativeUpsert)
        {
            var refusal = upsert.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("key-upsert excluded role", refusal.Message);
            refusal.Message.Should().Match("Provider '*does not support role 'excluded' in key-upsert assignments*");
            recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
            recorder.Executing.Should().Be(0, "no statement is executed for a used unsupported role");
            T301ARead(T301ATargetId).Total.Should().Be(1000);
            return;
        }

        upsert.Should().NotThrow();

        recorder.Commands.Should().ContainSingle(command => command.Sql.Contains("t301a_target"));
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        T301ALogCommand("key-upsert excluded role", recorder);
        if (T301ADataContext.Dialect.SupportsOnDuplicateKey)
            recorder.Commands[0].Sql.Should().ContainEquivalentOf("values(", "MySQL/MariaDB spell the proposed row as VALUES(<column>)");
        else
            recorder.Commands[0].Sql.Should().ContainEquivalentOf("excluded.", "a native ON CONFLICT upsert reads excluded.<column>");

        T301ARead(T301ATargetId).Total.Should().Be(11, "the proposed row carries the source values 1 and 10");
    }

    /// <summary>
    /// R04/R06 + §7 native-omitted-default proof: the proposed row is <b>not</b> the source row. The insert arm
    /// omits <c>total</c>, so the server fills its default (<c>500</c>); the update arm reads
    /// <c>excluded.Total + excluded.A</c> = <c>501</c>. Reading the source instead would yield <c>0 + 1 = 1</c>,
    /// and reading the pre-update target would yield <c>1000 + 1 = 1001</c>, so a source↔proposed substitution
    /// cannot pass. An engine without a native proposed row refuses the used role before executing anything.
    /// </summary>
    [Fact]
    public void MergeAssignments_KeyUpsert_ExcludedShouldReadTheProposedRowNotTheSource()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var upsert = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s, excluded) => new { Total = excluded.Total + excluded.A })
            .WhenNotMatchedInsert(s => new { Id = s.Id, A = s.A, B = s.B })
            .Merge();

        if (!T301ASupportsNativeUpsert)
        {
            var refusal = upsert.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("key-upsert proposed row default", refusal.Message);
            refusal.Message.Should().Match("Provider '*does not support role 'excluded' in key-upsert assignments*");
            recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
            recorder.Executing.Should().Be(0, "no statement is executed for a used unsupported role");
            T301ARead(T301ATargetId).Total.Should().Be(1000);
            return;
        }

        upsert.Should().NotThrow();
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        T301ALogCommand("key-upsert proposed row default", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(501, "the proposed row carries the server default 500 for the omitted total");
        T301ARead(T301ATargetId).Total.Should().NotBe(1, "the proposed row is not the original source row (0 + 1)");
        T301ARead(T301ATargetId).Total.Should().NotBe(1001, "the proposed row is not the pre-update target row");
    }

    /// <summary>
    /// R04/R06 + §7 PostgreSQL <c>BEFORE INSERT</c> trigger proof: the proposed row is the row the server
    /// would insert, so a trigger rewrite of an inserted column is part of <c>excluded</c>. The insert arm
    /// omits <c>total</c> (column default <c>500</c>) and supplies the marker; the trigger overwrites the
    /// proposed <c>total</c> with <c>777</c>; the update arm stores <c>excluded.Total</c>. The stored value
    /// is therefore <c>777</c> — not the column default <c>500</c>, not the source <c>Total</c> (<c>0</c>,
    /// which a source↔proposed substitution would store) and not the pre-update target (<c>1000</c>) — in
    /// one statement. PostgreSQL only: the fixture is PL/pgSQL DDL and the claim is provider-specific.
    /// </summary>
    [Fact]
    public void MergeAssignments_KeyUpsert_ExcludedShouldCarryTheBeforeInsertTriggerRewrite()
    {
        Assert.SkipUnless(
            Provider.Name == "postgres",
            "The BEFORE INSERT trigger proof is PostgreSQL-only (PL/pgSQL fixture over the ON CONFLICT proposed row).");

        T301BResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        ctx.CreateMergeBuilder<MergeTriggerTarget>()
            .Using(T301BSource())
            .OnKeys()
            .WhenMatchedUpdate((t, s, excluded) => new { Total = excluded.Total })
            .WhenNotMatchedInsert(s => new { Id = s.Id, A = s.A, B = s.B, Marker = s.Marker })
            .Merge();

        var command = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301b_trigger")).Which;
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once (no read-modify-write)");
        var executedStatements = recorder.Executing;
        T301ALogCommand("key-upsert before-insert trigger", recorder, "t301b_trigger");
        command.Sql.Should().ContainEquivalentOf("excluded.", "a native ON CONFLICT upsert reads excluded.<column>");

        var stored = T301BRead();
        T301ALog($"[T301:{T301ADataContext.Dialect.GetType().Name}] before-insert trigger | values | stored={stored.Total} source={T301BSource().Total} previousTarget=1000 columnDefault={T301BDefaultTotal} marker={stored.Marker} executedStatements={executedStatements}");
        stored.Total.Should().Be(T301BTriggerTotal, "excluded carries the BEFORE INSERT trigger's rewrite of the proposed row");
        stored.Total.Should().NotBe(T301BDefaultTotal, "the column default alone is not the trigger-written proposed value");
        stored.Total.Should().NotBe(0, "the excluded value is not the original source row's total");
        stored.Total.Should().NotBe(1000, "the excluded value is not the pre-update target row's total");
        stored.Marker.Should().Be(0, "the update arm writes only the assigned column; the marker shaped only the proposed row");
    }

    /// <summary>
    /// R03/R09: a constant right-hand side is a bound statement parameter, never an inlined caller value, and
    /// the statement executes on every engine.
    /// </summary>
    [Fact]
    public void MergeAssignments_ConstantAssignmentShouldBeABoundParameter()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = 4242 })
            .WhenNotMatchedInsert()
            .Merge();

        var command = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_target")).Which;
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        T301ALogCommand("constant assignment", recorder);
        command.Sql.Should().NotContain("4242", "an assignment constant is bound, never inlined");
        command.HasValue(4242).Should().BeTrue("the constant is in the bound parameter list");
        command.Parameters.Should().OnlyContain(p => command.Sql.Contains(p.Name), "every parameter is referenced by the statement");

        T301ARead(T301ATargetId).Total.Should().Be(4242);
    }

    /// <summary>
    /// R07: a batch/query source with a used original-source role has no single materialized row to bind on a
    /// native upsert, so the form fails closed with the frozen cardinality diagnostic and writes nothing. An
    /// engine whose key upsert is a real <c>MERGE</c> correlates the source relation per row instead, and the
    /// fact proves that live result rather than refusing.
    /// </summary>
    [Fact]
    public void MergeAssignments_BatchSourceWithSourceRoleShouldFailClosedOnNativeUpsert()
    {
        T301AResetFixture();
        T301AExecute(_sut.DataProvider, $"insert into t301a_target (id, a, b, total) values ({T301AMissingId}, 5, 4, 2000)");
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var upsert = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using([
                T301ASource(T301ATargetId, 1, 10),
                T301ASource(T301AMissingId, 2, 20),
            ])
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        if (T301ASupportsNativeUpsert)
        {
            var refusal = upsert.Should().Throw<NotSupportedException>().Which;
            T301ALogRefusal("key-upsert batch source", refusal.Message);
            refusal.Message.Should().Match("Provider '*requires one materialized source row for original-source references in key-upsert UPDATE assignments*");
            recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
            recorder.Executing.Should().Be(0, "no statement is executed for an unsupported source shape");
            T301ARead(T301ATargetId).Total.Should().Be(1000);
            T301ARead(T301AMissingId).Total.Should().Be(2000);
            return;
        }

        upsert.Should().NotThrow();
        recorder.Executing.Should().Be(1, "one MERGE statement correlates the batch source");

        T301ALogCommand("key-upsert batch source", recorder);

        T301ARead(T301ATargetId).Total.Should().Be(11);
        T301ARead(T301AMissingId).Total.Should().Be(22);
    }

    /// <summary>
    /// R03: a right-hand side with no single-row scalar form is refused by name before execution, and the
    /// stored row is untouched.
    /// </summary>
    [Fact]
    public void MergeAssignments_UnsupportedValueShapeShouldFailBeforeExecution()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        var merge = () => ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = new[] { s.A, s.B }.Length })
            .WhenNotMatchedInsert()
            .Merge();

        var refusal = merge.Should().Throw<NotSupportedException>().Which;
        T301ALogRefusal("unsupported value shape", refusal.Message);
        refusal.Message.Should().Be("MERGE assignment for 'Total' cannot translate expression 'NewArray'.");

        recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
        recorder.Executing.Should().Be(0, "no statement is executed for an untranslatable expression");
        T301ARead(T301ATargetId).Total.Should().Be(1000);
    }

    /// <summary>
    /// R09: the rendered statement and the bound parameter list are observable per execution — the constant
    /// and the expression forms both bind parameters, a repeated DML execution re-binds new values against the
    /// same statement text (mutations are not cached), and the library's read-path plan cache returns the
    /// same prepared command for the same query shape.
    /// </summary>
    [Fact]
    public void MergeAssignments_ParametersShouldBeBoundAndTheReadPlanReused()
    {
        T301AResetFixture();
        var ctx = _sut.DataProvider;
        var recorder = T301ARecord(ctx);

        // (a) constant assignment — one bound parameter, nothing inlined.
        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = 4242 })
            .WhenNotMatchedInsert()
            .Merge();

        var constant = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_target")).Which;
        T301ALogCommand("parameters/constant", recorder);

        // (b) parameterised expression — the source row is bound, the target row is a column reference.
        recorder.Commands.Clear();
        recorder.Reset();
        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        var expression = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_target")).Which;
        T301ALogCommand("parameters/expression", recorder);

        constant.Sql.Should().NotContain("4242");
        constant.HasValue(4242).Should().BeTrue();
        expression.HasValue(1).Should().BeTrue("the source column A is bound as a parameter");
        expression.HasValue(10).Should().BeTrue("the source column B is bound as a parameter");
        expression.Sql.Should().NotContain("4242");

        // A second execution of the same shape re-binds new values against the same statement text.
        recorder.Commands.Clear();
        recorder.Reset();
        ctx.CreateMergeBuilder<MergeAssignTarget>()
            .Using(T301ASource(T301ATargetId, 2, 20))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        var repeated = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_target")).Which;
        T301ALogCommand("parameters/expression-repeat", recorder);
        repeated.Sql.Should().Be(expression.Sql, "the statement shape is stable across executions");
        repeated.HasValue(2).Should().BeTrue("the repeat binds its own closure/source values");
        repeated.HasValue(20).Should().BeTrue();
        repeated.HasValue(1).Should().BeFalse("no value is retained from the previous execution");
        T301ARead(T301ATargetId).Total.Should().Be(22);

        // The read path is cached: a second prepare of the same shape is a cache hit that re-executes the
        // stored plan without a new command initialization.
        ctx.PurgeQueryCache();
        recorder.Reset();
        var query = ctx.From<MergeAssignTarget>().Where(x => x.Id == T301ATargetId).Select(x => x.Total);
        var first = T301ADataContext.GetPreparedQueryCommand(query, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var firstRows = ctx.ToList(first);
        var second = T301ADataContext.GetPreparedQueryCommand(query, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var secondRows = ctx.ToList(second);

        second.Should().BeSameAs(first, "the same query shape is served from the plan cache");
        firstRows.Should().Equal(22);
        secondRows.Should().Equal(22);
        recorder.Initialized.Should().Be(1, "a cache hit does not initialize another command");
        recorder.Executing.Should().Be(2, "the stored plan is executed twice");
        T301ALog($"[T301:{T301ADataContext.Dialect.GetType().Name}] plan-cache | executes | preparedIsSame={ReferenceEquals(first, second)} | initialized={recorder.Initialized} executed={recorder.Executing} rows=[{string.Join(",", firstRows)}]/[{string.Join(",", secondRows)}]");
    }
}

/// <summary>
/// Writes the compact per-engine T301 matrix to the test output and, when <c>T301_MATRIX_OUT</c> names
/// a file, appends the same lines there so the matrix is durable without depending on a reporter flag.
/// The variable being unset is the normal path: no file is touched and behaviour is unchanged.
/// </summary>
internal static class T301MatrixLog
{
    private static readonly object Gate = new();

    /// <summary>Emits one matrix line.</summary>
    public static void Write(string message)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(message);

        var path = Environment.GetEnvironmentVariable("T301_MATRIX_OUT");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        lock (Gate)
            File.AppendAllText(path, message + Environment.NewLine);
    }
}

/// <summary>The class-owned T301 live fixture: a target row with two source-valued columns and a stored total.</summary>
[SqlTable("t301a_target")]
public sealed class MergeAssignTarget
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("a")]
    public int A { get; set; }

    [Column("b")]
    public int B { get; set; }

    [Column("total")]
    public int Total { get; set; }
}

/// <summary>
/// The E13 PostgreSQL trigger fixture: <c>marker</c> makes the table's <c>BEFORE INSERT</c> trigger rewrite
/// <c>total</c> on the proposed row, so the proposed-row role can be told apart from the column default,
/// the original source row and the pre-update target row.
/// </summary>
[SqlTable("t301b_trigger")]
public sealed class MergeTriggerTarget
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("a")]
    public int A { get; set; }

    [Column("b")]
    public int B { get; set; }

    [Column("total")]
    public int Total { get; set; }

    [Column("marker")]
    public int Marker { get; set; }
}

/// <summary>One command observed on the context's execution axis: its text and its bound parameters.</summary>
internal sealed record MergeRecordedCommand(string Sql, IReadOnlyList<(string Name, object? Value)> Parameters)
{
    /// <summary>Whether any bound parameter carries <paramref name="value"/>.</summary>
    public bool HasValue(object value) => Parameters.Any(p => Equals(p.Value, value));
}

/// <summary>
/// Records every command a context executes so a fact can assert the rendered SQL, the bound parameter list
/// and how often a plan was initialized versus executed.
/// </summary>
internal sealed class MergeCommandRecorder : IQueryInterceptor
{
    /// <summary>The commands observed immediately before execution.</summary>
    public List<MergeRecordedCommand> Commands { get; } = [];

    /// <summary>How often a command was created (a cached query plan is not re-initialized).</summary>
    public int Initialized { get; private set; }

    /// <summary>How often a command was about to execute.</summary>
    public int Executing { get; private set; }

    /// <summary>How often a command completed.</summary>
    public int Executed { get; private set; }

    /// <summary>Clears the counters (keeps nothing retained across a measurement window).</summary>
    public void Reset()
    {
        Initialized = 0;
        Executing = 0;
        Executed = 0;
    }

    /// <inheritdoc/>
    public void CommandInitialized(CommandEventData eventData, DbCommand command) => Initialized++;

    /// <inheritdoc/>
    public void CommandExecuting(CommandEventData eventData, DbCommand command)
    {
        Executing++;
        var parameters = new List<(string Name, object? Value)>();
        foreach (DbParameter parameter in command.Parameters)
            parameters.Add((parameter.ParameterName, parameter.Value));

        Commands.Add(new MergeRecordedCommand(command.CommandText, parameters));
    }

    /// <inheritdoc/>
    public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) => Executed++;
}
