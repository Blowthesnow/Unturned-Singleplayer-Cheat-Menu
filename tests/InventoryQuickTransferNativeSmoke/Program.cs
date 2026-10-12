using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.InventoryArrange;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

Player.LocalPlayer = new Player();
Player.LocalPlayer.inventory.items[PlayerInventory.BACKPACK].Jar = new ItemJar { item = new Item { id = 1 } };
InventoryArrangeFeature feature = new();
Harmony harmony = new();
ManualLogSource log = new();
InventoryArrangePatch.Install(harmony, log, feature);
Check(log.Errors.Count == 0, "Patch installation must succeed against the checked native signatures.");
Check(harmony.Prefixes.Keys.Any(method => method.Name == "onGrabbedItem"), "The native left-click entry must be patched.");
Check(harmony.Prefixes.Keys.All(method => method.Name != "onSelectedItem"), "The native right-click entry must remain unpatched.");

KeyCode[] modifiers =
[
    KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift,
    KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt
];
for (int mask = 0; mask < 64; mask++)
{
    InputEx.Held.Clear();
    for (int index = 0; index < modifiers.Length; index++)
        if ((mask & (1 << index)) != 0) InputEx.Held.Add(modifiers[index]);
    // Only one or both Ctrl keys, without any additional modifier, may consume a left click.
    bool exactCtrl = mask is 1 or 2 or 3;
    for (int button = 0; button < 3; button++)
    {
        feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = PlayerDashboardInventoryUI.NativeRightCalls = 0;
        harmony.DispatchMouse(button, PlayerInventory.BACKPACK);
        bool shouldTransfer = button == 0 && exactCtrl;
        Check(feature.Calls == (shouldTransfer ? 1 : 0),
            $"Mouse {button} with modifier mask {mask} violated the Ctrl+left transfer contract.");
        Check(PlayerDashboardInventoryUI.NativeLeftCalls == (button == 0 && !shouldTransfer ? 1 : 0) &&
              PlayerDashboardInventoryUI.NativeRightCalls == (button == 1 ? 1 : 0),
            $"Mouse {button} with modifier mask {mask} altered the native click forwarding contract.");
    }
}

InputEx.Held.Clear();
InputEx.Held.Add(KeyCode.LeftControl);
InputEx.ProcessKeys = false;
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 0 && PlayerDashboardInventoryUI.NativeLeftCalls == 1,
    "Native key suppression while typing or configuring controls must also suppress cross-container transfer.");
InputEx.ProcessKeys = true;
for (byte page = 0; page < 9; page++)
{
    feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
    harmony.DispatchMouse(0, page);
    bool carriedOrStorage = page >= PlayerInventory.BACKPACK && page <= PlayerInventory.STORAGE;
    Check(feature.Calls == (carriedOrStorage ? 1 : 0), $"Page {page} crossed the existing carried/storage boundary.");
    Check(PlayerDashboardInventoryUI.NativeLeftCalls == (carriedOrStorage ? 0 : 1), $"Page {page} lost native ownership.");
}

Player.LocalPlayer.inventory.isStoring = false;
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 0 && PlayerDashboardInventoryUI.NativeLeftCalls == 1,
    "Ctrl+left without an open storage must remain native.");
Player.LocalPlayer.inventory.isStoring = true;
Player.LocalPlayer = null;
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 0 && PlayerDashboardInventoryUI.NativeLeftCalls == 1,
    "Missing local player must retain the existing native path.");
Player.LocalPlayer = new Player();
Player.LocalPlayer.inventory.items[PlayerInventory.BACKPACK].Jar = new ItemJar { item = new Item { id = 1 } };
feature.Result = new InventoryArrangeResult { Success = false };
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 1 && PlayerDashboardInventoryUI.NativeLeftCalls == 0,
    "A rejected transfer of a valid source must keep the existing suppression of native dropping.");
Player.LocalPlayer.inventory.items[PlayerInventory.BACKPACK].Jar = null;
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 1 && PlayerDashboardInventoryUI.NativeLeftCalls == 1,
    "A stale source must retain the existing native forwarding behavior.");
feature.Result = null;
feature.Calls = PlayerDashboardInventoryUI.NativeLeftCalls = 0;
harmony.DispatchMouse(0, PlayerInventory.BACKPACK);
Check(feature.Calls == 1 && PlayerDashboardInventoryUI.NativeLeftCalls == 1,
    "A missing transfer result must retain the existing native forwarding behavior.");

Console.WriteLine($"Inventory quick-transfer native input smoke passed ({checks} checks).");
