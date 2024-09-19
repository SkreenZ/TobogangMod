using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace TobogangMod.Scripts.Items
{
    public class TobogangSwap : TobogangItem
    {
        TobogangSwap()
        {
            TobogangItemId = TobogangMod.TobogangItems.SWAP;
            CoinguesPrice = 350;
            Keywords = ["swap"];
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (!StartOfRound.Instance.inShipPhase)
            {
                ActivateServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ActivateServerRpc()
        {
            CoinguesManager.Instance.SwapAllPlayersServerRpc();

            ActivateClientRpc();
        }

        [ClientRpc]
        private void ActivateClientRpc()
        {
            DestroyObjectInHand(playerHeldBy);
        }
    }
}
