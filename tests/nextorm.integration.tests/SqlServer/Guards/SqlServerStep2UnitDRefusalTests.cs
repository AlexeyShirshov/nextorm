using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// Шаг 2, unit D: unversioned-отказы version-gated имён SQL Server, которые ещё не вызывались.
/// Каждый кейс — реальный запрос к контейнеру SQL Server на unversioned-диалекте: вызов обязан быть
/// отвергнут до SQL с диагностикой, вычисленной <c>SqlServerSpecificFunctions.GetUnsupportedReason</c>
/// (требуемая major-версия и объявленная версия), а не generic-сообщением.
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerStep2UnitDRefusalTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Version-gated scalar names of the SQL Server surface that no refusal test exercised yet.</summary>
    public static TheoryData<string> GatedNames => new()
    {
        "regexp_substr",
        "regexp_instr",
        "edit_distance",
        "edit_distance_similarity",
        "jaro_winkler_distance",
        "jaro_winkler_similarity",
        "right_shift",
        "get_bit",
        "set_bit"
    };

    /// <summary>
    /// Each gated name on the unversioned dialect must surface the version-specific refusal: the
    /// engine is never reached, and the message names the required major version.
    /// </summary>
    [Theory]
    [MemberData(nameof(GatedNames))]
    public void VersionGatedNames_OnUnversionedDialect_ShouldBeRefusedWithTheVersionDiagnostic(string name)
    {
        Action call = name switch
        {
            "regexp_substr" => () => Query(x => SqlFunctions.SqlServer.regexp_substr("abc", "a")),
            "regexp_instr" => () => Query(x => SqlFunctions.SqlServer.regexp_instr("abc", "a")),
            "edit_distance" => () => Query(x => SqlFunctions.SqlServer.edit_distance("abc", "abd")),
            "edit_distance_similarity" => () => Query(x => SqlFunctions.SqlServer.edit_distance_similarity("abc", "abd")),
            "jaro_winkler_distance" => () => Query(x => SqlFunctions.SqlServer.jaro_winkler_distance("abc", "abd")),
            "jaro_winkler_similarity" => () => Query(x => SqlFunctions.SqlServer.jaro_winkler_similarity("abc", "abd")),
            "right_shift" => () => Query(x => SqlFunctions.SqlServer.right_shift((byte)2, 1)),
            "get_bit" => () => Query(x => SqlFunctions.SqlServer.get_bit((byte)2, 1)),
            "set_bit" => () => Query(x => SqlFunctions.SqlServer.set_bit((byte)2, 1, true)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown gated name")
        };

        call.Should().Throw<NotSupportedException>()
            .WithMessage("*major version*", "the refusal must carry the required version, not a generic message");
    }

    /// <summary>Evaluates one scalar projection over the seeded probe row (the refusal fires before any SQL).</summary>
    private void Query<T>(Expression<Func<IComplexEntity, T>> select) =>
        Context.From<IComplexEntity>().Where(x => x.Id == 1).Select(select).ToList();
}
