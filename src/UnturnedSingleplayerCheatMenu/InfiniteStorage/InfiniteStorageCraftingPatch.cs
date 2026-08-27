using System;
using System.Linq;
using HarmonyLib;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal static class InfiniteStorageCraftingPatch
{
	private const byte SyntheticPage = byte.MaxValue;
	[ThreadStatic]
	private static int _craftingSearchDepth;
	[ThreadStatic]
	private static int _craftingOutputDepth;
	[ThreadStatic]
	private static VirtualStorageDocument _craftingStorageBefore;
	[ThreadStatic]
	private static System.Collections.Generic.IReadOnlyList<InventoryBridge.CapturedItem> _craftingInventoryBefore;

	internal static void Install(Harmony harmony, BepInEx.Logging.ManualLogSource log)
	{
		try
		{
			HarmonyMethod searchPrefix = new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(CraftingSearchPrefix)));
			HarmonyMethod searchFinalizer = new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(CraftingSearchFinalizer)));
			HarmonyMethod outputPrefix = new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(CraftingOutputPrefix)));
			HarmonyMethod outputFinalizer = new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(CraftingOutputFinalizer)));
			harmony.Patch(AccessTools.Method(typeof(PlayerCrafting), "UpdateBlueprintInputItemStatus"), prefix: searchPrefix, finalizer: searchFinalizer);
			harmony.Patch(AccessTools.Method(typeof(PlayerCrafting), "HandleCraftRequestInternal"), prefix: outputPrefix, finalizer: outputFinalizer);
			harmony.Patch(AccessTools.Method(typeof(PlayerInventory), "SearchContents"), postfix: new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(SearchContentsPostfix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventorySearchResultV2), "Delete"), prefix: new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(DeletePrefix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventorySearchResultV2), "DeleteAmount"), prefix: new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(DeleteAmountPrefix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventory), "forceAddItem", new[] { typeof(Item), typeof(bool) }), prefix: new HarmonyMethod(AccessTools.Method(typeof(InfiniteStorageCraftingPatch), nameof(ForceAddItemPrefix))));
			log?.LogInfo("无限储存制作适配已注册：材料搜索、材料扣除与满包产物转存。");
		}
		catch (Exception ex)
		{
			log?.LogError("无限储存制作适配注册失败：\n" + ex);
		}
	}

	private static void CraftingSearchPrefix()
	{
		_craftingSearchDepth++;
	}

	private static Exception CraftingSearchFinalizer(Exception __exception)
	{
		_craftingSearchDepth = Math.Max(0, _craftingSearchDepth - 1);
		return __exception;
	}

	private static void CraftingOutputPrefix()
	{
		_craftingOutputDepth++;
		InfiniteStorageRuntime runtime = CheatMenuPlugin.Instance?.InfiniteStorage;
		if (_craftingOutputDepth == 1 && runtime?.IsOperational == true && Player.LocalPlayer?.inventory != null)
		{
			_craftingStorageBefore = runtime.Storage.Snapshot(string.Empty, string.Empty);
			_craftingInventoryBefore = InventoryBridge.Capture(Player.LocalPlayer.inventory);
		}
	}

	private static Exception CraftingOutputFinalizer(Exception __exception)
	{
		if (__exception != null && _craftingOutputDepth == 1)
		{
			InfiniteStorageRuntime runtime = CheatMenuPlugin.Instance?.InfiniteStorage;
			try
			{
				if (runtime?.IsOperational == true)
				{
					InventoryBridge.Restore(Player.LocalPlayer?.inventory, _craftingInventoryBefore);
					runtime.AbortCrafting(_craftingStorageBefore);
				}
			}
			catch
			{
				// Preserve the original crafting exception; the runtime log records persistence failures.
			}
		}
		_craftingOutputDepth = Math.Max(0, _craftingOutputDepth - 1);
		if (_craftingOutputDepth == 0)
		{
			_craftingStorageBefore = null;
			_craftingInventoryBefore = null;
		}
		return __exception;
	}

	private static void SearchContentsPostfix(ref PlayerInventorySearchParameters parameters)
	{
		InfiniteStorageRuntime runtime = CheatMenuPlugin.Instance?.InfiniteStorage;
		if (_craftingSearchDepth <= 0 || runtime == null || !runtime.IsOperational || parameters.Results == null || !parameters.AssetRef.HasValue)
		{
			return;
		}
		ItemAsset requested = parameters.AssetRef.Value.Get<ItemAsset>();
		if (requested == null)
		{
			return;
		}
		foreach (VirtualStorageEntry entry in runtime.Storage.GetEntries(requested.id).Where(value => value.UnitKind == StackUnitKind.ResourceUnits))
		{
			byte maxAmount = entry.NativeMaxStack == 0 ? (byte)1 : entry.NativeMaxStack;
			long remaining = entry.StoredUnitCount;
			while (remaining > 0)
			{
				byte amount = (byte)Math.Min(maxAmount, remaining);
				if (parameters.ExcludeFullAmount && amount >= maxAmount)
				{
					remaining -= amount;
					continue;
				}
				Item item = new Item(entry.AssetId, amount, entry.NativeQuality, ItemInstanceSemantics.CloneState(entry.NativeState));
				ItemJar jar = new ItemJar(item);
				Items owner = new Items(SyntheticPage);
				owner.items.Add(jar);
				parameters.Results.Add(new PlayerInventorySearchResultV2(owner, jar));
				remaining -= amount;
				if (parameters.MaxResultsCount > 0 && parameters.Results.Count >= parameters.MaxResultsCount)
				{
					return;
				}
			}
		}
	}

	private static bool DeletePrefix(ref PlayerInventorySearchResultV2 __instance, Player player)
	{
		if (!TryGetSynthetic(ref __instance, out SyntheticItem synthetic))
		{
			return true;
		}
		if (!synthetic.Runtime.RemoveVirtual(synthetic.Entry.EntryId, Math.Max(1L, (long)synthetic.Amount)))
		{
			throw new InvalidOperationException("InfiniteStorage could not persist crafting material removal; crafting was aborted.");
		}
		return false;
	}

	private static bool DeleteAmountPrefix(ref PlayerInventorySearchResultV2 __instance, Player player, uint desiredAmount, ref uint __result, bool alwaysDeleteAtZeroAmount = true)
	{
		if (!TryGetSynthetic(ref __instance, out SyntheticItem synthetic))
		{
			return true;
		}
		uint removed = Math.Min(desiredAmount, (uint)Math.Max(1, (int)synthetic.Amount));
		if (removed == 0)
		{
			__result = 0;
			return false;
		}
		if (removed > 0 && synthetic.Runtime.RemoveVirtual(synthetic.Entry.EntryId, removed))
		{
			__result = removed;
		}
		else
		{
			__result = 0;
			throw new InvalidOperationException("InfiniteStorage could not persist crafting material removal; crafting was aborted.");
		}
		return false;
	}

	private static bool ForceAddItemPrefix(PlayerInventory __instance, Item item, bool auto)
	{
		InfiniteStorageRuntime runtime = CheatMenuPlugin.Instance?.InfiniteStorage;
		if (_craftingOutputDepth <= 0 || runtime == null || !runtime.IsOperational || item == null)
		{
			return true;
		}
		if (__instance.tryAddItem(item, true))
		{
			return false;
		}
		ItemAsset asset = item.GetAsset();
		if (asset != null && runtime.StoreProduced(item, asset))
		{
			return false;
		}
		throw new InvalidOperationException("InfiniteStorage could not persist the crafted output; crafting was aborted.");
	}

	private static bool TryGetSynthetic(ref PlayerInventorySearchResultV2 result, out SyntheticItem synthetic)
	{
		InfiniteStorageRuntime runtime = CheatMenuPlugin.Instance?.InfiniteStorage;
		if (result.JarOwner?.page == SyntheticPage && result.Jar?.item != null && runtime != null && runtime.IsOperational)
		{
			Item syntheticItem = result.Jar.item;
			ItemAsset asset = syntheticItem.GetAsset();
			VirtualStorageEntry entry = asset == null ? null : runtime.Storage.FindEntry(ItemInstanceSemantics.Describe(asset, syntheticItem));
			if (entry == null)
			{
				synthetic = null;
				return false;
			}
			synthetic = new SyntheticItem(runtime, entry, syntheticItem.amount);
			return true;
		}
		synthetic = null;
		return false;
	}

	private sealed class SyntheticItem
	{
		internal readonly InfiniteStorageRuntime Runtime;
		internal readonly VirtualStorageEntry Entry;
		internal readonly byte Amount;

		internal SyntheticItem(InfiniteStorageRuntime runtime, VirtualStorageEntry entry, byte amount)
		{
			Runtime = runtime;
			Entry = entry;
			Amount = amount;
		}
	}
}
