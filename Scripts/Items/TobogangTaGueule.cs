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

        protected override void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var targetPlayer = targetPlayerOrEnemy.GetComponent<PlayerControllerB>();

            if (targetPlayer == null)
            {
                return;
            }

            StartCoroutine(WaitAndUnmutePlayerCoroutine(targetPlayer));
        }

        protected override void ItemActivatedOnClient(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var targetPlayer = targetPlayerOrEnemy.GetComponent<PlayerControllerB>();

            if (targetPlayer == null || targetPlayer == StartOfRound.Instance.localPlayerController)
            {
                return;
            }

            CoinguesManager.Instance.MutePlayerServerRpc(targetPlayer.NetworkObject, sourcePlayer.NetworkObject);
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
