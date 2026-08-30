using System;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Pure search-edit state: previews never overwrite the committed value.</summary>
internal sealed class UiSearchEditState
{
	internal UiSearchEditState(string value)
	{
		Committed = Normalize(value);
		Draft = Committed;
		Applied = Committed;
	}

	internal string Committed { get; private set; }
	internal string Draft { get; private set; }
	internal string Applied { get; private set; }

	internal void ChangeDraft(string value)
	{
		Draft = Normalize(value);
	}

	internal bool ApplyPreview(out string value)
	{
		value = Draft;
		if (string.Equals(Applied, Draft, StringComparison.Ordinal))
		{
			return false;
		}
		Applied = Draft;
		return true;
	}

	internal bool Commit(string value, out string committed)
	{
		Draft = Normalize(value);
		Committed = Draft;
		committed = Committed;
		if (string.Equals(Applied, Committed, StringComparison.Ordinal))
		{
			return false;
		}
		Applied = Committed;
		return true;
	}

	internal bool Revert(out string committed)
	{
		Draft = Committed;
		committed = Committed;
		if (string.Equals(Applied, Committed, StringComparison.Ordinal))
		{
			return false;
		}
		Applied = Committed;
		return true;
	}

	private static string Normalize(string value)
	{
		return value ?? string.Empty;
	}
}
