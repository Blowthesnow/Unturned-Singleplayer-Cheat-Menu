namespace UnturnedSingleplayerCheatMenu.Services;

internal static class ScopeTextureViewportMapper
{
	internal static bool TryMap(
		float sourceX,
		float sourceY,
		float screenWidth,
		float screenHeight,
		bool isSingleRender,
		out float textureX,
		out float textureY)
	{
		textureX = sourceX;
		textureY = sourceY;
		if (!IsFinite(sourceX) || !IsFinite(sourceY) || !IsFinite(screenWidth) || !IsFinite(screenHeight)
			|| screenWidth <= 0f || screenHeight <= 0f)
		{
			return false;
		}
		if (isSingleRender)
		{
			if (screenWidth >= screenHeight)
			{
				float scale = screenHeight / screenWidth;
				float offset = (1f - scale) * 0.5f;
				textureX = (sourceX - offset) / scale;
			}
			else
			{
				float scale = screenWidth / screenHeight;
				float offset = (1f - scale) * 0.5f;
				textureY = (sourceY - offset) / scale;
			}
		}
		return textureX >= 0f && textureX <= 1f && textureY >= 0f && textureY <= 1f;
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
