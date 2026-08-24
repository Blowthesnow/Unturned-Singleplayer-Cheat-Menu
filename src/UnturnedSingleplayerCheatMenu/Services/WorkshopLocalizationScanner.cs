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
		List<ItemAsset> list = new List<ItemAsset>();
		List<VehicleAsset> list2 = new List<VehicleAsset>();
		Assets.find(list);
		Assets.find(list2);
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
		return new WorkshopLocalizationScanResult
		{
			Candidates = candidates,
			ItemCount = num,
			VehicleCount = num2
		};
	}

	private static bool IsSupportedAsset(Asset asset)
	{
		if (asset != null)
		{
			if (asset.assetOrigin != EAssetOrigin.OFFICIAL)
			{
				return asset.assetOrigin == EAssetOrigin.WORKSHOP;
			}
			return true;
		}
		return false;
	}

	private static void Add(ICollection<WorkshopLocalizationCandidate> candidates, object instance, WorkshopTextField field, string source, Asset asset, string sourceLanguage, string targetLanguage, string actionKey = null)
	{
		string text = WorkshopTextIdentity.NormalizeSource(source);
		if (HasTranslatableCharacters(text))
		{
			candidates.Add(new WorkshopLocalizationCandidate
			{
				Instance = instance,
				Field = field,
				SourceText = text,
				Key = WorkshopTextIdentity.CreateKey(sourceLanguage, targetLanguage, field, text),
				AssetName = asset?.name,
				AssetGuid = asset?.GUID.ToString("N"),
				LegacyId = (asset?.id ?? 0),
				ActionKey = actionKey
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
