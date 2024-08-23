using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerControllerPatch
    {
        [HarmonyPatch(nameof(PlayerControllerB.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
            if (__instance.isExhausted)
            {
                //__instance.KillPlayer(Vector3.zero);
            }
        }
    }
}
