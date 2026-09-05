using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Minimap;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Invoke(MinimapSceneryScope scope, string callback) =>
    typeof(MinimapSceneryScope).GetMethod(callback, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scope, null);
static Renderer Make(int layer) => new Renderer { gameObject = new GameObject { layer = layer } };

foreach (string cleanup in new[] { "OnPostRender", "OnDisable", "OnDestroy", "BeginFrame" })
{
    var cliff = Make(14);
    var sameNode = new Renderer { gameObject = cliff.gameObject };
    var hidden = Make(15); hidden.forceRenderingOff = true;
    var disabled = Make(16); disabled.enabled = false;
    var inactive = Make(17); inactive.gameObject.activeInHierarchy = false;
    var selected = new[] { cliff, cliff, sameNode, hidden, disabled, inactive };
    var scope = new MinimapSceneryScope();
    scope.Initialize(new ManualLogSource(), true, 31);
    scope.Arm(selected, true);
    Check(cliff.gameObject.layer == 14, "Scheduling must not mutate scene layers.");
    Invoke(scope, "OnPreCull");
    Check(cliff.gameObject.layer == 31 && sameNode.gameObject.layer == 31, "Selected cliff must enter terrain layer.");
    Check(hidden.gameObject.layer == 15 && disabled.gameObject.layer == 16 && inactive.gameObject.layer == 17,
        "Hidden, disabled and inactive objects must not be reactivated.");
    Invoke(scope, "OnPreCull"); // Idempotent: must not replace original receipts.
    if (cleanup == "BeginFrame") scope.Arm(null, false); else Invoke(scope, cleanup);
    Check(cliff.gameObject.layer == 14 && sameNode.gameObject.layer == 14, "Every cleanup must restore original layers despite duplicates.");
    Check(hidden.forceRenderingOff && !disabled.enabled && !inactive.gameObject.activeInHierarchy, "External visibility state changed.");
}

var mountain = Make(14);
var roadsScope = new MinimapSceneryScope();
var log = new ManualLogSource();
roadsScope.Initialize(log, false, 0);
roadsScope.Arm(new[] { mountain }, true);
Invoke(roadsScope, "OnPreCull");
Check(mountain.forceRenderingOff && mountain.gameObject.layer == 14, "Road pass hides cliff without changing its layer.");
mountain.FailNextRestore = true;
Invoke(roadsScope, "OnPostRender");
Check(mountain.forceRenderingOff && log.Warnings.Count == 1, "Restore failure must be observable and retain state.");
roadsScope.Arm(null, false);
Check(!mountain.forceRenderingOff, "Next-frame cleanup must retry failed restoration.");

var failingCliff = Make(12);
failingCliff.gameObject.FailNextLayer = 31;
var failingScope = new MinimapSceneryScope();
failingScope.Initialize(log, true, 31);
failingScope.Arm(new[] { failingCliff }, true);
Invoke(failingScope, "OnPreCull");
Check(failingCliff.gameObject.layer == 12, "A failed setup must restore the pre-change receipt.");
Invoke(failingScope, "OnPostRender");
Check(failingCliff.gameObject.layer == 12, "Cleanup after failed setup must be idempotent.");
var destroyedComponent = Make(13);
var survivingObject = destroyedComponent.gameObject;
var destroyedScope = new MinimapSceneryScope();
destroyedScope.Initialize(log, true, 31);
destroyedScope.Arm(new[] { destroyedComponent }, true);
Invoke(destroyedScope, "OnPreCull");
destroyedComponent.Destroyed = true;
Invoke(destroyedScope, "OnPostRender");
Check(survivingObject.layer == 13, "Destroying only the renderer must not strand a surviving GameObject on the terrain layer.");

const string On = "WATER_EDGEBLEND_ON", Off = "WATER_EDGEBLEND_OFF";
foreach (bool on in new[] { false, true })
foreach (bool off in new[] { false, true })
{
    Shader.Keywords.Clear();
    if (on) Shader.EnableKeyword(On);
    if (off) Shader.EnableKeyword(Off);
    var water = new MinimapWaterKeywordScope();
    Check(water.Begin() && !Shader.IsKeywordEnabled(On) && Shader.IsKeywordEnabled(Off), "Water capture must use the no-edge-fade variant.");
    water.Restore();
    Check(Shader.IsKeywordEnabled(On) == on && Shader.IsKeywordEnabled(Off) == off, "Both original keyword bits must be restored independently.");
}
Shader.Keywords.Clear(); Shader.EnableKeyword(On);
var retryWater = new MinimapWaterKeywordScope();
Check(retryWater.Begin(), "Water capture setup failed unexpectedly.");
Shader.FailEnableKeyword = On; Shader.FailEnableCount = 2;
retryWater.Restore();
Check(!retryWater.Begin(), "A pending failed restoration must prevent recapturing over the original receipt.");
retryWater.Restore();
Check(Shader.IsKeywordEnabled(On) && !Shader.IsKeywordEnabled(Off), "Retry must retain and restore the original water state.");
Console.WriteLine("Minimap scenery and water scope state tests passed (Unity rendering not simulated).");
float captureBias = MinimapWaterOpacityPolicy.ResolveCaptureBias("FX/Water4", 201, 0f);
Check(Math.Min(2f * captureBias + 0.5f, 1f) == 1f,
    "Installed low-quality Water4 must not remain half-transparent at orthographic normal incidence.");
Check(MinimapWaterOpacityPolicy.ResolveCaptureBias("Custom/Water", 201, 0f) == 0f &&
      MinimapWaterOpacityPolicy.ResolveCaptureBias("FX/Water4", 501, 0f) == 0f &&
      MinimapWaterOpacityPolicy.ResolveCaptureBias("FX/Water4", 201, 0.7f) == 0.7f,
    "Water opacity correction must not touch custom shaders, unverified higher LODs or stronger existing biases.");
var opacityMaterial = new Material { shader = new Shader { name = "FX/Water4", maximumLOD = 201 }, Value = new Vector4(0.25f, 0.5f, 0.4f, 0f) };
var opacityScope = new MinimapWaterOpacityScope();
Check(opacityScope.Begin(new[] { opacityMaterial, opacityMaterial }), "Opacity capture failed.");
Check(opacityMaterial.Value.w == 0.25f && opacityMaterial.SetCount == 1 &&
      opacityMaterial.Value.x == 0.25f && opacityMaterial.Value.y == 0.5f && opacityMaterial.Value.z == 0.4f,
    "Only Fresnel bias may change, and duplicate material bindings must not overwrite the receipt.");
opacityMaterial.FailRestoreCount = 2;
opacityScope.Restore();
Check(!opacityScope.Begin(new[] { opacityMaterial }), "Failed material restoration must block recapture.");
opacityScope.Restore();
Check(opacityMaterial.Value.w == 0f && opacityMaterial.Value.x == 0.25f && opacityMaterial.Value.y == 0.5f && opacityMaterial.Value.z == 0.4f,
    "Original full material vector must survive retries unchanged.");
Console.WriteLine("Low-quality water opacity and restoration checks passed.");
