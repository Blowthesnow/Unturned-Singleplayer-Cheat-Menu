using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class AssetHoverDetailsBuilder
{
    public static string BuildItem(ItemAsset asset, string displayName, string translatedDescription, AmmoCompatibilityIndex ammoIndex, IReadOnlyList<ItemAsset> loadedItems)
    {
        if (asset == null) return string.Empty;
        List<string> lines = new List<string>();
        string nativeDescription = BuildNativeDescription(asset);
        if (!string.IsNullOrWhiteSpace(nativeDescription)) lines.Add(nativeDescription);
        else if (!string.IsNullOrWhiteSpace(translatedDescription)) lines.Add(translatedDescription.Trim());
        if (asset is ItemGunAsset gun) AddGunLines(lines, gun, ammoIndex, loadedItems);
        if (asset is ItemConsumeableAsset consumeable) AddConsumeableLines(lines, consumeable);
        return new AssetHoverDetails(displayName, lines).ToString();
    }

    public static string BuildVehicle(VehicleAsset asset, string displayName)
    {
        if (asset == null) return string.Empty;
        List<string> lines = new List<string>
        {
            Label("类型") + VehicleCategory(asset),
            string.Format("{0}{1:0.#} km/h", Label("最高速度"), asset.TargetForwardVelocity * 3.6f),
            string.Format("{0}{1:N0}", Label("生命值"), asset.healthMax)
        };
        if (asset.trunkStorage_X > 0 && asset.trunkStorage_Y > 0) lines.Add(string.Format("{0}{1} × {2}", Label("储物空间"), asset.trunkStorage_X, asset.trunkStorage_Y));
        if (asset.hasHeadlights) lines.Add(Label("能力") + Value("车灯", "Headlights"));
        if (asset.hasSirens) lines.Add(Label("能力") + Value("警笛", "Sirens"));
        if (asset.hasHook) lines.Add(Label("能力") + Value("牵引钩", "Tow hook"));
        if (asset.hasZip) lines.Add(Label("能力") + Value("滑索", "Zipline"));
        return new AssetHoverDetails(displayName, lines).ToString();
    }

    private static string BuildNativeDescription(ItemAsset asset)
    {
        try
        {
            ItemDescriptionBuilder builder = new ItemDescriptionBuilder
            {
                flags = EItemDescriptionFlags.All,
                lines = new List<ItemDescriptionLine>(),
                stringBuilder = new StringBuilder()
            };
            asset.BuildDescription(builder, null);
            builder.lines.Sort();
            return string.Join("\n", builder.lines.Select(line => line.text).Where(text => !string.IsNullOrWhiteSpace(text)));
        }
        catch
        {
            return asset.itemDescription;
        }
    }

    private static void AddGunLines(List<string> lines, ItemGunAsset gun, AmmoCompatibilityIndex ammoIndex, IReadOnlyList<ItemAsset> loadedItems)
    {
        if (gun.playerDamageMultiplier != null && gun.playerDamageMultiplier.damage > 0f)
        {
            lines.Add(string.Format("{0}玩家 {1:0.#} · 僵尸 {2:0.#} · 动物 {3:0.#}", Label("伤害"), gun.playerDamageMultiplier.damage, gun.zombieDamageMultiplier.damage, gun.animalDamageMultiplier.damage));
        }
        if (gun.range > 0f) lines.Add(string.Format("{0}{1:0.#} m", Label("射程"), gun.range));
        ItemMagazineAsset magazine = gun.SelectDefaultMagazine();
        lines.Add(magazine != null ? string.Format("{0}{1:N0} {2}", Label("默认弹匣容量"), magazine.MaxAmount, Value("发", "rounds")) : Label("默认弹匣") + Value("未配置", "not configured"));
        float roundsPerSecond = 50f / Math.Max(1, gun.firerate + 1);
        lines.Add(string.Format("{0}{1:0.##} 发/秒", Label("射速"), roundsPerSecond));
        string fireModes = string.Join(" / ", new[] { gun.hasAuto ? "自动" : null, gun.hasSemi ? "半自动" : null, gun.hasBurst ? "点射" : null }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(fireModes)) lines.Add(Label("射击模式") + fireModes);
        if (magazine != null && magazine.pellets > 1) lines.Add(string.Format("{0}{1}", Label("弹丸数量"), magazine.pellets));
        AddAttachment(lines, "枪口", gun.barrelID, loadedItems);
        AddAttachment(lines, "握把", gun.gripID, loadedItems);
        AddAttachment(lines, "瞄具", gun.sightID, loadedItems);
        AddAttachment(lines, "战术配件", gun.tacticalID, loadedItems);
        if (ammoIndex != null)
        {
            IReadOnlyList<ItemAsset> ammo = ammoIndex.GetCompatibleAmmo(gun);
            lines.Add(AmmoTooltipText.Format(
                gun.infiniteAmmo,
                ammo.Select(item => item.FriendlyName),
                PluginLocalization.CurrentLanguage == PluginLanguage.English));
        }
    }

    private static void AddAttachment(List<string> lines, string label, ushort id, IReadOnlyList<ItemAsset> loadedItems)
    {
        if (id == 0 || loadedItems == null) return;
        ItemAsset attachment = loadedItems.FirstOrDefault(item => item != null && item.id == id);
        if (attachment != null) lines.Add("默认" + label + "：" + attachment.FriendlyName);
    }

    private static void AddConsumeableLines(List<string> lines, ItemConsumeableAsset asset)
    {
        AddSigned(lines, "食物", asset.food);
        AddSigned(lines, "饮水", asset.water);
        AddSigned(lines, "生命", asset.health);
        AddSigned(lines, "体力", asset.energy);
        AddSigned(lines, "消毒", asset.disinfectant);
        AddSigned(lines, "病毒", -asset.virus);
        AddSigned(lines, "氧气", asset.oxygen);
        AddSigned(lines, "温暖", (int)Math.Min(int.MaxValue, asset.warmth));
        if (asset.hasAid) lines.Add(Status("治疗", "Aid"));
        switch (asset.bleedingModifier)
        {
            case ItemConsumeableAsset.Bleeding.Heal: lines.Add(Status("止血", "Stops bleeding")); break;
            case ItemConsumeableAsset.Bleeding.Cut: lines.Add(Status("导致流血", "Causes bleeding")); break;
        }
        switch (asset.bonesModifier)
        {
            case ItemConsumeableAsset.Bones.Heal: lines.Add(Status("治疗骨折", "Heals broken bones")); break;
            case ItemConsumeableAsset.Bones.Break: lines.Add(Status("导致骨折", "Causes broken bones")); break;
        }
    }

    private static void AddSigned(List<string> lines, string label, int value)
    {
        if (value != 0) lines.Add(string.Format("{0}{1}{2}", Label(label), value > 0 ? "+" : string.Empty, value));
    }

    private static string VehicleCategory(VehicleAsset asset)
    {
        string category = AssetCatalog.GetVehicleCategory(asset);
        if (PluginLocalization.CurrentLanguage != PluginLanguage.English) return category;
        switch (category)
        {
            case "陆地车辆": return "Land vehicle";
            case "固定翼飞机": return "Plane";
            case "直升机": return "Helicopter";
            case "飞艇": return "Blimp";
            case "船只": return "Boat";
            case "火车": return "Train";
            default: return "Other";
        }
    }

    private static string Value(string chinese, string english)
    {
        return PluginLocalization.CurrentLanguage == PluginLanguage.English ? english : chinese;
    }

    private static string Status(string chinese, string english)
    {
        return (PluginLocalization.CurrentLanguage == PluginLanguage.English ? "Status: " : "状态：") + Value(chinese, english);
    }
    private static string Label(string chinese)
    {
        if (PluginLocalization.CurrentLanguage != PluginLanguage.English) return chinese + "：";
        switch (chinese)
        {
            case "类型": return "Type: ";
            case "最高速度": return "Top speed: ";
            case "生命值": return "Health: ";
            case "储物空间": return "Storage: ";
            case "能力": return "Ability: ";
            case "伤害": return "Damage: ";
            case "射程": return "Range: ";
            case "默认弹匣容量": return "Default magazine: ";
            case "射速": return "Rate of fire: ";
            case "射击模式": return "Fire mode: ";
            case "弹丸数量": return "Pellets: ";
            case "兼容弹药": return "Compatible ammo: ";
            case "食物": return "Food: ";
            case "饮水": return "Water: ";
            case "体力": return "Energy: ";
            case "消毒": return "Disinfectant: ";
            case "病毒": return "Virus: ";
            case "氧气": return "Oxygen: ";
            case "温暖": return "Warmth: ";
            default: return chinese + ": ";
        }
    }
}
