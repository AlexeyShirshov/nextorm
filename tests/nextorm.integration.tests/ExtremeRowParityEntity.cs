using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Integration.Tests;

/// <summary>
/// Deterministic fixture for issue #144 D7 native-vs-forced-portable integration parity. The physical
/// columns are deliberately renamed relative to the CLR properties (<c>Category</c> -> <c>g</c>,
/// <c>Amount</c> -> <c>k1</c>, <c>TieBreak</c> -> <c>k2</c>) so the native key/group aliases have to be
/// resolved from the mapping. Integral extreme/group keys are nullable and the payload carries both a
/// nullable string and a nullable integer, so null-key exclusion and null-payload preservation are both
/// exercisable. The dataset is fully deterministic:
/// <list type="bullet">
/// <item>group 10: k1=1 on ids 1..3 (a maximum tie; id 1 has a NULL label, id 4 has k1 NULL and is excluded);</item>
/// <item>group 20: k1=2,3 (id 6 is the unique maximum);</item>
/// <item>group 40: k1=1 (id 10), tying group 10 on the extreme value for the output-Distinct case;</item>
/// <item>group NULL: k1=5 (id 7, a unique global maximum and its own group);</item>
/// <item>group 30: ids 8 and 9 both have k1 NULL, so the all-null partition disappears.</item>
/// </list>
/// </summary>
[SqlTable("extreme_parity_144")]
public class ExtremeRowParityEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The renamed nullable integral group key (physical <c>g</c>).</summary>
    [Column("g")]
    public int? Category { get; set; }

    /// <summary>The renamed nullable integral extreme key (physical <c>k1</c>).</summary>
    [Column("k1")]
    public int? Amount { get; set; }

    /// <summary>The renamed nullable integral secondary/composite key (physical <c>k2</c>).</summary>
    [Column("k2")]
    public int? TieBreak { get; set; }

    /// <summary>The nullable string payload (physical <c>label</c>); id 1 keeps it NULL.</summary>
    [Column("label")]
    public string? Label { get; set; }

    /// <summary>The nullable integral payload (physical <c>n</c>).</summary>
    [Column("n")]
    public int? N { get; set; }
}

/// <summary>
/// A reference projection shape with no parameterless constructor: the materializer must bind the three
/// projected columns to the three-argument constructor positionally.
/// </summary>
public sealed class ExtremeRowParityDto
{
    public ExtremeRowParityDto(int id, int? amount, string? label)
    {
        Id = id;
        Amount = amount;
        Label = label;
    }

    public int Id { get; }

    public int? Amount { get; }

    public string? Label { get; }
}

/// <summary>A positional record projection shape (reference type, no parameterless constructor).</summary>
public sealed record ExtremeRowParityRecord(int Id, int? Amount, string? Label);

/// <summary>
/// Fixture whose mapped physical column names deliberately equal the native renderers' internal aliases:
/// <c>__nextorm_extreme</c> (PostgreSQL derived-table alias), <c>__nextorm_extreme_src</c> and
/// <c>__nextorm_extreme_tuple</c> (ClickHouse source/tuple aliases). A grouped PostgreSQL query keyed on
/// <see cref="DerivedAlias"/>, and a ClickHouse whole-row query whose payload carries all three columns,
/// would be ambiguous or duplicated if the renderers reused their alias bases verbatim.
/// </summary>
[SqlTable("extreme_alias_144")]
public class ExtremeRowAliasEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Physical name equals the PostgreSQL derived-table alias / all renderers' prefix.</summary>
    [Column("__nextorm_extreme")]
    public int? DerivedAlias { get; set; }

    /// <summary>Physical name equals the ClickHouse source-subquery alias.</summary>
    [Column("__nextorm_extreme_src")]
    public int? SourceAlias { get; set; }

    /// <summary>Physical name equals the ClickHouse tuple alias.</summary>
    [Column("__nextorm_extreme_tuple")]
    public int? TupleAlias { get; set; }

    /// <summary>The group key.</summary>
    [Column("g")]
    public int? G { get; set; }

    /// <summary>The ClickHouse extreme key.</summary>
    [Column("k")]
    public int? K { get; set; }
}
