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
    public class TobogangRPJoel : TobogangItem
    {
        public static readonly float DEAFEN_DURATION =
#if DEBUG
            5f;
#else
            30f;
#endif
        TobogangRPJoel()
        {
            TobogangItemId = TobogangMod.TobogangItems.RP_JOEL;
            CoinguesPrice = 150;
            Keywords = ["rp joel", "joel", "rp joe", "rpjoel", "rpjoe", "rpj", "rp j"];
        }

        protected override void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var targetPlayer = targetPlayerOrEnemy.GetComponent<PlayerControllerB>();

            if (targetPlayer != null)
            {
                CoinguesManager.Instance.DeafenPlayerServerRpc(targetPlayer.NetworkObject, sourcePlayer.NetworkObjectId, true);

                StartCoroutine(WaitAndUndeafenPlayer(targetPlayer));
            }
        }

        private IEnumerator WaitAndUndeafenPlayer(PlayerControllerB player)
        {
            yield return new WaitForSeconds(DEAFEN_DURATION);

            CoinguesManager.Instance.DeafenPlayerServerRpc(player.NetworkObject, TobogangMod.NULL_OBJECT, true);
        }
    }
}
