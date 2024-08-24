using HarmonyLib;
using TobogangMod.Model;
using TobogangMod.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Patches
{
    [HarmonyPatch]
    public class NetworkObjectManager
    {
        [HarmonyPostfix, HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        public static void Init()
        {
            if (NetworkPrefab != null)
                return;

            NetworkPrefab = (GameObject)TobogangMod.MainAssetBundle.LoadAsset("NetworkHandler");
            NetworkPrefab.AddComponent<NetworkHandler>();

            NetworkManager.Singleton.AddNetworkPrefab(NetworkPrefab);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        static void SpawnNetworkHandler()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                var networkHandlerHost = Object.Instantiate(NetworkPrefab, Vector3.zero, Quaternion.identity);
                networkHandlerHost.GetComponent<NetworkObject>().Spawn();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewFloor))]
        static void SubscribeToHandler()
        {
            NetworkHandler.LevelEvent += ReceivedEventFromServer;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
        static void UnsubscribeFromHandler()
        {
            NetworkHandler.LevelEvent -= ReceivedEventFromServer;
        }

        static void ReceivedEventFromServer(RandomSoundEvent e)
        {
            TobogangMod.Logger.LogInfo("Received event from server: " + e);
            RandomSound.PlaySoundForEnemy(e.SoundIndex, e.EnemyID);
        }

        static void SendEventToClients(RandomSoundEvent e)
        {
            if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
                return;

            NetworkHandler.Instance.EventClientRpc(e);
        }

        public static GameObject NetworkPrefab { get; private set; } = null!;
    }
}
