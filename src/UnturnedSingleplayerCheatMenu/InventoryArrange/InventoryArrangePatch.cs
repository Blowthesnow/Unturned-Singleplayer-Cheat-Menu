using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryArrangePatch
{
	private static InventoryArrangeFeature _feature;

	private static ManualLogSource _log;

	internal static void Install(Harmony harmony, ManualLogSource log, InventoryArrangeFeature feature)
	{
		_feature = feature;
		_log = log;
		try
		{
			MethodInfo methodInfo = AccessTools.Method(typeof(PlayerDashboardInventoryUI), "onSelectedItem", new Type[3]
			{
				typeof(byte),
				typeof(byte),
				typeof(byte)
			});
			MethodInfo methodInfo2 = AccessTools.Method(typeof(PlayerDashboardInventoryUI), "onGrabbedItem", new Type[4]
			{
				typeof(byte),
				typeof(byte),
				typeof(byte),
				typeof(SleekItem)
			});
			if (methodInfo == null || methodInfo2 == null)
			{
				throw new MissingMethodException(typeof(PlayerDashboardInventoryUI).FullName, "inventory quick-transfer handlers");
			}
			harmony.Patch(methodInfo, new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), "SelectedPrefix")));
			harmony.Patch(methodInfo2, new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), "GrabbedPrefix")));
			MethodInfo methodInfo3 = AccessTools.DeclaredMethod(typeof(Player), "InitializePlayer", Type.EmptyTypes);
			if (methodInfo3 == null)
			{
				throw new MissingMethodException(typeof(Player).FullName, "InitializePlayer");
			}
			harmony.Patch(methodInfo3, null, new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), "PlayerInitializePostfix")));
			log?.LogInfo("背包整理 Player.InitializePlayer 补丁已注册。");
			log?.LogInfo("同物品跨箱集中已接入原生快速转移入口（Ctrl/Shift 修饰键）。");
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

	private static bool SelectedPrefix(byte page, byte x, byte y)
	{
		return HandleQuickTransfer(page, x, y);
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
		if (!InputEx.GetKey(ControlsSettings.other) && !InputEx.GetKey(KeyCode.LeftShift))
		{
			return InputEx.GetKey(KeyCode.RightShift);
		}
		return true;
	}
}
