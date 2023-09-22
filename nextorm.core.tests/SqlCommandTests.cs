using System.Linq.Expressions;
using System.Text;
using FluentAssertions;

namespace nextorm.core.tests;

public class SqlCommandTests
{
    [Fact]
    public void Expression_Test()
    {
        Expression<Func<int, int, t>> exp = (id, p) => new t(id, p);

        var l = Expression.Lambda(exp.Body, exp.Parameters[0], exp.Parameters[1] ).Compile();

        var r = l.DynamicInvoke(1,2) as t;

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
        r.P.Should().Be(2);
    }
    [Fact]
    public void Expression_Test2()
    {
        Expression<Func<ctor, t>> exp = (c) => new t(c.GetId(), c.GetP());

        var l = Expression.Lambda(exp.Body, exp.Parameters[0] ).Compile();

        var r = l.DynamicInvoke(new ctor()) as t;

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
        r.P.Should().Be(2);
    }
    [Fact]
    public void Expression_Test3()
    {
        Expression<Func<ctor, t>> exp = (c) => new t(c.IsSome() ? null : c.GetP());

        var l = Expression.Lambda(exp.Body, exp.Parameters[0] ).Compile();

        var r = l.DynamicInvoke(new ctor()) as t;

        r.Should().NotBeNull();
    }
}
class ctor
{
    public int GetId()=>1;
    public int GetP()=>2;
    public bool IsSome()=>true;
}
public class t
{
    public t()
    {

    }
    public t(int? r)
    {

    }
    public t(int id, int p)
    {
        Id = id;
        P = p;
    }

    public int Id { get; set; }
    public int P { get; }
}