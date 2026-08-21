using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class PointToolInteractionPatch
{
    private const float NativeStorageRangeSquared = 400f;
    private const float NativeInteractionRangeSquared = 400f;
    private const float NativeVehicleRangeSquared = 100f;

    private static int _rangeOverrideDepth;
    private static float _rangeOverride = 100f;

    internal static bool IsRangeOverrideActive => _rangeOverrideDepth > 0;

    internal static IDisposable BeginRangeOverride(float range)
    {
        _rangeOverride = Mathf.Clamp(range, 5f, 250f);
        _rangeOverrideDepth++;
        return new RangeOverrideScope();
    }

    internal static void Install(Harmony harmony, ManualLogSource log)
    {
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableStorage),
            "ReceiveInteractRequest",
            NativeStorageRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableDoor),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableGenerator),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableSpot),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableFire),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableOven),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableOxygenator),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(InteractableSafezone),
            "ReceiveToggleRequest",
            NativeInteractionRangeSquared,
            nameof(GetInteractionRangeSquared));
        PatchDistanceConstant(
            harmony,
            log,
            typeof(VehicleManager),
            "ReceiveEnterVehicleRequest",
            NativeVehicleRangeSquared,
            nameof(GetVehicleInteractionRangeSquared));
    }

    internal static float GetInteractionRangeSquared()
    {
        return IsRangeOverrideActive
            ? _rangeOverride * _rangeOverride
            : NativeInteractionRangeSquared;
    }

    internal static float GetVehicleInteractionRangeSquared()
    {
        return IsRangeOverrideActive
            ? _rangeOverride * _rangeOverride
            : NativeVehicleRangeSquared;
    }

    private static void PatchDistanceConstant(
        Harmony harmony,
        ManualLogSource log,
        Type declaringType,
        string methodName,
        float originalValue,
        string replacementMethodName)
    {
        try
        {
            MethodInfo target = AccessTools.DeclaredMethod(declaringType, methodName);
            if (target == null)
                throw new MissingMethodException(declaringType.FullName, methodName);

            MethodInfo transpiler = AccessTools.Method(
                typeof(PointToolInteractionPatch),
                nameof(ReplaceDistanceConstant));
            MethodInfo replacement = AccessTools.Method(
                typeof(PointToolInteractionPatch),
                replacementMethodName);
            harmony.Patch(
                target,
                transpiler: new HarmonyMethod(transpiler));
            log?.LogInfo(
                $"[PointTool] 已挂接 {declaringType.FullName}.{methodName} 距离校验；" +
                $"原生阈值 {originalValue:0.##}，准星工具请求时使用 PointTool.Range。替换方法={replacement?.Name ?? "missing"}。");
        }
        catch (Exception ex)
        {
            log?.LogWarning(
                $"[PointTool] 无法挂接 {declaringType.FullName}.{methodName} 的距离校验：{ex}");
        }
    }

    private static IEnumerable<CodeInstruction> ReplaceDistanceConstant(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        float originalValue = __originalMethod.DeclaringType == typeof(VehicleManager)
            ? NativeVehicleRangeSquared
            : NativeInteractionRangeSquared;
        string replacementMethodName = __originalMethod.DeclaringType == typeof(VehicleManager)
            ? nameof(GetVehicleInteractionRangeSquared)
            : nameof(GetInteractionRangeSquared);
        MethodInfo replacement = AccessTools.Method(
            typeof(PointToolInteractionPatch),
            replacementMethodName);

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_R4
                && instruction.operand is float value
                && Math.Abs(value - originalValue) < 0.001f)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }

            yield return instruction;
        }
    }

    private sealed class RangeOverrideScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _rangeOverrideDepth = Math.Max(0, _rangeOverrideDepth - 1);
        }
    }
}
