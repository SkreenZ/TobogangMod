using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class AutoDespawnScript : NetworkBehaviour
    {
        public float DespawnTimeout = 5f;

        void Start()
        {
            if (IsServer || IsHost)
            {
                StartCoroutine(WaitAndDespawn());
            }
        }

        private IEnumerator WaitAndDespawn()
        {
            yield return new WaitForSeconds(DespawnTimeout);

            gameObject.GetComponent<NetworkObject>().Despawn();
        }
    }
}
