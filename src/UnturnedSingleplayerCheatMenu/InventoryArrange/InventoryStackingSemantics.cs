using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryStackingSemantics
{
	internal static string BuildConservationFingerprint(IEnumerable<InventoryArrangeItem> items)
	{
		Dictionary<string, long> totals = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (InventoryArrangeItem item in items ?? Enumerable.Empty<InventoryArrangeItem>())
		{
			if (item == null) continue;
			switch (item.StackingKind)
			{
				case ItemStackingKind.FungibleUnits:
					Add(totals, "units|" + FungibleIdentity(item), Math.Max(0, item.Amount));
					break;
				case ItemStackingKind.HomogeneousInstances:
					Add(totals, "homogeneous|" + HomogeneousIdentity(item), 1);
					break;
				case ItemStackingKind.AmmoContainer:
					Add(totals, "ammo-instance|" + InstanceIdentity(item), 1);
					Add(totals, "ammo-rounds|" + AmmoIdentity(item), Math.Max(0, item.Amount));
					break;
				default:
					Add(totals, "instance|" + InstanceIdentity(item) + "|amount:" + item.Amount, 1);
					break;
			}
		}
		return string.Join(";", totals.OrderBy(pair => pair.Key, StringComparer.Ordinal)
			.Select(pair => pair.Key + "=" + pair.Value));
	}

	internal static bool TryValidateTransition(IEnumerable<InventoryArrangeItem> beforeItems,
		IEnumerable<InventoryArrangeItem> afterItems, IEnumerable<AmmoRefillTransfer> transfers, out string reason)
	{
		reason = string.Empty;
		List<InventoryArrangeItem> before = (beforeItems ?? Enumerable.Empty<InventoryArrangeItem>()).Where(value => value != null).ToList();
		List<InventoryArrangeItem> after = (afterItems ?? Enumerable.Empty<InventoryArrangeItem>()).Where(value => value != null).ToList();
		if (!string.Equals(BuildInvariantFingerprint(before), BuildInvariantFingerprint(after), StringComparison.Ordinal))
		{
			reason = "整理计划改变了物品实例、品质、状态或容器容量，已取消。";
			return false;
		}

		Dictionary<string, long> beforeRounds = BuildAmmoRoundsByAsset(before);
		Dictionary<string, long> afterRounds = BuildAmmoRoundsByAsset(after);
		if (beforeRounds.Values.Sum() != afterRounds.Values.Sum())
		{
			reason = "整理计划未保持弹药总量，已取消。";
			return false;
		}

		Dictionary<string, InventoryArrangeItem> beforeByKey = before.ToDictionary(value => value.ItemKey, StringComparer.Ordinal);
		HashSet<string> assetsWithCurrentCrossAssetTargets = new HashSet<string>(before
			.Where(value => value.IsAmmoContainer && !value.IsProtected && !value.IsHotkeyBound)
			.SelectMany(value => (value.AmmoDirectSourceAssetGuids ?? Array.Empty<string>())
				.Select(AmmoRefillIdentity.NormalizeAssetGuid)
				.Where(sourceAsset => !string.Equals(sourceAsset,
					AmmoRefillIdentity.NormalizeAssetGuid(value.AssetGuid), StringComparison.Ordinal))), StringComparer.Ordinal);
		HashSet<string> unsafeBridgeItemKeys = new HashSet<string>(before
			.Where(value => value.IsAmmoContainer && !value.IsProtected && !value.IsHotkeyBound &&
				assetsWithCurrentCrossAssetTargets.Contains(AmmoRefillIdentity.NormalizeAssetGuid(value.AssetGuid)) &&
				(value.AmmoDirectSourceAssetGuids ?? Array.Empty<string>())
					.Select(AmmoRefillIdentity.NormalizeAssetGuid)
					.Any(sourceAsset => !string.Equals(sourceAsset,
						AmmoRefillIdentity.NormalizeAssetGuid(value.AssetGuid), StringComparison.Ordinal)))
			.Select(value => value.ItemKey), StringComparer.Ordinal);
		Dictionary<string, long> outboundByItem = new Dictionary<string, long>(StringComparer.Ordinal);
		Dictionary<string, long> outboundBySourceIdentity = new Dictionary<string, long>(StringComparer.Ordinal);
		Dictionary<string, long> ledgerDelta = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (AmmoRefillTransfer transfer in transfers ?? Enumerable.Empty<AmmoRefillTransfer>())
		{
			if (transfer == null || transfer.Rounds <= 0 ||
				!beforeByKey.TryGetValue(transfer.SourceLotKey, out InventoryArrangeItem source) ||
				!beforeByKey.TryGetValue(transfer.TargetLotKey, out InventoryArrangeItem target))
			{
				reason = "整理计划包含无效弹药转移记录，已取消。";
				return false;
			}
			string sourceAsset = AmmoRefillIdentity.NormalizeAssetGuid(source.AssetGuid);
			string targetAsset = AmmoRefillIdentity.NormalizeAssetGuid(target.AssetGuid);
			if (!string.Equals(sourceAsset, transfer.SourceAssetGuid, StringComparison.Ordinal) ||
				!string.Equals(targetAsset, transfer.TargetAssetGuid, StringComparison.Ordinal) ||
				unsafeBridgeItemKeys.Contains(source.ItemKey) ||
				!(target.AmmoDirectSourceAssetGuids ?? Array.Empty<string>())
					.Select(AmmoRefillIdentity.NormalizeAssetGuid).Contains(sourceAsset, StringComparer.Ordinal))
			{
				reason = "整理计划尝试使用未获直接 FillTargetItem 授权的来源，或让桥接容器跨资产外流，已取消。";
				return false;
			}

			outboundByItem.TryGetValue(source.ItemKey, out long outbound);
			outbound = checked(outbound + transfer.Rounds);
			if (outbound > Math.Max(0, source.Amount))
			{
				reason = "整理计划复用了本轮刚收到的弹药或超出来源快照预算，已取消。";
				return false;
			}
			outboundByItem[source.ItemKey] = outbound;
			if (source.IsFungible && source.IsAmmoSource)
				Add(outboundBySourceIdentity, FungibleIdentity(source), transfer.Rounds);
			Add(ledgerDelta, sourceAsset, -transfer.Rounds);
			Add(ledgerDelta, targetAsset, transfer.Rounds);
		}

		foreach (string assetGuid in beforeRounds.Keys.Concat(afterRounds.Keys).Concat(ledgerDelta.Keys).Distinct(StringComparer.Ordinal))
		{
			beforeRounds.TryGetValue(assetGuid, out long beforeAmount);
			afterRounds.TryGetValue(assetGuid, out long afterAmount);
			ledgerDelta.TryGetValue(assetGuid, out long expectedDelta);
			if (afterAmount - beforeAmount != expectedDelta)
			{
				reason = "整理计划的跨资产弹量变化与逐笔有向转移记录不一致，已取消。";
				return false;
			}
		}
		Dictionary<string, long> beforeSourceUnits = BuildAmmoSourceUnitsByIdentity(before);
		Dictionary<string, long> afterSourceUnits = BuildAmmoSourceUnitsByIdentity(after);
		foreach (string identity in beforeSourceUnits.Keys.Concat(afterSourceUnits.Keys)
			.Concat(outboundBySourceIdentity.Keys).Distinct(StringComparer.Ordinal))
		{
			beforeSourceUnits.TryGetValue(identity, out long beforeAmount);
			afterSourceUnits.TryGetValue(identity, out long afterAmount);
			outboundBySourceIdentity.TryGetValue(identity, out long expectedConsumption);
			if (beforeAmount - afterAmount != expectedConsumption)
			{
				reason = "整理计划改变了弹药资源来源的身份或扣减量，已取消。";
				return false;
			}
		}
		return true;
	}

	private static string InstanceIdentity(InventoryArrangeItem item)
	{
		return (item.AssetGuid ?? string.Empty) + "|quality:" + item.Quality + "|state:" +
			(item.StateHash ?? string.Empty) + "|max:" + item.MaxAmount;
	}

	private static string FungibleIdentity(InventoryArrangeItem item)
	{
		return (item.AssetGuid ?? string.Empty) + "|quality:" + item.Quality + "|state:" +
			(item.StateHash ?? string.Empty) + "|max:" + item.MaxAmount;
	}

	private static string HomogeneousIdentity(InventoryArrangeItem item)
	{
		return (item.AssetGuid ?? string.Empty) + "|amount:" + item.Amount + "|quality:" + item.Quality +
			"|state:" + (item.StateHash ?? string.Empty) + "|max:" + item.MaxAmount;
	}

	private static string AmmoIdentity(InventoryArrangeItem item)
	{
		return AmmoRefillIdentity.NormalizeAssetGuid(item.AssetGuid) + "|quality:" + item.Quality +
			"|state:" + (item.StateHash ?? string.Empty) + "|max:" + item.MaxAmount;
	}

	private static string BuildInvariantFingerprint(IEnumerable<InventoryArrangeItem> items)
	{
		Dictionary<string, long> totals = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (InventoryArrangeItem item in items)
		{
			switch (item.StackingKind)
			{
				case ItemStackingKind.FungibleUnits:
					if (!item.IsAmmoSource) Add(totals, "units|" + FungibleIdentity(item), Math.Max(0, item.Amount));
					break;
				case ItemStackingKind.HomogeneousInstances:
					Add(totals, "homogeneous|" + HomogeneousIdentity(item), 1);
					break;
				case ItemStackingKind.AmmoContainer:
					Add(totals, "ammo-instance|" + InstanceIdentity(item), 1);
					break;
				default:
					Add(totals, "instance|" + InstanceIdentity(item) + "|amount:" + item.Amount, 1);
					break;
			}
		}
		return string.Join(";", totals.OrderBy(pair => pair.Key, StringComparer.Ordinal)
			.Select(pair => pair.Key + "=" + pair.Value));
	}

	private static Dictionary<string, long> BuildAmmoRoundsByAsset(IEnumerable<InventoryArrangeItem> items)
	{
		Dictionary<string, long> totals = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (InventoryArrangeItem item in items.Where(value => value.IsAmmoContainer || value.IsAmmoSource))
		{
			Add(totals, AmmoRefillIdentity.NormalizeAssetGuid(item.AssetGuid), Math.Max(0, item.Amount));
		}
		return totals;
	}

	private static Dictionary<string, long> BuildAmmoSourceUnitsByIdentity(IEnumerable<InventoryArrangeItem> items)
	{
		Dictionary<string, long> totals = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (InventoryArrangeItem item in items.Where(value => value.IsFungible && value.IsAmmoSource))
		{
			Add(totals, FungibleIdentity(item), Math.Max(0, item.Amount));
		}
		return totals;
	}

	private static void Add(IDictionary<string, long> totals, string key, long value)
	{
		totals.TryGetValue(key, out long current);
		totals[key] = checked(current + value);
	}
}
