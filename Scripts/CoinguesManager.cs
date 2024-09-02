﻿using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TobogangMod.Model;
using TobogangMod.Patches;
using Unity.Netcode;
using UnityEngine;
using static LethalLib.Modules.ContentLoader;

namespace TobogangMod.Scripts
{
    public class CoinguesManager : NetworkBehaviour
    {
        public static readonly int DEATH_MALUS = 30;
        public static readonly int CRAMPTES_PROC_MALUS = 100;
        public static readonly float SCRAP_COINGUES_MULTIPLIER = 0.5f;

        public static CoinguesManager Instance { get; private set; }
        public static GameObject NetworkPrefab { get; private set; }

        public List<PlayerControllerB> MutedPlayers { get; private set; } = [];
        public List<PlayerControllerB> DeafenedPlayers { get; private set; } = [];

        private CoinguesStorage _coingues = new();

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "CoinguesManager");
            NetworkPrefab.AddComponent<CoinguesManager>();
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (IsServer)
            {
                var prefix = TobogangMod.Instance.Info.Metadata.Name + "_Coingues_";

                foreach (var key in ES3.GetKeys(GameNetworkManager.Instance.currentSaveFileName))
                {
                    if (!key.StartsWith(prefix))
                    {
                        continue;
                    }

                    _coingues[key.Substring(prefix.Length)] = ES3.Load<int>(key, GameNetworkManager.Instance.currentSaveFileName, 0);
                }

                TobogangMod.Logger.LogDebug($"Loaded coingues from save: {_coingues}");

                return;
            }

            SyncAllClientsServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void SyncAllClientsServerRpc()
        {
            SyncAllClientsClientRpc(_coingues);
        }

        [ClientRpc]
        private void SyncAllClientsClientRpc(CoinguesStorage inCoingues)
        {
            _coingues = inCoingues;

#if DEBUG
            TobogangMod.Logger.LogDebug($"CoinguesManager synced on client: {_coingues}");
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void GiveTobogangItemToPlayerServerRpc(string item, NetworkObjectReference player)
        {
            if (!player.TryGet(out var playerNetworkObject))
            {
                return;
            }

            var playerController = playerNetworkObject.GetComponent<PlayerControllerB>();

            GameObject gameObject = GameObject.Instantiate(((ScrapItem)TobogangMod.ContentLoader.LoadedContent[item]).Item.spawnPrefab);
            gameObject.GetComponent<NetworkObject>().Spawn();

            var grabbable = gameObject.GetComponent<GrabbableObject>();

            grabbable.StartCoroutine(WaitAndGiveItemToPlayer(playerController, grabbable));
        }

        private IEnumerator WaitAndGiveItemToPlayer(PlayerControllerB player, GrabbableObject grabbable)
        {
            yield return new WaitForEndOfFrame();

            bool grabValidated = !grabbable.heldByPlayerOnServer;

            if (grabValidated)
            {
                grabbable.heldByPlayerOnServer = true;
                grabbable.NetworkObject.ChangeOwnership(player.actualClientId);
            }

            player.GrabObjectClientRpc(grabValidated, grabbable.NetworkObject);
            SetGrabbableParentClientRpc(grabbable.NetworkObject, player.NetworkObject);
        }

        [ClientRpc]
        private void SetGrabbableParentClientRpc(NetworkObjectReference grabbableRef, NetworkObjectReference player)
        {
            if (!grabbableRef.TryGet(out var grabbableNet) || !player.TryGet(out var playerNet))
            {
                return;
            }

            var grabbable = grabbableNet.GetComponentInChildren<GrabbableObject>();
            grabbable.parentObject = playerNet.GetComponentInChildren<PlayerControllerB>().localItemHolder;
            foreach (var collider in grabbable.gameObject.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        public static string GetPlayerId(PlayerControllerB player)
        {
            return player.playerSteamId != 0 ? player.playerSteamId.ToString() : player.playerUsername;
        }

        public int GetCoingues(PlayerControllerB player)
        {
            return GetCoingues(GetPlayerId(player));
        }

        public int GetCoingues(string playerId)
        {
            return !_coingues.ContainsKey(playerId) ? 0 : _coingues[playerId];
        }

        public string[] GetRegisteredPlayers()
        {
            var players = new string[_coingues.Keys.Count];
            _coingues.Keys.CopyTo(players, 0);

            return players;
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddCoinguesServerRpc(NetworkObjectReference player, int amount)
        {
            if (!player.TryGet(out var networkPlayer))
            {
                return;
            }

            SetCoinguesClientRpc(player, GetCoingues(networkPlayer.gameObject.GetComponent<PlayerControllerB>()) + amount);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveCoinguesServerRpc(NetworkObjectReference player, int amount)
        {
            AddCoinguesServerRpc(player, -amount);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetCoinguesServerRpc(NetworkObjectReference player, int newCoingues)
        {
            SetCoinguesClientRpc(player, newCoingues);
        }

        [ClientRpc]
        private void SetCoinguesClientRpc(NetworkObjectReference player, int newCoingues)
        {
            if (!player.TryGet(out var playerNetworkObject))
            {
                return;
            }

            var playerController = playerNetworkObject.gameObject.GetComponent<PlayerControllerB>();

            _coingues[GetPlayerId(playerController)] = Math.Max(newCoingues, 0);

#if DEBUG
            TobogangMod.Logger.LogDebug($"{playerController.playerUsername} now has {_coingues[GetPlayerId(playerController)]} coingues");
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void MutePlayerServerRpc(NetworkObjectReference targetPlayerRef, ulong sourcePlayerRef, bool isMuted)
        {
            MutePlayerClientRpc(targetPlayerRef, sourcePlayerRef, isMuted);
        }

        [ClientRpc]
        private void MutePlayerClientRpc(NetworkObjectReference targetPlayerRef, ulong sourcePlayerRef, bool isMuted)
        {
            if (!targetPlayerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.gameObject.GetComponent<PlayerControllerB>();

            if (player == null || (isMuted && MutedPlayers.Contains(player)))
            {
                return;
            }

            if (isMuted)
            {
                TobogangMod.Logger.LogDebug($"Muted {player.playerUsername}");

                MutedPlayers.Add(player);
            }
            else
            {
                TobogangMod.Logger.LogDebug($"Unmuted {player.playerUsername}");

                MutedPlayers.Remove(player);
            }

            player.usernameCanvas.transform.Find(PlayerControllerPatch.MUTE_ICON).gameObject.SetActive(isMuted);
            LayoutPlayerIcons(player);

            var sourcePlayerNet = TobogangMod.TryGet(sourcePlayerRef);

            if (isMuted && player == StartOfRound.Instance.localPlayerController && sourcePlayerNet != null)
            {
                HUDManager.Instance.DisplayGlobalNotification($"{sourcePlayerNet.gameObject.GetComponent<PlayerControllerB>().playerUsername} a utilise Ta gueule pour te mute pendant {Math.Round(TobogangTaGueule.MUTE_DURATION)} secondes");
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        [ServerRpc(RequireOwnership = false)]
        public void DeafenPlayerServerRpc(NetworkObjectReference targetPlayerRef, ulong sourcePlayerRef, bool isDeaf)
        {
            DeafenPlayerClientRpc(targetPlayerRef, sourcePlayerRef, isDeaf);
        }

        [ClientRpc]
        private void DeafenPlayerClientRpc(NetworkObjectReference targetPlayerRef, ulong sourcePlayerRef, bool isDeaf)
        {
            if (!targetPlayerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.gameObject.GetComponent<PlayerControllerB>();

            if (player == null || (isDeaf && DeafenedPlayers.Contains(player)))
            {
                return;
            }

            if (isDeaf)
            {
                TobogangMod.Logger.LogDebug($"Deafened {player.playerUsername}");

                DeafenedPlayers.Add(player);
            }
            else
            {
                TobogangMod.Logger.LogDebug($"Un-deafened {player.playerUsername}");

                DeafenedPlayers.Remove(player);
            }

            player.usernameCanvas.transform.Find(PlayerControllerPatch.DEAF_ICON).gameObject.SetActive(isDeaf);
            LayoutPlayerIcons(player);

            var sourcePlayerNet = TobogangMod.TryGet(sourcePlayerRef);

            if (isDeaf && player == StartOfRound.Instance.localPlayerController && sourcePlayerNet != null)
            {
                HUDManager.Instance.DisplayGlobalNotification($"{sourcePlayerNet.gameObject.GetComponent<PlayerControllerB>().playerUsername} a utilise RP Joel pour te rendre sourd pendant {Math.Round(TobogangRPJoel.DEAFEN_DURATION)} secondes");
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        private static void LayoutPlayerIcons(PlayerControllerB player)
        {
            const float offset = 40f;

            var muteIcon = player.usernameCanvas.transform.Find(PlayerControllerPatch.MUTE_ICON).gameObject;
            var deafIcon = player.usernameCanvas.transform.Find(PlayerControllerPatch.DEAF_ICON).gameObject;

            var mutePos = muteIcon.GetComponent<RectTransform>().localPosition;
            var deafPos = deafIcon.GetComponent<RectTransform>().localPosition;

            var useOffset = muteIcon.activeSelf && deafIcon.activeSelf;

            mutePos.x = useOffset ? -offset : 0f;
            deafPos.x = useOffset ?  offset : 0f;

            muteIcon.GetComponent<RectTransform>().localPosition = mutePos;
            deafIcon.GetComponent<RectTransform>().localPosition = deafPos;
        }
    }
}
