using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TobogangMod.Patches;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts.Items
{
    public class TobogangArmageddon : TobogangItem
    {

        TobogangArmageddon()
        {
            TobogangItemId = TobogangMod.TobogangItems.ARMAGEDDON;
            CoinguesPrice = 1000;
            Keywords = ["armageddon", "arma", "armagedon", "armaguedon", "armaggedon", "armaggeddon", "armag"];
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!StartOfRound.Instance.inShipPhase && StartOfRound.Instance.currentLevel.levelID != (int)LevelIds.Company)
            {
                ArmageddonServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ArmageddonServerRpc()
        {
            CoinguesManager.Instance.ArmageddonServerRpc();
            ArmageddonClientRpc();
        }

        [ClientRpc]
        private void ArmageddonClientRpc()
        {
            DestroyObjectInHand(playerHeldBy);
        }
    }
}
