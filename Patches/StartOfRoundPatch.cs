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
    }
}
