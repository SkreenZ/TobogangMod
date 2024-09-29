using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TobogangMod.Scripts.Items
{
    public class TobogangButingue : TobogangItem
    {
        private enum Loot
        {
            Coingues50,
            Coingues100,
            Cramptes,
            Funky,
            Frenesie,
            RPJoel,
            TaGueule,
            Nuke,
            Swap,
            Armageddon,
            InstantCramptes
        }

        private static readonly Dictionary<Loot, int> LOOTS_PROBABILITIES = new()
        {
            { Loot.Coingues50, 60 },
            { Loot.Coingues100, 40 },
            { Loot.Cramptes, 10 },
            { Loot.Funky, 25 },
            { Loot.Frenesie, 25 },
            { Loot.RPJoel, 25 },
            { Loot.TaGueule, 25 },
            { Loot.Swap, 15 },
            { Loot.Nuke, 3 },
            { Loot.Armageddon, 1 },
            { Loot.InstantCramptes, 5 },
        };

        public TobogangButingue()
        {
            IsUsingPlaceholderPrefab = false;
            UsableInShip = false;
            UsableOnSelf = false;

            TobogangItemId = TobogangMod.TobogangItems.BUTINGUE;
            CoinguesPrice = 50;
            Keywords = ["boite de butingue", "butingue", "butin", "boite", "boite de", "boite de b", "boite de bu", "boite de but", "boite de buti", "boite de butin", "boite de buting", "boite de butingu"];
        }

        protected override void PostAwake()

        {
            itemProperties.positionOffset = new Vector3(-0.15f, -0.15f, 0.2f);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            if (StartOfRound.Instance.inShipPhase)
            {
                return;
            }

            var player = playerHeldBy;
            ActivateServerRpc();

            HUDManager.Instance.itemSlotIcons[player.currentItemSlot].enabled = false;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ActivateServerRpc()
        {
            var confetti = GameObject.Instantiate(TobogangMod.ConfettiPrefab, gameObject.transform.position, Quaternion.identity);
            confetti.GetComponent<NetworkObject>().Spawn();

            var loot = GetRandomLoot();
            TobogangMod.Logger.LogDebug($"Loot: {loot}");

            var player = playerHeldBy;
            player.DiscardHeldObject();

            switch (loot)
            {
                case Loot.Coingues50:
                {
                    CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 50);
                    break;
                }

                case Loot.Coingues100:
                {
                    CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 100);
                    break;
                }

                case Loot.Funky:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.FUNKY_BOULE, player.NetworkObject);
                    break;
                }

                case Loot.Frenesie:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.FRENESIE, player.NetworkObject);
                    break;
                }

                case Loot.RPJoel:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.RP_JOEL, player.NetworkObject);
                    break;
                }

                case Loot.TaGueule:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.TA_GUEULE, player.NetworkObject);
                    break;
                }

                case Loot.Swap:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.SWAP, player.NetworkObject);
                    break;
                }

                case Loot.Armageddon:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.ARMAGEDDON, player.NetworkObject);
                    break;
                }

                case Loot.Nuke:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.NUKE, player.NetworkObject);
                    break;
                }

                case Loot.Cramptes:
                {
                    if (CramptesManager.Instance.CurrentCramptesPlayer == player)
                    {
                        CramptesManager.Instance.SetCramptesPlayerServerRpc(TobogangMod.NULL_OBJECT);
                    }
                    else
                    {
                        CramptesManager.Instance.SetCramptesPlayerServerRpc(player.NetworkObjectId);
                    }
                    break;
                }

                case Loot.InstantCramptes:
                {
                    CramptesManager.Instance.SetCramptesPlayerServerRpc(player.NetworkObjectId);
                    CramptesManager.Instance.ProcCramptesServerRpc(true);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            gameObject.GetComponent<NetworkObject>().Despawn();
        }

        private static Loot GetRandomLoot()
        {
            var totalProb = LOOTS_PROBABILITIES.Values.Sum();
            var result = Random.Range(0, totalProb);
            var currentLoot = 0;

            while (result > 0)
            {
                result -= LOOTS_PROBABILITIES[(Loot)currentLoot];

                if (result > 0)
                {
                    currentLoot++;
                }
            }

            return (Loot)currentLoot;
        }
    }
}
