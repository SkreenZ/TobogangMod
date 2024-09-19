using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LethalLib.Modules;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    public enum LevelIds
    {
        Company = 3
    }

    public enum UnlockableIds
    {
        DiscoBall = 27
    }

    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPatch
    {

        [HarmonyPostfix, HarmonyPatch(nameof(StartOfRound.Awake))]
        private static void AwakePostfix(StartOfRound __instance)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                GameObject cramptesManager = GameObject.Instantiate(CramptesManager.NetworkPrefab, Vector3.zero, Quaternion.identity);
                cramptesManager.GetComponent<NetworkObject>().Spawn();

                GameObject coinguesManager = GameObject.Instantiate(CoinguesManager.NetworkPrefab, Vector3.zero, Quaternion.identity);
                coinguesManager.GetComponent<NetworkObject>().Spawn();
            }

#if DEBUG
            __instance.speakerAudioSource.volume = 0f; // Just so i don't go insane while testing
#endif
        }

        [HarmonyPatch(nameof(StartOfRound.UpdatePlayerVoiceEffects)), HarmonyPostfix]
        private static void UpdatePlayerVoiceEffectsPostfix(StartOfRound __instance)
        {
            foreach (var player in __instance.allPlayerScripts)
            {
                if (player != __instance.localPlayerController && player.voicePlayerState != null && 
                    (CoinguesManager.Instance.MutedPlayers.Contains(player) || CoinguesManager.Instance.DeafenedPlayers.Contains(__instance.localPlayerController)))
                {
                    player.voicePlayerState.Volume = 0f;
                }
            }
        }

        [HarmonyPatch(nameof(StartOfRound.StartGame)), HarmonyPostfix]
        private static void StartGamePostfix(StartOfRound __instance)
        {
            TobogangMod.Logger.LogDebug("Starting a new game");

#if DEBUG
            foreach (var scrap in __instance.currentLevel.spawnableScrap)
            {
                TobogangMod.Logger.LogDebug($"Spawnable scrap: {scrap.spawnableItem.name}, rarity: {scrap.rarity}");
            }
#endif

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                CoinguesManager.Instance.ResetClaimsServerRpc();

                if (__instance.currentLevel.levelID == (int)LevelIds.Company)
                {
                    TobogangMod.Logger.LogDebug("Spawning toboggan");
                    var toboggan = GameObject.Instantiate(TobogangMod.TobogganPrefab, new Vector3(-17.76f, -2.63f, -44.9f), Quaternion.Euler(0f, 180f, 0f));
                    toboggan.GetComponent<NetworkObject>().Spawn();
                }
            }
        }

        [HarmonyPatch(nameof(StartOfRound.ShipHasLeft)), HarmonyPostfix]
        private static void ShipHasLeftPostfix(StartOfRound __instance)
        {
            if (!__instance.IsServer)
            {
                return;
            }

            var toboggan = GameObject.FindFirstObjectByType<TobogganScript>();

            if (toboggan != null)
            {
                TobogangMod.Logger.LogDebug("Despawning toboggan");
                toboggan.GetComponent<NetworkObject>().Despawn();
            }
        }

        [HarmonyPatch(nameof(StartOfRound.PassTimeToNextDay)), HarmonyPostfix]
        private static void PassTimeToNextDayPostfix(StartOfRound __instance)
        {
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
            {
                return;
            }

            TobogangMod.Logger.LogDebug($"PassTimeToNextDayPostfix, remaining: {TimeOfDay.Instance.daysUntilDeadline}");
            var allPlayers = __instance.allPlayerScripts.ToList().FindAll(p => p.isPlayerControlled);

            for (int i = 0; i < allPlayers.Count; ++i)
            {
                var profit = __instance.gameStats.allPlayerStats[i].profitable;
                TobogangMod.Logger.LogDebug($"Player {i} profit this round: {profit}");

                var playerId = CoinguesManager.GetPlayerId(allPlayers[i]);
                var playerProfit = CoinguesManager.Instance.PlayerProfits.GetValueOrDefault(playerId, 0);

                CoinguesManager.Instance.PlayerProfits[playerId] = playerProfit + profit;
                TobogangMod.Logger.LogDebug($"Player {i} total profit: {CoinguesManager.Instance.PlayerProfits[playerId]}");
            }

            if (TimeOfDay.Instance.daysUntilDeadline == 0)
            {
                TobogangMod.Logger.LogDebug("Finishing betcoingues");
                CoinguesManager.Instance.FinishBetcoingueServerRpc();
            }
        }
    }
}
