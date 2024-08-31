﻿using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TobogangMod.Model;
using Unity.Netcode;
using UnityEngine;
using static LethalLib.Modules.ContentLoader;

namespace TobogangMod.Scripts
{
    public class CoinguesManager : NetworkBehaviour
    {
        public static readonly int DEATH_MALUS = 30;
        public static readonly int CRAMPTES_PROC_MALUS = 100;

        public static CoinguesManager Instance { get; private set; }
        public static GameObject NetworkPrefab { get; private set; }

        public List<PlayerControllerB> MutedPlayers { get; private set; } = [];

        private CoinguesStorage _coingues = new();

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "CoinguesManager");
            NetworkPrefab.AddComponent<CoinguesManager>();
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                TobogangMod.Logger.LogError("Multiple CoinguesManager spawned!");
            }
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
        private void SetGrabbableParentClientRpc(NetworkObjectReference grabbable, NetworkObjectReference player)
        {
            if (!grabbable.TryGet(out var grabbableNet) || !player.TryGet(out var playerNet))
            {
                return;
            }

            grabbableNet.GetComponentInChildren<GrabbableObject>().parentObject = playerNet.GetComponentInChildren<PlayerControllerB>().localItemHolder;
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
        }

        [ServerRpc(RequireOwnership = false)]
        public void MutePlayerServerRpc(NetworkObjectReference targetPlayerRef, NetworkObjectReference sourcePlayerRef)
        {
            MutePlayerClientRpc(targetPlayerRef, sourcePlayerRef);
        }

        [ClientRpc]
        private void MutePlayerClientRpc(NetworkObjectReference targetPlayerRef, NetworkObjectReference sourcePlayerRef)
        {
            if (!targetPlayerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.gameObject.GetComponent<PlayerControllerB>();

            if (player != null)
            {
                TobogangMod.Logger.LogDebug($"Muted {player.playerUsername}");

                MutedPlayers.Add(player);
            }

            if (player == StartOfRound.Instance.localPlayerController && sourcePlayerRef.TryGet(out var sourcePlayerNet))
            {
                HUDManager.Instance.DisplayGlobalNotification($"{sourcePlayerNet.gameObject.GetComponent<PlayerControllerB>().playerUsername} a utilise Ta gueule pour te mute pendant {Math.Round(TobogangTaGueule.MUTE_DURATION)} secondes");
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        [ServerRpc(RequireOwnership = false)]
        public void UnmutePlayerServerRpc(NetworkObjectReference playerRef)
        {
            UnmutePlayerClientRpc(playerRef);
        }

        [ClientRpc]
        private void UnmutePlayerClientRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.gameObject.GetComponent<PlayerControllerB>();

            if (player != null)
            {
                TobogangMod.Logger.LogDebug($"Unmuted {player.playerUsername}");

                MutedPlayers.Remove(player);
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }
    }
}
