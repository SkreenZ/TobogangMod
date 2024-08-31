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
        public static bool LocalPlayerIsDeaf { get; private set; } = false;

        TobogangRPJoel()
        {
            TobogangItemId = TobogangMod.TobogangItems.RP_JOEL;
            CoinguesPrice = 150;
            Keywords = ["rp joel", "joel", "rp joe", "rpjoel", "rpjoe", "rpj", "rp j"];
        }

        protected override void ItemActivatedOnClient(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var targetPlayer = targetPlayerOrEnemy.GetComponent<PlayerControllerB>();

            if (targetPlayer != null && targetPlayer == StartOfRound.Instance.localPlayerController)
            {
                LocalPlayerIsDeaf = true;
                StartOfRound.Instance.UpdatePlayerVoiceEffects();

                StartCoroutine(WaitAndUndeafenLocalPlayer());
            }
        }

        private IEnumerator WaitAndUndeafenLocalPlayer()
        {
            yield return new WaitForSeconds(DEAFEN_DURATION);

            LocalPlayerIsDeaf = false;
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }
    }
}
