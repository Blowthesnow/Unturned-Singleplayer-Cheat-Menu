using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class InfiniteStorageDestructionResult
{
	internal bool Success { get; private set; }
	internal long DestroyedCount { get; private set; }
	internal bool BlacklistAdded { get; private set; }
	internal bool BlacklistRollbackSucceeded { get; private set; }
	internal bool StorageStateUncertain { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static InfiniteStorageDestructionResult Succeeded(long destroyedCount,
		bool blacklistAdded) => new InfiniteStorageDestructionResult
	{
		Success = true,
		DestroyedCount = destroyedCount,
		BlacklistAdded = blacklistAdded,
		BlacklistRollbackSucceeded = true
	};

	internal static InfiniteStorageDestructionResult Failed(string errorCode,
		bool blacklistAdded = false, bool blacklistRollbackSucceeded = true,
		bool storageStateUncertain = false) => new InfiniteStorageDestructionResult
	{
		ErrorCode = errorCode ?? "infinite-storage-destruction-failed",
		BlacklistAdded = blacklistAdded,
		BlacklistRollbackSucceeded = blacklistRollbackSucceeded,
		StorageStateUncertain = storageStateUncertain
	};
}

internal static class InfiniteStorageDestructionCoordinator
{
	internal static InfiniteStorageDestructionResult Execute(ushort assetId,
		AutoPickupRuleDocument currentRules,
		Func<bool, AutoPickupFilterMode, IEnumerable<ItemFilterRule>, AutoPickupRuleLoadResult> saveRules,
		Func<SnapshotDestructionResult> destroy)
	{
		if (assetId == 0 || currentRules == null || saveRules == null || destroy == null)
			return InfiniteStorageDestructionResult.Failed("destruction-coordinator-invalid");

		AutoPickupRuleDocument original = CloneDocument(currentRules);
		List<ItemFilterRule> updatedRules = original.Rules.Select(rule => rule.Clone()).ToList();
		string assetIdText = assetId.ToString(CultureInfo.InvariantCulture);
		ItemFilterRule existing = updatedRules.FirstOrDefault(rule => IsAssetDenyRule(rule, assetId));
		bool blacklistAdded = existing?.Enabled != true;
		if (blacklistAdded)
		{
			if (existing != null)
				existing.Enabled = true;
			else
				updatedRules.Add(new ItemFilterRule
				{
					Enabled = true,
					Action = ItemFilterRuleAction.Deny,
					Field = ItemFilterRuleField.AssetId,
					Value = assetIdText
				});
			AutoPickupRuleLoadResult blacklistSave = saveRules(original.Enabled, original.Mode, updatedRules);
			if (!blacklistSave.Success)
			{
				AutoPickupRuleLoadResult rollback = saveRules(original.Enabled, original.Mode,
					original.Rules.Select(rule => rule.Clone()));
				return InfiniteStorageDestructionResult.Failed("blacklist-save-failed",
					blacklistAdded: true, blacklistRollbackSucceeded: rollback.Success);
			}
		}

		SnapshotDestructionResult destroyed;
		try
		{
			destroyed = destroy() ?? SnapshotDestructionResult.Failed("destruction-returned-null");
		}
		catch (Exception exception)
		{
			destroyed = SnapshotDestructionResult.Failed(
				"destruction-threw:" + exception.GetType().Name, rollbackSucceeded: true);
		}
		if (destroyed.Success)
			return InfiniteStorageDestructionResult.Succeeded(destroyed.DestroyedCount, blacklistAdded);
		if (!blacklistAdded)
			return InfiniteStorageDestructionResult.Failed(destroyed.ErrorCode,
				storageStateUncertain: !destroyed.RollbackSucceeded);
		if (!destroyed.RollbackSucceeded)
			return InfiniteStorageDestructionResult.Failed(destroyed.ErrorCode,
				blacklistAdded: true, blacklistRollbackSucceeded: false, storageStateUncertain: true);

		AutoPickupRuleLoadResult blacklistRollback = saveRules(original.Enabled, original.Mode,
			original.Rules.Select(rule => rule.Clone()));
		return InfiniteStorageDestructionResult.Failed(destroyed.ErrorCode,
			blacklistAdded: !blacklistRollback.Success,
			blacklistRollbackSucceeded: blacklistRollback.Success);
	}

	private static bool IsAssetDenyRule(ItemFilterRule rule, ushort assetId)
	{
		return rule != null && rule.Action == ItemFilterRuleAction.Deny &&
			rule.Field == ItemFilterRuleField.AssetId &&
			ushort.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
				out ushort parsed) && parsed == assetId;
	}

	private static AutoPickupRuleDocument CloneDocument(AutoPickupRuleDocument source) =>
		new AutoPickupRuleDocument
		{
			Version = source.Version,
			Enabled = source.Enabled,
			Mode = source.Mode,
			Rules = (source.Rules ?? new List<ItemFilterRule>())
				.Where(rule => rule != null).Select(rule => rule.Clone()).ToList()
		};
}
