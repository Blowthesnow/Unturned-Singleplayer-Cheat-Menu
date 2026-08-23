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
            MethodInfo selected = AccessTools.Method(
                typeof(PlayerDashboardInventoryUI),
                "onSelectedItem",
                new[] { typeof(byte), typeof(byte), typeof(byte) });
            MethodInfo grabbed = AccessTools.Method(
                typeof(PlayerDashboardInventoryUI),
                "onGrabbedItem",
                new[] { typeof(byte), typeof(byte), typeof(byte), typeof(SleekItem) });
            if (selected == null || grabbed == null)
                throw new MissingMethodException(typeof(PlayerDashboardInventoryUI).FullName, "inventory quick-transfer handlers");

            harmony.Patch(selected, prefix: new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(SelectedPrefix))));
            harmony.Patch(grabbed, prefix: new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(GrabbedPrefix))));

            MethodInfo playerInitialize = AccessTools.DeclaredMethod(typeof(Player), "InitializePlayer", Type.EmptyTypes);
            if (playerInitialize == null)
                throw new MissingMethodException(typeof(Player).FullName, "InitializePlayer");
            harmony.Patch(playerInitialize, postfix: new HarmonyMethod(AccessTools.Method(typeof(InventoryArrangePatch), nameof(PlayerInitializePostfix))));
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
            return true;

        InventoryArrangeResult result = _feature.BeginQuickTransfer(page, x, y);
        if (result?.Success == true)
        {
            _log?.LogInfo(result.Message);
            return false;
        }

        // A valid inventory click with the modifier is consumed even when the
        // destination is full, preventing the old Ctrl+left-click drop path.
        return result == null || !IsValidInventorySource(page, x, y);
    }

    private static bool ShouldHandle(byte page)
    {
        return Player.LocalPlayer != null
            && Player.LocalPlayer.inventory != null
            && Player.LocalPlayer.inventory.isStoring
            && page != PlayerInventory.AREA
            && (page == PlayerInventory.STORAGE || (page >= PlayerInventory.BACKPACK && page < PlayerInventory.STORAGE));
    }

    private static bool IsValidInventorySource(byte page, byte x, byte y)
    {
        PlayerInventory inventory = Player.LocalPlayer?.inventory;
        if (inventory == null || page >= inventory.items.Length || inventory.items[page] == null)
            return false;
        byte index = inventory.items[page].getIndex(x, y);
        return index != byte.MaxValue && inventory.items[page].getItem(index)?.item != null;
    }

    private static bool IsTransferModifierHeld()
    {
        return InputEx.GetKey(ControlsSettings.other)
            || InputEx.GetKey(KeyCode.LeftShift)
            || InputEx.GetKey(KeyCode.RightShift);
    }
}
