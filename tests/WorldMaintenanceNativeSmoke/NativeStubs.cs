// Only the SDK surface used by the linked production sources is stubbed here. Native behavior
// is injected by each fixture; the harness never copies the production bridge or service logic.
namespace UnityEngine
{
    public class Object { }
    public class Transform : Object { public Vector3 position; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 up => new(0f, 1f, 0f);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public static class Time { public static float realtimeSinceStartup = 1000f; }
    public static class Random
    {
        public static int Range(int min, int max) => min;
        public static float Range(float min, float max) => min;
    }
}

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public readonly List<string> Entries = new();
        public void LogInfo(object entry) => Entries.Add(entry?.ToString() ?? string.Empty);
        public void LogWarning(object entry) => Entries.Add(entry?.ToString() ?? string.Empty);
    }
}

namespace SDG.Unturned
{
    using UnityEngine;
    public sealed class LevelAsset
    {
        public struct SchedulableWeather { }
    }
    public static class LightingManager
    {
        private static LevelAsset.SchedulableWeather[] schedulableWeathers;
        private static bool shouldTickScheduledWeather;
        public static bool ShouldTickScheduledWeather => shouldTickScheduledWeather;
        public static LevelAsset.SchedulableWeather[] ScheduledWeathers => schedulableWeathers;
        public static bool ActiveWeather;
        public static int ResetCalls;
        public static void Reset(LevelAsset.SchedulableWeather[] weather)
        {
            schedulableWeathers = weather;
            shouldTickScheduledWeather = weather != null && weather.Length != 0;
            ActiveWeather = true;
            ResetCalls = 0;
        }
        public static void ResetScheduledWeather() { ActiveWeather = false; ResetCalls++; }
        public static void DisableWeather() { ActiveWeather = false; shouldTickScheduledWeather = false; }
    }
    public sealed class ModeConfigData
    {
        public ItemsConfigData Items = new();
        public ObjectsConfigData Objects = new();
    }
    public sealed class ItemsConfigData
    {
        public float Respawn_Time = 600f;
        public float Spawn_Chance = 1f;
    }
    public sealed class ObjectsConfigData { public bool Items_Obstruct_Tree_Respawns; }
    public static class Provider
    {
        public static bool isServer = true;
        public static ModeConfigData modeConfigData = new();
    }
    public static class Dedicator { public static bool IsDedicatedServer; }
    public static class RayMasks { public const int PLAYER = 1, ENEMY = 2, BARRICADE = 4; }
    public static class Regions { public const int WORLD_SIZE = 1; }
    public sealed class Player
    {
        public static Player LocalPlayer;
        public Transform transform = new();
    }
    public sealed class ResourceSpawnpoint { public bool isDead; public Vector3 point; }
    public sealed class ResourceManager
    {
        private static ResourceManager manager = new();
        private bool overlapTreeColliders(ResourceSpawnpoint spawn, int layerMask) => false;
        public static void ServerSetResourceAlive(byte x, byte y, ushort index) { }
    }
    public static class LevelGround
    {
        public static List<ResourceSpawnpoint> GetTreesOrNullInRegion(byte x, byte y) => null;
    }
    public sealed class Item
    {
        public ushort id;
        public byte amount = 1, quality = 100;
        public byte[] state = Array.Empty<byte>();
    }
    public sealed class ItemData
    {
        public Item item;
        public Vector3 point;
        public uint instanceID;
    }
    public sealed class ItemDrop
    {
        public uint instanceID;
        public Transform model = new();
    }
    public sealed class ItemRegion
    {
        public List<ItemData> items = new();
        public List<ItemDrop> drops = new();
        public float lastRespawn;
        public bool isNetworked = true;
    }
    public sealed class ItemSpawnpoint { public Vector3 point; public byte type; }
    public static class LevelItems { public static List<ItemSpawnpoint>[,] spawns; }
    public sealed class ItemManager
    {
        public static ItemManager instance;
        public static ItemRegion[,] regions;
        private static byte respawnItems_X = 19, respawnItems_Y = 23;
        internal static Func<byte, byte, bool> RespawnBehavior;
        internal static Action<byte, byte, ItemData> LocalSpawnBehavior;
        internal static int RespawnCalls, LocalSpawnCalls;
        internal static readonly List<(byte X, byte Y, ItemData Data)> PendingDrops = new();
        internal static (byte X, byte Y) Cursor => (respawnItems_X, respawnItems_Y);

        // Exact private signatures are required: production resolves and binds these via reflection.
        private bool respawnItems()
        {
            RespawnCalls++;
            return RespawnBehavior?.Invoke(respawnItems_X, respawnItems_Y) ?? false;
        }
        private void spawnItem(byte x, byte y, ushort id, byte amount, byte quality, byte[] state,
            Vector3 point, uint instanceID, bool shouldPlayEffect)
        {
            LocalSpawnCalls++;
            LocalSpawnBehavior?.Invoke(x, y, new ItemData
            {
                instanceID = instanceID, point = point,
                item = new Item { id = id, amount = amount, quality = quality, state = state }
            });
        }
        public static void ServerClearItemsInSphere(Vector3 center, float radius) { }
        public static void askClearRegionItems(byte x, byte y) => regions[x, y].items.Clear();

        internal static void NativeCommitData(byte x, byte y, ItemData data)
        {
            regions[x, y].items.Add(data);
            // The installed native local network dispatch defers model construction to Update.
            if (regions[x, y].isNetworked) PendingDrops.Add((x, y, data));
        }

        internal static void NativeUpdate()
        {
            foreach ((byte x, byte y, ItemData data) in PendingDrops)
                instance.spawnItem(x, y, data.item.id, data.item.amount, data.item.quality,
                    data.item.state, data.point, data.instanceID, false);
            PendingDrops.Clear();
        }

        internal static ItemRegion Reset(bool networked = true)
        {
            Provider.modeConfigData = new();
            Provider.isServer = true;
            Dedicator.IsDedicatedServer = false;
            Player.LocalPlayer = new();
            instance = new();
            respawnItems_X = 19;
            respawnItems_Y = 23;
            RespawnCalls = LocalSpawnCalls = 0;
            PendingDrops.Clear();
            RespawnBehavior = null;
            LocalSpawnBehavior = (x, y, item) => regions[x, y].drops.Add(new ItemDrop { instanceID = item.instanceID });
            ItemRegion region = new() { lastRespawn = 123f, isNetworked = networked };
            regions = new[,] { { region } };
            LevelItems.spawns = new[,] { { new List<ItemSpawnpoint> { new() { point = new Vector3(10f, 0f, 10f) } } } };
            SafezoneManager.PointValid = _ => true;
            ZombieManager.AllZombies.Clear();
            ZombieManager.regions = Array.Empty<ZombieRegion>();
            LevelZombies.zombies = Array.Empty<List<ZombieSpawnpoint>>();
            return region;
        }
    }
    public enum ERagdollEffect { None }
    public sealed class Zombie
    {
        public bool isDead, isBoss, isMega;
        public byte bound, type, speciality, shirt, pants, hat, gear;
        public Transform transform = new();
    }
    public sealed class ZombieRegion
    {
        public List<Zombie> zombies = new();
        public bool hasBeacon;
    }
    public sealed class ZombieSpawnpoint { public Vector3 point; }
    public static class ZombieManager
    {
        public static List<Zombie> AllZombies = new();
        public static ZombieRegion[] regions = Array.Empty<ZombieRegion>();
        public static void sendZombieDead(Zombie zombie, Vector3 point, ERagdollEffect effect) { }
        public static void sendZombieAlive(Zombie zombie, byte type, byte speciality, byte shirt,
            byte pants, byte hat, byte gear, Vector3 point, byte angle) { }
    }
    public static class LevelZombies { public static List<ZombieSpawnpoint>[] zombies = Array.Empty<List<ZombieSpawnpoint>>(); }
    public static class SafezoneManager
    {
        internal static Func<Vector3, bool> PointValid = _ => true;
        public static bool checkPointValid(Vector3 point) => PointValid(point);
    }
    public static class MeasurementTool { public static byte angleToByte(float angle) => 0; }
}
