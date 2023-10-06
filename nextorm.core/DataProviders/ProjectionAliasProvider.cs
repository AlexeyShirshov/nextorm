using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

public class ProjectionAliasProvider : IAliasProvider
{
    private readonly int _dim;
    private readonly Type _projection;

    public ProjectionAliasProvider(int dim, Type projectionType)
    {
        _dim = dim;
        _projection = projectionType;
    }

    public string FindAlias(ParameterExpression param)
    {
        Type declaringType = param.Type;
        var idx = 0;
        foreach (var prop in _projection.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (++idx > _dim && prop.PropertyType == declaringType)
                return prop.Name;
        }

        throw new BuildSqlCommandException($"Cannot find alias of type {declaringType} in {_projection}");
    }
}
