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
            MethodInfo target = AccessTools.GetDeclaredMethods(typeof(PlayerMovement))
                .Where(method => method.Name == "simulate"
                    && method.ReturnType == typeof(void))
                .FirstOrDefault(IsWalkingSimulation);
            if (target == null)
                throw new MissingMethodException(
                    typeof(PlayerMovement).FullName,
                    "simulate(uint,int,int,int,float,float,bool,bool,float)");

            MethodInfo prefix = AccessTools.Method(
                typeof(MovementSimulationPatch),
                nameof(Prefix));
            MethodInfo postfix = AccessTools.Method(
                typeof(MovementSimulationPatch),
                nameof(Postfix));
            harmony.Patch(
                target,
                prefix: new HarmonyMethod(prefix),
                postfix: new HarmonyMethod(postfix));

            MethodInfo resimulate = AccessTools.DeclaredMethod(
                typeof(PlayerInput),
                "ClientResimulate");
            if (resimulate == null)
                throw new MissingMethodException(
                    typeof(PlayerInput).FullName,
                    "ClientResimulate");

            MethodInfo resimulationPrefix = AccessTools.Method(
                typeof(MovementSimulationPatch),
                nameof(ResimulationPrefix));
            MethodInfo resimulationPostfix = AccessTools.Method(
                typeof(MovementSimulationPatch),
                nameof(ResimulationPostfix));
            harmony.Patch(
                resimulate,
                prefix: new HarmonyMethod(resimulationPrefix),
                postfix: new HarmonyMethod(resimulationPostfix));
            log?.LogInfo(
                "[Movement] 已挂接 PlayerMovement.simulate 与重模拟保护；"
                + "飞行/穿墙将使用原生模拟周期移动。");
        }
        catch (Exception ex)
        {
            log?.LogWarning(
                "[Movement] 无法挂接 PlayerMovement.simulate；飞行功能保持关闭以避免回退到逐帧瞬移。\n"
                + ex);
        }
    }

    private static bool IsWalkingSimulation(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 9)
            return false;

        return parameters[0].ParameterType == typeof(uint)
            && parameters[1].ParameterType == typeof(int)
            && parameters[2].ParameterType == typeof(int)
            && parameters[3].ParameterType == typeof(int)
            && parameters[4].ParameterType == typeof(float)
            && parameters[5].ParameterType == typeof(float)
            && parameters[6].ParameterType == typeof(bool)
            && parameters[7].ParameterType == typeof(bool)
            && parameters[8].ParameterType == typeof(float);
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
        CheatMenuPlugin.Instance?.Movement?.SetSimulationResimulating(true);
    }

    private static void ResimulationPostfix()
    {
        CheatMenuPlugin.Instance?.Movement?.SetSimulationResimulating(false);
    }
}
