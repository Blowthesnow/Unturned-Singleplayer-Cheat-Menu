using System;
using System.Globalization;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

/// <summary>Byte-stable semantic color copied from ControlGallery.Ugui 1.1.0.</summary>
internal readonly struct RgbaColor : IEquatable<RgbaColor>
{
	internal RgbaColor(byte red, byte green, byte blue, byte alpha = 255)
	{
		Red = red;
		Green = green;
		Blue = blue;
		Alpha = alpha;
	}

	internal byte Red { get; }
	internal byte Green { get; }
	internal byte Blue { get; }
	internal byte Alpha { get; }

	internal static RgbaColor Parse(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A color value is required.", nameof(value));
		}

		string hex = value.Trim();
		if (hex[0] == '#')
		{
			hex = hex.Substring(1);
		}
		if (hex.Length != 6 && hex.Length != 8)
		{
			throw new FormatException("Colors must use #RRGGBB or #RRGGBBAA.");
		}

		return new RgbaColor(
			ParseByte(hex, 0),
			ParseByte(hex, 2),
			ParseByte(hex, 4),
			hex.Length == 8 ? ParseByte(hex, 6) : (byte)255);
	}

	internal RgbaColor WithAlpha(byte alpha) => new RgbaColor(Red, Green, Blue, alpha);

	public bool Equals(RgbaColor other) =>
		Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha;

	public override bool Equals(object obj) => obj is RgbaColor other && Equals(other);

	public override int GetHashCode()
	{
		unchecked
		{
			int hash = Red;
			hash = (hash * 397) ^ Green;
			hash = (hash * 397) ^ Blue;
			hash = (hash * 397) ^ Alpha;
			return hash;
		}
	}

	public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}";

	private static byte ParseByte(string value, int startIndex) =>
		byte.Parse(value.Substring(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
