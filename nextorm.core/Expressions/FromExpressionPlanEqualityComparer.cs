using System.Diagnostics.CodeAnalysis;
using nextorm.core;

public sealed class FromExpressionPlanEqualityComparer : IEqualityComparer<FromExpression>
{
    private FromExpressionPlanEqualityComparer() { }
    public static FromExpressionPlanEqualityComparer Instance => new();
    public bool Equals(FromExpression? x, FromExpression? y)
    {
        if (x == y) return true;
        if (x is null || y is null) return false;

        return x.TableAlias == y.TableAlias && x.Table.IsT0 == y.Table.IsT0 && x.Table.Match(
                tbl => tbl == y.Table.AsT0,
                cmd => QueryPlanEqualityComparer.Instance.Equals(cmd, y.Table.AsT1)
                );
    }

    public int GetHashCode([DisallowNull] FromExpression obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            var hash = new HashCode();

            hash.Add(obj.TableAlias);

            obj.Table.Switch(
                 tbl => hash.Add(tbl),
                 cmd => hash.Add(cmd, QueryPlanEqualityComparer.Instance)
            );

            return hash.ToHashCode();
        }
    }
}