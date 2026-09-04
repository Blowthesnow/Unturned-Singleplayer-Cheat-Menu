using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;

internal static class SnapshotCraftingPatch
{
	private const byte SyntheticPage = byte.MaxValue;
	private static readonly ConditionalWeakTable<ItemJar, SnapshotCraftingSyntheticItem> SyntheticItems =
		new ConditionalWeakTable<ItemJar, SnapshotCraftingSyntheticItem>();
	private static SnapshotCraftingModule _module;
	private static ManualLogSource _log;
	[ThreadStatic] private static Stack<SnapshotCraftingSearchContext> _activeSearches;
	[ThreadStatic] private static int _outputDepth;
	[ThreadStatic] private static SnapshotKernelCheckpoint _storageBefore;
	[ThreadStatic] private static SnapshotInventoryCheckpoint _inventoryBefore;

	internal static void Configure(SnapshotCraftingModule module) => _module = module;

	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		_log = log;
		if (_module?.IsEnabled != true) return;
		try
		{
			harmony.Patch(AccessTools.Method(typeof(PlayerCrafting), "UpdateBlueprintInputItemStatus"),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(SearchPrefix))),
				finalizer: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(SearchFinalizer))));
			harmony.Patch(AccessTools.Method(typeof(PlayerCrafting), "HandleCraftRequestInternal"),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(OutputPrefix))),
				finalizer: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(OutputFinalizer))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventory), "SearchContents"),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(SearchContentsPostfix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventorySearchResultV2), "Delete"),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(DeletePrefix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventorySearchResultV2), "DeleteAmount"),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(DeleteAmountPrefix))));
			harmony.Patch(AccessTools.Method(typeof(PlayerInventory), "forceAddItem", new[] { typeof(Item), typeof(bool) }),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(SnapshotCraftingPatch), nameof(ForceAddItemPrefix))));
			log?.LogInfo("InfiniteStorage Snapshot Crafting adapter registered inside the main plugin DLL.");
		}
		catch (Exception exception)
		{
			log?.LogError("InfiniteStorage Snapshot Crafting adapter registration failed; Core remains available:\n" + exception);
		}
	}

	private static void SearchPrefix(object[] __args)
	{
		_activeSearches ??= new Stack<SnapshotCraftingSearchContext>();
		SnapshotCraftingSearchRole role = SnapshotCraftingSearchRole.Target;
		BlueprintSupply supply = __args != null && __args.Length > 1
			? __args[1] as BlueprintSupply
			: null;
		TryClassifySearchRole(__args, out role);
		_activeSearches.Push(new SnapshotCraftingSearchContext(supply, role));
	}

	private static Exception SearchFinalizer(Exception __exception)
	{
		if (_activeSearches?.Count > 0) _activeSearches.Pop();
		return __exception;
	}

	private static void SearchContentsPostfix(ref PlayerInventorySearchParameters parameters)
	{
		if (_module?.IsOperational != true || _activeSearches?.Count <= 0 || parameters.Results == null) return;
		SnapshotCraftingSearchContext context = _activeSearches.Peek();
		if (!SnapshotCraftingSearchPolicy.ShouldInjectSynthetic(context.Role)) return;
		for (int index = parameters.Results.Count - 1; index >= 0; index--)
		{
			if (SnapshotCraftingSearchPolicy.ShouldExcludeItemToIgnore(
				parameters.Results[index].Jar, parameters.ItemToIgnore)) parameters.Results.RemoveAt(index);
		}
		BlueprintSupply supply = context.Supply;
		foreach (SnapshotCraftingSyntheticItem synthetic in _module.FindSyntheticItems(supply, in parameters))
		{
			Item item = new Item(synthetic.Snapshot.AssetId, synthetic.Snapshot.Amount,
				synthetic.Snapshot.Quality, synthetic.Snapshot.State);
			ItemJar jar = new ItemJar(item);
			Items owner = new Items(SyntheticPage);
			owner.items.Add(jar);
			SyntheticItems.Add(jar, synthetic);
			parameters.Results.Add(new PlayerInventorySearchResultV2(owner, jar));
			if (parameters.MaxResultsCount > 0 && parameters.Results.Count >= parameters.MaxResultsCount) return;
		}
	}

	private static bool TryClassifySearchRole(object[] arguments,
		out SnapshotCraftingSearchRole role)
	{
		role = SnapshotCraftingSearchRole.Target;
		if (arguments == null || arguments.Length < 3 || arguments[0] == null || arguments[2] == null)
			return false;
		try
		{
			object status = arguments[0].GetType().GetField("status",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(arguments[0]);
			if (status == null) return false;
			object targetStatus = status.GetType().GetField("targetStatus",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(status);
			role = ReferenceEquals(arguments[2], targetStatus)
				? SnapshotCraftingSearchRole.Target
				: SnapshotCraftingSearchRole.Supply;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool DeletePrefix(ref PlayerInventorySearchResultV2 __instance)
	{
		if (!TryGetSynthetic(ref __instance, out SnapshotCraftingSyntheticItem synthetic)) return true;
		if (!_module.ConsumeWhole(synthetic))
			throw new InvalidOperationException("Snapshot crafting material removal could not be persisted.");
		return false;
	}

	private static bool DeleteAmountPrefix(ref PlayerInventorySearchResultV2 __instance, uint desiredAmount,
		ref uint __result, bool alwaysDeleteAtZeroAmount = true)
	{
		if (!TryGetSynthetic(ref __instance, out SnapshotCraftingSyntheticItem synthetic)) return true;
		__result = _module.ConsumeAmount(synthetic, desiredAmount, alwaysDeleteAtZeroAmount);
		if (__result == 0 && desiredAmount > 0)
			throw new InvalidOperationException("Snapshot crafting amount removal could not be persisted.");
		return false;
	}

	private static void OutputPrefix()
	{
		_outputDepth++;
		if (_outputDepth != 1 || _module?.IsOperational != true) return;
		_storageBefore = _module.CaptureCheckpoint();
		_inventoryBefore = SnapshotInventoryBridge.Capture(Player.LocalPlayer?.inventory);
	}

	private static Exception OutputFinalizer(Exception __exception)
	{
		if (__exception != null && _outputDepth == 1 && _storageBefore != null)
		{
			SnapshotTransactionCommitResult storageRestored = null;
			SnapshotCraftingAbortResult abort = SnapshotCraftingRollbackGate.Execute(() =>
				SnapshotCraftingAbortCoordinator.Execute(
					restoreStorage: delegate
					{
						try
						{
							storageRestored = _module.RestoreCheckpoint(_storageBefore);
							return storageRestored?.Success == true;
						}
						catch (Exception rollbackException)
						{
							_log?.LogError("Snapshot crafting abort rollback threw while restoring storage:\n" + rollbackException);
							return false;
						}
					},
					restoreNative: delegate
					{
						try
						{
							return SnapshotInventoryBridge.Restore(Player.LocalPlayer?.inventory, _inventoryBefore);
						}
						catch (Exception rollbackException)
						{
							_log?.LogError("Snapshot crafting abort rollback threw while restoring inventory:\n" + rollbackException);
							return false;
						}
					},
					latchReconciliation: () => _module.LatchReconciliationFailure(
						SnapshotCraftingReconciliationReason.Build(
							__exception?.GetType().Name, storageRestored))));
			if (!abort.Success)
			{
				_log?.LogError("Snapshot crafting abort rollback failed closed; stage=" + abort.ErrorCode +
					" storage=" + (storageRestored?.ErrorCode ?? "not-restored"));
			}
		}
		_outputDepth = Math.Max(0, _outputDepth - 1);
		if (_outputDepth == 0)
		{
			_storageBefore = null;
			_inventoryBefore = null;
		}
		return __exception;
	}

	private static bool ForceAddItemPrefix(PlayerInventory __instance, Item item)
	{
		if (SnapshotCraftingRollbackGate.IsActive) return true;
		if (_outputDepth <= 0 || _module?.IsOperational != true || item == null) return true;
		if (__instance.tryAddItem(item, true) || _module.StoreProduced(item)) return false;
		throw new InvalidOperationException("Snapshot crafted output could not be placed or persisted.");
	}

	private static bool TryGetSynthetic(ref PlayerInventorySearchResultV2 result,
		out SnapshotCraftingSyntheticItem synthetic)
	{
		synthetic = null;
		return _module?.IsOperational == true && result.JarOwner?.page == SyntheticPage && result.Jar != null &&
			SyntheticItems.TryGetValue(result.Jar, out synthetic);
	}

	private readonly struct SnapshotCraftingSearchContext
	{
		internal SnapshotCraftingSearchContext(BlueprintSupply supply,
			SnapshotCraftingSearchRole role)
		{
			Supply = supply;
			Role = role;
		}

		internal BlueprintSupply Supply { get; }
		internal SnapshotCraftingSearchRole Role { get; }
	}
}
