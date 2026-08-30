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
