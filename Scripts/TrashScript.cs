using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using TobogangMod.Scripts.Items;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TobogangMod.Scripts
{
    public class TrashScript : NetworkBehaviour
    {
        private InteractTrigger _interact = null!;
        private const float SellPercent = 0.5f;

        void Start()
        {
            _interact = GetComponentInChildren<InteractTrigger>();
            _interact.onInteract.AddListener(this, GetType().GetMethod(nameof(OnInteract)));
        }

        void Update()
        {
            string text;
            var player = StartOfRound.Instance.localPlayerController;

            if (player == null)
            {
                return;
            }

            var heldItem = player.ItemSlots[player.currentItemSlot];

            _interact.interactable = false;

            if (heldItem == null)
            {
                text = "[Aucun item en main]";
            }
            else
            {
                var toboItem = heldItem.GetComponentInChildren<TobogangItem>();

                if (toboItem == null)
                {
                    text = "[Impossible de jeter cet item]";
                }
                else
                {
                    var sellValue = (int)Math.Round(SellPercent * toboItem.CoinguesPrice);
                    text = $"Maintenir [E] pour jeter {heldItem.itemProperties.itemName} contre {sellValue} coingues";

                    _interact.interactable = true;
                }
            }

            _interact.hoverTip = text;
            _interact.holdTip = text;
            _interact.disabledHoverTip = text;
        }

        public void OnInteract(PlayerControllerB player)
        {
            SellItemServerRpc(player.NetworkObject);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SellItemServerRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.GetComponent<PlayerControllerB>();
            var heldItem = player.ItemSlots[player.currentItemSlot];
            var toboItem = heldItem.GetComponentInChildren<TobogangItem>();

            if (toboItem == null)
            {
                TobogangMod.Logger.LogError("Trying to sell invalid item");
                return;
            }

            CoinguesManager.Instance.AddCoinguesServerRpc(playerRef, (int)Math.Round(toboItem.CoinguesPrice * SellPercent));
            SellitemClientRpc(playerRef);
        }

        [ClientRpc]
        private void SellitemClientRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.GetComponent<PlayerControllerB>();
            player.ItemSlots[player.currentItemSlot].DestroyObjectInHand(player);

            StartCoroutine(PlaySoundCoroutine());
        }

        private IEnumerator PlaySoundCoroutine()
        {
            var audio = GetComponentInChildren<AudioSource>();
            audio.pitch = Random.Range(0.8f, 1.2f);
            audio.PlayOneShot(TobogangMod.RecycleClip);

            yield return new WaitForSeconds(TobogangMod.RecycleClip.length);
            audio.pitch = 1f;
        }
    }
}
