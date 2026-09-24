using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The transaction role is deliberately not part of <see cref="IDataContext"/>, so a context without a
/// connection (the in-memory one) must not implement it.
/// </summary>
public class TransactionRoleTests
{
    [Fact]
    public void InMemoryDataContext_ShouldNotImplementITransactionManager()
    {
        using var ctx = new InMemoryDataContext();

        (ctx is ITransactionManager).Should().BeFalse();
    }
}
