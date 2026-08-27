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
	private static ObjectAsset _pendingNoteAsset;

	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		PatchGetter(harmony, log, typeof(ItemAsset), "FriendlyName", "ItemFriendlyNamePostfix");
		PatchGetter(harmony, log, typeof(ItemAsset), "itemName", "ItemNamePostfix");
		PatchGetter(harmony, log, typeof(ItemAsset), "itemDescription", "ItemDescriptionPostfix");
		PatchGetter(harmony, log, typeof(VehicleAsset), "FriendlyName", "VehicleFriendlyNamePostfix");
		PatchGetter(harmony, log, typeof(VehicleAsset), "vehicleName", "VehicleNamePostfix");
		PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "text", "ActionTextPostfix");
		PatchGetter(harmony, log, typeof(SDG.Unturned.Action), "tooltip", "ActionTooltipPostfix");
		PatchGetter(harmony, log, typeof(DialoguePage), "text", "DialoguePageTextPostfix");
		PatchGetter(harmony, log, typeof(DialogueResponse), "text", "DialogueResponseTextPostfix");
		PatchGetter(harmony, log, typeof(ObjectNPCAsset), "npcName", "NpcCharacterPostfix");
		PatchGetter(harmony, log, typeof(QuestAsset), "questName", "QuestNamePostfix");
		PatchGetter(harmony, log, typeof(QuestAsset), "questDescription", "QuestDescriptionPostfix");
		PatchGetter(harmony, log, typeof(ObjectAsset), "FriendlyName", "ObjectFriendlyNamePostfix");
		PatchGetter(harmony, log, typeof(ObjectAsset), "objectName", "ObjectNamePostfix");
		PatchGetter(harmony, log, typeof(ObjectAsset), "InteractabilityDialogueDisplayName", "ObjectDialogueNamePostfix");
		PatchGetter(harmony, log, typeof(VendorAsset), "vendorName", "VendorNamePostfix");
		PatchGetter(harmony, log, typeof(VendorAsset), "vendorDescription", "VendorDescriptionPostfix");
		PatchGetter(harmony, log, typeof(VendorBuying), "displayDesc", "VendorBuyingDescriptionPostfix");
		PatchGetter(harmony, log, typeof(VendorSellingItem), "displayDesc", "VendorSellingItemDescriptionPostfix");
		PatchGetter(harmony, log, typeof(VendorSellingVehicle), "displayDesc", "VendorSellingVehicleDescriptionPostfix");
		PatchMethod(harmony, log, typeof(ItemAsset), "BuildDescription", "ItemBuildDescriptionPostfix");
		PatchMethod(harmony, log, typeof(SleekInventory), "updateInventory", "SleekInventoryUpdateInventoryPostfix");
		PatchMethod(harmony, log, typeof(InteractableObjectDialogue), "checkHint", "InteractableObjectDialogueCheckHintPostfix");
		PatchMethod(harmony, log, typeof(InteractableObjectNote), "use", Type.EmptyTypes, "NoteUsePrefix", "NoteUsePostfix");
		PatchMethod(harmony, log, typeof(PlayerBarricadeSignUI), "open", new[] { typeof(string) }, "NoteTextWindowOpenPrefix", null);
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
		PatchMethod(harmony, log, type, method, null, postfixName);
	}

	private static void PatchMethod(Harmony harmony, ManualLogSource log, Type type, string method, string prefixName, string postfixName)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.Method(type, method);
			MethodInfo prefixMethod = string.IsNullOrEmpty(prefixName) ? null : AccessTools.Method(typeof(WorkshopLocalizationPatch), prefixName);
			MethodInfo postfixMethod = string.IsNullOrEmpty(postfixName) ? null : AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
			if (methodInfo == null || (prefixMethod == null && postfixMethod == null))
			{
				throw new MissingMethodException(type.FullName, method);
			}
			harmony.Patch(methodInfo,
				prefixMethod == null ? null : new HarmonyMethod(prefixMethod),
				postfixMethod == null ? null : new HarmonyMethod(postfixMethod));
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

	private static void DialoguePageTextPostfix(DialoguePage __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.DialogueMessagePage, ref __result);
	}

	private static void DialogueResponseTextPostfix(DialogueResponse __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.DialogueResponse, ref __result);
	}

	private static void NpcCharacterPostfix(ObjectNPCAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.NpcCharacter, ref __result);
	}

	private static void QuestNamePostfix(QuestAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.QuestName, ref __result);
	}

	private static void QuestDescriptionPostfix(QuestAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.QuestDescription, ref __result);
	}

	private static void ObjectFriendlyNamePostfix(ObjectAsset __instance, ref string __result)
	{
		ReplaceObjectName(__instance, ref __result);
	}

	private static void ObjectNamePostfix(ObjectAsset __instance, ref string __result)
	{
		ReplaceObjectName(__instance, ref __result);
	}

	private static void ObjectDialogueNamePostfix(ObjectAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.ObjectDialogueName, ref __result);
	}

	private static void VendorNamePostfix(VendorAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VendorName, ref __result);
	}

	private static void VendorDescriptionPostfix(VendorAsset __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VendorDescription, ref __result);
	}

	private static void VendorBuyingDescriptionPostfix(VendorBuying __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VendorBuyingDescription, ref __result);
	}

	private static void VendorSellingItemDescriptionPostfix(VendorSellingItem __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VendorSellingDescription, ref __result);
	}

	private static void VendorSellingVehicleDescriptionPostfix(VendorSellingVehicle __instance, ref string __result)
	{
		Replace(__instance, WorkshopTextField.VendorSellingDescription, ref __result);
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

	private static void InteractableObjectDialogueCheckHintPostfix(InteractableObjectDialogue __instance, ref string text)
	{
		ObjectAsset objectAsset = __instance?.objectAsset;
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (objectAsset != null && workshopLocalizationService != null && workshopLocalizationService.TryGetOriginal(objectAsset, WorkshopTextField.ObjectInteract, out var source) && string.Equals(WorkshopTextIdentity.NormalizeSource(text), WorkshopTextIdentity.NormalizeSource(source), StringComparison.Ordinal))
		{
			Replace(objectAsset, WorkshopTextField.ObjectInteract, ref text);
		}
	}

	private static void NoteUsePrefix(InteractableObjectNote __instance)
	{
		_pendingNoteAsset = __instance?.objectAsset;
	}

	private static void NoteUsePostfix()
	{
		_pendingNoteAsset = null;
	}

	private static void NoteTextWindowOpenPrefix(ref string __0)
	{
		ObjectAsset objectAsset = _pendingNoteAsset;
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (objectAsset == null || workshopLocalizationService == null || !workshopLocalizationService.Enabled || !workshopLocalizationService.ApplyToNativeUi || string.IsNullOrWhiteSpace(__0))
		{
			return;
		}
		if (!workshopLocalizationService.TryGetOriginal(objectAsset, WorkshopTextField.ObjectInteract, out var source) || !string.Equals(WorkshopTextIdentity.NormalizeSource(__0), WorkshopTextIdentity.NormalizeSource(source), StringComparison.Ordinal))
		{
			return;
		}
		if (workshopLocalizationService.ShouldPreserveNativeTranslation(objectAsset, WorkshopTextField.ObjectInteract, __0))
		{
			return;
		}
		if (workshopLocalizationService.TryGetTranslation(objectAsset, WorkshopTextField.ObjectInteract, out var translation))
		{
			__0 = translation;
		}
	}

	private static void PatchMethod(Harmony harmony, ManualLogSource log, Type type, string method, Type[] parameters, string prefixName, string postfixName)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(type, method, parameters);
			MethodInfo prefixMethod = string.IsNullOrEmpty(prefixName) ? null : AccessTools.Method(typeof(WorkshopLocalizationPatch), prefixName);
			MethodInfo postfixMethod = string.IsNullOrEmpty(postfixName) ? null : AccessTools.Method(typeof(WorkshopLocalizationPatch), postfixName);
			if (methodInfo == null || (prefixMethod == null && postfixMethod == null))
			{
				throw new MissingMethodException(type.FullName, method);
			}
			harmony.Patch(methodInfo,
				prefixMethod == null ? null : new HarmonyMethod(prefixMethod),
				postfixMethod == null ? null : new HarmonyMethod(postfixMethod));
			log?.LogInfo("Workshop localization UI patch registered: " + type.Name + "." + method + ".");
		}
		catch (Exception arg)
		{
			log?.LogWarning($"Workshop localization could not patch {type.FullName}.{method}; that UI path will remain original.\n{arg}");
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

	private static void ReplaceObjectName(ObjectAsset instance, ref string result)
	{
		WorkshopLocalizationService workshopLocalizationService = CheatMenuPlugin.Instance?.WorkshopLocalization;
		if (workshopLocalizationService == null || !workshopLocalizationService.Enabled || !workshopLocalizationService.ApplyToNativeUi || workshopLocalizationService.ShouldPreserveNativeTranslation(instance, WorkshopTextField.ObjectName, result) || !workshopLocalizationService.TryGetOriginal(instance, WorkshopTextField.ObjectName, out var source) || !workshopLocalizationService.TryGetTranslation(instance, WorkshopTextField.ObjectName, out var translation))
		{
			return;
		}
		string suffix = string.Empty;
		if (!string.IsNullOrEmpty(source) && result != null && result.StartsWith(source, StringComparison.Ordinal) && result.Length > source.Length)
		{
			suffix = result.Substring(source.Length);
		}
		result = translation + suffix;
	}
}
