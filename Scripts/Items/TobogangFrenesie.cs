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
    public class TobogangFrenesie : TobogangItem
    {
        TobogangFrenesie()
        {
            TobogangItemId = TobogangMod.TobogangItems.FRENESIE;
            CoinguesPrice = 200;
            Keywords = ["frenesie", "frénésie", "frénesie", "frenésie"];
        }

        protected override void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var targetPlayer = targetPlayerOrEnemy.GetComponent<PlayerControllerB>();

            if (targetPlayer == null)
            {
                return;
            }

            CoinguesManager.Instance.ForcePlayerSprint(targetPlayer);
        }
    }
}
