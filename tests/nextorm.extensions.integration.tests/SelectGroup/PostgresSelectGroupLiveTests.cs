using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;
}
