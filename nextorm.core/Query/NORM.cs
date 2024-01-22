

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

public static class NORM
{
    public static NORM_SQL SQL => default!;
    public static T Param<T>(int idx) => default!;
    public class NORM_SQL
    {
        [Browsable(false)]
        public static readonly MethodInfo ExistsMI = typeof(NORM_SQL).GetMethod(nameof(exists), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo MinMI = typeof(NORM_SQL).GetMethod(nameof(min), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo MaxMI = typeof(NORM_SQL).GetMethod(nameof(max), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo AvgMI = typeof(NORM_SQL).GetMethod(nameof(avg), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo SumMI = typeof(NORM_SQL).GetMethod(nameof(sum), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo StdevMI = typeof(NORM_SQL).GetMethod(nameof(stdev), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo StdevpMI = typeof(NORM_SQL).GetMethod(nameof(stdevp), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo VarMI = typeof(NORM_SQL).GetMethod(nameof(var), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly MethodInfo VarpMI = typeof(NORM_SQL).GetMethod(nameof(varp), BindingFlags.Public | BindingFlags.Instance)!;
        [Browsable(false)]
        public static readonly ConstantExpression SQLExpression = Expression.Constant(new NORM_SQL());
        public bool exists(QueryCommand cmd) => default!;
        public bool @in<T>(T column, QueryCommand<T> cmd) => default!;
        public T any<T>(QueryCommand<T> cmd) => default!;
        public T all<T>(QueryCommand<T> cmd) => default!;
        public int count(params object?[] properties) => default!;
        public long count_big(params object?[] properties) => default!;
        public int count_distinct(params object?[] properties) => default!;
        public long count_big_distinct(params object?[] properties) => default!;
        public T? min<T>(T? property) => default!;
        public T? max<T>(T? property) => default!;
        public T? avg<T>(T? property) => default!;
        public T? avg_distinct<T>(T? property) => default!;
        public T? sum<T>(T? property) => default!;
        public T? sum_distinct<T>(T? property) => default!;
        public T? stdev<T>(T? property) => default!;
        public T? stdev_distinct<T>(T? property) => default!;
        public T? stdevp<T>(T? property) => default!;
        public T? stdevp_distinct<T>(T? property) => default!;
        public T? var<T>(T? property) => default!;
        public T? var_distinct<T>(T? property) => default!;
        public T? varp<T>(T? property) => default!;
        public T? varp_distinct<T>(T? property) => default!;
    }
}