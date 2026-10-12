using System;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal enum WorldScenarioKind
{
	ClearNoon,
	FullMoonNight,
	SnowstormNight,
	RestoreNatural
}

internal sealed class CharacterPageSnapshot
{
	internal bool IsReady { get; }
	internal byte Health { get; }
	internal byte Food { get; }
	internal byte Water { get; }
	internal byte Virus { get; }
	internal byte Stamina { get; }
	internal byte Oxygen { get; }
	internal uint Experience { get; }
	internal int Reputation { get; }
	internal bool GodModeEnabled { get; }
	internal bool InfiniteNeedsEnabled { get; }

	internal CharacterPageSnapshot(bool isReady, byte health, byte food, byte water, byte virus, byte stamina, byte oxygen,
		uint experience, int reputation, bool godModeEnabled, bool infiniteNeedsEnabled)
	{
		IsReady = isReady;
		Health = health;
		Food = food;
		Water = water;
		Virus = virus;
		Stamina = stamina;
		Oxygen = oxygen;
		Experience = experience;
		Reputation = reputation;
		GodModeEnabled = godModeEnabled;
		InfiniteNeedsEnabled = infiniteNeedsEnabled;
	}
}

internal sealed class CharacterPagePresenter
{
	private readonly CheatActions _actions;
	private readonly Func<string, bool> _ensureGameplayAccess;

	internal CharacterPagePresenter(CheatActions actions, Func<string, bool> ensureGameplayAccess)
	{
		_actions = actions ?? throw new ArgumentNullException(nameof(actions));
		_ensureGameplayAccess = ensureGameplayAccess ?? throw new ArgumentNullException(nameof(ensureGameplayAccess));
	}

	internal CharacterPageSnapshot CaptureSnapshot()
	{
		Player player = Player.LocalPlayer;
		PlayerLife life = player?.life;
		PlayerSkills skills = player?.skills;
		if (life == null || skills == null)
		{
			return new CharacterPageSnapshot(false, 0, 0, 0, 0, 0, 0, 0, 0, _actions.GodModeEnabled, _actions.InfiniteNeedsEnabled);
		}
		return new CharacterPageSnapshot(
			true,
			life.health,
			life.food,
			life.water,
			life.virus,
			life.stamina,
			life.oxygen,
			skills.experience,
			skills.reputation,
			_actions.GodModeEnabled,
			_actions.InfiniteNeedsEnabled);
	}

	internal void SetGodMode(bool value)
	{
		if (!value || _ensureGameplayAccess(GlobalShortcutActions.ToggleGodMode)) _actions.SetGodMode(value);
	}
	internal void SetInfiniteNeeds(bool value)
	{
		if (!value || _ensureGameplayAccess(GlobalShortcutActions.ToggleInfiniteNeeds)) _actions.SetInfiniteNeeds(value);
	}
	internal bool SetLifeStats(byte health, byte food, byte water, byte virus, byte stamina, byte oxygen) =>
		_ensureGameplayAccess("character.life-stats") && _actions.SetLifeStats(health, food, water, virus, stamina, oxygen);
	internal bool RestoreEverything() =>
		_ensureGameplayAccess(GlobalShortcutActions.RestoreEverything) && _actions.RestoreEverything();
	internal bool RefillNeeds() =>
		_ensureGameplayAccess(GlobalShortcutActions.RefillNeeds) && _actions.RefillNeeds();
	internal bool CureInjuries() => _ensureGameplayAccess("character.cure-injuries") && _actions.CureInjuries();
	internal bool AddExperience(uint amount) => _ensureGameplayAccess("character.add-experience") && _actions.AddExperience(amount);
	internal bool AddReputation(int amount) => _ensureGameplayAccess("character.add-reputation") && _actions.AddReputation(amount);
	internal int MaxAllSkills() => _ensureGameplayAccess("character.max-skills") ? _actions.MaxAllSkills() : 0;
}

internal sealed class WorldPageSnapshot
{
	internal uint Cycle { get; }
	internal uint Time { get; }
	internal bool FreezeTimeEnabled { get; }
	internal bool FullMoonEnabled { get; }
	internal string ActiveWeatherName { get; }
	internal int ItemCount { get; }
	internal int WorkshopItemCount { get; }
	internal int VehicleCount { get; }
	internal int WorkshopVehicleCount { get; }
	internal float NormalizedTime => (float)Time / Math.Max(1u, Cycle);

	internal WorldPageSnapshot(uint cycle, uint time, bool freezeTimeEnabled, bool fullMoonEnabled, string activeWeatherName,
		int itemCount, int workshopItemCount, int vehicleCount, int workshopVehicleCount)
	{
		Cycle = cycle;
		Time = time;
		FreezeTimeEnabled = freezeTimeEnabled;
		FullMoonEnabled = fullMoonEnabled;
		ActiveWeatherName = activeWeatherName ?? string.Empty;
		ItemCount = itemCount;
		WorkshopItemCount = workshopItemCount;
		VehicleCount = vehicleCount;
		WorkshopVehicleCount = workshopVehicleCount;
	}
}

internal sealed class WorldPagePresenter
{
	private readonly IWorldUiFeature _feature;

	internal WorldPagePresenter(IWorldUiFeature feature)
	{
		_feature = feature ?? throw new ArgumentNullException(nameof(feature));
	}

	internal WorldPageSnapshot CaptureSnapshot()
	{
		return new WorldPageSnapshot(
			_feature.Cycle,
			_feature.Time,
			_feature.FreezeTimeEnabled,
			_feature.FullMoonEnabled,
			_feature.ActiveWeatherName,
			_feature.ItemCount,
			_feature.WorkshopItemCount,
			_feature.VehicleCount,
			_feature.WorkshopVehicleCount);
	}

	internal void SetTime(uint value) => _feature.SetTime(value);
	internal void SetDay() => _feature.SetDay();
	internal void SetNight() => _feature.SetNight();
	internal void SetFreezeTime(bool value) => _feature.SetFreezeTime(value);
	internal void SetFullMoon(bool value) => _feature.SetFullMoon(value);
	internal bool CallAirdrop() => _feature.CallAirdrop();
	internal bool StartRain() => _feature.StartRain();
	internal bool StartSnow() => _feature.StartSnow();
	internal void ClearWeather() => _feature.ClearWeather();
	internal void DisableWeather() => _feature.DisableWeather();
	internal void RefreshCatalog() => _feature.RefreshCatalog();
	internal bool ApplyScenario(WorldScenarioKind scenario) => _feature.ApplyScenario(scenario);
}
