using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class WorkshopLocalizationPatch
{
    internal static void Install(Harmony harmony, ManualLogSource log)
    {
        PatchGetter(harmony, log, typeof(ItemAsset), "FriendlyName", nameof(ItemFriendlyNamePostfix));
        PatchGetter(harmony, log, typeof(ItemAsset), "itemName", nameof(ItemNamePostfix));
        PatchGetter(harmony, log, typeof(ItemAsset), "itemDescription", nameof(ItemDescriptionPostfix));
        PatchGetter(harmony, log, typeof(VehicleAsset), "FriendlyName", nameof(VehicleFriendlyNamePostfix));
        PatchGetter(harmony, log, typeof(VehicleAsset), "vehicleName", nameof(VehicleNamePostfix));
        PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "text", nameof(ActionTextPostfix));
        PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "tooltip", nameof(ActionTooltipPostfix));
        PatchMethod(harmony, log, typeof(ItemAsset), "BuildDescription", nameof(ItemBuildDescriptionPostfix));
        PatchMethod(harmony, log, typeof(SleekInventory), "updateInventory", nameof(SleekInventoryUpdateInventoryPostfix));
    }

    private static void PatchGetter(Harmony harmony, ManualLogSource log, Type type, string property, string postfixName)
    {
        try
        {
            MethodInfo target = AccessTools.PropertyGetter(type, property);
            MethodInfo postfix = AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
            if (target == null || postfix == null)
                throw new MissingMethodException(type.FullName, property);
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            log?.LogInfo($"Workshop localization Getter patch registered: {type.Name}.{property}.");
        }
        catch (Exception ex)
        {
            log?.LogWarning($"Workshop localization could not patch {type.FullName}.{property}; original text will remain available.\n{ex}");
        }
    }

    private static void PatchMethod(Harmony harmony, ManualLogSource log, Type type, string method, string postfixName)
    {
        try
        {
            MethodInfo target = AccessTools.Method(type, method);
            MethodInfo postfix = AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
            if (target == null || postfix == null)
                throw new MissingMethodException(type.FullName, method);
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            log?.LogInfo($"Workshop localization UI patch registered: {type.Name}.{method}.");
        }
        catch (Exception ex)
        {
            log?.LogWarning($"Workshop localization could not patch {type.FullName}.{method}; that UI path will remain original.\n{ex}");
        }
    }

    private static void ItemFriendlyNamePostfix(ItemAsset __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.ItemName, ref __result);

    private static void ItemNamePostfix(ItemAsset __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.ItemName, ref __result);

    private static void ItemDescriptionPostfix(ItemAsset __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.ItemDescription, ref __result);

    private static void VehicleFriendlyNamePostfix(VehicleAsset __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.VehicleName, ref __result);

    private static void VehicleNamePostfix(VehicleAsset __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.VehicleName, ref __result);

    private static void ActionTextPostfix(SDG.Unturned.Action __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.ActionText, ref __result);

    private static void ActionTooltipPostfix(SDG.Unturned.Action __instance, ref string __result) =>
        Replace(__instance, WorkshopTextField.ActionTooltip, ref __result);

    private static void ItemBuildDescriptionPostfix(ItemAsset __instance, ref ItemDescriptionBuilder __0)
    {
        CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
        WorkshopLocalizationService service = plugin?.WorkshopLocalization;
        if (service == null || !service.Enabled || __instance == null || __0.lines == null)
            return;
        if (!service.TryGetTranslation(__instance, WorkshopTextField.ItemDescription, out string translation)
            || !service.TryGetOriginal(__instance, WorkshopTextField.ItemDescription, out string original))
            return;

        for (int index = 0; index < __0.lines.Count; index++)
        {
            ItemDescriptionLine line = __0.lines[index];
            if (!string.Equals(
                    WorkshopTextIdentity.NormalizeSource(line.text),
                    WorkshopTextIdentity.NormalizeSource(original),
                    StringComparison.Ordinal))
                continue;
            if (service.ShouldPreserveNativeTranslation(__instance, WorkshopTextField.ItemDescription, line.text))
                return;
            line.text = translation;
            __0.lines[index] = line;
            return;
        }
    }

    private static void SleekInventoryUpdateInventoryPostfix(SleekInventory __instance)
    {
        CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
        WorkshopLocalizationService service = plugin?.WorkshopLocalization;
        if (service == null || !service.Enabled || __instance?.itemAsset == null)
            return;

        FieldInfo nameLabelField = AccessTools.Field(typeof(SleekInventory), "nameLabel");
        if (nameLabelField?.GetValue(__instance) is not ISleekLabel nameLabel)
            return;
        string currentText = nameLabel.Text;
        if (service.ShouldPreserveNativeTranslation(__instance.itemAsset, WorkshopTextField.ItemName, currentText))
            return;
        if (!service.TryGetTranslation(__instance.itemAsset, WorkshopTextField.ItemName, out string translation))
            return;

        nameLabel.Text = __instance.quantity > 1
            ? $"{translation} x{__instance.quantity}"
            : translation;
    }

    private static void Replace(object instance, WorkshopTextField field, ref string result)
    {
        CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
        WorkshopLocalizationService service = plugin?.WorkshopLocalization;
        if (service == null || !service.Enabled || !service.ApplyToNativeUi)
            return;
        if (service.ShouldPreserveNativeTranslation(instance, field, result))
            return;
        if (service.TryGetTranslation(instance, field, out string translation))
            result = translation;
    }
}
