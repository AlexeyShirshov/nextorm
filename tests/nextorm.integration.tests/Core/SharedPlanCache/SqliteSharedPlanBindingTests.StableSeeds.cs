using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
    [Fact]
    public void ConstantValueList_MutatingTheCallerArrayDoesNotChangeThePublishedKey()
    {
        var firstTap = new BindingTap();
        var secondTap = new BindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var authored = new[] { 1 };
        var original = Execute(first, authored, 1);
        firstTap.LastParameters.Should().Equal((0, "p0", 1));

        authored[0] = 2;
        var equal = Execute(second, [1], 1);
        secondTap.LastParameters.Should().Equal((0, "p0", 1));
        equal.Should().BeSameAs(original, "the key must keep the original literal shape");
        var different = Execute(second, authored, 2);
        secondTap.LastParameters.Should().Equal((0, "p0", 2));
        different.Should().NotBeSameAs(original);

        static DbPreparedQueryCommand<int> Execute(SqliteDataContext context, int[] values, int expected)
        {
            Expression<Func<BindingRow, bool>> template = row => Enumerable.Contains(new[] { 0 }, row.Id);
            var contains = (MethodCallExpression)template.Body;
            var predicate = Expression.Lambda<Func<BindingRow, bool>>(
                contains.Update(contains.Object, [Expression.Constant(values), contains.Arguments[1]]), template.Parameters);
            var query = context.From<BindingRow>().WithTag("constant value list key").Where(predicate).Select(row => row.Id);
            var invocation = context.GetStreamingRowsQueryCommand(query, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(expected);
            return BoundQueryInvocation<int>.GetPlan(invocation)!;
        }
    }

    [Fact]
    public void InlineConvertedValues_BindAnIndependentBlobOnEachConnection()
    {
        var firstTap = new BlobBindingTap();
        var secondTap = new BlobBindingTap();
        using var firstConnection = OpenSeededConnection();
        using var secondConnection = OpenSeededConnection();
        SeedBlobs(firstConnection);
        SeedBlobs(secondConnection);
        using var first = new SqliteDataContext(firstConnection, new DataContextBuilder().AddInterceptor(firstTap));
        using var second = new SqliteDataContext(secondConnection, new DataContextBuilder().AddInterceptor(secondTap));
        var firstPlan = Execute(first);
        var secondPlan = Execute(second);

        secondPlan.Should().BeSameAs(firstPlan);
        firstTap.ParameterName.Should().Be("p0");
        secondTap.ParameterName.Should().Be("p0");
        firstTap.Value.Should().Equal((byte)1);
        secondTap.Value.Should().Equal((byte)1);
        secondTap.Value.Should().NotBeSameAs(firstTap.Value, "a stable inline expression does not make its converted byte array immutable");
        firstPlan.ParamSeeds[0].Value.Should().BeNull();
        firstPlan.DbCommandParams[0].Value.Should().Be(DBNull.Value);

        static DbPreparedQueryCommand<int> Execute(SqliteDataContext context)
        {
            var query = context.From<BlobSeedRow>()
                .Where(row => SqlFunctions.Sql.@in(row.Code, new[] { 1 })).Select(row => row.Id);
            var invocation = context.GetStreamingRowsQueryCommand(query, TestContext.Current.CancellationToken);
            invocation.ToList(context).Should().Equal(1);
            return BoundQueryInvocation<int>.GetPlan(invocation)!;
        }

        static void SeedBlobs(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "create table issue16_seed_rows (Id integer not null, Code blob not null);"
                + " insert into issue16_seed_rows values (1, x'01'), (2, x'02');";
            command.ExecuteNonQuery();
        }
    }

    [SqlTable("issue16_seed_rows")]
    public sealed class BlobSeedRow
    {
        public int Id { get; set; }

        [ValueConverter(typeof(IntToBlobSeedConverter))]
        public int Code { get; set; }
    }

    public sealed class IntToBlobSeedConverter : ValueConverter<int, byte[]>
    {
        public override byte[] ConvertToProvider(int model) => [(byte)model];

        public override int ConvertFromProvider(byte[]? provider) => provider![0];
    }

    private sealed class BlobBindingTap : IQueryInterceptor
    {
        internal byte[]? Value { get; private set; }
        internal string? ParameterName { get; private set; }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            command.Parameters.Count.Should().Be(1);
            var parameter = command.Parameters[0];
            ParameterName = parameter.ParameterName;
            Value = (byte[])parameter.Value!;
        }
    }
}
