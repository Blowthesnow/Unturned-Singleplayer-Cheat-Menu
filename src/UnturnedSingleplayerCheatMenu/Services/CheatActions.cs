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

    public CheatActions(ManualLogSource log)
    {
        _log = log;
    }

    public bool GodModeEnabled { get; private set; }
    public bool InfiniteNeedsEnabled { get; private set; }
    public bool FreezeTimeEnabled { get; private set; }

    public void SetGodMode(bool enabled)
    {
        GodModeEnabled = enabled;
        if (enabled)
            RestoreEverything();
    }

    public void SetInfiniteNeeds(bool enabled)
    {
        InfiniteNeedsEnabled = enabled;
        if (enabled)
            RefillNeeds();
    }

    public void SetFreezeTime(bool enabled)
    {
        FreezeTimeEnabled = enabled;
        _frozenTime = LightingManager.time;
        _log.LogInfo(enabled
            ? $"时间冻结已开启：游戏时间={_frozenTime}。"
            : "时间冻结已关闭。");
    }

    public void MaintainEnabledCheats()
    {
        if (!SingleplayerGuard.IsReady)
            return;

        if (GodModeEnabled)
            RestoreEverything();
        else if (InfiniteNeedsEnabled)
            RefillNeeds();

        if (FreezeTimeEnabled && LightingManager.time != _frozenTime)
            LightingManager.time = _frozenTime;
    }

    public bool RestoreEverything()
    {
        Player player = Player.LocalPlayer;
        if (player?.life == null)
            return false;

        PlayerLife life = player.life;
        life.serverModifyHealth(100 - life.health);
        life.serverModifyFood(100 - life.food);
        life.serverModifyWater(100 - life.water);
        life.serverModifyVirus(100 - life.virus);
        life.serverModifyStamina(100 - life.stamina);
        life.askBreath((byte)(100 - life.oxygen));
        life.serverSetBleeding(false);
        life.serverSetLegsBroken(false);
        return true;
    }

    public bool RefillNeeds()
    {
        PlayerLife life = Player.LocalPlayer?.life;
        if (life == null)
            return false;

        life.serverModifyFood(100 - life.food);
        life.serverModifyWater(100 - life.water);
        life.serverModifyVirus(100 - life.virus);
        life.serverModifyStamina(100 - life.stamina);
        life.askBreath((byte)(100 - life.oxygen));
        return true;
    }

    public bool CureInjuries()
    {
        PlayerLife life = Player.LocalPlayer?.life;
        if (life == null)
            return false;
        life.serverSetBleeding(false);
        life.serverSetLegsBroken(false);
        return true;
    }

    public bool SetLifeStats(int health, int food, int water, int virus, int stamina, int oxygen)
    {
        PlayerLife life = Player.LocalPlayer?.life;
        if (life == null)
            return false;

        health = Mathf.Clamp(health, 1, 100);
        food = Mathf.Clamp(food, 0, 100);
        water = Mathf.Clamp(water, 0, 100);
        virus = Mathf.Clamp(virus, 0, 100);
        stamina = Mathf.Clamp(stamina, 0, 100);
        oxygen = Mathf.Clamp(oxygen, 0, 100);
        life.serverModifyHealth(health - life.health);
        life.serverModifyFood(food - life.food);
        life.serverModifyWater(water - life.water);
        life.serverModifyVirus(virus - life.virus);
        life.serverModifyStamina(stamina - life.stamina);
        life.simulatedModifyOxygen(oxygen - life.oxygen);
        return true;
    }

    public bool AddExperience(uint amount)
    {
        PlayerSkills skills = Player.LocalPlayer?.skills;
        if (skills == null)
            return false;
        skills.askAward(amount);
        return true;
    }

    public bool AddReputation(int amount)
    {
        PlayerSkills skills = Player.LocalPlayer?.skills;
        if (skills == null)
            return false;
        skills.askRep(amount);
        return true;
    }

    public int MaxAllSkills()
    {
        PlayerSkills playerSkills = Player.LocalPlayer?.skills;
        if (playerSkills?.skills == null)
            return 0;

        int changed = 0;
        for (int speciality = 0; speciality < playerSkills.skills.Length; speciality++)
        {
            Skill[] skills = playerSkills.skills[speciality];
            for (int index = 0; index < skills.Length; index++)
            {
                if (playerSkills.ServerSetSkillLevel(speciality, index, skills[index].max))
                    changed++;
            }
        }
        return changed;
    }

    public bool GiveItem(ItemAsset asset, byte amount)
    {
        Player player = Player.LocalPlayer;
        if (player == null || asset == null || amount == 0)
            return false;
        return ItemTool.tryForceGiveItem(player, asset.id, amount);
    }

    public int SpawnVehicles(VehicleAsset asset, int amount)
    {
        Player player = Player.LocalPlayer;
        if (player == null || asset == null)
            return 0;

        amount = Mathf.Clamp(amount, 1, 20);
        if (amount == 1)
            return VehicleTool.SpawnVehicleForPlayer(player, asset) != null ? 1 : 0;

        const int columns = 4;
        const float lateralSpacing = 7f;
        const float forwardSpacing = 9f;
        int spawned = 0;
        Quaternion rotation = player.transform.rotation;

        for (int index = 0; index < amount; index++)
        {
            int row = index / columns;
            int column = index % columns;
            int columnsInRow = Math.Min(columns, amount - row * columns);
            float centeredColumn = column - (columnsInRow - 1) * 0.5f;
            Vector3 point = player.transform.position
                + player.transform.forward * (8f + row * forwardSpacing)
                + player.transform.right * (centeredColumn * lateralSpacing);

            if (Physics.Raycast(point + Vector3.up * 16f, Vector3.down, out RaycastHit hit, 32f, RayMasks.BLOCK_VEHICLE)
                && hit.collider != null)
            {
                point.y = hit.point.y + 16f;
            }

            if (VehicleManager.spawnVehicleV2(asset, point, rotation) != null)
                spawned++;
        }

        return spawned;
    }

    public bool Teleport(TeleportPoint point)
    {
        Player player = Player.LocalPlayer;
        if (player == null || point == null)
        {
            _log.LogWarning("传送失败：玩家或传送点不可用。");
            return false;
        }
        if (!string.Equals(point.Map, Provider.map, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning($"传送失败：传送点“{point.Name}”属于地图 {point.Map}，当前地图为 {Provider.map}。");
            return false;
        }
        if (player.movement?.getVehicle() != null)
        {
            _log.LogWarning($"传送失败：使用传送点“{point.Name}”前需要离开车辆。");
            return false;
        }

        Vector3 landingPoint = ResolveTeleportPointTarget(point.Position);

        bool success = player.teleportToLocation(landingPoint, point.Yaw);
        if (success)
            _log.LogInfo($"已传送到“{point.Name}”：地图={point.Map}，位置={landingPoint}，朝向={point.Yaw:F1}。");
        else
            _log.LogWarning($"传送失败：游戏拒绝了传送点“{point.Name}”。");
        return success;
    }

    private Vector3 ResolveTeleportPointTarget(Vector3 savedPosition)
    {
        Player player = Player.LocalPlayer;
        int obstructionMask = (RayMasks.BLOCK_STANCE | RayMasks.VEHICLE)
            & ~RayMasks.CLIP
            & ~RayMasks.RESOURCE;
        Vector3 capsuleBottom = savedPosition + Vector3.up * 0.05f;
        Vector3 capsuleTop = savedPosition + Vector3.up * 1.75f;
        Collider[] colliders = Physics.OverlapCapsule(
            capsuleBottom,
            capsuleTop,
            0.35f,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        float highestObstacleTop = savedPosition.y;
        bool foundObstacle = false;
        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;
            if (player != null && collider.GetComponentInParent<Player>() == player)
                continue;

            // Large-height terrain/ground colliders are not an obstruction. Vehicles,
            // structures, barricades, and storage objects have bounded mesh bounds.
            if (collider.bounds.size.y > 64f)
                continue;
            if (collider.bounds.max.y <= savedPosition.y + 0.05f)
                continue;

            foundObstacle = true;
            highestObstacleTop = Mathf.Max(highestObstacleTop, collider.bounds.max.y);
        }

        if (!foundObstacle)
            return savedPosition;

        return new Vector3(savedPosition.x, highestObstacleTop + 0.05f, savedPosition.z);
    }

    public bool TeleportToMapPosition(Vector3 horizontalPosition)
    {
        return TeleportToMapPosition(horizontalPosition, out _);
    }

    internal bool TeleportToMapPosition(Vector3 horizontalPosition, out Vector3 landingPoint)
    {
        landingPoint = horizontalPosition;
        Player player = Player.LocalPlayer;
        if (player == null)
        {
            _log.LogWarning("地图传送失败：玩家不可用。");
            return false;
        }
        if (!SingleplayerGuard.IsReady)
        {
            _log.LogWarning("地图传送失败：当前不是可用的单人世界。");
            return false;
        }
        if (player.movement?.getVehicle() != null)
        {
            _log.LogWarning("地图传送失败：使用地图传送前需要离开车辆。");
            return false;
        }
        if (!TryFindSafeLandingPoint(horizontalPosition, out landingPoint))
        {
            _log.LogWarning($"地图传送失败：未找到安全落点，水平坐标={horizontalPosition}。");
            return false;
        }

        float yaw = player.transform.rotation.eulerAngles.y;
        bool success = player.teleportToLocation(landingPoint, yaw);
        if (success)
            _log.LogInfo($"地图传送成功：地图={Provider.map}，目标={landingPoint}，朝向={yaw:F1}。");
        else
            _log.LogWarning($"地图传送失败：游戏拒绝了目标位置 {landingPoint}。");
        return success;
    }

    private bool TryFindSafeLandingPoint(Vector3 horizontalPosition, out Vector3 landingPoint)
    {
        landingPoint = horizontalPosition;
        horizontalPosition.y = 0f;
        if (TryFindSafeLandingPointAt(horizontalPosition, out landingPoint))
            return true;

        foreach (float radius in new[] { 1.5f, 3f })
        {
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * 2f / 8f;
                Vector3 nearbyPosition = horizontalPosition
                    + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (TryFindSafeLandingPointAt(nearbyPosition, out landingPoint))
                {
                    _log.LogInfo(
                        $"地图传送目标被占用，已使用附近安全落点：请求={horizontalPosition}，落点={landingPoint}。" );
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryValidateLandingPoint(
        Vector3 candidate,
        Player player,
        out Vector3 landingPoint)
    {
        landingPoint = candidate;
        return Level.checkSafeIncludingClipVolumes(candidate)
            && (player?.stance == null
                || player.stance.wouldHaveHeightClearanceAtPosition(candidate, 0.5f));
    }

    private static bool TryValidateSurfaceLandingPoint(
        Vector3 surfacePoint,
        Player player,
        out Vector3 landingPoint)
    {
        foreach (float offset in new[] { 0.05f, 0.15f, 0.3f })
        {
            Vector3 candidate = surfacePoint + Vector3.up * offset;
            if (TryValidateLandingPoint(candidate, player, out landingPoint))
                return true;
        }

        landingPoint = surfacePoint;
        return false;
    }

    private bool TryFindSafeLandingPointAt(Vector3 horizontalPosition, out Vector3 landingPoint)
    {
        landingPoint = horizontalPosition;
        horizontalPosition.y = 0f;
        Player player = Player.LocalPlayer;
        float rayStart = Mathf.Max(
            Level.HEIGHT + 64f,
            player?.transform.position.y + 128f ?? Level.HEIGHT + 64f);
        int landingMask = RayMasks.BLOCK_STANCE
            & ~RayMasks.CLIP
            & ~RayMasks.RESOURCE
            & ~RayMasks.VEHICLE;
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(horizontalPosition.x, rayStart, horizontalPosition.z),
            Vector3.down,
            rayStart + 256f,
            landingMask,
            QueryTriggerInteraction.Ignore);

        Array.Sort(hits, (left, right) => right.point.y.CompareTo(left.point.y));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;
            if (player != null && hit.collider.GetComponentInParent<Player>() == player)
                continue;
            if (TryValidateSurfaceLandingPoint(hit.point, player, out landingPoint))
                return true;
        }

        try
        {
            float groundHeight = LevelGround.getHeight(horizontalPosition);
            Vector3 surfacePoint = new(horizontalPosition.x, groundHeight, horizontalPosition.z);
            if (TryValidateSurfaceLandingPoint(surfacePoint, player, out landingPoint))
                return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning($"计算地图地表高度失败：{horizontalPosition}\n{ex}");
        }

        return false;
    }

    public void SetDay()
    {
        LightingManager.time = (uint)(LightingManager.cycle * LevelLighting.transition);
        CaptureFrozenTime();
        _log.LogInfo($"已设置为白天：游戏时间={LightingManager.time}。");
    }

    public void SetNight()
    {
        LightingManager.time = (uint)(LightingManager.cycle * (LevelLighting.bias + LevelLighting.transition));
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
            AirdropDevkitNode node = LevelManager.GetRandomAirdropNode();
            if (node == null)
            {
                _log.LogWarning("呼叫空投未执行：当前地图没有可用的空投节点或货物表。");
                return false;
            }
            LevelManager.SpawnAirdropAtNode(node);
            _log.LogInfo("已请求游戏在随机空投节点生成空投。");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"呼叫空投失败：{ex}");
            return false;
        }
    }

    public bool StartRain() => StartWeather(WeatherAssetBase.DEFAULT_RAIN.Find(), "雨天");
    public bool StartSnow() => StartWeather(WeatherAssetBase.DEFAULT_SNOW.Find(), "暴雪");

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
            _log.LogWarning($"触发{label}未执行：当前地图没有对应的默认天气资产。");
            return false;
        }

        bool forecasted = LightingManager.ForecastWeatherImmediately(weather);
        if (!forecasted)
            LightingManager.ActivatePerpetualWeather(weather);
        _log.LogInfo(forecasted
            ? $"已请求立即触发{label}。"
            : $"立即天气调度不可用，已改为持续触发{label}。");
        return true;
    }

    private void CaptureFrozenTime()
    {
        if (FreezeTimeEnabled)
            _frozenTime = LightingManager.time;
    }
}
