using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class AssetHoverDetails
{
    public AssetHoverDetails(string title, IEnumerable<string> lines)
    {
        Title = title ?? string.Empty;
        Lines = new List<string>(lines ?? new string[0]);
    }

    public string Title { get; }

    public IReadOnlyList<string> Lines { get; }

    public override string ToString()
    {
        return Lines.Count == 0 ? Title : Title + "\n" + string.Join("\n", Lines);
    }
}