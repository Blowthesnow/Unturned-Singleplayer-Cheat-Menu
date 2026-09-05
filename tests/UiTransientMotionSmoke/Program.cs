using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI;
using UnturnedSingleplayerCheatMenu.UI.Motion;

int failed = 0, passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Near(float value, float expected, string message) => Check(Math.Abs(value - expected) < 0.001f, $"{message}: actual={value}, expected={expected}");
void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
CheatMenuOverlayUi Tooltip()
{
    var ui = new CheatMenuOverlayUi(); ui._hoverExplanationRoot.SetActive(false); return ui;
}
Run("cold tooltip retains entry and click-through", () =>
{
    var ui = Tooltip(); ui.ShowForTest(false);
    var group = ui._hoverExplanationRoot.GetComponent<CanvasGroup>();
    Near(group.alpha, 0, "cold start");
    Check(!group.blocksRaycasts && !group.interactable, "tooltip captures input");
    ui._uiRoot.Motion.Pump(0.11f); Near(group.alpha, 1, "cold completion");
});
Run("warm tooltip after prior exit is immediately legible", () =>
{
    var ui = Tooltip(); ui.ShowForTest(false); ui._uiRoot.Motion.Pump(0.11f);
    ui.HideForTest(); ui._uiRoot.Motion.Pump(0.11f); ui.ShowForTest(true);
    Near(ui._hoverExplanationRoot.GetComponent<CanvasGroup>().alpha, 1, "warm alpha");
    Near(ui._hoverExplanationRoot.transform.localScale.x, 1, "warm scale");
    Check(ui._uiRoot.Motion.ActiveCount == 0, "warm switch creates animation");
});
Run("warm reversal cancels old exit completion", () =>
{
    var ui = Tooltip(); ui.ShowForTest(false); ui._uiRoot.Motion.Pump(0.11f);
    ui.HideForTest(); ui._uiRoot.Motion.Pump(0.05f); ui.ShowForTest(true);
    Near(ui._hoverExplanationRoot.GetComponent<CanvasGroup>().alpha, 1, "warm reversal alpha");
    ui._uiRoot.Motion.Pump(1f);
    Check(ui._hoverExplanationRoot.activeSelf, "old completion hid replacement tooltip");
    ui.CloseForTest(); ui._uiRoot.Motion.Pump(1f);
    Check(!ui._hoverExplanationRoot.activeSelf && ui._uiRoot.Motion.ActiveCount == 0, "forced close did not settle");
});
Run("toast removal includes spacing without final jump", () =>
{
    using var f = new ToastFixture();
    f.Show("A", 1); f.Show("B", 10); f.Show("C", 10); f.Tick(0.18f);
    var b = f.Roots[1]; Near(b.anchoredPosition.y, -78, "initial second slot");
    f.ExpireFirst(); f.Tick(0.129f);
    float beforeRemoval = b.anchoredPosition.y;
    f.Tick(0.0011f);
    Near(b.anchoredPosition.y, -24, "second slot became first");
    Check(Math.Abs(b.anchoredPosition.y - beforeRemoval) < 1f, "slot removal leaves an 8px spacing jump");
});
Run("toast exit preserves text surface height", () =>
{
    using var f = new ToastFixture(); f.Show("A", 1); f.Show("B", 10); f.Tick(0.18f);
    var a = f.Roots[0]; var visual = (RectTransform)a.GetChild(0);
    Near(visual.rect.height, 46, "initial toast height");
    f.ExpireFirst(); f.Tick(0.065f);
    Near(visual.rect.height, 46, "exit squashes text surface");
});
Run("reflow retarget and teardown settle", () =>
{
    using var f = new ToastFixture(); f.Show("A", 10); f.Show("B", 10); f.Show("C", 10); f.Tick(0.18f);
    var c = f.Roots[2]; f.Show("D", 10); f.Tick(0.04f);
    float current = c.anchoredPosition.y;
    f.Show("E", 10); Near(c.anchoredPosition.y, current, "retarget jumped from current position");
    f.Tick(0.2f); Near(c.anchoredPosition.y, -24, "surviving oldest slot");
    Check(f.Roots.Length == 3, "exiting slots leaked");
    f.Service.Dispose(); f.Driver.Pump(1f);
    Check(f.Roots.Length == 0 && f.Driver.ActiveCount == 0, "teardown retained objects or animations");
});
Run("toast evicted during reflow freezes its outgoing row", () =>
{
    using var f = new ToastFixture(); f.Show("A", 10); f.Show("B", 10); f.Show("C", 10); f.Tick(0.03f);
    var b = f.Roots[1]; f.Show("D", 10); f.Tick(0.04f);
    float y = b.anchoredPosition.y;
    float alpha = b.GetChild(0).GetComponent<CanvasGroup>().alpha;
    f.Show("E", 10);
    Near(b.GetChild(0).GetComponent<CanvasGroup>().alpha, alpha, "exit restarted opacity");
    f.Tick(0.04f); Near(b.anchoredPosition.y, y, "exiting row continued moving");
    f.Tick(0.2f); Check(f.Roots.Length == 3, "burst eviction leaked slots");
});
Run("simultaneous expiry cleans all slots", () =>
{
    using var f = new ToastFixture(); f.Show("A", 1); f.Show("B", 1); f.Show("C", 1); f.Tick(0.18f);
    f.ExpireFirst(); f.Tick(0.2f);
    Check(f.Roots.Length == 0 && f.Driver.ActiveCount == 0, "batch expiry retained slots or motion");
    f.Show("D", 10); Near(f.Roots[0].anchoredPosition.y, -24, "replacement stack retained gap");
});
Run("no-driver fallback keeps slots and cleanup synchronous", () =>
{
    using var f = new ToastFixture(withMotion: false); f.Show("A", 1); f.Show("B", 10);
    var b = f.Roots[1]; f.ExpireFirst();
    Near(b.anchoredPosition.y, -24, "fallback failed to reflow");
    Near(b.GetChild(0).GetComponent<CanvasGroup>().alpha, 1, "fallback toast is invisible");
    Check(f.Roots.Length == 1, "fallback exit was deferred");
});
Run("cold delay and warm boundary flow through real control and bridge", () =>
{
    Time.unscaledTime = 100f;
    var ui = Tooltip();
    var bridge = new LegacyFluentTooltipBridge((message, target, position, warm) => ui.ShowForTest(warm), ui.HideForTest);
    var controls = Enumerable.Range(0, 3).Select(i => new GameObject("Control" + i).AddComponent<OverlaySelectableVisual>()).ToArray();
    var onEnable = typeof(OverlaySelectableVisual).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var onDisable = typeof(OverlaySelectableVisual).GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var pointer = new UnityEngine.EventSystems.PointerEventData();
    try
    {
        foreach (var control in controls) { onEnable.Invoke(control, null); bridge.Attach(control, "Description", true); }
        controls[0].OnPointerEnter(pointer);
        Time.unscaledTime = 100.44f; OverlaySelectableVisual.PumpAll();
        Check(!ui._hoverExplanationRoot.activeSelf, "cold tooltip ignored delay");
        Time.unscaledTime = 100.451f; OverlaySelectableVisual.PumpAll();
        Check(ui._hoverExplanationRoot.activeSelf, "cold tooltip never appeared");
        Near(ui._hoverExplanationRoot.GetComponent<CanvasGroup>().alpha, 0, "cold tooltip skipped fade");
        ui._uiRoot.Motion.Pump(0.11f); controls[0].OnPointerExit(pointer); ui._uiRoot.Motion.Pump(0.11f);
        Time.unscaledTime = 101.14f; controls[1].OnPointerEnter(pointer);
        Near(ui._hoverExplanationRoot.GetComponent<CanvasGroup>().alpha, 1, "warm flag lost across bridge");
        Check(ui._uiRoot.Motion.ActiveCount == 0, "warm browsing animated");
        controls[1].OnPointerExit(pointer); ui._uiRoot.Motion.Pump(0.11f);
        Time.unscaledTime = 101.85f; controls[2].OnPointerEnter(pointer);
        Check(!ui._hoverExplanationRoot.activeSelf, "expired warm window bypassed cold delay");
    }
    finally { foreach (var control in controls) onDisable.Invoke(control, null); ui.CloseForTest(); }
});
Run("paused frame loop expires notifications using unscaled time", () =>
{
    using var f = new ToastFixture(); f.Show("A", 1); f.Show("B", 1); f.Show("C", 1);
    Time.timeScale = 0;
    try
    {
        for (int i = 0; i < 20; i++) f.Tick(0.1f);
        Check(f.Roots.Length == 0 && f.Driver.ActiveCount == 0, "paused frame loop did not expire notifications");
    }
    finally { Time.timeScale = 1; }
});
Console.WriteLine($"TRANSIENT_MOTION_TESTS={passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class ToastFixture : IDisposable
{
    internal readonly GameObject Layer = new("ToastLayer", typeof(RectTransform));
    internal readonly UiMotionDriver Driver = new();
    internal readonly UiToastService Service;
    internal ToastFixture(bool withMotion = true) { Time.unscaledTime = 0; Service = new((RectTransform)Layer.transform, null, withMotion ? Driver : null); }
    internal RectTransform[] Roots => Layer.transform.Children.Select(c => (RectTransform)c).ToArray();
    internal void Layout() => Layer.GetComponent<VerticalLayoutGroup>()?.SimulateLayout();
    internal void Show(string text, float seconds) { Service.Show(text, seconds, Color.white); Layout(); }
    internal void Tick(float delta) { Time.unscaledTime += delta; Service.Pump(); Driver.Pump(delta); Layout(); }
    internal void ExpireFirst() { Time.unscaledTime = 1f; Service.Pump(); Layout(); }
    public void Dispose() { Service.Dispose(); Driver.Dispose(); }
}
