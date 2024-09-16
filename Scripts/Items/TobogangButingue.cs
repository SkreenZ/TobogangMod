﻿using System;
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
            Coingues20,
            Coingues50,
            Coingues100,
            Cramptes,
            Crazy,
            RPJoel,
            TaGueule,
            InstantCramptes
        }

        private static readonly Dictionary<Loot, int> LOOTS_PROBABILITIES = new()
        {
            { Loot.Coingues20, 60 },
            { Loot.Coingues50, 40 },
            { Loot.Coingues100, 10 },
            { Loot.Cramptes, 10 },
            { Loot.Crazy, 25 },
            { Loot.RPJoel, 25 },
            { Loot.TaGueule, 25 },
            { Loot.InstantCramptes, 5 },
        };

        public TobogangButingue()
        {
            IsUsingPlaceholderPrefab = false;

            TobogangItemId = TobogangMod.TobogangItems.BUTINGUE;
            CoinguesPrice = 50;
            Keywords = ["boite de butingue", "butingue", "boite", "boite de", "boite de b", "boite de bu", "boite de but", "boite de buti", "boite de butin", "boite de buting", "boite de butingu"];
        }

        protected override void PostAwake()

        {
            itemProperties.positionOffset = new Vector3(-0.15f, -0.15f, 0.2f);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            var player = playerHeldBy;
            ActivateServerRpc();

            HUDManager.Instance.itemSlotIcons[player.currentItemSlot].enabled = false;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ActivateServerRpc()
        {
            var confetti = GameObject.Instantiate(TobogangMod.ConfettiPrefab, gameObject.transform.position, Quaternion.identity);
            confetti.GetComponent<NetworkObject>().Spawn();

            StartCoroutine(WaitAndDestroyConfetti(confetti));

            var loot = GetRandomLoot();
            TobogangMod.Logger.LogDebug($"Loot: {loot}");

            var player = playerHeldBy;
            player.DiscardHeldObject();

            switch (loot)
            {
                case Loot.Coingues20:
                {
                    CoinguesManager.Instance.AddCoinguesServerRpc(player.NetworkObject, 20);
                    break;
                }

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

                case Loot.Crazy:
                {
                    CoinguesManager.Instance.GiveTobogangItemToPlayerServerRpc(TobogangMod.TobogangItems.CRAZY_TOBOBOT, player.NetworkObject);
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
                    CramptesManager.Instance.ProcCramptesServerRpc();
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            gameObject.GetComponent<NetworkObject>().Despawn();
        }

        private IEnumerator WaitAndDestroyConfetti(GameObject confetti)
        {
            yield return new WaitForSeconds(5f);
            confetti.GetComponent<NetworkObject>().Despawn();
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