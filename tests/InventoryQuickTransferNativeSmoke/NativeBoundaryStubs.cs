using System.Reflection;
using UnturnedSingleplayerCheatMenu.InventoryArrange;

namespace UnityEngine
{
    public enum KeyCode { LeftControl, RightControl, LeftShift, RightShift, LeftAlt, RightAlt }
}

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public List<string> Errors { get; } = new();
        public void LogInfo(object message) { }
        public void LogError(object message) => Errors.Add(message?.ToString());
    }
}

namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] arguments = null) =>
            arguments == null
                ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                : type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    null, arguments, null);
        public static MethodInfo DeclaredMethod(Type type, string name, Type[] arguments) => Method(type, name, arguments);
    }

    public sealed class HarmonyMethod
    {
        public HarmonyMethod(MethodInfo method) => Method = method;
        public MethodInfo Method { get; }
    }

    public sealed class Harmony
    {
        public Dictionary<MethodInfo, HarmonyMethod> Prefixes { get; } = new();
        public void Patch(MethodInfo original, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            if (prefix != null) Prefixes.Add(original, prefix);
        }

        public void DispatchMouse(int button, byte page, byte x = 0, byte y = 0)
        {
            // Current installed SleekItem/SleekItems contract: left -> onGrabbedItem,
            // right -> onSelectedItem. Middle has no item callback.
            if (button != 0 && button != 1) return;
            MethodInfo native = AccessTools.Method(typeof(SDG.Unturned.PlayerDashboardInventoryUI),
                button == 0 ? "onGrabbedItem" : "onSelectedItem");
            if (Prefixes.TryGetValue(native, out HarmonyMethod prefix) &&
                !(bool)prefix.Method.Invoke(null, new object[] { page, x, y })) return;
            native.Invoke(null, button == 0
                ? new object[] { page, x, y, new SDG.Unturned.SleekItem() }
                : new object[] { page, x, y });
        }
    }
}

namespace SDG.Unturned
{
    public static class InputEx
    {
        public static HashSet<UnityEngine.KeyCode> Held { get; } = new();
        public static bool ProcessKeys = true;
        public static bool GetKey(UnityEngine.KeyCode key) => ProcessKeys && Held.Contains(key);
    }

    public static class ControlsSettings
    {
        public static UnityEngine.KeyCode other = UnityEngine.KeyCode.LeftAlt;
    }

    public sealed class SleekItem { }
    public sealed class Item { public ushort id; }
    public sealed class ItemJar { public Item item; public byte x; public byte y; }
    public sealed class Items
    {
        public ItemJar Jar;
        public byte getIndex(byte x, byte y) => Jar == null ? byte.MaxValue : (byte)0;
        public ItemJar getItem(byte index) => index == 0 ? Jar : null;
    }

    public sealed class PlayerInventory
    {
        public const byte SLOTS = 2, BACKPACK = 3, STORAGE = 7, AREA = 8;
        public bool isStoring = true;
        public Items[] items = Enumerable.Range(0, 9).Select(_ => new Items()).ToArray();
        public void ReceiveDragItem(byte page_0, byte x_0, byte y_0, byte page_1, byte x_1, byte y_1, byte rot_1) { }
        public void ReceiveSwapItem(byte page_0, byte x_0, byte y_0, byte rot_0, byte page_1, byte x_1, byte y_1, byte rot_1) { }
    }

    public sealed class HotkeyInfo { public ushort id; public byte page; public byte x; public byte y; }
    public sealed class PlayerEquipment { public HotkeyInfo[] hotkeys; public Action onHotkeysUpdated; }
    public sealed class Player
    {
        public static Player LocalPlayer;
        public PlayerInventory inventory = new();
        public PlayerEquipment equipment = new();
        private void InitializePlayer() { }
    }

    public static class PlayerDashboardInventoryUI
    {
        public static int NativeLeftCalls;
        public static int NativeRightCalls;
        private static void onGrabbedItem(byte page, byte x, byte y, SleekItem item) => NativeLeftCalls++;
        private static void onSelectedItem(byte page, byte x, byte y) => NativeRightCalls++;
    }
}

namespace UnturnedSingleplayerCheatMenu
{
    internal sealed class CheatMenuPlugin
    {
        internal static CheatMenuPlugin Instance = null;
        internal bool CanExecuteGameplay = false;
    }
}

namespace UnturnedSingleplayerCheatMenu.Services { }

namespace UnturnedSingleplayerCheatMenu.InventoryArrange
{
    internal sealed class InventoryArrangePlan { }
    internal sealed class InventoryArrangeFeature
    {
        internal int Calls;
        internal byte LastPage;
        internal InventoryArrangeResult Result = new() { Success = true, Message = "moved", Plan = new InventoryArrangePlan() };
        internal InventoryArrangeResult BeginQuickTransfer(byte page, byte x, byte y)
        {
            Calls++;
            LastPage = page;
            return Result;
        }
        internal void OnPlayerInitialized() { }
    }

    // These types support compilation of the unrelated manual Drag/Swap hotkey hooks.
    // Their behavior is covered by InventoryArrangePlannerSmoke, not this input fixture.
    internal readonly record struct InventoryItemAddress(byte Page, byte X, byte Y);
    internal readonly record struct InventoryItemMove(ushort AssetId, InventoryItemAddress Source, InventoryItemAddress Destination);
    internal readonly record struct InventoryHotkeyBindingState(int HotkeyIndex, ushort AssetId, InventoryItemAddress Address);
    internal static class InventoryHotkeyBindingPolicy
    {
        internal static IReadOnlyList<InventoryHotkeyBindingState> Remap(
            IEnumerable<InventoryHotkeyBindingState> bindings, IEnumerable<InventoryItemMove> moves) => bindings.ToArray();
    }
}
