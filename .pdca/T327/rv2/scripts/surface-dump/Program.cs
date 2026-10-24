using System.Reflection;
using System.Text;

var (assemblyPath, outPath) = (args[0], args[1]);
var dir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
var refs = new List<string>(Directory.GetFiles(dir, "*.dll"));
refs.AddRange(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"));
var resolver = new PathAssemblyResolver(refs.Distinct());
using var mlc = new MetadataLoadContext(resolver);
var asm = mlc.LoadFromAssemblyPath(assemblyPath);

var lines = new List<string>();
static bool IsApi(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
static bool IsApiField(FieldInfo f) => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;
static bool IsApiProp(PropertyInfo p) => p.GetMethod is { } g && IsApi(g) || p.SetMethod is { } s && IsApi(s);
static bool IsApiEvent(EventInfo e) => e.AddMethod is { } a && IsApi(a);

foreach (var type in asm.GetTypes().Where(t => t.IsVisible).OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    lines.Add("TYPE " + type.FullName);
    foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.ToString(), StringComparer.Ordinal))
    {
        var isApi = m switch
        {
            MethodBase mb => IsApi(mb),
            FieldInfo f => IsApiField(f),
            PropertyInfo p => IsApiProp(p),
            EventInfo e => IsApiEvent(e),
            Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
            _ => false,
        };
        if (isApi) lines.Add("  " + m.MemberType + " " + m);
    }
}
File.WriteAllLines(outPath, lines);
Console.WriteLine($"{assemblyPath}: {lines.Count} lines -> {outPath}");
