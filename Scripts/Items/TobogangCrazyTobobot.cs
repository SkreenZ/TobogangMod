using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts.Items
{
    internal class TobogangCrazyTobobot : TobogangItem
    {
        private static readonly float CRAZY_DURATION = 30f;

        public static List<PlayerControllerB> CrazyPlayers = [];

        TobogangCrazyTobobot()
        {
            TobogangItemId = TobogangMod.TobogangItems.CRAZY_TOBOBOT;
            CoinguesPrice = 200;
            Keywords = ["crazy tobobot", "crazy", "crazy ", "crazy t", "crazy to", "crazy tob", "crazy tobo", "crazy tobob", "crazy tobobo",
                        "crazyt", "crazyto", "crazytob", "crazytobo", "crazytobob", "crazytobobo", "crazytobobot"];
        }

        protected override void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            SetPlayerIsCrazyServerRpc(targetPlayerOrEnemy.GetComponent<PlayerControllerB>().NetworkObject, true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerIsCrazyServerRpc(NetworkObjectReference playerRef, bool isCrazy)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            TobogangMod.Logger.LogDebug("SetPlayerIsCrazyServerRpc");

            SetPlayerIsCrazyClientRpc(playerRef, isCrazy);

            var randomSound = playerNet.GetComponentInChildren<RandomSound>();

            randomSound.SetCrazy(isCrazy);
            randomSound.SetActiveServerRpc(isCrazy);

            if (isCrazy)
            {
                StartCoroutine(WaitAndRemoveCrazyFromPlayer(playerNet.GetComponentInChildren<PlayerControllerB>()));
            }
        }

        [ClientRpc]
        void SetPlayerIsCrazyClientRpc(NetworkObjectReference playerRef, bool isCrazy)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.GetComponentInChildren<PlayerControllerB>();

            if (player == null)
            {
                return;
            }

            if (isCrazy && !CrazyPlayers.Contains(player))
            {
                CrazyPlayers.Add(player);
            }
            else if (!isCrazy)
            {
                CrazyPlayers.Remove(player);
            }
        }

        private IEnumerator WaitAndRemoveCrazyFromPlayer(PlayerControllerB player)
        {
            yield return new WaitForSeconds(CRAZY_DURATION);

            SetPlayerIsCrazyServerRpc(player.NetworkObject, false);
        }
    }
}
