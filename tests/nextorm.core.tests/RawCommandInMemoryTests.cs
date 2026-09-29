using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory context executes LINQ only; raw SQL execution is rejected through the
/// <see cref="IRawCommandExecutor"/> default interface implementation.
/// </summary>
public class RawCommandInMemoryTests
{
    [Fact]
    public void ExecuteRaw_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = () => context.ExecuteRaw("select 1");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteRawAsync_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = async () => await context.ExecuteRawAsync("select 1");

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void ExecuteProcedure_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = () => context.ExecuteProcedure("my_proc");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteProcedureAsync_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = async () => await context.ExecuteProcedureAsync("my_proc");

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void ExecuteRaw_ExpandedForm_SingleParameter_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = () => context.ExecuteRaw("select @p as value", new ProcedureParameter("p", 1));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteRawAsync_ExpandedForm_SingleParameter_NoToken_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = async () =>
        {
            Func<string, IReadOnlyList<ProcedureParameter>, Task<ProcedureResult>> rawAsync = context.ExecuteRawAsync;
            await rawAsync("select @p as value", [new ProcedureParameter("p", 1)]);
        };

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void ExecuteProcedure_ExpandedForm_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = () => context.ExecuteProcedure("my_proc", new ProcedureParameter("p", 1));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteProcedureAsync_ExpandedForm_NoToken_ThrowsNotSupported()
    {
        using IDataContext context = new InMemoryDataContext();

        var act = async () =>
        {
            Func<string, IReadOnlyList<ProcedureParameter>, Task<ProcedureResult>> procedureAsync = context.ExecuteProcedureAsync;
            await procedureAsync("my_proc", [new ProcedureParameter("p", 1)]);
        };

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
