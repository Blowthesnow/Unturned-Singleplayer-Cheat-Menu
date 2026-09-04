using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Decomposition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

SnapshotStorageIdentity storageIdentity = new SnapshotStorageIdentity("world", "player");
ItemGroupIdentity sourceGroup = new ItemGroupIdentity(800,
    Guid.Parse("80000000-0000-0000-0000-000000000001"));
ItemGroupIdentity outputGroup = new ItemGroupIdentity(801,
    Guid.Parse("80000000-0000-0000-0000-000000000002"));
ItemSnapshot sourceA = new ItemSnapshot(800, 1, 90, new byte[] { 1, 2 });
ItemSnapshot sourceB = new ItemSnapshot(800, 1, 40, new byte[] { 3, 4 });
ItemSnapshot defaultOutput = new ItemSnapshot(801, 5, 100, new byte[] { 9 });

SnapshotDecompositionRecipe normalRecipe = new SnapshotDecompositionRecipe(new[]
{
    new SnapshotDecompositionOutput(outputGroup, defaultOutput, instancesPerInput: 2,
        transferSourceSnapshot: false)
});
SnapshotKernel singleKernel = new SnapshotKernel();
Assert(singleKernel.Store(sourceGroup, sourceA, 2).Success &&
       singleKernel.Store(sourceGroup, sourceB, 1).Success,
    "Decomposition fixture should store two exact source buckets.");
Assert(SnapshotDecompositionPlanner.TryPlan(singleKernel.GetGroups(), sourceGroup, sourceA,
        SnapshotDecompositionScope.Single, normalRecipe, out SnapshotDecompositionPlan singlePlan,
        out string singleError),
    "Single scope should plan one selected snapshot. Error=" + singleError);
SnapshotKernelTransaction singleTransaction = new SnapshotKernelTransaction(singleKernel, storageIdentity);
Assert(singlePlan.Apply(singleTransaction).Success &&
       singleTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success &&
       singleKernel.GetGroups().Single(group => group.AssetId == 800).TotalCount == 2 &&
       singleKernel.GetGroups().Single(group => group.AssetId == 801).Buckets.Single().Count == 2,
    "Single scope must remove one source instance and add exactly two blueprint output instances.");

SnapshotDecompositionRecipe transferRecipe = new SnapshotDecompositionRecipe(new[]
{
    new SnapshotDecompositionOutput(outputGroup, defaultOutput, instancesPerInput: 1,
        transferSourceSnapshot: true)
});
SnapshotKernel groupKernel = new SnapshotKernel();
Assert(groupKernel.Store(sourceGroup, sourceA, 2).Success && groupKernel.Store(sourceGroup, sourceB, 1).Success,
    "Asset-group fixture should store both exact source buckets.");
Assert(SnapshotDecompositionPlanner.TryPlan(groupKernel.GetGroups(), sourceGroup, sourceA,
        SnapshotDecompositionScope.AssetGroup, transferRecipe, out SnapshotDecompositionPlan groupPlan, out _),
    "AssetGroup scope should plan every bucket in one ItemGroup.");
SnapshotKernelTransaction groupTransaction = new SnapshotKernelTransaction(groupKernel, storageIdentity);
Assert(groupPlan.Apply(groupTransaction).Success &&
       groupTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success,
    "Transferred-state decomposition should commit.");
ItemGroupView transferred = groupKernel.GetGroups().Single();
Assert(transferred.AssetId == 801 && transferred.TotalCount == 3 && transferred.Buckets.Count == 2 &&
       transferred.Buckets.Any(bucket => bucket.Count == 2 && bucket.Snapshot.Amount == 1 &&
           bucket.Snapshot.Quality == 90 && bucket.Snapshot.State.SequenceEqual(new byte[] { 1, 2 })) &&
       transferred.Buckets.Any(bucket => bucket.Count == 1 && bucket.Snapshot.Amount == 1 &&
           bucket.Snapshot.Quality == 40 && bucket.Snapshot.State.SequenceEqual(new byte[] { 3, 4 })),
    "TransferState must preserve opaque amount, quality, and state per source bucket without cross-merging.");

SnapshotKernel rollbackKernel = new SnapshotKernel();
Assert(rollbackKernel.Store(sourceGroup, sourceA, 2).Success,
    "Decomposition rollback fixture should store two sources.");
Assert(SnapshotDecompositionPlanner.TryPlan(rollbackKernel.GetGroups(), sourceGroup, sourceA,
        SnapshotDecompositionScope.Bucket, normalRecipe, out SnapshotDecompositionPlan rollbackPlan, out _),
    "Bucket scope should plan the selected bucket count.");
SnapshotKernelTransaction rollbackTransaction = new SnapshotKernelTransaction(rollbackKernel, storageIdentity);
Assert(rollbackPlan.Apply(rollbackTransaction).Success, "Decomposition rollback plan should stage.");
SnapshotTransactionCommitResult rollbackCommit = rollbackTransaction.Commit(_ =>
    SnapshotSaveResult.Failed("injected-save-failure"));
Assert(!rollbackCommit.Success && rollbackCommit.RollbackSucceeded &&
       rollbackKernel.GetGroups().Single().AssetId == 800 &&
       rollbackKernel.GetGroups().Single().Buckets.Single().Count == 2,
    "Decomposition persistence failure must restore every source and remove staged products.");

SnapshotDecompositionRecipe overflowingRecipe = new SnapshotDecompositionRecipe(new[]
{
    new SnapshotDecompositionOutput(outputGroup, defaultOutput, long.MaxValue, false)
});
Assert(!SnapshotDecompositionPlanner.TryPlan(rollbackKernel.GetGroups(), sourceGroup, sourceA,
        SnapshotDecompositionScope.Bucket, overflowingRecipe, out _, out string overflowError) &&
       overflowError == "arithmetic-overflow" && rollbackKernel.GetGroups().Single().TotalCount == 2,
    "Output count overflow must fail during planning without mutating storage.");

Console.WriteLine("Snapshot decomposition adapter smoke checks passed.");
