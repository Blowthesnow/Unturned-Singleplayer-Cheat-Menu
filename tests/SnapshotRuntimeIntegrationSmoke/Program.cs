using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HarmonyLib;
using SDG.Unturned;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRepositoryRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "UnturnedSingleplayerCheatMenu.slnx"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Repository root marker was not found.");
}

static int CountDirectCalls(MethodInfo caller, int calleeMetadataToken)
{
    byte[] il = caller?.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
    Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);
    int count = 0;
    for (int index = 0; index < il.Length;)
    {
        short value = il[index++] == 0xfe
            ? unchecked((short)(0xfe00 | il[index++]))
            : il[index - 1];
        if (!opcodes.TryGetValue(value, out OpCode opcode)) return -1;
        if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) &&
            index + sizeof(int) <= il.Length &&
            BitConverter.ToInt32(il, index) == calleeMetadataToken) count++;
        int operandSize = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => index + sizeof(int) <= il.Length
                ? checked(sizeof(int) + BitConverter.ToInt32(il, index) * sizeof(int))
                : int.MaxValue,
            _ => 4
        };
        if (operandSize < 0 || index > il.Length - operandSize) return -1;
        index += operandSize;
    }
    return count;
}

static bool ContainsDirectCall(MethodInfo caller, int calleeMetadataToken) =>
    CountDirectCalls(caller, calleeMetadataToken) > 0;

static bool ContainsMemberReferenceCall(string assemblyPath,
    string callerNamespace, string callerTypeName, string callerMethodName,
    string calleeNamespace, string calleeTypeName, string calleeMethodName)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader peReader = new PEReader(stream);
    MetadataReader reader = peReader.GetMetadataReader();
    MethodDefinitionHandle callerHandle = default;
    foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
    {
        TypeDefinition type = reader.GetTypeDefinition(typeHandle);
        if (reader.GetString(type.Namespace) != callerNamespace ||
            reader.GetString(type.Name) != callerTypeName) continue;
        foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(methodHandle).Name) == callerMethodName)
            {
                callerHandle = methodHandle;
                break;
            }
        }
        break;
    }
    if (callerHandle.IsNil) return false;
    MethodDefinition callerDefinition = reader.GetMethodDefinition(callerHandle);
    byte[] il = peReader.GetMethodBody(callerDefinition.RelativeVirtualAddress).GetILBytes();
    Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);
    for (int index = 0; index < il.Length;)
    {
        short value = il[index++] == 0xfe
            ? unchecked((short)(0xfe00 | il[index++]))
            : il[index - 1];
        if (!opcodes.TryGetValue(value, out OpCode opcode)) break;
        int operandStart = index;
        int operandSize = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => index + sizeof(int) <= il.Length
                ? checked(sizeof(int) + BitConverter.ToInt32(il, index) * sizeof(int))
                : int.MaxValue,
            _ => 4
        };
        if (operandSize < 0 || index > il.Length - operandSize) break;
        if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && operandSize == sizeof(int))
        {
            int token = BitConverter.ToInt32(il, operandStart);
            EntityHandle entity = MetadataTokens.EntityHandle(token);
            if (entity.Kind == HandleKind.MemberReference)
            {
                MemberReference member = reader.GetMemberReference((MemberReferenceHandle)entity);
                if (reader.GetString(member.Name) == calleeMethodName &&
                    member.Parent.Kind == HandleKind.TypeReference)
                {
                    TypeReference parent = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                    if (reader.GetString(parent.Namespace) == calleeNamespace &&
                        reader.GetString(parent.Name) == calleeTypeName) return true;
                }
            }
        }
        index += operandSize;
    }
    return false;
}

static string ResolveTypeIdentity(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind == HandleKind.TypeReference)
    {
        TypeReference type = reader.GetTypeReference((TypeReferenceHandle)handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    if (handle.Kind == HandleKind.TypeDefinition)
    {
        TypeDefinition type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    return string.Empty;
}

static string ResolveMethodIdentity(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind == HandleKind.MemberReference)
    {
        MemberReference member = reader.GetMemberReference((MemberReferenceHandle)handle);
        return ResolveTypeIdentity(reader, member.Parent) + "::" + reader.GetString(member.Name);
    }
    if (handle.Kind == HandleKind.MethodDefinition)
    {
        MethodDefinitionHandle methodHandle = (MethodDefinitionHandle)handle;
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(typeHandle);
            if (!type.GetMethods().Contains(methodHandle)) continue;
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name) +
                "::" + reader.GetString(method.Name);
        }
    }
    if (handle.Kind == HandleKind.MethodSpecification)
    {
        MethodSpecification specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
        return ResolveMethodIdentity(reader, specification.Method);
    }
    return string.Empty;
}

static int CountCallsByIdentity(string assemblyPath,
    string callerNamespace, string callerTypeName, string callerMethodName,
    string calleeNamespace, string calleeTypeName, string calleeMethodName)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader peReader = new PEReader(stream);
    MetadataReader reader = peReader.GetMetadataReader();
    MethodDefinitionHandle callerHandle = default;
    foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
    {
        TypeDefinition type = reader.GetTypeDefinition(typeHandle);
        if (reader.GetString(type.Namespace) != callerNamespace ||
            reader.GetString(type.Name) != callerTypeName) continue;
        foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(methodHandle).Name) == callerMethodName)
            {
                callerHandle = methodHandle;
                break;
            }
        }
        break;
    }
    if (callerHandle.IsNil) return -1;
    MethodDefinition callerDefinition = reader.GetMethodDefinition(callerHandle);
    byte[] il = peReader.GetMethodBody(callerDefinition.RelativeVirtualAddress).GetILBytes();
    Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);
    string expectedCallee = calleeNamespace + "." + calleeTypeName + "::" + calleeMethodName;
    int count = 0;
    for (int index = 0; index < il.Length;)
    {
        short value = il[index++] == 0xfe
            ? unchecked((short)(0xfe00 | il[index++]))
            : il[index - 1];
        if (!opcodes.TryGetValue(value, out OpCode opcode)) return -1;
        int operandStart = index;
        int operandSize = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => index + sizeof(int) <= il.Length
                ? checked(sizeof(int) + BitConverter.ToInt32(il, index) * sizeof(int))
                : int.MaxValue,
            _ => 4
        };
        if (operandSize < 0 || index > il.Length - operandSize) return -1;
        if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && operandSize == sizeof(int))
        {
            EntityHandle called = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, operandStart));
            if (ResolveMethodIdentity(reader, called) == expectedCallee) count++;
        }
        index += operandSize;
    }
    return count;
}

static bool HasContinuingCatchAroundCall(string assemblyPath,
    string callerNamespace, string callerTypeName, string callerMethodName,
    string protectedCalleeIdentity, string continuationCalleeIdentity)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader peReader = new PEReader(stream);
    MetadataReader reader = peReader.GetMetadataReader();
    MethodDefinitionHandle callerHandle = default;
    foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
    {
        TypeDefinition type = reader.GetTypeDefinition(typeHandle);
        if (reader.GetString(type.Namespace) != callerNamespace ||
            reader.GetString(type.Name) != callerTypeName) continue;
        callerHandle = type.GetMethods().FirstOrDefault(methodHandle =>
            reader.GetString(reader.GetMethodDefinition(methodHandle).Name) == callerMethodName);
        break;
    }
    if (callerHandle.IsNil) return false;
    MethodDefinition callerDefinition = reader.GetMethodDefinition(callerHandle);
    MethodBodyBlock body = peReader.GetMethodBody(callerDefinition.RelativeVirtualAddress);
    byte[] il = body.GetILBytes();
    Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => opcode.Value);
    List<(int Offset, OpCode Opcode)> instructions = new();
    int protectedCallOffset = -1;
    int continuationCallOffset = -1;
    for (int index = 0; index < il.Length;)
    {
        int offset = index;
        short value = il[index++] == 0xfe
            ? unchecked((short)(0xfe00 | il[index++]))
            : il[index - 1];
        if (!opcodes.TryGetValue(value, out OpCode opcode)) return false;
        instructions.Add((offset, opcode));
        int operandStart = index;
        int operandSize = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => index + sizeof(int) <= il.Length
                ? checked(sizeof(int) + BitConverter.ToInt32(il, index) * sizeof(int))
                : int.MaxValue,
            _ => 4
        };
        if (operandSize < 0 || index > il.Length - operandSize) return false;
        if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && operandSize == sizeof(int))
        {
            string identity = ResolveMethodIdentity(reader,
                MetadataTokens.EntityHandle(BitConverter.ToInt32(il, operandStart)));
            if (identity == protectedCalleeIdentity) protectedCallOffset = offset;
            if (identity == continuationCalleeIdentity) continuationCallOffset = offset;
        }
        index += operandSize;
    }
    if (protectedCallOffset < 0 || continuationCallOffset < 0) return false;
    foreach (ExceptionRegion region in body.ExceptionRegions)
    {
        if (region.Kind != ExceptionRegionKind.Catch ||
            protectedCallOffset < region.TryOffset ||
            protectedCallOffset >= region.TryOffset + region.TryLength ||
            continuationCallOffset < region.HandlerOffset + region.HandlerLength) continue;
        bool rethrows = instructions.Any(instruction =>
            instruction.Offset >= region.HandlerOffset &&
            instruction.Offset < region.HandlerOffset + region.HandlerLength &&
            (instruction.Opcode == OpCodes.Throw || instruction.Opcode == OpCodes.Rethrow));
        if (!rethrows) return true;
    }
    return false;
}

static int FindMethodToken(string assemblyPath, string typeNamespace, string typeName, string methodName)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using PEReader peReader = new PEReader(stream);
    MetadataReader reader = peReader.GetMetadataReader();
    foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
    {
        TypeDefinition type = reader.GetTypeDefinition(typeHandle);
        if (reader.GetString(type.Namespace) != typeNamespace || reader.GetString(type.Name) != typeName) continue;
        foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
        {
            if (reader.GetString(reader.GetMethodDefinition(methodHandle).Name) == methodName)
                return MetadataTokens.GetToken(methodHandle);
        }
    }
    return 0;
}

Assembly product = Assembly.Load("UnturnedSingleplayerCheatMenu");
Assembly nativeAssembly = typeof(Item).Assembly;
Assert(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativeAssembly.Location))) ==
       "A110F5FE471422254C594E29F90FC4058D04D357EB3AE9CB4D3EDA0FFEC33F10",
    "Assembly-CSharp ABI hash drifted and requires a new explicit audit.");
Assert(typeof(Item).GetConstructor(new[] { typeof(ushort), typeof(byte), typeof(byte), typeof(byte[]) }) != null &&
       typeof(Item).GetConstructor(new[] { typeof(ItemAsset), typeof(EItemOrigin) }) != null &&
       typeof(ItemAsset).GetProperty("MaxAmount")?.PropertyType == typeof(int) &&
       typeof(Characters).GetProperty("selected")?.PropertyType == typeof(byte),
    "Native Item snapshot, full-template, capacity, or character-slot ABI is missing.");
MethodInfo farmState = typeof(ItemBarricadeAsset).GetMethod("getState", new[] { typeof(EItemOrigin) })!;
int providerTimeToken = FindMethodToken(nativeAssembly.Location, "SDG.Unturned", "Provider", "get_time");
Assert(farmState != null && providerTimeToken != 0 && ContainsDirectCall(farmState, providerTimeToken),
    "Current native Farm state no longer directly reads Provider.time; canonicalization evidence must be re-audited.");
Type[] productTypes = product.GetTypes();
Type localizationType = product.GetType("UnturnedSingleplayerCheatMenu.Services.PluginLocalization", true)!;
MethodInfo initializeLocalization = localizationType.GetMethod("Initialize",
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
MethodInfo translateLocalization = localizationType.GetMethod("Translate",
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
    binder: null, types: new[] { typeof(string) }, modifiers: null)!;
Type infiniteStorageViewType = product.GetType(
    "UnturnedSingleplayerCheatMenu.UI.CheatMenuOverlayUi", true)!;
MethodInfo resolveFallbackGroupName = infiniteStorageViewType.GetMethod(
    "ResolveInfiniteStorageGroupDisplayName", BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(resolveFallbackGroupName != null,
    "Infinite Storage must expose a pure display-name boundary for localized product fallbacks and untouched resolved asset names.");
initializeLocalization.Invoke(null, new object[] { "English", "Schinese" });
string unresolvedGroupName = (string)resolveFallbackGroupName.Invoke(null,
    new object[] { "", false, false })!;
string conflictingGroupName = (string)resolveFallbackGroupName.Invoke(null,
    new object[] { "", true, false })!;
string workshopResolvedName = (string)resolveFallbackGroupName.Invoke(null,
    new object[] { "Workshop Native Name", true, true })!;
Assert(unresolvedGroupName == "Unresolved ItemGroup (read-only)" &&
       conflictingGroupName == "Asset-identity conflict ItemGroup (read-only)" &&
       workshopResolvedName == "Workshop Native Name",
    "English mode must translate only product fallback group titles while preserving resolved native/Workshop asset names exactly.");
initializeLocalization.Invoke(null, new object[] { "Chinese", "English" });
Assert((string)resolveFallbackGroupName.Invoke(null, new object[] { "", false, false })! ==
       "未解析物品组（只读）" &&
       (string)resolveFallbackGroupName.Invoke(null, new object[] { "", true, false })! ==
       "资产标识冲突的物品组（只读）",
    "Chinese mode must retain localized product fallback group titles.");
Type identityPolicy = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotRuntimeIdentity", true)!;
int freeCharacters = Convert.ToInt32(typeof(Customization).GetField("FREE_CHARACTERS",
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null));
int proCharacters = Convert.ToInt32(typeof(Customization).GetField("PRO_CHARACTERS",
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null));
int policySlotCount = Convert.ToInt32(identityPolicy.GetField("NativeCharacterSlotCount",
    BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue());
Assert(freeCharacters == 1 && proCharacters == 4 && policySlotCount == freeCharacters + proCharacters,
    "Snapshot identity slot range no longer matches the current native FREE+PRO character ABI.");
Type[] pluginTypes = productTypes.Where(type => type.CustomAttributes.Any(attribute =>
    attribute.AttributeType.FullName == "BepInEx.BepInPlugin")).ToArray();
Assert(pluginTypes.Length == 1 && pluginTypes[0].FullName == "UnturnedSingleplayerCheatMenu.CheatMenuPlugin",
    "The product must contain exactly one existing BepInPlugin entry point.");
Type assetOriginClassifierType = product.GetType(
    "UnturnedSingleplayerCheatMenu.Services.AssetOriginClassifier", true)!;
MethodInfo compiledIsWorkshop = assetOriginClassifierType.GetMethod("IsWorkshop",
    BindingFlags.Static | BindingFlags.Public)!;
Assert(compiledIsWorkshop != null &&
       !(bool)compiledIsWorkshop.Invoke(null, new object[] { null })!,
    "The compiled product must classify a null or unresolved asset origin as unknown, never Workshop.");

string[] moduleTypes =
[
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting.SnapshotCraftingModule",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.AmmoRefill.SnapshotAmmoRefillModule",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Decomposition.SnapshotDecompositionModule"
];
Assert(moduleTypes.All(name => product.GetType(name, throwOnError: false) is { } type && type.Assembly == product &&
        !type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "BepInEx.BepInPlugin")),
    "Crafting, AmmoRefill, and Decomposition must be internal modules in the one main product assembly.");

string[] retiredTypes =
[
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.VirtualStorageService",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.VirtualStoragePersistence",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.VirtualStorageDocument",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.InfiniteStorageIdentity",
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.InfiniteStorageRuntime"
];
Assert(retiredTypes.All(name => product.GetType(name, throwOnError: false) == null),
    "The released product must not contain legacy storage, migration, provenance, identity, or runtime types.");
Assert(product.GetType("UnturnedSingleplayerCheatMenu.Patches.AttachmentUiDiagnosticPatch", false) == null &&
       product.GetType("UnturnedSingleplayerCheatMenu.Services.AttachmentUiDiagnosticPolicy", false) == null &&
       product.GetType("UnturnedSingleplayerCheatMenu.Services.AttachmentUiRecoveryPolicy", false) == null,
    "The clean product must retain the attachment root fix without temporary diagnostics or symptom-recovery hooks.");
Assert(product.GetType("UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime.SnapshotInfiniteStorageRuntime") != null &&
       product.GetType("UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotDocument") != null,
    "The Snapshot Runtime and schema-1 document must be present in the product.");
foreach (string guardedMethod in new[] { "Pump", "HasReconciliationLatchForCurrentIdentity" })
{
    Assert(!ContainsMemberReferenceCall(product.Location,
            "UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime", "SnapshotInfiniteStorageRuntime", guardedMethod,
            "SDG.Unturned", "SteamPlayerID", "op_Equality"),
        "Snapshot Runtime null guards must not invoke SteamPlayerID.op_Equality; its current native implementation dereferences both operands and throws for null. Method=" + guardedMethod);
}
Assert(HasContinuingCatchAroundCall(product.Location,
        "UnturnedSingleplayerCheatMenu.Services", "PluginRuntimeContext", "Update",
        "UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime.SnapshotInfiniteStorageRuntime::Pump",
        "UnturnedSingleplayerCheatMenu.Services.CreatureHealthBarService::Pump"),
    "A Snapshot Runtime Pump exception must be isolated so the downstream creature-health-bar Pump still runs in the same frame.");
Type inventoryEvidenceType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupInventoryEvidence", true)!;
Type nativePickupAttemptType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.NativePickupAttempt", true)!;
Type nativePickupExecutorType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.NativePickupExecutor", true)!;
Type autoPickupFeatureType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupFeature", true)!;
Type pendingCoordinatorType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupPendingConfirmationCoordinator", true)!;
Type finalAttemptPolicyType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupFinalAttemptPolicy", true)!;
Type attemptRecordCoordinatorType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupAttemptRecordCoordinator", true)!;
Type pendingPickupType = autoPickupFeatureType.GetNestedType("PendingPickup",
    BindingFlags.NonPublic)!;
Assert(nativePickupAttemptType.GetProperty("InventoryBefore")?.PropertyType == inventoryEvidenceType &&
       nativePickupAttemptType.GetProperty("InventoryAfter")?.PropertyType == inventoryEvidenceType &&
       nativePickupAttemptType.GetProperty("IsInitialized")?.PropertyType == typeof(bool) &&
       nativePickupExecutorType.GetMethod("CaptureInventoryEvidence",
           BindingFlags.Static | BindingFlags.NonPublic)?.ReturnType == inventoryEvidenceType &&
       pendingPickupType?.GetField("InventoryBefore",
           BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.FieldType == inventoryEvidenceType,
    "Native pickup must carry composite inventory evidence from capture through the before/after attempt boundary.");
Assert(ContainsMemberReferenceCall(product.Location,
           "UnturnedSingleplayerCheatMenu.AutoPickup", "NativePickupExecutor", "CaptureInventoryEvidence",
           "SDG.Unturned", "PlayerInventory", "getItemCount") &&
       ContainsMemberReferenceCall(product.Location,
           "UnturnedSingleplayerCheatMenu.AutoPickup", "NativePickupExecutor", "CaptureInventoryEvidence",
           "SDG.Unturned", "PlayerInventory", "getItem"),
    "Compiled inventory evidence capture must enumerate matching ItemJar instances, including amount=0 jars excluded by legacy amount search.");
MethodInfo nativePickupExecutorExecute = nativePickupExecutorType.GetMethod("Execute",
    BindingFlags.Instance | BindingFlags.Public)!;
MethodInfo normalizeNativeAttempt = finalAttemptPolicyType.GetMethod("NormalizeNativeAttempt",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo captureNativeCallback = finalAttemptPolicyType.GetMethod("CaptureNativeCallback",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo captureStorageCallback = finalAttemptPolicyType.GetMethod("CaptureStorageCallback",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo buildFailedAttempt = finalAttemptPolicyType.GetMethod("BuildFailedAttempt",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo recordAttemptMethod = autoPickupFeatureType.GetMethod("RecordAttempt",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo recordCoordinatorExecute = attemptRecordCoordinatorType.GetMethod("Execute",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo[] nativeExecutionMethods = productTypes
    .Where(type => type == nativePickupExecutorType ||
                   type.FullName?.StartsWith(nativePickupExecutorType.FullName + "+",
                       StringComparison.Ordinal) == true)
    .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.Public | BindingFlags.NonPublic))
    .ToArray();
Assert(nativePickupExecutorExecute != null && normalizeNativeAttempt != null &&
       captureNativeCallback != null && captureStorageCallback != null && buildFailedAttempt != null &&
       nativeExecutionMethods.Any(method => ContainsDirectCall(method, normalizeNativeAttempt.MetadataToken)) &&
       nativeExecutionMethods.Any(method => ContainsDirectCall(method, captureNativeCallback.MetadataToken)) &&
       nativeExecutionMethods.Any(method => ContainsDirectCall(method, captureStorageCallback.MetadataToken)) &&
       nativeExecutionMethods.Any(method => ContainsDirectCall(method, buildFailedAttempt.MetadataToken)),
    "Compiled NativePickupExecutor must normalize proven delivery, capture callback throw/default outcomes, and preserve failed attempts.");
Type resultKindType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupResultKind", true)!;
Type postActionType = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupNativePostAction", true)!;
object compiledBeforeEvidence = Activator.CreateInstance(inventoryEvidenceType,
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
    new object[] { 1, 5L, 0 }, null)!;
object compiledAfterEvidence = Activator.CreateInstance(inventoryEvidenceType,
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
    new object[] { 2, 10L, 1 }, null)!;
const string compiledOriginalDetail =
    "无法读取系统字体列表：$1\\compiled\\path；内部分隔\r\nstate=$1\\unchanged";
object compiledAttempt = Activator.CreateInstance(nativePickupAttemptType,
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
    new object[]
    {
        Enum.Parse(resultKindType, "UnknownFailure"), compiledBeforeEvidence,
        compiledAfterEvidence, compiledOriginalDetail, true, false
    }, null)!;
object compiledNormalizedAttempt = normalizeNativeAttempt.Invoke(null, new object[]
{
    compiledAttempt, Enum.Parse(postActionType, "AcceptNative")
})!;
string compiledNormalizedDetail = (string)nativePickupAttemptType.GetProperty("Detail")!
    .GetValue(compiledNormalizedAttempt)!;
Assert(compiledNormalizedDetail.StartsWith(compiledOriginalDetail, StringComparison.Ordinal) &&
       compiledNormalizedDetail.Contains("背包复合证据已确认原生拾取交付。",
           StringComparison.Ordinal),
    "Compiled delivery normalization must retain the original native detail and append the localized evidence-confirmation detail.");
byte[] compiledOriginalDetailUtf8 = Encoding.UTF8.GetBytes(compiledOriginalDetail);
byte[] compiledNormalizedDetailUtf8 = Encoding.UTF8.GetBytes(compiledNormalizedDetail);
Assert(compiledNormalizedDetailUtf8.Length >= compiledOriginalDetailUtf8.Length &&
       compiledNormalizedDetailUtf8.Take(compiledOriginalDetailUtf8.Length)
           .SequenceEqual(compiledOriginalDetailUtf8),
    "Compiled delivery normalization must preserve dollar, backslash, full-width-semicolon, and CRLF prefix bytes exactly.");
initializeLocalization.Invoke(null, new object[] { "English", "Schinese" });
string compiledTranslatedDetail = (string)translateLocalization.Invoke(null,
    new object[] { compiledNormalizedDetail })!;
const string compiledEnglishEvidenceSuffix =
    "; composite inventory evidence confirmed native pickup delivery.";
const string compiledChineseEvidenceSuffix = "背包复合证据已确认原生拾取交付。";
int compiledSuffixIndex = compiledTranslatedDetail.IndexOf(
    compiledEnglishEvidenceSuffix, StringComparison.Ordinal);
Assert(compiledTranslatedDetail == compiledOriginalDetail + compiledEnglishEvidenceSuffix &&
       compiledSuffixIndex >= 0 &&
       compiledSuffixIndex == compiledTranslatedDetail.LastIndexOf(
           compiledEnglishEvidenceSuffix, StringComparison.Ordinal) &&
       !compiledTranslatedDetail.Contains(compiledChineseEvidenceSuffix, StringComparison.Ordinal),
    "Compiled localization must preserve the adversarial native prefix and translate exactly one fixed evidence suffix.");
object compiledNormalizedTwice = normalizeNativeAttempt.Invoke(null, new object[]
{
    compiledNormalizedAttempt, Enum.Parse(postActionType, "AcceptNative")
})!;
string compiledNormalizedTwiceDetail = (string)nativePickupAttemptType.GetProperty("Detail")!
    .GetValue(compiledNormalizedTwice)!;
Assert(compiledNormalizedTwiceDetail == compiledNormalizedDetail,
    "Compiled delivery normalization must be idempotent and append its confirmation only once.");
Assert(recordAttemptMethod != null && recordCoordinatorExecute != null &&
       ContainsDirectCall(recordAttemptMethod, recordCoordinatorExecute.MetadataToken),
    "Runtime RecordAttempt must use the tested recorder that rejects an uninitialized default Requested enum value.");
MethodInfo confirmPendingMethod = autoPickupFeatureType.GetMethod("ConfirmPending",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo pendingCoordinatorExecute = pendingCoordinatorType.GetMethod("Execute",
    BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(confirmPendingMethod != null && pendingCoordinatorExecute != null &&
       ContainsDirectCall(confirmPendingMethod, pendingCoordinatorExecute.MetadataToken),
    "Runtime ConfirmPending must delegate stale/timeout decisions to the same tested native post-state coordinator.");
Type snapshotRuntimeType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime.SnapshotInfiniteStorageRuntime", true)!;
Type backgroundStoreHandleType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime.SnapshotBackgroundStoreHandle", true)!;
Type backgroundPersistenceType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotBackgroundPersistence", true)!;
Type backgroundCompletionPolicyType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotBackgroundStoreCompletionPolicy", true)!;
Type backgroundCompletionResultType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotBackgroundStoreCompletionResult", true)!;
Type backgroundCompletionRouterType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotBackgroundStoreCompletionRouter", true)!;
MethodInfo beginBackgroundStore = snapshotRuntimeType.GetMethod(
    "TryBeginStoreCandidateInBackground", BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo completeBackgroundStore = snapshotRuntimeType.GetMethod(
    "TryCompleteBackgroundStore", BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo latchBackgroundStore = snapshotRuntimeType.GetMethod(
    "LatchBackgroundStoreReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo beginPendingStorage = autoPickupFeatureType.GetMethod(
    "TryBeginPendingStorage", BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo completePendingStorage = autoPickupFeatureType.GetMethod(
    "CompletePendingStorage", BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo backgroundStart = backgroundPersistenceType.GetMethod(
    "Start", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo resolveBackgroundCompletion = backgroundCompletionPolicyType.GetMethod(
    "Resolve", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo routeBackgroundCompletion = backgroundCompletionRouterType.GetMethod(
    "Route", BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(backgroundStoreHandleType != null && beginBackgroundStore != null &&
       completeBackgroundStore != null && latchBackgroundStore != null && beginPendingStorage != null &&
       completePendingStorage != null && backgroundStart != null &&
       backgroundCompletionResultType != null && backgroundCompletionRouterType != null &&
       backgroundStoreHandleType.GetProperty("ConnectionKey", BindingFlags.Instance | BindingFlags.NonPublic) != null &&
       backgroundStoreHandleType.GetProperty("PersistencePath", BindingFlags.Instance | BindingFlags.NonPublic) != null &&
       backgroundStoreHandleType.GetProperty("TargetPersistence", BindingFlags.Instance | BindingFlags.NonPublic) != null &&
       completeBackgroundStore.GetParameters()[2].ParameterType.GetElementType() == backgroundCompletionResultType &&
       resolveBackgroundCompletion != null && routeBackgroundCompletion != null &&
       ContainsDirectCall(beginBackgroundStore, backgroundStart.MetadataToken) &&
       ContainsDirectCall(completeBackgroundStore, resolveBackgroundCompletion.MetadataToken) &&
       ContainsDirectCall(beginPendingStorage, beginBackgroundStore.MetadataToken) &&
       ContainsDirectCall(completePendingStorage, completeBackgroundStore.MetadataToken),
    "AutoPickup Snapshot writes must use one isolated background persistence operation and apply the tested completion policy before world-drop finalization.");
Assert(CountCallsByIdentity(product.Location,
        "UnturnedSingleplayerCheatMenu.AutoPickup", "AutoPickupFeature", "CompletePendingStorage",
        "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel",
        "SnapshotBackgroundStoreCompletionRouter", "Route") == 1,
    "The compiled AutoPickup completion path must route exactly once through the executable detached/current-session side-effect policy.");

Type storageIdentityType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotStorageIdentity", true)!;
Type groupIdentityType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.ItemGroupIdentity", true)!;
Type itemSnapshotType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.ItemSnapshot", true)!;
Type persistenceType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotPersistence", true)!;
Type saveResultType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel.SnapshotSaveResult", true)!;
MethodInfo saveSucceeded = saveResultType.GetMethod("Succeeded",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo saveFailed = saveResultType.GetMethod("Failed",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo taskFromResult = typeof(Task).GetMethods(BindingFlags.Static | BindingFlags.Public)
    .Single(method => method.Name == "FromResult" && method.IsGenericMethodDefinition)
    .MakeGenericMethod(saveResultType);
ConstructorInfo backgroundConstructor = backgroundPersistenceType.GetConstructors(
    BindingFlags.Instance | BindingFlags.NonPublic).Single();
ConstructorInfo runtimeConstructor = snapshotRuntimeType.GetConstructors(
    BindingFlags.Instance | BindingFlags.NonPublic)
    .Single(constructor => constructor.GetParameters().Length == 1);
ConstructorInfo handleConstructor = backgroundStoreHandleType.GetConstructors(
    BindingFlags.Instance | BindingFlags.NonPublic).Single();
FieldInfo activePersistenceField = snapshotRuntimeType.GetField("_activePersistence",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
FieldInfo activePersistencePathField = snapshotRuntimeType.GetField("_activePersistencePath",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
FieldInfo backgroundStoreField = snapshotRuntimeType.GetField("_backgroundStore",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo invalidateHandle = backgroundStoreHandleType.GetMethod("InvalidateSession",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
PropertyInfo completionDetached = backgroundCompletionResultType.GetProperty("SessionDetached",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
PropertyInfo completionRequiresReconciliation = backgroundCompletionResultType.GetProperty(
    "RequiresReconciliation", BindingFlags.Instance | BindingFlags.NonPublic)!;
PropertyInfo completionMarkerPersisted = backgroundCompletionResultType.GetProperty(
    "ReconciliationMarkerPersisted", BindingFlags.Instance | BindingFlags.NonPublic)!;

object detachedSafeSave = saveFailed.Invoke(null,
    new object[] { "detached-safe", false, string.Empty, string.Empty, string.Empty })!;
object detachedSuccessfulSave = saveSucceeded.Invoke(null,
    new object[] { string.Empty, string.Empty, string.Empty })!;
object detachedUncertainSave = saveFailed.Invoke(null,
    new object[] { "detached-uncertain", true, string.Empty, "old-candidate.json", string.Empty })!;
var detachedCompletionCases = new[]
{
    new { Name = "safe", Save = detachedSafeSave, RequiresMarker = false },
    new { Name = "success", Save = detachedSuccessfulSave, RequiresMarker = true },
    new { Name = "uncertain", Save = detachedUncertainSave, RequiresMarker = true }
};
string detachedRoot = Path.Combine(Path.GetTempPath(),
    "SnapshotRuntimeIntegrationSmoke-detached-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(detachedRoot);
try
{
    foreach (var testCase in detachedCompletionCases)
    {
        string caseRoot = Path.Combine(detachedRoot, testCase.Name);
        string oldPath = Path.Combine(caseRoot, "old", "snapshot.json");
        string currentPath = Path.Combine(caseRoot, "current", "snapshot.json");
        object oldPersistence = Activator.CreateInstance(persistenceType,
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { oldPath }, null)!;
        object currentPersistence = Activator.CreateInstance(persistenceType,
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { currentPath }, null)!;
        object backgroundTask = taskFromResult.Invoke(null, new[] { testCase.Save })!;
        object backgroundPersistence = backgroundConstructor.Invoke(new[] { backgroundTask });
        object storageIdentity = Activator.CreateInstance(storageIdentityType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { "old-world", "old-player" }, null)!;
        object groupIdentity = Activator.CreateInstance(groupIdentityType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { (ushort)700, Guid.Parse("07000000-0000-0000-0000-000000000001") }, null)!;
        object itemSnapshot = Activator.CreateInstance(itemSnapshotType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { (ushort)700, (byte)17, (byte)63, new byte[] { 4, 2, 9 } }, null)!;
        object handle = handleConstructor.Invoke(new[]
        {
            storageIdentity, "old-connection", oldPath, oldPersistence,
            groupIdentity, itemSnapshot, backgroundPersistence
        });
        invalidateHandle.Invoke(handle, null);
        object runtime = runtimeConstructor.Invoke(new object[] { null });
        activePersistenceField.SetValue(runtime, currentPersistence);
        activePersistencePathField.SetValue(runtime, currentPath);
        backgroundStoreField.SetValue(runtime, handle);
        object[] completionArguments = { handle, true, null! };
        Assert((bool)completeBackgroundStore.Invoke(runtime, completionArguments)! &&
               completionArguments[2] != null,
            "A pre-completed detached background store did not produce a completion result.");
        object completion = completionArguments[2];
        bool detached = (bool)completionDetached.GetValue(completion)!;
        bool requiresReconciliation = (bool)completionRequiresReconciliation.GetValue(completion)!;
        bool markerPersisted = (bool)completionMarkerPersisted.GetValue(completion)!;
        bool oldMarkerExists = File.Exists(oldPath + ".reconciliation-required");
        bool currentMarkerExists = File.Exists(currentPath + ".reconciliation-required");
        Assert(detached && requiresReconciliation == testCase.RequiresMarker &&
               markerPersisted == testCase.RequiresMarker &&
               oldMarkerExists == testCase.RequiresMarker && !currentMarkerExists,
            "Detached completion must write reconciliation only to its captured old path. case=" +
            testCase.Name + " requires=" + requiresReconciliation + " marker=" + markerPersisted +
            " old=" + oldMarkerExists + " current=" + currentMarkerExists);
    }
}
finally
{
    Directory.Delete(detachedRoot, recursive: true);
}
ParameterInfo[] nativeExecuteParameters = nativePickupExecutorExecute.GetParameters();
Assert(nativeExecuteParameters.Length == 3 &&
       nativeExecuteParameters[2].ParameterType == typeof(bool) &&
       nativeExecuteParameters[2].HasDefaultValue &&
       Equals(nativeExecuteParameters[2].DefaultValue, true),
    "Native fallback must expose an explicit switch that prevents a failed background stage from re-entering synchronous Snapshot persistence.");
MethodInfo buildAutoPickupSettingsPage = infiniteStorageViewType.GetMethod("BuildAutoPickupSettingsPage",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo rawLastFailureGetter = product.GetType(
    "UnturnedSingleplayerCheatMenu.AutoPickup.AutoPickupDiagnosticsSnapshot", true)!
    .GetProperty("LastFailure")!.GetMethod!;
Assert(buildAutoPickupSettingsPage != null &&
       !ContainsDirectCall(buildAutoPickupSettingsPage, rawLastFailureGetter.MetadataToken),
    "AutoPickup UI must not render raw LastFailure enum/detail composites without structured localization.");

Type patch = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting.SnapshotCraftingPatch", true)!;
Type craftingSearchPolicyType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting.SnapshotCraftingSearchPolicy", true)!;
MethodInfo compiledShouldHandleSearchContents = craftingSearchPolicyType.GetMethod(
    "ShouldHandleSearchContents", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo compiledSearchContentsPostfix = patch.GetMethod("SearchContentsPostfix",
    BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(compiledShouldHandleSearchContents != null && compiledSearchContentsPostfix != null &&
       !(bool)compiledShouldHandleSearchContents.Invoke(null, new object[] { true, null, true })! &&
       !(bool)compiledShouldHandleSearchContents.Invoke(null, new object[] { true, 0, true })! &&
       (bool)compiledShouldHandleSearchContents.Invoke(null, new object[] { true, 1, true })! &&
       ContainsDirectCall(compiledSearchContentsPostfix,
           compiledShouldHandleSearchContents.MetadataToken),
    "The compiled global SearchContents Postfix must call the null-safe crafting-context policy before it can reach the thread-local stack.");
MethodInfo nativeSearch = typeof(PlayerCrafting).GetMethod("UpdateBlueprintInputItemStatus",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo nativeCraft = typeof(PlayerCrafting).GetMethod("HandleCraftRequestInternal",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
Assert(nativeSearch != null && nativeCraft != null, "Current Assembly-CSharp crafting targets must exist.");
MethodInfo nativeForceAddItem = typeof(PlayerInventory).GetMethod("forceAddItem",
    BindingFlags.Instance | BindingFlags.Public, null,
    new[] { typeof(Item), typeof(bool) }, null)!;
MethodInfo outputTranspiler = patch.GetMethod("OutputTranspiler",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo forceAddCraftedOutput = patch.GetMethod("ForceAddCraftedOutput",
    BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(nativeForceAddItem != null &&
       CountCallsByIdentity(typeof(PlayerCrafting).Assembly.Location,
           "SDG.Unturned", "PlayerCrafting", "HandleCraftRequestInternal",
           "SDG.Unturned", "PlayerInventory", "forceAddItem") == 2,
    "Current HandleCraftRequestInternal must contain exactly the two proven native crafted-output grants.");
Assert(CountCallsByIdentity(typeof(PlayerCrafting).Assembly.Location,
           "SDG.Unturned", "UseableGun", "updateAttach",
           "SDG.Unturned", "PlayerInventory", "FindAttachmentsByCaliber") == 5 &&
       CountCallsByIdentity(typeof(PlayerCrafting).Assembly.Location,
           "SDG.Unturned", "PlayerInventory", "FindAttachmentsByCaliber",
           "SDG.Unturned", "PlayerInventory", "SearchContents") == 1,
    "Current native attachment UI must retain its five slot searches through PlayerInventory.SearchContents.");
Assert(outputTranspiler != null && forceAddCraftedOutput != null,
    "Snapshot crafting must replace only the proven output grant call sites instead of globally patching PlayerInventory.forceAddItem.");
CodeInstruction unrelatedInstruction = new CodeInstruction(OpCodes.Nop);
CodeInstruction[] rewrittenOutputCalls = ((IEnumerable<CodeInstruction>)outputTranspiler.Invoke(null,
    new object[]
    {
        new[]
        {
            new CodeInstruction(OpCodes.Callvirt, nativeForceAddItem),
            unrelatedInstruction,
            new CodeInstruction(OpCodes.Callvirt, nativeForceAddItem)
        }
    })!).ToArray();
Assert(rewrittenOutputCalls.Count(instruction =>
           instruction.opcode == OpCodes.Call && Equals(instruction.operand, forceAddCraftedOutput)) == 2 &&
       rewrittenOutputCalls.All(instruction =>
           !(instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, nativeForceAddItem))) &&
       ReferenceEquals(rewrittenOutputCalls[1], unrelatedInstruction),
    "The output transpiler must rewrite exactly the crafted-output grants and preserve unrelated IL instructions.");
Type craftingBatchType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting.SnapshotCraftingBatch", true)!;
Type craftingModuleType = product.GetType(
    "UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting.SnapshotCraftingModule", true)!;
FieldInfo craftingBatchField = patch.GetField("_batch",
    BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo batchCommit = craftingBatchType.GetMethod("Commit",
    BindingFlags.Instance | BindingFlags.NonPublic)!;
MethodInfo outputFinalizer = patch.GetMethod("OutputFinalizer",
    BindingFlags.Static | BindingFlags.NonPublic)!;
Assert(craftingBatchField != null && craftingBatchField.FieldType == craftingBatchType &&
       craftingBatchField.CustomAttributes.Any(attribute =>
           attribute.AttributeType == typeof(ThreadStaticAttribute)) &&
       craftingModuleType.GetMethod("BeginTransaction", BindingFlags.Instance | BindingFlags.NonPublic) != null &&
       craftingModuleType.GetMethod("CommitBatch", BindingFlags.Instance | BindingFlags.NonPublic) != null &&
       batchCommit != null && outputFinalizer != null &&
       ContainsDirectCall(outputFinalizer, batchCommit.MetadataToken),
    "One native craft invocation must share one thread-local Snapshot transaction and commit it once in the outer finalizer.");
Type updateStatusParametersType = nativeSearch.GetParameters()[0].ParameterType.GetElementType()!;
FieldInfo nativeStatusField = updateStatusParametersType.GetField("status",
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
FieldInfo nativeTargetStatusField = nativeStatusField.FieldType.GetField("targetStatus",
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
Assert(nativeStatusField != null && nativeTargetStatusField != null &&
       nativeSearch.GetParameters()[2].ParameterType == nativeTargetStatusField.FieldType &&
       nativeSearch.GetParameters()[3].ParameterType == typeof(ItemJar),
    "Current Assembly target/supply role and ItemToIgnore ABI must match the reflection classifier.");
Assert(patch.GetMethod("SearchPrefix", BindingFlags.Static | BindingFlags.NonPublic)?
           .GetParameters().Single().ParameterType == typeof(object[]) &&
       patch.GetMethod("SearchContentsPostfix", BindingFlags.Static | BindingFlags.NonPublic)?
           .GetParameters().Single().ParameterType == typeof(PlayerInventorySearchParameters).MakeByRefType() &&
        patch.GetMethod("DeletePrefix", BindingFlags.Static | BindingFlags.NonPublic)?
           .GetParameters().Single().ParameterType == typeof(PlayerInventorySearchResultV2).MakeByRefType(),
    "Crafting adapter Harmony seams must match the current native parameter ABI.");
MethodInfo classifySearchRole = patch.GetMethod("TryClassifySearchRole",
    BindingFlags.Static | BindingFlags.NonPublic)!;
object targetStatusIdentity = new object();
FakeBlueprintStatus fakeBlueprintStatus = new FakeBlueprintStatus { targetStatus = targetStatusIdentity };
object[] targetClassifierArguments =
{
    new object[] { new FakeUpdateBlueprintStatusParameters { status = fakeBlueprintStatus }, null!, targetStatusIdentity },
    null!
};
object[] supplyClassifierArguments =
{
    new object[] { new FakeUpdateBlueprintStatusParameters { status = fakeBlueprintStatus }, null!, new object() },
    null!
};
object[] failedClassifierArguments = { new object[] { new object(), null!, new object() }, null! };
bool targetClassified = (bool)classifySearchRole.Invoke(null, targetClassifierArguments)!;
bool supplyClassified = (bool)classifySearchRole.Invoke(null, supplyClassifierArguments)!;
bool failedClassified = (bool)classifySearchRole.Invoke(null, failedClassifierArguments)!;
string targetRole = targetClassifierArguments[1]?.ToString() ?? "null";
string supplyRole = supplyClassifierArguments[1]?.ToString() ?? "null";
string failedRole = failedClassifierArguments[1]?.ToString() ?? "null";
Assert(targetClassified && targetRole == "Target" &&
       supplyClassified && supplyRole == "Supply" &&
       !failedClassified && failedRole == "Target",
    "Crafting search-role classification must distinguish target/supply by status object identity and fail closed as target." +
    " target=" + targetClassified + "/" + targetRole +
    " supply=" + supplyClassified + "/" + supplyRole +
    " failed=" + failedClassified + "/" + failedRole);

string root = FindRepositoryRoot();
string storageSource = Path.Combine(root, "src", "UnturnedSingleplayerCheatMenu", "InfiniteStorage");
string inventoryBridgeSource = File.ReadAllText(Path.Combine(storageSource, "Runtime", "SnapshotInventoryBridge.cs"));
string runtimeSource = File.ReadAllText(Path.Combine(storageSource, "Runtime", "SnapshotInfiniteStorageRuntime.cs"));
string runtimePortSource = File.ReadAllText(Path.Combine(storageSource, "Runtime", "SnapshotStorageRuntimePort.cs"));
string autoPickupRuntimeSource = File.ReadAllText(Path.Combine(root, "src", "UnturnedSingleplayerCheatMenu",
    "AutoPickup", "AutoPickupRuntime.cs"));
string craftingPatchSource = File.ReadAllText(Path.Combine(storageSource, "Adapters", "Crafting", "SnapshotCraftingPatch.cs"));
string craftingModuleSource = File.ReadAllText(Path.Combine(storageSource, "Adapters", "Crafting", "SnapshotCraftingModule.cs"));
string decompositionModuleSource = File.ReadAllText(Path.Combine(storageSource, "Adapters", "Decomposition", "SnapshotDecompositionModule.cs"));
string pluginSource = File.ReadAllText(Path.Combine(root, "src", "UnturnedSingleplayerCheatMenu", "CheatMenuPlugin.cs"));
Assert(runtimeSource.Contains("public bool IsOperational => Enabled && RuntimeSessionBoundary.CaptureCurrent().CanUseSnapshotStorage", StringComparison.Ordinal) &&
       runtimeSource.Contains("public SnapshotKernelTransaction BeginTransaction() => IsOperational", StringComparison.Ordinal),
    "Snapshot runtime entry points must fail closed immediately after leaving true singleplayer, before the next Pump.");
Assert(runtimeSource.Contains("RuntimeSessionBoundary.CaptureConnectionKey(state)", StringComparison.Ordinal) &&
       runtimeSource.Contains("handle.InvalidateSession();", StringComparison.Ordinal) &&
       runtimeSource.Contains("IsBackgroundStoreSessionCurrent(handle)", StringComparison.Ordinal) &&
       runtimeSource.Contains("LatchReconciliationAt(handle.PersistencePath", StringComparison.Ordinal) &&
       runtimeSource.Contains("handle.TargetPersistence", StringComparison.Ordinal) &&
       autoPickupRuntimeSource.Contains("SnapshotBackgroundStoreCompletionRouter.Route(", StringComparison.Ordinal) &&
       autoPickupRuntimeSource.Contains("BuildDetachedBackgroundStoreAttempt", StringComparison.Ordinal),
    "A background Snapshot store must bind its origin connection and old persistence target, then abandon all current-session effects after a boundary change.");
int moduleCommitStart = runtimeSource.IndexOf("public SnapshotTransactionCommitResult Commit(", StringComparison.Ordinal);
int checkpointRestoreStart = runtimeSource.IndexOf("public SnapshotKernelCheckpoint CaptureCheckpoint()", moduleCommitStart, StringComparison.Ordinal);
string moduleCommitBody = runtimeSource.Substring(moduleCommitStart, checkpointRestoreStart - moduleCommitStart);
Assert(moduleCommitBody.Contains("if (!IsOperational)", StringComparison.Ordinal) &&
       moduleCommitBody.Contains("transaction?.Rollback()", StringComparison.Ordinal),
    "A Snapshot module transaction must roll back rather than persist if the session leaves true singleplayer before commit.");
int restoreCheckpointStart = runtimeSource.IndexOf("public SnapshotTransactionCommitResult RestoreCheckpointAndPersist", StringComparison.Ordinal);
int storeSnapshotBoundary = runtimeSource.IndexOf("public SnapshotMutationResult StoreSnapshot", restoreCheckpointStart, StringComparison.Ordinal);
string restoreCheckpointBody = runtimeSource.Substring(restoreCheckpointStart, storeSnapshotBoundary - restoreCheckpointStart);
int commitWithReconciliationStart = runtimeSource.IndexOf("private SnapshotTransactionCommitResult CommitWithReconciliation", StringComparison.Ordinal);
Assert(restoreCheckpointBody.Contains("if (!IsOperational)", StringComparison.Ordinal) &&
       runtimeSource.IndexOf("if (!IsOperational)", commitWithReconciliationStart, StringComparison.Ordinal) > commitWithReconciliationStart,
    "Snapshot persistence and rollback entry points must re-check true-singleplayer capability at their final write boundary.");
int firstAmmoRefreshCall = pluginSource.IndexOf("_snapshotAmmoRefillModule.RefreshAssets();", StringComparison.Ordinal);
int assetRefreshHandler = pluginSource.IndexOf("internal void HandleAssetsRefreshed()", StringComparison.Ordinal);
Assert(firstAmmoRefreshCall < 0 && assetRefreshHandler >= 0 &&
       pluginSource.IndexOf("_snapshotAmmoRefillModule?.RefreshAssets();", assetRefreshHandler,
           StringComparison.Ordinal) > assetRefreshHandler,
    "Snapshot AmmoRefill assets must refresh only from the post-load Assets.onAssetsRefreshed lifecycle, not during Chainloader bootstrap.");
Assert(inventoryBridgeSource.Contains("CarriedPages", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("foreach (byte page in CarriedPages)", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("(byte)0, (byte)1", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("SnapshotInventoryCheckpoint", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("CapturedClothing", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("CapturedEquipmentSelection", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("SnapshotNativeRollbackCoordinator.Execute", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("clothing.ReceiveWearShirt", StringComparison.Ordinal) &&
       inventoryBridgeSource.Contains("clothing.ReceiveWearBackpack", StringComparison.Ordinal) &&
       !inventoryBridgeSource.Contains("clothing.askWear", StringComparison.Ordinal) &&
       !inventoryBridgeSource.Contains("new HashSet<byte>(captured.Select", StringComparison.Ordinal),
    "Native rollback must clear and restore every carried page, including pages that were empty at checkpoint time.");
Assert(runtimeSource.Contains("private bool TryRestoreInventory(", StringComparison.Ordinal) &&
       runtimeSource.Contains("RollbackTransaction(transaction", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotNativeRecoveryCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotCrossBoundaryRecoveryCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("LatchReconciliationFailure", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("return __exception;", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("crafting abort rollback threw", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("latchReconciliation:", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("SnapshotCraftingReconciliationReason.Build", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("TryClassifySearchRole", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("SnapshotCraftingSearchPolicy.ShouldInjectSynthetic", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("SnapshotCraftingSearchPolicy.ShouldExcludeItemToIgnore", StringComparison.Ordinal) &&
       craftingModuleSource.Contains("parameters.ItemToIgnore", StringComparison.Ordinal) &&
       craftingModuleSource.Contains("LatchReconciliationFailure", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("_module?.IsOperational != true", StringComparison.Ordinal),
    "Native inventory exceptions must roll back staged Snapshot state, latch failed recovery, and preserve crafting's original exception.");
Assert(runtimeSource.Contains("HasReconciliationLatch", StringComparison.Ordinal) &&
       runtimeSource.Contains("TryCreateReconciliationLatch", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotReconciliationLatchCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("LatchReconciliationFailure", StringComparison.Ordinal),
    "Snapshot Runtime does not persist or honor external reconciliation latches across activation.");
Assert(runtimePortSource.Contains("bool LatchReconciliationFailure(string reason);", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotNativeRecoveryCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotStorageRollbackReconciliationCoordinator.Observe", StringComparison.Ordinal) &&
       runtimeSource.Contains("persistReconciliation:", StringComparison.Ordinal),
    "Native StoreAll/Withdraw recovery failures do not flow through the persistent reconciliation port.");
int checkpointRollbackStart = runtimeSource.IndexOf(
    "public SnapshotTransactionCommitResult RestoreCheckpointAndPersist", StringComparison.Ordinal);
int storeSnapshotStart = runtimeSource.IndexOf("public SnapshotMutationResult StoreSnapshot", checkpointRollbackStart,
    StringComparison.Ordinal);
string checkpointRollbackBody = runtimeSource.Substring(checkpointRollbackStart,
    storeSnapshotStart - checkpointRollbackStart);
Assert(checkpointRollbackBody.Contains("SnapshotStorageRollbackReconciliationCoordinator.Observe", StringComparison.Ordinal),
    "Crafting checkpoint rollback diagnostics bypass the tested recovery/candidate path propagation.");
Assert(runtimeSource.Contains("HasReconciliationLatchForCurrentIdentity", StringComparison.Ordinal) &&
       runtimeSource.Contains("SnapshotReconciliationPathLatch", StringComparison.Ordinal) &&
       runtimeSource.Contains("_reconciliationPathLatch.IsBlocked(path", StringComparison.Ordinal) &&
       autoPickupRuntimeSource.Contains("RequiresInfiniteStorageReconciliation", StringComparison.Ordinal) &&
       autoPickupRuntimeSource.Contains("isReconciliationRequired: _requiresReconciliation", StringComparison.Ordinal) &&
       autoPickupRuntimeSource.Contains("AutoPickupExecutionPath.ReconciliationBlocked", StringComparison.Ordinal),
    "AutoPickup does not honor the persistent Snapshot reconciliation latch after scheduler/feature recreation.");
int decomposeCanStart = decompositionModuleSource.IndexOf("public bool CanExecute(", StringComparison.Ordinal);
int decomposeExecuteStart = decompositionModuleSource.IndexOf("public SnapshotModuleExecutionResult Execute(", StringComparison.Ordinal);
string decomposeCanBody = decompositionModuleSource.Substring(decomposeCanStart,
    decomposeExecuteStart - decomposeCanStart);
Assert(decomposeCanBody.Contains("TryResolveBlueprint", StringComparison.Ordinal) &&
       !decomposeCanBody.Contains("TryBuildRecipe", StringComparison.Ordinal) &&
       !decomposeCanBody.Contains("new Item(", StringComparison.Ordinal),
    "Decomposition eligibility checks must not instantiate output items or advance Unity RNG.");
Assert(craftingModuleSource.Contains("SnapshotCraftingPlanner.TryPlanSelected", StringComparison.Ordinal),
    "The native crafting Hook bypasses the planner exercised by SnapshotCraftingAdapterSmoke.");
Assert(craftingPatchSource.Contains("SnapshotCraftingAbortCoordinator.Execute", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("restoreStorage:", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("restoreNative:", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("SnapshotCraftingRollbackGate.Execute", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("SnapshotCraftingRollbackGate.IsActive", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("nameof(OutputTranspiler)", StringComparison.Ordinal) &&
       craftingPatchSource.Contains("ForceAddCraftedOutput", StringComparison.Ordinal) &&
       !craftingPatchSource.Contains(
           "harmony.Patch(AccessTools.Method(typeof(PlayerInventory), \"forceAddItem\"",
           StringComparison.Ordinal),
    "The Harmony crafting finalizer bypasses the tested storage-first abort coordinator.");
Assert(craftingModuleSource.Contains("RegisterDefaultTemplate", StringComparison.Ordinal) &&
       decompositionModuleSource.Contains("RegisterDefaultTemplate", StringComparison.Ordinal),
    "Adapter-created ItemGroups do not register runtime-only default templates through the Core port.");
Assert(pluginSource.Contains("CraftingAdapterEnabled", StringComparison.Ordinal) &&
       pluginSource.Contains("AmmoRefillAdapterEnabled", StringComparison.Ordinal) &&
       pluginSource.Contains("DecompositionAdapterEnabled", StringComparison.Ordinal) &&
       pluginSource.Contains("_snapshotStorageModules.Register(craftingModule)", StringComparison.Ordinal) &&
       pluginSource.Contains("_snapshotStorageModules.Register(_snapshotAmmoRefillModule)", StringComparison.Ordinal) &&
       pluginSource.Contains("_snapshotStorageModules.Register(decompositionModule)", StringComparison.Ordinal) &&
       pluginSource.Contains("SnapshotCraftingPatch.Configure(craftingModule)", StringComparison.Ordinal),
    "The one plugin composition root does not independently configure and register all three internal adapters.");
foreach (string moduleDirectory in new[] { "Crafting", "AmmoRefill", "Decomposition" })
{
    string[] moduleSources = Directory.GetFiles(Path.Combine(storageSource, "Adapters", moduleDirectory), "*.cs");
    foreach (string moduleSource in moduleSources)
    {
        string text = File.ReadAllText(moduleSource);
        foreach (string other in new[] { "Crafting", "AmmoRefill", "Decomposition" }.Where(value => value != moduleDirectory))
            Assert(!text.Contains("Adapters." + other, StringComparison.Ordinal),
                moduleDirectory + " adapter depends on sibling adapter " + other + ": " + moduleSource);
    }
}
Assert(!Directory.EnumerateFiles(storageSource, "*.csproj", SearchOption.AllDirectories).Any(),
    "Infinite Storage modules must not create product csproj or companion DLL projects.");
foreach (string coreFile in Directory.EnumerateFiles(Path.Combine(storageSource, "SnapshotKernel"), "*.cs"))
{
    string source = File.ReadAllText(coreFile);
    Assert(!source.Contains("InfiniteStorage.Adapters", StringComparison.Ordinal),
        "Snapshot Core must not reference adapter namespaces: " + coreFile);
}

Console.WriteLine("Snapshot runtime integration smoke checks passed; one product DLL and one BepInPlugin entry verified.");

internal sealed class FakeUpdateBlueprintStatusParameters
{
    public FakeBlueprintStatus status;
}

internal sealed class FakeBlueprintStatus
{
    public object targetStatus;
}
