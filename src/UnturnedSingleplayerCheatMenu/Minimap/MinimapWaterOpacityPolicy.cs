using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal static class MinimapWaterOpacityPolicy
{
	// FX/Water4 LOD 200: alpha = min(2 * fresnel + 0.5, 1).
	// At normal incidence fresnel approaches the _DistortParams.w bias.
	internal static float ResolveCaptureBias(string shaderName, int maximumLod, float bias)
	{
		if (!string.Equals(shaderName, "FX/Water4", StringComparison.Ordinal) ||
			maximumLod < 200 || maximumLod >= 300 || float.IsNaN(bias) ||
			float.IsInfinity(bias) || bias < 0f || bias > 1f) return bias;
		return Math.Max(0.25f, bias);
	}
}
