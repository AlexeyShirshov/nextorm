namespace NextORM.Core;

/// <summary>
/// The cast target of the optional <c>AS {CHAR|BINARY}(N)</c> clause of MySQL's
/// <c>WEIGHT_STRING</c> function, used by the cast/length overload of <c>weight_string</c>.
/// </summary>
/// <remarks>
/// <c>AS CHAR(N)</c> pads the value with spaces and <c>AS BINARY(N)</c> pads it with <c>0x00</c> bytes
/// before the weight is computed; both pad to <c>N</c> or truncate to <c>N</c> characters/bytes when the
/// value is longer. MySQL 8.4 documents no other modifier clause (the <c>LEVEL</c> flags are unused), so
/// no further option is modelled here.
/// </remarks>
public enum MySqlWeightStringCast
{
    /// <summary>Pad or truncate with spaces: <c>AS CHAR(N)</c>.</summary>
    Char,

    /// <summary>Pad or truncate with <c>0x00</c> bytes: <c>AS BINARY(N)</c>.</summary>
    Binary
}

/// <summary>
/// The format family of MySQL's <c>GET_FORMAT</c> function, used as the first argument of
/// <c>get_format</c>. The value is rendered as the bare keyword (<c>DATE</c>, <c>TIME</c>,
/// <c>DATETIME</c>) and must be a compile-time constant.
/// </summary>
public enum MySqlFormatKind
{
    /// <summary>The date formats: <c>GET_FORMAT(DATE, 'EUR')</c>.</summary>
    Date,

    /// <summary>The time formats: <c>GET_FORMAT(TIME, 'EUR')</c>.</summary>
    Time,

    /// <summary>The date-time formats: <c>GET_FORMAT(DATETIME, 'EUR')</c>.</summary>
    DateTime
}
