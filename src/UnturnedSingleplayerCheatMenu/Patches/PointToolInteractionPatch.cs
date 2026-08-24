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
	private sealed class RangeOverrideScope : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
				_rangeOverrideDepth = Math.Max(0, _rangeOverrideDepth - 1);
			}
		}
	}

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
		PatchDistanceConstant(harmony, log, typeof(InteractableStorage), "ReceiveInteractRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableDoor), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableGenerator), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableSpot), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableFire), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableOven), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableOxygenator), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(InteractableSafezone), "ReceiveToggleRequest", 400f, "GetInteractionRangeSquared");
		PatchDistanceConstant(harmony, log, typeof(VehicleManager), "ReceiveEnterVehicleRequest", 100f, "GetVehicleInteractionRangeSquared");
	}

	internal static float GetInteractionRangeSquared()
	{
		if (!IsRangeOverrideActive)
		{
			return 400f;
		}
		return _rangeOverride * _rangeOverride;
	}

	internal static float GetVehicleInteractionRangeSquared()
	{
		if (!IsRangeOverrideActive)
		{
			return 100f;
		}
		return _rangeOverride * _rangeOverride;
	}

	private static void PatchDistanceConstant(Harmony harmony, ManualLogSource log, Type declaringType, string methodName, float originalValue, string replacementMethodName)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(declaringType, methodName);
			if (methodInfo == null)
			{
				throw new MissingMethodException(declaringType.FullName, methodName);
			}
			MethodInfo method = AccessTools.Method(typeof(PointToolInteractionPatch), "ReplaceDistanceConstant");
			MethodInfo methodInfo2 = AccessTools.Method(typeof(PointToolInteractionPatch), replacementMethodName);
			harmony.Patch(methodInfo, null, null, new HarmonyMethod(method));
			log?.LogInfo("[PointTool] 已挂接 " + declaringType.FullName + "." + methodName + " 距离校验；" + string.Format("原生阈值 {0:0.##}，准星工具请求时使用 PointTool.Range。替换方法={1}。", originalValue, methodInfo2?.Name ?? "missing"));
		}
		catch (Exception arg)
		{
			log?.LogWarning($"[PointTool] 无法挂接 {declaringType.FullName}.{methodName} 的距离校验：{arg}");
		}
	}

	private static IEnumerable<CodeInstruction> ReplaceDistanceConstant(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		float originalValue = ((__originalMethod.DeclaringType == typeof(VehicleManager)) ? 100f : 400f);
		string name = ((__originalMethod.DeclaringType == typeof(VehicleManager)) ? "GetVehicleInteractionRangeSquared" : "GetInteractionRangeSquared");
		MethodInfo replacement = AccessTools.Method(typeof(PointToolInteractionPatch), name);
		foreach (CodeInstruction instruction in instructions)
		{
			if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float num && Math.Abs(num - originalValue) < 0.001f)
			{
				instruction.opcode = OpCodes.Call;
				instruction.operand = replacement;
			}
			yield return instruction;
		}
	}
}
