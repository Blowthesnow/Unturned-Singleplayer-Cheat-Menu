namespace SDG.Unturned;

// Native DTO surface used to execute the production world-drop adapter without Unity.
internal static class Provider
{
    internal static bool isServer = true;
}

internal static class Regions
{
    internal static bool checkSafe(byte x, byte y) => x < 2 && y < 2;
}

internal sealed class Item
{
    internal ushort id;
    internal byte amount;
    internal byte quality;
    internal byte[] state;
}

internal sealed class ItemData
{
    internal uint instanceID;
    internal Item item;
}

internal sealed class InteractableItem
{
    internal Item item;
}

internal sealed class ItemDrop
{
    internal uint instanceID;
    internal object model = new object();
    internal InteractableItem interactableItem;
}

internal sealed class ItemRegion
{
    internal List<ItemData> items = new List<ItemData>();
    internal List<ItemDrop> drops = new List<ItemDrop>();
}

internal static class ItemManager
{
    internal static ItemRegion[,] regions;
    internal static Action<byte, byte, uint, bool> Destroy;

    internal static void ReceiveDestroyItem(byte x, byte y, uint instanceID, bool shouldPlayEffect) =>
        Destroy(x, y, instanceID, shouldPlayEffect);
}
