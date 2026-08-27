using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class AmmoTooltipText
{
    public static string Format(bool infiniteAmmo, IEnumerable<string> ammoNames, bool english)
    {
        if (infiniteAmmo)
        {
            return english ? "Ammo: Infinite" : "弹药：无限子弹";
        }

        List<string> names = (ammoNames ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        if (names.Count > 0)
        {
            return (english ? "Compatible ammo: " : "兼容弹药：") +
                   string.Join(english ? ", " : "、", names);
        }

        return english
            ? "Compatible ammo: no confirmed Ammo relation in loaded assets"
            : "兼容弹药：未找到当前已加载资产中的已确认 Ammo 关系";
    }
}
