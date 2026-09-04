using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

ItemGroupIdentity group = new ItemGroupIdentity(900,
    Guid.Parse("90000000-0000-0000-0000-000000000001"));
ItemSnapshot snapshot = new ItemSnapshot(900, 7, 88, new byte[] { 1 });
FakeRuntimePort port = new FakeRuntimePort(group, snapshot);
SnapshotModuleRegistry registry = new SnapshotModuleRegistry();
Assert(registry.Actions.Count == 0 && port.IsOperational && port.Groups.Single().TotalCount == 1,
    "Core must remain complete and operational when no optional modules are registered.");

Assert(registry.Register(new FakeModule("crafting", SnapshotModuleKind.RuntimeHook, enabled: false,
           _ => SnapshotModuleExecutionResult.Succeeded("must-not-run"))) && registry.Actions.Count == 0,
    "A disabled module may be registered but must expose no UI action.");
Assert(registry.Execute("crafting", SnapshotModuleRequest.Empty).ErrorCode == "module-unavailable" &&
       port.Groups.Single().TotalCount == 1,
    "A disabled module must not execute or mutate Core through a hidden entry point.");
Assert(registry.Register(new FakeModule("ammo", SnapshotModuleKind.GlobalAction, enabled: true,
           _ => throw new InvalidOperationException("injected-module-failure"))) &&
       registry.Register(new FakeModule("decomposition", SnapshotModuleKind.BucketAction, enabled: true,
           _ => SnapshotModuleExecutionResult.Succeeded("decomposed"))),
    "Ammo and decomposition modules should register independently.");
Assert(!registry.Register(new FakeModule("ammo", SnapshotModuleKind.GlobalAction, enabled: true,
           _ => SnapshotModuleExecutionResult.Succeeded("duplicate"))),
    "Duplicate module IDs must be rejected without replacing the original registration.");
SnapshotModuleExecutionResult failed = registry.Execute("ammo", SnapshotModuleRequest.Empty);
Assert(!failed.Success && failed.ErrorCode == "module-threw:InvalidOperationException" &&
       port.IsOperational && port.Groups.Single().TotalCount == 1,
    "A module exception must be contained and leave Core state untouched.");
SnapshotModuleExecutionResult missing = registry.Execute("missing", SnapshotModuleRequest.Empty);
Assert(!missing.Success && missing.ErrorCode == "module-unavailable" &&
       registry.Actions.Count == 2 && registry.Actions.Any(action => action.Id == "ammo") &&
       registry.Actions.Any(action => action.Id == "decomposition"),
    "Missing registration must fail locally while other enabled modules remain available.");

Console.WriteLine("Snapshot module registry smoke checks passed.");

internal sealed class FakeRuntimePort : ISnapshotStorageRuntimePort
{
    private readonly SnapshotKernel _kernel = new SnapshotKernel();
    private readonly SnapshotStorageIdentity _identity = new SnapshotStorageIdentity("world", "player");

    internal FakeRuntimePort(ItemGroupIdentity group, ItemSnapshot snapshot) => _kernel.Store(group, snapshot, 1);
    public bool IsOperational => true;
    public IReadOnlyList<ItemGroupView> Groups => _kernel.GetGroups();
    public bool TryGetGroup(ItemGroupIdentity identity, out ItemGroupView group) =>
        _kernel.TryGetGroup(identity, out group) && group.TotalCount > 0;
    public SnapshotKernelTransaction BeginTransaction() => new SnapshotKernelTransaction(_kernel, _identity);
    public SnapshotTransactionCommitResult Commit(SnapshotKernelTransaction transaction) =>
        transaction.Commit(_ => SnapshotSaveResult.Succeeded());
    public SnapshotKernelCheckpoint CaptureCheckpoint() => _kernel.CreateCheckpoint();
    public SnapshotTransactionCommitResult RestoreCheckpointAndPersist(SnapshotKernelCheckpoint checkpoint)
    {
        SnapshotMutationResult result = _kernel.RestoreCheckpoint(checkpoint);
        return result.Success ? SnapshotTransactionCommitResult.Succeeded() :
            SnapshotTransactionCommitResult.Failed(result.ErrorCode, false);
    }
    public SnapshotMutationResult StoreSnapshot(ItemGroupIdentity identity, ItemSnapshot snapshot) =>
        _kernel.Store(identity, snapshot, 1);
    public SnapshotMutationResult RegisterDefaultTemplate(ItemGroupIdentity identity, ItemSnapshot snapshot) =>
        _kernel.RegisterDefaultTemplate(identity, snapshot);
    public bool LatchReconciliationFailure(string reason) => true;
}

internal sealed class FakeModule : ISnapshotStorageModule
{
    private readonly Func<SnapshotModuleRequest, SnapshotModuleExecutionResult> _execute;

    internal FakeModule(string id, SnapshotModuleKind kind, bool enabled,
        Func<SnapshotModuleRequest, SnapshotModuleExecutionResult> execute)
    {
        Id = id;
        Kind = kind;
        IsEnabled = enabled;
        _execute = execute;
    }

    public string Id { get; }
    public string Label => Id;
    public string Tooltip => Id;
    public SnapshotModuleKind Kind { get; }
    public bool IsEnabled { get; }
    public bool CanExecute(SnapshotModuleRequest request) => true;
    public SnapshotModuleExecutionResult Execute(SnapshotModuleRequest request) => _execute(request);
}
