using System;
using System.Reflection;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class AssetOriginClassifier
{
	private static readonly FieldInfo AssetOriginField = typeof(Asset).GetField("origin", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo CoreOriginField = typeof(Assets).GetField("coreOrigin", BindingFlags.Static | BindingFlags.NonPublic);

	private static readonly FieldInfo LegacyOfficialOriginField = typeof(Assets).GetField("legacyOfficialOrigin", BindingFlags.Static | BindingFlags.NonPublic);

	public static bool IsWorkshop(Asset asset)
	{
		AssetOrigin origin = GetOrigin(asset);
		return origin != null && origin.workshopFileId != 0L;
	}

	public static bool IsOfficial(Asset asset)
	{
		AssetOrigin origin = GetOrigin(asset);
		return origin != null && (ReferenceEquals(origin, CoreOriginField?.GetValue(null))
			|| ReferenceEquals(origin, LegacyOfficialOriginField?.GetValue(null)));
	}

	private static AssetOrigin GetOrigin(Asset asset)
	{
		return asset == null ? null : AssetOriginField?.GetValue(asset) as AssetOrigin;
	}
}
