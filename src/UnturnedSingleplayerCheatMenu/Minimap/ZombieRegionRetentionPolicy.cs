using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal static class ZombieRegionRetentionPolicy
{
	internal const int MaxFarRetainedRegions = 8;
	internal const float ProtectionRadiusPaddingMeters = 16f;

	internal static bool IsEnabled(bool singleplayerReady, bool minimapEnabled, bool showZombies)
	{
		return singleplayerReady && minimapEnabled && showZombies;
	}

	internal static bool ShouldSuppressBoundExitDestroy(bool retentionEnabled, bool insideBoundUpdate,
		int existingZombieCount)
	{
		return retentionEnabled && insideBoundUpdate && existingZombieCount > 0;
	}

	internal static bool ShouldReuseExistingRegion(bool retentionEnabled, bool insideBoundUpdate,
		int existingZombieCount)
	{
		return retentionEnabled && insideBoundUpdate && existingZombieCount > 0;
	}

	internal static float ResolveProtectionRadius(float candidateScanRadius)
	{
		if (float.IsNaN(candidateScanRadius) || float.IsInfinity(candidateScanRadius) || candidateScanRadius < 0f)
		{
			candidateScanRadius = 0f;
		}
		return candidateScanRadius + ProtectionRadiusPaddingMeters;
	}

	internal static bool IsInsideProtectionRadius(float deltaX, float deltaZ, float protectionRadius)
	{
		if (float.IsNaN(protectionRadius) || float.IsInfinity(protectionRadius) || protectionRadius < 0f)
		{
			return false;
		}
		double x = deltaX;
		double z = deltaZ;
		double radius = protectionRadius;
		return x * x + z * z <= radius * radius;
	}

	internal static bool CanRecycleRegion(bool trackedByPlugin, bool isNetworked, int playerCount,
		bool hasLiveZombieInsideProtectionRadius)
	{
		return trackedByPlugin && !isNetworked && playerCount <= 0 && !hasLiveZombieInsideProtectionRadius;
	}

	internal static int ResolveFarRecycleCount(int farRetainedCount,
		int maxFarRetainedRegions = MaxFarRetainedRegions)
	{
		return Math.Max(0, farRetainedCount - Math.Max(0, maxFarRetainedRegions));
	}
}
