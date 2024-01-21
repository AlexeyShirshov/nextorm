

using System.Linq.Expressions;

namespace nextorm.core;

public static class NORM
{
    public static NORM_SQL SQL => default!;
    public static T Param<T>(int idx) => default!;
    public class NORM_SQL
    {
        public bool exists(QueryCommand cmd) => default!;
        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;
        public T any<T>(QueryCommand<T> cmd) => default!;
        public T all<T>(QueryCommand<T> cmd) => default!;
        public int count(params object?[] exps) => default!;
        public long count_big(params object?[] exps) => default!;
        public int distinct_count(params object?[] exps) => default!;
        public long distinct_count_big(params object?[] exps) => default!;
    }
}