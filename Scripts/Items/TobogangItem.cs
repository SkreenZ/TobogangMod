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
        public bool IsUsableOnPlayers = true;
        public bool IsUsableOnEnemies = false;

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
            GameObject? hitObject = null;

            foreach (var hit in Physics.RaycastAll(playerHeldBy.interactRay, playerHeldBy.grabDistance))
            {
                if (hit.collider.gameObject.GetComponent<PlayerControllerB>() == playerHeldBy ||
                    (hit.collider.gameObject.GetComponent<PlayerControllerB>() == null
                     && hit.collider.gameObject.GetComponent<EnemyAI>() == null))
                {
                    continue;
                }

                if (closest == -1f || hit.distance < closest)
                {
                    closest = hit.distance;
                    hitObject = hit.collider.gameObject;
                }
            }

            if (hitObject != null)
            {
                var targetIsPlayer = hitObject.GetComponent<PlayerControllerB>() != null;

                if (targetIsPlayer && !IsUsableOnPlayers)
                {
                    return;
                }

                if (!targetIsPlayer && !IsUsableOnEnemies)
                {
                    return;
                }

                if (targetIsPlayer)
                {
                    TobogangMod.Logger.LogDebug($"Player hit: {hitObject.GetComponent<PlayerControllerB>().playerUsername}");
                }
                else
                {
                    TobogangMod.Logger.LogDebug($"Enemy hit: {hitObject.GetComponent<EnemyAI>().enemyType.enemyName}");
                }

                ActivateOnPlayerOrEnemyServerRpc(hitObject.GetComponent<NetworkObject>(), playerHeldBy.NetworkObject);
            }
            else
            {
                TobogangMod.Logger.LogDebug("No player hit");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ActivateOnPlayerOrEnemyServerRpc(NetworkObjectReference targetPlayerOrEnemyRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerOrEnemyRef.TryGet(out var targetPlayerOrEnemy) || !sourcePlayerRef.TryGet(out var sourcePlayer))
            {
                return;
            }

            ItemActivatedOnServer(targetPlayerOrEnemy.gameObject, sourcePlayer.gameObject.GetComponent<PlayerControllerB>());
            ActivateOnPlayerOrEnemyClientRpc(targetPlayerOrEnemyRef, playerHeldBy.NetworkObject);
        }

        [ClientRpc]
        void ActivateOnPlayerOrEnemyClientRpc(NetworkObjectReference targetPlayerOrEnemyRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerOrEnemyRef.TryGet(out var targetPlayerOrEnemyNet) || !sourcePlayerRef.TryGet(out var sourcePlayerNet))
            {
                return;
            }

            var targetPlayerOrEnemy = targetPlayerOrEnemyNet.gameObject;

            DestroyObjectInHand(sourcePlayerNet.GetComponentInChildren<PlayerControllerB>());

            ItemActivatedOnClient(targetPlayerOrEnemy, sourcePlayerNet.gameObject.GetComponent<PlayerControllerB>());
        }

        protected virtual void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
        }

        protected virtual void ItemActivatedOnClient(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
        }
    }
}
