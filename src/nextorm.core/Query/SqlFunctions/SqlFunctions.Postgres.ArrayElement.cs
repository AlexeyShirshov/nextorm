namespace NextORM.Core;

public sealed partial class PostgresFunctions
{
    /// <summary>Reads a rank-1 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[]? array, int? index1) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-1 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[]? array, int? index1) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-2 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[,]? array, int? index1, int? index2) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-2 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[,]? array, int? index1, int? index2) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-3 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[,,]? array, int? index1, int? index2, int? index3) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-3 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[,,]? array, int? index1, int? index2, int? index3) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-4 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[,,,]? array, int? index1, int? index2, int? index3, int? index4) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-4 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[,,,]? array, int? index1, int? index2, int? index3, int? index4) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-5 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-5 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-6 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T[,,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5, int? index6) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-6 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public T? array_element<T>(T?[,,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5, int? index6) where T : struct
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

}

/// <summary>PostgreSQL array-element query markers for reference-type elements.</summary>
public static class PostgresArrayElementExtensions
{
    /// <summary>Reads a rank-1 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[]? array, int? index1) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-2 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[,]? array, int? index1, int? index2) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-3 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[,,]? array, int? index1, int? index2, int? index3) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-4 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[,,,]? array, int? index1, int? index2, int? index3, int? index4) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-5 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

    /// <summary>Reads a rank-6 PostgreSQL array element using native SQL coordinates, or returns SQL NULL.</summary>
    public static T? array_element<T>(this PostgresFunctions functions, T?[,,,,,]? array, int? index1, int? index2, int? index3, int? index4, int? index5, int? index6) where T : class
        => throw new NotSupportedException("array_element is a PostgreSQL query marker.");

}
