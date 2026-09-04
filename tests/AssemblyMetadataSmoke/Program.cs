using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string assemblyPath = Path.Combine(repoRoot, "lib", "Assembly-CSharp.dll");

if (!File.Exists(assemblyPath))
    throw new FileNotFoundException("Unturned Assembly-CSharp.dll was not found.", assemblyPath);

var requiredProperties = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["SDG.Unturned.DialoguePage"] = ["text"],
    ["SDG.Unturned.DialogueResponse"] = ["text"],
    ["SDG.Unturned.ObjectNPCAsset"] = ["npcName"],
    ["SDG.Unturned.QuestAsset"] = ["questName", "questDescription"],
    ["SDG.Unturned.ObjectAsset"] = ["FriendlyName", "objectName", "InteractabilityDialogueDisplayName"],
    ["SDG.Unturned.VendorAsset"] = ["vendorName", "vendorDescription"],
    ["SDG.Unturned.VendorBuying"] = ["displayDesc"],
    ["SDG.Unturned.VendorSellingItem"] = ["displayDesc"],
    ["SDG.Unturned.VendorSellingVehicle"] = ["displayDesc"],
};

var requiredFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["SDG.Unturned.DialoguePage"] = ["<text>k__BackingField"],
    ["SDG.Unturned.DialogueResponse"] = ["<text>k__BackingField"],
    ["SDG.Unturned.ObjectNPCAsset"] = ["<npcName>k__BackingField"],
    ["SDG.Unturned.QuestAsset"] = ["<questName>k__BackingField", "<questDescription>k__BackingField"],
    ["SDG.Unturned.ObjectAsset"] = ["_objectName", "<InteractabilityDialogueDisplayName>k__BackingField"],
    ["SDG.Unturned.VendorAsset"] = ["<vendorName>k__BackingField", "<vendorDescription>k__BackingField"],
    ["SDG.Unturned.VendorElement"] = ["descriptionOverride"],
};

using var stream = File.OpenRead(assemblyPath);
using var peReader = new PEReader(stream);
MetadataReader reader = peReader.GetMetadataReader();
var types = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);

foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
{
    TypeDefinition type = reader.GetTypeDefinition(handle);
    string @namespace = reader.GetString(type.Namespace);
    string name = reader.GetString(type.Name);
    types[string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}"] = type;
}

foreach (string typeName in new[]
{
    "SDG.Unturned.Provider",
    "SDG.Unturned.MenuUI",
    "SDG.Unturned.PlayerUI",
})
{
    if (!types.TryGetValue(typeName, out TypeDefinition type))
        throw new InvalidOperationException($"Required runtime Update type is missing: {typeName}");

    int updateCount = type.GetMethods()
        .Select(handle => reader.GetMethodDefinition(handle))
        .Count(method =>
            reader.GetString(method.Name) == "Update" &&
            (method.Attributes & System.Reflection.MethodAttributes.Static) == 0 &&
            method.GetParameters().All(handle => reader.GetParameter(handle).SequenceNumber == 0));

    if (updateCount != 1)
        throw new InvalidOperationException($"Expected exactly one instance parameterless Update method on {typeName}, found {updateCount}.");
}

if (!types.TryGetValue("SDG.Unturned.Dedicator", out TypeDefinition dedicator))
    throw new InvalidOperationException("Required type is missing: SDG.Unturned.Dedicator");

PropertyDefinition dedicatedProperty = dedicator.GetProperties()
    .Select(handle => reader.GetPropertyDefinition(handle))
    .Single(property => reader.GetString(property.Name) == "isStandaloneDedicatedServer");
MethodDefinition dedicatedGetter = reader.GetMethodDefinition(dedicatedProperty.GetAccessors().Getter);
if ((dedicatedGetter.Attributes & System.Reflection.MethodAttributes.Public) == 0 ||
    (dedicatedGetter.Attributes & System.Reflection.MethodAttributes.Static) == 0)
{
    throw new InvalidOperationException("Dedicator.isStandaloneDedicatedServer must remain public static.");
}

TypeDefinition movement = types["SDG.Unturned.PlayerMovement"];
MethodDefinition walkingSimulation = movement.GetMethods()
    .Select(handle => reader.GetMethodDefinition(handle))
    .Single(method =>
    {
        if (reader.GetString(method.Name) != "simulate") return false;
        string[] parameterNames = method.GetParameters()
            .Select(handle => reader.GetParameter(handle))
            .Where(parameter => parameter.SequenceNumber > 0)
            .OrderBy(parameter => parameter.SequenceNumber)
            .Select(parameter => reader.GetString(parameter.Name))
            .ToArray();
        return parameterNames.SequenceEqual(new[]
        {
            "simulation", "recov", "input_x", "input_y", "look_x", "look_y",
            "inputJump", "inputSprint", "deltaTime"
        }, StringComparer.Ordinal);
    });
if ((walkingSimulation.Attributes & System.Reflection.MethodAttributes.Public) == 0 ||
    (walkingSimulation.Attributes & System.Reflection.MethodAttributes.Static) != 0)
{
    throw new InvalidOperationException("PlayerMovement.simulate walking overload must remain public instance API.");
}

TypeDefinition playerLook = types["SDG.Unturned.PlayerLook"];
PropertyDefinition aimProperty = playerLook.GetProperties()
    .Select(handle => reader.GetPropertyDefinition(handle))
    .Single(property => reader.GetString(property.Name) == "aim");
MethodDefinition aimGetter = reader.GetMethodDefinition(aimProperty.GetAccessors().Getter);
if ((aimGetter.Attributes & System.Reflection.MethodAttributes.Public) == 0 ||
    (aimGetter.Attributes & System.Reflection.MethodAttributes.Static) != 0)
{
    throw new InvalidOperationException("PlayerLook.aim must remain a public instance getter.");
}

TypeDefinition controlsSettings = types["SDG.Unturned.ControlsSettings"];
Dictionary<string, PropertyDefinition> controlProperties = controlsSettings.GetProperties()
    .Select(handle => reader.GetPropertyDefinition(handle))
    .ToDictionary(property => reader.GetString(property.Name), StringComparer.Ordinal);
foreach (string controlName in new[] { "up", "down", "left", "right", "ascend", "descend" })
{
    if (!controlProperties.TryGetValue(controlName, out PropertyDefinition property))
        throw new InvalidOperationException("ControlsSettings." + controlName + " binding property is missing.");
    MethodDefinition getter = reader.GetMethodDefinition(property.GetAccessors().Getter);
    if ((getter.Attributes & System.Reflection.MethodAttributes.Public) == 0 ||
        (getter.Attributes & System.Reflection.MethodAttributes.Static) == 0)
    {
        throw new InvalidOperationException("ControlsSettings." + controlName + " must remain a public static getter.");
    }
}

TypeDefinition useableGun = types["SDG.Unturned.UseableGun"];
HashSet<string> useableGunMethods = useableGun.GetMethods()
    .Select(handle => reader.GetString(reader.GetMethodDefinition(handle).Name))
    .ToHashSet(StringComparer.Ordinal);
foreach (string methodName in new[] { "tick", "startAttach", "stopAttach" })
{
    if (!useableGunMethods.Contains(methodName))
        throw new InvalidOperationException("Required attachment UI lifecycle method is missing: UseableGun." + methodName);
}

HashSet<string> useableGunFields = useableGun.GetFields()
    .Select(handle => reader.GetString(reader.GetFieldDefinition(handle).Name))
    .ToHashSet(StringComparer.Ordinal);
foreach (string fieldName in new[]
{
    "isAttaching", "sightButton", "tacticalButton", "gripButton", "barrelButton", "magazineButton"
})
{
    if (!useableGunFields.Contains(fieldName))
        throw new InvalidOperationException("Required attachment UI state field is missing: UseableGun." + fieldName);
}

TypeDefinition inputEx = types["SDG.Unturned.InputEx"];
bool hasConsumeKeyDown = inputEx.GetMethods()
    .Select(handle => reader.GetMethodDefinition(handle))
    .Any(method => reader.GetString(method.Name) == "ConsumeKeyDown" &&
        method.GetParameters().Count(handle => reader.GetParameter(handle).SequenceNumber > 0) == 1);
if (!hasConsumeKeyDown)
    throw new InvalidOperationException("InputEx.ConsumeKeyDown(KeyCode) is missing from the current game assembly.");

TypeDefinition playerUi = types["SDG.Unturned.PlayerUI"];
HashSet<string> playerUiFields = playerUi.GetFields()
    .Select(handle => reader.GetString(reader.GetFieldDefinition(handle).Name))
    .ToHashSet(StringComparer.Ordinal);
foreach (string fieldName in new[] { "window", "container" })
{
    if (!playerUiFields.Contains(fieldName))
        throw new InvalidOperationException("Required native UI state field is missing: PlayerUI." + fieldName);
}

foreach ((string typeName, string[] properties) in requiredProperties)
{
    if (!types.TryGetValue(typeName, out TypeDefinition type))
        throw new InvalidOperationException($"Required type is missing: {typeName}");

    var propertyNames = type.GetProperties().Select(handle => reader.GetString(reader.GetPropertyDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
    foreach (string property in properties)
        if (!propertyNames.Contains(property))
            throw new InvalidOperationException($"Required property is missing: {typeName}.{property}");
}

foreach ((string typeName, string[] fields) in requiredFields)
{
    TypeDefinition type = types[typeName];
    var fieldNames = type.GetFields().Select(handle => reader.GetString(reader.GetFieldDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
    foreach (string field in fields)
        if (!fieldNames.Contains(field))
            throw new InvalidOperationException($"Required raw field is missing: {typeName}.{field}");
}

Console.WriteLine("Assembly metadata smoke checks passed.");
