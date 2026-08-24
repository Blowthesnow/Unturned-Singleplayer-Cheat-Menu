using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class MovementSimulationPatch
{
	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		try
		{
			MethodInfo methodInfo = (from methodInfo3 in AccessTools.GetDeclaredMethods(typeof(PlayerMovement))
				where methodInfo3.Name == "simulate" && methodInfo3.ReturnType == typeof(void)
				select methodInfo3).FirstOrDefault(IsWalkingSimulation);
			if (methodInfo == null)
			{
				throw new MissingMethodException(typeof(PlayerMovement).FullName, "simulate(uint,int,int,int,float,float,bool,bool,float)");
			}
			MethodInfo method = AccessTools.Method(typeof(MovementSimulationPatch), "Prefix");
			MethodInfo method2 = AccessTools.Method(typeof(MovementSimulationPatch), "Postfix");
			harmony.Patch(methodInfo, new HarmonyMethod(method), new HarmonyMethod(method2));
			MethodInfo methodInfo2 = AccessTools.DeclaredMethod(typeof(PlayerInput), "ClientResimulate");
			if (methodInfo2 == null)
			{
				throw new MissingMethodException(typeof(PlayerInput).FullName, "ClientResimulate");
			}
			MethodInfo method3 = AccessTools.Method(typeof(MovementSimulationPatch), "ResimulationPrefix");
			MethodInfo method4 = AccessTools.Method(typeof(MovementSimulationPatch), "ResimulationPostfix");
			harmony.Patch(methodInfo2, new HarmonyMethod(method3), new HarmonyMethod(method4));
			log?.LogInfo("[Movement] 已挂接 PlayerMovement.simulate 与重模拟保护；飞行/穿墙将使用原生模拟周期移动。");
		}
		catch (Exception ex)
		{
			log?.LogWarning("[Movement] 无法挂接 PlayerMovement.simulate；飞行功能保持关闭以避免回退到逐帧瞬移。\n" + ex);
		}
	}

	private static bool IsWalkingSimulation(MethodInfo method)
	{
		ParameterInfo[] parameters = method.GetParameters();
		if (parameters.Length != 9)
		{
			return false;
		}
		if (parameters[0].ParameterType == typeof(uint) && parameters[1].ParameterType == typeof(int) && parameters[2].ParameterType == typeof(int) && parameters[3].ParameterType == typeof(int) && parameters[4].ParameterType == typeof(float) && parameters[5].ParameterType == typeof(float) && parameters[6].ParameterType == typeof(bool) && parameters[7].ParameterType == typeof(bool))
		{
			return parameters[8].ParameterType == typeof(float);
		}
		return false;
	}

	private static void Prefix(PlayerMovement __instance)
	{
		CheatMenuPlugin.Instance?.Movement?.PrepareNativeSimulation(__instance);
	}

	private static void Postfix(PlayerMovement __instance, float deltaTime)
	{
		CheatMenuPlugin.Instance?.Movement?.SimulateMovement(__instance, deltaTime);
	}

	private static void ResimulationPrefix()
	{
		CheatMenuPlugin.Instance?.Movement?.SetSimulationResimulating(value: true);
	}

	private static void ResimulationPostfix()
	{
		CheatMenuPlugin.Instance?.Movement?.SetSimulationResimulating(value: false);
	}
}
