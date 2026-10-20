using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>Serializes the SQL Server-only D201-4 cases (they mutate the process-wide caches).</summary>
[CollectionDefinition("T201 cached preparation parity", DisableParallelization = true)]
public sealed class CachedPreparationParityCollectionDefinition;

/// <summary>
/// T201 (#201) D201-4 — SQL Server-only coverage for the D201-3 cached-path preparation recipe.
///
/// <para>
/// Two branch families the recipe guards must prove on the real provider (EC201-11):
/// the <b>temporary-table</b> source (<c>QueryCommand.HasTemporaryTableSource</c>) and the
/// <b>table-valued-parameter</b> route. Both must select the ordinary path <i>before any effect</i> and
/// the recipe must never enter TVP parameter binding. TVP metadata resolution (automatic vs configured,
/// configured precedence, <c>DataContextCache.Clear()</c>) is exercised against the real SQL Server
/// context.
/// </para>
/// <para>
/// Runs in a <see cref="CollectionDefinitionAttribute.DisableParallelization"/> collection because it
/// mutates and clears the process-wide caches (<see cref="DataContextCache"/>), exactly like the
/// existing <c>EfCoreQueryFilterLifecycleTests</c>.
/// </para>
/// </summary>
[Collection("T201 cached preparation parity")]
public sealed class SqlServerCachedPreparationSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    // -------------------------------------------------------------------------------------------
    // Temporary-table branch
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// A lazy temporary-table source is excluded by the guard before any effect: its descriptor cannot
    /// be built, so nothing is probed, applied or published, and the failure comes from the ordinary
    /// render (SQL Server has no <c>CREATE TEMPORARY TABLE ... AS SELECT</c>).
    /// </summary>
    [Fact]
    public void CachedPreparation_TempTableSourceGuardSelectsOrdinaryPathBeforeEffects()
    {
        var ctx = _sut.DataProvider;

        var source = ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }).AsTempTable();
        var read = ctx.From(source).Select(t => new { Id = t.GetInt32("id") });

        read.HasTemporaryTableSource().Should().BeTrue("the FROM carries the lazy TempTable marker");
        QueryCommand.CaptureRawShape(read).Should().BeNull(
            "a lazy temporary-table source has no provable raw identity, so the recipe can never engage");

        var holdersBefore = QueryPlanStore.Values.Count();
        Action act = () => ((DataContext)ctx).GetPreparedQueryCommand(read, false, true, CancellationToken.None);
        act.Should().Throw<NotSupportedException>(
            "the guard keeps the ordinary path, whose render rejects SQL Server temporary materialisation");
        QueryPlanStore.Values.Count().Should().Be(holdersBefore,
            "the guarded ordinary path publishes neither a normalized plan nor a raw candidate");
    }

    /// <summary>A real SQL Server session table (<c>#name</c>) reads through the ordinary cacheable path.</summary>
    [Fact]
    public void CachedPreparation_SessionTempTableReadsOnTheOrdinaryPath()
    {
        var ctx = _sut.DataProvider;
        var name = "#t201_cached_prep_" + Guid.NewGuid().ToString("N")[..8];

        Execute(ctx, $"create table {name} (id int not null)");
        Execute(ctx, $"insert into {name} (id) values (1), (2)");

        try
        {
            var first = CachedPreparationRender(ctx, ctx.From(name).Select(t => new { Id = t.GetInt32("id") }));

            // A second, equivalent fresh command reuses the ordinary cached plan.
            var second = CachedPreparationRender(ctx, ctx.From(name).Select(t => new { Id = t.GetInt32("id") }));
            second.Should().Be(first);

            ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList()
                .Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {name}");
        }
    }

    // -------------------------------------------------------------------------------------------
    // Table-valued-parameter branch
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// A table-valued parameter is bound through the raw command executor and never enters command
    /// preparation, so the recipe cannot reach TVP parameter binding: no plan (and no raw candidate) is
    /// created while the TVP executes.
    /// </summary>
    [Fact]
    public void CachedPreparation_TvpBindingNeverEntersTheRecipe()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpCachedPrep_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            var holdersBefore = QueryPlanStore.Values.Count();
            using (var result = ctx.ExecuteRaw(
                       "select sum(value) as s from @p",
                       [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3 })]))
            {
                // the table-valued parameter binds and executes
                result.Read<int>().Should().Equal(6);
            }

            QueryPlanStore.Values.Count().Should().Be(holdersBefore,
                "the TVP path never enters preparation, so the recipe can never enter TVP parameter binding");
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    private sealed class CachedPrepTvpRow
    {
        public string? Name { get; set; }
    }

    /// <summary>
    /// TVP metadata resolution against the real SQL Server context: the automatic path uses the CLR
    /// property name and never seeds the configured cache, a configured registration wins (precedence),
    /// and <see cref="DataContextCache.Clear"/> leaves no auto metadata that shadows a later configuration.
    /// </summary>
    [Fact]
    public void CachedPreparation_TvpMetadataPrecedenceAndClear()
    {
        var ctx = _sut.DataProvider;
        var rowType = typeof(CachedPrepTvpRow);

        DataContextCache.Clear();

        // 1. Automatic resolution uses the CLR property name and does not seed the configured cache.
        var autoCarrier = CachedPreparationTvpCarrier(new[] { new CachedPrepTvpRow { Name = "a" } });
        autoCarrier.GetColumns((DataContext)ctx).Select(c => c.Name).Should().Equal("Name");
        DataContextCache.Metadata.ContainsKey(rowType).Should().BeFalse(
            "auto-resolving a TVP row type must not register it as a configured mapping");

        // 2. A later configured registration is honored (configured precedence).
        ctx.From<CachedPrepTvpRow>(cfg => cfg.Property(x => x.Name!).HasColumnName("ConfiguredName"));
        autoCarrier.GetColumns((DataContext)ctx).Select(c => c.Name).Should().Equal("ConfiguredName");

        // 3. Clear drops the auto TVP metadata with every other cache, so a fresh configuration wins.
        DataContextCache.Clear();
        ctx.From<CachedPrepTvpRow>(cfg => cfg.Property(x => x.Name!).HasColumnName("AfterClearName"));
        CachedPreparationTvpCarrier(new[] { new CachedPrepTvpRow { Name = "a" } })
            .GetColumns((DataContext)ctx).Select(c => c.Name).Should().Equal("AfterClearName");
    }

    // -------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------

    private static TableParameterValue CachedPreparationTvpCarrier(IEnumerable<CachedPrepTvpRow> rows)
    {
        var parameter = ProcedureParameter.Table("p", rows);
        return (TableParameterValue)parameter.Value!;
    }

    /// <summary>Renders a prepared command's SQL without executing it (no connection is opened by preparation).</summary>
    private static string CachedPreparationRender<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => ((DbPreparedQueryCommand<TResult>)((DataContext)ctx)
                .GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
