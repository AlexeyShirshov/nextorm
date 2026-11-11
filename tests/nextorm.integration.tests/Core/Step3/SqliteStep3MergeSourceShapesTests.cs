using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 3, batch 2 — the remaining source and assignment shapes of the portable key-upsert
/// <c>MergeBuilder&lt;TEntity&gt;</c> on live SQLite (<c>src/nextorm.core/Builders/Merge/MergeBuilder.cs</c>,
/// <c>MergeBuilder.Columns.cs</c>): a batch <c>Using(IEnumerable&lt;T&gt;)</c> source, a server-side
/// query source (refused by the dialect capability before any SQL), a named-initializer assignment body,
/// the destination/value-type/key guards, the VALUES-source column guard and the row-returning terminal.
/// Every accepted fact asserts the persisted state; every refusal is a pre-SQL guard asserted by
/// exception type plus the semantic identifier the guard carries.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep3MergeSourceShapesTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s3_ms_row (id integer primary key, name text null, age int not null);
        delete from s3_ms_row;
        create table if not exists s3_ms_identity (id integer primary key autoincrement, name text null);
        delete from s3_ms_identity;
        """;

    private static int _seed = -910_000_000;

    private static int NextKey() => Interlocked.Add(ref _seed, -1);

    public SqliteStep3MergeSourceShapesTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    private (string? Name, int Age) Read(int id)
    {
        var row = Context.From<S3MsRow>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Age })
            .Single();
        return (row.Name, row.Age);
    }

    /// <summary>A multi-row <c>Using(IEnumerable&lt;T&gt;)</c> source inserts every batch row.</summary>
    [Fact]
    public void BatchSource_ShouldInsertEveryRow()
    {
        var first = NextKey();
        var second = first - 1;

        var affected = Context.CreateMergeBuilder<S3MsRow>()
            .Using([
                new S3MsRow { Id = first, Name = "batch-a", Age = 1 },
                new S3MsRow { Id = second, Name = "batch-b", Age = 2 },
            ])
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge();

        affected.Should().Be(2);
        Read(first).Should().Be(("batch-a", 1));
        Read(second).Should().Be(("batch-b", 2));
    }

    /// <summary>A server-side entity query is only valid for the full MERGE form; SQLite refuses it.</summary>
    [Fact]
    public void EntityQuerySource_ShouldBeRefusedByTheDialectCapability()
    {
        var act = () => Context.CreateMergeBuilder<S3MsRow>()
            .Using(Context.From<S3MsRow>().Where(x => x.Age > 0))
            .On((target, source) => target.Id == source.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*SqliteDialect*does not support the full MERGE statement*");
    }

    /// <summary>The command form of the same query source is refused identically.</summary>
    [Fact]
    public void QueryCommandSource_ShouldBeRefusedByTheDialectCapability()
    {
        var source = Context.From<S3MsRow>().Where(x => x.Age > 0).ToCommand();

        var act = () => Context.CreateMergeBuilder<S3MsRow>()
            .Using(source)
            .On((target, row) => target.Id == row.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*SqliteDialect*does not support the full MERGE statement*");
    }

    /// <summary>A named-initializer assignment body is parsed by member name and applied.</summary>
    [Fact]
    public void MemberInitAssignments_ShouldApplyTheComputedUpdate()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MsRow>()
            .Values(new S3MsRow { Id = id, Name = "base", Age = 10 })
            .Insert();

        Context.CreateMergeBuilder<S3MsRow>()
            .Using(new S3MsRow { Id = id, Name = "source", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new S3MsRow { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .Merge();

        Read(id).Should().Be(("base", 15));
    }

    /// <summary>An assignment destination that is not a mapped property is rejected by name.</summary>
    [Fact]
    public void UnknownAssignmentDestination_ShouldBeRefusedByName()
    {
        var act = () => Context.CreateMergeBuilder<S3MsRow>()
            .Using(new S3MsRow { Id = NextKey(), Name = "a", Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate((_, _) => new { Missing = 1 })
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*destination 'Missing' is not a mapped CLR property*");
    }

    /// <summary>A value whose type cannot be stored in the destination is rejected by name and type.</summary>
    [Fact]
    public void IncompatibleAssignmentValueType_ShouldBeRefusedByName()
    {
        var act = () => Context.CreateMergeBuilder<S3MsRow>()
            .Using(new S3MsRow { Id = NextKey(), Name = "a", Age = 1 })
            .OnKeys()
            .WhenMatchedUpdate((_, _) => new { Age = "not-an-int" })
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*assignment for 'Age' has incompatible value type*");
    }

    /// <summary>The merge key cannot be assigned by the matched-update action.</summary>
    [Fact]
    public void KeyAssignmentInUpdate_ShouldBeRefusedByName()
    {
        var act = () => Context.CreateMergeBuilder<S3MsRow>()
            .Using(new S3MsRow { Id = NextKey(), Name = "a", Age = 2 })
            .OnKeys()
            .WhenMatchedUpdate((_, source) => new { Id = source.Id, Age = source.Age })
            .WhenNotMatchedInsert()
            .Merge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*MERGE UPDATE cannot assign merge key property 'Id'*");
    }

    /// <summary>A VALUES source excludes the identity column, so a source reference to it is refused.</summary>
    [Fact]
    public void IdentitySourceCondition_ShouldBeRefusedAsNotASourceColumn()
    {
        var act = () => Context.CreateMergeBuilder<S3MsIdentity>()
            .Using(new S3MsIdentity { Id = 5, Name = "x" })
            .On((target, source) => target.Id == source.Id)
            .WhenMatched().ThenUpdate()
            .WhenNotMatched().ThenInsert()
            .ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Property Id*is not a column of the VALUES-derived merge source*");
    }

    /// <summary>The key-upsert terminal can return the merged row through SQLite RETURNING.</summary>
    [Fact]
    public void ReturningTerminal_ShouldReturnTheUpsertedRow()
    {
        var id = NextKey();
        Context.CreateInsertBuilder<S3MsRow>()
            .Values(new S3MsRow { Id = id, Name = "base", Age = 4 })
            .Insert();

        var rows = Context.CreateMergeBuilder<S3MsRow>()
            .Using(new S3MsRow { Id = id, Name = "source", Age = 6 })
            .OnKeys()
            .WhenMatchedUpdate((target, source) => new { Age = target.Age + source.Age })
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id, x.Age })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Age.Should().Be(10);
    }

    /// <summary>Marker table of the upsert facts.</summary>
    [SqlTable("s3_ms_row")]
    public sealed class S3MsRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("age")]
        public int Age { get; set; }
    }

    /// <summary>Marker entity whose key is database-generated.</summary>
    [SqlTable("s3_ms_identity")]
    public sealed class S3MsIdentity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }
}
