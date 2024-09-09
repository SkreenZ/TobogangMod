﻿using System.Collections.Generic;
using HarmonyLib;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPatch
    {

        [HarmonyPostfix, HarmonyPatch(nameof(StartOfRound.Awake))]
        private static void AwakePostfix(StartOfRound __instance)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                foreach (var item in TobogangMod.ContentLoader.LoadedContent.Values)
                {
                    var scrap = item as LethalLib.Modules.ContentLoader.ScrapItem;
                    if (scrap != null)
                    {
                        __instance.allItemsList.itemsList.Add(scrap.Item);
                    }
                }

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

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                CoinguesManager.Instance.ResetClaimsServerRpc();
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

            for (int i = 0; i < __instance.allPlayerScripts.Length; ++i)
            {
                var profit = __instance.gameStats.allPlayerStats[i].profitable;
                TobogangMod.Logger.LogDebug($"Player {i} profit this round: {profit}");

                var playerId = CoinguesManager.GetPlayerId(__instance.allPlayerScripts[i]);
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
