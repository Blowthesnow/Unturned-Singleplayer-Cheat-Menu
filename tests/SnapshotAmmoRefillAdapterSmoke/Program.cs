using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.AmmoRefill;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.ItemStacking;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string KernelFingerprint(SnapshotKernel value) => string.Join(";", value.GetGroups()
    .OrderBy(group => group.AssetId)
    .SelectMany(group => group.Buckets.Select(bucket => string.Join("|",
        group.AssetId, group.AssetGuid.ToString("D"), bucket.Snapshot.Amount,
        bucket.Snapshot.Quality, Convert.ToBase64String(bucket.Snapshot.State), bucket.Count))));

Guid targetGuid = Guid.Parse("70000000-0000-0000-0000-000000000001");
Guid sourceGuid = Guid.Parse("70000000-0000-0000-0000-000000000002");
ItemGroupIdentity targetGroup = new ItemGroupIdentity(700, targetGuid);
ItemGroupIdentity sourceGroup = new ItemGroupIdentity(701, sourceGuid);
ItemSnapshot targetSnapshot = new ItemSnapshot(700, 5, 61, new byte[] { 8, 9 });
ItemSnapshot sourceSnapshot = new ItemSnapshot(701, 10, 44, Array.Empty<byte>());
AmmoRefillAssetRules targetRules = new AmmoRefillAssetRules(targetGuid.ToString("D"),
    isMagazine: true, isDirectSource: false, new[] { sourceGuid.ToString("D") });
AmmoRefillAssetRules sourceRules = new AmmoRefillAssetRules(sourceGuid.ToString("D"),
    isMagazine: false, isDirectSource: true, Array.Empty<string>());

SnapshotKernel kernel = new SnapshotKernel();
Assert(kernel.Store(targetGroup, targetSnapshot, 2).Success &&
       kernel.Store(sourceGroup, sourceSnapshot, 1).Success,
    "Ammo fixture should contain two half-full targets and one source snapshot.");
SnapshotAmmoCandidate target = new SnapshotAmmoCandidate("target", targetGroup, targetSnapshot,
    count: 2, capacity: 10, SnapshotAmmoRole.Target, targetRules);
SnapshotAmmoCandidate source = new SnapshotAmmoCandidate("source", sourceGroup, sourceSnapshot,
    count: 1, capacity: 10, SnapshotAmmoRole.Source, sourceRules);
Assert(SnapshotAmmoRefillPlanner.TryPlan(new[] { target, source }, out SnapshotAmmoRefillPlan plan,
        out string planError),
    "Compatible target-only/source-only roles should plan. Error=" + planError);
SnapshotKernelTransaction transaction = new SnapshotKernelTransaction(kernel,
    new SnapshotStorageIdentity("world", "player"));
Assert(plan.Apply(transaction).Success && transaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success,
    "Ammo refill plan should commit exact bucket rewrites.");
ItemGroupView rewrittenTarget = kernel.GetGroups().Single(group => group.AssetId == 700);
Assert(rewrittenTarget.Buckets.Count == 1 && rewrittenTarget.Buckets[0].Snapshot.Amount == 10 &&
       rewrittenTarget.Buckets[0].Count == 2 && rewrittenTarget.Buckets[0].Snapshot.Quality == 61 &&
       rewrittenTarget.Buckets[0].Snapshot.State.SequenceEqual(new byte[] { 8, 9 }) &&
       kernel.GetGroups().All(group => group.AssetId != 701) && plan.TotalRounds == 20,
    "Refill must conserve 20 rounds while preserving target quality/state and consuming only the source role.");

SnapshotAmmoCandidate targetOnly = new SnapshotAmmoCandidate("target-only", targetGroup, targetSnapshot,
    1, 10, SnapshotAmmoRole.Target, targetRules);
SnapshotAmmoCandidate sourceOnly = new SnapshotAmmoCandidate("source-only", sourceGroup, sourceSnapshot,
    1, 10, SnapshotAmmoRole.Source, sourceRules);
SnapshotAmmoCandidate both = new SnapshotAmmoCandidate("both", targetGroup,
    new ItemSnapshot(700, 3, 61, new byte[] { 8, 9 }), 1, 10,
    SnapshotAmmoRole.Source | SnapshotAmmoRole.Target,
    new AmmoRefillAssetRules(targetGuid.ToString("D"), true, true,
        new[] { sourceGuid.ToString("D") }));
Assert(targetOnly.IsTarget && !targetOnly.IsSource && sourceOnly.IsSource && !sourceOnly.IsTarget &&
       both.IsSource && both.IsTarget,
    "Source-only, target-only, and both roles must remain independent through the adapter contract.");
Assert(SnapshotAmmoRefillPlanner.TryPlan(new[] { sourceOnly },
        out SnapshotAmmoRefillPlan sourceOnlyPlan, out _) && !sourceOnlyPlan.Changed &&
       sourceOnlyPlan.ContainerCount == 0 && sourceOnlyPlan.TotalRounds == 10,
    "A source-only lot must never become a fill target when no target role is present.");

Guid unrelatedGuid = Guid.Parse("70000000-0000-0000-0000-000000000099");
AmmoRefillAssetRules incompatibleTargetRules = new AmmoRefillAssetRules(targetGuid.ToString("D"),
    isMagazine: true, isDirectSource: false, new[] { unrelatedGuid.ToString("D") });
SnapshotAmmoCandidate incompatibleTarget = new SnapshotAmmoCandidate("incompatible-target", targetGroup,
    targetSnapshot, 2, 10, SnapshotAmmoRole.Target, incompatibleTargetRules);
SnapshotAmmoCandidate incompatibleSource = new SnapshotAmmoCandidate("incompatible-source", sourceGroup,
    sourceSnapshot, 1, 10, SnapshotAmmoRole.Source, sourceRules);
SnapshotKernel incompatibleKernel = new SnapshotKernel();
Assert(incompatibleKernel.Store(targetGroup, targetSnapshot, 2).Success &&
       incompatibleKernel.Store(sourceGroup, sourceSnapshot, 1).Success,
    "An incompatible fixture should store both exact groups.");
Assert(SnapshotAmmoRefillPlanner.TryPlan(new[] { incompatibleTarget, incompatibleSource },
        out SnapshotAmmoRefillPlan incompatiblePlan, out _),
    "An incompatible fixture may still consolidate targets inside their own exact identity.");
SnapshotKernelTransaction incompatibleTransaction = new SnapshotKernelTransaction(incompatibleKernel,
    new SnapshotStorageIdentity("world", "player"));
Assert(incompatiblePlan.Apply(incompatibleTransaction).Success &&
       incompatibleTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success &&
       incompatibleKernel.GetGroups().Single(group => group.AssetId == 701).Buckets.Single().Count == 1 &&
       incompatibleKernel.GetGroups().Single(group => group.AssetId == 701).Buckets.Single().Snapshot.Amount == 10,
    "An equal-sized but incompatible ammo source must remain completely unconsumed.");
SnapshotAmmoCandidate overCapacity = new SnapshotAmmoCandidate("over-capacity", targetGroup,
    new ItemSnapshot(700, 11, 61, new byte[] { 8, 9 }), 1, 10,
    SnapshotAmmoRole.Target, targetRules);
Assert(!SnapshotAmmoRefillPlanner.TryPlan(new[] { overCapacity }, out _, out string overCapacityError) &&
       overCapacityError == "invalid-ammo-candidate",
    "An ammo snapshot amount above current asset capacity must fail closed.");

foreach ((string Name, SnapshotAmmoCandidate Candidate) invalidRole in new[]
{
    ("source-only", new SnapshotAmmoCandidate("over-capacity-source", sourceGroup,
        new ItemSnapshot(701, 11, 44, Array.Empty<byte>()), 1, 10,
        SnapshotAmmoRole.Source, sourceRules)),
    ("target-only", new SnapshotAmmoCandidate("over-capacity-target", targetGroup,
        new ItemSnapshot(700, 11, 61, new byte[] { 8, 9 }), 1, 10,
        SnapshotAmmoRole.Target, targetRules)),
    ("source-and-target", new SnapshotAmmoCandidate("over-capacity-both", targetGroup,
        new ItemSnapshot(700, 11, 61, new byte[] { 8, 9 }), 1, 10,
        SnapshotAmmoRole.Source | SnapshotAmmoRole.Target,
        new AmmoRefillAssetRules(targetGuid.ToString("D"), true, true,
            new[] { sourceGuid.ToString("D") })))
})
{
    SnapshotKernel invalidCapacityKernel = new SnapshotKernel();
    Assert(invalidCapacityKernel.Store(invalidRole.Candidate.Identity,
            invalidRole.Candidate.Snapshot, invalidRole.Candidate.Count).Success,
        "Invalid-capacity fixture must remain representable as an opaque exact Core snapshot.");
    string beforeInvalidCapacity = KernelFingerprint(invalidCapacityKernel);
    int invalidCapacityPersistCalls = 0;
    bool invalidCapacityPlanned = SnapshotAmmoRefillPlanner.TryPlan(
        new[] { invalidRole.Candidate }, out SnapshotAmmoRefillPlan invalidCapacityPlan,
        out string invalidCapacityError);
    if (invalidCapacityPlanned && invalidCapacityPlan.Changed)
    {
        SnapshotKernelTransaction invalidCapacityTransaction = new SnapshotKernelTransaction(
            invalidCapacityKernel, new SnapshotStorageIdentity("world", "player"));
        if (invalidCapacityPlan.Apply(invalidCapacityTransaction).Success)
            invalidCapacityTransaction.Commit(_ =>
            {
                invalidCapacityPersistCalls++;
                return SnapshotSaveResult.Succeeded();
            });
    }
    Assert(!invalidCapacityPlanned && invalidCapacityError == "invalid-ammo-candidate" &&
           KernelFingerprint(invalidCapacityKernel) == beforeInvalidCapacity &&
           invalidCapacityPersistCalls == 0,
        "Every ammo role must reject amount above capacity before storage mutation or persistence: " +
        invalidRole.Name);
}

SnapshotKernel rollbackKernel = new SnapshotKernel();
Assert(rollbackKernel.Store(targetGroup, targetSnapshot, 2).Success &&
       rollbackKernel.Store(sourceGroup, sourceSnapshot, 1).Success,
    "Ammo rollback fixture should store the original exact buckets.");
Assert(SnapshotAmmoRefillPlanner.TryPlan(new[] { target, source },
        out SnapshotAmmoRefillPlan rollbackPlan, out _),
    "Ammo rollback fixture should plan from the original exact buckets.");
SnapshotKernelTransaction rollbackTransaction = new SnapshotKernelTransaction(rollbackKernel,
    new SnapshotStorageIdentity("world", "player"));
Assert(rollbackPlan.Apply(rollbackTransaction).Success, "Ammo rollback plan should stage.");
SnapshotTransactionCommitResult rollbackCommit = rollbackTransaction.Commit(_ =>
    SnapshotSaveResult.Failed("injected-save-failure"));
Assert(!rollbackCommit.Success && rollbackCommit.RollbackSucceeded &&
       rollbackKernel.GetGroups().Single(group => group.AssetId == 700).Buckets.Single().Snapshot.Amount == 5 &&
       rollbackKernel.GetGroups().Single(group => group.AssetId == 701).Buckets.Single().Snapshot.Amount == 10,
    "Ammo persistence failure must restore both target and source snapshots.");

Console.WriteLine("Snapshot ammo refill adapter smoke checks passed.");
