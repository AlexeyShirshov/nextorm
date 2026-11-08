using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

[Collection("MySql")]
public sealed class MySqlSelectGroupLiveTests : SelectGroupLiveTestsBase
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;
}
