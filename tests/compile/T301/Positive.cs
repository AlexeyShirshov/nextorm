using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using NextORM.Core;

namespace T301.Api;

/// <summary>Interface-mapped entity: the assignment destination binds by CLR property without materialising it.</summary>
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

/// <summary>Class entity used by the typed <c>MemberInit</c> case.</summary>
public sealed class Item : IItem
{
    public long Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public int Total { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Compile-only positive cases (C6). Every method takes an already constructed builder and is never
/// called: the fixture proves overload resolution, lambda-arity binding and initializer shape, not
/// execution. The runtime behaviour of the same shapes is covered by the core and provider tests.
/// </summary>
public static class PositiveCases
{
    /// <summary>Full MERGE update, two-parameter anonymous initializer.</summary>
    public static void FullUpdate_TwoParameter(MergeBuilder<Item> merge)
        => merge.WhenMatched().ThenUpdate((target, source) => new { Total = target.Total + source.A + source.B });

    /// <summary>Full MERGE update, three-parameter initializer with an unused <c>excluded</c> parameter.</summary>
    public static void FullUpdate_ThreeParameter_UnusedExcluded(MergeBuilder<Item> merge)
        => merge.WhenMatched().ThenUpdate((target, source, excluded) => new { Total = target.Total + source.A });

    /// <summary>Full MERGE update over an interface-mapped entity.</summary>
    public static void FullUpdate_InterfaceEntity(MergeBuilder<IItem> merge)
        => merge.WhenMatched().ThenUpdate((target, source) => new { Total = target.Total + source.A });

    /// <summary>Typed <c>MemberInit</c> initializer assignable to the entity.</summary>
    public static void FullUpdate_TypedInitializer(MergeBuilder<Item> merge)
        => merge.WhenMatched().ThenUpdate((target, source) => new Item { Total = target.Total + source.B, Name = source.Name });

    /// <summary>Closure over a local, an explicitly typed null and an unboxing round trip.</summary>
    public static void FullUpdate_ClosureBoxingAndTypedNull(MergeBuilder<Item> merge, int bump)
    {
        merge.WhenMatched().ThenUpdate((target, source) => new
        {
            Total = target.Total + bump,
            A = (int)(object)source.A,
            Name = (string?)null,
        });
    }

    /// <summary>A lambda explicitly typed as a delegate expression resolves to the same overload.</summary>
    public static void FullUpdate_ExplicitDelegateExpression(MergeBuilder<Item> merge)
        => merge.WhenMatched().ThenUpdate((Expression<Func<Item, Item, object?>>)((target, source) => new { target.Total }));

    /// <summary>Full MERGE insert, one parameter, with a server-side expression.</summary>
    public static void FullInsert_OneParameter(MergeBuilder<Item> merge)
        => merge.WhenNotMatched().ThenInsert(source => new { Id = source.Id, Name = source.Name, Total = source.A * 2 });

    /// <summary>Key upsert update, two- and three-parameter forms.</summary>
    public static void KeyUpdate(MergeBuilder<Item> merge)
    {
        merge.WhenMatchedUpdate((target, source) => new { Total = target.Total + source.A });
        merge.WhenMatchedUpdate((target, source, excluded) => new { Total = target.Total + source.B });
    }

    /// <summary>Key upsert insert with the mandatory direct conflict-key binding.</summary>
    public static void KeyInsert(MergeBuilder<Item> merge)
        => merge.WhenNotMatchedInsert(source => new { Id = source.Id, Name = source.Name });

    /// <summary>Key upsert insert over an interface entity.</summary>
    public static void KeyInsert_Interface(MergeBuilder<IItem> merge)
        => merge.WhenNotMatchedInsert(source => new { Id = source.Id, Total = source.A + source.B });

    /// <summary>Every preserved no-argument form still resolves.</summary>
    public static void NoArgumentFormsPreserved(MergeBuilder<Item> merge)
    {
        merge.WhenMatched().ThenUpdate();
        merge.WhenNotMatched().ThenInsert();
        merge.WhenMatchedUpdate();
        merge.WhenNotMatchedInsert();
    }
}
