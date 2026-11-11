using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the dictionary and region surface of <see cref="ClickHouseFunctions"/> (<c>dict_get</c>,
/// <c>dict_get_or_default</c>, <c>dict_get_or_null</c>, <c>dict_get_all</c>, <c>dict_get_descendants</c>,
/// <c>dict_has</c>, <c>dict_get_hierarchy</c>, <c>dict_get_children</c>, <c>dict_is_in</c> and the
/// <c>region_to_*</c>/<c>region_hierarchy</c>/<c>region_in</c> accessors). Only a dialect that opts in
/// with <see cref="ISqlDialect.SupportsDictionaries"/> (ClickHouse) may use these constructs; every other
/// provider rejects them with a clear message.
/// </summary>
internal static class DictionarySqlTranslator
{
    /// <summary>
    /// Members refused for a demonstrated server-version limitation. The refusal is raised before the
    /// dialect gate, so every provider gets the precise technical reason instead of SQL the pinned
    /// ClickHouse server would reject. <c>dictGetKeys</c> (25.12), <c>dictGetRoot</c> (26.7),
    /// <c>regionToParent</c> and <c>regionToParents</c> are absent from 25.8.33.6; the absences are
    /// verified against <c>system.functions</c> and by the <c>UNKNOWN_FUNCTION</c> failure (F18 live cell).
    /// </summary>
    private static readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal)
    {
        [nameof(ClickHouseFunctions.dict_get_keys)] =
            "The dict_get_keys function is refused: dictGetKeys was introduced in ClickHouse 25.12 " +
            "and does not exist on the pinned 25.8.33.6 server (absent from system.functions; the name " +
            "is rejected with UNKNOWN_FUNCTION). No documented equivalent reverse lookup exists for " +
            "this server version.",
        [nameof(ClickHouseFunctions.dict_get_root)] =
            "The dict_get_root function is refused: dictGetRoot was introduced in ClickHouse 26.7 and " +
            "does not exist on the pinned 25.8.33.6 server (absent from system.functions; the name is " +
            "rejected with UNKNOWN_FUNCTION). Use dict_get_hierarchy and take its last element instead.",
        [nameof(ClickHouseFunctions.region_to_parent)] =
            "The region_to_parent function is refused: ClickHouse 25.8.33.6 has no regionToParent " +
            "function (absent from system.functions; the name is rejected with UNKNOWN_FUNCTION). " +
            "Use region_hierarchy and skip the first element instead.",
        [nameof(ClickHouseFunctions.region_to_parents)] =
            "The region_to_parents function is refused: ClickHouse 25.8.33.6 has no regionToParents " +
            "function (absent from system.functions; the name is rejected with UNKNOWN_FUNCTION). " +
            "Use region_hierarchy and skip the first element instead."
    };

    /// <summary>
    /// Translates a dictionary or region function call. Returns <c>false</c> when the call is not part
    /// of this surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (Refused.TryGetValue(node.Method.Name, out var refusal))
            throw new NotSupportedException(refusal);

        switch (node.Method.Name)
        {
            case nameof(ClickHouseFunctions.dict_get) when node.Arguments.Count == 3:
                EmitFunction(visitor, node, "dict_get");
                return true;
            case nameof(ClickHouseFunctions.dict_get_or_default) when node.Arguments.Count == 4:
                EmitFunction(visitor, node, "dict_get_or_default");
                return true;
            case nameof(ClickHouseFunctions.dict_get_or_null) when node.Arguments.Count == 3:
                EmitFunction(visitor, node, "dict_get_or_null");
                return true;
            case nameof(ClickHouseFunctions.dict_get_all) when node.Arguments.Count is 3 or 4:
                EmitFunction(visitor, node, "dict_get_all");
                return true;
            case nameof(ClickHouseFunctions.dict_get_descendants) when node.Arguments.Count is 2 or 3:
                EmitFunction(visitor, node, "dict_get_descendants");
                return true;
            case nameof(ClickHouseFunctions.dict_has) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "dict_has");
                return true;
            case nameof(ClickHouseFunctions.dict_get_hierarchy) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "dict_get_hierarchy");
                return true;
            case nameof(ClickHouseFunctions.dict_get_children) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "dict_get_children");
                return true;
            case nameof(ClickHouseFunctions.dict_is_in) when node.Arguments.Count == 3:
                EmitFunction(visitor, node, "dict_is_in");
                return true;
            case nameof(ClickHouseFunctions.region_to_name) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_name", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_city) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_city", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_area) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_area", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_district) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_district", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_country) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_country", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_continent) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_continent", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_top_continent) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_top_continent", 1);
                return true;
            case nameof(ClickHouseFunctions.region_to_population) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_to_population", 1);
                return true;
            case nameof(ClickHouseFunctions.region_hierarchy) when node.Arguments.Count is 1 or 2:
                EmitRegionFunction(visitor, node, "region_hierarchy", 1);
                return true;
            case nameof(ClickHouseFunctions.region_in) when node.Arguments.Count is 2 or 3:
                EmitRegionFunction(visitor, node, "region_in", 2);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Emits a region function. ClickHouse requires the region-id arguments to be UInt32; a C# integer
    /// literal is typed UInt8 by the server, so the numeric positions are wrapped in <c>toUInt32</c> and
    /// the optional language/geobase string is left untouched.
    /// </summary>
    private static void EmitRegionFunction(
        BaseExpressionVisitor visitor, MethodCallExpression node, string name, int numericArguments)
    {
        if (!visitor.Dialect.SupportsDictionaries)
            EmitFunction(visitor, node, name);

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            var argument = visitor.VisitToString(args[i]);
            rendered[i] = i < numericArguments ? $"toUInt32({argument})" : argument;
        }

        visitor.Builder!.Append(visitor.Dialect.MakeDictionaryFunction(name, rendered));
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (!visitor.Dialect.SupportsDictionaries)
            throw new NotSupportedException(
                "The dictionary and region functions (dictGet, dictGetOrDefault, dictGetOrNull, dictGetAll, " +
                "dictGetDescendants, dictHas, dictGetHierarchy, dictGetChildren, dictIsIn, regionToName, " +
                "regionToCity, regionToArea, regionToDistrict, regionToCountry, regionToContinent, " +
                "regionToTopContinent, regionToPopulation, regionHierarchy, regionIn) are not supported " +
                "by this provider.");

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeDictionaryFunction(name, rendered));
    }
}
