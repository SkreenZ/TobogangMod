using HarmonyLib;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch]
    public class GameNetworkManagerPatch
    {
        [HarmonyPostfix, HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        public static void Init()
        {
            NetworkManager.Singleton.AddNetworkPrefab(TobogangMod.NetworkPrefab);
        }
    }
}
