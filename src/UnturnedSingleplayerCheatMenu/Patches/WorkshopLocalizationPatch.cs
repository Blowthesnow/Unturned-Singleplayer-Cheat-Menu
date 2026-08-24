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
		PatchGetter(harmony, log, typeof(ItemAsset), "FriendlyName", "ItemFriendlyNamePostfix");
		PatchGetter(harmony, log, typeof(ItemAsset), "itemName", "ItemNamePostfix");
		PatchGetter(harmony, log, typeof(ItemAsset), "itemDescription", "ItemDescriptionPostfix");
		PatchGetter(harmony, log, typeof(VehicleAsset), "FriendlyName", "VehicleFriendlyNamePostfix");
		PatchGetter(harmony, log, typeof(VehicleAsset), "vehicleName", "VehicleNamePostfix");
		PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "text", "ActionTextPostfix");
		PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "tooltip", "ActionTooltipPostfix");
		PatchMethod(harmony, log, typeof(ItemAsset), "BuildDescription", "ItemBuildDescriptionPostfix");
		PatchMethod(harmony, log, typeof(SleekInventory), "updateInventory", "SleekInventoryUpdateInventoryPostfix");
	}

	private static void PatchGetter(Harmony harmony, ManualLogSource log, Type type, string property, string postfixName)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.PropertyGetter(type, property);
			MethodInfo methodInfo2 = AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
			if (methodInfo == null || methodInfo2 == null)
			{
				throw new MissingMethodException(type.FullName, property);
			}
			harmony.Patch(methodInfo, null, new HarmonyMethod(methodInfo2));
			log?.LogInfo("Workshop localization Getter patch registered: " + type.Name + "." + property + ".");
		}
		catch (Exception arg)
		{
			log?.LogWarning($"Workshop localization could not patch {type.FullName}.{property}; original text will remain available.\n{arg}");
		}
	}

	private static void PatchMethod(Harmony harmony, ManualLogSource log, Type type, string method, string postfixName)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.Method(type, method);
			MethodInfo methodInfo2 = AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
			if (methodInfo == null || methodInfo2 == null)
			{
				throw new MissingMethodException(type.FullName, method);
			}
			harmony.Patch(methodInfo, null, new HarmonyMethod(methodInfo2));
			log?.LogInfo("Workshop localization UI patch registered: " + type.Name + "." + method + ".");
		}
		catch (Exception arg)
		{
			log?.LogWarning($"Workshop localization could not patch {type.FullName}.{method}; that UI path will remain original.\n{arg}");
		}
	}

	private static void ItemFriendlyNamePostfix(ItemAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ItemName, ref __result);
	}

	private static void ItemNamePostfix(ItemAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ItemName, ref __result);
	}

	private static void ItemDescriptionPostfix(ItemAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ItemDescription, ref __result);
	}

	private static void VehicleFriendlyNamePostfix(VehicleAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VehicleName, ref __result);
	}

	private static void VehicleNamePostfix(VehicleAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VehicleName, ref __result);
	}

	private static void ActionTextPostfix(SDG.Unturned.Action __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ActionText, ref __result);
	}

	private static void ActionTooltipPostfix(SDG.Unturned.Action __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ActionTooltip, ref __result);
	}

	private static void ItemBuildDescriptionPostfix(ItemAsset __instance, ref ItemDescriptionBuilder __0)
	{
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (workshopLocalizationService == null || !workshopLocalizationService.Enabled || __instance == null || __0.lines == null || !workshopLocalizationService.TryGetTranslation(__instance, WorkshopTextField.ItemDescription, out var translation) || !workshopLocalizationService.TryGetOriginal(__instance, WorkshopTextField.ItemDescription, out var source))
		{
			return;
		}
		for (int i = 0; i < __0.lines.Count; i++)
		{
			ItemDescriptionLine value = __0.lines[i];
			if (string.Equals(WorkshopTextIdentity.NormalizeSource(value.text), WorkshopTextIdentity.NormalizeSource(source), StringComparison.Ordinal))
			{
				if (!workshopLocalizationService.ShouldPreserveNativeTranslation(__instance, WorkshopTextField.ItemDescription, value.text))
				{
					value.text = translation;
					__0.lines[i] = value;
				}
				break;
			}
		}
	}

	private static void SleekInventoryUpdateInventoryPostfix(SleekInventory __instance)
	{
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (workshopLocalizationService != null && workshopLocalizationService.Enabled && __instance?.itemAsset != null && AccessTools.Field(typeof(SleekInventory), "nameLabel")?.GetValue(__instance) is ISleekLabel { Text: var text } sleekLabel && !workshopLocalizationService.ShouldPreserveNativeTranslation(__instance.itemAsset, WorkshopTextField.ItemName, text) && workshopLocalizationService.TryGetTranslation(__instance.itemAsset, WorkshopTextField.ItemName, out var translation))
		{
			sleekLabel.Text = ((__instance.quantity > 1) ? $"{translation} x{__instance.quantity}" : translation);
		}
	}

	private static void Replace(object instance, WorkshopTextField field, ref string result)
	{
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (workshopLocalizationService != null && workshopLocalizationService.Enabled && workshopLocalizationService.ApplyToNativeUi && !workshopLocalizationService.ShouldPreserveNativeTranslation(instance, field, result) && workshopLocalizationService.TryGetTranslation(instance, field, out var translation))
		{
			result = translation;
		}
	}
}
