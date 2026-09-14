using System.Data.SQLite;

namespace nextorm.sqlite;

[SQLiteFunction(Name = "stdev", Arguments = 1, FuncType = FunctionType.Aggregate)]
public class StdevFunction : SQLiteFunction
{
    public override void Step(object[] args, int stepNumber, ref object? contextData)
    {
        if (args[0] is null or DBNull) return;

        var value = Convert.ToDouble(args[0]);
        var acc = contextData as double[] ?? new double[3];
        acc[0] += 1;
        acc[1] += value;
        acc[2] += value * value;
        contextData = acc;
    }

    public override object? Final(object? contextData)
    {
        if (contextData is not double[] acc || acc[0] < 2) return null;

        var variance = (acc[2] - acc[1] * acc[1] / acc[0]) / (acc[0] - 1);
        return Math.Sqrt(variance);
    }
}

[SQLiteFunction(Name = "stdevp", Arguments = 1, FuncType = FunctionType.Aggregate)]
public class StdevpFunction : SQLiteFunction
{
    public override void Step(object[] args, int stepNumber, ref object? contextData)
    {
        if (args[0] is null or DBNull) return;

        var value = Convert.ToDouble(args[0]);
        var acc = contextData as double[] ?? new double[3];
        acc[0] += 1;
        acc[1] += value;
        acc[2] += value * value;
        contextData = acc;
    }

    public override object? Final(object? contextData)
    {
        if (contextData is not double[] acc || acc[0] < 1) return null;

        var variance = (acc[2] - acc[1] * acc[1] / acc[0]) / acc[0];
        return Math.Sqrt(variance);
    }
}
