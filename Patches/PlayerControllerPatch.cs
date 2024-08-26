using GameNetcodeStuff;
using HarmonyLib;
using TobogangMod.Scripts;
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

        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPostfix]
        private static void DamagePlayerPostfix(int damageNumber, bool hasDamageSFX, bool callRPC, CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force, PlayerControllerB __instance)
        {
            CramptesManager.Instance.TryGiveCramptesOnDamageServerRpc(__instance.NetworkObjectId);
        }
    }
}
