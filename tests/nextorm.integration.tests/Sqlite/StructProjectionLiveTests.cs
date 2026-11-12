using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class StructProjectionLiveTests : IDisposable
{
    private const string Table = "t26_struct_projection";
    private const string NumericTable = "t26_numeric_projection";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.t26.{Guid.NewGuid():N}.db");
    private readonly SqliteDataContext _ctx;

    public StructProjectionLiveTests()
    {
        _ctx = new SqliteDataContext($"Data Source='{_path}';Pooling=False", new DataContextBuilder());
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand($"create table {Table} (id integer primary key, value text not null); insert into {Table} values (1, 'one'), (2, 'two');");
        command.ExecuteNonQuery();
        using var numericCommand = _ctx.CreateCommand($"create table {NumericTable} (id integer primary key);");
        numericCommand.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _ctx.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [SqlTable(Table)]
    public sealed class Row
    {
        [Key, Column("id")] public int Id { get; set; }
        [Column("value")] public string Value { get; set; } = string.Empty;
    }

    public struct MutableProjection
    {
        public int Id { get; set; }
        public string Value { get; set; }
    }

    public readonly record struct ConstructorProjection(int Id, string Value);

    [SqlTable(NumericTable)]
    public sealed class NumericRow
    {
        [Key, Column("id")] public int Id { get; set; }
    }

    public sealed class NumericClassProjection
    {
        public NumericClassProjection(int id) => Id = id;
        public int Id { get; }
    }

    public readonly record struct NumericStructProjection(int Id);

    [Fact]
    public void SelectToList_MutableStructMemberInit()
    {
        List<MutableProjection> rows = _ctx.From<Row>()
            .OrderBy(x => x.Id)
            .Select(x => new MutableProjection { Id = x.Id, Value = x.Value })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(x => (x.Id, x.Value)).Should().Equal((1, "one"), (2, "two"));
    }

    [Fact]
    public void SelectToList_ReadonlyStructConstructor()
    {
        List<ConstructorProjection> rows = _ctx.From<Row>()
            .OrderBy(x => x.Id)
            .Select(x => new ConstructorProjection(x.Id, x.Value))
            .ToList();

        rows.Should().Equal(new ConstructorProjection(1, "one"), new ConstructorProjection(2, "two"));
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void Measure_ConstructorProjectionAllocations_ClassVersusReadonlyStruct()
    {
        using (var insert = _ctx.CreateCommand($"with recursive numbers(id) as (select 1 union all select id + 1 from numbers where id < 10000) insert into {NumericTable} select id from numbers;"))
            insert.ExecuteNonQuery();

        foreach (var count in new[] { 1000, 10000 })
        {
            // Warm query/mapper caches before measuring; keep the measured path synchronous and on this thread.
            MeasureClass(count).Should().HaveCount(count);
            MeasureStruct(count).Should().HaveCount(count);
            var classBytes = new long[5];
            var structBytes = new long[5];
            for (var i = 0; i < classBytes.Length; i++)
            {
                classBytes[i] = MeasureBytes(() => MeasureClass(count));
                structBytes[i] = MeasureBytes(() => MeasureStruct(count));
            }

            Array.Sort(classBytes);
            Array.Sort(structBytes);
            var classMedian = classBytes[classBytes.Length / 2];
            var structMedian = structBytes[structBytes.Length / 2];
            Console.WriteLine($"ALLOC_DIAG count={count} repetitions=5 warmups=1 class_record_bytes={Unsafe.SizeOf<NumericClassProjection>()} struct_record_bytes={Unsafe.SizeOf<NumericStructProjection>()} class_total_bytes={classMedian} struct_total_bytes={structMedian} class_per_row={(double)classMedian / count:F2} struct_per_row={(double)structMedian / count:F2} delta_per_row={(double)(classMedian - structMedian) / count:F2}");
            classMedian.Should().BeGreaterThan(0);
            structMedian.Should().BeGreaterThan(0);
        }
    }

    private List<NumericClassProjection> MeasureClass(int count) => _ctx.From<NumericRow>()
        .Where(x => x.Id <= count)
        .OrderBy(x => x.Id)
        .Select(x => new NumericClassProjection(x.Id))
        .ToList();

    private List<NumericStructProjection> MeasureStruct(int count) => _ctx.From<NumericRow>()
        .Where(x => x.Id <= count)
        .OrderBy(x => x.Id)
        .Select(x => new NumericStructProjection(x.Id))
        .ToList();

    private static long MeasureBytes<T>(Func<T> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
