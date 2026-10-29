using System.Diagnostics;
using System.Globalization;
using MySqlConnector;

namespace NextORM.Integration.Tests;

/// <summary>
/// One raw observation of a T350 candidate statement: what the MariaDB 11.4 server accepted, the type
/// the connector materialised for the first column, the text value, and — when the statement failed —
/// the vendor error number, message and SQLSTATE. The observation is the only decision input the
/// inventory dispositions are taken from.
/// </summary>
/// <param name="Sql">The statement that was executed.</param>
/// <param name="FieldType">The full CLR name of the first column's field type, or <c>null</c>.</param>
/// <param name="ValueType">The CLR type name of the materialised value, <c>NULL</c> for a database NULL, or <c>null</c>.</param>
/// <param name="IsNull">True when the first column of the first row is a database NULL.</param>
/// <param name="ErrorNumber">The vendor error number, or <c>null</c> when the statement was accepted.</param>
/// <param name="Text">The text projection of the value (hex for binary), or <c>null</c>.</param>
/// <param name="Error">The <c>number: message</c> text of the failure, or <c>null</c>.</param>
/// <param name="SqlState">The SQLSTATE of the failure, or <c>null</c>.</param>
/// <param name="DurationMs">The wall-clock duration of the observation.</param>
internal sealed record T350Observation(
    string Sql,
    string? FieldType,
    string? ValueType,
    bool IsNull,
    int? ErrorNumber,
    string? Text,
    string? Error,
    string? SqlState,
    long DurationMs)
{
    /// <summary>True when the server accepted the statement (no vendor error was raised).</summary>
    internal bool Accepted => ErrorNumber is null && Error is null;
}

/// <summary>
/// The shared raw-ADO.NET oracle helpers of the T350 (issue #284) S1 probe: one statement, one
/// observation, no product path involved, so the recorded fact is the server's answer rather than the
/// provider's translation.
/// </summary>
internal static class T350RawOracle
{
    internal static T350Observation Observe(MySqlConnection connection, string sql)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            if (reader.FieldCount == 0)
                return new T350Observation(sql, null, null, false, null, "no result set", null, null, stopwatch.ElapsedMilliseconds);
            if (!reader.Read())
                return new T350Observation(sql, null, null, false, null, "no rows", null, null, stopwatch.ElapsedMilliseconds);

            var fieldType = reader.GetFieldType(0).FullName;
            var isNull = reader.IsDBNull(0);
            var value = isNull ? null : reader.GetValue(0);
            var valueType = isNull ? "NULL" : ClrName(value!.GetType());
            var text = isNull ? null : Format(value);

            while (reader.Read())
            {
                // Drain the remaining rows so the connection stays usable for the next statement.
            }

            return new T350Observation(sql, fieldType, valueType, isNull, null, text, null, null, stopwatch.ElapsedMilliseconds);
        }
        catch (MySqlException exception)
        {
            return new T350Observation(
                sql, null, null, false, exception.Number, null,
                exception.Number + ": " + exception.Message, exception.SqlState, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            return new T350Observation(
                sql, null, null, false, null, null,
                exception.GetType().Name + ": " + exception.Message, null, stopwatch.ElapsedMilliseconds);
        }
    }

    internal static void Execute(MySqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    internal static string? Scalar(MySqlConnection connection, string sql)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            return exception.GetType().Name + ": " + exception.Message;
        }
    }

    internal static string ClrName(Type type) => type == typeof(byte[]) ? "byte[]" : type.FullName ?? type.Name;

    internal static string? Format(object? value)
    {
        var text = value switch
        {
            null => null,
            byte[] bytes => "hex:" + Convert.ToHexString(bytes),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return text is { Length: > 300 } ? text[..300] : text;
    }

    internal static string PathOf(string variable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        var path = configured is { Length: > 0 } ? configured : fallback;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return path;
    }

    internal static StreamWriter Open(string path, FileMode mode) =>
        new(new FileStream(path, mode, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
}
