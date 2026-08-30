namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>
/// Separates the stable family name consumed by Unity from the localized label shown in the UI.
/// </summary>
internal sealed class SystemFontFamilyOption
{
	internal SystemFontFamilyOption(string familyName, string displayName)
	{
		FamilyName = familyName;
		DisplayName = displayName;
	}

	internal string FamilyName { get; }

	internal string DisplayName { get; }
}
