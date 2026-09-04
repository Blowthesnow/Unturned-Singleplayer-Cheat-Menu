using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string Fingerprint(SnapshotKernel kernel) => string.Join(";", kernel.GetGroups()
    .SelectMany(groupValue => groupValue.Buckets.Select(bucket => string.Join("|",
        groupValue.AssetId, groupValue.AssetGuid, bucket.Snapshot.Amount,
        bucket.Snapshot.Quality, Convert.ToBase64String(bucket.Snapshot.State), bucket.Count))));

ItemGroupIdentity group = new ItemGroupIdentity(500,
    Guid.Parse("50000000-0000-0000-0000-000000000001"));
ItemSnapshot amountTen = new ItemSnapshot(500, 10, 73, new byte[] { 4, 5, 6 });
SnapshotStorageIdentity storageIdentity = new SnapshotStorageIdentity("world", "player");

foreach (string operation in new[] { "FillTargetItem", "RepairTargetItem" })
{
    SnapshotKernel targetOnlyKernel = new SnapshotKernel();
    Assert(targetOnlyKernel.Store(group, amountTen, 1).Success,
        operation + " target-only fixture should contain one Snapshot target/same-asset bucket.");
    string storageBeforeTargetSearch = Fingerprint(targetOnlyKernel);
    const string nativeBeforeTargetSearch = "native-target=missing;native-supplies=unchanged";
    int persistenceCalls = 0;
    bool syntheticTargetInjected = SnapshotCraftingSearchPolicy.ShouldInjectSynthetic(
        SnapshotCraftingSearchRole.Target);
    bool craftSucceeded = false;
    if (syntheticTargetInjected)
    {
        SnapshotKernelTransaction unsafeTargetTransaction =
            new SnapshotKernelTransaction(targetOnlyKernel, storageIdentity);
        Assert(unsafeTargetTransaction.Withdraw(group, amountTen, 1).Success,
            operation + " unsafe-target simulation should select the same Snapshot bucket as a supply.");
        craftSucceeded = unsafeTargetTransaction.Commit(_ =>
        {
            persistenceCalls++;
            return SnapshotSaveResult.Succeeded();
        }).Success;
    }
    Assert(!syntheticTargetInjected && !craftSucceeded && persistenceCalls == 0 &&
           Fingerprint(targetOnlyKernel) == storageBeforeTargetSearch &&
           nativeBeforeTargetSearch == "native-target=missing;native-supplies=unchanged",
        operation + " must remain uncraftable when the target exists only in Snapshot storage; no storage/native mutation may persist.");
}

object nativeTargetJar = new object();
object sameAssetOtherJar = new object();
object[] sameAssetCandidates = { nativeTargetJar, sameAssetOtherJar };
object[] filteredSameAssetCandidates = sameAssetCandidates
    .Where(candidate => !SnapshotCraftingSearchPolicy.ShouldExcludeItemToIgnore(
        candidate, nativeTargetJar)).ToArray();
Assert(filteredSameAssetCandidates.Length == 1 &&
       ReferenceEquals(filteredSameAssetCandidates[0], sameAssetOtherJar),
    "Supply search must exclude the exact ItemToIgnore instance even when target and supply share an asset ID.");

SnapshotKernel itemKernel = new SnapshotKernel();
Assert(itemKernel.Store(group, amountTen, 3).Success, "TotalItems fixture should store three snapshots.");
Assert(SnapshotCraftingPlanner.TryPlan(itemKernel.GetGroups(), group, requiredUnits: 2,
        SnapshotCraftingCountingMethod.TotalItems, countEmptyAsOne: false,
        out SnapshotCraftingPlan itemPlan, out string itemPlanError),
    "TotalItems should plan against snapshot instance Count. Error=" + itemPlanError);
SnapshotKernelTransaction itemTransaction = new SnapshotKernelTransaction(itemKernel, storageIdentity);
Assert(itemPlan.Apply(itemTransaction).Success &&
       itemTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success &&
       itemKernel.GetGroups().Single().Buckets.Single().Count == 1 &&
       itemKernel.GetGroups().Single().Buckets.Single().Snapshot.Amount == 10,
    "TotalItems must consume two whole snapshots without rewriting opaque amount.");

SnapshotKernel amountKernel = new SnapshotKernel();
Assert(amountKernel.Store(group, amountTen, 3).Success, "TotalAmount fixture should store three snapshots.");
Assert(SnapshotCraftingPlanner.TryPlan(amountKernel.GetGroups(), group, requiredUnits: 12,
        SnapshotCraftingCountingMethod.TotalAmount, countEmptyAsOne: false,
        out SnapshotCraftingPlan amountPlan, out string amountPlanError),
    "TotalAmount should be interpreted only by the crafting adapter. Error=" + amountPlanError);
SnapshotKernelTransaction amountTransaction = new SnapshotKernelTransaction(amountKernel, storageIdentity);
Assert(amountPlan.Apply(amountTransaction).Success &&
       amountTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success,
    "TotalAmount plan should commit its exact snapshot rewrite.");
ItemGroupView amountGroup = amountKernel.GetGroups().Single();
Assert(amountGroup.TotalCount == 2 &&
       amountGroup.Buckets.Any(bucket => bucket.Snapshot.Amount == 10 && bucket.Count == 1) &&
       amountGroup.Buckets.Any(bucket => bucket.Snapshot.Amount == 8 && bucket.Count == 1 &&
           bucket.Snapshot.Quality == 73 && bucket.Snapshot.State.SequenceEqual(new byte[] { 4, 5, 6 })),
    "Consuming 12 from three amount-10 snapshots must leave one amount-10 and one exact amount-8 snapshot.");

string beforeInsufficient = string.Join(";", amountKernel.GetGroups().Single().Buckets.Select(bucket =>
    bucket.Snapshot.Amount + ":" + bucket.Count));
Assert(!SnapshotCraftingPlanner.TryPlan(amountKernel.GetGroups(), group, requiredUnits: 19,
        SnapshotCraftingCountingMethod.TotalAmount, countEmptyAsOne: false,
        out _, out string insufficientError) && insufficientError == "insufficient-units" &&
       beforeInsufficient == string.Join(";", amountKernel.GetGroups().Single().Buckets.Select(bucket =>
           bucket.Snapshot.Amount + ":" + bucket.Count)),
    "An insufficient crafting requirement must not mutate the kernel.");

SnapshotKernel rollbackKernel = new SnapshotKernel();
Assert(rollbackKernel.Store(group, amountTen, 2).Success,
    "Rollback fixture should store its original exact snapshots.");
Assert(SnapshotCraftingPlanner.TryPlan(rollbackKernel.GetGroups(), group, 6,
        SnapshotCraftingCountingMethod.TotalAmount, false, out SnapshotCraftingPlan rollbackPlan, out _),
    "Rollback fixture should produce a valid partial plan.");
SnapshotKernelTransaction rollbackTransaction = new SnapshotKernelTransaction(rollbackKernel, storageIdentity);
Assert(rollbackPlan.Apply(rollbackTransaction).Success, "Rollback plan should stage successfully.");
SnapshotTransactionCommitResult rollbackCommit = rollbackTransaction.Commit(_ =>
    SnapshotSaveResult.Failed("injected-save-failure"));
Assert(!rollbackCommit.Success && rollbackCommit.RollbackSucceeded &&
       rollbackKernel.GetGroups().Single().Buckets.Single().Snapshot.Amount == 10 &&
       rollbackKernel.GetGroups().Single().Buckets.Single().Count == 2,
    "Crafting persistence failure must restore every original snapshot and count.");

ItemSnapshot selectedQuality = new ItemSnapshot(500, 10, 22, new byte[] { 7, 8 });
SnapshotKernel selectedKernel = new SnapshotKernel();
Assert(selectedKernel.Store(group, amountTen, 1).Success &&
       selectedKernel.Store(group, selectedQuality, 1).Success,
    "Selected-snapshot fixture should keep two exact quality/state buckets.");
Assert(SnapshotCraftingPlanner.TryPlanSelected(selectedKernel.GetGroups(), group, selectedQuality,
        requiredUnits: 3, SnapshotCraftingCountingMethod.TotalAmount, countEmptyAsOne: false,
        preserveZeroResidual: false, out SnapshotCraftingPlan selectedPlan, out string selectedError),
    "The runtime synthetic-item path should plan against the selected exact bucket. Error=" + selectedError);
SnapshotKernelTransaction selectedTransaction = new SnapshotKernelTransaction(selectedKernel, storageIdentity);
Assert(selectedPlan.Apply(selectedTransaction).Success &&
       selectedTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success &&
       selectedKernel.GetGroups().Single().Buckets.Any(bucket => bucket.Snapshot.Equals(amountTen) && bucket.Count == 1) &&
       selectedKernel.GetGroups().Single().Buckets.Any(bucket => bucket.Snapshot.Amount == 7 &&
           bucket.Snapshot.Quality == 22 && bucket.Snapshot.State.SequenceEqual(new byte[] { 7, 8 })),
    "Consuming a selected synthetic snapshot must not remove a different quality/state bucket.");

List<string> abortOrder = new List<string>();
SnapshotCraftingAbortResult successfulAbort = SnapshotCraftingAbortCoordinator.Execute(
    () => { abortOrder.Add("storage"); return true; },
    () => { abortOrder.Add("native"); return true; },
    () => { abortOrder.Add("latch"); return true; });
Assert(successfulAbort.Success && !successfulAbort.ReconciliationAttempted &&
       abortOrder.SequenceEqual(new[] { "storage", "native" }),
    "Crafting abort must restore persisted Snapshot state before native inventory.");
abortOrder.Clear();
SnapshotCraftingAbortResult failedStorageAbort = SnapshotCraftingAbortCoordinator.Execute(
    () => { abortOrder.Add("storage"); return false; },
    () => { abortOrder.Add("native"); return true; },
    () => { abortOrder.Add("latch"); return true; });
Assert(!failedStorageAbort.Success && failedStorageAbort.StorageRollbackAttempted &&
       !failedStorageAbort.NativeRollbackAttempted && failedStorageAbort.MutationsClosed &&
       failedStorageAbort.ReconciliationAttempted && failedStorageAbort.MarkerPersisted &&
       abortOrder.SequenceEqual(new[] { "storage", "latch" }),
    "A failed storage rollback must skip native restoration and latch Core to avoid duplicate materials or outputs.");
abortOrder.Clear();
SnapshotCraftingAbortResult failedNativeAbort = SnapshotCraftingAbortCoordinator.Execute(
    () => { abortOrder.Add("storage"); return true; },
    () => { abortOrder.Add("native"); return false; },
    () => { abortOrder.Add("latch"); return true; });
Assert(!failedNativeAbort.Success && failedNativeAbort.NativeRollbackAttempted &&
       failedNativeAbort.MutationsClosed && failedNativeAbort.MarkerPersisted &&
       abortOrder.SequenceEqual(new[] { "storage", "native", "latch" }),
    "A native rollback failure after a successful storage rollback must latch Core.");
SnapshotCraftingAbortResult markerFailureAbort = SnapshotCraftingAbortCoordinator.Execute(
    () => false, () => throw new InvalidOperationException("must-not-restore-native"), () => false);
Assert(!markerFailureAbort.Success && markerFailureAbort.ReconciliationAttempted &&
       !markerFailureAbort.MarkerPersisted && markerFailureAbort.MutationsClosed &&
       markerFailureAbort.ErrorCode == "storage-rollback-failed:marker-persistence-failed",
    "Crafting must remain fail-closed and expose marker persistence failure without attempting native restore.");

SnapshotTransactionCommitResult craftingEvidenceFailure = SnapshotTransactionCommitResult.Failed(
    "checkpoint-save-uncertain", rollbackSucceeded: false,
    recoveryBackupPath: "crafting-instant-A-or-C.json",
    candidateRecoveryPath: "crafting-candidate-B.json",
    candidateHistoryRecoveryPath: "crafting-history-C.json");
string craftingEvidenceReason = SnapshotCraftingReconciliationReason.Build(
    "InvalidOperationException", craftingEvidenceFailure);
int craftingMarkerAttempts = 0;
string finalCraftingMarkerReason = string.Empty;
bool PersistCraftingMarker(string reason)
{
    craftingMarkerAttempts++;
    if (craftingMarkerAttempts == 1) return false;
    finalCraftingMarkerReason = reason;
    return true;
}
Assert(!PersistCraftingMarker(craftingEvidenceReason),
    "Crafting diagnostic fixture should fail its first marker write.");
SnapshotCraftingAbortResult secondMarkerAbort = SnapshotCraftingAbortCoordinator.Execute(
    () => false, () => throw new InvalidOperationException("must-not-restore-native"),
    () => PersistCraftingMarker(craftingEvidenceReason));
Assert(secondMarkerAbort.MarkerPersisted && craftingMarkerAttempts == 2 &&
       finalCraftingMarkerReason.Contains("recovery=crafting-instant-A-or-C.json", StringComparison.Ordinal) &&
       finalCraftingMarkerReason.Contains("candidate=crafting-candidate-B.json", StringComparison.Ordinal) &&
       finalCraftingMarkerReason.Contains("candidate-history=crafting-history-C.json", StringComparison.Ordinal),
    "If the first marker write fails, the second successful Crafting latch must retain all recovery paths.");

bool observedRollbackScope = false;
SnapshotCraftingRollbackGate.Execute(() =>
{
    observedRollbackScope = SnapshotCraftingRollbackGate.IsActive;
    return true;
});
Assert(observedRollbackScope && !SnapshotCraftingRollbackGate.IsActive,
    "Crafting rollback-generated native additions must be bypassed only inside the rollback scope.");
try
{
    SnapshotCraftingRollbackGate.Execute<bool>(() => throw new InvalidOperationException("rollback-scope-fault"));
}
catch (InvalidOperationException)
{
}
Assert(!SnapshotCraftingRollbackGate.IsActive,
    "Crafting rollback scope must reset after an exception so later crafted outputs remain intercepted.");

Console.WriteLine("Snapshot crafting adapter smoke checks passed.");
