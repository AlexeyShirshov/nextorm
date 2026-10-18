using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #265: the additive <c>RequiresRecursiveCteUnionAll</c> capability defaults to <c>false</c> on a
/// dialect that does not opt in, so no provider other than SQL Server can start rejecting a legacy
/// distinct <c>UNION</c>.
/// </summary>
public class T265RecursiveUnionTests
{
    [Fact]
    public void DialectRestriction_DefaultsOff()
    {
        ISqlDialect dialect = new DefaultDialect();

        dialect.RequiresRecursiveCteUnionAll.Should().BeFalse();
    }

    private sealed class DefaultDialect : SqlDialectBase
    {
        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }
}
