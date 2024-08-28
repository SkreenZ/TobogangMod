using HarmonyLib;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager))]
    public class GameNetworkManagerPatch
    {
        [HarmonyPatch(nameof(GameNetworkManager.Start)), HarmonyPostfix]
        public static void Init()
        {
            NetworkManager.Singleton.AddNetworkPrefab(TobogangMod.NetworkPrefab);
        }

        [HarmonyPatch(nameof(GameNetworkManager.SaveGame)), HarmonyPostfix]
        private static void SaveGamePostfix(GameNetworkManager __instance)
        {
            var prefix = TobogangMod.Instance.Info.Metadata.Name + "_";

            foreach (var player in CoinguesManager.Instance.GetRegisteredPlayers())
            {
                ES3.Save(prefix + "Coingues_" + player, CoinguesManager.Instance.GetCoingues(player), __instance.currentSaveFileName);
            }
        }
    }
}
