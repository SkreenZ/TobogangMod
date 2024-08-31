using GameNetcodeStuff;
using LCSoundTool;
using Unity.Netcode;
using UnityEngine;

namespace TobogangMod.Scripts
{
    internal class CramptesManager : NetworkBehaviour
    {
        public static CramptesManager Instance { get; private set; } = null!;
        public static GameObject NetworkPrefab { get; private set; } = null!;
        public PlayerControllerB? CurrentCramptesPlayer { get; private set; }

#if DEBUG
        private static readonly float CRAMPTES_CHANCE_ON_DAMAGE = 1f;
        private static readonly float CRAMPTES_CHANCE_ON_ITEM_IN_SHIP = 1f;
        private static readonly float PROBA_INCREASE = 0.01f;
#else
        private static readonly float CRAMPTES_CHANCE_ON_DAMAGE = 0.05f;
        private static readonly float CRAMPTES_CHANCE_ON_ITEM_IN_SHIP = 0.025f;
        private static readonly float PROBA_INCREASE = 0.00001f; // Average 395s
#endif

        private float _timeUntilIncrease = 1f;
        private float _currentProba = PROBA_INCREASE;
        private bool _isProccing = false;
        private float _clipTimer = 0f;

        public static void Init()
        {
            NetworkPrefab = LethalLib.Modules.NetworkPrefabs.CloneNetworkPrefab(TobogangMod.NetworkPrefab, "CramptesManager");
            NetworkPrefab.AddComponent<CramptesManager>();
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                TobogangMod.Logger.LogError("Multiple CramptesManager spawned!");
            }
        }

        void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (_isProccing)
            {
                if (CurrentCramptesPlayer == null)
                {
                    _isProccing = false;
                    return;
                }

                _clipTimer -= Time.deltaTime;

                if (_clipTimer <= 0f)
                {
                    FinishProc();
                }

                return;
            }

            if (CurrentCramptesPlayer == null || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving || CurrentCramptesPlayer.isPlayerDead)
            {
                return;
            }

            _timeUntilIncrease -= Time.deltaTime;

            if (_timeUntilIncrease > 0f)
            {
                return;
            }

            _timeUntilIncrease = 1f;

            if (Random.Range(0f, 1f) <= _currentProba)
            {
                ProcClientRpc(Random.RandomRangeInt(0, RandomSound.Sounds.Count));
                return;
            }

            _currentProba += PROBA_INCREASE;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TryGiveCramptesOnDamageServerRpc(ulong playerId)
        {
            if (Random.Range(0f, 1f) <= CRAMPTES_CHANCE_ON_DAMAGE)
            {
                SetCramptesPlayerClientRpc(playerId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TryLoseCramptesServerRpc()
        {
            if (Random.Range(0f, 1f) <= CRAMPTES_CHANCE_ON_ITEM_IN_SHIP)
            {
                SetCramptesPlayerClientRpc(0);
            }
        }

        [ClientRpc]
        void SetCramptesPlayerClientRpc(ulong playerNetId, bool announce = true)
        {
            if (CurrentCramptesPlayer != null && CurrentCramptesPlayer.NetworkObjectId == playerNetId)
            {
                return;
            }

            string lastCramptesPlayer = CurrentCramptesPlayer != null ? CurrentCramptesPlayer.playerUsername : "";

            CurrentCramptesPlayer = playerNetId == 0 ? null : GetPlayer(playerNetId);
            _currentProba = PROBA_INCREASE;

            if (CurrentCramptesPlayer != null)
            {
                TobogangMod.Logger.LogDebug("Given cramptés to " + CurrentCramptesPlayer.playerUsername);

                if (announce)
                {
                    HUDManager.Instance.DisplayGlobalNotification($"{CurrentCramptesPlayer.playerUsername} a choppe les cramptes");
                }
            }
            else if (lastCramptesPlayer != "")
            {
                TobogangMod.Logger.LogDebug("Removed cramptés from " + lastCramptesPlayer);

                if (announce)
                {
                    HUDManager.Instance.DisplayGlobalNotification($"{lastCramptesPlayer} a perdu les cramptes");
                }
            }
        }

        [ClientRpc]
        void ProcClientRpc(int soundIndex)
        {
            TobogangMod.Logger.LogDebug("Proc cramptés for " + CurrentCramptesPlayer.playerUsername);

            _isProccing = true;

            AudioClip clip = SoundTool.GetAudioClip("TobogangMod/sounds", RandomSound.Sounds[soundIndex]);
            CurrentCramptesPlayer.GetComponent<RandomSound>().AudioSource.PlayOneShot(clip);
            _clipTimer = clip.length;
        }

        [ClientRpc]
        void AddNoteToCramptesPlayerClientRpc(string note)
        {
            if (CurrentCramptesPlayer == null)
            {
                return;
            }

            StartOfRound.Instance.gameStats.allPlayerStats[CurrentCramptesPlayer.playerClientId].playerNotes.Add(note);
        }

        void FinishProc()
        {
            _isProccing = false;

            if (CurrentCramptesPlayer == null)
            {
                return;
            }

            FinishProcClientRpc(CurrentCramptesPlayer.NetworkObject);
            AddNoteToCramptesPlayerClientRpc("Cramptes time");
            CoinguesManager.Instance.RemoveCoinguesServerRpc(CurrentCramptesPlayer.NetworkObject, CoinguesManager.CRAMPTES_PROC_MALUS);
            SetCramptesPlayerClientRpc(0, false);
        }

        [ClientRpc]
        void FinishProcClientRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out var player))
            {
                return;
            }

            Landmine.SpawnExplosion(player.transform.position + Vector3.up, true, 5.7f, 6f);
        }

        static PlayerControllerB GetPlayer(ulong playerNetId)
        {
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerNetId].GetComponent<PlayerControllerB>();
        }
    }
}
