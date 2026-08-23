using SDG.Unturned;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.UI
{
    /// <summary>
    /// Formats rich tooltip text for item and vehicle asset cards.
    /// Prioritizes native in-game description and auto-stat descriptions over editor fields.
    /// </summary>
    internal static class AssetTooltipFormatter
    {
        public static string FormatItemTooltip(ItemAsset asset)
        {
            if (asset == null)
                return string.Empty;

            var sb = new StringBuilder();
            
            // Layer 1: Name + Description + Native auto-stats
            sb.AppendLine($"<b>{asset.FriendlyName}</b>");
            
            // Get the item description (may already include auto-stats if Use_Auto_Stat_Descriptions is true)
            string description = asset.itemDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine(description);
            }

            // Layer 2: Type-specific useful information
            if (asset is ItemGunAsset gun)
            {
                AppendGunDetails(sb, gun);
            }
            else if (asset is ItemFoodAsset food)
            {
                AppendFoodDetails(sb, food);
            }
            else if (asset is ItemMedicalAsset medical)
            {
                AppendMedicalDetails(sb, medical);
            }
            else if (asset is ItemMeleeAsset melee)
            {
                AppendMeleeDetails(sb, melee);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendGunDetails(StringBuilder sb, ItemGunAsset gun)
        {
            sb.AppendLine(); // separator
            
            // Damage by target type
            if (gun.playerDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（玩家）: {gun.playerDamageMultiplier.damage}");
            }
            if (gun.zombieDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（僵尸）: {gun.zombieDamageMultiplier.damage}");
            }
            if (gun.animalDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（动物）: {gun.animalDamageMultiplier.damage}");
            }
            
            // Range
            if (gun.range > 0)
            {
                sb.AppendLine($"射程: {gun.range}m");
            }
            
            // Magazine capacity (from default magazine)
            ushort magId = gun.GetDefaultMagazineLegacyId();
            if (magId != 0)
            {
                ItemAsset magAsset = Assets.find(EAssetType.ITEM, magId) as ItemAsset;
                if (magAsset is ItemMagazineAsset magazine)
                {
                    sb.AppendLine($"弹容量: {magazine.amount}发（默认弹匣）");
                }
                else
                {
                    sb.AppendLine("弹容量: 默认弹匣未解析");
                }
            }
            
            // Fire rate
            float rateOfFire = 50f / (gun.firerate + 1);
            sb.AppendLine($"射速: {rateOfFire:F1}发/秒");
            
            // Pellets (if applicable)
            if (gun.ammoPerShot > 1)
            {
                sb.AppendLine($"弹丸: {gun.ammoPerShot}");
            }
            
            // Default attachments
            List<string> attachments = new List<string>();
            if (gun.barrelID != 0)
            {
                ItemAsset barrel = Assets.find(EAssetType.ITEM, gun.barrelID) as ItemAsset;
                if (barrel != null) attachments.Add($"枪口: {barrel.FriendlyName}");
            }
            if (gun.gripID != 0)
            {
                ItemAsset grip = Assets.find(EAssetType.ITEM, gun.gripID) as ItemAsset;
                if (grip != null) attachments.Add($"握把: {grip.FriendlyName}");
            }
            if (gun.sightID != 0)
            {
                ItemAsset sight = Assets.find(EAssetType.ITEM, gun.sightID) as ItemAsset;
                if (sight != null) attachments.Add($"瞄具: {sight.FriendlyName}");
            }
            if (gun.tacticalID != 0)
            {
                ItemAsset tactical = Assets.find(EAssetType.ITEM, gun.tacticalID) as ItemAsset;
                if (tactical != null) attachments.Add($"战术: {tactical.FriendlyName}");
            }
            
            if (attachments.Count > 0)
            {
                sb.AppendLine("默认配件:");
                foreach (string att in attachments)
                {
                    sb.AppendLine($"  {att}");
                }
            }
            
            // Compatible ammo (simplified for now - full implementation requires caliber + blueprint analysis)
            ushort[] calibers = gun.magazineCalibers;
            if (calibers != null && calibers.Length > 0)
            {
                sb.AppendLine($"兼容口径: {string.Join(", ", calibers)}");
            }
        }

        private static void AppendFoodDetails(StringBuilder sb, ItemFoodAsset food)
        {
            sb.AppendLine(); // separator
            
            if (food.food > 0)
                sb.AppendLine($"食物: +{food.food}");
            if (food.water > 0)
                sb.AppendLine($"饮水: +{food.water}");
            if (food.health > 0)
                sb.AppendLine($"生命: +{food.health}");
            if (food.virus > 0)
                sb.AppendLine($"免疫: +{food.virus}");
            else if (food.virus < 0)
                sb.AppendLine($"病毒: {food.virus}");
            if (food.oxygen != 0)
                sb.AppendLine($"氧气: {(food.oxygen > 0 ? "+" : "")}{food.oxygen}");
            if (food.warmth != 0)
                sb.AppendLine($"体温: {(food.warmth > 0 ? "+" : "")}{food.warmth}");
        }

        private static void AppendMedicalDetails(StringBuilder sb, ItemMedicalAsset medical)
        {
            sb.AppendLine(); // separator
            
            if (medical.health > 0)
                sb.AppendLine($"生命: +{medical.health}");
            if (medical.food > 0)
                sb.AppendLine($"食物: +{medical.food}");
            if (medical.water > 0)
                sb.AppendLine($"饮水: +{medical.water}");
            if (medical.virus > 0)
                sb.AppendLine($"免疫: +{medical.virus}");
            if (medical.oxygen > 0)
                sb.AppendLine($"氧气: +{medical.oxygen}");
            if (medical.disinfectant > 0)
                sb.AppendLine($"消毒: +{medical.disinfectant}");
        }

        private static void AppendMeleeDetails(StringBuilder sb, ItemMeleeAsset melee)
        {
            sb.AppendLine(); // separator
            
            if (melee.playerDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（玩家）: {melee.playerDamageMultiplier.damage}");
            }
            if (melee.zombieDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（僵尸）: {melee.zombieDamageMultiplier.damage}");
            }
            if (melee.animalDamageMultiplier.damage > 0)
            {
                sb.AppendLine($"伤害（动物）: {melee.animalDamageMultiplier.damage}");
            }
            
            if (melee.range > 0)
            {
                sb.AppendLine($"攻击范围: {melee.range}m");
            }
        }

        public static string FormatVehicleTooltip(VehicleAsset asset)
        {
            if (asset == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{asset.vehicleName}</b>");
            
            sb.AppendLine(); // separator
            
            // Max speed (use TargetForwardVelocity, convert m/s to km/h)
            if (asset.TargetForwardVelocity > 0)
            {
                float speedKmh = asset.TargetForwardVelocity * 3.6f;
                sb.AppendLine($"最高速度: {speedKmh:F0} km/h");
            }
            
            // Health
            if (asset.health > 0)
            {
                sb.AppendLine($"生命值: {asset.health}");
            }
            
            // Fuel capacity
            if (asset.fuel > 0)
            {
                sb.AppendLine($"燃料容量: {asset.fuel}");
            }
            
            // Battery charge (for electric vehicles)
            if (asset.batterySpawnChargeMultiplier > 0)
            {
                sb.AppendLine($"电池容量: {asset.batterySpawnChargeMultiplier * 10000:F0}");
            }
            
            // Trunk storage
            if (asset.trunkStorage_X > 0 && asset.trunkStorage_Y > 0)
            {
                sb.AppendLine($"储物空间: {asset.trunkStorage_X}x{asset.trunkStorage_Y}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
