﻿using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TobogangMod.Model;
using TobogangMod.Patches;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static LethalLib.Modules.ContentLoader;

namespace TobogangMod.Scripts
{
    public class CoinguesManager : NetworkBehaviour
    {
        private struct ClaimInfo
        {
            public bool ClaimedToday;
            public int Streak;
        }

        public struct BetInfo
        {
            public ulong PlayerNetId;
            public uint Amount;
        }

        public static readonly int CLAIM_VALUE = 10;
        public static readonly int DEATH_MALUS = 30;
        public static readonly int CRAMPTES_PROC_MALUS = 100;
        public static readonly float SCRAP_COINGUES_MULTIPLIER = 0.5f;
        public static readonly float CLAIM_TIME = 360f; // 12 A.M

        public static CoinguesManager Instance { get; private set; }
        public static GameObject NetworkPrefab { get; private set; }

        public List<PlayerControllerB> MutedPlayers { get; private set; } = [];
        public List<PlayerControllerB> DeafenedPlayers { get; private set; } = [];

        private CoinguesStorage _coingues = new();
        private Dictionary<string, ClaimInfo> _playerClaims = [];
        private Dictionary<string, BetInfo> _playerBets = [];
        public Dictionary<string, int> PlayerProfits = [];

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
                var coinguesPrefix = TobogangMod.Instance.Info.Metadata.Name + "_Coingues_";
                var claimPrefix = TobogangMod.Instance.Info.Metadata.Name + "_ClaimStreak_";

                foreach (var key in ES3.GetKeys(GameNetworkManager.Instance.currentSaveFileName))
                {
                    if (key == null || (!key.StartsWith(coinguesPrefix) && !key.StartsWith(claimPrefix)))
                    {
                        continue;
                    }

                    if (key.StartsWith(coinguesPrefix))
                    {
                        _coingues[key.Substring(coinguesPrefix.Length)] = ES3.Load<int>(key, GameNetworkManager.Instance.currentSaveFileName, 0);
                    }
                    else if (key.StartsWith(claimPrefix))
                    {
                        var streak = ES3.Load<int>(key, GameNetworkManager.Instance.currentSaveFileName, 0);

                        _playerClaims[key.Substring(claimPrefix.Length)] = new ClaimInfo { ClaimedToday = false, Streak = streak };
                    }
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

            foreach (var (key, value) in _playerClaims.ToDictionary(e => e.Key, e => e.Value))
            {
                SetPlayerClaimClientRpc(key, value.ClaimedToday, value.Streak);
            }

            foreach (var (key, value) in _playerBets.ToDictionary(e => e.Key, e => e.Value))
            {
                SetPlayerBetClientRpc(key, value.PlayerNetId, value.Amount);
            }
        }

        [ClientRpc]
        private void SyncAllClientsClientRpc(CoinguesStorage inCoingues)
        {
            _coingues = inCoingues;

#if DEBUG
            TobogangMod.Logger.LogDebug($"CoinguesManager synced on client: {_coingues}");
#endif
        }

        [ClientRpc]
        private void SetPlayerClaimClientRpc(string playerId, bool claimedToday, int streak)
        {
            _playerClaims[playerId] = new ClaimInfo { ClaimedToday = claimedToday, Streak = streak };

#if DEBUG
            TobogangMod.Logger.LogDebug($"Set {playerId} claim info: {claimedToday}, {streak}");
#endif
        }

        [ClientRpc]
        private void SetPlayerBetClientRpc(string playerId, ulong betPlayerNetId, uint amount)
        {
            _playerBets[playerId] = new BetInfo { PlayerNetId = betPlayerNetId, Amount = amount };

#if DEBUG
            TobogangMod.Logger.LogDebug($"Set {playerId} bet info: {betPlayerNetId}, {amount}");
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerBetServerRpc(NetworkObjectReference player, NetworkObjectReference targetPlayer, uint betAmount)
        {
            if (!player.TryGet(out var playerNet))
            {
                return;
            }

            var playerId = GetPlayerId(playerNet.GetComponentInChildren<PlayerControllerB>());

            if (_playerBets.ContainsKey(playerId) || GetCoingues(playerId) < betAmount)
            {
                return;
            }

            RemoveCoinguesServerRpc(player, (int)betAmount);

            SetPlayerBetClientRpc(playerId, targetPlayer.NetworkObjectId, betAmount);
        }

        [ServerRpc]
        public void FinishBetcoingueServerRpc()
        {
            string? mostProfitablePlayer = null;
            int maxProfit = 0;

            foreach (var (playerId, profit) in PlayerProfits)
            {
                if (profit > maxProfit)
                {
                    mostProfitablePlayer = playerId;
                    maxProfit = profit;
                }
            }

            StartCoroutine(WaitAndFinishBet(mostProfitablePlayer));
        }

        private IEnumerator WaitAndFinishBet(string? mostProfitablePlayer)
        {
            yield return new WaitForSeconds(5f);

            foreach (var (playerId, bet) in _playerBets)
            {
                if (mostProfitablePlayer != null && bet.PlayerNetId == GetPlayer(mostProfitablePlayer)?.NetworkObjectId)
                {
                    AddCoinguesServerRpc(GetPlayer(playerId)?.NetworkObject, (int)bet.Amount * _playerBets.Count);
                }
            }

            FinishBetcoingueClientRpc(mostProfitablePlayer ?? "");
        }

        [ClientRpc]
        private void FinishBetcoingueClientRpc(string winningPlayerId)
        {
            StartCoroutine(FinishBetcoingueCoroutine(winningPlayerId));
        }

        private IEnumerator FinishBetcoingueCoroutine(string winningPlayerId)
        {
            var winningPlayer = GetPlayer(winningPlayerId);

            PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/NoBet").gameObject.SetActive(false);

            var playerNameText = PlayerControllerPatch.LocalPlayerCanvas.transform.Find("BetcoingueResult/Header/PlayerName").gameObject.GetComponent<TextMeshProUGUI>();
            playerNameText.gameObject.SetActive(false);
            playerNameText.text = winningPlayer != null ? winningPlayer.playerUsername : "Personne !";

            for (int i = 0; i < 10; i++)
            {
                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/Content/Player{i}").gameObject.SetActive(false);
            }

            PlayerControllerPatch.LocalPlayerCanvas.gameObject.SetActive(true);
            HUDManager.Instance.UIAudio.PlayOneShot(TobogangMod.DrumRoll);

            yield return new WaitForSeconds(TobogangMod.DrumRoll.length);

            playerNameText.gameObject.SetActive(true);
            HUDManager.Instance.UIAudio.PlayOneShot(TobogangMod.PartyHorn);

            yield return new WaitForSeconds(1.5f);

            if (_playerBets.Count == 0)
            {
                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/NoBet").gameObject.SetActive(true);
            }

            for (int i = 0; i < _playerBets.Count; i++)
            {
                if (i >= 10)
                {
                    break;
                }

                var player = GetPlayer(_playerBets.Keys.ToArray()[i]);
                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/Content/Player{i}/Name").GetComponent<TextMeshProUGUI>().text = player.playerUsername;
                var bet = _playerBets.Values.ToArray()[i];
                var isWin = winningPlayer != null && bet.PlayerNetId == winningPlayer.NetworkObjectId;
                var winOrLoseAmount = isWin ? bet.Amount * _playerBets.Count : bet.Amount;
                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/Content/Player{i}/Amount").GetComponent<TextMeshProUGUI>().text = $"{(isWin ? "+" : "-")}{winOrLoseAmount}";

                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/Content/Player{i}").gameObject.SetActive(true);

                yield return new WaitForSeconds(1.5f);
            }

            yield return new WaitForSeconds(7.5f);

            PlayerControllerPatch.LocalPlayerCanvas.gameObject.SetActive(false);

            if (IsServer)
            {
                ClearAllBetsClientRpc();
            }
        }

        public BetInfo? GetPlayerBet(PlayerControllerB player)
        {
            var playerId = GetPlayerId(player);

            if (!_playerBets.TryGetValue(playerId, out var bet))
            {
                return null;
            }

            return bet;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ClearAllBetsServerRpc()
        {
            ClearAllBetsClientRpc();
        }

        [ClientRpc]
        private void ClearAllBetsClientRpc()
        {
            _playerBets.Clear();
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

        public static PlayerControllerB? GetPlayer(string playerId)
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.playerUsername == playerId || (ulong.TryParse(playerId, out var steamId) && steamId == player.playerSteamId))
                {
                    return player;
                }
            }

            return null;
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

        [ServerRpc(RequireOwnership = false)]
        public void ClaimServerRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var playerNet) || RoundManager.Instance.timeScript.currentDayTime < CLAIM_TIME)
            {
                return;
            }

            var player = playerNet.GetComponentInChildren<PlayerControllerB>();
            var playerId = GetPlayerId(player);

            var alreadyClaimed = _playerClaims.ContainsKey(playerId) && _playerClaims[playerId].ClaimedToday;

            ClaimClientRpc(playerRef);

            if (!alreadyClaimed)
            {
                AddCoinguesServerRpc(playerRef, CLAIM_VALUE + _playerClaims[playerId].Streak);
            }
            else
            {
                RemoveCoinguesServerRpc(player.NetworkObject, 1);
                CramptesManager.Instance.SetCramptesPlayerServerRpc(player.NetworkObject);
            }
        }

        [ClientRpc]
        private void ClaimClientRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.GetComponentInChildren<PlayerControllerB>();
            var playerId = GetPlayerId(player);

            if (!_playerClaims.ContainsKey(playerId))
            {
                _playerClaims[playerId] = new ClaimInfo { ClaimedToday = true, Streak = 0 };
            }
            else
            {
                var claim = _playerClaims[playerId];
                claim.ClaimedToday = true;
                claim.Streak++;
                _playerClaims[playerId] = claim;
            }
        }

        [ServerRpc]
        public void ResetClaimsServerRpc()
        {
            ResetClaimsClientRpc();
        }

        [ClientRpc]
        private void ResetClaimsClientRpc()
        {
            foreach (var playerId in _playerClaims.Keys.ToList())
            {
                var claim = _playerClaims[playerId];

                if (!claim.ClaimedToday)
                {
                    claim.Streak = -1;
                }

                claim.ClaimedToday = false;
                _playerClaims[playerId] = claim;
            }

#if DEBUG
            TobogangMod.Logger.LogDebug("Reset claims of the day");
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetClaimStreakServerRpc(NetworkObjectReference playerRef, int streak)
        {
            SetClaimStreakClientRpc(playerRef, streak);
        }

        [ClientRpc]
        private void SetClaimStreakClientRpc(NetworkObjectReference playerRef, int streak)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var playerId = GetPlayerId(playerNet.GetComponentInChildren<PlayerControllerB>());

            if (_playerClaims.ContainsKey(playerId))
            {
                var claim = _playerClaims[playerId];
                claim.Streak = streak;
                _playerClaims[playerId] = claim;
            }
            else
            {
                _playerClaims[playerId] = new ClaimInfo { ClaimedToday = false, Streak = streak };
            }
        }

        public bool HasClaimedToday(PlayerControllerB player)
        {
            return HasClaimedToday(GetPlayerId(player));
        }

        public bool HasClaimedToday(string playerId)
        {
            if (!_playerClaims.ContainsKey(playerId))
            {
                return false;
            }

            return _playerClaims[playerId].ClaimedToday;
        }

        public int GetClaimStreak(PlayerControllerB player)
        {
            return GetClaimStreak(GetPlayerId(player));
        }

        public int GetClaimStreak(string playerId)
        {
            if (!_playerClaims.ContainsKey(playerId))
            {
                return -1;
            }

            return _playerClaims[playerId].Streak;
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
