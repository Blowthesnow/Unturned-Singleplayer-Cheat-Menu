using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string FindWorkspaceRoot()
{
    DirectoryInfo? current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if ((Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git"))) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "UnturnedSingleplayerCheatMenu")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Unturned BepInEx workspace root.");
}

static HashSet<string> ReadTypeNames(string assemblyPath, out HashSet<string> references)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader pe = new PEReader(stream);
    MetadataReader metadata = pe.GetMetadataReader();
    references = metadata.AssemblyReferences
        .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    return metadata.TypeDefinitions
        .Select(handle => metadata.GetTypeDefinition(handle))
        .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
        .ToHashSet(StringComparer.Ordinal);
}

string root = FindWorkspaceRoot();
string testProject = Path.Combine(root, "tests", "UiControlLibrarySmoke", "UiControlLibrarySmoke.csproj");
string projectText = File.ReadAllText(testProject);
Require(projectText.Contains("<ProjectReference", StringComparison.Ordinal), "Smoke must reference the production project.");
Require(!Regex.IsMatch(projectText, @"<Compile\s+Include=.*(?:UI|ControlLibrary)", RegexOptions.IgnoreCase),
    "Smoke must not shadow-compile production UI sources.");

string localAssembly = Path.Combine(AppContext.BaseDirectory, "UnturnedSingleplayerCheatMenu.dll");
string productionAssembly = Path.Combine(root, "artifacts", "UnturnedSingleplayerCheatMenu.dll");
Require(File.Exists(localAssembly), "ProjectReference did not copy the production assembly into the Smoke output.");
Require(File.Exists(productionAssembly), "Production assembly was not built at artifacts/UnturnedSingleplayerCheatMenu.dll.");
Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(localAssembly))) ==
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(productionAssembly))),
    "Smoke assembly is not byte-identical to the production build output.");

HashSet<string> types = ReadTypeNames(localAssembly, out HashSet<string> references);
string[] requiredTypes =
{
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core.RgbaColor",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core.FluentTheme",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core.FluentThemes",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core.FluentTimeRingOptions",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime.FluentRoundedGraphic",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime.LegacyFluentSurface",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime.LegacyFluentControlFactory",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime.LegacyFluentEllipseGraphic",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime.LegacyFluentTimeRingGraphic",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls.LegacyFluentControlHandle",
    "UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls.LegacyFluentTimeRingControl",
    "UnturnedSingleplayerCheatMenu.UI.LegacyFluentUiContext",
    "UnturnedSingleplayerCheatMenu.UI.LegacyFluentHandleRegistry",
    "UnturnedSingleplayerCheatMenu.UI.LegacyFluentTooltipBridge",
    "UnturnedSingleplayerCheatMenu.UI.LegacyFluentControlAdapter"
};
foreach (string type in requiredTypes)
{
    Require(types.Contains(type), "Production assembly is missing control-library type: " + type);
}

string[] forbiddenReferences = { "ControlGallery.Ugui", "Unity.TextMeshPro", "TMPro", "UnityEngine.TextCoreFontEngineModule", "UnityEngine.TextCoreTextEngineModule" };
foreach (string reference in forbiddenReferences)
{
    Require(!references.Contains(reference), "Production assembly contains forbidden reference: " + reference);
}

string controlRoot = Path.Combine(root, "src", "UnturnedSingleplayerCheatMenu", "UI", "ControlLibrary");
string[] sourceFiles = Directory.GetFiles(controlRoot, "*.cs", SearchOption.AllDirectories);
Require(sourceFiles.Length >= 8, "Legacy control subset is unexpectedly incomplete.");
Regex forbiddenSource = new(@"\bTMPro\b|\bTMP_|Unity\.TextMeshPro|TextCore|\bBepInEx\b|\bSDG\.Unturned\b|Assembly-CSharp|IconPacks|\.svg\b|\.png\b", RegexOptions.IgnoreCase);
foreach (string file in sourceFiles)
{
    string source = File.ReadAllText(file);
    Require(!forbiddenSource.IsMatch(source), "Forbidden dependency or icon token in " + Path.GetRelativePath(root, file));
}

string handleSource = File.ReadAllText(Path.Combine(controlRoot, "Controls", "LegacyFluentControlHandle.cs"));
Require(!Regex.IsMatch(handleSource, @"\bObject\.Destroy\b|\bDestroy\s*\(", RegexOptions.IgnoreCase),
    "Control handles must not destroy page-owned GameObjects.");
string themeSource = File.ReadAllText(Path.Combine(controlRoot, "Core", "FluentTheme.cs"));
string themesSource = File.ReadAllText(Path.Combine(controlRoot, "Core", "FluentThemes.cs"));
string factorySource = File.ReadAllText(Path.Combine(controlRoot, "Runtime", "LegacyFluentControlFactory.cs"));
string timeRingGraphicSource = File.ReadAllText(Path.Combine(controlRoot, "Runtime", "LegacyFluentHudGraphics.cs"));
string timeRingControlSource = File.ReadAllText(Path.Combine(controlRoot, "Controls", "LegacyFluentTimeRingControl.cs"));
Require(themeSource.Contains("internal void ApplyAccent(RgbaColor accent)", StringComparison.Ordinal) &&
        themeSource.Contains("Accent { get; private set; }", StringComparison.Ordinal) &&
        themeSource.Contains("FocusSoft { get; private set; }", StringComparison.Ordinal),
    "The shared production theme cannot update one live accent/focus family safely.");
Require(themesSource.Contains("#0078D4", StringComparison.Ordinal) && !themesSource.Contains("#E87562", StringComparison.Ordinal),
    "The production control theme does not default to Fluent blue.");
Require(factorySource.Contains("AdaptTooltipSurface", StringComparison.Ordinal) &&
        factorySource.Contains("borderThickness: 2f", StringComparison.Ordinal) &&
        factorySource.Contains("AdaptTimeRing", StringComparison.Ordinal),
    "The production control library does not expose the dedicated two-pixel tooltip surface.");
Require(timeRingGraphicSource.Contains("IPointerDownHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IInitializePotentialDragHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IBeginDragHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IDragHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IEndDragHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IScrollHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("IMoveHandler", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("RectTransformUtility.ScreenPointToLocalPointInRectangle", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("Mathf.Atan2(delta.x, delta.y)", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("Mathf.Repeat(_time, 12f) / 12f * 360f", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("for (int tick = 0; tick < 60; tick++)", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("bool major = tick % 5 == 0", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("eventData.useDragThreshold = false", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("EndPointerAdjustment()", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("private void Update()", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("Input.GetMouseButton(0)", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("Input.mousePosition", StringComparison.Ordinal) &&
        timeRingGraphicSource.Contains("LockParentScroll()", StringComparison.Ordinal) &&
		timeRingGraphicSource.Contains("scrollRect.horizontal = false", StringComparison.Ordinal) &&
		timeRingGraphicSource.Contains("scrollRect.vertical = false", StringComparison.Ordinal) &&
		timeRingGraphicSource.Contains("RestoreParentScroll()", StringComparison.Ordinal) &&
		timeRingGraphicSource.Contains("_dragAccumulator.Begin(_time, angle)", StringComparison.Ordinal) &&
		timeRingGraphicSource.Contains("SetTime(_dragAccumulator.Continue(angle, _step), notify: true)", StringComparison.Ordinal),
	"The adapted time ring lost its twelve-hour face, cross-period drag, wheel, navigation, or 15-minute snapping contract.");
Require(timeRingControlSource.Contains("TimeLabel.text = _timeFormatter(Graphic.Time)", StringComparison.Ordinal) &&
        timeRingControlSource.Contains("Graphic.TimeChanged -= _graphicListener", StringComparison.Ordinal),
    "The adapted time ring no longer delegates localized clock formatting or retires its upstream event subscription.");
string registrySource = File.ReadAllText(Path.Combine(root, "src", "UnturnedSingleplayerCheatMenu", "UI", "LegacyFluentHandleRegistry.cs"));
Require(registrySource.Contains("Scope scope = FindExact(controlRoot)", StringComparison.Ordinal) &&
        registrySource.Contains("candidateTransform.IsChildOf(rootTransform)", StringComparison.Ordinal),
    "Handle registry must attach lifetimes to exact control roots so nested page refreshes retire them immediately.");
Require(File.Exists(Path.Combine(controlRoot, "UPSTREAM-MANIFEST.md")), "Upstream manifest is missing.");

Console.WriteLine($"UI control-library smoke checks passed: types={requiredTypes.Length}, sourceFiles={sourceFiles.Length}, refs={references.Count}.");
