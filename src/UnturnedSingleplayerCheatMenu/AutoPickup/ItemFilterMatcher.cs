using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal sealed class ItemFilterMatcher
{
	private sealed class RuleIndex
	{
		public readonly HashSet<string> AssetIds = new HashSet<string>(StringComparer.Ordinal);

		public void Add(string value)
		{
			AssetIds.Add(value);
		}

		public bool Matches(AutoPickupItemDescriptor item)
		{
			return AssetIds.Contains(item.AssetId.ToString(CultureInfo.InvariantCulture));
		}
	}

	private RuleIndex _allow = new RuleIndex();

	private RuleIndex _deny = new RuleIndex();

	private bool _whitelistEnabled;

	public AutoPickupFilterMode Mode { get; private set; } = AutoPickupFilterMode.BlacklistOnly;

	public int Version { get; private set; }

	public AutoPickupRuleLoadResult Replace(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules, out IReadOnlyList<ItemFilterRule> acceptedRules)
	{
		List<ItemFilterRule> accepted = new List<ItemFilterRule>();
		RuleIndex allow = new RuleIndex();
		RuleIndex deny = new RuleIndex();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		int rejected = 0;
		foreach (ItemFilterRule input in rules ?? Enumerable.Empty<ItemFilterRule>())
		{
			if (!TryNormalize(input, out ItemFilterRule normalized, out string value))
			{
				rejected++;
				continue;
			}
			string key = normalized.Action + "\u001f" + normalized.Field + "\u001f" + value;
			if (!seen.Add(key))
			{
				rejected++;
				continue;
			}
			accepted.Add(normalized);
			if (normalized.Action == ItemFilterRuleAction.Allow)
			{
				allow.Add(value);
			}
			else
			{
				deny.Add(value);
			}
		}
		_whitelistEnabled = enabled && (mode == AutoPickupFilterMode.WhitelistOnly || mode == AutoPickupFilterMode.WhitelistThenBlacklist);
		Mode = _whitelistEnabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly;
		_allow = allow;
		_deny = deny;
		Version++;
		acceptedRules = accepted;
		return new AutoPickupRuleLoadResult(accepted.Count, rejected, string.Empty);
	}

	public bool Matches(AutoPickupItemDescriptor item)
	{
		if (item == null)
		{
			return false;
		}
		return (!_whitelistEnabled || _allow.Matches(item)) && !_deny.Matches(item);
	}

	private static bool TryNormalize(ItemFilterRule input, out ItemFilterRule normalized, out string value)
	{
		normalized = null;
		value = Normalize(input?.Value);
		if (input == null || !input.Enabled || value.Length == 0)
		{
			return false;
		}
		if ((input.Action != ItemFilterRuleAction.Allow && input.Action != ItemFilterRuleAction.Deny) || input.Field != ItemFilterRuleField.AssetId)
		{
			return false;
		}
		if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
		{
			return false;
		}
		normalized = input.Clone();
		normalized.Id = string.IsNullOrWhiteSpace(input.Id) ? System.Guid.NewGuid().ToString("N") : input.Id.Trim();
		normalized.Value = value;
		return true;
	}

	internal static string Normalize(string value)
	{
		return (value ?? string.Empty).Trim().ToLowerInvariant();
	}

}
