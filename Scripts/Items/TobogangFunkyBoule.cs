using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts.Items
{
    public class TobogangFunkyBoule : TobogangItem
    {
        TobogangFunkyBoule()
        {
            TobogangItemId = TobogangMod.TobogangItems.FUNKY_BOULE;
            CoinguesPrice = 250;
            Keywords = ["funky boule", "funky boul", "funky bou", "funky bo", "funky b", "funky", "funk", "boule"];
        }

        protected override bool CanUseOnPlayerOrEnemy(GameObject playerOrEnemy)
        {
            return !CoinguesManager.Instance.DiscoPlayers.Contains(playerOrEnemy.GetComponent<NetworkObject>().NetworkObjectId);
        }

        protected override void ItemActivatedOnServer(GameObject targetPlayerOrEnemy, PlayerControllerB sourcePlayer)
        {
            var playerNet = targetPlayerOrEnemy.GetComponent<NetworkObject>();
            CoinguesManager.Instance.SetPlayerDiscoServerRpc(playerNet, true);
        }
    }
}
