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
    public class TobogangNuke : TobogangItem
    {
        TobogangNuke()
        {
            TobogangItemId = TobogangMod.TobogangItems.NUKE;
            CoinguesPrice = 500;
            Keywords = ["nuke"];
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!StartOfRound.Instance.inShipPhase && StartOfRound.Instance.currentLevel.levelID != (int)LevelIds.Company)
            {
                NukeServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void NukeServerRpc()
        {
            NukeClientRpc();
        }

        [ClientRpc]
        private void NukeClientRpc()
        {
            DestroyObjectInHand(playerHeldBy);

            CoinguesManager.Instance.StartNuke();
        }
    }
}
