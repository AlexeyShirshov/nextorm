using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Expression-cache branch coverage (cycle 7, cluster D1) executed against a live SQLite database. Each
/// fact seeds the class-owned <c>ex7_item</c> table, builds TWO commands over one <see cref="IDataContext"/>
/// that differ in exactly one expression component, prepares both through the cached path, asserts the
/// live rows of each side, and asserts the expression/plan-key outcome through the public
/// <see cref="QueryCommand.GetExpressionPlanEqualityComparer"/> and
/// <see cref="QueryCommand.GetQueryPlanEqualityComparer"/> accessors.
///
/// <para>
/// Targeted arms of <c>src/nextorm.core/Query/ExpressionPlanEqualityComparer.cs</c>:
/// <c>ExpressionComparer.Compare2</c> (:311 type switch) and the <c>CompareBlock</c> (:367),
/// <c>CompareGoto</c> (:374), <c>CompareInvocation</c> (:390), <c>CompareIndex</c> (:380),
/// <c>CompareLabel</c> (:396), <c>CompareLoop</c> (:404), <c>CompareRuntimeVariables</c> (:446),
/// <c>CompareSwitch</c>/<c>CompareSwitchCaseList</c>/<c>CompareSwitchCase</c> (:452/:642/:657) and
/// <c>CompareTry</c>/<c>CompareCatchBlockList</c>/<c>CompareCatchBlock</c> (:460/:671/:685) families,
/// plus the residual <c>CompareLambda</c> parameter-scope spill, <c>CompareBinding</c> list/member-member
/// arms, <c>CompareMemberList</c>, <c>CompareElementInit(List)</c> and the matching
/// <c>Visitor.Visit*</c> hash arms.
/// </para>
///
/// <para>
/// Public-route observation for the switch/catch (and block/goto/label/loop/invocation/index/
/// runtime-variables) arms: no public lambda accepted by <c>EntityBuilder</c> produces those node kinds,
/// but <see cref="ExpressionPlanEqualityComparer"/> itself is public, its equality entry point takes any
/// <see cref="Expression"/>, and it is obtained through the public
/// <see cref="QueryCommand.GetExpressionPlanEqualityComparer"/> accessor. This class therefore drives
/// those arms by handing publicly constructed <see cref="Expression"/> trees to that public comparer and
/// additionally runs a live differential query pair in every fact, so no fact is translation-only.
/// </para>
[Collection("Query plan cache")]
public sealed class Cycle7ExpressionCacheTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists ex7_item;
        create table ex7_item
        (
            id integer primary key,
            name text not null,
            code text not null,
            grp int not null,
            tag int not null,
            score int null
        );
        insert into ex7_item (id, name, code, grp, tag, score) values
            (1, 'alpha',   'a', 1, 10, 5),
            (2, 'beta',    'b', 1, 20, 5),
            (3, 'gamma',   'a', 1, 10, 9),
            (4, 'delta',   'b', 2, 20, 1),
            (5, 'epsilon', 'c', 2, 10, 2),
            (6, 'zeta',    'c', 2, 20, null);
        """;

    public Cycle7ExpressionCacheTests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>A public comparer owned by a throwaway probe command over the seeded table.</summary>
    private ExpressionPlanEqualityComparer ExpressionComparer()
        => Context.From<Ex7Item>().Where(x => x.Id > 0).Select(x => x.Id).GetExpressionPlanEqualityComparer();

    /// <summary>
    /// Runs a one-component predicate differential live and asserts that the public cache path refuses to
    /// reuse the left plan while the expression comparer rejects the pair.
    /// </summary>
    private void AssertDifferent(
        Expression<Func<Ex7Item, bool>> left,
        Expression<Func<Ex7Item, bool>> right,
        long[] leftRows,
        long[] rightRows,
        string because)
    {
        ResetPlanCache();

        var leftCommand = Context.From<Ex7Item>().Where(left).OrderBy(x => x.Id).Select(x => x.Id);
        var rightCommand = Context.From<Ex7Item>().Where(right).OrderBy(x => x.Id).Select(x => x.Id);

        var leftPrepared = Prepare(leftCommand);
        Rows(leftPrepared).Should().Equal(leftRows, "the left predicate runs against the live ex7_item rows");
        Prepare(rightCommand).Should().NotBeSameAs(leftPrepared, because);
        Rows(Prepare(rightCommand)).Should().Equal(rightRows, "the right predicate runs against the live ex7_item rows");

        leftCommand.GetExpressionPlanEqualityComparer().Equals(left, right).Should().BeFalse(because);
        leftCommand.GetQueryPlanEqualityComparer().Equals(leftCommand, rightCommand).Should().BeFalse(because);
    }

    /// <summary>
    /// Control pair: two structurally identical commands must compare equal, share one cached plan and
    /// read the same rows, so the differential facts cannot pass on a comparer that always says false.
    /// </summary>
    private void AssertSame(
        Expression<Func<Ex7Item, bool>> left,
        Expression<Func<Ex7Item, bool>> right,
        long[] rows,
        string because)
    {
        ResetPlanCache();

        var leftCommand = Context.From<Ex7Item>().Where(left).OrderBy(x => x.Id).Select(x => x.Id);
        var rightCommand = Context.From<Ex7Item>().Where(right).OrderBy(x => x.Id).Select(x => x.Id);

        var prepared = Prepare(leftCommand);
        Rows(prepared).Should().Equal(rows, "the control predicate runs against the live ex7_item rows");
        Prepare(rightCommand).Should().BeSameAs(prepared, because);

        leftCommand.GetExpressionPlanEqualityComparer().Equals(left, right).Should().BeTrue(because);
        leftCommand.GetQueryPlanEqualityComparer().Equals(leftCommand, rightCommand).Should().BeTrue(because);
    }

    /// <summary>Asserts equality and equal hashes for two publicly constructed expression trees.</summary>
    private void AssertNodeEqual(Expression left, Expression right, string because)
    {
        var comparer = ExpressionComparer();
        comparer.Equals(left, right).Should().BeTrue(because);
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right), "equal expression trees hash equally");
    }

    /// <summary>Asserts that two publicly constructed expression trees are rejected by the comparer.</summary>
    private void AssertNodeDifferent(Expression left, Expression right, string because)
    {
        ExpressionComparer().Equals(left, right).Should().BeFalse(because);
    }

    private static MethodInfo Method(Type type, string name, params Type[] parameterTypes)
        => type.GetMethod(name, parameterTypes) ?? throw new InvalidOperationException($"Missing method {type.Name}.{name}");

    // ---------------------------------------------------------------------------------------------
    // 1. Method-call argument value
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void MethodCall_ArgumentValue_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> alpha = x => x.Name.StartsWith("a");
        Expression<Func<Ex7Item, bool>> zulu = x => x.Name.StartsWith("z");
        Expression<Func<Ex7Item, bool>> alphaAgain = x => x.Name.StartsWith("a");

        AssertDifferent(alpha, zulu, [1], [6], "a changed method argument is a different expression (CompareMethodCall/CompareExpressionList)");
        AssertSame(alpha, alphaAgain, [1], "an identical method call reuses one cached plan");

        var p = Expression.Parameter(typeof(string), "s");
        var starts = Method(typeof(string), nameof(string.StartsWith), typeof(string));
        AssertNodeEqual(
            Expression.Call(p, starts, Expression.Constant("a")),
            Expression.Call(Expression.Parameter(typeof(string), "s2"), starts, Expression.Constant("a")),
            "identical call argument nodes compare equal");
        AssertNodeDifferent(
            Expression.Call(p, starts, Expression.Constant("a")),
            Expression.Call(p, starts, Expression.Constant("b")),
            "CompareMethodCall compares the argument list element by element");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Method-call method identity and receiver
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void MethodCall_DifferentMethodAndReceiver_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> starts = x => x.Name.StartsWith("a");
        Expression<Func<Ex7Item, bool>> ends = x => x.Name.EndsWith("a");

        AssertDifferent(starts, ends, [1], [1, 2, 3, 4, 6], "a different method is a different expression (CompareMethodCall method arm)");

        var comparer = ExpressionComparer();
        var p = Expression.Parameter(typeof(string), "s");
        var starts0 = Expression.Call(p, Method(typeof(string), nameof(string.StartsWith), typeof(string)), Expression.Constant("a"));
        var ends0 = Expression.Call(p, Method(typeof(string), nameof(string.EndsWith), typeof(string)), Expression.Constant("a"));
        comparer.Equals(starts0, ends0).Should().BeFalse("Equals(a.Method, b.Method) rejects a different method");

        comparer.Equals(
            Expression.Call(p, Method(typeof(string), nameof(string.Contains), typeof(string)), Expression.Constant("a")),
            Expression.Call(Expression.Parameter(typeof(string), "s2"), Method(typeof(string), nameof(string.Contains), typeof(string)), Expression.Constant("a")))
            .Should().BeTrue("parameter receivers of the same type compare equal (CompareParameter)");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Binary operand swap
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Binary_OperandSwap_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> memberLeft = x => x.Grp == 1;
        Expression<Func<Ex7Item, bool>> constantLeft = x => 1 == x.Grp;

        AssertDifferent(memberLeft, constantLeft, [1, 2, 3], [1, 2, 3],
            "swapping the binary operands changes the compared left/right expressions even when the rows match");

        var p = Expression.Parameter(typeof(int), "v");
        AssertNodeEqual(
            Expression.Equal(p, Expression.Constant(1)),
            Expression.Equal(Expression.Parameter(typeof(int), "v2"), Expression.Constant(1)),
            "identical binary nodes compare equal");
        AssertNodeDifferent(
            Expression.Equal(p, Expression.Constant(1)),
            Expression.NotEqual(p, Expression.Constant(1)),
            "a different binary node type is rejected before Compare2");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Nested binary operand
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Binary_NestedOperand_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> ten = x => x.Grp == 1 && x.Tag == 10;
        Expression<Func<Ex7Item, bool>> twenty = x => x.Grp == 1 && x.Tag == 20;

        AssertDifferent(ten, twenty, [1, 3], [2], "a changed right-hand binary operand is a different expression (CompareBinary)");

        var p = Expression.Parameter(typeof(int), "v");
        var q = Expression.Parameter(typeof(int), "w");
        AssertNodeEqual(
            Expression.AndAlso(Expression.Equal(p, Expression.Constant(1)), Expression.Equal(q, Expression.Constant(10))),
            Expression.AndAlso(Expression.Equal(Expression.Parameter(typeof(int), "v2"), Expression.Constant(1)), Expression.Equal(Expression.Parameter(typeof(int), "w2"), Expression.Constant(10))),
            "two structurally identical AndAlso trees (both operands) compare equal");
        AssertNodeDifferent(
            Expression.AndAlso(Expression.Equal(p, Expression.Constant(1)), Expression.Equal(q, Expression.Constant(10))),
            Expression.AndAlso(Expression.Equal(p, Expression.Constant(1)), Expression.Equal(q, Expression.Constant(11))),
            "the right operand participates in CompareBinary");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Unary NOT
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Unary_Not_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> notEqual = x => !(x.Grp == 1);
        Expression<Func<Ex7Item, bool>> notEqualAgain = x => !(x.Grp == 1);
        Expression<Func<Ex7Item, bool>> notTwo = x => !(x.Grp == 2);

        AssertSame(notEqual, notEqualAgain, [4, 5, 6], "identical NOT nodes reuse one cached plan");
        AssertDifferent(notEqual, notTwo, [4, 5, 6], [1, 2, 3], "a changed unary operand is a different expression (CompareUnary)");

        var p = Expression.Parameter(typeof(int), "v");
        AssertNodeEqual(
            Expression.Not(Expression.Equal(p, Expression.Constant(1))),
            Expression.Not(Expression.Equal(Expression.Parameter(typeof(int), "v2"), Expression.Constant(1))),
            "identical unary NOT nodes compare equal");
        AssertNodeDifferent(
            Expression.Not(Expression.Equal(p, Expression.Constant(1))),
            Expression.Not(Expression.Equal(p, Expression.Constant(2))),
            "CompareUnary compares the operand");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Unary convert
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Unary_Convert_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> aboveOne = x => (double)x.Grp > 1.0;
        Expression<Func<Ex7Item, bool>> aboveTwo = x => (double)x.Grp > 2.0;

        AssertDifferent(aboveOne, aboveTwo, [4, 5, 6], [], "a changed convert operand bound is a different expression (CompareUnary/CompareConstant)");

        var p = Expression.Parameter(typeof(int), "v");
        AssertNodeEqual(
            Expression.Convert(p, typeof(double)),
            Expression.Convert(Expression.Parameter(typeof(int), "v2"), typeof(double)),
            "identical convert nodes compare equal");
        AssertNodeDifferent(
            Expression.Convert(p, typeof(double)),
            Expression.Convert(Expression.Constant(1), typeof(double)),
            "CompareUnary compares the operand expression");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Conditional branch
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Conditional_Branch_ShouldKeySeparately()
    {
        ResetPlanCache();

        Expression<Func<Ex7Item, int>> zeroSelector = x => x.Score == null ? 0 : x.Score.Value;
        Expression<Func<Ex7Item, int>> oneSelector = x => x.Score == null ? 1 : x.Score.Value;
        var zero = Context.From<Ex7Item>().OrderBy(x => x.Id).Select(zeroSelector);
        var one = Context.From<Ex7Item>().OrderBy(x => x.Id).Select(oneSelector);

        var zeroPrepared = Prepare(zero);
        Rows(zeroPrepared).Should().Equal(new[] { 5, 5, 9, 1, 2, 0 }, "the null score falls into the zero branch of the live CASE");
        Prepare(one).Should().NotBeSameAs(zeroPrepared, "a changed conditional branch is a different plan key");
        Rows(Prepare(one)).Should().Equal(new[] { 5, 5, 9, 1, 2, 1 }, "the null score falls into the one branch of the live CASE");

        zero.GetExpressionPlanEqualityComparer().Equals(zeroSelector, oneSelector).Should()
            .BeFalse("CompareConditional compares the IfFalse branch");

        var p = Expression.Parameter(typeof(int), "v");
        var test = Expression.Equal(p, Expression.Constant(1));
        AssertNodeEqual(
            Expression.Condition(test, Expression.Constant(10), Expression.Constant(20)),
            Expression.Condition(Expression.Equal(Expression.Parameter(typeof(int), "v2"), Expression.Constant(1)), Expression.Constant(10), Expression.Constant(20)),
            "identical conditional nodes compare equal");
        AssertNodeDifferent(
            Expression.Condition(test, Expression.Constant(10), Expression.Constant(20)),
            Expression.Condition(test, Expression.Constant(10), Expression.Constant(30)),
            "CompareConditional compares the IfFalse branch");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Member chains
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void MemberChain_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> nameLength = x => x.Name.Length > 4;
        Expression<Func<Ex7Item, bool>> codeLength = x => x.Code.Length > 0;

        AssertDifferent(nameLength, codeLength, [1, 3, 4, 5], [1, 2, 3, 4, 5, 6],
            "a different member in the chain is a different expression (CompareMember non-constant receiver)");

        var x = Expression.Parameter(typeof(Ex7Item), "x");
        var nameChain = Expression.Property(Expression.Property(x, nameof(Ex7Item.Name)), nameof(string.Length));
        var codeChain = Expression.Property(Expression.Property(x, nameof(Ex7Item.Code)), nameof(string.Length));

        AssertNodeEqual(
            nameChain,
            Expression.Property(Expression.Property(Expression.Parameter(typeof(Ex7Item), "x2"), nameof(Ex7Item.Name)), nameof(string.Length)),
            "identical member chains compare equal");
        AssertNodeDifferent(nameChain, codeChain, "CompareMember compares the member identity on a non-constant receiver");

        var pairA = Expression.Property(Expression.Constant(new Ex7Pair()), nameof(Ex7Pair.A));
        var pairB = Expression.Property(Expression.Constant(new Ex7Pair()), nameof(Ex7Pair.B));
        AssertNodeDifferent(pairA, pairB, "CompareMember compares the member name on the constant-receiver fast path");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Constants: null versus value, changed value
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Constant_NullAndChangedValue_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> isNull = x => x.Score == null;
        Expression<Func<Ex7Item, bool>> isFive = x => x.Score == 5;

        AssertDifferent(isNull, isFive, [6], [1, 2], "a null constant and a value constant are different expressions (CompareConstant)");

        var comparer = ExpressionComparer();
        comparer.Equals(Expression.Constant(null, typeof(string)), Expression.Constant(null, typeof(string))).Should()
            .BeTrue("two null constants of the same type are equal");
        comparer.Equals(Expression.Constant("a"), Expression.Constant("a")).Should().BeTrue("equal constant values are equal");
        comparer.Equals(Expression.Constant("a"), Expression.Constant("b")).Should().BeFalse("CompareConstant compares the value");
        comparer.Equals(Expression.Constant(1), Expression.Constant(1L)).Should()
            .BeFalse("a different constant type is rejected by the Equals type guard");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Lambda parameter identity
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void LambdaParameterIdentity_ShouldCompareByIdentityScope()
    {
        Expression<Func<Ex7Item, bool>> alpha = x => x.Name.StartsWith("a");
        Expression<Func<Ex7Item, bool>> zulu = x => x.Name.StartsWith("z");
        Expression<Func<Ex7Item, bool>> alphaAgain = x => x.Name.StartsWith("a");

        AssertSame(alpha, alphaAgain, [1], "a structurally identical lambda still reuses one cached plan");
        AssertDifferent(alpha, zulu, [1], [6], "a changed literal argument is a different lambda body");

        var v = Expression.Parameter(typeof(long), "v");
        var w = Expression.Parameter(typeof(long), "w");
        AssertNodeEqual(
            Expression.Lambda<Func<long, bool>>(Expression.Equal(v, Expression.Constant(1L)), v),
            Expression.Lambda<Func<long, bool>>(Expression.Equal(w, Expression.Constant(1L)), w),
            "CompareLambda maps each parameter by position (CompareParameter)");
        AssertNodeDifferent(
            Expression.Lambda<Func<long, bool>>(Expression.Equal(v, Expression.Constant(1L)), v),
            Expression.Lambda<Func<long, bool>>(Expression.Equal(v, Expression.Constant(2L)), v),
            "CompareLambda compares the lambda body");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Array lengths 0/1/2
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NewArray_Lengths_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> two = x => new[] { 1L, 2L }.Contains(x.Id);
        Expression<Func<Ex7Item, bool>> one = x => new[] { 1L }.Contains(x.Id);

        AssertDifferent(two, one, [1, 2], [1], "a different array literal length is a different expression (CompareNewArray/CompareExpressionList count)");

        var empty = Expression.NewArrayInit(typeof(long));
        var single = Expression.NewArrayInit(typeof(long), Expression.Constant(1L));
        var pair = Expression.NewArrayInit(typeof(long), Expression.Constant(1L), Expression.Constant(2L));

        AssertNodeDifferent(empty, single, "a zero-element array and a one-element array differ at the list count");
        AssertNodeDifferent(single, pair, "a one-element array and a two-element array differ at the list count");
        AssertNodeEqual(
            pair,
            Expression.NewArrayInit(typeof(long), Expression.Constant(1L), Expression.Constant(2L)),
            "two identically sized array literals compare element by element");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. Array first / last element difference
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void NewArray_FirstAndLastElement_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> baseArray = x => new[] { 1L, 2L, 3L }.Contains(x.Id);
        Expression<Func<Ex7Item, bool>> firstChanged = x => new[] { 9L, 2L, 3L }.Contains(x.Id);
        Expression<Func<Ex7Item, bool>> lastChanged = x => new[] { 1L, 2L, 9L }.Contains(x.Id);

        AssertDifferent(baseArray, firstChanged, [1, 2, 3], [2, 3], "a difference in the first array element is detected");
        AssertDifferent(baseArray, lastChanged, [1, 2, 3], [1, 2], "a difference in the last array element is detected");

        Expression Array(params long[] values)
            => Expression.NewArrayInit(typeof(long), values.Select(v => (Expression)Expression.Constant(v)));

        AssertNodeEqual(Array(1, 2, 3), Array(1, 2, 3), "identical three-element arrays compare equal");
        AssertNodeDifferent(Array(1, 2, 3), Array(9, 2, 3), "the first element is compared");
        AssertNodeDifferent(Array(1, 2, 3), Array(1, 2, 9), "the last element is compared");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. List initializer / element init
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ListInit_ElementInit_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> pair = x => new List<long> { 1L, 2L }.Contains(x.Id);
        Expression<Func<Ex7Item, bool>> otherPair = x => new List<long> { 1L, 3L }.Contains(x.Id);

        AssertDifferent(pair, otherPair, [1, 2], [1, 3], "a changed list initializer element is a different expression (CompareListInit)");

        var add = Method(typeof(Ex7MultiAdd), nameof(Ex7MultiAdd.Add), typeof(int));
        var otherAdd = Method(typeof(Ex7MultiAdd), nameof(Ex7MultiAdd.Add), typeof(string));

        Expression ListWith(params ElementInit[] initializers)
            => Expression.ListInit(Expression.New(typeof(Ex7MultiAdd)), initializers);

        AssertNodeEqual(
            ListWith(Expression.ElementInit(add, Expression.Constant(1))),
            ListWith(Expression.ElementInit(add, Expression.Constant(1))),
            "identical list initializers compare equal");
        AssertNodeDifferent(
            ListWith(Expression.ElementInit(add, Expression.Constant(1))),
            ListWith(Expression.ElementInit(add, Expression.Constant(2))),
            "CompareElementInit compares the initializer arguments");
        AssertNodeDifferent(
            ListWith(Expression.ElementInit(add, Expression.Constant(1))),
            ListWith(Expression.ElementInit(otherAdd, Expression.Constant("x"))),
            "CompareElementInit compares the Add method identity");
        AssertNodeDifferent(
            ListWith(Expression.ElementInit(add, Expression.Constant(1))),
            ListWith(Expression.ElementInit(add, Expression.Constant(1)), Expression.ElementInit(add, Expression.Constant(2))),
            "CompareElementInitList rejects a different initializer count");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. Switch case list: count and ordering
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void SwitchCaseList_CountAndOrdering_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> tagTen = x => x.Tag == 10;
        Expression<Func<Ex7Item, bool>> tagTwenty = x => x.Tag == 20;

        AssertDifferent(tagTen, tagTwenty, [1, 3, 5], [2, 4, 6], "a changed switch-case-side equality operand is a different expression");

        var selector = Expression.Parameter(typeof(int), "sel");
        SwitchCase Case(int body, int test) => Expression.SwitchCase(Expression.Constant(body), Expression.Constant(test));

        var twoCases = Expression.Switch(selector, Expression.Constant(0), Case(10, 1), Case(20, 2));
        var twoCasesAgain = Expression.Switch(
            Expression.Parameter(typeof(int), "sel2"), Expression.Constant(0), Case(10, 1), Case(20, 2));
        var oneCase = Expression.Switch(selector, Expression.Constant(0), Case(10, 1));
        var reversed = Expression.Switch(selector, Expression.Constant(0), Case(20, 2), Case(10, 1));

        AssertNodeEqual(twoCases, twoCasesAgain, "identical switch expressions compare equal, including both cases");
        AssertNodeDifferent(twoCases, oneCase, "CompareSwitchCaseList rejects a different case count");
        AssertNodeDifferent(twoCases, reversed, "CompareSwitchCaseList compares cases positionally (ordering matters)");
    }

    // ---------------------------------------------------------------------------------------------
    // 15. Switch case body/test values and default body
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void SwitchCase_BodyTestAndDefault_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> nullScore = x => x.Score == null;
        Expression<Func<Ex7Item, bool>> group = x => x.Grp == 2;

        AssertDifferent(nullScore, group, [6], [4, 5, 6], "a different predicate operand is a different expression");

        var selector = Expression.Parameter(typeof(int), "sel");
        SwitchCase Case(int body, int test) => Expression.SwitchCase(Expression.Constant(body), Expression.Constant(test));

        var baseSwitch = Expression.Switch(selector, Expression.Constant(0), Case(10, 1));
        AssertNodeDifferent(
            baseSwitch,
            Expression.Switch(selector, Expression.Constant(0), Expression.SwitchCase(Expression.Constant(11), Expression.Constant(1))),
            "CompareSwitchCase compares the case body");
        AssertNodeDifferent(
            baseSwitch,
            Expression.Switch(selector, Expression.Constant(0), Expression.SwitchCase(Expression.Constant(10), Expression.Constant(2))),
            "CompareSwitchCase compares the test-value list");
        var voidCase = Expression.SwitchCase(Expression.Empty(), Expression.Constant(1));
        AssertNodeDifferent(
            Expression.Switch(selector, default(Expression), voidCase),
            Expression.Switch(selector, Expression.Empty(), voidCase),
            "CompareSwitch compares a null default body against a non-null one");
        AssertNodeDifferent(
            baseSwitch,
            Expression.Switch(selector, Expression.Constant(5), Case(10, 1)),
            "CompareSwitch compares two non-null default bodies");
    }

    // ---------------------------------------------------------------------------------------------
    // 16. Try/catch handler list, test type and body
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void TryCatch_HandlerListAndTest_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> aboveThree = x => x.Id > 3;
        Expression<Func<Ex7Item, bool>> aboveFour = x => x.Id > 4;

        AssertDifferent(aboveThree, aboveFour, [4, 5, 6], [5, 6], "a changed comparison bound is a different expression");

        var body = Expression.Constant(1);
        var first = Expression.Catch(typeof(InvalidOperationException), Expression.Constant(2));
        var second = Expression.Catch(typeof(ArgumentException), Expression.Constant(3));
        var tryA = Expression.TryCatch(body, first);
        var tryAgain = Expression.TryCatch(Expression.Constant(1), Expression.Catch(typeof(InvalidOperationException), Expression.Constant(2)));
        var twoHandlers = Expression.TryCatch(body, first, second);

        AssertNodeEqual(tryA, tryAgain, "identical try/catch nodes compare equal");
        AssertNodeDifferent(tryA, twoHandlers, "CompareCatchBlockList rejects a different handler count");
        AssertNodeDifferent(
            tryA,
            Expression.TryCatch(body, Expression.Catch(typeof(ArgumentException), Expression.Constant(2))),
            "CompareCatchBlock compares the catch test type");
        AssertNodeDifferent(
            tryA,
            Expression.TryCatch(body, Expression.Catch(typeof(InvalidOperationException), Expression.Constant(9))),
            "CompareCatchBlock compares the catch body");
    }

    // ---------------------------------------------------------------------------------------------
    // 17. Catch filter and catch variable
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void TryCatch_FilterAndVariable_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> alpha = x => x.Name == "alpha";
        Expression<Func<Ex7Item, bool>> beta = x => x.Name == "beta";

        AssertDifferent(alpha, beta, [1], [2], "a changed constant comparison operand is a different expression");

        var body = Expression.Constant(1);
        var trueFilter = Expression.Catch(typeof(Exception), body, Expression.Constant(true));
        var falseFilter = Expression.Catch(typeof(Exception), body, Expression.Constant(false));

        AssertNodeDifferent(
            Expression.TryCatch(body, trueFilter),
            Expression.TryCatch(body, falseFilter),
            "CompareCatchBlock compares the handler filter");

        var variableA = Expression.Parameter(typeof(Exception), "exA");
        var variableB = Expression.Parameter(typeof(Exception), "exB");
        AssertNodeEqual(
            Expression.TryCatch(body, Expression.Catch(variableA, Expression.Constant(2))),
            Expression.TryCatch(Expression.Constant(1), Expression.Catch(variableB, Expression.Constant(2))),
            "catch variables of the same type compare by type through CompareParameter");

        AssertNodeDifferent(
            Expression.TryCatch(body, Expression.Catch(variableA, Expression.Constant(2))),
            Expression.TryCatch(body, Expression.Catch(Expression.Parameter(typeof(InvalidOperationException), "exC"), Expression.Constant(2))),
            "a catch variable of a different type is rejected by the handler test/value comparison");
    }

    // ---------------------------------------------------------------------------------------------
    // 18. Try fault / finally optional children
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void TryFaultFinally_OptionalChildren_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> one = x => x.Grp == 1;
        Expression<Func<Ex7Item, bool>> two = x => x.Grp == 2;

        AssertDifferent(one, two, [1, 2, 3], [4, 5, 6], "a changed group constant is a different expression");

        var body = Expression.Constant(1);
        var tryCatch = Expression.TryCatch(body, Expression.Catch(typeof(Exception), Expression.Constant(2)));
        var tryFault = Expression.TryFault(body, Expression.Empty());
        var tryFinally = Expression.TryFinally(body, Expression.Empty());

        AssertNodeDifferent(tryCatch, tryFault, "CompareTry compares the optional Fault child (null versus non-null)");
        AssertNodeDifferent(tryCatch, tryFinally, "CompareTry compares the optional Finally child (null versus non-null)");
        AssertNodeDifferent(
            tryFault,
            Expression.TryFault(body, Expression.Block()),
            "CompareTry compares two non-null fault bodies");
        AssertNodeEqual(
            tryFinally,
            Expression.TryFinally(Expression.Constant(1), Expression.Empty()),
            "identical try/finally nodes compare equal");
    }

    // ---------------------------------------------------------------------------------------------
    // 19. Block variables and expressions
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Block_VariablesAndExpressions_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> atLeastTwo = x => x.Id >= 2;
        Expression<Func<Ex7Item, bool>> atLeastThree = x => x.Id >= 3;

        AssertDifferent(atLeastTwo, atLeastThree, [2, 3, 4, 5, 6], [3, 4, 5, 6], "a changed comparison bound is a different expression");

        var single = Expression.Block(typeof(int), Expression.Constant(1));
        var singleAgain = Expression.Block(typeof(int), Expression.Constant(1));
        var doubleBody = Expression.Block(typeof(int), Expression.Constant(1), Expression.Constant(2));
        var withVariable = Expression.Block(typeof(int), new[] { Expression.Parameter(typeof(int), "b") }, Expression.Constant(1));
        var withLongVariable = Expression.Block(typeof(int), new[] { Expression.Parameter(typeof(long), "bl") }, Expression.Constant(1));

        AssertNodeEqual(single, singleAgain, "identical block expressions compare equal");
        AssertNodeDifferent(single, doubleBody, "CompareBlock/CompareExpressionList rejects a different expression count");
        AssertNodeDifferent(single, withVariable, "CompareBlock compares the variables list (absent versus present)");
        AssertNodeDifferent(withVariable, withLongVariable, "a block variable of a different type is rejected by the type guard");
    }

    // ---------------------------------------------------------------------------------------------
    // 20. Goto kind, target and value
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Goto_KindTargetAndValue_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> belowThree = x => x.Id < 3;
        Expression<Func<Ex7Item, bool>> belowFour = x => x.Id < 4;

        AssertDifferent(belowThree, belowFour, [1, 2], [1, 2, 3], "a changed comparison bound is a different expression");

        var target = Expression.Label(typeof(int), "g");
        var otherTarget = Expression.Label(typeof(int), "g2");
        var gotoA = Expression.MakeGoto(GotoExpressionKind.Goto, target, Expression.Constant(5), typeof(int));
        var gotoAgain = Expression.MakeGoto(GotoExpressionKind.Goto, target, Expression.Constant(5), typeof(int));

        AssertNodeEqual(gotoA, gotoAgain, "identical goto nodes compare equal");
        AssertNodeDifferent(
            gotoA,
            Expression.MakeGoto(GotoExpressionKind.Return, target, Expression.Constant(5), typeof(int)),
            "CompareGoto compares the kind");
        AssertNodeDifferent(
            gotoA,
            Expression.MakeGoto(GotoExpressionKind.Goto, target, Expression.Constant(6), typeof(int)),
            "CompareGoto compares the value");
        AssertNodeDifferent(
            gotoA,
            Expression.MakeGoto(GotoExpressionKind.Goto, otherTarget, Expression.Constant(5), typeof(int)),
            "CompareGoto compares the label target identity");
    }

    // ---------------------------------------------------------------------------------------------
    // 21. Label target and default value
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Label_TargetAndDefaultValue_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> notAlpha = x => x.Name != "alpha";
        Expression<Func<Ex7Item, bool>> notBeta = x => x.Name != "beta";

        AssertDifferent(notAlpha, notBeta, [2, 3, 4, 5, 6], [1, 3, 4, 5, 6], "a changed constant operand is a different expression");

        var target = Expression.Label(typeof(int), "l");
        var otherTarget = Expression.Label(typeof(int), "l2");
        var voidTarget = Expression.Label("lv");

        AssertNodeEqual(
            Expression.Label(target, Expression.Constant(5)),
            Expression.Label(target, Expression.Constant(5)),
            "identical labels compare equal");
        AssertNodeDifferent(
            Expression.Label(target, Expression.Constant(5)),
            Expression.Label(target, Expression.Constant(6)),
            "CompareLabel compares the default value");
        AssertNodeDifferent(Expression.Label(voidTarget), Expression.Label(voidTarget, Expression.Empty()),
            "CompareLabel compares an absent default value against a present one");
        AssertNodeDifferent(
            Expression.Label(target, Expression.Constant(5)),
            Expression.Label(otherTarget, Expression.Constant(5)),
            "CompareLabel compares the label target identity");
    }

    // ---------------------------------------------------------------------------------------------
    // 22. Loop body and labels
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Loop_BodyAndLabels_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> anyGroup = x => x.Grp >= 1;
        Expression<Func<Ex7Item, bool>> secondGroup = x => x.Grp >= 2;

        AssertDifferent(anyGroup, secondGroup, [1, 2, 3, 4, 5, 6], [4, 5, 6], "a changed comparison bound is a different expression");

        var breakLabel = Expression.Label("brk");
        var continueLabel = Expression.Label("cont");
        var loopA = Expression.Loop(Expression.Empty(), breakLabel, continueLabel);
        var loopAgain = Expression.Loop(Expression.Empty(), breakLabel, continueLabel);

        AssertNodeEqual(loopA, loopAgain, "identical loop nodes compare equal");
        AssertNodeDifferent(
            loopA,
            Expression.Loop(Expression.Block(), breakLabel, continueLabel),
            "CompareLoop compares the body");
        AssertNodeDifferent(
            loopA,
            Expression.Loop(Expression.Empty(), Expression.Label("brk2"), continueLabel),
            "CompareLoop compares the break label identity");
        AssertNodeDifferent(
            loopA,
            Expression.Loop(Expression.Empty()),
            "CompareLoop compares an absent break label against a present one");
    }

    // ---------------------------------------------------------------------------------------------
    // 23. Invocation expression and arguments
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Invocation_ExpressionAndArguments_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> first = x => x.Id == 1;
        Expression<Func<Ex7Item, bool>> second = x => x.Id == 2;

        AssertDifferent(first, second, [1], [2], "a changed constant operand is a different expression");

        var one = Expression.Lambda<Func<int>>(Expression.Constant(1));
        var zeroArgA = Expression.Invoke(one);
        var zeroArgAgain = Expression.Invoke(Expression.Lambda<Func<int>>(Expression.Constant(1)));
        var parameter = Expression.Parameter(typeof(int), "iv");
        var adder = Expression.Lambda<Func<int, int>>(Expression.Add(parameter, Expression.Constant(1)), parameter);

        AssertNodeEqual(zeroArgA, zeroArgAgain, "CompareInvocation compares the invoked lambda through CompareLambda");
        AssertNodeDifferent(
            zeroArgA,
            Expression.Invoke(Expression.Lambda<Func<int>>(Expression.Constant(2))),
            "CompareInvocation compares the invoked expression body");
        AssertNodeDifferent(
            Expression.Invoke(adder, Expression.Constant(2)),
            Expression.Invoke(adder, Expression.Constant(3)),
            "CompareInvocation compares the argument list");
    }

    // ---------------------------------------------------------------------------------------------
    // 24. Index: indexer, object and arguments
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void Index_ObjectArgumentsAndIndexer_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> firstChar = x => x.Name.Substring(0, 1) == "a";
        Expression<Func<Ex7Item, bool>> firstCodeChar = x => x.Code.Substring(0, 1) == "a";

        AssertDifferent(firstChar, firstCodeChar, [1], [1, 3], "a changed member receiver is a different expression (CompareMember)");

        var list = new List<int> { 10, 20 };
        var otherList = new List<int> { 10, 20 };
        var indexer = typeof(List<int>).GetProperty("Item")!;
        var dictionaryIndexer = typeof(Dictionary<int, int>).GetProperty("Item")!;
        var listConstant = Expression.Constant(list);

        Expression Index(Expression instance, PropertyInfo property, int index)
            => Expression.MakeIndex(instance, property, new[] { Expression.Constant(index) });

        AssertNodeEqual(Index(listConstant, indexer, 0), Index(listConstant, indexer, 0), "identical index nodes compare equal");
        AssertNodeDifferent(Index(listConstant, indexer, 0), Index(listConstant, indexer, 1),
            "CompareIndex compares the argument list");
        AssertNodeDifferent(Index(listConstant, indexer, 0), Index(Expression.Constant(otherList), indexer, 0),
            "CompareIndex compares the object expression");
        AssertNodeDifferent(Index(listConstant, indexer, 0), Index(Expression.Constant(new Dictionary<int, int> { [1] = 1 }), dictionaryIndexer, 1),
            "CompareIndex compares the indexer identity");
    }

    // ---------------------------------------------------------------------------------------------
    // 25. Runtime variables
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void RuntimeVariables_ListLengthAndType_ShouldKeySeparately()
    {
        Expression<Func<Ex7Item, bool>> containsA = x => x.Name.Contains("a");
        Expression<Func<Ex7Item, bool>> codeContainsA = x => x.Code.Contains("a");

        AssertDifferent(containsA, codeContainsA, [1, 2, 3, 4, 6], [1, 3], "a changed member receiver is a different expression");

        var first = Expression.Parameter(typeof(int), "rv1");
        var second = Expression.Parameter(typeof(int), "rv2");
        var variablesA = Expression.RuntimeVariables(first);
        var variablesAgain = Expression.RuntimeVariables(second);
        var variablesPair = Expression.RuntimeVariables(first, second);
        var variablesLong = Expression.RuntimeVariables(Expression.Parameter(typeof(long), "rv3"));

        AssertNodeEqual(variablesA, variablesAgain, "CompareRuntimeVariables compares the variable list by parameter type");
        AssertNodeDifferent(variablesA, variablesPair, "CompareRuntimeVariables rejects a different variable count");
        AssertNodeDifferent(variablesA, variablesLong, "CompareRuntimeVariables rejects a different variable type");
    }

    // ---------------------------------------------------------------------------------------------
    // 26. Extension node
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void ExtensionNode_ShouldCompareByNodeEquality()
    {
        Expression<Func<Ex7Item, bool>> aboveTwo = x => x.Score > 2;
        Expression<Func<Ex7Item, bool>> aboveThree = x => x.Score > 5;

        AssertDifferent(aboveTwo, aboveThree, [1, 2, 3], [3], "a changed comparison bound is a different expression");

        var comparer = ExpressionComparer();
        comparer.Equals(new Ex7Extension(1), new Ex7Extension(1)).Should()
            .BeTrue("Compare2 delegates an Extension node to the node's own Equals");
        comparer.Equals(new Ex7Extension(1), new Ex7Extension(2)).Should()
            .BeFalse("the Extension arm forwards the node equality result");
    }

    // ---------------------------------------------------------------------------------------------
    // 27. Member lists, member bindings and the New member list
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void MemberListsAndBindings_ShouldKeySeparately()
    {
        ResetPlanCache();

        Expression<Func<Ex7Item, Ex7Dto>> namesSelector = x => new Ex7Dto { Id = x.Id, Text = x.Name };
        Expression<Func<Ex7Item, Ex7Dto>> codesSelector = x => new Ex7Dto { Id = x.Id, Text = x.Code };
        var names = Context.From<Ex7Item>().OrderBy(x => x.Id).Select(namesSelector);
        var codes = Context.From<Ex7Item>().OrderBy(x => x.Id).Select(codesSelector);

        var namesPrepared = Prepare(names);
        Rows(namesPrepared).Select(r => (r.Id, r.Text)).Should()
            .Equal((1L, "alpha"), (2L, "beta"), (3L, "gamma"), (4L, "delta"), (5L, "epsilon"), (6L, "zeta"));
        Prepare(codes).Should().NotBeSameAs(namesPrepared, "a changed member-init assignment expression is a different plan key");
        Rows(Prepare(codes)).Select(r => (r.Id, r.Text)).Should()
            .Equal((1L, "a"), (2L, "b"), (3L, "a"), (4L, "b"), (5L, "c"), (6L, "c"));

        names.GetExpressionPlanEqualityComparer().Equals(namesSelector, codesSelector).Should()
            .BeFalse("CompareBinding compares the assignment expression");

        // NewExpression.Members: the member list of an anonymous-shaped constructor.
        var constructor = typeof(Ex7Point).GetConstructor(new[] { typeof(int), typeof(int) })!;
        var xMember = typeof(Ex7Point).GetProperty(nameof(Ex7Point.X))!;
        var yMember = typeof(Ex7Point).GetProperty(nameof(Ex7Point.Y))!;
        var pointArguments = new Expression[] { Expression.Constant(1), Expression.Constant(2) };
        Expression Point(MemberInfo[]? members)
            => members is null
                ? Expression.New(constructor, pointArguments)
                : Expression.New(constructor, pointArguments, members);

        AssertNodeEqual(Point(new MemberInfo[] { xMember, yMember }), Point(new MemberInfo[] { xMember, yMember }),
            "CompareMemberList compares the member list element by element");
        AssertNodeDifferent(Point(new MemberInfo[] { xMember, yMember }), Point(null),
            "CompareMemberList rejects an absent member list");
        AssertNodeDifferent(Point(new MemberInfo[] { xMember, yMember }), Point(new MemberInfo[] { yMember, yMember }),
            "CompareMemberList compares the member identity");

        // MemberListBinding through MemberInit.
        var itemsProperty = typeof(Ex7Bag).GetProperty(nameof(Ex7Bag.Items))!;
        var add = Method(typeof(List<int>), nameof(List<int>.Add), typeof(int));
        Expression Bag(int value)
            => Expression.MemberInit(
                Expression.New(typeof(Ex7Bag)),
                Expression.ListBind(itemsProperty, Expression.ElementInit(add, Expression.Constant(value))));

        AssertNodeEqual(Bag(1), Bag(1), "identical member-list bindings compare equal");
        AssertNodeDifferent(Bag(1), Bag(2), "CompareBinding compares a member-list binding's initializer arguments");

        // MemberMemberBinding through MemberInit.
        var innerProperty = typeof(Ex7Outer).GetProperty(nameof(Ex7Outer.Inner))!;
        var valueProperty = typeof(Ex7Inner).GetProperty(nameof(Ex7Inner.Value))!;
        Expression Outer(int value)
            => Expression.MemberInit(
                Expression.New(typeof(Ex7Outer)),
                Expression.MemberBind(innerProperty, Expression.Bind(valueProperty, Expression.Constant(value))));

        AssertNodeEqual(Outer(1), Outer(1), "identical member-member bindings compare equal");
        AssertNodeDifferent(Outer(1), Outer(2), "CompareBinding compares a nested member-member binding");
    }

    // ---------------------------------------------------------------------------------------------
    // 28. Lambda parameter scope spill (more than four parameters)
    // ---------------------------------------------------------------------------------------------
    [Fact]
    public void LambdaParameterScope_SpillAtFiveParameters_ShouldCompare()
    {
        Expression<Func<Ex7Item, bool>> atLeastFive = x => x.Id >= 5;
        Expression<Func<Ex7Item, bool>> atLeastSix = x => x.Id >= 6;

        AssertSame(atLeastFive, atLeastFive, [5, 6], "an identical predicate reuses one cached plan");
        AssertDifferent(atLeastFive, atLeastSix, [5, 6], [6], "a changed comparison bound is a different expression");

        ParameterExpression[] Parameters(int count)
            => Enumerable.Range(0, count)
                .Select(i => Expression.Parameter(typeof(int), "p" + i))
                .ToArray();

        static Expression Sum(ParameterExpression[] parameters)
            => parameters.Aggregate((Expression)Expression.Constant(0), (accumulator, parameter) => Expression.Add(accumulator, parameter));

        var fiveA = Parameters(5);
        var fiveB = Parameters(5);
        var lambdaA = Expression.Lambda<Func<int, int, int, int, int, int>>(Sum(fiveA), fiveA);
        var lambdaB = Expression.Lambda<Func<int, int, int, int, int, int>>(Sum(fiveB), fiveB);

        AssertNodeEqual(lambdaA, lambdaB,
            "five parameters spill the parameter scope to the dictionary and still map positionally");
        AssertNodeDifferent(
            lambdaA,
            Expression.Lambda<Func<int, int, int, int, int, int>>(Expression.Add(Sum(fiveA), Expression.Constant(1)), fiveA),
            "the spilled parameter scope still compares the body");
    }

    /// <summary>Named projection shape for the member-init binding fact.</summary>
    public sealed class Ex7Dto
    {
        public long Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Constant-receiver probe with two same-typed members.</summary>
    public sealed class Ex7Pair
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    /// <summary>Constructor shape carrying an explicit member list.</summary>
    public sealed class Ex7Point
    {
        public Ex7Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    /// <summary>Collection with two overloaded Add methods, used by the element-init arm.</summary>
    public sealed class Ex7MultiAdd : IEnumerable<int>
    {
        public void Add(int value)
        {
        }

        public void Add(string value)
        {
        }

        public IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Container used by the member-list binding arm.</summary>
    public sealed class Ex7Bag
    {
        public List<int> Items { get; set; } = [];
    }

    /// <summary>Outer container used by the member-member binding arm.</summary>
    public sealed class Ex7Outer
    {
        public Ex7Inner Inner { get; set; } = new();
    }

    /// <summary>Nested container used by the member-member binding arm.</summary>
    public sealed class Ex7Inner
    {
        public int Value { get; set; }
    }

    /// <summary>Public extension node whose equality is defined by its value.</summary>
    public sealed class Ex7Extension(int value) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(int);

        public override bool Equals(object? obj) => obj is Ex7Extension other && other.Value == Value;

        public override int GetHashCode() => Value;

        private int Value { get; } = value;

        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }

    /// <summary>Class-owned entity over <c>ex7_item</c>.</summary>
    [SqlTable("ex7_item")]
    public sealed class Ex7Item
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("score")]
        public int? Score { get; set; }
    }
}
