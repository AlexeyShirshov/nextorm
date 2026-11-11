using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class SqliteSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;
}
