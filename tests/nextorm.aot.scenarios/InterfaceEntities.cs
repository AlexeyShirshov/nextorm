using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.AotScenarios;

public static partial class AotScenarioCatalog
{
    private static object? InterfaceEntityMaterialization(IDataContext context)
    {
        var expected = context.From<AotSimpleEntity>().OrderBy(x => x.Id).ToList().Select(x => x.Id).ToArray();
        var rows = context.From<IAotMaterializedEntity>().OrderBy(x => x.Id).ToList();
        var actual = rows.Select(x => x.Id).ToArray();
        if (expected.Length == 0 || !expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException("Native baseline interface rows did not match the concrete entity rows.");
        }

        return string.Join(",", actual);
    }
}

internal interface IAotMaterializedKey
{
    [Key, Column("id")]
    int Id { get; }
}

[SqlTable("simple_entity")]
internal interface IAotMaterializedEntity : IAotMaterializedKey
{
}
