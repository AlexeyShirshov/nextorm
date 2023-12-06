using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
namespace nextorm.core;
public struct Sorting
{
    public Expression Expression;
    public OrderDirection Direction;
}