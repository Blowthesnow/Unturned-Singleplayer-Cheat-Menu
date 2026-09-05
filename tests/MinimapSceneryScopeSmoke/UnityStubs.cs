// State-only doubles: these tests do not simulate Unity rendering or static batching.
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class GameObject
    {
        private static int nextId;
        private readonly int id = ++nextId;
        private int currentLayer;
        public bool activeInHierarchy = true;
        public int FailNextLayer = -1;
        public int layer
        {
            get => currentLayer;
            set
            {
                if (value == FailNextLayer)
                {
                    FailNextLayer = -1;
                    throw new InvalidOperationException("Injected layer failure");
                }
                currentLayer = value;
            }
        }
        public int GetInstanceID() => id;
    }
    public class Renderer
    {

        public bool Destroyed;
        private static bool IsNull(Renderer value) => ReferenceEquals(value, null) || value.Destroyed;
        public static bool operator ==(Renderer a, Renderer b) =>
            IsNull(a) ? IsNull(b) : !IsNull(b) && ReferenceEquals(a, b);
        public static bool operator !=(Renderer a, Renderer b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => id;
        private static int nextId;
        private readonly int id = ++nextId;
        private bool off;
        public GameObject gameObject = new GameObject();
        public bool enabled = true;
        public bool isPartOfStaticBatch;
        public bool FailNextRestore;
        public bool forceRenderingOff
        {
            get => off;
            set
            {
                if (!value && FailNextRestore)
                {
                    FailNextRestore = false;
                    throw new InvalidOperationException("Injected restore failure");
                }
                off = value;
            }
        }
        public int GetInstanceID() => id;
    }
    public class Shader
    {
        public string name;
        public int maximumLOD;
        public static readonly HashSet<string> Keywords = new();
        public static string FailEnableKeyword;
        public static int FailEnableCount;
        public static bool IsKeywordEnabled(string key) => Keywords.Contains(key);
        public static void EnableKeyword(string key)
        {
            if (key == FailEnableKeyword && FailEnableCount > 0)
            {
                FailEnableCount--;
                throw new InvalidOperationException("Injected keyword failure");
            }
            Keywords.Add(key);
        }
        public static void DisableKeyword(string key) => Keywords.Remove(key);
    }
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
    }
    public class Material
    {
        private static int nextId;
        private readonly int id = ++nextId;
        public Shader shader;
        public Vector4 Value;
        public int FailRestoreCount;
        public int SetCount;
        public bool HasProperty(string name) => name == "_DistortParams";
        public int GetInstanceID() => id;
        public Vector4 GetVector(string name) => Value;
        public void SetVector(string name, Vector4 value)
        {
            if(value.w == 0f && FailRestoreCount > 0) { FailRestoreCount--; throw new InvalidOperationException("Injected material restore failure"); }
            Value=value; SetCount++;
        }
    }
}
namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public readonly List<string> Warnings = new();
        public void LogInfo(string text) { }
        public void LogWarning(string text) => Warnings.Add(text);
    }
}
