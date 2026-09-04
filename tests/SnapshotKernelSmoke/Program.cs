using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using Newtonsoft.Json.Linq;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static ItemSnapshot Snapshot(ushort id, byte amount, byte quality, params byte[] state) =>
    new ItemSnapshot(id, amount, quality, state);

static string KernelFingerprint(SnapshotKernel value) => string.Join(";", value.GetGroups()
    .OrderBy(groupValue => groupValue.AssetId)
    .SelectMany(groupValue => groupValue.Buckets.Select(bucket => string.Join("|",
        groupValue.AssetId, groupValue.AssetGuid.ToString("D"), bucket.Snapshot.Amount,
        bucket.Snapshot.Quality, Convert.ToBase64String(bucket.Snapshot.State), bucket.Count))));

static SnapshotDocument DocumentWithExact(SnapshotStorageIdentity identity,
    ItemGroupIdentity groupIdentity, ItemSnapshot snapshot, long count)
{
    SnapshotDocument document = SnapshotDocument.Empty(identity);
    if (count <= 0) return document;
    document.Groups.Add(new SnapshotDocumentGroup
    {
        AssetId = groupIdentity.AssetId,
        AssetGuid = groupIdentity.AssetGuid.ToString("D"),
        Buckets = new List<SnapshotDocumentBucket>
        {
            new SnapshotDocumentBucket
            {
                Amount = snapshot.Amount,
                Quality = snapshot.Quality,
                State = snapshot.State,
                Count = count
            }
        }
    });
    return document;
}

static SnapshotDocument ReadSnapshotDocument(string path)
{
    string json = File.ReadAllText(path);
    Assert(SnapshotDocumentValidator.TryParse(json, out SnapshotDocument document, out string error),
        "Recovery evidence must remain valid Snapshot JSON. path=" + path + " error=" + error);
    return document;
}

static long CountExact(SnapshotDocument document, ItemGroupIdentity identity, ItemSnapshot snapshot) =>
    document.Groups
        .Where(groupValue => groupValue.AssetId == identity.AssetId &&
            string.Equals(groupValue.AssetGuid, identity.AssetGuid.ToString("D"), StringComparison.Ordinal))
        .SelectMany(groupValue => groupValue.Buckets)
        .Where(bucket => bucket.Amount == snapshot.Amount && bucket.Quality == snapshot.Quality &&
            (bucket.State ?? Array.Empty<byte>()).SequenceEqual(snapshot.State))
        .Sum(bucket => bucket.Count);

static void AssertBackgroundRoute(SnapshotBackgroundStoreCompletionResult completion,
    string expected)
{
    int detached = 0;
    int committed = 0;
    int safeFailure = 0;
    int uncertain = 0;
    string actual = SnapshotBackgroundStoreCompletionRouter.Route(completion,
        onDetached: () => { detached++; return "detached"; },
        onCommittedCurrentSession: () => { committed++; return "committed"; },
        onSafeFailureCurrentSession: () => { safeFailure++; return "safe"; },
        onUncertainCurrentSession: () => { uncertain++; return "uncertain"; });
    Assert(actual == expected && detached + committed + safeFailure + uncertain == 1 &&
           detached == (expected == "detached" ? 1 : 0) &&
           committed == (expected == "committed" ? 1 : 0) &&
           safeFailure == (expected == "safe" ? 1 : 0) &&
           uncertain == (expected == "uncertain" ? 1 : 0),
        "Background completion routed to an unexpected or duplicate side-effect branch. expected=" +
        expected + " actual=" + actual);
}

static string FileSha256(string path) => Convert.ToHexString(
    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

static string FindRepositoryRoot()
{
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UnturnedSingleplayerCheatMenu.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("Could not locate repository root from stable solution marker.");
}

ItemGroupIdentity group = new ItemGroupIdentity(100, Guid.Parse("11111111-1111-1111-1111-111111111111"));
ItemSnapshot baseline = Snapshot(100, 10, 80, 1, 2, 3);
SnapshotKernel kernel = new SnapshotKernel();

Assert(kernel.Store(group, baseline, 2).Success && kernel.Store(group, baseline, 3).Success,
    "Identical snapshots should store successfully.");
ItemGroupView mergedGroup = kernel.GetGroups().Single();
Assert(mergedGroup.TotalCount == 5 && mergedGroup.Buckets.Count == 1 &&
       mergedGroup.Buckets[0].Count == 5,
    "Identical native snapshots must merge by instance count rather than by amount units.");
Assert(kernel.TryGetGroup(group, out ItemGroupView pointLookupGroup) &&
       pointLookupGroup.TotalCount == 5 && pointLookupGroup.Buckets.Count == 1 &&
       !kernel.TryGetGroup(new ItemGroupIdentity(group.AssetId,
           Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")), out _),
    "Point lookup must return one exact ID/GUID group without scanning or exporting the complete warehouse.");
byte[] pointLookupState = pointLookupGroup.Buckets[0].Snapshot.State;
pointLookupState[0] ^= 0xff;
Assert(kernel.TryGetGroup(group, out ItemGroupView freshPointLookupGroup) &&
       freshPointLookupGroup.Buckets[0].Snapshot.State.SequenceEqual(new byte[] { 1, 2, 3 }),
    "Point lookup must keep the same deep-copy boundary as the full presentation snapshot.");

Assert(kernel.Store(group, Snapshot(100, 9, 80, 1, 2, 3), 1).Success &&
       kernel.Store(group, Snapshot(100, 10, 79, 1, 2, 3), 1).Success &&
       kernel.Store(group, Snapshot(100, 10, 80, 1, 2, 4), 1).Success,
    "Distinct snapshot fixtures should store successfully.");
ItemGroupView splitGroup = kernel.GetGroups().Single();
Assert(splitGroup.Buckets.Count == 4 && splitGroup.TotalCount == 8,
    "Changing amount, quality, or one state byte must produce a distinct bucket.");

ItemGroupIdentity destructionGroup = new ItemGroupIdentity(101,
    Guid.Parse("12121212-1212-1212-1212-121212121212"));
ItemSnapshot destructionA = Snapshot(101, 10, 80, 1, 2, 3);
ItemSnapshot destructionB = Snapshot(101, 9, 79, 4, 5, 6);
SnapshotKernel destructionKernel = new SnapshotKernel();
Assert(destructionKernel.Store(destructionGroup, destructionA, 3).Success &&
       destructionKernel.Store(destructionGroup, destructionB, 2).Success,
    "Destruction fixtures should store two exact buckets under one item ID.");
Assert(SnapshotDestructionPlanner.TryPlan(destructionKernel.GetGroups(), destructionGroup,
        destructionA, SnapshotDestructionScope.AssetId, out SnapshotDestructionPlan destructionPlan,
        out string destructionPlanError) && string.IsNullOrEmpty(destructionPlanError) &&
       destructionPlan.DestroyedCount == 5 && destructionPlan.RemovalCount == 2,
    "ID destruction must include every exact snapshot bucket for the selected item ID.");
SnapshotKernelTransaction destructionTransaction = new SnapshotKernelTransaction(destructionKernel,
    new SnapshotStorageIdentity("destruction-world", "destruction-player"));
Assert(destructionPlan.Apply(destructionTransaction).Success &&
       destructionKernel.GetGroups().Count == 0,
    "Applying an ID destruction plan must remove every selected bucket without creating native output.");

SnapshotKernel bucketDestructionKernel = new SnapshotKernel();
Assert(bucketDestructionKernel.Store(destructionGroup, destructionA, 3).Success &&
       bucketDestructionKernel.Store(destructionGroup, destructionB, 2).Success &&
       SnapshotDestructionPlanner.TryPlan(bucketDestructionKernel.GetGroups(), destructionGroup,
           destructionA, SnapshotDestructionScope.Bucket, out SnapshotDestructionPlan bucketPlan, out _) &&
       bucketPlan.DestroyedCount == 3 && bucketPlan.RemovalCount == 1,
    "Bucket destruction must remain limited to the selected exact snapshot bucket.");

SnapshotKernel singleDestructionKernel = new SnapshotKernel();
Assert(singleDestructionKernel.Store(destructionGroup, destructionA, 3).Success &&
       SnapshotDestructionPlanner.TryPlan(singleDestructionKernel.GetGroups(), destructionGroup,
           destructionA, SnapshotDestructionScope.Single, out SnapshotDestructionPlan singlePlan, out _) &&
       singlePlan.DestroyedCount == 1 && singlePlan.RemovalCount == 1,
    "Single destruction must remove exactly one snapshot instance.");

byte[] expectedEncoding =
[
    1, 0,
    1, 0, 0, 0, 10,
    1, 0, 0, 0, 80,
    3, 0, 0, 0, 1, 2, 3
];
Assert(SnapshotKey.EncodeCanonical(baseline).SequenceEqual(expectedEncoding),
    "SnapshotKey v1 must use positional little-endian length prefixes for amount, quality, and state.");

SnapshotKernel collisionKernel = new SnapshotKernel(new ConstantSnapshotHashProvider("same-hash"));
Assert(collisionKernel.Store(group, Snapshot(100, 10, 80, 7), 1).Success &&
       collisionKernel.Store(group, Snapshot(100, 10, 80, 8), 1).Success &&
       collisionKernel.GetGroups().Single().Buckets.Count == 2,
    "Hash equality is only an index hint; full fields must be compared before merging.");

byte[] callerState = { 11, 22, 33 };
ItemSnapshot callerSnapshot = new ItemSnapshot(100, 4, 55, callerState);
SnapshotKernel copyKernel = new SnapshotKernel();
Assert(copyKernel.Store(group, callerSnapshot, 1).Success, "Deep-copy fixture should store.");
callerState[0] = 99;
byte[] exposedState = copyKernel.GetGroups().Single().Buckets.Single().Snapshot.State;
exposedState[1] = 99;
byte[] freshState = copyKernel.GetGroups().Single().Buckets.Single().Snapshot.State;
Assert(freshState.SequenceEqual(new byte[] { 11, 22, 33 }),
    "Input and returned state arrays must be deep copies.");

SnapshotKernel overflowKernel = new SnapshotKernel();
Assert(overflowKernel.Store(group, baseline, long.MaxValue).Success,
    "The maximum representable instance count should store.");
SnapshotMutationResult overflow = overflowKernel.Store(group, baseline, 1);
Assert(!overflow.Success && overflow.ErrorCode == "count-overflow" &&
       overflowKernel.GetGroups().Single().Buckets.Single().Count == long.MaxValue,
    "Count overflow must fail without mutating the existing bucket.");
SnapshotMutationResult groupTotalOverflow = overflowKernel.Store(group,
    Snapshot(100, 9, 80, 7), 1);
Assert(!groupTotalOverflow.Success && groupTotalOverflow.ErrorCode == "count-overflow" &&
       overflowKernel.GetGroups().Single().Buckets.Count == 1 &&
       overflowKernel.GetGroups().Single().TotalCount == long.MaxValue,
    "ItemGroup total-count overflow across distinct buckets must fail before adding the new bucket.");

SnapshotKernel templateKernel = new SnapshotKernel();
ItemSnapshot template = Snapshot(100, 30, 100, 0, 0, 0, 0);
Assert(templateKernel.RegisterDefaultTemplate(group, template).Success,
    "Default template registration should succeed.");
ItemGroupView templateOnly = templateKernel.GetGroups().Single();
Assert(templateOnly.TotalCount == 0 && templateOnly.Buckets.Count == 0 &&
       templateOnly.DefaultTemplate != null,
    "DefaultTemplate is metadata and must not create a stock bucket.");
SnapshotWithdrawalResult templateWithdrawal = templateKernel.Withdraw(group, template, 1);
Assert(!templateWithdrawal.Success && templateWithdrawal.ErrorCode == "bucket-not-found" &&
       templateKernel.GetGroups().Single().TotalCount == 0,
    "Count zero must never withdraw an instance from DefaultTemplate.");

SnapshotKernel identityKernel = new SnapshotKernel();
Assert(identityKernel.Store(group, baseline, 1).Success,
    "Identity-guard fixture should store its first group.");
ItemGroupIdentity conflictingGuid = new ItemGroupIdentity(100,
    Guid.Parse("22222222-2222-2222-2222-222222222222"));
SnapshotMutationResult identityConflict = identityKernel.Store(conflictingGuid, baseline, 1);
Assert(!identityConflict.Success && identityConflict.ErrorCode == "asset-guid-conflict" &&
       identityKernel.GetGroups().Single().TotalCount == 1,
    "Asset ID is the group identity while GUID mismatch must reject cross-group merging.");

SnapshotKernel priorityKernel = new SnapshotKernel();
ItemSnapshot nonDefault = Snapshot(100, 1, 10, 9);
ItemSnapshot defaultBucket = Snapshot(100, 30, 100, 0, 0, 0, 0);
Assert(priorityKernel.RegisterDefaultTemplate(group, defaultBucket).Success &&
       priorityKernel.Store(group, nonDefault, 1).Success &&
       priorityKernel.Store(group, defaultBucket, 1).Success &&
       priorityKernel.GetGroups().Single().Buckets[0].IsDefaultTemplate,
    "A real bucket equal to DefaultTemplate must sort first without changing its count.");

ItemSnapshot farmRaw = Snapshot(330, 1, 47, 1, 2, 3, 4);
SnapshotCanonicalizationFacts provenFarm = new SnapshotCanonicalizationFacts(
    isFarmBuild: true, assetMaxAmount: 1);
ItemSnapshot farmCanonical = SnapshotCanonicalizer.Canonicalize(farmRaw, provenFarm);
Assert(farmCanonical.Amount == 1 && farmCanonical.Quality == 47 &&
       farmCanonical.State.SequenceEqual(new byte[4]),
    "Only the proven Farm timestamp bytes may canonicalize; hidden quality remains exact.");
ItemSnapshot[] exactFallbacks =
[
    SnapshotCanonicalizer.Canonicalize(Snapshot(330, 1, 47, 1, 2, 3), provenFarm),
    SnapshotCanonicalizer.Canonicalize(Snapshot(330, 1, 47, 1, 2, 3, 4, 5), provenFarm),
    SnapshotCanonicalizer.Canonicalize(farmRaw,
        new SnapshotCanonicalizationFacts(isFarmBuild: true, assetMaxAmount: 2)),
    SnapshotCanonicalizer.Canonicalize(Snapshot(330, 2, 47, 1, 2, 3, 4), provenFarm),
    SnapshotCanonicalizer.Canonicalize(farmRaw,
        new SnapshotCanonicalizationFacts(isFarmBuild: false, assetMaxAmount: 1))
];
Assert(exactFallbacks[0].State.SequenceEqual(new byte[] { 1, 2, 3 }) &&
       exactFallbacks[1].State.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }) &&
       exactFallbacks[2].State.SequenceEqual(farmRaw.State) &&
       exactFallbacks[3].Amount == 2 && exactFallbacks[3].State.SequenceEqual(new byte[] { 1, 2, 3, 4 }) &&
       exactFallbacks[4].State.SequenceEqual(farmRaw.State),
    "Malformed, non-unit-capacity, non-unit-amount, unknown, and Workshop-shaped items must stay exact.");

Assert(SnapshotRuntimeIdentity.TryCreate("Singleplayer_0", "PEI", "76561198000000001",
        nativeCharacterSlot: 0, selectedCharacterSlot: 0, hasSync: false,
        out SnapshotStorageIdentity slotZero, out string slotZeroError),
    "A consistent native singleplayer identity should resolve. Error=" + slotZeroError);
Assert(SnapshotRuntimeIdentity.TryCreate("Singleplayer_1", "PEI", "76561198000000001",
        nativeCharacterSlot: 1, selectedCharacterSlot: 1, hasSync: false,
        out SnapshotStorageIdentity slotOne, out string slotOneError),
    "A second valid native character slot should resolve. Error=" + slotOneError);
Assert(slotZero.PlayerKey != slotOne.PlayerKey &&
       SnapshotStoragePath.Resolve(Path.GetTempPath(), slotZero) !=
       SnapshotStoragePath.Resolve(Path.GetTempPath(), slotOne),
    "The same account and map must never collide across native character slots.");
Assert(SnapshotRuntimeIdentity.TryCreate("Singleplayer_0", "PEI", "76561198000000001",
        0, 0, false, out SnapshotStorageIdentity slotZeroAgain, out _) &&
       slotZero.WorldKey == slotZeroAgain.WorldKey && slotZero.PlayerKey == slotZeroAgain.PlayerKey,
    "The same native identity must be stable across repeated resolution.");
Assert(!SnapshotRuntimeIdentity.TryCreate("DedicatedServer", "PEI", "76561198000000001",
           0, 0, false, out _, out _) &&
       !SnapshotRuntimeIdentity.TryCreate("PublicServer", "PEI", "76561198000000001",
           0, 0, false, out _, out _) &&
       !SnapshotRuntimeIdentity.TryCreate("Singleplayer_0", "PEI", "76561198000000001",
           0, 1, false, out _, out _) &&
       !SnapshotRuntimeIdentity.TryCreate("Singleplayer_5", "PEI", "76561198000000001",
           5, 5, false, out _, out _),
    "Dedicated, multiplayer, mismatched, and out-of-range character identities must fail closed.");

for (byte slot = 0; slot < 5; slot++)
{
    Assert(SnapshotRuntimeIdentity.TryCreate("Singleplayer_" + slot, "PEI", "76561198000000001",
               slot, slot, false, out SnapshotStorageIdentity stableIdentity, out string stableError) &&
           stableIdentity.WorldKey.Length > 0 && stableIdentity.PlayerKey.Length > 0,
        "Existing Singleplayer_0 through Singleplayer_4 identities must remain valid. Slot=" + slot + "; error=" + stableError);
}

SnapshotActivationAttemptPolicy activationPolicy = new SnapshotActivationAttemptPolicy();
Assert(activationPolicy.ShouldAttempt(slotZero), "The first activation attempt should be allowed.");
activationPolicy.RecordFailure(slotZero);
Assert(Enumerable.Range(0, 16).All(_ => !activationPolicy.ShouldAttempt(slotZero)),
    "A failed identity must latch and suppress repeated frame-by-frame attempts.");
Assert(activationPolicy.ShouldAttempt(slotOne),
    "Changing the complete identity should clear the previous identity's failure latch.");
activationPolicy.RecordFailure(slotOne);
activationPolicy.Reset();
Assert(activationPolicy.ShouldAttempt(slotOne),
    "An explicit lifecycle reset should permit one later retry.");

SnapshotKernel transactionKernel = new SnapshotKernel();
Assert(transactionKernel.RegisterDefaultTemplate(group, template).Success &&
       transactionKernel.Store(group, baseline, 1).Success,
    "Transaction fixture should start with one exact snapshot and one runtime template.");
string beforeFailedStore = KernelFingerprint(transactionKernel);
SnapshotKernelTransaction failedStoreTransaction = new SnapshotKernelTransaction(transactionKernel, slotZero);
Assert(failedStoreTransaction.Store(group, baseline, 2).Success,
    "A staged store should mutate memory before its persistence commit.");
SnapshotTransactionCommitResult failedStoreCommit = failedStoreTransaction.Commit(_ =>
    SnapshotSaveResult.Failed("injected-save-failure"));
Assert(!failedStoreCommit.Success && failedStoreCommit.RollbackSucceeded &&
       KernelFingerprint(transactionKernel) == beforeFailedStore &&
       transactionKernel.GetGroups().Single().DefaultTemplate?.Equals(template) == true,
    "A failed store persistence commit must restore counts without discarding runtime template metadata.");

string beforeFailedWithdraw = KernelFingerprint(transactionKernel);
SnapshotKernelTransaction failedWithdrawTransaction = new SnapshotKernelTransaction(transactionKernel, slotZero);
Assert(failedWithdrawTransaction.Withdraw(group, baseline, 1).Success,
    "A staged withdrawal should remove one exact instance before commit.");
SnapshotTransactionCommitResult failedWithdrawCommit = failedWithdrawTransaction.Commit(_ =>
    SnapshotSaveResult.Failed("injected-save-failure"));
Assert(!failedWithdrawCommit.Success && failedWithdrawCommit.RollbackSucceeded &&
       KernelFingerprint(transactionKernel) == beforeFailedWithdraw,
    "A failed withdrawal persistence commit must put back the exact snapshot and count.");

string beforeThrownPersistence = KernelFingerprint(transactionKernel);
SnapshotKernelTransaction thrownPersistenceTransaction = new SnapshotKernelTransaction(transactionKernel, slotZero);
Assert(thrownPersistenceTransaction.Store(group, baseline, 1).Success,
    "Thrown-persistence fixture should stage one exact snapshot.");
SnapshotTransactionCommitResult thrownPersistenceCommit = thrownPersistenceTransaction.Commit(_ =>
    throw new IOException("persistence-outcome-unknown"));
Assert(!thrownPersistenceCommit.Success && !thrownPersistenceCommit.RollbackSucceeded &&
       KernelFingerprint(transactionKernel) == beforeThrownPersistence,
    "A throwing persistence delegate may have committed; memory may restore, but durable rollback must remain unproven.");

string beforeNullPersistence = KernelFingerprint(transactionKernel);
SnapshotKernelTransaction nullPersistenceTransaction = new SnapshotKernelTransaction(transactionKernel, slotZero);
Assert(nullPersistenceTransaction.Store(group, baseline, 1).Success,
    "Null-persistence fixture should stage one exact snapshot.");
SnapshotTransactionCommitResult nullPersistenceCommit = nullPersistenceTransaction.Commit(_ => null);
Assert(!nullPersistenceCommit.Success && !nullPersistenceCommit.RollbackSucceeded &&
       KernelFingerprint(transactionKernel) == beforeNullPersistence,
    "A null persistence result is outcome-unknown and must never authorize native compensation.");

SnapshotKernelTransaction successfulTransaction = new SnapshotKernelTransaction(transactionKernel, slotZero);
Assert(successfulTransaction.Store(group, baseline, 1).Success &&
       successfulTransaction.Commit(_ => SnapshotSaveResult.Succeeded()).Success &&
       transactionKernel.GetGroups().Single().TotalCount == 2,
    "A successful transaction must retain its staged mutation exactly once.");

SnapshotStorageSession failedActivationSession = new SnapshotStorageSession();
int failedLoadCalls = 0;
SnapshotSessionActivationResult firstFailedActivation = failedActivationSession.Activate(slotZero,
    () =>
    {
        failedLoadCalls++;
        return SnapshotLoadResult.Failed("injected-load-failure", slotZero);
    }, _ => SnapshotSaveResult.Succeeded());
Assert(!firstFailedActivation.Success && firstFailedActivation.Attempted && failedLoadCalls == 1 &&
       Enumerable.Range(0, 16).All(_ => !failedActivationSession.Activate(slotZero,
           () =>
           {
               failedLoadCalls++;
               return SnapshotLoadResult.Failed("must-not-repeat", slotZero);
           }, __ => SnapshotSaveResult.Succeeded()).Attempted) && failedLoadCalls == 1,
    "A load failure must latch the complete identity and suppress every later automatic frame attempt.");
failedActivationSession.Reset();
SnapshotSessionActivationResult recoveredActivation = failedActivationSession.Activate(slotZero,
    () => SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(slotZero), false),
    _ => SnapshotSaveResult.Succeeded());
Assert(recoveredActivation.Success && recoveredActivation.Attempted && failedActivationSession.IsOperational,
    "An explicit lifecycle reset should allow one clean activation retry.");

SnapshotKernelTransaction failedSessionMutation = failedActivationSession.BeginTransaction();
Assert(failedSessionMutation.Store(group, baseline, 1).Success,
    "An operational session should allow a staged exact store.");
SnapshotTransactionCommitResult failedSessionCommit = failedActivationSession.Commit(failedSessionMutation,
    _ => SnapshotSaveResult.Failed("injected-runtime-save-failure"));
Assert(!failedSessionCommit.Success && failedSessionCommit.RollbackSucceeded &&
       !failedActivationSession.IsOperational && failedActivationSession.Kernel.GetGroups().Count == 0 &&
       !failedActivationSession.Activate(slotZero,
           () => SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(slotZero), false),
           _ => SnapshotSaveResult.Succeeded()).Attempted,
    "A persistence failure must roll back memory, close mutations, and latch the active identity.");

SnapshotStorageSession handoffSession = new SnapshotStorageSession();
SnapshotDocument slotZeroDocument = SnapshotDocument.Empty(slotZero);
slotZeroDocument.Groups.Add(new SnapshotDocumentGroup
{
    AssetId = group.AssetId,
    AssetGuid = group.AssetGuid.ToString("D"),
    Buckets = new List<SnapshotDocumentBucket>
    {
        new SnapshotDocumentBucket
        {
            Amount = baseline.Amount,
            Quality = baseline.Quality,
            State = baseline.State,
            Count = 1
        }
    }
});
Assert(handoffSession.Activate(slotZero, () => SnapshotLoadResult.Succeeded(slotZeroDocument, false),
           _ => SnapshotSaveResult.Succeeded()).Success && handoffSession.Kernel.GetGroups().Count == 1,
    "The first identity should activate its own exact snapshot document.");
Assert(handoffSession.Activate(slotOne, () => SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(slotOne), false),
           _ => SnapshotSaveResult.Succeeded()).Success && handoffSession.Kernel.GetGroups().Count == 0,
    "Switching identity must replace the active kernel rather than expose the previous character's groups.");

SnapshotStorageSession craftingRollbackSession = new SnapshotStorageSession();
Assert(craftingRollbackSession.Activate(slotZero,
        () => SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(slotZero), false),
        _ => SnapshotSaveResult.Succeeded()).Success,
    "Crafting rollback fixture should activate an empty session.");
SnapshotKernelCheckpoint beforeCrafting = craftingRollbackSession.CaptureCheckpoint();
SnapshotKernelTransaction consumedCrafting = craftingRollbackSession.BeginTransaction();
Assert(consumedCrafting.Store(group, baseline, 1).Success &&
       craftingRollbackSession.Commit(consumedCrafting).Success,
    "Crafting rollback fixture should first persist one mutation.");
SnapshotTransactionCommitResult rollbackPersistenceFailure =
    craftingRollbackSession.RestoreCheckpointAndPersist(beforeCrafting,
        _ => SnapshotSaveResult.Failed("injected-rollback-save-failure",
            mayHaveCommitted: true,
            recoveryBackupPath: "crafting-pre-replace-A.json",
            candidateRecoveryPath: "crafting-candidate-B.json"));
Assert(!rollbackPersistenceFailure.Success && !rollbackPersistenceFailure.RollbackSucceeded &&
       rollbackPersistenceFailure.RecoveryBackupPath == "crafting-pre-replace-A.json" &&
       rollbackPersistenceFailure.CandidateRecoveryPath == "crafting-candidate-B.json" &&
       !craftingRollbackSession.IsOperational &&
       craftingRollbackSession.Kernel.GetGroups().Single().TotalCount == 1 &&
       !craftingRollbackSession.Activate(slotZero,
           () => SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(slotZero), false),
           _ => SnapshotSaveResult.Succeeded()).Attempted,
    "Rollback persistence failure must restore the last persisted in-memory state and latch mutations closed.");
string craftingCheckpointReason = string.Empty;
SnapshotStorageRollbackReconciliationResult craftingCheckpointReconciliation =
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        rollbackPersistenceFailure, "checkpoint-rollback", reason =>
        {
            craftingCheckpointReason = reason;
            return true;
        });
Assert(craftingCheckpointReconciliation.MarkerPersisted &&
       craftingCheckpointReason.Contains("recovery=crafting-pre-replace-A.json", StringComparison.Ordinal) &&
       craftingCheckpointReason.Contains("candidate=crafting-candidate-B.json", StringComparison.Ordinal),
    "Crafting checkpoint reconciliation diagnostics must identify both pre-replace A and candidate B.");

SnapshotAssetResolution matchingAsset = SnapshotAssetGuard.Evaluate(group, assetExists: true,
    runtimeAssetId: 100, runtimeAssetGuid: group.AssetGuid);
SnapshotAssetResolution missingAsset = SnapshotAssetGuard.Evaluate(group, assetExists: false,
    runtimeAssetId: 0, runtimeAssetGuid: Guid.Empty);
SnapshotAssetResolution conflictingAsset = SnapshotAssetGuard.Evaluate(group, assetExists: true,
    runtimeAssetId: 100, runtimeAssetGuid: Guid.Parse("33333333-3333-3333-3333-333333333333"));
Assert(matchingAsset.CanWithdraw && !missingAsset.CanWithdraw && missingAsset.ErrorCode == "asset-missing" &&
       !conflictingAsset.CanWithdraw && conflictingAsset.ErrorCode == "asset-guid-conflict" &&
       identityKernel.GetGroups().Single().TotalCount == 1,
    "Missing and ID-to-GUID-conflicting assets must preserve the stored group while disabling withdrawal.");

SnapshotBucketMetadata amountMetadata = SnapshotBucketMetadata.Build(Snapshot(100, 10, 80, 1, 2, 3),
    count: 3, showQuality: true, SnapshotBucketDisplayFacts.NativeAmount);
SnapshotBucketMetadata hiddenQualityMetadata = SnapshotBucketMetadata.Build(Snapshot(100, 10, 33, 1, 2, 3),
    count: 3, showQuality: false);
SnapshotBucketMetadata emptyMagazineMetadata = SnapshotBucketMetadata.Build(Snapshot(100, 0, 80),
    count: 1, showQuality: true, SnapshotBucketDisplayFacts.AmmoContainer(capacity: 40));
Assert(amountMetadata.Text.Contains("原生数量 10", StringComparison.Ordinal) &&
	   amountMetadata.Text.Contains("实例 ×3", StringComparison.Ordinal) &&
	   !amountMetadata.Text.Contains("原生总量", StringComparison.Ordinal) &&
	   !amountMetadata.Text.Contains("弹药", StringComparison.Ordinal) &&
	   amountMetadata.Text.Contains("品质 80", StringComparison.Ordinal) &&
	   !hiddenQualityMetadata.Text.Contains("品质", StringComparison.Ordinal) &&
	   emptyMagazineMetadata.Text.Contains("弹药 0/40", StringComparison.Ordinal) &&
	   emptyMagazineMetadata.Text.Contains("实例 ×1", StringComparison.Ordinal) &&
	   !emptyMagazineMetadata.Text.Contains("原生数量", StringComparison.Ordinal) &&
	   !emptyMagazineMetadata.Text.Contains("原生总量", StringComparison.Ordinal),
    "Presentation must reserve ammo labels for proven ammo containers, keep other items neutral, and omit redundant amount-times-count totals.");

ItemGroupIdentity hiddenQualityGroup = new ItemGroupIdentity(1113,
    Guid.Parse("45454545-4545-4545-4545-454545454545"));
SnapshotKernel hiddenQualityKernel = new SnapshotKernel();
foreach (byte quality in new byte[] { 100, 17, 40, 73 })
    Assert(hiddenQualityKernel.Store(hiddenQualityGroup,
        Snapshot(1113, 1, quality), 1).Success,
        "Hidden-quality display aggregation fixtures should store exactly.");
ItemGroupView hiddenQualityView = hiddenQualityKernel.GetGroups().Single();
IReadOnlyList<SnapshotBucketDisplayGroup> mergedDisplayGroups =
    SnapshotBucketDisplayAggregator.Build(hiddenQualityView, mergeHiddenQuality: true);
IReadOnlyList<SnapshotBucketDisplayGroup> exactDisplayGroups =
    SnapshotBucketDisplayAggregator.Build(hiddenQualityView, mergeHiddenQuality: false);
Assert(mergedDisplayGroups.Count == 1 && mergedDisplayGroups[0].TotalCount == 4 &&
       mergedDisplayGroups[0].Buckets.Count == 4 && exactDisplayGroups.Count == 4,
    "Hidden-quality buckets with the same amount/state must merge only in the display projection, never in exact storage.");
Assert(hiddenQualityKernel.GetGroups().Single().Buckets.Count == 4,
    "Display aggregation must not mutate or merge the underlying exact Snapshot buckets.");

SnapshotKernel visibleStateKernel = new SnapshotKernel();
Assert(visibleStateKernel.Store(hiddenQualityGroup, Snapshot(1113, 1, 100), 1).Success &&
       visibleStateKernel.Store(hiddenQualityGroup, Snapshot(1113, 1, 17, 1), 1).Success &&
       SnapshotBucketDisplayAggregator.Build(visibleStateKernel.GetGroups().Single(),
           mergeHiddenQuality: true).Count == 2,
    "Different state bytes must remain separate display cards even when quality is hidden.");

List<string> nativeRollbackOrder = new List<string>();
SnapshotNativeRollbackResult nativeRollback = SnapshotNativeRollbackCoordinator.Execute(
    () => { nativeRollbackOrder.Add("dequip"); return true; },
    () => { nativeRollbackOrder.Add("clothing"); return true; },
    Enumerable.Range(0, 7).Select(value => (byte)value).ToArray(),
    page => { nativeRollbackOrder.Add("page:" + page); return true; },
    () => { nativeRollbackOrder.Add("items"); return true; },
    () => { nativeRollbackOrder.Add("selection"); return true; });
Assert(nativeRollback.Success && nativeRollbackOrder.SequenceEqual(new[]
    {
        "dequip", "page:0", "page:1", "page:2", "page:3", "page:4", "page:5", "page:6",
        "clothing", "page:0", "page:1", "page:2", "page:3", "page:4", "page:5", "page:6",
        "items", "selection"
    }),
    "Native rollback must clear current items before clothing resize, clear auto-returned clothing again, then restore exact items and selection.");
int simulatedPageItems = 1;
bool simulatedWorldDrop = false;
SnapshotNativeRollbackResult resizeSafeRollback = SnapshotNativeRollbackCoordinator.Execute(
    () => true,
    () =>
    {
        if (simulatedPageItems > 0) simulatedWorldDrop = true;
        simulatedPageItems++;
        return true;
    },
    new byte[] { 2 },
    _ => { simulatedPageItems = 0; return true; },
    () => simulatedPageItems == 0,
    () => true);
Assert(resizeSafeRollback.Success && !simulatedWorldDrop,
    "Clothing rollback must not resize occupied pages or leave the replaced worn item in inventory/world state.");
int bestEffortCalls = 0;
SnapshotNativeRollbackResult partialNativeRollback = SnapshotNativeRollbackCoordinator.Execute(
    () => { bestEffortCalls++; return true; },
    () => { bestEffortCalls++; return false; },
    new byte[] { 0, 1 },
    _ => { bestEffortCalls++; return true; },
    () => { bestEffortCalls++; return true; },
    () => { bestEffortCalls++; return true; });
Assert(!partialNativeRollback.Success && bestEffortCalls == 8,
    "Native rollback must attempt all recovery stages while reporting any failed stage to the mutation latch.");

List<string> nativeRecoveryOrder = new List<string>();
SnapshotNativeRecoveryResult storeAllNativeRecovery = SnapshotNativeRecoveryCoordinator.Execute(
    () => { nativeRecoveryOrder.Add("restore-native"); return false; },
    () => { nativeRecoveryOrder.Add("persist-reconciliation"); return true; });
Assert(!storeAllNativeRecovery.Success && storeAllNativeRecovery.NativeRestoreAttempted &&
       storeAllNativeRecovery.ReconciliationAttempted && storeAllNativeRecovery.MarkerPersisted &&
       nativeRecoveryOrder.SequenceEqual(new[] { "restore-native", "persist-reconciliation" }),
    "A false StoreAll native restore must persistently latch reconciliation before any later mutation can run.");
nativeRecoveryOrder.Clear();
SnapshotNativeRecoveryResult withdrawNativeRecovery = SnapshotNativeRecoveryCoordinator.Execute(
    () => { nativeRecoveryOrder.Add("restore-native"); throw new InvalidOperationException("native-restore"); },
    () => { nativeRecoveryOrder.Add("persist-reconciliation"); return false; });
Assert(!withdrawNativeRecovery.Success && withdrawNativeRecovery.NativeRestoreThrew &&
       withdrawNativeRecovery.ReconciliationAttempted && !withdrawNativeRecovery.MarkerPersisted &&
       withdrawNativeRecovery.ErrorCode == "native-restore-threw:marker-persistence-failed" &&
       nativeRecoveryOrder.SequenceEqual(new[] { "restore-native", "persist-reconciliation" }),
    "A thrown Withdraw native restore plus marker-write failure must remain fail-closed and expose both failure stages.");

List<string> latchOrder = new List<string>();
SnapshotReconciliationLatchResult markerWriteFailure = SnapshotReconciliationLatchCoordinator.Execute(
    () => latchOrder.Add("close-mutations"),
    () => { latchOrder.Add("persist-marker"); return false; });
Assert(!markerWriteFailure.Success && markerWriteFailure.MutationsClosed &&
       markerWriteFailure.MarkerPersistenceAttempted && !markerWriteFailure.MarkerPersisted &&
       markerWriteFailure.ErrorCode == "marker-persistence-failed" &&
       latchOrder.SequenceEqual(new[] { "close-mutations", "persist-marker" }),
    "Marker creation failure must remain closed, observable, and ordered after the in-memory mutation gate closes.");
latchOrder.Clear();
SnapshotReconciliationLatchResult markerWriteThrow = SnapshotReconciliationLatchCoordinator.Execute(
    () => latchOrder.Add("close-mutations"),
    () => { latchOrder.Add("persist-marker"); throw new IOException("marker-write"); });
Assert(!markerWriteThrow.Success && markerWriteThrow.MutationsClosed &&
       markerWriteThrow.MarkerPersistenceThrew && !markerWriteThrow.MarkerPersisted &&
       latchOrder.SequenceEqual(new[] { "close-mutations", "persist-marker" }),
    "A thrown marker write must not undo the already-closed mutation gate.");

SnapshotReconciliationPathLatch processPathLatch = new SnapshotReconciliationPathLatch();
processPathLatch.Latch("snapshot-path-A");
int reactivationCalls = 0;
Assert(processPathLatch.IsBlocked("snapshot-path-A", persistentMarkerExists: false) &&
       !processPathLatch.IsBlocked("snapshot-path-B", persistentMarkerExists: false),
    "An in-memory reconciliation latch must be scoped to the exact Snapshot path.");
if (!processPathLatch.IsBlocked("snapshot-path-A", persistentMarkerExists: false)) reactivationCalls++;
Assert(reactivationCalls == 0,
    "A marker-write failure must still block path A after an identity round-trip or session reset.");
Assert(processPathLatch.IsBlocked("snapshot-path-C", persistentMarkerExists: true) &&
       processPathLatch.IsBlocked("snapshot-path-C", persistentMarkerExists: false),
    "Observing a disk marker must promote that path to a process-lifetime latch.");
string crossInstancePathA = "snapshot-cross-instance-A-" + Guid.NewGuid().ToString("N");
string crossInstancePathB = "snapshot-cross-instance-B-" + Guid.NewGuid().ToString("N");
SnapshotReconciliationPathLatch runtimeInstanceA = new SnapshotReconciliationPathLatch();
runtimeInstanceA.Latch(crossInstancePathA);
SnapshotReconciliationPathLatch runtimeInstanceB = new SnapshotReconciliationPathLatch();
Assert(runtimeInstanceB.IsBlocked(crossInstancePathA, persistentMarkerExists: false) &&
       !runtimeInstanceB.IsBlocked(crossInstancePathB, persistentMarkerExists: false),
    "A process-level reconciliation latch must survive Runtime destruction/recreation without leaking to another path.");

int storageRollbackLatchCalls = 0;
SnapshotStorageRollbackReconciliationResult storeAllStorageRollback =
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        SnapshotTransactionCommitResult.Failed("persist-store-all", rollbackSucceeded: false,
            recoveryBackupPath: "recovery-primary-C.json",
            candidateRecoveryPath: "recovery-candidate-B.json"),
        "store-all", reason =>
        {
            storageRollbackLatchCalls++;
            return reason.Contains("store-all", StringComparison.Ordinal) &&
                   reason.Contains("recovery=recovery-primary-C.json", StringComparison.Ordinal) &&
                   reason.Contains("candidate=recovery-candidate-B.json", StringComparison.Ordinal);
        });
Assert(storeAllStorageRollback.RequiresReconciliation &&
       storeAllStorageRollback.ReconciliationAttempted && storeAllStorageRollback.MarkerPersisted &&
       storageRollbackLatchCalls == 1,
    "StoreAll storage rollback failure must persist reconciliation exactly once.");
SnapshotStorageRollbackReconciliationResult withdrawSafeRollback =
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        SnapshotTransactionCommitResult.Failed("persist-withdraw", rollbackSucceeded: true),
        "withdraw", _ => throw new InvalidOperationException("must-not-latch"));
Assert(!withdrawSafeRollback.RequiresReconciliation &&
       !withdrawSafeRollback.ReconciliationAttempted && storageRollbackLatchCalls == 1,
    "A proven successful Withdraw storage rollback must not create a false reconciliation incident.");
SnapshotStorageRollbackReconciliationResult withdrawStorageRollback =
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        SnapshotTransactionCommitResult.Failed("persist-withdraw", rollbackSucceeded: false),
        "withdraw", _ => false);
Assert(withdrawStorageRollback.RequiresReconciliation &&
       withdrawStorageRollback.ReconciliationAttempted && !withdrawStorageRollback.MarkerPersisted &&
       withdrawStorageRollback.ErrorCode.EndsWith(":marker-persistence-failed", StringComparison.Ordinal),
    "Withdraw storage rollback plus marker-write failure must remain observable and fail closed.");

int unsafeStoreAllNativeCalls = 0;
SnapshotCrossBoundaryRecoveryResult unsafeStoreAllRecovery =
    SnapshotCrossBoundaryRecoveryCoordinator.Execute(storageRollbackProven: false,
        restoreNative: () => { unsafeStoreAllNativeCalls++; return true; });
Assert(!unsafeStoreAllRecovery.Success && !unsafeStoreAllRecovery.NativeRestoreAttempted &&
       unsafeStoreAllNativeCalls == 0 && unsafeStoreAllRecovery.ErrorCode == "storage-rollback-unproven",
    "StoreAll must not restore native inventory when storage rollback is unproven.");
int safeWithdrawNativeCalls = 0;
SnapshotCrossBoundaryRecoveryResult safeWithdrawRecovery =
    SnapshotCrossBoundaryRecoveryCoordinator.Execute(storageRollbackProven: true,
        restoreNative: () => { safeWithdrawNativeCalls++; return true; });
Assert(safeWithdrawRecovery.Success && safeWithdrawRecovery.NativeRestoreAttempted &&
       safeWithdrawNativeCalls == 1,
    "Withdraw may restore native inventory only after storage rollback is proven.");

string persistenceRoot = Path.Combine(Path.GetTempPath(),
    "SnapshotKernelPersistenceSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(persistenceRoot);
try
{
    SnapshotStorageIdentity storageIdentity = new SnapshotStorageIdentity("native-world-key", "native-player-slot-key");
    string snapshotPath = SnapshotStoragePath.Resolve(persistenceRoot, storageIdentity);
    Assert(snapshotPath.Contains(Path.Combine("InfiniteStorageSnapshots", "v1"),
               StringComparison.OrdinalIgnoreCase) &&
           !snapshotPath.Contains(Path.Combine("InfiniteStorage", "native-world-key"),
               StringComparison.OrdinalIgnoreCase),
        "Snapshot schema 1 must use a distinct versioned path, never the legacy InfiniteStorage tree.");

    string legacySentinelPath = Path.Combine(persistenceRoot, "UnturnedSingleplayerCheatMenu",
        "InfiniteStorage", "legacy-world", "legacy-player.json");
    Directory.CreateDirectory(Path.GetDirectoryName(legacySentinelPath)!);
    File.WriteAllText(legacySentinelPath, "legacy-sentinel-must-remain-byte-identical");
    byte[] legacySentinelBytes = File.ReadAllBytes(legacySentinelPath);
    DateTime legacySentinelWriteTime = File.GetLastWriteTimeUtc(legacySentinelPath);

    SnapshotPersistence emptyPersistence = new SnapshotPersistence(snapshotPath);
    SnapshotLoadResult emptyLoad = emptyPersistence.Load(storageIdentity);
    Assert(emptyLoad.Success && !emptyLoad.LoadedFromBackup && emptyLoad.Document.Groups.Count == 0 &&
           !File.Exists(snapshotPath) &&
           File.ReadAllBytes(legacySentinelPath).SequenceEqual(legacySentinelBytes) &&
           File.GetLastWriteTimeUtc(legacySentinelPath) == legacySentinelWriteTime,
        "A missing new snapshot file must load as empty without probing or touching legacy storage.");

    SnapshotKernel persistedKernel = new SnapshotKernel();
    Assert(persistedKernel.RegisterDefaultTemplate(group, template).Success &&
           persistedKernel.Store(group, baseline, 1).Success,
        "Persistence fixture should contain one real snapshot plus runtime-only template metadata.");
    SnapshotDocument firstDocument = persistedKernel.ExportDocument(storageIdentity);
    SnapshotSaveResult firstSave = emptyPersistence.Save(firstDocument);
    Assert(firstSave.Success && File.Exists(snapshotPath) && !File.Exists(snapshotPath + ".bak"),
        "First schema-1 save must atomically create only the new primary. Error=" + firstSave.ErrorCode);
    DirectoryInfo playerHashDirectory = Directory.GetParent(snapshotPath)!;
    DirectoryInfo worldHashDirectory = playerHashDirectory.Parent!;
    Assert(SnapshotStoragePath.IdentityHashHexLength == 24 &&
           playerHashDirectory.Name.Length == SnapshotStoragePath.IdentityHashHexLength &&
           worldHashDirectory.Name.Length == SnapshotStoragePath.IdentityHashHexLength,
        "Snapshot identity path segments must stay within the Unity Mono path budget using 96-bit SHA-256 prefixes.");
    SnapshotLoadResult mismatchedIdentityLoad = new SnapshotPersistence(snapshotPath).Load(
        new SnapshotStorageIdentity("different-world-key", "different-player-key"));
    Assert(!mismatchedIdentityLoad.Success &&
           mismatchedIdentityLoad.ErrorCode == "primary-and-backup-invalid",
        "A shortened path-hash collision must fail closed because the full WorldKey and PlayerKey remain bound inside the document.");
    JObject firstJson = JObject.Parse(File.ReadAllText(snapshotPath));
    Assert(firstJson["SchemaVersion"]?.Value<int>() == 1 &&
           firstJson["Groups"] is JArray { Count: 1 } &&
           firstJson.ToString().Contains("\"Count\": 1", StringComparison.Ordinal) &&
           !firstJson.ToString().Contains("DefaultTemplate", StringComparison.Ordinal) &&
           !firstJson.ToString().Contains("SemanticsRevision", StringComparison.Ordinal) &&
           !firstJson.ToString().Contains("MigrationRecords", StringComparison.Ordinal) &&
           !firstJson.ToString().Contains("LegacyAmbiguous", StringComparison.Ordinal),
        "Snapshot schema 1 must contain only identity, groups, exact snapshots, and instance counts.");
    string strictJson = File.ReadAllText(snapshotPath);
    string duplicateRootJson = strictJson.Insert(strictJson.IndexOf('{') + 1, "\"SchemaVersion\":1,");
    Assert(!SnapshotDocumentValidator.TryParse(strictJson + Environment.NewLine + "{}",
               out _, out _) &&
           !SnapshotDocumentValidator.TryParse(duplicateRootJson, out _, out _),
        "Schema 1 must reject trailing JSON values and duplicate required root properties.");

    Assert(persistedKernel.Store(group, baseline, 1).Success,
        "Second persistence fixture mutation should increase instance count to two.");
    byte[] firstPrimaryBytes = File.ReadAllBytes(snapshotPath);
    SnapshotSaveResult secondSave = emptyPersistence.Save(persistedKernel.ExportDocument(storageIdentity));
    Assert(secondSave.Success && File.Exists(snapshotPath + ".bak") &&
           File.ReadAllBytes(snapshotPath + ".bak").SequenceEqual(firstPrimaryBytes),
        "Replacing a valid primary must atomically retain the previous schema-1 document as .bak.");

    File.WriteAllText(snapshotPath, "{ damaged snapshot primary");
    byte[] goodBackupBytes = File.ReadAllBytes(snapshotPath + ".bak");
    string goodBackupHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(goodBackupBytes));
    SnapshotPersistence fallbackPersistence = new SnapshotPersistence(snapshotPath);
    SnapshotLoadResult fallbackLoad = fallbackPersistence.Load(storageIdentity);
    SnapshotKernel fallbackKernel = new SnapshotKernel();
    SnapshotMutationResult fallbackImport = fallbackKernel.ReplaceFromDocument(fallbackLoad.Document);
    Assert(fallbackLoad.Success && fallbackLoad.LoadedFromBackup && fallbackImport.Success &&
           fallbackKernel.GetGroups().Single().TotalCount == 1,
        "A malformed new primary must recover only from its new-format .bak.");
    Assert(fallbackKernel.Store(group, baseline, 1).Success,
        "Recovered snapshot should accept one later store before protected save.");
    SnapshotSaveResult fallbackSave = fallbackPersistence.Save(fallbackKernel.ExportDocument(storageIdentity));
    Assert(fallbackSave.Success &&
           Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(snapshotPath + ".bak"))) == goodBackupHash,
        "The first save after backup recovery must preserve the unique good .bak bytes. Error=" +
        fallbackSave.ErrorCode);

    byte[] primaryBeforeRejectedSave = File.ReadAllBytes(snapshotPath);
    SnapshotPersistence rejectingPersistence = new SnapshotPersistence(snapshotPath,
        (_, _, _) => false);
    SnapshotSaveResult rejectedSave = rejectingPersistence.Save(
        fallbackKernel.ExportDocument(storageIdentity));
    Assert(!rejectedSave.Success &&
           File.ReadAllBytes(snapshotPath).SequenceEqual(primaryBeforeRejectedSave),
        "An atomic replacement refusal must leave the active primary byte-identical.");

    byte[] backupBeforeDrift = File.ReadAllBytes(snapshotPath + ".bak");
    byte[] injectedPrimaryBytes = null;
    DateTime injectedPrimaryWriteTime = default;
    SnapshotDocument externalDocument = SnapshotDocument.Empty(storageIdentity);
    string externalJson = SnapshotDocumentCodec.Serialize(externalDocument);
    SnapshotPersistence driftingPersistence = new SnapshotPersistence(snapshotPath, null, stage =>
    {
        if (stage != "before-replace") return;
        File.WriteAllText(snapshotPath, externalJson);
        injectedPrimaryBytes = File.ReadAllBytes(snapshotPath);
        injectedPrimaryWriteTime = File.GetLastWriteTimeUtc(snapshotPath);
    });
    SnapshotSaveResult driftedSave = driftingPersistence.Save(
        fallbackKernel.ExportDocument(storageIdentity));
    Assert(!driftedSave.Success && driftedSave.ErrorCode == "primary-drift" &&
           File.ReadAllBytes(snapshotPath).SequenceEqual(injectedPrimaryBytes!) &&
           File.GetLastWriteTimeUtc(snapshotPath) == injectedPrimaryWriteTime &&
           File.ReadAllBytes(snapshotPath + ".bak").SequenceEqual(backupBeforeDrift),
        "A primary changed inside the save window must remain byte- and mtime-identical and must not rotate backup.");

    string tempDriftPath = Path.Combine(persistenceRoot, "temp-drift", "snapshot.json");
    SnapshotPersistence tempDriftBaseline = new SnapshotPersistence(tempDriftPath);
    Assert(tempDriftBaseline.Save(fallbackKernel.ExportDocument(storageIdentity)).Success &&
           tempDriftBaseline.Save(fallbackKernel.ExportDocument(storageIdentity)).Success,
        "Temp-drift fixture should establish an isolated primary and backup.");
    byte[] primaryBeforeTempDrift = File.ReadAllBytes(tempDriftPath);
    byte[] backupBeforeTempDrift = File.ReadAllBytes(tempDriftPath + ".bak");
    SnapshotPersistence tempDriftPersistence = new SnapshotPersistence(tempDriftPath, null, stage =>
    {
        if (stage != "before-replace") return;
        string temporary = Directory.EnumerateFiles(Path.GetDirectoryName(tempDriftPath)!,
            Path.GetFileName(tempDriftPath) + ".write-*.tmp").Single();
        File.WriteAllText(temporary, externalJson);
    });
    SnapshotSaveResult tempDriftSave = tempDriftPersistence.Save(
        fallbackKernel.ExportDocument(storageIdentity));
    Assert(!tempDriftSave.Success && tempDriftSave.ErrorCode == "temporary-drift" &&
           File.ReadAllBytes(tempDriftPath).SequenceEqual(primaryBeforeTempDrift) &&
           File.ReadAllBytes(tempDriftPath + ".bak").SequenceEqual(backupBeforeTempDrift),
        "A replaced transaction temp must be rejected before it can overwrite primary or rotate backup.");

    string firstCreatePath = Path.Combine(persistenceRoot, "first-create-drift", "snapshot.json");
    byte[] externallyCreatedBytes = null;
    DateTime externallyCreatedWriteTime = default;
    SnapshotPersistence firstCreateDrift = new SnapshotPersistence(firstCreatePath, null, stage =>
    {
        if (stage != "before-replace") return;
        Directory.CreateDirectory(Path.GetDirectoryName(firstCreatePath)!);
        File.WriteAllText(firstCreatePath, externalJson);
        externallyCreatedBytes = File.ReadAllBytes(firstCreatePath);
        externallyCreatedWriteTime = File.GetLastWriteTimeUtc(firstCreatePath);
    });
    SnapshotSaveResult firstCreateDriftResult = firstCreateDrift.Save(
        fallbackKernel.ExportDocument(storageIdentity));
    Assert(!firstCreateDriftResult.Success && firstCreateDriftResult.ErrorCode == "primary-drift" &&
           File.ReadAllBytes(firstCreatePath).SequenceEqual(externallyCreatedBytes!) &&
           File.GetLastWriteTimeUtc(firstCreatePath) == externallyCreatedWriteTime,
        "An externally created primary must never be deleted or overwritten by a stale first-save attempt.");

    string forensicFailurePath = Path.Combine(persistenceRoot, "failure-forensic", "snapshot.json");
    SnapshotPersistence forensicFailure = new SnapshotPersistence(forensicFailurePath, null, stage =>
    {
        if (stage == "before-replace") throw new InvalidOperationException("injected-before-replace");
    });
    SnapshotSaveResult forensicFailureResult = forensicFailure.Save(
        fallbackKernel.ExportDocument(storageIdentity));
    string[] forensicTemps = Directory.EnumerateFiles(Path.GetDirectoryName(forensicFailurePath)!,
        Path.GetFileName(forensicFailurePath) + ".write-*.tmp").ToArray();
    Assert(!forensicFailureResult.Success &&
           forensicFailureResult.ErrorCode == "save-failed:InvalidOperationException" &&
           !File.Exists(forensicFailurePath) && forensicTemps.Length == 1 &&
           SnapshotDocumentValidator.TryParse(File.ReadAllText(forensicTemps[0]), out _, out _),
        "A failed save must preserve its flushed temporary candidate as a forensic artifact; failure cleanup must not race-delete an external replacement.");

    string candidateFailurePath = Path.Combine(persistenceRoot, "candidate-persist-failure", "snapshot.json");
    SnapshotPersistence candidateFailureBaseline = new SnapshotPersistence(candidateFailurePath);
    Assert(candidateFailureBaseline.Save(fallbackKernel.ExportDocument(storageIdentity)).Success,
        "Candidate persistence failure fixture should establish A.");
    byte[] candidateFailureA = File.ReadAllBytes(candidateFailurePath);
    SnapshotPersistence candidateFailurePersistence = new SnapshotPersistence(candidateFailurePath, null, stage =>
    {
        if (stage.StartsWith("before-candidate-persist|", StringComparison.Ordinal))
            throw new IOException("candidate-persist-injected");
    });
    SnapshotLoadResult candidateFailureLoad = candidateFailurePersistence.Load(storageIdentity);
    SnapshotKernel candidateFailureKernel = new SnapshotKernel();
    Assert(candidateFailureLoad.Success &&
           candidateFailureKernel.ReplaceFromDocument(candidateFailureLoad.Document).Success,
        "Candidate persistence failure fixture should load A.");
    SnapshotKernelTransaction candidateFailureTransaction =
        new SnapshotKernelTransaction(candidateFailureKernel, storageIdentity);
    Assert(candidateFailureTransaction.Store(group, baseline, 1).Success,
        "Candidate persistence failure fixture should stage B.");
    byte[] candidateFailureB = null;
    SnapshotTransactionCommitResult candidateFailureCommit = candidateFailureTransaction.Commit(document =>
    {
        candidateFailureB = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return candidateFailurePersistence.Save(document);
    });
    Assert(!candidateFailureCommit.Success && candidateFailureCommit.RollbackSucceeded &&
           candidateFailureCommit.ErrorCode == "candidate-persistence-failed" &&
           File.ReadAllBytes(candidateFailurePath).SequenceEqual(candidateFailureA) &&
           !Directory.EnumerateFiles(Path.GetDirectoryName(candidateFailurePath)!,
               Path.GetFileName(candidateFailurePath) + ".pre-replace-*.bak").Any() &&
           !string.IsNullOrWhiteSpace(candidateFailureCommit.CandidateRecoveryPath) &&
           File.ReadAllBytes(candidateFailureCommit.CandidateRecoveryPath).SequenceEqual(candidateFailureB!),
        "Candidate persistence failure must stop before Replace while preserving A and the flushed write candidate B.");

    string lostUpdatePath = Path.Combine(persistenceRoot, "load-save-drift", "snapshot.json");
    SnapshotPersistence lostUpdatePersistence = new SnapshotPersistence(lostUpdatePath);
    SnapshotDocument loadedA = fallbackKernel.ExportDocument(storageIdentity);
    Assert(lostUpdatePersistence.Save(loadedA).Success &&
           lostUpdatePersistence.Load(storageIdentity).Success,
        "Load-to-save drift fixture should establish and read document A.");
    SnapshotDocument externalB = SnapshotDocument.Empty(storageIdentity);
    string externalBJson = SnapshotDocumentCodec.Serialize(externalB);
    File.WriteAllText(lostUpdatePath, externalBJson);
    byte[] externalBBytes = File.ReadAllBytes(lostUpdatePath);
    DateTime externalBWriteTime = File.GetLastWriteTimeUtc(lostUpdatePath);
    SnapshotSaveResult lostUpdateSave = lostUpdatePersistence.Save(loadedA);
    Assert(!lostUpdateSave.Success && lostUpdateSave.ErrorCode == "primary-drift" &&
           File.ReadAllBytes(lostUpdatePath).SequenceEqual(externalBBytes) &&
           File.GetLastWriteTimeUtc(lostUpdatePath) == externalBWriteTime &&
           !File.Exists(lostUpdatePath + ".bak"),
        "A valid external update after Load must not be overwritten by a stale in-memory mutation.");

    byte[] primaryBeforeReconciliationLatch = File.ReadAllBytes(lostUpdatePath);
    Assert(lostUpdatePersistence.TryCreateReconciliationLatch(
               "autopickup-instance=777;storage-authoritative") &&
           lostUpdatePersistence.HasReconciliationLatch &&
           new SnapshotPersistence(lostUpdatePath).HasReconciliationLatch &&
           !new SnapshotPersistence(lostUpdatePath).Load(storageIdentity).Success &&
           File.ReadAllBytes(lostUpdatePath).SequenceEqual(primaryBeforeReconciliationLatch) &&
           !File.Exists(lostUpdatePath + ".bak"),
        "A reconciliation latch must survive runtime recreation without changing Snapshot primary or backup.");
    SnapshotSaveResult markerBlockedSave = lostUpdatePersistence.Save(loadedA);
    Assert(!markerBlockedSave.Success && markerBlockedSave.ErrorCode == "reconciliation-required" &&
           File.ReadAllBytes(lostUpdatePath).SequenceEqual(primaryBeforeReconciliationLatch) &&
           !File.Exists(lostUpdatePath + ".bak"),
        "Save must fail closed at entry while a persistent reconciliation marker exists.");

    string markerWindowPath = Path.Combine(persistenceRoot, "marker-window", "snapshot.json");
    SnapshotPersistence markerWindowBaseline = new SnapshotPersistence(markerWindowPath);
    Assert(markerWindowBaseline.Save(loadedA).Success && markerWindowBaseline.Save(loadedA).Success,
        "Marker-window fixture should establish an isolated primary and backup.");
    byte[] markerWindowPrimary = File.ReadAllBytes(markerWindowPath);
    byte[] markerWindowBackup = File.ReadAllBytes(markerWindowPath + ".bak");
    byte[] externalMarkerBytes = System.Text.Encoding.UTF8.GetBytes("external-reconciliation-marker");
    SnapshotPersistence markerWindowPersistence = new SnapshotPersistence(markerWindowPath, null, stage =>
    {
        if (stage == "before-replace") File.WriteAllBytes(
            markerWindowPath + ".reconciliation-required", externalMarkerBytes);
    });
    Assert(markerWindowPersistence.Load(storageIdentity).Success,
        "Marker-window fixture must bind the loaded primary/backup versions before injection.");
    SnapshotSaveResult markerWindowSave = markerWindowPersistence.Save(loadedA);
    Assert(!markerWindowSave.Success && markerWindowSave.ErrorCode == "reconciliation-required" &&
           File.ReadAllBytes(markerWindowPath).SequenceEqual(markerWindowPrimary) &&
           File.ReadAllBytes(markerWindowPath + ".bak").SequenceEqual(markerWindowBackup) &&
           File.ReadAllBytes(markerWindowPath + ".reconciliation-required").SequenceEqual(externalMarkerBytes),
        "A reconciliation marker created inside the save window must block replacement without altering primary, backup, or marker.");

    string postReplacePath = Path.Combine(persistenceRoot, "post-replace-uncertain", "snapshot.json");
    SnapshotPersistence postReplaceBaseline = new SnapshotPersistence(postReplacePath);
    Assert(postReplaceBaseline.Save(loadedA).Success,
        "Post-replace uncertainty fixture should establish document A.");
    byte[] postReplaceBaselineBytes = File.ReadAllBytes(postReplacePath);
    byte[] injectedPostReplaceBytes = System.Text.Encoding.UTF8.GetBytes(externalJson);
    SnapshotPersistence uncertainPersistence = new SnapshotPersistence(postReplacePath, null, stage =>
    {
        if (stage == "after-replace-before-verification")
            File.WriteAllBytes(postReplacePath, injectedPostReplaceBytes);
    });
    Assert(uncertainPersistence.Load(storageIdentity).Success,
        "Post-replace uncertainty fixture should bind document A before mutation.");
    SnapshotKernel uncertainKernel = new SnapshotKernel();
    Assert(uncertainKernel.ReplaceFromDocument(loadedA).Success,
        "Post-replace uncertainty fixture should import document A.");
    SnapshotKernelTransaction uncertainTransaction = new SnapshotKernelTransaction(uncertainKernel, storageIdentity);
    Assert(uncertainTransaction.Store(group, baseline, 1).Success,
        "Post-replace uncertainty fixture should stage A-prime.");
    byte[] committedCandidateB = null;
    SnapshotTransactionCommitResult uncertainCommit = uncertainTransaction.Commit(document =>
    {
        committedCandidateB = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return uncertainPersistence.Save(document);
    });
    string uncertainRecoveryHash = File.Exists(uncertainCommit.RecoveryBackupPath)
        ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(uncertainCommit.RecoveryBackupPath))) : "missing";
    string uncertainCandidateHash = File.Exists(uncertainCommit.CandidateRecoveryPath)
        ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(uncertainCommit.CandidateRecoveryPath))) : "missing";
    string expectedCandidateHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        committedCandidateB ?? Array.Empty<byte>()));
    Assert(!uncertainCommit.Success && !uncertainCommit.RollbackSucceeded &&
           uncertainCommit.ErrorCode == "saved-file-verification-failed" &&
           File.ReadAllBytes(postReplacePath).SequenceEqual(injectedPostReplaceBytes) &&
           !string.IsNullOrWhiteSpace(uncertainCommit.RecoveryBackupPath) &&
           File.ReadAllBytes(uncertainCommit.RecoveryBackupPath).SequenceEqual(postReplaceBaselineBytes) &&
           !string.IsNullOrWhiteSpace(uncertainCommit.CandidateRecoveryPath) &&
           File.ReadAllBytes(uncertainCommit.CandidateRecoveryPath).SequenceEqual(committedCandidateB!),
        "A late overwrite after Replace must preserve both pre-replace A and committed candidate B before native compensation is forbidden." +
        " error=" + uncertainCommit.ErrorCode +
        " recovery=" + uncertainCommit.RecoveryBackupPath + " recoverySha=" + uncertainRecoveryHash +
        " candidate=" + uncertainCommit.CandidateRecoveryPath + " candidateSha=" + uncertainCandidateHash +
        " expectedCandidateSha=" + expectedCandidateHash);

    string mutatingThrowPath = Path.Combine(persistenceRoot, "replace-mutates-then-throws", "snapshot.json");
    SnapshotPersistence mutatingThrowBaseline = new SnapshotPersistence(mutatingThrowPath);
    Assert(mutatingThrowBaseline.Save(loadedA).Success,
        "Mutating-throw fixture should establish document A.");
    byte[] mutatingThrowExternal = System.Text.Encoding.UTF8.GetBytes(externalJson);
    SnapshotPersistence mutatingThrowPersistence = new SnapshotPersistence(mutatingThrowPath,
        (_, destination, _) =>
        {
            File.WriteAllBytes(destination, mutatingThrowExternal);
            throw new IOException("replace-mutated-then-threw");
        });
    Assert(mutatingThrowPersistence.Load(storageIdentity).Success,
        "Mutating-throw fixture should bind A before commit.");
    SnapshotKernel mutatingThrowKernel = new SnapshotKernel();
    Assert(mutatingThrowKernel.ReplaceFromDocument(loadedA).Success,
        "Mutating-throw fixture should import A.");
    SnapshotKernelTransaction mutatingThrowTransaction = new SnapshotKernelTransaction(mutatingThrowKernel, storageIdentity);
    Assert(mutatingThrowTransaction.Store(group, baseline, 1).Success,
        "Mutating-throw fixture should stage A-prime.");
    byte[] mutatingThrowCandidate = null;
    SnapshotTransactionCommitResult mutatingThrowCommit = mutatingThrowTransaction.Commit(document =>
    {
        mutatingThrowCandidate = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return mutatingThrowPersistence.Save(document);
    });
    Assert(!mutatingThrowCommit.Success && !mutatingThrowCommit.RollbackSucceeded &&
           mutatingThrowCommit.ErrorCode == "save-failed:IOException" &&
           File.ReadAllBytes(mutatingThrowPath).SequenceEqual(mutatingThrowExternal) &&
           !string.IsNullOrWhiteSpace(mutatingThrowCommit.CandidateRecoveryPath) &&
           File.ReadAllBytes(mutatingThrowCommit.CandidateRecoveryPath).SequenceEqual(mutatingThrowCandidate!),
        "A replace operation that mutates then throws must preserve both observed external bytes and candidate B.");

    string mutatingFalsePath = Path.Combine(persistenceRoot, "replace-mutates-then-false", "snapshot.json");
    SnapshotPersistence mutatingFalseBaseline = new SnapshotPersistence(mutatingFalsePath);
    Assert(mutatingFalseBaseline.Save(loadedA).Success,
        "Mutating-false fixture should establish document A.");
    byte[] mutatingFalseExternal = System.Text.Encoding.UTF8.GetBytes(externalJson);
    SnapshotPersistence mutatingFalsePersistence = new SnapshotPersistence(mutatingFalsePath,
        (_, destination, _) =>
        {
            File.WriteAllBytes(destination, mutatingFalseExternal);
            return false;
        });
    Assert(mutatingFalsePersistence.Load(storageIdentity).Success,
        "Mutating-false fixture should bind A before commit.");
    SnapshotKernel mutatingFalseKernel = new SnapshotKernel();
    Assert(mutatingFalseKernel.ReplaceFromDocument(loadedA).Success,
        "Mutating-false fixture should import A.");
    SnapshotKernelTransaction mutatingFalseTransaction = new SnapshotKernelTransaction(mutatingFalseKernel, storageIdentity);
    Assert(mutatingFalseTransaction.Store(group, baseline, 1).Success,
        "Mutating-false fixture should stage A-prime.");
    byte[] mutatingFalseCandidate = null;
    SnapshotTransactionCommitResult mutatingFalseCommit = mutatingFalseTransaction.Commit(document =>
    {
        mutatingFalseCandidate = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return mutatingFalsePersistence.Save(document);
    });
    Assert(!mutatingFalseCommit.Success && !mutatingFalseCommit.RollbackSucceeded &&
           mutatingFalseCommit.ErrorCode == "atomic-replacement-conflict" &&
           File.ReadAllBytes(mutatingFalsePath).SequenceEqual(mutatingFalseExternal) &&
           !string.IsNullOrWhiteSpace(mutatingFalseCommit.CandidateRecoveryPath) &&
           File.ReadAllBytes(mutatingFalseCommit.CandidateRecoveryPath).SequenceEqual(mutatingFalseCandidate!),
        "A replace operation that mutates then returns false must preserve both conflict bytes and candidate B.");

    string backupFallbackRacePath = Path.Combine(persistenceRoot,
        "backup-fallback-final-replace-race", "snapshot.json");
    Directory.CreateDirectory(Path.GetDirectoryName(backupFallbackRacePath)!);
    File.WriteAllText(backupFallbackRacePath, "{ damaged primary A");
    File.WriteAllText(backupFallbackRacePath + ".bak", SnapshotDocumentCodec.Serialize(loadedA));
    byte[] stableFallbackBackup = File.ReadAllBytes(backupFallbackRacePath + ".bak");
    byte[] externalPrimaryC = System.Text.Encoding.UTF8.GetBytes(externalJson);
    SnapshotPersistence backupFallbackRace = new SnapshotPersistence(backupFallbackRacePath,
        (source, destination, atomicBackup) =>
        {
            File.WriteAllBytes(destination, externalPrimaryC);
            File.Replace(source, destination,
                string.IsNullOrWhiteSpace(atomicBackup) ? null : atomicBackup,
                ignoreMetadataErrors: true);
            return true;
        });
    SnapshotLoadResult backupFallbackRaceLoad = backupFallbackRace.Load(storageIdentity);
    Assert(backupFallbackRaceLoad.Success && backupFallbackRaceLoad.LoadedFromBackup,
        "Final-replace race fixture must load the unique good stable backup B.");
    SnapshotSaveResult backupFallbackRaceSave = backupFallbackRace.Save(loadedA);
    byte[] backupFallbackCandidateB = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    string[] instantPrimaryBackups = Directory.EnumerateFiles(
        Path.GetDirectoryName(backupFallbackRacePath)!,
        Path.GetFileName(backupFallbackRacePath) + ".pre-replace-*.bak").ToArray();
    Assert(!backupFallbackRaceSave.Success && backupFallbackRaceSave.MayHaveCommitted &&
           instantPrimaryBackups.Length == 1 &&
           backupFallbackRaceSave.RecoveryBackupPath == instantPrimaryBackups[0] &&
           File.ReadAllBytes(instantPrimaryBackups[0]).SequenceEqual(externalPrimaryC) &&
           !string.IsNullOrWhiteSpace(backupFallbackRaceSave.CandidateRecoveryPath) &&
           File.ReadAllBytes(backupFallbackRaceSave.CandidateRecoveryPath).SequenceEqual(backupFallbackCandidateB) &&
           File.ReadAllBytes(backupFallbackRacePath + ".bak").SequenceEqual(stableFallbackBackup),
        "A loaded-from-backup final Replace race must preserve stable B, instant primary C, and committed candidate B.");

    string rotationPath = Path.Combine(persistenceRoot, "clean-backup-rotation", "snapshot.json");
    SnapshotKernel rotationKernel = new SnapshotKernel();
    Assert(rotationKernel.ReplaceFromDocument(loadedA).Success,
        "Clean rotation fixture should import document A.");
    SnapshotPersistence rotationPersistence = new SnapshotPersistence(rotationPath);
    Assert(rotationPersistence.Save(rotationKernel.ExportDocument(storageIdentity)).Success,
        "Clean rotation fixture should save A.");
    byte[] rotationA = File.ReadAllBytes(rotationPath);
    Assert(rotationKernel.Store(group, baseline, 1).Success &&
           rotationPersistence.Save(rotationKernel.ExportDocument(storageIdentity)).Success,
        "Clean rotation fixture should save B.");
    byte[] rotationB = File.ReadAllBytes(rotationPath);
    Assert(File.ReadAllBytes(rotationPath + ".bak").SequenceEqual(rotationA),
        "The second clean save must promote instant primary A to stable .bak.");
    Assert(rotationKernel.Store(group, baseline, 1).Success &&
           rotationPersistence.Save(rotationKernel.ExportDocument(storageIdentity)).Success &&
           File.ReadAllBytes(rotationPath + ".bak").SequenceEqual(rotationB) &&
           File.ReadAllBytes(rotationPath + ".bak.previous").SequenceEqual(rotationA),
        "The third clean save must promote B to .bak and atomically retain prior .bak A as .bak.previous.");
    byte[] rotationC = File.ReadAllBytes(rotationPath);
    Assert(rotationKernel.Store(group, baseline, 1).Success &&
           rotationPersistence.Save(rotationKernel.ExportDocument(storageIdentity)).Success &&
           File.ReadAllBytes(rotationPath + ".bak").SequenceEqual(rotationC) &&
           File.ReadAllBytes(rotationPath + ".bak.previous").SequenceEqual(rotationB) &&
           File.ReadAllBytes(rotationPath + ".candidate.latest").SequenceEqual(
               File.ReadAllBytes(rotationPath)) &&
           File.ReadAllBytes(rotationPath + ".candidate.previous").SequenceEqual(rotationC) &&
           !Directory.EnumerateFiles(Path.GetDirectoryName(rotationPath)!,
               Path.GetFileName(rotationPath) + ".candidate-recovery-*.json").Any(),
        "Repeated clean rotation must keep both backup and candidate success histories bounded to two fixed generations.");

    string postReplaceMarkerPath = Path.Combine(persistenceRoot, "post-replace-marker", "snapshot.json");
    SnapshotPersistence postReplaceMarkerBaseline = new SnapshotPersistence(postReplaceMarkerPath);
    Assert(postReplaceMarkerBaseline.Save(loadedA).Success,
        "Post-replace marker fixture should establish A.");
    byte[] postReplaceMarkerA = File.ReadAllBytes(postReplaceMarkerPath);
    SnapshotPersistence postReplaceMarkerPersistence = new SnapshotPersistence(postReplaceMarkerPath, null, stage =>
    {
        if (stage == "after-replace-before-verification")
            File.WriteAllText(postReplaceMarkerPath + ".reconciliation-required", "external-marker");
    });
    Assert(postReplaceMarkerPersistence.Load(storageIdentity).Success,
        "Post-replace marker fixture should bind A.");
    SnapshotSaveResult postReplaceMarkerSave = postReplaceMarkerPersistence.Save(loadedA);
    byte[] postReplaceMarkerCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!postReplaceMarkerSave.Success && postReplaceMarkerSave.MayHaveCommitted &&
           postReplaceMarkerSave.ErrorCode == "reconciliation-required" &&
           !string.IsNullOrWhiteSpace(postReplaceMarkerSave.RecoveryBackupPath) &&
           File.ReadAllBytes(postReplaceMarkerSave.RecoveryBackupPath).SequenceEqual(postReplaceMarkerA) &&
           !string.IsNullOrWhiteSpace(postReplaceMarkerSave.CandidateRecoveryPath) &&
           File.ReadAllBytes(postReplaceMarkerSave.CandidateRecoveryPath).SequenceEqual(postReplaceMarkerCandidate),
        "A marker appearing after Replace must retain both the instant primary and committed candidate.");

    string finalVerificationPath = Path.Combine(persistenceRoot, "final-output-verification", "snapshot.json");
    SnapshotPersistence finalVerificationBaseline = new SnapshotPersistence(finalVerificationPath);
    Assert(finalVerificationBaseline.Save(loadedA).Success &&
           finalVerificationBaseline.Save(externalDocument).Success,
        "Final output verification fixture should establish bounded success history S1/S0.");
    byte[] finalVerificationA = File.ReadAllBytes(finalVerificationPath);
    byte[] finalHistoryLatest = File.ReadAllBytes(finalVerificationPath + ".candidate.latest");
    byte[] finalHistoryPrevious = File.ReadAllBytes(finalVerificationPath + ".candidate.previous");
    byte[] finalVerificationExternal = System.Text.Encoding.UTF8.GetBytes(externalJson);
    SnapshotPersistence finalVerificationPersistence = new SnapshotPersistence(finalVerificationPath, null, stage =>
    {
        if (stage == "after-backup-promotion-before-final-verification")
            File.WriteAllBytes(finalVerificationPath, finalVerificationExternal);
    });
    Assert(finalVerificationPersistence.Load(storageIdentity).Success,
        "Final output verification fixture should bind A.");
    SnapshotSaveResult finalVerificationSave = finalVerificationPersistence.Save(loadedA);
    byte[] finalVerificationCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!finalVerificationSave.Success && finalVerificationSave.MayHaveCommitted &&
           finalVerificationSave.ErrorCode == "saved-file-verification-failed" &&
           File.ReadAllBytes(finalVerificationPath).SequenceEqual(finalVerificationExternal) &&
           File.ReadAllBytes(finalVerificationPath + ".bak").SequenceEqual(finalVerificationA) &&
           !string.IsNullOrWhiteSpace(finalVerificationSave.CandidateRecoveryPath) &&
           finalVerificationSave.CandidateRecoveryPath.Contains(".candidate-recovery-", StringComparison.Ordinal) &&
           File.ReadAllBytes(finalVerificationSave.CandidateRecoveryPath).SequenceEqual(finalVerificationCandidate) &&
           File.ReadAllBytes(finalVerificationPath + ".candidate.latest").SequenceEqual(finalHistoryLatest) &&
           File.ReadAllBytes(finalVerificationPath + ".candidate.previous").SequenceEqual(finalHistoryPrevious),
        "Final output drift must preserve unique candidate B without changing successful S1/S0 history.");

    string finalMarkerHistoryPath = Path.Combine(persistenceRoot, "final-marker-history", "snapshot.json");
    SnapshotPersistence finalMarkerHistoryBaseline = new SnapshotPersistence(finalMarkerHistoryPath);
    Assert(finalMarkerHistoryBaseline.Save(loadedA).Success &&
           finalMarkerHistoryBaseline.Save(externalDocument).Success,
        "Final marker fixture should establish bounded success history S1/S0.");
    byte[] markerHistoryLatest = File.ReadAllBytes(finalMarkerHistoryPath + ".candidate.latest");
    byte[] markerHistoryPrevious = File.ReadAllBytes(finalMarkerHistoryPath + ".candidate.previous");
    SnapshotPersistence finalMarkerHistoryPersistence = new SnapshotPersistence(finalMarkerHistoryPath, null, stage =>
    {
        if (stage == "after-backup-promotion-before-final-verification")
            File.WriteAllText(finalMarkerHistoryPath + ".reconciliation-required", "late-marker");
    });
    Assert(finalMarkerHistoryPersistence.Load(storageIdentity).Success,
        "Final marker fixture should bind the current success document.");
    SnapshotSaveResult finalMarkerHistorySave = finalMarkerHistoryPersistence.Save(loadedA);
    byte[] finalMarkerCandidate = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(loadedA));
    string finalMarkerReason = string.Empty;
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        SnapshotTransactionCommitResult.Failed(finalMarkerHistorySave.ErrorCode,
            rollbackSucceeded: false,
            recoveryBackupPath: finalMarkerHistorySave.RecoveryBackupPath,
            candidateRecoveryPath: finalMarkerHistorySave.CandidateRecoveryPath),
        "final-marker-history", reason => { finalMarkerReason = reason; return true; });
    Assert(!finalMarkerHistorySave.Success && finalMarkerHistorySave.MayHaveCommitted &&
           finalMarkerHistorySave.ErrorCode == "reconciliation-required" &&
           finalMarkerHistorySave.CandidateRecoveryPath.Contains(".candidate-recovery-", StringComparison.Ordinal) &&
           File.ReadAllBytes(finalMarkerHistorySave.CandidateRecoveryPath).SequenceEqual(finalMarkerCandidate) &&
           File.ReadAllBytes(finalMarkerHistoryPath + ".candidate.latest").SequenceEqual(markerHistoryLatest) &&
           File.ReadAllBytes(finalMarkerHistoryPath + ".candidate.previous").SequenceEqual(markerHistoryPrevious) &&
           finalMarkerReason.Contains(finalMarkerHistorySave.CandidateRecoveryPath, StringComparison.Ordinal),
        "A final marker must preserve unique candidate B and leave successful S1/S0 history unchanged and diagnosable.");

    string promotionFailurePath = Path.Combine(persistenceRoot, "candidate-promotion-failure", "snapshot.json");
    SnapshotPersistence promotionFailureBaseline = new SnapshotPersistence(promotionFailurePath);
    Assert(promotionFailureBaseline.Save(loadedA).Success &&
           promotionFailureBaseline.Save(externalDocument).Success,
        "Candidate promotion failure fixture should establish S1/S0.");
    byte[] promotionFailureLatest = File.ReadAllBytes(promotionFailurePath + ".candidate.latest");
    byte[] promotionFailurePrevious = File.ReadAllBytes(promotionFailurePath + ".candidate.previous");
    SnapshotPersistence promotionFailurePersistence = new SnapshotPersistence(promotionFailurePath, null, stage =>
    {
        if (stage == "during-candidate-history-promotion")
            throw new IOException("candidate-history-promotion-injected");
    });
    Assert(promotionFailurePersistence.Load(storageIdentity).Success,
        "Candidate promotion failure fixture should bind the current document.");
    SnapshotSaveResult promotionFailureSave = promotionFailurePersistence.Save(loadedA);
    byte[] promotionFailureCandidate = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!promotionFailureSave.Success && promotionFailureSave.MayHaveCommitted &&
           promotionFailureSave.ErrorCode == "save-failed:IOException" &&
           promotionFailureSave.CandidateRecoveryPath.Contains(".candidate-recovery-", StringComparison.Ordinal) &&
           File.ReadAllBytes(promotionFailureSave.CandidateRecoveryPath).SequenceEqual(promotionFailureCandidate) &&
           File.ReadAllBytes(promotionFailurePath + ".candidate.latest").SequenceEqual(promotionFailureLatest) &&
           File.ReadAllBytes(promotionFailurePath + ".candidate.previous").SequenceEqual(promotionFailurePrevious),
        "Candidate history promotion failure must not report success or alter S1/S0, and unique B must remain recoverable.");

    string duringHistoryDriftPath = Path.Combine(persistenceRoot,
        "candidate-history-drift-during-promotion", "snapshot.json");
    SnapshotPersistence duringHistoryDriftBaseline = new SnapshotPersistence(duringHistoryDriftPath);
    Assert(duringHistoryDriftBaseline.Save(loadedA).Success &&
           duringHistoryDriftBaseline.Save(externalDocument).Success,
        "During-promotion history drift fixture should establish S1/S0.");
    byte[] duringHistoryDriftLatest = File.ReadAllBytes(duringHistoryDriftPath + ".candidate.latest");
    SnapshotDocument externalPreviousDocument = DocumentWithExact(storageIdentity, group,
        Snapshot(100, 12, 34, 5, 6, 7, 8), 9);
    byte[] externalPreviousBytes = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(externalPreviousDocument));
    SnapshotPersistence duringHistoryDriftPersistence = new SnapshotPersistence(
        duringHistoryDriftPath, null, stage =>
        {
            if (stage == "during-candidate-history-promotion")
                File.WriteAllBytes(duringHistoryDriftPath + ".candidate.previous", externalPreviousBytes);
        });
    Assert(duringHistoryDriftPersistence.Load(storageIdentity).Success,
        "During-promotion history drift fixture should bind the current document.");
    SnapshotSaveResult duringHistoryDriftSave = duringHistoryDriftPersistence.Save(loadedA);
    byte[] duringHistoryDriftCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!duringHistoryDriftSave.Success,
        "A candidate.previous update inside the promotion hook must never be overwritten and reported as success.");
    Assert(duringHistoryDriftSave.MayHaveCommitted &&
           duringHistoryDriftSave.ErrorCode == "candidate-history-drift" &&
           duringHistoryDriftSave.CandidateRecoveryPath.Contains(".candidate-recovery-", StringComparison.Ordinal) &&
           File.ReadAllBytes(duringHistoryDriftSave.CandidateRecoveryPath)
               .SequenceEqual(duringHistoryDriftCandidate) &&
           File.ReadAllBytes(duringHistoryDriftPath + ".candidate.latest")
               .SequenceEqual(duringHistoryDriftLatest) &&
           File.ReadAllBytes(duringHistoryDriftPath + ".candidate.previous")
               .SequenceEqual(externalPreviousBytes),
        "During-promotion candidate history drift must preserve external C, leave S1 unchanged, and retain unique B.");

    string finalHistoryLatestPath = Path.Combine(persistenceRoot,
        "final-history-latest-replacement", "snapshot.json");
    SnapshotPersistence finalHistoryLatestBaseline = new SnapshotPersistence(finalHistoryLatestPath);
    Assert(finalHistoryLatestBaseline.Save(loadedA).Success &&
           finalHistoryLatestBaseline.Save(externalDocument).Success,
        "Final-history latest fixture should establish S1/S0.");
    byte[] finalHistoryLatestPrevious = File.ReadAllBytes(finalHistoryLatestPath + ".candidate.previous");
    SnapshotDocument finalHistoryLatestExternalDocument = DocumentWithExact(storageIdentity, group,
        Snapshot(100, 21, 43, 1, 9, 1, 9), 11);
    byte[] finalHistoryLatestExternal = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(finalHistoryLatestExternalDocument));
    bool finalHistoryLatestHookObserved = false;
    SnapshotPersistence finalHistoryLatestPersistence = new SnapshotPersistence(
        finalHistoryLatestPath, null, stage =>
        {
            if (stage != "after-final-candidate-history-check-before-replace") return;
            finalHistoryLatestHookObserved = true;
            File.WriteAllBytes(finalHistoryLatestPath + ".candidate.latest", finalHistoryLatestExternal);
        });
    Assert(finalHistoryLatestPersistence.Load(storageIdentity).Success,
        "Final-history latest fixture should bind the current primary.");
    SnapshotSaveResult finalHistoryLatestSave = finalHistoryLatestPersistence.Save(loadedA);
    byte[] finalHistoryLatestCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(finalHistoryLatestHookObserved,
        "The deterministic final-history check-to-Replace injection seam must execute.");
    Assert(!finalHistoryLatestSave.Success,
        "A replacement-instant candidate.latest update must not be reported as success.");
    string finalHistoryLatestRecovery = finalHistoryLatestSave.CandidateHistoryRecoveryPath;
    Assert(finalHistoryLatestSave.MayHaveCommitted &&
           finalHistoryLatestSave.ErrorCode == "candidate-history-drift" &&
           !string.IsNullOrWhiteSpace(finalHistoryLatestRecovery) &&
           File.ReadAllBytes(finalHistoryLatestRecovery).SequenceEqual(finalHistoryLatestExternal) &&
           File.ReadAllBytes(finalHistoryLatestSave.CandidateRecoveryPath)
               .SequenceEqual(finalHistoryLatestCandidate) &&
           File.ReadAllBytes(finalHistoryLatestPath + ".candidate.previous")
               .SequenceEqual(finalHistoryLatestPrevious),
        "Atomic latest replacement must retain exact external C at a reported unique recovery path, keep B locatable, and preserve fixed previous.");

    string finalHistoryPreviousPath = Path.Combine(persistenceRoot,
        "final-history-previous-replacement", "snapshot.json");
    SnapshotPersistence finalHistoryPreviousBaseline = new SnapshotPersistence(finalHistoryPreviousPath);
    Assert(finalHistoryPreviousBaseline.Save(loadedA).Success &&
           finalHistoryPreviousBaseline.Save(externalDocument).Success,
        "Final-history previous fixture should establish S1/S0.");
    byte[] finalHistoryPreviousLatest = File.ReadAllBytes(
        finalHistoryPreviousPath + ".candidate.latest");
    SnapshotDocument finalHistoryPreviousExternalDocument = DocumentWithExact(storageIdentity, group,
        Snapshot(100, 22, 42, 2, 8, 2, 8), 12);
    byte[] finalHistoryPreviousExternal = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(finalHistoryPreviousExternalDocument));
    bool finalHistoryPreviousHookObserved = false;
    SnapshotPersistence finalHistoryPreviousPersistence = new SnapshotPersistence(
        finalHistoryPreviousPath, null, stage =>
        {
            if (stage != "after-final-candidate-history-check-before-replace") return;
            finalHistoryPreviousHookObserved = true;
            File.WriteAllBytes(finalHistoryPreviousPath + ".candidate.previous",
                finalHistoryPreviousExternal);
        });
    SnapshotLoadResult finalHistoryPreviousLoad = finalHistoryPreviousPersistence.Load(storageIdentity);
    SnapshotKernel finalHistoryPreviousKernel = new SnapshotKernel();
    Assert(finalHistoryPreviousLoad.Success &&
           finalHistoryPreviousKernel.ReplaceFromDocument(finalHistoryPreviousLoad.Document).Success &&
           finalHistoryPreviousKernel.Store(group, baseline, 1).Success,
        "Final-history previous fixture should stage exact candidate B.");
    byte[] finalHistoryPreviousCandidate = null;
    SnapshotKernelTransaction finalHistoryPreviousTransaction = new SnapshotKernelTransaction(
        finalHistoryPreviousKernel, storageIdentity);
    SnapshotTransactionCommitResult finalHistoryPreviousCommit = finalHistoryPreviousTransaction.Commit(
        document =>
        {
            finalHistoryPreviousCandidate = System.Text.Encoding.UTF8.GetBytes(
                SnapshotDocumentCodec.Serialize(document));
            return finalHistoryPreviousPersistence.Save(document);
        });
    Assert(finalHistoryPreviousHookObserved,
        "The final-history previous injection must execute inside the check-to-Replace window.");
    Assert(!finalHistoryPreviousCommit.Success,
        "A replacement-instant candidate.previous update must not be overwritten and reported as success.");
    string finalHistoryPreviousRecovery = finalHistoryPreviousCommit.CandidateHistoryRecoveryPath;
    string finalHistoryPreviousReason = string.Empty;
    SnapshotStorageRollbackReconciliationResult finalHistoryPreviousReconciliation =
        SnapshotStorageRollbackReconciliationCoordinator.Observe(finalHistoryPreviousCommit,
            "candidate-history-previous", reason =>
            {
                finalHistoryPreviousReason = reason;
                return true;
            });
    Assert(!finalHistoryPreviousCommit.RollbackSucceeded &&
           finalHistoryPreviousCommit.ErrorCode == "candidate-history-drift" &&
           !string.IsNullOrWhiteSpace(finalHistoryPreviousRecovery) &&
           File.ReadAllBytes(finalHistoryPreviousRecovery).SequenceEqual(finalHistoryPreviousExternal) &&
           File.ReadAllBytes(finalHistoryPreviousCommit.CandidateRecoveryPath)
               .SequenceEqual(finalHistoryPreviousCandidate!) &&
           File.ReadAllBytes(finalHistoryPreviousPath + ".candidate.latest")
               .SequenceEqual(finalHistoryPreviousCandidate!) &&
           File.ReadAllBytes(finalHistoryPreviousPath + ".candidate.previous")
               .SequenceEqual(finalHistoryPreviousLatest) &&
           finalHistoryPreviousReconciliation.MarkerPersisted &&
           finalHistoryPreviousReason.Contains(
               "candidate-history=" + finalHistoryPreviousRecovery, StringComparison.Ordinal),
        "Atomic previous replacement must retain exact external C at a reported unique recovery path, keep B locatable, and propagate C into reconciliation reason.");

    Type dispositionInfoType = typeof(SnapshotPersistence).GetNestedType("FileDispositionInfo",
        System.Reflection.BindingFlags.NonPublic);
    Assert(dispositionInfoType != null &&
           System.Runtime.InteropServices.Marshal.SizeOf(dispositionInfoType) == 1,
        "FILE_DISPOSITION_INFO must match the Win32 one-byte BOOLEAN ABI exactly.");
    System.Reflection.MethodInfo verifiedDeleteMethod = typeof(SnapshotPersistence).GetMethod(
        "DeleteVerifiedFileByIdentity", System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.NonPublic);
    string missingDeleteTarget = Path.Combine(persistenceRoot, "missing-verified-delete-target.tmp");
    SnapshotVerifiedDeleteOutcome missingDeleteOutcome = (SnapshotVerifiedDeleteOutcome)
        verifiedDeleteMethod!.Invoke(null, new object[]
        {
            missingDeleteTarget, new string('0', 64), null
        })!;
    Assert(missingDeleteOutcome.Result == SnapshotVerifiedDeleteResult.OpenFailed &&
           missingDeleteOutcome.NativeError != 0 && !File.Exists(missingDeleteTarget),
        "A real CreateFileW failure must preserve its immediate nonzero Win32 error.");

    string constructorFailurePath = Path.Combine(persistenceRoot,
        "owned-handle-constructor-failure.tmp");
    string constructorFailureReplacementPath = constructorFailurePath + ".replacement";
    File.WriteAllText(constructorFailurePath, "locked-original");
    System.Reflection.MethodInfo constructorPathMethod = typeof(SnapshotPersistence).GetMethod(
        "ToExtendedWindowsPath", System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.NonPublic);
    System.Reflection.MethodInfo createFileMethod = typeof(SnapshotPersistence).GetMethod(
        "CreateFileW", System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.NonPublic);
    string constructorFailureExtendedPath = (string)constructorPathMethod!.Invoke(null,
        new object[] { constructorFailurePath })!;
    Microsoft.Win32.SafeHandles.SafeFileHandle constructorFailureHandle =
        (Microsoft.Win32.SafeHandles.SafeFileHandle)createFileMethod!.Invoke(null, new object[]
        {
            constructorFailureExtendedPath, 0x80010000u, 0x00000001u,
            IntPtr.Zero, 3u, 0x00000080u, IntPtr.Zero
        })!;
    Assert(!constructorFailureHandle.IsInvalid && !constructorFailureHandle.IsClosed,
        "Constructor-failure fixture must own a real DELETE/share-read kernel handle.");
    Microsoft.Win32.SafeHandles.SafeFileHandle observedConstructorFailureHandle = null;
    bool constructorFailureThrew = false;
    Func<Microsoft.Win32.SafeHandles.SafeFileHandle, FileStream> throwingStreamFactory = handle =>
    {
        observedConstructorFailureHandle = handle;
        throw new InvalidOperationException("file-stream-constructor-injected");
    };
    try
    {
        SnapshotPersistence.WithOwnedReadHandle(constructorFailureHandle,
            throwingStreamFactory, _ => SnapshotVerifiedDeleteResult.Deleted);
    }
    catch (InvalidOperationException exception) when (
        exception.Message == "file-stream-constructor-injected")
    {
        constructorFailureThrew = true;
    }
    bool constructorFailureClosedAfterThrow = constructorFailureHandle.IsClosed;
    bool constructorFailureReplaceSucceeded = false;
    bool constructorFailureReopenSucceeded = false;
    bool constructorFailureDeleteSucceeded = false;
    try
    {
        File.WriteAllText(constructorFailureReplacementPath, "replacement");
        File.Replace(constructorFailureReplacementPath, constructorFailurePath, null,
            ignoreMetadataErrors: true);
        constructorFailureReplaceSucceeded = true;
        using (FileStream reopened = new FileStream(constructorFailurePath, FileMode.Open,
                   FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            reopened.Position = reopened.Length;
            reopened.WriteByte(0x2A);
            reopened.Flush(true);
        }
        constructorFailureReopenSucceeded = true;
        File.Delete(constructorFailurePath);
        constructorFailureDeleteSucceeded = !File.Exists(constructorFailurePath);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
    finally
    {
        constructorFailureHandle.Dispose();
        if (File.Exists(constructorFailureReplacementPath))
            File.Delete(constructorFailureReplacementPath);
        if (File.Exists(constructorFailurePath)) File.Delete(constructorFailurePath);
    }
    Assert(constructorFailureThrew &&
           ReferenceEquals(observedConstructorFailureHandle, constructorFailureHandle) &&
           constructorFailureClosedAfterThrow && constructorFailureReplaceSucceeded &&
           constructorFailureReopenSucceeded && constructorFailureDeleteSucceeded,
        "A real DELETE/share-read kernel handle must release its path lock when FileStream construction throws.");

    System.Reflection.MethodInfo extendedPathMethod = typeof(SnapshotPersistence).GetMethod(
        "ToExtendedWindowsPath", System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.NonPublic);
    string normalizedDrivePath = (string)extendedPathMethod!.Invoke(null,
        new object[] { @"C:\snapshot-kernel\file.tmp" })!;
    string normalizedUncPath = (string)extendedPathMethod.Invoke(null,
        new object[] { @"\\server\share\snapshot-kernel\file.tmp" })!;
    string alreadyExtendedUnc = @"\\?\UNC\server\share\snapshot-kernel\file.tmp";
    string normalizedExtendedUnc = (string)extendedPathMethod.Invoke(null,
        new object[] { alreadyExtendedUnc })!;
    Assert(normalizedDrivePath == @"\\?\C:\snapshot-kernel\file.tmp" &&
           normalizedUncPath == @"\\?\UNC\server\share\snapshot-kernel\file.tmp" &&
           normalizedExtendedUnc == alreadyExtendedUnc,
        "Extended path normalization must preserve drive and UNC identity without double-prefixing.");

    string longPersistenceRoot = Path.Combine(persistenceRoot, new string('a', 80),
        new string('b', 80), new string('c', 80));
    string longSnapshotPath = SnapshotStoragePath.Resolve(longPersistenceRoot, storageIdentity);
    Assert(longSnapshotPath.Length > 260,
        "Extended-path cleanup fixture must exceed the traditional Windows MAX_PATH boundary.");
    bool longCleanupHookObserved = false;
    bool longCleanupReplaceRejected = false;
    string longCleanupExternalTemporary = string.Empty;
    byte[] longCleanupExternal = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(DocumentWithExact(storageIdentity, group,
            Snapshot(100, 24, 40, 4, 6, 4, 6), 14)));
    SnapshotPersistence longPathPersistence = new SnapshotPersistence(longSnapshotPath, null, stage =>
    {
        const string prefix = "after-candidate-history-cleanup-check-before-delete|";
        if (!stage.StartsWith(prefix, StringComparison.Ordinal)) return;
        longCleanupHookObserved = true;
        string recoveryPath = stage.Substring(prefix.Length);
        longCleanupExternalTemporary = recoveryPath + ".external-" +
            Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(longCleanupExternalTemporary, longCleanupExternal);
        try
        {
            File.Replace(longCleanupExternalTemporary, recoveryPath, null,
                ignoreMetadataErrors: true);
        }
        catch (IOException)
        {
            longCleanupReplaceRejected = true;
        }
        catch (UnauthorizedAccessException)
        {
            longCleanupReplaceRejected = true;
        }
    });
    Assert(longPathPersistence.Save(loadedA).Success &&
           longPathPersistence.Save(externalDocument).Success,
        "Extended-path fixture should establish S1/S0 before cleanup.");
    SnapshotSaveResult longPathCleanupSave = longPathPersistence.Save(loadedA);
    Assert(longPathCleanupSave.Success && longCleanupHookObserved &&
           longCleanupReplaceRejected &&
           File.ReadAllBytes(longCleanupExternalTemporary).SequenceEqual(longCleanupExternal),
        "Identity-bound cleanup must work end-to-end beyond MAX_PATH while rejecting external replacement and preserving C.");

    string historyCleanupPath = Path.Combine(persistenceRoot,
        "candidate-history-cleanup-identity", "snapshot.json");
    SnapshotPersistence historyCleanupBaseline = new SnapshotPersistence(historyCleanupPath);
    Assert(historyCleanupBaseline.Save(loadedA).Success &&
           historyCleanupBaseline.Save(externalDocument).Success,
        "History cleanup identity fixture should establish S1/S0.");
    byte[] historyCleanupLatest = File.ReadAllBytes(historyCleanupPath + ".candidate.latest");
    SnapshotDocument historyCleanupExternalDocument = DocumentWithExact(storageIdentity, group,
        Snapshot(100, 23, 41, 3, 7, 3, 7), 13);
    byte[] historyCleanupExternal = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(historyCleanupExternalDocument));
    bool historyCleanupHookObserved = false;
    bool historyCleanupExternalReplaceRejected = false;
    string historyCleanupRecoveryPath = string.Empty;
    string historyCleanupExternalTemporary = string.Empty;
    SnapshotPersistence historyCleanupPersistence = new SnapshotPersistence(
        historyCleanupPath, null, stage =>
        {
            const string prefix = "after-candidate-history-cleanup-check-before-delete|";
            if (!stage.StartsWith(prefix, StringComparison.Ordinal)) return;
            historyCleanupHookObserved = true;
            historyCleanupRecoveryPath = stage.Substring(prefix.Length);
            historyCleanupExternalTemporary = historyCleanupRecoveryPath + ".external-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(historyCleanupExternalTemporary, historyCleanupExternal);
            try
            {
                File.Replace(historyCleanupExternalTemporary, historyCleanupRecoveryPath, null,
                    ignoreMetadataErrors: true);
            }
            catch (IOException)
            {
                historyCleanupExternalReplaceRejected = true;
            }
            catch (UnauthorizedAccessException)
            {
                historyCleanupExternalReplaceRejected = true;
            }
        });
    Assert(historyCleanupPersistence.Load(storageIdentity).Success,
        "History cleanup identity fixture should bind the current primary.");
    SnapshotSaveResult historyCleanupSave = historyCleanupPersistence.Save(loadedA);
    byte[] historyCleanupCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(historyCleanupHookObserved,
        "The deterministic cleanup check-to-delete injection seam must execute.");
    Assert(historyCleanupSave.Success && historyCleanupExternalReplaceRejected &&
           File.ReadAllBytes(historyCleanupExternalTemporary).SequenceEqual(historyCleanupExternal) &&
           !File.Exists(historyCleanupRecoveryPath) &&
           string.IsNullOrWhiteSpace(historyCleanupSave.CandidateHistoryRecoveryPath) &&
           File.ReadAllBytes(historyCleanupPath + ".candidate.latest")
               .SequenceEqual(historyCleanupCandidate) &&
           File.ReadAllBytes(historyCleanupPath + ".candidate.previous")
               .SequenceEqual(historyCleanupLatest),
        "Cleanup must hold one verified file identity through delete marking, reject external replacement, retain C, and keep B at fixed latest.");

    string cleanupApiFailurePath = Path.Combine(persistenceRoot,
        "candidate-history-cleanup-api-failure", "snapshot.json");
    SnapshotPersistence cleanupApiFailureBaseline = new SnapshotPersistence(cleanupApiFailurePath);
    Assert(cleanupApiFailureBaseline.Save(loadedA).Success &&
           cleanupApiFailureBaseline.Save(externalDocument).Success,
        "Cleanup API failure fixture should establish S1/S0.");
    byte[] cleanupApiFailureHistory = File.ReadAllBytes(
        cleanupApiFailurePath + ".candidate.previous");
    SnapshotPersistence cleanupApiFailurePersistence = new SnapshotPersistence(
        cleanupApiFailurePath, null, null,
        (_, _, _) => new SnapshotVerifiedDeleteOutcome(
            SnapshotVerifiedDeleteResult.MarkFailed, nativeError: 5));
    Assert(cleanupApiFailurePersistence.Load(storageIdentity).Success,
        "Cleanup API failure fixture should bind its current primary.");
    SnapshotSaveResult cleanupApiFailureSave = cleanupApiFailurePersistence.Save(loadedA);
    byte[] cleanupApiFailureCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!cleanupApiFailureSave.Success && cleanupApiFailureSave.MayHaveCommitted &&
           cleanupApiFailureSave.ErrorCode == "candidate-history-cleanup-mark-failed:win32-5" &&
           File.ReadAllBytes(cleanupApiFailureSave.CandidateHistoryRecoveryPath)
               .SequenceEqual(cleanupApiFailureHistory) &&
           File.ReadAllBytes(cleanupApiFailureSave.CandidateRecoveryPath)
               .SequenceEqual(cleanupApiFailureCandidate),
        "A delete API failure must fail closed and retain both candidate B and history recovery.");

    string cleanupApiExceptionPath = Path.Combine(persistenceRoot,
        "candidate-history-cleanup-api-exception", "snapshot.json");
    SnapshotPersistence cleanupApiExceptionBaseline = new SnapshotPersistence(cleanupApiExceptionPath);
    Assert(cleanupApiExceptionBaseline.Save(loadedA).Success &&
           cleanupApiExceptionBaseline.Save(externalDocument).Success,
        "Cleanup API exception fixture should establish S1/S0.");
    byte[] cleanupApiExceptionHistory = File.ReadAllBytes(
        cleanupApiExceptionPath + ".candidate.previous");
    SnapshotPersistence cleanupApiExceptionPersistence = new SnapshotPersistence(
        cleanupApiExceptionPath, null, null,
        (_, _, _) => throw new InvalidOperationException("delete-api-injected"));
    Assert(cleanupApiExceptionPersistence.Load(storageIdentity).Success,
        "Cleanup API exception fixture should bind its current primary.");
    SnapshotSaveResult cleanupApiExceptionSave = cleanupApiExceptionPersistence.Save(loadedA);
    byte[] cleanupApiExceptionCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!cleanupApiExceptionSave.Success && cleanupApiExceptionSave.MayHaveCommitted &&
           cleanupApiExceptionSave.ErrorCode == "save-failed:InvalidOperationException" &&
           File.ReadAllBytes(cleanupApiExceptionSave.CandidateHistoryRecoveryPath)
               .SequenceEqual(cleanupApiExceptionHistory) &&
           File.ReadAllBytes(cleanupApiExceptionSave.CandidateRecoveryPath)
               .SequenceEqual(cleanupApiExceptionCandidate),
        "A delete API exception must fail closed and retain both candidate B and history recovery.");

    string cleanupPathOpenFailurePath = Path.Combine(persistenceRoot,
        "candidate-history-cleanup-path-open-failure", "snapshot.json");
    SnapshotPersistence cleanupPathOpenFailureBaseline = new SnapshotPersistence(
        cleanupPathOpenFailurePath);
    Assert(cleanupPathOpenFailureBaseline.Save(loadedA).Success &&
           cleanupPathOpenFailureBaseline.Save(externalDocument).Success,
        "Second-handle open failure fixture should establish S1/S0.");
    byte[] cleanupPathOpenFailureHistory = File.ReadAllBytes(
        cleanupPathOpenFailurePath + ".candidate.previous");
    SnapshotPersistence cleanupPathOpenFailurePersistence = new SnapshotPersistence(
        cleanupPathOpenFailurePath, null, null,
        (_, _, _) => new SnapshotVerifiedDeleteOutcome(
            SnapshotVerifiedDeleteResult.PathOpenFailed, nativeError: 206));
    Assert(cleanupPathOpenFailurePersistence.Load(storageIdentity).Success,
        "Second-handle open failure fixture should bind its current primary.");
    SnapshotSaveResult cleanupPathOpenFailureSave = cleanupPathOpenFailurePersistence.Save(loadedA);
    byte[] cleanupPathOpenFailureCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!cleanupPathOpenFailureSave.Success && cleanupPathOpenFailureSave.MayHaveCommitted &&
           cleanupPathOpenFailureSave.ErrorCode ==
               "candidate-history-cleanup-path-open-failed:win32-206" &&
           File.ReadAllBytes(cleanupPathOpenFailureSave.CandidateHistoryRecoveryPath)
               .SequenceEqual(cleanupPathOpenFailureHistory) &&
           File.ReadAllBytes(cleanupPathOpenFailureSave.CandidateRecoveryPath)
               .SequenceEqual(cleanupPathOpenFailureCandidate),
        "A second extended-handle open failure must fail closed with Win32 diagnostics and retain B/history recovery.");

    string cleanupUnsupportedPath = Path.Combine(persistenceRoot,
        "candidate-history-cleanup-unsupported", "snapshot.json");
    SnapshotPersistence cleanupUnsupportedBaseline = new SnapshotPersistence(cleanupUnsupportedPath);
    Assert(cleanupUnsupportedBaseline.Save(loadedA).Success &&
           cleanupUnsupportedBaseline.Save(externalDocument).Success,
        "Unsupported cleanup fixture should establish S1/S0.");
    byte[] cleanupUnsupportedHistory = File.ReadAllBytes(
        cleanupUnsupportedPath + ".candidate.previous");
    SnapshotPersistence cleanupUnsupportedPersistence = new SnapshotPersistence(
        cleanupUnsupportedPath, null, null,
        (_, _, _) => SnapshotVerifiedDeleteResult.Unsupported);
    Assert(cleanupUnsupportedPersistence.Load(storageIdentity).Success,
        "Unsupported cleanup fixture should bind its current primary.");
    SnapshotSaveResult cleanupUnsupportedSave = cleanupUnsupportedPersistence.Save(loadedA);
    Assert(!cleanupUnsupportedSave.Success && cleanupUnsupportedSave.MayHaveCommitted &&
           cleanupUnsupportedSave.ErrorCode == "candidate-history-cleanup-unsupported" &&
           File.ReadAllBytes(cleanupUnsupportedSave.CandidateHistoryRecoveryPath)
               .SequenceEqual(cleanupUnsupportedHistory),
        "A platform without an identity-bound delete primitive must fail closed and retain history evidence.");

    string historyDriftPath = Path.Combine(persistenceRoot, "candidate-history-drift", "snapshot.json");
    SnapshotPersistence historyDriftBaseline = new SnapshotPersistence(historyDriftPath);
    Assert(historyDriftBaseline.Save(loadedA).Success && historyDriftBaseline.Save(externalDocument).Success,
        "Candidate history drift fixture should establish S1/S0.");
    byte[] historyDriftPrevious = File.ReadAllBytes(historyDriftPath + ".candidate.previous");
    byte[] externalHistoryLatest = System.Text.Encoding.UTF8.GetBytes("external-candidate-history");
    SnapshotPersistence historyDriftPersistence = new SnapshotPersistence(historyDriftPath, null, stage =>
    {
        if (stage == "before-candidate-history-promotion")
            File.WriteAllBytes(historyDriftPath + ".candidate.latest", externalHistoryLatest);
    });
    Assert(historyDriftPersistence.Load(storageIdentity).Success,
        "Candidate history drift fixture should bind the current document.");
    SnapshotSaveResult historyDriftSave = historyDriftPersistence.Save(loadedA);
    byte[] historyDriftCandidate = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(loadedA));
    Assert(!historyDriftSave.Success && historyDriftSave.MayHaveCommitted &&
           historyDriftSave.ErrorCode == "candidate-history-drift" &&
           historyDriftSave.CandidateRecoveryPath.Contains(".candidate-recovery-", StringComparison.Ordinal) &&
           File.ReadAllBytes(historyDriftSave.CandidateRecoveryPath).SequenceEqual(historyDriftCandidate) &&
           File.ReadAllBytes(historyDriftPath + ".candidate.latest").SequenceEqual(externalHistoryLatest) &&
           File.ReadAllBytes(historyDriftPath + ".candidate.previous").SequenceEqual(historyDriftPrevious),
        "External candidate-history drift must fail closed without overwriting external latest or previous, while unique B remains recoverable.");

    string postPromotionMarkerPath = Path.Combine(persistenceRoot,
        "post-candidate-promotion-marker", "snapshot.json");
    SnapshotPersistence postPromotionMarkerBaseline = new SnapshotPersistence(postPromotionMarkerPath);
    Assert(postPromotionMarkerBaseline.Save(externalDocument).Success,
        "Post-promotion marker fixture should establish one prior successful candidate.");
    SnapshotPersistence postPromotionMarkerPersistence = new SnapshotPersistence(
        postPromotionMarkerPath, null, stage =>
        {
            if (stage == "after-candidate-history-promotion-before-success")
                File.WriteAllText(postPromotionMarkerPath + ".reconciliation-required", "post-promotion-marker");
        });
    Assert(postPromotionMarkerPersistence.Load(storageIdentity).Success,
        "Post-promotion marker fixture should bind its current document.");
    SnapshotSaveResult postPromotionMarkerSave = postPromotionMarkerPersistence.Save(loadedA);
    byte[] postPromotionCandidate = System.Text.Encoding.UTF8.GetBytes(
        SnapshotDocumentCodec.Serialize(loadedA));
    string postPromotionMarkerReason = string.Empty;
    SnapshotStorageRollbackReconciliationCoordinator.Observe(
        SnapshotTransactionCommitResult.Failed(postPromotionMarkerSave.ErrorCode,
            rollbackSucceeded: false,
            recoveryBackupPath: postPromotionMarkerSave.RecoveryBackupPath,
            candidateRecoveryPath: postPromotionMarkerSave.CandidateRecoveryPath),
        "post-candidate-promotion-marker", reason =>
        {
            postPromotionMarkerReason = reason;
            return true;
        });
    Assert(!postPromotionMarkerSave.Success && postPromotionMarkerSave.MayHaveCommitted &&
           postPromotionMarkerSave.ErrorCode == "reconciliation-required" &&
           postPromotionMarkerSave.CandidateRecoveryPath.EndsWith(".candidate.latest", StringComparison.Ordinal) &&
           File.ReadAllBytes(postPromotionMarkerSave.CandidateRecoveryPath).SequenceEqual(postPromotionCandidate) &&
           postPromotionMarkerReason.Contains(postPromotionMarkerSave.CandidateRecoveryPath,
               StringComparison.Ordinal),
        "A marker appearing after candidate promotion must fail closed and identify candidate B at fixed latest.");

    string successTailPath = Path.Combine(persistenceRoot, "success-linearization-tail", "snapshot.json");
    SnapshotPersistence successTailBaseline = new SnapshotPersistence(successTailPath);
    Assert(successTailBaseline.Save(loadedA).Success,
        "Success-tail fixture should establish primary A.");
    byte[] successTailA = File.ReadAllBytes(successTailPath);
    bool successTailHookObserved = false;
    bool recoveryExistedAtFinalHook = false;
    SnapshotPersistence successTailPersistence = new SnapshotPersistence(successTailPath, null, stage =>
    {
        if (stage != "after-candidate-history-promotion-before-success") return;
        successTailHookObserved = true;
        recoveryExistedAtFinalHook = File.Exists(successTailPath + ".bak");
    });
    Assert(successTailPersistence.Load(storageIdentity).Success,
        "Success-tail fixture should bind primary A.");
    SnapshotSaveResult successTailSave = successTailPersistence.Save(externalDocument);
    Assert(successTailSave.Success && successTailHookObserved && recoveryExistedAtFinalHook &&
           successTailSave.RecoveryBackupPath == successTailPath + ".bak" &&
           File.ReadAllBytes(successTailSave.RecoveryBackupPath).SequenceEqual(successTailA),
        "A clean success must report the recovery path resolved before its final verification point.");

    string persistenceSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src",
        "UnturnedSingleplayerCheatMenu", "InfiniteStorage", "SnapshotKernel", "SnapshotPersistence.cs"));
    int invalidHandleBranch = persistenceSource.IndexOf(
        "if (handle == null || handle.IsInvalid)", StringComparison.Ordinal);
    int immediateOpenErrorCapture = persistenceSource.IndexOf(
        "Marshal.GetLastWin32Error()", invalidHandleBranch, StringComparison.Ordinal);
    int invalidHandleDispose = persistenceSource.IndexOf(
        "handle?.Dispose()", invalidHandleBranch, StringComparison.Ordinal);
    Assert(invalidHandleBranch >= 0 && immediateOpenErrorCapture > invalidHandleBranch &&
           invalidHandleDispose > immediateOpenErrorCapture,
        "CreateFileW failure must capture GetLastWin32Error before SafeFileHandle disposal.");
    int extendedPathMappingOpen = persistenceSource.IndexOf(
        "SafeFileHandle pathHandle = CreateFileW(ToExtendedWindowsPath(path)",
        StringComparison.Ordinal);
    Assert(extendedPathMappingOpen >= 0,
        "Path-to-handle mapping verification must use the same extended-length Win32 path boundary.");
    int finalSuccessHook = persistenceSource.IndexOf(
        "_faultInjection?.Invoke(\"after-candidate-history-promotion-before-success\")",
        StringComparison.Ordinal);
    int successfulRecoveryCache = persistenceSource.LastIndexOf(
        "string successfulRecoveryPath =", finalSuccessHook, StringComparison.Ordinal);
    int finalMarkerCheck = persistenceSource.IndexOf("if (HasReconciliationLatch)", finalSuccessHook,
        StringComparison.Ordinal);
    int finalPrimaryCheck = persistenceSource.IndexOf("if (!File.Exists(_path)", finalMarkerCheck,
        StringComparison.Ordinal);
    int finalStateCommit = persistenceSource.IndexOf("_expectedPrimaryState =", finalSuccessHook,
        StringComparison.Ordinal);
    int successReturn = persistenceSource.IndexOf("return SnapshotSaveResult.Succeeded(", finalStateCommit,
        StringComparison.Ordinal);
    int successReturnEnd = persistenceSource.IndexOf(';', successReturn) + 1;
    Assert(successfulRecoveryCache >= 0 && successfulRecoveryCache < finalSuccessHook &&
           finalMarkerCheck > finalSuccessHook && finalPrimaryCheck > finalMarkerCheck &&
           finalStateCommit > finalPrimaryCheck && successReturn > finalStateCommit &&
           successReturnEnd > successReturn,
        "Success-tail structural gate must prove recovery-path resolution precedes the final hook and marker/SHA verification precedes state commit.");
    string postVerificationSuccessTail = persistenceSource.Substring(finalStateCommit,
        successReturnEnd - finalStateCommit);
    Assert(!postVerificationSuccessTail.Contains("File.", StringComparison.Ordinal) &&
           !postVerificationSuccessTail.Contains("ExistingRecoveryPath(", StringComparison.Ordinal) &&
           !postVerificationSuccessTail.Contains("ComputeSha256(", StringComparison.Ordinal) &&
           !postVerificationSuccessTail.Contains("HasReconciliationLatch", StringComparison.Ordinal) &&
           !postVerificationSuccessTail.Contains("_faultInjection", StringComparison.Ordinal) &&
           !postVerificationSuccessTail.Contains("_replaceOperation", StringComparison.Ordinal),
        "After final marker/SHA verification, the success tail must contain only in-memory state and result construction.");

    ItemGroupIdentity storeAllEvidenceGroup = new ItemGroupIdentity(65000,
        Guid.Parse("65000000-0000-0000-0000-000000000001"));
    ItemSnapshot storeAllEvidenceX = Snapshot(65000, 11, 77, 9, 8, 7, 6);
    SnapshotDocument storeAllA = DocumentWithExact(storageIdentity,
        storeAllEvidenceGroup, storeAllEvidenceX, 0);
    SnapshotDocument storeAllC = DocumentWithExact(storageIdentity,
        storeAllEvidenceGroup, storeAllEvidenceX, 2);
    string storeAllEvidencePath = Path.Combine(persistenceRoot, "store-all-abc", "snapshot.json");
    SnapshotPersistence storeAllBaseline = new SnapshotPersistence(storeAllEvidencePath);
    Assert(storeAllBaseline.Save(storeAllA).Success,
        "StoreAll A/B/C fixture should persist A with native X only.");
    byte[] storeAllCBytes = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(storeAllC));
    SnapshotPersistence storeAllPersistence = new SnapshotPersistence(storeAllEvidencePath, null, stage =>
    {
        if (stage == "during-candidate-history-promotion")
            File.WriteAllBytes(storeAllEvidencePath, storeAllCBytes);
    });
    SnapshotLoadResult storeAllLoad = storeAllPersistence.Load(storageIdentity);
    SnapshotKernel storeAllKernel = new SnapshotKernel();
    Assert(storeAllLoad.Success && storeAllKernel.ReplaceFromDocument(storeAllLoad.Document).Success,
        "StoreAll A/B/C fixture should load A.");
    SnapshotKernelTransaction storeAllTransaction = new SnapshotKernelTransaction(storeAllKernel, storageIdentity);
    Assert(storeAllTransaction.Store(storeAllEvidenceGroup, storeAllEvidenceX, 1).Success,
        "StoreAll A/B/C fixture should stage candidate B with one exact X.");
    byte[] storeAllCandidateBytes = null;
    SnapshotTransactionCommitResult storeAllCommit = storeAllTransaction.Commit(document =>
    {
        storeAllCandidateBytes = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return storeAllPersistence.Save(document);
    });
    Assert(!storeAllCommit.Success,
        "StoreAll must not report success when primary changes silently during candidate-history promotion.");
    int storeAllNativeX = 0;
    int storeAllNativeCompensationCalls = 0;
    SnapshotCrossBoundaryRecoveryResult storeAllAbcNativeRecovery =
        SnapshotCrossBoundaryRecoveryCoordinator.Execute(storeAllCommit.RollbackSucceeded,
            () => { storeAllNativeCompensationCalls++; storeAllNativeX = 1; return true; });
    string storeAllReason = string.Empty;
    SnapshotReconciliationPathLatch storeAllPathLatch = new SnapshotReconciliationPathLatch();
    SnapshotStorageRollbackReconciliationResult storeAllReconciliation =
        SnapshotStorageRollbackReconciliationCoordinator.Observe(storeAllCommit, "store-all-abc", reason =>
        {
            storeAllReason = reason;
            storeAllPathLatch.Latch(storeAllEvidencePath);
            return new SnapshotPersistence(storeAllEvidencePath).TryCreateReconciliationLatch(reason);
        });
    SnapshotDocument recoveredStoreAllA = ReadSnapshotDocument(storeAllCommit.RecoveryBackupPath);
    SnapshotDocument recoveredStoreAllB = ReadSnapshotDocument(storeAllCommit.CandidateRecoveryPath);
    SnapshotDocument observedStoreAllC = ReadSnapshotDocument(storeAllEvidencePath);
    Assert(!storeAllCommit.Success && !storeAllCommit.RollbackSucceeded &&
           storeAllCandidateBytes!.SequenceEqual(File.ReadAllBytes(storeAllCommit.CandidateRecoveryPath)) &&
           CountExact(recoveredStoreAllA, storeAllEvidenceGroup, storeAllEvidenceX) == 0 &&
           CountExact(recoveredStoreAllB, storeAllEvidenceGroup, storeAllEvidenceX) == 1 &&
           CountExact(observedStoreAllC, storeAllEvidenceGroup, storeAllEvidenceX) == 2 &&
           CountExact(recoveredStoreAllB, storeAllEvidenceGroup, storeAllEvidenceX) + storeAllNativeX == 1 &&
           new[] { FileSha256(storeAllCommit.RecoveryBackupPath),
               FileSha256(storeAllCommit.CandidateRecoveryPath), FileSha256(storeAllEvidencePath) }
               .Distinct(StringComparer.Ordinal).Count() == 3 &&
           storeAllNativeCompensationCalls == 0 && !storeAllAbcNativeRecovery.NativeRestoreAttempted &&
           storeAllReconciliation.MarkerPersisted &&
           storeAllReason.Contains(storeAllCommit.RecoveryBackupPath, StringComparison.Ordinal) &&
           storeAllReason.Contains(storeAllCommit.CandidateRecoveryPath, StringComparison.Ordinal) &&
           !new SnapshotPersistence(storeAllEvidencePath).Load(storageIdentity).Success &&
           new SnapshotReconciliationPathLatch().IsBlocked(storeAllEvidencePath, false),
        "StoreAll uncertain commit must preserve unambiguous A/B/C evidence, conserve exact X as candidate B plus frozen native = 1, and remain locked after restart.");

    ItemGroupIdentity withdrawEvidenceGroup = new ItemGroupIdentity(65001,
        Guid.Parse("65010000-0000-0000-0000-000000000001"));
    ItemSnapshot withdrawEvidenceX = Snapshot(65001, 23, 66, 1, 3, 3, 7);
    SnapshotDocument withdrawA = DocumentWithExact(storageIdentity,
        withdrawEvidenceGroup, withdrawEvidenceX, 1);
    SnapshotDocument withdrawC = DocumentWithExact(storageIdentity,
        withdrawEvidenceGroup, withdrawEvidenceX, 2);
    string withdrawEvidencePath = Path.Combine(persistenceRoot, "withdraw-abc", "snapshot.json");
    SnapshotPersistence withdrawBaseline = new SnapshotPersistence(withdrawEvidencePath);
    Assert(withdrawBaseline.Save(withdrawA).Success,
        "Withdraw A/B/C fixture should persist A with one stored X.");
    byte[] withdrawCBytes = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(withdrawC));
    SnapshotPersistence withdrawPersistence = new SnapshotPersistence(withdrawEvidencePath, null, stage =>
    {
        if (stage == "during-candidate-history-promotion")
            File.WriteAllBytes(withdrawEvidencePath, withdrawCBytes);
    });
    SnapshotLoadResult withdrawLoad = withdrawPersistence.Load(storageIdentity);
    SnapshotKernel withdrawKernel = new SnapshotKernel();
    Assert(withdrawLoad.Success && withdrawKernel.ReplaceFromDocument(withdrawLoad.Document).Success,
        "Withdraw A/B/C fixture should load A.");
    int withdrawNativeX = 1;
    SnapshotKernelTransaction withdrawTransaction = new SnapshotKernelTransaction(withdrawKernel, storageIdentity);
    Assert(withdrawTransaction.Withdraw(withdrawEvidenceGroup, withdrawEvidenceX, 1).Success,
        "Withdraw A/B/C fixture should stage candidate B with zero stored X and one native X.");
    byte[] withdrawCandidateBytes = null;
    SnapshotTransactionCommitResult withdrawCommit = withdrawTransaction.Commit(document =>
    {
        withdrawCandidateBytes = System.Text.Encoding.UTF8.GetBytes(SnapshotDocumentCodec.Serialize(document));
        return withdrawPersistence.Save(document);
    });
    Assert(!withdrawCommit.Success,
        "Withdraw must not report success when primary changes silently during candidate-history promotion.");
    int withdrawNativeCompensationCalls = 0;
    SnapshotCrossBoundaryRecoveryResult withdrawAbcNativeRecovery =
        SnapshotCrossBoundaryRecoveryCoordinator.Execute(withdrawCommit.RollbackSucceeded,
            () => { withdrawNativeCompensationCalls++; withdrawNativeX = 0; return true; });
    string withdrawReason = string.Empty;
    SnapshotReconciliationPathLatch withdrawPathLatch = new SnapshotReconciliationPathLatch();
    SnapshotStorageRollbackReconciliationResult withdrawReconciliation =
        SnapshotStorageRollbackReconciliationCoordinator.Observe(withdrawCommit, "withdraw-abc", reason =>
        {
            withdrawReason = reason;
            withdrawPathLatch.Latch(withdrawEvidencePath);
            return new SnapshotPersistence(withdrawEvidencePath).TryCreateReconciliationLatch(reason);
        });
    SnapshotDocument recoveredWithdrawA = ReadSnapshotDocument(withdrawCommit.RecoveryBackupPath);
    SnapshotDocument recoveredWithdrawB = ReadSnapshotDocument(withdrawCommit.CandidateRecoveryPath);
    SnapshotDocument observedWithdrawC = ReadSnapshotDocument(withdrawEvidencePath);
    Assert(!withdrawCommit.Success && !withdrawCommit.RollbackSucceeded &&
           withdrawCandidateBytes!.SequenceEqual(File.ReadAllBytes(withdrawCommit.CandidateRecoveryPath)) &&
           CountExact(recoveredWithdrawA, withdrawEvidenceGroup, withdrawEvidenceX) == 1 &&
           CountExact(recoveredWithdrawB, withdrawEvidenceGroup, withdrawEvidenceX) == 0 &&
           CountExact(observedWithdrawC, withdrawEvidenceGroup, withdrawEvidenceX) == 2 &&
           CountExact(recoveredWithdrawB, withdrawEvidenceGroup, withdrawEvidenceX) + withdrawNativeX == 1 &&
           new[] { FileSha256(withdrawCommit.RecoveryBackupPath),
               FileSha256(withdrawCommit.CandidateRecoveryPath), FileSha256(withdrawEvidencePath) }
               .Distinct(StringComparer.Ordinal).Count() == 3 &&
           withdrawNativeCompensationCalls == 0 && !withdrawAbcNativeRecovery.NativeRestoreAttempted &&
           withdrawReconciliation.MarkerPersisted &&
           withdrawReason.Contains(withdrawCommit.RecoveryBackupPath, StringComparison.Ordinal) &&
           withdrawReason.Contains(withdrawCommit.CandidateRecoveryPath, StringComparison.Ordinal) &&
           !new SnapshotPersistence(withdrawEvidencePath).Load(storageIdentity).Success &&
           new SnapshotReconciliationPathLatch().IsBlocked(withdrawEvidencePath, false),
        "Withdraw uncertain commit must preserve unambiguous A/B/C evidence, conserve exact X as candidate B plus frozen native = 1, and remain locked after restart.");

    string loadMarkerWindowPath = Path.Combine(persistenceRoot, "load-marker-window", "snapshot.json");
    SnapshotPersistence loadMarkerBaseline = new SnapshotPersistence(loadMarkerWindowPath);
    Assert(loadMarkerBaseline.Save(loadedA).Success,
        "Load-marker fixture should establish an isolated valid primary.");
    byte[] loadMarkerPrimary = File.ReadAllBytes(loadMarkerWindowPath);
    SnapshotPersistence loadMarkerWindow = new SnapshotPersistence(loadMarkerWindowPath, null, stage =>
    {
        if (stage == "before-load-accept") File.WriteAllBytes(
            loadMarkerWindowPath + ".reconciliation-required", externalMarkerBytes);
    });
    SnapshotLoadResult loadMarkerResult = loadMarkerWindow.Load(storageIdentity);
    Assert(!loadMarkerResult.Success && loadMarkerResult.ErrorCode == "reconciliation-required" &&
           File.ReadAllBytes(loadMarkerWindowPath).SequenceEqual(loadMarkerPrimary) &&
           File.ReadAllBytes(loadMarkerWindowPath + ".reconciliation-required").SequenceEqual(externalMarkerBytes),
        "A reconciliation marker created during Load must block final activation without altering source bytes.");

    string markerBlockedParent = Path.Combine(persistenceRoot, "marker-parent-is-file");
    File.WriteAllText(markerBlockedParent, "not-a-directory");
    SnapshotPersistence markerFailurePersistence = new SnapshotPersistence(
        Path.Combine(markerBlockedParent, "snapshot.json"));
    Assert(!markerFailurePersistence.TryCreateReconciliationLatch("injected-marker-write-failure") &&
           !markerFailurePersistence.HasReconciliationLatch,
        "A real marker I/O failure must return false without pretending the reconciliation latch was persisted.");

    File.WriteAllText(snapshotPath, SnapshotDocumentCodec.Serialize(
        fallbackKernel.ExportDocument(storageIdentity)));

    JObject oldSchema = (JObject)JObject.Parse(File.ReadAllText(snapshotPath)).DeepClone();
    oldSchema["SchemaVersion"] = 2;
    File.WriteAllText(snapshotPath, oldSchema.ToString());
    File.WriteAllText(snapshotPath + ".bak", oldSchema.ToString());
    SnapshotLoadResult oldSchemaLoad = new SnapshotPersistence(snapshotPath).Load(storageIdentity);
    Assert(!oldSchemaLoad.Success,
        "Legacy or future schema values in the new path must be rejected rather than converted.");

    string commentBaseJson = SnapshotDocumentCodec.Serialize(fallbackKernel.ExportDocument(storageIdentity));
    string[] commentedJson =
    {
        "// root comment\n" + commentBaseJson,
        commentBaseJson.Insert(commentBaseJson.IndexOf('{') + 1, "\n/* interior comment */"),
        commentBaseJson + "\n// trailing comment"
    };
    foreach (string commented in commentedJson)
    {
        Assert(!SnapshotDocumentValidator.TryParse(commented, out _, out string commentError) &&
               commentError == "snapshot-json-comments-not-allowed",
            "Snapshot schema 1 is strict JSON; comments must never be silently ignored.");
    }

    JObject missingRequired = (JObject)oldSchema.DeepClone();
    missingRequired["SchemaVersion"] = 1;
    missingRequired.Remove("Groups");
    File.WriteAllText(snapshotPath, missingRequired.ToString());
    File.WriteAllText(snapshotPath + ".bak", missingRequired.ToString());
    Assert(!new SnapshotPersistence(snapshotPath).Load(storageIdentity).Success,
        "A current-schema document missing a required root array must fail closed.");

    JObject unknownRoot = (JObject)oldSchema.DeepClone();
    unknownRoot["SchemaVersion"] = 1;
    unknownRoot["FutureRoot"] = "must-not-be-silently-erased";
    File.WriteAllText(snapshotPath, unknownRoot.ToString());
    File.WriteAllText(snapshotPath + ".bak", unknownRoot.ToString());
    Assert(!new SnapshotPersistence(snapshotPath).Load(storageIdentity).Success,
        "Unknown schema-1 root fields must fail closed rather than be erased by a typed round-trip.");

    JObject nullState = (JObject)oldSchema.DeepClone();
    nullState["SchemaVersion"] = 1;
    nullState["Groups"]![0]!["Buckets"]![0]!["State"] = JValue.CreateNull();
    File.WriteAllText(snapshotPath, nullState.ToString());
    File.WriteAllText(snapshotPath + ".bak", nullState.ToString());
    Assert(!new SnapshotPersistence(snapshotPath).Load(storageIdentity).Success,
        "A null state is not an empty native state and must be rejected.");

    Assert(File.ReadAllBytes(legacySentinelPath).SequenceEqual(legacySentinelBytes) &&
           File.GetLastWriteTimeUtc(legacySentinelPath) == legacySentinelWriteTime,
        "All new persistence operations must leave the legacy-tree sentinel untouched.");
}
finally
{
    Directory.Delete(persistenceRoot, true);
}

ManualResetEventSlim backgroundStarted = new ManualResetEventSlim(false);
ManualResetEventSlim releaseBackground = new ManualResetEventSlim(false);
int callerThreadId = Environment.CurrentManagedThreadId;
int persistenceThreadId = callerThreadId;
SnapshotStorageIdentity backgroundIdentity = new SnapshotStorageIdentity(
    "background-world", "background-player");
SnapshotDocument backgroundDocument = SnapshotDocument.Empty(backgroundIdentity);
SnapshotBackgroundPersistence backgroundPersistence = SnapshotBackgroundPersistence.Start(
    backgroundDocument, document =>
    {
        persistenceThreadId = Environment.CurrentManagedThreadId;
        backgroundStarted.Set();
        releaseBackground.Wait();
        return ReferenceEquals(document, backgroundDocument)
            ? SnapshotSaveResult.Succeeded()
            : SnapshotSaveResult.Failed("background-document-changed");
    });
Assert(backgroundStarted.Wait(TimeSpan.FromSeconds(5)) &&
       persistenceThreadId != callerThreadId && !backgroundPersistence.IsCompleted,
    "Background Snapshot persistence must start off the caller thread and return before the gated save completes.");
Assert(!backgroundPersistence.TryGetResult(out _),
    "A gated background save must not report a result before persistence finishes.");
releaseBackground.Set();
SnapshotSaveResult backgroundResult = backgroundPersistence.WaitForResult();
Assert(backgroundResult.Success && backgroundPersistence.IsCompleted &&
       backgroundPersistence.TryGetResult(out SnapshotSaveResult repeatedBackgroundResult) &&
       ReferenceEquals(backgroundResult, repeatedBackgroundResult),
    "Background Snapshot persistence must publish one stable successful result after completion.");

SnapshotBackgroundPersistence throwingBackground = SnapshotBackgroundPersistence.Start(
    backgroundDocument, _ => throw new IOException("injected-background-save-failure"));
SnapshotSaveResult throwingBackgroundResult = throwingBackground.WaitForResult();
Assert(!throwingBackgroundResult.Success && throwingBackgroundResult.MayHaveCommitted &&
       throwingBackgroundResult.ErrorCode == "background-persistence-threw:IOException",
    "An unexpected background persistence exception must fail closed as a possibly committed write.");

int backgroundApplyCalls = 0;
SnapshotBackgroundStoreCompletionResult safeBackgroundCompletion =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Failed("background-safe-failure"), sessionCurrent: true,
        () => { backgroundApplyCalls++; return SnapshotMutationResult.Succeeded(); });
SnapshotTransactionCommitResult safeBackgroundFailure = safeBackgroundCompletion.Transaction;
Assert(safeBackgroundCompletion.Disposition == SnapshotBackgroundStoreDisposition.SafeFailureCurrentSession &&
       !safeBackgroundFailure.Success && safeBackgroundFailure.RollbackSucceeded && backgroundApplyCalls == 0,
    "A definitely uncommitted background save must keep memory unchanged and permit native fallback.");
SnapshotBackgroundStoreCompletionResult uncertainBackgroundCompletion =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Failed("background-uncertain", mayHaveCommitted: true,
            recoveryBackupPath: "A.json", candidateRecoveryPath: "B.json",
            candidateHistoryRecoveryPath: "C.json"),
        sessionCurrent: true,
        () => { backgroundApplyCalls++; return SnapshotMutationResult.Succeeded(); });
SnapshotTransactionCommitResult uncertainBackgroundFailure = uncertainBackgroundCompletion.Transaction;
Assert(!uncertainBackgroundFailure.Success && !uncertainBackgroundFailure.RollbackSucceeded &&
       uncertainBackgroundCompletion.Disposition == SnapshotBackgroundStoreDisposition.UncertainCurrentSession &&
       uncertainBackgroundFailure.RecoveryBackupPath == "A.json" &&
       uncertainBackgroundFailure.CandidateRecoveryPath == "B.json" &&
       uncertainBackgroundFailure.CandidateHistoryRecoveryPath == "C.json" &&
       backgroundApplyCalls == 0,
    "An uncertain background save must preserve A/B/C evidence and forbid in-memory apply/native fallback.");
SnapshotBackgroundStoreCompletionResult successfulBackgroundCompletion =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(SnapshotSaveResult.Succeeded(), sessionCurrent: true,
        () => { backgroundApplyCalls++; return SnapshotMutationResult.Succeeded(); });
SnapshotTransactionCommitResult successfulBackgroundApply = successfulBackgroundCompletion.Transaction;
Assert(successfulBackgroundCompletion.Disposition == SnapshotBackgroundStoreDisposition.CommittedCurrentSession &&
       successfulBackgroundCompletion.CanFinalizeWorldDrop && !successfulBackgroundCompletion.CanUseNativeFallback &&
       successfulBackgroundApply.Success && backgroundApplyCalls == 1,
    "A durable background save must apply its exact mutation to memory once.");
SnapshotBackgroundStoreCompletionResult failedBackgroundCompletion =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Succeeded(recoveryBackupPath: "persisted-A.json"),
        sessionCurrent: true,
        () => SnapshotMutationResult.Failed("injected-memory-apply-failure"));
SnapshotTransactionCommitResult failedBackgroundApply = failedBackgroundCompletion.Transaction;
Assert(!failedBackgroundApply.Success && !failedBackgroundApply.RollbackSucceeded &&
       failedBackgroundCompletion.Disposition == SnapshotBackgroundStoreDisposition.UncertainCurrentSession &&
       failedBackgroundApply.ErrorCode ==
           "background-memory-apply-failed:injected-memory-apply-failure" &&
       failedBackgroundApply.RecoveryBackupPath == "persisted-A.json",
    "If durable state cannot be mirrored into memory, mutations must lock with recovery evidence.");

int detachedApplyCalls = 0;
SnapshotBackgroundStoreCompletionResult detachedSafeFailure =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Failed("detached-before-commit"), sessionCurrent: false,
        () => { detachedApplyCalls++; return SnapshotMutationResult.Succeeded(); });
Assert(detachedSafeFailure.Disposition == SnapshotBackgroundStoreDisposition.DetachedBeforeCommit &&
       detachedSafeFailure.Transaction.RollbackSucceeded && detachedSafeFailure.SessionDetached &&
       !detachedSafeFailure.CanFinalizeWorldDrop && !detachedSafeFailure.CanUseNativeFallback &&
       !detachedSafeFailure.RequiresReconciliation && detachedApplyCalls == 0,
    "A detached definitely-uncommitted save must be abandoned without touching either session.");
SnapshotBackgroundStoreCompletionResult detachedSuccess =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Succeeded(recoveryBackupPath: "old-session-A.json"), sessionCurrent: false,
        () => { detachedApplyCalls++; return SnapshotMutationResult.Succeeded(); });
Assert(detachedSuccess.Disposition == SnapshotBackgroundStoreDisposition.DetachedCommittedOrUncertain &&
       !detachedSuccess.Transaction.Success && !detachedSuccess.Transaction.RollbackSucceeded &&
       detachedSuccess.RequiresReconciliation && detachedApplyCalls == 0 &&
       detachedSuccess.Transaction.RecoveryBackupPath == "old-session-A.json",
    "A detached successful save must never apply to memory and must require old-session reconciliation.");
SnapshotBackgroundStoreCompletionResult detachedUncertain =
    SnapshotBackgroundStoreCompletionPolicy.Resolve(
        SnapshotSaveResult.Failed("detached-uncertain", mayHaveCommitted: true,
            candidateRecoveryPath: "old-session-B.json"), sessionCurrent: false,
        () => { detachedApplyCalls++; return SnapshotMutationResult.Succeeded(); });
Assert(detachedUncertain.Disposition == SnapshotBackgroundStoreDisposition.DetachedCommittedOrUncertain &&
       detachedUncertain.RequiresReconciliation && !detachedUncertain.Transaction.RollbackSucceeded &&
       detachedUncertain.Transaction.CandidateRecoveryPath == "old-session-B.json" && detachedApplyCalls == 0,
    "A detached uncertain save must preserve old-session evidence and forbid all current-session effects.");
AssertBackgroundRoute(successfulBackgroundCompletion, "committed");
AssertBackgroundRoute(safeBackgroundCompletion, "safe");
AssertBackgroundRoute(uncertainBackgroundCompletion, "uncertain");
AssertBackgroundRoute(failedBackgroundCompletion, "uncertain");
AssertBackgroundRoute(detachedSafeFailure, "detached");
AssertBackgroundRoute(detachedSuccess, "detached");
AssertBackgroundRoute(detachedUncertain, "detached");

SnapshotDocument directStage = SnapshotDocument.Empty(backgroundIdentity);
ItemGroupIdentity directStageIdentity = new ItemGroupIdentity(700,
    Guid.Parse("07000000-0000-0000-0000-000000000001"));
ItemSnapshot directStageSnapshot = Snapshot(700, 17, 63, 4, 2, 9);
Assert(SnapshotDocumentMutation.TryStoreExact(directStage, directStageIdentity,
        directStageSnapshot, 1).Success &&
       CountExact(directStage, directStageIdentity, directStageSnapshot) == 1,
    "Direct background staging must add one exact snapshot to an isolated document.");
Assert(SnapshotDocumentMutation.TryStoreExact(directStage, directStageIdentity,
        directStageSnapshot, 2).Success &&
       CountExact(directStage, directStageIdentity, directStageSnapshot) == 3,
    "Direct background staging must merge only an exact amount/quality/state bucket.");
string directStageBeforeConflict = SnapshotDocumentCodec.Serialize(directStage);
Assert(!SnapshotDocumentMutation.TryStoreExact(directStage,
        new ItemGroupIdentity(700, Guid.Parse("07000000-0000-0000-0000-000000000002")),
        directStageSnapshot, 1).Success &&
       SnapshotDocumentCodec.Serialize(directStage) == directStageBeforeConflict,
    "Direct background staging must reject an AssetId/GUID conflict without mutating the document.");
directStage.Groups.Single().Buckets.Single().Count = long.MaxValue;
string directStageBeforeOverflow = SnapshotDocumentCodec.Serialize(directStage);
Assert(!SnapshotDocumentMutation.TryStoreExact(directStage, directStageIdentity,
        directStageSnapshot, 1).Success &&
       SnapshotDocumentCodec.Serialize(directStage) == directStageBeforeOverflow,
    "Direct background staging must reject count overflow without mutating the document.");

Console.WriteLine("Snapshot kernel core smoke checks passed.");

internal sealed class ConstantSnapshotHashProvider : ISnapshotHashProvider
{
    private readonly string _hash;

    internal ConstantSnapshotHashProvider(string hash) => _hash = hash;

    public string ComputeHash(byte[] encodedSnapshot) => _hash;
}
