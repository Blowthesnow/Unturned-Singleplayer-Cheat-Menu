using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal enum InfiniteStorageDecomposeScope
{
	SingleItem,
	AssetId,
	Stack
}

internal readonly struct InfiniteStorageDecomposeTarget
{
	internal InfiniteStorageDecomposeTarget(string entryId, long units)
	{
		EntryId = entryId ?? string.Empty;
		Units = units;
	}

	internal string EntryId { get; }
	internal long Units { get; }
}

internal static class InfiniteStorageDecomposeRangePolicy
{
	internal static IReadOnlyList<InfiniteStorageDecomposeTarget> Resolve(
		IReadOnlyList<VirtualStorageEntry> entries,
		string pointedEntryId,
		InfiniteStorageDecomposeScope scope)
	{
		if (entries == null || string.IsNullOrWhiteSpace(pointedEntryId))
		{
			return Array.Empty<InfiniteStorageDecomposeTarget>();
		}

		VirtualStorageEntry pointed = entries.FirstOrDefault(entry =>
			entry != null && entry.StoredUnitCount > 0 &&
			string.Equals(entry.EntryId, pointedEntryId, StringComparison.Ordinal));
		if (pointed == null)
		{
			return Array.Empty<InfiniteStorageDecomposeTarget>();
		}

		if (scope == InfiniteStorageDecomposeScope.SingleItem)
		{
			return new[] { new InfiniteStorageDecomposeTarget(pointed.EntryId, 1) };
		}
		if (scope == InfiniteStorageDecomposeScope.Stack)
		{
			return new[] { new InfiniteStorageDecomposeTarget(pointed.EntryId, pointed.StoredUnitCount) };
		}

		return entries
			.Where(entry => entry != null && entry.StoredUnitCount > 0 && entry.AssetId == pointed.AssetId)
			.OrderBy(entry => entry.EntryId, StringComparer.Ordinal)
			.Select(entry => new InfiniteStorageDecomposeTarget(entry.EntryId, entry.StoredUnitCount))
			.ToList();
	}
}

internal static class InfiniteStorageDecomposeEligibility
{
	internal static bool IsStorageCandidate(VirtualStorageEntry entry)
	{
		return entry != null && entry.StoredUnitCount > 0 && entry.UnitKind != StackUnitKind.ResourceUnits;
	}

	internal static bool IsMaterialAsset(ItemAsset asset)
	{
		return asset != null && (asset.type == EItemType.SUPPLY ||
			string.Equals(asset.GetType().Name, "ItemResourceAsset", StringComparison.Ordinal));
	}
}

internal enum InfiniteStorageDecomposeFailure
{
	None,
	StorageUnavailable,
	EntryNotFound,
	MaterialNotDecomposable,
	AssetNotFound,
	NoSalvageRecipe,
	UnsupportedSalvageRecipe,
	OutputAssetNotFound,
	PersistenceFailed,
	ArithmeticOverflow,
	UnexpectedFailure
}

internal sealed class InfiniteStorageDecomposeResult
{
	internal bool Success => Failure == InfiniteStorageDecomposeFailure.None;
	internal InfiniteStorageDecomposeFailure Failure { get; private set; }
	internal long DecomposedUnitCount { get; private set; }
	internal long ProducedUnitCount { get; private set; }
	internal int ProducedAssetCount { get; private set; }

	internal static InfiniteStorageDecomposeResult Failed(InfiniteStorageDecomposeFailure failure)
	{
		return new InfiniteStorageDecomposeResult { Failure = failure };
	}

	internal static InfiniteStorageDecomposeResult Completed(long decomposedUnits, long producedUnits, int producedAssetCount)
	{
		return new InfiniteStorageDecomposeResult
		{
			Failure = InfiniteStorageDecomposeFailure.None,
			DecomposedUnitCount = decomposedUnits,
			ProducedUnitCount = producedUnits,
			ProducedAssetCount = producedAssetCount
		};
	}
}

internal static class InfiniteStorageSalvageRecipeResolver
{
	internal static Blueprint Find(ItemAsset asset)
	{
		if (asset?.blueprints == null)
		{
			return null;
		}

		Blueprint fallback = null;
		foreach (Blueprint blueprint in asset.blueprints)
		{
			if (!IsNativeSalvageCandidate(asset, blueprint))
			{
				continue;
			}
			fallback ??= blueprint;
			if (blueprint.CategoryTagRef == EBlueprintTypeEx.salvageCategoryTagRef ||
				string.Equals(blueprint.Name, "Salvage", StringComparison.OrdinalIgnoreCase))
			{
				return blueprint;
			}
		}
		return fallback;
	}

	internal static bool IsSupported(ItemAsset asset, Blueprint blueprint)
	{
		if (!IsNativeSalvageCandidate(asset, blueprint) ||
			blueprint.outputs == null || blueprint.outputs.Length == 0)
		{
			return false;
		}

		BlueprintSupply input = blueprint.supplies[0];
		return input.ShouldConsume && input.amount == 1 &&
			input.CountingMethod == ECraftingInputCountingMethod.TotalItems &&
			blueprint.outputs.All(output => output != null && output.amount > 0);
	}

	private static bool IsNativeSalvageCandidate(ItemAsset asset, Blueprint blueprint)
	{
		return asset != null && blueprint != null && blueprint.Operation == EBlueprintOperation.None &&
			blueprint.TargetItem == null && blueprint.supplies != null && blueprint.supplies.Length == 1 &&
			blueprint.supplies[0] != null && blueprint.supplies[0].IsItem(asset);
	}
}

internal static class InfiniteStorageDecomposer
{
	private sealed class PlannedOutput
	{
		internal ItemStorageDescriptor Descriptor;
		internal long Occurrences;
	}

	internal static bool CanDecompose(VirtualStorageEntry entry, ItemAsset asset)
	{
		if (!InfiniteStorageDecomposeEligibility.IsStorageCandidate(entry) || asset == null ||
			InfiniteStorageDecomposeEligibility.IsMaterialAsset(asset))
		{
			return false;
		}
		Blueprint blueprint = InfiniteStorageSalvageRecipeResolver.Find(asset);
		return InfiniteStorageSalvageRecipeResolver.IsSupported(asset, blueprint) &&
			blueprint.outputs.All(output => output.FindItemAsset() != null);
	}

	internal static NativeItemPayload CreateTransferredPayload(ushort outputAssetId, VirtualStorageEntry sourceEntry)
	{
		if (sourceEntry == null) throw new ArgumentNullException(nameof(sourceEntry));
		return new NativeItemPayload(outputAssetId, sourceEntry.NativeAmount, sourceEntry.NativeQuality, sourceEntry.NativeState);
	}

	internal static InfiniteStorageDecomposeResult Execute(
		VirtualStorageService storage,
		string entryId,
		InfiniteStorageDecomposeScope scope,
		Func<ushort, ItemAsset> assetResolver,
		Func<VirtualStorageTransaction, bool> commit)
	{
		if (storage == null || assetResolver == null || commit == null)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.StorageUnavailable);
		}

		IReadOnlyList<VirtualStorageEntry> entries = storage.Entries;
		VirtualStorageEntry selected = entries.FirstOrDefault(entry =>
			entry != null && string.Equals(entry.EntryId, entryId, StringComparison.Ordinal));
		if (selected == null)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.EntryNotFound);
		}
		if (!InfiniteStorageDecomposeEligibility.IsStorageCandidate(selected))
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.MaterialNotDecomposable);
		}

		ItemAsset sourceAsset = assetResolver(selected.AssetId);
		if (sourceAsset == null)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.AssetNotFound);
		}
		if (InfiniteStorageDecomposeEligibility.IsMaterialAsset(sourceAsset))
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.MaterialNotDecomposable);
		}

		Blueprint blueprint = InfiniteStorageSalvageRecipeResolver.Find(sourceAsset);
		if (blueprint == null)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.NoSalvageRecipe);
		}
		if (!InfiniteStorageSalvageRecipeResolver.IsSupported(sourceAsset, blueprint))
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.UnsupportedSalvageRecipe);
		}

		Dictionary<string, VirtualStorageEntry> entriesById = entries
			.Where(InfiniteStorageDecomposeEligibility.IsStorageCandidate)
			.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
		IReadOnlyList<InfiniteStorageDecomposeTarget> targets = InfiniteStorageDecomposeRangePolicy
			.Resolve(entries, entryId, scope)
			.Where(target => entriesById.ContainsKey(target.EntryId))
			.ToList();
		if (targets.Count == 0)
		{
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.EntryNotFound);
		}

		VirtualStorageTransaction transaction = null;
		try
		{
			long decomposedUnits = targets.Aggregate(0L, (total, target) => checked(total + target.Units));
			long producedUnits = 0;
			HashSet<ushort> producedAssetIds = new HashSet<ushort>();
			List<PlannedOutput> outputs = new List<PlannedOutput>(blueprint.outputs.Length);
			if (blueprint.transferState)
			{
				foreach (InfiniteStorageDecomposeTarget target in targets)
				{
					VirtualStorageEntry sourceEntry = entriesById[target.EntryId];
					foreach (BlueprintOutput output in blueprint.outputs)
					{
						ItemAsset outputAsset = output.FindItemAsset();
						if (outputAsset == null)
						{
							return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.OutputAssetNotFound);
						}
						Item outputItem = CreateTransferredOutputItem(blueprint, sourceEntry, sourceAsset, outputAsset);
						ItemStorageDescriptor descriptor = ItemInstanceSemantics.Describe(outputAsset, outputItem);
						long occurrences = checked(target.Units * output.amount);
						long unitsPerOccurrence = descriptor.UnitKind == StackUnitKind.ResourceUnits ? descriptor.NativeAmount : 1;
						producedUnits = checked(producedUnits + checked(occurrences * unitsPerOccurrence));
						producedAssetIds.Add(outputAsset.id);
						outputs.Add(new PlannedOutput { Descriptor = descriptor, Occurrences = occurrences });
					}
				}
			}
			else
			{
				foreach (BlueprintOutput output in blueprint.outputs)
				{
					ItemAsset outputAsset = output.FindItemAsset();
					if (outputAsset == null)
					{
						return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.OutputAssetNotFound);
					}
					Item outputItem = new Item(outputAsset.id, output.origin);
					ItemStorageDescriptor descriptor = ItemInstanceSemantics.Describe(outputAsset, outputItem);
					long occurrences = checked(decomposedUnits * output.amount);
					long unitsPerOccurrence = descriptor.UnitKind == StackUnitKind.ResourceUnits ? descriptor.NativeAmount : 1;
					producedUnits = checked(producedUnits + checked(occurrences * unitsPerOccurrence));
					producedAssetIds.Add(outputAsset.id);
					outputs.Add(new PlannedOutput { Descriptor = descriptor, Occurrences = occurrences });
				}
			}

			transaction = new VirtualStorageTransaction(storage, string.Empty, string.Empty);
			foreach (InfiniteStorageDecomposeTarget target in targets)
			{
				if (!transaction.Remove(target.EntryId, target.Units))
				{
					transaction.Rollback();
					return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.EntryNotFound);
				}
			}
			foreach (PlannedOutput output in outputs)
			{
				if (!transaction.Add(output.Descriptor, output.Occurrences))
				{
					transaction.Rollback();
					return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.ArithmeticOverflow);
				}
			}
			if (!commit(transaction))
			{
				return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.PersistenceFailed);
			}
			return InfiniteStorageDecomposeResult.Completed(decomposedUnits, producedUnits, producedAssetIds.Count);
		}
		catch (OverflowException)
		{
			transaction?.Rollback();
			return InfiniteStorageDecomposeResult.Failed(InfiniteStorageDecomposeFailure.ArithmeticOverflow);
		}
		catch
		{
			transaction?.Rollback();
			throw;
		}
	}

	private static Item CreateTransferredOutputItem(
		Blueprint blueprint,
		VirtualStorageEntry sourceEntry,
		ItemAsset sourceAsset,
		ItemAsset outputAsset)
	{
		NativeItemPayload payload = CreateTransferredPayload(outputAsset.id, sourceEntry);
		Item item = new Item(payload.AssetId, payload.Amount, payload.Quality, payload.State);
		if (sourceAsset is ItemGunAsset && outputAsset is ItemGunAsset outputGun && item.state.Length >= 12)
		{
			if (blueprint.withoutAttachments)
			{
				Array.Clear(item.state, 0, item.state.Length);
			}
			EFiremode defaultFiremode = outputGun.hasAuto ? EFiremode.AUTO :
				outputGun.hasSemi ? EFiremode.SEMI :
				outputGun.hasBurst ? EFiremode.BURST : EFiremode.SAFETY;
			item.state[11] = (byte)defaultFiremode;
		}
		return item;
	}
}
