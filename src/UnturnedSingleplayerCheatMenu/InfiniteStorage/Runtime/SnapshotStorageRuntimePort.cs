using System.Collections.Generic;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;

internal interface ISnapshotStorageRuntimePort
{
	bool IsOperational { get; }
	IReadOnlyList<ItemGroupView> Groups { get; }
	SnapshotKernelTransaction BeginTransaction();
	SnapshotTransactionCommitResult Commit(SnapshotKernelTransaction transaction);
	SnapshotKernelCheckpoint CaptureCheckpoint();
	SnapshotTransactionCommitResult RestoreCheckpointAndPersist(SnapshotKernelCheckpoint checkpoint);
	SnapshotMutationResult StoreSnapshot(ItemGroupIdentity identity, ItemSnapshot snapshot);
	SnapshotMutationResult RegisterDefaultTemplate(ItemGroupIdentity identity, ItemSnapshot snapshot);
	bool LatchReconciliationFailure(string reason);
}
