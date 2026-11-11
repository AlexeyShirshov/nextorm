using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;
}
