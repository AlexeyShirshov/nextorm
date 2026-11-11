using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class SqlServerSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;
}
