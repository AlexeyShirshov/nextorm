using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace T301.Api;

/// <summary>The same shape as <c>Positive.cs</c>, so only the selector form differs.</summary>
[SqlTable("t301_item")]
public interface IItem
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("a")]
    int A { get; set; }

    [Column("b")]
    int B { get; set; }

    [Column("total")]
    int Total { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

/// <summary>Class entity.</summary>
public sealed class Item : IItem
{
    public long Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public int Total { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Intentionally broken case (C7): the removed one-parameter column selector must no longer resolve to
/// any <c>ThenUpdate</c> overload. The expected diagnostic is a lambda-arity conversion error on
/// <c>ThenUpdate</c> — an unrelated build error is a failure of the fixture, not a passing negative.
/// </summary>
public static class RemovedUpdateSelectorCases
{
    /// <summary>Old column-selector form: one parameter, must not compile.</summary>
    public static void ThenUpdate_ColumnSelector_IsRemoved(MergeBuilder<Item> merge)
        => merge.WhenMatched().ThenUpdate(d => new { d.Name });
}
