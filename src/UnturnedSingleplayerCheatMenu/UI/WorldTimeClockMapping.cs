using System;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Maps Unturned's dawn-based world cycle to a conventional 24-hour clock and back.</summary>
internal static class WorldTimeClockMapping
{
	internal const float DawnHour = 6f;

	internal static float GameNormalizedToClockHours(float normalized)
	{
		if (float.IsNaN(normalized) || float.IsInfinity(normalized))
		{
			return DawnHour;
		}
		return Repeat(normalized * 24f + DawnHour, 24f);
	}

	internal static float ClockHoursToGameNormalized(float hours)
	{
		if (float.IsNaN(hours) || float.IsInfinity(hours))
		{
			return 0f;
		}
		return Repeat(hours - DawnHour, 24f) / 24f;
	}

	internal static string FormatTwelveHour(float hours, bool english)
	{
		if (float.IsNaN(hours) || float.IsInfinity(hours))
		{
			hours = 0f;
		}
		int totalMinutes = (int)Math.Round(Repeat(hours, 24f) * 60f, MidpointRounding.AwayFromZero) % (24 * 60);
		int hour24 = totalMinutes / 60;
		int minute = totalMinutes % 60;
		int hour12 = hour24 % 12;
		if (hour12 == 0)
		{
			hour12 = 12;
		}
		if (english)
		{
			return $"{hour12}:{minute:00} {(hour24 < 12 ? "AM" : "PM")}";
		}
		return (hour24 < 12 ? "\u4E0A\u5348 " : "\u4E0B\u5348 ") + $"{hour12}:{minute:00}";
	}

	private static float Repeat(float value, float length)
	{
		float result = value % length;
		return result < 0f ? result + length : result;
	}
}
