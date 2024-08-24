using System;
using System.Collections.Generic;
using System.Text;
using TobogangMod.Model;
using TobogangMod.Patches;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class NetworkHandler : NetworkBehaviour
    {

        public static NetworkHandler Instance { get; private set; }

        public static event Action<RandomSoundEvent> LevelEvent;

        [ClientRpc]
        public void EventClientRpc(RandomSoundEvent e)
        {
            LevelEvent?.Invoke(e);
        }

        public override void OnNetworkSpawn()
        {
            LevelEvent = null;

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>().Despawn();
            Instance = this;

            base.OnNetworkSpawn();
        }
    }
}
