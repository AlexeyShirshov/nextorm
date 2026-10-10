using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Benchmark;

// ---- Entities used by the provider-extensions baseline fixtures ----

[SqlTable("complex_entity")]
public interface IChComplexEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("nullableint")]
    int? Int { get; set; }
}

[SqlTable("array_entity")]
public interface IChArrayEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("tags")]
    string[] Tags { get; set; }
}

[SqlTable("simple_entity")]
public interface IPgEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

[SqlTable("sales")]
public interface ISsSalesEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("category")]
    string? Category { get; set; }

    [Column("quarter")]
    int Quarter { get; set; }

    [Column("margin")]
    decimal? Margin { get; set; }
}

[SqlTable("quarterly")]
public interface ISsQuarterlyEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("category")]
    string? Category { get; set; }

    [Column("q1")]
    decimal? Q1 { get; set; }

    [Column("q2")]
    decimal? Q2 { get; set; }
}

/// <summary>
/// D191 baseline fixtures for the relocation-affected phrase paths. They call the existing
/// provider fluent API (before relocation) with placeholder connection strings and never open a
/// connection; only query construction / clone / join composition is measured. After relocation the
/// same calls must resolve to the provider extension-host classes without a measured regression.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class ProviderExtensionsBenchmark
{
    private const string ClickHouseConnection =
        "Host=localhost;Port=8123;Username=default;Password=nextorm;Database=nextorm";
    private const string PostgresConnection =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";
    private const string SqlServerConnection =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private readonly IDataContext _ch;
    private readonly IDataContext _pg;
    private readonly IDataContext _ss;

    public ProviderExtensionsBenchmark()
    {
        _ch = new ClickHouseDataContext(ClickHouseConnection, new DataContextBuilder());
        _pg = new PostgresDataContext(PostgresConnection, new DataContextBuilder());
        _ss = new SqlServerDataContext(SqlServerConnection, new DataContextBuilder());
    }

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public EntityBuilder<IChComplexEntity> ClickHouse_Construct_Final_PreWhere_Settings()
        => _ch.From<IChComplexEntity>()
            .Final()
            .PreWhere(x => x.Id > 1L)
            .Settings(("max_threads", "2"));

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public EntityBuilder<IChArrayEntity> ClickHouse_Construct_ArrayJoin()
        => _ch.From<IChArrayEntity>().ArrayJoin(x => x.Tags);

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public JoinedEntityBuilder<IChArrayEntity, IChComplexEntity> ClickHouse_Joined_ArrayJoin()
        => _ch.From<IChArrayEntity>()
            .Join(_ch.From<IChComplexEntity>(), (a, b) => a.Id == b.Id)
            .ArrayJoin(p => p.Item1.Tags);

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public JoinedEntityBuilder<IChArrayEntity, IChComplexEntity> ClickHouse_Construct_PasteJoin()
        => _ch.From<IChArrayEntity>().PasteJoin(_ch.From<IChComplexEntity>());

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public EntityBuilder<IPgEntity> Postgres_Construct_DistinctOn()
        => _pg.From<IPgEntity>().DistinctOn(x => x.Id);

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public EntityBuilder<TableAlias> SqlServer_Construct_Pivot()
        => _ss.From<ISsSalesEntity>().Pivot(
            PivotAggregate.Sum,
            s => s.Margin,
            s => s.Quarter,
            PivotValue.Create("1"),
            PivotValue.Create("2"));

    [Benchmark]
    [BenchmarkCategory("provider-extensions")]
    public EntityBuilder<TableAlias> SqlServer_Construct_Unpivot()
        => _ss.From<ISsQuarterlyEntity>().Unpivot(
            "val",
            "qtr",
            UnpivotColumn.Create("q1"),
            UnpivotColumn.Create("q2"));
}
