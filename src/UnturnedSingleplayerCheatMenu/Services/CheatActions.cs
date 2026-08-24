using System;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class CheatActions
{
	private readonly ManualLogSource _log;

	private uint _frozenTime;

	public bool GodModeEnabled { get; private set; }

	public bool InfiniteNeedsEnabled { get; private set; }

	public bool FreezeTimeEnabled { get; private set; }

	public CheatActions(ManualLogSource log)
	{
		_log = log;
	}

	public void SetGodMode(bool enabled)
	{
		GodModeEnabled = enabled;
		if (enabled)
		{
			RestoreEverything();
		}
	}

	public void SetInfiniteNeeds(bool enabled)
	{
		InfiniteNeedsEnabled = enabled;
		if (enabled)
		{
			RefillNeeds();
		}
	}

	public void SetFreezeTime(bool enabled)
	{
		FreezeTimeEnabled = enabled;
		_frozenTime = LightingManager.time;
		_log.LogInfo(enabled ? $"时间冻结已开启：游戏时间={_frozenTime}。" : "时间冻结已关闭。");
	}

	public void MaintainEnabledCheats()
	{
		if (SingleplayerGuard.IsReady)
		{
			if (GodModeEnabled)
			{
				RestoreEverything();
			}
			else if (InfiniteNeedsEnabled)
			{
				RefillNeeds();
			}
			if (FreezeTimeEnabled && LightingManager.time != _frozenTime)
			{
				LightingManager.time = _frozenTime;
			}
		}
	}

	public bool RestoreEverything()
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer?.life == null)
		{
			return false;
		}
		PlayerLife life = localPlayer.life;
		life.serverModifyHealth(100 - life.health);
		life.serverModifyFood(100 - life.food);
		life.serverModifyWater(100 - life.water);
		life.serverModifyVirus(100 - life.virus);
		life.serverModifyStamina(100 - life.stamina);
		life.askBreath((byte)(100 - life.oxygen));
		life.serverSetBleeding(newBleeding: false);
		life.serverSetLegsBroken(newLegsBroken: false);
		return true;
	}

	public bool RefillNeeds()
	{
		PlayerLife playerLife = Player.LocalPlayer?.life;
		if (playerLife == null)
		{
			return false;
		}
		playerLife.serverModifyFood(100 - playerLife.food);
		playerLife.serverModifyWater(100 - playerLife.water);
		playerLife.serverModifyVirus(100 - playerLife.virus);
		playerLife.serverModifyStamina(100 - playerLife.stamina);
		playerLife.askBreath((byte)(100 - playerLife.oxygen));
		return true;
	}

	public bool CureInjuries()
	{
		PlayerLife playerLife = Player.LocalPlayer?.life;
		if (playerLife == null)
		{
			return false;
		}
		playerLife.serverSetBleeding(newBleeding: false);
		playerLife.serverSetLegsBroken(newLegsBroken: false);
		return true;
	}

	public bool SetLifeStats(int health, int food, int water, int virus, int stamina, int oxygen)
	{
		PlayerLife playerLife = Player.LocalPlayer?.life;
		if (playerLife == null)
		{
			return false;
		}
		health = Mathf.Clamp(health, 1, 100);
		food = Mathf.Clamp(food, 0, 100);
		water = Mathf.Clamp(water, 0, 100);
		virus = Mathf.Clamp(virus, 0, 100);
		stamina = Mathf.Clamp(stamina, 0, 100);
		oxygen = Mathf.Clamp(oxygen, 0, 100);
		playerLife.serverModifyHealth(health - playerLife.health);
		playerLife.serverModifyFood(food - playerLife.food);
		playerLife.serverModifyWater(water - playerLife.water);
		playerLife.serverModifyVirus(virus - playerLife.virus);
		playerLife.serverModifyStamina(stamina - playerLife.stamina);
		playerLife.simulatedModifyOxygen(oxygen - playerLife.oxygen);
		return true;
	}

	public bool AddExperience(uint amount)
	{
		PlayerSkills playerSkills = Player.LocalPlayer?.skills;
		if (playerSkills == null)
		{
			return false;
		}
		playerSkills.askAward(amount);
		return true;
	}

	public bool AddReputation(int amount)
	{
		PlayerSkills playerSkills = Player.LocalPlayer?.skills;
		if (playerSkills == null)
		{
			return false;
		}
		playerSkills.askRep(amount);
		return true;
	}

	public int MaxAllSkills()
	{
		PlayerSkills playerSkills = Player.LocalPlayer?.skills;
		if (playerSkills?.skills == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < playerSkills.skills.Length; i++)
		{
			Skill[] array = playerSkills.skills[i];
			for (int j = 0; j < array.Length; j++)
			{
				if (playerSkills.ServerSetSkillLevel(i, j, array[j].max))
				{
					num++;
				}
			}
		}
		return num;
	}

	public bool GiveItem(ItemAsset asset, byte amount)
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null || asset == null || amount == 0)
		{
			return false;
		}
		return ItemTool.tryForceGiveItem(localPlayer, asset.id, amount);
	}

	public int SpawnVehicles(VehicleAsset asset, int amount)
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null || asset == null)
		{
			return 0;
		}
		amount = Mathf.Clamp(amount, 1, 20);
		if (amount == 1)
		{
			return (VehicleTool.SpawnVehicleForPlayer(localPlayer, asset) != null) ? 1 : 0;
		}
		int num = 0;
		Quaternion rotation = localPlayer.transform.rotation;
		for (int i = 0; i < amount; i++)
		{
			int num2 = i / 4;
			int num3 = i % 4;
			int num4 = Math.Min(4, amount - num2 * 4);
			float num5 = (float)num3 - (float)(num4 - 1) * 0.5f;
			Vector3 vector = localPlayer.transform.position + localPlayer.transform.forward * (8f + (float)num2 * 9f) + localPlayer.transform.right * (num5 * 7f);
			if (Physics.Raycast(vector + Vector3.up * 16f, Vector3.down, out var hitInfo, 32f, RayMasks.BLOCK_VEHICLE) && hitInfo.collider != null)
			{
				vector.y = hitInfo.point.y + 16f;
			}
			if (VehicleManager.spawnVehicleV2(asset, vector, rotation) != null)
			{
				num++;
			}
		}
		return num;
	}

	public bool Teleport(TeleportPoint point)
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null || point == null)
		{
			_log.LogWarning("传送失败：玩家或传送点不可用。");
			return false;
		}
		if (!string.Equals(point.Map, Provider.map, StringComparison.OrdinalIgnoreCase))
		{
			_log.LogWarning("传送失败：传送点“" + point.Name + "”属于地图 " + point.Map + "，当前地图为 " + Provider.map + "。");
			return false;
		}
		if (localPlayer.movement?.getVehicle() != null)
		{
			_log.LogWarning("传送失败：使用传送点“" + point.Name + "”前需要离开车辆。");
			return false;
		}
		Vector3 vector = ResolveTeleportPointTarget(point.Position);
		bool flag = localPlayer.teleportToLocation(vector, point.Yaw);
		if (flag)
		{
			_log.LogInfo($"已传送到“{point.Name}”：地图={point.Map}，位置={vector}，朝向={point.Yaw:F1}。");
		}
		else
		{
			_log.LogWarning("传送失败：游戏拒绝了传送点“" + point.Name + "”。");
		}
		return flag;
	}

	private Vector3 ResolveTeleportPointTarget(Vector3 savedPosition)
	{
		Player localPlayer = Player.LocalPlayer;
		int layerMask = (RayMasks.BLOCK_STANCE | 0x4000000) & -2097153 & -16385;
		Vector3 point = savedPosition + Vector3.up * 0.05f;
		Vector3 point2 = savedPosition + Vector3.up * 1.75f;
		Collider[] array = Physics.OverlapCapsule(point, point2, 0.35f, layerMask, QueryTriggerInteraction.Ignore);
		float num = savedPosition.y;
		bool flag = false;
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			if (!(collider == null) && (!(localPlayer != null) || !(collider.GetComponentInParent<Player>() == localPlayer)) && !(collider.bounds.size.y > 64f) && !(collider.bounds.max.y <= savedPosition.y + 0.05f))
			{
				flag = true;
				num = Mathf.Max(num, collider.bounds.max.y);
			}
		}
		if (!flag)
		{
			return savedPosition;
		}
		return new Vector3(savedPosition.x, num + 0.05f, savedPosition.z);
	}

	public bool TeleportToMapPosition(Vector3 horizontalPosition)
	{
		Vector3 landingPoint;
		return TeleportToMapPosition(horizontalPosition, out landingPoint);
	}

	internal bool TeleportToMapPosition(Vector3 horizontalPosition, out Vector3 landingPoint)
	{
		landingPoint = horizontalPosition;
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null)
		{
			_log.LogWarning("地图传送失败：玩家不可用。");
			return false;
		}
		if (!SingleplayerGuard.IsReady)
		{
			_log.LogWarning("地图传送失败：当前不是可用的单人世界。");
			return false;
		}
		if (localPlayer.movement?.getVehicle() != null)
		{
			_log.LogWarning("地图传送失败：使用地图传送前需要离开车辆。");
			return false;
		}
		if (!TryFindSafeLandingPoint(horizontalPosition, out landingPoint))
		{
			_log.LogWarning($"地图传送失败：未找到安全落点，水平坐标={horizontalPosition}。");
			return false;
		}
		float y = localPlayer.transform.rotation.eulerAngles.y;
		bool num = localPlayer.teleportToLocation(landingPoint, y);
		if (num)
		{
			_log.LogInfo($"地图传送成功：地图={Provider.map}，目标={landingPoint}，朝向={y:F1}。");
			return num;
		}
		_log.LogWarning($"地图传送失败：游戏拒绝了目标位置 {landingPoint}。");
		return num;
	}

	private bool TryFindSafeLandingPoint(Vector3 horizontalPosition, out Vector3 landingPoint)
	{
		landingPoint = horizontalPosition;
		horizontalPosition.y = 0f;
		if (TryFindSafeLandingPointAt(horizontalPosition, out landingPoint))
		{
			return true;
		}
		float[] array = new float[2] { 1.5f, 3f };
		foreach (float num in array)
		{
			for (int j = 0; j < 8; j++)
			{
				float f = (float)j * MathF.PI * 2f / 8f;
				Vector3 horizontalPosition2 = horizontalPosition + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
				if (TryFindSafeLandingPointAt(horizontalPosition2, out landingPoint))
				{
					_log.LogInfo($"地图传送目标被占用，已使用附近安全落点：请求={horizontalPosition}，落点={landingPoint}。");
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryValidateLandingPoint(Vector3 candidate, Player player, out Vector3 landingPoint)
	{
		landingPoint = candidate;
		if (Level.checkSafeIncludingClipVolumes(candidate))
		{
			if (!(player?.stance == null))
			{
				return player.stance.wouldHaveHeightClearanceAtPosition(candidate, 0.5f);
			}
			return true;
		}
		return false;
	}

	private static bool TryValidateSurfaceLandingPoint(Vector3 surfacePoint, Player player, out Vector3 landingPoint)
	{
		float[] array = new float[3] { 0.05f, 0.15f, 0.3f };
		foreach (float num in array)
		{
			if (TryValidateLandingPoint(surfacePoint + Vector3.up * num, player, out landingPoint))
			{
				return true;
			}
		}
		landingPoint = surfacePoint;
		return false;
	}

	private bool TryFindSafeLandingPointAt(Vector3 horizontalPosition, out Vector3 landingPoint)
	{
		landingPoint = horizontalPosition;
		horizontalPosition.y = 0f;
		Player localPlayer = Player.LocalPlayer;
		float num = Mathf.Max(Level.HEIGHT + 64f, ((object)localPlayer != null) ? (localPlayer.transform.position.y + 128f) : (Level.HEIGHT + 64f));
		int layerMask = RayMasks.BLOCK_STANCE & -2097153 & -16385 & -67108865;
		RaycastHit[] array = Physics.RaycastAll(new Vector3(horizontalPosition.x, num, horizontalPosition.z), Vector3.down, num + 256f, layerMask, QueryTriggerInteraction.Ignore);
		Array.Sort(array, (RaycastHit left, RaycastHit right) => right.point.y.CompareTo(left.point.y));
		RaycastHit[] array2 = array;
		for (int num2 = 0; num2 < array2.Length; num2++)
		{
			RaycastHit raycastHit = array2[num2];
			if (!(raycastHit.collider == null) && (!(localPlayer != null) || !(raycastHit.collider.GetComponentInParent<Player>() == localPlayer)) && TryValidateSurfaceLandingPoint(raycastHit.point, localPlayer, out landingPoint))
			{
				return true;
			}
		}
		try
		{
			float height = LevelGround.getHeight(horizontalPosition);
			if (TryValidateSurfaceLandingPoint(new Vector3(horizontalPosition.x, height, horizontalPosition.z), localPlayer, out landingPoint))
			{
				return true;
			}
		}
		catch (Exception arg)
		{
			_log.LogWarning($"计算地图地表高度失败：{horizontalPosition}\n{arg}");
		}
		return false;
	}

	public void SetDay()
	{
		LightingManager.time = (uint)((float)LightingManager.cycle * LevelLighting.transition);
		CaptureFrozenTime();
		_log.LogInfo($"已设置为白天：游戏时间={LightingManager.time}。");
	}

	public void SetNight()
	{
		LightingManager.time = (uint)((float)LightingManager.cycle * (LevelLighting.bias + LevelLighting.transition));
		CaptureFrozenTime();
		_log.LogInfo($"已设置为夜晚：游戏时间={LightingManager.time}。");
	}

	public void SetTime(uint value)
	{
		LightingManager.time = value;
		CaptureFrozenTime();
		_log.LogInfo($"已设置游戏时间：{LightingManager.time}。");
	}

	public void SetFullMoon(bool enabled)
	{
		LightingManager.isFullMoon = enabled;
		_log.LogInfo(enabled ? "强制满月已开启。" : "强制满月已关闭。");
	}

	public bool CallAirdrop()
	{
		try
		{
			AirdropDevkitNode randomAirdropNode = LevelManager.GetRandomAirdropNode();
			if (randomAirdropNode == null)
			{
				_log.LogWarning("呼叫空投未执行：当前地图没有可用的空投节点或货物表。");
				return false;
			}
			LevelManager.SpawnAirdropAtNode(randomAirdropNode);
			_log.LogInfo("已请求游戏在随机空投节点生成空投。");
			return true;
		}
		catch (Exception arg)
		{
			_log.LogError($"呼叫空投失败：{arg}");
			return false;
		}
	}

	public bool StartRain()
	{
		return StartWeather(WeatherAssetBase.DEFAULT_RAIN.Find(), "雨天");
	}

	public bool StartSnow()
	{
		return StartWeather(WeatherAssetBase.DEFAULT_SNOW.Find(), "暴雪");
	}

	public void ClearWeather()
	{
		LightingManager.ResetScheduledWeather();
		_log.LogInfo("已清除当前天气；自然天气调度仍可继续。");
	}

	public void DisableWeather()
	{
		LightingManager.DisableWeather();
		_log.LogInfo("已关闭当前天气和自然天气调度。");
	}

	private bool StartWeather(WeatherAssetBase weather, string label)
	{
		if (weather == null)
		{
			_log.LogWarning("触发" + label + "未执行：当前地图没有对应的默认天气资产。");
			return false;
		}
		bool flag = LightingManager.ForecastWeatherImmediately(weather);
		if (!flag)
		{
			LightingManager.ActivatePerpetualWeather(weather);
		}
		_log.LogInfo(flag ? ("已请求立即触发" + label + "。") : ("立即天气调度不可用，已改为持续触发" + label + "。"));
		return true;
	}

	private void CaptureFrozenTime()
	{
		if (FreezeTimeEnabled)
		{
			_frozenTime = LightingManager.time;
		}
	}
}
