using System.Linq.Expressions;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T312 (#312): a derived source whose projection is an unaliased computed scalar must expose the
/// output identifier its consumers reference. Before the fix the derived subquery rendered
/// <c>select (id * 2)</c> with no alias while the enclosing query referenced <c>c0</c>, so the server
/// rejected the statement (<c>no such column: c0</c> / SQLSTATE 42703). These are SQL-generation
/// assertions over a placeholder connection: the API under test is the rendered text and its
/// identifier/quoting/uniqueness contract, never a connection.
/// </summary>
public class T312DerivedScalarAliasTests
{
    private const string OpenQuote = "'";
    private const string CloseQuote = "'";

    private static readonly Regex AliasPattern = new(
        "\\bas\\s+" + Regex.Escape(OpenQuote) + "(?<id>[^" + Regex.Escape(OpenQuote) + Regex.Escape(CloseQuote).Replace("]", "\\]") + "]+)" + Regex.Escape(CloseQuote),
        RegexOptions.Compiled);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static string Decode(string text)
        => text.Replace("'", string.Empty).Replace("\"", string.Empty).Replace("`", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty);

    private static List<string> DeclaredAliases(string selectList)
        => AliasPattern.Matches(selectList).Select(m => m.Groups["id"].Value).ToList();

    /// <summary>The projection list of a SELECT body, excluding the FROM/WHERE/ORDER BY clauses.</summary>
    private static string SelectListOf(string queryBody)
    {
        var start = queryBody.IndexOf("select ", StringComparison.OrdinalIgnoreCase);
        start.Should().BeGreaterThanOrEqualTo(0, "the rendered body must contain a SELECT: " + queryBody);
        start += "select ".Length;
        var end = queryBody.Length;
        foreach (var terminator in new[] { "\n from ", " from ", "\n where ", " where ", "\n order by ", " order by " })
        {
            var hit = queryBody.IndexOf(terminator, start, StringComparison.OrdinalIgnoreCase);
            if (hit >= 0 && hit < end)
                end = hit;
        }

        return queryBody[start..end].Trim();
    }

    /// <summary>The content of the first derived source, found by matching its opening parenthesis.</summary>
    private static string FirstDerivedSource(string sql)
    {
        var marker = sql.IndexOf("\n from (", StringComparison.Ordinal);
        if (marker < 0)
            marker = sql.IndexOf(" from (", StringComparison.Ordinal);
        marker.Should().BeGreaterThanOrEqualTo(0, "the query must render a derived source; SQL: " + sql);

        var open = sql.IndexOf('(', marker);
        var start = open + 1;
        var depth = 0;
        for (var i = open; i < sql.Length; i++)
        {
            if (sql[i] == '(') depth++;
            else if (sql[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return sql[start..i];
            }
        }

        throw new InvalidOperationException("unbalanced derived source; SQL: " + sql);
    }

    /// <summary>Every derived source's select list, outermost first.</summary>
    private static List<string> DerivedSelectLists(string sql)
    {
        var lists = new List<string>();
        var index = 0;
        while (true)
        {
            var marker = sql.IndexOf(" from (", index, StringComparison.Ordinal);
            if (marker < 0)
                return lists;

            var open = sql.IndexOf('(', marker);
            var depth = 0;
            var close = -1;
            for (var i = open; i < sql.Length; i++)
            {
                if (sql[i] == '(') depth++;
                else if (sql[i] == ')' && --depth == 0)
                {
                    close = i;
                    break;
                }
            }

            if (close < 0)
                throw new InvalidOperationException("unbalanced derived source; SQL: " + sql);

            var body = sql[(open + 1)..close];
            if (body.StartsWith("select ", StringComparison.OrdinalIgnoreCase))
                lists.Add(SelectListOf(body));
            index = open + 1;
        }
    }

    private static string TopSelectList(string sql)
    {
        var start = sql.IndexOf("select ", StringComparison.OrdinalIgnoreCase);
        start.Should().BeGreaterThanOrEqualTo(0, "the statement must render a SELECT; SQL: " + sql);
        start += "select ".Length;

        var end = sql.Length;
        foreach (var terminator in new[] { "\n from (", "\n from ", "\n where ", "\n order by ", " into " })
        {
            var hit = sql.IndexOf(terminator, start, StringComparison.OrdinalIgnoreCase);
            if (hit >= 0 && hit < end)
                end = hit;
        }

        return sql[start..end].Trim();
    }

    private static void AssertContainsIdentifier(string text, string identifier, string because, bool ignoreCase = false)
    {
        var pattern = "(?<![A-Za-z0-9_])" + Regex.Escape(identifier) + "(?![A-Za-z0-9_])";
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        Regex.IsMatch(Decode(text), pattern, options).Should().BeTrue(
            because + " (expected identifier '" + identifier + "' in: " + text + ")");
    }

    /// <summary>
    /// AC4: the derived source declares exactly one output identifier (provider-quoted) and every
    /// enclosing reference resolves to that same decoded identifier.
    /// </summary>
    private static void AssertDerivedScalarContract<T>(IDataContext ctx, QueryCommand<T> command, string because)
    {
        var sql = SqlOf(ctx, command);
        var source = SelectListOf(FirstDerivedSource(sql));
        var aliases = DeclaredAliases(source);

        aliases.Should().ContainSingle(
            because + ": the derived projection must declare the scalar output identifier exactly once; SQL: " + sql);

        var identifier = aliases[0];
        AssertContainsIdentifier(TopSelectList(sql), identifier, because + ": the outer projection must reference the declared identifier; SQL: " + sql);
    }

    [Fact]
    public void PlainDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);

        AssertDerivedScalarContract(ctx, ctx.From(inner).Select(x => x * 3), "plain derived scalar");
    }

    [Fact]
    public void OrderedDerived_ShouldMatchInnerDeclarationAndOrderByReference()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);

        var sql = SqlOf(ctx, ctx.From(inner).OrderBy(x => x).Select(x => x * 3));
        var aliases = DeclaredAliases(SelectListOf(FirstDerivedSource(sql)));
        aliases.Should().ContainSingle("the ordered derived projection must declare the scalar output identifier exactly once; SQL: " + sql);

        var identifier = aliases[0];
        var orderBy = sql[(sql.IndexOf(" order by ", StringComparison.OrdinalIgnoreCase) + " order by ".Length)..].Trim();
        AssertContainsIdentifier(orderBy, identifier, "ORDER BY must reference the declared identifier; SQL: " + sql);
        AssertContainsIdentifier(TopSelectList(sql), identifier, "the outer projection must reference the declared identifier; SQL: " + sql);
        orderBy.Should().Contain(".", "ORDER BY must reference the derived source alias; SQL: " + sql);
    }

    [Fact]
    public void NestedDerived_ShouldDeclareEveryDerivedScalarLevel()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var middle = ctx.From(inner).Select(x => x * 3);

        var sql = SqlOf(ctx, ctx.From(middle).Select(y => y + 1));
        var levels = DerivedSelectLists(sql);
        levels.Should().HaveCountGreaterThanOrEqualTo(2, "the nested query must render two derived levels; SQL: " + sql);

        foreach (var level in levels)
        {
            var aliases = DeclaredAliases(level);
            aliases.Should().ContainSingle("every derived scalar level must declare its output identifier exactly once; level: " + level + "; SQL: " + sql);
        }

        var innermost = DeclaredAliases(levels[^1])[0];
        AssertContainsIdentifier(levels[0], innermost, "the outer derived level must reference the innermost identifier; SQL: " + sql);
    }

    [Fact]
    public void JoinedDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        // The derived source itself contains the join; its projection is an unaliased computed scalar
        // over the joined row, which the outer query references.
        var inner = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => p.Item1.Id * 2);

        var sql = SqlOf(ctx, ctx.From(inner).Select(x => x * 3));
        var source = SelectListOf(FirstDerivedSource(sql));
        var aliases = DeclaredAliases(source);
        aliases.Should().ContainSingle("the joined derived source must declare the scalar output identifier exactly once; SQL: " + sql);
        AssertContainsIdentifier(TopSelectList(sql), aliases[0], "the outer projection must reference the declared identifier; SQL: " + sql);
    }

    [Fact]
    public void CtasProjection_ShouldDeclareScalarColumnName()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var outer = ctx.From(inner).OrderBy(x => x).Select(x => x * 3);

        var sql = Normalize(outer.ToTableSql("t312_ctas"));
        var aliases = DeclaredAliases(SelectListOf(FirstDerivedSource(sql)));
        aliases.Should().ContainSingle("the CTAS source must expose the derived scalar output identifier exactly once; SQL: " + sql);

        var identifier = aliases[0];
        AssertContainsIdentifier(TopSelectList(sql), identifier, "the CTAS select list must reference the derived identifier; SQL: " + sql);
        DeclaredAliases(TopSelectList(sql)).Should().ContainSingle(
            "the CTAS body must declare the scalar column name exactly once; SQL: " + sql)
            .Which.Should().Be(identifier);
    }

    [Fact]
    public void NamedAnonymous_ShouldKeepExistingPassThroughIdentifiers()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => new { s.Id });

        var sql = SqlOf(ctx, ctx.From(inner).Select(d => d.Id * 2));
        var source = SelectListOf(FirstDerivedSource(sql));
        // A named member whose expression is the source column of the same name stays a bare
        // pass-through identifier (existing policy); the fix must not invent a c0 alias here.
        DeclaredAliases(source).Should().NotContain("c0", "a named pass-through keeps its physical identifier; SQL: " + sql);
        AssertContainsIdentifier(source, "id", "the derived source keeps the physical identifier; SQL: " + sql, ignoreCase: true);
        AssertContainsIdentifier(TopSelectList(sql), "id", "the outer projection must reference the named member; SQL: " + sql, ignoreCase: true);
    }

    [Fact]
    public void PhysicalColumn_ShouldKeepPhysicalSpellingAndDeclaredCase()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<IComplexEntity>().Select(c => c.String);

        var sql = SqlOf(ctx, ctx.From(inner).Select(s => s));
        var source = SelectListOf(FirstDerivedSource(sql));
        Decode(source).Should().Contain("somestring", "the physical column spelling must be preserved; SQL: " + sql);
        DeclaredAliases(source).Should().Contain("String", "the declared member case must be preserved; SQL: " + sql);
    }

    [Fact]
    public void FloatDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => (double)s.Id / 2);

        AssertDerivedScalarContract(ctx, ctx.From(inner).Select(x => x * 3), "float derived scalar");
    }

    [Fact]
    public void StringDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.String + "-x");

        AssertDerivedScalarContract(ctx, ctx.From(inner).Select(s => s + "!"), "string derived scalar");
    }

    [Fact]
    public void NullableDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => c.Int + 1);

        AssertDerivedScalarContract(ctx, ctx.From(inner).Select(x => x * 2), "nullable derived scalar");
    }

    [Fact]
    public void DefaultValueDerived_ShouldDeclareInnerScalarIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2)
            .Select(s => s.Id == 0 ? default(int) : s.Id * 2);

        AssertDerivedScalarContract(ctx, ctx.From(inner).Select(x => x * 3), "default-value derived scalar");
    }

    [Fact]
    public void CaseOnlyNames_ShouldKeepDistinctIdentifiers()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => new { Id = s.Id, id = s.Id });

        var sql = SqlOf(ctx, ctx.From(inner).Select(d => new { d.Id, d.id }));
        var source = SelectListOf(FirstDerivedSource(sql));
        // Both case-only members resolve to the same physical pass-through column today; the fix must
        // not add an alias or a generated name to either of them.
        DeclaredAliases(source).Should().BeEmpty("case-only pass-through members keep no alias; SQL: " + sql);
        var top = Decode(TopSelectList(sql));
        Regex.Matches(top, "(?<![A-Za-z0-9_])id(?![A-Za-z0-9_])", RegexOptions.IgnoreCase).Count
            .Should().Be(2, "both case-only references survive unchanged; SQL: " + sql);
    }

    [Fact]
    public void ExplicitC0Name_ShouldKeepUserAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => new { c0 = s.Id * 2 });

        var sql = SqlOf(ctx, ctx.From(inner).Select(d => d.c0 * 3));
        var aliases = DeclaredAliases(SelectListOf(FirstDerivedSource(sql)));
        aliases.Should().ContainSingle("the explicitly named c0 projection keeps exactly one declaration; SQL: " + sql)
            .Which.Should().Be("c0");
        AssertContainsIdentifier(TopSelectList(sql), "c0", "the outer projection must reference c0; SQL: " + sql);
    }

    [Fact]
    public void MulticolumnAnonymous_ShouldKeepEveryIdentifier()
    {
        using var ctx = SqliteTestContext.Create();
        var inner = ctx.From<IComplexEntity>().Where(c => c.Id <= 2).Select(c => new { c.Id, Doubled = c.Id * 2, c.String });

        var sql = SqlOf(ctx, ctx.From(inner).Select(d => new { d.Id, d.Doubled, d.String }));
        var source = SelectListOf(FirstDerivedSource(sql));
        var top = TopSelectList(sql);
        AssertContainsIdentifier(source, "id", "every anonymous member keeps its identifier; SQL: " + sql, ignoreCase: true);
        foreach (var identifier in new[] { "Doubled", "String" })
        {
            DeclaredAliases(source).Should().Contain(identifier, "every named member keeps its alias; SQL: " + sql);
            AssertContainsIdentifier(top, identifier, "the outer projection must resolve " + identifier + "; SQL: " + sql);
        }

        AssertContainsIdentifier(top, "id", "the outer projection must resolve the pass-through member; SQL: " + sql, ignoreCase: true);
    }

    [Fact]
    public void TypedCte_ShouldKeepExistingDeclarationNaming()
    {
        using var ctx = SqliteTestContext.Create();
        var cte = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2).AsCte("t312_nums");

        var sql = SqlOf(ctx, ctx.From(cte).Select(x => x * 3));
        var marker = sql.IndexOf(" t312_nums as (", StringComparison.OrdinalIgnoreCase);
        marker.Should().BeGreaterThanOrEqualTo(0, "the typed CTE declaration must be rendered; SQL: " + sql);
        var open = sql.IndexOf('(', marker);
        var depth = 0;
        var close = -1;
        for (var i = open; i < sql.Length; i++)
        {
            if (sql[i] == '(') depth++;
            else if (sql[i] == ')' && --depth == 0) { close = i; break; }
        } 
        close.Should().BeGreaterThan(open, "the typed CTE declaration must be balanced; SQL: " + sql);
        var declaration = SelectListOf(sql[(open + 1)..close]);
        var aliases = DeclaredAliases(declaration);
        aliases.Should().ContainSingle("the typed CTE declaration keeps its existing single alias; SQL: " + sql);

        var identifier = aliases[0];
        AssertContainsIdentifier(TopSelectList(sql), identifier, "the CTE consumer must reference the declared identifier; SQL: " + sql);
    }
}
