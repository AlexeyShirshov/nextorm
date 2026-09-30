using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// The container shape of a JSON stream: <see cref="Array"/> wraps every row in one JSON array,
/// <see cref="NdJson"/> writes one standalone JSON value per line.
/// </summary>
public enum JsonStreamMode
{
    /// <summary>All rows are emitted as one JSON array.</summary>
    Array,

    /// <summary>Each row is emitted as a standalone JSON value followed by a newline (NDJSON).</summary>
    NdJson,
}

/// <summary>
/// Shape options for the JSON streaming terminals (<c>QueryCommand.WriteJson</c> /
/// <c>EntityBuilder.WriteJsonAsync</c>). The options are validated when the shape plan is built.
/// </summary>
public sealed class JsonStreamOptions
{
    /// <summary>The container shape. Defaults to <see cref="JsonStreamMode.Array"/>.</summary>
    public JsonStreamMode Mode { get; init; } = JsonStreamMode.Array;

    /// <summary>
    /// The property name of the wrapper object around an <see cref="JsonStreamMode.Array"/> document
    /// (<c>{"root":[...]}</c>), or <see langword="null"/> for a bare array. Not allowed in
    /// <see cref="JsonStreamMode.NdJson"/>.
    /// </summary>
    public string? Root { get; init; }

    /// <summary>When <see langword="true"/>, object properties whose value is SQL NULL are omitted.</summary>
    public bool IgnoreNull { get; init; }

    /// <summary>When <see langword="true"/>, the array document is indented. Not allowed in <see cref="JsonStreamMode.NdJson"/>.</summary>
    public bool WriteIndented { get; init; }

    /// <summary>The policy applied to the projected property names, or <see langword="null"/> to keep them as-is.</summary>
    public JsonNamingPolicy? PropertyNamingPolicy { get; init; }
}
