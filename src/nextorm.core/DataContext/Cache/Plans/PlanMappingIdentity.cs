namespace NextORM.Core;

/// <summary>
/// Identity of the services that resolve a query's effective table and column mapping.
/// The built-in defaults all read the same mapping store. Custom services have no
/// structural compatibility contract, so they participate by identity without being retained.
/// </summary>
internal readonly record struct PlanMappingIdentity(object? MetadataProvider, object? VariantResolver)
{
    internal static PlanMappingIdentity For(IDataContext? context)
        => context is DataContext sql
            ? new(
                sql.EntityMetadataProvider is DefaultEntityMetadataProvider ? null : PlanObjectIdentity.For(sql.EntityMetadataProvider),
                sql.EntityVariantResolver is DefaultEntityVariantResolver ? null : PlanObjectIdentity.For(sql.EntityVariantResolver))
            : default;
}
