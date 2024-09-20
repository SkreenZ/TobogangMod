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
    public class TobogangAnnihilation : TobogangItem
    {
        TobogangAnnihilation()
        {
            TobogangItemId = TobogangMod.TobogangItems.ANNIHILATION;
            CoinguesPrice = 9999;
            Keywords = ["annihilation"];
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!StartOfRound.Instance.inShipPhase && StartOfRound.Instance.currentLevel.levelID != (int)LevelIds.Company)
            {
                AnnihilateServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void AnnihilateServerRpc()
        {
            CoinguesManager.Instance.SetSunExplodingServerRpc();
            AnnihilateClientRpc();
        }

        [ClientRpc]
        private void AnnihilateClientRpc()
        {
            StartOfRound.Instance.gameStats.allPlayerStats[playerHeldBy.actualClientId].playerNotes.Add("A annihilé la planète");
            DestroyObjectInHand(playerHeldBy);
        }
    }
}
