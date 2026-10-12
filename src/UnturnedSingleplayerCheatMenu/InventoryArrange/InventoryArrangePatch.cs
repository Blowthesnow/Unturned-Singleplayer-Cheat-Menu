using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryArrangePatch
{
	private sealed class ManualMoveState
	{
		internal PlayerInventory Inventory;
		internal Item FirstItem;
		internal ushort FirstAssetId;
		internal InventoryItemAddress FirstSource;
		internal InventoryItemAddress FirstDestination;
		internal Item SecondItem;
		internal ushort SecondAssetId;
		internal InventoryItemAddress SecondSource;
		internal InventoryItemAddress SecondDestination;
		internal bool HasSecondItem;
	}

	private static InventoryArrangeFeature _feature;

	private static ManualLogSource _log;

	internal static void Install(Harmony harmony, ManualLogSource log, InventoryArrangeFeature feature)
	{
		_feature = feature;
		_log = log;
		try
		{
			MethodInfo methodInfo2 = AccessTools.Method(typeof(PlayerDashboardInventoryUI), "onGrabbedItem", new Type[4]
			{
				typeof(byte),
				typeof(byte),
				typeof(byte),
				typeof(SleekItem)
			});
			MethodInfo methodInfo3 = AccessTools.DeclaredMethod(typeof(Player), "InitializePlayer", Type.EmptyTypes);
			MethodInfo dragMethod = AccessTools.Method(typeof(PlayerInventory), "ReceiveDragItem", new Type[7]
			{
				typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte)
			});
			MethodInfo swapMethod = AccessTools.Method(typeof(PlayerInventory), "ReceiveSwapItem", new Type[8]
			{
				typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte)
			});
			if (methodInfo2 == null || methodInfo3 == null || dragMethod == null || swapMethod == null)
			{
				throw new MissingMethodException("Inventory arrange native handlers are incomplete in the current game assembly.");
			}
			// Native onGrabbedItem is dispatched only by the item's left-click event.
			// Keep the right-click onSelectedItem path under native inventory ownership.
			harmony.Patch(methodInfo2, new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), "GrabbedPrefix")));
			harmony.Patch(methodInfo3, null, new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), "PlayerInitializePostfix")));
			harmony.Patch(dragMethod,
				new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(DragPrefix))),
				new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(DragPostfix))));
			harmony.Patch(swapMethod,
				new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(SwapPrefix))),
				new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(SwapPostfix))));
			log?.LogInfo("背包整理 Player.InitializePlayer 补丁已注册。");
			log?.LogInfo("同物品跨箱集中已接入原生左键入口（仅 Ctrl + 鼠标左键）。");
			log?.LogInfo("快捷栏绑定已接入原生 Drag/Swap 坐标迁移与数字刷新。");
		}
		catch (Exception ex)
		{
			log?.LogError("背包整理补丁注册失败；普通整理功能仍可使用。\n" + ex);
		}
	}

	private static void PlayerInitializePostfix()
	{
		_feature?.OnPlayerInitialized();
	}

	private static void DragPrefix(PlayerInventory __instance, byte page_0, byte x_0, byte y_0,
		byte page_1, byte x_1, byte y_1, byte rot_1, out ManualMoveState __state)
	{
		__state = CaptureDrag(__instance, page_0, x_0, y_0, page_1, x_1, y_1);
	}

	private static void DragPostfix(PlayerInventory __instance, ManualMoveState __state)
	{
		if (__state == null || !ReferenceEquals(__state.Inventory, __instance)) return;
		ItemJar destination = GetJar(__instance, __state.FirstDestination);
		if (destination?.item == null || !ReferenceEquals(destination.item, __state.FirstItem)) return;
		ApplyHotkeyMoves(new InventoryItemMove(__state.FirstAssetId, __state.FirstSource, __state.FirstDestination));
	}

	private static void SwapPrefix(PlayerInventory __instance, byte page_0, byte x_0, byte y_0, byte rot_0,
		byte page_1, byte x_1, byte y_1, byte rot_1, out ManualMoveState __state)
	{
		__state = CaptureSwap(__instance, page_0, x_0, y_0, page_1, x_1, y_1);
	}

	private static void SwapPostfix(PlayerInventory __instance, ManualMoveState __state)
	{
		if (__state == null || !__state.HasSecondItem || !ReferenceEquals(__state.Inventory, __instance)) return;
		ItemJar firstDestination = GetJar(__instance, __state.FirstDestination);
		ItemJar secondDestination = GetJar(__instance, __state.SecondDestination);
		if (firstDestination?.item == null || secondDestination?.item == null ||
			!ReferenceEquals(firstDestination.item, __state.FirstItem) ||
			!ReferenceEquals(secondDestination.item, __state.SecondItem)) return;
		ApplyHotkeyMoves(
			new InventoryItemMove(__state.FirstAssetId, __state.FirstSource, __state.FirstDestination),
			new InventoryItemMove(__state.SecondAssetId, __state.SecondSource, __state.SecondDestination));
	}

	private static ManualMoveState CaptureDrag(PlayerInventory inventory, byte page0, byte x0, byte y0,
		byte page1, byte x1, byte y1)
	{
		if (!CanTrackManualMove(inventory, page0, page1)) return null;
		ItemJar source = GetJar(inventory, new InventoryItemAddress(page0, x0, y0));
		if (source?.item == null) return null;
		InventoryItemAddress sourceAddress = new InventoryItemAddress(page0, source.x, source.y);
		if (!HasBinding(source.item.id, sourceAddress)) return null;
		return new ManualMoveState
		{
			Inventory = inventory,
			FirstItem = source.item,
			FirstAssetId = source.item.id,
			FirstSource = sourceAddress,
			FirstDestination = new InventoryItemAddress(page1, x1, y1)
		};
	}

	private static ManualMoveState CaptureSwap(PlayerInventory inventory, byte page0, byte x0, byte y0,
		byte page1, byte x1, byte y1)
	{
		if (!CanTrackManualMove(inventory, page0, page1)) return null;
		ItemJar first = GetJar(inventory, new InventoryItemAddress(page0, x0, y0));
		ItemJar second = GetJar(inventory, new InventoryItemAddress(page1, x1, y1));
		if (first?.item == null || second?.item == null) return null;
		InventoryItemAddress firstSource = new InventoryItemAddress(page0, first.x, first.y);
		InventoryItemAddress secondSource = new InventoryItemAddress(page1, second.x, second.y);
		if (!HasBinding(first.item.id, firstSource) && !HasBinding(second.item.id, secondSource)) return null;
		return new ManualMoveState
		{
			Inventory = inventory,
			FirstItem = first.item,
			FirstAssetId = first.item.id,
			FirstSource = firstSource,
			FirstDestination = new InventoryItemAddress(page1, x1, y1),
			SecondItem = second.item,
			SecondAssetId = second.item.id,
			SecondSource = secondSource,
			SecondDestination = new InventoryItemAddress(page0, x0, y0),
			HasSecondItem = true
		};
	}

	private static bool CanTrackManualMove(PlayerInventory inventory, byte sourcePage, byte destinationPage)
	{
		return CheatMenuPlugin.Instance?.CanExecuteGameplay == true && inventory != null &&
			ReferenceEquals(Player.LocalPlayer?.inventory, inventory) &&
			IsHotkeyPage(sourcePage) && IsHotkeyPage(destinationPage) &&
			Player.LocalPlayer?.equipment?.hotkeys != null;
	}

	private static bool IsHotkeyPage(byte page) => page >= PlayerInventory.SLOTS && page < PlayerInventory.STORAGE;

	private static bool HasBinding(ushort assetId, InventoryItemAddress address)
	{
		HotkeyInfo[] hotkeys = Player.LocalPlayer?.equipment?.hotkeys;
		if (hotkeys == null) return false;
		foreach (HotkeyInfo hotkey in hotkeys)
		{
			if (hotkey != null && hotkey.id == assetId && hotkey.page == address.Page && hotkey.x == address.X && hotkey.y == address.Y)
			{
				return true;
			}
		}
		return false;
	}

	private static ItemJar GetJar(PlayerInventory inventory, InventoryItemAddress address)
	{
		if (inventory?.items == null || address.Page >= inventory.items.Length || inventory.items[address.Page] == null) return null;
		byte index = inventory.items[address.Page].getIndex(address.X, address.Y);
		return index == byte.MaxValue ? null : inventory.items[address.Page].getItem(index);
	}

	private static void ApplyHotkeyMoves(params InventoryItemMove[] moves)
	{
		PlayerEquipment equipment = Player.LocalPlayer?.equipment;
		HotkeyInfo[] hotkeys = equipment?.hotkeys;
		if (hotkeys == null || moves == null || moves.Length == 0) return;
		List<InventoryHotkeyBindingState> bindings = new List<InventoryHotkeyBindingState>(hotkeys.Length);
		for (int index = 0; index < hotkeys.Length; index++)
		{
			HotkeyInfo hotkey = hotkeys[index];
			if (hotkey != null && hotkey.id != 0)
			{
				bindings.Add(new InventoryHotkeyBindingState(index, hotkey.id,
					new InventoryItemAddress(hotkey.page, hotkey.x, hotkey.y)));
			}
		}
		IReadOnlyList<InventoryHotkeyBindingState> remapped = InventoryHotkeyBindingPolicy.Remap(bindings, moves);
		int changed = 0;
		foreach (InventoryHotkeyBindingState binding in remapped)
		{
			HotkeyInfo hotkey = hotkeys[binding.HotkeyIndex];
			if (hotkey.page == binding.Address.Page && hotkey.x == binding.Address.X && hotkey.y == binding.Address.Y) continue;
			hotkey.page = binding.Address.Page;
			hotkey.x = binding.Address.X;
			hotkey.y = binding.Address.Y;
			changed++;
		}
		equipment.onHotkeysUpdated?.Invoke();
		_log?.LogInfo("[InventoryArrangeHotkeys] Refreshed manual Drag/Swap shortcut display; relocated bindings=" + changed + ".");
	}

	private static bool GrabbedPrefix(byte page, byte x, byte y)
	{
		return HandleQuickTransfer(page, x, y);
	}

	private static bool HandleQuickTransfer(byte page, byte x, byte y)
	{
		if (_feature == null || !ShouldHandle(page) || !IsTransferModifierHeld())
		{
			return true;
		}
		InventoryArrangeResult inventoryArrangeResult = _feature.BeginQuickTransfer(page, x, y);
		if (inventoryArrangeResult != null && inventoryArrangeResult.Success)
		{
			_log?.LogInfo(inventoryArrangeResult.Message);
			return false;
		}
		if (inventoryArrangeResult != null)
		{
			return !IsValidInventorySource(page, x, y);
		}
		return true;
	}

	private static bool ShouldHandle(byte page)
	{
		if (Player.LocalPlayer != null && Player.LocalPlayer.inventory != null && Player.LocalPlayer.inventory.isStoring && page != PlayerInventory.AREA)
		{
			if (page != PlayerInventory.STORAGE)
			{
				if (page >= PlayerInventory.BACKPACK)
				{
					return page < PlayerInventory.STORAGE;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static bool IsValidInventorySource(byte page, byte x, byte y)
	{
		PlayerInventory playerInventory = Player.LocalPlayer?.inventory;
		if (playerInventory == null || page >= playerInventory.items.Length || playerInventory.items[page] == null)
		{
			return false;
		}
		byte index = playerInventory.items[page].getIndex(x, y);
		if (index != byte.MaxValue)
		{
			return playerInventory.items[page].getItem(index)?.item != null;
		}
		return false;
	}

	private static bool IsTransferModifierHeld()
	{
		return (InputEx.GetKey(KeyCode.LeftControl) || InputEx.GetKey(KeyCode.RightControl)) &&
			!InputEx.GetKey(KeyCode.LeftShift) && !InputEx.GetKey(KeyCode.RightShift) &&
			!InputEx.GetKey(KeyCode.LeftAlt) && !InputEx.GetKey(KeyCode.RightAlt);
	}
}
