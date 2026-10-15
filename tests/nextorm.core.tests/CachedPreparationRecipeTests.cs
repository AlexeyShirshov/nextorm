using System.Reflection;
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
