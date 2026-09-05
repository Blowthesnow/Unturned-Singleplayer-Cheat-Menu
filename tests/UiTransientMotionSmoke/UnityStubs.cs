// Test-only substitutes for Unity native objects. This harness executes production
// tween/service code; it does not claim Unity layout, rendering or input acceptance.
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static void Destroy(Object value)
        {
            if (value is GameObject go) { go.SetActive(false); go.transform.SetParent(null, false); }
            if (!ReferenceEquals(value, null)) value.Destroyed = true;
        }
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.Destroyed;
            bool bn = ReferenceEquals(b, null) || b.Destroyed;
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : Component => GetComponent<T>() ?? transform.parent?.GetComponentInParent<T>();
    }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled => gameObject.activeInHierarchy; }
    public class GameObject : Object
    {
        readonly List<Component> components = new();
        public readonly string name;
        public Transform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => !Destroyed && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name, params Type[] types)
        {
            this.name = name;
            transform = new RectTransform { gameObject = this };
            components.Add(transform);
            foreach (Type type in types) if (!typeof(Transform).IsAssignableFrom(type)) Add(type);
        }
        Component Add(Type type) { var c = (Component)Activator.CreateInstance(type); c.gameObject = this; components.Add(c); return c; }
        public T AddComponent<T>() where T : Component, new() => (T)Add(typeof(T));
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        public int GetInstanceID() => GetHashCode();
        public void SetActive(bool value) => activeSelf = value;
    }
    public class Transform : Component
    {
        public Transform parent;
        internal readonly List<Transform> Children = new();
        public Vector3 localScale = Vector3.one;
        public int childCount => Children.Count;
        public Transform GetChild(int index) => Children[index];
        public void SetAsLastSibling() { var p = parent; SetParent(p, false); }
        public void SetParent(Transform target, bool worldPositionStays)
        { parent?.Children.Remove(this); parent = target; parent?.Children.Add(this); }
        public bool IsChildOf(Transform root) => this == root || parent != null && parent.IsChildOf(root);
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, offsetMin, offsetMax, sizeDelta, anchoredPosition, pivot;
        public Rect rect
        {
            get
            {
                float parentHeight = parent is RectTransform p ? p.rect.height : 0f;
                return new Rect { height = (anchorMax.y - anchorMin.y) * parentHeight + sizeDelta.y + offsetMax.y - offsetMin.y };
            }
        }
    }
    public struct Rect { public float height; }
    public struct Vector2(float x, float y)
    {
        public float x = x, y = y;
        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new(Mathf.LerpUnclamped(a.x, b.x, t), Mathf.LerpUnclamped(a.y, b.y, t));
    }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new(Mathf.LerpUnclamped(a.x, b.x, t), Mathf.LerpUnclamped(a.y, b.y, t), Mathf.LerpUnclamped(a.z, b.z, t));
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color clear => new();
        public static Color white => new();
        public static Color black => new();
        public static Color Lerp(Color a, Color b, float t) => LerpUnclamped(a, b, Math.Clamp(t, 0, 1));
        public static Color LerpUnclamped(Color a, Color b, float t) => new(Mathf.LerpUnclamped(a.r, b.r, t), Mathf.LerpUnclamped(a.g, b.g, t), Mathf.LerpUnclamped(a.b, b.b, t), Mathf.LerpUnclamped(a.a, b.a, t));
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float a) => Math.Abs(a);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
    }
    public class CanvasGroup : Component { public float alpha = 1; public bool interactable, blocksRaycasts; }
    public class Canvas : Component { public float scaleFactor = 1; }
    public static class Input { public static Vector2 mousePosition => Vector2.zero; }
    public class Font { }
    public class RectOffset(int left, int right, int top, int bottom) { public int left = left, right = right, top = top, bottom = bottom; }
    public static class Time { public static float unscaledTime, timeScale = 1; public static int frameCount; }
    public enum TextAnchor { UpperRight, MiddleLeft }
    public enum HorizontalWrapMode { Wrap }
    public enum VerticalWrapMode { Truncate }
}
namespace UnityEngine.UI
{
    public class Image : UnityEngine.Component { public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform; }
    public class Selectable : UnityEngine.Component { public bool interactable = true; }
    public class Outline : UnityEngine.Component { public UnityEngine.Color effectColor; public UnityEngine.Vector2 effectDistance; public bool enabled; }
    public class LayoutElement : UnityEngine.Component { public float preferredWidth, preferredHeight; }
    public class VerticalLayoutGroup : UnityEngine.Component
    {
        public UnityEngine.RectOffset padding;
        public float spacing;
        public UnityEngine.TextAnchor childAlignment;
        public bool childControlWidth, childControlHeight, childForceExpandWidth, childForceExpandHeight;
        // Only the existing fixed-height, upper-right layout is modeled to expose its spacing seam.
        public void SimulateLayout()
        {
            float y = -padding.top;
            foreach (var child in transform.Children)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var size = child.GetComponent<LayoutElement>();
                if (size == null) continue;
                var rect = (UnityEngine.RectTransform)child;
                rect.sizeDelta = new(size.preferredWidth, size.preferredHeight);
                rect.anchoredPosition = new(-padding.right, y);
                y -= size.preferredHeight + spacing;
            }
        }
    }
}
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace UnityEngine.EventSystems
{
    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData data); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData data); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData data); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData data); }
    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData data); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData data); }
    public class BaseEventData { }
    public class PointerEventData : BaseEventData { public UnityEngine.Vector2 position; }
}
namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core
{
    public struct FakeColor { public UnityEngine.Color ToUnity() => new(); }
    public class FluentTheme { public FakeColor HudBackdrop, HudBorder, HudText; public float RadiusControl; }
    public class FluentSelectablePalette
    {
        public FakeColor DisabledFill, DisabledBorder, DisabledText, PressedFill, PressedBorder,
            NormalText, HoverFill, HoverBorder, FocusedFill, FocusedBorder, NormalFill, NormalBorder;
    }
    public static class FluentThemes { public static FluentTheme DarkInk = new(); }
}
namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime
{
    public class LegacyFluentSurface : IDisposable
    {
        public UnityEngine.Color CurrentFill, CurrentBorder;
        public void SetColors(UnityEngine.Color fill, UnityEngine.Color border) { CurrentFill = fill; CurrentBorder = border; }
        public static LegacyFluentSurface Attach(UnityEngine.GameObject root, UnityEngine.Color fill, UnityEngine.Color border, float radius) => new();
        public void Dispose() { }
    }
}
namespace UnturnedSingleplayerCheatMenu.UI
{
    public static class CheatMenuStyle
    {
        public static UnityEngine.Color DisabledSurface, DisabledText, ButtonBorderDisabled, Text, ButtonBorderPressed, ButtonBorderHover, ButtonBorder;
    }
    public class UiText : UnityEngine.Component
    {
        public UnityEngine.Font font;
        public string text;
        public int fontSize;
        public UnityEngine.Color color;
        public UnityEngine.TextAnchor alignment;
        public UnityEngine.HorizontalWrapMode horizontalOverflow;
        public UnityEngine.VerticalWrapMode verticalOverflow;
        public bool raycastTarget;
    }
    public static class SystemFontProvider { public static UnityEngine.Font Resolve(BepInEx.Logging.ManualLogSource log) => new(); }
    internal sealed partial class CheatMenuOverlayUi
    {
        internal class RootStub
        {
            internal Motion.UiMotionDriver Motion = new();
            internal void UnregisterLayerSurface(UnityEngine.GameObject root) { }
        }
        internal class HandlesStub { internal void DisposeSubtree(UnityEngine.GameObject root) { } }
        internal class DestroyStub { internal void Enqueue(UnityEngine.GameObject root, int frame, Action<UnityEngine.GameObject> hide) => hide(root); }
        internal readonly RootStub _uiRoot = new();
        readonly HandlesStub _fluentHandles = new();
        readonly DestroyStub _deferredDestroy = new();
        internal UnityEngine.GameObject _hoverExplanationRoot = new("Tooltip", typeof(UnityEngine.RectTransform));
        void CloseTransientVisualSurfaces() => HideHoverExplanation();
        internal void ShowForTest(bool warm)
        {
            var method = typeof(CheatMenuOverlayUi).GetMethod("ShowHoverExplanationVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object rect = _hoverExplanationRoot.GetComponent<UnityEngine.RectTransform>();
            // Accept the old signature for the red run; both runs execute the real production method.
            method.Invoke(this, method.GetParameters().Length == 1 ? new[] { rect } : new[] { rect, (object)warm });
        }
        internal void HideForTest() => HideHoverExplanation();
        internal void CloseForTest() => RunWithoutTransientMotion(HideHoverExplanation);
    }
}
