using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using TobogangMod.Model;
using TobogangMod.Patches;
using Unity.AI.Navigation;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static LethalLib.Modules.ContentLoader;
using Random = UnityEngine.Random;
using UnityEngine.AI;
using DunGen;

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
        private const float ARMAGEDDON_RANGE = 100f;

        public static CoinguesManager Instance { get; private set; }
        public static GameObject NetworkPrefab { get; private set; }

        public List<PlayerControllerB> MutedPlayers { get; private set; } = [];
        public List<PlayerControllerB> DeafenedPlayers { get; private set; } = [];

        public List<int> TobogangUnlockableIds { get; private set; } = [];

        private CoinguesStorage _coingues = new();
        private Dictionary<string, ClaimInfo> _playerClaims = [];
        private Dictionary<string, BetInfo> _playerBets = [];
        public Dictionary<string, int> PlayerProfits = [];
        public HashSet<ulong> DiscoPlayers = [];

        private TextMeshProUGUI? _localPlayerCoinguesDisplay = null;
        private TextMeshProUGUI? _localPlayerCoinguesDisplayS = null;

        private GameObject _explosionPrefab = null!;
        private GameObject _discoPrefab = null!;

        public bool IsSunExploding { get; private set; } = false;
        public float CurrentSunIntensity { get; private set; } = 0f;
        private float _currentSunExplosionTime = 0f;
        private float _sunLerp = 0f;

        private AudioSource _alarmAudio = null!;

        private static  readonly AnimationCurve SUN_EXPLOSION_CURVE =
            new(new Keyframe(0f, 0f), new Keyframe(0.8f, 0.008f), new Keyframe(1f, 1f));

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
#if DEBUG
            foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
            {
                TobogangMod.Logger.LogDebug($"{item.itemName} ({item.itemId}): {item.minValue} / {item.maxValue}, {item.isScrap}");
            }
#endif
            _explosionPrefab = GameObject.Instantiate(StartOfRound.Instance.explosionPrefab);
            _explosionPrefab.SetActive(false);
            _explosionPrefab.AddComponent<AutoDestroy>();

            _discoPrefab = GameObject.Instantiate(StartOfRound.Instance.unlockablesList.unlockables[(int)UnlockableIds.DiscoBall].prefabObject, Vector3.zero, Quaternion.identity);
            _discoPrefab.SetActive(false);
            Destroy(_discoPrefab.GetComponent<AutoParentToShip>());
            Destroy(_discoPrefab.GetComponent<NetworkObject>());

            var audio = _explosionPrefab.GetComponentInChildren<AudioSource>();
            audio.maxDistance = 50f;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            var farAudio = audio.gameObject.AddComponent<FarSound>();
            farAudio.Clip = TobogangMod.DistantExplosionClip;

            var shipTransform = StartOfRound.Instance.middleOfShipNode.transform;
            var alarm = Instantiate(new GameObject("TobogangAlarm"), shipTransform.position + Vector3.up * 10, Quaternion.identity, shipTransform);
            _alarmAudio = alarm.AddComponent<AudioSource>();
            _alarmAudio.spatialBlend = 1f;
            _alarmAudio.minDistance = 0f;

            if (IsServer)
            {
                if (ES3.FileExists(GameNetworkManager.Instance.currentSaveFileName))
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
                }

                foreach (var unlockable in TobogangMod.Unlockables.unlockables)
                {
                    StartOfRound.Instance.unlockablesList.unlockables.Add(unlockable);
                    TobogangUnlockableIds.Add(StartOfRound.Instance.unlockablesList.unlockables.Count - 1);
                }

                var trash = Instantiate(TobogangMod.TrashPrefab, StartOfRound.Instance.elevatorTransform.position, Quaternion.identity);
                var placeable = trash.GetComponentInChildren<PlaceableShipObject>();
                placeable.placeObjectSFX = GameObject.Find("Terminal").GetComponentInChildren<Terminal>().placeableObject.placeObjectSFX;
                placeable.unlockableID = TobogangUnlockableIds[0];
                trash.GetComponent<NetworkObject>().Spawn();

                // ShipBuildModeManager.Instance.PlaceShipObject(trash.gameObject.transform.position, trash.gameObject.transform.rotation.eulerAngles, placeable, false);

                return;
            }

            SyncAllClientsServerRpc();
        }

        void Update()
        {
            UpdateSunExplosion();

            if (_localPlayerCoinguesDisplay == null || _localPlayerCoinguesDisplayS == null)
            {
                if (PlayerControllerPatch.LocalPlayerCanvas != null)
                {
                    _localPlayerCoinguesDisplay = PlayerControllerPatch.LocalPlayerCanvas.transform.Find("CoinguesAmount").gameObject.GetComponent<TextMeshProUGUI>();
                    _localPlayerCoinguesDisplayS = PlayerControllerPatch.LocalPlayerCanvas.transform.Find("CoinguesAmountS").gameObject.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    return;
                }
            }

            var amount = GetCoingues(StartOfRound.Instance.localPlayerController);
            _localPlayerCoinguesDisplay.text = $"{amount} coingue";
            _localPlayerCoinguesDisplayS.gameObject.SetActive(amount > 1);
        }

        private void UpdateSunExplosion()
        {
            if (!IsSunExploding)
            {
                return;
            }

            _currentSunExplosionTime = Mathf.Lerp(0f, 1f, _sunLerp);
            _sunLerp += 0.05f * Time.deltaTime;

            CurrentSunIntensity = SUN_EXPLOSION_CURVE.Evaluate(_currentSunExplosionTime) * TimeOfDayPatch.MAX_INTENSITY;
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
        public void ResetCoinguesServerRpc()
        {
            ResetCoinguesClientRpc();
        }

        [ClientRpc]
        private void ResetCoinguesClientRpc()
        {
            _coingues = new CoinguesStorage();
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

            var betcoingue = PlayerControllerPatch.LocalPlayerCanvas.transform.Find("BetcoingueResult");
            PlayerControllerPatch.LocalPlayerCanvas.transform.Find("BetcoingueResult/NoBet").gameObject.SetActive(false);

            var playerNameText = PlayerControllerPatch.LocalPlayerCanvas.transform.Find("BetcoingueResult/Header/PlayerName").gameObject.GetComponent<TextMeshProUGUI>();
            playerNameText.gameObject.SetActive(false);
            playerNameText.text = winningPlayer != null ? winningPlayer.playerUsername : "Personne !";

            for (int i = 0; i < 10; i++)
            {
                PlayerControllerPatch.LocalPlayerCanvas.transform.Find($"BetcoingueResult/Content/Player{i}").gameObject.SetActive(false);
            }

            betcoingue.gameObject.SetActive(true);
            HUDManager.Instance.UIAudio.PlayOneShot(TobogangMod.DrumRollClip);

            yield return new WaitForSeconds(TobogangMod.DrumRollClip.length);

            playerNameText.gameObject.SetActive(true);
            HUDManager.Instance.UIAudio.PlayOneShot(TobogangMod.PartyHornClip);

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

            yield return new WaitForSeconds(5f);

            betcoingue.gameObject.SetActive(false);

            if (IsServer)
            {
                foreach (var (playerId, bet) in _playerBets)
                {
                    if (winningPlayerId != "" && bet.PlayerNetId == GetPlayer(winningPlayerId)?.NetworkObjectId)
                    {
                        AddCoinguesServerRpc(GetPlayer(playerId)?.NetworkObject, (int)bet.Amount * _playerBets.Count);
                    }
                }

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

            GameObject gameObject = GameObject.Instantiate(((CustomItem)TobogangMod.ContentLoader.LoadedContent[item]).Item.spawnPrefab);
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
            var oldCoingues = _coingues[GetPlayerId(playerController)];
            newCoingues = Math.Max(newCoingues, 0);
            var diff = newCoingues - oldCoingues;

            _coingues[GetPlayerId(playerController)] = newCoingues;

            if (playerController == StartOfRound.Instance.localPlayerController && diff != 0)
            {
                var canvasTransform = PlayerControllerPatch.LocalPlayerCanvas.gameObject.transform;
                var textTransform = canvasTransform.Find("CoinguesText");

                var textObject = GameObject.Instantiate(textTransform.gameObject, canvasTransform);
                textObject.AddComponent<FadingText>();
                textObject.GetComponent<TextMeshProUGUI>().text = $"{(diff > 0 ? "+" : "")}{diff}";
                textObject.GetComponent<TextMeshProUGUI>().color = diff > 0 ? Color.green : Color.red;

                textObject.SetActive(true);
            }

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
                CramptesManager.Instance.SetCramptesPlayerServerRpc(player.NetworkObjectId);
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

        public GameObject? SpawnScrap(Item item, Vector3 location, bool falling = false)
        {
            if (!IsServer)
            {
                return null;
            }

            GameObject spawnedItem = GameObject.Instantiate(item.spawnPrefab, location, Quaternion.Euler(item.restingRotation), RoundManager.Instance.spawnedScrapContainer);

            var grabbable = spawnedItem.GetComponent<GrabbableObject>();

            if (falling)
            {
                grabbable.startFallingPosition = location;
                StartCoroutine(SetObjectToHitGroundSFX(grabbable));
                grabbable.targetFloorPosition = grabbable.GetItemFloorPosition(location);
            }

            var random = new System.Random((int)grabbable.targetFloorPosition.x + (int)grabbable.targetFloorPosition.y);
            grabbable.SetScrapValue((int)((double)random.Next(item.minValue + 25, item.maxValue + 35) * RoundManager.Instance.scrapValueMultiplier));

            var itemNet = spawnedItem.GetComponent<NetworkObject>();
            itemNet.Spawn();

            SpawnScrapClientRpc(itemNet, grabbable.scrapValue, location, falling);

            return spawnedItem;
        }

        [ClientRpc]
        private void SpawnScrapClientRpc(NetworkObjectReference itemRef, int value, Vector3 location, bool falling)
        {
            if (!IsServer)
            {
                StartCoroutine(WaitForItemToSpawnOnClient(itemRef, value, location, falling));
            }
        }

        private IEnumerator SetObjectToHitGroundSFX(GrabbableObject gObject)
        {
            yield return new WaitForEndOfFrame();
            gObject.reachedFloorTarget = false;
            gObject.hasHitGround = false;
            gObject.fallTime = 0.0f;
        }

        private IEnumerator WaitForItemToSpawnOnClient(NetworkObjectReference netObjectRef, int value, Vector3 location, bool falling)
        {
            float startTime = Time.realtimeSinceStartup;
            NetworkObject? netObject = null;
            while (Time.realtimeSinceStartup - startTime < 8.0f && !netObjectRef.TryGet(out netObject))
            {
                yield return new WaitForSeconds(0.03f);
            }

            if (netObject == null)
            {
                TobogangMod.Logger.LogError("Failed to spawn scrap: No network object found");
            }
            else
            {
                yield return new WaitForEndOfFrame();
                GrabbableObject grabbable = netObject.GetComponent<GrabbableObject>();
                RoundManager.Instance.totalScrapValueInLevel += grabbable.scrapValue;
                grabbable.SetScrapValue(value);

                if (falling)
                {
                    grabbable.startFallingPosition = location;
                    grabbable.fallTime = 0f;
                    grabbable.hasHitGround = false;
                    grabbable.reachedFloorTarget = false;
                }
                else
                {
                    grabbable.fallTime = 1f;
                    grabbable.hasHitGround = true;
                    grabbable.reachedFloorTarget = true;
                }
            }
        }

        public void PlayAlarmSound(AudioClip clip, bool hearInside = false, float range = 1000f)
        {
            _alarmAudio.maxDistance = hearInside ? 1000f : range;

            if (hearInside)
            {
                _alarmAudio.rolloffMode = AudioRolloffMode.Custom;
                AnimationCurve distanceCurve = new AnimationCurve(new[] { new Keyframe(0, 1), new Keyframe(0.1f, 0.1f), new Keyframe(1, 0) });
                _alarmAudio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, distanceCurve);
            }
            else
            {
                _alarmAudio.rolloffMode = AudioRolloffMode.Linear;
            }

            _alarmAudio.PlayOneShot(clip);
        }

        public void StartNuke()
        {
            PlayAlarmSound(TobogangMod.NukeAlarmClip, true);

            StartCoroutine(Nuke());
        }

        private IEnumerator Nuke()
        {
            yield return new WaitForSeconds(TobogangMod.NukeAlarmClip.length);

            foreach (var enemy in GameObject.FindObjectsOfType<EnemyAI>())
            {
                Landmine.SpawnExplosion(enemy.transform.position, true, 4f, 6f, overridePrefab: _explosionPrefab);
                yield return new WaitForSeconds(0.2f);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerDiscoServerRpc(NetworkObjectReference playerRef, bool isDisco)
        {
            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            if (isDisco)
            {
                DiscoPlayers.Add(playerRef.NetworkObjectId);
            }
            else
            {
                DiscoPlayers.Remove(playerRef.NetworkObjectId);
            }

            SetPlayerDiscoClientRpc(playerRef, DiscoPlayers.ToArray());

            if (isDisco)
            {
                StartCoroutine(WaitForEndOfRoundAndStopDisco(playerNet.gameObject));
            }
        }

        [ClientRpc]
        private void SetPlayerDiscoClientRpc(NetworkObjectReference playerRef, ulong[] allDiscoPlayers)
        {
            DiscoPlayers.Clear();
            foreach (var discoPlayer in allDiscoPlayers)
            {
                DiscoPlayers.Add(discoPlayer);
            }

            if (!playerRef.TryGet(out var playerNet))
            {
                return;
            }

            var player = playerNet.gameObject.GetComponent<PlayerControllerB>();
            var parent = player.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/UpperSpinePoint");

            if (allDiscoPlayers.Contains(playerNet.NetworkObjectId))
            {
                var discoContainer = GameObject.Instantiate(_discoPrefab, Vector3.zero, Quaternion.identity);

                if (discoContainer == null)
                {
                    return;
                }

                discoContainer.transform.SetParent(parent);
                discoContainer.transform.rotation = Quaternion.Euler(84.2214f, 180.8894f, 358.59f);
                discoContainer.transform.localPosition = new Vector3(0f, 0.3959f, 0f);
                var discoBall = discoContainer.transform.Find("AnimContainer/DiscoBall");
                discoBall.rotation = Quaternion.identity;
                discoBall.localPosition = Vector3.zero;
                discoBall.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                discoContainer.SetActive(true);
            }
            else
            {
                StartCoroutine(WaitAndDestroyDisco(parent));
            }
        }

        private IEnumerator WaitForEndOfRoundAndStopDisco(GameObject player)
        {
            while (!StartOfRound.Instance.inShipPhase)
            {
                yield return new WaitForSeconds(0.1f);
            }

            TobogangMod.Logger.LogDebug($"Stopping disco for {player.GetComponent<PlayerControllerB>().playerUsername}");
            SetPlayerDiscoServerRpc(player.GetComponent<NetworkObject>(), false);
        }

        private IEnumerator WaitAndDestroyDisco(Transform parent)
        {
            yield return new WaitForSeconds(0.3f);

            Destroy(parent.gameObject.GetComponentInChildren<CozyLights>().transform.parent.gameObject);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SwapAllPlayersServerRpc()
        {
            var allPlayers = StartOfRound.Instance.allPlayerScripts.ToList().FindAll(p => p.isPlayerControlled).OrderBy(_ => Random.Range(0f, 1f)).ToList();
            var allPositions = allPlayers.ConvertAll(p => p.transform.position);
            var lastTransform = allPositions.Last();
            allPositions.RemoveAt(allPositions.Count - 1);
            allPositions.Insert(0, lastTransform);

            SwapAllPlayersClientRpc(allPlayers.ConvertAll(p => new NetworkObjectReference(p.NetworkObject)).ToArray(), allPositions.ToArray());
        }

        [ClientRpc]
        private void SwapAllPlayersClientRpc(NetworkObjectReference[] allPlayersRefs, Vector3[] allPositions)
        {
            List<PlayerControllerB> allPlayers = [];

            foreach (var playerRef in allPlayersRefs)
            {
                if (!playerRef.TryGet(out var playerNet))
                {
                    return;
                }

                allPlayers.Add(playerNet.gameObject.GetComponent<PlayerControllerB>());
            }

            for (int i = 0; i < allPlayers.Count; i++)
            {
                TeleportPlayer(allPlayers[i], allPositions[i]);
            }
        }

        public void TeleportPlayer(PlayerControllerB player, Vector3 position)
        {
            player.TeleportPlayer(position);
            player.beamOutParticle.Play();
            player.movementAudio.PlayOneShot(TobogangMod.ShipTeleporterBeamClip);

            if (player == StartOfRound.Instance.localPlayerController)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetSunExplodingServerRpc()
        {
            SetSunExplodingClientRpc();
        }

        [ClientRpc]
        private void SetSunExplodingClientRpc()
        {
            StartCoroutine(SunExplosion());
        }

        private IEnumerator SunExplosion()
        {
            var oldVolume = HUDManager.Instance.UIAudio.volume;

            HUDManager.Instance.UIAudio.volume = 1f;
            HUDManager.Instance.UIAudio.PlayOneShot(TobogangMod.SunExplosionClip);
            IsSunExploding = true;

            yield return new WaitForSeconds(17.5f);

            StartOfRound.Instance.localPlayerController.KillPlayer(Vector3.zero, true, CauseOfDeath.Burning);

            foreach (var enemy in FindObjectsOfType<EnemyAI>())
            {
                enemy.KillEnemy();
            }

            yield return new WaitForSeconds(12.5f);

            var elapsedTime = 0f;

            while (elapsedTime < 5f)
            {
                HUDManager.Instance.UIAudio.volume = Mathf.Lerp(1f, 0f, elapsedTime / 5f);
                elapsedTime += Time.deltaTime;

                yield return null;
            }

            HUDManager.Instance.UIAudio.Stop(true);
            HUDManager.Instance.UIAudio.volume = oldVolume;

            IsSunExploding = false;
            _currentSunExplosionTime = 0f;
            CurrentSunIntensity = 0f;
            TimeOfDay.Instance.sunAnimator.enabled = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ArmageddonServerRpc()
        {
            StartCoroutine(ArmageddonExplosions());
            ArmageddonClientRpc();
        }

        [ClientRpc]
        private void ArmageddonClientRpc()
        {
            StartCoroutine(ArmageddonSiren());
        }

        private IEnumerator ArmageddonSiren()
        {
            while (!StartOfRound.Instance.inShipPhase)
            {
                PlayAlarmSound(TobogangMod.ArmageddonSirenClip, false, ARMAGEDDON_RANGE + 10f);

                yield return new WaitForSeconds(TobogangMod.ArmageddonSirenClip.length);
            }
        }

        private IEnumerator ArmageddonExplosions()
        {
            if (!IsServer)
            {
                yield break;
            }

            yield return new WaitForSeconds(TobogangMod.ArmageddonSirenClip.length * 2f);

            while (!StartOfRound.Instance.inShipPhase)
            {
                var point = GetRandomPointAroundShip(ARMAGEDDON_RANGE);

                if (point.HasValue)
                {
                    SpawnArmageddonExplosionClientRpc(point.Value);
                }

                yield return new WaitForSeconds(0.25f);
            }
        }

        [ClientRpc]
        private void SpawnArmageddonExplosionClientRpc(Vector3 location)
        {
            Landmine.SpawnExplosion(location, true, 6f, 7f, overridePrefab:_explosionPrefab);
        }

        public Vector3? GetRandomPointAroundShip(float radius)
        {
            var center = StartOfRound.Instance.middleOfShipNode.position;

            Vector3? pos = null;
            int tries = 0, maxTries = 100;

            while (tries < maxTries && (!pos.HasValue || StartOfRound.Instance.shipBounds.bounds.Contains(pos.Value)))
            {
                Vector3 randomDirection = Random.insideUnitSphere * radius;
                randomDirection += center;

                if (NavMesh.SamplePosition(randomDirection, out var hit, radius, NavMesh.AllAreas))
                {
                    pos = hit.position;
                }

                tries++;
            }

            return pos;
        }
    }
}
