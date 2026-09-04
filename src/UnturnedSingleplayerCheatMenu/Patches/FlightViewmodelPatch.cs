using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class FlightViewmodelPatch
{
	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		try
		{
			MethodInfo target = AccessTools.DeclaredMethod(
				typeof(PlayerAnimator),
				"Update",
				Type.EmptyTypes);
			if (target == null || target.ReturnType != typeof(void))
			{
				throw new MissingMethodException(typeof(PlayerAnimator).FullName, "Update()");
			}

			MethodInfo transpiler = AccessTools.Method(typeof(FlightViewmodelPatch), nameof(Transpiler));
			harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
			log?.LogInfo(
				"[Movement] 已挂接飞行视模稳定；本地飞行时仅跳过 PlayerAnimator.Update 的空中视模倾斜，真实落地状态保持不变。");
		}
		catch (Exception exception)
		{
			log?.LogWarning(
				"[Movement] 无法挂接飞行视模稳定；飞行瞄准仍可能受到原版空中视模倾斜。\n" + exception);
		}
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		List<CodeInstruction> rewritten = new List<CodeInstruction>(instructions);
		MethodInfo nativeGroundedGetter = AccessTools.PropertyGetter(
			typeof(PlayerMovement),
			nameof(PlayerMovement.isGrounded));
		MethodInfo flightAwareGroundedGetter = AccessTools.Method(
			typeof(FlightViewmodelPatch),
			nameof(IsGroundedForViewmodel));

		int replacementCount = 0;
		foreach (CodeInstruction instruction in rewritten)
		{
			if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
				Equals(instruction.operand, nativeGroundedGetter))
			{
				instruction.opcode = OpCodes.Call;
				instruction.operand = flightAwareGroundedGetter;
				replacementCount++;
			}
		}

		if (replacementCount != 1)
		{
			throw new InvalidProgramException(
				$"Expected exactly one PlayerMovement.isGrounded read in PlayerAnimator.Update, found {replacementCount}.");
		}

		return rewritten;
	}

	private static bool IsGroundedForViewmodel(PlayerMovement movement)
	{
		if (movement == null)
		{
			return true;
		}

		bool isLocalMovement = ReferenceEquals(movement, Player.LocalPlayer?.movement);
		bool flightEnabled = CheatMenuPlugin.Instance?.Movement?.FlightEnabled == true;
		return FlightViewmodelPolicy.ShouldTreatAsGrounded(
			movement.isGrounded,
			isLocalMovement,
			flightEnabled);
	}
}
