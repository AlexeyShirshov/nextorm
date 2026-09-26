using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Regression tests for the table-valued parameter (TVP) metadata cache. Auto-resolving a row type's
/// mapping must <b>not</b> seed the process-wide configured-metadata cache: otherwise a later fluent
/// registration through <c>From&lt;T&gt;(cfg)</c> is silently ignored, because the configured lookup
/// sees the auto-built entry and reuses it. The TVP auto path keeps its own cache inside
/// <see cref="DataContextCache"/>, always defers to a configured mapping, and is cleared by
/// <see cref="DataContextCache.Clear"/>.
/// <para>
/// Runs in the existing "Query cache controls" collection (serialized, parallelization disabled)
/// because it mutates and clears the process-wide <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class TvpMetadataCacheTests
{
    /// <summary>
    /// A row type unique to this class, so no other test can populate the process-wide caches for it.
    /// A single mapped property keeps the fluent registration shallow: <c>From&lt;T&gt;(cfg)</c> with a
    /// declared property maps exactly that property, so the expected column set is unambiguous.
    /// </summary>
    private sealed class TvpCacheRow
    {
        public string? Name { get; set; }
    }

    /// <summary>A row type whose auto mapping reads the <see cref="ColumnAttribute"/> name.</summary>
    [SqlTable("tvp_cache_attributed")]
    private sealed class AttributedTvpCacheRow
    {
        [Column("attributed_name")]
        public string? Name { get; set; }
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    private static TableParameterValue Carrier<T>(IEnumerable<T> rows)
    {
        var parameter = ProcedureParameter.Table("p", rows);
        return (TableParameterValue)parameter.Value!;
    }

    [Fact]
    public void Auto_Resolution_First_Then_Fluent_Registration_Should_Be_Honored_On_Next_Bind()
    {
        DataContextCache.Clear();
        using var ctx = new TestContext();
        var carrier = Carrier(new[] { new TvpCacheRow { Name = "a" } });

        // First bind auto-resolves the row type and uses the CLR property name.
        carrier.GetColumns(ctx).Select(c => c.Name).Should().Equal("Name");

        // The auto resolution must not seed the process-wide configured cache...
        DataContextCache.Metadata.ContainsKey(typeof(TvpCacheRow)).Should().BeFalse(
            "auto-resolving a TVP row type must not register it as a configured mapping");

        // ...so a later fluent registration is honored by the next bind.
        ctx.From<TvpCacheRow>(cfg => cfg.Property(x => x.Name!).HasColumnName("FullName"));

        carrier.GetColumns(ctx).Select(c => c.Name).Should().Equal("FullName");
    }

    [Fact]
    public void Fluent_Registration_First_Should_Be_Honored_By_Auto_Resolution()
    {
        DataContextCache.Clear();
        using var ctx = new TestContext();

        ctx.From<TvpCacheRow>(cfg => cfg.Property(x => x.Name!).HasColumnName("FullName"));

        var columns = Carrier(new[] { new TvpCacheRow { Name = "a" } }).GetColumns(ctx);

        columns.Select(c => c.Name).Should().Equal("FullName");
    }

    [Fact]
    public void Clear_Should_Leave_No_Auto_Metadata_That_Shadows_A_Later_Fluent_Registration()
    {
        DataContextCache.Clear();
        using var ctx = new TestContext();

        // Populate the auto cache first.
        _ = Carrier(new[] { new TvpCacheRow { Name = "a" } }).GetColumns(ctx);

        DataContextCache.Clear();

        ctx.From<TvpCacheRow>(cfg => cfg.Property(x => x.Name!).HasColumnName("FullName"));

        Carrier(new[] { new TvpCacheRow { Name = "a" } }).GetColumns(ctx)
            .Select(c => c.Name).Should().Equal("FullName");
    }

    [Fact]
    public void Auto_Resolution_Should_Honor_ColumnAttribute()
    {
        DataContextCache.Clear();
        using var ctx = new TestContext();

        var columns = Carrier(new[] { new AttributedTvpCacheRow { Name = "a" } }).GetColumns(ctx);

        columns.Select(c => c.Name).Should().Equal("attributed_name");
    }
}
