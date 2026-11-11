using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;
using NextORM.EntityFrameworkCore;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4 — EF Core query-filter bridge refusal/acceptance surface on live SQLite
/// (<c>src/nextorm.entityframeworkcore/EfQueryFilterTranslator.cs</c>, in particular
/// <c>OwnerRewriter</c>): every refusal is a pre-SQL guard asserted by exception type plus the
/// semantic marker the guard carries; the accepted shapes drive a live query (DB-result).
/// </summary>
[Collection("EF query filter lifecycle")]
public sealed class SqliteStep4EfQueryFilterTests
{
    /// <summary>A plain anonymous filter is imported and drives a live query.</summary>
    [Fact]
    public void AnonymousFilter_ShouldBeImportedAndDriveTheQuery()
    {
        var path = NewPath();
        try
        {
            CreateDatabase(path, "create table ef4_a (id integer primary key, value int not null);",
                "insert into ef4_a (id, value) values (1, 5), (2, -1);");

            using var db = new Ef4ContextA(Options<Ef4ContextA>(path));
            NextOrmModelMapper.Register(db.Model);

            var filters = DataContextCache.Metadata[typeof(Ef4RowA)].Filters;
            filters.Should().ContainSingle();

            using var next = NextContext(path);
            var ids = next.From<Ef4RowA>().OrderBy(x => x.Id).Select(x => x.Id).ToList();
            ids.Should().Equal(new[] { 1 }, "the imported anonymous filter keeps only non-negative values");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>A filter referencing a navigation member is refused as a navigation member.</summary>
    [Fact]
    public void NavigationMemberFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextB(Options<Ef4ContextB>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*navigation member*");
    }

    /// <summary>A filter using EF.Property is refused.</summary>
    [Fact]
    public void EfPropertyFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextC(Options<Ef4ContextC>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*EF.Property*");
    }

    /// <summary>A filter using EF.Functions is refused.</summary>
    [Fact]
    public void EfFunctionsFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextD(Options<Ef4ContextD>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*EF.Functions*");
    }

    /// <summary>A filter capturing a local closure value is refused.</summary>
    [Fact]
    public void ClosureCaptureFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextE(Options<Ef4ContextE>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*closure/local capture*");
    }

    /// <summary>A filter invoking a method is refused as an invocation expression.</summary>
    [Fact]
    public void InvocationFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextF(Options<Ef4ContextF>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*invocation expression*");
    }

    /// <summary>A static member capture from a user type is refused.</summary>
    [Fact]
    public void StaticCaptureFilter_ShouldBeRefused()
    {
        using var db = new Ef4ContextH(Options<Ef4ContextH>());
        var act = () => NextOrmModelMapper.Register(db.Model);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*static-member capture*");
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"nextorm.s4ef.{Guid.NewGuid():N}.db");

    private static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static DbContextOptions<TContext> Options<TContext>(string? path = null)
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseSqlite($"Data Source='{path ?? NewPath()}';Pooling=False")
            .Options;

    private static SqliteDataContext NextContext(string path)
        => new($"Data Source='{path}';Pooling=False", new DataContextBuilder());

    private static void CreateDatabase(string path, params string[] statements)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source='{path}';Pooling=False");
        connection.Open();
        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    // --- row types ---
    public sealed class Ef4RowA { public int Id { get; set; } public int Value { get; set; } }
    public sealed class Ef4RowB { public int Id { get; set; } public Ef4ChildB? Child { get; set; } }
    public sealed class Ef4ChildB { public int Id { get; set; } }
    public sealed class Ef4RowC { public int Id { get; set; } public int Value { get; set; } }
    public sealed class Ef4RowD { public int Id { get; set; } public int Value { get; set; } }
    public sealed class Ef4RowE { public int Id { get; set; } public int Value { get; set; } }
    public sealed class Ef4RowF { public int Id { get; set; } public int Value { get; set; } }
    public sealed class Ef4RowH { public int Id { get; set; } public int Value { get; set; } }


    // --- contexts ---
    private sealed class Ef4ContextA(DbContextOptions<Ef4ContextA> o) : DbContext(o)
    {
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowA>(e => { e.ToTable("ef4_a"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => x.Value >= 0); });
    }
    private sealed class Ef4ContextB(DbContextOptions<Ef4ContextB> o) : DbContext(o)
    {
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowB>(e => { e.ToTable("ef4_b"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.HasQueryFilter(x => x.Child != null); });
    }
    private sealed class Ef4ContextC(DbContextOptions<Ef4ContextC> o) : DbContext(o)
    {
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowC>(e => { e.ToTable("ef4_c"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => EF.Property<int>(x, "Value") >= 0); });
    }
    private sealed class Ef4ContextD(DbContextOptions<Ef4ContextD> o) : DbContext(o)
    {
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowD>(e => { e.ToTable("ef4_d"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => EF.Functions.Like(x.Value.ToString(), "%0%")); });
    }
    private sealed class Ef4ContextE(DbContextOptions<Ef4ContextE> o) : DbContext(o)
    {
        public int Threshold { get; } = 5;
        protected override void OnModelCreating(ModelBuilder m)
        {
            var local = 5;
            m.Entity<Ef4RowE>(e => { e.ToTable("ef4_e"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => x.Value >= local); });
        }
    }
    private sealed class Ef4ContextF(DbContextOptions<Ef4ContextF> o) : DbContext(o)
    {
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowF>(e => { e.ToTable("ef4_f"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => ((Func<int, bool>)(v => v >= 0))(x.Value)); });
    }

    private sealed class Ef4ContextH(DbContextOptions<Ef4ContextH> o) : DbContext(o)
    {
        public static int StaticThreshold = 5;
        protected override void OnModelCreating(ModelBuilder m) =>
            m.Entity<Ef4RowH>(e => { e.ToTable("ef4_h"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); e.Property(x => x.Value).HasColumnName("value"); e.HasQueryFilter(x => x.Value >= StaticThreshold); });
    }
}