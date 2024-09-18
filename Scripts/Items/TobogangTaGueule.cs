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
            UsableInShip = true;

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
            CoinguesManager.Instance.MutePlayerServerRpc(targetPlayer.NetworkObject, sourcePlayer.NetworkObjectId, true);
        }

        private IEnumerator WaitAndUnmutePlayerCoroutine(PlayerControllerB player)
        {
            yield return new WaitForSeconds(MUTE_DURATION);

            CoinguesManager.Instance.MutePlayerServerRpc(player.NetworkObject, TobogangMod.NULL_OBJECT, false);
        }
    }
}
