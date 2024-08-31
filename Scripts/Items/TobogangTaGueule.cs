using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using TobogangMod.Scripts.Items;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class TobogangTaGueule : TobogangItem
    {
        public static readonly float MUTE_DURATION =
#if DEBUG
            5f;
#else
            30f;
#endif

        TobogangTaGueule()
        {
            TobogangItemId = TobogangMod.TobogangItems.TA_GUEULE;
            CoinguesPrice = 150;
            Keywords = ["ta gueule", "ta gueul", "ta gueu", "ta gue", "ta gu", "ta g", "tg", "tagueule", "tagueul", "tagueu", "tague", "tagu", "tag"];
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            playerHeldBy.interactRay = new Ray(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward);

            float closest = -1f;
            PlayerControllerB? hitPlayer = null;

            foreach (var hit in Physics.RaycastAll(playerHeldBy.interactRay, playerHeldBy.grabDistance, playerHeldBy.playerMask))
            {
                if (hit.collider.gameObject.GetComponent<PlayerControllerB>() == playerHeldBy)
                {
                    continue;
                }

                if (closest == -1f || hit.distance < closest)
                {
                    closest = hit.distance;
                    hitPlayer = hit.collider.gameObject.GetComponent<PlayerControllerB>();
                }
            }

            if (hitPlayer != null)
            {
                TobogangMod.Logger.LogDebug($"Player hit: {hitPlayer.playerUsername}");
                ActivateOnPlayerServerRpc(hitPlayer.NetworkObject);
            }
            else
            {
                TobogangMod.Logger.LogDebug("No player hit");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ActivateOnPlayerServerRpc(NetworkObjectReference targetPlayerRef)
        {
            if (!targetPlayerRef.TryGet(out var targetPlayer))
            {
                return;
            }

            ActivateOnPlayerClientRpc(targetPlayerRef, playerHeldBy.NetworkObject);
            StartCoroutine(WaitAndUnmutePlayerCoroutine(targetPlayer.gameObject.GetComponent<PlayerControllerB>()));
        }

        [ClientRpc]
        void ActivateOnPlayerClientRpc(NetworkObjectReference targetPlayerRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerRef.TryGet(out var targetPlayerNet))
            {
                return;
            }

            var player = targetPlayerNet.gameObject.GetComponent<PlayerControllerB>();

            DestroyObjectInHand(player);

            if (player == StartOfRound.Instance.localPlayerController)
            {
                return;
            }

            CoinguesManager.Instance.MutePlayerServerRpc(player.NetworkObject, sourcePlayerRef);
        }

        private IEnumerator WaitAndUnmutePlayerCoroutine(PlayerControllerB player)
        {
            yield return new WaitForSeconds(MUTE_DURATION);

            if (player != null)
            {
                CoinguesManager.Instance.UnmutePlayerServerRpc(player.NetworkObject);
            }
        }
    }
}
