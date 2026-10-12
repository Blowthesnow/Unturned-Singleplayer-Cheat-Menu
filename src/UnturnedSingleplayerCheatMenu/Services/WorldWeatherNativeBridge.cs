using System;
using System.Reflection;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class WorldWeatherNativeBridge
{
	private static readonly FieldInfo ScheduledWeathersField = typeof(LightingManager).GetField(
		"schedulableWeathers", BindingFlags.Static | BindingFlags.NonPublic);
	private static readonly FieldInfo ShouldTickScheduledWeatherField = typeof(LightingManager).GetField(
		"shouldTickScheduledWeather", BindingFlags.Static | BindingFlags.NonPublic);

	internal static void RestoreNaturalWeather()
	{
		if (ScheduledWeathersField == null ||
			ScheduledWeathersField.FieldType != typeof(LevelAsset.SchedulableWeather[]))
			throw new MissingFieldException(typeof(LightingManager).FullName, "schedulableWeathers");
		if (ShouldTickScheduledWeatherField == null ||
			ShouldTickScheduledWeatherField.FieldType != typeof(bool))
			throw new MissingFieldException(typeof(LightingManager).FullName, "shouldTickScheduledWeather");

		LevelAsset.SchedulableWeather[] scheduledWeathers =
			(LevelAsset.SchedulableWeather[])ScheduledWeathersField.GetValue(null);
		LightingManager.ResetScheduledWeather();
		// ResetScheduledWeather clears the current event but leaves the scheduler gate
		// disabled after DisableWeather. Restore the exact native level-load condition.
		ShouldTickScheduledWeatherField.SetValue(null,
			scheduledWeathers != null && scheduledWeathers.Length != 0);
	}
}
