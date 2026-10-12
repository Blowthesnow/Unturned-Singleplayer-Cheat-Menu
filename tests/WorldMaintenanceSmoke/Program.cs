using UnturnedSingleplayerCheatMenu.Models;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Check(WorldMaintenancePolicy.NormalizeRadius(1f) == 100f &&
      WorldMaintenancePolicy.NormalizeRadius(149f) == 100f &&
      WorldMaintenancePolicy.NormalizeRadius(150f) == 250f &&
      WorldMaintenancePolicy.NormalizeRadius(374f) == 250f &&
      WorldMaintenancePolicy.NormalizeRadius(375f) == 500f &&
      WorldMaintenancePolicy.NormalizeRadius(float.NaN) == 250f,
    "Maintenance radius must normalize to the 100/250/500m choices with a 250m fallback.");

Check(WorldMaintenancePolicy.IsWithinHorizontalRadius(60f, 80f, 100f) &&
      !WorldMaintenancePolicy.IsWithinHorizontalRadius(60.01f, 80f, 100f) &&
      WorldMaintenancePolicy.IsWithinHorizontalRadius(0f, 500f, 500f),
    "Nearby maintenance must include the horizontal radius boundary and reject points beyond it.");

Check(WorldMaintenancePolicy.ClassifyResource(exists: false, isDead: true, inScope: true) == WorldMaintenanceCandidateDecision.Invalid &&
      WorldMaintenancePolicy.ClassifyResource(exists: true, isDead: false, inScope: true) == WorldMaintenanceCandidateDecision.Invalid &&
      WorldMaintenancePolicy.ClassifyResource(exists: true, isDead: true, inScope: false) == WorldMaintenanceCandidateDecision.OutOfScope &&
      WorldMaintenancePolicy.ClassifyResource(exists: true, isDead: true, inScope: true) == WorldMaintenanceCandidateDecision.Include,
    "Resource preview must exclude null, alive, and out-of-range spawnpoints.");

Check(WorldMaintenancePolicy.ClassifyZombie(exists: false, isDead: false, inScope: true, isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Invalid &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: true, inScope: true, isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Invalid &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: false, inScope: false, isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.OutOfScope &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: false, inScope: true, isBoss: true, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Protected &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: false, inScope: true, isBoss: false, isMega: true, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Protected &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: false, inScope: true, isBoss: false, isMega: false, hasBeacon: true, includeSpecial: false) == WorldMaintenanceCandidateDecision.Protected &&
      WorldMaintenancePolicy.ClassifyZombie(exists: true, isDead: false, inScope: true, isBoss: true, isMega: true, hasBeacon: true, includeSpecial: true) == WorldMaintenanceCandidateDecision.Include,
    "Zombie preview must exclude dead/null/out-of-range targets and protect Boss, Mega, and beacon zombies by default.");

Check(WorldMaintenancePolicy.ResolveFrameBudget(WorldMaintenanceActionKind.RefreshNaturalResources, WorldMaintenanceScopeKind.WholeMap) == 64 &&
      WorldMaintenancePolicy.ResolveFrameBudget(WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.LoadedWorld) == 16 &&
      WorldMaintenancePolicy.ResolveFrameBudget(WorldMaintenanceActionKind.ClearGroundItems, WorldMaintenanceScopeKind.WholeMap) == 8 &&
      WorldMaintenancePolicy.ResolveFrameBudget(WorldMaintenanceActionKind.RestockGroundItems, WorldMaintenanceScopeKind.WholeMap) == 8 &&
      WorldMaintenancePolicy.ResolveFrameBudget(WorldMaintenanceActionKind.GenerateZombies, WorldMaintenanceScopeKind.LoadedWorld) == 16,
    "Per-frame budgets must remain 64 resources, 16 zombies, and 8 item/restock units.");

Check(WorldMaintenancePolicy.ClassifyZombieGeneration(exists: true, isDead: false, inScope: true,
          isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Invalid &&
      WorldMaintenancePolicy.ClassifyZombieGeneration(exists: true, isDead: true, inScope: false,
          isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.OutOfScope &&
      WorldMaintenancePolicy.ClassifyZombieGeneration(exists: true, isDead: true, inScope: true,
          isBoss: true, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Protected &&
      WorldMaintenancePolicy.ClassifyZombieGeneration(exists: true, isDead: true, inScope: true,
          isBoss: false, isMega: false, hasBeacon: false, includeSpecial: false) == WorldMaintenanceCandidateDecision.Include,
    "Zombie generation must use dead loaded slots, preserve special protection, and reject out-of-scope slots.");

WorldMaintenanceResult partial = new WorldMaintenanceResult(
    WorldMaintenanceActionKind.ClearZombies,
    WorldMaintenanceScopeKind.LoadedWorld,
    WorldMaintenanceRunState.Stopped,
    succeededCount: 5,
    protectedCount: 2,
    blockedCount: 0,
    invalidCount: 0,
    failedCount: 0,
    unprocessedCount: 3,
    originalCandidateCount: 10,
    message: "stopped");
Check(partial.SucceededCount == 5 && partial.RunState == WorldMaintenanceRunState.Stopped,
    "Stopping must preserve already completed results rather than rolling them back.");
Check(WorldMaintenancePolicy.IsResultConserved(partial),
    "Completed, protected, blocked, invalid, and failed counts must conserve the original candidate total.");

WorldMaintenancePreview preview = new WorldMaintenancePreview(
    WorldMaintenanceActionKind.ClearGroundItems,
    WorldMaintenanceScopeKind.WholeMap,
    radiusMeters: 250f,
    candidateCount: 8,
    protectedCount: 0,
    blockedCount: 0,
    snapshotVersion: 12,
    sceneVersion: 4,
    includeSpecialZombies: false);
Check(WorldMaintenancePolicy.IsPreviewCurrent(preview, snapshotVersion: 12, sceneVersion: 4) &&
      !WorldMaintenancePolicy.IsPreviewCurrent(preview, snapshotVersion: 13, sceneVersion: 4) &&
      !WorldMaintenancePolicy.IsPreviewCurrent(preview, snapshotVersion: 12, sceneVersion: 5),
    "A world or scene version change must invalidate an old preview.");

WorldMaintenancePreview resourceBatchPreview = new(
    WorldMaintenanceActionKind.RefreshNaturalResources, WorldMaintenanceScopeKind.WholeMap,
    250f, 70, 0, 0, snapshotVersion: 20, sceneVersion: 8, includeSpecialZombies: false);
List<FakeCandidate> resourceCandidates = Enumerable.Range(0, 70).Select(index => new FakeCandidate(index)).ToList();
int resourceActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> resourceRunner = new(
    candidate => candidate.Live,
    candidate => candidate.Decision,
    candidate => resourceActionCalls++);
Check(resourceRunner.TryStart(resourceBatchPreview, resourceCandidates, currentSnapshotVersion: 20, currentSceneVersion: 8),
    "A current non-empty resource preview should start exactly one batch.");
Check(!resourceRunner.TryStart(resourceBatchPreview, new[] { new FakeCandidate(999) }, 20, 8) &&
      resourceRunner.TotalCount == 70 && resourceRunner.ProcessedCount == 0,
    "A second start while Running must fail closed without replacing the active queue.");
resourceRunner.Pump(currentSnapshotVersion: 20, currentSceneVersion: 8);
Check(resourceActionCalls == 64 && resourceRunner.State == WorldMaintenanceRunState.Running,
    "One resource Pump must execute at most 64 candidates.");
resourceRunner.Pump(currentSnapshotVersion: 20, currentSceneVersion: 8);
Check(resourceActionCalls == 70 && resourceRunner.State == WorldMaintenanceRunState.Completed,
    "A second resource Pump should complete only the remaining candidates.");

WorldMaintenancePreview zombieBatchPreview = new(
    WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.LoadedWorld,
    250f, 17, 0, 0, snapshotVersion: 21, sceneVersion: 8, includeSpecialZombies: false);
int zombieActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> zombieRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => zombieActionCalls++);
Check(zombieRunner.TryStart(zombieBatchPreview, Enumerable.Range(0, 17).Select(index => new FakeCandidate(index)).ToList(), 21, 8),
    "Zombie batch failed to start.");
zombieRunner.Pump(21, 8);
Check(zombieActionCalls == 16 && zombieRunner.State == WorldMaintenanceRunState.Running,
    "One zombie Pump must execute at most 16 candidates.");

WorldMaintenancePreview itemBatchPreview = new(
    WorldMaintenanceActionKind.ClearGroundItems, WorldMaintenanceScopeKind.WholeMap,
    250f, 9, 0, 0, snapshotVersion: 22, sceneVersion: 8, includeSpecialZombies: false);
int itemRegionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> itemRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => itemRegionCalls++);
Check(itemRunner.TryStart(itemBatchPreview, Enumerable.Range(0, 9).Select(index => new FakeCandidate(index)).ToList(), 22, 8),
    "Item-region batch failed to start.");
itemRunner.Pump(22, 8);
Check(itemRegionCalls == 8 && itemRunner.State == WorldMaintenanceRunState.Running,
    "One whole-map item Pump must execute at most 8 regions.");
int restockActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> restockRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => restockActionCalls++);
WorldMaintenancePreview restockPreview = new(
    WorldMaintenanceActionKind.RestockGroundItems, WorldMaintenanceScopeKind.WholeMap,
    250f, 9, 0, 0, 23, 8, false);
Check(restockRunner.TryStart(restockPreview, Enumerable.Range(0, 9).Select(index => new FakeCandidate(index)).ToList(), 23, 8),
    "Restock batch failed to start.");
restockRunner.Pump(23, 8);
Check(restockActionCalls == 8 && restockRunner.State == WorldMaintenanceRunState.Running,
    "One whole-map restock Pump must execute at most 8 item spawnpoints.");
int generationActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> generationRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => generationActionCalls++);
WorldMaintenancePreview generationPreview = new(
    WorldMaintenanceActionKind.GenerateZombies, WorldMaintenanceScopeKind.LoadedWorld,
    250f, 17, 0, 0, 24, 8, false);
Check(generationRunner.TryStart(generationPreview, Enumerable.Range(0, 17).Select(index => new FakeCandidate(index)).ToList(), 24, 8),
    "Generation batch failed to start.");
generationRunner.Pump(24, 8);
Check(generationActionCalls == 16 && generationRunner.State == WorldMaintenanceRunState.Running,
    "One loaded-zombie generation Pump must execute at most 16 slots.");

List<FakeCandidate> weightedItemRegions = new()
{
    new FakeCandidate(1) { Weight = 3 },
    new FakeCandidate(2) { Weight = 2, Live = false },
    new FakeCandidate(3) { Weight = 4 }
};
WorldMaintenanceBatchRunner<FakeCandidate> weightedItemRunner = new(
    candidate => candidate.Live,
    candidate => candidate.Decision,
    candidate => candidate.Weight,
    candidate => candidate.Weight);
Check(weightedItemRunner.TryStart(itemBatchPreview, weightedItemRegions, 22, 8),
    "Weighted item-region batch failed to start.");
weightedItemRunner.Pump(22, 8);
Check(weightedItemRunner.Result.OriginalCandidateCount == 9 &&
      weightedItemRunner.Result.SucceededCount == 7 && weightedItemRunner.Result.InvalidCount == 2 &&
      WorldMaintenancePolicy.IsResultConserved(weightedItemRunner.Result),
    "Item-region execution must conserve item counts rather than reporting only the number of regions.");

int stoppedActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> stoppedRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => stoppedActionCalls++);
Check(stoppedRunner.TryStart(resourceBatchPreview, resourceCandidates, 20, 8), "Stop test batch failed to start.");
stoppedRunner.Pump(20, 8);
stoppedRunner.Stop("operator stop");
stoppedRunner.Pump(20, 8);
Check(stoppedActionCalls == 64 && stoppedRunner.State == WorldMaintenanceRunState.Stopped &&
      stoppedRunner.Result.SucceededCount == 64 && stoppedRunner.Result.InvalidCount == 0 &&
      stoppedRunner.Result.UnprocessedCount == 6 && WorldMaintenancePolicy.IsResultConserved(stoppedRunner.Result),
    "Stop must preserve completed work, account for the remainder, and prevent any later action call.");

WorldMaintenanceBatchRunner<FakeCandidate> versionRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => { });
Check(!versionRunner.TryStart(resourceBatchPreview, resourceCandidates, currentSnapshotVersion: 21, currentSceneVersion: 8),
    "A stale snapshot preview must be rejected before Running.");
Check(versionRunner.TryStart(resourceBatchPreview, resourceCandidates, 20, 8), "Current preview should start after stale rejection.");
versionRunner.Pump(currentSnapshotVersion: 21, currentSceneVersion: 8);
Check(versionRunner.State == WorldMaintenanceRunState.Stopped && versionRunner.Result.InvalidCount == 0 &&
      versionRunner.Result.UnprocessedCount == 70 && WorldMaintenancePolicy.IsResultConserved(versionRunner.Result),
    "A snapshot-version change must terminate an old running batch without misreporting candidates as stale.");
WorldMaintenanceBatchRunner<FakeCandidate> sceneVersionRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => { });
Check(sceneVersionRunner.TryStart(resourceBatchPreview, resourceCandidates, 20, 8), "Scene-version batch failed to start.");
sceneVersionRunner.Pump(currentSnapshotVersion: 20, currentSceneVersion: 9);
Check(sceneVersionRunner.State == WorldMaintenanceRunState.Stopped && sceneVersionRunner.Result.InvalidCount == 0 &&
      sceneVersionRunner.Result.UnprocessedCount == 70 && WorldMaintenancePolicy.IsResultConserved(sceneVersionRunner.Result),
    "A scene-version change must terminate an old running batch without misreporting candidates as stale.");

List<FakeCandidate> revalidatedCandidates = new()
{
    new FakeCandidate(1),
    new FakeCandidate(2) { Live = false },
    new FakeCandidate(3)
};
WorldMaintenancePreview revalidatedPreview = new(
    WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.Nearby,
    100f, 3, 0, 0, 30, 10, false);
int revalidatedActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> revalidatedRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => revalidatedActionCalls++);
Check(revalidatedRunner.TryStart(revalidatedPreview, revalidatedCandidates, 30, 10), "Revalidation batch failed to start.");
revalidatedRunner.Pump(30, 10);
Check(revalidatedActionCalls == 2 && revalidatedRunner.Result.InvalidCount == 1 &&
      WorldMaintenancePolicy.IsResultConserved(revalidatedRunner.Result),
    "Each candidate must be revalidated before execution; stale targets must not reach the native action.");

List<FakeCandidate> classifiedCandidates = new()
{
    new FakeCandidate(1),
    new FakeCandidate(2) { Decision = WorldMaintenanceCandidateDecision.Protected },
    new FakeCandidate(3) { Decision = WorldMaintenanceCandidateDecision.Blocked },
    new FakeCandidate(4) { Decision = WorldMaintenanceCandidateDecision.Invalid },
    new FakeCandidate(5)
};
WorldMaintenancePreview classifiedPreview = new(
    WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.LoadedWorld,
    250f, 2, 1, 1, 31, 10, false);
int classifiedCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> classifiedRunner = new(
    candidate => candidate.Live,
    candidate => candidate.Decision,
    candidate =>
    {
        classifiedCalls++;
        if (candidate.Id == 5) throw new InvalidOperationException("injected action failure");
    });
Check(classifiedRunner.TryStart(classifiedPreview, classifiedCandidates, 31, 10), "Classification batch failed to start.");
classifiedRunner.Pump(31, 10);
Check(classifiedRunner.State == WorldMaintenanceRunState.Completed && classifiedCalls == 2 &&
      classifiedRunner.Result.SucceededCount == 1 && classifiedRunner.Result.ProtectedCount == 1 &&
      classifiedRunner.Result.BlockedCount == 1 && classifiedRunner.Result.InvalidCount == 1 &&
      classifiedRunner.Result.FailedCount == 1 && classifiedRunner.Result.UnprocessedCount == 0 &&
      WorldMaintenancePolicy.IsResultConserved(classifiedRunner.Result),
    "Candidate decisions and action exceptions must be counted separately while the batch continues to conservation.");
WorldMaintenancePreview subsequentPreview = new(
    WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.LoadedWorld,
    250f, 1, 0, 0, 32, 10, false);
Check(classifiedRunner.TryStart(subsequentPreview, new[] { new FakeCandidate(6) }, 32, 10),
    "A terminal runner must allow a later non-concurrent maintenance task.");
classifiedRunner.Pump(32, 10);
Check(classifiedRunner.Result.SucceededCount == 1 && classifiedRunner.Result.OriginalCandidateCount == 1,
    "A later task must start with fresh counters and candidates.");

List<FakeCandidate> capabilityCandidates = new()
{
    new FakeCandidate(1),
    new FakeCandidate(2) { Decision = WorldMaintenanceCandidateDecision.CapabilityUnavailable },
    new FakeCandidate(3)
};
WorldMaintenancePreview capabilityPreview = new(
    WorldMaintenanceActionKind.RefreshNaturalResources, WorldMaintenanceScopeKind.WholeMap,
    250f, 3, 0, 0, 33, 10, false);
int capabilityActionCalls = 0;
WorldMaintenanceBatchRunner<FakeCandidate> capabilityRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => capabilityActionCalls++);
Check(capabilityRunner.TryStart(capabilityPreview, capabilityCandidates, 33, 10),
    "Capability-loss batch failed to start.");
capabilityRunner.Pump(33, 10);
Check(capabilityRunner.State == WorldMaintenanceRunState.Failed && capabilityActionCalls == 1 &&
      capabilityRunner.Result.SucceededCount == 1 && capabilityRunner.Result.UnprocessedCount == 2 &&
      WorldMaintenancePolicy.IsResultConserved(capabilityRunner.Result),
    "A runtime capability loss must fail closed, preserve completed work, and stop all remaining native actions.");

List<FakeCandidate> throwingValidationCandidates = new()
{
    new FakeCandidate(1),
    new FakeCandidate(2),
    new FakeCandidate(3)
};
WorldMaintenancePreview throwingValidationPreview = new(
    WorldMaintenanceActionKind.ClearZombies, WorldMaintenanceScopeKind.LoadedWorld,
    250f, 3, 0, 0, 34, 10, false);
int validationActionCalls = 0;
int downstreamSentinel = 0;
WorldMaintenanceBatchRunner<FakeCandidate> throwingValidationRunner = new(
    candidate => candidate.Id == 1 ? throw new InvalidOperationException("injected revalidation failure") : candidate.Live,
    candidate => candidate.Id == 2 ? throw new InvalidOperationException("injected classification failure") : candidate.Decision,
    candidate => validationActionCalls++);
Check(throwingValidationRunner.TryStart(throwingValidationPreview, throwingValidationCandidates, 34, 10),
    "Throwing-validation batch failed to start.");
throwingValidationRunner.Pump(34, 10);
downstreamSentinel++;
Check(throwingValidationRunner.State == WorldMaintenanceRunState.Completed && validationActionCalls == 1 &&
      throwingValidationRunner.Result.FailedCount == 2 && downstreamSentinel == 1 &&
      WorldMaintenancePolicy.IsResultConserved(throwingValidationRunner.Result),
    "Revalidation/classification exceptions must be contained so later candidates and downstream work still run.");

int safeScanFailureReports = 0;
int safeScanDownstream = 0;
int safeScanValue = WorldMaintenanceScanCoordinator.SafeScan(
    () => throw new InvalidOperationException("injected scan failure"),
    exception => { safeScanFailureReports++; return -1; });
safeScanDownstream++;
Check(safeScanValue == -1 && safeScanFailureReports == 1 && safeScanDownstream == 1,
    "A scan exception must return its fail-closed fallback and preserve downstream UI execution.");

List<string> mutationOrder = new();
int observerFailures = 0;
int removedCount = WorldMaintenanceMutationCoordinator.Execute(
    () => { mutationOrder.Add("native"); return 4; },
    () => { mutationOrder.Add("notify"); throw new InvalidOperationException("injected observer failure"); },
    _ => observerFailures++);
Check(removedCount == 4 && mutationOrder.SequenceEqual(new[] { "native", "notify" }) && observerFailures == 1,
    "Ground-item mutation must notify exactly once after native deletion, without converting observer failure into native failure.");
bool nativeFailurePropagated = false;
int notifyAfterNativeFailure = 0;
try
{
    WorldMaintenanceMutationCoordinator.Execute(
        () => throw new InvalidOperationException("injected native failure"),
        () => notifyAfterNativeFailure++,
        _ => observerFailures++);
}
catch (InvalidOperationException)
{
    nativeFailurePropagated = true;
}
Check(nativeFailurePropagated && notifyAfterNativeFailure == 0,
    "A failed native mutation must propagate to Failed accounting and must not emit a false change notification.");

static string FindRepositoryRoot()
{
    DirectoryInfo directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src", "UnturnedSingleplayerCheatMenu")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate repository root.");
}
string serviceSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "UnturnedSingleplayerCheatMenu", "Services", "WorldMaintenanceService.cs"));
int nullResourceGuard = serviceSource.IndexOf("if (spawn == null || !spawn.isDead)", StringComparison.Ordinal);
int resourcePointRead = serviceSource.IndexOf("spawn.point", nullResourceGuard >= 0 ? nullResourceGuard : 0, StringComparison.Ordinal);
Check(nullResourceGuard >= 0 && resourcePointRead > nullResourceGuard,
    "Resource scanning must reject null/alive spawnpoints before reading spawn.point.");
Check(serviceSource.Contains("WorldMaintenanceCandidateDecision.CapabilityUnavailable", StringComparison.Ordinal) &&
      serviceSource.Contains("batch.ResourceSupported = false", StringComparison.Ordinal),
    "Resource overlap failure must disable the batch and stop execution rather than masquerading as an ordinary collision.");
Check(serviceSource.Contains("internal ItemRegion Region { get; }", StringComparison.Ordinal) &&
      serviceSource.Contains("ReferenceEquals(regions[candidate.X, candidate.Y], candidate.Region)", StringComparison.Ordinal) &&
      serviceSource.Contains("candidate.Region.items != null", StringComparison.Ordinal),
    "Whole-map item cleanup must revalidate the original ItemRegion identity and item collection before clearing.");
Check(serviceSource.Contains("WorldMaintenanceMutationCoordinator.Execute", StringComparison.Ordinal) &&
      !serviceSource.Contains("GroundItemsChanged?.Invoke()", StringComparison.Ordinal),
    "Ground-item writes must use the exception-isolated native-then-notify coordinator.");

WorldMaintenancePreview emptyPreview = new(
    WorldMaintenanceActionKind.ClearGroundItems, WorldMaintenanceScopeKind.Nearby,
    250f, 0, 0, 0, 40, 11, false);
WorldMaintenanceBatchRunner<FakeCandidate> emptyRunner = new(
    candidate => candidate.Live, candidate => candidate.Decision, candidate => { });
Check(!emptyRunner.TryStart(emptyPreview, Array.Empty<FakeCandidate>(), 40, 11) &&
      emptyRunner.State == WorldMaintenanceRunState.Idle,
    "A zero-candidate preview must not enter Running.");

Console.WriteLine("World maintenance policy smoke checks passed.");

internal sealed class FakeCandidate
{
    internal FakeCandidate(int id) => Id = id;
    internal int Id { get; }
    internal bool Live { get; set; } = true;
    internal int Weight { get; set; } = 1;
    internal WorldMaintenanceCandidateDecision Decision { get; set; } = WorldMaintenanceCandidateDecision.Include;
}
