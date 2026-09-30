using System.Text;

namespace NextORM.Core;

/// <summary>
/// The resolved per-call field policy: the NULL marker, the Excel formula guard and the optional
/// per-value transform. It is built once from <see cref="CsvStreamOptions"/> and shared by every
/// compiled column formatter through the row buffer, so the unconfigured default path stays box-free.
/// </summary>
internal sealed class CsvFieldPolicy
{
    /// <summary>The default policy: <c>\N</c> NULL marker, no Excel guard, no transform.</summary>
    public static readonly CsvFieldPolicy Default = new(CsvStreamOptions.DefaultNullMarker, excelMode: false, valueTransform: null);

    public CsvFieldPolicy(string nullMarker, bool excelMode, Func<object?, string?>? valueTransform)
    {
        NullMarker = nullMarker;
        NullMarkerUtf8 = Encoding.UTF8.GetBytes(nullMarker);
        ExcelMode = excelMode;
        ValueTransform = valueTransform;
    }

    /// <summary>The literal text written for a SQL NULL.</summary>
    public string NullMarker { get; }

    /// <summary>The NULL marker encoded as UTF-8.</summary>
    public byte[] NullMarkerUtf8 { get; }

    /// <summary>Whether fields beginning with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> are prefixed with an apostrophe.</summary>
    public bool ExcelMode { get; }

    /// <summary>The optional per-value transform, or <see langword="null"/> for the box-free default path.</summary>
    public Func<object?, string?>? ValueTransform { get; }
}
