using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using MySqlConnector;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// Pins the command-aware parameter factory added for driver-agnostic mints: the executing command's
/// own driver creates the parameter (so a <c>MySql.Data</c> command accepts it), while the public
/// 2-arg factory keeps returning the <c>MySqlConnector</c> parameter unchanged.
/// </summary>
public class CommandParameterCreationTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    [Fact]
    public void CommandAwareFactory_MintsThroughTheExecutingCommand()
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());
        using var command = new CountingDbCommand();

        var parameter = ctx.Mint(command, "p0", 42);

        command.CreateParameterCalls.Should().Be(1);
        parameter.Should().BeSameAs(command.LastParameter);
        parameter.ParameterName.Should().Be("p0");
        parameter.Value.Should().Be(42);
    }

    [Fact]
    public void CommandAwareFactory_MapsNullToDbNull()
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());
        using var command = new CountingDbCommand();

        var parameter = ctx.Mint(command, "p0", null);

        command.CreateParameterCalls.Should().Be(1);
        parameter.Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void PublicTwoArgFactory_StillReturnsMySqlConnectorParameter()
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());

        var parameter = ctx.CreateParam("p0", null);

        parameter.Should().BeOfType<MySqlParameter>();
        parameter.ParameterName.Should().Be("p0");
        parameter.Value.Should().Be(DBNull.Value);
    }

    /// <summary>
    /// The command-unaware compatibility overload must still apply the descriptor's metadata to the
    /// minted parameter. Pins every field: dropping <c>Direction</c>, <c>DbType</c> or <c>Size</c>
    /// from the shared applier must fail this test.
    /// </summary>
    [Fact]
    public void CommandUnawareProcedureFactory_AppliesDescriptorMetadata()
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());

        var parameter = ctx.MintProcedure(new ProcedureParameter(
            "p0", 42, ParameterDirection.InputOutput, DbType.Int64, 17));

        parameter.Should().BeOfType<MySqlParameter>();
        parameter.ParameterName.Should().Be("p0");
        parameter.Value.Should().Be(42);
        parameter.Direction.Should().Be(ParameterDirection.InputOutput);
        parameter.DbType.Should().Be(DbType.Int64);
        parameter.Size.Should().Be(17);
    }

    [Fact]
    public void CommandAwareProcedureFactory_MintsThroughTheExecutingCommand()
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());
        using var command = new SentinelDbCommand();

        // The sentinel command returns its own DbParameter type: if the procedure path had used the
        // 2-arg factory (or `new MySqlConnector.MySqlParameter`) the result would be a MySqlParameter
        // and this same-instance assertion would fail.
        var parameter = ctx.MintProcedure(command, new ProcedureParameter("p0", 42, DbType: DbType.Int32));

        command.CreateParameterCalls.Should().Be(1);
        parameter.Should().BeSameAs(command.LastParameter);
        parameter.Should().BeOfType<SentinelDbParameter>();
        parameter.Should().NotBeOfType<MySqlParameter>();
        parameter.ParameterName.Should().Be("p0");
        parameter.Value.Should().Be(42);
        parameter.DbType.Should().Be(DbType.Int32);
    }

    /// <summary>
    /// The command-unaware procedure factory must validate the descriptor name before any provider
    /// work, so a malformed name fails with <see cref="ArgumentException"/> rather than producing a
    /// nameless parameter.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandUnawareProcedureFactory_RejectsInvalidName(string? name)
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());

        // The positional record constructor does not validate `Name`; `ProcedureParameter.Table` does.
        var act = () => ctx.MintProcedure(new ProcedureParameter(name!, 42));

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// The command-aware procedure factory must perform the same name validation before it touches the
    /// executing command, so an invalid descriptor never mints a parameter.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandAwareProcedureFactory_RejectsInvalidNameBeforeMinting(string? name)
    {
        using var ctx = new ProbeContext(ConnectionString, new DataContextBuilder());
        using var command = new SentinelDbCommand();

        var act = () => ctx.MintProcedure(command, new ProcedureParameter(name!, 42));

        act.Should().Throw<ArgumentException>();
        command.CreateParameterCalls.Should().Be(0);
    }

    // A derived context is required because the command-aware CreateParam overloads are protected
    // internal and the test assembly is neither the core provider assembly nor an InternalsVisibleTo
    // friend.
    private sealed class ProbeContext(string connectionString, DataContextBuilder builder)
        : MySqlDataContext(connectionString, builder)
    {
        public DbParameter Mint(DbCommand command, string name, object? value)
            => CreateParam(command, name, value);

        public DbParameter MintProcedure(ProcedureParameter parameter)
            => CreateProcedureParameter(parameter);

        public DbParameter MintProcedure(DbCommand command, ProcedureParameter parameter)
            => CreateProcedureParameter(command, parameter);
    }

    /// <summary>
    /// Minimal <see cref="DbCommand"/> whose only meaningful behaviour is counting
    /// <c>CreateParameter()</c> calls and remembering the parameter it minted, so the test can prove
    /// the command-aware factory went through the executing command rather than <c>new</c>.
    /// </summary>
    private sealed class CountingDbCommand : DbCommand
    {
        public int CreateParameterCalls { get; private set; }

        public DbParameter? LastParameter { get; private set; }

        protected override DbParameter CreateDbParameter()
        {
            CreateParameterCalls++;
            LastParameter = new MySqlParameter();
            return LastParameter;
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

    /// <summary>
    /// Minimal <see cref="DbCommand"/> that mints a <see cref="SentinelDbParameter"/> rather than a
    /// <c>MySqlConnector</c> parameter, so a test can prove the returned instance came from this command
    /// and not from the provider's 2-arg factory.
    /// </summary>
    private sealed class SentinelDbCommand : DbCommand
    {
        public int CreateParameterCalls { get; private set; }

        public SentinelDbParameter? LastParameter { get; private set; }

        protected override DbParameter CreateDbParameter()
        {
            CreateParameterCalls++;
            LastParameter = new SentinelDbParameter();
            return LastParameter;
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

    /// <summary>A driver-free parameter used to tell a command-minted instance from a factory one.</summary>
    private sealed class SentinelDbParameter : DbParameter
    {
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
}
