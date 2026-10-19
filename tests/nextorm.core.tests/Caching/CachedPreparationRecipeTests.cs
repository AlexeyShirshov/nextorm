using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T201 (#201) cached-path parity — unit D201-2. In this unit the file carries the
/// <b>public-surface export</b> mechanism only.
///
/// <para>
/// It reflects over the compiler-emitted metadata of the NextORM assemblies this test project
/// references and writes a canonical, order-stable JSON inventory to the path selected by the
/// <c>T201_API_EXPORT_PATH</c> environment variable (repository-relative default
/// <c>artifacts/pdca/T201/rv1/api/baseline.json</c>). DO captures the <i>baseline</i> export before
/// any production edit and the <i>candidate</i> export after them; the diff of the two inventories
/// is the R201-API evidence (<c>api/baseline.json</c>, <c>api/candidate.json</c>, <c>api/diff.json</c>).
/// </para>
/// <para>
/// D201-2 asserts nothing about a candidate inventory: no production source was edited, so
/// <c>baseline == candidate</c> is trivially true and is not claimed here (plan §D201-2 negative:
/// "no assertion yet that candidate API is equal").
/// </para>
/// <para>
/// The exported document also carries <c>sha256</c>, the digest of its own canonical (compact) form
/// computed before the digest field is appended. The evidence wrapper owns the surrounding
/// <c>capture</c> block (tree identity, command, wall time) and records the file hash of the artifact.
/// </para>
/// </summary>
public sealed class CachedPreparationRecipeTests
{
    private const string ExportPathEnv = "T201_API_EXPORT_PATH";
    private const string ExportLabelEnv = "T201_API_EXPORT_LABEL";
    private const string DefaultExportRelative = "artifacts/pdca/T201/rv1/api/baseline.json";

    /// <summary>Simple names of the NextORM assemblies this test project has a compile reference to.</summary>
    private static readonly string[] NextormAssemblies =
    [
        "nextorm.core",
        "nextorm.postgres",
        "nextorm.sqlserver",
        "nextorm.clickhouse",
    ];

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "nextorm.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static string ExportPath()
    {
        var configured = Environment.GetEnvironmentVariable(ExportPathEnv);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var value = configured!;
            return Path.IsPathRooted(value) ? value : Path.Combine(RepoRoot(), value);
        }

        var preferred = Path.Combine(RepoRoot(), DefaultExportRelative);
        // A captured export (written by the evidence wrapper and carrying the tree identity of the
        // pre-production tree) must never be clobbered by a bare `dotnet test` run of this file: the
        // wrapper's `--stage api` would then re-capture it *after* production edits and silently
        // replace the baseline.  Fall back to a side artefact instead.
        if (File.Exists(preferred)
            && File.ReadAllText(preferred).Contains("\"capture\"", StringComparison.Ordinal))
        {
            return Path.Combine(
                Path.GetDirectoryName(preferred)!, "baseline-unconfigured-run.json");
        }
        return preferred;
    }

    private static string ExportLabel()
    {
        var configured = Environment.GetEnvironmentVariable(ExportLabelEnv);
        return string.IsNullOrWhiteSpace(configured) ? "baseline" : configured!;
    }

    // -------------------------------------------------------------------------------------------
    // Inventory construction (compiler metadata, deterministic formatting)
    // -------------------------------------------------------------------------------------------

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
            return FormatType(type.GetElementType()!) + "&";
        if (type.IsPointer)
            return FormatType(type.GetElementType()!) + "*";
        if (type.IsArray)
            return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter)
            return type.Name;
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = (definition.FullName ?? definition.Name).Split('`')[0];
            var arguments = string.Join(", ", type.GetGenericArguments().Select(FormatType));
            return name + "<" + arguments + ">";
        }
        return type.FullName ?? type.Name;
    }

    private static string? Visibility(MethodBase method) => method switch
    {
        { IsPublic: true } => "public",
        { IsFamily: true } => "protected",
        { IsFamilyOrAssembly: true } => "protected internal",
        _ => null,
    };

    private static string? Visibility(FieldInfo field) => field switch
    {
        { IsPublic: true } => "public",
        { IsFamily: true } => "protected",
        { IsFamilyOrAssembly: true } => "protected internal",
        _ => null,
    };

    private static string FormatParameter(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        var byRef = type.IsByRef;
        var core = byRef ? type.GetElementType()! : type;
        var modifier = parameter.IsOut ? "out " : byRef ? "ref " : "";
        var optional = parameter.HasDefaultValue ? " = default" : "";
        return modifier + FormatType(core) + " " + (parameter.Name ?? "arg") + optional;
    }

    private static string FormatMethod(MethodInfo method)
    {
        var modifiers = new StringBuilder();
        if (method.IsStatic)
            modifiers.Append("static ");
        if (method.IsAbstract)
            modifiers.Append("abstract ");
        else if (method.IsVirtual && !method.IsFinal)
            modifiers.Append("virtual ");
        var generic = method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">"
            : "";
        var parameters = string.Join(", ", method.GetParameters().Select(FormatParameter));
        return $"{Visibility(method)} method {modifiers}{FormatType(method.ReturnType)} {method.Name}{generic}({parameters})";
    }

    private static ApiType BuildTypeInventory(Type type)
    {
        var members = new SortedSet<string>(StringComparer.Ordinal);
        var accessors = new HashSet<MethodInfo>();

        foreach (var property in type.GetProperties(DeclaredMembers))
        {
            var getter = property.GetMethod;
            var setter = property.SetMethod;
            if (getter is not null)
                accessors.Add(getter);
            if (setter is not null)
                accessors.Add(setter);
            if (getter is null || Visibility(getter) is null)
                continue;
            var access = getter.IsPublic ? "get;" : "";
            if (setter is not null && setter.IsPublic)
                access += " set;";
            var indexParameters = property.GetIndexParameters();
            var name = indexParameters.Length == 0
                ? property.Name
                : "this[" + string.Join(", ", indexParameters.Select(parameter => FormatType(parameter.ParameterType))) + "]";
            members.Add($"property {name} : {FormatType(property.PropertyType)} {{ {access.Trim()} }}");
        }

        foreach (var eventInfo in type.GetEvents(DeclaredMembers))
        {
            var adder = eventInfo.AddMethod;
            var remover = eventInfo.RemoveMethod;
            if (adder is not null)
                accessors.Add(adder);
            if (remover is not null)
                accessors.Add(remover);
            if (adder is null || Visibility(adder) is null || eventInfo.EventHandlerType is null)
                continue;
            members.Add($"event {FormatType(eventInfo.EventHandlerType)} {eventInfo.Name}");
        }

        foreach (var field in type.GetFields(DeclaredMembers))
        {
            if (Visibility(field) is null)
                continue;
            var modifiers = field.IsLiteral ? "const " : field.IsInitOnly ? "readonly " : field.IsStatic ? "static " : "";
            members.Add($"field {modifiers}{FormatType(field.FieldType)} {field.Name}");
        }

        foreach (var constructor in type.GetConstructors(DeclaredMembers))
        {
            if (Visibility(constructor) is null)
                continue;
            var parameters = string.Join(", ", constructor.GetParameters().Select(FormatParameter));
            members.Add($"{Visibility(constructor)} ctor({parameters})");
        }

        foreach (var method in type.GetMethods(DeclaredMembers))
        {
            if (accessors.Contains(method) || Visibility(method) is null)
                continue;
            members.Add(FormatMethod(method));
        }

        var typeKind = type.IsEnum
            ? "enum"
            : type.IsValueType
                ? "struct"
                : type.IsInterface
                    ? "interface"
                    : typeof(Delegate).IsAssignableFrom(type)
                        ? "delegate"
                        : "class";
        var typeModifiers = new List<string> { "public" };
        if (type.IsAbstract && type.IsSealed)
            typeModifiers.Add("static");
        else
        {
            if (type.IsAbstract)
                typeModifiers.Add("abstract");
            if (type.IsSealed)
                typeModifiers.Add("sealed");
            if (type.IsValueType)
                typeModifiers.Add("readonly-if-annotated");
        }

        return new ApiType(
            type.FullName ?? type.Name,
            typeKind,
            string.Join(" ", typeModifiers),
            type.BaseType is null ? null : FormatType(type.BaseType),
            type.GetInterfaces().Select(FormatType).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            members.ToArray());
    }

    private static ApiAssembly BuildAssemblyInventory(string simpleName)
    {
        // Presence is decided by the emitted artifact next to the test assembly, so an assembly that
        // this project does not reference is recorded as absent instead of failing the export.
        var artifact = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");
        if (!File.Exists(artifact))
            return new ApiAssembly(simpleName, null, false, 0, []);

        var assembly = Assembly.Load(new AssemblyName(simpleName));
        var types = assembly.GetExportedTypes()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(BuildTypeInventory)
            .ToArray();
        return new ApiAssembly(
            assembly.GetName().Name ?? simpleName,
            assembly.GetName().Version?.ToString() ?? "unknown",
            true,
            types.Length,
            types);
    }

    private static ApiInventory BuildInventory() => new(
        "T201",
        "compiler-metadata-public-surface",
        ExportLabel(),
        NextormAssemblies,
        NextormAssemblies.Select(BuildAssemblyInventory).ToArray());

    /// <summary>Canonical (compact) inventory JSON plus the sha256 digest of exactly that text.</summary>
    private static (string Canonical, string Digest) CanonicalInventory()
    {
        var canonical = JsonSerializer.Serialize(BuildInventory(), CanonicalOptions);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return (canonical, digest);
    }

    /// <summary>The exported document: canonical inventory plus the trailing <c>sha256</c> field.</summary>
    private static string ExportJson()
    {
        var (canonical, digest) = CanonicalInventory();
        using var document = JsonDocument.Parse(canonical);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
                property.WriteTo(writer);
            writer.WriteString("sha256", digest);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // -------------------------------------------------------------------------------------------
    // Facts
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Public_surface_inventory_is_deterministic()
    {
        var (first, firstDigest) = CanonicalInventory();
        var (second, secondDigest) = CanonicalInventory();

        firstDigest.Should().Be(secondDigest);
        second.Should().Be(first);
    }

    [Fact]
    public void Public_surface_inventory_is_exported_and_nonempty()
    {
        var path = ExportPath();
        var text = ExportJson();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text + Environment.NewLine, new UTF8Encoding(false));

        File.Exists(path).Should().BeTrue();
        File.ReadAllText(path).Should().Be(text + Environment.NewLine);

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        root.GetProperty("task").GetString().Should().Be("T201");
        root.GetProperty("kind").GetString().Should().Be("compiler-metadata-public-surface");
        root.GetProperty("label").GetString().Should().Be(ExportLabel());
        root.GetProperty("sha256").GetString().Should().HaveLength(64);

        var presentTypes = 0;
        foreach (var assembly in root.GetProperty("assemblies").EnumerateArray())
        {
            if (!assembly.GetProperty("present").GetBoolean())
                continue;
            assembly.GetProperty("public_types").EnumerateArray().Should().NotBeEmpty();
            presentTypes += assembly.GetProperty("public_type_count").GetInt32();
        }

        presentTypes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Public_surface_inventory_covers_the_referenced_nextorm_assemblies()
    {
        var (canonical, _) = CanonicalInventory();
        using var document = JsonDocument.Parse(canonical);
        var names = document.RootElement.GetProperty("assemblies")
            .EnumerateArray()
            .ToDictionary(
                assembly => assembly.GetProperty("name").GetString()!,
                assembly => assembly.GetProperty("present").GetBoolean());

        foreach (var expected in NextormAssemblies)
        {
            names.Should().ContainKey(expected);
            names[expected].Should().BeTrue($"the test project compiles against {expected}");
        }
    }

    // -------------------------------------------------------------------------------------------
    // T201 D201-3 — specialized top-level preparation reuse (differential + cache-lifetime cases)
    // -------------------------------------------------------------------------------------------

    private const string SqlServerPlaceholder =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static SqlServerDataContext CreateSqlServerContext()
        => new(SqlServerPlaceholder, new DataContextBuilder());

    /// <summary>Builds a placeholder-connection context. No connection is ever opened.</summary>
    private static string PrepareSql<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache = true)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> PrepareCommand<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache = true)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static void ResetRecipeCache()
    {
        QueryPlanStore.Clear();
        QueryPlanner.ResetCounters();
    }

    // --- shape builders (runtime-equivalent forms of the five approved Category B arms) -------------

    private static QueryCommand<int> SimpleShape(IDataContext ctx)
        => ctx.From<SimpleEntity>().Where(x => x.Id > 10).Select(x => x.Id);

    /// <summary>B_Cte arm shape: a CTE read by name joined to an entity.</summary>
    private static QueryCommand<int> CteShape(IDataContext ctx)
    {
        var recent = ctx.From<SimpleEntity>().Where(c => c.Id > 1).Select(c => new { c.Id });
        return ctx.With("recent", recent)
            .From("recent")
            .Join(ctx.From<SimpleEntity>(), (r, s) => r["id"].AsInt == s.Id)
            .Select(p => p.Item1["id"].AsInt);
    }

    /// <summary>B_RecursiveCte arm shape: a recursive CTE over anchor UNION ALL step.</summary>
    private static QueryCommand<int> RecursiveCteShape(IDataContext ctx)
    {
        var anchor = ctx.From<SimpleEntity>().Where(s => s.Id == 1).Select(s => s.Id);
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => t["n"].AsInt + 1);
        return ctx.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
    }

    private static QueryCommand<int> SetOperationShape(IDataContext ctx, bool except)
    {
        var left = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var right = ctx.From<SimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id);
        return except ? left.Except(right) : left.Intersect(right);
    }

    private static QueryCommand<int> DistinctShape(IDataContext ctx)
        => ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id).Distinct();

    // --- 1. RawDescriptorDifferential ----------------------------------------------------------------

    [Fact]
    public void Recipe_BareEntitySelect_IsNotProbed()
    {
        using var ctx = CreateSqlServerContext();

        // A bare projection has no preparation work worth a raw descriptor: probing it would allocate
        // the shape on every fresh-command execution (the Iteration-list regression) and reuse less
        // preparation than the descriptor costs.
        QueryCommand.CaptureRawShape(ctx.From<SimpleEntity>().ToCommand()).Should().BeNull(
            "a bare entity select has no non-trivial preparation to reuse");

        // A condition keeps the recipe: the descriptor can skip real preparation work.
        QueryCommand.CaptureRawShape(SimpleShape(ctx)).Should().NotBeNull();

        // Paging is a real clause, so the shape is not trivial.
        QueryCommand.CaptureRawShape(ctx.From<SimpleEntity>().Page(10, 0).ToCommand()).Should().NotBeNull();
    }

    [Fact]
    public void Recipe_RawDescriptor_DistinguishesTheSevenSettingsAndRawClauses()
    {
        using var ctx = CreateSqlServerContext();

        var baseline = QueryCommand.CaptureRawShape(SimpleShape(ctx));
        baseline.Should().NotBeNull();

        QueryCommand.CaptureRawShape(SimpleShape(ctx))!.Matches(baseline!, SimpleShape(ctx)).Should().BeTrue();

        // Each of the seven raw settings whose Resolved* counterpart an apply overwrites must be part
        // of the descriptor; the post-apply key check cannot see them.
        QueryCommand.CaptureRawShape(SimpleShape(ctx).WithCommandTimeout(42))!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();
        QueryCommand.CaptureRawShape(SimpleShape(ctx).WithQuotedIdentifiers())!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();
        QueryCommand.CaptureRawShape(SimpleShape(ctx).WithKeywordCase(KeywordCase.Lower))!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();
        QueryCommand.CaptureRawShape(SimpleShape(ctx).WithNamingConvention(new SnakeCaseNamingConvention()))!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();

        // SQL-affecting raw clauses: a different literal is a different raw graph.
        QueryCommand.CaptureRawShape(ctx.From<SimpleEntity>().Where(x => x.Id > 11).Select(x => x.Id))!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();
        QueryCommand.CaptureRawShape(DistinctShape(ctx))!
            .Matches(baseline!, SimpleShape(ctx)).Should().BeFalse();
    }

    /// <summary>
    /// The negative case for <see cref="QueryPlanEqualityComparer"/>: it compares only the prepared
    /// projection/condition/grouping forms, so prepared-form equality can never substitute for raw
    /// verification. Here the raw descriptor decides, and the recipe must not reuse across a raw
    /// difference even though the two commands differ only in a raw lambda.
    /// </summary>
    [Fact]
    public void Recipe_PreparedFormEqualityCannotMaskARawDifference()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        QueryPlanner.RecipeReuses.Should().Be(0);

        // Same prepared shape (same projection/columns), different raw predicate: no hit, and the SQL differs.
        var other = ctx.From<SimpleEntity>().Where(x => x.Id > 11).Select(x => x.Id);
        var second = PrepareSql(ctx, other);

        QueryPlanner.RecipeReuses.Should().Be(0);
        second.Should().NotBe(first);
        second.Should().Contain("11");
    }

    [Fact]
    public void Recipe_MissThenHit_ReusesPreparationForAnEquivalentFreshCommand()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        QueryPlanner.RecipeReuses.Should().Be(0);

        var probeShape = SimpleShape(ctx);
        var storedShape = QueryCommand.CaptureRawShape(SimpleShape(ctx));
        var probedShape = QueryCommand.CaptureRawShape(probeShape);
        probedShape!.Fingerprint.Should().Be(storedShape!.Fingerprint);
        probedShape.Matches(storedShape, probeShape).Should().BeTrue();

        var second = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipeApplyMismatches.Should().Be(0);
        QueryPlanner.RecipeReuses.Should().Be(1);
        second.Should().Be(first);
    }

    /// <summary>
    /// Two same-shaped commands over DIFFERENT named raw sources must never share a plan through the
    /// raw index: the source identity is part of the descriptor and the source binding/table must be
    /// compared explicitly, not inferred from the (overwritten) prepared state.
    /// </summary>
    [Fact]
    public void Recipe_DifferentNamedSourcesAreDistinguished()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var a1 = PrepareSql(ctx, ctx.From("t201_recipe_a").Select(x => x["id"].AsInt));
        QueryPlanner.RecipePublishes.Should().Be(1);

        var b1 = PrepareSql(ctx, ctx.From("t201_recipe_b").Select(x => x["id"].AsInt));
        QueryPlanner.RecipeReuses.Should().Be(0, "a different named source is a different raw graph");
        b1.Should().NotBe(a1);
        b1.Should().Contain("t201_recipe_b");
        a1.Should().Contain("t201_recipe_a");

        // ...and the same source still reuses.
        PrepareSql(ctx, ctx.From("t201_recipe_a").Select(x => x["id"].AsInt));
        QueryPlanner.RecipeReuses.Should().Be(1);
    }

    // --- 2. CurrentGraphBinding ----------------------------------------------------------------------

    [Fact]
    public void Recipe_WholeGraphCte_ReusesAndMatchesOrdinaryPreparation()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, CteShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);

        var second = PrepareSql(ctx, CteShape(ctx));
        QueryPlanner.RecipeReuses.Should().Be(1);
        second.Should().Be(first);
        second.Should().Contain("recent");
        second.Should().StartWith("with");

        // Differential against ordinary preparation of the same shape (no cache).
        using var fresh = CreateSqlServerContext();
        var ordinary = PrepareSql(fresh, CteShape(fresh), storeInCache: false);
        ordinary.Should().Be(second);
    }

    [Fact]
    public void Recipe_RecursiveCte_ReusesAndMatchesOrdinaryPreparation()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, RecursiveCteShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);

        var second = PrepareSql(ctx, RecursiveCteShape(ctx));
        QueryPlanner.RecipeReuses.Should().Be(1);
        second.Should().Be(first);
        first.Should().Contain("nums");
    }

    [Fact]
    public void Recipe_SetOperationKindIsPartOfTheRawGraph()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var except = PrepareSql(ctx, SetOperationShape(ctx, except: true));
        QueryPlanner.RecipePublishes.Should().Be(1);

        // Same operands, same order, different set-operation kind: no reuse, different SQL.
        var intersect = PrepareSql(ctx, SetOperationShape(ctx, except: false));
        QueryPlanner.RecipeReuses.Should().Be(0);
        intersect.Should().NotBe(except);

        // Operand order matters too.
        var reversed = PrepareSql(ctx, ReverseSetOperation(ctx));
        QueryPlanner.RecipeReuses.Should().Be(0);
        reversed.Should().NotBe(except);
    }

    private static QueryCommand<int> ReverseSetOperation(IDataContext ctx)
    {
        var left = ctx.From<SimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id);
        var right = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        return left.Except(right);
    }

    [Fact]
    public void Recipe_SubquerySourceAndForeignNestedCommandsFallBackWithoutEffects()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var inner = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });
        var fromSubquery = ctx.From(inner).Select(t => t.Id);

        var sql = PrepareSql(ctx, fromSubquery);
        sql.Should().NotBeNullOrWhiteSpace();
        QueryCommand.CaptureRawShape(fromSubquery).Should().BeNull("a derived-table source has no provable raw identity");
        QueryPlanner.RecipePublishes.Should().Be(0);
        QueryPlanner.RecipeReuses.Should().Be(0);
    }

    // --- 3. FreshClosureValues -----------------------------------------------------------------------

    [Fact]
    public void Recipe_ChangedClosureValuesAreFresh_NoStaleCapture()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var threshold = 10;
        var first = PrepareCommand(ctx, ctx.From<SimpleEntity>().Where(x => x.Id > threshold).Select(x => x.Id));
        first.DbCommand.Parameters[0].Value.Should().Be(10);

        // A closure-bearing shape is ineligible: the captured value can never be served from a
        // value-independent descriptor, so it keeps the ordinary route and binds the fresh value.
        QueryPlanner.RecipePublishes.Should().Be(0);
        QueryPlanner.RecipeReuses.Should().Be(0);
        QueryCommand.CaptureRawShape(ctx.From<SimpleEntity>().Where(x => x.Id > threshold).Select(x => x.Id))
            .Should().BeNull("a captured member access is not a provable raw identity");

        threshold = 99;
        var second = PrepareCommand(ctx, ctx.From<SimpleEntity>().Where(x => x.Id > threshold).Select(x => x.Id));
        second.DbCommand.Parameters[0].Value.Should().Be(99);
        QueryPlanner.RecipeReuses.Should().Be(0);
    }

    // --- 4. lifetime: Clear and thread separation -----------------------------------------------------

    [Fact]
    public void Recipe_ClearDropsTheRawIndex()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var first = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipeReuses.Should().Be(1, "the second equivalent command reuses");

        // Clear must drop both dictionaries under the one generation transition: no usable alias remains.
        QueryPlanStore.Clear();
        QueryPlanner.ResetCounters();

        var afterClear = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipeReuses.Should().Be(0);
        afterClear.Should().Be(first);
    }

    [Fact]
    public void Recipe_ThreadSeparation_NoCrossThreadReuse()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var onThisThread = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);

        string? onOtherThread = null;
        var thread = new Thread(() => onOtherThread = PrepareSql(ctx, SimpleShape(ctx)));
        thread.Start();
        thread.Join();

        onOtherThread.Should().Be(onThisThread);
        QueryPlanner.RecipeReuses.Should().Be(0, "the raw index is thread-local");
        QueryPlanner.RecipePublishes.Should().Be(2, "the other thread published in its own domain");
    }

    // --- shared AnyCommand route + later cacheable hits ----------------------------------------------

    [Fact]
    public void Recipe_SharedAnyCommandInstanceIsExcluded_AndLaterCacheableQueriesStillHit()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var predicate = ctx.From<SimpleEntity>().Where(x => x.Id > 1).Select(x => x.Id);
        var shared = EntityBuilderExtensions.GetAnyCommand(ctx, predicate);
        shared.Should().NotBeNull();

        PrepareSql(ctx, shared);
        QueryPlanner.RecipePublishes.Should().Be(0, "the actual shared instance is excluded by identity");
        QueryPlanner.RecipeReuses.Should().Be(0);

        // A later ordinary cacheable query is unaffected.
        var first = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        var second = PrepareSql(ctx, SimpleShape(ctx));
        QueryPlanner.RecipeReuses.Should().Be(1);
        second.Should().Be(first);
    }

    // --- 5. the five Category B arm families are eligible and reuse -----------------------------------

    [Fact]
    public void Recipe_AllFiveCategoryBArmShapesAreEligibleAndReuse()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        (string Name, Func<IDataContext, QueryCommand<int>> Build)[] arms =
        [
            ("Cte", CteShape),
            ("RecursiveCte", RecursiveCteShape),
            ("Except", c => SetOperationShape(c, except: true)),
            ("Intersect", c => SetOperationShape(c, except: false)),
            ("Distinct", DistinctShape),
        ];

        foreach (var (name, build) in arms)
        {
            ResetRecipeCache();
            using var armContext = CreateSqlServerContext();

            QueryCommand.CaptureRawShape(build(armContext)).Should().NotBeNull($"{name} must be an eligible raw graph");

            var first = PrepareSql(armContext, build(armContext));
            QueryPlanner.RecipePublishes.Should().Be(1, $"{name} publishes a candidate");

            var second = PrepareSql(armContext, build(armContext));
            QueryPlanner.RecipeReuses.Should().Be(1, $"{name} actually uses the mechanism");
            second.Should().Be(first, $"{name} is differential-equal to its ordinary preparation");
        }
    }

    // -------------------------------------------------------------------------------------------
    // CCORR-201 D01 — a global/builder query filter must never be transferred across contexts by the
    // raw pre-preparation reuse probe (the filter is injected only at preparation and is absent from the
    // value-free raw descriptor). All cases are differential against ordinary preparation (storeInCache
    // false), the design's oracle, and check the raw index directly through the planner counters.
    // -------------------------------------------------------------------------------------------

    private const string RecipeFilterKey = "ccorr201_recipe_tenant";

    /// <summary>Filtered under a context-dependent builder-function (the context property selects the predicate).</summary>
    [SqlTable("ccorr201_ctx_filtered")]
    private sealed class RecipeCtxEntity
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }
    }

    /// <summary>A plain, single-property filtered entity for the scope and nested-source cases.</summary>
    [SqlTable("ccorr201_scope_filtered")]
    private sealed class RecipeScopeEntity
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }
    }

    /// <summary>An unfiltered entity used as the main/left source in the nested-source cases.</summary>
    [SqlTable("ccorr201_plain")]
    private sealed class RecipePlainEntity
    {
        [Key]
        public int Id { get; set; }
    }

    /// <summary>A dedicated entity for the filter-invocation counter (its own metadata registration).</summary>
    [SqlTable("ccorr201_counter_filtered")]
    private sealed class RecipeCounterEntity
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }
    }

    private int _ctxFilterInvocations;

    private static SqlServerDataContext CreateRecipeContext(int tenant, Action<IDataContext>? register = null)
    {
        var ctx = new SqlServerDataContext(SqlServerPlaceholder, new DataContextBuilder());
        ctx.Properties[RecipeFilterKey] = tenant;
        register?.Invoke(ctx);
        return ctx;
    }

    /// <summary>A context-dependent builder filter: property 1 selects a permissive predicate, otherwise a restrictive one.</summary>
    private static void RegisterContextFilter(IDataContext ctx)
        => ctx.From<RecipeCtxEntity>(b => b.HasQueryFilter("ccorr201-ctx", (eb, x) => (int)x.Properties[RecipeFilterKey] == 1
            ? eb.Where(e => e.TenantId > 0)
            : eb.Where(e => e.TenantId == 2)));

    private static void RegisterScopeFilter(IDataContext ctx)
        => ctx.From<RecipeScopeEntity>(b => b.HasQueryFilter("tenant", (e, _) => e.TenantId == 1));

    private static QueryCommand<int> CtxShape(IDataContext ctx)
        => ctx.From<RecipeCtxEntity>().Where(x => x.Id > 10).Select(x => x.Id);

    private static QueryCommand<int> ScopeShape(IDataContext ctx, bool all = false, string? disabledKey = null)
    {
        var builder = ctx.From<RecipeScopeEntity>().Where(x => x.Id > 10);
        if (all)
            builder = builder.IgnoreFilters();
        else if (disabledKey is not null)
            builder = builder.IgnoreFilters([disabledKey]);
        return builder.Select(x => x.Id);
    }

    /// <summary>Renders the prepared command's SQL plus its bound parameter values, so a stale predicate is observable.</summary>
    private static string DescribePrepared<T>(DbPreparedQueryCommand<T> command)
    {
        var text = command.DbCommand.CommandText.Replace("\r\n", "\n");
        var parameters = string.Join(", ", command.DbCommand.Parameters.Cast<DbParameter>()
            .Select(p => $"{p.ParameterName}={p.Value}"));
        return text + " || " + parameters;
    }

    /// <summary>
    /// AC01 (permissive -> restrictive). Context 1 resolves a permissive filter and publishes a raw
    /// candidate; context 2 resolves a restrictive filter for the SAME raw shape. The raw probe must not
    /// hand context 2 the permissive predicate prepared for context 1.
    /// </summary>
    [Fact]
    public void Recipe_ContextDependentFilterIsNotReused_PermissiveThenRestrictive()
    {
        ResetRecipeCache();

        using var permissive = CreateRecipeContext(1, RegisterContextFilter);
        PrepareCommand(permissive, CtxShape(permissive));

        using var restrictive = CreateRecipeContext(2, RegisterContextFilter);
        var reused = PrepareCommand(restrictive, CtxShape(restrictive));
        var oracle = PrepareCommand(restrictive, CtxShape(restrictive), storeInCache: false);

        QueryPlanner.RecipeReuses.Should().Be(0, "an applicable context-dependent filter makes the raw graph ineligible for reuse");
        DescribePrepared(reused).Should().Be(DescribePrepared(oracle),
            "the second context must resolve its own filter, never a stale predicate prepared for another context");
        DescribePrepared(reused).Should().Contain("TenantId = 2", "the restrictive context must not inherit the permissive predicate");
        DescribePrepared(reused).Should().NotContain("TenantId > 0");
    }

    /// <summary>AC01 (reverse ordering: restrictive -> permissive).</summary>
    [Fact]
    public void Recipe_ContextDependentFilterIsNotReused_RestrictiveThenPermissive()
    {
        ResetRecipeCache();

        using var restrictive = CreateRecipeContext(2, RegisterContextFilter);
        PrepareCommand(restrictive, CtxShape(restrictive));

        using var permissive = CreateRecipeContext(1, RegisterContextFilter);
        var reused = PrepareCommand(permissive, CtxShape(permissive));
        var oracle = PrepareCommand(permissive, CtxShape(permissive), storeInCache: false);

        QueryPlanner.RecipeReuses.Should().Be(0, "an applicable context-dependent filter makes the raw graph ineligible for reuse");
        DescribePrepared(reused).Should().Be(DescribePrepared(oracle));
        DescribePrepared(reused).Should().Contain("TenantId > 0", "the permissive context must not inherit the restrictive predicate");
        DescribePrepared(reused).Should().NotContain("TenantId = 2");
    }

    /// <summary>
    /// AC03. A disabled filter is not applicable metadata: <c>IgnoreFilters()</c> (scope All) is
    /// filter-free and keeps the raw publish/reuse, and a selective named disable is likewise accepted by
    /// the eligibility guard (it is not counted as applicable metadata and produces the unfiltered SQL).
    /// A plain active filter, by contrast, is ineligible.
    /// </summary>
    /// <remarks>
    /// The selective named disable does not additionally produce a raw <i>reuse</i>: each
    /// <c>IgnoreFilters([key])</c> call builds a fresh <see cref="QueryFilterScope"/> instance and
    /// <c>RawShape.FilterScopeEqual</c> compares scopes with <c>Equals</c>, which is reference identity
    /// for the (equality-less) scope type. So the two fresh commands are guard-eligible but their raw
    /// descriptors do not match; the second preparation is served by the ordinary normalized plan cache.
    /// This is measured behaviour, not a guard rejection — <c>RecipeRejections</c> stays 0.
    /// </remarks>
    [Fact]
    public void Recipe_IgnoredOrSelectivelyDisabledFilters_KeepUnfilteredReuse()
    {
        ResetRecipeCache();
        using var ctx = CreateRecipeContext(1, RegisterScopeFilter);

        // scope All: every filter is disabled, so the graph is filter-free and stays raw-eligible.
        // IgnoreFilters() returns the QueryFilterScope.AllFilters singleton, so both commands share it.
        var allFirst = PrepareSql(ctx, ScopeShape(ctx, all: true));
        QueryPlanner.RecipePublishes.Should().Be(1, "an all-disabled scope leaves the graph raw-eligible");
        var allSecond = PrepareSql(ctx, ScopeShape(ctx, all: true));
        QueryPlanner.RecipeReuses.Should().Be(1);
        allSecond.Should().Be(allFirst);
        allSecond.Should().NotContain("TenantId", "IgnoreFilters() must leave the predicate out");

        // selective named disable: the only filter is dropped, so the graph is eligible (guard does not
        // fire) and unfiltered; no raw reuse, because the per-command scope instances are not reference
        // equal (measured: fpEq=True, Matches=False, guard=False — artifacts/pdca/CCORR-201/rv1/E/e03-named-scope-diag.log).
        ResetRecipeCache();
        var namedFirst = PrepareSql(ctx, ScopeShape(ctx, disabledKey: "tenant"));
        QueryPlanner.RecipePublishes.Should().Be(1, "a selectively disabled filter leaves the graph raw-eligible");
        var namedSecond = PrepareSql(ctx, ScopeShape(ctx, disabledKey: "tenant"));
        QueryPlanner.RecipeRejections.Should().Be(0, "the eligibility guard must not fire for a disabled filter");
        QueryPlanner.RecipeReuses.Should().Be(0, "distinct scope instances make the raw descriptors unequal");
        QueryPlanner.RecipePublishes.Should().Be(1, "the second preparation is served by the ordinary plan cache, not republished");
        namedSecond.Should().Be(namedFirst);
        namedSecond.Should().NotContain("TenantId");

        // control: with the filter active the graph is ineligible.
        ResetRecipeCache();
        PrepareSql(ctx, ScopeShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(0);
        QueryPlanner.RecipeReuses.Should().Be(0);
    }

    /// <summary>
    /// AC02 negative. A filter that applies only to a JOIN, a CTE body or a set-operation operand still
    /// makes the whole graph ineligible — it must not be bypassed by the probe.
    /// </summary>
    [Fact]
    public void Recipe_FilterOnlyInJoinCteOrUnionSource_IsNotBypassed()
    {
        using var ctx = CreateRecipeContext(1, RegisterScopeFilter);

        ResetRecipeCache();
        var joinShape = ctx.From<RecipePlainEntity>()
            .Join(ctx.From<RecipeScopeEntity>(), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id);
        PrepareSql(ctx, joinShape);
        QueryPlanner.RecipePublishes.Should().Be(0, "a filter on a joined source must not be bypassed");
        QueryPlanner.RecipeReuses.Should().Be(0);

        ResetRecipeCache();
        var cteBody = ctx.From<RecipeScopeEntity>().Where(x => x.Id > 1).Select(x => new { x.Id });
        var cteShape = ctx.With("ccorr201_recipe_cte", cteBody)
            .From("ccorr201_recipe_cte")
            .Select(t => t["id"].AsInt);
        PrepareSql(ctx, cteShape);
        QueryPlanner.RecipePublishes.Should().Be(0, "a filter inside a CTE body must not be bypassed");
        QueryPlanner.RecipeReuses.Should().Be(0);

        ResetRecipeCache();
        var unionShape = ctx.From<RecipePlainEntity>().Select(x => x.Id)
            .Union(ctx.From<RecipeScopeEntity>().Select(x => x.Id));
        PrepareSql(ctx, unionShape);
        QueryPlanner.RecipePublishes.Should().Be(0, "a filter on a set-operation operand must not be bypassed");
        QueryPlanner.RecipeReuses.Should().Be(0);
    }

    /// <summary>
    /// AC02. The eligibility probe is metadata-only: it must not invoke the builder-function filter. The
    /// invocation count of two cached preparations must equal an independent ordinary-preparation
    /// reference, and raw reuse must be skipped.
    /// </summary>
    [Fact]
    public void Recipe_FilterProbeDoesNotInvokeTheUserFunction()
    {
        ResetRecipeCache();
        Volatile.Write(ref _ctxFilterInvocations, 0);
        using (var ctx = CreateRecipeContext(1, RegisterCountingFilter))
        {
            PrepareCommand(ctx, CounterShape(ctx));
            PrepareCommand(ctx, CounterShape(ctx));
        }
        var cached = Volatile.Read(ref _ctxFilterInvocations);

        ResetRecipeCache();
        Volatile.Write(ref _ctxFilterInvocations, 0);
        using (var fresh = CreateRecipeContext(1, RegisterCountingFilter))
        {
            PrepareCommand(fresh, CounterShape(fresh), storeInCache: false);
            PrepareCommand(fresh, CounterShape(fresh), storeInCache: false);
        }
        var ordinary = Volatile.Read(ref _ctxFilterInvocations);

        QueryPlanner.RecipeReuses.Should().Be(0, "the filtered graph is ineligible for raw reuse");
        cached.Should().Be(2, "the filter function runs exactly once per preparation, never for the probe");
        cached.Should().Be(ordinary, "the eligibility probe must not add an invocation over ordinary preparation");
    }

    private void RegisterCountingFilter(IDataContext ctx)
        => ctx.From<RecipeCounterEntity>(b => b.HasQueryFilter("ccorr201-count", (eb, x) =>
        {
            Interlocked.Increment(ref _ctxFilterInvocations);
            return (int)x.Properties[RecipeFilterKey] == 1 ? eb.Where(e => e.TenantId == 1) : eb.Where(e => e.Id == 1);
        }));

    private static QueryCommand<int> CounterShape(IDataContext ctx)
        => ctx.From<RecipeCounterEntity>().Where(x => x.Id > 10).Select(x => x.Id);

    // --- D01c fail-closed guard: an indeterminable entity type; a raw TableAlias stays eligible ------

    /// <summary>A named raw (untyped) source — read through TableAlias accessors.</summary>
    private static QueryCommand<int> GuardRawShape(IDataContext ctx)
        => ctx.From("ccorr201_guard_raw").Select(x => x["id"].AsInt);

    /// <summary>
    /// CCORR-201 D01c. The raw-preparation eligibility guard is defined fail-closed: a source whose
    /// effective entity type cannot be determined must be reported as filter-bearing, so
    /// <c>QueryFilterResolver.GetFilters(null, …)</c> returning an empty list must never be read as
    /// "no filters". The synthetic raw <c>TableAlias</c> shortcut, by contrast, is a proven filter-free
    /// shape (it resolves no metadata by design, <c>QueryFilterResolver.cs:33-37</c>) and must keep its
    /// unfiltered raw publish/reuse.
    /// </summary>
    /// <remarks>
    /// Reachability is measured, not assumed: on a fresh command <c>_srcType</c> is always <c>null</c>
    /// (it is only assigned during preparation), so <c>RawShape.EntityType</c> is <c>null</c> for every
    /// captured graph and the effective type is taken from the projection parameter — which is always a
    /// concrete type through the public builder surface. The null-entity arm of the guard is therefore a
    /// defensive (fail-closed) arm; it is closed on the captured descriptor directly. Its planner-level
    /// consequence — the raw path is skipped and <c>RecipeRejections</c> is incremented instead of a
    /// publish/reuse — is the branch at <c>QueryPlanner.cs:627-631</c>.
    /// </remarks>
    [Fact]
    public void Recipe_IndeterminableEntityTypeIsFailClosed_RawTableAliasStaysEligible()
    {
        ResetRecipeCache();
        using var ctx = CreateRecipeContext(1, RegisterScopeFilter);

        // The raw named source captures as an eligible graph whose effective type is the synthetic
        // TableAlias: the guard reports no applicable filter (`false` path).
        var rawShape = QueryCommand.CaptureRawShape(GuardRawShape(ctx));
        rawShape.Should().NotBeNull("a named raw source is a describable raw graph");
        rawShape!.EntityType.Should().BeNull("a fresh command carries no source type before preparation");
        QueryCommand.HasApplicableFiltersInRawGraph(rawShape, ctx).Should().BeFalse(
            "a raw TableAlias source resolves no entity metadata and therefore no filters");

        // End-to-end: the unfiltered reuse is retained (publish then reuse) and the SQL is unchanged.
        var first = PrepareSql(ctx, GuardRawShape(ctx));
        QueryPlanner.RecipePublishes.Should().Be(1);
        var second = PrepareSql(ctx, GuardRawShape(ctx));
        QueryPlanner.RecipeRejections.Should().Be(0, "the eligibility guard must not fire for a raw TableAlias source");
        QueryPlanner.RecipeReuses.Should().Be(1, "an unfiltered raw TableAlias source keeps its reuse");
        second.Should().Be(first);
        second.Should().Contain("ccorr201_guard_raw");

        // Fail-closed arm: with no determinable effective entity type the graph is reported
        // filter-bearing, so the planner skips the raw path entirely and the ordinary preparation route
        // decides the semantics — never an optimistic reuse of a value-free descriptor.
        var indeterminable = QueryCommand.CaptureRawShape(GuardRawShape(ctx))!;
        indeterminable.EntityType = null;
        indeterminable.Exp = null;
        QueryCommand.HasApplicableFiltersInRawGraph(indeterminable, ctx).Should().BeTrue(
            "an entity type that cannot be determined must be treated as filter-bearing (fail closed)");

        // The same query still prepares correctly through the ordinary route (differential oracle).
        using var fresh = CreateSqlServerContext();
        var ordinary = PrepareSql(fresh, GuardRawShape(fresh), storeInCache: false);
        ordinary.Should().NotBeNullOrWhiteSpace();
        ordinary.Should().Contain("ccorr201_guard_raw");
    }

    // -------------------------------------------------------------------------------------------
    // E41 — the staged recipe adopts the cache-side description by reference under copy-on-write
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Every value <c>ApplyPreparationFrom</c> must stage is exactly what the old
    /// <c>CopyTo(copyAll: true)</c> path plus its four legacy assignments produced — but the raw
    /// description is now adopted by reference instead of being copied member by member.
    /// </summary>
    [Fact]
    public void ApplyPreparationFrom_AdoptedDescription_ParityWithCopyTo()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var raw = SimpleShape(ctx)
            .Distinct()
            .WithTag("e41")
            .WithCommandTimeout(42)
            .WithQuotedIdentifiers()
            .Page(10, 5);
        raw.Cache = false;

        var prepared = (QueryCommand<int>)raw.Clone();
        PrepareSql(ctx, prepared);
        var recipe = prepared.CloneForCache();

        // (a) the new path: adopt the description + copy the non-description state.
        var adopted = (QueryCommand<int>)raw.Clone();
        adopted.ApplyPreparationFrom(recipe);

        // (b) the old path: CopyTo(copyAll: true) + the four legacy assignments.
        var copied = (QueryCommand<int>)raw.Clone();
        CopyToOldPath(recipe, copied);

        var fields = typeof(QueryCommand).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        fields.Should().HaveCount(18, "the parity contract covers all QueryCommand instance fields");

        foreach (var field in fields)
        {
            var adoptedValue = field.GetValue(adopted);
            var copiedValue = field.GetValue(copied);
            switch (field.Name)
            {
                case "_definitionMemo":
                    adoptedValue.Should().BeNull("the node-owned memo is cleared on adoption");
                    copiedValue.Should().BeNull("the old path cleared the memo through WriteDescription");
                    break;
                case "_description":
                    CompareDescriptionContents(adoptedValue, copiedValue);
                    break;
                case "_preparation":
                    CompareFieldValues("_preparation", adoptedValue, copiedValue);
                    break;
                case "_binding":
                    CompareFieldValues("_binding", adoptedValue, copiedValue);
                    break;
                case "Paging":
                    adoptedValue.Should().Be(copiedValue, "Paging is a node-owned value");
                    break;
                default:
                    if (field.FieldType.IsValueType)
                        adoptedValue.Should().Be(copiedValue, $"{field.Name} must match the old copy path");
                    else
                        adoptedValue.Should().BeSameAs(copiedValue, $"{field.Name} must match the old copy path");
                    break;
            }
        }
    }

    /// <summary>
    /// The red arm of the adoption: with a plain alias (no <c>MarkShared</c>) a post-adopt raw write
    /// would mutate the stored recipe through the shared description. With the shared guard every write
    /// splits first, so the recipe and a second apply stay exactly as prepared.
    /// </summary>
    [Fact]
    public void ApplyPreparationFrom_AdoptedDescription_IsolatesRecipeFromPostAdoptWrites()
    {
        ResetRecipeCache();
        using var ctx = CreateSqlServerContext();

        var raw = SimpleShape(ctx)
            .Distinct()
            .WithTag("e41")
            .WithCommandTimeout(42)
            .Page(10, 5);
        raw.Cache = false;

        var prepared = (QueryCommand<int>)raw.Clone();
        PrepareSql(ctx, prepared);
        var recipe = prepared.CloneForCache();

        var before = SnapshotRecipeState(recipe);

        var adopted = (QueryCommand<int>)raw.Clone();
        adopted.ApplyPreparationFrom(recipe);

        // Every one of these routes a raw write through WriteDescription/Write* and must split first.
        adopted.Tag = "mutated";
        adopted.CommandTimeout = 99;
        adopted.IsDistinct = false;
        adopted.LimitBy = new LimitByClause((Expression<Func<SimpleEntity, int>>)(x => x.Id), 5, 1);
        adopted.Paging = new Paging { Limit = 3, Offset = 1 };

        SnapshotRecipeState(recipe).Should().Be(before,
            "post-adopt writes on the adopting command must never reach the stored recipe");

        // Reading Definition publishes a node-owned memo only; the shared description is not written.
        adopted.Definition.Should().NotBeNull();
        SnapshotRecipeState(recipe).Should().Be(before,
            "the definition memo is node-owned and never stored on the shared description");

        // A second fresh command applying the same (still pristine) recipe matches ordinary preparation.
        var second = (QueryCommand<int>)raw.Clone();
        second.ApplyPreparationFrom(recipe);
        second.IsPrepared.Should().BeTrue("the recipe is a prepared cache-side command");

        var fromRecipe = PrepareSql(ctx, second, storeInCache: false);

        using var fresh = CreateSqlServerContext();
        var ordinary = PrepareSql(fresh, (QueryCommand<int>)raw.Clone(), storeInCache: false);
        fromRecipe.Should().Be(ordinary,
            "a later apply of the untouched recipe must stay differential-equal to ordinary preparation");
    }

    /// <summary>Runs the pre-E41 apply path: <c>CopyTo(copyAll: true)</c> plus its four extra assignments.</summary>
    private static void CopyToOldPath(QueryCommand source, QueryCommand target)
    {
        typeof(QueryCommand)
            .GetMethod("CopyTo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(source, [target, true]);

        SetCommandProperty(target, "_joins", GetCommandProperty(source, "_joins"));
        SetCommandProperty(target, "_whereBasePlanHash", GetCommandProperty(source, "_whereBasePlanHash"));
        target._preparedArrayJoin = source._preparedArrayJoin;
        target.OneColumn = source.OneColumn;
    }

    private static object? GetCommandProperty(QueryCommand command, string name)
        => typeof(QueryCommand)
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(command);

    private static void SetCommandProperty(QueryCommand command, string name, object? value)
        => typeof(QueryCommand)
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(command, value);

    private static readonly BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Type[] DescriptionBlockTypes =
    [
        typeof(QueryDescription),
        typeof(QueryPredicates),
        typeof(QueryRelations),
        typeof(QueryClauses),
        typeof(QueryOptions),
    ];

    /// <summary>Flattens a description tree into ordered (path, value) pairs, ignoring only the Shared flags.</summary>
    private static List<(string Path, object? Value)> DescribeContents(object? root)
    {
        var entries = new List<(string, object?)>();
        DescribeInto("description", root, entries);
        return entries;
    }

    private static void DescribeInto(string path, object? value, List<(string, object?)> entries)
    {
        if (value is null)
        {
            entries.Add((path, null));
            return;
        }

        if (DescriptionBlockTypes.Contains(value.GetType()))
        {
            foreach (var field in value.GetType().GetFields(DeclaredInstanceFields).OrderBy(it => it.Name, StringComparer.Ordinal))
            {
                if (field.Name == "Shared") continue;
                var child = field.GetValue(value);
                if (child is not null && DescriptionBlockTypes.Contains(child.GetType()))
                    DescribeInto(path + "." + field.Name, child, entries);
                else
                    entries.Add((path + "." + field.Name, child));
            }

            return;
        }

        entries.Add((path, value));
    }

    /// <summary>Compares two descriptions field by field and fails naming the first differing member.</summary>
    private static void CompareDescriptionContents(object? left, object? right)
    {
        var leftEntries = DescribeContents(left);
        var rightEntries = DescribeContents(right);
        leftEntries.Count.Should().Be(rightEntries.Count, "both descriptions must expose the same member set");

        for (var index = 0; index < leftEntries.Count; index++)
        {
            var (path, leftValue) = leftEntries[index];
            var (rightPath, rightValue) = rightEntries[index];
            rightPath.Should().Be(path);
            if (leftValue is null || rightValue is null)
            {
                leftValue.Should().BeNull($"description member {path} differs");
                rightValue.Should().BeNull($"description member {path} differs");
                continue;
            }

            leftValue.Should().Be(rightValue, $"first differing description member: {path}");
        }
    }

    /// <summary>Compares every declared field of an internal state object (comparers by reference).</summary>
    private static void CompareFieldValues(string owner, object? left, object? right)
    {
        if (left is null || right is null)
        {
            left.Should().BeSameAs(right, $"{owner} must be null on both sides");
            return;
        }

        left.GetType().Should().BeSameAs(right.GetType(), $"{owner} must have the same type");
        foreach (var field in left.GetType().GetFields(DeclaredInstanceFields).OrderBy(it => it.Name, StringComparer.Ordinal))
        {
            var leftValue = field.GetValue(left);
            var rightValue = field.GetValue(right);
            if (field.FieldType.IsValueType)
                leftValue.Should().Be(rightValue, $"{owner}.{field.Name} differs");
            else
                leftValue.Should().BeSameAs(rightValue, $"{owner}.{field.Name} must be shared by reference");
        }
    }

    /// <summary>A stable textual snapshot of the recipe's description and preparation (including reference ids).</summary>
    private static string SnapshotRecipeState(QueryCommand command)
    {
        var builder = new StringBuilder();
        AppendSnapshot(builder, "_description", GetCommandField(command, "_description"));
        AppendSnapshot(builder, "_preparation", GetCommandField(command, "_preparation"));
        AppendSnapshot(builder, "_binding", GetCommandField(command, "_binding"));
        return builder.ToString();
    }

    private static object? GetCommandField(QueryCommand command, string name)
        => typeof(QueryCommand).GetField(name, DeclaredInstanceFields)!.GetValue(command);

    private static void AppendSnapshot(StringBuilder builder, string path, object? value)
    {
        if (value is null)
        {
            builder.Append(path).Append("=null\n");
            return;
        }

        var type = value.GetType();
        if (DescriptionBlockTypes.Contains(type)
            || type == typeof(QueryCommandPreparation)
            || type == typeof(QueryCommandBinding))
        {
            foreach (var field in type.GetFields(DeclaredInstanceFields).OrderBy(it => it.Name, StringComparer.Ordinal))
            {
                if (field.Name == "Shared") continue;
                AppendSnapshot(builder, path + "." + field.Name, field.GetValue(value));
            }

            return;
        }

        if (value is string text)
        {
            builder.Append(path).Append("=\"").Append(text).Append("\"\n");
            return;
        }

        if (type.IsValueType)
        {
            builder.Append(path).Append('=').Append(value).Append('\n');
            return;
        }

        builder.Append(path).Append("=ref#").Append(RuntimeHelpers.GetHashCode(value)).Append('\n');
    }

    // -------------------------------------------------------------------------------------------
    // Export document model
    // -------------------------------------------------------------------------------------------

    private sealed record ApiType(
        string FullName,
        string TypeKind,
        string Modifiers,
        string? BaseType,
        string[] Interfaces,
        string[] Members);

    private sealed record ApiAssembly(
        string Name,
        string? Version,
        bool Present,
        int PublicTypeCount,
        ApiType[] PublicTypes);

    private sealed record ApiInventory(
        string Task,
        string Kind,
        string Label,
        string[] ReferenceAssemblies,
        ApiAssembly[] Assemblies);
}
