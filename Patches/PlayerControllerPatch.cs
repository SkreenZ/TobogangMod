using GameNetcodeStuff;
using HarmonyLib;
using TobogangMod.Scripts;
using Unity.Netcode;
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

            if (NetworkManager.Singleton.IsServer)
            {
                GameObject randomSoundObject = GameObject.Instantiate(RandomSound.NetworkPrefab, __instance.transform.position, Quaternion.identity);
                var randomSound = randomSoundObject.GetComponent<RandomSound>();
                randomSound.NetworkObject.Spawn();

                randomSound.SetParentClientRpc(__instance.NetworkObject);
                randomSound.SetActiveServerRpc(false);
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.Update)), HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
#if DEBUG
            __instance.sprintMeter = 1f;
#endif
        }

        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer)), HarmonyPostfix]
        private static void DamagePlayerPostfix(int damageNumber, bool hasDamageSFX, bool callRPC,
            CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force, PlayerControllerB __instance)
        {
#if DEBUG
            TobogangMod.Logger.LogDebug($"{__instance.playerUsername} took {damageNumber} damage from {causeOfDeath}");
#endif

            if (causeOfDeath != CauseOfDeath.Bludgeoning)
            {
                CramptesManager.Instance.TryGiveCramptesOnDamageServerRpc(__instance.NetworkObjectId);
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.SetItemInElevator)), HarmonyPrefix]
        private static bool SetItemInElevatorPrefix(bool droppedInShipRoom, bool droppedInElevator,
            GrabbableObject gObject, PlayerControllerB __instance)
        {
            bool isServer = NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (!isServer)
            {
                return true;
            }

            if (droppedInShipRoom && gObject.itemProperties.isScrap && gObject.scrapValue > 0 && !gObject.scrapPersistedThroughRounds && !RoundManager.Instance.scrapCollectedThisRound.Contains(gObject))
            {
#if DEBUG
                TobogangMod.Logger.LogDebug($"{__instance.playerUsername} dropped in ship {gObject.itemProperties.itemName}");
#endif
                CoinguesManager.Instance.AddCoinguesServerRpc(__instance.NetworkObject, gObject.scrapValue);
                CramptesManager.Instance.TryLoseCramptesServerRpc();
            }

            return true;
        }

        [HarmonyPatch(nameof(PlayerControllerB.KillPlayer)), HarmonyPostfix]
        private static void KillPlayerPostfix(CauseOfDeath causeOfDeath, PlayerControllerB __instance)
        {
            CoinguesManager.Instance.RemoveCoinguesServerRpc(__instance.NetworkObject, CoinguesManager.DEATH_MALUS);
        }
    }
}
