using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Pins the behaviour-preserving default of the command-aware parameter factory added for
/// driver-agnostic mints: for a non-MySQL provider <c>CreateParam(DbCommand, name, value)</c> must
/// delegate to the public 2-arg <see cref="DataContext.CreateParam(string, object?)"/> and never touch
/// the executing command. Only the MySQL provider overrides it to mint through
/// <c>command.CreateParameter()</c> (pinned in <c>nextorm.mysql.tests</c>), because its EF Core
/// provider runs on a different ADO driver.
/// </summary>
public class BaseCommandParameterCreationTests
{
    private const string TwoArgOrigin = "two-arg";

    [Fact]
    public void CommandOverload_NonMySql_MintsThroughTwoArgFactory()
    {
        using var context = new TestContext();
        using var command = new CountingDbCommand();

        var expected = context.CreateParam("p0", 42);
        var actual = context.CreateThroughCommand(command, "p0", 42);

        // The default base implementation ignores the executing command entirely: a driver-agnostic
        // provider must keep minting through the 2-arg factory it already used.
        command.CreateParameterCalls.Should().Be(0);
        actual.Should().BeOfType<FakeParameter>();
        ((FakeParameter)actual).Origin.Should().Be(TwoArgOrigin);
        actual.ParameterName.Should().Be(expected.ParameterName);
        actual.Value.Should().Be(expected.Value);
    }

    [Fact]
    public void CommandOverload_NonMySql_MapsNullToDbNull()
    {
        using var context = new TestContext();
        using var command = new CountingDbCommand();

        var actual = context.CreateThroughCommand(command, "p0", null);

        command.CreateParameterCalls.Should().Be(0);
        actual.Value.Should().Be(DBNull.Value);
    }

    /// <summary>Minimal dialect that supplies only the two abstract members the context needs.</summary>
    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>
    /// A provider-free <see cref="DataContext"/> whose 2-arg factory returns a distinguishable
    /// <see cref="FakeParameter"/>. The wrappers expose the protected-internal command-aware overloads.
    /// </summary>
    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value)
            => new FakeParameter { ParameterName = name, Value = value ?? DBNull.Value, Origin = TwoArgOrigin };

        protected override DbConnection CreateDbConnection(string? connectionString)
            => throw new NotSupportedException();

        public DbParameter CreateThroughCommand(DbCommand command, string name, object? value)
            => CreateParam(command, name, value);
    }

    private sealed class FakeParameter : DbParameter
    {
        public string Origin { get; set; } = string.Empty;

        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }

    /// <summary>
    /// Minimal <see cref="DbCommand"/> that counts <c>CreateParameter()</c> calls; the base overload
    /// must leave the counter at zero.
    /// </summary>
    private sealed class CountingDbCommand : DbCommand
    {
        public int CreateParameterCalls { get; private set; }

        protected override DbParameter CreateDbParameter()
        {
            CreateParameterCalls++;
            return new FakeParameter { Origin = "command" };
        }

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = null!;

        protected override DbParameterCollection DbParameterCollection { get; } = null!;

        [AllowNull]
        protected override DbTransaction DbTransaction { get; set; } = null!;

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }
}
