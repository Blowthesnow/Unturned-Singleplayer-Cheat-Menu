using System;
using System.Collections.Generic;
using System.Reflection;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationScanner
{
	private readonly FieldInfo _itemNameField = typeof(ItemAsset).GetField("_itemName", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _itemDescriptionField = typeof(ItemAsset).GetField("_itemDescription", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _vehicleNameField = typeof(VehicleAsset).GetField("_vehicleName", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _actionTextField = typeof(SDG.Unturned.Action).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _actionTooltipField = typeof(SDG.Unturned.Action).GetField("_tooltip", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _actionKeyField = typeof(SDG.Unturned.Action).GetField("_key", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _dialoguePageTextField = typeof(DialoguePage).GetField("<text>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _dialogueResponseTextField = typeof(DialogueResponse).GetField("<text>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _npcCharacterField = typeof(ObjectNPCAsset).GetField("<npcName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _questNameField = typeof(QuestAsset).GetField("<questName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _questDescriptionField = typeof(QuestAsset).GetField("<questDescription>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _objectNameField = typeof(ObjectAsset).GetField("_objectName", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _objectDialogueNameField = typeof(ObjectAsset).GetField("<InteractabilityDialogueDisplayName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _vendorNameField = typeof(VendorAsset).GetField("<vendorName>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _vendorDescriptionField = typeof(VendorAsset).GetField("<vendorDescription>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo _vendorElementDescriptionField = typeof(VendorElement).GetField("descriptionOverride", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly Action<string> _warn;

	private bool _warnedUnavailable;

	internal WorkshopLocalizationScanner(Action<string> warn = null)
	{
		_warn = warn;
	}

	internal WorkshopLocalizationScanResult Scan(string sourceLanguage, string targetLanguage)
	{
		if (_itemNameField == null || _itemDescriptionField == null || _vehicleNameField == null || _actionTextField == null || _actionTooltipField == null || _actionKeyField == null)
		{
			if (!_warnedUnavailable)
			{
				_warnedUnavailable = true;
				_warn?.Invoke("Workshop localization disabled: one or more verified private asset fields could not be resolved.");
			}
			return new WorkshopLocalizationScanResult
			{
				Candidates = Array.Empty<WorkshopLocalizationCandidate>(),
				ItemCount = 0,
				VehicleCount = 0
			};
		}
		WarnUnavailableNpcFields();
		List<ItemAsset> list = new List<ItemAsset>();
		List<VehicleAsset> list2 = new List<VehicleAsset>();
		List<DialogueAsset> list3 = new List<DialogueAsset>();
		List<ObjectNPCAsset> list4 = new List<ObjectNPCAsset>();
		List<QuestAsset> list5 = new List<QuestAsset>();
		List<ObjectAsset> list6 = new List<ObjectAsset>();
		List<VendorAsset> list7 = new List<VendorAsset>();
		Assets.find(list);
		Assets.find(list2);
		Assets.find(list3);
		Assets.find(list4);
		Assets.find(list5);
		Assets.find(list6);
		Assets.find(list7);
		List<WorkshopLocalizationCandidate> candidates = new List<WorkshopLocalizationCandidate>();
		int num = 0;
		int num2 = 0;
		foreach (ItemAsset item in list)
		{
			if (!IsSupportedAsset(item))
			{
				continue;
			}
			num++;
			Add(candidates, item, WorkshopTextField.ItemName, Read(_itemNameField, item), item, sourceLanguage, targetLanguage);
			Add(candidates, item, WorkshopTextField.ItemDescription, Read(_itemDescriptionField, item), item, sourceLanguage, targetLanguage);
			IReadOnlyList<SDG.Unturned.Action> actions = item.actions;
			if (actions == null)
			{
				continue;
			}
			foreach (SDG.Unturned.Action item2 in actions)
			{
				if (item2 != null)
				{
					string actionKey = Read(_actionKeyField, item2);
					Add(candidates, item2, WorkshopTextField.ActionText, Read(_actionTextField, item2), item, sourceLanguage, targetLanguage, actionKey);
					Add(candidates, item2, WorkshopTextField.ActionTooltip, Read(_actionTooltipField, item2), item, sourceLanguage, targetLanguage, actionKey);
				}
			}
		}
		foreach (VehicleAsset item3 in list2)
		{
			if (IsSupportedAsset(item3))
			{
				num2++;
				Add(candidates, item3, WorkshopTextField.VehicleName, Read(_vehicleNameField, item3), item3, sourceLanguage, targetLanguage);
			}
		}
		foreach (DialogueAsset dialogue in list3)
		{
			if (!IsSupportedAsset(dialogue))
			{
				continue;
			}
			for (int messageIndex = 0; dialogue.messages != null && messageIndex < dialogue.messages.Length; messageIndex++)
			{
				DialogueMessage dialogueMessage = dialogue.messages[messageIndex];
				for (int pageIndex = 0; dialogueMessage?.pages != null && pageIndex < dialogueMessage.pages.Length; pageIndex++)
				{
					DialoguePage dialoguePage = dialogueMessage.pages[pageIndex];
					Add(candidates, dialoguePage, WorkshopTextField.DialogueMessagePage, Read(_dialoguePageTextField, dialoguePage), dialogue, sourceLanguage, targetLanguage, localizationKey: $"Message_{messageIndex}_Page_{pageIndex}");
				}
			}
			for (int responseIndex = 0; dialogue.responses != null && responseIndex < dialogue.responses.Length; responseIndex++)
			{
				DialogueResponse dialogueResponse = dialogue.responses[responseIndex];
				Add(candidates, dialogueResponse, WorkshopTextField.DialogueResponse, Read(_dialogueResponseTextField, dialogueResponse), dialogue, sourceLanguage, targetLanguage, localizationKey: $"Response_{responseIndex}");
			}
		}
		foreach (ObjectNPCAsset npc in list4)
		{
			if (IsSupportedAsset(npc))
			{
				Add(candidates, npc, WorkshopTextField.NpcCharacter, Read(_npcCharacterField, npc), npc, sourceLanguage, targetLanguage, localizationKey: "Character");
			}
		}
		foreach (QuestAsset quest in list5)
		{
			if (IsSupportedAsset(quest))
			{
				Add(candidates, quest, WorkshopTextField.QuestName, Read(_questNameField, quest), quest, sourceLanguage, targetLanguage, localizationKey: "Name");
				Add(candidates, quest, WorkshopTextField.QuestDescription, Read(_questDescriptionField, quest), quest, sourceLanguage, targetLanguage, localizationKey: "Description");
			}
		}
		foreach (ObjectAsset obj in list6)
		{
			if (IsSupportedAsset(obj))
			{
				Add(candidates, obj, WorkshopTextField.ObjectName, Read(_objectNameField, obj), obj, sourceLanguage, targetLanguage, localizationKey: "Name");
				Add(candidates, obj, WorkshopTextField.ObjectInteract, obj.interactabilityText, obj, sourceLanguage, targetLanguage, localizationKey: "Interact");
				Add(candidates, obj, WorkshopTextField.ObjectDialogueName, Read(_objectDialogueNameField, obj), obj, sourceLanguage, targetLanguage, localizationKey: "Dialogue_Name");
			}
		}
		foreach (VendorAsset vendor in list7)
		{
			if (IsSupportedAsset(vendor))
			{
				Add(candidates, vendor, WorkshopTextField.VendorName, Read(_vendorNameField, vendor), vendor, sourceLanguage, targetLanguage, localizationKey: "Name");
				Add(candidates, vendor, WorkshopTextField.VendorDescription, Read(_vendorDescriptionField, vendor), vendor, sourceLanguage, targetLanguage, localizationKey: "Description");
				if (vendor.buying != null)
				{
					for (int index = 0; index < vendor.buying.Length; index++)
					{
						VendorBuying buying = vendor.buying[index];
						Add(candidates, buying, WorkshopTextField.VendorBuyingDescription, Read(_vendorElementDescriptionField, buying), vendor, sourceLanguage, targetLanguage, localizationKey: $"Buying_{index}_Description");
					}
				}
				if (vendor.selling != null)
				{
					for (int index = 0; index < vendor.selling.Length; index++)
					{
						VendorSellingBase selling = vendor.selling[index];
						Add(candidates, selling, WorkshopTextField.VendorSellingDescription, Read(_vendorElementDescriptionField, selling), vendor, sourceLanguage, targetLanguage, localizationKey: $"Selling_{index}_Description");
					}
				}
			}
		}
		return new WorkshopLocalizationScanResult
		{
			Candidates = candidates,
			ItemCount = num,
			VehicleCount = num2
		};
	}

	private static bool IsSupportedAsset(Asset asset)
	{
		return asset != null && (AssetCatalog.IsOfficialAsset(asset) || AssetCatalog.IsWorkshopAsset(asset));
	}

	private void WarnUnavailableNpcFields()
	{
		if (_warnedUnavailable || (_dialoguePageTextField != null && _dialogueResponseTextField != null && _npcCharacterField != null && _questNameField != null && _questDescriptionField != null && _objectNameField != null && _objectDialogueNameField != null && _vendorNameField != null && _vendorDescriptionField != null && _vendorElementDescriptionField != null))
		{
			return;
		}
		_warnedUnavailable = true;
		_warn?.Invoke("Workshop NPC localization fields could not all be resolved; unavailable text domains will remain original.");
	}

	private static void Add(ICollection<WorkshopLocalizationCandidate> candidates, object instance, WorkshopTextField field, string source, Asset asset, string sourceLanguage, string targetLanguage, string actionKey = null, string localizationKey = null)
	{
		string text = WorkshopTextIdentity.NormalizeSource(source);
		if (HasTranslatableCharacters(text))
		{
			candidates.Add(new WorkshopLocalizationCandidate
			{
				Instance = instance,
				Field = field,
				SourceText = text,
				Key = string.IsNullOrWhiteSpace(localizationKey) ? WorkshopTextIdentity.CreateKey(sourceLanguage, targetLanguage, field, text) : WorkshopTextIdentity.CreateAssetKey(sourceLanguage, targetLanguage, field, asset?.GetType().Name, asset?.GUID.ToString("N"), localizationKey, text),
				AssetName = asset?.name,
				AssetGuid = asset?.GUID.ToString("N"),
				LegacyId = (asset?.id ?? 0),
				ActionKey = actionKey,
				AssetType = asset?.GetType().Name,
				LocalizationKey = localizationKey,
				SchemaVersion = string.IsNullOrWhiteSpace(localizationKey) ? WorkshopTextIdentity.SchemaVersion : WorkshopTextIdentity.AssetScopedSchemaVersion
			});
		}
	}

	private static string Read(FieldInfo field, object instance)
	{
		try
		{
			return field?.GetValue(instance) as string;
		}
		catch
		{
			return null;
		}
	}

	private static bool HasTranslatableCharacters(string text)
	{
		string text2 = text ?? string.Empty;
		for (int i = 0; i < text2.Length; i++)
		{
			if (char.IsLetter(text2[i]))
			{
				return true;
			}
		}
		return false;
	}
}
