using System.Text;

namespace NextORM.Core;

/// <summary>
/// Converts CLR names to <c>snake_case</c>: <c>SimpleEntity</c> becomes <c>simple_entity</c>,
/// <c>FirstName</c> becomes <c>first_name</c>. Runs of capitals are treated as one word, so
/// <c>OrderID</c> becomes <c>order_id</c> and <c>HTTPServer</c> becomes <c>http_server</c>.
/// </summary>
/// <remarks>
/// For interfaces the leading <c>I</c> is dropped when it is followed by another capital, so
/// <c>IProduct</c> maps to <c>product</c> (but <c>Idle</c> stays <c>idle</c>).
/// </remarks>
public sealed class SnakeCaseNamingConvention : INamingConvention
{
    /// <summary>The shared stateless instance.</summary>
    public static SnakeCaseNamingConvention Instance { get; } = new();

    /// <inheritdoc />
    public string TableName(string clrName, bool isInterface)
        => ToSnakeCase(isInterface && clrName.Length > 1 && clrName[0] == 'I' && char.IsUpper(clrName[1])
            ? clrName[1..]
            : clrName);

    /// <inheritdoc />
    public string ColumnName(string propertyName) => ToSnakeCase(propertyName);

    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    var prev = name[i - 1];

                    // Insert a separator at a word boundary: lower -> Upper (simpleEntity), or the
                    // last capital of a run -> Upper+lower (HTTPServer -> http_server).
                    if (char.IsLower(prev)
                        || char.IsDigit(prev)
                        || (char.IsUpper(prev) && i + 1 < name.Length && char.IsLower(name[i + 1])))
                        sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
