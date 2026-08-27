using System;
using System.Collections.Generic;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class VirtualStorageMaterialRequirement
{
	internal readonly Item Item;
	internal readonly ItemAsset Asset;
	internal readonly long Units;

	internal VirtualStorageMaterialRequirement(Item item, ItemAsset asset, long units)
	{
		Item = item;
		Asset = asset;
		Units = units;
	}
}

/// <summary>Small adapter keeping crafting code independent from persistence details.</summary>
internal sealed class VirtualStorageCraftingAdapter
{
	private readonly InfiniteStorageRuntime _runtime;

	internal VirtualStorageCraftingAdapter(InfiniteStorageRuntime runtime)
	{
		_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
	}

	internal bool ConsumeVirtual(IReadOnlyList<VirtualStorageMaterialRequirement> requirements)
	{
		if (_runtime == null || requirements == null) return false;
		return _runtime.ConsumeVirtualTransaction(requirements);
	}
}
