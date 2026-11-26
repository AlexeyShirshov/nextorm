using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed partial class SqliteSharedPlanBindingTests
{
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
