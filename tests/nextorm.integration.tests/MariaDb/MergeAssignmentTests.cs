using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T301 (#301) live assignment semantics on MariaDB. MariaDB is not part of the shared provider matrix: this
/// class owns its <see cref="MariaDbContainer"/> connection, its <c>t301a_mariadb</c> table (portable DDL on
/// <c>engine=InnoDB</c>) and its <see cref="MariaDbDialect"/>, so nothing here can be served by the MySQL
/// path. The row values repeat the shared fixture (<c>a = 7, b = 3, total = 1000</c> in the target,
/// <c>a = 1, b = 10</c> in the source) so the three roles stay numerically distinguishable.
/// </summary>
public sealed class MergeAssignmentTests : IDisposable
{
    private const int TargetId = 7401;
    private const int MissingId = 7402;

    private readonly MariaDbDataContext _ctx;

    public MergeAssignmentTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");
        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Execute("drop table if exists t301a_mariadb");
        Execute("create table t301a_mariadb (id int not null primary key, a int not null, b int not null, total int not null default 500) engine=InnoDB");
        Execute($"insert into t301a_mariadb (id, a, b, total) values ({TargetId}, 7, 3, 1000)");
    }

    public void Dispose() => _ctx.Dispose();

    private int Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var command = ((DataContext)_ctx).CreateCommand(sql);
        return command.ExecuteNonQuery();
    }

    private static MergeAssignmentRow Source(int id, int a, int b) => new() { Id = id, A = a, B = b, Total = 0 };

    private MergeAssignmentRow Read(int id)
        => _ctx.From<MergeAssignmentRow>().Where(x => x.Id == id).Single();

    private MergeCommandRecorder Record()
    {
        var recorder = new MergeCommandRecorder();
        ((DataContext)_ctx).AddInterceptor(recorder);
        return recorder;
    }

    private static void Log(string message) => T301MatrixLog.Write(message);

    /// <summary>Records the observed statement (text plus bound parameters) of one form on MariaDB.</summary>
    private void LogCommand(string label, MergeCommandRecorder recorder)
    {
        var command = recorder.Commands.LastOrDefault(c => c.Sql.Contains("t301a_mariadb"));
        var parameters = command is null
            ? "-"
            : string.Join(", ", command.Parameters.Select(p => $"{p.Name}={p.Value ?? "null"}"));
        Log($"[T301:MariaDbDialect] {label} | executes | {command?.Sql ?? "<none>"} | {parameters}");
    }

    /// <summary>Records the observed refusal diagnostic of one form on MariaDB.</summary>
    private static void LogRefusal(string label, string message)
        => Log($"[T301:MariaDbDialect] {label} | refuses | - | {message}");

    /// <summary>
    /// The fixture executes on the MariaDB container with the MariaDB dialect — its own engine, never the
    /// shared MySQL suite — and the rendered upsert uses MariaDB's own grammar.
    /// </summary>
    [Fact]
    public void MergeAssignments_MariaDb_RunsOnItsOwnEngine()
    {
        ((DataContext)_ctx).Dialect.Should().BeOfType<MariaDbDialect>();
        MariaDbContainer.ConnectionString.Should().NotBe(MySqlContainer.ConnectionString,
            "the MariaDB fixture never connects to the MySQL container");

        ((DataContext)_ctx).EnsureConnectionOpen();
        using var version = ((DataContext)_ctx).CreateCommand("select version()");
        var server = (string?)version.ExecuteScalar();

        server.Should().NotBeNull();
        server.Should().ContainEquivalentOf("mariadb");
        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A })
            .WhenNotMatchedInsert()
            .ToSql()
            .Should().ContainEquivalentOf("on duplicate key update");
        Log($"[T301:MariaDbDialect] engine | info | server={server} | dialect={((DataContext)_ctx).Dialect.GetType().Name}");
    }

    /// <summary>R04: the right-hand side <c>s.A + s.B</c> reads the source row; the destination is the target row.</summary>
    [Fact]
    public void MergeAssignments_MariaDb_SourceExpressionShouldBindTheSourceRow()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_mariadb"));
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("key-upsert source expression", recorder);

        Read(TargetId).Total.Should().Be(11, "the right-hand side binds the source row (1 + 10)");
        Read(TargetId).Total.Should().NotBe(110);
        Read(TargetId).Total.Should().NotBe(1000, "the target's prior value is not the written value");
        Read(TargetId).Total.Should().NotBe(1);
        Read(TargetId).Total.Should().NotBe(10);
    }

    /// <summary>
    /// R04/R09 binding proof: <c>s.A + s.B</c> is commutative, so a column-order swap inside the source row is
    /// invisible to it; <c>s.A - s.B</c> is not. <c>-9</c> differs from the swapped binding <c>s.B - s.A</c>
    /// (<c>9</c>), from the role swap <c>t.A - t.B</c> (<c>4</c>) and from the target's previous <c>Total</c>.
    /// </summary>
    [Fact]
    public void MergeAssignments_MariaDb_SourceColumnOrderShouldBindEachColumnByName()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A - s.B })
            .WhenNotMatchedInsert()
            .Merge();

        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("key-upsert source column order", recorder);

        Read(TargetId).Total.Should().Be(-9, "the right-hand side binds source A (1) then source B (10)");
        Read(TargetId).Total.Should().NotBe(9, "the source columns were not bound in swapped order");
        Read(TargetId).Total.Should().NotBe(4, "the target row's own columns are not the source row");
        Read(TargetId).Total.Should().NotBe(1000, "the target's prior value is not the written value");
    }

    /// <summary>R04: one expression reading both rows — <c>t.Total + (s.A * 1000) + s.B</c> is <c>2010</c>.</summary>
    [Fact]
    public void MergeAssignments_MariaDb_TargetAndSourceInOneExpressionShouldBindEachRoleSeparately()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = t.Total + (s.A * 1000) + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("key-upsert target+source expression", recorder);

        Read(TargetId).Total.Should().Be(2010);
        Read(TargetId).Total.Should().NotBe(1010, "the target read did not bind the source row");
        Read(TargetId).Total.Should().NotBe(8003, "the source reads did not bind the target row");
    }

    /// <summary>R04/R06: the proposed row is MariaDB's <c>VALUES(&lt;column&gt;)</c> reference and carries the source values.</summary>
    [Fact]
    public void MergeAssignments_MariaDb_ExcludedRoleShouldReadTheProposedRow()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s, excluded) => new { Total = excluded.A + excluded.B })
            .WhenNotMatchedInsert()
            .Merge();

        var command = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_mariadb")).Which;
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("key-upsert excluded role", recorder);
        command.Sql.Should().ContainEquivalentOf("values(a)", "MariaDB spells the proposed row as VALUES(<column>)");
        command.Sql.Should().NotContainEquivalentOf("excluded.");

        Read(TargetId).Total.Should().Be(11);
    }

    /// <summary>
    /// R04/R06 + §7 native-omitted-default proof on MariaDB: the proposed row is not the source row. The insert
    /// arm omits <c>total</c>, so MariaDB fills its default (<c>500</c>) and <c>VALUES(total) + VALUES(a)</c> is
    /// <c>501</c>; the source row is <c>0 + 1 = 1</c> and the pre-update target is <c>1000 + 1 = 1001</c>, so a
    /// source↔proposed substitution cannot pass.
    /// </summary>
    [Fact]
    public void MergeAssignments_MariaDb_ExcludedShouldReadTheProposedRowNotTheSource()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s, excluded) => new { Total = excluded.Total + excluded.A })
            .WhenNotMatchedInsert(s => new { Id = s.Id, A = s.A, B = s.B })
            .Merge();

        var command = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_mariadb")).Which;
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("key-upsert proposed row default", recorder);
        command.Sql.Should().ContainEquivalentOf("values(total)", "MariaDB reads the proposed row, not the source");

        Read(TargetId).Total.Should().Be(501, "the proposed row carries the server default 500 for the omitted total");
        Read(TargetId).Total.Should().NotBe(1, "the proposed row is not the original source row (0 + 1)");
        Read(TargetId).Total.Should().NotBe(1001, "the proposed row is not the pre-update target row");
    }

    /// <summary>R03/R09: a constant right-hand side is a bound parameter, never an inlined caller value.</summary>
    [Fact]
    public void MergeAssignments_MariaDb_ConstantAssignmentShouldBeABoundParameter()
    {
        var recorder = Record();

        _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = 4242 })
            .WhenNotMatchedInsert()
            .Merge();

        var command = recorder.Commands.Should().ContainSingle(c => c.Sql.Contains("t301a_mariadb")).Which;
        recorder.Executing.Should().Be(1, "the expression statement executes exactly once");
        LogCommand("constant assignment", recorder);
        command.Sql.Should().NotContain("4242");
        command.HasValue(4242).Should().BeTrue();

        Read(TargetId).Total.Should().Be(4242);
    }

    /// <summary>
    /// R07: a batch source with a used original-source role has no single materialized row on MariaDB's native
    /// upsert, so the form fails closed with the frozen diagnostic and writes nothing.
    /// </summary>
    [Fact]
    public void MergeAssignments_MariaDb_BatchSourceWithSourceRoleShouldFailClosed()
    {
        Execute($"insert into t301a_mariadb (id, a, b, total) values ({MissingId}, 5, 4, 2000)");
        var recorder = Record();

        var upsert = () => _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using([Source(TargetId, 1, 10), Source(MissingId, 2, 20)])
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = s.A + s.B })
            .WhenNotMatchedInsert()
            .Merge();

        var refusal = upsert.Should().Throw<NotSupportedException>().Which;
        LogRefusal("batch source", refusal.Message);
        refusal.Message.Should().Be("Provider 'MariaDbDialect' requires one materialized source row for original-source references in key-upsert UPDATE assignments.");

        recorder.Commands.Should().BeEmpty("the refusal happens before any statement is sent");
        recorder.Executing.Should().Be(0, "no statement is executed for an unsupported source shape");
        Read(TargetId).Total.Should().Be(1000);
        Read(MissingId).Total.Should().Be(2000);
    }

    /// <summary>R03: a right-hand side with no single-row scalar form is refused by name before execution.</summary>
    [Fact]
    public void MergeAssignments_MariaDb_UnsupportedValueShapeShouldFailBeforeExecution()
    {
        var recorder = Record();

        var upsert = () => _ctx.CreateMergeBuilder<MergeAssignmentRow>()
            .Using(Source(TargetId, 1, 10))
            .OnKeys()
            .WhenMatchedUpdate((t, s) => new { Total = new[] { s.A, s.B }.Length })
            .WhenNotMatchedInsert()
            .Merge();

        var refusal = upsert.Should().Throw<NotSupportedException>().Which;
        LogRefusal("unsupported value shape", refusal.Message);
        refusal.Message.Should().Be("MERGE assignment for 'Total' cannot translate expression 'NewArray'.");

        recorder.Commands.Should().BeEmpty();
        recorder.Executing.Should().Be(0, "no statement is executed for an untranslatable expression");
        Read(TargetId).Total.Should().Be(1000);
    }
}

/// <summary>The class-owned MariaDB fixture row.</summary>
[SqlTable("t301a_mariadb")]
public sealed class MergeAssignmentRow
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
