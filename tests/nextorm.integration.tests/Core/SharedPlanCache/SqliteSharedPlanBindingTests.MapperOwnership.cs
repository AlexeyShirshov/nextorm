using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void UncachedProjection_DoesNotKeepItsConverterInTheMapperCache()
    {
        var converter = Execute(40);
        Execute(98);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        converter.IsAlive.Should().BeFalse("the shared mapper cache must not own the projection's converter");

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(int offset)
        {
            using var connection = OpenSeededConnection();
            using var context = new SqliteDataContext(connection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var query = context.From<BindingRow>().Where(row => row.Id == 1).Select(row => row.Id);
            query.PrepareCommand(token);
            // Supply a command-local converter without registering it in global entity metadata.
            var converter = new ProjectionConverter(offset);
            query.SelectList![0].ProviderType = typeof(int);
            query.SelectList[0].Converter = converter;
            var first = context.GetPreparedQueryCommand(query, false, false, token);
            var second = context.GetPreparedQueryCommand(query, false, false, token);
            first.ToList(context).Should().Equal(offset + 1);
            second.ToList(context).Should().Equal(offset + 1);
            BoundQueryInvocation<int>.GetPlan(second)!.MapDelegate.Should()
                .BeSameAs(BoundQueryInvocation<int>.GetPlan(first)!.MapDelegate);
            return new WeakReference(converter);
        }
    }

    [Fact]
    public void UncachedProjection_KeepingOneConverterAliveDoesNotKeepAnotherAlive()
    {
        var liveConverter = new ProjectionConverter(40);
        var releasedConverter = Execute(liveConverter);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        releasedConverter.IsAlive.Should().BeFalse("a mapper depends on the lifetime of both of its converters");
        GC.KeepAlive(liveConverter);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference Execute(ProjectionConverter first)
        {
            using var connection = OpenSeededConnection();
            using var context = new SqliteDataContext(connection, new DataContextBuilder());
            var token = TestContext.Current.CancellationToken;
            var query = context.From<BindingRow>().Where(row => row.Id == 1)
                .Select(row => new { First = row.Id, Second = row.Id });
            query.PrepareCommand(token);
            var second = new ProjectionConverter(98);
            var columns = query.SelectList!;
            columns[0].ProviderType = typeof(int);
            columns[0].Converter = first;
            columns[1].ProviderType = typeof(int);
            columns[1].Converter = second;

            var row = context.GetPreparedQueryCommand(query, false, false, token).ToList(context).Should().ContainSingle().Which;
            (row.First, row.Second).Should().Be((41, 99));
            return new WeakReference(second);
        }
    }

    private sealed class ProjectionConverter(int offset) : ValueConverter<int, int>
    {
        public override int ConvertToProvider(int model) => model - offset;

        public override int ConvertFromProvider(int provider) => provider + offset;
    }
}
