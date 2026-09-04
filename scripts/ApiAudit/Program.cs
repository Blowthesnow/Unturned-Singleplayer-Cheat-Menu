using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ApiAudit <repo-root> <project-file> <output-tsv>");
    return 2;
}

string repoRoot = Path.GetFullPath(args[0]);
string projectFile = Path.GetFullPath(args[1]);
string outputPath = Path.GetFullPath(args[2]);
string projectDirectory = Path.GetDirectoryName(projectFile)!;
var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
string[] sourceFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
    .Where(path => !HasSegment(path, "bin") && !HasSegment(path, "obj"))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
var trees = sourceFiles.Select(path => CSharpSyntaxTree.ParseText(
    File.ReadAllText(path, Encoding.UTF8), parseOptions, path, Encoding.UTF8)).ToArray();

var references = new List<MetadataReference>();
DirectoryInfo? dotnetRoot = new(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
while (dotnetRoot != null && !Directory.Exists(Path.Combine(dotnetRoot.FullName, "packs")))
    dotnetRoot = dotnetRoot.Parent;
if (dotnetRoot == null) throw new DirectoryNotFoundException("Unable to locate the dotnet packs directory.");
string packsRoot = Path.Combine(dotnetRoot.FullName, "packs", "NETStandard.Library.Ref");
string referencePack = Directory.GetDirectories(packsRoot)
    .OrderByDescending(path => Version.Parse(Path.GetFileName(path)))
    .Select(path => Path.Combine(path, "ref", "netstandard2.1"))
    .First(Directory.Exists);
string[] frameworkReferences = Directory.GetFiles(referencePack, "*.dll");
foreach (string dll in frameworkReferences) references.Add(MetadataReference.CreateFromFile(dll));
var frameworkFileNames = new HashSet<string>(frameworkReferences.Select(path => Path.GetFileName(path)!),
    StringComparer.OrdinalIgnoreCase);
foreach (string dll in Directory.GetFiles(Path.Combine(repoRoot, "lib"), "*.dll"))
{
    if (!frameworkFileNames.Contains(Path.GetFileName(dll)))
        references.Add(MetadataReference.CreateFromFile(dll));
}

var compilation = CSharpCompilation.Create(
    "UnturnedSingleplayerCheatMenu.ApiAudit",
    trees,
    references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Disable,
        allowUnsafe: true));
Diagnostic[] errors = compilation.GetDiagnostics()
    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
    .ToArray();
if (errors.Length > 0)
{
    foreach (Diagnostic error in errors) Console.Error.WriteLine(error);
    return 3;
}

var usages = new ConcurrentDictionary<ApiKey, ApiUsage>();
Parallel.ForEach(trees, tree =>
{
    SemanticModel model = compilation.GetSemanticModel(tree, ignoreAccessibility: false);
    new ExternalApiWalker(model, compilation.AssemblyName!, repoRoot, usages).Visit(tree.GetRoot());
});

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
{
    writer.WriteLine("Assembly\tNamespace\tContainingType\tMemberKind\tMember\tSignature\tAccess\tUsageCount\tLocations");
    foreach (ApiUsage usage in usages.Values
        .OrderBy(value => value.Key.Assembly, StringComparer.Ordinal)
        .ThenBy(value => value.Key.Namespace, StringComparer.Ordinal)
        .ThenBy(value => value.Key.ContainingType, StringComparer.Ordinal)
        .ThenBy(value => value.Key.MemberKind, StringComparer.Ordinal)
        .ThenBy(value => value.Key.Signature, StringComparer.Ordinal))
    {
        writer.WriteLine(string.Join('\t', new[]
        {
            Clean(usage.Key.Assembly), Clean(usage.Key.Namespace), Clean(usage.Key.ContainingType),
            Clean(usage.Key.MemberKind), Clean(usage.Key.Member), Clean(usage.Key.Signature),
            Clean(string.Join(",", usage.Accesses.OrderBy(value => value, StringComparer.Ordinal))),
            usage.Occurrences.ToString(),
            Clean(string.Join(";", usage.Locations.OrderBy(value => value, StringComparer.Ordinal)))
        }));
    }
}

Console.WriteLine($"SourceFiles={sourceFiles.Length}");
Console.WriteLine($"ExternalSymbols={usages.Count}");
foreach (IGrouping<string, ApiUsage> group in usages.Values.GroupBy(value => value.Key.Assembly).OrderBy(group => group.Key))
    Console.WriteLine($"{group.Key}={group.Count()}");
Console.WriteLine($"Output={outputPath}");
return 0;

static bool HasSegment(string path, string segment) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
    .Any(value => string.Equals(value, segment, StringComparison.OrdinalIgnoreCase));

static string Clean(string value) => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

internal readonly record struct ApiKey(string Assembly, string Namespace, string ContainingType,
    string MemberKind, string Member, string Signature);

internal sealed class ApiUsage
{
    internal ApiUsage(ApiKey key) => Key = key;
    internal ApiKey Key { get; }
    internal ConcurrentDictionary<string, byte> AccessMap { get; } = new(StringComparer.Ordinal);
    internal ConcurrentDictionary<string, byte> LocationMap { get; } = new(StringComparer.Ordinal);
    private int _occurrences;
    internal int Occurrences => _occurrences;
    internal IEnumerable<string> Accesses => AccessMap.Keys;
    internal IEnumerable<string> Locations => LocationMap.Keys;

    internal void Add(string access, string location)
    {
        AccessMap.TryAdd(access, 0);
        LocationMap.TryAdd(location, 0);
        Interlocked.Increment(ref _occurrences);
    }
}

internal sealed class ExternalApiWalker : CSharpSyntaxWalker
{
    private static readonly SymbolDisplayFormat SignatureFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly SemanticModel _model;
    private readonly string _ownAssembly;
    private readonly string _repoRoot;
    private readonly ConcurrentDictionary<ApiKey, ApiUsage> _usages;

    internal ExternalApiWalker(SemanticModel model, string ownAssembly, string repoRoot,
        ConcurrentDictionary<ApiKey, ApiUsage> usages)
    {
        _model = model;
        _ownAssembly = ownAssembly;
        _repoRoot = repoRoot;
        _usages = usages;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        Add(_model.GetSymbolInfo(node).Symbol, "call", node);
        base.VisitInvocationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        Add(_model.GetSymbolInfo(node).Symbol, "new", node);
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        Add(_model.GetSymbolInfo(node).Symbol, "new", node);
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        Add(_model.GetSymbolInfo(node).Symbol, "attribute", node);
        base.VisitAttribute(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        ISymbol? symbol = _model.GetSymbolInfo(node).Symbol;
        if (symbol is IPropertySymbol or IFieldSymbol or IEventSymbol) Add(symbol, GetAccess(node), node);
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        ISymbol? symbol = _model.GetSymbolInfo(node).Symbol;
        if (symbol is IPropertySymbol or IFieldSymbol or IEventSymbol) Add(symbol, GetAccess(node), node);
        base.VisitMemberBindingExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!(node.Parent is MemberAccessExpressionSyntax member && member.Name == node) &&
            node.Parent is not MemberBindingExpressionSyntax)
        {
            ISymbol? symbol = _model.GetSymbolInfo(node).Symbol;
            if (symbol is IPropertySymbol or IFieldSymbol or IEventSymbol) Add(symbol, GetAccess(node), node);
        }
        base.VisitIdentifierName(node);
    }

    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        Add(_model.GetSymbolInfo(node).Symbol, GetAccess(node), node);
        base.VisitElementAccessExpression(node);
    }

    private void Add(ISymbol? rawSymbol, string access, SyntaxNode node)
    {
        if (rawSymbol == null) return;
        ISymbol symbol = rawSymbol is IMethodSymbol method
            ? (method.ReducedFrom ?? method).OriginalDefinition
            : rawSymbol.OriginalDefinition;
        IAssemblySymbol? assembly = symbol.ContainingAssembly;
        if (assembly == null || string.Equals(assembly.Identity.Name, _ownAssembly, StringComparison.Ordinal)) return;
        INamedTypeSymbol? type = symbol.ContainingType;
        if (type == null) return;

        var key = new ApiKey(
            assembly.Identity.Name,
            type.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            type.ToDisplayString(SignatureFormat),
            symbol.Kind.ToString(),
            symbol.MetadataName,
            symbol.ToDisplayString(SignatureFormat));
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        string file = Path.GetRelativePath(_repoRoot, span.Path).Replace('\\', '/');
        string location = $"{file}:{span.StartLinePosition.Line + 1}";
        _usages.GetOrAdd(key, static value => new ApiUsage(value)).Add(access, location);
    }

    private static string GetAccess(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent is ParenthesizedExpressionSyntax) current = current.Parent;
        if (current.Parent is AssignmentExpressionSyntax assignment && assignment.Left == current)
            return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? "write" : "read-write";
        if (current.Parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax) return "read-write";
        if (current.Parent is ArgumentSyntax argument &&
            argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword) return "read-write";
        return "read";
    }
}
