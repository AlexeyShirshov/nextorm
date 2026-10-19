using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.PublicExtensibility.Tests;

/// <summary>
/// External-subclass fixture for the eleven protected preparation/description fields restored on
/// <see cref="QueryCommand"/> for third-party subclasses compiled against the 0.8.0.0.4366 surface.
/// This assembly receives no <c>InternalsVisibleTo</c> grant, so the only members reachable from
/// <see cref="T297CommandProbe"/> are the public/protected surface a real consumer sees.
/// </summary>
public sealed class T297CommandProbe : QueryCommand
{
    /// <summary>Creates a probe over the given shape, using the protected external-consumer constructor.</summary>
    public T297CommandProbe(IDataContext? dataContext, QueryDefinition definition)
        : base(dataContext, definition)
    {
    }

    /// <summary>Reads the eleven protected fields directly (field syntax, not property syntax).</summary>
    public (SelectExpression[]? SelectList, FromExpression? From, bool IsPrepared, LambdaExpression? Exp,
        Type? SrcType, LambdaExpression? Condition, LambdaExpression? GroupExp, LambdaExpression? Having,
        LambdaExpression? PreWhere, LambdaExpression[]? ArrayJoins, Sorting[]? Sorting) ReadFields() =>
        (_selectList, _from, _isPrepared, _exp, _srcType, _condition, _groupExp, _having, _preWhere,
            _arrayJoins, _sorting);

    // Direct writes are the compatibility contract: a field is assignable, and the assignment is the
    // same storage the public getters and the preparation reset path read.
    public void WriteSelectList(SelectExpression[]? value) => _selectList = value;
    public void WriteFrom(FromExpression? value) => _from = value;
    public void WriteIsPrepared(bool value) => _isPrepared = value;
    public void WriteExp(LambdaExpression? value) => _exp = value;
    public void WriteSrcType(Type? value) => _srcType = value;
    public void WriteCondition(LambdaExpression? value) => _condition = value;
    public void WriteGroupExp(LambdaExpression? value) => _groupExp = value;
    public void WriteHaving(LambdaExpression? value) => _having = value;
    public void WritePreWhere(LambdaExpression? value) => _preWhere = value;
    public void WriteArrayJoins(LambdaExpression[]? value) => _arrayJoins = value;
    public void WriteSorting(Sorting[]? value) => _sorting = value;

    public void WriteAll(SelectExpression[]? selectList, FromExpression? from, bool isPrepared,
        LambdaExpression? exp, Type? srcType, LambdaExpression? condition, LambdaExpression? groupExp,
        LambdaExpression? having, LambdaExpression? preWhere, LambdaExpression[]? arrayJoins,
        Sorting[]? sorting)
    {
        _selectList = selectList;
        _from = from;
        _isPrepared = isPrepared;
        _exp = exp;
        _srcType = srcType;
        _condition = condition;
        _groupExp = groupExp;
        _having = having;
        _preWhere = preWhere;
        _arrayJoins = arrayJoins;
        _sorting = sorting;
    }
}

/// <summary>
/// E19: binary compatibility of the eleven protected fields restored on <see cref="QueryCommand"/>.
/// The tests are written the way an external consumer subclass would use them: direct field reads
/// and writes plus assertions over the command's public surface.
/// </summary>
public class T297ProtectedFieldCompatibilityTests
{
    private static readonly (string Name, Type FieldType)[] ProtectedFields =
    [
        ("_selectList", typeof(SelectExpression[])),
        ("_from", typeof(FromExpression)),
        ("_isPrepared", typeof(bool)),
        ("_exp", typeof(LambdaExpression)),
        ("_srcType", typeof(Type)),
        ("_condition", typeof(LambdaExpression)),
        ("_groupExp", typeof(LambdaExpression)),
        ("_having", typeof(LambdaExpression)),
        ("_preWhere", typeof(LambdaExpression)),
        ("_arrayJoins", typeof(LambdaExpression[])),
        ("_sorting", typeof(Sorting[])),
    ];

    private static LambdaExpression Projection()
    {
        Expression<Func<T297Row, int>> projection = r => r.Id;
        return projection;
    }

    private static LambdaExpression Predicate()
    {
        Expression<Func<T297Row, bool>> predicate = r => r.Id > 0;
        return predicate;
    }

    private static T297CommandProbe NewProbe() =>
        new(null, QueryDefinition.ForEntity(typeof(T297Row)));

    // ---- metadata -------------------------------------------------------------------------

    [Fact]
    public void ProtectedSurface_ShouldBeElevenInstanceFields_DeclaredOnQueryCommand()
    {
        ProtectedFields.Should().HaveCount(11);

        foreach (var (name, fieldType) in ProtectedFields)
        {
            var field = typeof(QueryCommand).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

            field.Should().NotBeNull($"{name} must be a protected field, not a property");
            field!.DeclaringType.Should().Be(typeof(QueryCommand));
            field.FieldType.Should().Be(fieldType);
            field.IsStatic.Should().BeFalse();
            field.IsPublic.Should().BeFalse();
            field.IsFamily.Should().BeTrue();
            field.IsInitOnly.Should().BeFalse();
            field.IsLiteral.Should().BeFalse();
        }
    }

    [Fact]
    public void Paging_ShouldBeField_AndEagerLoadMode_ShouldBeProperty()
    {
        var paging = typeof(QueryCommand).GetField("Paging", BindingFlags.Instance | BindingFlags.Public);
        paging.Should().NotBeNull("Paging is part of the external field contract");
        paging!.FieldType.Should().Be(typeof(Paging));
        paging.IsStatic.Should().BeFalse();

        typeof(QueryCommand).GetField("EagerLoadMode", BindingFlags.Instance | BindingFlags.Public)
            .Should().BeNull("EagerLoadMode must not be a field substitute");

        var eagerLoad = typeof(QueryCommand).GetProperty("EagerLoadMode", BindingFlags.Instance | BindingFlags.Public);
        eagerLoad.Should().NotBeNull();
        eagerLoad!.PropertyType.Should().Be(typeof(EagerLoadMode?));
        eagerLoad.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ConsumerAssembly_ShouldNotReceiveInternalsVisibleTo()
    {
        var consumer = typeof(T297ProtectedFieldCompatibilityTests).Assembly.GetName().Name;
        typeof(QueryCommand).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .Should().NotContain(consumer);
    }

    // ---- scenarios ------------------------------------------------------------------------

    [Fact]
    public void WriteBeforePreparation_ShouldPopulatePublicSurfaceWithReferenceIdentity()
    {
        var probe = NewProbe();
        var selectList = new[] { new SelectExpression(typeof(int)) };
        var from = new FromExpression("t297_before");
        var projection = Projection();
        var condition = Predicate();
        var group = Projection();
        var having = Predicate();
        var preWhere = Predicate();
        var arrayJoins = new[] { (LambdaExpression)Expression.Lambda(Expression.Constant(1)) };
        var sorting = new[] { new Sorting(0) };

        probe.WriteAll(selectList, from, false, projection, typeof(T297Row), condition, group, having,
            preWhere, arrayJoins, sorting);

        probe.SelectList.Should().BeSameAs(selectList);
        probe.From.Should().BeSameAs(from);
        probe.IsPrepared.Should().BeFalse();
        probe.EntityType.Should().Be(typeof(T297Row));
        probe.Condition.Should().BeSameAs(condition);
        probe.GroupBy.Should().BeSameAs(group);
        probe.Having.Should().BeSameAs(having);
        probe.PreWhere.Should().BeSameAs(preWhere);
        probe.Sorting.Should().BeSameAs(sorting);

        // _exp and _arrayJoins have no public reader on QueryCommand; the field round-trip is the
        // observable contract an external subclass has for them.
        var fields = probe.ReadFields();
        fields.Exp.Should().BeSameAs(projection);
        fields.ArrayJoins.Should().BeSameAs(arrayJoins);
        fields.SelectList.Should().BeSameAs(selectList);
        fields.From.Should().BeSameAs(from);
        fields.Condition.Should().BeSameAs(condition);
        fields.GroupExp.Should().BeSameAs(group);
        fields.Having.Should().BeSameAs(having);
        fields.PreWhere.Should().BeSameAs(preWhere);
        fields.Sorting.Should().BeSameAs(sorting);
    }

    [Fact]
    public void WriteAfterPreparation_ShouldBeObservedByPublicSurfaceAndResetPrepPreparation()
    {
        var probe = NewProbe();
        var selectList = new[] { new SelectExpression(typeof(string)), new SelectExpression(typeof(int)) };
        var from = new FromExpression("t297_after");

        probe.WriteIsPrepared(true);
        probe.WriteSelectList(selectList);
        probe.WriteFrom(from);

        probe.IsPrepared.Should().BeTrue();
        probe.SelectList.Should().BeSameAs(selectList);
        probe.From.Should().BeSameAs(from);

        probe.ResetPreparation();

        probe.IsPrepared.Should().BeFalse("ResetPreparation clears the same _isPrepared storage");
        probe.SelectList.Should().BeNull("ResetPreparation clears the same _selectList storage");
        probe.From.Should().BeNull("ResetPreparation clears the same _from storage");
    }

    [Fact]
    public void NullAndDefaultWrites_ShouldBeStoredAsIs()
    {
        var probe = NewProbe();
        var initial = probe.ReadFields();
        initial.SelectList.Should().BeNull();
        initial.From.Should().BeNull();
        initial.IsPrepared.Should().BeFalse();
        initial.Exp.Should().BeNull();
        initial.SrcType.Should().Be(typeof(T297Row));
        initial.Condition.Should().BeNull();
        initial.GroupExp.Should().BeNull();
        initial.Having.Should().BeNull();
        initial.PreWhere.Should().BeNull();
        initial.ArrayJoins.Should().BeNull();
        initial.Sorting.Should().BeNull();

        probe.WriteAll(null, null, false, null, null, null, null, null, null, null, null);

        var fields = probe.ReadFields();
        fields.SelectList.Should().BeNull();
        fields.From.Should().BeNull();
        fields.IsPrepared.Should().BeFalse();
        fields.Exp.Should().BeNull();
        fields.SrcType.Should().BeNull();
        fields.Condition.Should().BeNull();
        fields.GroupExp.Should().BeNull();
        fields.Having.Should().BeNull();
        fields.PreWhere.Should().BeNull();
        fields.ArrayJoins.Should().BeNull();
        fields.Sorting.Should().BeNull();
        probe.EntityType.Should().BeNull();
    }

    [Fact]
    public void BooleanField_ShouldRoundTripFalseThenTrue()
    {
        var probe = NewProbe();

        probe.WriteIsPrepared(false);
        probe.IsPrepared.Should().BeFalse();

        probe.WriteIsPrepared(true);
        probe.IsPrepared.Should().BeTrue();

        probe.WriteIsPrepared(false);
        probe.IsPrepared.Should().BeFalse();
    }

    [Fact]
    public void DistinguishableReferenceValues_ShouldNotCollapse()
    {
        var first = NewProbe();
        var second = NewProbe();
        var firstFrom = new FromExpression("t297_first");
        var secondFrom = new FromExpression("t297_second");
        var firstSelect = new[] { new SelectExpression(typeof(int)) };
        var secondSelect = new[] { new SelectExpression(typeof(int)), new SelectExpression(typeof(long)) };

        first.WriteFrom(firstFrom);
        second.WriteFrom(secondFrom);
        first.WriteSelectList(firstSelect);
        second.WriteSelectList(secondSelect);

        first.From.Should().BeSameAs(firstFrom);
        second.From.Should().BeSameAs(secondFrom);
        first.From.Should().NotBeSameAs(second.From);
        first.SelectList.Should().BeSameAs(firstSelect);
        second.SelectList.Should().BeSameAs(secondSelect);
        first.SelectList.Should().NotBeSameAs(second.SelectList);
        first.SelectList.Should().HaveCount(1);
        second.SelectList.Should().HaveCount(2);
    }

    [Fact]
    public void PagingField_ShouldBeAssignableThroughThePublicField()
    {
        var probe = NewProbe();
        probe.Paging = new Paging { Limit = 7, Offset = 3 };
        probe.Paging.Limit.Should().Be(7);
        probe.Paging.Offset.Should().Be(3);
    }

    public sealed class T297Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
