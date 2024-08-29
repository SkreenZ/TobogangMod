﻿using GameNetcodeStuff;
using HarmonyLib;
using TobogangMod.Scripts;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerControllerPatch
    {
        [HarmonyPatch(nameof(PlayerControllerB.Start))]
        [HarmonyPostfix]
        private static void StartPostfix(PlayerControllerB __instance)
        {
            TobogangMod.Logger.LogDebug($"Player steam id: {__instance.playerSteamId}");
            __instance.gameObject.AddComponent<AudioSourcePlayer>();
        }

        [HarmonyPatch(nameof(PlayerControllerB.Update)), HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
        }

        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer)), HarmonyPostfix]
        private static void DamagePlayerPostfix(int damageNumber, bool hasDamageSFX, bool callRPC,
            CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force, PlayerControllerB __instance)
        {
            CramptesManager.Instance.TryGiveCramptesOnDamageServerRpc(__instance.NetworkObjectId);
        }

        [HarmonyPatch(nameof(PlayerControllerB.SetItemInElevator)), HarmonyPrefix]
        private static bool SetItemInElevatorPrefix(bool droppedInShipRoom, bool droppedInElevator,
            GrabbableObject gObject, PlayerControllerB __instance)
        {
            if (droppedInShipRoom && gObject.itemProperties.isScrap && gObject.scrapValue > 0 && !gObject.scrapPersistedThroughRounds && !RoundManager.Instance.scrapCollectedThisRound.Contains(gObject))
            {
#if DEBUG
                TobogangMod.Logger.LogDebug($"{__instance.playerUsername} dropped in ship {gObject.itemProperties.itemName}");
#endif
                CoinguesManager.Instance.AddCoinguesServerRpc(__instance.NetworkObject, gObject.scrapValue);
            }

            return true;
        }
    }
}
