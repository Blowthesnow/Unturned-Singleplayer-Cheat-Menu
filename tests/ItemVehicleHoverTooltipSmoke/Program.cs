using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

AssetHoverDetails details = new("Rifle", new[] { "Description", "Damage: 20", "Range: 100 m" });
string text = details.ToString();
Check(text.StartsWith("Rifle\n", StringComparison.Ordinal), "Tooltip title should be first.");
Check(text.Contains("Description", StringComparison.Ordinal), "Original description should be retained.");
Check(!text.Contains("GUID", StringComparison.OrdinalIgnoreCase), "Tooltip model should not add asset identifiers.");

Check(AmmoTooltipText.Format(true, Array.Empty<string>(), english: false) == "弹药：无限子弹",
    "Infinite-ammo guns should be labeled explicitly even without Ammo blueprint relations.");
Check(AmmoTooltipText.Format(true, new[] { "Ammo Box" }, english: true) == "Ammo: Infinite",
    "Infinite-ammo labeling should take precedence over compatible Ammo blueprint names.");
Check(AmmoTooltipText.Format(false, new[] { "Military Ammunition Crate", "Ranger Ammunition Box" }, english: false) ==
      "兼容弹药：Military Ammunition Crate、Ranger Ammunition Box",
    "Finite-ammo guns should retain their confirmed compatible ammo list.");
Check(AmmoTooltipText.Format(false, Array.Empty<string>(), english: true) ==
      "Compatible ammo: no confirmed Ammo relation in loaded assets",
    "Finite-ammo guns without a confirmed relation should not be mislabeled as infinite.");

Check(PageNavigation.TryMove(1, 3, 1f, false, out int previous) && previous == 0, "Scroll up should move one page backward.");
Check(PageNavigation.TryMove(1, 3, -1f, false, out int next) && next == 2, "Scroll down should move one page forward.");
Check(!PageNavigation.TryMove(0, 3, 1f, false, out int first) && first == 0, "First page should clamp on upward scroll.");
Check(!PageNavigation.TryMove(2, 3, -1f, false, out int last) && last == 2, "Last page should clamp on downward scroll.");
Check(!PageNavigation.TryMove(1, 3, -1f, true, out int inputPage) && inputPage == 1, "Input fields must not consume page scroll.");
Check(!PageNavigation.TryMove(1, 3, 0f, false, out int noScroll) && noScroll == 1, "Zero scroll must not change page.");

Console.WriteLine("Item/vehicle hover tooltip smoke: PASS");
