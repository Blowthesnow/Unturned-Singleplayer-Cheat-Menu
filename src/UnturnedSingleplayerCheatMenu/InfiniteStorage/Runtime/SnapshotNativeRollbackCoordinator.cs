using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;

internal sealed class SnapshotNativeRollbackResult
{
	internal bool Success { get; private set; }
	internal int FailedStageCount { get; private set; }

	internal static SnapshotNativeRollbackResult Completed(int failedStageCount) =>
		new SnapshotNativeRollbackResult
		{
			Success = failedStageCount == 0,
			FailedStageCount = failedStageCount
		};
}

internal sealed class SnapshotNativeRecoveryResult
{
	internal bool Success { get; private set; }
	internal bool NativeRestoreAttempted { get; private set; }
	internal bool NativeRestoreThrew { get; private set; }
	internal bool ReconciliationAttempted { get; private set; }
	internal bool MarkerPersisted { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotNativeRecoveryResult Completed(bool success, bool restoreThrew,
		bool reconciliationAttempted, bool markerPersisted, string errorCode) =>
		new SnapshotNativeRecoveryResult
		{
			Success = success,
			NativeRestoreAttempted = true,
			NativeRestoreThrew = restoreThrew,
			ReconciliationAttempted = reconciliationAttempted,
			MarkerPersisted = markerPersisted,
			ErrorCode = errorCode ?? string.Empty
		};
}

internal static class SnapshotNativeRecoveryCoordinator
{
	internal static SnapshotNativeRecoveryResult Execute(Func<bool> restoreNative,
		Func<bool> persistReconciliation)
	{
		bool restoreThrew = false;
		bool restored = false;
		try { restored = restoreNative?.Invoke() == true; }
		catch (Exception) { restoreThrew = true; }
		if (restored)
			return SnapshotNativeRecoveryResult.Completed(true, false, false, false, string.Empty);

		bool reconciliationAttempted = persistReconciliation != null;
		bool markerPersisted = false;
		try { markerPersisted = persistReconciliation?.Invoke() == true; }
		catch (Exception) { markerPersisted = false; }
		string errorCode = restoreThrew ? "native-restore-threw" : "native-restore-failed";
		if (!markerPersisted) errorCode += ":marker-persistence-failed";
		return SnapshotNativeRecoveryResult.Completed(false, restoreThrew,
			reconciliationAttempted, markerPersisted, errorCode);
	}
}

internal sealed class SnapshotCrossBoundaryRecoveryResult
{
	internal bool Success { get; private set; }
	internal bool NativeRestoreAttempted { get; private set; }
	internal bool NativeRestoreThrew { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotCrossBoundaryRecoveryResult Completed(bool success,
		bool nativeAttempted, bool nativeThrew, string errorCode) =>
		new SnapshotCrossBoundaryRecoveryResult
		{
			Success = success,
			NativeRestoreAttempted = nativeAttempted,
			NativeRestoreThrew = nativeThrew,
			ErrorCode = errorCode ?? string.Empty
		};
}

internal static class SnapshotCrossBoundaryRecoveryCoordinator
{
	internal static SnapshotCrossBoundaryRecoveryResult Execute(bool storageRollbackProven,
		Func<bool> restoreNative)
	{
		if (!storageRollbackProven)
			return SnapshotCrossBoundaryRecoveryResult.Completed(false, false, false,
				"storage-rollback-unproven");
		try
		{
			bool restored = restoreNative?.Invoke() == true;
			return SnapshotCrossBoundaryRecoveryResult.Completed(restored, true, false,
				restored ? string.Empty : "native-restore-failed");
		}
		catch (Exception)
		{
			return SnapshotCrossBoundaryRecoveryResult.Completed(false, true, true,
				"native-restore-threw");
		}
	}
}

internal static class SnapshotNativeRollbackCoordinator
{
	internal static SnapshotNativeRollbackResult Execute(Func<bool> dequip,
		Func<bool> restoreClothing, IReadOnlyList<byte> pages, Func<byte, bool> clearPage,
		Func<bool> restoreItems, Func<bool> restoreSelection)
	{
		int failures = 0;
		if (!TryStage(dequip)) failures++;
		failures += ClearPages(pages, clearPage);
		if (!TryStage(restoreClothing)) failures++;
		failures += ClearPages(pages, clearPage);
		if (!TryStage(restoreItems)) failures++;
		if (!TryStage(restoreSelection)) failures++;
		return SnapshotNativeRollbackResult.Completed(failures);
	}

	private static int ClearPages(IReadOnlyList<byte> pages, Func<byte, bool> clearPage)
	{
		if (pages == null || clearPage == null)
			return 1;
		int failures = 0;
		for (int index = 0; index < pages.Count; index++)
			if (!TryStage(() => clearPage(pages[index]))) failures++;
		return failures;
	}

	private static bool TryStage(Func<bool> stage)
	{
		if (stage == null) return false;
		try { return stage(); }
		catch (Exception) { return false; }
	}
}
