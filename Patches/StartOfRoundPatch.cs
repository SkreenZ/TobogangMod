using HarmonyLib;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch]
    public class StartOfRoundPatch
    {

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void AwakePostfix()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                foreach (var item in TobogangMod.ContentLoader.LoadedContent.Values)
                {
                    var scrap = item as LethalLib.Modules.ContentLoader.ScrapItem;
                    if (scrap != null)
                    {
                        StartOfRound.Instance.allItemsList.itemsList.Add(scrap.Item);
                    }
                }

                GameObject cramptesManager = GameObject.Instantiate(CramptesManager.NetworkPrefab, Vector3.zero, Quaternion.identity);
                cramptesManager.GetComponent<NetworkObject>().Spawn();

                GameObject coinguesManager = GameObject.Instantiate(CoinguesManager.NetworkPrefab, Vector3.zero, Quaternion.identity);
                coinguesManager.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
