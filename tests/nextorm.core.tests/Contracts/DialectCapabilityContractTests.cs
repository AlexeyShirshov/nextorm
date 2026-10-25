using System.Reflection;
using System.Text;
using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Guards the dialect capability contract: every class-A capability is expressed as a nullable
/// capability object, where the object's presence <em>is</em> the capability, so a dialect can never
/// report support without supplying its rendering. A non-null object must render every name/method it
/// reports as supported without throwing. Complements the SQL-shape assertions in the per-provider test
/// projects, which only validate the renderers that already exist.
/// </summary>
public sealed class DialectCapabilityContractTests
{
    private static readonly string[] SessionNames =
        ["current_user", "session_user", "current_schema", "current_database", "version"];

    private static readonly string[] UuidNames = ["gen_random_uuid", "uuidv7"];

    /// <summary>
    /// The session/server identity keys added by T219 (#219): the shared identity members and the
    /// vendor-keyed SQL Server / ClickHouse members, as registered on the session-information renderer.
    /// </summary>
    private static readonly string[] IdentityNames =
    [
        "pg_backend_pid", "inet_server_addr", "inet_server_port", "inet_client_addr", "inet_client_port",
        "connection_id", "user",
        "sqlserver.server_name", "sqlserver.host_id", "sqlserver.host_name", "sqlserver.app_name",
        "sqlserver.suser_name", "sqlserver.original_login",
        "clickhouse.host_name", "clickhouse.tcp_port"
    ];

    private static readonly string[] XmlNames = ["value", "query", "exist"];

    private static readonly string[] ScalarNames =
        ["left", "right", "lpad", "rpad", "repeat", "reverse", "space", "concat_ws", "translate", "ascii", "char"];

    private static IReadOnlyList<string> ScalarArgs(string name) => name switch
    {
        "left" or "right" or "repeat" => ["[s]", "3"],
        "lpad" or "rpad" or "translate" => ["[s]", "3", "' '"],
        "concat_ws" => ["','", "[s]"],
        _ => ["[s]"]
    };

    private static PivotExpression CreatePivot()
    {
        var factory = typeof(PivotExpression).GetMethod("ForPivot", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (PivotExpression)factory.Invoke(
            null,
            [new FromExpression(typeof(object)), typeof(object), PivotAggregate.Sum, null!, null!, new[] { PivotValue.Create("1") }])!;
    }

    private static PivotExpression CreateUnpivot()
    {
        var factory = typeof(PivotExpression).GetMethod("ForUnpivot", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (PivotExpression)factory.Invoke(
            null,
            [new FromExpression(typeof(object)), typeof(object), "val", "nm", new[] { UnpivotColumn.Create("c1") }])!;
    }

    private static IEnumerable<ISqlDialect> AllDialects()
    {
        // Provider assemblies are referenced but lazily loaded, so load every NextORM assembly that
        // sits in the test output before reflecting over the dialects it contains.
        var assemblies = Directory.EnumerateFiles(AppContext.BaseDirectory, "nextorm.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !name.EndsWith(".tests", StringComparison.OrdinalIgnoreCase))
            .Select(name => Assembly.Load(name!));

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsClass: true, IsAbstract: false } && typeof(ISqlDialect).IsAssignableFrom(type))
                    yield return (ISqlDialect)Activator.CreateInstance(type)!;
            }
        }
    }

    /// <summary>
    /// A non-null capability object implies its renderer does not throw: every name/method the object
    /// reports as supported must render.
    /// </summary>
    [Fact]
    public void SupportedCapabilityObjects_ShouldRenderWithoutThrowing()
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty("the provider dialect assemblies must be loaded");

        foreach (var dialect in dialects)
        {
            if (dialect.Iif is { } iif)
                iif.Render("1 = 1", "1", "0").Should().NotBeNullOrEmpty();

            if (dialect.SessionInfoFunctions is { } session)
            {
                foreach (var name in SessionNames)
                {
                    if (!session.Supports(name))
                        continue;

                    session.Render(name).Should().NotBeNullOrEmpty();
                }

                // T219 (#219): every added identity key reaches every dialect through the same renderer.
                // A key the dialect reports as supported must render native SQL; one it does not support
                // must fail closed with NotSupportedException and never fall back to a portable rendering.
                foreach (var name in IdentityNames)
                {
                    if (session.Supports(name))
                    {
                        session.Render(name).Should().NotBeNullOrEmpty(
                            $"{dialect.GetType().Name} reports support for {name}");
                    }
                    else
                    {
                        var render = () => session.Render(name);
                        render.Should().Throw<NotSupportedException>().WithMessage($"*{name}*");
                    }
                }
            }

            if (dialect.UuidGenerators is { } uuid)
            {
                foreach (var name in UuidNames)
                {
                    if (!uuid.Supports(name))
                        continue;

                    uuid.Render(name).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.LimitBy is { } limitBy)
                limitBy.Render(5, 0, ["a", "b"]).Should().NotBeNullOrEmpty();

            if (dialect.XmlFunctions is { } xml)
            {
                foreach (var name in XmlNames)
                {
                    if (!xml.Supports(name))
                        continue;

                    xml.Render(name, "[x]", ["'a'"]).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.ScalarFunctions is { } scalar)
            {
                foreach (var name in ScalarNames)
                {
                    if (!scalar.Supports(name))
                        continue;

                    scalar.Render(name, ScalarArgs(name)).Should().NotBeNullOrEmpty();
                }
            }

            // T282 (#282): a capability flag and its renderer agree, and an absent capability fails
            // closed instead of emitting SQL the provider cannot execute.
            if (dialect.SupportsBase64Encode)
                dialect.MakeBase64Encode("[x]").Should().NotBeNullOrEmpty();
            else
                FluentActions.Invoking(() => dialect.MakeBase64Encode("[x]")).Should().Throw<NotSupportedException>();

            if (dialect.SupportsBase64Decode)
                dialect.MakeBase64Decode("[x]").Should().NotBeNullOrEmpty();
            else
                FluentActions.Invoking(() => dialect.MakeBase64Decode("[x]")).Should().Throw<NotSupportedException>();

            if (dialect.SupportsCryptGenRandom)
                dialect.MakeCryptGenRandom("[n]").Should().NotBeNullOrEmpty();
            else
                FluentActions.Invoking(() => dialect.MakeCryptGenRandom("[n]")).Should().Throw<NotSupportedException>();

            if (dialect.StringSplit is { } split)
                split.Render("','", "[x]").Should().NotBeNullOrEmpty();

            if (dialect.DateConversion is { } dateConversion)
                dateConversion.Render("to_date", ["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.SequenceAggregates is { } sequence)
                sequence.Render("window_funnel", "10", "[t], [c]").Should().NotBeNullOrEmpty();

            if (dialect.UniqAggregates is { } uniq)
                uniq.Render("uniq", "[x]").Should().NotBeNullOrEmpty();

            if (dialect.QuantileAggregates is { } quantile)
            {
                quantile.Render("quantile", "0.5", "[x]").Should().NotBeNullOrEmpty();
                quantile.RenderLevels("quantiles", "0.5", "[x]").Should().NotBeNullOrEmpty();
                quantile.RenderMedian("[x]").Should().NotBeNullOrEmpty();
            }

            if (dialect.TopKAggregates is { } topK)
            {
                topK.Render("top_k", "3", "[x]").Should().NotBeNullOrEmpty();
                topK.RenderWeighted("top_k_weighted", "2", "[x]", "[w]").Should().NotBeNullOrEmpty();
            }

            if (dialect.MultiIf is { } multiIf)
                multiIf.Render(["[c]", "1", "0"], typeof(int)).Should().NotBeNullOrEmpty();

            if (dialect.DistinctOn is { } distinctOn)
                distinctOn.Render(["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.TableSample is { } tableSample)
            {
                foreach (var method in Enum.GetValues<TableSampleMethod>())
                {
                    if (!tableSample.Supports(method))
                        continue;

                    tableSample.Render(method, 10, null).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.ArrayJoinClause is { } arrayJoin)
                arrayJoin.Render(ArrayJoinKind.Left, ["[x]"]).Should().NotBeNullOrEmpty();

            if (dialect.Lock is { } lockRenderer)
            {
                foreach (var mode in Enum.GetValues<LockMode>())
                    foreach (var wait in Enum.GetValues<LockWaitMode>())
                        lockRenderer.Render(mode, wait).Should().NotBeNullOrEmpty();
            }

            if (dialect.Pivot is { } pivotRenderer)
            {
                var pivot = CreatePivot();
                pivotRenderer.RenderPivot(pivot, "[src]", "[agg]", "[for]", "t1").Should().NotBeNullOrEmpty();

                var unpivot = CreateUnpivot();
                pivotRenderer.RenderUnpivot(unpivot, "[src]", "t1").Should().NotBeNullOrEmpty();
            }

            if (dialect.StringFormats is { } formats)
            {
                foreach (var specifier in new[] { 'N', 'F', 'D', 'X' })
                {
                    if (!formats.SupportsNumber(specifier))
                        continue;

                    formats.RenderNumber("[x]", specifier, 2).Should().NotBeNullOrEmpty();
                }

                foreach (var dateFormat in new[] { "yyyy", "yyyy-MM-dd", "HH:mm:ss" })
                {
                    if (!formats.SupportsDateFormat(dateFormat))
                        continue;

                    formats.RenderDate("[x]", dateFormat).Should().NotBeNullOrEmpty();
                }
            }

            if (dialect.SupportsCollation)
                dialect.MakeCollate("[x]", "C").Should().NotBeNullOrEmpty();

            if (dialect.SupportsOrdinalComparison)
                dialect.MakeOrdinal("[x]", false).Should().NotBeNullOrEmpty();

            if (dialect.SupportsRegex)
            {
                dialect.MakeRegexMatch("[x]", "p", false).Should().NotBeNullOrEmpty();
                dialect.MakeRegexReplace("[x]", "p", "r", false).Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public void AtLeastOneDialect_ShouldExposeEachClassACapability()
    {
        var dialects = AllDialects().ToList();

        dialects.Should().Contain(d => d.Iif != null);
        dialects.Should().Contain(d => d.SessionInfoFunctions != null);
        dialects.Should().Contain(d => d.UuidGenerators != null);
        dialects.Should().Contain(d => d.LimitBy != null);
        dialects.Should().Contain(d => d.XmlFunctions != null);
        dialects.Should().Contain(d => d.ScalarFunctions != null);
        dialects.Should().Contain(d => d.StringSplit != null);
        dialects.Should().Contain(d => d.DateConversion != null);
        dialects.Should().Contain(d => d.SequenceAggregates != null);
        dialects.Should().Contain(d => d.UniqAggregates != null);
        dialects.Should().Contain(d => d.QuantileAggregates != null);
        dialects.Should().Contain(d => d.TopKAggregates != null);
        dialects.Should().Contain(d => d.MultiIf != null);
        dialects.Should().Contain(d => d.DistinctOn != null);
        dialects.Should().Contain(d => d.TableSample != null);
        dialects.Should().Contain(d => d.Pivot != null);
        dialects.Should().Contain(d => d.ArrayJoinClause != null);
        dialects.Should().Contain(d => d.Lock != null);
        dialects.Should().Contain(d => d.StringFormats != null);
        dialects.Should().Contain(d => d.SupportsCollation);
        dialects.Should().Contain(d => d.SupportsOrdinalComparison);
        dialects.Should().Contain(d => d.SupportsRegex);
        dialects.Should().Contain(d => d.SupportsSavepoints);
    }

    /// <summary>
    /// <see cref="ISqlDialect.SupportsSavepoints"/> is a default interface method, so it cannot be
    /// implemented by a member declared in a derived class: an interface DIM stays in force for the
    /// interface dispatch even when the class declares a same-named member. The base class therefore
    /// carries a <c>virtual</c> member as well, and a third-party dialect opts in with
    /// <c>override</c>. Both shapes are pinned here so a regression to the class-member-only form
    /// fails loudly instead of silently reporting <c>false</c> through the interface.
    /// </summary>
    [Fact]
    public void SupportsSavepoints_ThirdPartyDialectDerivedFromBase_ReadsThroughTheInterface()
    {
        ISqlDialect optingIn = SavepointCapableThirdPartyDialect.Instance;
        ISqlDialect defaulted = SavepointlessThirdPartyDialect.Instance;

        optingIn.SupportsTransactions.Should().BeTrue();
        optingIn.SupportsSavepoints.Should().BeTrue();
        defaulted.SupportsTransactions.Should().BeTrue();
        defaulted.SupportsSavepoints.Should().BeFalse("the base-class default is fail-closed");
    }

    /// <summary>A third-party-style dialect deriving from <see cref="SqlDialectBase"/> and opting in.</summary>
    private sealed class SavepointCapableThirdPartyDialect : SqlDialectBase
    {
        internal static readonly SavepointCapableThirdPartyDialect Instance = new();

        public override bool SupportsSavepoints => true;

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A third-party-style dialect deriving from <see cref="SqlDialectBase"/> that declares nothing.</summary>
    private sealed class SavepointlessThirdPartyDialect : SqlDialectBase
    {
        internal static readonly SavepointlessThirdPartyDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>
    /// <c>UPDATE ... RETURNING</c> is derived from the general <c>RETURNING</c> capability by default:
    /// a dialect that cannot return rows at all cannot return updated rows either, so every provider
    /// except MariaDB reports <see cref="ISqlDialect.SupportsUpdateReturning"/> equal to
    /// <see cref="ISqlDialect.SupportsReturning"/>. PostgreSQL and SQLite support RETURNING (hence
    /// UPDATE RETURNING); SQL Server, MySQL and ClickHouse do not. MariaDB deliberately overrides the
    /// default with its 13.0+ version gate, so the unset dialect reports false.
    /// </summary>
    [Fact]
    public void SupportsUpdateReturning_ShouldDefaultToSupportsReturning()
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty("the provider dialect assemblies must be loaded");

        foreach (var dialect in dialects.Where(d => d.GetType().Name != "MariaDbDialect"))
            dialect.SupportsUpdateReturning.Should().Be(dialect.SupportsReturning);

        var byName = dialects.ToDictionary(d => d.GetType().Name);

        byName["PostgresDialect"].SupportsReturning.Should().BeTrue();
        byName["PostgresDialect"].SupportsUpdateReturning.Should().BeTrue();

        byName["SqliteDialect"].SupportsReturning.Should().BeTrue();
        byName["SqliteDialect"].SupportsUpdateReturning.Should().BeTrue();

        byName["SqlServerDialect"].SupportsReturning.Should().BeFalse();
        byName["SqlServerDialect"].SupportsUpdateReturning.Should().BeFalse();

        byName["MySqlDialect"].SupportsReturning.Should().BeFalse();
        byName["MySqlDialect"].SupportsUpdateReturning.Should().BeFalse();

        byName["ClickHouseDialect"].SupportsReturning.Should().BeFalse();
        byName["ClickHouseDialect"].SupportsUpdateReturning.Should().BeFalse();

        // MariaDB overrides the default with its 13.0+ gate: unset is false (and it cannot RETURN rows).
        byName["MariaDbDialect"].SupportsReturning.Should().BeFalse();
        byName["MariaDbDialect"].SupportsUpdateReturning.Should().BeFalse();
    }

    /// <summary>
    /// The source-level row-lock capability (<c>FOR UPDATE OF</c>) must be explicit per dialect: only a
    /// grammar that can address a named source opts in. MariaDB must stay fail-closed because its grammar
    /// has no <c>OF</c> form; a dialect with no lock renderer at all cannot express either scope.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T216")]
    public void SupportsSourceLocks_ShouldBeExplicitPerDialect()
    {
        var byName = AllDialects().ToDictionary(d => d.GetType().Name);

        byName["PostgresDialect"].Lock!.SupportsSourceLocks.Should().BeTrue();
        byName["MySqlDialect"].Lock!.SupportsSourceLocks.Should().BeTrue();
        byName["SqlServerDialect"].Lock!.SupportsSourceLocks.Should().BeTrue();
        byName["MariaDbDialect"].Lock!.SupportsSourceLocks.Should().BeFalse(
            "MariaDB has no FOR UPDATE OF form; it must never inherit MySQL's OF.");
        byName["SqliteDialect"].Lock.Should().BeNull();
        byName["ClickHouseDialect"].Lock.Should().BeNull();
    }

    /// <summary>
    /// The target-aware lock overload is fail-closed: a dialect that reports
    /// <see cref="ILockRenderer.SupportsSourceLocks"/> renders a non-empty targeted token, and one that
    /// does not rejects the target with <see cref="NotSupportedException"/> instead of emitting a
    /// whole-query lock or an unsupported <c>OF</c>.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T216")]
    public void SourceLockTarget_ShouldBeTargetedOrFailClosed()
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty();

        foreach (var dialect in dialects)
        {
            if (dialect.Lock is not { } lockRenderer)
                continue;

            if (lockRenderer.SupportsSourceLocks)
            {
                lockRenderer.Render(LockMode.Update, LockWaitMode.Wait, KeywordCase.Lower, "\"t1\"")
                    .Should().NotBeNullOrEmpty($"{dialect.GetType().Name} reports source-lock support");
            }
            else
            {
                var act = () => lockRenderer.Render(LockMode.Update, LockWaitMode.Wait, KeywordCase.Lower, "\"t1\"");
                act.Should().Throw<NotSupportedException>($"{dialect.GetType().Name} must reject a source lock");
            }
        }
    }

    /// <summary>
    /// T219 (#219): the T-SQL identity members are gated by
    /// <see cref="ISqlServerFunctions.SupportsSessionIdentity"/>, and the flag is an opt-in: only the SQL
    /// Server dialect reports <c>true</c>. A dialect that never exposes the surface has no
    /// <see cref="ISqlDialect.SqlServerFunctions"/> object at all, so the members cannot leak.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T219")]
    public void SessionIdentity_ShouldBeOptedIntoBySqlServerOnly()
    {
        var dialects = AllDialects().ToList();
        dialects.Should().NotBeEmpty("the provider dialect assemblies must be loaded");

        var sqlServer = dialects.Single(d => d.GetType().Name == "SqlServerDialect");
        sqlServer.SqlServerFunctions.Should().NotBeNull("SQL Server exposes the T-SQL identity surface");
        sqlServer.SqlServerFunctions!.SupportsSessionIdentity.Should().BeTrue();

        foreach (var dialect in dialects.Where(d => d.GetType().Name != "SqlServerDialect"))
        {
            if (dialect.SqlServerFunctions is not { } functions)
                continue;

            functions.SupportsSessionIdentity.Should().BeFalse(
                $"{dialect.GetType().Name} must not report the T-SQL identity surface");
        }
    }

    /// <summary>
    /// T219 (#219): <c>SupportsSessionIdentity</c> must stay a default interface method, not an abstract
    /// member: an <see cref="ISqlServerFunctions"/> implementation written before the member existed keeps
    /// the fail-closed default. The default value itself is pinned by the core tests; this asserts the
    /// interface shape so adding a member cannot silently become a breaking change.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T219")]
    public void SupportsSessionIdentity_ShouldRemainADefaultInterfaceMethod()
    {
        var property = typeof(ISqlServerFunctions).GetProperty(nameof(ISqlServerFunctions.SupportsSessionIdentity));

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<bool>();
        property.GetMethod.Should().NotBeNull();
        property.GetMethod!.IsAbstract.Should().BeFalse(
            "the member keeps its default implementation so existing custom dialects stay compatible");
    }

    /// <summary>
    /// T219 (#219): the per-provider contract of the 15 identity keys. Exactly the keys of the approved
    /// matrix are advertised, every other key is refused by <c>Supports</c> (and by <c>Render</c>, asserted
    /// above), so no provider silently over- or under-reports the surface — including MariaDB, which
    /// inherits the MySQL set.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T219")]
    public void SessionIdentityKeys_ShouldFollowThePerProviderContract()
    {
        var byName = AllDialects().ToDictionary(d => d.GetType().Name);

        var expected = new Dictionary<string, string[]>
        {
            ["PostgresDialect"] =
                ["pg_backend_pid", "inet_server_addr", "inet_server_port", "inet_client_addr", "inet_client_port"],
            ["MySqlDialect"] = ["connection_id", "user"],
            ["MariaDbDialect"] = ["connection_id", "user"],
            ["SqlServerDialect"] =
                ["sqlserver.server_name", "sqlserver.host_id", "sqlserver.host_name", "sqlserver.app_name",
                 "sqlserver.suser_name", "sqlserver.original_login"],
            ["ClickHouseDialect"] = ["connection_id", "clickhouse.host_name", "clickhouse.tcp_port"],
            ["SqliteDialect"] = []
        };

        foreach (var (dialectName, keys) in expected)
        {
            byName.Should().ContainKey(dialectName);
            var session = byName[dialectName].SessionInfoFunctions;
            session.Should().NotBeNull($"{dialectName} renders the session family");

            foreach (var key in IdentityNames)
            {
                session!.Supports(key).Should().Be(
                    keys.Contains(key), $"{dialectName} must advertise {key} exactly as the contract states");
            }
        }
    }
}
