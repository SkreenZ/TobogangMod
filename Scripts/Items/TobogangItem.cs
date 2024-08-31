using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts.Items
{
    public class TobogangItem : PhysicsProp
    {
        public string TobogangItemId { get; protected set; } = "";
        public int CoinguesPrice { get; set; } = 0;
        public string[] Keywords { get; protected set; } = [];

        protected bool IsUsingPlaceholderPrefab = true;

        void Awake()
        {
            gameObject.GetComponentInChildren<ScanNodeProperties>().headerText = itemProperties.itemName;

            grabbable = true;

            if (IsUsingPlaceholderPrefab)
            {
                itemProperties.rotationOffset = new Vector3(0f, 90f, 0f);
                itemProperties.positionOffset = new Vector3(0f, 0.08f, 0f);
            }
#if DEBUG
            itemProperties.canBeGrabbedBeforeGameStart = true;
#endif
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
                ActivateOnPlayerServerRpc(hitPlayer.NetworkObject, playerHeldBy.NetworkObject);
            }
            else
            {
                TobogangMod.Logger.LogDebug("No player hit");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ActivateOnPlayerServerRpc(NetworkObjectReference targetPlayerRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerRef.TryGet(out var targetPlayer) || !sourcePlayerRef.TryGet(out var sourcePlayer))
            {
                return;
            }

            ItemActivatedOnServer(targetPlayer.gameObject.GetComponent<PlayerControllerB>(), sourcePlayer.gameObject.GetComponent<PlayerControllerB>());
            ActivateOnPlayerClientRpc(targetPlayerRef, playerHeldBy.NetworkObject);
        }

        [ClientRpc]
        void ActivateOnPlayerClientRpc(NetworkObjectReference targetPlayerRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerRef.TryGet(out var targetPlayerNet) || !sourcePlayerRef.TryGet(out var sourcePlayerNet))
            {
                return;
            }

            var targetPlayer = targetPlayerNet.gameObject.GetComponent<PlayerControllerB>();

            DestroyObjectInHand(targetPlayer);

            ItemActivatedOnClient(targetPlayer, sourcePlayerNet.gameObject.GetComponent<PlayerControllerB>());
        }

        protected virtual void ItemActivatedOnServer(PlayerControllerB targetPlayer, PlayerControllerB sourcePlayer)
        {
        }

        protected virtual void ItemActivatedOnClient(PlayerControllerB targetPlayer, PlayerControllerB sourcePlayer)
        {
        }
    }
}
